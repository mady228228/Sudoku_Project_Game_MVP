using System;
using System.Collections.Generic;

namespace SudokuEngine
{
    /// <summary>Быстрый перебор с выбором клетки с минимумом кандидатов. Нужен для проверки единственности решения.</summary>
    public static class Backtracking
    {
        /// <summary>
        /// Считает решения (не больше limit). В solution возвращается первое найденное (или null).
        /// Если rng != null, цифры перебираются в случайном порядке (так получают случайную полную сетку).
        /// Если в исходной расстановке уже есть повтор цифры, возвращает 0.
        /// </summary>
        public static int Count(int[] grid, int limit, out int[] solution, Random rng)
        {
            var search = new Searcher();
            search.limit = limit;
            search.rng = rng;
            if (!search.Init(grid))
            {
                solution = null;
                return 0;
            }
            search.Run();
            solution = search.first;
            return search.count;
        }

        /// <summary>Клетки, цифры которых повторяются в строке, столбце или блоке.</summary>
        public static List<int> FindConflicts(int[] grid)
        {
            var bad = new List<int>();
            for (int u = 0; u < 27; u++)
            {
                int[] unit = Geo.Units[u];
                for (int d = 1; d <= 9; d++)
                {
                    int found = 0;
                    for (int k = 0; k < 9; k++)
                    {
                        if (grid[unit[k]] == d) found++;
                    }
                    if (found < 2) continue;
                    for (int k = 0; k < 9; k++)
                    {
                        if (grid[unit[k]] == d && !bad.Contains(unit[k])) bad.Add(unit[k]);
                    }
                }
            }
            return bad;
        }

        sealed class Searcher
        {
            public readonly int[] g = new int[81];
            public readonly int[] rows = new int[9];
            public readonly int[] cols = new int[9];
            public readonly int[] bxs = new int[9];
            public int count;
            public int limit;
            public int[] first;
            public Random rng;

            public bool Init(int[] grid)
            {
                for (int i = 0; i < 81; i++)
                {
                    int v = grid[i];
                    g[i] = v;
                    if (v == 0) continue;
                    int bit = Geo.Bit[v];
                    int r = i / 9;
                    int c = i % 9;
                    int bx = Geo.BoxOf(i);
                    if ((rows[r] & bit) != 0 || (cols[c] & bit) != 0 || (bxs[bx] & bit) != 0) return false;
                    rows[r] |= bit;
                    cols[c] |= bit;
                    bxs[bx] |= bit;
                }
                return true;
            }

            public void Run()
            {
                int best = -1;
                int bestMask = 0;
                int bestCount = 10;
                for (int i = 0; i < 81; i++)
                {
                    if (g[i] != 0) continue;
                    int m = ~(rows[i / 9] | cols[i % 9] | bxs[Geo.BoxOf(i)]) & Geo.All;
                    int n = Geo.Pop(m);
                    if (n < bestCount)
                    {
                        bestCount = n;
                        best = i;
                        bestMask = m;
                        if (n <= 1) break;
                    }
                }
                if (best < 0)
                {
                    count++;
                    if (first == null) first = (int[])g.Clone();
                    return;
                }
                if (bestCount == 0) return;

                int rr = best / 9;
                int cc = best % 9;
                int bb = Geo.BoxOf(best);
                int[] ds = new int[bestCount];
                int di = 0;
                for (int d = 1; d <= 9; d++)
                {
                    if ((bestMask & Geo.Bit[d]) != 0) ds[di++] = d;
                }
                if (rng != null)
                {
                    for (int x = di - 1; x > 0; x--)
                    {
                        int y = rng.Next(x + 1);
                        int tmp = ds[x];
                        ds[x] = ds[y];
                        ds[y] = tmp;
                    }
                }
                for (int q = 0; q < di; q++)
                {
                    int digit = ds[q];
                    int bit = Geo.Bit[digit];
                    g[best] = digit;
                    rows[rr] |= bit;
                    cols[cc] |= bit;
                    bxs[bb] |= bit;
                    Run();
                    g[best] = 0;
                    rows[rr] &= ~bit;
                    cols[cc] &= ~bit;
                    bxs[bb] &= ~bit;
                    if (count >= limit) return;
                }
            }
        }
    }
}
