using System.Collections.Generic;
using UnityEngine;

// ═══════════════════════════════════════════════════════════
// 상점 탭 타입
// ═══════════════════════════════════════════════════════════

public enum ShopTabType
{
    Daily,      // 일일상점
    Weekly,     // 주간상점
    Monthly     // 월간상점
}

// ═══════════════════════════════════════════════════════════
// 상점 데이터 ScriptableObject
// - 일일/주간/월간 아이템 분리
// - 할인율 설정
// ═══════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "ShopData", menuName = "Game/Shop Data")]
public class ShopData : ScriptableObject
{
    [Header("일일상점 아이템")]
    public List<ItemData> dailyItems = new List<ItemData>();

    [Header("주간상점 아이템")]
    public List<ItemData> weeklyItems = new List<ItemData>();

    [Header("월간상점 아이템")]
    public List<ItemData> monthlyItems = new List<ItemData>();

    [Header("기본 아이템 (폴백용)")]
    public List<ItemData> baseItems = new List<ItemData>();

    [Header("희귀 아이템 (낮은 확률로 등장)")]
    public List<ItemData> rareItems = new List<ItemData>();

    [Header("일일 교환 패키지 (젬→재화)")]
    public List<ShopPackage> dailyPackages = new List<ShopPackage>();

    [Header("주간 교환 패키지 (젬→재화)")]
    public List<ShopPackage> weeklyPackages = new List<ShopPackage>();

    [Header("월간 교환 패키지 (젬→재화)")]
    public List<ShopPackage> monthlyPackages = new List<ShopPackage>();

    [Header("할인 설정")]
    public List<CategoryDiscount> discounts = new List<CategoryDiscount>();

    /// <summary>탭별 아이템 가져오기</summary>
    public List<ItemData> GetItemsByTab(ShopTabType tab)
    {
        List<ItemData> items = tab switch
        {
            ShopTabType.Daily   => new List<ItemData>(dailyItems),
            ShopTabType.Weekly  => new List<ItemData>(weeklyItems),
            ShopTabType.Monthly => new List<ItemData>(monthlyItems),
            _ => new List<ItemData>()
        };

        // 탭 전용 아이템이 비어있으면 baseItems + rareItems 폴백
        if (items.Count == 0)
        {
            items.AddRange(baseItems);
            foreach (var item in rareItems)
            {
                if (Random.Range(0f, 1f) <= 0.3f)
                    items.Add(item);
            }
        }

        return items;
    }

    /// <summary>탭별 패키지 가져오기</summary>
    public List<ShopPackage> GetPackagesByTab(ShopTabType tab)
    {
        return tab switch
        {
            ShopTabType.Daily   => new List<ShopPackage>(dailyPackages),
            ShopTabType.Weekly  => new List<ShopPackage>(weeklyPackages),
            ShopTabType.Monthly => new List<ShopPackage>(monthlyPackages),
            _ => new List<ShopPackage>()
        };
    }

    /// <summary>모든 아이템 가져오기 (폴백용)</summary>
    public List<ItemData> GetAllItems()
    {
        List<ItemData> allItems = new List<ItemData>();
        allItems.AddRange(dailyItems);
        allItems.AddRange(weeklyItems);
        allItems.AddRange(monthlyItems);
        allItems.AddRange(baseItems);
        return allItems;
    }

    /// <summary>카테고리별 할인율 가져오기</summary>
    public float GetDiscountRate(ItemType itemType)
    {
        foreach (var discount in discounts)
        {
            if (discount.itemType == itemType)
                return discount.discountRate;
        }
        return 0f;
    }
}

// ═══════════════════════════════════════════════════════════
// 카테고리별 할인 설정
// ═══════════════════════════════════════════════════════════

[System.Serializable]
public class CategoryDiscount
{
    public ItemType itemType;
    [Range(0f, 1f)]
    public float discountRate; // 0.2 = 20% 할인
}

// ═══════════════════════════════════════════════════════════
// ★ 상점 교환 패키지 (젬으로 다른 재화 구매)
// ═══════════════════════════════════════════════════════════

public enum PackageRewardType
{
    CompanionTicket,    // 동료 뽑기 티켓
    EquipmentTicket,    // 장비 뽑기 티켓
    Gold,               // 골드
    Gem,                // 다이아 (보너스)
    CropPoint           // 작물 포인트
}

[System.Serializable]
public class ShopPackage
{
    [Tooltip("표시 이름 (예: '동료티켓 5장')")]
    public string packageName;

    [Tooltip("아이콘 스프라이트")]
    public Sprite icon;

    [TextArea(1, 2)]
    [Tooltip("설명 (선택)")]
    public string description;

    [Tooltip("필요 다이아 (젬)")]
    public int gemCost;

    [Tooltip("주는 보상 종류")]
    public PackageRewardType rewardType;

    [Tooltip("주는 보상 수량")]
    public int rewardAmount;
}
