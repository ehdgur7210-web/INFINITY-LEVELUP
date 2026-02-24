using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 습득한 스킬 정보
[System.Serializable]
public class LearnedSkill
{
    public SkillData skillData;
    public int currentLevel;
    public float cooldownRemaining;

    public LearnedSkill(SkillData data)
    {
        skillData = data;
        currentLevel = 1;
        cooldownRemaining = 0f;
    }
}

/// <summary>
/// SkillManager — 스킬 시스템 관리
/// 
/// ✅ 수정 내역:
///   - [Bug2/3 수정] SwapEquipmentSkillOnHotbarAtIndex: oldSkill 제거 시
///     "모든 슬롯 순회 + 같은 skillID 전부 클리어" 방식 제거
///     → 이제 EquipmentSkillSystem이 저장한 정확한 슬롯 인덱스로만 클리어 (이 파일에선 해당 로직 삭제)
///   - [Bug5 수정] SaveSkills: 핫바 슬롯 배정(어떤 슬롯에 어떤 스킬)도 함께 저장
///   - [Bug5 수정] LoadSkills → LoadHotbarSlots: 핫바 배정 복원
///   - [Bug5 수정] RoutineRefreshEquipmentSkills: 장비 스킬 재배치 후 수동 배정 핫바도 복원
/// </summary>
public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance;

    public Canvas hotbarCanvas;

    [Header("스킬 포인트")]
    public int availableSkillPoints = 1000;
    public int usedSkillPoints = 500;

    [Header("습득 가능한 스킬")]
    public List<SkillData> availableSkills = new List<SkillData>();

    [Header(" 습득한 스킬 (자동 업데이트)")]
    public List<LearnedSkill> learnedSkills = new List<LearnedSkill>();

    [Header("스킬 UI")]
    public GameObject skillTreePanel;
    public Transform skillSlotParent;
    public GameObject skillSlotPrefab;

    [Header("핫바 UI")]
    public SkillHotbarSlot[] hotbarSlots;

    [Header("플레이어 참조")]
    public PlayerStats playerStats;

    [Header("테스트: 시작 시 자동으로 스킬 배우기")]
    public bool autoLearnAllSkillsOnStart = true;

    [Header("디버깅")]
    public bool debugMode = true;

    private PlayerController playerController;
    private Dictionary<int, LearnedSkill> skillDictionary;
    private bool isSkillTreeInitialized = false;

    // ✅ PlayerPrefs 키
    private const string KEY_LEARNED_SKILLS = "LearnedSkills";
    private const string KEY_HOTBAR_SLOTS = "HotbarSlotAssignments";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        Debug.Log("[SkillManager] ========== Awake ==========");
        FindUIReferences();
        InitializeSkillSystem();
    }

    private void FindUIReferences()
    {
        if (skillTreePanel != null && skillSlotParent != null)
        {
            Debug.Log("[SkillManager] 이미 UI가 연결되어 있어 검색을 생략합니다.");
            return;
        }

        if (skillTreePanel == null)
            skillTreePanel = GameObject.Find("SkillTreePanel");

        if (skillSlotParent == null && skillTreePanel != null)
        {
            SkillTreeSlot[] childSlots = skillTreePanel.GetComponentsInChildren<SkillTreeSlot>(true);
            if (childSlots.Length > 0)
            {
                skillSlotParent = childSlots[0].transform.parent;
                Debug.Log($"[SkillManager] 슬롯을 통해 Parent 발견: {skillSlotParent.name}");
            }
        }
    }

    void Start()
    {
        Debug.Log("========== SkillManager Start ==========");

        if (skillTreePanel != null)
        {
            skillTreePanel.SetActive(false);
            Debug.Log("[SkillManager] 스킬 트리 패널 비활성화");
        }

        if (playerStats != null)
            playerController = playerStats.GetComponent<PlayerController>();
        else
            playerController = FindObjectOfType<PlayerController>();

        Debug.Log($"[SkillManager] PlayerController 참조: {(playerController != null ? "OK" : "NULL")}");

        if (autoLearnAllSkillsOnStart)
            AutoLearnAllSkills();

        InitializeSkillTreeUI();
        LoadSkills();

        // ✅ [Bug5 수정] UI가 완전히 준비된 후(한 프레임 뒤):
        //   1. 장비 스킬을 EquipmentManager 기반으로 핫바에 재배치
        //   2. 수동 배정 핫바 슬롯도 PlayerPrefs에서 복원
        StartCoroutine(RoutineRefreshEquipmentSkills());

        Debug.Log("========================================");
    }

    // ✅ [Bug5 수정] 장비 스킬 재배치 → 이후 수동 배정 핫바 복원 순서 보장
    private IEnumerator RoutineRefreshEquipmentSkills()
    {
        yield return null; // UI 생성 대기 (1프레임)
        yield return null; // 장비 로드 완료 대기 (2프레임)

        // 1단계: EquipmentManager에서 현재 장착 장비 스킬을 핫바에 재배치
        if (EquipmentSkillSystem.Instance != null)
        {
            EquipmentSkillSystem.Instance.RefreshAllEquippedSkills();
            Debug.Log("[SkillManager] 장비 스킬 핫바 재배치 완료");
        }

        // 2단계: 수동으로 배정한 핫바 슬롯 복원 (장비 스킬 배치 이후 적용)
        // ※ 장비 스킬 슬롯과 겹치지 않는 슬롯만 복원됨
        LoadHotbarSlots();
    }

    // ───────────────────────────────────────────────────────────
    // 저장 / 로드
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// ✅ [Bug5 수정] 습득 스킬 + 핫바 슬롯 배정 모두 저장
    /// </summary>
    public void SaveSkills()
    {
        // --- 습득 스킬 저장 ---
        List<int> learnedIDs = new List<int>();
        foreach (var learned in learnedSkills)
        {
            if (learned.currentLevel > 0)
                learnedIDs.Add(learned.skillData.skillID);
        }
        PlayerPrefs.SetString(KEY_LEARNED_SKILLS, string.Join(",", learnedIDs));

        // --- 핫바 슬롯 배정 저장 ---
        // 형식: "슬롯0_skillID,슬롯1_skillID,...,슬롯N_skillID"
        // 비어있는 슬롯은 -1로 저장
        if (hotbarSlots != null && hotbarSlots.Length > 0)
        {
            List<string> slotData = new List<string>();
            foreach (var slot in hotbarSlots)
            {
                int id = (slot != null && slot.assignedSkill?.skillData != null)
                    ? slot.assignedSkill.skillData.skillID
                    : -1;
                slotData.Add(id.ToString());
            }
            PlayerPrefs.SetString(KEY_HOTBAR_SLOTS, string.Join(",", slotData));
        }

        PlayerPrefs.Save();
        Debug.Log("[SkillManager] 스킬 + 핫바 데이터 저장 완료");
    }

    public void LoadSkills()
    {
        if (!PlayerPrefs.HasKey(KEY_LEARNED_SKILLS)) return;

        string saveData = PlayerPrefs.GetString(KEY_LEARNED_SKILLS);
        if (string.IsNullOrEmpty(saveData)) return;

        string[] ids = saveData.Split(',');
        foreach (string idStr in ids)
        {
            if (int.TryParse(idStr, out int id))
            {
                SkillData data = availableSkills.Find(s => s.skillID == id);
                if (data != null && GetLearnedSkill(id) == null)
                {
                    LearnedSkill ls = new LearnedSkill(data) { currentLevel = 1 };
                    learnedSkills.Add(ls);
                    skillDictionary[id] = ls;
                }
            }
        }
        Debug.Log("[SkillManager] 스킬 데이터 로드 완료");
    }

    /// <summary>
    /// ✅ [Bug5 수정] 핫바 슬롯 배정 복원
    /// 장비 스킬이 이미 특정 슬롯을 점유하고 있다면 덮어쓰지 않음
    /// </summary>
    public void LoadHotbarSlots()
    {
        if (!PlayerPrefs.HasKey(KEY_HOTBAR_SLOTS)) return;
        if (hotbarSlots == null || hotbarSlots.Length == 0) return;

        string raw = PlayerPrefs.GetString(KEY_HOTBAR_SLOTS);
        if (string.IsNullOrEmpty(raw)) return;

        string[] parts = raw.Split(',');
        for (int i = 0; i < hotbarSlots.Length && i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out int skillID) || skillID < 0) continue;

            // 이미 장비 스킬이 배치된 슬롯은 건드리지 않음
            if (hotbarSlots[i] != null && hotbarSlots[i].assignedSkill != null) continue;

            LearnedSkill ls = GetLearnedSkill(skillID);
            if (ls != null && hotbarSlots[i] != null)
            {
                hotbarSlots[i].AssignSkill(ls);
                Debug.Log($"[SkillManager] 핫바 {i}번 슬롯 복원: {ls.skillData.skillName}");
            }
        }
    }

    // ───────────────────────────────────────────────────────────

    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.K))
            ToggleSkillTree();
        CheckHotbarInput();
#endif
        if (learnedSkills.Count > 0)
            UpdateCooldowns();
    }

    void InitializeSkillSystem()
    {
        skillDictionary = new Dictionary<int, LearnedSkill>();
        Debug.Log("[SkillManager] 스킬 시스템 초기화 완료");
    }

    private void AutoLearnAllSkills()
    {
        Debug.Log("[SkillManager] ========== 자동 스킬 배우기 시작 ==========");

        foreach (SkillData skill in availableSkills)
        {
            if (skill == null) continue;

            if (GetLearnedSkill(skill.skillID) != null)
            {
                Debug.Log($"[SkillManager] {skill.skillName}은 이미 배웠습니다.");
                continue;
            }

            LearnedSkill learned = new LearnedSkill(skill);
            learnedSkills.Add(learned);
            skillDictionary[skill.skillID] = learned;

            Debug.Log($"[SkillManager] {skill.skillName} 자동 습득! (Lv.{learned.currentLevel})");
        }

        Debug.Log($"[SkillManager] ========== 총 {learnedSkills.Count}개 스킬 습득 완료 ==========");
    }

    private void InitializeSkillTreeUI()
    {
        Debug.Log("[SkillManager] ========== InitializeSkillTreeUI 시작 ==========");

        if (isSkillTreeInitialized)
        {
            Debug.Log("[SkillManager] 스킬 트리 UI 이미 초기화됨");
            return;
        }

        if (skillSlotParent == null)
        {
            Debug.LogError("[SkillManager] skillSlotParent가 NULL! UI 초기화 불가!");
            return;
        }

        SkillTreeSlot[] existingSlots = skillSlotParent.GetComponentsInChildren<SkillTreeSlot>(true);

        int mappedCount = 0;
        for (int i = 0; i < existingSlots.Length && i < availableSkills.Count; i++)
        {
            if (availableSkills[i] == null) continue;

            SkillData skillData = availableSkills[i];
            SkillTreeSlot slot = existingSlots[i];

            LearnedSkill actualLearned = GetLearnedSkill(skillData.skillID);

            if (actualLearned != null)
            {
                slot.learnedSkill = actualLearned;
            }
            else
            {
                LearnedSkill tempLearned = new LearnedSkill(skillData);
                tempLearned.currentLevel = 0;
                slot.learnedSkill = tempLearned;
            }

            slot.SetupSlot(skillData);
            mappedCount++;
        }

        isSkillTreeInitialized = true;
        Debug.Log($"[SkillManager] ========== 스킬 트리 UI 초기화 완료: {mappedCount}개 슬롯 매핑 ==========");
    }

    public void ToggleSkillTree()
    {
        if (skillTreePanel == null)
        {
            Debug.LogError("[SkillManager] skillTreePanel이 NULL!");
            return;
        }

        bool wasActive = skillTreePanel.activeSelf;
        skillTreePanel.SetActive(!wasActive);

        Debug.Log($"[SkillManager] 스킬 트리 {(wasActive ? "닫기" : "열기")}");

        if (HotbarManager.Instance != null)
        {
            if (!wasActive)
                HotbarManager.Instance.OnSkillTreeOpened();
            else
                HotbarManager.Instance.OnSkillTreeClosed();
        }

        if (hotbarCanvas != null)
        {
            hotbarCanvas.sortingOrder = wasActive ? 0 : 2;
            Debug.Log($"[SkillManager] 핫바 Sort Order → {hotbarCanvas.sortingOrder}");
        }

        if (!wasActive)
            UpdateSkillTreeUI();
    }

    public bool LearnSkill(SkillData skill)
    {
        if (skill == null)
        {
            Debug.LogError("[SkillManager] 스킬 데이터가 null입니다!");
            return false;
        }

        Debug.Log($"[SkillManager] ========== LearnSkill 호출: {skill.skillName} ==========");

        if (availableSkillPoints < skill.requiredSkillPoints)
        {
            Debug.LogWarning($"[SkillManager] 스킬 포인트 부족! (필요: {skill.requiredSkillPoints}, 현재: {availableSkillPoints})");
            UIManager.Instance?.ShowMessage("스킬 포인트 부족!", Color.red);
            return false;
        }

        int myLevel = PlayerStats.Instance != null ? PlayerStats.Instance.level
                    : (playerStats != null ? playerStats.level : 0);
        if (myLevel > 0 && myLevel < skill.requiredLevel)
        {
            Debug.LogWarning($"[SkillManager] 레벨 부족! (필요: {skill.requiredLevel}, 현재: {myLevel})");
            UIManager.Instance?.ShowMessage($"레벨 {skill.requiredLevel} 필요!", Color.red);
            return false;
        }

        if (skill.prerequisiteSkills != null && skill.prerequisiteSkills.Length > 0)
        {
            foreach (SkillData prereq in skill.prerequisiteSkills)
            {
                LearnedSkill prereqSkill = GetLearnedSkill(prereq.skillID);
                if (prereqSkill == null || prereqSkill.currentLevel == 0)
                {
                    Debug.LogWarning($"[SkillManager] 선행 스킬 필요: {prereq.skillName}");
                    UIManager.Instance?.ShowMessage($"{prereq.skillName} 선행 스킬 필요!", Color.red);
                    return false;
                }
            }
        }

        LearnedSkill learned = GetLearnedSkill(skill.skillID);

        if (learned == null)
        {
            Debug.Log($"[SkillManager] 신규 스킬 습득: {skill.skillName}");

            learned = new LearnedSkill(skill);
            learned.currentLevel = 1;
            learnedSkills.Add(learned);
            skillDictionary[skill.skillID] = learned;

            if (skill.skillType == SkillType.Active)
                AutoAssignToHotbar(learned);

            Debug.Log($"[SkillManager]  {skill.skillName} 습득 완료! (Lv.{learned.currentLevel})");
            UIManager.Instance?.ShowMessage($" {skill.skillName} 습득!", Color.green);
        }
        else
        {
            if (learned.currentLevel >= skill.maxLevel)
            {
                Debug.LogWarning($"[SkillManager] 이미 최대 레벨입니다! ({learned.currentLevel}/{skill.maxLevel})");
                UIManager.Instance?.ShowMessage($"{skill.skillName} 최대 레벨!", Color.yellow);
                return false;
            }

            int oldLevel = learned.currentLevel;
            learned.currentLevel++;
            Debug.Log($"[SkillManager]  {skill.skillName} 레벨업! (Lv.{oldLevel} → Lv.{learned.currentLevel})");
            UIManager.Instance?.ShowMessage($" {skill.skillName} Lv.{learned.currentLevel}!", Color.cyan);
        }

        availableSkillPoints -= skill.requiredSkillPoints;
        usedSkillPoints += skill.requiredSkillPoints;

        Debug.Log($"[SkillManager] 스킬 포인트 차감: -{skill.requiredSkillPoints} (남은 포인트: {availableSkillPoints})");
        UpdateSkillTreeUI();
        Debug.Log($"[SkillManager] ========== LearnSkill 완료 ==========");
        SaveSkills();
        return true;
    }

    /// <summary>장비로부터 스킬 습득 (등급별 스킬 시스템용)</summary>
    public void LearnSkillFromEquipment(SkillData skill)
    {
        if (skill == null)
        {
            Debug.LogWarning("[SkillManager] LearnSkillFromEquipment: skill이 null!");
            return;
        }

        LearnedSkill existing = GetLearnedSkill(skill.skillID);
        if (existing != null)
        {
            // 이미 습득된 경우에도 레벨이 0이면 강제로 활성화
            if (existing.currentLevel <= 0)
                existing.currentLevel = 1;
            Debug.Log($"[SkillManager] {skill.skillName}은 이미 습득되어 있음");
            return;
        }

        LearnedSkill learned = new LearnedSkill(skill);
        learned.currentLevel = 1;
        learnedSkills.Add(learned);
        skillDictionary[skill.skillID] = learned;

        Debug.Log($"[SkillManager]  장비로부터 스킬 습득: {skill.skillName} (Lv.{learned.currentLevel})");

        if (skillTreePanel != null && skillTreePanel.activeSelf)
            UpdateSkillTreeUI();
    }

    /// <summary>장비 해제 시 스킬 제거</summary>
    public void RemoveSkillFromEquipment(SkillData skill)
    {
        if (skill == null)
        {
            Debug.LogWarning("[SkillManager] RemoveSkillFromEquipment: skill이 null!");
            return;
        }

        // ✅ [Bug2/3 수정] 핫바에서의 클리어는 EquipmentSkillSystem이 직접 인덱스로 처리함
        // 여기서는 learnedSkills에서 제거하지 않음
        // → 같은 스킬이 여러 장비에 쓰일 수 있으므로 learned 목록 유지
        // → 핫바 슬롯 클리어는 EquipmentSkillSystem.DeactivateSkill에서 처리됨
        Debug.Log($"[SkillManager] 장비 해제 스킬 알림: {skill.skillName} (learned 목록 유지)");

        if (skillTreePanel != null && skillTreePanel.activeSelf)
            UpdateSkillTreeUI();
    }

    private void AutoAssignToHotbar(LearnedSkill skill)
    {
        if (hotbarSlots == null) return;

        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (hotbarSlots[i] != null && hotbarSlots[i].assignedSkill == null)
            {
                hotbarSlots[i].AssignSkill(skill);
                Debug.Log($"[SkillManager]{skill.skillData.skillName}을 핫바 {i + 1}번에 자동 배정!");
                return;
            }
        }

        Debug.Log($"[SkillManager] 핫바가 꽉 찼습니다.");
    }

    // ───────────────────────────────────────────────────────────────
    /// <summary>
    /// ★ 장비 착용 시 정해진 슬롯 위치에 스킬 배치
    /// EquipmentSkillSystem에서 계산한 targetSlotIndex에 정확히 배치
    /// 
    /// ✅ [Bug2/3 수정]
    ///   - oldSkill 제거 로직 제거 (EquipmentSkillSystem이 직접 인덱스로 처리)
    ///   - skillID 기반 전체 순회 방식 제거
    /// </summary>
    public void SwapEquipmentSkillOnHotbarAtIndex(SkillData oldSkill, SkillData newSkill, int targetSlotIndex)
    {
        if (newSkill == null) return;
        if (hotbarSlots == null || hotbarSlots.Length == 0)
        {
            Debug.LogWarning("[SkillManager] 핫바 슬롯이 준비되지 않았습니다. 잠시 후 재시도합니다.");
            return;
        }

        // ─── 1. 새 스킬 강제 활성화 (선행 스킬/레벨/포인트 전부 무시) ───
        LearnedSkill newLearned = GetLearnedSkill(newSkill.skillID);
        if (newLearned == null)
        {
            newLearned = new LearnedSkill(newSkill);
            newLearned.currentLevel = 1;
            learnedSkills.Add(newLearned);
            skillDictionary[newSkill.skillID] = newLearned;
        }
        else if (newLearned.currentLevel <= 0)
        {
            newLearned.currentLevel = 1;
        }

        // ✅ [Bug2/3 수정] oldSkill 제거는 EquipmentSkillSystem.DeactivateSkill에서
        //    저장된 핫바 인덱스를 사용해 직접 처리하므로 여기선 생략
        //    (이전 코드: skillID 검색으로 모든 슬롯 순회 → 다른 장비 슬롯도 지우던 버그)

        // ─── 2. 정확한 인덱스에 강제 배치 ───
        if (targetSlotIndex >= 0 && targetSlotIndex < hotbarSlots.Length)
        {
            if (hotbarSlots[targetSlotIndex] != null)
            {
                hotbarSlots[targetSlotIndex].AssignSkill(newLearned);
                Debug.Log($"[SkillManager] {targetSlotIndex}번 슬롯에 '{newSkill.skillName}' 등록 완료!");
            }
        }
        else
        {
            Debug.LogWarning($"[SkillManager] targetSlotIndex({targetSlotIndex})가 범위 밖입니다. 핫바 슬롯이 30개 이상인지 확인하세요.");
        }

        SaveSkills();
        UpdateSkillTreeUI();
    }

    /// <summary>기존 호환성 유지용 (사용하지 않는 것 권장)</summary>
    public void SwapEquipmentSkillOnHotbar(SkillData oldSkill, SkillData newSkill)
    {
        SwapEquipmentSkillOnHotbarAtIndex(oldSkill, newSkill, -1);
    }
    // ───────────────────────────────────────────────────────────────

    private void RemoveFromHotbar(SkillData skillData)
    {
        if (hotbarSlots == null) return;

        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (hotbarSlots[i] != null &&
                hotbarSlots[i].assignedSkill != null &&
                hotbarSlots[i].assignedSkill.skillData.skillID == skillData.skillID)
            {
                hotbarSlots[i].ClearSlot();
                Debug.Log($"[SkillManager] 핫바 {i + 1}번에서 {skillData.skillName} 제거");
                return; // ★ 첫 번째 슬롯만 제거하고 종료
            }
        }
    }

    public bool UseSkill(int skillID)
    {
        LearnedSkill skill = GetLearnedSkill(skillID);
        if (skill == null) return false;

        if (skill.cooldownRemaining > 0)
        {
            Debug.Log($"쿨다운 중! ({skill.cooldownRemaining:F1}초 남음)");
            return false;
        }

        ExecuteSkill(skill);
        skill.cooldownRemaining = skill.skillData.cooldown;
        return true;
    }

    void ExecuteSkill(LearnedSkill skill)
    {
        SkillData data = skill.skillData;
        float value = data.GetValueAtLevel(skill.currentLevel);

        switch (data.effectType)
        {
            case SkillEffectType.Damage:
                ExecuteAttackSkill(data, value);
                break;

            case SkillEffectType.Heal:
                playerStats?.Heal((int)value);
                break;

            case SkillEffectType.AttackBuff:
            case SkillEffectType.DefenseBuff:
            case SkillEffectType.SpeedBuff:
                ApplyBuff(data, value);
                break;
        }

        if (data.effectPrefab != null && playerStats != null)
        {
            Vector3 spawnPos = playerStats.transform.position + Vector3.up;
            GameObject effect = Instantiate(data.effectPrefab, spawnPos, Quaternion.identity);
            Destroy(effect, 2f);
        }

        if (data.skillSound != null && playerStats != null)
            AudioSource.PlayClipAtPoint(data.skillSound, playerStats.transform.position);
    }

    private void ExecuteAttackSkill(SkillData data, float damageValue)
    {
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();

        if (playerController == null)
        {
            Debug.LogError("[SkillManager] PlayerController를 찾을 수 없습니다!");
            return;
        }

        switch (data.attackStyle)
        {
            case AttackStyleType.Melee:
                playerController.PerformSkillMelee(data, damageValue);
                break;

            case AttackStyleType.Ranged:
                playerController.PerformSkillRanged(data, damageValue);
                break;

            case AttackStyleType.Magic:
                playerController.PerformSkillMagic(data, damageValue);
                break;
        }
    }

    void ApplyBuff(SkillData skill, float value)
    {
        Debug.Log($"버프 적용: {skill.effectType} +{value} ({skill.duration}초)");
    }

    void UpdateCooldowns()
    {
        foreach (LearnedSkill skill in learnedSkills)
        {
            if (skill.cooldownRemaining > 0)
            {
                skill.cooldownRemaining -= Time.deltaTime;
                if (skill.cooldownRemaining < 0)
                    skill.cooldownRemaining = 0;
            }
        }
    }

    void CheckHotbarInput()
    {
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (hotbarSlots != null && i < hotbarSlots.Length && hotbarSlots[i] != null)
                    hotbarSlots[i].UseSkill();
            }
        }
    }

    public LearnedSkill GetLearnedSkill(int skillID)
    {
        if (skillDictionary != null && skillDictionary.TryGetValue(skillID, out LearnedSkill skill))
            return skill;
        return learnedSkills.FirstOrDefault(s => s.skillData.skillID == skillID);
    }

    void UpdateSkillTreeUI()
    {
        if (skillSlotParent == null)
        {
            Debug.LogError("[SkillManager] skillSlotParent가 NULL!");
            return;
        }

        SkillTreeSlot[] slots = skillSlotParent.GetComponentsInChildren<SkillTreeSlot>(true);

        int updatedCount = 0;
        foreach (SkillTreeSlot slot in slots)
        {
            if (slot.learnedSkill != null && slot.learnedSkill.skillData != null)
            {
                int skillID = slot.learnedSkill.skillData.skillID;
                LearnedSkill actualReference = GetLearnedSkill(skillID);

                if (actualReference != null)
                {
                    slot.learnedSkill = actualReference;
                    updatedCount++;
                }
                else
                {
                    if (slot.learnedSkill.currentLevel != 0)
                        slot.learnedSkill.currentLevel = 0;
                }
            }
        }

        Debug.Log($"[SkillManager] UpdateSkillTreeUI 완료: {updatedCount}개 슬롯 동기화됨");
    }

    public void ResetSkills()
    {
        availableSkillPoints += usedSkillPoints;
        usedSkillPoints = 0;
        learnedSkills.Clear();
        skillDictionary.Clear();

        if (hotbarSlots != null)
            foreach (var slot in hotbarSlots)
                if (slot != null)
                    slot.ClearSlot();

        Debug.Log("스킬 초기화 완료!");
        UIManager.Instance?.ShowMessage("스킬 초기화!");
        UpdateSkillTreeUI();
    }
}