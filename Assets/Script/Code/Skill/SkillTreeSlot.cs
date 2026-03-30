using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 스킬 트리 슬롯 UI (깔끔한 아이콘 + 레벨 뱃지)
/// 클릭하면 SkillDetailPanel에 상세 정보 표시
/// </summary>
public class SkillTreeSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("스킬 정보")]
    public LearnedSkill learnedSkill;

    [Header("UI 참조")]
    public Image skillIconImage;            // 스킬 아이콘
    public TextMeshProUGUI skillLevelText;  // 레벨 뱃지 ("Lv.3")
    public GameObject levelBadge;           // 레벨 뱃지 배경 오브젝트
    public Image slotFrame;                 // 슬롯 테두리 (선택 강조용)

    [Header("상태")]
    public bool isLearned = false;
    public bool canLearn = false;

    [Header("드래그")]
    private GameObject dragIcon;
    private Canvas canvas;

    // 선택 상태
    private bool isSelected = false;
    private static SkillTreeSlot currentSelected;

    // 선택 시 테두리 색상
    private static readonly Color selectedColor = new Color(1f, 0.8f, 0f);     // 금색
    private static readonly Color normalColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    private static readonly Color learnedColor = new Color(1f, 1f, 1f, 0.8f);

    private int _syncCounter = 0;
    private const int SYNC_INTERVAL = 30;

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        UpdateSlotUI();
    }

    void Update()
    {
        if (++_syncCounter < SYNC_INTERVAL) return;
        _syncCounter = 0;

        if (canvas != null && !canvas.gameObject.activeInHierarchy) return;
        if (learnedSkill == null || learnedSkill.skillData == null) return;
        if (SkillManager.Instance == null) return;

        SyncWithSkillManager();
        UpdateLearnability();
    }

    private void SyncWithSkillManager()
    {
        if (learnedSkill == null || learnedSkill.skillData == null) return;
        if (SkillManager.Instance == null) return;

        LearnedSkill latest = SkillManager.Instance.GetLearnedSkill(learnedSkill.skillData.skillID);
        if (latest != null && latest != learnedSkill)
            learnedSkill = latest;
    }

    // ══════════════════════════════════════════════════════
    // 슬롯 클릭 → 하단 상세 팝업 표시
    // ══════════════════════════════════════════════════════

    public void OnPointerClick(PointerEventData eventData)
    {
        SelectSlot();
    }

    public void SelectSlot()
    {
        // 이전 선택 해제
        if (currentSelected != null && currentSelected != this)
            currentSelected.Deselect();

        currentSelected = this;
        isSelected = true;

        // 테두리 강조
        if (slotFrame != null)
            slotFrame.color = selectedColor;

        // 상세 패널에 정보 전달
        if (SkillDetailPanel.Instance != null)
            SkillDetailPanel.Instance.ShowSkillDetail(this);
    }

    public void Deselect()
    {
        isSelected = false;
        if (slotFrame != null)
            slotFrame.color = isLearned ? learnedColor : normalColor;
    }

    // ══════════════════════════════════════════════════════
    // 슬롯 설정 및 UI 업데이트
    // ══════════════════════════════════════════════════════

    public void SetupSlot(SkillData skillData)
    {
        if (skillData == null) return;

        if (SkillManager.Instance != null)
        {
            LearnedSkill existing = SkillManager.Instance.GetLearnedSkill(skillData.skillID);
            if (existing != null)
                learnedSkill = existing;
            else
            {
                learnedSkill = new LearnedSkill(skillData);
                learnedSkill.currentLevel = 0;
            }
        }

        UpdateSlotUI();
    }

    public void UpdateSlotUI()
    {
        if (learnedSkill == null || learnedSkill.skillData == null) return;

        SkillData skill = learnedSkill.skillData;
        isLearned = learnedSkill.currentLevel > 0;

        // 아이콘 (미습득 시 어둡게)
        if (skillIconImage != null)
        {
            skillIconImage.sprite = skill.skillIcon;
            skillIconImage.color = isLearned ? Color.white : new Color(0.4f, 0.4f, 0.4f);
        }

        // 레벨 뱃지 (습득한 스킬만 표시)
        if (levelBadge != null)
            levelBadge.SetActive(isLearned);

        if (skillLevelText != null)
        {
            if (isLearned)
                skillLevelText.text = $"Lv.{learnedSkill.currentLevel}";
            else
                skillLevelText.text = "";
        }

        // 테두리 색상
        if (slotFrame != null && !isSelected)
            slotFrame.color = isLearned ? learnedColor : normalColor;
    }

    // ══════════════════════════════════════════════════════
    // 습득 가능 여부
    // ══════════════════════════════════════════════════════

    void UpdateLearnability()
    {
        if (learnedSkill == null || learnedSkill.skillData == null || SkillManager.Instance == null)
        {
            canLearn = false;
            return;
        }

        SkillData skill = learnedSkill.skillData;

        bool hasEnoughSP = SkillManager.Instance.availableSkillPoints >= skill.requiredSkillPoints;

        bool hasEnoughLevel = true;
        int myLevel = PlayerStats.Instance != null ? PlayerStats.Instance.level
                    : (SkillManager.Instance.playerStats != null ? SkillManager.Instance.playerStats.level : 0);
        if (myLevel > 0)
            hasEnoughLevel = myLevel >= skill.requiredLevel;

        bool hasPrerequisites = true;
        if (skill.prerequisiteSkills != null && skill.prerequisiteSkills.Length > 0)
        {
            foreach (SkillData prereq in skill.prerequisiteSkills)
            {
                LearnedSkill prereqSkill = SkillManager.Instance.GetLearnedSkill(prereq.skillID);
                if (prereqSkill == null || prereqSkill.currentLevel == 0)
                {
                    hasPrerequisites = false;
                    break;
                }
            }
        }

        canLearn = hasEnoughSP && hasEnoughLevel && hasPrerequisites;
    }

    // ══════════════════════════════════════════════════════
    // 습득/레벨업 (SkillDetailPanel에서 호출)
    // ══════════════════════════════════════════════════════

    public bool TryLearnOrLevelUp()
    {
        if (learnedSkill == null || learnedSkill.skillData == null || SkillManager.Instance == null)
            return false;

        if (SkillManager.Instance.LearnSkill(learnedSkill.skillData))
        {
            SoundManager.Instance?.PlaySkillLearn();
            LearnedSkill latest = SkillManager.Instance.GetLearnedSkill(learnedSkill.skillData.skillID);
            if (latest != null)
                learnedSkill = latest;

            UpdateSlotUI();
            return true;
        }
        return false;
    }

    // ══════════════════════════════════════════════════════
    // 드래그 앤 드롭
    // ══════════════════════════════════════════════════════

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isLearned) return;

        dragIcon = new GameObject("DragIcon");
        dragIcon.transform.SetParent(canvas.transform, false);
        dragIcon.transform.SetAsLastSibling();

        Image dragImage = dragIcon.AddComponent<Image>();
        dragImage.sprite = learnedSkill.skillData.skillIcon;
        dragImage.raycastTarget = false;

        RectTransform rectTransform = dragIcon.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(50, 50);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIcon != null)
            dragIcon.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIcon != null)
            Destroy(dragIcon);
    }
}
