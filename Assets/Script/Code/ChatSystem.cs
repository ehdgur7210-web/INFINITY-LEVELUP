using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ChatSystem — 채팅 UI (뒤끝 SDK 연동 + 하단 최소화 바)
///
/// [5탭] 전체 / 월드 / 길드 / 귓속말 / 시스템
///
/// [최소화 모드]
///   하단 바 (확장 버튼 + 최신 메시지 한 줄)
///   확장 버튼 → 전체 채팅 패널 펼침
///
/// [Inspector 연결]
///   chatPanel, minimizedBar, tabButtons[5],
///   messageScrollRect, messageContent, messagePrefab,
///   chatInputField, sendButton,
///   collapseButton, expandButton, minimizedMessageText,
///   whisperTargetInput, connectionIndicator
/// </summary>
public class ChatSystem : MonoBehaviour
{
    public static ChatSystem Instance { get; private set; }

    // ═══ 채팅 탭 ══════════════════════════════════════════════════

    public enum ChatTab { All = 0, World = 1, Guild = 2, Private = 3, System = 4 }

    private static readonly string[] TabNames = { "전체", "월드", "길드", "귓속말", "시스템" };
    private const int TAB_COUNT = 5;

    // ═══ Inspector 필드 ═══════════════════════════════════════════

    [Header("===== 패널 루트 =====")]
    public GameObject chatPanel;

    [Header("===== 최소화 바 =====")]
    public GameObject minimizedBar;

    [Header("===== 탭 버튼 (전체/월드/길드/귓속말/시스템) =====")]
    public Button[] tabButtons = new Button[TAB_COUNT];

    [Header("===== 탭 색상 =====")]
    [SerializeField] private Color tabActiveColor = new Color(0.35f, 0.38f, 0.50f, 1f);
    [SerializeField] private Color tabInactiveColor = new Color(0.30f, 0.32f, 0.42f, 1f);
    [SerializeField] private Color tabActiveTextColor = Color.white;
    [SerializeField] private Color tabInactiveTextColor = new Color(0.80f, 0.80f, 0.80f);

    [Header("===== 메시지 영역 =====")]
    public ScrollRect messageScrollRect;
    public Transform messageContent;
    public GameObject messagePrefab;

    [Header("===== 구출 요청 프리팹 (선택) =====")]
    public GameObject rescuePrefab;

    [Header("===== 입력 영역 =====")]
    public TMP_InputField chatInputField;
    public Button sendButton;

    [Header("===== 귓속말 대상 입력 (귓속말 탭 전용) =====")]
    public TMP_InputField whisperTargetInput;

    [Header("===== 펼침/축소 버튼 =====")]
    public Button collapseButton;
    public Button expandButton;

    [Header("===== 최소화 바 텍스트 =====")]
    public TextMeshProUGUI minimizedMessageText;

    [Header("===== 연결 상태 표시 (선택) =====")]
    public Image connectionIndicator;
    public TextMeshProUGUI connectionStatusText;

    [Header("===== 메시지 설정 =====")]
    [SerializeField] private float messageItemHeight = 48f;
    [SerializeField] private int maxMessages = 50;
    [SerializeField] private float messageFontSize = 28f;
    [SerializeField] private float avatarSize = 40f;

    [Header("===== 탭 아이콘 (선택) =====")]
    [SerializeField] private Sprite tabIconAll;
    [SerializeField] private Sprite tabIconWorld;
    [SerializeField] private Sprite tabIconGuild;
    [SerializeField] private Sprite tabIconPrivate;
    [SerializeField] private Sprite tabIconSystem;

    [Header("===== 기본 아바타 =====")]
    [Tooltip("채팅 아바타 기본 이미지 (NPC/상대방)")]
    [SerializeField] private Sprite defaultAvatar;

    [Header("===== 채팅 펼칠 때 숨길 패널들 =====")]
    [Tooltip("채팅 펼치면 숨기고, 접으면 복원할 패널 목록")]
    [SerializeField] private GameObject[] hidePanelsOnExpand;

    [Header("===== 채팅 배경 프레임 =====")]
    [Tooltip("채팅 펼침 시 뒤에 표시할 배경 프레임 (반투명 패널)")]
    [SerializeField] private GameObject chatBackgroundFrame;

    // 자동 축소 제거됨 — 축소 버튼으로만 축소

    // ═══ 내부 상태 ════════════════════════════════════════════════

    private bool isExpanded = false;
    private bool isVisible = false;
    private bool useServerChat = false;
    private bool[] panelWasActive; // 채팅 펼치기 전 패널 활성 상태 기억

    private string whisperTargetNickname;
    private ChatTab currentTab = ChatTab.All;
    private RectTransform chatPanelRect;
    private Coroutine fakeChatCoroutine;

    // 탭별 메시지 리스트
    private Dictionary<ChatTab, List<ChatMessageData>> tabMessages
        = new Dictionary<ChatTab, List<ChatMessageData>>();

    private List<GameObject> activeMessageObjects = new List<GameObject>();

    // ── 메시지 데이터 ─────────────────────────────────────────

    private class ChatMessageData
    {
        public enum MsgType { Normal, System, Rescue }
        public MsgType type;
        public string senderName;
        public string message;
        public Color nameColor;
        public string timestamp;
        public bool isMe;
        public int combatPower;
        public Action onRescueClick;
        public Sprite avatar;
    }

    // ── NPC 더미 (오프라인 폴백) ──────────────────────────────

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

    // ═══════════════════════════════════════════════════════════════
    //  Unity 생명주기
    // ═══════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        // 탭별 리스트 초기화
        for (int i = 0; i < TAB_COUNT; i++)
            tabMessages[(ChatTab)i] = new List<ChatMessageData>();

        // 초기 상태: chatPanel 활성 유지 (자식 제어), 내용 숨김
        if (chatPanel != null) chatPanel.SetActive(true);
        SetExpandedContentVisible(false);
        if (minimizedBar != null) minimizedBar.SetActive(false);
        if (chatBackgroundFrame != null) chatBackgroundFrame.SetActive(false);
    }

    void Start()
    {
        if (Instance != this) return;

        if (chatPanel != null)
            chatPanelRect = chatPanel.GetComponent<RectTransform>();

        // Content 레이아웃 보장
        if (messageContent != null)
        {
            var vlg = messageContent.GetComponent<VerticalLayoutGroup>()
                   ?? messageContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(8, 8, 8, 8);

            var csf = messageContent.GetComponent<ContentSizeFitter>()
                   ?? messageContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // 버튼 바인딩
        SetupTabButtons();
        if (sendButton != null) sendButton.onClick.AddListener(OnSendButtonClicked);
        if (chatInputField != null) chatInputField.onSubmit.AddListener(_ => OnSendButtonClicked());
        if (collapseButton != null) collapseButton.onClick.AddListener(Collapse);
        if (expandButton != null) expandButton.onClick.AddListener(Expand);

        // 뒤끝 채팅 연동
        ConnectToBackendChat();

        AddSystemMessage("채팅에 오신 것을 환영합니다!");

        isExpanded = false;
        isVisible = false;
    }

    // Update 제거 — 바깥 클릭 자동 축소 기능 삭제
    // 축소는 축소 버튼(collapseButton)으로만 가능

    void OnDestroy()
    {
        if (BackendChatManager.Instance != null)
        {
            BackendChatManager.OnChatMessageReceived -= OnServerMessage;
            BackendChatManager.OnConnectionChanged -= OnConnectionStatusChanged;
        }
        if (Instance == this) Instance = null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  뒤끝 채팅 연동
    // ═══════════════════════════════════════════════════════════════

    private void ConnectToBackendChat()
    {
        if (BackendChatManager.Instance != null)
        {
            BackendChatManager.OnChatMessageReceived += OnServerMessage;
            BackendChatManager.OnConnectionChanged += OnConnectionStatusChanged;
        }

        bool loggedIn = BackendManager.Instance != null && BackendManager.Instance.IsLoggedIn;
        bool chatReady = BackendChatManager.Instance != null;
        bool chatConnected = chatReady && BackendChatManager.Instance.IsConnected;

        Debug.Log($"[ChatSystem] 연결 체크 — 로그인:{loggedIn}, ChatManager:{chatReady}, Connected:{chatConnected}");

        if (chatReady && loggedIn)
        {
            if (chatConnected)
            {
                useServerChat = true;
                BackendChatManager.Instance.RejoinDefaultChannels();
            }
            else
            {
                useServerChat = false;
                BackendChatManager.Instance.ConnectChat();
            }
        }
        else
        {
            useServerChat = false;
        }

        UpdateConnectionUI();

        if (!useServerChat)
            fakeChatCoroutine = StartCoroutine(FakeChatCoroutine());
    }

    private void OnConnectionStatusChanged(bool connected)
    {
        useServerChat = connected;
        UpdateConnectionUI();

        if (connected)
        {
            if (fakeChatCoroutine != null) { StopCoroutine(fakeChatCoroutine); fakeChatCoroutine = null; }
            AddSystemMessage("서버 채팅에 연결되었습니다.");
        }
        else
        {
            AddSystemMessage("서버 연결이 끊어졌습니다. 오프라인 모드로 전환합니다.");
            if (fakeChatCoroutine == null) fakeChatCoroutine = StartCoroutine(FakeChatCoroutine());
        }
    }

    private void UpdateConnectionUI()
    {
        if (connectionIndicator != null)
            connectionIndicator.color = useServerChat
                ? new Color(0.3f, 0.9f, 0.3f)
                : new Color(0.9f, 0.3f, 0.3f);

        if (connectionStatusText != null)
            connectionStatusText.text = useServerChat ? "온라인" : "오프라인";
    }

    /// <summary>서버 메시지 수신 콜백</summary>
    private void OnServerMessage(BackendChatManager.ChatMessage msg)
    {
        bool isMe = BackendChatManager.Instance != null
                  && BackendChatManager.Instance.IsMyMessage(msg);

        ChatTab tab = msg.channel switch
        {
            BackendChatManager.ChannelType.World   => ChatTab.World,
            BackendChatManager.ChannelType.Guild   => ChatTab.Guild,
            BackendChatManager.ChannelType.Private => ChatTab.Private,
            _ => ChatTab.World
        };

        Color nameColor = isMe
            ? new Color(1f, 1f, 0.6f)
            : NameColors[Mathf.Abs(msg.senderNickname.GetHashCode()) % NameColors.Length];

        if (msg.isSystem)
        {
            AddSystemMessage(msg.message);
            return;
        }

        if (msg.isRescue)
        {
            var data = new ChatMessageData
            {
                type = ChatMessageData.MsgType.Rescue,
                senderName = msg.senderNickname,
                combatPower = msg.combatPower,
                nameColor = nameColor,
                timestamp = msg.timestamp,
                isMe = isMe
            };
            AddToTab(tab, data);
            AddToAllTab(data);
            string cpStr = RankingFormatUtil.FormatK(msg.combatPower);
            UpdateMinimizedText($"[{GetTabDisplayName(tab)}] {msg.senderNickname}({cpStr}) 구출 요청!");
            return;
        }

        // 귓속말 수신 시 답장 대상 자동 설정
        if (tab == ChatTab.Private && !isMe)
        {
            whisperTargetNickname = msg.senderNickname;
            if (whisperTargetInput != null)
                whisperTargetInput.text = msg.senderNickname;
        }

        var normalData = new ChatMessageData
        {
            type = ChatMessageData.MsgType.Normal,
            senderName = msg.senderNickname,
            message = msg.message,
            nameColor = nameColor,
            timestamp = msg.timestamp,
            isMe = isMe
        };
        AddToTab(tab, normalData);
        AddToAllTab(normalData);
        UpdateMinimizedText($"[{GetTabDisplayName(tab)}] {msg.senderNickname}: {msg.message}");
    }

    // ═══════════════════════════════════════════════════════════════
    //  외부 API
    // ═══════════════════════════════════════════════════════════════

    public void ShowChat()
    {
        if (isVisible) return;
        isVisible = true;

        if (chatPanel != null) chatPanel.SetActive(true);

        if (isExpanded)
            ShowExpandedPanel();
        else
            ShowMinimizedBar();
    }

    public void HideChat()
    {
        if (!isVisible) return;
        isVisible = false;

        SetExpandedContentVisible(false);
        if (minimizedBar != null) minimizedBar.SetActive(false);
    }

    public void AddChatMessage(string playerName, string message, Color nameColor)
    {
        var data = new ChatMessageData
        {
            type = ChatMessageData.MsgType.Normal,
            senderName = playerName,
            message = message,
            nameColor = nameColor,
            timestamp = DateTime.Now.ToString("HH:mm"),
            isMe = false
        };
        AddToTab(ChatTab.World, data);
        AddToAllTab(data);
        UpdateMinimizedText($"[월드] {playerName}: {message}");
    }

    public void AddSystemMessage(string message)
    {
        var data = new ChatMessageData
        {
            type = ChatMessageData.MsgType.System,
            message = message,
            timestamp = DateTime.Now.ToString("HH:mm")
        };

        AddToTab(ChatTab.System, data);
        AddToAllTab(data);
        UpdateMinimizedText($"[시스템] {message}");
    }

    public void AddRescueMessage(string playerName, int combatPower,
                                  Color nameColor, Action onRescue = null)
    {
        var data = new ChatMessageData
        {
            type = ChatMessageData.MsgType.Rescue,
            senderName = playerName,
            combatPower = combatPower,
            nameColor = nameColor,
            onRescueClick = onRescue,
            timestamp = DateTime.Now.ToString("HH:mm")
        };
        AddToTab(ChatTab.World, data);
        AddToAllTab(data);
        string cpStr = RankingFormatUtil.FormatK(combatPower);
        UpdateMinimizedText($"[구출] {playerName}({cpStr}) 구출 요청!");
    }

    public void AddMessageToTab(ChatTab tab, string playerName, string message, Color nameColor)
    {
        var data = new ChatMessageData
        {
            type = ChatMessageData.MsgType.Normal,
            senderName = playerName,
            message = message,
            nameColor = nameColor,
            timestamp = DateTime.Now.ToString("HH:mm")
        };
        AddToTab(tab, data);
        AddToAllTab(data);
        UpdateMinimizedText($"[{GetTabDisplayName(tab)}] {playerName}: {message}");
    }

    // ═══════════════════════════════════════════════════════════════
    //  메시지 전송
    // ═══════════════════════════════════════════════════════════════

    private void OnSendButtonClicked()
    {
        if (chatInputField == null) return;
        string text = chatInputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        string myName = "나";
        if (BackendManager.Instance != null && !string.IsNullOrEmpty(BackendManager.Instance.LoggedInUsername))
            myName = BackendManager.Instance.LoggedInUsername;
        else if (GameManager.Instance != null)
            myName = $"Lv.{GameManager.Instance.PlayerLevel} 나";

        chatInputField.text = "";
        chatInputField.ActivateInputField();

        // 시스템 탭에서는 전송 불가
        if (currentTab == ChatTab.System)
        {
            AddSystemMessage("시스템 탭에서는 메시지를 보낼 수 없습니다.");
            return;
        }

        // 전체 탭에서 보내면 월드로 전송
        ChatTab sendTab = (currentTab == ChatTab.All) ? ChatTab.World : currentTab;

        if (useServerChat && BackendChatManager.Instance != null)
        {
            if (sendTab == ChatTab.Private || text.StartsWith("/w "))
            {
                HandleWhisperSend(myName, text);
                return;
            }

            BackendChatManager.ChannelType serverChannel = sendTab switch
            {
                ChatTab.World => BackendChatManager.ChannelType.World,
                ChatTab.Guild => BackendChatManager.ChannelType.Guild,
                _ => BackendChatManager.ChannelType.World
            };

            BackendChatManager.Instance.SendChatMessage(serverChannel, text, success =>
            {
                if (!success)
                {
                    AddLocalMyMessage(myName, text);
                    AddSystemMessage("메시지 전송에 실패했습니다.");
                }
            });
        }
        else
        {
            if (sendTab == ChatTab.Private || text.StartsWith("/w "))
                HandleWhisperSend(myName, text);
            else
                AddLocalMyMessage(myName, text);
        }

        TutorialManager.Instance?.OnActionCompleted("ChatMessageSent");
    }

    private void HandleWhisperSend(string myName, string text)
    {
        string whisperTarget = null;
        string whisperMsg = text;

        if (text.StartsWith("/w "))
        {
            string afterCmd = text.Substring(3).TrimStart();
            int spaceIdx = afterCmd.IndexOf(' ');
            if (spaceIdx > 0)
            {
                whisperTarget = afterCmd.Substring(0, spaceIdx);
                whisperMsg = afterCmd.Substring(spaceIdx + 1).Trim();
            }
            else
            {
                AddSystemMessage("사용법: /w 닉네임 메시지");
                return;
            }
        }
        else if (whisperTargetInput != null && !string.IsNullOrEmpty(whisperTargetInput.text))
        {
            whisperTarget = whisperTargetInput.text.Trim();
        }
        else if (!string.IsNullOrEmpty(whisperTargetNickname))
        {
            whisperTarget = whisperTargetNickname;
        }
        else
        {
            AddSystemMessage("귓속말 대상을 지정해주세요. (/w 닉네임 메시지)");
            return;
        }

        whisperTargetNickname = whisperTarget;
        if (whisperTargetInput != null) whisperTargetInput.text = whisperTarget;

        if (useServerChat && BackendChatManager.Instance != null)
        {
            BackendChatManager.Instance.SendWhisperMessage(whisperTarget, whisperMsg, success =>
            {
                if (success)
                    AddLocalMyMessage(myName, $"[→ {whisperTarget}] {whisperMsg}");
                else
                    AddSystemMessage("귓속말 전송에 실패했습니다.");
            });
        }
        else
        {
            AddLocalMyMessage(myName, $"[→ {whisperTarget}] {whisperMsg}");
        }
    }

    private void AddLocalMyMessage(string myName, string text)
    {
        ChatTab sendTab = (currentTab == ChatTab.All) ? ChatTab.World : currentTab;
        var data = new ChatMessageData
        {
            type = ChatMessageData.MsgType.Normal,
            senderName = myName,
            message = text,
            nameColor = new Color(1f, 1f, 0.6f),
            timestamp = DateTime.Now.ToString("HH:mm"),
            isMe = true
        };
        AddToTab(sendTab, data);
        AddToAllTab(data);
        UpdateMinimizedText($"[{GetTabDisplayName(sendTab)}] {myName}: {text}");
        StartCoroutine(ScrollToBottomNextFrame());
    }

    // ═══════════════════════════════════════════════════════════════
    //  펼침 / 축소
    // ═══════════════════════════════════════════════════════════════

    public void Collapse()
    {
        if (!isExpanded) return;
        isExpanded = false;
        if (isVisible) ShowMinimizedBar();
        RestoreHiddenPanels();
        TutorialManager.Instance?.OnActionCompleted("ChatCollapse");
    }

    public void Expand()
    {
        if (isExpanded) return;
        isExpanded = true;
        HidePanelsForChat();
        if (isVisible) ShowExpandedPanel();
        TutorialManager.Instance?.OnActionCompleted("ChatExpand");
    }

    // ★ 동료 핫바 숨김 상태 추적
    private bool _companionHotbarWasActive = false;
    private GameObject _companionHotbarPanel = null;

    /// <summary>채팅 펼칠 때 — 하단 패널들 + 동료 핫바 숨기기 (상태 기억)</summary>
    private void HidePanelsForChat()
    {
        // Inspector에 연결된 패널들 숨기기
        if (hidePanelsOnExpand != null && hidePanelsOnExpand.Length > 0)
        {
            panelWasActive = new bool[hidePanelsOnExpand.Length];
            for (int i = 0; i < hidePanelsOnExpand.Length; i++)
            {
                if (hidePanelsOnExpand[i] != null)
                {
                    panelWasActive[i] = hidePanelsOnExpand[i].activeSelf;
                    hidePanelsOnExpand[i].SetActive(false);
                }
            }
        }

        // ★ 동료 핫바 자동 숨김 (Inspector 연결 없이도 동작)
        if (_companionHotbarPanel == null && CompanionHotbarManager.Instance != null)
            _companionHotbarPanel = CompanionHotbarManager.Instance.hotbarParent?.gameObject
                                 ?? CompanionHotbarManager.Instance.gameObject;

        if (_companionHotbarPanel != null)
        {
            _companionHotbarWasActive = _companionHotbarPanel.activeSelf;
            _companionHotbarPanel.SetActive(false);
        }

        // ★ 배경 프레임 활성화
        if (chatBackgroundFrame != null)
            chatBackgroundFrame.SetActive(true);
    }

    /// <summary>채팅 접을 때 — 원래 활성이었던 패널만 복원</summary>
    private void RestoreHiddenPanels()
    {
        // Inspector 패널 복원
        if (hidePanelsOnExpand != null && panelWasActive != null)
        {
            for (int i = 0; i < hidePanelsOnExpand.Length; i++)
            {
                if (hidePanelsOnExpand[i] != null && i < panelWasActive.Length)
                    hidePanelsOnExpand[i].SetActive(panelWasActive[i]);
            }
            panelWasActive = null;
        }

        // ★ 동료 핫바 복원
        if (_companionHotbarPanel != null)
        {
            _companionHotbarPanel.SetActive(_companionHotbarWasActive);
            _companionHotbarWasActive = false;
        }

        // ★ 배경 프레임 비활성화
        if (chatBackgroundFrame != null)
            chatBackgroundFrame.SetActive(false);
    }

    public void ToggleMinimize()
    {
        if (isExpanded) Collapse();
        else Expand();
    }

    private void ShowMinimizedBar()
    {
        SetExpandedContentVisible(false);
        if (minimizedBar != null) minimizedBar.SetActive(true);
    }

    private void SetExpandedContentVisible(bool visible)
    {
        if (chatPanel == null) return;
        foreach (Transform child in chatPanel.transform)
        {
            if (minimizedBar != null && child.gameObject == minimizedBar) continue;
            child.gameObject.SetActive(visible);
        }
    }

    private void ShowExpandedPanel()
    {
        if (chatPanel == null) return;
        chatPanel.SetActive(true);

        if (minimizedBar != null) minimizedBar.SetActive(false);
        SetExpandedContentVisible(true);

        UpdateWhisperInputVisibility();
        RefreshMessageDisplay();
        StartCoroutine(ScrollToBottomNextFrame());
    }

    // ═══════════════════════════════════════════════════════════════
    //  탭 시스템
    // ═══════════════════════════════════════════════════════════════

    private void SetupTabButtons()
    {
        Sprite[] tabIcons = { tabIconAll, tabIconWorld, tabIconGuild, tabIconPrivate, tabIconSystem };

        for (int i = 0; i < tabButtons.Length && i < TAB_COUNT; i++)
        {
            if (tabButtons[i] == null) continue;
            int tabIdx = i;
            tabButtons[i].onClick.AddListener(() => OnTabClicked((ChatTab)tabIdx));

            // 텍스트 설정
            var txt = tabButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.text = TabNames[i];

            // 탭 아이콘 추가 (Inspector에 Sprite가 연결된 경우)
            if (i < tabIcons.Length && tabIcons[i] != null)
            {
                // 버튼 안에 아이콘 Image가 있으면 설정
                Transform iconTf = tabButtons[i].transform.Find("Icon");
                if (iconTf != null)
                {
                    Image iconImg = iconTf.GetComponent<Image>();
                    if (iconImg != null)
                        iconImg.sprite = tabIcons[i];
                }
            }
        }
        UpdateTabVisuals();
    }

    private void OnTabClicked(ChatTab tab)
    {
        SoundManager.Instance?.PlayButtonClick();
        currentTab = tab;
        UpdateTabVisuals();
        UpdateWhisperInputVisibility();
        RefreshMessageDisplay();

        // 서버 채널 입장 (전체/시스템 탭은 스킵)
        if (useServerChat && BackendChatManager.Instance != null
            && tab != ChatTab.All && tab != ChatTab.System)
        {
            BackendChatManager.ChannelType ch = tab switch
            {
                ChatTab.World   => BackendChatManager.ChannelType.World,
                ChatTab.Guild   => BackendChatManager.ChannelType.Guild,
                ChatTab.Private => BackendChatManager.ChannelType.Private,
                _               => BackendChatManager.ChannelType.World
            };
            BackendChatManager.Instance.JoinChannel(ch);
        }

        StartCoroutine(ScrollToBottomNextFrame());
    }

    private void UpdateTabVisuals()
    {
        for (int i = 0; i < tabButtons.Length && i < TAB_COUNT; i++)
        {
            if (tabButtons[i] == null) continue;
            bool active = ((int)currentTab == i);

            // ★ 버튼 배경 색상은 건드리지 않음 — 원본 디자인 유지

            // ★ Outline으로만 선택된 탭 표시 (없으면 자동 추가)
            var outline = tabButtons[i].GetComponent<Outline>();
            if (outline == null)
                outline = tabButtons[i].gameObject.AddComponent<Outline>();

            outline.enabled = active;
            if (active)
            {
                outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f); // 금색 테두리
                outline.effectDistance = new Vector2(2f, 2f);
            }

            // 텍스트 색상: 선택된 탭은 밝게, 나머지는 살짝 어둡게
            var txt = tabButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.color = active ? Color.white : new Color(0.75f, 0.75f, 0.75f);
        }
    }

    private void UpdateWhisperInputVisibility()
    {
        if (whisperTargetInput != null)
            whisperTargetInput.gameObject.SetActive(currentTab == ChatTab.Private);
    }

    // ═══════════════════════════════════════════════════════════════
    //  메시지 관리
    // ═══════════════════════════════════════════════════════════════

    /// <summary>특정 탭에 메시지 추가 (전체 탭 제외)</summary>
    private void AddToTab(ChatTab tab, ChatMessageData data)
    {
        if (tab == ChatTab.All) return; // 전체 탭은 AddToAllTab으로만
        if (!tabMessages.ContainsKey(tab)) return;

        var list = tabMessages[tab];
        list.Add(data);
        while (list.Count > maxMessages) list.RemoveAt(0);

        if (tab == currentTab && isExpanded && isVisible)
        {
            SpawnSingleMessage(data);
            StartCoroutine(ScrollToBottomNextFrame());
        }
    }

    /// <summary>전체 탭에 메시지 추가 (모든 메시지가 여기에도 들어감)</summary>
    private void AddToAllTab(ChatMessageData data)
    {
        if (!tabMessages.ContainsKey(ChatTab.All)) return;

        var list = tabMessages[ChatTab.All];
        list.Add(data);
        while (list.Count > maxMessages) list.RemoveAt(0);

        if (currentTab == ChatTab.All && isExpanded && isVisible)
        {
            SpawnSingleMessage(data);
            StartCoroutine(ScrollToBottomNextFrame());
        }
    }

    private void RefreshMessageDisplay()
    {
        ClearDisplayedMessages();
        if (!tabMessages.ContainsKey(currentTab)) return;

        var list = tabMessages[currentTab];
        for (int i = 0; i < list.Count; i++)
            SpawnSingleMessage(list[i]);
    }

    // ═══════════════════════════════════════════════════════════════
    //  메시지 UI 렌더링
    // ═══════════════════════════════════════════════════════════════

    private void SpawnSingleMessage(ChatMessageData data)
    {
        if (messageContent == null) return;
        switch (data.type)
        {
            case ChatMessageData.MsgType.Normal:  SpawnNormalMessage(data); break;
            case ChatMessageData.MsgType.System:  SpawnSystemMessage(data); break;
            case ChatMessageData.MsgType.Rescue:  SpawnRescueMessage(data); break;
        }
    }

    private void SpawnNormalMessage(ChatMessageData data)
    {
        if (messagePrefab == null || messageContent == null) return;

        GameObject msgObj = Instantiate(messagePrefab, messageContent);
        TextMeshProUGUI tmp = msgObj.GetComponent<TextMeshProUGUI>()
                          ?? msgObj.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            string hex = ColorUtility.ToHtmlStringRGB(data.nameColor);
            string timeStr = !string.IsNullOrEmpty(data.timestamp)
                ? $"<color=#999999><size={messageFontSize * 0.7f:F0}>{data.timestamp}</size></color> " : "";
            string msgColor = (data.message != null && data.message.StartsWith("[→")) ? "#FFAAFF" : "#EEEEEEFF";
            tmp.text = $"{timeStr}<color=#{hex}>{data.senderName}</color>  <color={msgColor}>{data.message}</color>";
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.fontSize = messageFontSize;
            tmp.alignment = TextAlignmentOptions.Left;
        }

        ConfigureMessageLayout(msgObj);
        activeMessageObjects.Add(msgObj);
    }

    private void SpawnSystemMessage(ChatMessageData data)
    {
        if (messagePrefab == null) return;

        GameObject msgObj = Instantiate(messagePrefab, messageContent);
        TextMeshProUGUI tmp = msgObj.GetComponent<TextMeshProUGUI>()
                          ?? msgObj.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = $"<color=#FFD700>[시스템] {data.message}</color>";
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.fontSize = messageFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        ConfigureMessageLayout(msgObj);
        activeMessageObjects.Add(msgObj);
    }

    private void SpawnRescueMessage(ChatMessageData data)
    {
        GameObject prefab = rescuePrefab != null ? rescuePrefab : messagePrefab;
        if (prefab == null) return;

        GameObject msgObj = Instantiate(prefab, messageContent);

        if (rescuePrefab != null)
        {
            var nameText = FindChildTMP(msgObj, "NameText");
            var powerText = FindChildTMP(msgObj, "PowerText");
            var rescueBtn = FindChildButton(msgObj, "RescueButton");

            if (nameText != null)
            {
                string hex = ColorUtility.ToHtmlStringRGB(data.nameColor);
                nameText.text = $"<color=#{hex}>{data.senderName}</color>";
            }
            if (powerText != null)
                powerText.text = RankingFormatUtil.FormatK(data.combatPower);
            if (rescueBtn != null && data.onRescueClick != null)
                rescueBtn.onClick.AddListener(() => data.onRescueClick());
        }
        else
        {
            string cpStr = RankingFormatUtil.FormatK(data.combatPower);
            TextMeshProUGUI tmp = msgObj.GetComponent<TextMeshProUGUI>()
                              ?? msgObj.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                string hex = ColorUtility.ToHtmlStringRGB(data.nameColor);
                tmp.text = $"<color=#{hex}>[{data.senderName}]</color> <color=#FF9999>전투력 {cpStr} — 구출 요청!</color>";
                tmp.enableWordWrapping = true;
                tmp.fontSize = messageFontSize;
            }
        }

        ConfigureMessageLayout(msgObj);
        activeMessageObjects.Add(msgObj);
    }

    private void ConfigureMessageLayout(GameObject msgObj)
    {
        // RectTransform을 가로 전체 stretch로 설정
        RectTransform rt = msgObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
        }

        // ContentSizeFitter — 높이만 자동 (텍스트 줄바꿈 시 늘어남)
        var csf = msgObj.GetComponent<ContentSizeFitter>()
               ?? msgObj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // LayoutElement — 최소 높이만 보장
        LayoutElement le = msgObj.GetComponent<LayoutElement>() ?? msgObj.AddComponent<LayoutElement>();
        le.minHeight = messageItemHeight;
        le.preferredHeight = -1; // 자동
    }

    private void ClearDisplayedMessages()
    {
        for (int i = activeMessageObjects.Count - 1; i >= 0; i--)
        {
            if (activeMessageObjects[i] != null) Destroy(activeMessageObjects[i]);
        }
        activeMessageObjects.Clear();
    }

    private void UpdateMinimizedText(string text)
    {
        if (minimizedMessageText != null)
            minimizedMessageText.text = text;
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (messageScrollRect != null)
            messageScrollRect.verticalNormalizedPosition = 0f;
    }

    // ═══════════════════════════════════════════════════════════════
    //  NPC 자동 채팅 (오프라인 폴백)
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator FakeChatCoroutine()
    {
        yield return new WaitForSecondsRealtime(3f);

        while (true)
        {
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(15f, 45f));
            if (useServerChat) yield break;

            string name = FakeNames[UnityEngine.Random.Range(0, FakeNames.Length)];
            string msg = FakeMessages[UnityEngine.Random.Range(0, FakeMessages.Length)];
            Color color = NameColors[UnityEngine.Random.Range(0, NameColors.Length)];
            AddChatMessage(name, msg, color);

            // 구출 요청 (20%)
            if (UnityEngine.Random.value < 0.2f)
            {
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(5f, 15f));
                if (useServerChat) yield break;
                string rName = FakeNames[UnityEngine.Random.Range(0, FakeNames.Length)];
                int rPower = UnityEngine.Random.Range(800, 250000);
                Color rColor = NameColors[UnityEngine.Random.Range(0, NameColors.Length)];
                AddRescueMessage(rName, rPower, rColor);
            }

            // 길드 메시지 (15%)
            if (UnityEngine.Random.value < 0.15f)
            {
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(3f, 10f));
                if (useServerChat) yield break;
                string gName = FakeNames[UnityEngine.Random.Range(0, FakeNames.Length)];
                string gMsg = FakeMessages[UnityEngine.Random.Range(0, FakeMessages.Length)];
                Color gColor = NameColors[UnityEngine.Random.Range(0, NameColors.Length)];
                AddMessageToTab(ChatTab.Guild, gName, gMsg, gColor);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  유틸
    // ═══════════════════════════════════════════════════════════════

    private string GetTabDisplayName(ChatTab tab)
    {
        int idx = (int)tab;
        return (idx >= 0 && idx < TabNames.Length) ? TabNames[idx] : "전체";
    }

    private TextMeshProUGUI FindChildTMP(GameObject parent, string childName)
    {
        Transform t = parent.transform.Find(childName);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    private Button FindChildButton(GameObject parent, string childName)
    {
        Transform t = parent.transform.Find(childName);
        return t != null ? t.GetComponent<Button>() : null;
    }
}
