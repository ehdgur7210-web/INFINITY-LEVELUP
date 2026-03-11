using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// FarmInventoryConnector
/// FarmManager.Instance.OnHarvestComplete (UnityEvent) 구독
/// itemName을 고유 키로 사용 (itemID 타입 불명이므로 이름 기준)
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
        public string itemName;   // 고유 키로 사용
        public Sprite icon;
        public int count;
        public CropItemType itemType;
        public int sellPrice;
    }

    public enum CropItemType { Vegetable, Fruit, Other }

    private List<FarmItem> farmInventory = new List<FarmItem>();
    private List<FarmSlotUI> slotUIs = new List<FarmSlotUI>();

    public IReadOnlyList<FarmItem> FarmInventory => farmInventory;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        InitializeSlots();

        if (FarmManager.Instance != null)
            FarmManager.Instance.OnHarvestComplete.AddListener(OnHarvestComplete);
        else
            Debug.LogWarning("[FarmInventoryConnector] FarmManager.Instance 없음 - 수확 이벤트 구독 실패");
    }

    void OnDestroy()
    {
        if (FarmManager.Instance != null)
            FarmManager.Instance.OnHarvestComplete.RemoveListener(OnHarvestComplete);
    }

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
        Debug.Log($"[FarmInventoryConnector] 슬롯 {farmInventorySize}개 생성 완료");
    }

    // CropHarvestReward 실제 필드: item(ItemData), minAmount, maxAmount, goldReward, gemReward
    private void OnHarvestComplete(int plotIndex, List<CropHarvestReward> rewards)
    {
        if (rewards == null) return;
        foreach (var reward in rewards)
        {
            if (reward == null || reward.item == null) continue;
            ItemData itm = reward.item;
            int count = Mathf.Max(1, reward.minAmount);
            AddFarmCrop(
                itemName: itm.itemName,
                icon: itm.itemIcon,
                count: count,
                itemType: DetectItemType(itm.itemName),
                sellPrice: itm.sellPrice > 0 ? itm.sellPrice : 10
            );
        }
    }

    /// <summary>Farm 인벤에 작물 추가 (외부 호출 가능)</summary>
    public void AddFarmCrop(string itemName, Sprite icon,
                            int count, CropItemType itemType = CropItemType.Other, int sellPrice = 10)
    {
        // 기존 스택 합치기
        foreach (var existing in farmInventory)
        {
            if (existing.itemName == itemName)
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
            sellPrice = sellPrice
        });

        RefreshSlotUI();
        Debug.Log($"[FarmInventoryConnector] {itemName} x{count} 추가");
    }

    public bool RemoveFarmCrop(string itemName, int count)
    {
        for (int i = 0; i < farmInventory.Count; i++)
        {
            if (farmInventory[i].itemName != itemName) continue;
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

    public int GetCropCount(string itemName)
    {
        foreach (var item in farmInventory)
            if (item.itemName == itemName) return item.count;
        return 0;
    }

    public bool RegisterFarmCropToAuction(string itemName, int quantity, int startBid, int buyout, float duration)
    {
        FarmItem item = farmInventory.Find(f => f.itemName == itemName);
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

        RemoveFarmCrop(itemName, quantity);
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

    private CropItemType DetectItemType(string name)
    {
        string lower = (name ?? "").ToLower();
        string[] vegetables = { "당근", "양배추", "토마토", "감자", "고구마", "옥수수", "배추", "호박", "오이", "파" };
        string[] fruits = { "사과", "딸기", "포도", "수박", "복숭아", "귤", "참외", "바나나", "망고", "블루베리" };
        foreach (string v in vegetables) if (lower.Contains(v)) return CropItemType.Vegetable;
        foreach (string f in fruits) if (lower.Contains(f)) return CropItemType.Fruit;
        return CropItemType.Other;
    }

    public FarmItem[] GetSaveData() => farmInventory.ToArray();
    public void LoadSaveData(FarmItem[] data)
    {
        farmInventory = new List<FarmItem>(data ?? new FarmItem[0]);
        RefreshSlotUI();
    }
}

// ─── Farm 슬롯 UI ───
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