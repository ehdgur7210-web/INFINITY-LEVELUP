using UnityEngine;

/// <summary>
/// 장비 타입 열거형 (최종 버전)
/// ⭐ Weapon → WeaponLeft, WeaponRight 분리
/// ⭐ Accessory 제거
/// ⭐ 총 6개 타입
/// </summary>
public enum EquipmentType
{
    WeaponLeft,  // 왼손 무기
    WeaponRight, // 오른손 무기
    Helmet,      // 투구
    Armor,       // 갑옷
    Gloves,      // 장갑
    Boots        // 신발
}

/// <summary>
/// 장비 아이템 데이터
/// </summary>
[CreateAssetMenu(fileName = "New Equipment", menuName = "Game/Equipment Data")]
public class EquipmentData : ItemData
{
    [Header("장비 정보")]
    public EquipmentType equipmentType;     // 장비 타입

    [Header("스탯 보너스")]
    public int attackBonus = 0;             // 공격력 증가
    public int defenseBonus = 0;            // 방어력 증가
    public int hpBonus = 0;                 // 체력 증가
    public int speedBonus = 0;              // 이동속도 증가
    public int criticalBonus = 0;           // 치명타 확률 증가 (%)

    [Header("특수 효과")]
    public bool hasSpecialEffect = false;   // 특수 효과 여부
    [TextArea(2, 4)]
    public string specialEffectDescription; // 특수 효과 설명

    [Header("요구 레벨")]
    public int requiredLevel = 1;           // 장착 요구 레벨

    [Header("시각 효과")]
    public GameObject equipmentModel;       // 장비 3D 모델
    public Material equipmentMaterial;      // 장비 재질

    // ═══ 장비 레벨업 시스템 ═══════════════════════════════════════
    [Header("장비 레벨업")]
    [Tooltip("장비 최대 레벨 (0이면 레벨업 불가)")]
    public int maxItemLevel = 10;

    [Tooltip("레벨별 골드 비용 (배열 크기 < maxItemLevel이면 마지막 값 반복)")]
    public int[] levelUpGoldCost = { 500, 1000, 2000, 4000, 8000, 15000, 25000, 40000, 60000, 100000 };

    [Tooltip("레벨별 스탯 배율 (1.0 = 기본, 1.1 = +10%). 배열 크기 < maxItemLevel이면 마지막 값 반복)")]
    public float[] levelUpStatMultiplier = { 1.0f, 1.1f, 1.2f, 1.35f, 1.5f, 1.7f, 1.9f, 2.15f, 2.4f, 2.7f, 3.0f };

    [Tooltip("레벨별 필요 동일 아이템 수 (또는 강화석). 배열 크기 < maxItemLevel이면 마지막 값 반복)")]
    public int[] requiredMaterialCount = { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 };

    /// <summary>해당 레벨에서 다음 레벨로 올리는 데 필요한 골드</summary>
    public int GetLevelUpGold(int currentLevel)
    {
        if (levelUpGoldCost == null || levelUpGoldCost.Length == 0) return 9999999;
        int idx = Mathf.Clamp(currentLevel, 0, levelUpGoldCost.Length - 1);
        return levelUpGoldCost[idx];
    }

    /// <summary>해당 레벨의 스탯 배율</summary>
    public float GetStatMultiplier(int level)
    {
        if (levelUpStatMultiplier == null || levelUpStatMultiplier.Length == 0) return 1f;
        int idx = Mathf.Clamp(level, 0, levelUpStatMultiplier.Length - 1);
        return levelUpStatMultiplier[idx];
    }

    /// <summary>해당 레벨에서 다음 레벨로 올리는 데 필요한 재료 수</summary>
    public int GetRequiredMaterials(int currentLevel)
    {
        if (requiredMaterialCount == null || requiredMaterialCount.Length == 0) return 1;
        int idx = Mathf.Clamp(currentLevel, 0, requiredMaterialCount.Length - 1);
        return requiredMaterialCount[idx];
    }

    /// <summary>레벨 적용된 스탯 보너스 계산</summary>
    public EquipmentStats GetLeveledStats(int itemLevel)
    {
        float mult = GetStatMultiplier(itemLevel);
        EquipmentStats leveled = new EquipmentStats();
        leveled.attack = Mathf.RoundToInt(equipmentStats.attack * mult);
        leveled.defense = Mathf.RoundToInt(equipmentStats.defense * mult);
        leveled.health = Mathf.RoundToInt(equipmentStats.health * mult);
        leveled.mana = Mathf.RoundToInt(equipmentStats.mana * mult);
        leveled.criticalRate = Mathf.RoundToInt(equipmentStats.criticalRate * mult);
        leveled.criticalDamage = equipmentStats.criticalDamage * mult;
        leveled.attackSpeed = equipmentStats.attackSpeed * mult;
        leveled.moveSpeed = equipmentStats.moveSpeed * mult;
        return leveled;
    }
}

