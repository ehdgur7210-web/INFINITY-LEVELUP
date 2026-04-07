using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

/// <summary>
/// 개선된 상점 관리 시스템
/// - 일일/주간/월간 탭 전환
/// - 카테고리별 필터링
/// - 아이템 검색
/// - 할인 시스템 (자동 매핑)
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("상점 UI")]
    [HideInInspector] public GameObject shopPanel;
    [Tooltip("폴백용 — 탭 패널이 없을 때 사용 (일일/주간/월간 패널이 설정되어 있으면 무시됨)")]
    [SerializeField] private Transform shopSlotParent;
    [SerializeField] private GameObject shopSlotPrefab;

    /// <summary>
    /// 현재 탭에 맞는 슬롯 부모 반환
    /// dailyShopPanel/weeklyShopPanel/monthlyShopPanel을 직접 슬롯 부모로 사용
    /// (이 필드들이 각 탭의 Content GameObject로 설정되어 있음)
    /// </summary>
    private Transform GetCurrentSlotParent()
    {
        GameObject panel = currentTab switch
        {
            ShopTabType.Daily   => dailyShopPanel,
            ShopTabType.Weekly  => weeklyShopPanel,
            ShopTabType.Monthly => monthlyShopPanel,
            _ => null
        };
        return panel != null ? panel.transform : shopSlotParent;
    }

    [Header("상점 데이터")]
    [SerializeField] private ShopData shopData; // ScriptableObject로 관리

    private List<ShopPackage> currentPackages = new List<ShopPackage>();

    [Header("상점 탭 버튼")]
    [SerializeField] private Button dailyTabButton;
    [SerializeField] private Button weeklyTabButton;
    [SerializeField] private Button monthlyTabButton;

    [Header("상점 탭 패널 (Hierarchy 오브젝트)")]
    [Tooltip("일일상점 콘텐츠 패널")]
    [SerializeField] private GameObject dailyShopPanel;
    [Tooltip("주간상점 콘텐츠 패널")]
    [SerializeField] private GameObject weeklyShopPanel;
    [Tooltip("월간상점 콘텐츠 패널")]
    [SerializeField] private GameObject monthlyShopPanel;

    [Header("탭 버튼 색상")]
    [SerializeField] private Color activeTabColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color inactiveTabColor = new Color(0.55f, 0.55f, 0.55f);

    [Header("카테고리 필터")]
    [SerializeField] private Toggle allCategoryToggle;
    [SerializeField] private Toggle weaponCategoryToggle;
    [SerializeField] private Toggle armorCategoryToggle;
    [SerializeField] private Toggle consumableCategoryToggle;

    [Header("검색")]
    [SerializeField] private TMP_InputField searchInputField;
    [SerializeField] private Button searchButton;

    [Header("새로고침")]
    [SerializeField] private Button refreshButton;
    [SerializeField] private TextMeshProUGUI refreshTimerText;
    [SerializeField] private float refreshCooldown = 300f; // 5분

    [Header("표시 제한")]
    [SerializeField] private int maxDisplayCount = 8;

    // 현재 상태
    private List<ShopSlot> shopSlots = new List<ShopSlot>();
    private List<ItemData> currentItems = new List<ItemData>();
    private ShopTabType currentTab = ShopTabType.Daily;
    private ItemType currentFilter = ItemType.Misc; // 전체
    private float nextRefreshTime;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] ShopManager가 생성되었습니다.");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void OnDestroy()
    {
        CancelInvoke();
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        SetupTabButtons();
        InitializeShop();
        SetupUI();

        // ★ ScrollView Viewport의 raycastTarget 끄기 (버튼 클릭 차단 방지)
        FixViewportRaycast();

        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }

        // ✅ 매 프레임 타이머 체크 대신 1초마다 갱신
        InvokeRepeating(nameof(UpdateRefreshTimer), 1f, 1f);
    }

    /// <summary>
    /// ★ Shop 내 모든 ScrollRect의 Viewport Image raycastTarget을 끈다.
    /// Viewport Image가 raycast를 가로채면 자식 버튼이 눌리지 않는 버그 방지.
    /// </summary>
    private void FixViewportRaycast()
    {
        if (shopPanel == null) return;

        // shopPanel 하위 모든 ScrollRect의 Viewport 탐색
        ScrollRect[] scrollRects = shopPanel.GetComponentsInChildren<ScrollRect>(true);
        foreach (var sr in scrollRects)
        {
            if (sr.viewport != null)
            {
                Image vpImage = sr.viewport.GetComponent<Image>();
                if (vpImage != null)
                {
                    vpImage.raycastTarget = false;
                    Debug.Log($"[ShopManager] Viewport raycastTarget OFF: {sr.viewport.name}");
                }
            }
        }

        // shopSlotParent 상위에 Viewport가 있는 경우도 체크
        if (shopSlotParent != null)
        {
            Transform parent = shopSlotParent;
            while (parent != null && parent != shopPanel.transform)
            {
                if (parent.name.ToLower().Contains("viewport"))
                {
                    Image img = parent.GetComponent<Image>();
                    if (img != null && img.raycastTarget)
                    {
                        img.raycastTarget = false;
                        Debug.Log($"[ShopManager] 부모 Viewport raycastTarget OFF: {parent.name}");
                    }
                }
                parent = parent.parent;
            }
        }
    }

    // ══════════════════════════════════════════════════════
    //  탭 버튼 시스템
    // ══════════════════════════════════════════════════════

    private void SetupTabButtons()
    {
        if (dailyTabButton != null)
            dailyTabButton.onClick.AddListener(() => SwitchTab(ShopTabType.Daily));
        if (weeklyTabButton != null)
            weeklyTabButton.onClick.AddListener(() => SwitchTab(ShopTabType.Weekly));
        if (monthlyTabButton != null)
            monthlyTabButton.onClick.AddListener(() => SwitchTab(ShopTabType.Monthly));
    }

    /// <summary>탭 전환 — 해당 패널만 활성화, 아이템 새로 로드</summary>
    public void SwitchTab(ShopTabType tab)
    {
        currentTab = tab;
        SoundManager.Instance?.PlayButtonClick();

        // 패널 활성화/비활성화
        if (dailyShopPanel != null)  dailyShopPanel.SetActive(tab == ShopTabType.Daily);
        if (weeklyShopPanel != null) weeklyShopPanel.SetActive(tab == ShopTabType.Weekly);
        if (monthlyShopPanel != null) monthlyShopPanel.SetActive(tab == ShopTabType.Monthly);

        // 탭 버튼 색상 갱신
        UpdateTabColors();

        // 해당 탭 아이템 로드 및 표시
        LoadItemsForCurrentTab();
        RefreshShopDisplay();

        Debug.Log($"[ShopManager] 탭 전환: {tab}");
    }

    private void UpdateTabColors()
    {
        SetTabButtonColor(dailyTabButton, currentTab == ShopTabType.Daily);
        SetTabButtonColor(weeklyTabButton, currentTab == ShopTabType.Weekly);
        SetTabButtonColor(monthlyTabButton, currentTab == ShopTabType.Monthly);
    }

    private void SetTabButtonColor(Button btn, bool isActive)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null)
            img.color = isActive ? activeTabColor : inactiveTabColor;
    }

    /// <summary>현재 탭에 맞는 아이템 + 패키지를 ShopData에서 로드</summary>
    private void LoadItemsForCurrentTab()
    {
        if (shopData != null)
        {
            currentItems = shopData.GetItemsByTab(currentTab);
            currentPackages = shopData.GetPackagesByTab(currentTab);
        }
        else
        {
            Debug.LogWarning("[ShopManager] ShopData가 설정되지 않았습니다!");
            currentItems = new List<ItemData>();
            currentPackages = new List<ShopPackage>();
        }

        // 폴백: 아이템이 비어있으면 자동 로드
        if (currentItems == null || currentItems.Count == 0)
        {
            Debug.LogWarning($"[ShopManager] {currentTab} 아이템 0개 → 폴백 로드");
            currentItems = LoadItemsFallback();
        }
    }

    /// <summary>
    /// 상점 초기화
    /// </summary>
    private void InitializeShop()
    {
        // 일일상점 탭으로 초기화
        SwitchTab(ShopTabType.Daily);

        // 새로고침 타이머 초기화
        nextRefreshTime = Time.time + refreshCooldown;
    }

    /// <summary>
    /// ✅ 자동 폴백 로더
    /// 우선순위: ItemDatabase.allItems → Resources/Items → Resources/Equipments
    /// </summary>
    private List<ItemData> LoadItemsFallback()
    {
        var items = new List<ItemData>();

        // 1순위: ItemDatabase 싱글톤에서 로드
        if (ItemDatabase.Instance != null
            && ItemDatabase.Instance.allItems != null
            && ItemDatabase.Instance.allItems.Count > 0)
        {
            // ✅ 전설 등급만 필터링
            foreach (var item in ItemDatabase.Instance.allItems)
            {
                if (item != null && item.rarity == ItemRarity.Legendary)
                    items.Add(item);
            }

            // ✅ 장비도 전설만 필터링
            foreach (var eq in ItemDatabase.Instance.allEquipments)
            {
                if (eq != null && eq.rarity == ItemRarity.Legendary)
                    items.Add(eq);
            }

            Debug.Log("[ShopManager] 전설 아이템 " + items.Count + "개 로드 완료");
            return items;
        }

        // 2순위: Resources에서 직접 로드 후 필터링
        var resItems = Resources.LoadAll<ItemData>("Items");
        foreach (var item in resItems)
        {
            if (item != null && item.rarity == ItemRarity.Legendary)
                items.Add(item);
        }

        var resEquips = Resources.LoadAll<EquipmentData>("Equipments");
        foreach (var eq in resEquips)
        {
            if (eq != null && eq.rarity == ItemRarity.Legendary)
                items.Add(eq);
        }

        if (items.Count > 0)
        {
            Debug.Log("[ShopManager] Resources에서 전설 아이템 " + items.Count + "개 로드 완료");
            return items;
        }

        Debug.LogError("[ShopManager] 전설 아이템이 없습니다!");
        return items;
    }

    /// <summary>
    /// UI 설정
    /// </summary>
    private void SetupUI()
    {
        // 카테고리 필터 설정
        if (allCategoryToggle != null)
        {
            allCategoryToggle.onValueChanged.AddListener(isOn => {
                if (isOn) FilterByCategory(ItemType.Misc); // 전체
            });
        }

        if (weaponCategoryToggle != null)
        {
            weaponCategoryToggle.onValueChanged.AddListener(isOn => {
                if (isOn) FilterByCategory(ItemType.Weapon);
            });
        }

        if (armorCategoryToggle != null)
        {
            armorCategoryToggle.onValueChanged.AddListener(isOn => {
                if (isOn) FilterByCategory(ItemType.Armor);
            });
        }

        if (consumableCategoryToggle != null)
        {
            consumableCategoryToggle.onValueChanged.AddListener(isOn => {
                if (isOn) FilterByCategory(ItemType.Consumable);
            });
        }

        // 검색 버튼
        if (searchButton != null)
        {
            searchButton.onClick.AddListener(OnSearchClicked);
        }

        // 검색 입력 필드 (엔터 키)
        if (searchInputField != null)
        {
            searchInputField.onSubmit.AddListener(query => OnSearchClicked());
        }

        // 새로고침 버튼
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(OnRefreshClicked);
        }
    }

    /// <summary>
    /// 상점 표시 새로고침
    /// </summary>
    private void RefreshShopDisplay()
    {
        // 기존 슬롯 제거
        ClearShopSlots();

        // 필터링 및 정렬된 아이템 가져오기
        List<ItemData> displayItems = GetFilteredAndSortedItems();

        // 새 슬롯 생성 (null 항목 필터링)
        int nullCount = displayItems.RemoveAll(i => i == null);
        if (nullCount > 0)
            Debug.LogWarning("[ShopManager] null 아이템 " + nullCount + "개 제거됨 (ItemDatabase 빈 슬롯 확인 필요)");

        // ★ 최대 표시 개수 제한
        int displayCount = Mathf.Min(displayItems.Count, maxDisplayCount);
        for (int i = 0; i < displayCount; i++)
        {
            CreateShopSlot(displayItems[i]);
        }

        // ★ 교환 패키지 슬롯 (젬으로 동료티켓/골드 교환)
        if (currentPackages != null)
        {
            foreach (var pkg in currentPackages)
            {
                if (pkg != null) CreatePackageSlot(pkg);
            }
        }

        Debug.Log($"상점 새로고침: {displayCount}/{displayItems.Count}개 아이템 + {currentPackages?.Count ?? 0}개 패키지 표시");
    }

    private void CreatePackageSlot(ShopPackage pkg)
    {
        Transform parent = GetCurrentSlotParent();
        if (shopSlotPrefab == null || parent == null) return;

        GameObject slotObj = Instantiate(shopSlotPrefab, parent);
        ShopSlot slot = slotObj.GetComponent<ShopSlot>();
        if (slot == null)
        {
            Destroy(slotObj);
            return;
        }

        slot.SetupPackage(pkg);
        shopSlots.Add(slot);
    }

    /// <summary>
    /// 필터링 및 정렬된 아이템 가져오기
    /// </summary>
    public List<ItemData> GetFilteredAndSortedItems()
    {
        List<ItemData> items = new List<ItemData>(currentItems);

        // 카테고리 필터 — Equipment 자동 매칭
        if (currentFilter != ItemType.Misc) // Misc는 전체
        {
            items = items.Where(item =>
            {
                if (item == null) return false;
                if (item.itemType == currentFilter) return true;

                // ★ EquipmentData는 EquipmentType으로 카테고리 매칭
                if (item is EquipmentData eq)
                {
                    if (currentFilter == ItemType.Weapon)
                        return eq.equipmentType == EquipmentType.WeaponLeft || eq.equipmentType == EquipmentType.WeaponRight;
                    if (currentFilter == ItemType.Armor)
                        return eq.equipmentType == EquipmentType.Helmet || eq.equipmentType == EquipmentType.Armor
                            || eq.equipmentType == EquipmentType.Gloves || eq.equipmentType == EquipmentType.Boots;
                }
                return false;
            }).ToList();
        }

        // 검색어 필터
        if (searchInputField != null && !string.IsNullOrEmpty(searchInputField.text))
        {
            string query = searchInputField.text.ToLower();
            items = items.Where(item =>
                item.itemName.ToLower().Contains(query) ||
                item.itemDescription.ToLower().Contains(query)
            ).ToList();
        }

        return items;
    }

    /// <summary>
    /// 상점 슬롯 생성 (자동 매핑 적용)
    /// </summary>
    private void CreateShopSlot(ItemData item)
    {
        // ✅ item 자체 null 방어 - ItemDatabase allItems에 빈 슬롯이 있으면 발생
        if (item == null)
        {
            Debug.LogWarning("[ShopManager] CreateShopSlot: item null - 건너뜀");
            return;
        }

        Transform parent = GetCurrentSlotParent();
        if (shopSlotPrefab == null || parent == null)
        {
            Debug.LogError($"[ShopManager] shopSlotPrefab 또는 슬롯부모가 null! 탭={currentTab}");
            return;
        }

        GameObject slotObj = Instantiate(shopSlotPrefab, parent);
        ShopSlot slot = slotObj.GetComponent<ShopSlot>();

        // ✅ SlotPrefab에 ShopSlot 컴포넌트가 없는 경우 방어
        if (slot == null)
        {
            Debug.LogError("[ShopManager] shopSlotPrefab에 ShopSlot 컴포넌트가 없습니다!");
            Destroy(slotObj);
            return;
        }

        // 할인율 계산 (자동 매핑 사용)
        ItemType categoryForDiscount = item.itemType;

        // EquipmentData인 경우 EquipmentType을 ItemType으로 자동 변환
        if (item is EquipmentData equipment)
        {
            categoryForDiscount = equipment.equipmentType.ToShopCategory();
        }

        float discountRate = shopData != null ?
            shopData.GetDiscountRate(categoryForDiscount) : 0f;
        int finalPrice = Mathf.RoundToInt(item.buyPrice * (1f - discountRate));

        slot.SetupSlot(item, finalPrice);
        shopSlots.Add(slot);
        Debug.Log("[ShopManager] 슬롯 생성 완료: " + item.itemName + " / " + finalPrice + "G");
    }

    /// <summary>
    /// 상점 슬롯 모두 제거
    /// </summary>
    private void ClearShopSlots()
    {
        foreach (ShopSlot slot in shopSlots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }
        shopSlots.Clear();
    }

    #region UI 이벤트 핸들러

    /// <summary>
    /// 카테고리 필터
    /// </summary>
    private void FilterByCategory(ItemType itemType)
    {
        currentFilter = itemType;
        RefreshShopDisplay();
    }

    /// <summary>
    /// 검색 버튼 클릭
    /// </summary>
    private void OnSearchClicked()
    {
        RefreshShopDisplay();
    }

    /// <summary>
    /// 새로고침 버튼 클릭
    /// </summary>
    private void OnRefreshClicked()
    {
        if (Time.time >= nextRefreshTime)
        {
            // 현재 탭의 아이템 다시 로드
            LoadItemsForCurrentTab();
            RefreshShopDisplay();
            nextRefreshTime = Time.time + refreshCooldown;

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMessage("상점 새로고침 완료!");
            }
        }
        else
        {
            float remainingTime = nextRefreshTime - Time.time;
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMessage(
                    $"새로고침 대기 중... ({Mathf.CeilToInt(remainingTime)}초)"
                );
            }
        }
    }

    #endregion

    /// <summary>
    /// 새로고침 타이머 업데이트
    /// </summary>
    void UpdateRefreshTimer()
    {
        if (refreshTimerText == null) return;

        if (Time.time >= nextRefreshTime)
        {
            refreshTimerText.text = "새로고침 가능!";
        }
        else
        {
            float remainingTime = nextRefreshTime - Time.time;
            int minutes = Mathf.FloorToInt(remainingTime / 60f);
            int seconds = Mathf.FloorToInt(remainingTime % 60f);
            refreshTimerText.text = $"새로고침: {minutes:00}:{seconds:00}";
        }
    }

    /// <summary>
    /// 상점 토글
    /// </summary>
    public void ToggleShop()
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.ShouldBlockNonFocusButtons) return;
        if (shopPanel != null)
        {
            bool isActive = shopPanel.activeSelf;
            shopPanel.SetActive(!isActive);

            if (!isActive)
            {
                // 상점 열 때 현재 탭 상태 복원
                SwitchTab(currentTab);
            }
            else
            {
                // ★ 상점 닫을 때 배너 복원
                TopMenuManager.Instance?.ClearBanner();
            }
        }
        else
        {
            Debug.LogError("shopPanel이 null입니다! Inspector에서 연결 필요!");
        }
    }

    public void ShowShop()
    {
        if (shopPanel == null) return;
        if (shopPanel.activeSelf) return;
        shopPanel.SetActive(true);
        SwitchTab(currentTab);
    }

    public void HideShop()
    {
        if (shopPanel == null) return;
        shopPanel.SetActive(false);
        TopMenuManager.Instance?.ClearBanner();
    }

    /// <summary>
    /// 아이템 구매
    /// </summary>
    public void BuyItem(ItemData item, int price)
    {
        if (item == null || GameManager.Instance == null) return;

        // 골드 확인 및 차감
        if (GameManager.Instance.SpendGold(price))
        {
            // 인벤토리에 아이템 추가
            if (InventoryManager.Instance != null)
            {
                if (InventoryManager.Instance.AddItem(item, 1))
                {
                    // ★ 구매 성공 효과음
                    SoundManager.Instance?.PlayPurchaseSound();
                    SaveLoadManager.Instance?.SaveGame();
                    Debug.Log($"{item.itemName} 구매 성공! (-{price} 골드)");

                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.ShowMessage(
                            $"{item.itemName} 구매 완료!",
                            Color.green
                        );
                    }
                }
                else
                {
                    // 인벤토리 가득 참 - 골드 환불
                    GameManager.Instance.AddGold(price);

                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.ShowMessage(
                            "인벤토리가 가득 찼습니다!",
                            Color.red
                        );
                    }
                }
            }
        }
        else
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMessage(
                    "골드가 부족합니다!",
                    Color.red
                );
            }
        }
    }

    /// <summary>
    /// ★ 패키지 구매 (젬 → 동료티켓/장비티켓/골드/젬 교환)
    /// </summary>
    public void BuyPackage(ShopPackage pkg)
    {
        if (pkg == null || GameManager.Instance == null) return;

        // 젬 확인
        if (GameManager.Instance.PlayerGem < pkg.gemCost)
        {
            UIManager.Instance?.ShowConfirmDialog(
                $"다이아가부족합니다.\n필요:{pkg.gemCost}\n보유:{GameManager.Instance.PlayerGem}",
                onConfirm: null);
            return;
        }

        // 차감
        if (!GameManager.Instance.SpendGem(pkg.gemCost))
            return;

        // 보상 지급
        switch (pkg.rewardType)
        {
            case PackageRewardType.CompanionTicket:
                if (ResourceBarManager.Instance != null)
                    ResourceBarManager.Instance.AddCompanionTickets(pkg.rewardAmount);
                else if (GameDataBridge.CurrentData != null)
                    GameDataBridge.CurrentData.companionTickets += pkg.rewardAmount;
                break;

            case PackageRewardType.EquipmentTicket:
                if (ResourceBarManager.Instance != null)
                    ResourceBarManager.Instance.AddEquipmentTickets(pkg.rewardAmount);
                else if (GameDataBridge.CurrentData != null)
                    GameDataBridge.CurrentData.equipmentTickets += pkg.rewardAmount;
                break;

            case PackageRewardType.Gold:
                GameManager.Instance.AddGold(pkg.rewardAmount);
                break;

            case PackageRewardType.Gem:
                GameManager.Instance.AddGem(pkg.rewardAmount);
                break;

            case PackageRewardType.CropPoint:
                if (FarmManager.Instance != null)
                    FarmManager.Instance.AddCropPoints(pkg.rewardAmount);
                else if (GameDataBridge.CurrentData != null)
                    GameDataBridge.CurrentData.cropPoints += pkg.rewardAmount;
                break;
        }

        SoundManager.Instance?.PlayPurchaseSound();
        SaveLoadManager.Instance?.SaveGame();

        UIManager.Instance?.ShowMessage(
            $"{pkg.packageName}구매완료!",
            Color.green);

        Debug.Log($"[ShopManager] 패키지 구매: {pkg.packageName} -{pkg.gemCost}젬 → {pkg.rewardType} +{pkg.rewardAmount}");
    }

    /// <summary>
    /// 아이템 판매
    /// </summary>
    public void SellItem(ItemData item, int count = 1)
    {
        if (item == null) return;

        // 인벤토리에서 아이템 제거
        if (InventoryManager.Instance != null && InventoryManager.Instance.RemoveItem(item, count))
        {
            // 판매 가격 계산
            int totalPrice = item.sellPrice * count;

            // 골드 추가
            if (GameManager.Instance != null)
            {
                // ★ 판매 성공 효과음
                SoundManager.Instance?.PlaySellItem();
                GameManager.Instance.AddGold(totalPrice);

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowMessage(
                        $"{item.itemName} x{count} 판매! +{totalPrice}G",
                        Color.yellow
                    );
                }
            }

            Debug.Log($"{item.itemName} {count}개 판매! (+{totalPrice} 골드)");
        }
    }
}

// ★ ShopData, ShopTabType, ShopPackage, PackageRewardType, CategoryDiscount는
//   Assets/Script/Code/ShopData.cs 파일로 분리됨 (None script 에러 방지)