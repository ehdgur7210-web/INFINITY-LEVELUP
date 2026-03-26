using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 동료 스킬 슬롯 프리팹용 컴포넌트
///
/// [프리팹 구조]
///   CompanionSkillSlot (Button)
///   ├── SkillIcon (Image)          → skillIcon
///   ├── SkillName (TMP)            → skillNameText
///   ├── SkillLevel (TMP)           → skillLevelText
///   ├── LockIcon (Image/GO)        → lockIcon (잠금 시 표시)
///   └── Background (Image)         → background (선택 강조용)
///
/// [Inspector 연결]
///   skillIcon       → SkillIcon (Image)
///   skillNameText   → SkillName (TextMeshProUGUI)
///   skillLevelText  → SkillLevel (TextMeshProUGUI)
///   lockIcon        → LockIcon (GameObject, 선택)
///   background      → Background (Image, 선택)
/// </summary>
public class CompanionSkillSlot : MonoBehaviour
{
    [Header("UI 참조")]
    public Image skillIcon;
    public TextMeshProUGUI skillNameText;
    public TextMeshProUGUI skillLevelText;
    public GameObject lockIcon;
    public Image background;

    [Header("색상")]
    public Color normalColor = new Color(0.25f, 0.25f, 0.25f, 0.9f);
    public Color selectedColor = new Color(0.4f, 0.3f, 0.1f, 0.9f);
    public Color lockedColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);

    private CompanionSkillInfo _skillInfo;
    private int _skillLevel;
    private bool _locked;
    private bool _selected;

    /// <summary>슬롯 설정</summary>
    public void Setup(CompanionSkillInfo skill, int level, bool locked)
    {
        _skillInfo = skill;
        _skillLevel = level;
        _locked = locked;

        // 아이콘
        if (skillIcon != null)
        {
            if (skill != null && skill.icon != null)
            {
                skillIcon.sprite = skill.icon;
                skillIcon.color = locked ? new Color(0.3f, 0.3f, 0.3f) : Color.white;
                skillIcon.gameObject.SetActive(true);
            }
            else
            {
                skillIcon.gameObject.SetActive(false);
            }
        }

        // 이름
        if (skillNameText != null)
        {
            skillNameText.text = skill != null ? skill.skillName : "";
            skillNameText.color = locked ? Color.gray : Color.white;
        }

        // 레벨
        if (skillLevelText != null)
        {
            if (locked)
                skillLevelText.text = $"Lv.{skill?.unlockCompanionLevel} 해금";
            else
                skillLevelText.text = $"Lv.{level}";
            skillLevelText.color = locked ? Color.gray : Color.white;
        }

        // 잠금 아이콘
        if (lockIcon != null)
            lockIcon.SetActive(locked);

        // 배경
        if (background != null)
            background.color = locked ? lockedColor : normalColor;
    }

    /// <summary>선택 강조</summary>
    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (background != null && !_locked)
            background.color = selected ? selectedColor : normalColor;
    }

    /// <summary>자동 바인딩 — Inspector 미연결 시 자식에서 탐색</summary>
    public void AutoBind()
    {
        if (skillIcon == null)
        {
            Transform t = transform.Find("SkillIcon");
            if (t != null) skillIcon = t.GetComponent<Image>();
        }
        if (skillNameText == null)
        {
            Transform t = transform.Find("SkillName");
            if (t != null) skillNameText = t.GetComponent<TextMeshProUGUI>();
        }
        if (skillLevelText == null)
        {
            Transform t = transform.Find("SkillLevel");
            if (t != null) skillLevelText = t.GetComponent<TextMeshProUGUI>();
        }
        if (lockIcon == null)
        {
            Transform t = transform.Find("LockIcon");
            if (t != null) lockIcon = t.gameObject;
        }
        if (background == null)
        {
            background = GetComponent<Image>();
        }
    }
}
