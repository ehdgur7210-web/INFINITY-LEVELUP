using UnityEngine;
using System;

/// <summary>
/// 아이템 데이터 ScriptableObject
/// 새 항목: ItemType에 FarmProduce, Companion 추가
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Game/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public int itemID;
    public string itemName;
    [TextArea(3, 5)]
    public string itemDescription;

    [Header("분류")]
    public ItemType itemType;
    public ItemRarity rarity;

    [Header("아이콘")]
    public Sprite itemIcon;
    public Sprite background;

    [Header("스택")]
    public int maxStack = 9999;
    public bool isConsumable = false;

    [Header("가격")]
    public int sellPrice;
    public int buyPrice;

    [Header("장비 스탯 (Equipment 타입)")]
    [Tooltip("ItemType == Equipment 일 때 사용 가능")]
    public EquipmentStats equipmentStats;

    [Header("소모품 효과 (Consumable 타입)")]
    [Tooltip("ItemType == Consumable 일 때 사용 가능")]
    public ConsumableEffect consumableEffect;

    public float GetItemValue()
    {
        float rarityMultiplier = GetRarityMultiplier();
        return buyPrice * rarityMultiplier;
    }

    public float GetRarityMultiplier()
    {
        switch (rarity)
        {
            case ItemRarity.Common: return 1f;
            case ItemRarity.Uncommon: return 1.5f;
            case ItemRarity.Rare: return 2.5f;
            case ItemRarity.Epic: return 4f;
            case ItemRarity.Legendary: return 8f;
            default: return 1f;
        }
    }

    public Color GetRarityColor()
    {
        switch (rarity)
        {
            case ItemRarity.Common: return new Color(0.8f, 0.8f, 0.8f);
            case ItemRarity.Uncommon: return new Color(0.2f, 0.8f, 0.2f);
            case ItemRarity.Rare: return new Color(0.2f, 0.4f, 1f);
            case ItemRarity.Epic: return new Color(0.7f, 0.2f, 1f);
            case ItemRarity.Legendary: return new Color(1f, 0.6f, 0f);
            default: return Color.white;
        }
    }
}

/// <summary>
/// 새 FarmProduce / Companion 추가
/// </summary>
public enum ItemType
{
    Weapon,
    Armor,
    Accessory,
    Consumable,
    Material,
    Quest,
    Currency,
    Misc,
    Equipment,
    FarmVegetable,
    FarmFruit,
    Companion,
    OfflineReward_2h,
    OfflineReward_4h,
    OfflineReward_8h,
    OfflineReward_12h,
    GachaTicket_5Star,
    GachaTicket_3to5Star,
}

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

[System.Serializable]
public class EquipmentStats
{
    public int attack = 0;
    public int defense = 0;
    public int health = 0;
    public int mana = 0;
    public int criticalRate = 0;
    public float criticalDamage = 0f;
    public float attackSpeed = 0f;
    public float moveSpeed = 0f;

    public EquipmentStats GetEnhancedStats(int enhanceLevel)
    {
        EquipmentStats enhanced = new EquipmentStats();
        float multiplier = EnhancementSystem.GetEnhanceMultiplier(enhanceLevel);
        enhanced.attack = Mathf.RoundToInt(attack * multiplier);
        enhanced.defense = Mathf.RoundToInt(defense * multiplier);
        enhanced.health = Mathf.RoundToInt(health * multiplier);
        enhanced.mana = Mathf.RoundToInt(mana * multiplier);
        enhanced.criticalRate = Mathf.RoundToInt(criticalRate * multiplier);
        enhanced.criticalDamage = criticalDamage * multiplier;
        enhanced.attackSpeed = attackSpeed * multiplier;
        enhanced.moveSpeed = moveSpeed * multiplier;
        return enhanced;
    }
}

[System.Serializable]
public class ConsumableEffect
{
    public ConsumableType type;
    public float value;
    public float duration;

    public void ApplyEffect(PlayerStats playerStats)
    {
        if (playerStats == null) return;
        switch (type)
        {
            case ConsumableType.HealthRestore:
                playerStats.Heal(Mathf.RoundToInt(value));
                break;
        }
    }
}

public enum ConsumableType
{
    HealthRestore,
    ManaRestore,
    AttackBuff,
    DefenseBuff,
    SpeedBuff
}