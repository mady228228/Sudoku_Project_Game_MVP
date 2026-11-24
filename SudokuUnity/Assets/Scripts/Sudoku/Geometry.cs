using System;
using System.Collections.Generic;
using System.Text;

namespace SudokuEngine
{
    /// <summary>
    /// Геометрия поля 9x9: строки, столбцы, блоки, соседи клеток, битовые маски кандидатов.
    /// Клетки нумеруются 0..80 (строка * 9 + столбец). Цифра d хранится в маске как бит (1 &lt;&lt; (d-1)).
    /// </summary>
    public static class Geo
    {
        public const int All = 0x1FF;

        public static readonly int[][] Rows = new int[9][];
        public static readonly int[][] Cols = new int[9][];
        public static readonly int[][] Boxes = new int[9][];
        /// <summary>0..8 — строки, 9..17 — столбцы, 18..26 — блоки.</summary>
        public static readonly int[][] Units = new int[27][];
        /// <summary>Для каждой клетки номера трёх её групп (строка, столбец, блок).</summary>
        public static readonly int[][] UnitsOf = new int[81][];
        /// <summary>Для каждой клетки 20 «соседей» (те, что видят её).</summary>
        public static readonly int[][] Peers = new int[81][];
        public static readonly int[] Bit = new int[10];
        public static readonly int[] PopTable = new int[512];
        public static readonly int[] LowTable = new int[512];
        static readonly bool[] seesTable = new bool[81 * 81];

        static Geo()
        {
            for (int d = 1; d <= 9; d++) Bit[d] = 1 << (d - 1);
            for (int m = 0; m < 512; m++)
            {
                int n = 0;
                int low = 0;
                for (int d = 1; d <= 9; d++)
                {
                    if ((m & Bit[d]) == 0) continue;
                    n++;
                    if (low == 0) low = d;
                }
                PopTable[m] = n;
                LowTable[m] = low;
            }
            for (int i = 0; i < 9; i++)
            {
                Rows[i] = new int[9];
                Cols[i] = new int[9];
                Boxes[i] = new int[9];
            }
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    int cellIndex = r * 9 + c;
                    Rows[r][c] = cellIndex;
                    Cols[c][r] = cellIndex;
                    Boxes[(r / 3) * 3 + c / 3][(r % 3) * 3 + c % 3] = cellIndex;
                }
            }
            for (int i = 0; i < 9; i++)
            {
                Units[i] = Rows[i];
                Units[9 + i] = Cols[i];
                Units[18 + i] = Boxes[i];
            }
            for (int cell = 0; cell < 81; cell++)
            {
                UnitsOf[cell] = new int[] { cell / 9, 9 + cell % 9, 18 + BoxOf(cell) };
                var list = new List<int>();
                for (int k = 0; k < 3; k++)
                {
                    int[] unit = Units[UnitsOf[cell][k]];
                    for (int q = 0; q < 9; q++)
                    {
                        if (unit[q] != cell && !list.Contains(unit[q])) list.Add(unit[q]);
                    }
                }
                list.Sort();
                Peers[cell] = list.ToArray();
                for (int q = 0; q < list.Count; q++) seesTable[cell * 81 + list[q]] = true;
            }
        }

        public static int BoxOf(int cell) { return (cell / 27) * 3 + (cell % 9) / 3; }
        public static int Pop(int mask) { return PopTable[mask]; }
        public static bool Sees(int a, int b) { return seesTable[a * 81 + b]; }

        /// <summary>Запись клетки в виде R3C4 (строка 3, столбец 4).</summary>
        public static string RC(int cell) { return "R" + (cell / 9 + 1) + "C" + (cell % 9 + 1); }

        public static string UnitIn(int unit)
        {
            if (unit < 9) return "в строке " + (unit + 1);
            if (unit < 18) return "в столбце " + (unit - 8);
            return "в блоке " + (unit - 17);
        }

        /// <summary>Строка «3, 7» для маски кандидатов.</summary>
        public static string Digits(int mask)
        {
            var sb = new StringBuilder();
            for (int d = 1; d <= 9; d++)
            {
                if ((mask & Bit[d]) == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(d);
            }
            return sb.ToString();
        }

        public static string CellList(IList<int> cells)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(RC(cells[i]));
            }
            return sb.ToString();
        }
    }

    /// <summary>Состояние поля: поставленные цифры и маски кандидатов каждой клетки.</summary>
    public sealed class Board
    {
        public readonly int[] Val = new int[81];
        public readonly int[] Cand = new int[81];

        public static Board FromGrid(int[] grid)
        {
            var b = new Board();
            for (int i = 0; i < 81; i++) b.Val[i] = grid[i];
            for (int i = 0; i < 81; i++)
            {
                if (b.Val[i] != 0) continue;
                int m = Geo.All;
                int[] peers = Geo.Peers[i];
                for (int k = 0; k < peers.Length; k++)
                {
                    int v = b.Val[peers[k]];
                    if (v != 0) m &= ~Geo.Bit[v];
                }
                b.Cand[i] = m;
            }
            return b;
        }

        public void CopyFrom(Board other)
        {
            Array.Copy(other.Val, Val, 81);
            Array.Copy(other.Cand, Cand, 81);
        }

        public Board Clone()
        {
            var b = new Board();
            b.CopyFrom(this);
            return b;
        }

        public void Place(int cell, int digit)
        {
            Val[cell] = digit;
            Cand[cell] = 0;
            int clear = ~Geo.Bit[digit];
            int[] peers = Geo.Peers[cell];
            for (int k = 0; k < peers.Length; k++) Cand[peers[k]] &= clear;
        }

        public bool Has(int cell, int digit)
        {
            return Val[cell] == 0 && (Cand[cell] & Geo.Bit[digit]) != 0;
        }

        public bool IsSolved()
        {
            for (int i = 0; i < 81; i++)
            {
                if (Val[i] == 0) return false;
            }
            return true;
        }
    }

    public struct CellDigit
    {
        public int Cell;
        public int Digit;

        public CellDigit(int cell, int digit)
        {
            Cell = cell;
            Digit = digit;
        }
    }

    /// <summary>Один шаг пути решения: какой приём применён и что он дал.</summary>
    public sealed class SolveStep
    {
        public int Number;
        public Technique Technique;
        /// <summary>Название приёма: «Скрытый одиночный кандидат (Hidden Single)».</summary>
        public string Title = "";
        /// <summary>Короткое объяснение, почему приём применим.</summary>
        public string Reason = "";
        /// <summary>Какие цифры ставятся.</summary>
        public readonly List<CellDigit> Placements = new List<CellDigit>();
        /// <summary>Какие кандидаты удаляются.</summary>
        public readonly List<CellDigit> Eliminations = new List<CellDigit>();
        /// <summary>Клетки, образующие шаблон приёма (для подсветки).</summary>
        public readonly List<int> Cells = new List<int>();

        public string ResultText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Placements.Count; i++)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(Geo.RC(Placements[i].Cell)).Append(" = ").Append(Placements[i].Digit);
            }
            for (int d = 1; d <= 9; d++)
            {
                bool first = true;
                for (int i = 0; i < Eliminations.Count; i++)
                {
                    if (Eliminations[i].Digit != d) continue;
                    if (first)
                    {
                        if (sb.Length > 0) sb.Append("; ");
                        sb.Append("убрать ").Append(d).Append(" из ");
                        first = false;
                    }
                    else
                    {
                        sb.Append(", ");
                    }
                    sb.Append(Geo.RC(Eliminations[i].Cell));
                }
            }
            return sb.ToString();
        }

        public string Describe()
        {
            return Number + ". " + Title + ": " + Reason + " → " + ResultText();
        }

        public override string ToString() { return Describe(); }
    }
}
