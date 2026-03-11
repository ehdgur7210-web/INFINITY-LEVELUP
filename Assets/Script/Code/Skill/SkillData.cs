using UnityEngine;

// ============================================================
// 스킬 타입
// ============================================================
public enum SkillType
{
    Active,
    Passive,
    Buff,
    Debuff
}

public enum SkillTarget
{
    Self,
    Monster,
    Ally,
    Area
}

public enum SkillEffectType
{
    Damage,
    Heal,
    AttackBuff,
    DefenseBuff,
    SpeedBuff,
    Stun,
    Slow,
    DamageOverTime,
    Shield
}

public enum AttackStyleType
{
    Melee,
    Ranged,
    Magic
}

// ============================================================
// 스킬 데이터
// ============================================================
[CreateAssetMenu(fileName = "New Skill", menuName = "Game/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("기본 정보")]
    public int skillID;
    public string skillName;
    [TextArea(2, 4)]
    public string skillDescription;
    public Sprite skillIcon;

    [Header("스킬 타입")]
    public SkillType skillType;
    public SkillTarget targetType;
    public SkillEffectType effectType;

    [Header("공격 스타일 (Damage 타입 스킬용)")]
    public AttackStyleType attackStyle = AttackStyleType.Melee;

    [Header("스킬 수치")]
    public int requiredLevel = 1;
    public int requiredSkillPoints = 1;
    public int maxLevel = 5;

    [Header("효과")]
    public float baseValue;
    public float valuePerLevel;
    public float duration;

    [Header("사용 조건")]
    public float cooldown;
    public int manaCost;
    public float castTime;

    [Header("범위")]
    public float range;
    public float areaRadius;

    [Header("선행 스킬")]
    public SkillData[] prerequisiteSkills;

    [Header("시각 효과")]
    public GameObject effectPrefab;
    public AudioClip skillSound;

    [Header("공격 이펙트 프리팹")]
    [Tooltip("근거리=검기이펙트 / 원거리=총알프리팹 / 마법=파이어볼프리팹")]
    public GameObject attackEffectPrefab;

    public float GetValueAtLevel(int level)
    {
        return baseValue + (valuePerLevel * (level - 1));
    }
}