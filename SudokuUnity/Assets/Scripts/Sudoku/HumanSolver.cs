using System;
using System.Collections.Generic;
using System.Text;

namespace SudokuEngine
{
    /// <summary>
    /// «Человеческий» решатель. На каждом шаге применяет самый лёгкий из доступных приёмов
    /// (порядок — по весу первого применения M из таблицы работы, при равенстве — по порядку строк таблицы),
    /// так же как это делают SER и HoDoKu. Получившаяся цепочка шагов и есть «оптимальный путь решения»,
    /// а по ней считается оценка сложности (Model.Compute).
    /// </summary>
    public sealed partial class HumanSolver
    {
        readonly int[] solution;
        readonly bool assumeUnique;
        readonly Technique[] order;
        readonly int[] pl = new int[81];
        readonly Board[] branch = new Board[] { new Board(), new Board(), new Board() };
        readonly Board probe = new Board();

        /// <param name="solution">Известное решение (нужно только для шага «Перебор»).</param>
        /// <param name="assumeUnique">true — решение единственное, можно применять Unique Rectangle.</param>
        public HumanSolver(int[] solution, bool assumeUnique)
        {
            this.solution = solution;
            this.assumeUnique = assumeUnique;
            this.order = BuildOrder();
        }

        static Technique[] BuildOrder()
        {
            var list = new List<Technique>();
            for (int t = 0; t < Model.Table.Length; t++)
            {
                TechniqueInfo info = Model.Table[t];
                if (!info.Implemented || info.Id == Technique.BruteForce) continue;
                list.Add(info.Id);
            }
            // устойчивая сортировка вставками по весу первого применения
            for (int i = 1; i < list.Count; i++)
            {
                Technique x = list[i];
                int j = i - 1;
                while (j >= 0 && Model.Info(list[j]).FirstScore > Model.Info(x).FirstScore)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = x;
            }
            return list.ToArray();
        }

        /// <summary>Полный путь решения (при необходимости с шагами «Перебор»).</summary>
        public List<SolveStep> SolveAll(int[] grid)
        {
            return TrySolve(grid, Level.Extreme);
        }

        /// <summary>
        /// Пытается решить, не используя приёмы выше уровня cap и не выходя за него по баллам.
        /// Возвращает null, если для решения нужно что-то сложнее. Так генератор быстро отбрасывает слишком трудные варианты.
        /// </summary>
        public List<SolveStep> TrySolve(int[] grid, Level cap)
        {
            Board b = Board.FromGrid(grid);
            var steps = new List<SolveStep>();
            var counts = new int[Model.Table.Length];
            int guard = 0;
            while (!b.IsSolved())
            {
                guard++;
                if (guard > 600) throw new InvalidOperationException("Решатель не сходится");
                SolveStep s = NextStep(b, cap);
                if (s == null) return null;
                counts[(int)s.Technique]++;
                if (Model.LevelFromScore(Model.ScoreOf(counts)) > cap) return null;
                s.Number = steps.Count + 1;
                if (!Apply(b, s)) throw new InvalidOperationException("Приём " + s.Technique + " ничего не изменил");
                steps.Add(s);
            }
            return steps;
        }

        /// <summary>Следующий шаг: самый лёгкий применимый приём не выше уровня cap (или null).</summary>
        public SolveStep NextStep(Board b, Level cap)
        {
            for (int k = 0; k < order.Length; k++)
            {
                Technique t = order[k];
                if (Model.Info(t).MinLevel > cap) continue;
                if (t == Technique.UniqueRectangle && !assumeUnique) continue;
                SolveStep s = Run(t, b);
                if (s != null) return s;
            }
            if (cap >= Level.Extreme) return FindBruteForce(b);
            return null;
        }

        public static bool Apply(Board b, SolveStep s)
        {
            bool changed = false;
            for (int i = 0; i < s.Eliminations.Count; i++)
            {
                CellDigit e = s.Eliminations[i];
                if (b.Val[e.Cell] == 0 && (b.Cand[e.Cell] & Geo.Bit[e.Digit]) != 0)
                {
                    b.Cand[e.Cell] &= ~Geo.Bit[e.Digit];
                    changed = true;
                }
            }
            for (int i = 0; i < s.Placements.Count; i++)
            {
                b.Place(s.Placements[i].Cell, s.Placements[i].Digit);
                changed = true;
            }
            return changed;
        }

        SolveStep Run(Technique t, Board b)
        {
            switch (t)
            {
                case Technique.Single: return FindSingle(b);
                case Technique.NakedPair: return FindNaked(b, 2);
                case Technique.HiddenPair: return FindHidden(b, 2);
                case Technique.BoxLine: return FindIntersection(b);
                case Technique.NakedTriple: return FindNaked(b, 3);
                case Technique.HiddenTriple: return FindHidden(b, 3);
                case Technique.SimpleColoring: return FindColoring(b);
                case Technique.XYWing: return FindXYWing(b);
                case Technique.XYChainShort: return FindXYChain(b, 4, Model.ShortChain);
                case Technique.XWing: return FindFish(b, 2);
                case Technique.UniqueRectangle: return FindUniqueRectangle(b);
                case Technique.XYZWing: return FindXYZWing(b);
                case Technique.XYChainLong: return FindXYChain(b, Model.ShortChain + 1, 99);
                case Technique.ForcingChainShort: return FindForcing(b, Model.ShortChain);
                case Technique.Swordfish: return FindFish(b, 3);
                case Technique.ForcingChainLong: return FindForcing(b, 99);
                case Technique.Jellyfish: return FindFish(b, 4);
                case Technique.Nishio: return FindNishio(b);
                default: return null;
            }
        }

        // ---------- вспомогательное ----------

        static string T(Technique t) { return Model.Info(t).Title; }

        /// <summary>Все k-элементные подмножества индексов 0..n-1.</summary>
        static IEnumerable<int[]> Combos(int n, int k)
        {
            if (k > n || k <= 0) yield break;
            int[] idx = new int[k];
            for (int i = 0; i < k; i++) idx[i] = i;
            while (true)
            {
                yield return (int[])idx.Clone();
                int p = k - 1;
                while (p >= 0 && idx[p] == n - k + p) p--;
                if (p < 0) yield break;
                idx[p]++;
                for (int q = p + 1; q < k; q++) idx[q] = idx[q - 1] + 1;
            }
        }

        /// <summary>Номера строк/столбцов из маски (нумерация с 1): «2, 6».</summary>
        static string LineNumbers(int mask)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 9; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(i + 1);
            }
            return sb.ToString();
        }

        // ---------- одиночки ----------

        SolveStep FindSingle(Board b)
        {
            for (int i = 0; i < 81; i++)
            {
                if (b.Val[i] != 0) continue;
                int m = b.Cand[i];
                if (m != 0 && (m & (m - 1)) == 0)
                {
                    int d = Geo.LowTable[m];
                    var s = new SolveStep();
                    s.Technique = Technique.Single;
                    s.Title = "Явный одиночный кандидат (Naked Single)";
                    s.Reason = "в клетке " + Geo.RC(i) + " остался единственный кандидат — цифра " + d;
                    s.Placements.Add(new CellDigit(i, d));
                    s.Cells.Add(i);
                    return s;
                }
            }
            for (int u = 0; u < 27; u++)
            {
                int[] unit = Geo.Units[u];
                int once = 0;
                int twice = 0;
                for (int k = 0; k < 9; k++)
                {
                    int c = unit[k];
                    if (b.Val[c] != 0) continue;
                    int cm = b.Cand[c];
                    twice |= once & cm;
                    once |= cm;
                }
                int single = once & ~twice;
                if (single == 0) continue;
                int digit = Geo.LowTable[single];
                int pos = -1;
                for (int k = 0; k < 9; k++)
                {
                    int c = unit[k];
                    if (b.Has(c, digit)) { pos = c; break; }
                }
                var hs = new SolveStep();
                hs.Technique = Technique.Single;
                hs.Title = "Скрытый одиночный кандидат (Hidden Single)";
                hs.Reason = Geo.UnitIn(u) + " цифра " + digit + " может стоять только в клетке " + Geo.RC(pos);
                hs.Placements.Add(new CellDigit(pos, digit));
                hs.Cells.Add(pos);
                return hs;
            }
            return null;
        }

        // ---------- явные пары / тройки ----------

        SolveStep FindNaked(Board b, int k)
        {
            Technique tech = k == 2 ? Technique.NakedPair : Technique.NakedTriple;
            for (int u = 0; u < 27; u++)
            {
                int[] unit = Geo.Units[u];
                var cells = new List<int>(9);
                for (int x = 0; x < 9; x++)
                {
                    int c = unit[x];
                    if (b.Val[c] != 0) continue;
                    int n = Geo.Pop(b.Cand[c]);
                    if (n >= 2 && n <= k) cells.Add(c);
                }
                if (cells.Count < k) continue;
                foreach (int[] combo in Combos(cells.Count, k))
                {
                    int union = 0;
                    for (int q = 0; q < k; q++) union |= b.Cand[cells[combo[q]]];
                    if (Geo.Pop(union) != k) continue;
                    var s = new SolveStep();
                    s.Technique = tech;
                    for (int q = 0; q < k; q++) s.Cells.Add(cells[combo[q]]);
                    for (int x = 0; x < 9; x++)
                    {
                        int c = unit[x];
                        if (b.Val[c] != 0 || s.Cells.Contains(c)) continue;
                        int hit = b.Cand[c] & union;
                        for (int d = 1; d <= 9; d++)
                        {
                            if ((hit & Geo.Bit[d]) != 0) s.Eliminations.Add(new CellDigit(c, d));
                        }
                    }
                    if (s.Eliminations.Count == 0) continue;
                    s.Title = T(tech);
                    s.Reason = "клетки " + Geo.CellList(s.Cells) + " " + Geo.UnitIn(u) + " содержат только цифры " + Geo.Digits(union)
                        + " — эти цифры займут именно их, поэтому в остальных клетках группы они невозможны";
                    return s;
                }
            }
            return null;
        }

        // ---------- скрытые пары / тройки ----------

        SolveStep FindHidden(Board b, int k)
        {
            Technique tech = k == 2 ? Technique.HiddenPair : Technique.HiddenTriple;
            for (int u = 0; u < 27; u++)
            {
                int[] unit = Geo.Units[u];
                var digs = new List<int>();
                int[] posMask = new int[10];
                for (int d = 1; d <= 9; d++)
                {
                    int pm = 0;
                    int cnt = 0;
                    for (int x = 0; x < 9; x++)
                    {
                        if (b.Has(unit[x], d)) { pm |= 1 << x; cnt++; }
                    }
                    if (cnt >= 2 && cnt <= k)
                    {
                        digs.Add(d);
                        posMask[d] = pm;
                    }
                }
                if (digs.Count < k) continue;
                foreach (int[] combo in Combos(digs.Count, k))
                {
                    int cellsMask = 0;
                    int digMask = 0;
                    for (int q = 0; q < k; q++)
                    {
                        int dg = digs[combo[q]];
                        cellsMask |= posMask[dg];
                        digMask |= Geo.Bit[dg];
                    }
                    if (Geo.Pop(cellsMask) != k) continue;
                    var s = new SolveStep();
                    s.Technique = tech;
                    for (int x = 0; x < 9; x++)
                    {
                        if ((cellsMask & (1 << x)) == 0) continue;
                        int c = unit[x];
                        s.Cells.Add(c);
                        int extra = b.Cand[c] & ~digMask;
                        for (int d = 1; d <= 9; d++)
                        {
                            if ((extra & Geo.Bit[d]) != 0) s.Eliminations.Add(new CellDigit(c, d));
                        }
                    }
                    if (s.Eliminations.Count == 0) continue;
                    s.Title = T(tech);
                    s.Reason = "цифры " + Geo.Digits(digMask) + " " + Geo.UnitIn(u) + " возможны только в клетках " + Geo.CellList(s.Cells)
                        + " — значит, других цифр в этих клетках быть не может";
                    return s;
                }
            }
            return null;
        }

        // ---------- пересечения блок/линия ----------

        SolveStep FindIntersection(Board b)
        {
            // Pointing: цифра в блоке лежит в одной линии -> убираем её из остальной линии
            for (int bx = 0; bx < 9; bx++)
            {
                int[] box = Geo.Boxes[bx];
                for (int d = 1; d <= 9; d++)
                {
                    var cells = new List<int>(9);
                    int rowMask = 0;
                    int colMask = 0;
                    for (int k = 0; k < 9; k++)
                    {
                        int c = box[k];
                        if (!b.Has(c, d)) continue;
                        cells.Add(c);
                        rowMask |= 1 << (c / 9);
                        colMask |= 1 << (c % 9);
                    }
                    if (cells.Count < 2) continue;
                    if (Geo.Pop(rowMask) == 1)
                    {
                        int rowIdx = Geo.LowTable[rowMask] - 1;
                        SolveStep ps = PointingStep(b, Geo.Rows[rowIdx], bx, d, cells, "строке " + (rowIdx + 1));
                        if (ps != null) return ps;
                    }
                    if (Geo.Pop(colMask) == 1)
                    {
                        int colIdx = Geo.LowTable[colMask] - 1;
                        SolveStep cs = PointingStep(b, Geo.Cols[colIdx], bx, d, cells, "столбце " + (colIdx + 1));
                        if (cs != null) return cs;
                    }
                }
            }
            // Box-Line: цифра в линии лежит в одном блоке -> убираем её из остального блока
            for (int ln = 0; ln < 18; ln++)
            {
                int[] line = Geo.Units[ln];
                for (int d = 1; d <= 9; d++)
                {
                    var cells = new List<int>(9);
                    int boxMask = 0;
                    for (int k = 0; k < 9; k++)
                    {
                        int c = line[k];
                        if (!b.Has(c, d)) continue;
                        cells.Add(c);
                        boxMask |= 1 << Geo.BoxOf(c);
                    }
                    if (cells.Count < 2 || Geo.Pop(boxMask) != 1) continue;
                    int boxIdx = Geo.LowTable[boxMask] - 1;
                    var s = new SolveStep();
                    int[] target = Geo.Boxes[boxIdx];
                    for (int k = 0; k < 9; k++)
                    {
                        int c = target[k];
                        if (Array.IndexOf(line, c) < 0 && b.Has(c, d)) s.Eliminations.Add(new CellDigit(c, d));
                    }
                    if (s.Eliminations.Count == 0) continue;
                    s.Technique = Technique.BoxLine;
                    s.Title = "Пересечение линия → блок (Box-Line)";
                    string lineName = ln < 9 ? "строке " + (ln + 1) : "столбце " + (ln - 8);
                    s.Reason = "в " + lineName + " цифра " + d + " возможна только в блоке " + (boxIdx + 1) + " (" + Geo.CellList(cells)
                        + "), поэтому в остальных клетках этого блока её быть не может";
                    s.Cells.AddRange(cells);
                    return s;
                }
            }
            return null;
        }

        SolveStep PointingStep(Board b, int[] line, int bx, int d, List<int> cells, string lineName)
        {
            var s = new SolveStep();
            for (int k = 0; k < 9; k++)
            {
                int c = line[k];
                if (Geo.BoxOf(c) != bx && b.Has(c, d)) s.Eliminations.Add(new CellDigit(c, d));
            }
            if (s.Eliminations.Count == 0) return null;
            s.Technique = Technique.BoxLine;
            s.Title = "Пересечение блок → линия (Pointing)";
            s.Reason = "в блоке " + (bx + 1) + " цифра " + d + " возможна только в " + lineName + " (" + Geo.CellList(cells)
                + "), поэтому вне блока в этой линии её быть не может";
            s.Cells.AddRange(cells);
            return s;
        }

        // ---------- X-Wing / Swordfish / Jellyfish ----------

        SolveStep FindFish(Board b, int k)
        {
            Technique tech = k == 2 ? Technique.XWing : (k == 3 ? Technique.Swordfish : Technique.Jellyfish);
            for (int d = 1; d <= 9; d++)
            {
                for (int orient = 0; orient < 2; orient++)
                {
                    int[][] baseLines = orient == 0 ? Geo.Rows : Geo.Cols;
                    var lineIdx = new List<int>();
                    var coverMasks = new List<int>();
                    for (int li = 0; li < 9; li++)
                    {
                        int cnt = 0;
                        int cover = 0;
                        for (int x = 0; x < 9; x++)
                        {
                            int c = baseLines[li][x];
                            if (!b.Has(c, d)) continue;
                            cnt++;
                            cover |= 1 << (orient == 0 ? c % 9 : c / 9);
                        }
                        if (cnt >= 2 && cnt <= k)
                        {
                            lineIdx.Add(li);
                            coverMasks.Add(cover);
                        }
                    }
                    if (lineIdx.Count < k) continue;
                    foreach (int[] combo in Combos(lineIdx.Count, k))
                    {
                        int cov = 0;
                        int baseMask = 0;
                        for (int q = 0; q < k; q++)
                        {
                            cov |= coverMasks[combo[q]];
                            baseMask |= 1 << lineIdx[combo[q]];
                        }
                        if (Geo.Pop(cov) != k) continue;
                        var s = new SolveStep();
                        s.Technique = tech;
                        for (int cv = 0; cv < 9; cv++)
                        {
                            if ((cov & (1 << cv)) == 0) continue;
                            int[] coverLine = orient == 0 ? Geo.Cols[cv] : Geo.Rows[cv];
                            for (int x = 0; x < 9; x++)
                            {
                                int c = coverLine[x];
                                int bl = orient == 0 ? c / 9 : c % 9;
                                if ((baseMask & (1 << bl)) == 0 && b.Has(c, d)) s.Eliminations.Add(new CellDigit(c, d));
                            }
                        }
                        if (s.Eliminations.Count == 0) continue;
                        for (int q = 0; q < k; q++)
                        {
                            int[] bl2 = baseLines[lineIdx[combo[q]]];
                            for (int x = 0; x < 9; x++)
                            {
                                if (b.Has(bl2[x], d)) s.Cells.Add(bl2[x]);
                            }
                        }
                        s.Title = T(tech);
                        s.Reason = "цифра " + d + ": базовые " + (orient == 0 ? "строки " : "столбцы ") + LineNumbers(baseMask)
                            + ", покрывающие " + (orient == 0 ? "столбцы " : "строки ") + LineNumbers(cov)
                            + " — в базовых линиях цифра " + d + " стоит только в покрывающих, поэтому в остальных клетках покрывающих линий её быть не может";
                        return s;
                    }
                }
            }
            return null;
        }

        // ---------- перебор (последний рубеж) ----------

        SolveStep FindBruteForce(Board b)
        {
            if (solution == null) throw new InvalidOperationException("Для шага перебора нужно известное решение");
            int best = -1;
            int bestN = 10;
            for (int i = 0; i < 81; i++)
            {
                if (b.Val[i] != 0) continue;
                int n = Geo.Pop(b.Cand[i]);
                if (n < bestN)
                {
                    bestN = n;
                    best = i;
                }
            }
            if (best < 0) return null;
            int d = solution[best];
            var s = new SolveStep();
            s.Technique = Technique.BruteForce;
            s.Title = T(Technique.BruteForce);
            s.Reason = "логические приёмы закончились: в клетке " + Geo.RC(best) + " (кандидаты: " + Geo.Digits(b.Cand[best])
                + ") пробуем цифру " + d + " — она подтверждена перебором";
            s.Placements.Add(new CellDigit(best, d));
            s.Cells.Add(best);
            return s;
        }
    }
}
