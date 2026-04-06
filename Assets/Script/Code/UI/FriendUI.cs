using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// FriendUI — 친구 시스템 UI 패널
///
/// [탭 구조]
///   0. 친구 목록 + 우정포인트 전송
///   1. 받은 요청 (수락/거절)
///   2. 유저 검색 + 친구 요청
///
/// ★ 탭 패널(friendListPanel/requestPanel/searchPanel)이
///   Inspector에서 연결 안 되어있어도 동작하도록
///   모든 콘텐츠 요소를 개별적으로 직접 on/off 합니다.
/// </summary>
public class FriendUI : MonoBehaviour
{
    public static FriendUI Instance { get; private set; }

    [Header("===== 메인 패널 =====")]
    [SerializeField] private GameObject friendPanel;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("===== 탭 버튼 =====")]
    [SerializeField] private Button tabFriendListBtn;
    [SerializeField] private Button tabRequestBtn;
    [SerializeField] private Button tabSearchBtn;

    [Header("===== 탭 패널 (선택 — 없어도 동작) =====")]
    [SerializeField] private GameObject friendListPanel;
    [SerializeField] private GameObject requestPanel;
    [SerializeField] private GameObject searchPanel;

    // ── 친구 목록 탭 ──
    [Header("===== 친구 목록 =====")]
    [SerializeField] private Transform friendListContent;
    [SerializeField] private GameObject friendItemPrefab;
    [SerializeField] private TextMeshProUGUI friendCountText;
    [SerializeField] private TextMeshProUGUI friendPointText;
    [SerializeField] private Button sendAllBtn;
    [SerializeField] private Button claimAllBtn;

    // ── 받은 요청 탭 ──
    [Header("===== 받은 요청 =====")]
    [SerializeField] private Transform requestListContent;
    [SerializeField] private GameObject requestItemPrefab;
    [SerializeField] private TextMeshProUGUI requestCountText;

    // ── 검색 탭 ──
    [Header("===== 유저 검색 =====")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Button searchBtn;
    [SerializeField] private Transform searchResultContent;
    [SerializeField] private GameObject searchResultItemPrefab;

    // ── 공통 ──
    [Header("===== 공통 =====")]
    [SerializeField] private Button closeBtn;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private GameObject messagePopup;

    private int currentTab = 0;
    private Color activeColor = new Color(1f, 0.84f, 0f);
    private Color inactiveColor = Color.white;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 탭 버튼
        if (tabFriendListBtn) tabFriendListBtn.onClick.AddListener(() => SwitchTab(0));
        if (tabRequestBtn) tabRequestBtn.onClick.AddListener(() => SwitchTab(1));
        if (tabSearchBtn) tabSearchBtn.onClick.AddListener(() => SwitchTab(2));

        // 액션
        if (sendAllBtn) sendAllBtn.onClick.AddListener(OnSendAll);
        if (claimAllBtn) claimAllBtn.onClick.AddListener(OnClaimAll);
        if (searchBtn) searchBtn.onClick.AddListener(OnSearch);
        if (closeBtn) closeBtn.onClick.AddListener(Hide);

        // 이벤트 구독
        BackendFriendManager.OnFriendListLoaded += OnFriendListUpdated;
        BackendFriendManager.OnRequestListLoaded += OnRequestListUpdated;
        BackendFriendManager.OnSearchResultLoaded += OnSearchResultUpdated;
        BackendFriendManager.OnFriendPointReceived += _ => RefreshPointUI();
        BackendFriendManager.OnFriendMessage += ShowMessage;
        BackendFriendManager.OnFriendError += ShowMessage;

        if (friendPanel) friendPanel.SetActive(false);
    }

    void OnDestroy()
    {
        BackendFriendManager.OnFriendListLoaded -= OnFriendListUpdated;
        BackendFriendManager.OnRequestListLoaded -= OnRequestListUpdated;
        BackendFriendManager.OnSearchResultLoaded -= OnSearchResultUpdated;
        BackendFriendManager.OnFriendMessage -= ShowMessage;
        BackendFriendManager.OnFriendError -= ShowMessage;
        if (Instance == this) Instance = null;
    }

    // ═══════════════════════════════════════
    //  표시 / 숨기기
    // ═══════════════════════════════════════

    public void Show()
    {
        if (friendPanel) friendPanel.SetActive(true);
        if (canvasGroup) { canvasGroup.alpha = 1f; canvasGroup.interactable = true; canvasGroup.blocksRaycasts = true; }
        SwitchTab(0);
    }

    public void Hide()
    {
        if (friendPanel) friendPanel.SetActive(false);
        if (canvasGroup) { canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }
    }

    public bool IsOpen => friendPanel != null && friendPanel.activeSelf;

    // ═══════════════════════════════════════
    //  탭 전환 — 핵심 수정
    // ═══════════════════════════════════════

    private void SwitchTab(int tab)
    {
        currentTab = tab;

        // ── 1) 패널 토글 (있으면 사용) ──
        if (friendListPanel) friendListPanel.SetActive(tab == 0);
        if (requestPanel) requestPanel.SetActive(tab == 1);
        if (searchPanel) searchPanel.SetActive(tab == 2);

        // ── 2) 모든 콘텐츠 요소 직접 on/off ──
        //    패널이 null이거나 계층 구조가 잘못되어도 확실히 동작
        SetVisible(friendListContent, tab == 0);
        SetVisible(friendCountText, tab == 0);
        SetVisible(friendPointText, tab == 0);
        SetVisible(sendAllBtn, tab == 0);
        SetVisible(claimAllBtn, tab == 0);

        SetVisible(requestListContent, tab == 1);
        SetVisible(requestCountText, tab == 1);

        SetVisible(searchInput, tab == 2);
        SetVisible(searchBtn, tab == 2);
        SetVisible(searchResultContent, tab == 2);

        // ── 3) 탭 색상 ──
        SetTabColor(tabFriendListBtn, tab == 0);
        SetTabColor(tabRequestBtn, tab == 1);
        SetTabColor(tabSearchBtn, tab == 2);

        // ── 4) 데이터 로드 ──
        switch (tab)
        {
            case 0:
                BackendFriendManager.Instance?.LoadFriendList();
                RefreshPointUI();
                break;
            case 1:
                BackendFriendManager.Instance?.LoadReceivedRequests();
                break;
            case 2:
                BackendFriendManager.Instance?.LoadRandomUsers();
                break;
        }

        Debug.Log($"[FriendUI] SwitchTab({tab})");
    }

    /// <summary>Component의 GameObject를 안전하게 on/off</summary>
    private void SetVisible(Component comp, bool visible)
    {
        if (comp != null) comp.gameObject.SetActive(visible);
    }

    private void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        var text = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (text) text.color = active ? activeColor : inactiveColor;
    }

    // ═══════════════════════════════════════
    //  탭 0: 친구 목록
    // ═══════════════════════════════════════

    private void OnFriendListUpdated(List<FriendData> friends)
    {
        // ★ 현재 탭이 0이 아니면 무시 (다른 탭에서 콜백 올 때 방지)
        if (currentTab != 0) return;
        if (friendListContent == null || friendItemPrefab == null) return;

        foreach (Transform child in friendListContent) Destroy(child.gameObject);

        if (friendCountText)
            friendCountText.text = $"친구: {friends.Count}명";

        foreach (var friend in friends)
        {
            var item = Instantiate(friendItemPrefab, friendListContent);
            item.SetActive(true);

            var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 1) texts[0].text = friend.nickname;

            var buttons = item.GetComponentsInChildren<Button>();

            // 포인트 전송 버튼
            if (buttons.Length >= 1)
            {
                var sendBtn = buttons[0];
                var sendBtnText = sendBtn.GetComponentInChildren<TextMeshProUGUI>();

                if (friend.sentToday)
                {
                    sendBtn.interactable = false;
                    if (sendBtnText) sendBtnText.text = "전송완료";
                }
                else
                {
                    sendBtn.interactable = true;
                    if (sendBtnText) sendBtnText.text = "포인트전송";
                    string inDate = friend.inDate;
                    sendBtn.onClick.AddListener(() =>
                    {
                        BackendFriendManager.Instance?.SendFriendPoint(inDate, (success, msg) =>
                        {
                            if (success)
                            {
                                sendBtn.interactable = false;
                                if (sendBtnText) sendBtnText.text = "전송완료";
                            }
                            ShowMessage(msg);
                        });
                    });
                }
            }

            // 삭제 버튼
            if (buttons.Length >= 2)
            {
                string inDate = friend.inDate;
                string nickname = friend.nickname;
                buttons[1].onClick.AddListener(() =>
                {
                    UIManager.Instance?.ShowConfirmDialog($"{nickname}님을 친구에서 삭제하시겠습니까?", () =>
                    {
                        BackendFriendManager.Instance?.RemoveFriend(inDate);
                    });
                });
            }
        }

        RefreshPointUI();
    }

    private void RefreshPointUI()
    {
        if (friendPointText && BackendFriendManager.Instance != null)
            friendPointText.text = $"우정포인트: {BackendFriendManager.Instance.FriendPoint}P";
    }

    private void OnSendAll()
    {
        BackendFriendManager.Instance?.SendPointToAll(count =>
        {
            BackendFriendManager.Instance?.LoadFriendList();
        });
    }

    private void OnClaimAll()
    {
        BackendFriendManager.Instance?.ClaimReceivedPoints(total =>
        {
            RefreshPointUI();
        });
    }

    // ═══════════════════════════════════════
    //  탭 1: 받은 요청
    // ═══════════════════════════════════════

    private void OnRequestListUpdated(List<FriendRequestData> requests)
    {
        // ★ 현재 탭이 1이 아니면 무시
        if (currentTab != 1) return;
        if (requestListContent == null || requestItemPrefab == null) return;

        foreach (Transform child in requestListContent) Destroy(child.gameObject);

        if (requestCountText)
            requestCountText.text = $"받은 요청: {requests.Count}건";

        foreach (var req in requests)
        {
            var item = Instantiate(requestItemPrefab, requestListContent);
            item.SetActive(true);

            var text = item.GetComponentInChildren<TextMeshProUGUI>();
            if (text) text.text = req.nickname;

            var buttons = item.GetComponentsInChildren<Button>();
            string inDate = req.inDate;

            if (buttons.Length >= 1)
            {
                buttons[0].onClick.AddListener(() =>
                {
                    BackendFriendManager.Instance?.AcceptRequest(inDate);
                });
            }
            if (buttons.Length >= 2)
            {
                buttons[1].onClick.AddListener(() =>
                {
                    BackendFriendManager.Instance?.RejectRequest(inDate);
                });
            }
        }
    }

    // ═══════════════════════════════════════
    //  탭 2: 유저 검색
    // ═══════════════════════════════════════

    private void OnSearch()
    {
        if (searchInput == null) return;
        string keyword = searchInput.text.Trim();

        if (string.IsNullOrEmpty(keyword))
        {
            BackendFriendManager.Instance?.LoadRandomUsers();
            return;
        }
        BackendFriendManager.Instance?.SearchUser(keyword);
    }

    private void OnSearchResultUpdated(List<FriendSearchResult> results)
    {
        // ★ 현재 탭이 2가 아니면 무시
        if (currentTab != 2) return;
        if (searchResultContent == null || searchResultItemPrefab == null) return;

        foreach (Transform child in searchResultContent) Destroy(child.gameObject);

        foreach (var result in results)
        {
            var item = Instantiate(searchResultItemPrefab, searchResultContent);
            item.SetActive(true);

            var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 1) texts[0].text = result.nickname;

            var btn = item.GetComponentInChildren<Button>();
            if (btn != null)
            {
                if (result.isAlreadyFriend)
                {
                    btn.interactable = false;
                    var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (btnText) btnText.text = "이미 친구";
                }
                else
                {
                    string inDate = result.inDate;
                    string nickname = result.nickname;
                    btn.onClick.AddListener(() =>
                    {
                        if (string.IsNullOrEmpty(inDate))
                        {
                            btn.interactable = false;
                            BackendFriendManager.Instance?.ResolveInDateAndRequest(nickname, (success, msg) =>
                            {
                                ShowMessage(msg);
                                if (success)
                                {
                                    var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
                                    if (btnText) btnText.text = "요청완료";
                                }
                                else
                                {
                                    btn.interactable = true;
                                }
                            });
                        }
                        else
                        {
                            BackendFriendManager.Instance?.SendFriendRequest(inDate, (success, msg) =>
                            {
                                ShowMessage(msg);
                                if (success)
                                {
                                    btn.interactable = false;
                                    var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
                                    if (btnText) btnText.text = "요청완료";
                                }
                            });
                        }
                    });
                }
            }
        }
    }

    // ═══════════════════════════════════════
    //  메시지
    // ═══════════════════════════════════════

    private void ShowMessage(string msg)
    {
        if (messagePopup != null && messageText != null)
        {
            messageText.text = msg;
            messagePopup.SetActive(true);
            StopCoroutine(nameof(HideMessage));
            StartCoroutine(HideMessage());
        }
        else
        {
            UIManager.Instance?.ShowMessage(msg, Color.yellow);
        }
    }

    private IEnumerator HideMessage()
    {
        yield return new WaitForSecondsRealtime(2.5f);
        if (messagePopup) messagePopup.SetActive(false);
    }
}
