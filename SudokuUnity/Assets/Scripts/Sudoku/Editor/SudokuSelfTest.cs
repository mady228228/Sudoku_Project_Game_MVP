using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using SudokuEngine;

/// <summary>
/// Самотест движка: меню Sudoku → Run engine self-test. Результат — в окне Console.
/// Запустите его сразу после импорта скриптов: так сразу видно, что движок работает так, как описано в работе.
/// </summary>
public static class SudokuSelfTest
{
    [MenuItem("Sudoku/Run engine self-test")]
    public static void Run()
    {
        var log = new StringBuilder();
        int failed = 0;

        // ---- 1. таблица весов ----
        Check(log, ref failed, "Таблица весов согласована с перечислением приёмов (21 строка)", Model.TableIsConsistent());

        // ---- 2. пример из раздела 4.2 работы: 15 одиночных + 2 X-Wing ----
        var steps = new List<SolveStep>();
        for (int i = 0; i < 15; i++)
        {
            var single = new SolveStep();
            single.Technique = Technique.Single;
            steps.Add(single);
        }
        for (int i = 0; i < 2; i++)
        {
            var xw = new SolveStep();
            xw.Technique = Technique.XWing;
            steps.Add(xw);
        }
        Rating example = Model.Compute(steps);
        Check(log, ref failed, "Пример из работы: S = 15·1 + (200 + (2−1)·30) = 245 (получилось " + example.Score + ")", example.Score == 245);
        Check(log, ref failed, "Пример из работы: по баллам Medium", example.LevelByScore == Level.Medium);
        Check(log, ref failed, "Пример из работы: итог Hard из-за X-Wing", example.Level == Level.Hard);

        // ---- 3. границы диапазонов ----
        bool ranges = Model.LevelFromScore(49) == Level.VeryEasy && Model.LevelFromScore(50) == Level.Easy
            && Model.LevelFromScore(199) == Level.Easy && Model.LevelFromScore(200) == Level.Medium
            && Model.LevelFromScore(499) == Level.Medium && Model.LevelFromScore(500) == Level.Hard
            && Model.LevelFromScore(999) == Level.Hard && Model.LevelFromScore(1000) == Level.Expert
            && Model.LevelFromScore(1999) == Level.Expert && Model.LevelFromScore(2000) == Level.Extreme;
        Check(log, ref failed, "Границы диапазонов очков 49/50, 199/200, 499/500, 999/1000, 1999/2000", ranges);

        // ---- 4. классическая лёгкая задача (все ходы — одиночки: 51 пустая клетка → S = 51) ----
        int[] easy = PuzzleAnalyzer.ParseGrid("53..7....6..195....98....6.8...6...34..8.3..17...2...6.6....28....419..5....8..79");
        AnalysisResult ea = PuzzleAnalyzer.Analyze(easy);
        Check(log, ref failed, "Лёгкая задача: решение единственное", ea.Status == PuzzleStatus.Unique);
        Check(log, ref failed, "Лёгкая задача: S = 51 и уровень Easy (получилось S = " + (ea.Rating != null ? ea.Rating.Score : -1) + ")",
            ea.Rating != null && ea.Rating.Score == 51 && ea.Rating.Level == Level.Easy);

        // ---- 5. очень трудная задача (Arto Inkala, 2012) ----
        DateTime t0 = DateTime.UtcNow;
        int[] hard = PuzzleAnalyzer.ParseGrid("8..........36......7..9.2...5...7.......457.....1...3...1....68..85...1..9....4..");
        AnalysisResult ha = PuzzleAnalyzer.Analyze(hard);
        double hardSec = (DateTime.UtcNow - t0).TotalSeconds;
        Check(log, ref failed, "Задача Инкалы: решение единственное", ha.Status == PuzzleStatus.Unique);
        Check(log, ref failed, "Задача Инкалы: уровень Extreme (S = " + (ha.Rating != null ? ha.Rating.Score : -1) + ", шагов "
            + (ha.Steps != null ? ha.Steps.Count : -1) + ", " + hardSec.ToString("0.00") + " c)",
            ha.Rating != null && ha.Rating.Level == Level.Extreme);

        // ---- 6. нерешаемые варианты ----
        int[] dup = new int[81];
        dup[0] = 5;
        dup[1] = 5;
        Check(log, ref failed, "Повтор цифры в строке → «не решаемо» (InvalidGivens)", PuzzleAnalyzer.Analyze(dup).Status == PuzzleStatus.InvalidGivens);

        int[] none = new int[81];
        for (int c = 0; c < 8; c++) none[c] = c + 1;   // строка 1: 1..8 и пустая клетка
        none[4 * 9 + 8] = 9;                          // цифра 9 в том же столбце: у пустой клетки нет кандидатов
        Check(log, ref failed, "Расстановка без решения → «не решаемо» (NoSolution)", PuzzleAnalyzer.Analyze(none).Status == PuzzleStatus.NoSolution);

        Check(log, ref failed, "Пустая сетка → несколько решений (MultipleSolutions)", PuzzleAnalyzer.Analyze(new int[81]).Status == PuzzleStatus.MultipleSolutions);

        // ---- 7. генерация по уровням ----
        Level[] levels = (Level[])Enum.GetValues(typeof(Level));
        for (int i = 0; i < levels.Length; i++)
        {
            Level lv = levels[i];
            GeneratedPuzzle gp = PuzzleGenerator.Generate(lv, 1000 + i, 30000, null);
            int[] solution;
            int count = Backtracking.Count(gp.Grid, 2, out solution, null);
            log.AppendLine("   " + lv + ": подсказок " + gp.Clues + ", S = " + gp.Rating.Score + ", попыток " + gp.Attempts
                + ", " + gp.Seconds.ToString("0.00") + " c");
            Check(log, ref failed, "Генерация " + lv + ": решение единственное", count == 1);
            Check(log, ref failed, "Генерация " + lv + ": уровень совпал с запрошенным (получился " + gp.Rating.Level + ")", gp.ExactMatch);
        }

        log.AppendLine(failed == 0 ? "ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ" : "ПРОВАЛЕНО ПРОВЕРОК: " + failed);
        if (failed == 0) Debug.Log(log.ToString());
        else Debug.LogError(log.ToString());
    }

    static void Check(StringBuilder log, ref int failed, string name, bool ok)
    {
        log.AppendLine((ok ? "[OK]   " : "[FAIL] ") + name);
        if (!ok) failed++;
    }
}
