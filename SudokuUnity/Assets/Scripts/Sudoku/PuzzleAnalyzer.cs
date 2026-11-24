using System;
using System.Collections.Generic;

namespace SudokuEngine
{
    public enum PuzzleStatus
    {
        /// <summary>Решение ровно одно — обычное судоку.</summary>
        Unique,
        /// <summary>Решений больше одного.</summary>
        MultipleSolutions,
        /// <summary>Нет ни одного решения.</summary>
        NoSolution,
        /// <summary>Уже в исходной расстановке цифра повторяется в строке, столбце или блоке.</summary>
        InvalidGivens
    }

    public sealed class AnalysisResult
    {
        public PuzzleStatus Status;
        public string Message = "";
        public int[] Grid;
        public int[] Solution;
        /// <summary>Клетки с повторяющимися цифрами (для Status == InvalidGivens).</summary>
        public List<int> ConflictCells = new List<int>();
        /// <summary>Оценка по модели из работы (null, если решения нет).</summary>
        public Rating Rating;
        /// <summary>Оптимальный путь решения: на каждом шаге самый лёгкий приём (null, если решения нет).</summary>
        public List<SolveStep> Steps;

        public bool IsSolvable
        {
            get { return Status == PuzzleStatus.Unique || Status == PuzzleStatus.MultipleSolutions; }
        }
    }

    /// <summary>Проверяет расстановку игрока, оценивает её сложность и строит путь решения.</summary>
    public static class PuzzleAnalyzer
    {
        public static AnalysisResult Analyze(int[] grid)
        {
            var res = new AnalysisResult();
            res.Grid = (int[])grid.Clone();

            List<int> bad = Backtracking.FindConflicts(grid);
            if (bad.Count > 0)
            {
                res.Status = PuzzleStatus.InvalidGivens;
                res.ConflictCells = bad;
                res.Message = "Не решаемо: в строке, столбце или блоке повторяется цифра (клетки отмечены красным).";
                return res;
            }

            int[] sol;
            int n = Backtracking.Count(grid, 2, out sol, null);
            if (n == 0)
            {
                res.Status = PuzzleStatus.NoSolution;
                res.Message = "Не решаемо: у этой расстановки нет ни одного решения.";
                return res;
            }

            res.Solution = sol;
            res.Status = n == 1 ? PuzzleStatus.Unique : PuzzleStatus.MultipleSolutions;
            try
            {
                var solver = new HumanSolver(sol, n == 1);
                res.Steps = solver.SolveAll(grid);
                res.Rating = Model.Compute(res.Steps);
            }
            catch (InvalidOperationException ex)
            {
                res.Steps = null;
                res.Rating = null;
                res.Message = "Ошибка анализа: " + ex.Message;
                return res;
            }

            if (n == 1)
            {
                res.Message = "Решение единственное.";
            }
            else
            {
                res.Message = "Решений несколько (найдено минимум два) — это не классическое судоку. "
                    + "Оценка приблизительная: приём «Уникальный прямоугольник» в таком случае не используется.";
            }
            return res;
        }

        /// <summary>Разбор строки из 81 символа (цифры, пустые клетки — точка или 0).</summary>
        public static int[] ParseGrid(string s)
        {
            var grid = new int[81];
            int n = 0;
            for (int i = 0; i < s.Length && n < 81; i++)
            {
                char ch = s[i];
                if (ch == '.' || ch == '0') { grid[n++] = 0; }
                else if (ch >= '1' && ch <= '9') { grid[n++] = ch - '0'; }
            }
            if (n != 81) throw new ArgumentException("Нужно ровно 81 клетка, получено " + n);
            return grid;
        }
    }
}
