using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SudokuEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Прототип интерфейса: весь экран строится из кода, сцену собирать не нужно.
/// Достаточно, чтобы скрипт лежал в проекте — он сам создаётся при запуске (см. Boot).
/// Когда будете делать свой интерфейс, удалите атрибут у Boot() и вызывайте движок напрямую:
///   PuzzleGenerator.Generate(...), PuzzleAnalyzer.Analyze(...).
/// Рассчитан на портретную ориентацию.
/// </summary>
public class SudokuApp : MonoBehaviour
{
    enum Mode { Menu, Play, Editor }

    static bool exists;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { exists = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (exists) return;
        new GameObject("SudokuApp").AddComponent<SudokuApp>();
    }

    // ---------- цвета ----------
    static readonly Color ColBg = new Color(0.95f, 0.95f, 0.97f);
    static readonly Color ColInk = new Color(0.10f, 0.10f, 0.14f);
    static readonly Color ColUser = new Color(0.10f, 0.36f, 0.85f);
    static readonly Color ColGray = new Color(0.40f, 0.40f, 0.46f);
    static readonly Color ColButton = new Color(0.20f, 0.30f, 0.55f);
    static readonly Color ColPaper = Color.white;
    static readonly Color ColPeer = new Color(0.91f, 0.94f, 1.00f);
    static readonly Color ColSel = new Color(0.62f, 0.78f, 1.00f);
    static readonly Color ColSame = new Color(1.00f, 0.96f, 0.72f);
    static readonly Color ColBad = new Color(1.00f, 0.74f, 0.74f);
    static readonly Color ColHintPattern = new Color(0.78f, 0.93f, 0.78f);
    static readonly Color ColHintElim = new Color(1.00f, 0.85f, 0.62f);
    static readonly Color ColHintPlace = new Color(0.45f, 0.85f, 0.55f);
    static readonly Color[] LevelColors = new Color[]
    {
        new Color(0.20f, 0.65f, 0.35f), new Color(0.45f, 0.70f, 0.25f), new Color(0.88f, 0.62f, 0.12f),
        new Color(0.92f, 0.45f, 0.15f), new Color(0.80f, 0.22f, 0.22f), new Color(0.45f, 0.20f, 0.60f)
    };

    // ---------- состояние игры ----------
    Mode mode = Mode.Menu;
    readonly int[] grid = new int[81];
    readonly bool[] given = new bool[81];
    int selected = -1;
    Level playLevel = Level.Easy;
    readonly List<int> hintPattern = new List<int>();
    readonly List<int> hintElim = new List<int>();
    readonly List<int> markedBad = new List<int>();
    int hintPlace = -1;

    // ---------- интерфейс ----------
    Font font;
    RectTransform safe;
    Rect lastSafe;
    GameObject menuPanel;
    GameObject gamePanel;
    GameObject infoPanel;
    GameObject busyPanel;
    Text headerText;
    Text messageText;
    Text busyText;
    Text infoTitle;
    readonly Image[] cellImg = new Image[81];
    readonly Text[] cellTxt = new Text[81];
    RectTransform infoContent;
    ScrollRect infoScroll;
    Button infoAction;
    Action infoActionCallback;
    Button btnA;
    Button btnB;
    Button btnC;

    // ---------- фоновые задачи ----------
    bool busy;
    Func<bool> poll;

    void Awake() { exists = true; }

    void Start()
    {
        font = LoadFont();
        EnsureEventSystem();
        BuildCanvas();
        BuildMenu();
        BuildGame();
        BuildInfo();
        BuildBusy();
        OpenMenu();
    }

    void Update()
    {
        if (poll != null && poll()) poll = null;
        if (safe != null && Screen.safeArea != lastSafe) ApplySafeArea();
    }

    // =====================================================================
    //  Построение интерфейса
    // =====================================================================

    static Font LoadFont()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Anchor(RectTransform rt, float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static void TopRow(RectTransform rt, float top, float height, float marginX)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(marginX, -(top + height));
        rt.offsetMax = new Vector2(-marginX, -top);
    }

    Text StyleText(Text t, string text, int size, TextAnchor anchor, Color color)
    {
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.text = text;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    Text MakeText(RectTransform parent, string text, int size, TextAnchor anchor, Color color)
    {
        RectTransform rt = NewRect("Text", parent);
        Anchor(rt, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);
        return StyleText(rt.gameObject.AddComponent<Text>(), text, size, anchor, color);
    }

    Text Label(RectTransform parent, string text, int size, TextAnchor anchor, Color color, float top, float height)
    {
        RectTransform rt = NewRect("Label", parent);
        TopRow(rt, top, height, 40f);
        return StyleText(rt.gameObject.AddComponent<Text>(), text, size, anchor, color);
    }

    Button ButtonOn(RectTransform rt, string label, Color bg, int size, Action onClick)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = bg;
        Button btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        Text tx = MakeText(rt, label, size, TextAnchor.MiddleCenter, Color.white);
        tx.resizeTextForBestFit = true;
        tx.resizeTextMinSize = 18;
        tx.resizeTextMaxSize = size;
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        return btn;
    }

    static void SetButton(Button btn, string label, Action onClick)
    {
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClick());
        btn.GetComponentInChildren<Text>().text = label;
    }

    void BuildCanvas()
    {
        var cgo = new GameObject("SudokuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = (RectTransform)cgo.transform;
        RectTransform bg = NewRect("Background", root);
        Anchor(bg, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);
        bg.gameObject.AddComponent<Image>().color = ColBg;

        safe = NewRect("SafeArea", root);
        ApplySafeArea();
    }

    void ApplySafeArea()
    {
        Rect sa = Screen.safeArea;
        lastSafe = sa;
        if (Screen.width <= 0 || Screen.height <= 0) return;
        safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
        safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        safe.offsetMin = Vector2.zero;
        safe.offsetMax = Vector2.zero;
    }

    void BuildMenu()
    {
        RectTransform root = NewRect("Menu", safe);
        Anchor(root, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);
        menuPanel = root.gameObject;

        Label(root, "Судоку", 110, TextAnchor.MiddleCenter, ColInk, 110, 140).fontStyle = FontStyle.Bold;
        Label(root, "Сложность считается по модели из научной работы", 38, TextAnchor.MiddleCenter, ColGray, 250, 90);

        for (int i = 0; i < 6; i++)
        {
            Level lv = (Level)i;
            RectTransform rt = NewRect("Level" + i, root);
            TopRow(rt, 380f + i * 150f, 125f, 70f);
            ButtonOn(rt, Model.LevelNameRu(lv) + "   ·   " + Model.LevelNameEn(lv), LevelColors[i], 46, () => StartGeneration(lv));
        }

        RectTransform own = NewRect("Own", root);
        TopRow(own, 1330f, 140f, 70f);
        ButtonOn(own, "Своё судоку: проверить и оценить сложность", ColButton, 40, () => OpenEditor());

        Label(root, "R3C4 — строка 3, столбец 4", 32, TextAnchor.MiddleCenter, ColGray, 1500, 60);
    }

    void BuildGame()
    {
        RectTransform root = NewRect("Game", safe);
        Anchor(root, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);
        gamePanel = root.gameObject;

        // шапка
        RectTransform header = NewRect("Header", root);
        TopRow(header, 20f, 150f, 30f);
        RectTransform back = NewRect("Back", header);
        back.anchorMin = new Vector2(0f, 0.12f);
        back.anchorMax = new Vector2(0.26f, 0.88f);
        back.offsetMin = Vector2.zero;
        back.offsetMax = Vector2.zero;
        ButtonOn(back, "Меню", ColButton, 40, () => OpenMenu());
        RectTransform ht = NewRect("Title", header);
        ht.anchorMin = new Vector2(0.28f, 0f);
        ht.anchorMax = new Vector2(1f, 1f);
        ht.offsetMin = Vector2.zero;
        ht.offsetMax = Vector2.zero;
        headerText = StyleText(ht.gameObject.AddComponent<Text>(), "", 42, TextAnchor.MiddleCenter, ColInk);

        // поле
        RectTransform gridRt = NewRect("Grid", root);
        gridRt.anchorMin = new Vector2(0.5f, 1f);
        gridRt.anchorMax = new Vector2(0.5f, 1f);
        gridRt.pivot = new Vector2(0.5f, 1f);
        gridRt.anchoredPosition = new Vector2(0f, -190f);
        gridRt.sizeDelta = new Vector2(1000f, 1000f);
        gridRt.gameObject.AddComponent<Image>().color = ColInk;
        for (int i = 0; i < 81; i++)
        {
            int r = i / 9;
            int c = i % 9;
            RectTransform cell = NewRect("Cell" + i, gridRt);
            cell.anchorMin = new Vector2(c / 9f, 1f - (r + 1) / 9f);
            cell.anchorMax = new Vector2((c + 1) / 9f, 1f - r / 9f);
            float left = (c % 3 == 0) ? 4f : 1f;
            float right = (c % 3 == 2) ? 4f : 1f;
            float top = (r % 3 == 0) ? 4f : 1f;
            float bottom = (r % 3 == 2) ? 4f : 1f;
            cell.offsetMin = new Vector2(left, bottom);
            cell.offsetMax = new Vector2(-right, -top);
            Image img = cell.gameObject.AddComponent<Image>();
            img.color = ColPaper;
            Button btn = cell.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            int idx = i;
            btn.onClick.AddListener(() => OnCellClicked(idx));
            cellImg[i] = img;
            cellTxt[i] = MakeText(cell, "", 64, TextAnchor.MiddleCenter, ColInk);
        }

        // сообщение
        RectTransform msg = NewRect("Message", root);
        TopRow(msg, 1205f, 110f, 40f);
        messageText = StyleText(msg.gameObject.AddComponent<Text>(), "", 34, TextAnchor.MiddleCenter, ColGray);
        messageText.resizeTextForBestFit = true;
        messageText.resizeTextMinSize = 20;
        messageText.resizeTextMaxSize = 34;
        messageText.verticalOverflow = VerticalWrapMode.Truncate;

        // цифры
        RectTransform pad = NewRect("Pad", root);
        TopRow(pad, 1330f, 120f, 40f);
        for (int d = 1; d <= 9; d++)
        {
            int digit = d;
            RectTransform b = NewRect("Digit" + d, pad);
            b.anchorMin = new Vector2((d - 1) / 9f, 0f);
            b.anchorMax = new Vector2(d / 9f, 1f);
            b.offsetMin = new Vector2(3f, 0f);
            b.offsetMax = new Vector2(-3f, 0f);
            ButtonOn(b, d.ToString(), ColButton, 56, () => OnDigit(digit));
        }

        // кнопки действий
        RectTransform actions = NewRect("Actions", root);
        TopRow(actions, 1470f, 120f, 40f);
        Button[] row = new Button[4];
        string[] labels = new string[] { "Стереть", "", "", "" };
        for (int k = 0; k < 4; k++)
        {
            RectTransform b = NewRect("Action" + k, actions);
            b.anchorMin = new Vector2(k / 4f, 0f);
            b.anchorMax = new Vector2((k + 1) / 4f, 1f);
            b.offsetMin = new Vector2(3f, 0f);
            b.offsetMax = new Vector2(-3f, 0f);
            row[k] = ButtonOn(b, labels[k], ColButton, 36, null);
        }
        SetButton(row[0], "Стереть", () => OnDigit(0));
        btnA = row[1];
        btnB = row[2];
        btnC = row[3];
    }

    void BuildInfo()
    {
        RectTransform root = NewRect("Info", safe);
        Anchor(root, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);
        infoPanel = root.gameObject;
        root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        RectTransform card = NewRect("Card", root);
        Anchor(card, 0f, 0f, 1f, 1f, 30f, 60f, 30f, 60f);
        card.gameObject.AddComponent<Image>().color = Color.white;

        infoTitle = Label(card, "", 44, TextAnchor.MiddleLeft, ColInk, 20f, 110f);
        infoTitle.fontStyle = FontStyle.Bold;

        RectTransform scrollRt = NewRect("Scroll", card);
        Anchor(scrollRt, 0f, 0f, 1f, 1f, 20f, 150f, 20f, 140f);
        infoScroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = NewRect("Viewport", scrollRt);
        Anchor(viewport, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        infoContent = NewRect("Content", viewport);
        infoContent.anchorMin = new Vector2(0f, 1f);
        infoContent.anchorMax = new Vector2(1f, 1f);
        infoContent.pivot = new Vector2(0.5f, 1f);
        infoContent.offsetMin = Vector2.zero;
        infoContent.offsetMax = Vector2.zero;
        VerticalLayoutGroup vlg = infoContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 14f;
        vlg.padding = new RectOffset(10, 10, 10, 10);
        ContentSizeFitter fitter = infoContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        infoScroll.viewport = viewport;
        infoScroll.content = infoContent;
        infoScroll.horizontal = false;
        infoScroll.vertical = true;
        infoScroll.movementType = ScrollRect.MovementType.Elastic;
        infoScroll.scrollSensitivity = 40f;

        RectTransform bottom = NewRect("Bottom", card);
        bottom.anchorMin = new Vector2(0f, 0f);
        bottom.anchorMax = new Vector2(1f, 0f);
        bottom.offsetMin = new Vector2(20f, 20f);
        bottom.offsetMax = new Vector2(-20f, 130f);
        RectTransform a = NewRect("Action", bottom);
        a.anchorMin = new Vector2(0f, 0f);
        a.anchorMax = new Vector2(0.64f, 1f);
        a.offsetMin = Vector2.zero;
        a.offsetMax = new Vector2(-6f, 0f);
        infoAction = ButtonOn(a, "", ColButton, 36, () => { if (infoActionCallback != null) infoActionCallback(); });
        RectTransform c = NewRect("Close", bottom);
        c.anchorMin = new Vector2(0.64f, 0f);
        c.anchorMax = new Vector2(1f, 1f);
        c.offsetMin = new Vector2(6f, 0f);
        c.offsetMax = Vector2.zero;
        ButtonOn(c, "Закрыть", ColGray, 36, () => HideInfo());
        infoPanel.SetActive(false);
    }

    void BuildBusy()
    {
        RectTransform root = NewRect("Busy", safe);
        Anchor(root, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);
        busyPanel = root.gameObject;
        root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        busyText = MakeText(root, "", 52, TextAnchor.MiddleCenter, Color.white);
        busyPanel.SetActive(false);
    }

    // =====================================================================
    //  Экраны и режимы
    // =====================================================================

    void OpenMenu()
    {
        mode = Mode.Menu;
        menuPanel.SetActive(true);
        gamePanel.SetActive(false);
        infoPanel.SetActive(false);
    }

    void ShowGame()
    {
        menuPanel.SetActive(false);
        gamePanel.SetActive(true);
    }

    void StartGeneration(Level level)
    {
        int seed = Environment.TickCount ^ UnityEngine.Random.Range(0, 1 << 30);
        Level lv = level;
        Background("Генерация: " + Model.LevelNameRu(lv) + "…",
            () => PuzzleGenerator.Generate(lv, seed, 20000, null),
            gp => StartPlay(gp.Grid, gp.Solution, gp.Rating.Level, gp.Rating.Score, gp.ExactMatch ? "" :
                "Точный уровень «" + Model.LevelNameRu(gp.Target) + "» за отведённое время найти не удалось — показан ближайший. "));
        playLevel = lv;
    }

    void StartPlay(int[] puzzle, int[] sol, Level level, int score, string note)
    {
        mode = Mode.Play;
        for (int i = 0; i < 81; i++)
        {
            grid[i] = puzzle[i];
            given[i] = puzzle[i] != 0;
        }
        selected = -1;
        ClearHints();
        ShowGame();
        headerText.text = Model.LevelNameRu(level) + "  ·  S = " + score;
        SetMessage(note + "Выберите клетку и цифру");
        SetButton(btnA, "Подсказка", () => OnHint());
        SetButton(btnB, "Путь решения", () => OnPath());
        SetButton(btnC, "Новая", () => StartGeneration(playLevel));
        RefreshBoard();
    }

    void OpenEditor()
    {
        mode = Mode.Editor;
        for (int i = 0; i < 81; i++)
        {
            grid[i] = 0;
            given[i] = false;
        }
        selected = -1;
        ClearHints();
        ShowGame();
        headerText.text = "Своё судоку";
        SetMessage("Введите цифры условия и нажмите «Оценить»");
        SetButton(btnA, "Оценить", () => OnRate());
        SetButton(btnB, "Играть", () => OnPlayOwn());
        SetButton(btnC, "Очистить", () => OnClear());
        RefreshBoard();
    }

    void SetMessage(string s)
    {
        if (messageText != null) messageText.text = s;
    }

    void ClearHints()
    {
        hintPattern.Clear();
        hintElim.Clear();
        markedBad.Clear();
        hintPlace = -1;
    }

    void RefreshBoard()
    {
        List<int> bad = Backtracking.FindConflicts(grid);
        int selDigit = selected >= 0 ? grid[selected] : 0;
        for (int i = 0; i < 81; i++)
        {
            Color bg = ColPaper;
            if (selected >= 0 && i != selected && Geo.Sees(selected, i)) bg = ColPeer;
            if (selDigit != 0 && grid[i] == selDigit) bg = ColSame;
            if (i == selected) bg = ColSel;
            if (hintPattern.Contains(i)) bg = ColHintPattern;
            if (hintElim.Contains(i)) bg = ColHintElim;
            if (i == hintPlace) bg = ColHintPlace;
            if (bad.Contains(i) || markedBad.Contains(i)) bg = ColBad;
            cellImg[i].color = bg;
            cellTxt[i].text = grid[i] == 0 ? "" : grid[i].ToString();
            cellTxt[i].color = given[i] ? ColInk : ColUser;
            cellTxt[i].fontStyle = given[i] ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    // =====================================================================
    //  Ввод
    // =====================================================================

    void OnCellClicked(int i)
    {
        selected = i;
        ClearHints();
        RefreshBoard();
    }

    void OnDigit(int d)
    {
        if (selected < 0 || busy) return;
        if (mode == Mode.Play && given[selected]) return;
        grid[selected] = d;
        if (mode == Mode.Editor) given[selected] = d != 0;
        ClearHints();
        RefreshBoard();
        CheckWin();
    }

    void OnClear()
    {
        for (int i = 0; i < 81; i++)
        {
            grid[i] = 0;
            given[i] = false;
        }
        ClearHints();
        SetMessage("Введите цифры условия и нажмите «Оценить»");
        RefreshBoard();
    }

    void CheckWin()
    {
        if (mode != Mode.Play) return;
        for (int i = 0; i < 81; i++)
        {
            if (grid[i] == 0) return;
        }
        if (Backtracking.FindConflicts(grid).Count > 0) return;
        SetMessage("Решено! Все цифры расставлены верно.");
    }

    // =====================================================================
    //  Подсказка, путь решения, оценка
    // =====================================================================

    int[] Snapshot()
    {
        return (int[])grid.Clone();
    }

    void OnHint()
    {
        int[] snap = Snapshot();
        Background("Ищу подсказку…", () => PuzzleAnalyzer.Analyze(snap), res =>
        {
            if (!res.IsSolvable)
            {
                ShowUnsolvable(res);
                return;
            }
            if (res.Steps == null)
            {
                SetMessage(res.Message);
                return;
            }
            if (res.Steps.Count == 0)
            {
                SetMessage("Судоку уже решено.");
                return;
            }
            // ближайший ход, который ставит цифру, и все приёмы, которые к нему ведут
            int k = 0;
            while (k < res.Steps.Count && res.Steps[k].Placements.Count == 0) k++;
            if (k >= res.Steps.Count) k = res.Steps.Count - 1;
            SolveStep last = res.Steps[k];

            var rows = new List<string>();
            if (last.Placements.Count > 0)
            {
                CellDigit p = last.Placements[0];
                rows.Add("Следующая цифра: " + Geo.RC(p.Cell) + " = " + p.Digit + ". Приёмы, которые к ней ведут:");
            }
            else
            {
                rows.Add("Следующие приёмы:");
            }
            for (int i = 0; i <= k; i++) rows.Add(res.Steps[i].Describe());

            hintPattern.Clear();
            hintElim.Clear();
            hintPlace = -1;
            hintPattern.AddRange(last.Cells);
            for (int i = 0; i < last.Eliminations.Count; i++) hintElim.Add(last.Eliminations[i].Cell);
            Action apply = null;
            string label = null;
            if (last.Placements.Count > 0)
            {
                CellDigit pl = last.Placements[0];
                hintPlace = pl.Cell;
                label = "Поставить " + pl.Digit;
                apply = () =>
                {
                    grid[pl.Cell] = pl.Digit;
                    selected = pl.Cell;
                    ClearHints();
                    RefreshBoard();
                    CheckWin();
                };
            }
            RefreshBoard();
            ShowInfo("Подсказка", rows, label, apply);
        });
    }

    void OnPath()
    {
        int[] snap = Snapshot();
        Background("Строю путь решения…", () => PuzzleAnalyzer.Analyze(snap), res =>
        {
            if (!res.IsSolvable)
            {
                ShowUnsolvable(res);
                return;
            }
            ShowPath(res);
        });
    }

    void OnRate()
    {
        int[] snap = Snapshot();
        Background("Оцениваю сложность…", () => PuzzleAnalyzer.Analyze(snap), res =>
        {
            if (!res.IsSolvable)
            {
                ShowUnsolvable(res);
                return;
            }
            ShowRating(res);
        });
    }

    void OnPlayOwn()
    {
        int[] snap = Snapshot();
        Background("Проверяю расстановку…", () => PuzzleAnalyzer.Analyze(snap), res =>
        {
            if (!res.IsSolvable)
            {
                ShowUnsolvable(res);
                return;
            }
            if (res.Status != PuzzleStatus.Unique || res.Rating == null)
            {
                ShowRating(res);
                return;
            }
            playLevel = res.Rating.Level;
            StartPlay(res.Grid, res.Solution, res.Rating.Level, res.Rating.Score, "");
        });
    }

    void ShowUnsolvable(AnalysisResult res)
    {
        markedBad.Clear();
        markedBad.AddRange(res.ConflictCells);
        RefreshBoard();
        SetMessage(res.Message);
        var rows = new List<string>();
        rows.Add(res.Message);
        ShowInfo("Не решаемо", rows, null, null);
    }

    void ShowRating(AnalysisResult res)
    {
        var rows = new List<string>();
        rows.Add(res.Message);
        Rating r = res.Rating;
        if (r != null)
        {
            rows.Add("Сложность: " + Model.LevelNameRu(r.Level) + " (" + Model.LevelNameEn(r.Level) + ")");
            rows.Add("Баллы S = " + r.Score + "  — уровень по баллам: " + Model.LevelNameRu(r.LevelByScore)
                + " (диапазон " + Model.ScoreRange(r.LevelByScore) + ")");
            string hardest = r.Hardest != null ? "; самый тяжёлый приём: " + r.Hardest.Title : "";
            rows.Add("Минимальный уровень по приёмам: " + Model.LevelNameRu(r.LevelByTechnique) + hardest);
            rows.Add("Использованные приёмы, число применений и баллы по формуле M + (n − 1)·I:");
            for (int i = 0; i < r.Usage.Count; i++)
            {
                TechniqueUsage u = r.Usage[i];
                rows.Add("•  " + u.Info.Title + ": " + u.Count + " → " + u.Points);
            }
            rows.Add("Шагов в оптимальном пути: " + r.StepCount);
        }
        if (r != null && res.Steps != null) ShowInfo("Оценка сложности", rows, "Показать путь решения", () => ShowPath(res));
        else ShowInfo("Оценка сложности", rows, null, null);
    }

    void ShowPath(AnalysisResult res)
    {
        var rows = new List<string>();
        if (res.Steps == null)
        {
            rows.Add(res.Message);
            ShowInfo("Путь решения", rows, null, null);
            return;
        }
        rows.Add("Каждый шаг — самый лёгкий из доступных приёмов (по весам из работы). R3C4 — строка 3, столбец 4.");
        if (res.Rating != null)
        {
            rows.Add("Итог: " + Model.LevelNameRu(res.Rating.Level) + ", S = " + res.Rating.Score + ", шагов: " + res.Rating.StepCount);
        }
        for (int i = 0; i < res.Steps.Count; i++) rows.Add(res.Steps[i].Describe());
        ShowInfo("Оптимальный путь решения", rows, null, null);
    }

    // =====================================================================
    //  Окно с прокруткой
    // =====================================================================

    void ShowInfo(string title, List<string> rows, string actionLabel, Action action)
    {
        infoTitle.text = title;
        for (int i = infoContent.childCount - 1; i >= 0; i--)
        {
            Transform child = infoContent.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        for (int i = 0; i < rows.Count; i++)
        {
            RectTransform rt = NewRect("Row", infoContent);
            Text t = rt.gameObject.AddComponent<Text>();
            StyleText(t, rows[i], 34, TextAnchor.UpperLeft, ColInk);
            if (i == 0) t.fontStyle = FontStyle.Bold;
        }
        infoActionCallback = action;
        infoAction.gameObject.SetActive(actionLabel != null);
        if (actionLabel != null) infoAction.GetComponentInChildren<Text>().text = actionLabel;
        infoPanel.SetActive(true);
        Canvas.ForceUpdateCanvases();
        infoScroll.verticalNormalizedPosition = 1f;
    }

    void HideInfo()
    {
        infoPanel.SetActive(false);
    }

    // =====================================================================
    //  Фоновые задачи (генерация и анализ могут занимать до нескольких секунд)
    // =====================================================================

    void Background<T>(string text, Func<T> work, Action<T> done)
    {
        if (busy) return;
        busy = true;
        busyText.text = text;
        busyPanel.SetActive(true);
        Task<T> task = Task.Run(work);
        poll = () =>
        {
            if (!task.IsCompleted) return false;
            busy = false;
            busyPanel.SetActive(false);
            if (task.IsFaulted)
            {
                string msg = task.Exception != null ? task.Exception.GetBaseException().Message : "неизвестная ошибка";
                SetMessage("Ошибка: " + msg);
            }
            else
            {
                done(task.Result);
            }
            return true;
        };
    }
}
