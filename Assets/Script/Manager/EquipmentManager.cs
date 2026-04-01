using UnityEngine;
using System.Collections.Generic;

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }

    // 장비 변경 이벤트 (UI 슬롯 갱신용)
    public static System.Action<EquipmentType, EquipmentData, int> OnEquipmentChanged;

    private class EquippedEntry
    {
        public EquipmentData equipment;
        public int enhanceLevel;
        public int itemLevel;
    }

    private Dictionary<EquipmentType, EquippedEntry> equippedItems
        = new Dictionary<EquipmentType, EquippedEntry>();

    // EquipPanelSlot 캐시 (비활성 상태에서도 접근 가능)
    private EquipPanelSlot[] cachedPanelSlots;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] EquipmentManager가 생성되었습니다.");
        }
        else { enabled = false; Destroy(gameObject); return; }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            OnEquipmentChanged = null;
            Instance = null;
        }
    }

    void Start()
    {
        if (Instance != this) return;

        // EquipPanelSlot 캐시 (비활성 포함)
        CacheEquipPanelSlots();

        // Start()에서 등록 → 모든 Awake()가 끝난 뒤라 Instance가 반드시 존재
        CombatPowerManager.Instance?.RegisterEquipmentProvider(GetEquippedItemInfos);

        // 등록 후 초기 전투력 계산
        CombatPowerManager.Instance?.Recalculate();

        // 씬 전환 후 PlayerStats가 재생성될 경우 장비 스탯 재적용
        RecalculateStats();
    }

    /// <summary>EquipPanelSlot이 Awake에서 자가 등록</summary>
    public void RegisterPanelSlot(EquipPanelSlot slot)
    {
        if (slot == null) return;
        if (registeredPanelSlots.Contains(slot)) return;
        registeredPanelSlots.Add(slot);
        cachedPanelSlots = registeredPanelSlots.ToArray();
        Debug.Log($"[EquipmentManager] EquipPanelSlot 등록: {slot.slotType} (총 {cachedPanelSlots.Length}개)");
    }

    private List<EquipPanelSlot> registeredPanelSlots = new List<EquipPanelSlot>();

    /// <summary>EquipPanelSlot 참조 캐시 (비활성 오브젝트 포함)</summary>
    public void CacheEquipPanelSlots()
    {
        // Resources.FindObjectsOfTypeAll: 비활성 포함, 모든 Unity 버전 호환
        var all = Resources.FindObjectsOfTypeAll<EquipPanelSlot>();
        var sceneSlots = new List<EquipPanelSlot>();
        foreach (var s in all)
        {
            if (s != null && s.gameObject.scene.isLoaded)
                sceneSlots.Add(s);
        }

        cachedPanelSlots = sceneSlots.ToArray();
        registeredPanelSlots = new List<EquipPanelSlot>(sceneSlots);

        Debug.Log($"[EquipmentManager] EquipPanelSlot 캐시: {cachedPanelSlots.Length}개");
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
    public bool EquipItem(EquipmentData equipment, int enhanceLevel = 0, int itemLevel = 0)
    {
        if (equipment == null) return false;

        EquipmentType type = equipment.equipmentType;

        // 기존 장비 제거
        if (equippedItems.ContainsKey(type))
        {
            var old = equippedItems[type];

            ApplyEquipmentStats(old.equipment, old.enhanceLevel, false);
            // ★ 강화 레벨 보존하여 인벤토리에 반환
            InventoryManager.Instance?.AddItemWithEnhancement(old.equipment, 1, old.enhanceLevel);
            EquipmentSkillSystem.Instance?.OnEquipmentUnequipped(type);
        }

        equippedItems[type] = new EquippedEntry
        {
            equipment = equipment,
            enhanceLevel = enhanceLevel,
            itemLevel = itemLevel
        };

        ApplyEquipmentStats(equipment, enhanceLevel, true);
        RefreshEquipmentSlotUI(type, equipment, enhanceLevel);

        OnEquipmentChanged?.Invoke(type, equipment, enhanceLevel);
        EquipmentSkillSystem.Instance?.OnEquipmentEquipped(type, equipment);

        // ★ 스킬 조합 재판정 (장비 레어리티 변경)
        SkillComboSystem.Instance?.ForceReshuffle();

        // 장착 즉시 전투력 갱신
        CombatPowerManager.Instance?.Recalculate();

        // 장착 변경 즉시 저장
        SaveLoadManager.Instance?.SaveGame();

        Debug.Log($"[EquipmentManager] 장착: {equipment.itemName} +{enhanceLevel} Lv.{itemLevel}");
        TutorialManager.Instance?.OnActionCompleted("EquipItem");

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

        // ★ 강화 레벨 보존하여 인벤토리에 반환
        InventoryManager.Instance?.AddItemWithEnhancement(entry.equipment, 1, entry.enhanceLevel);
        ClearEquipmentSlotUI(type);

        OnEquipmentChanged?.Invoke(type, null, 0);
        EquipmentSkillSystem.Instance?.OnEquipmentUnequipped(type);

        // ★ 스킬 조합 재판정
        SkillComboSystem.Instance?.ForceReshuffle();

        CombatPowerManager.Instance?.Recalculate();

        // 해제 변경 즉시 저장
        SaveLoadManager.Instance?.SaveGame();

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

        if (cachedPanelSlots == null || cachedPanelSlots.Length == 0)
            CacheEquipPanelSlots();

        // ★ CacheEquipPanelSlots 후에도 null일 수 있으므로 방어
        if (cachedPanelSlots == null) return;

        // ★ 같은 slotType의 모든 슬롯에 강화 레벨 반영
        foreach (var slot in cachedPanelSlots)
        {
            if (slot == null) continue;
            if (slot.slotType == type)
                slot.UpdateEnhanceLevel(newLevel);
        }

        OnEquipmentChanged?.Invoke(type, entry.equipment, newLevel);

        // ★ 강화 레벨에 따른 스킬 자동 레벨업
        UpdateSkillLevelByEnhance(type, newLevel);

        PlayerStats.Instance?.UpdateStatsUI();
        CombatPowerManager.Instance?.Recalculate();

        // 강화 변경 즉시 저장
        SaveLoadManager.Instance?.SaveGame();
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

    /// <summary>itemID로 장착 여부 확인 (인벤토리 슬롯 E뱃지용)</summary>
    public bool IsEquippedByID(int itemID)
    {
        foreach (var kvp in equippedItems)
        {
            if (kvp.Value != null && kvp.Value.equipment != null
                && kvp.Value.equipment.itemID == itemID)
                return true;
        }
        return false;
    }

    /// <summary>itemID로 장착 여부 확인 (InventorySlot 호환)</summary>
    public bool IsItemEquipped(int itemID)
    {
        return IsEquippedByID(itemID);
    }

    /// <summary>
    /// 인벤토리에서 직접 장착 (InventorySlot 액션 버튼용)
    /// 인벤토리에서 아이템 제거 + 기존 장비 교체 + 장착
    /// </summary>
    public bool EquipFromInventory(InventoryItemData invItem, int slotIndex)
    {
        if (invItem == null) return false;

        // ItemDatabase에서 EquipmentData 조회
        EquipmentData equipment = ItemDatabase.Instance?.GetEquipmentByID(invItem.itemID);
        if (equipment == null)
        {
            Debug.LogWarning($"[EquipmentManager] EquipFromInventory: itemID={invItem.itemID} 장비 데이터 없음");
            UIManager.Instance?.ShowMessage("장비 정보를 찾을 수 없습니다!", Color.red);
            return false;
        }

        // 인벤토리에서 제거
        if (InventoryManager.Instance != null)
        {
            if (slotIndex >= 0)
                InventoryManager.Instance.RemoveItemAt(slotIndex);
            else
                InventoryManager.Instance.RemoveItem(equipment, 1);
        }

        // 장착 (기존 장비는 EquipItem 내부에서 인벤토리로 반환)
        bool result = EquipItem(equipment, invItem.enhanceLevel, invItem.itemLevel);

        if (result)
        {
            SoundManager.Instance?.PlayEquip();
            UIManager.Instance?.ShowMessage($"{equipment.itemName} 장착!", Color.green);
            InventoryManager.Instance?.RefreshEquipDisplay();
        }

        return result;
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
        if (cachedPanelSlots == null || cachedPanelSlots.Length == 0)
            CacheEquipPanelSlots();
        if (cachedPanelSlots == null) return;

        // ★ 같은 slotType의 모든 슬롯 업데이트 (복사된 슬롯이 여러 개 있을 수 있음)
        foreach (var slot in cachedPanelSlots)
        {
            if (slot == null) continue;
            if (slot.slotType == type)
                slot.EquipItem(eq, enhLevel);
        }
    }

    private void ClearEquipmentSlotUI(EquipmentType type)
    {
        if (cachedPanelSlots == null || cachedPanelSlots.Length == 0)
            CacheEquipPanelSlots();
        if (cachedPanelSlots == null) return;

        // ★ 같은 slotType의 모든 슬롯 클리어
        foreach (var slot in cachedPanelSlots)
        {
            if (slot == null) continue;
            if (slot.slotType == type)
                slot.UnequipItem();
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
                enhanceLevel = kvp.Value.enhanceLevel,
                itemLevel = kvp.Value.itemLevel
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
                enhanceLevel = slotData.enhanceLevel,
                itemLevel = slotData.itemLevel
            };

            RefreshEquipmentSlotUI(slotData.slotType, eq, slotData.enhanceLevel);
            EquipmentSkillSystem.Instance?.OnEquipmentEquipped(slotData.slotType, eq);
        }

        RecalculateStats();
        CombatPowerManager.Instance?.Recalculate();

        Debug.Log($"[EquipmentManager] 장비 로드 완료: {equippedItems.Count}개");

        // ★ 로드 후 모든 장비의 강화 레벨에 따른 스킬 레벨 동기화
        foreach (var kvp in equippedItems)
            UpdateSkillLevelByEnhance(kvp.Key, kvp.Value.enhanceLevel);
    }

    // ═══════════════════════════════════════════════════════
    //  강화 레벨 → 스킬 자동 레벨업
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 장비 강화 레벨에 따라 해당 장비 스킬의 레벨을 자동 설정
    /// 공식: 스킬 레벨 = 1 + (강화레벨 / EnhancePerSkillLevel)
    /// 기본: 강화 5당 스킬 레벨 +1 (5강=Lv2, 10강=Lv3...)
    /// </summary>
    private void UpdateSkillLevelByEnhance(EquipmentType type, int enhanceLevel)
    {
        if (SkillManager.Instance == null || EquipmentSkillSystem.Instance == null)
            return;

        if (!equippedItems.TryGetValue(type, out var entry) || entry.equipment == null)
            return;

        // 이 장비 슬롯의 스킬 찾기
        SkillData skill = EquipmentSkillSystem.Instance.GetActiveSkillForSlot(type);
        if (skill == null) return;

        LearnedSkill learned = SkillManager.Instance.GetLearnedSkill(skill.skillID);
        if (learned == null) return;

        int newSkillLevel = SkillAutoLevelConfig.GetSkillLevel(enhanceLevel);
        newSkillLevel = Mathf.Clamp(newSkillLevel, 1, skill.maxLevel);

        if (learned.currentLevel != newSkillLevel)
        {
            int oldLevel = learned.currentLevel;
            learned.currentLevel = newSkillLevel;

            if (newSkillLevel > oldLevel)
            {
                UIManager.Instance?.ShowMessage(
                    $"스킬 레벨업! {skill.skillName} Lv.{newSkillLevel}", Color.yellow);
            }

            Debug.Log($"[Equipment→Skill] {type} 강화+{enhanceLevel} → {skill.skillName} Lv.{newSkillLevel}");
        }
    }
}