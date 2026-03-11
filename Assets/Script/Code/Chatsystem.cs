using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ChatSystem — 채팅창 시스템
///
/// ★ 수정 내역:
///   - enabled=false 추가 (Start() 즉시 차단)
///   - Start() 가드 추가
/// </summary>
public class ChatSystem : MonoBehaviour
{
    public static ChatSystem Instance { get; private set; }

    [Header("채팅 패널 루트")]
    public GameObject chatPanel;

    [Header("채팅창 크기 설정")]
    public float chatPanelHeight = 130f;
    public float chatPanelWidth = 460f;
    public float chatPanelPosY = 10f;

    [Header("메시지 UI")]
    public ScrollRect messageScrollRect;
    public GameObject messagePrefab;
    public Transform messageContent;

    [Header("입력 UI")]
    public TMP_InputField chatInputField;
    public Button sendButton;

    [Header("페이드 설정")]
    public float fadeDuration = 0.2f;

    [Header("메시지 아이템 설정")]
    public float messageItemHeight = 28f;
    public int maxMessages = 50;

    [Header("최소화 버튼")]
    public Button minimizeButton;
    public TextMeshProUGUI minimizeBtnText;

    [Header("높이 설정")]
    public float expandedHeight = 200f;
    public float minimizedHeight = 36f;
    public float resizeDuration = 0.2f;

    private RectTransform chatPanelRect;
    private bool isMinimized = false;
    private Coroutine resizeCoroutine;
    private CanvasGroup canvasGroup;
    private bool isVisible = false;
    private Coroutine fadeCoroutine;
    private List<GameObject> messageObjects = new List<GameObject>();

    private static readonly string[] FakeNames = {
        "검은기사","불꽃마법사","전설용사","어둠사냥꾼",
        "천상여전사","바람도적","얼음술사","폭풍전사"
    };
    private static readonly Color[] NameColors = {
        new Color(1f,0.4f,0.4f), new Color(0.4f,0.8f,1f),
        new Color(0.6f,1f,0.4f), new Color(1f,0.8f,0.2f),
        new Color(0.9f,0.5f,1f), new Color(1f,0.6f,0.2f),
    };
    private static readonly string[] FakeMessages = {
        "몬스터 어디있냐","골드 빨리 모아야되는데","가챠 전설 떴다!!","레벨업 완료~",
        "이 스테이지 너무 어렵다","장비 좀 업글해야겠어","스킬 쿨타임 줄이는법 있나요?",
        "다음 업데이트 언제임","ㅋㅋㅋ 몬스터한테 죽었다","보스 언제 나와",
        "인벤 꽉 차서 못줍네","강화 +7 성공!",
    };

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            enabled = false; // ★ Start() 즉시 차단
            Destroy(gameObject);
            return;
        }

        // ★ Awake에서 즉시 숨김 (Start 전에 1프레임도 보이지 않도록)
        if (chatPanel != null)
            chatPanel.SetActive(false);

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
        if (Instance != this) return; // ★ 중복 차단

        if (chatPanel != null)
        {
            RectTransform rt = chatPanel.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(chatPanelWidth, chatPanelHeight);
        }

        if (messageContent != null)
        {
            var vlg = messageContent.GetComponent<VerticalLayoutGroup>()
                   ?? messageContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 40f;
            vlg.padding = new RectOffset(90, 6, 20, 4);

            var csf = messageContent.GetComponent<ContentSizeFitter>()
                   ?? messageContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // ★ 씬에서 패널이 처음부터 켜져 있으면 isVisible도 true로 맞춰줌
        isVisible = chatPanel != null && chatPanel.activeSelf;

        if (sendButton != null) sendButton.onClick.AddListener(SendMessage);
        if (chatInputField != null) chatInputField.onSubmit.AddListener(_ => SendMessage());
        if (minimizeButton != null) minimizeButton.onClick.AddListener(ToggleMinimize);
        if (minimizeBtnText != null) minimizeBtnText.text = "▲";

        if (chatPanel != null)
            chatPanelRect = chatPanel.GetComponent<RectTransform>();

        AddSystemMessage("채팅에 오신 것을 환영합니다!");
        StartCoroutine(FakeChatCoroutine());
    }

    // ─────────────────────────────────────────
    // 표시 / 숨김
    // ─────────────────────────────────────────
    public void ShowChat()
    {
        if (isVisible || chatPanel == null) return;
        isVisible = true;
        chatPanel.SetActive(true);
        if (canvasGroup != null) { canvasGroup.interactable = true; canvasGroup.blocksRaycasts = true; }
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeTo(1f));
        StartCoroutine(ScrollToBottomNextFrame());
    }

    public void HideChat()
    {
        if (!isVisible || chatPanel == null) return;
        isVisible = false;
        if (canvasGroup != null) { canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutAndDeactivate());
    }

    // ─────────────────────────────────────────
    // 최소화
    // ─────────────────────────────────────────
    public void ToggleMinimize()
    {
        isMinimized = !isMinimized;
        if (minimizeBtnText != null) minimizeBtnText.text = isMinimized ? "▼" : "▲";
        if (messageScrollRect != null) messageScrollRect.gameObject.SetActive(!isMinimized);
        if (resizeCoroutine != null) StopCoroutine(resizeCoroutine);
        resizeCoroutine = StartCoroutine(ResizePanel(isMinimized ? minimizedHeight : expandedHeight));
    }

    private IEnumerator ResizePanel(float targetHeight)
    {
        if (chatPanelRect == null) yield break;
        float start = chatPanelRect.sizeDelta.y, elapsed = 0f;
        while (elapsed < resizeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / resizeDuration), 3f);
            Vector2 s = chatPanelRect.sizeDelta; s.y = Mathf.Lerp(start, targetHeight, t);
            chatPanelRect.sizeDelta = s; yield return null;
        }
        Vector2 f = chatPanelRect.sizeDelta; f.y = targetHeight; chatPanelRect.sizeDelta = f;
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
        string myName = GameManager.Instance != null ? GameManager.Instance.PlayerLevel + "Lv 나" : "나";
        AddChatMessage(myName, text, new Color(1f, 1f, 0.6f));
        chatInputField.text = "";
        chatInputField.ActivateInputField();
        // ★ 전송 시엔 isVisible 관계없이 무조건 스크롤
        StartCoroutine(ScrollToBottomNextFrame());
    }

    public void AddChatMessage(string playerName, string message, Color nameColor)
    {
        if (messagePrefab == null || messageContent == null) return;
        bool wasActive = chatPanel != null && chatPanel.activeSelf;
        if (!wasActive && chatPanel != null) chatPanel.SetActive(true);

        GameObject msgObj = Instantiate(messagePrefab, messageContent);
        TextMeshProUGUI tmp = msgObj.GetComponent<TextMeshProUGUI>()
                          ?? msgObj.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            string hex = ColorUtility.ToHtmlStringRGB(nameColor);
            tmp.text = $"<color=#{hex}>[{playerName}]</color> <color=#FFFFFF>{message}</color>";
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.color = Color.black;
            tmp.fontSize = 16f;
            RectTransform tr = tmp.GetComponent<RectTransform>();
            if (tr != null && tr.gameObject != msgObj)
            { tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(4f, 0f); tr.offsetMax = Vector2.zero; }
        }
        var csf = msgObj.GetComponent<ContentSizeFitter>();
        if (csf != null) Destroy(csf);
        LayoutElement le = msgObj.GetComponent<LayoutElement>() ?? msgObj.AddComponent<LayoutElement>();
        le.minHeight = messageItemHeight; le.preferredHeight = messageItemHeight;

        if (!wasActive && chatPanel != null) chatPanel.SetActive(false);
        RegisterMessage(msgObj);
        // ★ isVisible 플래그 대신 패널 실제 활성 여부로 판단
        bool panelVisible = isVisible || (chatPanel != null && chatPanel.activeSelf);
        if (panelVisible) StartCoroutine(ScrollToBottomNextFrame());
    }

    public void AddSystemMessage(string message)
    {
        if (messagePrefab == null || messageContent == null) return;
        bool wasActive = chatPanel != null && chatPanel.activeSelf;
        if (!wasActive && chatPanel != null) chatPanel.SetActive(true);

        GameObject msgObj = Instantiate(messagePrefab, messageContent);
        TextMeshProUGUI tmp = msgObj.GetComponent<TextMeshProUGUI>()
                          ?? msgObj.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        { tmp.text = $"<color=#FFD700>[시스템] {message}</color>"; tmp.enableWordWrapping = true; tmp.overflowMode = TextOverflowModes.Overflow; tmp.color = Color.white; tmp.fontSize = 16f; }
        var csf = msgObj.GetComponent<ContentSizeFitter>();
        if (csf != null) Destroy(csf);
        LayoutElement le = msgObj.GetComponent<LayoutElement>() ?? msgObj.AddComponent<LayoutElement>();
        le.minHeight = messageItemHeight; le.preferredHeight = messageItemHeight;

        if (!wasActive && chatPanel != null) chatPanel.SetActive(false);
        RegisterMessage(msgObj);
        // ★ isVisible 플래그 대신 패널 실제 활성 여부로 판단
        bool sysPanelVisible = isVisible || (chatPanel != null && chatPanel.activeSelf);
        if (sysPanelVisible) StartCoroutine(ScrollToBottomNextFrame());
    }

    private void RegisterMessage(GameObject msgObj)
    {
        messageObjects.Add(msgObj);
        while (messageObjects.Count > maxMessages)
        { Destroy(messageObjects[0]); messageObjects.RemoveAt(0); }
    }

    private void ScrollToBottom()
    { if (messageScrollRect == null) return; Canvas.ForceUpdateCanvases(); messageScrollRect.verticalNormalizedPosition = 0f; }

    private IEnumerator ScrollToBottomNextFrame()
    { yield return null; Canvas.ForceUpdateCanvases(); if (messageScrollRect != null) messageScrollRect.verticalNormalizedPosition = 0f; }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (canvasGroup == null) yield break;
        float start = canvasGroup.alpha, elapsed = 0f;
        while (elapsed < fadeDuration)
        { elapsed += Time.unscaledDeltaTime; canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, Mathf.Clamp01(elapsed / fadeDuration)); yield return null; }
        canvasGroup.alpha = targetAlpha; fadeCoroutine = null;
    }

    private IEnumerator FadeOutAndDeactivate()
    { yield return StartCoroutine(FadeTo(0f)); if (chatPanel != null) chatPanel.SetActive(false); }

    private IEnumerator FakeChatCoroutine()
    {
        yield return new WaitForSecondsRealtime(3f);
        while (true)
        {
            yield return new WaitForSecondsRealtime(Random.Range(15f, 45f));
            if (isVisible)
                AddChatMessage(
                    FakeNames[Random.Range(0, FakeNames.Length)],
                    FakeMessages[Random.Range(0, FakeMessages.Length)],
                    NameColors[Random.Range(0, NameColors.Length)]);
        }
    }
}