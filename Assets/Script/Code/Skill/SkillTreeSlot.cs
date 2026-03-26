using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 스킬 트리 슬롯 UI (참조 동기화 수정 버전)
/// ⭐ 수정: learnedSkill이 항상 SkillManager의 실제 참조를 가리키도록 수정
/// </summary>
public class SkillTreeSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("스킬 정보")]
    public LearnedSkill learnedSkill;       // 습득한 스킬 (참조만 유지)

    [Header("UI 참조")]
    public Image skillIconImage;            // 스킬 아이콘
    public TextMeshProUGUI skillNameText;   // 스킬 이름
    public TextMeshProUGUI skillLevelText;  // 스킬 레벨
    public TextMeshProUGUI costText;        // 필요 SP
    public Button learnButton;              // 습득 버튼
    public Button levelUpButton;            // 레벨업 버튼

    [Header("상태")]
    public bool isLearned = false;          // 습득 여부
    public bool canLearn = false;           // 습득 가능 여부

    [Header("드래그")]
    private GameObject dragIcon;            // 드래그 중 아이콘
    private Canvas canvas;                  // 캔버스 참조

    private int _syncCounter = 0;
    private const int SYNC_INTERVAL = 30;   // 30프레임마다 동기화

    void Start()
    {
        // 버튼 이벤트
        if (learnButton != null)
        {
            learnButton.onClick.AddListener(OnLearnButtonClicked);
        }

        if (levelUpButton != null)
        {
            levelUpButton.onClick.AddListener(OnLevelUpButtonClicked);
        }

        // 캔버스 찾기
        canvas = GetComponentInParent<Canvas>();

        UpdateSlotUI();
    }

    void Update()
    {
        // 30프레임마다만 동기화 (매 프레임 불필요)
        if (++_syncCounter < SYNC_INTERVAL) return;
        _syncCounter = 0;

        // Canvas가 비활성화 상태면 Update 실행 안 함
        if (canvas != null && !canvas.gameObject.activeInHierarchy)
            return;

        // learnedSkill이 null이면 Update 실행 안 함
        if (learnedSkill == null || learnedSkill.skillData == null)
            return;

        // SkillManager가 null이면 Update 실행 안 함
        if (SkillManager.Instance == null)
            return;

        // ⭐ 30프레임 간격으로 SkillManager의 최신 참조 가져오기
        SyncWithSkillManager();

        // 스킬 가능 여부 업데이트
        UpdateLearnability();
    }

    /// <summary>
    /// ⭐ SkillManager와 동기화 (매 프레임)
    /// </summary>
    private void SyncWithSkillManager()
    {
        if (learnedSkill == null || learnedSkill.skillData == null) return;
        if (SkillManager.Instance == null) return;

        // ⭐ SkillManager에서 최신 참조 가져오기
        LearnedSkill latest = SkillManager.Instance.GetLearnedSkill(learnedSkill.skillData.skillID);

        if (latest != null && latest != learnedSkill)
        {
            // ⭐ 참조가 다르면 교체
            learnedSkill = latest;
        }
    }

    /// <summary>
    /// 슬롯 설정
    /// </summary>
    public void SetupSlot(SkillData skillData)
    {
        if (skillData == null) return;

        // ⭐ SkillManager에서 실제 참조 가져오기
        if (SkillManager.Instance != null)
        {
            LearnedSkill existing = SkillManager.Instance.GetLearnedSkill(skillData.skillID);

            if (existing != null)
            {
                // ⭐ 이미 배운 스킬이면 실제 참조 사용
                learnedSkill = existing;
            }
            else
            {
                // ⭐ 아직 안 배운 스킬이면 임시 객체 생성
                learnedSkill = new LearnedSkill(skillData);
                learnedSkill.currentLevel = 0; // 미습득 상태
            }
        }

        UpdateSlotUI();
    }

    /// <summary>
    /// 슬롯 UI 업데이트
    /// </summary>
    public void UpdateSlotUI()
    {
        if (learnedSkill == null || learnedSkill.skillData == null) return;

        SkillData skill = learnedSkill.skillData;
        isLearned = learnedSkill.currentLevel > 0;

        // 스킬 아이콘
        if (skillIconImage != null)
        {
            skillIconImage.sprite = skill.skillIcon;
            skillIconImage.color = isLearned ? Color.white : new Color(0.5f, 0.5f, 0.5f);
        }

        // 스킬 이름
        if (skillNameText != null)
        {
            skillNameText.text = skill.skillName;
        }

        // 스킬 레벨
        if (skillLevelText != null)
        {
            if (isLearned)
            {
                skillLevelText.text = $"{learnedSkill.currentLevel}/{skill.maxLevel}";
            }
            else
            {
                skillLevelText.text = "";
            }
        }

        // 비용
        if (costText != null)
        {
            costText.text = $"SP: {skill.requiredSkillPoints}";
        }

        // 버튼 표시
        UpdateButtonState();
    }

    /// <summary>
    /// ⭐ 버튼 상태 업데이트
    /// </summary>
    private void UpdateButtonState()
    {
        if (isLearned)
        {
            // 이미 습득함 - 레벨업 버튼만
            if (learnButton != null)
            {
                learnButton.gameObject.SetActive(false);
            }

            if (levelUpButton != null)
            {
                bool canLevelUp = learnedSkill.currentLevel < learnedSkill.skillData.maxLevel;
                levelUpButton.gameObject.SetActive(canLevelUp);
                levelUpButton.interactable = canLearn && canLevelUp;
            }
        }
        else
        {
            // 미습득 - 습득 버튼만
            if (learnButton != null)
            {
                learnButton.gameObject.SetActive(true);
                learnButton.interactable = canLearn;
            }

            if (levelUpButton != null)
            {
                levelUpButton.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 습득 가능 여부 업데이트
    /// </summary>
    void UpdateLearnability()
    {
        if (learnedSkill == null || learnedSkill.skillData == null)
        {
            canLearn = false;
            return;
        }

        if (SkillManager.Instance == null)
        {
            canLearn = false;
            return;
        }

        SkillData skill = learnedSkill.skillData;

        // 스킬 포인트 확인
        bool hasEnoughSP = SkillManager.Instance.availableSkillPoints >= skill.requiredSkillPoints;

        // ✅ 레벨 확인 - level=0이면 아직 로드 전이므로 통과
        bool hasEnoughLevel = true;
        int myLevel = PlayerStats.Instance != null ? PlayerStats.Instance.level
                    : (SkillManager.Instance.playerStats != null ? SkillManager.Instance.playerStats.level : 0);
        // level > 0일 때만 체크 (0이면 초기화 전 → 통과)
        if (myLevel > 0)
            hasEnoughLevel = myLevel >= skill.requiredLevel;

        // 선행 스킬 확인
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

        // ⭐ 버튼 상태도 같이 업데이트
        UpdateButtonState();
    }

    /// <summary>
    /// 습득 버튼 클릭
    /// </summary>
    void OnLearnButtonClicked()
    {
        if (learnedSkill == null || learnedSkill.skillData == null)
        {
            Debug.LogError("[SkillTreeSlot] learnedSkill이 null입니다!");
            return;
        }

        if (SkillManager.Instance == null)
        {
            Debug.LogError("[SkillTreeSlot] SkillManager.Instance가 null입니다!");
            return;
        }

        Debug.Log($"[SkillTreeSlot] 습득 버튼 클릭: {learnedSkill.skillData.skillName}");

        if (SkillManager.Instance.LearnSkill(learnedSkill.skillData))
        {
            // ★ 스킬 습득 효과음
            SoundManager.Instance?.PlaySkillLearn();
            // ⭐ 성공 시 최신 참조 가져오기
            LearnedSkill latest = SkillManager.Instance.GetLearnedSkill(learnedSkill.skillData.skillID);
            if (latest != null)
            {
                learnedSkill = latest;
            }

            UpdateSlotUI();
        }
    }

    /// <summary>
    /// 레벨업 버튼 클릭
    /// </summary>
    void OnLevelUpButtonClicked()
    {
        if (learnedSkill == null || learnedSkill.skillData == null)
        {
            Debug.LogError("[SkillTreeSlot] learnedSkill이 null입니다!");
            return;
        }

        if (SkillManager.Instance == null)
        {
            Debug.LogError("[SkillTreeSlot] SkillManager.Instance가 null입니다!");
            return;
        }

        Debug.Log($"[SkillTreeSlot] 레벨업 버튼 클릭: {learnedSkill.skillData.skillName} (현재 Lv.{learnedSkill.currentLevel})");

        if (SkillManager.Instance.LearnSkill(learnedSkill.skillData))
        {
            // ★ 스킬 레벨업 효과음
            SoundManager.Instance?.PlaySkillLearn();
            // ⭐ 성공 시 최신 참조 가져오기
            LearnedSkill latest = SkillManager.Instance.GetLearnedSkill(learnedSkill.skillData.skillID);
            if (latest != null)
            {
                learnedSkill = latest;
            }

            Debug.Log($"[SkillTreeSlot] 레벨업 성공! 새 레벨: {learnedSkill.currentLevel}");
            UpdateSlotUI();
        }
        else
        {
            Debug.LogWarning($"[SkillTreeSlot] 레벨업 실패!");
        }
    }

    // === 드래그 앤 드롭 기능 ===

    /// <summary>
    /// 드래그 시작
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isLearned) return; // 미습득 스킬은 드래그 불가

        // 드래그 아이콘 생성
        dragIcon = new GameObject("DragIcon");
        dragIcon.transform.SetParent(canvas.transform, false);
        dragIcon.transform.SetAsLastSibling();

        Image dragImage = dragIcon.AddComponent<Image>();
        dragImage.sprite = learnedSkill.skillData.skillIcon;
        dragImage.raycastTarget = false;

        RectTransform rectTransform = dragIcon.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(50, 50);
    }

    /// <summary>
    /// 드래그 중
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (dragIcon != null)
        {
            dragIcon.transform.position = eventData.position;
        }
    }

    /// <summary>
    /// 드래그 종료
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIcon != null)
        {
            Destroy(dragIcon);
        }
    }
}