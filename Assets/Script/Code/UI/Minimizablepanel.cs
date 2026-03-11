using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// ══════════════════════════════════════════════════════════
///  MinimizablePanel — 패널 최소화/복원 + 드래그 이동 컴포넌트
/// ══════════════════════════════════════════════════════════
///
/// 사용법:
///   1. 패널 루트 GameObject에 이 컴포넌트 추가
///   2. Inspector에서 panelContent, minimizeButton, floatingButton 연결
///   3. floatingButton 위치가 드래그로 이동 가능한 미니 버튼이 됨
///
/// ✅ 기능:
///   - 최소화 버튼 → 패널 숨김 + 플로팅 버튼 표시
///   - 플로팅 버튼 드래그 → 화면 어디든 자유 이동
///   - 플로팅 버튼 탭 → 패널 원상복귀
///   - 애니메이션 (Scale + Fade)
/// </summary>
public class MinimizablePanel : MonoBehaviour, IPointerClickHandler
{
    // ─────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────
    [Header("패널 콘텐츠 (최소화 시 숨길 영역)")]
    [Tooltip("최소화할 패널 본체 (이 GameObject를 껐다 켬)")]
    [SerializeField] private GameObject panelContent;

    [Header("최소화 버튼 (패널 안에 있는 버튼)")]
    [Tooltip("패널 우측 상단 등에 배치된 최소화 버튼")]
    [SerializeField] private Button minimizeButton;
    [SerializeField] private TextMeshProUGUI minimizeBtnIcon; // ▼ 텍스트

    [Header("플로팅 미니 버튼 (최소화 상태에서 표시)")]
    [Tooltip("최소화 후 화면에 떠 있는 작은 버튼 (드래그 이동 가능)")]
    [SerializeField] private RectTransform floatingButton;
    [Tooltip("플로팅 버튼 안의 레이블 텍스트 (예: 채팅 / 보상 / 퀘스트)")]
    [SerializeField] private TextMeshProUGUI floatingLabel;
    [Tooltip("플로팅 버튼에 표시할 이름")]
    [SerializeField] private string panelLabel = "패널";

    [Header("애니메이션")]
    [SerializeField] private float animDuration = 0.18f;

    [Header("시작 상태")]
    [SerializeField] private bool startMinimized = false;

    // ─────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────
    private bool isMinimized = false;
    private Canvas rootCanvas;
    private RectTransform floatingRT;
    private Coroutine animCoroutine;

    // 드래그용
    private Vector2 dragOffset;
    private bool isDragging = false;
    private float clickTime;
    private const float DRAG_THRESHOLD = 0.15f; // 드래그/탭 구분 시간

    // ─────────────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────────────
    void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        if (floatingButton != null)
        {
            floatingRT = floatingButton.GetComponent<RectTransform>();
            if (floatingRT == null) floatingRT = floatingButton;
        }
    }

    void Start()
    {
        // 최소화 버튼 연결
        if (minimizeButton != null)
            minimizeButton.onClick.AddListener(ToggleMinimize);

        // 플로팅 버튼에 드래그 이벤트 추가
        if (floatingButton != null)
            SetupFloatingButtonEvents();

        // 레이블 설정
        if (floatingLabel != null)
            floatingLabel.text = panelLabel;

        // 초기 상태 적용
        ApplyState(startMinimized, instant: true);
    }

    // ─────────────────────────────────────────────────
    // 플로팅 버튼 드래그 이벤트 세팅
    // ─────────────────────────────────────────────────
    private void SetupFloatingButtonEvents()
    {
        EventTrigger trigger = floatingButton.gameObject.GetComponent<EventTrigger>()
                            ?? floatingButton.gameObject.AddComponent<EventTrigger>();

        // PointerDown → 드래그 시작
        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener((data) => OnFloatingPointerDown((PointerEventData)data));
        trigger.triggers.Add(down);

        // Drag → 이동
        var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        drag.callback.AddListener((data) => OnFloatingDrag((PointerEventData)data));
        trigger.triggers.Add(drag);

        // PointerUp → 탭이면 복원
        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener((data) => OnFloatingPointerUp((PointerEventData)data));
        trigger.triggers.Add(up);
    }

    private void OnFloatingPointerDown(PointerEventData data)
    {
        isDragging = false;
        clickTime = Time.realtimeSinceStartup;

        // 드래그 시작 오프셋 계산
        if (rootCanvas != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.GetComponent<RectTransform>(),
                data.position,
                data.pressEventCamera,
                out Vector2 localPoint
            );
            dragOffset = floatingRT.anchoredPosition - localPoint;
        }
    }

    private void OnFloatingDrag(PointerEventData data)
    {
        isDragging = true;

        if (rootCanvas == null || floatingRT == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(),
            data.position,
            data.pressEventCamera,
            out Vector2 localPoint
        );

        // 화면 경계 안으로 클램프
        Vector2 newPos = localPoint + dragOffset;
        Rect canvasRect = rootCanvas.GetComponent<RectTransform>().rect;
        float halfW = floatingRT.rect.width * 0.5f;
        float halfH = floatingRT.rect.height * 0.5f;

        newPos.x = Mathf.Clamp(newPos.x, canvasRect.xMin + halfW, canvasRect.xMax - halfW);
        newPos.y = Mathf.Clamp(newPos.y, canvasRect.yMin + halfH, canvasRect.yMax - halfH);

        floatingRT.anchoredPosition = newPos;
    }

    private void OnFloatingPointerUp(PointerEventData data)
    {
        float elapsed = Time.realtimeSinceStartup - clickTime;

        // 짧게 눌렀으면 탭 → 패널 복원
        if (!isDragging || elapsed < DRAG_THRESHOLD)
        {
            Restore();
        }

        isDragging = false;
    }

    // ─────────────────────────────────────────────────
    // 공개 메서드
    // ─────────────────────────────────────────────────
    public void ToggleMinimize()
    {
        SoundManager.Instance?.PlayButtonClick();
        ApplyState(!isMinimized, instant: false);
    }

    public void Minimize()
    {
        SoundManager.Instance?.PlayButtonClick();
        ApplyState(true, instant: false);
    }

    public void Restore()
    {
        SoundManager.Instance?.PlayButtonClick();
        ApplyState(false, instant: false);
    }

    public bool IsMinimized => isMinimized;

    // ─────────────────────────────────────────────────
    // 상태 적용 (애니메이션 포함)
    // ─────────────────────────────────────────────────
    private void ApplyState(bool minimize, bool instant)
    {
        isMinimized = minimize;

        if (animCoroutine != null) StopCoroutine(animCoroutine);

        if (instant)
        {
            if (panelContent != null) panelContent.SetActive(!minimize);
            if (floatingButton != null) floatingButton.gameObject.SetActive(minimize);
            if (minimizeBtnIcon != null) minimizeBtnIcon.text = minimize ? "▶" : "▼";
        }
        else
        {
            animCoroutine = StartCoroutine(AnimateStateChange(minimize));
        }
    }

    private IEnumerator AnimateStateChange(bool minimize)
    {
        if (minimize)
        {
            // ── 패널 → 최소화 ──
            // 패널 페이드 아웃
            CanvasGroup cg = panelContent?.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                float t = 0f;
                while (t < animDuration)
                {
                    t += Time.unscaledDeltaTime;
                    cg.alpha = Mathf.Lerp(1f, 0f, t / animDuration);
                    yield return null;
                }
                cg.alpha = 0f;
            }

            if (panelContent != null) panelContent.SetActive(false);

            // 플로팅 버튼 팝업
            if (floatingButton != null)
            {
                floatingButton.gameObject.SetActive(true);
                floatingButton.localScale = Vector3.zero;
                float t = 0f;
                while (t < animDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float s = Mathf.Lerp(0f, 1f, t / animDuration);
                    // 스프링 느낌
                    s = 1f - Mathf.Pow(1f - s, 3f);
                    floatingButton.localScale = Vector3.one * s;
                    yield return null;
                }
                floatingButton.localScale = Vector3.one;
            }

            if (minimizeBtnIcon != null) minimizeBtnIcon.text = "▶";
        }
        else
        {
            // ── 최소화 → 복원 ──
            // 플로팅 버튼 페이드 아웃
            if (floatingButton != null)
            {
                float t = 0f;
                while (t < animDuration * 0.5f)
                {
                    t += Time.unscaledDeltaTime;
                    float s = Mathf.Lerp(1f, 0f, t / (animDuration * 0.5f));
                    floatingButton.localScale = Vector3.one * s;
                    yield return null;
                }
                floatingButton.gameObject.SetActive(false);
            }

            // 패널 페이드 인
            if (panelContent != null)
            {
                panelContent.SetActive(true);
                CanvasGroup cg = panelContent.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 0f;
                    float t = 0f;
                    while (t < animDuration)
                    {
                        t += Time.unscaledDeltaTime;
                        cg.alpha = Mathf.Lerp(0f, 1f, t / animDuration);
                        yield return null;
                    }
                    cg.alpha = 1f;
                }
            }

            if (minimizeBtnIcon != null) minimizeBtnIcon.text = "▼";
        }
    }

    // IPointerClickHandler (패널 자체 클릭은 무시, 플로팅 버튼에서 처리)
    public void OnPointerClick(PointerEventData eventData) { }
}