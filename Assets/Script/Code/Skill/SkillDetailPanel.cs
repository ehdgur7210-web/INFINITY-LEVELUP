using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 스킬 상세 정보 패널 (스킬 슬롯 클릭 시 하단에 표시)
/// 레벨업은 장비 강화로 자동 처리됨 (강화 5당 스킬 Lv +1)
///
/// ★ Unity 계층 구조:
///   SkillDetailPanel
///     ├── SkillIcon (Image)
///     ├── SkillName (TMP)
///     ├── SkillType (TMP)
///     ├── SkillDescription (TMP)
///     ├── StatsText (TMP) — 현재 데미지/효과
///     └── LevelInfoText (TMP) — "Lv.2 (다음: 강화 +10)"
/// </summary>
public class SkillDetailPanel : MonoBehaviour
{
    public static SkillDetailPanel Instance { get; private set; }

    [Header("스킬 정보 UI")]
    [SerializeField] private Image skillIcon;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI skillDescriptionText;
    [SerializeField] private TextMeshProUGUI skillTypeText;

    [Header("수치 UI")]
    [SerializeField] private TextMeshProUGUI statsText;

    [Header("레벨 정보 UI")]
    [SerializeField] private TextMeshProUGUI levelInfoText;

    [Header("패널")]
    [SerializeField] private GameObject detailRoot;

    private SkillTreeSlot currentSlot;

    void Awake()
    {
        Instance = this;
        if (detailRoot != null)
            detailRoot.SetActive(false);
    }

    /// <summary>스킬 슬롯 클릭 시 호출</summary>
    public void ShowSkillDetail(SkillTreeSlot slot)
    {
        if (slot == null || slot.learnedSkill == null || slot.learnedSkill.skillData == null)
            return;

        currentSlot = slot;

        if (detailRoot != null)
            detailRoot.SetActive(true);

        RefreshDetail();
    }

    /// <summary>패널 닫기</summary>
    public void HideDetail()
    {
        if (detailRoot != null)
            detailRoot.SetActive(false);
        currentSlot = null;
    }

    /// <summary>상세 정보 갱신</summary>
    private void RefreshDetail()
    {
        if (currentSlot == null || currentSlot.learnedSkill == null) return;

        SkillData skill = currentSlot.learnedSkill.skillData;
        int curLevel = currentSlot.learnedSkill.currentLevel;
        bool isLearned = curLevel > 0;
        bool isMaxLevel = curLevel >= skill.maxLevel;

        // ── 아이콘 ──
        if (skillIcon != null)
        {
            skillIcon.sprite = skill.skillIcon;
            skillIcon.color = Color.white;
        }

        // ── 이름 ──
        if (skillNameText != null)
            skillNameText.text = skill.skillName;

        // ── 설명 ──
        if (skillDescriptionText != null)
            skillDescriptionText.text = skill.skillDescription;

        // ── 스킬 타입 ──
        if (skillTypeText != null)
        {
            string typeStr = skill.skillType switch
            {
                SkillType.Active => "액티브",
                SkillType.Passive => "패시브",
                SkillType.Buff => "버프",
                SkillType.Debuff => "디버프",
                _ => ""
            };
            skillTypeText.text = typeStr;
        }

        // ── 수치 표시 ──
        UpdateStats(skill, curLevel, isLearned);

        // ── 레벨 정보 ──
        UpdateLevelInfo(skill, curLevel, isLearned, isMaxLevel);
    }

    /// <summary>현재 수치 표시 (데미지/효과)</summary>
    private void UpdateStats(SkillData skill, int curLevel, bool isLearned)
    {
        if (statsText == null) return;

        string effectLabel = skill.effectType switch
        {
            SkillEffectType.Damage => "데미지",
            SkillEffectType.Heal => "회복량",
            SkillEffectType.AttackBuff => "공격력 증가",
            SkillEffectType.DefenseBuff => "방어력 증가",
            SkillEffectType.SpeedBuff => "이동속도 증가",
            _ => "효과"
        };

        int displayLevel = isLearned ? curLevel : 1;
        float value = skill.GetValueAtLevel(displayLevel);

        string text = $"{effectLabel}: <color=#00FF88>{value:F0}</color>";

        if (skill.cooldown > 0)
            text += $"\n쿨타임: {skill.cooldown:F1}초";
        if (skill.duration > 0)
            text += $"\n지속시간: {skill.duration:F1}초";
        if (skill.manaCost > 0)
            text += $"\nMP 소모: {skill.manaCost:F0}";

        statsText.text = text;
    }

    /// <summary>레벨 정보 + 다음 레벨업 조건</summary>
    private void UpdateLevelInfo(SkillData skill, int curLevel, bool isLearned, bool isMaxLevel)
    {
        if (levelInfoText == null) return;

        if (!isLearned)
        {
            // 장비 장착 시 자동 습득
            levelInfoText.text = "장비 장착 시 자동 습득";
        }
        else if (isMaxLevel)
        {
            levelInfoText.text = $"<color=#FFD700>Lv.{curLevel} (MAX)</color>";
        }
        else
        {
            // 다음 레벨에 필요한 강화 수치
            int nextEnhanceNeeded = (curLevel) * SkillAutoLevelConfig.EnhancePerSkillLevel;
            levelInfoText.text = $"Lv.{curLevel}  →  다음 Lv: 강화 +{nextEnhanceNeeded} 필요";
        }
    }
}

/// <summary>
/// 스킬 자동 레벨업 설정값
/// 강화 5당 스킬 레벨 1 상승 (변경 가능)
/// </summary>
public static class SkillAutoLevelConfig
{
    /// <summary>스킬 레벨 1당 필요한 장비 강화 수치</summary>
    public static int EnhancePerSkillLevel = 5;

    /// <summary>강화 레벨로 스킬 레벨 계산</summary>
    public static int GetSkillLevel(int enhanceLevel)
    {
        return 1 + (enhanceLevel / EnhancePerSkillLevel);
    }
}
