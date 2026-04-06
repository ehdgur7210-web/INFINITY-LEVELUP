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
        if (tabFriendListBtn) tabFriendListBtn.onClick.AddListener(() => { Debug.Log("[FriendUI] ▶ 친구목록 버튼 클릭"); SwitchTab(0); });
        if (tabRequestBtn) tabRequestBtn.onClick.AddListener(() => { Debug.Log("[FriendUI] ▶ 받은목록 버튼 클릭"); SwitchTab(1); });
        if (tabSearchBtn) tabSearchBtn.onClick.AddListener(() => { Debug.Log("[FriendUI] ▶ 검색 버튼 클릭"); SwitchTab(2); });

        // 액션
        if (sendAllBtn) sendAllBtn.onClick.AddListener(OnSendAll);
        if (claimAllBtn) claimAllBtn.onClick.AddListener(OnClaimAll);
        if (searchBtn) searchBtn.onClick.AddListener(OnSearch);
        if (closeBtn) closeBtn.onClick.AddListener(Hide);

        // ★ Inspector에서 연결 안 된 참조를 자동으로 찾기
        AutoFindReferences();

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
    //  Inspector 참조 자동 탐색
    // ═══════════════════════════════════════

    /// <summary>Inspector에서 빠진 참조를 friendPanel 하위에서 자동으로 찾는다</summary>
    private void AutoFindReferences()
    {
        if (friendPanel == null) return;
        var allTMP = friendPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
        var allInput = friendPanel.GetComponentsInChildren<TMP_InputField>(true);

        // searchInput 자동 탐색
        if (searchInput == null)
        {
            foreach (var inp in allInput)
            {
                searchInput = inp;
                Debug.Log($"[FriendUI] searchInput 자동 발견: {inp.gameObject.name}");
                break;
            }
        }

        // requestCountText 자동 탐색 — "받은" 포함 텍스트 또는 이름
        if (requestCountText == null)
        {
            foreach (var t in allTMP)
            {
                if (t.gameObject.name.Contains("request") || t.gameObject.name.Contains("Request") ||
                    t.gameObject.name.Contains("받은") || t.text.Contains("받은"))
                {
                    requestCountText = t;
                    Debug.Log($"[FriendUI] requestCountText 자동 발견: {t.gameObject.name}");
                    break;
                }
            }
        }

        // friendCountText 자동 탐색
        if (friendCountText == null)
        {
            foreach (var t in allTMP)
            {
                if (t.gameObject.name.Contains("friendCount") || t.gameObject.name.Contains("FriendCount") ||
                    t.gameObject.name.Contains("친구") || t.text.Contains("친구:"))
                {
                    friendCountText = t;
                    Debug.Log($"[FriendUI] friendCountText 자동 발견: {t.gameObject.name}");
                    break;
                }
            }
        }

        // friendPointText 자동 탐색
        if (friendPointText == null)
        {
            foreach (var t in allTMP)
            {
                if (t.gameObject.name.Contains("point") || t.gameObject.name.Contains("Point") ||
                    t.gameObject.name.Contains("포인트") || t.text.Contains("포인트"))
                {
                    friendPointText = t;
                    Debug.Log($"[FriendUI] friendPointText 자동 발견: {t.gameObject.name}");
                    break;
                }
            }
        }

        // ★ 모든 참조 상태 로그
        Debug.Log($"[FriendUI] 참조 상태 — " +
            $"friendListPanel:{friendListPanel != null}, requestPanel:{requestPanel != null}, searchPanel:{searchPanel != null}, " +
            $"friendListContent:{friendListContent != null}, requestListContent:{requestListContent != null}, searchResultContent:{searchResultContent != null}, " +
            $"friendCountText:{friendCountText != null}, friendPointText:{friendPointText != null}, requestCountText:{requestCountText != null}, " +
            $"searchInput:{searchInput != null}, searchBtn:{searchBtn != null}, " +
            $"sendAllBtn:{sendAllBtn != null}, claimAllBtn:{claimAllBtn != null}");
    }

    // ═══════════════════════════════════════
    //  탭 전환
    // ═══════════════════════════════════════

    private void SwitchTab(int tab)
    {
        currentTab = tab;

        // ── 1) 먼저 모든 것을 숨긴다 ──
        HideAll();

        // ── 2) 현재 탭만 보인다 ──
        switch (tab)
        {
            case 0: ShowTab0(); break;
            case 1: ShowTab1(); break;
            case 2: ShowTab2(); break;
        }

        // ── 3) 탭 색상 ──
        SetTabColor(tabFriendListBtn, tab == 0);
        SetTabColor(tabRequestBtn, tab == 1);
        SetTabColor(tabSearchBtn, tab == 2);

        Debug.Log($"[FriendUI] SwitchTab({tab})");
    }

    /// <summary>모든 탭 콘텐츠를 숨기고 동적 아이템 제거</summary>
    private void HideAll()
    {
        // ★ 패널 토글 안 함 — 자식 연쇄 활성화 방지
        // 탭0 요소
        ClearChildren(friendListContent);
        SetGO(friendCountText, false);
        SetGO(friendPointText, false);
        SetGO(sendAllBtn, false);
        SetGO(claimAllBtn, false);
        SetGO(friendListContent, false);

        // 탭1 요소
        ClearChildren(requestListContent);
        SetGO(requestCountText, false);
        if (requestCountText) requestCountText.text = "";
        SetGO(requestListContent, false);

        // 탭2 요소
        ClearChildren(searchResultContent);
        SetGO(searchInput, false);
        SetGO(searchBtn, false);
        SetGO(searchResultContent, false);
    }

    private void ShowTab0()
    {
        // ★ friendListPanel 토글 안 함 — 개별 요소만 켬
        SetGO(friendCountText, true);
        SetGO(friendPointText, true);
        SetGO(sendAllBtn, true);
        SetGO(claimAllBtn, true);
        SetGO(friendListContent, true);

        BackendFriendManager.Instance?.LoadFriendList();
        RefreshPointUI();
    }

    private void ShowTab1()
    {
        SetGO(requestCountText, true);
        SetGO(requestListContent, true);

        BackendFriendManager.Instance?.LoadReceivedRequests();
    }

    private void ShowTab2()
    {
        SetGO(searchInput, true);
        SetGO(searchBtn, true);
        SetGO(searchResultContent, true);

        BackendFriendManager.Instance?.LoadRandomUsers();
    }

    private void SetGO(Component comp, bool active)
    {
        if (comp != null) comp.gameObject.SetActive(active);
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
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
        Debug.Log($"[FriendUI] OnFriendListUpdated — count:{friends.Count}, currentTab:{currentTab}, isOpen:{IsOpen}");
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
        Debug.Log($"[FriendUI] OnRequestListUpdated — count:{requests.Count}, currentTab:{currentTab}, isOpen:{IsOpen}");
        // 가드 제거 — 항상 빌드, 표시/숨기기는 SwitchTab이 담당
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
        Debug.Log($"[FriendUI] OnSearchResultUpdated — count:{results.Count}, currentTab:{currentTab}, isOpen:{IsOpen}");
        // 가드 제거 — 항상 빌드, 표시/숨기기는 SwitchTab이 담당
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
