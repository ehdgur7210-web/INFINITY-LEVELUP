using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TopMenuManager - 우측 상단 메뉴 버튼 슬라이드 토글 관리
///
/// ★★★ 핵심 수정 사항 ★★★
///
/// [버그 원인]
///   InitializeMenu()에서 expandedPosition = menuRectTransform.anchoredPosition 를 사용했는데
///   에디터에서 패널이 화면 밖(예: PosX 1373)에 배치되어 있으면
///   그 위치가 그대로 "열린 위치"로 저장됨
///   → expandedPosition = 화면 밖, collapsedPosition = 더 먼 화면 밖
///   → 버튼을 눌러도 눈에 보이는 변화 없음
///
/// [수정]
///   expandedPosXOverride(Inspector)로 "열린 위치 X"를 직접 지정
///   기본값 0 = 패널 오른쪽 끝이 화면 오른쪽 끝에 딱 맞음
///   음수값(예: -10) = 패널이 화면 안쪽으로 10px 들어옴
///   collapsedPosition은 panelWidth 만큼 오른쪽(화면 밖)으로 자동 계산
///
/// [Inspector 설정]
///   Expanded Pos X Override : 0 (기본값, 패널이 화면 끝에 붙음)
///   Start Expanded : false (시작 시 접힌 상태)
///   Enable Slide Animation : true
///   Slide Speed : 8
/// </summary>
public class TopMenuManager : MonoBehaviour
{
    public static TopMenuManager Instance;

    [Header("메뉴 버튼들")]
    [SerializeField] private Button inventoryButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button equipmentButton;
    [SerializeField] private Button skillButton;
    [SerializeField] private Button mailButton;
    [SerializeField] private Button achieveButton;
    [SerializeField] private Button craftButton;
    [SerializeField] private Button enhancementButton;
    [SerializeField] private Button auctionButton;
    [SerializeField] private Button rankingButton;
    [SerializeField] private Button settingsButton;

    [Header("알림 배지 (선택)")]
    [SerializeField] private GameObject inventoryBadge;
    [SerializeField] private GameObject questBadge;
    [SerializeField] private GameObject shopBadge;
    [SerializeField] private GameObject achieveBadge;

    [Header("메뉴 패널 (접기/펼치기)")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button toggleButton;
    [SerializeField] private bool startExpanded = false;
    [SerializeField] private GameObject equipmentPanel;

    [Header("슬라이드 위치 설정")]
    // ★ 핵심 수정: 에디터 배치 위치에 의존하지 않고 직접 지정
    // 패널 Anchor가 오른쪽(right)일 때:
    //   0   = 패널 오른쪽 끝이 화면 오른쪽 끝에 딱 붙음 (기본 추천)
    //  -10  = 패널이 화면 안쪽으로 10px 들어옴
    //  300  = 패널이 화면 밖으로 300px 나감 (완전히 숨김)
    [SerializeField] private float expandedPosXOverride = 0f;

    // 접혔을 때 패널이 얼마나 더 오른쪽으로 나갈지 (기본: panelWidth + 여백)
    // 0이면 자동으로 panelWidth + 60 으로 계산
    [SerializeField] private float collapsedExtraOffset = 0f;

    [Header("애니메이션")]
    [SerializeField] private bool enableSlideAnimation = true;
    [SerializeField] private float slideSpeed = 8f;

    // ── 런타임 변수 ──────────────────────────────────────────
    private bool isExpanded;
    private Vector2 expandedPosition;   // 열린 상태 anchoredPosition
    private Vector2 collapsedPosition;  // 닫힌 상태 anchoredPosition
    private RectTransform menuRectTransform;
    private bool isAnimating = false;
    private bool menuInitialized = false; // 초기화 완료 플래그

    /// <summary>전역 UIClickGuard를 사용하여 동시 입력 방지</summary>
    private bool IsButtonOnCooldown()
    {
        return !UIClickGuard.Consume();
    }

    /// <summary>
    /// 튜토리얼 중 포커스 대상이 아닌 버튼 클릭 차단.
    /// buttonName = 이 버튼의 GameObject 이름 (Inspector에서 설정된 이름)
    /// </summary>
    private bool IsBlockedByTutorial(Button btn)
    {
        if (TutorialManager.Instance == null || !TutorialManager.Instance.ShouldBlockNonFocusButtons)
            return false;
        // 튜토리얼 포커스 대상 버튼이면 허용
        bool isFocus = btn != null && TutorialManager.Instance.IsCurrentFocusTarget(btn.gameObject);
        Debug.Log($"[TopMenu] IsBlockedByTutorial: btn={btn?.gameObject.name}, isFocusTarget={isFocus}, frame={Time.frameCount}");
        if (isFocus)
            return false;
        // ClickFocusTarget 단계에서 포커스 대상이 아닌 버튼은 차단
        return true;
    }

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            enabled = false;
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (Instance != this) return;
        SetupButtons();
        HideAllBadges();
        // Canvas 레이아웃 계산 완료 후 초기화 (1프레임 대기)
        StartCoroutine(InitializeMenuDelayed());
    }

    void Update()
    {
        // 초기화 완료 + 애니메이션 활성 + RectTransform 존재 시에만 실행
        if (enableSlideAnimation && menuRectTransform != null && isAnimating)
            UpdateSlideAnimation();
    }

    // ─────────────────────────────────────────────────────────
    // 씬 재로드 시 버튼 재등록 (TopMenuButtonRegistrar가 호출)
    // ─────────────────────────────────────────────────────────
    public void RebindButton(string buttonType, Button btn)
    {
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();

        switch (buttonType.ToLower())
        {
            case "inventory": inventoryButton = btn; btn.onClick.AddListener(OnInventoryButtonClicked); break;
            case "shop": shopButton = btn; btn.onClick.AddListener(OnShopButtonClicked); break;
            case "equipment": equipmentButton = btn; btn.onClick.AddListener(OnEquipmentButtonClicked); break;
            case "skill": skillButton = btn; btn.onClick.AddListener(OnSkillButtonClicked); break;
            case "mail": mailButton = btn; btn.onClick.AddListener(OnmailButtonClicked); break;
            case "achieve": achieveButton = btn; btn.onClick.AddListener(OnAchieveButtonClicked); break;
            case "craft": craftButton = btn; btn.onClick.AddListener(OnCraftButtonClicked); break;
            case "enhancement": enhancementButton = btn; btn.onClick.AddListener(OnenhancementButtonClicked); break;
            case "auction": auctionButton = btn; btn.onClick.AddListener(OnAuctionButtonClicked); break;
            case "ranking": rankingButton = btn; btn.onClick.AddListener(OnRankingButtonClicked); break;
            case "settings": settingsButton = btn; btn.onClick.AddListener(OnSettingsButtonClicked); break;
            case "toggle":
                toggleButton = btn;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ToggleMenu);
                // ★ 주의: 여기서 InitializeMenu() 호출 금지!
                // 이미 Start()에서 초기화됐고, 다시 호출 시 위치가 망가짐
                break;
        }
        Debug.Log($"[TopMenuManager] 버튼 재등록: {buttonType}");
    }

    public void RebindEquipmentPanel(GameObject panel) { equipmentPanel = panel; }

    // ─────────────────────────────────────────────────────────
    // menuPanel 재연결 (UIAutoRegister가 씬 재로드 시 호출)
    // ─────────────────────────────────────────────────────────
    public void RebindMenuPanel(GameObject panel)
    {
        menuPanel = panel;
        menuInitialized = false; // 재초기화 허용
        StartCoroutine(InitializeMenuDelayed());
        Debug.Log("[TopMenuManager] menuPanel 재바인딩 → 슬라이드 위치 재계산");
    }

    // ─────────────────────────────────────────────────────────
    // 버튼 이벤트 연결
    // ─────────────────────────────────────────────────────────
    private void SetupButtons()
    {
        if (inventoryButton != null) { inventoryButton.onClick.RemoveAllListeners(); inventoryButton.onClick.AddListener(OnInventoryButtonClicked); }
        if (shopButton != null) { shopButton.onClick.RemoveAllListeners(); shopButton.onClick.AddListener(OnShopButtonClicked); }
        if (equipmentButton != null) { equipmentButton.onClick.RemoveAllListeners(); equipmentButton.onClick.AddListener(OnEquipmentButtonClicked); }
        if (skillButton != null) { skillButton.onClick.RemoveAllListeners(); skillButton.onClick.AddListener(OnSkillButtonClicked); }
        if (mailButton != null) { mailButton.onClick.RemoveAllListeners(); mailButton.onClick.AddListener(OnmailButtonClicked); }
        if (achieveButton != null) { achieveButton.onClick.RemoveAllListeners(); achieveButton.onClick.AddListener(OnAchieveButtonClicked); }
        if (craftButton != null) { craftButton.onClick.RemoveAllListeners(); craftButton.onClick.AddListener(OnCraftButtonClicked); }
        if (auctionButton != null) { auctionButton.onClick.RemoveAllListeners(); auctionButton.onClick.AddListener(OnAuctionButtonClicked); }
        if (enhancementButton != null) { enhancementButton.onClick.RemoveAllListeners(); enhancementButton.onClick.AddListener(OnenhancementButtonClicked); }
        if (rankingButton != null) { rankingButton.onClick.RemoveAllListeners(); rankingButton.onClick.AddListener(OnRankingButtonClicked); }
        else Debug.LogWarning("[TopMenuManager] rankingButton이 Inspector에 연결되지 않았습니다!");
        if (settingsButton != null) { settingsButton.onClick.RemoveAllListeners(); settingsButton.onClick.AddListener(OnSettingsButtonClicked); }
        if (toggleButton != null) { toggleButton.onClick.RemoveAllListeners(); toggleButton.onClick.AddListener(ToggleMenu); }
    }

    // ─────────────────────────────────────────────────────────
    // 1프레임 대기 후 초기화 (Canvas 레이아웃 계산 완료 보장)
    // ─────────────────────────────────────────────────────────
    private System.Collections.IEnumerator InitializeMenuDelayed()
    {
        yield return null;              // 1프레임 대기
        Canvas.ForceUpdateCanvases();   // 레이아웃 강제 계산
        InitializeMenu();
    }

    // ─────────────────────────────────────────────────────────
    // ★★★ 핵심 수정: 위치를 에디터 배치가 아닌 Inspector 값으로 계산 ★★★
    // ─────────────────────────────────────────────────────────
    private void InitializeMenu()
    {
        if (menuPanel == null)
        {
            Debug.LogWarning("[TopMenuManager] menuPanel이 연결되지 않았습니다!");
            return;
        }

        menuRectTransform = menuPanel.GetComponent<RectTransform>();
        if (menuRectTransform == null) return;

        // 패널 너비 읽기 (rect.width 우선, 안되면 sizeDelta.x)
        float panelWidth = menuRectTransform.rect.width;
        if (panelWidth < 1f) panelWidth = menuRectTransform.sizeDelta.x;
        if (panelWidth < 1f)
        {
            Debug.LogWarning("[TopMenuManager] menuPanel 너비 읽기 실패 — 다음 프레임 재시도");
            StartCoroutine(InitializeMenuDelayed());
            return;
        }

        // ★ 수정 포인트:
        //   에디터에서 패널이 어디 있든 상관없이
        //   expandedPosXOverride 값을 "열린 위치 X"로 강제 설정
        //
        //   expandedPosXOverride = 0   → 패널 오른쪽 끝이 화면 오른쪽 끝에 딱 맞음
        //   expandedPosXOverride = -10 → 패널이 화면 안으로 10px 들어옴
        float currentY = menuRectTransform.anchoredPosition.y;

        expandedPosition = new Vector2(expandedPosXOverride, currentY);

        // 닫힌 위치: 열린 위치에서 panelWidth + 여백 만큼 오른쪽(화면 밖)
        float offset = (collapsedExtraOffset > 0f) ? collapsedExtraOffset : panelWidth + 60f;
        collapsedPosition = new Vector2(expandedPosXOverride + offset, currentY);

        // 초기 상태 설정
        isExpanded = startExpanded;
        menuRectTransform.anchoredPosition = isExpanded ? expandedPosition : collapsedPosition;
        menuInitialized = true;

        Debug.Log($"[TopMenuManager] 초기화 완료 ★ " +
                  $"panelWidth={panelWidth:F0} | " +
                  $"expanded={expandedPosition} | " +
                  $"collapsed={collapsedPosition} | " +
                  $"startExpanded={startExpanded}");
    }

    // ─────────────────────────────────────────────────────────
    // 슬라이드 애니메이션 (Update에서 호출)
    // ─────────────────────────────────────────────────────────
    private void UpdateSlideAnimation()
    {
        Vector2 target = isExpanded ? expandedPosition : collapsedPosition;

        menuRectTransform.anchoredPosition = Vector2.Lerp(
            menuRectTransform.anchoredPosition,
            target,
            Time.deltaTime * slideSpeed
        );

        // 목표 위치에 충분히 가까워지면 애니메이션 종료
        if (Vector2.Distance(menuRectTransform.anchoredPosition, target) < 1f)
        {
            menuRectTransform.anchoredPosition = target;
            isAnimating = false;
        }
    }

    // ─────────────────────────────────────────────────────────
    // 버튼 클릭 핸들러
    // ─────────────────────────────────────────────────────────
    #region 버튼 클릭

    /// <summary>
    /// Toggle 패널용: 이전 패널 강제 닫기 → 새 패널 열기 → 배너 갱신
    /// ★ 핵심: 토글이 아닌 "강제 닫기(forceClose)" 액션을 저장
    ///   → 이미 닫힌 패널을 다시 여는 버그 완전 방지
    /// </summary>
    private string _currentBanner = null;
    private System.Action _currentCloseAction = null;

    /// <param name="bannerName">배너 이름 (같으면 닫기, 다르면 열기)</param>
    /// <param name="openAction">패널을 여는 액션</param>
    /// <param name="closeAction">패널을 강제로 닫는 액션 (SetActive(false) 등)</param>
    private void ToggleWithBanner(string bannerName, System.Action openAction, System.Action closeAction)
    {
        // 같은 배너가 이미 열려있으면 → 닫기
        if (_currentBanner == bannerName)
        {
            closeAction?.Invoke();
            _currentBanner = null;
            _currentCloseAction = null;
            UIManager.Instance?.ResetHeaderToGame();
        }
        else
        {
            // ★ 이전에 열린 패널이 있으면 강제 닫기
            ForceCloseCurrentPanel();

            // 새 패널 열기
            openAction?.Invoke();
            _currentBanner = bannerName;
            _currentCloseAction = closeAction; // ★ 강제 닫기 액션 저장
            UIManager.Instance?.SetHeaderBanner(bannerName);
        }
    }

    /// <summary>현재 열린 패널을 강제로 닫는다 (내부용)</summary>
    private void ForceCloseCurrentPanel()
    {
        if (_currentBanner == null) return;

        if (_currentCloseAction != null)
        {
            _currentCloseAction.Invoke();
            Debug.Log($"[TopMenuManager] 이전 패널 강제 닫기: {_currentBanner}");
        }

        _currentBanner = null;
        _currentCloseAction = null;
        UIManager.Instance?.ResetHeaderToGame();
    }

    /// <summary>외부에서 패널 닫힐 때 배너 리셋 (각 패널 Close에서 호출 가능)</summary>
    public void ClearBanner()
    {
        _currentBanner = null;
        _currentCloseAction = null;
        UIManager.Instance?.ResetHeaderToGame();
    }

    private void OnInventoryButtonClicked()
    {
        if (IsButtonOnCooldown()) return;
        if (IsBlockedByTutorial(inventoryButton)) return;
        ForceCloseCurrentPanel();
        InventoryManager.Instance?.ToggleInventory();
        if (inventoryBadge != null) inventoryBadge.SetActive(false);
        PlayButtonSound();
    }
    private void OnShopButtonClicked()
    {
        if (IsButtonOnCooldown()) return;
        if (IsBlockedByTutorial(shopButton)) return;
        ToggleWithBanner("상점",
            () => ShopManager.Instance?.ShowShop(),
            () => ShopManager.Instance?.HideShop());
        if (shopBadge != null) shopBadge.SetActive(false);
        PlayButtonSound();
    }
    private void OnAuctionButtonClicked()
    {
        if (IsButtonOnCooldown()) return;
        if (IsBlockedByTutorial(auctionButton)) return;
        var auctionUI = FindObjectOfType<AuctionUI>();
        ToggleWithBanner("경매",
            () => auctionUI?.OpenAuction(),
            () => auctionUI?.CloseAuction());
        PlayButtonSound();
    }
    private void OnAchieveButtonClicked()
    {
        if (IsButtonOnCooldown()) return;
        if (IsBlockedByTutorial(achieveButton)) return;
        if (achieveBadge != null) achieveBadge.SetActive(false);
        ToggleWithBanner("업적",
            () => AchievementSystem.Instance?.ShowAchievementUI(),
            () => AchievementSystem.Instance?.HideAchievementUI());
        PlayButtonSound();
    }
    private void OnEquipmentButtonClicked()
    {
        if (IsButtonOnCooldown()) return;
        if (IsBlockedByTutorial(equipmentButton)) return;
        ToggleWithBanner("장착",
            () => { if (equipmentPanel != null) equipmentPanel.SetActive(true); },
            () => { if (equipmentPanel != null) equipmentPanel.SetActive(false); });
        if (questBadge != null) questBadge.SetActive(false);
        PlayButtonSound();
    }
    private void OnSkillButtonClicked()
    {
        if (IsButtonOnCooldown()) return;
        if (IsBlockedByTutorial(skillButton)) return;
        ToggleWithBanner("스킬",
            () => SkillManager.Instance?.ShowSkillTree(),
            () => SkillManager.Instance?.HideSkillTree());
        PlayButtonSound();
    }
    private void OnmailButtonClicked()
    {
        if (IsButtonOnCooldown()) return;
        if (IsBlockedByTutorial(mailButton)) return;
        ForceCloseCurrentPanel();
        UIManager.Instance?.SetHeaderBanner("우편");
        var mailUI = MailUI.Instance;
        if (mailUI == null)
            mailUI = FindObjectOfType<MailUI>(true);
        if (mailUI != null)
        {
            MailUI.Instance = mailUI;
            mailUI.OpenMailPanel();
        }
        PlayButtonSound();
    }
    private void OnSettingsButtonClicked()
    {
        if (IsButtonOnCooldown()) return;
        if (IsBlockedByTutorial(settingsButton)) return;
        ToggleWithBanner("설정",
            () => OptionUI.Instance?.ShowOptionPanel(),
            () => OptionUI.Instance?.HideOptionPanel());
        PlayButtonSound();
    }
    private void OnCraftButtonClicked()
    {
        if (IsButtonOnCooldown()) return;
        if (IsBlockedByTutorial(craftButton)) return;
        ToggleWithBanner("제작",
            () => CraftingManager.Instance?.ShowCraftingUI(),
            () => CraftingManager.Instance?.HideCraftingUI());
        PlayButtonSound();
    }
    private void OnenhancementButtonClicked()
    {
        if (IsButtonOnCooldown()) return;
        if (IsBlockedByTutorial(enhancementButton)) return;
        ToggleWithBanner("강화",
            () => EnhancementSystem.Instance?.ShowEnhancementUI(),
            () => EnhancementSystem.Instance?.HideEnhancementUI());
        PlayButtonSound();
    }
    private void OnRankingButtonClicked()
    {
        if (IsButtonOnCooldown()) return;
        if (IsBlockedByTutorial(rankingButton)) return;

        // ★ 이전에 열린 패널 닫기
        ForceCloseCurrentPanel();

        UIManager.Instance?.SetHeaderBanner("랭킹");
        if (RankingManager.Instance != null)
        {
            RankingManager.Instance.OpenPanel();
        }
        else
        {
            var rm = FindObjectOfType<RankingManager>(true);
            if (rm != null)
            {
                RankingManager.Instance = rm;
                rm.gameObject.SetActive(true);
                rm.OpenPanel();
            }
        }
        PlayButtonSound();
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    // 메뉴 토글 (토글 버튼 클릭 시 호출)
    // ─────────────────────────────────────────────────────────
    public void ToggleMenu()
    {
        // 초기화가 아직 안 됐으면 무시 (버튼 연타 방지)
        if (!menuInitialized)
        {
            Debug.LogWarning("[TopMenuManager] 아직 초기화 중입니다. 잠시 후 다시 누르세요.");
            return;
        }

        isExpanded = !isExpanded;

        if (enableSlideAnimation && menuRectTransform != null)
        {
            // 슬라이드 애니메이션 시작
            isAnimating = true;
        }
        else if (menuRectTransform != null)
        {
            // 애니메이션 없이 즉시 이동
            menuRectTransform.anchoredPosition = isExpanded ? expandedPosition : collapsedPosition;
        }

        TutorialManager.Instance?.OnActionCompleted("ToggleMenu");
        PlayButtonSound();

        Debug.Log($"[TopMenuManager] ToggleMenu → isExpanded={isExpanded}");
    }

    public void ExpandMenu() { if (!isExpanded) ToggleMenu(); }
    public void CollapseMenu() { if (isExpanded) ToggleMenu(); }

    // ─────────────────────────────────────────────────────────
    // 배지
    // ─────────────────────────────────────────────────────────
    public void ShowInventoryBadge() { if (inventoryBadge != null) inventoryBadge.SetActive(true); }
    public void ShowQuestBadge() { if (questBadge != null) questBadge.SetActive(true); }
    public void ShowShopBadge() { if (shopBadge != null) shopBadge.SetActive(true); }
    public void ShowAchieveBadge() { if (achieveBadge != null) achieveBadge.SetActive(true); }
    public void HideAllBadges()
    {
        if (inventoryBadge != null) inventoryBadge.SetActive(false);
        if (questBadge != null) questBadge.SetActive(false);
        if (shopBadge != null) shopBadge.SetActive(false);
        if (achieveBadge != null) achieveBadge.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────
    // 버튼 활성/비활성 제어
    // ─────────────────────────────────────────────────────────
    public void SetButtonInteractable(string buttonName, bool interactable)
    {
        Button b = null;
        switch (buttonName.ToLower())
        {
            case "inventory": b = inventoryButton; break;
            case "shop": b = shopButton; break;
            case "equipment": b = equipmentButton; break;
            case "skill": b = skillButton; break;
            case "achieve": b = achieveButton; break;
            case "mail": b = mailButton; break;
            case "craft": b = craftButton; break;
            case "auction": b = auctionButton; break;
            case "ranking": b = rankingButton; break;
            case "enhance": b = enhancementButton; break;
            case "settings": b = settingsButton; break;
        }
        if (b != null) b.interactable = interactable;
    }

    private void PlayButtonSound() { SoundManager.Instance?.PlayButtonClick(); }
}