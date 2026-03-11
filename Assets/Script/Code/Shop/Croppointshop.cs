using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 작물 포인트 상점 — 작물 포인트 → 강화 재료 교환
///
/// 사용 방법:
///   - FarmManager.cropPoints를 소모해서 강화 재료 ItemData를 획득
///   - EnhancementSystem이 소비하는 아이템을 여기서 구매
///   - UI: CropPointShopUI가 이 매니저를 통해 구매 버튼 처리
/// </summary>
public class CropPointShop : MonoBehaviour
{
    public static CropPointShop Instance { get; private set; }

    [Header("교환 목록")]
    public List<CropPointShopItem> shopItems = new List<CropPointShopItem>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 작물 포인트로 아이템 구매
    /// </summary>
    public bool Purchase(int shopItemIndex, int quantity = 1)
    {
        if (shopItemIndex < 0 || shopItemIndex >= shopItems.Count)
        {
            Debug.LogWarning("[CropPointShop] 잘못된 상점 인덱스");
            return false;
        }

        CropPointShopItem shopItem = shopItems[shopItemIndex];

        if (shopItem.item == null)
        {
            Debug.LogWarning("[CropPointShop] 아이템 데이터 없음");
            return false;
        }

        // 레벨 조건 체크
        int playerLevel = PlayerStats.Instance != null ? PlayerStats.Instance.level : 1;
        if (playerLevel < shopItem.requiredPlayerLevel)
        {
            UIManager.Instance?.ShowMessage(
                $"레벨 {shopItem.requiredPlayerLevel} 필요!", Color.red);
            return false;
        }

        int totalCost = shopItem.cropPointCost * quantity;

        // 작물 포인트 차감
        if (FarmManager.Instance == null || !FarmManager.Instance.SpendCropPoints(totalCost))
            return false;

        // 인벤토리에 아이템 추가
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(shopItem.item, shopItem.itemAmount * quantity);
        }

        UIManager.Instance?.ShowMessage(
            $"{shopItem.item.itemName} x{shopItem.itemAmount * quantity} 획득! " +
            $"(포인트 -{totalCost})", Color.green);

        Debug.Log($"[CropPointShop] 구매: {shopItem.item.itemName} x{shopItem.itemAmount * quantity} " +
                  $"(비용: {totalCost} 작물포인트)");
        return true;
    }

    /// <summary>구매 가능 여부 체크</summary>
    public bool CanPurchase(int shopItemIndex, int quantity = 1)
    {
        if (shopItemIndex < 0 || shopItemIndex >= shopItems.Count) return false;
        if (FarmManager.Instance == null) return false;

        CropPointShopItem shopItem = shopItems[shopItemIndex];
        int totalCost = shopItem.cropPointCost * quantity;

        int playerLevel = PlayerStats.Instance != null ? PlayerStats.Instance.level : 1;
        if (playerLevel < shopItem.requiredPlayerLevel) return false;

        return FarmManager.Instance.GetCropPoints() >= totalCost;
    }

    public List<CropPointShopItem> GetShopItems() => shopItems;
}

// ════════════════════════════════════════════════
//  상점 아이템 데이터
// ════════════════════════════════════════════════
[System.Serializable]
public class CropPointShopItem
{
    [Header("교환 아이템")]
    public ItemData item;
    [Tooltip("한 번에 획득하는 수량")]
    public int itemAmount = 1;

    [Header("비용")]
    [Tooltip("소모할 작물 포인트")]
    public int cropPointCost = 20;

    [Header("조건")]
    public int requiredPlayerLevel = 1;

    [Header("UI 표시")]
    public string itemDescription = "";  // 추가 설명 (강화에 사용 등)
}