using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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
        {
            skillTreePanel = GameObject.Find("SkillTreePanel");
        }

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
        {
            AutoLearnAllSkills();
        }

        InitializeSkillTreeUI();

        Debug.Log("========================================");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("[SkillManager] K 키 입력 감지!");
            ToggleSkillTree();
        }

        UpdateCooldowns();
        CheckHotbarInput();
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

            if (skill.skillType == SkillType.Active)
            {
                AutoAssignToHotbar(learned);
            }
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
        Debug.Log($"[SkillManager] 기존 슬롯 개수: {existingSlots.Length}");
        Debug.Log($"[SkillManager] availableSkills 개수: {availableSkills.Count}");
        Debug.Log($"[SkillManager] learnedSkills 개수: {learnedSkills.Count}");

        int mappedCount = 0;
        for (int i = 0; i < existingSlots.Length && i < availableSkills.Count; i++)
        {
            if (availableSkills[i] == null)
            {
                Debug.LogWarning($"[SkillManager] availableSkills[{i}]가 NULL!");
                continue;
            }

            SkillData skillData = availableSkills[i];
            SkillTreeSlot slot = existingSlots[i];

            LearnedSkill actualLearned = GetLearnedSkill(skillData.skillID);

            if (actualLearned != null)
            {
                slot.learnedSkill = actualLearned;
                Debug.Log($"[SkillManager] 슬롯 매핑 (이미 배움): {slot.name} → {skillData.skillName} (Lv.{actualLearned.currentLevel})");
            }
            else
            {
                LearnedSkill tempLearned = new LearnedSkill(skillData);
                tempLearned.currentLevel = 0;
                slot.learnedSkill = tempLearned;
                Debug.Log($"[SkillManager] 슬롯 매핑 (미습득): {slot.name} → {skillData.skillName} (Lv.0)");
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

        // ⭐ 핫바 위치 조정
        if (HotbarManager.Instance != null)
        {
            if (!wasActive)
            {
                // 스킬 트리 열림
                HotbarManager.Instance.OnSkillTreeOpened();
            }
            else
            {
                // 스킬 트리 닫힘
                HotbarManager.Instance.OnSkillTreeClosed();
            }
        }

        if (hotbarCanvas != null)
        {
            hotbarCanvas.sortingOrder = wasActive ? 0 : 2;
            Debug.Log($"[SkillManager] 핫바 Sort Order → {hotbarCanvas.sortingOrder}");
        }

        if (!wasActive)
        {
            UpdateSkillTreeUI();
        }

        //Time.timeScale = wasActive ? 1f : 0f;
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

        // ✅ 레벨 확인 - level=0이면 아직 초기화 전이므로 체크 스킵
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
            {
                AutoAssignToHotbar(learned);
            }

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
        return true;
    }

    /// <summary>
    /// ⭐ 장비로부터 스킬 습득 (등급별 스킬 시스템용)
    /// </summary>
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
            Debug.Log($"[SkillManager] {skill.skillName}은 이미 습득되어 있음 (장비 스킬 중복 방지)");
            return;
        }

        LearnedSkill learned = new LearnedSkill(skill);
        learned.currentLevel = 1; // 장비 스킬은 레벨 1로 시작

        learnedSkills.Add(learned);
        skillDictionary[skill.skillID] = learned;

        Debug.Log($"[SkillManager]  장비로부터 스킬 습득: {skill.skillName} (Lv.{learned.currentLevel})");

        if (skill.skillType == SkillType.Active)
        {
            AutoAssignToHotbar(learned);
        }

        // UI 업데이트
        if (skillTreePanel != null && skillTreePanel.activeSelf)
        {
            UpdateSkillTreeUI();
        }
    }

    /// <summary>
    /// ⭐ 장비 해제 시 스킬 제거
    /// </summary>
    public void RemoveSkillFromEquipment(SkillData skill)
    {
        if (skill == null)
        {
            Debug.LogWarning("[SkillManager] RemoveSkillFromEquipment: skill이 null!");
            return;
        }

        LearnedSkill toRemove = GetLearnedSkill(skill.skillID);
        if (toRemove == null)
        {
            Debug.LogWarning($"[SkillManager] {skill.skillName}을 찾을 수 없음 (이미 제거됨?)");
            return;
        }

        learnedSkills.Remove(toRemove);
        skillDictionary.Remove(skill.skillID);
        RemoveFromHotbar(skill);

        Debug.Log($"[SkillManager]  장비 해제로 스킬 제거: {skill.skillName}");

        // UI 업데이트
        if (skillTreePanel != null && skillTreePanel.activeSelf)
        {
            UpdateSkillTreeUI();
        }
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
                return;
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
                {
                    hotbarSlots[i].UseSkill();
                }
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
        Debug.Log($"[SkillManager] ========== UpdateSkillTreeUI: {slots.Length}개 슬롯 업데이트 시작 ==========");

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

                    Debug.Log($"[SkillManager]{actualReference.skillData.skillName}: Lv.{actualReference.currentLevel}/{actualReference.skillData.maxLevel}");
                }
                else
                {
                    if (slot.learnedSkill.currentLevel != 0)
                    {
                        slot.learnedSkill.currentLevel = 0;
                        Debug.Log($"[SkillManager]   - {slot.learnedSkill.skillData.skillName}: 미습득");
                    }
                }
            }
        }

        Debug.Log($"[SkillManager] ========== UpdateSkillTreeUI 완료: {updatedCount}개 슬롯 동기화됨 ==========");
    }

    public void ResetSkills()
    {
        availableSkillPoints += usedSkillPoints;
        usedSkillPoints = 0;
        learnedSkills.Clear();
        skillDictionary.Clear();

        if (hotbarSlots != null)
        {
            foreach (var slot in hotbarSlots)
            {
                if (slot != null)
                    slot.ClearSlot();
            }
        }

        Debug.Log("스킬 초기화 완료!");
        UIManager.Instance?.ShowMessage("스킬 초기화!");

        UpdateSkillTreeUI();
    }
}