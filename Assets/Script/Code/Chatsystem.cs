using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ============================================================
/// ChatSystem — 채팅창 시스템
/// ============================================================
/// - 인벤토리 패널이 내려가면 HotSkillBar 위에 나타남
/// - 인벤토리 패널이 올라오면 숨겨짐
/// - 페이드 인/아웃 애니메이션
/// 
/// [씬 구조 예시]
/// Canvas
///   └─ ChatPanel          ← 이 스크립트를 붙이는 GameObject
///        ├─ ChatBackground (Image)
///        ├─ MessageScrollView
///        │    └─ Viewport
///        │         └─ Content (Vertical Layout Group)
///        │              └─ MessageItem (Prefab)
///        ├─ InputArea
///        │    ├─ InputField (TMP_InputField)
///        │    └─ SendButton (Button)
///        └─ ToggleButton (선택사항)
/// ============================================================
/// </summary>
public class ChatSystem : MonoBehaviour
{
    public static ChatSystem Instance { get; private set; }

    // ─────────────────────────────────────────
    // Inspector 연결
    // ─────────────────────────────────────────
    [Header("채팅 패널 루트")]
    [Tooltip("채팅창 전체 GameObject (이 스크립트가 붙은 오브젝트)")]
    public GameObject chatPanel;

    [Header("채팅창 크기 설정 (소형화)")]
    [Tooltip("채팅창 높이 (px). 핫바 위에 딱 맞게 작게 설정권장: 120~160)")]
    public float chatPanelHeight = 130f;
    [Tooltip("채팅창 너비 (px). 화면 절반 정도 권장: 400~500)")]
    public float chatPanelWidth = 460f;
    [Tooltip("채팅창 Y 위치 (핫바 바로 위, 양수로 설정)")]
    public float chatPanelPosY = 10f;

    [Header("메시지 UI")]
    [Tooltip("메시지가 쌓이는 ScrollRect")]
    public ScrollRect messageScrollRect;
    [Tooltip("메시지 아이템 프리팹 (TextMeshProUGUI가 있어야 함)")]
    public GameObject messagePrefab;
    [Tooltip("메시지 아이템들이 들어갈 Content Transform")]
    public Transform messageContent;

    [Header("입력 UI")]
    public TMP_InputField chatInputField;
    public Button sendButton;

    [Header("페이드 설정")]
    [Tooltip("페이드 인/아웃 시간 (초)")]
    public float fadeDuration = 0.2f;

    [Header("메시지 아이템 설정")]
    [Tooltip("메시지 한 줄 높이 (px)")]
    public float messageItemHeight = 28f;
    [Tooltip("화면에 유지할 최대 메시지 수")]
    public int maxMessages = 50;

    [Header("최소화 버튼")]
    [Tooltip("최소화/복원 버튼 연결")]
    public Button minimizeButton;
    [Tooltip("최소화 버튼 안의 텍스트 (▲ / ▼ 전환)")]
    public TextMeshProUGUI minimizeBtnText;

    [Header("높이 설정")]
    [Tooltip("펼쳐진 상태 높이 (px)")]
    public float expandedHeight = 200f;
    [Tooltip("최소화 상태 높이 = 프리팹 1개 높이 + 입력창")]
    public float minimizedHeight = 36f;
    [Tooltip("높이 변환 속도 (초)")]
    public float resizeDuration = 0.2f;

    private RectTransform chatPanelRect;
    private bool isMinimized = false;
    private Coroutine resizeCoroutine;

    // ─────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────
    private CanvasGroup canvasGroup;
    private bool isVisible = false;
    private Coroutine fadeCoroutine;

    // 메시지 기록 (가짜 NPC 채팅용)
    private List<GameObject> messageObjects = new List<GameObject>();

    // 가짜 플레이어 이름 & 색상
    private static readonly string[] FakeNames = {
        "검은기사", "불꽃마법사", "전설용사", "어둠사냥꾼",
        "천상여전사", "바람도적", "얼음술사", "폭풍전사"
    };
    private static readonly Color[] NameColors = {
        new Color(1f, 0.4f, 0.4f),     // 빨강
        new Color(0.4f, 0.8f, 1f),     // 하늘
        new Color(0.6f, 1f, 0.4f),     // 초록
        new Color(1f, 0.8f, 0.2f),     // 노랑
        new Color(0.9f, 0.5f, 1f),     // 보라
        new Color(1f, 0.6f, 0.2f),     // 주황
    };

    // ─────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // ★ Awake에서 즉시 숨김 (Start 전에 1프레임도 보이지 않도록)
        if (chatPanel != null)
            chatPanel.SetActive(false);

        // CanvasGroup이 없으면 자동 추가 (페이드용)
        canvasGroup = chatPanel != null
            ? chatPanel.GetComponent<CanvasGroup>() ?? chatPanel.AddComponent<CanvasGroup>()
            : null;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    void Start()
    {
        // ★ 크기만 코드로 설정, 위치는 Inspector에서 직접 세팅한 값 유지
        if (chatPanel != null)
        {
            RectTransform rt = chatPanel.GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = new Vector2(chatPanelWidth, chatPanelHeight);
        }

        // ★ Content에 Vertical Layout Group + Content Size Fitter 자동 추가
        // (없으면 메시지가 쌓여도 보이지 않음)
        if (messageContent != null)
        {
            // Vertical Layout Group
            var vlg = messageContent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (vlg == null) vlg = messageContent.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 40f;
            vlg.padding = new RectOffset(6, 6, 4, 4);

            // Content Size Fitter (세로 자동 확장)
            var csf = messageContent.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (csf == null) csf = messageContent.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            csf.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        }

        isVisible = false;

        if (sendButton != null)
            sendButton.onClick.AddListener(SendMessage);

        if (chatInputField != null)
            chatInputField.onSubmit.AddListener(_ => SendMessage());

        // ★ 최소화 버튼 연결
        if (minimizeButton != null)
            minimizeButton.onClick.AddListener(ToggleMinimize);

        if (minimizeBtnText != null)
            minimizeBtnText.text = "▲";

        // 패널 RectTransform 캐싱
        if (chatPanel != null)
            chatPanelRect = chatPanel.GetComponent<RectTransform>();

        AddSystemMessage("채팅에 오신 것을 환영합니다!");
        StartCoroutine(FakeChatCoroutine());
    }

    // ─────────────────────────────────────────
    // 표시 / 숨김 (InventoryManager에서 호출)
    // ─────────────────────────────────────────

    /// <summary>인벤토리가 내려갈 때 호출</summary>
    public void ShowChat()
    {
        if (isVisible) return;
        isVisible = true;

        if (chatPanel != null)
            chatPanel.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeTo(1f));

        // ★ 패널이 활성화된 다음 프레임에 스크롤 (레이아웃 반영 보장)
        StartCoroutine(ScrollToBottomNextFrame());
    }

    /// <summary>인벤토리가 올라올 때 호출</summary>
    public void HideChat()
    {
        if (!isVisible) return;
        isVisible = false;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutAndDeactivate());
    }

    // ─────────────────────────────────────────
    // 최소화 / 복원
    // ─────────────────────────────────────────

    /// <summary>최소화 버튼 클릭 시 호출</summary>
    public void ToggleMinimize()
    {
        isMinimized = !isMinimized;

        float targetHeight = isMinimized ? minimizedHeight : expandedHeight;

        // 버튼 텍스트 전환
        if (minimizeBtnText != null)
            minimizeBtnText.text = isMinimized ? "▼" : "▲";

        // 스크롤뷰 및 입력창 표시/숨김
        if (messageScrollRect != null)
            messageScrollRect.gameObject.SetActive(!isMinimized);

        if (resizeCoroutine != null) StopCoroutine(resizeCoroutine);
        resizeCoroutine = StartCoroutine(ResizePanel(targetHeight));

        Debug.Log($"[ChatSystem] {(isMinimized ? "최소화" : "복원")} → 높이 {targetHeight}px");
    }

    private IEnumerator ResizePanel(float targetHeight)
    {
        if (chatPanelRect == null) yield break;

        float startHeight = chatPanelRect.sizeDelta.y;
        float elapsed = 0f;

        while (elapsed < resizeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / resizeDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic

            Vector2 size = chatPanelRect.sizeDelta;
            size.y = Mathf.Lerp(startHeight, targetHeight, eased);
            chatPanelRect.sizeDelta = size;

            yield return null;
        }

        Vector2 final = chatPanelRect.sizeDelta;
        final.y = targetHeight;
        chatPanelRect.sizeDelta = final;

        resizeCoroutine = null;
    }

    // ─────────────────────────────────────────
    // 메시지 전송
    // ─────────────────────────────────────────
    public void SendMessage()
    {
        if (chatInputField == null) return;

        string text = chatInputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        // 내 메시지 추가
        string myName = GameManager.Instance != null
            ? GameManager.Instance.PlayerLevel + "Lv 나"
            : "나";

        AddChatMessage(myName, text, new Color(1f, 1f, 0.6f));

        chatInputField.text = "";
        chatInputField.ActivateInputField(); // 포커스 유지

        ScrollToBottom();
    }

    // ─────────────────────────────────────────
    // 메시지 추가 헬퍼
    // ─────────────────────────────────────────

    /// <summary>일반 채팅 메시지</summary>
    public void AddChatMessage(string playerName, string message, Color nameColor)
    {
        if (messagePrefab == null || messageContent == null)
        {
            Debug.LogWarning("[ChatSystem] messagePrefab 또는 messageContent가 null!");
            return;
        }

        bool wasActive = chatPanel != null && chatPanel.activeSelf;
        if (!wasActive && chatPanel != null) chatPanel.SetActive(true);

        GameObject msgObj = Instantiate(messagePrefab, messageContent);

        // 루트 또는 자식에서 TMP 검색
        TextMeshProUGUI tmp = msgObj.GetComponent<TextMeshProUGUI>()
                           ?? msgObj.GetComponentInChildren<TextMeshProUGUI>(true);

        if (tmp != null)
        {
            string nameHex = ColorUtility.ToHtmlStringRGB(nameColor);
            tmp.text = $"<color=#{nameHex}>[{playerName}]</color> <color=#000000>{message}</color>";
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.color = Color.black;  // ★ 기본 색상 흰색으로 강제 설정
            tmp.fontSize = 16f;

            // TMP가 자식인 경우 부모 RectTransform 크기에 꽉 차게 설정
            RectTransform tmpRect = tmp.GetComponent<RectTransform>();
            if (tmpRect != null && tmpRect.gameObject != msgObj)
            {
                tmpRect.anchorMin = Vector2.zero;
                tmpRect.anchorMax = Vector2.one;
                tmpRect.offsetMin = new Vector2(4f, 0f);  // 좌측 여백 4px
                tmpRect.offsetMax = Vector2.zero;
            }
        }
        else
        {
            Debug.LogWarning("[ChatSystem] messagePrefab에 TextMeshProUGUI가 없습니다!");
        }

        // ★ ContentSizeFitter 대신 LayoutElement 사용
        // (부모 Content의 "Control Child Size: Height"와 충돌하지 않음)
        var csf = msgObj.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (csf != null) Destroy(csf);

        LayoutElement le = msgObj.GetComponent<LayoutElement>() ?? msgObj.AddComponent<LayoutElement>();
        le.minHeight = messageItemHeight;
        le.preferredHeight = messageItemHeight;

        if (!wasActive && chatPanel != null) chatPanel.SetActive(false);

        RegisterMessage(msgObj);
        if (isVisible) StartCoroutine(ScrollToBottomNextFrame());
    }

    /// <summary>시스템 메시지 (노란색)</summary>
    public void AddSystemMessage(string message)
    {
        if (messagePrefab == null || messageContent == null)
        {
            Debug.LogWarning("[ChatSystem] messagePrefab 또는 messageContent가 null!");
            return;
        }

        bool wasActive = chatPanel != null && chatPanel.activeSelf;
        if (!wasActive && chatPanel != null) chatPanel.SetActive(true);

        GameObject msgObj = Instantiate(messagePrefab, messageContent);

        TextMeshProUGUI tmp = msgObj.GetComponent<TextMeshProUGUI>()
                           ?? msgObj.GetComponentInChildren<TextMeshProUGUI>(true);

        if (tmp != null)
        {
            tmp.text = $"<color=#FFD700>[시스템] {message}</color>";
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.color = Color.black;
            tmp.fontSize = 16f;

            RectTransform tmpRect = tmp.GetComponent<RectTransform>();
            if (tmpRect != null && tmpRect.gameObject != msgObj)
            {
                tmpRect.anchorMin = Vector2.zero;
                tmpRect.anchorMax = Vector2.one;
                tmpRect.offsetMin = new Vector2(4f, 0f);
                tmpRect.offsetMax = Vector2.zero;
            }
        }

        var csf = msgObj.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (csf != null) Destroy(csf);

        LayoutElement le = msgObj.GetComponent<LayoutElement>() ?? msgObj.AddComponent<LayoutElement>();
        le.minHeight = messageItemHeight;
        le.preferredHeight = messageItemHeight;

        if (!wasActive && chatPanel != null) chatPanel.SetActive(false);

        RegisterMessage(msgObj);
        if (isVisible) StartCoroutine(ScrollToBottomNextFrame());
    }

    private void RegisterMessage(GameObject msgObj)
    {
        messageObjects.Add(msgObj);

        // 최대 메시지 수 초과 시 오래된 것 삭제
        while (messageObjects.Count > maxMessages)
        {
            Destroy(messageObjects[0]);
            messageObjects.RemoveAt(0);
        }
    }

    // ─────────────────────────────────────────
    // 스크롤 맨 아래
    // ─────────────────────────────────────────
    private void ScrollToBottom()
    {
        if (messageScrollRect == null) return;
        Canvas.ForceUpdateCanvases();
        messageScrollRect.verticalNormalizedPosition = 0f;
    }

    // ★ 레이아웃 리빌드 후 다음 프레임에 스크롤 (메시지 추가 후 크기 반영 보장)
    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null; // 1프레임 대기
        Canvas.ForceUpdateCanvases();
        if (messageScrollRect != null)
            messageScrollRect.verticalNormalizedPosition = 0f;
    }

    // ─────────────────────────────────────────
    // 페이드 코루틴
    // ─────────────────────────────────────────
    private IEnumerator FadeTo(float targetAlpha)
    {
        if (canvasGroup == null) yield break;

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        fadeCoroutine = null;
    }

    private IEnumerator FadeOutAndDeactivate()
    {
        yield return StartCoroutine(FadeTo(0f));
        if (chatPanel != null)
            chatPanel.SetActive(false);
    }

    // ─────────────────────────────────────────
    // 가짜 NPC 채팅 (분위기용, 필요 없으면 제거)
    // ─────────────────────────────────────────
    private static readonly string[] FakeMessages = {
        "몬스터 어디있냐", "골드 빨리 모아야되는데",
        "가챠 전설 떴다!!", "레벨업 완료~",
        "이 스테이지 너무 어렵다", "장비 좀 업글해야겠어",
        "스킬 쿨타임 줄이는법 있나요?", "다음 업데이트 언제임",
        "ㅋㅋㅋ 몬스터한테 죽었다", "보스 언제 나와",
        "인벤 꽉 차서 못줍네", "강화 +7 성공!",
    };

    private IEnumerator FakeChatCoroutine()
    {
        // 처음 3초 대기
        yield return new WaitForSecondsRealtime(3f);

        while (true)
        {
            // 15~45초 간격으로 랜덤 채팅
            float waitTime = Random.Range(15f, 45f);
            yield return new WaitForSecondsRealtime(waitTime);

            // 패널이 보일 때만 추가
            if (isVisible)
            {
                int nameIdx = Random.Range(0, FakeNames.Length);
                int msgIdx = Random.Range(0, FakeMessages.Length);
                int colorIdx = Random.Range(0, NameColors.Length);

                AddChatMessage(FakeNames[nameIdx], FakeMessages[msgIdx], NameColors[colorIdx]);
            }
        }
    }
}