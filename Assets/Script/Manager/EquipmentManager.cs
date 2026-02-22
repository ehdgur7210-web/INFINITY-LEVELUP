using UnityEngine;
using System.Collections.Generic;

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }
    public static System.Action<EquipmentType, EquipmentData, int> OnEquipmentChanged;

    private class EquippedEntry
    {
        public EquipmentData equipment;
        public int enhanceLevel;
    }

    private Dictionary<EquipmentType, EquippedEntry> equippedItems
        = new Dictionary<EquipmentType, EquippedEntry>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        // ✅ Start()에서 등록 → 모든 Awake()가 끝난 뒤라 Instance가 반드시 존재
        CombatPowerManager.Instance?.RegisterEquipmentProvider(GetEquippedItemInfos);
        // ✅ 등록 후 초기 전투력 계산
        CombatPowerManager.Instance?.Recalculate();
    }

    private List<CombatPowerManager.EquippedItemInfo> GetEquippedItemInfos()
    {
        var result = new List<CombatPowerManager.EquippedItemInfo>();
        foreach (var kvp in equippedItems)
        {
            EquippedEntry entry = kvp.Value;
            if (entry == null || entry.equipment == null) continue;
            EquipmentData eq = entry.equipment;
            result.Add(new CombatPowerManager.EquippedItemInfo
            {
                attackBonus = eq.attackBonus,
                defenseBonus = eq.defenseBonus,
                hpBonus = eq.hpBonus,
                criticalBonus = eq.criticalBonus,
                enhanceLevel = entry.enhanceLevel,
                rarityIndex = (int)eq.rarity
            });
        }
        return result;
    }

    // ─────────────────────────────────────────
    // 장착
    // ─────────────────────────────────────────
    public bool EquipItem(EquipmentData equipment, int enhanceLevel = 0)
    {
        if (equipment == null) return false;

        EquipmentType type = equipment.equipmentType;

        if (equippedItems.ContainsKey(type))
        {
            var old = equippedItems[type];
            ApplyEquipmentStats(old.equipment, old.enhanceLevel, false);
            InventoryManager.Instance?.AddItem(old.equipment, 1);
        }

        equippedItems[type] = new EquippedEntry { equipment = equipment, enhanceLevel = enhanceLevel };

        ApplyEquipmentStats(equipment, enhanceLevel, true);
        RefreshEquipmentSlotUI(type, equipment, enhanceLevel);
        OnEquipmentChanged?.Invoke(type, equipment, enhanceLevel);

        // ✅ 장착 시 전투력 즉시 갱신
        CombatPowerManager.Instance?.Recalculate();

        Debug.Log($"[EquipmentManager] 장착: {equipment.itemName} +{enhanceLevel}");
        UIManager.Instance?.ShowMessage($"{equipment.itemName} +{enhanceLevel} 장착!", Color.green);
        return true;
    }

    // ─────────────────────────────────────────
    // 해제
    // ─────────────────────────────────────────
    public void UnequipItem(EquipmentType type)
    {
        if (!equippedItems.ContainsKey(type)) return;

        var entry = equippedItems[type];
        ApplyEquipmentStats(entry.equipment, entry.enhanceLevel, false);
        equippedItems.Remove(type);

        InventoryManager.Instance?.AddItem(entry.equipment, 1);
        ClearEquipmentSlotUI(type);
        OnEquipmentChanged?.Invoke(type, null, 0);

        // ✅ 해제 시 전투력 즉시 갱신
        CombatPowerManager.Instance?.Recalculate();

        Debug.Log($"[EquipmentManager] 해제: {entry.equipment.itemName} → 인벤 반환");
        UIManager.Instance?.ShowMessage($"{entry.equipment.itemName} 해제", Color.white);
    }

    // ─────────────────────────────────────────
    // 강화 후 스탯 재계산 (EnhancementSystem에서 호출)
    // ─────────────────────────────────────────
    public void RecalculateStats()
    {
        if (PlayerStats.Instance == null) return;

        PlayerStats.Instance.bonusAttack = 0;
        PlayerStats.Instance.bonusDefense = 0;
        PlayerStats.Instance.bonusMaxHp = 0;
        PlayerStats.Instance.bonusSpeed = 0;
        PlayerStats.Instance.bonusCritical = 0;

        foreach (var kvp in equippedItems)
            ApplyEquipmentStats(kvp.Value.equipment, kvp.Value.enhanceLevel, true);

        PlayerStats.Instance?.UpdateStatsUI();
        // ✅ 전투력 갱신
        CombatPowerManager.Instance?.Recalculate();
        Debug.Log("[EquipmentManager] RecalculateStats 완료");
    }

    // ─────────────────────────────────────────
    // 강화 레벨 갱신 (EnhancementSystem에서 호출)
    // ─────────────────────────────────────────
    public void UpdateEnhanceLevel(EquipmentType type, int newLevel)
    {
        if (!equippedItems.ContainsKey(type)) return;

        var entry = equippedItems[type];
        ApplyEquipmentStats(entry.equipment, entry.enhanceLevel, false);
        entry.enhanceLevel = newLevel;
        ApplyEquipmentStats(entry.equipment, entry.enhanceLevel, true);

        foreach (var slot in FindObjectsOfType<EquipmentSlot>())
            if (slot.slotType == type) { slot.UpdateEnhanceLevel(newLevel); break; }

        OnEquipmentChanged?.Invoke(type, entry.equipment, newLevel);
        PlayerStats.Instance?.UpdateStatsUI();

        // ✅ 강화 레벨 변경 시 전투력 즉시 갱신
        CombatPowerManager.Instance?.Recalculate();
    }

    // ─────────────────────────────────────────
    // 조회
    // ─────────────────────────────────────────
    public EquipmentData GetEquippedItem(EquipmentType type)
        => equippedItems.TryGetValue(type, out var e) ? e.equipment : null;

    public int GetEnhanceLevel(EquipmentType type)
        => equippedItems.TryGetValue(type, out var e) ? e.enhanceLevel : 0;

    public bool IsEquipped(EquipmentType type)
        => equippedItems.ContainsKey(type);

    public bool IsItemEquipped(EquipmentData equipment)
    {
        if (equipment == null) return false;
        if (!equippedItems.TryGetValue(equipment.equipmentType, out var e)) return false;
        return e.equipment == equipment;
    }

    // ─────────────────────────────────────────
    // 내부 스탯 적용/제거
    // ─────────────────────────────────────────
    private void ApplyEquipmentStats(EquipmentData eq, int enhanceLevel, bool apply)
    {
        if (PlayerStats.Instance == null || eq == null) return;

        float sign = apply ? 1f : -1f;
        float bonus = 1f + (enhanceLevel * 0.1f);

        PlayerStats.Instance.bonusAttack += eq.attackBonus * bonus * sign;
        PlayerStats.Instance.bonusDefense += eq.defenseBonus * bonus * sign;
        PlayerStats.Instance.bonusMaxHp += eq.hpBonus * bonus * sign;
        PlayerStats.Instance.bonusSpeed += eq.speedBonus * bonus * sign;
        PlayerStats.Instance.bonusCritical += eq.criticalBonus * bonus * sign;

        PlayerStats.Instance?.UpdateStatsUI();
    }

    private void RefreshEquipmentSlotUI(EquipmentType type, EquipmentData eq, int enhLevel)
    {
        foreach (var slot in FindObjectsOfType<EquipmentSlot>())
            if (slot.slotType == type) { slot.EquipItem(eq, enhLevel); break; }
    }

    private void ClearEquipmentSlotUI(EquipmentType type)
    {
        foreach (var slot in FindObjectsOfType<EquipmentSlot>())
            if (slot.slotType == type) { slot.UnequipItem(); break; }
    }
}