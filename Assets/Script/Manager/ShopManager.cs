using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

/// <summary>
/// 개선된 상점 관리 시스템
/// - 카테고리별 필터링
/// - 아이템 검색
/// - 정렬 기능
/// - 할인 시스템 (자동 매핑)
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("상점 UI")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Transform shopSlotParent;
    [SerializeField] private GameObject shopSlotPrefab;

    [Header("상점 데이터")]
    [SerializeField] private ShopData shopData; // ScriptableObject로 관리

    [Header("카테고리 필터")]
    [SerializeField] private Toggle allCategoryToggle;
    [SerializeField] private Toggle weaponCategoryToggle;
    [SerializeField] private Toggle armorCategoryToggle;
    [SerializeField] private Toggle consumableCategoryToggle;

    [Header("검색")]
    [SerializeField] private TMP_InputField searchInputField;
    [SerializeField] private Button searchButton;

    [Header("정렬")]
    [SerializeField] private TMP_Dropdown sortDropdown;

    [Header("새로고침")]
    [SerializeField] private Button refreshButton;
    [SerializeField] private TextMeshProUGUI refreshTimerText;
    [SerializeField] private float refreshCooldown = 300f; // 5분

    // 현재 상태
    private List<ShopSlot> shopSlots = new List<ShopSlot>();
    private List<ItemData> currentItems = new List<ItemData>();
    private ItemType currentFilter = ItemType.Misc; // 전체
    private SortType currentSort = SortType.Default;
    private float nextRefreshTime;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        InitializeShop();
        SetupUI();
        SetupSortDropdown();

        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            ToggleShop();
        }

        // 새로고침 타이머 업데이트
        UpdateRefreshTimer();
    }

    /// <summary>
    /// 상점 초기화
    /// ✅ 수정: 인트로씬 전환 후 shopData SO 참조가 깨지는 경우를 대비해
    ///          ItemDatabase → Resources 순으로 자동 폴백 로드
    /// </summary>
    private void InitializeShop()
    {
        // ShopData에서 아이템 로드
        if (shopData != null)
        {
            currentItems = shopData.GetAllItems();
        }
        else
        {
            Debug.LogWarning("[ShopManager] ShopData가 설정되지 않았습니다!");
        }

        // ✅ 핵심 수정: 기존 코드는 shopData null이면 return으로 중단됐음
        //    인트로 → 메인씬 전환 시 currentItems가 비어있으면 자동 폴백
        if (currentItems == null || currentItems.Count == 0)
        {
            Debug.LogWarning("[ShopManager] 아이템 0개 감지 → 자동 폴백 로드 시작");
            currentItems = LoadItemsFallback();
        }

        // 상점 아이템 표시
        RefreshShopDisplay();

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

    private void SetupSortDropdown()
    {
        if (sortDropdown == null) return;

        sortDropdown.ClearOptions();

        List<string> options = new List<string>
        {
            "일일상점",
            "주간상점",
            "월간상점",
        };

        sortDropdown.AddOptions(options);
        sortDropdown.value = 0;
        sortDropdown.RefreshShownValue();

        Debug.Log($"드롭다운 옵션 {options.Count}개 추가됨");
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

        // 정렬 드롭다운
        if (sortDropdown != null)
        {
            sortDropdown.onValueChanged.AddListener(OnSortChanged);
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

        foreach (ItemData item in displayItems)
        {
            CreateShopSlot(item);
        }

        Debug.Log($"상점 새로고침: {displayItems.Count}개 아이템 표시");
    }

    /// <summary>
    /// 필터링 및 정렬된 아이템 가져오기
    /// </summary>
    public List<ItemData> GetFilteredAndSortedItems()
    {
        List<ItemData> items = new List<ItemData>(currentItems);

        // 카테고리 필터
        if (currentFilter != ItemType.Misc) // Misc는 전체
        {
            items = items.Where(item => item.itemType == currentFilter).ToList();
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

        // 정렬
        items = SortItems(items, currentSort);

        return items;
    }

    /// <summary>
    /// 아이템 정렬
    /// </summary>
    private List<ItemData> SortItems(List<ItemData> items, SortType sortType)
    {
        switch (sortType)
        {
            case SortType.PriceAscending:
                return items.OrderBy(item => item.buyPrice).ToList();

            case SortType.PriceDescending:
                return items.OrderByDescending(item => item.buyPrice).ToList();

            case SortType.RarityAscending:
                return items.OrderBy(item => item.rarity).ToList();

            case SortType.RarityDescending:
                return items.OrderByDescending(item => item.rarity).ToList();

            case SortType.Name:
                return items.OrderBy(item => item.itemName).ToList();

            case SortType.Default:
            default:
                return items; // 기본 순서 유지
        }
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

        if (shopSlotPrefab == null || shopSlotParent == null)
        {
            Debug.LogError("[ShopManager] shopSlotPrefab 또는 shopSlotParent가 null! Inspector 연결 필요!");
            return;
        }

        GameObject slotObj = Instantiate(shopSlotPrefab, shopSlotParent);
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
    /// 정렬 변경
    /// </summary>
    private void OnSortChanged(int index)
    {
        currentSort = (SortType)index;
        RefreshShopDisplay();
    }

    /// <summary>
    /// 새로고침 버튼 클릭
    /// </summary>
    private void OnRefreshClicked()
    {
        if (Time.time >= nextRefreshTime)
        {
            // 상점 아이템 재구성
            if (shopData != null)
            {
                currentItems = shopData.GetRandomItems(6); // 랜덤 6개
            }
            else
            {
                // ✅ shopData 없으면 폴백에서 다시 로드
                currentItems = LoadItemsFallback();
            }

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
    private void UpdateRefreshTimer()
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
        if (shopPanel != null)
        {
            bool isActive = shopPanel.activeSelf;
            shopPanel.SetActive(!isActive);

            if (!isActive)
            {
                // ✅ 상점 열 때 아이템이 비어있으면 다시 로드 시도
                if (currentItems == null || currentItems.Count == 0)
                    currentItems = LoadItemsFallback();

                RefreshShopDisplay();
            }
        }
        else
        {
            Debug.LogError("shopPanel이 null입니다! Inspector에서 연결 필요!");
        }
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

/// <summary>
/// 상점 데이터 ScriptableObject
/// - 판매할 아이템 목록
/// - 할인율 설정
/// - 랜덤 아이템 생성
/// </summary>
[CreateAssetMenu(fileName = "ShopData", menuName = "Game/Shop Data")]
public class ShopData : ScriptableObject
{
    [Header("기본 아이템")]
    public List<ItemData> baseItems = new List<ItemData>();

    [Header("희귀 아이템 (낮은 확률로 등장)")]
    public List<ItemData> rareItems = new List<ItemData>();

    [Header("할인 설정")]
    public List<CategoryDiscount> discounts = new List<CategoryDiscount>();

    /// <summary>
    /// 모든 아이템 가져오기
    /// </summary>
    public List<ItemData> GetAllItems()
    {
        List<ItemData> allItems = new List<ItemData>();
        allItems.AddRange(baseItems);

        // 희귀 아이템은 30% 확률로 추가
        foreach (var item in rareItems)
        {
            if (Random.Range(0f, 1f) <= 0.3f)
            {
                allItems.Add(item);
            }
        }

        return allItems;
    }

    /// <summary>
    /// 랜덤 아이템 가져오기
    /// </summary>
    public List<ItemData> GetRandomItems(int count)
    {
        List<ItemData> allItems = GetAllItems();

        // 셔플
        for (int i = 0; i < allItems.Count; i++)
        {
            int randomIndex = Random.Range(i, allItems.Count);
            ItemData temp = allItems[i];
            allItems[i] = allItems[randomIndex];
            allItems[randomIndex] = temp;
        }

        // 원하는 개수만큼 반환
        return allItems.Take(Mathf.Min(count, allItems.Count)).ToList();
    }

    /// <summary>
    /// 카테고리별 할인율 가져오기
    /// </summary>
    public float GetDiscountRate(ItemType itemType)
    {
        foreach (var discount in discounts)
        {
            if (discount.itemType == itemType)
            {
                return discount.discountRate;
            }
        }
        return 0f;
    }
}

/// <summary>
/// 카테고리별 할인 설정
/// </summary>
[System.Serializable]
public class CategoryDiscount
{
    public ItemType itemType;
    [Range(0f, 1f)]
    public float discountRate; // 0.2 = 20% 할인
}

/// <summary>
/// 정렬 타입
/// </summary>
public enum SortType
{
    Default,            // 기본 (등록 순서)
    PriceAscending,     // 가격 오름차순
    PriceDescending,    // 가격 내림차순
    RarityAscending,    // 희귀도 오름차순
    RarityDescending,   // 희귀도 내림차순
    Name                // 이름순
}