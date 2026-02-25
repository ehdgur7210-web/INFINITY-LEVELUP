using UnityEngine;
using System.Collections.Generic;

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }

    // 장비 변경 이벤트 (SPUM 외형 갱신 등에서 사용)
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
        // Start()에서 등록 → 모든 Awake()가 끝난 뒤라 Instance가 반드시 존재
        CombatPowerManager.Instance?.RegisterEquipmentProvider(GetEquippedItemInfos);

        // 등록 후 초기 전투력 계산
        CombatPowerManager.Instance?.Recalculate();

        // 씬 전환 후 PlayerStats가 재생성될 경우 장비 스탯 재적용
        RecalculateStats();

        // SPUM 비주얼 시스템 갱신용 이벤트
        OnEquipmentChanged?.Invoke(default, null, 0);
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
                attackBonus = eq.equipmentStats.attack,
                defenseBonus = eq.equipmentStats.defense,
                hpBonus = eq.equipmentStats.health,
                criticalBonus = eq.equipmentStats.criticalRate,
                enhanceLevel = entry.enhanceLevel,
                rarityIndex = (int)eq.rarity
            });
        }

        return result;
    }

    // ================================
    // 장착
    // ================================
    public bool EquipItem(EquipmentData equipment, int enhanceLevel = 0)
    {
        if (equipment == null) return false;

        EquipmentType type = equipment.equipmentType;

        // 기존 장비 제거
        if (equippedItems.ContainsKey(type))
        {
            var old = equippedItems[type];

            ApplyEquipmentStats(old.equipment, old.enhanceLevel, false);
            InventoryManager.Instance?.AddItem(old.equipment, 1);
            EquipmentSkillSystem.Instance?.OnEquipmentUnequipped(type);
        }

        equippedItems[type] = new EquippedEntry
        {
            equipment = equipment,
            enhanceLevel = enhanceLevel
        };

        ApplyEquipmentStats(equipment, enhanceLevel, true);
        RefreshEquipmentSlotUI(type, equipment, enhanceLevel);

        OnEquipmentChanged?.Invoke(type, equipment, enhanceLevel);
        EquipmentSkillSystem.Instance?.OnEquipmentEquipped(type, equipment);

        // 장착 즉시 전투력 갱신
        CombatPowerManager.Instance?.Recalculate();

        Debug.Log($"[EquipmentManager] 장착: {equipment.itemName} +{enhanceLevel}");
        UIManager.Instance?.ShowMessage($"{equipment.itemName} +{enhanceLevel} 장착!", Color.green);

        return true;
    }

    // ================================
    // 해제
    // ================================
    public void UnequipItem(EquipmentType type)
    {
        if (!equippedItems.ContainsKey(type)) return;

        var entry = equippedItems[type];

        ApplyEquipmentStats(entry.equipment, entry.enhanceLevel, false);
        equippedItems.Remove(type);

        InventoryManager.Instance?.AddItem(entry.equipment, 1);
        ClearEquipmentSlotUI(type);

        OnEquipmentChanged?.Invoke(type, null, 0);
        EquipmentSkillSystem.Instance?.OnEquipmentUnequipped(type);

        CombatPowerManager.Instance?.Recalculate();

        Debug.Log($"[EquipmentManager] 해제: {entry.equipment.itemName}");
        UIManager.Instance?.ShowMessage($"{entry.equipment.itemName} 해제", Color.white);
    }

    // ================================
    // 스탯 전체 재계산
    // ================================
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

        PlayerStats.Instance.UpdateStatsUI();
        CombatPowerManager.Instance?.Recalculate();

        Debug.Log("[EquipmentManager] RecalculateStats 완료");
    }

    // ================================
    // 강화 레벨 변경
    // ================================
    public void UpdateEnhanceLevel(EquipmentType type, int newLevel)
    {
        if (!equippedItems.ContainsKey(type)) return;

        var entry = equippedItems[type];

        ApplyEquipmentStats(entry.equipment, entry.enhanceLevel, false);
        entry.enhanceLevel = newLevel;
        ApplyEquipmentStats(entry.equipment, entry.enhanceLevel, true);

        foreach (var slot in FindObjectsOfType<EquipmentSlot>())
            if (slot.slotType == type)
            {
                slot.UpdateEnhanceLevel(newLevel);
                break;
            }

        OnEquipmentChanged?.Invoke(type, entry.equipment, newLevel);

        PlayerStats.Instance?.UpdateStatsUI();
        CombatPowerManager.Instance?.Recalculate();
    }

    // ================================
    // 조회
    // ================================
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

    // ================================
    // 내부 스탯 적용/제거
    // ================================
    private void ApplyEquipmentStats(EquipmentData eq, int enhanceLevel, bool apply)
    {
        if (PlayerStats.Instance == null || eq == null) return;

        float sign = apply ? 1f : -1f;
        float bonusMultiplier = 1f + (enhanceLevel * 0.1f); // 강화당 10% 증가

        PlayerStats.Instance.bonusAttack += eq.equipmentStats.attack * bonusMultiplier * sign;
        PlayerStats.Instance.bonusDefense += eq.equipmentStats.defense * bonusMultiplier * sign;
        PlayerStats.Instance.bonusMaxHp += eq.equipmentStats.health * bonusMultiplier * sign;
        PlayerStats.Instance.bonusSpeed += eq.equipmentStats.moveSpeed * bonusMultiplier * sign;
        PlayerStats.Instance.bonusCritical += eq.equipmentStats.criticalRate * bonusMultiplier * sign;

        PlayerStats.Instance.UpdateStatsUI();
    }

    private void RefreshEquipmentSlotUI(EquipmentType type, EquipmentData eq, int enhLevel)
    {
        foreach (var slot in FindObjectsOfType<EquipmentSlot>())
            if (slot.slotType == type)
            {
                slot.EquipItem(eq, enhLevel);
                break;
            }
    }

    private void ClearEquipmentSlotUI(EquipmentType type)
    {
        foreach (var slot in FindObjectsOfType<EquipmentSlot>())
            if (slot.slotType == type)
            {
                slot.UnequipItem();
                break;
            }
    }

    // ================================
    // 저장 / 로드
    // ================================
    public EquipmentSaveData GetEquipmentSaveData()
    {
        EquipmentSaveData data = new EquipmentSaveData();
        data.slots = new List<EquippedSlotData>();

        foreach (var kvp in equippedItems)
        {
            if (kvp.Value == null || kvp.Value.equipment == null) continue;

            data.slots.Add(new EquippedSlotData
            {
                slotType = kvp.Key,
                itemID = kvp.Value.equipment.itemID,
                enhanceLevel = kvp.Value.enhanceLevel
            });
        }

        Debug.Log($"[EquipmentManager] 장비 저장: {data.slots.Count}개 슬롯");
        return data;
    }

    public void LoadEquipmentSaveData(EquipmentSaveData data)
    {
        if (data == null || data.slots == null) return;

        equippedItems.Clear();

        foreach (var slotData in data.slots)
        {
            if (slotData.itemID < 0) continue;

            EquipmentData eq = ItemDatabase.Instance?.GetEquipmentByID(slotData.itemID);
            if (eq == null)
            {
                Debug.LogWarning($"[EquipmentManager] Load 실패: itemID={slotData.itemID}");
                continue;
            }

            equippedItems[slotData.slotType] = new EquippedEntry
            {
                equipment = eq,
                enhanceLevel = slotData.enhanceLevel
            };

            RefreshEquipmentSlotUI(slotData.slotType, eq, slotData.enhanceLevel);
            EquipmentSkillSystem.Instance?.OnEquipmentEquipped(slotData.slotType, eq);
        }

        RecalculateStats();
        CombatPowerManager.Instance?.Recalculate();

        Debug.Log($"[EquipmentManager] 장비 로드 완료: {equippedItems.Count}개");
    }
}