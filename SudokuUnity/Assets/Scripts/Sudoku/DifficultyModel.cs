using System;
using System.Collections.Generic;

namespace SudokuEngine
{
    /// <summary>Уровни сложности из раздела 4.2 работы.</summary>
    public enum Level
    {
        VeryEasy = 0,
        Easy = 1,
        Medium = 2,
        Hard = 3,
        Expert = 4,
        Extreme = 5
    }

    /// <summary>
    /// Приёмы в том же порядке, что и строки таблицы весов из работы.
    /// Числовые значения совпадают с индексами в Model.Table — не меняйте их порядок отдельно от таблицы.
    /// </summary>
    public enum Technique
    {
        Single = 0,             // Naked / Hidden Single
        NakedPair = 1,
        HiddenPair = 2,
        BoxLine = 3,            // Box-Line / Pointing
        NakedTriple = 4,
        HiddenTriple = 5,
        XYWing = 6,
        XYZWing = 7,
        XWing = 8,
        Swordfish = 9,
        Jellyfish = 10,         // Jellyfish / higher fish
        SimpleColoring = 11,
        AdvancedColoring = 12,  // пока не реализован в решателе
        XYChainShort = 13,      // длина <= 6 клеток
        XYChainLong = 14,       // длина > 6 клеток
        UniqueRectangle = 15,
        ALS = 16,               // пока не реализован в решателе
        ForcingChainShort = 17,
        ForcingChainLong = 18,
        Nishio = 19,
        BruteForce = 20
    }

    public sealed class TechniqueInfo
    {
        public readonly Technique Id;
        public readonly string NameRu;
        public readonly string NameEn;
        /// <summary>M — баллы за первое применение.</summary>
        public readonly int FirstScore;
        /// <summary>I — добавка за каждое повторное применение.</summary>
        public readonly int Increment;
        /// <summary>Минимальный уровень задачи, если приём хотя бы раз понадобился.</summary>
        public readonly Level MinLevel;
        /// <summary>Умеет ли решатель находить этот приём.</summary>
        public readonly bool Implemented;

        public TechniqueInfo(Technique id, string nameRu, string nameEn, int first, int inc, Level min, bool implemented)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            FirstScore = first;
            Increment = inc;
            MinLevel = min;
            Implemented = implemented;
        }

        public string Title { get { return NameRu + " (" + NameEn + ")"; } }
    }

    public sealed class TechniqueUsage
    {
        public TechniqueInfo Info;
        public int Count;
        public int Points;
    }

    /// <summary>Результат оценки задачи по модели из работы.</summary>
    public sealed class Rating
    {
        /// <summary>Итоговый балл S = сумма по приёмам: M + (n - 1) * I.</summary>
        public int Score;
        /// <summary>Уровень по таблице диапазонов очков.</summary>
        public Level LevelByScore;
        /// <summary>Максимум из «минимальных уровней» использованных приёмов.</summary>
        public Level LevelByTechnique;
        /// <summary>Итоговый уровень = максимум из двух предыдущих.</summary>
        public Level Level;
        /// <summary>Самый тяжёлый (по весу M) применённый приём; null, если шагов не было.</summary>
        public TechniqueInfo Hardest;
        public int StepCount;
        public readonly List<TechniqueUsage> Usage = new List<TechniqueUsage>();
    }

    public static class Model
    {
        /// <summary>
        /// Порог «короткой» цепочки. В работе он задан для XY-цепочек (&lt;= 6 клеток);
        /// для принудительных цепочек используется тот же порог, но считается в «раундах» вывода.
        /// </summary>
        public const int ShortChain = 6;

        /// <summary>Таблица весов из раздела 4.2 работы (первое применение, добавка, минимальный уровень).</summary>
        public static readonly TechniqueInfo[] Table = new TechniqueInfo[]
        {
            new TechniqueInfo(Technique.Single,            "Одиночный кандидат",              "Naked / Hidden Single",      1,   1,   Level.VeryEasy, true),
            new TechniqueInfo(Technique.NakedPair,         "Явная пара",                      "Naked Pair",                 10,  5,   Level.VeryEasy, true),
            new TechniqueInfo(Technique.HiddenPair,        "Скрытая пара",                    "Hidden Pair",                12,  5,   Level.Easy,     true),
            new TechniqueInfo(Technique.BoxLine,           "Пересечение блок–линия",          "Box-Line / Pointing",        15,  7,   Level.Easy,     true),
            new TechniqueInfo(Technique.NakedTriple,       "Явная тройка",                    "Naked Triple",               25,  10,  Level.Medium,   true),
            new TechniqueInfo(Technique.HiddenTriple,      "Скрытая тройка",                  "Hidden Triple",              30,  10,  Level.Medium,   true),
            new TechniqueInfo(Technique.XYWing,            "XY-Wing",                         "XY-Wing",                    180, 30,  Level.Hard,     true),
            new TechniqueInfo(Technique.XYZWing,           "XYZ-Wing",                        "XYZ-Wing",                   220, 35,  Level.Hard,     true),
            new TechniqueInfo(Technique.XWing,             "X-Wing",                          "X-Wing",                     200, 30,  Level.Hard,     true),
            new TechniqueInfo(Technique.Swordfish,         "Меч-рыба",                        "Swordfish",                  300, 50,  Level.Expert,   true),
            new TechniqueInfo(Technique.Jellyfish,         "Медуза",                          "Jellyfish / higher fish",    400, 70,  Level.Expert,   true),
            new TechniqueInfo(Technique.SimpleColoring,    "Простая раскраска",               "Simple Coloring",            150, 40,  Level.Hard,     true),
            new TechniqueInfo(Technique.AdvancedColoring,  "Расширенная раскраска",          "Advanced / Multi-Coloring",  250, 60,  Level.Expert,   false),
            new TechniqueInfo(Technique.XYChainShort,      "XY-цепочка (короткая, до 6)",     "XY-chain",                   180, 30,  Level.Hard,     true),
            new TechniqueInfo(Technique.XYChainLong,       "XY-цепочка (длинная, больше 6)",  "XY-chain, long",             250, 50,  Level.Expert,   true),
            new TechniqueInfo(Technique.UniqueRectangle,   "Уникальный прямоугольник",        "Unique Rectangle",           200, 40,  Level.Hard,     true),
            new TechniqueInfo(Technique.ALS,               "Почти заблокированные множества", "ALS",                        300, 50,  Level.Expert,   false),
            new TechniqueInfo(Technique.ForcingChainShort, "Принудительная цепочка (короткая)", "Forcing Chain",            250, 50,  Level.Expert,   true),
            new TechniqueInfo(Technique.ForcingChainLong,  "Принудительная цепочка (длинная)",  "Forcing Chain, long",      350, 80,  Level.Expert,   true),
            new TechniqueInfo(Technique.Nishio,            "Нишио (логическое предположение)", "Nishio",                    500, 150, Level.Extreme,  true),
            new TechniqueInfo(Technique.BruteForce,        "Перебор",                         "Brute Force / Backtracking", 800, 200, Level.Extreme,  true)
        };

        public static TechniqueInfo Info(Technique t) { return Table[(int)t]; }

        /// <summary>Проверка согласованности enum и таблицы (используется в самотесте).</summary>
        public static bool TableIsConsistent()
        {
            for (int i = 0; i < Table.Length; i++)
            {
                if ((int)Table[i].Id != i) return false;
            }
            return Table.Length == 21;
        }

        /// <summary>Диапазоны очков S из раздела 4.2 работы.</summary>
        public static Level LevelFromScore(int s)
        {
            if (s < 50) return Level.VeryEasy;
            if (s < 200) return Level.Easy;
            if (s < 500) return Level.Medium;
            if (s < 1000) return Level.Hard;
            if (s < 2000) return Level.Expert;
            return Level.Extreme;
        }

        /// <summary>S = сумма по всем использованным приёмам: M + (n - 1) * I.</summary>
        public static int ScoreOf(int[] counts)
        {
            int total = 0;
            for (int t = 0; t < Table.Length; t++)
            {
                if (counts[t] > 0) total += Table[t].FirstScore + (counts[t] - 1) * Table[t].Increment;
            }
            return total;
        }

        /// <summary>Считает оценку по готовому пути решения.</summary>
        public static Rating Compute(IList<SolveStep> steps)
        {
            var counts = new int[Table.Length];
            for (int i = 0; i < steps.Count; i++) counts[(int)steps[i].Technique]++;

            var rating = new Rating();
            rating.StepCount = steps.Count;
            Level minLevel = Level.VeryEasy;
            for (int t = 0; t < Table.Length; t++)
            {
                int n = counts[t];
                if (n == 0) continue;
                TechniqueInfo info = Table[t];
                var usage = new TechniqueUsage();
                usage.Info = info;
                usage.Count = n;
                usage.Points = info.FirstScore + (n - 1) * info.Increment;
                rating.Usage.Add(usage);
                if (info.MinLevel > minLevel) minLevel = info.MinLevel;
                if (rating.Hardest == null || info.FirstScore > rating.Hardest.FirstScore) rating.Hardest = info;
            }
            rating.Score = ScoreOf(counts);
            rating.LevelByScore = LevelFromScore(rating.Score);
            rating.LevelByTechnique = minLevel;
            rating.Level = rating.LevelByScore > minLevel ? rating.LevelByScore : minLevel;
            return rating;
        }

        public static string LevelNameRu(Level l)
        {
            switch (l)
            {
                case Level.VeryEasy: return "Очень лёгкий";
                case Level.Easy: return "Лёгкий";
                case Level.Medium: return "Средний";
                case Level.Hard: return "Сложный";
                case Level.Expert: return "Эксперт";
                default: return "Экстремальный";
            }
        }

        public static string LevelNameEn(Level l)
        {
            switch (l)
            {
                case Level.VeryEasy: return "Very Easy";
                case Level.Easy: return "Easy";
                case Level.Medium: return "Medium";
                case Level.Hard: return "Hard";
                case Level.Expert: return "Expert";
                default: return "Extreme";
            }
        }

        public static string ScoreRange(Level l)
        {
            switch (l)
            {
                case Level.VeryEasy: return "0–49";
                case Level.Easy: return "50–199";
                case Level.Medium: return "200–499";
                case Level.Hard: return "500–999";
                case Level.Expert: return "1000–1999";
                default: return "от 2000";
            }
        }
    }
}
