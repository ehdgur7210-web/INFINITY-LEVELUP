using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 빌드 후 화면에서 디버그 로그 확인 (경량 버전)
/// - 빈 GameObject에 붙이면 자동으로 LOG 버튼 생성
/// - 오브젝트 매 프레임 생성/삭제 없이 텍스트만 업데이트
/// </summary>
public class InGameDebugLogger : MonoBehaviour
{
    public static InGameDebugLogger Instance { get; private set; }

    [Header("설정")]
    public int maxLogCount = 30;
    public bool openOnStart = false;
    public bool autoOpenOnError = true;

    [Header("토글 방법 - 화면 구석 탭")]
    public int tapCountToOpen = 3;          // 몇 번 탭해야 열리는지
    public float tapResetTime = 1.5f;       // 탭 사이 초기화 시간(초)
    public TapCorner corner = TapCorner.BottomLeft;

    public enum TapCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    // ── 탭 감지 ──
    private int tapCount = 0;
    private float lastTapTime = -99f;

    // ── 로그 데이터 ──
    private readonly List<string> logLines = new List<string>();
    private bool isDirty = false;

    // ── UI 참조 ──
    private GameObject panelRoot;
    private TextMeshProUGUI logText;
    private bool isPanelOpen = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] InGameDebugLogger가 생성되었습니다.");
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        BuildUI();
        Application.logMessageReceived += OnLogReceived;
    }

    void Start()
    {
        SetPanelVisible(openOnStart);
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= OnLogReceived;
    }

    // ── 패널 자동 열기 플래그 (OnLogReceived에서 직접 SetActive 금지라 여기서 처리) ──
    private bool pendingOpen = false;

    void Update()
    {
        // ✅ autoOpenOnError 패널 열기
        if (pendingOpen)
        {
            pendingOpen = false;
            SetPanelVisible(true);
        }

        if (isDirty && isPanelOpen && logText != null)
        {
            logText.text = string.Join("\n", logLines);
            isDirty = false;
        }

        CheckCornerTap();
    }

    private void CheckCornerTap()
    {
        // 마우스 클릭(에디터) + 터치(모바일) 모두 지원
        bool tapped = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        tapped = Input.GetMouseButtonDown(0);
        if (tapped) CheckTapPosition(Input.mousePosition);
#else
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
                CheckTapPosition(t.position);
        }
#endif
    }

    private void CheckTapPosition(Vector2 pos)
    {
        // 화면 구석 영역 계산 (화면의 1/5 크기)
        float zoneW = Screen.width / 5f;
        float zoneH = Screen.height / 5f;

        Rect zone = corner switch
        {
            TapCorner.TopLeft => new Rect(0, Screen.height - zoneH, zoneW, zoneH),
            TapCorner.TopRight => new Rect(Screen.width - zoneW, Screen.height - zoneH, zoneW, zoneH),
            TapCorner.BottomRight => new Rect(Screen.width - zoneW, 0, zoneW, zoneH),
            _ => new Rect(0, 0, zoneW, zoneH), // BottomLeft
        };

        if (!zone.Contains(pos)) return;

        float now = Time.unscaledTime;

        // 탭 간격이 너무 길면 초기화
        if (now - lastTapTime > tapResetTime)
            tapCount = 0;

        tapCount++;
        lastTapTime = now;

        if (tapCount >= tapCountToOpen)
        {
            tapCount = 0;
            SetPanelVisible(!isPanelOpen);
        }
    }

    // ─────────────────────────────────────────
    // 로그 수신 (UI 조작 금지!)
    // ─────────────────────────────────────────
    private void OnLogReceived(string message, string stackTrace, LogType type)
    {
        // ⚠️ 이 안에서 절대 Destroy/Instantiate/UI 조작 금지 → 무한루프 원인
        string prefix = type switch
        {
            LogType.Warning => "<color=#FFD93D>[W]</color>",
            LogType.Error => "<color=#FF4444>[E]</color>",
            LogType.Exception => "<color=#FF4444>[X]</color>",
            _ => "<color=#AAAAAA>[ ]</color>"
        };

        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        logLines.Add($"<size=12><color=#666666>[{timestamp}]</color></size> {prefix} {message}");

        if (logLines.Count > maxLogCount)
            logLines.RemoveAt(0);

        isDirty = true;

        if (autoOpenOnError && (type == LogType.Error || type == LogType.Exception))
            pendingOpen = true; // ✅ Update에서 SetPanelVisible(true) 실행하도록 플래그
    }

    // ─────────────────────────────────────────
    // UI 생성 (최초 1회)
    // ─────────────────────────────────────────
    private void BuildUI()
    {
        // Canvas
        GameObject canvasObj = new GameObject("DebugLoggerCanvas");
        DontDestroyOnLoad(canvasObj);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        // ✅ LOG 버튼 제거 - 화면 구석 탭으로 토글
        // Inspector에서 corner / tapCountToOpen 설정 가능
        // 기본값: 왼쪽 하단 구석 3번 탭

        // 로그 패널 (전체 화면 절반)
        panelRoot = new GameObject("DebugPanel");
        panelRoot.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRt = panelRoot.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = new Vector2(1f, 0.55f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        panelRoot.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.08f, 0.88f);

        // 닫기 버튼
        GameObject closeObj = new GameObject("CloseBtn");
        closeObj.transform.SetParent(panelRoot.transform, false);
        RectTransform closeRt = closeObj.AddComponent<RectTransform>();
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(1, 1);
        closeRt.pivot = new Vector2(1, 1);
        closeRt.anchoredPosition = new Vector2(-5, -5);
        closeRt.sizeDelta = new Vector2(60, 36);
        closeObj.AddComponent<Image>().color = new Color(0.8f, 0.15f, 0.15f);
        Button closeBtn = closeObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(() => SetPanelVisible(false));
        GameObject closeLabel = new GameObject("Label");
        closeLabel.transform.SetParent(closeObj.transform, false);
        RectTransform clRt = closeLabel.AddComponent<RectTransform>();
        clRt.anchorMin = Vector2.zero; clRt.anchorMax = Vector2.one;
        clRt.offsetMin = clRt.offsetMax = Vector2.zero;
        var closeTmp = closeLabel.AddComponent<TextMeshProUGUI>();
        closeTmp.text = "✕"; closeTmp.fontSize = 20;
        closeTmp.fontStyle = FontStyles.Bold;
        closeTmp.color = Color.white;
        closeTmp.alignment = TextAlignmentOptions.Center;

        // Clear 버튼
        GameObject clearObj = new GameObject("ClearBtn");
        clearObj.transform.SetParent(panelRoot.transform, false);
        RectTransform clearRt = clearObj.AddComponent<RectTransform>();
        clearRt.anchorMin = clearRt.anchorMax = new Vector2(0, 1);
        clearRt.pivot = new Vector2(0, 1);
        clearRt.anchoredPosition = new Vector2(5, -5);
        clearRt.sizeDelta = new Vector2(80, 36);
        clearObj.AddComponent<Image>().color = new Color(0.4f, 0.15f, 0.6f);
        Button clearBtn = clearObj.AddComponent<Button>();
        clearBtn.onClick.AddListener(ClearLogs);
        GameObject clearLabel = new GameObject("Label");
        clearLabel.transform.SetParent(clearObj.transform, false);
        RectTransform clrRt = clearLabel.AddComponent<RectTransform>();
        clrRt.anchorMin = Vector2.zero; clrRt.anchorMax = Vector2.one;
        clrRt.offsetMin = clrRt.offsetMax = Vector2.zero;
        var clearTmp = clearLabel.AddComponent<TextMeshProUGUI>();
        clearTmp.text = "Clear"; clearTmp.fontSize = 16;
        clearTmp.fontStyle = FontStyles.Bold;
        clearTmp.color = Color.white;
        clearTmp.alignment = TextAlignmentOptions.Center;

        // 스크롤 뷰
        GameObject scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(panelRoot.transform, false);
        RectTransform scrollRt = scrollObj.AddComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(0, 0);
        scrollRt.offsetMax = new Vector2(0, -46);
        scrollObj.AddComponent<Image>().color = Color.clear;
        ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        RectTransform vpRt = viewport.AddComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = Color.clear;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        // Content (단일 TMP)
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewport.transform, false);
        RectTransform contentRt = contentObj.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0, 1);
        contentRt.offsetMin = contentRt.offsetMax = Vector2.zero;

        // ✅ 단일 TMP - 전체 로그를 하나의 텍스트로 표시 (오브젝트 생성/삭제 없음)
        logText = contentObj.AddComponent<TextMeshProUGUI>();
        logText.fontSize = 13;
        logText.color = new Color(0.9f, 0.9f, 0.9f);
        logText.enableWordWrapping = true;
        logText.richText = true;
        logText.text = "";

        ContentSizeFitter csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = vpRt;
        scrollRect.content = contentRt;

        panelRoot.SetActive(false);
    }

    // ─────────────────────────────────────────
    private void SetPanelVisible(bool visible)
    {
        isPanelOpen = visible;
        if (panelRoot != null)
            panelRoot.SetActive(visible);

        if (visible)
            isDirty = true; // 패널 열릴 때 즉시 갱신
    }

    private void ClearLogs()
    {
        logLines.Clear();
        if (logText != null) logText.text = "";
    }
}