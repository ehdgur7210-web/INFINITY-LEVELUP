using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ============================================================
/// TopMenuManager - 우측 상단 메뉴 버튼 관리 (사운드 + 옵션 추가 버전)
/// ============================================================
/// 
/// 【변경사항】
/// ★ 추가: settingsButton (설정/옵션 버튼)
/// ★ 추가: PlayButtonSound()에 SoundManager 연동
/// ★ 추가: 각 패널 열기/닫기 시 효과음 재생
/// ============================================================
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

    // ★★★ 새로 추가: 설정(옵션) 버튼 ★★★
    [Header("===== ★ 새로 추가: 설정 버튼 =====")]
    [Tooltip("옵션/설정 창을 여는 버튼 (Inspector에서 연결)")]
    [SerializeField] private Button settingsButton;

    [Header("알림 배지 (선택)")]
    [SerializeField] private GameObject inventoryBadge;
    [SerializeField] private GameObject questBadge;
    [SerializeField] private GameObject shopBadge;

    [Header("메뉴 패널 (접기/펼치기 기능)")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button toggleButton;
    [SerializeField] private bool startExpanded = false;
    [SerializeField] private GameObject equipmentPanel;

    [Header("애니메이션")]
    [SerializeField] private bool enableSlideAnimation = true;
    [SerializeField] private float slideSpeed = 5f;

    private bool isExpanded;
    private Vector3 expandedPosition;
    private Vector3 collapsedPosition;
    private RectTransform menuRectTransform;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        SetupButtons();
        InitializeMenu();
        HideAllBadges();
    }

    void Update()
    {
        if (enableSlideAnimation && menuRectTransform != null)
        {
            UpdateSlideAnimation();
        }
    }

    private void SetupButtons()
    {
        if (inventoryButton != null)
        {
            inventoryButton.onClick.AddListener(OnInventoryButtonClicked);
        }

        if (shopButton != null)
        {
            shopButton.onClick.AddListener(OnShopButtonClicked);
        }

        if (equipmentButton != null)
        {
            equipmentButton.onClick.AddListener(OnEquipmentButtonClicked);
        }

        if (skillButton != null)
        {
            skillButton.onClick.AddListener(OnSkillButtonClicked);
        }

        if (mailButton != null)
        {
            mailButton.onClick.AddListener(OnmailButtonClicked);
        }

        if (achieveButton != null)
        {
            achieveButton.onClick.AddListener(OnAchieveButtonClicked);
        }

        if (craftButton != null)
        {
            craftButton.onClick.AddListener(OnCraftButtonClicked);
        }

        if (auctionButton != null)
        {
            auctionButton.onClick.AddListener(OnAuctionButtonClicked);
        }

        if (enhancementButton != null)
        {
            enhancementButton.onClick.AddListener(OnenhancementButtonClicked);
        }

        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleMenu);
        }

        // ★★★ 설정 버튼 이벤트 연결 ★★★
        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        }
    }

    private void InitializeMenu()
    {
        if (menuPanel == null) return;

        menuRectTransform = menuPanel.GetComponent<RectTransform>();

        if (menuRectTransform != null)
        {
            expandedPosition = menuRectTransform.anchoredPosition;
            collapsedPosition = expandedPosition + new Vector3(menuRectTransform.rect.width + 50, 0, 0);

            isExpanded = startExpanded;

            if (!isExpanded)
            {
                menuRectTransform.anchoredPosition = collapsedPosition;
            }
        }
    }

    private void UpdateSlideAnimation()
    {
        Vector3 targetPosition = isExpanded ? expandedPosition : collapsedPosition;
        menuRectTransform.anchoredPosition = Vector3.Lerp(
            menuRectTransform.anchoredPosition,
            targetPosition,
            Time.deltaTime * slideSpeed
        );
    }

    #region 버튼 클릭 이벤트

    private void OnInventoryButtonClicked()
    {
        Debug.Log("[TopMenu] 인벤토리 버튼 클릭");

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ToggleInventory();
        }

        if (inventoryBadge != null)
        {
            inventoryBadge.SetActive(false);
        }

        PlayButtonSound();
    }

    private void OnShopButtonClicked()
    {
        Debug.Log("[TopMenu] 상점 버튼 클릭");

        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.ToggleShop();
        }

        if (shopBadge != null)
        {
            shopBadge.SetActive(false);
        }

        PlayButtonSound();
    }

    void OnAuctionButtonClicked()
    {
        Debug.Log("[TopMenu] 경매장 버튼 클릭");

        AuctionUI auctionUI = FindObjectOfType<AuctionUI>();
        if (auctionUI != null)
        {
            auctionUI.ToggleAuction();
        }
        else
        {
            Debug.LogError("AuctionUI를 찾을 수 없음!");
        }

        PlayButtonSound();
    }

    private void OnAchieveButtonClicked()
    {
        Debug.Log("[TopMenu] 업적 버튼 클릭");

        if (AchievementSystem.Instance != null)
        {
            AchievementSystem.Instance.ToggleAchievementUI();
        }

        if (shopBadge != null)
        {
            shopBadge.SetActive(false);
        }

        PlayButtonSound();
    }

    private void OnEquipmentButtonClicked()
    {
        Debug.Log("[TopMenu] 장비 버튼 클릭");

        ToggleEquipment();

        if (questBadge != null)
        {
            questBadge.SetActive(false);
        }

        PlayButtonSound();
    }

    private void OnSkillButtonClicked()
    {
        Debug.Log("[TopMenu] 스킬 버튼 클릭");

        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.ToggleSkillTree();
        }

        PlayButtonSound();
    }

    private void OnmailButtonClicked()
    {
        Debug.Log("[TopMenu] 메일 버튼 클릭");

        if (MailUI.Instance != null)
        {
            MailUI.Instance.OpenMailPanel();
        }

        PlayButtonSound();
    }

    // ★★★ 수정된 설정 버튼 - OptionUI 연동 ★★★
    private void OnSettingsButtonClicked()
    {
        Debug.Log("[TopMenu] 설정 버튼 클릭");

        // OptionUI 싱글톤을 통해 옵션 패널 토글
        if (OptionUI.Instance != null)
        {
            OptionUI.Instance.ToggleOptionPanel();
        }
        else
        {
            Debug.LogWarning("[TopMenu] OptionUI 인스턴스를 찾을 수 없습니다!");
        }

        PlayButtonSound();
    }

    private void OnCraftButtonClicked()
    {
        Debug.Log("[TopMenu] 제작 버튼 클릭");

        if (CraftingManager.Instance != null)
        {
            CraftingManager.Instance.ToggleCraftingUI();
        }

        PlayButtonSound();
    }

    private void OnenhancementButtonClicked()
    {
        Debug.Log("[TopMenu] 강화 버튼 클릭");

        if (EnhancementSystem.Instance != null)
        {
            EnhancementSystem.Instance.ToggleEnhancementUI();
        }

        PlayButtonSound();
    }

    #endregion

    #region 메뉴 접기/펼치기

    public void ToggleMenu()
    {
        isExpanded = !isExpanded;
        Debug.Log($"[TopMenu] 메뉴 {(isExpanded ? "펼치기" : "접기")}");

        if (!enableSlideAnimation && menuRectTransform != null)
        {
            menuRectTransform.anchoredPosition = isExpanded ? expandedPosition : collapsedPosition;
        }

        TutorialManager.Instance?.OnActionCompleted("ToggleMenu");
        PlayButtonSound();
    }

    private void ToggleEquipment()
    {
        if (equipmentPanel != null)
        {
            bool isActive = equipmentPanel.activeSelf;
            equipmentPanel.SetActive(!isActive);
            Debug.Log(isActive ? "장비창 닫힘" : "장비창 열림");
        }
        else
        {
            Debug.LogWarning("[TopMenuManager] equipmentPanel이 연결되지 않았습니다!");
        }
    }

    public void ExpandMenu()
    {
        if (!isExpanded)
        {
            ToggleMenu();
        }
    }

    public void CollapseMenu()
    {
        if (isExpanded)
        {
            ToggleMenu();
        }
    }

    #endregion

    #region 알림 배지 관리

    public void ShowInventoryBadge()
    {
        if (inventoryBadge != null)
        {
            inventoryBadge.SetActive(true);
        }
    }

    public void ShowQuestBadge()
    {
        if (questBadge != null)
        {
            questBadge.SetActive(true);
        }
    }

    public void ShowShopBadge()
    {
        if (shopBadge != null)
        {
            shopBadge.SetActive(true);
        }
    }

    public void HideAllBadges()
    {
        if (inventoryBadge != null) inventoryBadge.SetActive(false);
        if (questBadge != null) questBadge.SetActive(false);
        if (shopBadge != null) shopBadge.SetActive(false);
    }

    #endregion

    #region 유틸리티

    // ★★★ 수정됨: SoundManager 연동 ★★★
    /// <summary>
    /// 버튼 클릭 효과음 재생
    /// SoundManager가 있으면 SoundManager를 통해 재생
    /// </summary>
    private void PlayButtonSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayButtonClick();
        }
    }

    public void SetButtonInteractable(string buttonName, bool interactable)
    {
        Button button = null;

        switch (buttonName.ToLower())
        {
            case "inventory":
                button = inventoryButton;
                break;
            case "shop":
                button = shopButton;
                break;
            case "equipment":
                button = equipmentButton;
                break;
            case "skill":
                button = skillButton;
                break;
            case "achieve":
                button = achieveButton;
                break;
            case "mail":
                button = mailButton;
                break;
            case "craft":
                button = craftButton;
                break;
            case "auction":
                button = auctionButton;
                break;
            case "enhance":
                button = enhancementButton;
                break;
            // ★ 추가: settings
            case "settings":
                button = settingsButton;
                break;
        }

        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    #endregion
}