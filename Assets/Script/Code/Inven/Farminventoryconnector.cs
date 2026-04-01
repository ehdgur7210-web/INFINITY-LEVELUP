using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// FarmInventoryConnector — 농장 수확 → 인벤토리 연동 브리지
///
/// [동작 흐름]
///   FarmManager.OnHarvestComplete (UnityEvent)
///     + FarmManagerExtension.OnHarvestCompleteStatic (정적 이벤트)
///   → OnHarvestReceived()
///   → FarmVegetable/FarmFruit 타입 아이템을:
///     1. MainScene (InventoryManager 있음) → InventoryManager.AddItem() + RefreshInventoryUI()
///     2. FarmScene (InventoryManager 없음) → GameDataBridge.CurrentData.inventoryItems 배열에 직접 추가
///   → 인벤 가득 차면 MailManager로 오버플로
///   → 자체 farmInventory (경매/판매 UI용) 에도 동시 추가
/// </summary>
public class FarmInventoryConnector : MonoBehaviour
{
    public static FarmInventoryConnector Instance;

    [Header("Farm 슬롯 설정")]
    public GameObject farmSlotPrefab;
    public Transform farmSlotParent;
    public int farmInventorySize = 20;

    [System.Serializable]
    public class FarmItem
    {
        public string itemName;
        public Sprite icon;
        public int count;
        public CropItemType itemType;
        public int sellPrice;
        public int itemID;   // ItemData.itemID 참조
    }

    public enum CropItemType { Vegetable, Fruit, Other }

    private List<FarmItem> farmInventory = new List<FarmItem>();
    private List<FarmSlotUI> slotUIs = new List<FarmSlotUI>();

    public IReadOnlyList<FarmItem> FarmInventory => farmInventory;

    // 정적 이벤트 구독 여부 추적
    private bool subscribedToStaticEvent = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] FarmInventoryConnector가 생성되었습니다.");
        }
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        InitializeSlots();
        SubscribeEvents();
    }

    void OnEnable()
    {
        // 씬 전환 후 재구독
        SubscribeStaticEvent();
    }

    void OnDisable()
    {
        UnsubscribeStaticEvent();
    }

    void OnDestroy()
    {
        // UnityEvent 해제
        if (FarmManager.Instance != null)
            FarmManager.Instance.OnHarvestComplete.RemoveListener(OnHarvestFromUnityEvent);

        UnsubscribeStaticEvent();
    }

    // ═══════════════════════════════════════════════════════════════
    //  이벤트 구독
    // ═══════════════════════════════════════════════════════════════

    private void SubscribeEvents()
    {
        // 1. FarmManager.OnHarvestComplete (UnityEvent) — FarmManager가 같은 씬에 있을 때
        if (FarmManager.Instance != null)
        {
            FarmManager.Instance.OnHarvestComplete.RemoveListener(OnHarvestFromUnityEvent);
            FarmManager.Instance.OnHarvestComplete.AddListener(OnHarvestFromUnityEvent);
            Debug.Log("[FarmInventoryConnector] FarmManager UnityEvent 구독 완료");
        }

        // 2. FarmManagerExtension.OnHarvestCompleteStatic (정적 이벤트) — 어느 씬에서든 수신
        SubscribeStaticEvent();
    }

    private void SubscribeStaticEvent()
    {
        if (subscribedToStaticEvent) return;
        FarmManagerExtension.OnHarvestCompleteStatic += OnHarvestFromStaticEvent;
        subscribedToStaticEvent = true;
    }

    private void UnsubscribeStaticEvent()
    {
        if (!subscribedToStaticEvent) return;
        FarmManagerExtension.OnHarvestCompleteStatic -= OnHarvestFromStaticEvent;
        subscribedToStaticEvent = false;
    }

    // ═══════════════════════════════════════════════════════════════
    //  수확 이벤트 수신
    // ═══════════════════════════════════════════════════════════════

    // 중복 처리 방지: 같은 프레임에서 UnityEvent + StaticEvent 둘 다 올 수 있음
    private int lastHarvestFrame = -1;
    private int lastHarvestPlotIndex = -1;

    private void OnHarvestFromUnityEvent(int plotIndex, List<CropHarvestReward> rewards)
    {
        if (Time.frameCount == lastHarvestFrame && plotIndex == lastHarvestPlotIndex)
            return; // 이미 StaticEvent에서 처리됨
        lastHarvestFrame = Time.frameCount;
        lastHarvestPlotIndex = plotIndex;
        ProcessHarvest(plotIndex, rewards);
    }

    private void OnHarvestFromStaticEvent(int plotIndex, List<CropHarvestReward> rewards)
    {
        if (Time.frameCount == lastHarvestFrame && plotIndex == lastHarvestPlotIndex)
            return; // 이미 UnityEvent에서 처리됨
        lastHarvestFrame = Time.frameCount;
        lastHarvestPlotIndex = plotIndex;
        ProcessHarvest(plotIndex, rewards);
    }

    /// <summary>수확 보상 처리 — 인벤토리 연동 핵심</summary>
    private void ProcessHarvest(int plotIndex, List<CropHarvestReward> rewards)
    {
        if (rewards == null)
        {
            Debug.LogWarning("[FarmConnector] rewards가 null — 처리 중단");
            return;
        }

        Debug.Log($"[FarmConnector] 수확 이벤트 수신: plotIndex={plotIndex}, rewards={rewards.Count}개");

        foreach (var reward in rewards)
        {
            if (reward == null || reward.item == null)
            {
                Debug.LogWarning("[FarmConnector] reward 또는 reward.item이 null — 스킵");
                continue;
            }

            string itemName = reward.item.itemName;
            int amount = Mathf.Max(1, reward.minAmount);
            Debug.Log($"[FarmConnector] 이벤트 수신: {itemName} x{amount}");

            // ── reward.item을 우선 사용, ItemDatabase에서 재조회하여 확실한 ItemData 확보 ──
            ItemData item = reward.item;

            // ItemDatabase에서 itemID로 조회 → 최종 폴백 reward.item
            if (ItemDatabase.Instance != null)
            {
                ItemData dbItem = ItemDatabase.Instance.GetItemByID(item.itemID);
                if (dbItem != null)
                {
                    item = dbItem;
                    Debug.Log($"[FarmConnector] ItemData 조회 성공: ID={item.itemID}, type={item.itemType}");
                }
                else
                {
                    Debug.LogWarning($"[FarmConnector] ItemDatabase에서 ID={item.itemID} 조회 실패 — reward.item 사용");
                }
            }
            else
            {
                Debug.LogWarning("[FarmConnector] ItemDatabase.Instance가 null — reward.item 사용");
            }

            // FarmVegetable/FarmFruit 타입만 farmInventory 연동
            if (item.itemType != ItemType.FarmVegetable && item.itemType != ItemType.FarmFruit)
            {
                Debug.Log($"[FarmConnector] 비농작물 아이템 스킵: {item.itemName} (type={item.itemType})");
                continue;
            }

            // ★ 메인 인벤토리 추가는 FarmManager.HarvestCrop()에서 이미 처리함 (중복 방지)
            //   여기서는 farmInventory (경매/판매 UI용)만 추가

            // ── 자체 farmInventory (경매/UI용) ──
            AddFarmCrop(
                itemName: item.itemName,
                icon: item.itemIcon,
                count: amount,
                itemType: DetectItemType(item),
                sellPrice: item.sellPrice > 0 ? item.sellPrice : 10,
                itemID: item.itemID
            );

            Debug.Log($"[FarmConnector] 수확 처리 완료: {item.itemName} x{amount} (plot={plotIndex}, farmInventory 추가)");
        }

        SaveLoadManager.Instance?.SaveGame();
    }

    // ═══════════════════════════════════════════════════════════════
    //  메인 인벤토리 추가 (씬에 따라 분기)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// MainScene → InventoryManager.AddItem() + UI 갱신
    /// FarmScene → GameDataBridge.CurrentData.inventoryItems 직접 추가
    /// 인벤 가득 → MailManager 오버플로
    /// </summary>
    private bool AddToMainInventory(ItemData item, int amount)
    {
        // ── MainScene: InventoryManager 존재 ──
        if (InventoryManager.Instance != null)
        {
            bool added = InventoryManager.Instance.AddItem(item, amount);

            if (added)
            {
                // 기타 탭 즉시 갱신
                InventoryManager.Instance.RefreshInventoryUI();
                Debug.Log($"[FarmInventoryConnector→Inven] {item.itemName} x{amount} 추가 완료");
            }
            // AddItem 내부에서 인벤 가득 시 MailManager 오버플로 처리함
            return added;
        }

        // ── FarmScene: GameDataBridge 직접 기록 ──
        return AddItemToSaveData(item, amount);
    }

    /// <summary>
    /// GameDataBridge.CurrentData.inventoryItems에 직접 아이템 추가
    /// 메인씬 복귀 시 SaveLoadManager.ApplySaveData()에서 자동 반영됨
    /// </summary>
    private bool AddItemToSaveData(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return false;

        SaveData current = GameDataBridge.CurrentData;
        if (current == null)
        {
            Debug.LogWarning("[FarmInventoryConnector] GameDataBridge.CurrentData가 null!");
            return false;
        }

        // 배열 → 리스트 변환
        List<InventoryItemData> list = current.inventoryItems != null
            ? new List<InventoryItemData>(current.inventoryItems)
            : new List<InventoryItemData>();

        // 같은 itemID가 있으면 수량 합산
        bool found = false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].itemID == item.itemID)
            {
                list[i].count += amount;
                found = true;
                Debug.Log($"[FarmInventoryConnector→SaveData] 기존 합산: {item.itemName} +{amount} (총 {list[i].count})");
                break;
            }
        }

        if (!found)
        {
            // 인벤 용량 체크 (InventoryManager 기본 30슬롯)
            int maxSlots = 30;
            if (list.Count >= maxSlots)
            {
                // MailManager 오버플로
                if (MailManager.Instance != null)
                {
                    MailManager.Instance.SendItemToMail(item, amount, "농장 수확 (인벤토리 꽉 참)");
                    UIManager.Instance?.ShowMessage(
                        $"인벤토리가 꽉 찼습니다!\n{item.itemName}이(가) 메일함으로 전송되었습니다.",
                        Color.yellow);
                    Debug.Log($"[FarmInventoryConnector→Mail] 오버플로: {item.itemName} x{amount} → 메일");
                    return true;
                }

                UIManager.Instance?.ShowMessage("인벤토리가 꽉 찼습니다!", Color.red);
                return false;
            }

            list.Add(new InventoryItemData
            {
                itemID = item.itemID,
                count = amount,
                slotIndex = -1,
                enhanceLevel = 0,
                isUnlocked = true,
                itemLevel = 0
            });
            Debug.Log($"[FarmInventoryConnector→SaveData] 신규 추가: {item.itemName} x{amount}");
        }

        current.inventoryItems = list.ToArray();
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  자체 Farm 인벤토리 (경매/판매 UI용)
    // ═══════════════════════════════════════════════════════════════

    private void InitializeSlots()
    {
        if (farmSlotPrefab == null || farmSlotParent == null)
        {
            Debug.LogWarning("[FarmInventoryConnector] farmSlotPrefab / farmSlotParent 미설정!");
            return;
        }
        slotUIs.Clear();
        for (int i = 0; i < farmInventorySize; i++)
        {
            GameObject go = Instantiate(farmSlotPrefab, farmSlotParent);
            FarmSlotUI slot = go.GetComponent<FarmSlotUI>() ?? go.AddComponent<FarmSlotUI>();
            slot.Init(this, i);
            slotUIs.Add(slot);
        }
        Debug.Log($"[FarmInventoryConnector] 슬롯 {farmInventorySize}개 초기화 완료");
    }

    /// <summary>Farm 인벤토리에 작물 추가 (내부 + 외부 호출 가능)</summary>
    public void AddFarmCrop(string itemName, Sprite icon,
                            int count, CropItemType itemType = CropItemType.Other,
                            int sellPrice = 10, int itemID = -1)
    {
        // 동일 아이템 합치기 (itemID 우선, 없으면 itemName 폴백)
        foreach (var existing in farmInventory)
        {
            bool match = (itemID > 0 && existing.itemID == itemID)
                      || (itemID <= 0 && existing.itemName == itemName);
            if (match)
            {
                existing.count += count;
                RefreshSlotUI();
                return;
            }
        }

        if (farmInventory.Count >= farmInventorySize)
        {
            UIManager.Instance?.ShowMessage("농장 인벤토리가 꽉 찼습니다!", Color.yellow);
            return;
        }

        farmInventory.Add(new FarmItem
        {
            itemName = itemName,
            icon = icon,
            count = count,
            itemType = itemType,
            sellPrice = sellPrice,
            itemID = itemID
        });

        RefreshSlotUI();
        Debug.Log($"[FarmInventoryConnector] {itemName} x{count} 추가 (farmInventory)");
    }

    public bool RemoveFarmCrop(string itemName, int count, int itemID = -1)
    {
        for (int i = 0; i < farmInventory.Count; i++)
        {
            bool match = (itemID > 0 && farmInventory[i].itemID == itemID)
                      || (itemID <= 0 && farmInventory[i].itemName == itemName);
            if (!match) continue;
            if (farmInventory[i].count < count)
            {
                UIManager.Instance?.ShowMessage("작물이 부족합니다!", Color.red);
                return false;
            }
            farmInventory[i].count -= count;
            if (farmInventory[i].count <= 0) farmInventory.RemoveAt(i);
            RefreshSlotUI();
            return true;
        }
        return false;
    }

    public int GetCropCount(string itemName, int itemID = -1)
    {
        foreach (var item in farmInventory)
        {
            bool match = (itemID > 0 && item.itemID == itemID)
                      || (itemID <= 0 && item.itemName == itemName);
            if (match) return item.count;
        }
        return 0;
    }

    public bool RegisterFarmCropToAuction(string itemName, int quantity, int startBid, int buyout, float duration, int itemID = -1)
    {
        FarmItem item = (itemID > 0)
            ? farmInventory.Find(f => f.itemID == itemID)
            : farmInventory.Find(f => f.itemName == itemName);
        if (item == null || item.count < quantity)
        {
            UIManager.Instance?.ShowMessage("작물이 부족합니다!", Color.red);
            return false;
        }

        float feeRate = AuctionManager.Instance != null ? AuctionManager.Instance.ListingFeePercent / 100f : 0.05f;
        int fee = Mathf.Max(1, Mathf.RoundToInt(startBid * quantity * feeRate));

        if (GameManager.Instance == null || !GameManager.Instance.SpendGold(fee))
        {
            UIManager.Instance?.ShowMessage($"등록 수수료 부족! ({fee:N0}G)", Color.red);
            return false;
        }

        RemoveFarmCrop(itemName, quantity, item.itemID);
        AuctionFarmCategory.Instance?.CreateFarmAuction(item, quantity, startBid, buyout, duration);
        UIManager.Instance?.ShowMessage($"{item.itemName} x{quantity} 경매 등록!", Color.green);
        return true;
    }

    private void RefreshSlotUI()
    {
        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (i < farmInventory.Count) slotUIs[i].SetItem(farmInventory[i]);
            else slotUIs[i].ClearSlot();
        }
    }

    /// <summary>아이템 타입 판별 (ItemData.itemType 기반, 폴백: 이름 매칭)</summary>
    private CropItemType DetectItemType(ItemData item)
    {
        if (item.itemType == ItemType.FarmVegetable) return CropItemType.Vegetable;
        if (item.itemType == ItemType.FarmFruit) return CropItemType.Fruit;

        // 이름 기반 폴백
        return DetectItemTypeByName(item.itemName);
    }

    private CropItemType DetectItemTypeByName(string name)
    {
        string lower = (name ?? "").ToLower();
        string[] vegetables = { "배추", "양파", "토마토", "감자", "고구마", "당근", "옥수수", "호박", "상추", "무" };
        string[] fruits = { "사과", "딸기", "포도", "복숭아", "블루베리", "귤", "수박", "바나나", "체리", "파인애플" };
        foreach (string v in vegetables) if (lower.Contains(v)) return CropItemType.Vegetable;
        foreach (string f in fruits) if (lower.Contains(f)) return CropItemType.Fruit;
        return CropItemType.Other;
    }

    // ═══════════════════════════════════════════════════════════════
    //  저장/로드
    // ═══════════════════════════════════════════════════════════════

    public FarmItem[] GetSaveData() => farmInventory.ToArray();

    public void LoadSaveData(FarmItem[] data)
    {
        farmInventory = new List<FarmItem>(data ?? new FarmItem[0]);
        RefreshSlotUI();
    }
}

// ═══════════════════════════════════════════════════════════════════
//  Farm 슬롯 UI 컴포넌트
// ═══════════════════════════════════════════════════════════════════

public class FarmSlotUI : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI countText;
    public Image backgroundImage;

    public Color vegetableColor = new Color(0.4f, 0.8f, 0.4f, 1f);
    public Color fruitColor = new Color(0.9f, 0.5f, 0.7f, 1f);
    public Color defaultColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    private FarmInventoryConnector.FarmItem currentItem;
    private FarmInventoryConnector connector;
    private int slotIndex;

    public void Init(FarmInventoryConnector conn, int index)
    {
        connector = conn;
        slotIndex = index;
        var btn = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        btn.onClick.AddListener(OnSlotClicked);
        ClearSlot();
    }

    public void SetItem(FarmInventoryConnector.FarmItem item)
    {
        currentItem = item;
        if (iconImage != null) { iconImage.sprite = item.icon; iconImage.color = Color.white; }
        if (nameText != null) nameText.text = item.itemName;
        if (countText != null) countText.text = $"x{item.count}";
        if (backgroundImage != null)
            backgroundImage.color = item.itemType switch
            {
                FarmInventoryConnector.CropItemType.Vegetable => vegetableColor,
                FarmInventoryConnector.CropItemType.Fruit => fruitColor,
                _ => defaultColor
            };
    }

    public void ClearSlot()
    {
        currentItem = null;
        if (iconImage != null) { iconImage.sprite = null; iconImage.color = defaultColor; }
        if (nameText != null) nameText.text = "";
        if (countText != null) countText.text = "";
        if (backgroundImage != null) backgroundImage.color = defaultColor;
    }

    private void OnSlotClicked()
    {
        if (currentItem == null) return;
        AuctionFarmCategory.Instance?.OpenFarmRegisterPanel(currentItem);
    }
}
