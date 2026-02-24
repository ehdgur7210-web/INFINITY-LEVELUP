using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ============================================================
/// EquipmentSkillSystem — 장비 슬롯별 등급별 스킬 연동 시스템
/// ============================================================
/// 
/// ✅ 핫바 배치 구조 (장비 슬롯 = 고정 핫바 슬롯):
///
///   슬롯:  0        1        2      3      4      5
///          왼손무기  오른손무기  투구   갑옷   장갑   신발
///
/// ★ 등급이 달라져도 같은 슬롯에 배치됨 (스킬 아이콘만 교체)
/// ★ 핫바 슬롯이 6개 이상 있으면 정상 작동합니다!
///
/// ✅ 수정 내역:
///   - [스킬 소실 수정] 등급별 행 계산(row*6+col) 제거 → 장비타입=고정슬롯(0~5)
///             Common Armor=슬롯3, Uncommon Armor=슬롯3 (동일 위치, 스킬만 변경)
///   - [Bug2/3] DeactivateSkill이 skillID 검색 대신 저장된 슬롯 인덱스를 직접 클리어
///             → 갑옷/부츠 같은 SkillData일 때도 서로 침범하지 않음
///   - [Bug1/4] activeSkills에 rarity + 핫바 슬롯 인덱스 함께 저장
///   - [Bug1]   RefreshAllEquippedSkills가 EquipmentManager에서 실제 장착 장비를 읽어 재적용
/// </summary>
public class EquipmentSkillSystem : MonoBehaviour
{
    public static EquipmentSkillSystem Instance { get; private set; }

    [Header("장비 슬롯별 등급별 스킬 매핑")]
    [Tooltip("왼손 무기 슬롯의 등급별 스킬")]
    public RaritySkillMapping weaponLeftSkills;

    [Tooltip("오른손 무기 슬롯의 등급별 스킬")]
    public RaritySkillMapping weaponRightSkills;

    [Tooltip("투구 슬롯의 등급별 스킬")]
    public RaritySkillMapping helmetSkills;

    [Tooltip("갑옷 슬롯의 등급별 스킬")]
    public RaritySkillMapping armorSkills;

    [Tooltip("장갑 슬롯의 등급별 스킬")]
    public RaritySkillMapping glovesSkills;

    [Tooltip("신발 슬롯의 등급별 스킬")]
    public RaritySkillMapping bootsSkills;

    private Dictionary<EquipmentType, RaritySkillMapping> slotSkillMap =
        new Dictionary<EquipmentType, RaritySkillMapping>();

    // ✅ [Bug1/2/3/4 수정] 스킬 + 등급 + 핫바 슬롯 인덱스를 함께 저장
    private class ActiveSkillEntry
    {
        public SkillData skill;
        public ItemRarity rarity;
        public int hotbarSlotIndex; // 현재 이 스킬이 배치된 핫바 슬롯 번호
    }
    private Dictionary<EquipmentType, ActiveSkillEntry> activeSkills =
        new Dictionary<EquipmentType, ActiveSkillEntry>();

    // ─── 핫바 배치 계산용 상수 ───────────────────────────────
    private const int NUM_EQUIPMENT_SLOTS = 6;

    private static readonly Dictionary<EquipmentType, int> equipmentColumnIndex
        = new Dictionary<EquipmentType, int>
    {
        { EquipmentType.WeaponLeft,  0 },
        { EquipmentType.WeaponRight, 1 },
        { EquipmentType.Helmet,      2 },
        { EquipmentType.Armor,       3 },
        { EquipmentType.Gloves,      4 },
        { EquipmentType.Boots,       5 },
    };

    // ✅ [스킬 소실 수정] rarityRowIndex 제거 - 등급은 슬롯 위치에 영향 없음
    // ────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        BuildSlotSkillMap();
    }

    private void BuildSlotSkillMap()
    {
        slotSkillMap.Clear();
        slotSkillMap[EquipmentType.WeaponLeft] = weaponLeftSkills;
        slotSkillMap[EquipmentType.WeaponRight] = weaponRightSkills;
        slotSkillMap[EquipmentType.Helmet] = helmetSkills;
        slotSkillMap[EquipmentType.Armor] = armorSkills;
        slotSkillMap[EquipmentType.Gloves] = glovesSkills;
        slotSkillMap[EquipmentType.Boots] = bootsSkills;

        Debug.Log($"[EquipmentSkillSystem] 등급별 매핑 테이블 구성 완료: {slotSkillMap.Count}개 슬롯");
    }

    // ───────────────────────────────────────────────────────
    /// <summary>
    /// ★ 핫바 슬롯 인덱스 계산
    /// ✅ [스킬 소실 수정] 등급(행)은 슬롯 위치에 영향 주지 않음
    /// 장비 슬롯 타입만으로 고정 슬롯에 배치 (0~5번 슬롯)
    /// 
    /// WeaponLeft=0, WeaponRight=1, Helmet=2, Armor=3, Gloves=4, Boots=5
    /// 
    /// 이전: row*6+col → Common Armor=3, Uncommon Armor=9 (범위 초과!)
    /// 수정: col만 사용 → 항상 Armor=3 슬롯에 배치
    /// </summary>
    private int GetHotbarSlotIndex(EquipmentType slotType, ItemRarity rarity)
    {
        if (!equipmentColumnIndex.TryGetValue(slotType, out int col))
        {
            Debug.LogWarning($"[EquipmentSkillSystem] {slotType}의 슬롯 인덱스가 없습니다!");
            return -1;
        }

        // ✅ 등급에 관계없이 장비 타입 = 고정 슬롯 위치
        Debug.Log($"[EquipmentSkillSystem] {slotType}({col}번 슬롯) {rarity} 등급 스킬 배치");
        return col;
    }
    // ───────────────────────────────────────────────────────

    public void OnEquipmentEquipped(EquipmentType slotType, EquipmentData equipment)
    {
        if (equipment == null)
        {
            Debug.LogWarning($"[EquipmentSkillSystem] {slotType} - equipment가 null입니다!");
            return;
        }

        if (!slotSkillMap.TryGetValue(slotType, out RaritySkillMapping skillMapping))
        {
            Debug.Log($"[EquipmentSkillSystem] {slotType} 슬롯 매핑 없음");
            return;
        }

        SkillData skill = skillMapping.GetSkillByRarity(equipment.rarity);

        if (skill == null)
        {
            Debug.Log($"[EquipmentSkillSystem] {slotType} - {equipment.rarity} 등급 스킬이 설정되지 않음");
            DeactivateSkill(slotType);
            return;
        }

        Debug.Log($"[EquipmentSkillSystem] {slotType} 스킬 활성화 — 스킬: {skill.skillName}, 등급: {equipment.rarity}");
        ActivateSkill(slotType, skill, equipment.rarity);
    }

    public void OnEquipmentUnequipped(EquipmentType slotType)
    {
        DeactivateSkill(slotType);
    }

    private void ActivateSkill(EquipmentType slotType, SkillData skill, ItemRarity rarity)
    {
        // ✅ [Bug2/3 수정] 이전 스킬 제거 시 저장된 핫바 인덱스로 직접 클리어
        if (activeSkills.TryGetValue(slotType, out ActiveSkillEntry oldEntry))
        {
            ClearHotbarSlotDirect(oldEntry.hotbarSlotIndex);
        }

        int targetSlotIndex = GetHotbarSlotIndex(slotType, rarity);

        // ✅ [Bug1/4 수정] 등급 + 핫바 인덱스를 함께 저장
        activeSkills[slotType] = new ActiveSkillEntry
        {
            skill = skill,
            rarity = rarity,
            hotbarSlotIndex = targetSlotIndex
        };

        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.SwapEquipmentSkillOnHotbarAtIndex(null, skill, targetSlotIndex);
        }

        if (UIManager.Instance != null)
        {
            Color messageColor = GetRarityColor(rarity);
            UIManager.Instance.ShowMessage($"⭐ {rarity} {skill.skillName} 활성화!", messageColor);
        }
    }

    // ✅ [Bug2/3 수정] skillID 검색 없이 저장된 슬롯 인덱스를 직접 클리어
    private void DeactivateSkill(EquipmentType slotType)
    {
        if (!activeSkills.TryGetValue(slotType, out ActiveSkillEntry entry) || entry == null)
            return;

        // 핫바에서 정확한 슬롯 인덱스로만 클리어 (다른 장비 슬롯 침범 없음)
        ClearHotbarSlotDirect(entry.hotbarSlotIndex);

        // SkillManager의 learned 목록에서는 제거하지 않음
        // → 같은 스킬이 여러 장비에 공유되어도 안전하게 처리됨
        // → 장비 해제 시 스킬 자체를 잊는 것이 아니라 핫바에서만 제거
        activeSkills.Remove(slotType);

        Debug.Log($"[EquipmentSkillSystem] {entry.skill?.skillName} 비활성화 (슬롯: {slotType}, 핫바: {entry.hotbarSlotIndex})");
    }

    /// <summary>핫바 특정 인덱스 슬롯만 직접 클리어</summary>
    private void ClearHotbarSlotDirect(int hotbarSlotIndex)
    {
        if (SkillManager.Instance == null) return;
        var hotbarSlots = SkillManager.Instance.hotbarSlots;
        if (hotbarSlots == null) return;
        if (hotbarSlotIndex < 0 || hotbarSlotIndex >= hotbarSlots.Length) return;
        if (hotbarSlots[hotbarSlotIndex] != null)
            hotbarSlots[hotbarSlotIndex].ClearSlot();
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return Color.white;
            case ItemRarity.Uncommon: return Color.green;
            case ItemRarity.Rare: return Color.blue;
            case ItemRarity.Epic: return new Color(0.58f, 0f, 0.83f);
            case ItemRarity.Legendary: return new Color(1f, 0.5f, 0f);
            default: return Color.white;
        }
    }

    public bool IsSkillActive(EquipmentType slotType)
        => activeSkills.ContainsKey(slotType) && activeSkills[slotType] != null;

    public SkillData GetActiveSkillForSlot(EquipmentType slotType)
    {
        activeSkills.TryGetValue(slotType, out ActiveSkillEntry entry);
        return entry?.skill;
    }

    public List<SkillData> GetAllActiveSkills()
    {
        var list = new List<SkillData>();
        foreach (var entry in activeSkills.Values)
            if (entry?.skill != null)
                list.Add(entry.skill);
        return list;
    }

    public RaritySkillMapping GetSkillMappingForSlot(EquipmentType slotType)
    {
        slotSkillMap.TryGetValue(slotType, out RaritySkillMapping mapping);
        return mapping;
    }

    /// <summary>
    /// ✅ [Bug1/4/5 수정] 씬 로드 또는 게임 재시작 후 모든 장착 장비의 스킬을 핫바에 올바르게 재배치
    /// EquipmentManager에서 실제 장착 데이터를 읽어 정확한 등급/위치로 복원
    /// </summary>
    public void RefreshAllEquippedSkills()
    {
        Debug.Log("[EquipmentSkillSystem] 모든 장착 장비 스킬 새로고침 시작");

        if (EquipmentManager.Instance == null)
        {
            Debug.LogWarning("[EquipmentSkillSystem] EquipmentManager가 없어 새로고침 불가");
            return;
        }

        // EquipmentManager에서 현재 장착 장비를 슬롯별로 읽어 재적용
        foreach (EquipmentType type in System.Enum.GetValues(typeof(EquipmentType)))
        {
            EquipmentData equippedItem = EquipmentManager.Instance.GetEquippedItem(type);
            if (equippedItem != null)
            {
                Debug.Log($"[EquipmentSkillSystem] 새로고침: {type} → {equippedItem.itemName} ({equippedItem.rarity})");
                OnEquipmentEquipped(type, equippedItem);
            }
        }

        Debug.Log("[EquipmentSkillSystem] 새로고침 완료");
    }

    [ContextMenu("Debug: Print Active Skills")]
    public void DebugPrintActiveSkills()
    {
        Debug.Log("========== 현재 활성화된 스킬 ==========");
        foreach (var kvp in activeSkills)
            if (kvp.Value?.skill != null)
                Debug.Log($"{kvp.Key}: {kvp.Value.skill.skillName} (등급: {kvp.Value.rarity}, 핫바: {kvp.Value.hotbarSlotIndex})");
        Debug.Log($"총 {activeSkills.Count}개 활성화");
        Debug.Log("=========================================");
    }
}