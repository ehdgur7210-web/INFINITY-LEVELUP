using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GuildUI — 길드 메인 UI 패널
///
/// [탭 구조]
///   1. 내 길드 정보 (가입 시) / 길드 생성 (미가입 시)
///   2. 길드 검색 + 가입 신청
///   3. 멤버 관리 (길드장/부관: 승인/추방)
///   4. 출석 & 미션
///
/// [사용법]
///   TopMenuManager에서 ToggleWithBanner("길드", Show, Hide)
///   GuildUI 오브젝트에 이 스크립트 + CanvasGroup 부착
///   하위 패널들을 Inspector에 연결
/// </summary>
public class GuildUI : MonoBehaviour
{
    public static GuildUI Instance { get; private set; }

    [Header("===== 메인 패널 =====")]
    [SerializeField] private GameObject guildPanel;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("===== 탭 버튼 =====")]
    [SerializeField] private Button tabInfoBtn;
    [SerializeField] private Button tabSearchBtn;
    [SerializeField] private Button tabMemberBtn;
    [SerializeField] private Button tabMissionBtn;

    [Header("===== 탭 패널 =====")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private GameObject searchPanel;
    [SerializeField] private GameObject memberPanel;
    [SerializeField] private GameObject missionPanel;

    // ── 길드 정보 탭 (가입 상태) ──
    [Header("===== 길드 정보 =====")]
    [SerializeField] private GameObject guildInfoView;
    [SerializeField] private GameObject noGuildView;
    [SerializeField] private TextMeshProUGUI guildNameText;
    [SerializeField] private TextMeshProUGUI guildMasterText;
    [SerializeField] private TextMeshProUGUI guildMemberCountText;
    [SerializeField] private TextMeshProUGUI guildLevelText;
    [SerializeField] private TextMeshProUGUI guildCombatPowerText;
    [SerializeField] private TextMeshProUGUI guildDescriptionText;
    [SerializeField] private Button leaveGuildBtn;
    [SerializeField] private Button disbandGuildBtn;
    [SerializeField] private Button editDescriptionBtn;
    [SerializeField] private TMP_InputField descriptionInput;

    // ── 길드 생성 (미가입 상태) ──
    [Header("===== 길드 생성 =====")]
    [SerializeField] private TMP_InputField createNameInput;
    [SerializeField] private Button createGuildBtn;

    // ── 검색 탭 ──
    [Header("===== 길드 검색 =====")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Button searchBtn;
    [SerializeField] private Transform searchResultContent;
    [SerializeField] private GameObject searchResultItemPrefab;

    // ── 멤버 탭 ──
    [Header("===== 멤버 관리 =====")]
    [SerializeField] private Transform memberListContent;
    [SerializeField] private GameObject memberItemPrefab;
    [SerializeField] private GameObject applicantSection;
    [SerializeField] private Transform applicantListContent;
    [SerializeField] private GameObject applicantItemPrefab;

    // ── 출석/미션 탭 ──
    [Header("===== 출석 & 미션 =====")]
    [SerializeField] private Button attendanceBtn;
    [SerializeField] private TextMeshProUGUI attendanceStatusText;
    [SerializeField] private TextMeshProUGUI missionKillText;
    [SerializeField] private TextMeshProUGUI missionStageText;
    [SerializeField] private Slider missionKillSlider;
    [SerializeField] private Slider missionStageSlider;
    [SerializeField] private Button claimMissionBtn;
    [SerializeField] private TextMeshProUGUI missionRewardText;

    // ── 닫기 ──
    [Header("===== 공통 =====")]
    [SerializeField] private Button closeBtn;
    [SerializeField] private TextMeshProUGUI messagePopupText;
    [SerializeField] private GameObject messagePopup;

    private int currentTab = 0;
    private Color activeTabColor = new Color(1f, 0.84f, 0f);
    private Color inactiveTabColor = Color.white;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // ★ Awake에서 초기화 (비활성 상태에서도 InactiveInitializer로 실행)
        // 탭 버튼
        if (tabInfoBtn) tabInfoBtn.onClick.AddListener(() => SwitchTab(0));
        if (tabSearchBtn) tabSearchBtn.onClick.AddListener(() => SwitchTab(1));
        if (tabMemberBtn) tabMemberBtn.onClick.AddListener(() => SwitchTab(2));
        if (tabMissionBtn) tabMissionBtn.onClick.AddListener(() => SwitchTab(3));

        // 길드 생성
        if (createGuildBtn) createGuildBtn.onClick.AddListener(OnCreateGuild);

        // 검색
        if (searchBtn) searchBtn.onClick.AddListener(OnSearch);

        // 길드 정보 액션
        if (leaveGuildBtn) leaveGuildBtn.onClick.AddListener(OnLeaveGuild);
        if (disbandGuildBtn) disbandGuildBtn.onClick.AddListener(OnDisbandGuild);
        if (editDescriptionBtn) editDescriptionBtn.onClick.AddListener(OnEditDescription);

        // 출석/미션
        if (attendanceBtn) attendanceBtn.onClick.AddListener(OnAttendance);
        if (claimMissionBtn) claimMissionBtn.onClick.AddListener(OnClaimMission);

        // 닫기
        if (closeBtn) closeBtn.onClick.AddListener(Hide);

        // 이벤트 구독
        BackendGuildManager.OnGuildInfoLoaded += OnGuildInfoUpdated;
        BackendGuildManager.OnGuildListLoaded += OnSearchResultsLoaded;
        BackendGuildManager.OnMemberListLoaded += OnMemberListUpdated;
        BackendGuildManager.OnApplicantListLoaded += OnApplicantListUpdated;
        BackendGuildManager.OnGuildError += ShowMessage;
        BackendGuildManager.OnGuildCreated += () => { ShowMessage("길드가 생성되었습니다!"); SwitchTab(0); };
        BackendGuildManager.OnGuildJoined += () => { ShowMessage("길드에 가입되었습니다!"); SwitchTab(0); };
        BackendGuildManager.OnGuildLeft += () => { ShowMessage("길드를 떠났습니다."); RefreshInfoTab(); };

        if (guildPanel) guildPanel.SetActive(false);
    }

    void OnDestroy()
    {
        BackendGuildManager.OnGuildInfoLoaded -= OnGuildInfoUpdated;
        BackendGuildManager.OnGuildListLoaded -= OnSearchResultsLoaded;
        BackendGuildManager.OnMemberListLoaded -= OnMemberListUpdated;
        BackendGuildManager.OnApplicantListLoaded -= OnApplicantListUpdated;
        BackendGuildManager.OnGuildError -= ShowMessage;
        if (Instance == this) Instance = null;
    }

    // ═══════════════════════════════════════
    //  표시 / 숨기기
    // ═══════════════════════════════════════

    public void Show()
    {
        if (guildPanel) guildPanel.SetActive(true);
        if (canvasGroup) { canvasGroup.alpha = 1f; canvasGroup.interactable = true; canvasGroup.blocksRaycasts = true; }

        BackendGuildManager.Instance?.LoadMyGuildInfo();
        SwitchTab(0);
    }

    public void Hide()
    {
        if (guildPanel) guildPanel.SetActive(false);
        if (canvasGroup) { canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }
    }

    public bool IsOpen => guildPanel != null && guildPanel.activeSelf;

    // ═══════════════════════════════════════
    //  탭 전환
    // ═══════════════════════════════════════

    private void SwitchTab(int tabIndex)
    {
        currentTab = tabIndex;

        if (infoPanel) infoPanel.SetActive(tabIndex == 0);
        if (searchPanel) searchPanel.SetActive(tabIndex == 1);
        if (memberPanel) memberPanel.SetActive(tabIndex == 2);
        if (missionPanel) missionPanel.SetActive(tabIndex == 3);

        SetTabColor(tabInfoBtn, tabIndex == 0);
        SetTabColor(tabSearchBtn, tabIndex == 1);
        SetTabColor(tabMemberBtn, tabIndex == 2);
        SetTabColor(tabMissionBtn, tabIndex == 3);

        switch (tabIndex)
        {
            case 0: RefreshInfoTab(); break;
            case 1: break; // 검색은 버튼 누를 때
            case 2: RefreshMemberTab(); break;
            case 3: RefreshMissionTab(); break;
        }
    }

    private void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        var text = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (text) text.color = active ? activeTabColor : inactiveTabColor;
    }

    // ═══════════════════════════════════════
    //  탭 0: 길드 정보
    // ═══════════════════════════════════════

    private void RefreshInfoTab()
    {
        bool inGuild = BackendGuildManager.Instance != null && BackendGuildManager.Instance.IsInGuild;

        if (guildInfoView) guildInfoView.SetActive(inGuild);
        if (noGuildView) noGuildView.SetActive(!inGuild);

        if (inGuild)
        {
            var guild = BackendGuildManager.Instance.MyGuild;
            var role = BackendGuildManager.Instance.MyRole;

            if (guildNameText) guildNameText.text = guild.guildName;
            if (guildMasterText) guildMasterText.text = $"길드장: {guild.masterNickname}";
            if (guildMemberCountText) guildMemberCountText.text = $"멤버: {guild.memberCount}명";
            if (guildLevelText) guildLevelText.text = $"Lv.{guild.guildLevel}";
            if (guildCombatPowerText) guildCombatPowerText.text = $"전투력: {UIManager.FormatKoreanUnit(guild.totalCombatPower)}";
            if (guildDescriptionText) guildDescriptionText.text = string.IsNullOrEmpty(guild.description) ? "길드 소개가 없습니다." : guild.description;

            // 길드장만 보이는 버튼
            if (disbandGuildBtn) disbandGuildBtn.gameObject.SetActive(role == GuildMemberRole.Master);
            if (editDescriptionBtn) editDescriptionBtn.gameObject.SetActive(role == GuildMemberRole.Master);
            if (descriptionInput) descriptionInput.gameObject.SetActive(false);

            // 길드장은 탈퇴 대신 해산
            if (leaveGuildBtn) leaveGuildBtn.gameObject.SetActive(role != GuildMemberRole.Master);
        }
    }

    private void OnGuildInfoUpdated(GuildInfo info)
    {
        if (currentTab == 0) RefreshInfoTab();
    }

    // ═══════════════════════════════════════
    //  탭 0: 길드 생성
    // ═══════════════════════════════════════

    private void OnCreateGuild()
    {
        if (createNameInput == null) return;
        string name = createNameInput.text.Trim();
        if (string.IsNullOrEmpty(name) || name.Length < 2)
        {
            ShowMessage("길드 이름을 2자 이상 입력하세요.");
            return;
        }
        if (name.Length > 12)
        {
            ShowMessage("길드 이름은 12자 이하로 입력하세요.");
            return;
        }

        BackendGuildManager.Instance?.CreateGuild(name, 0, (success, msg) =>
        {
            ShowMessage(msg);
        });
    }

    // ═══════════════════════════════════════
    //  탭 0: 탈퇴 / 해산
    // ═══════════════════════════════════════

    private void OnLeaveGuild()
    {
        UIManager.Instance?.ShowConfirmDialog("정말 길드를 탈퇴하시겠습니까?", () =>
        {
            BackendGuildManager.Instance?.LeaveGuild((success, msg) => ShowMessage(msg));
        });
    }

    private void OnDisbandGuild()
    {
        UIManager.Instance?.ShowConfirmDialog("정말 길드를 해산하시겠습니까?\n이 작업은 되돌릴 수 없습니다.", () =>
        {
            BackendGuildManager.Instance?.DisbandGuild((success, msg) => ShowMessage(msg));
        });
    }

    private void OnEditDescription()
    {
        if (descriptionInput == null) return;

        bool editing = descriptionInput.gameObject.activeSelf;
        if (editing)
        {
            // 저장
            string desc = descriptionInput.text.Trim();
            BackendGuildManager.Instance?.UpdateGuildDescription(desc, success =>
            {
                if (success)
                {
                    ShowMessage("길드 소개가 수정되었습니다.");
                    if (guildDescriptionText) guildDescriptionText.text = desc;
                }
                descriptionInput.gameObject.SetActive(false);
            });
        }
        else
        {
            // 편집 모드 진입
            descriptionInput.gameObject.SetActive(true);
            descriptionInput.text = BackendGuildManager.Instance?.MyGuild?.description ?? "";
        }
    }

    // ═══════════════════════════════════════
    //  탭 1: 검색
    // ═══════════════════════════════════════

    private void OnSearch()
    {
        if (searchInput == null) return;
        string keyword = searchInput.text.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            ShowMessage("검색어를 입력하세요.");
            return;
        }
        BackendGuildManager.Instance?.SearchGuilds(keyword);
    }

    private void OnSearchResultsLoaded(List<GuildInfo> results)
    {
        if (searchResultContent == null || searchResultItemPrefab == null) return;

        // 기존 항목 제거
        foreach (Transform child in searchResultContent) Destroy(child.gameObject);

        if (results.Count == 0)
        {
            ShowMessage("검색 결과가 없습니다.");
            return;
        }

        foreach (var guild in results)
        {
            var item = Instantiate(searchResultItemPrefab, searchResultContent);
            item.SetActive(true);

            var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 1) texts[0].text = guild.guildName;
            if (texts.Length >= 2) texts[1].text = $"길드장: {guild.masterNickname}  멤버: {guild.memberCount}명";
            if (texts.Length >= 3) texts[2].text = $"Lv.{guild.guildLevel}  전투력: {UIManager.FormatKoreanUnit(guild.totalCombatPower)}";

            var applyBtn = item.GetComponentInChildren<Button>();
            if (applyBtn != null)
            {
                string guildInDate = guild.guildInDate;
                applyBtn.onClick.AddListener(() =>
                {
                    BackendGuildManager.Instance?.ApplyToGuild(guildInDate, (success, msg) =>
                    {
                        ShowMessage(msg);
                    });
                });
            }
        }
    }

    // ═══════════════════════════════════════
    //  탭 2: 멤버 관리
    // ═══════════════════════════════════════

    private void RefreshMemberTab()
    {
        if (!BackendGuildManager.Instance?.IsInGuild ?? true) return;

        BackendGuildManager.Instance.LoadMembers();
        BackendGuildManager.Instance.LoadApplicants();

        // 신청 목록은 길드장/부관만 표시
        var role = BackendGuildManager.Instance.MyRole;
        if (applicantSection) applicantSection.SetActive(role != GuildMemberRole.Member);
    }

    private void OnMemberListUpdated(List<GuildMemberData> members)
    {
        if (memberListContent == null || memberItemPrefab == null) return;

        foreach (Transform child in memberListContent) Destroy(child.gameObject);

        // 역할 순 정렬: Master > ViceMaster > Member
        members.Sort((a, b) => b.role.CompareTo(a.role));

        var myRole = BackendGuildManager.Instance?.MyRole ?? GuildMemberRole.Member;

        foreach (var member in members)
        {
            var item = Instantiate(memberItemPrefab, memberListContent);
            item.SetActive(true);

            var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            string roleTag = member.role == GuildMemberRole.Master ? " [길드장]" :
                             member.role == GuildMemberRole.ViceMaster ? " [부관]" : "";
            if (texts.Length >= 1) texts[0].text = $"{member.nickname}{roleTag}";
            if (texts.Length >= 2) texts[1].text = $"전투력: {UIManager.FormatKoreanUnit(member.combatPower)}";

            // 추방 버튼 (길드장/부관만, 자신과 길드장은 제외)
            var buttons = item.GetComponentsInChildren<Button>();
            if (buttons.Length >= 1)
            {
                bool canExpel = myRole > member.role && member.role != GuildMemberRole.Master;
                buttons[0].gameObject.SetActive(canExpel);
                if (canExpel)
                {
                    string memberInDate = member.inDate;
                    string memberName = member.nickname;
                    buttons[0].onClick.AddListener(() =>
                    {
                        UIManager.Instance?.ShowConfirmDialog($"{memberName}님을 추방하시겠습니까?", () =>
                        {
                            BackendGuildManager.Instance?.ExpelMember(memberInDate);
                        });
                    });
                }
            }

            // 부관 임명 버튼 (길드장만, 일반 멤버 대상)
            if (buttons.Length >= 2)
            {
                bool canPromote = myRole == GuildMemberRole.Master && member.role == GuildMemberRole.Member;
                buttons[1].gameObject.SetActive(canPromote);
                if (canPromote)
                {
                    string memberInDate = member.inDate;
                    buttons[1].onClick.AddListener(() =>
                    {
                        BackendGuildManager.Instance?.SetViceMaster(memberInDate, true);
                    });
                }
            }
        }
    }

    private void OnApplicantListUpdated(List<GuildApplicantData> applicants)
    {
        if (applicantListContent == null || applicantItemPrefab == null) return;

        foreach (Transform child in applicantListContent) Destroy(child.gameObject);

        foreach (var applicant in applicants)
        {
            var item = Instantiate(applicantItemPrefab, applicantListContent);
            item.SetActive(true);

            var text = item.GetComponentInChildren<TextMeshProUGUI>();
            if (text) text.text = applicant.nickname;

            var buttons = item.GetComponentsInChildren<Button>();
            string appInDate = applicant.inDate;
            // 승인 버튼
            if (buttons.Length >= 1)
            {
                buttons[0].onClick.AddListener(() =>
                {
                    BackendGuildManager.Instance?.ApproveApplicant(appInDate);
                });
            }
            // 거절 버튼
            if (buttons.Length >= 2)
            {
                buttons[1].onClick.AddListener(() =>
                {
                    BackendGuildManager.Instance?.RejectApplicant(appInDate);
                });
            }
        }
    }

    // ═══════════════════════════════════════
    //  탭 3: 출석 & 미션
    // ═══════════════════════════════════════

    private void RefreshMissionTab()
    {
        if (BackendGuildManager.Instance == null) return;

        var mgr = BackendGuildManager.Instance;

        // 출석
        if (attendanceBtn) attendanceBtn.interactable = !mgr.TodayAttended;
        if (attendanceStatusText) attendanceStatusText.text = mgr.TodayAttended ? "출석 완료!" : "출석하기";

        // 미션 진행도
        int killTarget = mgr.GetMissionKillTarget();
        int stageTarget = mgr.GetMissionStageTarget();

        if (missionKillText) missionKillText.text = $"몬스터 처치: {mgr.MissionKillCount} / {killTarget}";
        if (missionStageText) missionStageText.text = $"스테이지 클리어: {mgr.MissionStageCount} / {stageTarget}";

        if (missionKillSlider)
        {
            missionKillSlider.maxValue = killTarget;
            missionKillSlider.value = Mathf.Min(mgr.MissionKillCount, killTarget);
        }
        if (missionStageSlider)
        {
            missionStageSlider.maxValue = stageTarget;
            missionStageSlider.value = Mathf.Min(mgr.MissionStageCount, stageTarget);
        }

        if (claimMissionBtn) claimMissionBtn.interactable = mgr.IsMissionComplete;
    }

    private void OnAttendance()
    {
        BackendGuildManager.Instance?.CheckAttendance((success, msg) =>
        {
            ShowMessage(msg);
            RefreshMissionTab();
        });
    }

    private void OnClaimMission()
    {
        BackendGuildManager.Instance?.ClaimMissionReward((success, msg) =>
        {
            ShowMessage(msg);
            RefreshMissionTab();
        });
    }

    // ═══════════════════════════════════════
    //  메시지 팝업
    // ═══════════════════════════════════════

    private void ShowMessage(string msg)
    {
        if (messagePopup != null && messagePopupText != null)
        {
            messagePopupText.text = msg;
            messagePopup.SetActive(true);
            StopCoroutine(nameof(HideMessageAfterDelay));
            StartCoroutine(HideMessageAfterDelay());
        }
        else
        {
            UIManager.Instance?.ShowMessage(msg, Color.yellow);
        }
    }

    private IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSecondsRealtime(2.5f);
        if (messagePopup) messagePopup.SetActive(false);
    }
}
