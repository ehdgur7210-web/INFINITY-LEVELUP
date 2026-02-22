using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ============================================================
/// EquipmentSkillSystem — 장비 슬롯별 등급별 스킬 연동 시스템
/// ============================================================
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

    private Dictionary<EquipmentType, SkillData> activeSkills =
        new Dictionary<EquipmentType, SkillData>();

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
        if (activeSkills.ContainsKey(slotType) && activeSkills[slotType] != null)
        {
            var oldSkill = activeSkills[slotType];
            if (oldSkill != skill && SkillManager.Instance != null)
            {
                SkillManager.Instance.RemoveSkillFromEquipment(oldSkill);
                Debug.Log($"[EquipmentSkillSystem] 기존 스킬 제거: {oldSkill.skillName}");
            }
        }

        activeSkills[slotType] = skill;

        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.LearnSkillFromEquipment(skill);
        }

        Debug.Log($"[EquipmentSkillSystem] ⭐ {skill.skillName} 활성화! (슬롯: {slotType}, 등급: {rarity})");

        if (UIManager.Instance != null)
        {
            Color messageColor = GetRarityColor(rarity);
            UIManager.Instance.ShowMessage(
                $"⭐ {skill.skillName} 스킬 활성화! ({rarity})",
                messageColor
            );
        }
    }

    private void DeactivateSkill(EquipmentType slotType)
    {
        if (!activeSkills.TryGetValue(slotType, out SkillData skill) || skill == null)
            return;

        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.RemoveSkillFromEquipment(skill);
        }

        activeSkills.Remove(slotType);
        Debug.Log($"[EquipmentSkillSystem] {skill.skillName} 비활성화 (슬롯: {slotType})");
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return Color.white;
            case ItemRarity.Uncommon: return Color.green;
            case ItemRarity.Rare: return Color.blue;
            case ItemRarity.Epic: return new Color(0.58f, 0f, 0.83f); // 보라색
            case ItemRarity.Legendary: return new Color(1f, 0.5f, 0f);     // 주황색
            default: return Color.white;
        }
    }

    public bool IsSkillActive(EquipmentType slotType)
    {
        return activeSkills.ContainsKey(slotType) && activeSkills[slotType] != null;
    }

    public SkillData GetActiveSkillForSlot(EquipmentType slotType)
    {
        activeSkills.TryGetValue(slotType, out SkillData skill);
        return skill;
    }

    public List<SkillData> GetAllActiveSkills()
    {
        var list = new List<SkillData>();
        foreach (var skill in activeSkills.Values)
        {
            if (skill != null)
                list.Add(skill);
        }
        return list;
    }

    public RaritySkillMapping GetSkillMappingForSlot(EquipmentType slotType)
    {
        slotSkillMap.TryGetValue(slotType, out RaritySkillMapping mapping);
        return mapping;
    }

    [ContextMenu("Debug: Print Active Skills")]
    public void DebugPrintActiveSkills()
    {
        Debug.Log("========== 현재 활성화된 스킬 ==========");
        foreach (var kvp in activeSkills)
        {
            if (kvp.Value != null)
            {
                Debug.Log($"{kvp.Key}: {kvp.Value.skillName}");
            }
        }
        Debug.Log($"총 {activeSkills.Count}개 활성화");
        Debug.Log("=========================================");
    }
}