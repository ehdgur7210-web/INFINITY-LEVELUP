using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ══════════════════════════════════════════════════════════════════
///  전투력 시스템 (CombatPowerManager)
/// ══════════════════════════════════════════════════════════════════
///
///  ▶ 전투력에 기여하는 요소:
///    1. 레벨         : 레벨 × levelPowerPerLevel
///    2. 기본 스탯    : 공격력, 방어력, 체력, 크리티컬
///    3. 장비 착용    : 장비 스탯 + 희귀도 보너스
///    4. 장비 강화    : 강화 레벨 × enhancePowerPerLevel
///    5. 스킬 습득    : 습득한 스킬 레벨 합산
///    6. 스킬 핫바    : 핫바 장착 스킬 추가 보너스
///
///  ▶ 외부 연동 (한 줄씩 추가):
///    - PlayerStats.OnLevelChanged     → 자동 구독 (레벨업 시 갱신)
///    - EquipmentManager.RecalculateStats() 마지막 줄에:
///        CombatPowerManager.Instance?.Recalculate();
///    - SkillManager.LearnSkill() 성공 후:
///        CombatPowerManager.Instance?.Recalculate();
///    - SkillHotbarSlot.AssignSkill() / ClearSlot() 호출 후:
///        CombatPowerManager.Instance?.Recalculate();
///
/// ══════════════════════════════════════════════════════════════════
/// </summary>
public class CombatPowerManager : MonoBehaviour
{
    public static CombatPowerManager Instance { get; private set; }

    // ── 가중치 설정 (Inspector에서 조정 가능) ──────────────────────
    [Header("전투력 가중치 설정")]
    [Tooltip("레벨 1당 전투력 기여량")]
    public float levelPowerPerLevel = 100f;

    [Tooltip("공격력 1당 전투력 기여량")]
    public float attackWeight = 5f;

    [Tooltip("방어력 1당 전투력 기여량")]
    public float defenseWeight = 3f;

    [Tooltip("최대 HP 1당 전투력 기여량")]
    public float hpWeight = 0.5f;

    [Tooltip("크리티컬 확률 1% 당 전투력 기여량")]
    public float critWeight = 10f;

    [Tooltip("장비 강화 레벨 1당 전투력 기여량")]
    public float enhancePowerPerLevel = 50f;

    [Tooltip("장비 희귀도별 전투력 보너스 배율 (Common=1x, Uncommon=1.2x, Rare=1.5x, Epic=2x, Legendary=3x)")]
    public float[] rarityMultipliers = { 1f, 1.2f, 1.5f, 2f, 3f };

    [Tooltip("스킬 레벨 1당 전투력 기여량")]
    public float skillPowerPerLevel = 80f;

    [Tooltip("핫바에 장착된 스킬 추가 보너스 (배율, 기본 1.3 = 30% 추가)")]
    public float hotbarSkillBonus = 1.3f;

    // ── 현재 전투력 세부 수치 ──────────────────────────────────────
    [Header("전투력 현황 (읽기 전용 - 디버그용)")]
    [SerializeField] private int totalCombatPower;
    [SerializeField] private int levelPower;
    [SerializeField] private int statPower;
    [SerializeField] private int equipmentPower;
    [SerializeField] private int skillPower;

    // ── 이벤트 ────────────────────────────────────────────────────
    /// <summary>
    /// 전투력 변경 이벤트
    /// 파라미터: (새 전투력, 이전 전투력)
    /// CombatPowerUI 등에서 구독해서 UI 갱신에 사용
    /// </summary>
    public static event Action<int, int> OnCombatPowerChanged;

    // ── 내부 상태 ─────────────────────────────────────────────────
    private int previousCombatPower = 0;

    // EquipmentManager / SkillManager 참조용 인터페이스
    // (직접 참조 대신 Delegate로 주입해서 순환 참조 없이 연동)
    private Func<List<EquippedItemInfo>> getEquippedItemsFunc;
    private Func<List<LearnedSkillInfo>> getLearnedSkillsFunc;
    private Func<List<int>> getHotbarSkillIdsFunc;

    // ════════════════════════════════════════════════════════════════
    // 구조체: 외부 매니저에서 데이터 전달용
    // ════════════════════════════════════════════════════════════════

    /// <summary>장착된 장비 정보 전달 구조체</summary>
    public struct EquippedItemInfo
    {
        public int attackBonus;
        public int defenseBonus;
        public int hpBonus;
        public int criticalBonus;
        public int enhanceLevel;
        public int rarityIndex;   // 0=Common, 1=Uncommon, 2=Rare, 3=Epic, 4=Legendary
    }

    /// <summary>습득한 스킬 정보 전달 구조체</summary>
    public struct LearnedSkillInfo
    {
        public int skillID;
        public int currentLevel;
        public bool isOnHotbar;
    }

    // ════════════════════════════════════════════════════════════════
    // Unity 생명주기
    // ════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] CombatPowerManager가 생성되었습니다.");
        }
        else { enabled = false; Destroy(gameObject); return; }
    }

    void Start()
    {
        if (Instance != this) return;
        // PlayerStats 레벨업 이벤트 구독
        PlayerStats.OnLevelChanged += OnPlayerLevelChanged;

        // 초기 전투력 계산
        Recalculate();
    }

    void OnDestroy()
    {
        PlayerStats.OnLevelChanged -= OnPlayerLevelChanged;
    }

    // ════════════════════════════════════════════════════════════════
    // 외부 연동 등록 (EquipmentManager, SkillManager에서 Start()에 호출)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// EquipmentManager에서 장착 장비 데이터를 제공하는 함수 등록
    ///
    /// EquipmentManager.Start() 또는 Awake()에서 호출:
    ///   CombatPowerManager.Instance?.RegisterEquipmentProvider(() => GetEquippedItemInfos());
    /// </summary>
    public void RegisterEquipmentProvider(Func<List<EquippedItemInfo>> provider)
    {
        getEquippedItemsFunc = provider;
        Recalculate();
    }

    /// <summary>
    /// SkillManager에서 습득 스킬 데이터를 제공하는 함수 등록
    ///
    /// SkillManager.Start()에서 호출:
    ///   CombatPowerManager.Instance?.RegisterSkillProvider(() => GetLearnedSkillInfos());
    /// </summary>
    public void RegisterSkillProvider(Func<List<LearnedSkillInfo>> provider)
    {
        getLearnedSkillsFunc = provider;
        Recalculate();
    }

    // ════════════════════════════════════════════════════════════════
    // 전투력 계산 (핵심 메서드)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 전투력 전체 재계산
    /// EquipmentManager.RecalculateStats() / SkillManager.LearnSkill() 성공 후 호출하세요.
    /// </summary>
    public void Recalculate()
    {
        previousCombatPower = totalCombatPower;

        levelPower = CalculateLevelPower();
        statPower = CalculateStatPower();
        equipmentPower = CalculateEquipmentPower();
        skillPower = CalculateSkillPower();

        totalCombatPower = levelPower + statPower + equipmentPower + skillPower;

        // 변경이 있을 때만 이벤트 발생
        if (totalCombatPower != previousCombatPower)
        {
            OnCombatPowerChanged?.Invoke(totalCombatPower, previousCombatPower);

            Debug.Log($"[전투력] {previousCombatPower:N0} → {totalCombatPower:N0} " +
                      $"(레벨:{levelPower} 스탯:{statPower} 장비:{equipmentPower} 스킬:{skillPower})");
        }
    }

    // ── 1. 레벨 기여 ──────────────────────────────────────────────
    private int CalculateLevelPower()
    {
        if (PlayerStats.Instance == null) return 0;
        return Mathf.RoundToInt(PlayerStats.Instance.level * levelPowerPerLevel);
    }

    // ── 2. 기본 스탯 기여 (레벨 성장 포함) ────────────────────────
    private int CalculateStatPower()
    {
        if (PlayerStats.Instance == null) return 0;

        var ps = PlayerStats.Instance;

        float power = 0f;
        power += ps.GetTotalAttack() * attackWeight;
        power += ps.GetTotalDefense() * defenseWeight;
        power += ps.maxHealth * hpWeight;
        power += ps.GetTotalCritRate() * critWeight;

        return Mathf.RoundToInt(power);
    }

    // ── 3. 장비 기여 (착용 + 강화 + 희귀도) ──────────────────────
    private int CalculateEquipmentPower()
    {
        if (getEquippedItemsFunc == null) return 0;

        List<EquippedItemInfo> items = getEquippedItemsFunc.Invoke();
        if (items == null || items.Count == 0) return 0;

        float power = 0f;

        foreach (var item in items)
        {
            // 장비 기본 스탯 전투력
            float basePower = 0f;
            basePower += item.attackBonus * attackWeight;
            basePower += item.defenseBonus * defenseWeight;
            basePower += item.hpBonus * hpWeight;
            basePower += item.criticalBonus * critWeight;

            // 희귀도 배율 적용
            float rarity = GetRarityMultiplier(item.rarityIndex);
            basePower *= rarity;

            // 강화 레벨 기여 (+ 강화 레벨 × 50)
            float enhancePower = item.enhanceLevel * enhancePowerPerLevel;
            // 강화 레벨이 높을수록 스탯 성장도 반영 (공격 +2, 방어 +1 per level)
            enhancePower += (item.enhanceLevel * 2) * attackWeight;
            enhancePower += (item.enhanceLevel * 1) * defenseWeight;
            enhancePower *= rarity; // 강화도 희귀도 배율 적용

            power += basePower + enhancePower;
        }

        return Mathf.RoundToInt(power);
    }

    // ── 4. 스킬 기여 (습득 레벨 + 핫바 보너스) ───────────────────
    private int CalculateSkillPower()
    {
        if (getLearnedSkillsFunc == null) return 0;

        List<LearnedSkillInfo> skills = getLearnedSkillsFunc.Invoke();
        if (skills == null || skills.Count == 0) return 0;

        float power = 0f;

        foreach (var skill in skills)
        {
            if (skill.currentLevel <= 0) continue;

            float skillContrib = skill.currentLevel * skillPowerPerLevel;

            // 핫바에 장착된 스킬은 보너스 배율 적용
            if (skill.isOnHotbar)
                skillContrib *= hotbarSkillBonus;

            power += skillContrib;
        }

        return Mathf.RoundToInt(power);
    }

    // ════════════════════════════════════════════════════════════════
    // 유틸리티
    // ════════════════════════════════════════════════════════════════

    private float GetRarityMultiplier(int rarityIndex)
    {
        if (rarityMultipliers == null || rarityIndex < 0 || rarityIndex >= rarityMultipliers.Length)
            return 1f;
        return rarityMultipliers[rarityIndex];
    }

    private void OnPlayerLevelChanged(int newLevel)
    {
        Recalculate();
    }

    // ── 공개 Getter ───────────────────────────────────────────────

    public int TotalCombatPower => totalCombatPower;
    public int LevelPower => levelPower;
    public int StatPower => statPower;
    public int EquipmentPower => equipmentPower;
    public int SkillPower => skillPower;

    /// <summary>전투력 등급 텍스트 반환 (UI 표시용)</summary>
    public string GetPowerGrade()
    {
        if (totalCombatPower >= 100000) return "<color=#FF4500>신화</color>";
        if (totalCombatPower >= 50000) return "<color=#FF8C00>전설</color>";
        if (totalCombatPower >= 20000) return "<color=#9B59B6> 영웅</color>";
        if (totalCombatPower >= 8000) return "<color=#3498DB> 희귀</color>";
        if (totalCombatPower >= 2000) return "<color=#2ECC71> 일반</color>";
        return "<color=#95A5A6> 초보</color>";
    }
}