using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 서버 선택 시스템
/// - 서버 목록 표시
/// - 서버 정보 (이름, 상태, 인원)
/// - 캐릭터 선택 화면으로 이동
/// </summary>
public class ServerSelectionSystem : MonoBehaviour
{
    [Header("서버 선택 패널")]
    [SerializeField] private GameObject serverSelectionPanel;

    [Header("서버 목록")]
    [SerializeField] private Transform serverListContainer;
    [SerializeField] private GameObject serverButtonPrefab;

    [Header("서버 정보")]
    [SerializeField] private TextMeshProUGUI serverNameText;
    [SerializeField] private TextMeshProUGUI serverStatusText;
    [SerializeField] private TextMeshProUGUI serverPopulationText;

    [Header("버튼")]
    [SerializeField] private Button enterServerButton;
    [SerializeField] private Button backButton;

    [Header("다음 화면")]
    [SerializeField] private GameObject characterSelectionPanel;

    [Header("서버 데이터")]
    [SerializeField] private List<ServerInfo> servers = new List<ServerInfo>();

    private ServerInfo selectedServer;

    void Start()
    {
        InitializeServerSelection();
        SetupButtons();
        CreateServerList();
    }

    /// <summary>
    /// 서버 선택 초기화
    /// </summary>
    private void InitializeServerSelection()
    {
        //if (serverSelectionPanel != null)
        //    serverSelectionPanel.SetActive(true);

        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(false);

        // 기본 서버 데이터 생성 (에디터에서 설정하지 않은 경우)
        // ⭐ 주석 처리: Inspector에서 직접 서버 데이터를 설정하려면 아래 주석 해제
        /*
        if (servers.Count == 0)
        {
            CreateDefaultServers();
        }
        */
    }

    /// <summary>
    /// 기본 서버 생성
    /// </summary>
    private void CreateDefaultServers()
    {
        servers.Add(new ServerInfo
        {
            serverName = "서버 1 - 신규",
            serverStatus = ServerStatus.Normal,
            currentPlayers = 1234,
            maxPlayers = 5000
        });

        servers.Add(new ServerInfo
        {
            serverName = "서버 2 - 추천",
            serverStatus = ServerStatus.Normal,
            currentPlayers = 2341,
            maxPlayers = 5000
        });

        servers.Add(new ServerInfo
        {
            serverName = "서버 3 - 혼잡",
            serverStatus = ServerStatus.Crowded,
            currentPlayers = 4567,
            maxPlayers = 5000
        });

        servers.Add(new ServerInfo
        {
            serverName = "서버 4 - 점검중",
            serverStatus = ServerStatus.Maintenance,
            currentPlayers = 0,
            maxPlayers = 5000
        });
    }

    /// <summary>
    /// 버튼 설정
    /// </summary>
    private void SetupButtons()
    {
        if (enterServerButton != null)
        {
            enterServerButton.onClick.AddListener(OnEnterServerClicked);
            enterServerButton.interactable = false; // 초기에는 비활성화
        }

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    /// <summary>
    /// 서버 목록 생성
    /// </summary>
    private void CreateServerList()
    {
        if (serverListContainer == null || serverButtonPrefab == null)
            return;

        // 기존 버튼 제거
        foreach (Transform child in serverListContainer)
        {
            Destroy(child.gameObject);
        }

        // 서버 버튼 생성
        foreach (ServerInfo server in servers)
        {
            GameObject buttonObj = Instantiate(serverButtonPrefab, serverListContainer);
            ServerButton serverButton = buttonObj.GetComponent<ServerButton>();

            if (serverButton != null)
            {
                serverButton.SetupButton(server, () => OnServerSelected(server));
            }
        }
    }

    /// <summary>
    /// 서버 선택
    /// </summary>
    private void OnServerSelected(ServerInfo server)
    {
        selectedServer = server;
        // ★ 서버 클릭 효과음
        SoundManager.Instance?.PlayButtonClick();

        // 서버 정보 표시
        if (serverNameText != null)
            serverNameText.text = server.serverName;

        if (serverStatusText != null)
        {
            string statusText = GetStatusText(server.serverStatus);
            Color statusColor = GetStatusColor(server.serverStatus);
            serverStatusText.text = statusText;
            serverStatusText.color = statusColor;
        }

        if (serverPopulationText != null)
            serverPopulationText.text = $"{server.currentPlayers} / {server.maxPlayers}";

        // 입장 버튼 활성화/비활성화
        if (enterServerButton != null)
        {
            bool canEnter = server.serverStatus != ServerStatus.Maintenance &&
                           server.currentPlayers < server.maxPlayers;
            enterServerButton.interactable = canEnter;
        }
    }

    /// <summary>
    /// 서버 입장 버튼
    /// </summary>
    private void OnEnterServerClicked()
    {
        if (selectedServer == null)
            return;
        // ★ 서버 입장 효과음
        SoundManager.Instance?.PlayServerEnter();

        // 선택한 서버 정보 저장
        if (GameDataBridge.CurrentData != null)
            GameDataBridge.CurrentData.selectedServer = selectedServer.serverName;

        Debug.Log($"서버 입장: {selectedServer.serverName}");

        // 캐릭터 선택 화면으로 이동
        if (serverSelectionPanel != null)
            serverSelectionPanel.SetActive(false);

        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(true);
    }

    /// <summary>
    /// 뒤로가기 버튼
    /// </summary>
    private void OnBackClicked()
    {
        // ★ 뒤로가기 효과음
        SoundManager.Instance?.PlaySFX("PanelClose");
        // 서버 선택 패널 숨기기
        if (serverSelectionPanel != null)
            serverSelectionPanel.SetActive(false);

        // 로그인 화면으로 돌아가기
        LoginSystem loginSystem = FindObjectOfType<LoginSystem>();
        if (loginSystem != null)
        {
            loginSystem.ShowLoginPanel();
        }
    }

    /// <summary>
    /// 서버 선택 패널 표시 (외부에서 호출)
    /// </summary>
    public void ShowServerSelectionPanel()
    {
        if (serverSelectionPanel != null)
            serverSelectionPanel.SetActive(true);

        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(false);
    }

    /// <summary>
    /// 서버 상태 텍스트
    /// </summary>
    private string GetStatusText(ServerStatus status)
    {
        switch (status)
        {
            case ServerStatus.Normal:
                return "정상";
            case ServerStatus.Crowded:
                return "혼잡";
            case ServerStatus.Maintenance:
                return "점검중";
            default:
                return "알 수 없음";
        }
    }

    /// <summary>
    /// 서버 상태 색상
    /// </summary>
    private Color GetStatusColor(ServerStatus status)
    {
        switch (status)
        {
            case ServerStatus.Normal:
                return Color.green;
            case ServerStatus.Crowded:
                return Color.yellow;
            case ServerStatus.Maintenance:
                return Color.red;
            default:
                return Color.gray;
        }
    }
}

/// <summary>
/// 서버 정보
/// </summary>
[System.Serializable]
public class ServerInfo
{
    public string serverName;
    public ServerStatus serverStatus;
    public int currentPlayers;
    public int maxPlayers;
}

/// <summary>
/// 서버 상태
/// </summary>
public enum ServerStatus
{
    Normal,      // 정상
    Crowded,     // 혼잡
    Maintenance  // 점검중
}

/// <summary>
/// 서버 버튼 컴포넌트
/// </summary>