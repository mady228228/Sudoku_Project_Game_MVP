using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SudokuEngine
{
    public sealed class GeneratedPuzzle
    {
        public int[] Grid;
        public int[] Solution;
        public Rating Rating;
        public List<SolveStep> Steps;
        public Level Target;
        /// <summary>false — за отведённое время точный уровень не найден, возвращён ближайший.</summary>
        public bool ExactMatch;
        public int Attempts;
        public double Seconds;

        public int Clues
        {
            get
            {
                int n = 0;
                for (int i = 0; i < 81; i++)
                {
                    if (Grid[i] != 0) n++;
                }
                return n;
            }
        }
    }

    /// <summary>
    /// Генерация задач заданного уровня.
    /// Very Easy … Expert: «направленное» выкалывание. Берём случайную полную сетку и удаляем цифры в случайном порядке,
    /// оставляя удаление, только если решение остаётся единственным И задача решается приёмами не выше целевого уровня
    /// (и не выходит за него по баллам). Для уровня Medium выбор удаляемой цифры «жадный» по баллам S (см. DigDirected). Затем задача оценивается по модели из работы; если уровень совпал — готово,
    /// иначе пробуем снова.
    /// Extreme: случайное выкалывание почти никогда не даёт такой уровень (в моих тестах около 1 из 1300),
    /// поэтому берутся проверенные сильные головоломки и случайно преобразуются (перестановка цифр, строк, столбцов, транспонирование).
    /// Преобразования не меняют структуру задачи, а итоговая оценка всё равно пересчитывается.
    /// </summary>
    public static class PuzzleGenerator
    {
        // Проверены решателем из этого проекта: по модели из работы дают Extreme (нужны Nishio / перебор).
        static readonly string[] ExtremeSeeds = new string[]
        {
            "8..........36......7..9.2...5...7.......457.....1...3...1....68..85...1..9....4..", // Arto Inkala, 2012
            "1....7.9..3..2...8..96..5....53..9...1..8...26....4...3......1..4......7..7...3..", // AI Escargot
            "1.......2.9.4...5...6...7...5.9.3.......7.......85..4.7.....6...3...9.8...2.....1"  // Easter Monster
        };

        /// <param name="seed">Зерно генератора случайных чисел.</param>
        /// <param name="timeLimitMs">Сколько миллисекунд искать точное попадание в уровень.</param>
        /// <param name="cancelled">Необязательная функция отмены (может быть null).</param>
        public static GeneratedPuzzle Generate(Level target, int seed, int timeLimitMs, Func<bool> cancelled)
        {
            var rng = new Random(seed);
            var sw = Stopwatch.StartNew();
            GeneratedPuzzle best = null;
            int bestDist = int.MaxValue;
            int attempts = 0;

            while (true)
            {
                attempts++;
                int[] grid;
                int[] sol;
                if (target == Level.Extreme) FromSeed(rng, out grid, out sol);
                else DigDirected(target, target == Level.Medium ? 4 : 1, rng, out grid, out sol);

                List<SolveStep> steps = null;
                try
                {
                    steps = new HumanSolver(sol, true).SolveAll(grid);
                }
                catch (InvalidOperationException)
                {
                    steps = null;
                }

                if (steps != null)
                {
                    Rating rt = Model.Compute(steps);
                    int dist = Math.Abs((int)rt.Level - (int)target);
                    if (best == null || dist < bestDist)
                    {
                        bestDist = dist;
                        best = new GeneratedPuzzle();
                        best.Grid = grid;
                        best.Solution = sol;
                        best.Rating = rt;
                        best.Steps = steps;
                        best.Target = target;
                    }
                    if (dist == 0) break;
                }
                if (sw.ElapsedMilliseconds > timeLimitMs) break;
                if (cancelled != null && cancelled()) break;
            }

            if (best == null) throw new InvalidOperationException("Не удалось сгенерировать головоломку");
            best.ExactMatch = bestDist == 0;
            best.Attempts = attempts;
            best.Seconds = sw.Elapsed.TotalSeconds;
            return best;
        }

        // ---------- направленное выкалывание ----------

        /// <summary>
        /// Удаляет цифры из случайной полной сетки. Удаление принимается, только если решение остаётся единственным
        /// и задача решается приёмами не выше cap. При sample &gt; 1 на каждом шаге из нескольких случайных кандидатов
        /// выбирается тот, после которого сумма баллов S максимальна: так «средний» уровень (приёмы вроде тройки)
        /// находится примерно в 10 раз чаще, чем при чисто случайном выкалывании.
        /// </summary>
        static void DigDirected(Level cap, int sample, Random rng, out int[] grid, out int[] solution)
        {
            int[] sol;
            Backtracking.Count(new int[81], 1, out sol, rng);   // случайная полная сетка
            int[] p = (int[])sol.Clone();
            var remaining = new List<int>(Shuffled(81, rng));
            var dead = new List<int>();
            var solver = new HumanSolver(sol, true);
            int[] tmp;
            while (remaining.Count > 0)
            {
                int bestCell = -1;
                int bestScore = -1;
                dead.Clear();
                int look = Math.Min(sample, remaining.Count);
                for (int k = 0; k < look; k++)
                {
                    int cell = remaining[k];
                    int keep = p[cell];
                    p[cell] = 0;
                    int score = -1;
                    if (Backtracking.Count(p, 2, out tmp, null) == 1)
                    {
                        List<SolveStep> steps = solver.TrySolve(p, cap);
                        if (steps != null) score = Model.Compute(steps).Score;
                    }
                    p[cell] = keep;
                    if (score < 0)
                    {
                        dead.Add(cell);
                    }
                    else if (score > bestScore)
                    {
                        bestScore = score;
                        bestCell = cell;
                    }
                }
                for (int k = 0; k < dead.Count; k++) remaining.Remove(dead[k]);
                if (bestCell >= 0)
                {
                    p[bestCell] = 0;
                    remaining.Remove(bestCell);
                }
            }
            grid = p;
            solution = sol;
        }

        // ---------- Extreme: изоморфизмы проверенных задач ----------

        static void FromSeed(Random rng, out int[] grid, out int[] solution)
        {
            int[] seedGrid = PuzzleAnalyzer.ParseGrid(ExtremeSeeds[rng.Next(ExtremeSeeds.Length)]);
            int[] seedSol;
            Backtracking.Count(seedGrid, 1, out seedSol, null);

            int[] digitMap = Shuffled(9, rng);
            int[] rowMap = BuildLineMap(rng);
            int[] colMap = BuildLineMap(rng);
            bool transpose = rng.Next(2) == 1;

            grid = new int[81];
            solution = new int[81];
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    int nr = rowMap[r];
                    int nc = colMap[c];
                    if (transpose)
                    {
                        int tmp = nr;
                        nr = nc;
                        nc = tmp;
                    }
                    int src = r * 9 + c;
                    int dst = nr * 9 + nc;
                    grid[dst] = seedGrid[src] == 0 ? 0 : digitMap[seedGrid[src] - 1] + 1;
                    solution[dst] = digitMap[seedSol[src] - 1] + 1;
                }
            }
        }

        /// <summary>Перестановка линий, не нарушающая структуру судоку: порядок полос и порядок линий внутри каждой полосы.</summary>
        static int[] BuildLineMap(Random rng)
        {
            int[] bandOrder = Shuffled(3, rng);
            int[] map = new int[9];
            for (int band = 0; band < 3; band++)
            {
                int[] inner = Shuffled(3, rng);
                for (int k = 0; k < 3; k++) map[band * 3 + k] = bandOrder[band] * 3 + inner[k];
            }
            return map;
        }

        static int[] Shuffled(int n, Random rng)
        {
            int[] a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = a[i];
                a[i] = a[j];
                a[j] = t;
            }
            return a;
        }
    }
}
