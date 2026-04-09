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
    [Tooltip("스킬 최대 레벨 (기본 100)")]
    public int maxLevel = 100;

    // ─── 레벨업 재료 시스템 ──────────────────────────────────────
    [Header("★ 레벨업 재료 (인스펙터에서 설정)")]
    [Tooltip("이 스킬을 레벨업할 때 소비되는 동일 장비 (있으면 우선 사용)")]
    public EquipmentData materialEquipment;

    [Tooltip("이 스킬을 레벨업할 때 소비되는 동일 동료 (장비가 없을 때 사용)")]
    public CompanionData materialCompanion;

    [Tooltip("레벨별 필요 재료 개수. 배열 인덱스 = 현재 레벨-1.\n" +
             "배열 크기보다 레벨이 크면 마지막 값 사용.")]
    public int[] materialCountPerLevel = new int[]
    {
        1, 1, 2, 2, 3,  // Lv1→2 ~ Lv5→6
        3, 4, 4, 5, 5,  // Lv6→7 ~ Lv10→11
        6, 6, 7, 7, 8,  // Lv11→12 ~ Lv15→16
        8, 9, 9, 10, 10 // Lv16→17 ~ Lv20→21
        // 이후는 마지막 값(10) 반복
    };

    [Header("효과")]
    public float baseValue;
    public float valuePerLevel;
    public float duration;

    [Header("사용 조건")]
    public float cooldown;
    public int manaCost;
    public float castTime;

    [Header("발사 속도")]
    [Tooltip("투사체 발사 간격 (초). 0이면 PlayerController 기본 fireRate 사용")]
    public float fireRate;

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

    [Header("히트 이펙트 프리팹")]
    [Tooltip("맞은 몬스터마다 생성되는 이펙트 (번개, 폭발 등). 없으면 생략")]
    public GameObject hitEffectPrefab;

    public float GetValueAtLevel(int level)
    {
        return baseValue + (valuePerLevel * (level - 1));
    }

    /// <summary>
    /// 현재 레벨에서 다음 레벨로 올리는 데 필요한 재료 개수.
    /// 배열보다 레벨이 크면 마지막 값 반복.
    /// </summary>
    public int GetRequiredMaterialCount(int currentLevel)
    {
        if (materialCountPerLevel == null || materialCountPerLevel.Length == 0) return 1;
        int idx = Mathf.Clamp(currentLevel - 1, 0, materialCountPerLevel.Length - 1);
        return materialCountPerLevel[idx];
    }
}