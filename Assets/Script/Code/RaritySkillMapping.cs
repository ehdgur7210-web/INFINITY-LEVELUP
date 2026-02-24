using UnityEngine;

/// <summary>
/// 장비 슬롯별 등급별 스킬 매핑 클래스
/// 각 슬롯마다 5개 등급(Common~Legendary)별로 다른 스킬 설정 가능
/// </summary>
[System.Serializable]
public class RaritySkillMapping
{
    [Header("등급별 스킬 설정")]
    [Tooltip("커먼 등급 장비 장착 시 활성화될 스킬")]
    public SkillData commonSkill;

    [Tooltip("언커먼 등급 장비 장착 시 활성화될 스킬")]
    public SkillData uncommonSkill;

    [Tooltip("레어 등급 장비 장착 시 활성화될 스킬")]
    public SkillData rareSkill;

    [Tooltip("에픽 등급 장비 장착 시 활성화될 스킬")]
    public SkillData epicSkill;

    [Tooltip("레전더리 등급 장비 장착 시 활성화될 스킬")]
    public SkillData legendarySkill;

    /// <summary>
    /// 등급에 맞는 스킬 반환
    /// </summary>
    public SkillData GetSkillByRarity(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return commonSkill;
            case ItemRarity.Uncommon: return uncommonSkill;
            case ItemRarity.Rare: return rareSkill;
            case ItemRarity.Epic: return epicSkill;
            case ItemRarity.Legendary: return legendarySkill;
            default: return null;
        }
    }

    /// <summary>
    /// 설정된 스킬이 하나라도 있는지 확인
    /// </summary>
    public bool HasAnySkill()
    {
        return commonSkill != null || uncommonSkill != null ||
               rareSkill != null || epicSkill != null || legendarySkill != null;
    }
}