using UnityEngine;
using System;

/// <summary>
/// 아이템 데이터 ScriptableObject
/// - Unity 에디터에서 아이템 생성 및 관리 가능
/// - 메모리 효율적 (여러 인스턴스가 같은 데이터 참조)
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Game/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public int itemID;              // 고유 ID
    public string itemName;         // 아이템 이름

    [TextArea(3, 5)]
    public string itemDescription;  // 아이템 설명

    [Header("분류")]
    public ItemType itemType;       // 아이템 타입
    public ItemRarity rarity;       // 희귀도

    [Header("시각 요소")]
    public Sprite itemIcon;         // 아이템 아이콘
    public Sprite background;       // 배경 이미지 (희귀도별로 다르게 설정)

    [Header("스택 및 소비")]
    public int maxStack = 99;       // 최대 중첩 개수
    public bool isConsumable = false; // 소비 아이템 여부

    [Header("가격")]
    public int sellPrice;           // 판매 가격
    public int buyPrice;            // 구매 가격

    [Header("추가 속성 - 장비 아이템")]
    [Tooltip("장비 아이템인 경우의 스탯")]
    public EquipmentStats equipmentStats;

    [Header("추가 속성 - 소비 아이템")]
    [Tooltip("소비 아이템의 효과")]
    public ConsumableEffect consumableEffect;

    /// <summary>
    /// 아이템의 전체 가치 계산 (희귀도 가중치 적용)
    /// </summary>
    public float GetItemValue()
    {
        float rarityMultiplier = GetRarityMultiplier();
        return buyPrice * rarityMultiplier;
    }

    /// <summary>
    /// 희귀도별 배율 반환
    /// </summary>
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

    /// <summary>
    /// 희귀도별 색상 반환 (UI에서 사용)
    /// </summary>
    public Color GetRarityColor()
    {
        switch (rarity)
        {
            case ItemRarity.Common: return new Color(0.8f, 0.8f, 0.8f); // 회색
            case ItemRarity.Uncommon: return new Color(0.2f, 0.8f, 0.2f); // 초록
            case ItemRarity.Rare: return new Color(0.2f, 0.4f, 1f); // 파랑
            case ItemRarity.Epic: return new Color(0.7f, 0.2f, 1f); // 보라
            case ItemRarity.Legendary: return new Color(1f, 0.6f, 0f); // 주황
            default: return Color.white;
        }
    }
}

/// <summary>
/// 아이템 타입 (카테고리)
/// </summary>
public enum ItemType
{
    Weapon,         // 무기
    Armor,          // 방어구
    Accessory,      // 악세서리 (NEW)
    Consumable,     // 소비 아이템
    Material,       // 재료
    Quest,          // 퀘스트 아이템
    Currency,       // 화폐 아이템 (NEW)
    Misc,           // 기타
    Equipment
}

/// <summary>
/// 아이템 희귀도
/// </summary>
public enum ItemRarity
{
    Common,         // 일반 (흰색)
    Uncommon,       // 고급 (초록)
    Rare,           // 희귀 (파랑)
    Epic,           // 영웅 (보라)
    Legendary       // 전설 (주황)
}

/// <summary>
/// 장비 아이템의 스탯
/// </summary>
[System.Serializable]
public class EquipmentStats
{
    public int attack = 0;          // 공격력
    public int defense = 0;         // 방어력
    public int health = 0;          // 체력
    public int mana = 0;            // 마나
    public float criticalRate = 0f; // 치명타 확률 (%)
    public float criticalDamage = 0f; // 치명타 데미지 (%)
    public float attackSpeed = 0f;  // 공격 속도
    public float moveSpeed = 0f;    // 이동 속도

    /// <summary>
    /// 장비 강화 레벨에 따른 스탯 계산
    /// </summary>
    public EquipmentStats GetEnhancedStats(int enhanceLevel)
    {
        EquipmentStats enhanced = new EquipmentStats();
        float multiplier = 1 + (enhanceLevel * 0.1f); // 강화 레벨당 10% 증가

        enhanced.attack = Mathf.RoundToInt(attack * multiplier);
        enhanced.defense = Mathf.RoundToInt(defense * multiplier);
        enhanced.health = Mathf.RoundToInt(health * multiplier);
        enhanced.mana = Mathf.RoundToInt(mana * multiplier);
        enhanced.criticalRate = criticalRate * multiplier;
        enhanced.criticalDamage = criticalDamage * multiplier;
        enhanced.attackSpeed = attackSpeed * multiplier;
        enhanced.moveSpeed = moveSpeed * multiplier;

        return enhanced;
    }
}

/// <summary>
/// 소비 아이템의 효과
/// </summary>
[System.Serializable]
public class ConsumableEffect
{
    public ConsumableType type;     // 효과 타입
    public float value;             // 효과 값
    public float duration;          // 지속 시간 (버프의 경우)

    /// <summary>
    /// 효과 적용
    /// </summary>
    public void ApplyEffect(PlayerStats playerStats)
    {
        if (playerStats == null) return;

        switch (type)
        {
            case ConsumableType.HealthRestore:
                playerStats.Heal(Mathf.RoundToInt(value));
                Debug.Log($"체력 {value} 회복");
                break;

            case ConsumableType.ManaRestore:
                // playerStats.RestoreMana(Mathf.RoundToInt(value));
                Debug.Log($"마나 {value} 회복");
                break;

            case ConsumableType.AttackBuff:
                // playerStats.AddTemporaryBuff("Attack", value, duration);
                Debug.Log($"공격력 {value} 증가 ({duration}초)");
                break;

            case ConsumableType.DefenseBuff:
                // playerStats.AddTemporaryBuff("Defense", value, duration);
                Debug.Log($"방어력 {value} 증가 ({duration}초)");
                break;

            case ConsumableType.SpeedBuff:
                // playerStats.AddTemporaryBuff("Speed", value, duration);
                Debug.Log($"이동속도 {value} 증가 ({duration}초)");
                break;
        }
    }
}

/// <summary>
/// 소비 아이템 효과 타입
/// </summary>
public enum ConsumableType
{
    HealthRestore,  // 체력 회복
    ManaRestore,    // 마나 회복
    AttackBuff,     // 공격력 버프
    DefenseBuff,    // 방어력 버프
    SpeedBuff       // 이동속도 버프
}
