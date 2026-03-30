using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
// CompanionSkillUpTab.cs
//
// 동료 상세 패널 — [스킬업] 탭
//
// ★ 레이아웃:
//   ┌──────────────────────────────────────┐
//   │  스킬1 아이콘 | 이름 | Lv.3 | [UP]  │
//   │  스킬2 아이콘 | 이름 | Lv.1 | [UP]  │
//   │  스킬3 아이콘 | 이름 | 잠김  | 🔒   │
//   │  (스크롤)                            │
//   ├──────────────────────────────────────┤
//   │  선택된 스킬 상세                     │
//   │  설명 텍스트 / 효과 / 비용            │
//   └──────────────────────────────────────┘
//
// ★ CompanionSkillData는 CompanionData SO에 추가 예정
// ═══════════════════════════════════════════════════════════════════

/// <summary>동료 스킬 1개 정의 (CompanionData에 List로 포함)</summary>
[System.Serializable]
public class CompanionSkillInfo
{
    [Header("기본 정보")]
    public string skillID;
    public string skillName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;
    public int maxLevel = 5;
    public int unlockCompanionLevel = 1;      // 이 레벨 이상이어야 해금
    public int baseCostGold = 1000;            // 스킬 레벨업 골드 비용
    public float baseValue = 10f;              // 기본 효과 수치
    public float valuePerLevel = 5f;           // 레벨당 효과 증가량

    [Header("전투 설정")]
    public CompanionSkillType skillType = CompanionSkillType.SingleTarget;
    [Tooltip("쿨타임 (초)")]
    public float cooldown = 8f;
    [Tooltip("기본 공격력 대비 배율 (1.0 = 100%)")]
    public float damageMultiplier = 2f;
    [Tooltip("범위 스킬 시 반경")]
    public float areaRadius = 2f;
    [Tooltip("스킬 사거리 (0이면 기본 공격 사거리 사용)")]
    public float skillRange = 0f;

    [Header("이펙트")]
    public GameObject skillEffect;             // 스킬 이펙트 프리팹 (없으면 기본 attackEffect 사용)

    /// <summary>레벨 반영 데미지 배율: damageMultiplier + (level-1) * 0.3</summary>
    public float GetDamageMultiplier(int level)
    {
        return damageMultiplier + (level - 1) * 0.3f;
    }

    /// <summary>레벨 반영 효과 수치</summary>
    public float GetValue(int level)
    {
        return baseValue + (level - 1) * valuePerLevel;
    }
}

public enum CompanionSkillType
{
    SingleTarget,   // 단일 대상 강공격
    AreaDamage,     // 범위 피해
    Heal,           // 자가 치유
    Buff            // 공격력 버프 (일정 시간)
}

public class CompanionSkillUpTab : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  Inspector 필드
    // ─────────────────────────────────────────────────────────────

    [Header("===== 스킬 목록 =====")]
    [SerializeField] private Transform skillListContent;
    [SerializeField] private GameObject skillSlotPrefab;

    [Header("===== 스킬 상세 =====")]
    [SerializeField] private Image skillDetailIcon;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI skillLevelText;
    [SerializeField] private TextMeshProUGUI skillDescText;
    [SerializeField] private TextMeshProUGUI skillEffectText;     // "효과: 50"
    [SerializeField] private Image skillArrowImage;               // → 화살표 이미지
    [SerializeField] private TextMeshProUGUI skillEffectNextText; // "55" (다음 값)
    [SerializeField] private TextMeshProUGUI skillCostText;       // "비용: 2,000 골드"
    [SerializeField] private Button skillUpButton;

    // ─────────────────────────────────────────────────────────────
    //  내부 변수
    // ─────────────────────────────────────────────────────────────

    private CompanionData _companion;
    private int _companionLevel = 1;
    private int _selectedSkillIndex = -1;
    private readonly List<GameObject> _spawnedSlots = new List<GameObject>();

    // 스킬 레벨은 CompanionEntry.skillLevels에 저장됨

    // ─────────────────────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (skillUpButton != null)
            skillUpButton.onClick.AddListener(OnSkillUpClicked);
    }

    // ─────────────────────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────────────────────

    /// <summary>탭 활성화 시 호출</summary>
    public void Refresh(CompanionData companion, int companionLevel)
    {
        _companion = companion;
        _companionLevel = companionLevel;
        _selectedSkillIndex = -1;

        BuildSkillList();
        ClearDetail();

        // 첫 번째 스킬 자동 선택
        if (_companion != null && _companion.skills != null && _companion.skills.Count > 0)
            SelectSkill(0);
    }

    // ─────────────────────────────────────────────────────────────
    //  스킬 목록 빌드
    // ─────────────────────────────────────────────────────────────

    private readonly List<CompanionSkillSlot> _skillSlots = new List<CompanionSkillSlot>();

    private void BuildSkillList()
    {
        foreach (var go in _spawnedSlots)
            if (go != null) Destroy(go);
        _spawnedSlots.Clear();
        _skillSlots.Clear();

        if (skillListContent == null || skillSlotPrefab == null) return;
        if (_companion == null || _companion.skills == null) return;

        for (int i = 0; i < _companion.skills.Count; i++)
        {
            var skill = _companion.skills[i];
            if (skill == null) continue;

            int idx = i;
            GameObject slotGO = Instantiate(skillSlotPrefab, skillListContent);
            slotGO.SetActive(true);
            _spawnedSlots.Add(slotGO);

            // CompanionSkillSlot 컴포넌트 (없으면 자동 추가)
            CompanionSkillSlot slot = slotGO.GetComponent<CompanionSkillSlot>();
            if (slot == null) slot = slotGO.AddComponent<CompanionSkillSlot>();
            slot.AutoBind();

            int skillLv = GetSkillLevel(skill.skillID);
            bool locked = _companionLevel < skill.unlockCompanionLevel;
            slot.Setup(skill, skillLv, locked);
            _skillSlots.Add(slot);

            // 클릭
            Button btn = slotGO.GetComponent<Button>();
            if (btn == null) btn = slotGO.AddComponent<Button>();
            btn.onClick.AddListener(() => SelectSkill(idx));
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  스킬 선택 / 상세 표시
    // ─────────────────────────────────────────────────────────────

    private void SelectSkill(int index)
    {
        if (_companion == null || _companion.skills == null) return;
        if (index < 0 || index >= _companion.skills.Count) return;

        _selectedSkillIndex = index;

        // 선택 강조 갱신
        for (int i = 0; i < _skillSlots.Count; i++)
            _skillSlots[i].SetSelected(i == index);

        var skill = _companion.skills[index];
        int skillLv = GetSkillLevel(skill.skillID);
        bool locked = _companionLevel < skill.unlockCompanionLevel;

        if (skillDetailIcon != null && skill.icon != null)
            skillDetailIcon.sprite = skill.icon;
        if (skillNameText != null)
            skillNameText.text = skill.skillName;
        if (skillLevelText != null)
            skillLevelText.text = locked ? $"🔒 Lv.{skill.unlockCompanionLevel} 해금" : $"Lv.{skillLv} / {skill.maxLevel}";
        if (skillDescText != null)
            skillDescText.text = skill.description;

        // 효과 미리보기
        float currentVal = skill.baseValue + skill.valuePerLevel * (skillLv - 1);
        float nextVal = skill.baseValue + skill.valuePerLevel * skillLv;
        if (skillEffectText != null)
        {
            if (skillLv >= skill.maxLevel)
            {
                skillEffectText.text = $"효과: {currentVal:N0} (MAX)";
                if (skillArrowImage != null) skillArrowImage.gameObject.SetActive(false);
                if (skillEffectNextText != null) skillEffectNextText.gameObject.SetActive(false);
            }
            else
            {
                skillEffectText.text = $"효과: {currentVal:N0}";
                if (skillArrowImage != null) skillArrowImage.gameObject.SetActive(true);
                if (skillEffectNextText != null)
                {
                    skillEffectNextText.gameObject.SetActive(true);
                    skillEffectNextText.text = $"<color=#00FF00>{nextVal:N0}</color>";
                }
            }
        }

        // 비용
        int cost = skill.baseCostGold * skillLv;
        if (skillCostText != null)
            skillCostText.text = locked ? "잠김" : $"비용: {cost:N0} 골드";

        // 버튼 상태
        if (skillUpButton != null)
            skillUpButton.interactable = !locked && skillLv < skill.maxLevel;

        SoundManager.Instance?.PlayButtonClick();
    }

    private void ClearDetail()
    {
        if (skillDetailIcon != null) skillDetailIcon.sprite = null;
        if (skillNameText != null) skillNameText.text = "";
        if (skillLevelText != null) skillLevelText.text = "";
        if (skillDescText != null) skillDescText.text = "스킬을 선택하세요";
        if (skillEffectText != null) skillEffectText.text = "";
        if (skillCostText != null) skillCostText.text = "";
        if (skillUpButton != null) skillUpButton.interactable = false;
    }

    // ─────────────────────────────────────────────────────────────
    //  스킬 레벨업
    // ─────────────────────────────────────────────────────────────

    private void OnSkillUpClicked()
    {
        if (_companion == null || _companion.skills == null) return;
        if (_selectedSkillIndex < 0 || _selectedSkillIndex >= _companion.skills.Count) return;

        var skill = _companion.skills[_selectedSkillIndex];
        int skillLv = GetSkillLevel(skill.skillID);
        if (skillLv >= skill.maxLevel) return;

        int cost = skill.baseCostGold * skillLv;
        if (!TrySpendGold(cost)) return;

        SetSkillLevel(skill.skillID, skillLv + 1);
        SaveLoadManager.Instance?.SaveGame();
        SelectSkill(_selectedSkillIndex);

        SoundManager.Instance?.PlayQuestReward();
        Debug.Log($"[CompanionSkillUp] {skill.skillName} → Lv.{skillLv + 1}");
    }

    /// <summary>골드 차감 — GameManager → GameDataBridge 폴백</summary>
    private bool TrySpendGold(long amount)
    {
        if (GameManager.Instance != null)
        {
            if (!GameManager.Instance.SpendGold(amount))
            {
                UIManager.Instance?.ShowMessage($"골드가 부족합니다! ({amount:N0} 필요)", Color.red);
                return false;
            }
            return true;
        }
        if (GameDataBridge.CurrentData != null)
        {
            if (GameDataBridge.CurrentData.playerGold < amount)
            {
                UIManager.Instance?.ShowMessage($"골드가 부족합니다! ({amount:N0} 필요)", Color.red);
                return false;
            }
            GameDataBridge.CurrentData.playerGold -= amount;
            return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────────
    //  스킬 레벨 관리 (CompanionEntry.skillLevels 연동)
    // ─────────────────────────────────────────────────────────────

    private CompanionInventoryManager.CompanionEntry FindEntry()
    {
        if (_companion == null) return null;
        var invMgr = CompanionInventoryManager.Instance;
        if (invMgr == null) return null;

        var list = invMgr.GetCompanionList();
        if (list == null) return null;

        foreach (var entry in list)
        {
            if (entry != null && entry.data != null && entry.data.companionID == _companion.companionID)
                return entry;
        }
        return null;
    }

    private int GetSkillLevel(string skillID)
    {
        var entry = FindEntry();
        if (entry?.skillLevels == null) return 1;

        foreach (var sl in entry.skillLevels)
        {
            if (sl != null && sl.skillID == skillID)
                return Mathf.Max(1, sl.level);
        }
        return 1;
    }

    private void SetSkillLevel(string skillID, int level)
    {
        var entry = FindEntry();
        if (entry == null) return;

        // 기존 배열에서 검색
        if (entry.skillLevels != null)
        {
            for (int i = 0; i < entry.skillLevels.Length; i++)
            {
                if (entry.skillLevels[i] != null && entry.skillLevels[i].skillID == skillID)
                {
                    entry.skillLevels[i].level = level;
                    return;
                }
            }
        }

        // 새 엔트리 추가
        var list = new List<CompanionSkillLevelEntry>();
        if (entry.skillLevels != null) list.AddRange(entry.skillLevels);
        list.Add(new CompanionSkillLevelEntry { skillID = skillID, level = level });
        entry.skillLevels = list.ToArray();
    }
}
