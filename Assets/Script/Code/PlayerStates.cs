using System.Collections;
using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 스탯 및 체력 관리 (크리티컬 등급 시스템 + 레벨업 스탯 성장 추가 버전)
///
/// ★ 수정사항 (레벨업 스탯 성장):
///   - [Header("레벨업 스탯 성장")] 섹션 추가 - 레벨당 성장 수치 Inspector에서 설정
///   - OnLevelUp(int newLevel) 메서드 추가 - GameManager에서 레벨업 시 반드시 호출
///   - ApplyLevelStats() 메서드 추가 - 현재 레벨 기준으로 전체 스탯 재계산
///   - OnLevelChanged 이벤트 추가 - UI 등에서 구독 가능
///
/// ★ GameManager.cs 연동 방법:
///   레벨업이 발생하는 시점(level++)에 아래 한 줄 추가:
///   PlayerStats.Instance?.OnLevelUp(level);
/// </summary>
public class PlayerStats : MonoBehaviour, IHitable
{
    public static PlayerStats Instance { get; private set; }

    [Header("체력")]
    public float maxHealth;
    public float currentHealth;
    [HideInInspector] public float baseMaxHealth;

    [Header("마나")]
    public float maxMana;
    public float currentMana;
    public float manaRegenRate = 5f;

    [Header("스탯")]
    public float attackPower = 10f;
    public float defense = 5f;
    public float criticalRate = 10f;        // 크리티컬 확률 (%)
    public float criticalDamage = 150f;     // 크리티컬 데미지 배율 (%)

    // ─── 레벨업 스탯 성장 (★★★ 핵심 추가 영역 ★★★) ─────────────────────
    [Header("레벨업 스탯 성장 (레벨당 증가량)")]
    [Tooltip("레벨당 공격력 증가량")]
    public float attackGrowthPerLevel = 3f;

    [Tooltip("레벨당 방어력 증가량")]
    public float defenseGrowthPerLevel = 1.5f;

    [Tooltip("레벨당 최대 HP 증가량")]
    public float maxHpGrowthPerLevel = 20f;

    [Tooltip("레벨당 최대 마나 증가량")]
    public float maxManaGrowthPerLevel = 5f;

    [Tooltip("레벨당 크리티컬 확률 증가량 (%)")]
    public float critGrowthPerLevel = 0.5f;

    [Tooltip("레벨당 이동 속도 증가량")]
    public float speedGrowthPerLevel = 0.1f;

    [Tooltip("레벨업 스탯 성장에 가속도 적용 여부 (높은 레벨일수록 더 많이 성장)")]
    public bool useScalingGrowth = false;

    [Tooltip("스케일링 성장 배율 (useScalingGrowth가 true일 때)")]
    [Range(1f, 2f)]
    public float scalingFactor = 1.05f;
    // ─────────────────────────────────────────────────────────────────────

    [Header("크리티컬 등급 설정")]
    [Tooltip("슈퍼 크리티컬 데미지 배율 (%, 기본 250%)")]
    public float superCriticalDamage = 250f;

    [Tooltip("울트라 크리티컬 데미지 배율 (%, 기본 400%)")]
    public float ultraCriticalDamage = 400f;

    [Tooltip("슈퍼 크리티컬 발동 확률 (크리티컬 중에서, %, 기본 30%)")]
    [Range(0f, 100f)]
    public float superCriticalChance = 30f;

    [Tooltip("울트라 크리티컬 발동 확률 (슈퍼 크리티컬 중에서, %, 기본 20%)")]
    [Range(0f, 100f)]
    public float ultraCriticalChance = 20f;

    [Header("무적 시간")]
    public float invincibilityDuration = 1f;
    private float invincibilityTimer = 0f;
    private bool isInvincible = false;

    [Header("UI 참조")]
    public Slider healthBar;
    public Slider manaBar;

    // 현재 레벨
    public int level;

    // 보너스 스탯 (장비, 스킬, 레벨업 등으로 누적)
    // ★ bonusAttack, bonusDefense 등은 장비 보너스 전용으로 유지
    //    레벨 성장은 baseAttack + levelAttack 구조로 별도 관리
    public float bonusAttack;
    public float bonusDefense;
    public float bonusMaxHp;
    public float bonusSpeed;
    public float bonusCritical;
    [Tooltip("공격속도 보너스 (%). 장비/버프로 누적. 50이면 50% 빨라짐")]
    public float bonusAttackSpeed;

    // ★★★ 레벨업으로 누적된 스탯 (별도 추적) ★★★
    private float levelBonusAttack;
    private float levelBonusDefense;
    private float levelBonusMaxHp;
    private float levelBonusMaxMana;
    private float levelBonusSpeed;
    private float levelBonusCritical;

    public bool IsDead { get; private set; }

    // ─── 이벤트 ───
    public static event Action OnPlayerDeath;
    public static event Action<float, float> OnHealthChanged;
    public static event Action<float, float> OnManaChanged;

    /// <summary>
    /// ★ 레벨 변경 이벤트 (UI에서 구독해서 레벨 표시 갱신 가능)
    /// 파라미터: 새 레벨
    /// </summary>
    public static event Action<int> OnLevelChanged;

    void Awake()
    {
        // ★ 씬 전환 후 옛 Instance가 파괴됐을 수 있으므로 항상 갱신
        if (Instance == null || Instance == this || !Instance.gameObject.scene.isLoaded)
        {
            Instance = this;
            Debug.Log("[ManagerInit] PlayerStats가 생성되었습니다.");
        }
        else
        {
            Debug.LogWarning("PlayerStats 인스턴스가 이미 존재합니다!");
        }

        baseMaxHealth = maxHealth;
        currentHealth = maxHealth;
        currentMana = maxMana;
        IsDead = false;
    }

    void OnEnable()
    {
        // ★ DDOL Player의 PlayerStats가 항상 Instance로 등록되도록
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // 시작 시 현재 레벨 기준으로 스탯 적용 (저장 데이터 로드 후 복원용)
        if (level > 1)
        {
            ApplyLevelStats(level);
        }

        UpdateUI();
    }

    void Update()
    {
        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0f)
            {
                isInvincible = false;
            }
        }

        if (currentMana < maxMana && !IsDead)
        {
            RestoreMana(manaRegenRate * Time.deltaTime);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    #region ★★★ 레벨업 스탯 성장 시스템 (핵심 추가 영역) ★★★

    /// <summary>
    /// GameManager에서 레벨업이 발생할 때 반드시 호출하세요.
    ///
    /// 사용 예시 (GameManager.cs):
    ///   level++;
    ///   PlayerStats.Instance?.OnLevelUp(level);
    /// </summary>
    /// <param name="newLevel">새로운 레벨</param>
    public void OnLevelUp(int newLevel)
    {
        int prevLevel = level;
        level = newLevel;

        // 이전 레벨 → 새 레벨까지 성장치 재계산
        ApplyLevelStats(newLevel);

        // 레벨업 시 체력/마나 전체 회복 (원하지 않으면 제거 가능)
        currentHealth = maxHealth;
        currentMana = maxMana;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnManaChanged?.Invoke(currentMana, maxMana);
        OnLevelChanged?.Invoke(newLevel);

        UpdateUI();

        Debug.Log($"[PlayerStats]  레벨업! {prevLevel} → {newLevel} | " +
                  $"공격:{attackPower + bonusAttack + levelBonusAttack:F0} " +
                  $"방어:{defense + bonusDefense + levelBonusDefense:F0} " +
                  $"HP:{maxHealth:F0} " +
                  $"크리:{criticalRate + bonusCritical + levelBonusCritical:F1}%");
    }

    /// <summary>
    /// 현재 레벨에 맞게 레벨 성장 스탯을 재계산합니다.
    /// 레벨 데이터를 로드할 때도 사용됩니다.
    /// </summary>
    /// <param name="targetLevel">적용할 레벨 (1 이상)</param>
    public void ApplyLevelStats(int targetLevel)
    {
        if (targetLevel <= 1)
        {
            // 레벨 1이면 성장치 0
            levelBonusAttack = 0f;
            levelBonusDefense = 0f;
            levelBonusMaxHp = 0f;
            levelBonusMaxMana = 0f;
            levelBonusSpeed = 0f;
            levelBonusCritical = 0f;
            RecalculateMaxHp();
            UpdateUI();
            return;
        }

        int growthLevels = targetLevel - 1; // 레벨 1 기준, 레벨 2부터 성장

        if (useScalingGrowth)
        {
            // 가속 성장: 레벨이 높을수록 더 많이 성장
            // 합산 공식: growth * ((scalingFactor^growthLevels - 1) / (scalingFactor - 1))
            float scale = scalingFactor;
            float sum = scale > 1f
                ? (Mathf.Pow(scale, growthLevels) - 1f) / (scale - 1f)
                : growthLevels;

            levelBonusAttack = attackGrowthPerLevel * sum;
            levelBonusDefense = defenseGrowthPerLevel * sum;
            levelBonusMaxHp = maxHpGrowthPerLevel * sum;
            levelBonusMaxMana = maxManaGrowthPerLevel * sum;
            levelBonusSpeed = speedGrowthPerLevel * sum;
            levelBonusCritical = critGrowthPerLevel * sum;
        }
        else
        {
            // 선형 성장: 레벨마다 고정 수치 증가
            levelBonusAttack = attackGrowthPerLevel * growthLevels;
            levelBonusDefense = defenseGrowthPerLevel * growthLevels;
            levelBonusMaxHp = maxHpGrowthPerLevel * growthLevels;
            levelBonusMaxMana = maxManaGrowthPerLevel * growthLevels;
            levelBonusSpeed = speedGrowthPerLevel * growthLevels;
            levelBonusCritical = critGrowthPerLevel * growthLevels;
        }

        // 크리티컬 100% 초과 방지
        float maxCritBonus = 100f - criticalRate - bonusCritical;
        levelBonusCritical = Mathf.Min(levelBonusCritical, Mathf.Max(0f, maxCritBonus));

        // 최대 마나 반영
        float newMaxMana = GetBaseMaxMana() + levelBonusMaxMana;
        float manaRatio = maxMana > 0 ? currentMana / maxMana : 1f;
        maxMana = newMaxMana;
        currentMana = Mathf.Clamp(maxMana * manaRatio, 0, maxMana);
        OnManaChanged?.Invoke(currentMana, maxMana);

        // 최대 HP 반영 (bonusMaxHp + levelBonusMaxHp 합산)
        RecalculateMaxHp();

        UpdateUI();
    }

    // 기준 최대 마나 (Start에서 설정한 원본값 보존용)
    private float _baseMaxMana = -1f;
    private float GetBaseMaxMana()
    {
        if (_baseMaxMana < 0f) _baseMaxMana = maxMana;
        return _baseMaxMana;
    }

    /// <summary>
    /// 레벨 성장 공격력 반환 (외부 참조용)
    /// </summary>
    public float GetTotalAttack() => attackPower + bonusAttack + levelBonusAttack + GetCompanionBonusAttack();

    /// <summary>
    /// 레벨 성장 방어력 반환 (외부 참조용)
    /// </summary>
    public float GetTotalDefense() => defense + bonusDefense + levelBonusDefense + GetCompanionBonusDefense();

    /// <summary>
    /// 레벨 성장 크리티컬 확률 반환 (외부 참조용)
    /// </summary>
    public float GetTotalCritRate() => criticalRate + bonusCritical + levelBonusCritical;

    /// <summary>
    /// 레벨 성장 이동속도 보너스 반환
    /// PlayerController에서 moveSpeed + PlayerStats.Instance.GetTotalSpeedBonus() 형태로 사용
    /// </summary>
    public float GetTotalSpeedBonus() => bonusSpeed + levelBonusSpeed;

    /// <summary>
    /// 공격속도 배율 반환 (1.0 = 기본, 1.5 = 50% 빠름)
    /// fireRate에 나누기로 적용: 실제간격 = 기본간격 / GetAttackSpeedMultiplier()
    /// </summary>
    public float GetAttackSpeedMultiplier() => 1f + (bonusAttackSpeed / 100f);

    // ═══════════════════════════════════════════════════════════════
    //  ★ 동료 장착 보너스
    // ═══════════════════════════════════════════════════════════════
    //
    //  등급별 기본 전달 비율:
    //    Epic      = 300%
    //    Legendary = 500%
    //    그 외     = 0% (Common, Rare는 전달 없음)
    //
    //  성급(승급) 효과:
    //    동료 자체 스탯: 성급당 +100% (1성=x1, 2성=x2, 3성=x3 ...) — 동료 본체에 큰 영향
    //    캐릭터 전달 비율: 성급당 +10% 추가 — 플레이어 영향은 작게
    //      예) Epic 3성: 기본300% + (3-1)*10% = 320%, 동료 본체 스탯 x3
    //      예) Legendary 2성: 기본500% + (2-1)*10% = 510%, 동료 본체 스탯 x2
    // ═══════════════════════════════════════════════════════════════

    /// <summary>핫바에 장착된 모든 동료의 공격력 보너스 합산</summary>
    public float GetCompanionBonusAttack()
    {
        return GetCompanionStatBonus(c => c.attackPower);
    }

    /// <summary>핫바에 장착된 모든 동료의 방어력 보너스 합산</summary>
    public float GetCompanionBonusDefense()
    {
        return GetCompanionStatBonus(c => c.defense);
    }

    /// <summary>핫바에 장착된 모든 동료의 체력 보너스 합산</summary>
    public float GetCompanionBonusMaxHp()
    {
        return GetCompanionStatBonus(c => c.maxHealth);
    }

    private float GetCompanionStatBonus(System.Func<CompanionData, float> statSelector)
    {
        if (CompanionHotbarManager.Instance == null || CompanionInventoryManager.Instance == null)
            return 0f;

        float totalBonus = 0f;
        string[] hotbarIDs = CompanionHotbarManager.Instance.GetHotbarSaveData();
        if (hotbarIDs == null) return 0f;

        foreach (string id in hotbarIDs)
        {
            if (string.IsNullOrEmpty(id)) continue;

            var entry = CompanionInventoryManager.Instance.FindCompanionEntry(id);
            if (entry == null || entry.data == null) continue;

            CompanionData data = entry.data;

            // 등급별 기본 전달 비율 (%) — Epic 300%, Legendary 500%
            float baseTransferRate = data.rarity switch
            {
                CompanionRarity.Epic      => 300f,
                CompanionRarity.Legendary => 500f,
                _                         => 0f
            };

            if (baseTransferRate <= 0f) continue;

            // 성급 계산 (stars == -1이면 baseStars 사용)
            int stars = entry.stars >= 1 ? entry.stars : data.baseStars;

            // 동료 스탯 = 기본값 × 성급 (1성=x1, 2성=x2 ...)
            float companionStat = statSelector(data) * stars;

            // 전달 비율 = 기본 + (성급-1) × 10%
            float transferRate = baseTransferRate + (stars - 1) * 10f;

            // 캐릭터에 전달되는 보너스
            totalBonus += companionStat * (transferRate / 100f);
        }

        return totalBonus;
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────

    #region IHitable 인터페이스 구현

    public void TakeDamage(float damage)
    {
        if (IsDead || isInvincible) return;

        float totalDefense = GetTotalDefense();
        float damageReduction = totalDefense / (totalDefense + 100f);
        float actualDamage = damage * (1f - damageReduction);
        actualDamage = Mathf.Max(1f, actualDamage);

        currentHealth -= actualDamage;
        currentHealth = Mathf.Max(0f, currentHealth);

        StartInvincibility();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateUI();

        if (currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            OnHitEffect();
        }

        Debug.Log($"플레이어 피격! 데미지: {actualDamage:F1}, 남은 체력: {currentHealth:F1}");
    }

    public void Hit(float damage)
    {
        TakeDamage(damage);
    }

    /// <summary>공격자 위치 포함 피격 (4방향 사망 애니메이션용)</summary>
    public void Hit(float damage, Vector2 attackerPosition)
    {
        // 죽기 전에 피격 방향 기록
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
            controller.SetDamageDirection(attackerPosition);

        TakeDamage(damage);
    }

    #endregion

    #region 체력 관리

    public void Heal(float amount)
    {
        if (IsDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateUI();
    }

    public void IncreaseMaxHealth(float amount)
    {
        maxHealth += amount;
        currentHealth += amount;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateUI();
    }

    public void FullHeal()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnManaChanged?.Invoke(currentMana, maxMana);
        UpdateUI();
    }

    public void RecalculateMaxHp()
    {
        // 레벨 성장 HP도 포함
        float newMaxHealth = baseMaxHealth + bonusMaxHp + levelBonusMaxHp + GetCompanionBonusMaxHp();
        float healthRatio = maxHealth > 0 ? currentHealth / maxHealth : 1f;

        maxHealth = newMaxHealth;
        currentHealth = maxHealth * healthRatio;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateUI();
    }

    #endregion

    #region 마나 관리

    public bool UseMana(float amount)
    {
        if (currentMana >= amount)
        {
            currentMana -= amount;
            OnManaChanged?.Invoke(currentMana, maxMana);
            UpdateUI();
            return true;
        }
        return false;
    }

    public void RestoreMana(float amount)
    {
        currentMana += amount;
        currentMana = Mathf.Min(currentMana, maxMana);
        OnManaChanged?.Invoke(currentMana, maxMana);
        UpdateUI();
    }

    public void IncreaseMaxMana(float amount)
    {
        maxMana += amount;
        currentMana += amount;
        OnManaChanged?.Invoke(currentMana, maxMana);
        UpdateUI();
    }

    #endregion

    #region 스탯 관리

    public void IncreaseAttackPower(float amount)
    {
        attackPower += amount;
    }

    public void IncreaseDefense(float amount)
    {
        defense += amount;
    }

    public void IncreaseCriticalRate(float amount)
    {
        criticalRate += amount;
        criticalRate = Mathf.Min(criticalRate, 100f);
    }

    public bool RollCritical()
    {
        float roll = UnityEngine.Random.Range(0f, 100f);
        return roll <= GetTotalCritRate();
    }

    /// <summary>
    /// 크리티컬 등급 판정 (0=일반, 1=크리티컬, 2=슈퍼, 3=울트라)
    /// ★ 레벨업 크리티컬 보너스(levelBonusCritical) 포함
    /// </summary>
    public int RollCriticalTier()
    {
        float totalCrit = GetTotalCritRate();

        float roll1 = UnityEngine.Random.Range(0f, 100f);
        if (roll1 > totalCrit)
            return 0;

        float roll2 = UnityEngine.Random.Range(0f, 100f);
        if (roll2 > superCriticalChance)
            return 1;

        float roll3 = UnityEngine.Random.Range(0f, 100f);
        if (roll3 > ultraCriticalChance)
            return 2;

        return 3;
    }

    /// <summary>
    /// 최종 데미지 계산 (크리티컬 등급 포함)
    /// ★ 레벨업 공격 보너스(levelBonusAttack) 포함
    /// </summary>
    public float CalculateDamageWithTier(out int criticalTier)
    {
        // ★ 레벨 성장 공격력 포함
        float totalAttack = GetTotalAttack();

        criticalTier = RollCriticalTier();

        float finalDamage = totalAttack;

        switch (criticalTier)
        {
            case 0:
                break;
            case 1:
                finalDamage *= (criticalDamage / 100f);
                Debug.Log($" 크리티컬! 데미지: {finalDamage:F1}");
                break;
            case 2:
                finalDamage *= (superCriticalDamage / 100f);
                Debug.Log($" 슈퍼 크리티컬!! 데미지: {finalDamage:F1}");
                break;
            case 3:
                finalDamage *= (ultraCriticalDamage / 100f);
                Debug.Log($" 울트라 크리티컬!!! 데미지: {finalDamage:F1}");
                break;
        }

        return finalDamage;
    }

    public float CalculateDamage()
    {
        int tier;
        return CalculateDamageWithTier(out tier);
    }

    #endregion

    #region 사망 처리

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        OnPlayerDeath?.Invoke();

        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        OnDeathEffect();
        Invoke(nameof(ShowGameOverUI), 1f);
    }

    public void Revive()
    {
        IsDead = false;
        currentHealth = maxHealth * 0.5f;
        currentMana = maxMana * 0.5f;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnManaChanged?.Invoke(currentMana, maxMana);
        UpdateUI();

        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.enabled = true;
        }
    }

    #endregion

    #region 무적 시간

    private void StartInvincibility()
    {
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;
    }

    public void SetInvincible(float duration)
    {
        isInvincible = true;
        invincibilityTimer = duration;
    }

    #endregion

    #region UI 업데이트

    public void UpdateUI()
    {
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }

        if (manaBar != null)
        {
            manaBar.maxValue = maxMana;
            manaBar.value = currentMana;
        }
    }

    /// <summary>
    /// 장착/해제/강화 후 모든 보너스 스탯 반영
    /// EquipmentManager에서 장착/해제 시 자동 호출
    /// </summary>
    public void UpdateStatsUI()
    {
        RecalculateMaxHp();
        UpdateUI();

        // ★ Fix: UIManager의 장비창 스탯 텍스트도 갱신
        UIManager.Instance?.UpdateStatsUI();

        Debug.Log($"[PlayerStats] 스탯 갱신 - " +
                  $"공격:{GetTotalAttack():F0} " +
                  $"방어:{GetTotalDefense():F0} " +
                  $"HP:{maxHealth:F0} " +
                  $"속도보너스:{GetTotalSpeedBonus():F2} " +
                  $"크리:{GetTotalCritRate():F1}% " +
                  $"(레벨 성장 - 공격:{levelBonusAttack:F0} 방어:{levelBonusDefense:F0} HP:{levelBonusMaxHp:F0})");
    }

    #endregion

    #region 효과

    private void OnHitEffect()
    {
        StartCoroutine(BlinkEffect());
    }

    private System.Collections.IEnumerator BlinkEffect()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        for (int i = 0; i < 3; i++)
        {
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.materials)
                {
                    Color color = material.color;
                    color.a = 0.3f;
                    material.color = color;
                }
            }
            yield return new WaitForSeconds(0.1f);

            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.materials)
                {
                    Color color = material.color;
                    color.a = 1f;
                    material.color = color;
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void OnDeathEffect() { }

    private void ShowGameOverUI()
    {
        Debug.Log("[PlayerStats] 게임 오버 → 현재 스테이지 재시작");

        // ── 1. 플레이어 상태 초기화 ──
        IsDead = false;
        currentHealth = maxHealth;
        currentMana = maxMana;
        isInvincible = false;
        invincibilityTimer = 0f;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnManaChanged?.Invoke(currentMana, maxMana);
        UpdateUI();

        // ── 2. 플레이어 컨트롤러 재활성화 ──
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = true;

        // ── 3. 웨이브 스포너에 현재 스테이지 재시작 요청 ──
        if (WaveSpawner.Instance != null)
        {
            WaveSpawner.Instance.RestartCurrentStage();
        }

        // ── 4. 메시지 표시 ──
        UIManager.Instance?.ShowMessage("부활! 스테이지 재시작", Color.red);
    }

    #endregion
}