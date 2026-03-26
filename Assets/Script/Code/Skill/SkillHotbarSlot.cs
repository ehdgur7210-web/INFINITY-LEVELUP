using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 스킬 핫바 슬롯
/// ✅ 핵심: Update에서 SkillManager.GetLearnedSkill()로 직접 조회
///    → LearnedSkill 직렬화 복사본 문제 해결
/// </summary>
public class SkillHotbarSlot : MonoBehaviour, IDropHandler
{
    [Header("슬롯 정보")]
    public int slotIndex;
    public LearnedSkill assignedSkill;

    [Header("UI 참조")]
    public Image skillIconImage;
    public Image cooldownOverlay;
    public TextMeshProUGUI cooldownText;
    public TextMeshProUGUI keyBindText;

    // ─────────────────────────────────────────
    void Awake()
    {
        if (cooldownOverlay != null)
        {
            cooldownOverlay.type = Image.Type.Filled;
            cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
            cooldownOverlay.fillClockwise = true;
            cooldownOverlay.fillAmount = 0f;
            cooldownOverlay.enabled = false;
        }
        if (cooldownText != null)
            cooldownText.enabled = false;
    }

    void Start()
    {
        if (keyBindText != null)
            keyBindText.text = (slotIndex + 1).ToString();
        RefreshIcon();
        // ★ 추가: Button 컴포넌트 자동 연결
        Button btn = GetComponent<Button>();
        if (btn == null)
            btn = gameObject.AddComponent<Button>(); // 없으면 자동 추가

        btn.onClick.AddListener(UseSkill); // 클릭 시 UseSkill 호출
    }

    // ─────────────────────────────────────────
    // ✅ Update: SkillManager에서 직접 조회해서 쿨타임 표시
    // ─────────────────────────────────────────
    void Update()
    {
        if (assignedSkill == null || assignedSkill.skillData == null) return;
        if (cooldownOverlay == null) return;

        // ✅ 직렬화 복사본 문제 해결: SkillManager에서 실제 인스턴스 조회
        LearnedSkill live = null;
        if (SkillManager.Instance != null)
            live = SkillManager.Instance.GetLearnedSkill(assignedSkill.skillData.skillID);

        // 조회 실패 시 assignedSkill 직접 사용 (fallback)
        if (live == null) live = assignedSkill;

        float remaining = live.cooldownRemaining;
        float total = live.skillData.cooldown;

        if (remaining > 0f && total > 0f)
        {
            // ✅ 쿨타임 중: fillAmount 1→0 시계방향
            cooldownOverlay.enabled = true;
            cooldownOverlay.fillAmount = remaining / total;
            // ★ Fix: 오버레이 색상 유지 (검정 반투명), 아이콘 sprite는 이미 설정됨
            cooldownOverlay.color = new Color(0f, 0f, 0f, 0.65f);

            if (cooldownText != null)
            {
                cooldownText.enabled = true;
                cooldownText.text = remaining >= 1f
                    ? Mathf.CeilToInt(remaining).ToString()
                    : remaining.ToString("F1");
            }

            if (skillIconImage != null)
                skillIconImage.color = Color.white; // ★ Fix: 아이콘은 항상 선명하게, 오버레이로 어둡게 처리
        }
        else
        {
            
            cooldownOverlay.fillAmount = 0f;
            cooldownOverlay.enabled = false;

            if (cooldownText != null)
                cooldownText.enabled = false;

            if (skillIconImage != null)
                skillIconImage.color = Color.white;
        }
    }

    // ─────────────────────────────────────────
    // 스킬 사용: 차단 없이 SkillManager에 전달
    // ─────────────────────────────────────────
    public void UseSkill()
    {
        if (assignedSkill == null) return;
        // ★ 스킬 사용 효과음
        SoundManager.Instance?.PlaySkillSound();
        SkillManager.Instance?.UseSkill(assignedSkill.skillData.skillID);
    }

    // ─────────────────────────────────────────
    public void AssignSkill(LearnedSkill skill)
    {
        assignedSkill = skill;
        RefreshIcon();
        CombatPowerManager.Instance?.Recalculate();
    }

    public void ClearSlot()
    {
        assignedSkill = null;
        RefreshIcon();
        CombatPowerManager.Instance?.Recalculate();
    }

    void RefreshIcon()
    {
        if (skillIconImage == null) return;
        if (assignedSkill != null && assignedSkill.skillData != null)
        {
            skillIconImage.sprite = assignedSkill.skillData.skillIcon;
            skillIconImage.color = Color.white;
            skillIconImage.enabled = true;

            // ★ Fix: 쿨타임 오버레이에도 동일한 스킬 아이콘 적용
            // cooldownOverlay가 스킬 이미지 위에 fillAmount로 어두워지는 방식
            if (cooldownOverlay != null)
            {
                cooldownOverlay.sprite = assignedSkill.skillData.skillIcon;
                cooldownOverlay.color = new Color(0f, 0f, 0f, 0.65f); // 반투명 어두운 오버레이
            }
        }
        else
        {
            skillIconImage.enabled = false;
            if (cooldownOverlay != null)
                cooldownOverlay.sprite = null;
        }
    }

    // ─────────────────────────────────────────
    public void OnDrop(PointerEventData eventData)
    {
        SkillTreeSlot drag = eventData.pointerDrag?.GetComponent<SkillTreeSlot>();
        if (drag == null || drag.learnedSkill == null) return;

        if (drag.learnedSkill.skillData.skillType == SkillType.Active)
        {
            AssignSkill(drag.learnedSkill);
            Debug.Log($"[Hotbar] {slotIndex + 1}번 → {drag.learnedSkill.skillData.skillName}");
        }
        else
        {
            UIManager.Instance?.ShowMessage("액티브 스킬만 등록 가능!", Color.red);
        }
    }
}