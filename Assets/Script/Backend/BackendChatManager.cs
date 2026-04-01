using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if BACKND_CHAT
using BackndChat;
#endif

/// <summary>
/// BackendChatManager — 채팅 매니저 (DontDestroyOnLoad)
///
/// [BACKND_CHAT 정의 시]
///   BackndChat SDK 연동 (ChatClient + IChatClientListener)
///   Project Settings > Scripting Define Symbols에 BACKND_CHAT 추가 필요
///
/// [BACKND_CHAT 미정의 시]
///   NPC 로컬 더미 채팅 (기존 동작 유지)
///
/// [아키텍처]
///   BackendChatManager (DDOL, 데이터) ←→ ChatSystem (씬 로컬, UI)
///
/// [설치 순서]
///   1. BackndChat-1.4.0.unitypackage Import (Backend.dll 체크 해제)
///   2. 뒤끝 콘솔 > Chat > 설정에서 Chat UUID 확인
///   3. Unity > The Backend > Edit Chat Settings에서 Chat UUID 입력
///   4. Unity > The Backend > Edit Settings에서 Client App ID + Signature Key 입력
///   5. Project Settings > Player > Scripting Define Symbols에 BACKND_CHAT 추가
/// </summary>
public class BackendChatManager : MonoBehaviour
#if BACKND_CHAT
    , IChatClientListener
#endif
{
    public static BackendChatManager Instance { get; private set; }

    public enum ChannelType { World, Guild, Party, Private }

    [Serializable]
    public class ChatMessage
    {
        public ChannelType channel;
        public string senderNickname;
        public string senderInDate;
        public string message;
        public string timestamp;
        public bool isSystem;
        public bool isRescue;
        public int combatPower;
    }

    public static event Action<ChatMessage> OnChatMessageReceived;
    public static event Action<ChannelType, bool> OnChannelJoined;
    public static event Action<bool> OnConnectionChanged;

    public bool IsConnected { get; private set; }
    public HashSet<ChannelType> JoinedChannels { get; private set; } = new HashSet<ChannelType>();

    [Header("===== 채팅 서버 설정 =====")]
    [SerializeField] private string channelGroup = "global";
    [SerializeField] private string serverName = "server-1";
    [SerializeField] private string chatAvatar = "default";

#if BACKND_CHAT
    private ChatClient _chatClient;
    private string _worldChannelName;
    private UInt64 _worldChannelNumber;
    private string _guildChannelName;
    private UInt64 _guildChannelNumber;
    private string _partyChannelName;
    private UInt64 _partyChannelNumber;
    private string _myGamerName;
#endif

    private static readonly string[] NpcNames = {
        "검은기사", "불꽃마법사", "전설용사", "어둠사냥꾼",
        "천상여전사", "바람도적", "얼음술사", "폭풍전사",
        "세나", "카이", "리온", "미라"
    };
    private static readonly string[] NpcMessages = {
        "몬스터 어디있냐", "골드 빨리 모아야되는데", "가챠 전설 떴다!!",
        "레벨업 완료~", "이 스테이지 너무 어렵다", "장비 좀 업글해야겠어",
        "스킬 쿨타임 줄이는법 있나요?", "다음 업데이트 언제임",
        "ㅋㅋㅋ 몬스터한테 죽었다", "보스 언제 나와",
        "인벤 꽉 차서 못줍네", "강화 +7 성공!", "파티 구합니다",
        "이 던전 같이 돌 사람?", "드디어 레전드 장비 획득!"
    };
    private static readonly string[] GuildMessages = {
        "길드 레이드 참여해주세요!", "오늘 길드 미션 다 했나요?",
        "길드 출석 완료~", "새 길원 환영합니다!", "보스전 준비 완료"
    };
    private static readonly string[] RescueMessages = {
        "도와주세요~", "구출 부탁드려요!", "몬스터에게 잡혔어요!"
    };
    private Coroutine npcChatCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Debug.Log("[ManagerInit] BackendChatManager가 생성되었습니다.");
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
#if BACKND_CHAT
        _chatClient?.Update();
#endif
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
#if BACKND_CHAT
            _chatClient?.Dispose();
            _chatClient = null;
#endif
            Instance = null;
        }
    }

    void OnApplicationQuit()
    {
#if BACKND_CHAT
        _chatClient?.Dispose();
        _chatClient = null;
#endif
    }

    public void ConnectChat()
    {
#if BACKND_CHAT
        Debug.Log("[BackendChat] 채팅 초기화 시작 (BackndChat SDK 모드)");
        try
        {
            string nickname = BackendManager.Instance?.LoggedInUsername ?? "Player";
            int playerLevel = 1;
            int cp = 0;
            if (GameManager.Instance != null) playerLevel = GameManager.Instance.PlayerLevel;
            if (CombatPowerManager.Instance != null) cp = CombatPowerManager.Instance.CombatPower;

            var args = new ChatClientArguments
            {
                Avatar = chatAvatar,
                Metadata = new Dictionary<string, string>
                {
                    { "Level", playerLevel.ToString() },
                    { "CombatPower", cp.ToString() },
                    { "Server", serverName }
                }
            };
            _chatClient = new ChatClient(this, args);
            _myGamerName = nickname;
            Debug.Log($"[BackendChat] ChatClient 생성 완료 (nickname={nickname})");
            _chatClient.SendJoinOpenChannel(channelGroup, serverName);
            Debug.Log($"[BackendChat] 채널 입장 요청: group={channelGroup}, server={serverName}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BackendChat] ChatClient 초기화 실패: {e.Message}");
            FallbackToLocalMode();
        }
#else
        Debug.Log("[BackendChat] 채팅 초기화 (BACKND_CHAT 미정의 — NPC 로컬 모드)");
        FallbackToLocalMode();
#endif
    }

    public void DisconnectChat()
    {
        Debug.Log("[BackendChat] 채팅 해제");
        StopNpcChat();
#if BACKND_CHAT
        _chatClient?.Dispose();
        _chatClient = null;
        _worldChannelName = null;
        _guildChannelName = null;
        _partyChannelName = null;
#endif
        IsConnected = false;
        JoinedChannels.Clear();
        OnConnectionChanged?.Invoke(false);
    }

    private void FallbackToLocalMode()
    {
        IsConnected = false;
        JoinedChannels.Add(ChannelType.World);
        OnChannelJoined?.Invoke(ChannelType.World, true);
        StartNpcChat();
        OnConnectionChanged?.Invoke(false);
    }

    public void JoinChannel(ChannelType channel)
    {
        if (JoinedChannels.Contains(channel)) return;
#if BACKND_CHAT
        if (_chatClient != null && (channel == ChannelType.Guild || channel == ChannelType.Party))
        {
            _chatClient.SendJoinOpenChannel(GetChannelGroup(channel), serverName);
            Debug.Log($"[BackendChat] {channel} 채널 입장 요청");
            return;
        }
#endif
        JoinedChannels.Add(channel);
        Debug.Log($"[BackendChat] 채널 입장 (로컬): {channel}");
        OnChannelJoined?.Invoke(channel, true);
    }

    public void LeaveChannel(ChannelType channel)
    {
        if (!JoinedChannels.Contains(channel)) return;
        JoinedChannels.Remove(channel);
        Debug.Log($"[BackendChat] 채널 퇴장: {channel}");
    }

    public void LeaveAllChannels()
    {
        foreach (var ch in new List<ChannelType>(JoinedChannels)) LeaveChannel(ch);
    }

    public void RejoinDefaultChannels() { JoinChannel(ChannelType.World); }

    public void SendChatMessage(ChannelType channel, string message, Action<bool> onComplete = null)
    {
        if (string.IsNullOrEmpty(message)) { onComplete?.Invoke(false); return; }
        if (!JoinedChannels.Contains(channel))
        {
            Debug.LogWarning($"[BackendChat] 미입장 채널: {channel}");
            onComplete?.Invoke(false); return;
        }
#if BACKND_CHAT
        if (IsConnected && _chatClient != null)
        {
            try
            {
                if (channel == ChannelType.Private)
                {
                    Debug.LogWarning("[BackendChat] Private은 SendWhisperMessage 사용");
                    onComplete?.Invoke(false); return;
                }
                string chName; UInt64 chNumber;
                GetResolvedChannel(channel, out chName, out chNumber);
                if (string.IsNullOrEmpty(chName))
                {
                    SendLocalEcho(channel, message);
                    onComplete?.Invoke(true); return;
                }
                _chatClient.SendChatMessage(GetChannelGroup(channel), chName, chNumber, message);
                onComplete?.Invoke(true); return;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BackendChat] 서버 전송 실패: {e.Message}");
            }
        }
#endif
        SendLocalEcho(channel, message);
        onComplete?.Invoke(true);
    }

    public void SendWhisperMessage(string targetNickname, string message, Action<bool> onComplete = null)
    {
        if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(targetNickname))
        { onComplete?.Invoke(false); return; }
#if BACKND_CHAT
        if (IsConnected && _chatClient != null)
        {
            try
            {
                _chatClient.SendWhisperMessage(targetNickname, message);
                onComplete?.Invoke(true); return;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BackendChat] 귓속말 전송 실패: {e.Message}");
            }
        }
#endif
        SendLocalEcho(ChannelType.Private, $"[-> {targetNickname}] {message}");
        onComplete?.Invoke(true);
    }

    public void SendRescueRequest(int combatPower, Action<bool> onComplete = null)
    {
        string nickname = BackendManager.Instance?.LoggedInUsername ?? "나";
#if BACKND_CHAT
        if (IsConnected && _chatClient != null)
        {
            string rescueMsg = $"$$RESCUE$${nickname}|{combatPower}";
            SendChatMessage(ChannelType.World, rescueMsg, onComplete);
            return;
        }
#endif
        var chatMsg = new ChatMessage
        {
            channel = ChannelType.World, senderNickname = nickname,
            senderInDate = "_local_me_", message = "구출 요청!",
            timestamp = DateTime.Now.ToString("HH:mm"),
            isSystem = false, isRescue = true, combatPower = combatPower
        };
        OnChatMessageReceived?.Invoke(chatMsg);
        onComplete?.Invoke(true);
    }

#if BACKND_CHAT

    public void OnChatMessage(MessageInfo messageInfo)
    {
        ChannelType channel = ResolveChannelType(messageInfo.GetChannelGroup());
        string senderName = messageInfo.GetSenderName();
        string msg = messageInfo.GetMessage();
        Debug.Log($"[BackendChat] 메시지 수신: {senderName} > {msg}");

        if (msg.StartsWith("$$RESCUE$$"))
        { ParseAndEmitRescue(channel, senderName, msg); return; }

        var chatMsg = new ChatMessage
        {
            channel = channel, senderNickname = senderName,
            senderInDate = messageInfo.GetGamerName(), message = msg,
            timestamp = DateTime.Now.ToString("HH:mm"),
            isSystem = false, isRescue = false, combatPower = 0
        };
        OnChatMessageReceived?.Invoke(chatMsg);
    }

    public void OnWhisperMessage(WhisperMessageInfo messageInfo)
    {
        Debug.Log($"[BackendChat] 귓속말 수신: {messageInfo.GetSenderName()} > {messageInfo.GetMessage()}");
        var chatMsg = new ChatMessage
        {
            channel = ChannelType.Private, senderNickname = messageInfo.GetSenderName(),
            senderInDate = messageInfo.GetGamerName(), message = messageInfo.GetMessage(),
            timestamp = DateTime.Now.ToString("HH:mm"),
            isSystem = false, isRescue = false, combatPower = 0
        };
        OnChatMessageReceived?.Invoke(chatMsg);
    }

    public void OnJoinChannel(ChannelInfo channelInfo)
    {
        string group = channelInfo.GetChannelGroup();
        string chName = channelInfo.GetChannelName();
        UInt64 chNumber = channelInfo.GetChannelNumber();
        Debug.Log($"[BackendChat] 채널 입장 완료: group={group}, name={chName}, number={chNumber}");

        ChannelType type = ResolveChannelType(group);
        if (type == ChannelType.World)
        { _worldChannelName = chName; _worldChannelNumber = chNumber; }
        else if (type == ChannelType.Guild)
        { _guildChannelName = chName; _guildChannelNumber = chNumber; }
        else if (type == ChannelType.Party)
        { _partyChannelName = chName; _partyChannelNumber = chNumber; }

        JoinedChannels.Add(type);
        if (!IsConnected)
        {
            IsConnected = true;
            StopNpcChat();
            OnConnectionChanged?.Invoke(true);
            Debug.Log("[BackendChat] 서버 채팅 연결 완료!");
        }
        OnChannelJoined?.Invoke(type, true);
    }

    public void OnLeaveChannel(ChannelInfo channelInfo)
    {
        ChannelType type = ResolveChannelType(channelInfo.GetChannelGroup());
        JoinedChannels.Remove(type);
        Debug.Log($"[BackendChat] 서버 채널 퇴장: {type}");
    }

    public void OnJoinChannelPlayer(string channelGroup, string channelName,
        UInt64 channelNumber, PlayerInfo player)
    {
        var sysMsg = new ChatMessage
        {
            channel = ResolveChannelType(channelGroup), senderNickname = "시스템",
            senderInDate = "_system_",
            message = $"{player.GamerName}님이 입장했습니다.",
            timestamp = DateTime.Now.ToString("HH:mm"), isSystem = true
        };
        OnChatMessageReceived?.Invoke(sysMsg);
    }

    public void OnLeaveChannelPlayer(string channelGroup, string channelName,
        UInt64 channelNumber, PlayerInfo player)
    {
        var sysMsg = new ChatMessage
        {
            channel = ResolveChannelType(channelGroup), senderNickname = "시스템",
            senderInDate = "_system_",
            message = $"{player.GamerName}님이 퇴장했습니다.",
            timestamp = DateTime.Now.ToString("HH:mm"), isSystem = true
        };
        OnChatMessageReceived?.Invoke(sysMsg);
    }

    public void OnUpdatePlayerInfo(string channelGroup, string channelName,
        ulong channelNumber, PlayerInfo player) { }

    public void OnChangeGamerName(string oldGamerName, string newGamerName)
    {
        Debug.Log($"[BackendChat] 닉네임 변경: {oldGamerName} -> {newGamerName}");
        if (oldGamerName == _myGamerName) _myGamerName = newGamerName;
    }

    public void OnTranslateMessage(List<MessageInfo> messages) { }
    public void OnHideMessage(MessageInfo messageInfo) { }
    public void OnDeleteMessage(MessageInfo messageInfo) { }

    public void OnSuccess(SUCCESS_MESSAGE success, object param)
    {
        Debug.Log($"[BackendChat] 성공: {success}");
    }

    public void OnError(ERROR_MESSAGE error, object param)
    {
        Debug.LogWarning($"[BackendChat] 에러: {error}, param={param}");
        if (!IsConnected) { FallbackToLocalMode(); }
    }

#endif // BACKND_CHAT

    // NPC 자동 채팅 (로컬 더미 / 오프라인 폴백)

    private void StartNpcChat()
    {
        if (npcChatCoroutine != null) return;
        npcChatCoroutine = StartCoroutine(NpcChatLoop());
    }

    private void StopNpcChat()
    {
        if (npcChatCoroutine != null)
        { StopCoroutine(npcChatCoroutine); npcChatCoroutine = null; }
    }

    private IEnumerator NpcChatLoop()
    {
        yield return new WaitForSecondsRealtime(3f);
        while (true)
        {
            if (IsConnected) yield break;
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(12f, 40f));
            if (IsConnected) yield break;

            EmitNpcMessage(ChannelType.World,
                NpcNames[UnityEngine.Random.Range(0, NpcNames.Length)],
                NpcMessages[UnityEngine.Random.Range(0, NpcMessages.Length)]);

            if (UnityEngine.Random.value < 0.2f)
            {
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(4f, 12f));
                if (IsConnected) yield break;
                var rescueMsg = new ChatMessage
                {
                    channel = ChannelType.World,
                    senderNickname = NpcNames[UnityEngine.Random.Range(0, NpcNames.Length)],
                    senderInDate = "_npc_",
                    message = RescueMessages[UnityEngine.Random.Range(0, RescueMessages.Length)],
                    timestamp = DateTime.Now.ToString("HH:mm"),
                    isSystem = false, isRescue = true,
                    combatPower = UnityEngine.Random.Range(800, 250000)
                };
                OnChatMessageReceived?.Invoke(rescueMsg);
            }

            if (UnityEngine.Random.value < 0.15f)
            {
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(3f, 8f));
                if (IsConnected) yield break;
                EmitNpcMessage(ChannelType.Guild,
                    NpcNames[UnityEngine.Random.Range(0, NpcNames.Length)],
                    GuildMessages[UnityEngine.Random.Range(0, GuildMessages.Length)]);
            }
        }
    }

    private void EmitNpcMessage(ChannelType channel, string name, string message)
    {
        var chatMsg = new ChatMessage
        {
            channel = channel, senderNickname = name, senderInDate = "_npc_",
            message = message, timestamp = DateTime.Now.ToString("HH:mm"),
            isSystem = false, isRescue = false, combatPower = 0
        };
        OnChatMessageReceived?.Invoke(chatMsg);
    }

    // 유틸

    private void SendLocalEcho(ChannelType channel, string message)
    {
        string myNickname = BackendManager.Instance?.LoggedInUsername ?? "나";
        var chatMsg = new ChatMessage
        {
            channel = channel, senderNickname = myNickname, senderInDate = "_local_me_",
            message = message, timestamp = DateTime.Now.ToString("HH:mm"),
            isSystem = false, isRescue = false, combatPower = 0
        };
        OnChatMessageReceived?.Invoke(chatMsg);
    }

    public bool IsMyMessage(ChatMessage msg)
    {
        if (msg.senderInDate == "_local_me_") return true;
#if BACKND_CHAT
        if (!string.IsNullOrEmpty(_myGamerName) && msg.senderInDate == _myGamerName) return true;
        try
        {
            string userInDate = BackEnd.Backend.UserInDate;
            if (!string.IsNullOrEmpty(userInDate) && msg.senderInDate == userInDate) return true;
        }
        catch { }
#endif
        return false;
    }

    private string GetChannelGroup(ChannelType type) => type switch
    {
        ChannelType.World   => channelGroup,
        ChannelType.Guild   => "guild",
        ChannelType.Party   => "party",
        ChannelType.Private => channelGroup,
        _                   => channelGroup
    };

    private string GetChannelName(ChannelType type) => type switch
    {
        ChannelType.World   => "world_chat",
        ChannelType.Guild   => "guild_chat",
        ChannelType.Party   => "party_chat",
        ChannelType.Private => "private_chat",
        _                   => "world_chat"
    };

#if BACKND_CHAT
    private ChannelType ResolveChannelType(string group)
    {
        if (group == "guild") return ChannelType.Guild;
        if (group == "party") return ChannelType.Party;
        return ChannelType.World;
    }

    private void GetResolvedChannel(ChannelType type, out string chName, out UInt64 chNumber)
    {
        switch (type)
        {
            case ChannelType.World:
                chName = _worldChannelName; chNumber = _worldChannelNumber; return;
            case ChannelType.Guild:
                chName = _guildChannelName; chNumber = _guildChannelNumber; return;
            case ChannelType.Party:
                chName = _partyChannelName; chNumber = _partyChannelNumber; return;
            default:
                chName = _worldChannelName; chNumber = _worldChannelNumber; return;
        }
    }

    private void ParseAndEmitRescue(ChannelType channel, string senderName, string rawMsg)
    {
        string payload = rawMsg.Substring("$$RESCUE$$".Length);
        int pipeIdx = payload.IndexOf(System.Char.Parse("|"));
        string rescueSender = pipeIdx >= 0 ? payload.Substring(0, pipeIdx) : senderName;
        int power = 0;
        if (pipeIdx >= 0 && pipeIdx < payload.Length - 1)
            int.TryParse(payload.Substring(pipeIdx + 1), out power);

        var chatMsg = new ChatMessage
        {
            channel = channel, senderNickname = rescueSender, senderInDate = senderName,
            message = "구출 요청!", timestamp = DateTime.Now.ToString("HH:mm"),
            isSystem = false, isRescue = true, combatPower = power
        };
        OnChatMessageReceived?.Invoke(chatMsg);
    }
#endif
}
