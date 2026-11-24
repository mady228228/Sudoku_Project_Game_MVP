# Sudoku with a research-based difficulty rating

A Sudoku game for Unity, built for phones in portrait orientation. It generates puzzles at six difficulty levels, rates puzzles you enter yourself, and shows how to solve any position step by step, naming every technique it uses.

Difficulty is scored with the model from my research paper (see [About the research](#about-the-research)), so the level of every puzzle comes from one fixed formula.

## Features

- New games at six levels: Very Easy, Easy, Medium, Hard, Expert, Extreme. Every puzzle has exactly one solution.
- A rating for puzzles you type in yourself. If a puzzle can't be solved, the game says so and marks the cells that clash.
- A solution path for any position. Each step names the technique, explains why it applies and shows what it changes.
- Hints that give the next digit together with the techniques that lead to it.

## Playing

Pick a level on the main screen. While the puzzle is being generated, a waiting screen is shown.

The interface text is in Russian. These are the labels used in this README:

| On screen | Meaning |
| --- | --- |
| Очень лёгкий, Лёгкий, Средний, Сложный, Эксперт, Экстремальный | Very Easy, Easy, Medium, Hard, Expert, Extreme |
| Своё судоку | Your own puzzle |
| Меню | Menu |
| Стереть | Erase |
| Подсказка | Hint |
| Путь решения | Solution path |
| Новая | New (another puzzle of the same level) |
| Оценить | Rate |
| Играть | Play |
| Очистить | Clear |
| Поставить N | Place N |
| Показать путь решения | Show solution path |
| Закрыть | Close |
| Не решаемо | Unsolvable |

On the board:

- Tap a cell, then tap a digit on the number pad. "Erase" clears the selected cell.
- Digits that came with the puzzle are bold black, yours are blue.
- The selected cell is highlighted. Its row, column and box are light blue, and cells holding the same digit are yellow.
- A digit repeated in a row, column or box turns red.
- The header shows the puzzle's level and its score, for example `Сложный · S = 525`.
- When the board is full and correct, the game tells you the puzzle is solved.

Buttons under the number pad:

- Hint finds the next digit you can place and lists the techniques needed to get there. The cells involved are highlighted: green for the pattern, orange for cells that lose a candidate, darker green for the cell to fill. "Place N" puts the digit on the board.
- Solution path opens the whole path from the current position.
- New generates another puzzle of the same level.

"Menu" in the top-left corner goes back to the main screen.

Hints and the solution path are always calculated from the digits that are on the board right now. If those digits clash or can't lead to a solution, the game answers "Unsolvable" instead.

## Entering your own puzzle

1. On the main screen choose "Your own puzzle".
2. Tap cells and enter the given digits.
3. Press "Rate".

You get one of three results:

- Unsolvable. A digit repeats in a row, column or box (those cells turn red), or the position has no solution at all.
- More than one solution. This isn't a proper Sudoku. You still get a rating, but it is approximate.
- One solution. You see the level, the total score S, the techniques used with the number of times each was needed and the points it added, and a button that opens the full solution path.

"Play" starts a game with your puzzle (only when it has exactly one solution). "Clear" empties the board.

## Difficulty levels

| Level | Score S | Techniques that set this level |
| --- | --- | --- |
| Very Easy | 0–49 | naked/hidden singles, naked pairs |
| Easy | 50–199 | hidden pairs, pointing and box-line reduction |
| Medium | 200–499 | naked and hidden triples |
| Hard | 500–999 | X-Wing, XY-Wing, XYZ-Wing, simple coloring, unique rectangles, XY-chains up to 6 cells |
| Expert | 1000–1999 | Swordfish, Jellyfish, XY-chains longer than 6 cells, forcing chains |
| Extreme | 2000 and up | Nishio (trial and contradiction), brute force |

For each technique a puzzle needs, the score adds `M + (n − 1) · I`, where `n` is how many times the technique was used, `M` is its base score and `I` the increment for every repeat. The total over all techniques is S.

The final level is the higher of two things: the level of the score S, and the minimum level of the hardest technique the puzzle needs. A puzzle that scores 245 (Medium) but needs an X-Wing is therefore rated Hard.

## Solution path

The path is built the way a person would solve the puzzle: at every step the easiest technique that works is used, which is the same approach as in Sudoku Explainer and HoDoKu. The rating is calculated from this path.

Techniques the solver recognises: naked and hidden singles, pairs and triples, pointing and box-line reduction, X-Wing, Swordfish, Jellyfish, XY-Wing, XYZ-Wing, simple coloring, unique rectangles (types 1, 2 and 4), XY-chains, forcing chains and Nishio. If none of them helps, the path continues with a brute-force step. Puzzles that need one are rated Extreme.

Cells are written as R3C4, meaning row 3, column 4. A step looks like this:

```
7. Скрытый одиночный кандидат (Hidden Single): в строке 3 цифра 4 может стоять только в клетке R3C7 → R3C7 = 4
```

That reads: in row 3 the digit 4 can only go in cell R3C7, so R3C7 = 4.

## How puzzles are generated

Very Easy to Expert: the generator starts from a random solved grid and removes digits one by one. After each removal it checks that the solution is still unique and that the puzzle can still be solved with techniques no harder than the target level. The finished puzzle is rated with the full model, and if its level doesn't match the one you picked, the generator tries again. If no exact match turns up within the time limit, the game offers the closest level it found and says so.

Extreme: puzzles of this level almost never come out of random removal, so they are made from a small set of known hard puzzles by randomly relabelling the digits, shuffling rows and columns inside their bands and stacks, and optionally transposing the grid. The rating is recalculated for the result.

Generation runs on a background thread.

## Getting started

You need Unity 2021 LTS or newer with the Unity UI (uGUI) package, which is included in new projects by default.

1. Copy `Assets/Scripts/Sudoku` into the `Assets` folder of your project.
2. Open any scene and press Play. The interface builds itself on start, so there is nothing to set up in the scene.
3. For a phone build, switch the platform in Build Settings to Android or iOS and build as usual. The layout is made for portrait orientation.

The menu item Sudoku → Run engine self-test runs a set of checks (the worked example from the paper, unsolvable puzzles, generation of every level). The results go to the Console.

## Using the engine in your own project

The engine (namespace `SudokuEngine`) does not depend on Unity, so it works with any interface.

```csharp
using SudokuEngine;
using UnityEngine;

// Generate a puzzle. Run this on a background thread, harder levels can take a moment.
// The last two arguments: time limit in milliseconds, optional cancel callback.
int seed = System.Environment.TickCount;
GeneratedPuzzle puzzle = PuzzleGenerator.Generate(Level.Hard, seed, 20000, null);

// Rate any puzzle: int[81], 0 means empty
AnalysisResult result = PuzzleAnalyzer.Analyze(puzzle.Grid);

// result.Status   Unique, MultipleSolutions, NoSolution or InvalidGivens
// result.Message  text ready to show to the player
// result.Rating   score S, level, techniques used
// result.Steps    the solution path
foreach (SolveStep step in result.Steps)
    Debug.Log(step.Describe());
```

To stop the built-in interface from starting automatically, remove the `[RuntimeInitializeOnLoadMethod]` attribute from `Boot()` in `SudokuApp.cs`.

The technique weights and level ranges are in `DifficultyModel.cs` (`Model.Table`). Change them there and every part of the game follows.

## Project layout

| File | What it does |
| --- | --- |
| `DifficultyModel.cs` | the scoring model: weights, formula, level ranges |
| `Geometry.cs` | the board, candidates, solution-step data |
| `HumanSolver.cs`, `HumanSolverAdvanced.cs` | the solver that works like a person and produces the solution path |
| `Backtracking.cs` | counts solutions: none, one or several |
| `PuzzleAnalyzer.cs` | checks and rates a puzzle |
| `PuzzleGenerator.cs` | the puzzle generator |
| `SudokuApp.cs` | the built-in interface |
| `Editor/SudokuSelfTest.cs` | the engine self-test |
| `Reference/sudoku_ref.py` | the same solver and model written in Python, convenient for experiments |

## About the research

The difficulty model comes from my research paper *Determining the Difficulty Level of Sudoku Puzzles* (original title «Определение уровня сложности вариантов судоку», written in Russian, 2026).

Two systems are commonly used to rate Sudoku. Sudoku Explainer (SER) gives a puzzle the rating of the hardest technique it needs and ignores how many steps the solution takes. HoDoKu adds up the weights of all steps, but one rare hard technique does not necessarily lift a puzzle into a high category, and the result depends on the program's settings.

The paper proposes a hybrid of the two. Every technique has a base score for its first use and a smaller increment for each repeat, and the scores of all techniques are added up. On top of that comes a minimum-level rule: the first appearance of a hard technique sets a floor for the level of the puzzle. The paper gives the weights for every technique, the score ranges for the six levels and a worked example.

To test the model, 10 volunteers solved 100 puzzles and rated how hard each one was. The ratings of SER, HoDoKu and the hybrid model were compared with the volunteers' opinions on a 10-point scale. The similarity to human ratings was 72% for SER, 87% for HoDoKu and 95% for the hybrid model.
