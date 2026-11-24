using System;
using System.Collections.Generic;
using System.Text;

namespace SudokuEngine
{
    public sealed partial class HumanSolver
    {
        // ---------- XY-Wing ----------

        SolveStep FindXYWing(Board b)
        {
            for (int p = 0; p < 81; p++)
            {
                if (b.Val[p] != 0 || Geo.Pop(b.Cand[p]) != 2) continue;
                int pm = b.Cand[p];
                var wings = new List<int>();
                int[] pr = Geo.Peers[p];
                for (int k = 0; k < pr.Length; k++)
                {
                    int w = pr[k];
                    if (b.Val[w] == 0 && Geo.Pop(b.Cand[w]) == 2 && Geo.Pop(b.Cand[w] & pm) == 1) wings.Add(w);
                }
                for (int i = 0; i < wings.Count; i++)
                {
                    for (int j = i + 1; j < wings.Count; j++)
                    {
                        int wa = wings[i];
                        int wb = wings[j];
                        int ma = b.Cand[wa];
                        int mb = b.Cand[wb];
                        if ((ma & pm) == (mb & pm)) continue;
                        int z = ma & mb;
                        if (Geo.Pop(z) != 1 || (z & pm) != 0) continue;
                        int zd = Geo.LowTable[z];
                        var s = new SolveStep();
                        s.Technique = Technique.XYWing;
                        for (int t = 0; t < 81; t++)
                        {
                            if (b.Val[t] == 0 && (b.Cand[t] & z) != 0 && t != wa && t != wb && t != p
                                && Geo.Sees(wa, t) && Geo.Sees(wb, t))
                            {
                                s.Eliminations.Add(new CellDigit(t, zd));
                            }
                        }
                        if (s.Eliminations.Count == 0) continue;
                        s.Cells.Add(p);
                        s.Cells.Add(wa);
                        s.Cells.Add(wb);
                        s.Title = T(Technique.XYWing);
                        s.Reason = "ось " + Geo.RC(p) + " {" + Geo.Digits(pm) + "}, крылья " + Geo.RC(wa) + " {" + Geo.Digits(ma)
                            + "} и " + Geo.RC(wb) + " {" + Geo.Digits(mb) + "}: что бы ни стояло в оси, одно из крыльев окажется "
                            + zd + ", поэтому клетки, видящие оба крыла, не могут быть " + zd;
                        return s;
                    }
                }
            }
            return null;
        }

        // ---------- XYZ-Wing ----------

        SolveStep FindXYZWing(Board b)
        {
            for (int p = 0; p < 81; p++)
            {
                if (b.Val[p] != 0 || Geo.Pop(b.Cand[p]) != 3) continue;
                int pm = b.Cand[p];
                var wings = new List<int>();
                int[] pr = Geo.Peers[p];
                for (int k = 0; k < pr.Length; k++)
                {
                    int w = pr[k];
                    if (b.Val[w] == 0 && Geo.Pop(b.Cand[w]) == 2 && (b.Cand[w] & ~pm) == 0) wings.Add(w);
                }
                for (int i = 0; i < wings.Count; i++)
                {
                    for (int j = i + 1; j < wings.Count; j++)
                    {
                        int wa = wings[i];
                        int wb = wings[j];
                        int ma = b.Cand[wa];
                        int mb = b.Cand[wb];
                        if (ma == mb || (ma | mb) != pm) continue;
                        int z = ma & mb;
                        if (Geo.Pop(z) != 1) continue;
                        int zd = Geo.LowTable[z];
                        var s = new SolveStep();
                        s.Technique = Technique.XYZWing;
                        for (int t = 0; t < 81; t++)
                        {
                            if (b.Val[t] == 0 && (b.Cand[t] & z) != 0 && t != p && t != wa && t != wb
                                && Geo.Sees(p, t) && Geo.Sees(wa, t) && Geo.Sees(wb, t))
                            {
                                s.Eliminations.Add(new CellDigit(t, zd));
                            }
                        }
                        if (s.Eliminations.Count == 0) continue;
                        s.Cells.Add(p);
                        s.Cells.Add(wa);
                        s.Cells.Add(wb);
                        s.Title = T(Technique.XYZWing);
                        s.Reason = "ось " + Geo.RC(p) + " {" + Geo.Digits(pm) + "}, крылья " + Geo.RC(wa) + " {" + Geo.Digits(ma)
                            + "} и " + Geo.RC(wb) + " {" + Geo.Digits(mb) + "}: цифра " + zd
                            + " обязательно окажется в оси или в одном из крыльев, поэтому клетки, видящие все три клетки, не могут быть " + zd;
                        return s;
                    }
                }
            }
            return null;
        }

        // ---------- простая раскраска ----------

        SolveStep FindColoring(Board b)
        {
            var adj = new List<int>[81];
            var color = new int[81];
            for (int d = 1; d <= 9; d++)
            {
                for (int i = 0; i < 81; i++)
                {
                    adj[i] = null;
                    color[i] = -1;
                }
                // сильные связи: в группе цифра d возможна ровно в двух клетках
                for (int u = 0; u < 27; u++)
                {
                    int cnt = 0;
                    int p1 = -1;
                    int p2 = -1;
                    int[] unit = Geo.Units[u];
                    for (int k = 0; k < 9; k++)
                    {
                        int c = unit[k];
                        if (!b.Has(c, d)) continue;
                        cnt++;
                        if (p1 < 0) p1 = c; else p2 = c;
                    }
                    if (cnt != 2) continue;
                    if (adj[p1] == null) adj[p1] = new List<int>();
                    if (adj[p2] == null) adj[p2] = new List<int>();
                    adj[p1].Add(p2);
                    adj[p2].Add(p1);
                }
                for (int start = 0; start < 81; start++)
                {
                    if (adj[start] == null || color[start] >= 0) continue;
                    var comp0 = new List<int>();
                    var comp1 = new List<int>();
                    var stack = new Stack<int>();
                    color[start] = 0;
                    stack.Push(start);
                    while (stack.Count > 0)
                    {
                        int x = stack.Pop();
                        if (color[x] == 0) comp0.Add(x); else comp1.Add(x);
                        List<int> nbs = adj[x];
                        for (int q = 0; q < nbs.Count; q++)
                        {
                            int y = nbs[q];
                            if (color[y] < 0)
                            {
                                color[y] = 1 - color[x];
                                stack.Push(y);
                            }
                        }
                    }
                    comp0.Sort();
                    comp1.Sort();

                    // правило 2 (Color Wrap): две клетки одного цвета видят друг друга -> этот цвет ложный
                    for (int col = 0; col < 2; col++)
                    {
                        List<int> comp = col == 0 ? comp0 : comp1;
                        bool wrap = false;
                        for (int i1 = 0; i1 < comp.Count && !wrap; i1++)
                        {
                            for (int i2 = i1 + 1; i2 < comp.Count; i2++)
                            {
                                if (Geo.Sees(comp[i1], comp[i2])) { wrap = true; break; }
                            }
                        }
                        if (!wrap) continue;
                        var sw = new SolveStep();
                        sw.Technique = Technique.SimpleColoring;
                        for (int i3 = 0; i3 < comp.Count; i3++) sw.Eliminations.Add(new CellDigit(comp[i3], d));
                        sw.Cells.AddRange(comp0);
                        sw.Cells.AddRange(comp1);
                        sw.Title = "Простая раскраска: цветовое противоречие (Simple Coloring)";
                        sw.Reason = "цифра " + d + ": клетки цепочки раскрашены в два цвета, а две клетки одного цвета видят друг друга — "
                            + "этот цвет ложен, и цифра " + d + " убирается из всех его клеток";
                        return sw;
                    }

                    // правило 4 (Color Trap): клетка видит оба цвета -> цифры d в ней быть не может
                    var st = new SolveStep();
                    st.Technique = Technique.SimpleColoring;
                    for (int t = 0; t < 81; t++)
                    {
                        if (!b.Has(t, d) || comp0.Contains(t) || comp1.Contains(t)) continue;
                        bool seesA = false;
                        bool seesB = false;
                        for (int i4 = 0; i4 < comp0.Count && !seesA; i4++)
                        {
                            if (Geo.Sees(t, comp0[i4])) seesA = true;
                        }
                        for (int i5 = 0; i5 < comp1.Count && !seesB; i5++)
                        {
                            if (Geo.Sees(t, comp1[i5])) seesB = true;
                        }
                        if (seesA && seesB) st.Eliminations.Add(new CellDigit(t, d));
                    }
                    if (st.Eliminations.Count > 0)
                    {
                        st.Cells.AddRange(comp0);
                        st.Cells.AddRange(comp1);
                        st.Title = "Простая раскраска: клетка видит оба цвета (Simple Coloring)";
                        st.Reason = "цифра " + d + ": одна из двух раскрасок цепочки истинна, а эти клетки видят клетки обоих цветов, поэтому цифры "
                            + d + " в них быть не может";
                        return st;
                    }
                }
            }
            return null;
        }

        // ---------- уникальный прямоугольник (типы 1, 2, 4) ----------

        SolveStep FindUniqueRectangle(Board b)
        {
            int[] cells = new int[4];
            int[] masks = new int[4];
            for (int r1 = 0; r1 < 9; r1++)
            {
                for (int r2 = r1 + 1; r2 < 9; r2++)
                {
                    for (int c1 = 0; c1 < 9; c1++)
                    {
                        for (int c2 = c1 + 1; c2 < 9; c2++)
                        {
                            cells[0] = r1 * 9 + c1;
                            cells[1] = r1 * 9 + c2;
                            cells[2] = r2 * 9 + c1;
                            cells[3] = r2 * 9 + c2;
                            int boxBits = 0;
                            bool anySolved = false;
                            for (int k = 0; k < 4; k++)
                            {
                                boxBits |= 1 << Geo.BoxOf(cells[k]);
                                if (b.Val[cells[k]] != 0) anySolved = true;
                                masks[k] = b.Cand[cells[k]];
                            }
                            if (anySolved || Geo.Pop(boxBits) != 2) continue;
                            int common = masks[0] & masks[1] & masks[2] & masks[3];
                            if (Geo.Pop(common) < 2) continue;
                            for (int a = 1; a <= 9; a++)
                            {
                                if ((common & Geo.Bit[a]) == 0) continue;
                                for (int e = a + 1; e <= 9; e++)
                                {
                                    if ((common & Geo.Bit[e]) == 0) continue;
                                    SolveStep s = TryRectangle(b, cells, masks, a, e);
                                    if (s != null) return s;
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        SolveStep TryRectangle(Board b, int[] cells, int[] masks, int a, int e)
        {
            int pm = Geo.Bit[a] | Geo.Bit[e];
            int[] floor = new int[4];
            int[] roof = new int[4];
            int nf = 0;
            int nr = 0;
            for (int k = 0; k < 4; k++)
            {
                if (masks[k] == pm) floor[nf++] = cells[k];
                else roof[nr++] = cells[k];
            }
            string rect = Geo.RC(cells[0]) + ", " + Geo.RC(cells[1]) + ", " + Geo.RC(cells[2]) + ", " + Geo.RC(cells[3]);
            string pair = a + " и " + e;

            if (nf == 3)
            {
                var s1 = new SolveStep();
                s1.Technique = Technique.UniqueRectangle;
                s1.Eliminations.Add(new CellDigit(roof[0], a));
                s1.Eliminations.Add(new CellDigit(roof[0], e));
                s1.Cells.AddRange(cells);
                s1.Title = "Уникальный прямоугольник, тип 1 (Unique Rectangle)";
                s1.Reason = "в прямоугольнике " + rect + " три клетки содержат только цифры " + pair
                    + "; если бы четвёртая клетка (" + Geo.RC(roof[0]) + ") тоже осталась с ними, у головоломки было бы два решения, поэтому из неё убираются " + pair;
                return s1;
            }
            if (nf != 2) return null;

            int roofA = roof[0];
            int roofB = roof[1];
            int xa = b.Cand[roofA] & ~pm;
            int xb = b.Cand[roofB] & ~pm;

            // тип 2: обе «крышные» клетки содержат одну и ту же лишнюю цифру
            if (xa == xb && Geo.Pop(xa) == 1)
            {
                int z = Geo.LowTable[xa];
                var s2 = new SolveStep();
                s2.Technique = Technique.UniqueRectangle;
                for (int t = 0; t < 81; t++)
                {
                    if (Array.IndexOf(cells, t) >= 0) continue;
                    if (b.Val[t] == 0 && (b.Cand[t] & xa) != 0 && Geo.Sees(roofA, t) && Geo.Sees(roofB, t))
                    {
                        s2.Eliminations.Add(new CellDigit(t, z));
                    }
                }
                if (s2.Eliminations.Count > 0)
                {
                    s2.Cells.AddRange(cells);
                    s2.Title = "Уникальный прямоугольник, тип 2 (Unique Rectangle)";
                    s2.Reason = "в прямоугольнике " + rect + " две клетки содержат только цифры " + pair + ", а клетки " + Geo.RC(roofA) + " и " + Geo.RC(roofB)
                        + " ещё и цифру " + z + ": одна из них обязательно " + z + " (иначе два решения), поэтому клетки, видящие обе, не могут быть " + z;
                    return s2;
                }
            }

            // тип 4: одна из цифр пары образует «сильную связь» в группе крышных клеток
            for (int pass = 0; pass < 2; pass++)
            {
                int strong = pass == 0 ? a : e;
                int other = pass == 0 ? e : a;
                int[] unitsA = Geo.UnitsOf[roofA];
                for (int k = 0; k < 3; k++)
                {
                    int u = unitsA[k];
                    if (Array.IndexOf(Geo.UnitsOf[roofB], u) < 0) continue;
                    int cnt = 0;
                    bool hasA = false;
                    bool hasB = false;
                    int[] unit = Geo.Units[u];
                    for (int q = 0; q < 9; q++)
                    {
                        int c = unit[q];
                        if (!b.Has(c, strong)) continue;
                        cnt++;
                        if (c == roofA) hasA = true;
                        if (c == roofB) hasB = true;
                    }
                    if (cnt != 2 || !hasA || !hasB) continue;
                    var s4 = new SolveStep();
                    s4.Technique = Technique.UniqueRectangle;
                    s4.Eliminations.Add(new CellDigit(roofA, other));
                    s4.Eliminations.Add(new CellDigit(roofB, other));
                    s4.Cells.AddRange(cells);
                    s4.Title = "Уникальный прямоугольник, тип 4 (Unique Rectangle)";
                    s4.Reason = "в прямоугольнике " + rect + " цифра " + strong + " " + Geo.UnitIn(u) + " возможна только в клетках " + Geo.RC(roofA) + " и "
                        + Geo.RC(roofB) + "; если бы там встала " + other + ", получился бы «смертельный» прямоугольник с двумя решениями, поэтому " + other + " из них убирается";
                    return s4;
                }
            }
            return null;
        }

        // ---------- XY-цепочки ----------

        SolveStep FindXYChain(Board b, int lo, int hi)
        {
            var bi = new List<int>();
            var isBi = new bool[81];
            for (int i = 0; i < 81; i++)
            {
                if (b.Val[i] == 0 && Geo.Pop(b.Cand[i]) == 2)
                {
                    bi.Add(i);
                    isBi[i] = true;
                }
            }
            if (bi.Count < 3) return null;

            int bestLen = int.MaxValue;
            List<CellDigit> bestEl = null;
            List<int> bestPath = null;
            int bestDigit = 0;
            var parent = new int[810];
            var depth = new int[810];
            var queue = new int[810];

            foreach (int a in bi)
            {
                int ma = b.Cand[a];
                for (int x = 1; x <= 9; x++)
                {
                    if ((ma & Geo.Bit[x]) == 0) continue;
                    // «выходная» цифра: если a не x, то a равна y
                    int y = Geo.LowTable[ma & ~Geo.Bit[x]];
                    Array.Clear(depth, 0, depth.Length);
                    int head = 0;
                    int tail = 0;
                    int start = a * 10 + y;
                    parent[start] = -1;
                    depth[start] = 1;
                    queue[tail++] = start;
                    while (head < tail)
                    {
                        int cur = queue[head++];
                        int cell = cur / 10;
                        int outD = cur % 10;
                        int[] pr = Geo.Peers[cell];
                        for (int k = 0; k < pr.Length; k++)
                        {
                            int nb = pr[k];
                            if (!isBi[nb] || (b.Cand[nb] & Geo.Bit[outD]) == 0) continue;
                            int nout = Geo.LowTable[b.Cand[nb] & ~Geo.Bit[outD]];
                            int ns = nb * 10 + nout;
                            if (depth[ns] != 0) continue;
                            parent[ns] = cur;
                            depth[ns] = depth[cur] + 1;
                            queue[tail++] = ns;
                            int n = depth[ns];
                            if (nout != x || n < lo || n > hi || n >= bestLen) continue;

                            var path = new List<int>();
                            for (int st = ns; st != -1; st = parent[st]) path.Add(st / 10);
                            bool simple = true;
                            for (int i1 = 0; i1 < path.Count && simple; i1++)
                            {
                                for (int i2 = i1 + 1; i2 < path.Count; i2++)
                                {
                                    if (path[i1] == path[i2]) { simple = false; break; }
                                }
                            }
                            if (!simple) continue;

                            var el = new List<CellDigit>();
                            for (int t = 0; t < 81; t++)
                            {
                                if (b.Val[t] == 0 && (b.Cand[t] & Geo.Bit[x]) != 0 && !path.Contains(t)
                                    && Geo.Sees(a, t) && Geo.Sees(nb, t))
                                {
                                    el.Add(new CellDigit(t, x));
                                }
                            }
                            if (el.Count == 0) continue;
                            bestLen = n;
                            bestEl = el;
                            path.Reverse();
                            bestPath = path;
                            bestDigit = x;
                        }
                    }
                }
            }
            if (bestEl == null) return null;

            var s = new SolveStep();
            s.Technique = bestLen <= Model.ShortChain ? Technique.XYChainShort : Technique.XYChainLong;
            s.Eliminations.AddRange(bestEl);
            s.Cells.AddRange(bestPath);
            var sb = new StringBuilder();
            for (int i = 0; i < bestPath.Count; i++)
            {
                if (i > 0) sb.Append(" → ");
                sb.Append(Geo.RC(bestPath[i]));
            }
            s.Title = T(s.Technique);
            s.Reason = "цепочка из " + bestLen + " двухкандидатных клеток " + sb + ": если первая клетка не " + bestDigit + ", то последняя обязательно "
                + bestDigit + ", поэтому клетки, видящие обе крайние, не могут быть " + bestDigit;
            return s;
        }

        // ---------- вывод «по одиночкам» (общий для принудительных цепочек и Nishio) ----------

        /// <summary>
        /// Один «раунд» вывода: находит ВСЕ текущие одиночки (явные и скрытые) и ставит их одновременно.
        /// -1 — противоречие, 0 — одиночек нет, 1 — раунд выполнен.
        /// </summary>
        int PropagateRound(Board b)
        {
            bool any = false;
            for (int i = 0; i < 81; i++) pl[i] = 0;
            for (int i = 0; i < 81; i++)
            {
                if (b.Val[i] != 0) continue;
                int m = b.Cand[i];
                if (m == 0) return -1;
                if ((m & (m - 1)) == 0)
                {
                    pl[i] = Geo.LowTable[m];
                    any = true;
                }
            }
            for (int u = 0; u < 27; u++)
            {
                int[] unit = Geo.Units[u];
                int once = 0;
                int twice = 0;
                int placed = 0;
                for (int k = 0; k < 9; k++)
                {
                    int c = unit[k];
                    if (b.Val[c] != 0)
                    {
                        placed |= Geo.Bit[b.Val[c]];
                        continue;
                    }
                    int cm = b.Cand[c];
                    twice |= once & cm;
                    once |= cm;
                }
                if ((once | placed) != Geo.All) return -1;
                int single = once & ~twice;
                while (single != 0)
                {
                    int d = Geo.LowTable[single];
                    single &= single - 1;
                    int pos = -1;
                    for (int k = 0; k < 9; k++)
                    {
                        int c = unit[k];
                        if (b.Val[c] == 0 && (b.Cand[c] & Geo.Bit[d]) != 0)
                        {
                            pos = c;
                            break;
                        }
                    }
                    if (pl[pos] != 0 && pl[pos] != d) return -1;
                    pl[pos] = d;
                    any = true;
                }
            }
            if (!any) return 0;
            for (int i = 0; i < 81; i++)
            {
                if (pl[i] == 0) continue;
                int[] peers = Geo.Peers[i];
                for (int k = 0; k < peers.Length; k++)
                {
                    if (pl[peers[k]] == pl[i]) return -1;
                }
            }
            for (int i = 0; i < 81; i++)
            {
                if (pl[i] != 0) b.Place(i, pl[i]);
            }
            return 1;
        }

        // ---------- принудительные цепочки ----------

        sealed class ForceSource
        {
            public int Kind;      // 0 — клетка с 2–3 кандидатами, 1 — цифра в группе с 2–3 местами
            public int Index;     // клетка или номер группы
            public int Digit;     // для Kind == 1
            public int[] Cells;
            public int[] Digits;
        }

        static string SourceLabel(ForceSource src, Board b)
        {
            if (src.Kind == 0) return "клетка " + Geo.RC(src.Index) + " {" + Geo.Digits(b.Cand[src.Index]) + "}";
            var sb = new StringBuilder();
            sb.Append("цифра ").Append(src.Digit).Append(' ').Append(Geo.UnitIn(src.Index)).Append(" (клетки ");
            for (int k = 0; k < src.Cells.Length; k++)
            {
                if (k > 0) sb.Append(", ");
                sb.Append(Geo.RC(src.Cells[k]));
            }
            sb.Append(')');
            return sb.ToString();
        }

        /// <summary>
        /// Для клетки с 2–3 кандидатами (или цифры с 2–3 местами в группе) проверяем каждый вариант цепочкой простых выводов.
        /// Всё, что исчезает из кандидатов при любом варианте, можно убрать сразу. Длина цепочки — число раундов вывода.
        /// </summary>
        SolveStep FindForcing(Board b, int maxRounds)
        {
            var sources = new List<ForceSource>();
            for (int i = 0; i < 81; i++)
            {
                if (b.Val[i] != 0) continue;
                int n = Geo.Pop(b.Cand[i]);
                if (n < 2 || n > 3) continue;
                var fs = new ForceSource();
                fs.Kind = 0;
                fs.Index = i;
                fs.Cells = new int[n];
                fs.Digits = new int[n];
                int q = 0;
                for (int d = 1; d <= 9; d++)
                {
                    if ((b.Cand[i] & Geo.Bit[d]) == 0) continue;
                    fs.Cells[q] = i;
                    fs.Digits[q] = d;
                    q++;
                }
                sources.Add(fs);
            }
            for (int u = 0; u < 27; u++)
            {
                int[] unit = Geo.Units[u];
                for (int d = 1; d <= 9; d++)
                {
                    int n = 0;
                    for (int k = 0; k < 9; k++)
                    {
                        if (b.Has(unit[k], d)) n++;
                    }
                    if (n < 2 || n > 3) continue;
                    var fs = new ForceSource();
                    fs.Kind = 1;
                    fs.Index = u;
                    fs.Digit = d;
                    fs.Cells = new int[n];
                    fs.Digits = new int[n];
                    int q = 0;
                    for (int k = 0; k < 9; k++)
                    {
                        if (!b.Has(unit[k], d)) continue;
                        fs.Cells[q] = unit[k];
                        fs.Digits[q] = d;
                        q++;
                    }
                    sources.Add(fs);
                }
            }

            int bestRounds = int.MaxValue;
            List<CellDigit> bestEl = null;
            ForceSource bestSrc = null;
            for (int si = 0; si < sources.Count; si++)
            {
                ForceSource src = sources[si];
                int nb = src.Cells.Length;
                for (int k = 0; k < nb; k++)
                {
                    branch[k].CopyFrom(b);
                    branch[k].Place(src.Cells[k], src.Digits[k]);
                }
                for (int r = 1; r <= maxRounds && r < bestRounds; r++)
                {
                    int stuck = 0;
                    bool bad = false;
                    for (int k = 0; k < nb; k++)
                    {
                        int o = PropagateRound(branch[k]);
                        if (o < 0)
                        {
                            bad = true;
                            break;
                        }
                        if (o == 0) stuck++;
                    }
                    if (bad) break;
                    List<CellDigit> el = null;
                    for (int j = 0; j < 81; j++)
                    {
                        if (b.Val[j] != 0) continue;
                        int union = 0;
                        for (int k = 0; k < nb; k++)
                        {
                            Board br = branch[k];
                            union |= br.Val[j] == 0 ? br.Cand[j] : Geo.Bit[br.Val[j]];
                        }
                        int gone = b.Cand[j] & ~union;
                        if (gone == 0) continue;
                        if (el == null) el = new List<CellDigit>();
                        for (int d = 1; d <= 9; d++)
                        {
                            if ((gone & Geo.Bit[d]) != 0) el.Add(new CellDigit(j, d));
                        }
                    }
                    if (el != null)
                    {
                        bestRounds = r;
                        bestEl = el;
                        bestSrc = src;
                        break;
                    }
                    if (stuck == nb) break;
                }
                if (bestRounds == 1) break;
            }
            if (bestEl == null) return null;

            var s = new SolveStep();
            s.Technique = bestRounds <= Model.ShortChain ? Technique.ForcingChainShort : Technique.ForcingChainLong;
            s.Eliminations.AddRange(bestEl);
            for (int k = 0; k < bestSrc.Cells.Length; k++) s.Cells.Add(bestSrc.Cells[k]);
            s.Title = T(s.Technique);
            s.Reason = "рассматриваем варианты: " + SourceLabel(bestSrc, b) + ". Каждый вариант цепочкой простых выводов (глубина " + bestRounds
                + ") приводит к одному и тому же результату, поэтому он верен при любом выборе";
            return s;
        }

        // ---------- Nishio (предположение с противоречием) ----------

        SolveStep FindNishio(Board b)
        {
            int bestRounds = int.MaxValue;
            int bestCell = -1;
            int bestDigit = 0;
            for (int i = 0; i < 81; i++)
            {
                if (b.Val[i] != 0 || Geo.Pop(b.Cand[i]) < 2) continue;
                for (int d = 1; d <= 9; d++)
                {
                    if ((b.Cand[i] & Geo.Bit[d]) == 0) continue;
                    probe.CopyFrom(b);
                    probe.Place(i, d);
                    int rounds = 0;
                    bool contradiction = false;
                    while (true)
                    {
                        int o = PropagateRound(probe);
                        if (o < 0)
                        {
                            contradiction = true;
                            break;
                        }
                        if (o == 0) break;
                        rounds++;
                    }
                    if (contradiction && rounds < bestRounds)
                    {
                        bestRounds = rounds;
                        bestCell = i;
                        bestDigit = d;
                    }
                }
            }
            if (bestCell < 0) return null;

            var s = new SolveStep();
            s.Technique = Technique.Nishio;
            s.Eliminations.Add(new CellDigit(bestCell, bestDigit));
            s.Cells.Add(bestCell);
            s.Title = T(Technique.Nishio);
            s.Reason = "предположим, что в клетке " + Geo.RC(bestCell) + " стоит " + bestDigit + ": цепочка простых выводов (глубина " + bestRounds
                + ") приводит к противоречию, значит " + bestDigit + " там невозможна";
            return s;
        }
    }
}
