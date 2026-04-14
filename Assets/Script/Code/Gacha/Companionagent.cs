using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CompanionAgent (2D)
/// ─────────────────────────────────────────────────────────
/// 핫바에서 소환된 동료. 체력이 있고, 피격당하면 HP 감소.
/// HP가 0이 되면 사망 → 매니저에 통보 → 쿨타임 시작.
///
/// [동작 우선순위]
///   1. 적이 탐색 범위 내에 있으면 → Chase → Attack (전투 우선)
///   2. 적이 없으면 → 플레이어 근처를 따라다님 (Idle/Follow)
///   3. 플레이어와 너무 멀면(전투 중 아닐 때) → 빠르게 귀환
///
/// [프리팹 구성]
///   SpriteRenderer — 동료 외형
///   Animator       — 4방향 블렌드 트리 (Idle/Walk/Attack)
///   Collider2D     — 충돌 판정
///   Rigidbody2D    — gravityScale=0, freezeRotation=true (없으면 자동 추가)
///
/// [Animator 파라미터 — 4방향 블렌드 트리]
///   Float "MoveX"    — 이동 방향 X (-1=왼, 1=오른)
///   Float "MoveY"    — 이동 방향 Y (-1=아래, 1=위)
///   Bool  "IsMoving" — 이동 중 (Idle↔Walk 전환)
///   Trigger "Attack" — 공격
///   ★ Hit/Die 애니메이션 없음 — Hit은 스프라이트 흰색 플래시, Die는 파티클 이펙트
/// </summary>
public class CompanionAgent : MonoBehaviour, IHitable
{
    [Header("동료 데이터 (런타임 주입)")]
    public CompanionData data;

    [Header("이동 설정")]
    [Tooltip("플레이어 근처 대기 거리 (이 범위 안에서 따라다님)")]
    public float followDistance = 3f;
    [Tooltip("플레이어로부터 이 거리 이상이면 빠르게 귀환 (전투 중 아닐 때만)")]
    public float returnDistance = 12f;
    [Tooltip("플레이어로부터 이 거리 이상이면 즉시 텔레포트")]
    public float teleportDistance = 25f;

    [Header("탐색")]
    [Tooltip("적 탐색 반경")]
    public float detectionRadius = 10f;
    public LayerMask enemyLayer;

    [Header("시각 효과")]
    public GameObject summonEffect;
    public GameObject attackEffect;
    public GameObject deathEffect;
    public float summonEffectDuration = 1f;

    [Header("HP 바 (선택, 프리팹 자식)")]
    [Tooltip("WorldSpace 체력바 — 없으면 표시 안 함")]
    public Transform hpBarFill;

    // ── 체력 ──
    public float CurrentHealth { get; private set; }
    public float MaxHealth { get; private set; }
    public bool IsDead { get; private set; }

    /// <summary>사망 시 Manager에 통보하는 콜백</summary>
    public System.Action<CompanionAgent> OnDied;

    // ── 상태 머신 ──
    private enum AgentState { Follow, Chase, Attack, Return, Dead }
    private AgentState state = AgentState.Follow;

    private Transform player;
    private Transform target;
    private float attackCooldown;
    private float enemySearchTimer; // 적 탐색 간격 (매 프레임 X)
    private bool isActive = true;

    // 피격
    private float hitInvincibleTimer;
    private const float HIT_INVINCIBLE_TIME = 0.2f;
    private float flashTimer;
    private Color originalColor = Color.white;

    // ── 스킬 시스템 ──
    private float[] skillCooldowns;       // 스킬별 남은 쿨타임
    private int companionLevel = 1;       // 동료 레벨 (스킬 해금 판단용)
    private float buffTimer = 0f;         // 버프 남은 시간
    private float buffMultiplier = 1f;    // 버프 공격력 배율

    // ── 분리 캐시 (FindObjectsOfType 매 프레임 방지) ──
    private static readonly List<CompanionAgent> _allAgents = new List<CompanionAgent>();

    // ── 2D 컴포넌트 ──
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    // ── 애니메이션 파라미터 (4방향 블렌드 트리) ──
    private static readonly int AnimMoveX = Animator.StringToHash("MoveX");
    private static readonly int AnimMoveY = Animator.StringToHash("MoveY");
    private static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");

    // 마지막 이동 방향 (정지 시에도 Idle 블렌드 트리에서 바라보는 방향 유지)
    private Vector2 lastMoveDir = Vector2.down; // 기본: 아래 바라봄

    // ─────────────────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────────────────

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        // ★ Apply Root Motion 끄기 — 애니메이션이 위치를 원점으로 끌어당기는 버그 방지
        if (animator != null)
            animator.applyRootMotion = false;

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        // ★ 전역 레지스트리 등록 (FindObjectsOfType 매 프레임 대체)
        _allAgents.Add(this);
    }

    void OnDestroy()
    {
        _allAgents.Remove(this);
    }

    void Start()
    {
        // 체력 초기화
        MaxHealth = data != null ? data.maxHealth : 500f;
        CurrentHealth = MaxHealth;

        // 플레이어 탐색 — PlayerController 컴포넌트가 있는 오브젝트 우선
        player = FindPlayerTransform();

        // 스킬 쿨타임 초기화
        InitializeSkills();

        // 소환 이펙트
        if (summonEffect != null)
        {
            GameObject fx = Instantiate(summonEffect, transform.position, Quaternion.identity);
            Destroy(fx, summonEffectDuration);
        }

        UpdateHPBar();
        Debug.Log($"[CompanionAgent] {(data != null ? data.companionName : "동료")} 소환 완료 (HP:{CurrentHealth}/{MaxHealth}, 스킬:{(data?.skills?.Count ?? 0)}개)");
    }

    /// <summary>동료 레벨 주입 (CompanionHotbarManager에서 호출)</summary>
    public void SetCompanionLevel(int level)
    {
        companionLevel = Mathf.Max(1, level);
    }

    private void InitializeSkills()
    {
        if (data == null || data.skills == null || data.skills.Count == 0)
        {
            skillCooldowns = new float[0];
            return;
        }

        skillCooldowns = new float[data.skills.Count];

        // 동료 레벨 가져오기 (CompanionInventoryManager에서)
        if (CompanionInventoryManager.Instance != null && data != null)
        {
            var entry = CompanionInventoryManager.Instance.FindCompanionEntry(data.companionID);
            if (entry != null)
                companionLevel = entry.level;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  상태 머신
    // ─────────────────────────────────────────────────────────

    void Update()
    {
        if (!isActive || IsDead || player == null) return;

        attackCooldown -= Time.deltaTime;

        // 스킬 쿨타임 갱신
        if (skillCooldowns != null)
        {
            for (int i = 0; i < skillCooldowns.Length; i++)
                if (skillCooldowns[i] > 0f) skillCooldowns[i] -= Time.deltaTime;
        }

        // 버프 타이머
        if (buffTimer > 0f)
        {
            buffTimer -= Time.deltaTime;
            if (buffTimer <= 0f) buffMultiplier = 1f;
        }

        // 피격 타이머
        if (hitInvincibleTimer > 0f)
            hitInvincibleTimer -= Time.deltaTime;

        // 피격 플래시 복원
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && spriteRenderer != null)
                spriteRenderer.color = originalColor;
        }

        float distToPlayer = Vector2.Distance(transform.position, player.position);

        // ── 텔레포트 (너무 멀면 즉시 이동) ──
        if (distToPlayer > teleportDistance)
        {
            transform.position = player.position + (Vector3)(Random.insideUnitCircle * followDistance * 0.5f);
            rb.velocity = Vector2.zero;
            state = AgentState.Follow;
            target = null;
            return;
        }

        // ── 적 탐색 (0.2초 간격) ──
        enemySearchTimer -= Time.deltaTime;
        if (enemySearchTimer <= 0f)
        {
            enemySearchTimer = 0.2f;
            Transform nearest = FindNearestEnemy();

            // 전투 중이 아닌 상태에서 적 발견 → Chase
            if (nearest != null && state != AgentState.Attack)
            {
                target = nearest;
                state = AgentState.Chase;
            }
        }

        // ── 전투 중이 아닐 때만 귀환 체크 ──
        if (state == AgentState.Follow && distToPlayer > returnDistance)
        {
            state = AgentState.Return;
            target = null;
        }

        switch (state)
        {
            case AgentState.Follow: DoFollow(distToPlayer); break;
            case AgentState.Chase:  DoChase();              break;
            case AgentState.Attack: DoAttack();             break;
            case AgentState.Return: DoReturn(distToPlayer); break;
        }
    }

    // ── Follow: 플레이어 근처 대기 + 적 탐색 ──
    [Header("동료 간 분리")]
    [Tooltip("다른 동료와 이 거리 이내면 서로 밀어냄")]
    public float separationRadius = 1.5f;
    [Tooltip("분리 힘 세기")]
    public float separationForce = 3f;

    private void DoFollow(float distToPlayer)
    {
        float speed = data != null ? data.moveSpeed * 0.6f : 2f;

        if (distToPlayer > followDistance)
        {
            // 플레이어에게 다가가기 (감속 적용)
            float slowdown = Mathf.Clamp01((distToPlayer - followDistance * 0.5f) / followDistance);
            MoveTowards(player.position, speed * Mathf.Max(0.3f, slowdown));
            SetAnimMoving(true);
        }
        else
        {
            // 충분히 가까우면 정지 + 분리 힘만 적용
            rb.velocity = Vector2.zero;
            SetAnimMoving(false);
        }

        // ★ 동료 간 겹침 방지: 가까운 동료로부터 밀어내기
        ApplySeparation();
    }

    /// <summary>주변 동료와 겹치지 않도록 밀어내는 힘 적용</summary>
    private void ApplySeparation()
    {
        Vector2 sepDir = Vector2.zero;

        // ★ static 레지스트리 사용 — FindObjectsOfType 매 프레임 호출 제거
        foreach (var other in _allAgents)
        {
            if (other == this || other.IsDead || !other.isActive) continue;
            Vector2 diff = (Vector2)transform.position - (Vector2)other.transform.position;
            float dist = diff.magnitude;
            if (dist < separationRadius && dist > 0.01f)
            {
                sepDir += diff.normalized * (1f - dist / separationRadius);
            }
        }

        if (sepDir.sqrMagnitude > 0.01f)
        {
            rb.velocity += sepDir.normalized * separationForce;
        }
    }

    // ── Chase: 적 추격 ──
    private void DoChase()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            target = null;
            state = AgentState.Follow;
            return;
        }

        float distToTarget = Vector2.Distance(transform.position, target.position);
        float range = data != null ? data.attackRange : 2f;

        if (distToTarget <= range)
        {
            rb.velocity = Vector2.zero;
            state = AgentState.Attack;
        }
        else
        {
            float speed = data != null ? data.moveSpeed : 3.5f;
            MoveTowards(target.position, speed);
            SetAnimMoving(true);
        }
    }

    // ── Attack: 공격 ──
    private void DoAttack()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            target = null;
            state = AgentState.Follow;
            return;
        }

        float distToTarget = Vector2.Distance(transform.position, target.position);
        float range = data != null ? data.attackRange : 2f;

        // 사거리 벗어나면 다시 추격
        if (distToTarget > range * 1.5f)
        {
            state = AgentState.Chase;
            return;
        }

        rb.velocity = Vector2.zero;
        SetAnimMoving(false);
        FlipTowards(target.position);

        if (attackCooldown <= 0f)
        {
            PerformAttack();
            float atkSpeed = data != null ? data.attackSpeed : 1f;
            attackCooldown = 1f / Mathf.Max(0.1f, atkSpeed);
        }
    }

    // ── Return: 플레이어에게 빠르게 귀환 ──
    private void DoReturn(float distToPlayer)
    {
        if (distToPlayer <= followDistance)
        {
            state = AgentState.Follow;
            rb.velocity = Vector2.zero;
            return;
        }

        // 적 발견하면 전투 우선
        if (target != null && target.gameObject.activeInHierarchy)
        {
            state = AgentState.Chase;
            return;
        }

        float speed = data != null ? data.moveSpeed * 2f : 7f;
        MoveTowards(player.position, speed);
        SetAnimMoving(true);
    }

    // ─────────────────────────────────────────────────────────
    //  공격 처리
    // ─────────────────────────────────────────────────────────

    private void PerformAttack()
    {
        if (target == null) return;

        // ★ 스킬 사용 가능하면 스킬 우선 사용
        int readySkillIndex = FindReadySkill();
        if (readySkillIndex >= 0)
        {
            UseSkill(readySkillIndex);
            return;
        }

        // 기본 공격
        float dmg = (data != null ? data.attackPower : 10f) * buffMultiplier;

        if (animator != null)
            animator.SetTrigger(AnimAttack);

        DealDamageToTarget(target, dmg);
        SpawnEffect(attackEffect, Vector3.Lerp(transform.position, target.position, 0.5f), 0.5f);
    }

    // ─────────────────────────────────────────────────────────
    //  스킬 시스템
    // ─────────────────────────────────────────────────────────

    /// <summary>사용 가능한 스킬 인덱스 반환 (-1이면 없음)</summary>
    private int FindReadySkill()
    {
        string cName = data != null ? data.companionName : "???";

        if (data == null || data.skills == null || skillCooldowns == null)
            return -1;

        for (int i = 0; i < data.skills.Count; i++)
        {
            if (i >= skillCooldowns.Length) break;

            CompanionSkillInfo skill = data.skills[i];
            if (skill == null) continue;

            if (companionLevel < skill.unlockCompanionLevel) continue;
            if (skillCooldowns[i] > 0f) continue;

            return i;
        }

        return -1;
    }

    /// <summary>스킬 사용</summary>
    private void UseSkill(int skillIndex)
    {
        CompanionSkillInfo skill = data.skills[skillIndex];
        int skillLevel = GetSkillLevel(skill.skillID);
        float multiplier = skill.GetDamageMultiplier(skillLevel) * buffMultiplier;
        float baseDmg = data.attackPower * multiplier;

        // 쿨타임 시작
        skillCooldowns[skillIndex] = skill.cooldown;

        // 공격 애니메이션
        if (animator != null)
            animator.SetTrigger(AnimAttack);

        // 이펙트 (스킬 전용 이펙트 > 기본 attackEffect)
        GameObject fx = skill.skillEffect != null ? skill.skillEffect : attackEffect;

        switch (skill.skillType)
        {
            case CompanionSkillType.SingleTarget:
                DealDamageToTarget(target, baseDmg);
                SpawnEffect(fx, target.position, 0.8f);
                break;

            case CompanionSkillType.AreaDamage:
                // 컷씬 연출 후 데미지 적용
                if (CompanionCutsceneUI.Instance != null && OptionUI.CutsceneMode != 2)
                {
                    string cName = data != null ? data.companionName : "동료";
                    Sprite portrait = data != null ? data.fullIllust ?? data.portrait : null;
                    Vector3 pos = target.position;
                    float radius = skill.areaRadius;
                    float dmg = baseDmg;
                    GameObject skillFx = fx;
                    CompanionCutsceneUI.Instance.PlayCutscene(cName, skill.skillName, portrait, pos, () =>
                    {
                        DealAreaDamage(pos, radius, dmg);
                        SpawnEffect(skillFx, pos, 1f);
                    });
                }
                else
                {
                    DealAreaDamage(target.position, skill.areaRadius, baseDmg);
                    SpawnEffect(fx, target.position, 1f);
                }
                break;

            case CompanionSkillType.Heal:
                float healAmount = skill.GetValue(skillLevel);
                CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + healAmount);
                UpdateHPBar();
                SpawnEffect(fx, transform.position, 1f);
                if (DamagePopupManager.Instance != null)
                    DamagePopupManager.Instance.ShowDamage(transform.position, healAmount, 0);
                break;

            case CompanionSkillType.Buff:
                buffMultiplier = 1f + skill.GetValue(skillLevel) * 0.01f; // baseValue를 %로 사용
                buffTimer = 5f + skillLevel; // 5초 + 레벨당 1초
                SpawnEffect(fx, transform.position, 1f);
                break;
        }

        string companionName = data != null ? data.companionName : "동료";
        Debug.Log($"[CompanionAgent] {companionName} 스킬 사용: {skill.skillName} (Lv.{skillLevel}, 쿨타임:{skill.cooldown}s)");
    }

    // ★ NonAlloc 정적 버퍼 (ALLOC_TEMP_MAIN 제거)
    private static readonly Collider2D[] _areaBuffer   = new Collider2D[16];
    private static readonly Collider2D[] _searchBuffer = new Collider2D[16];

    /// <summary>범위 데미지 — 중심점 주변 적 전체 피격</summary>
    private void DealAreaDamage(Vector3 center, float radius, float damage)
    {
        int count = Physics2D.OverlapCircleNonAlloc(center, radius, _areaBuffer, enemyLayer);
        for (int i = 0; i < count; i++)
        {
            if (_areaBuffer[i] == null || _areaBuffer[i].gameObject == gameObject) continue;
            DealDamageToTarget(_areaBuffer[i].transform, damage);
        }

        // LayerMask 미설정 시 태그 폴백
        if (count == 0)
        {
            GameObject[] enemies = null;
            try { enemies = GameObject.FindGameObjectsWithTag("Monster"); } catch { }
            if (enemies == null) return;

            foreach (var e in enemies)
            {
                if (e == null || !e.activeInHierarchy) continue;
                if (Vector2.Distance(center, e.transform.position) <= radius)
                    DealDamageToTarget(e.transform, damage);
            }
        }
    }

    /// <summary>대상에게 데미지 적용 (Monster/IHitable 공통)</summary>
    private void DealDamageToTarget(Transform t, float dmg)
    {
        if (t == null) return;

        Monster monster = t.GetComponent<Monster>();
        if (monster != null)
        {
            monster.Hit(dmg, 0);
            return;
        }

        IHitable hitable = t.GetComponent<IHitable>();
        if (hitable != null)
        {
            hitable.Hit(dmg);
            if (DamagePopupManager.Instance != null)
                DamagePopupManager.Instance.ShowDamage(t.position, dmg, 0);
        }
    }

    /// <summary>이펙트 생성 헬퍼</summary>
    private void SpawnEffect(GameObject effectPrefab, Vector3 pos, float duration)
    {
        if (effectPrefab == null) return;
        GameObject go = Instantiate(effectPrefab, pos, Quaternion.identity);
        Destroy(go, duration);
    }

    /// <summary>스킬 레벨 가져오기 (CompanionInventoryManager 연동)</summary>
    private int GetSkillLevel(string skillID)
    {
        if (CompanionInventoryManager.Instance == null || data == null) return 1;

        var companions = CompanionInventoryManager.Instance.GetCompanionList();
        if (companions == null) return 1;

        foreach (var entry in companions)
        {
            if (entry.data != null && entry.data.companionID == data.companionID)
            {
                if (entry.skillLevels != null)
                {
                    foreach (var sl in entry.skillLevels)
                    {
                        if (sl.skillID == skillID)
                            return Mathf.Max(1, sl.level);
                    }
                }
                break;
            }
        }

        return 1;
    }

    // ─────────────────────────────────────────────────────────
    //  피격 (IHitable 구현)
    // ─────────────────────────────────────────────────────────

    public void Hit(float damage)
    {
        TakeDamage(damage);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || !isActive) return;
        if (hitInvincibleTimer > 0f) return;

        float def = data != null ? data.defense : 0f;
        float finalDmg = Mathf.Max(1f, damage - def);

        CurrentHealth -= finalDmg;
        hitInvincibleTimer = HIT_INVINCIBLE_TIME;

        // ★ Hit 애니메이션 없음 — 스프라이트 흰색 플래시로 피격 표현
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
            flashTimer = 0.15f;
        }

        if (DamagePopupManager.Instance != null)
            DamagePopupManager.Instance.ShowDamage(transform.position, finalDmg, 0);

        UpdateHPBar();

        if (CurrentHealth <= 0f)
        {
            CurrentHealth = 0f;
            Die();
        }
    }

    // ─────────────────────────────────────────────────────────
    //  사망
    // ─────────────────────────────────────────────────────────

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        isActive = false;
        state = AgentState.Dead;

        rb.velocity = Vector2.zero;
        SetAnimMoving(false);

        // ★ Die 애니메이션 대신 스프라이트 즉시 숨김 + 파티클로 사망 표현
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        if (deathEffect != null)
        {
            GameObject fx = Instantiate(deathEffect, transform.position, Quaternion.identity);
            Destroy(fx, 1f);
        }

        string companionName = data != null ? data.companionName : "동료";
        UIManager.Instance?.ShowMessage($"{companionName} 쓰러졌습니다!", Color.red);
        Debug.Log($"[CompanionAgent] {companionName} 사망");

        OnDied?.Invoke(this);
        Destroy(gameObject, 0.5f);
    }

    // ─────────────────────────────────────────────────────────
    //  체력 바
    // ─────────────────────────────────────────────────────────

    private void UpdateHPBar()
    {
        if (hpBarFill == null) return;
        float ratio = MaxHealth > 0f ? CurrentHealth / MaxHealth : 0f;
        hpBarFill.localScale = new Vector3(ratio, 1f, 1f);
    }

    // ─────────────────────────────────────────────────────────
    //  소환 해제 (Manager에서 직접 호출)
    // ─────────────────────────────────────────────────────────

    public void Dismiss()
    {
        isActive = false;
        IsDead = true;
        rb.velocity = Vector2.zero;

        if (summonEffect != null)
            Instantiate(summonEffect, transform.position, Quaternion.identity);

        Destroy(gameObject, 0.2f);
    }

    // ─────────────────────────────────────────────────────────
    //  적 탐색
    // ─────────────────────────────────────────────────────────

    private Transform FindNearestEnemy()
    {
        // ★ NonAlloc: 0.2초 간격이지만 동료 수만큼 반복 → 정적 버퍼 재사용
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, detectionRadius, _searchBuffer, enemyLayer);

        if (count > 0)
        {
            Transform nearest = null;
            float nearestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var col = _searchBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;
                float d = Vector2.Distance(transform.position, col.transform.position);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = col.transform;
                }
            }
            if (nearest != null) return nearest;
        }

        // 태그 폴백
        return FindNearestEnemyByTag();
    }

    private Transform FindNearestEnemyByTag()
    {
        GameObject[] enemies = null;
        try { enemies = GameObject.FindGameObjectsWithTag("Monster"); } catch { }
        if (enemies == null || enemies.Length == 0)
        {
            try { enemies = GameObject.FindGameObjectsWithTag("Enemy"); } catch { }
        }
        if (enemies == null || enemies.Length == 0) return null;

        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (var e in enemies)
        {
            if (e == null || !e.activeInHierarchy || e == gameObject) continue;
            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d < detectionRadius && d < bestDist)
            {
                bestDist = d;
                best = e.transform;
            }
        }
        return best;
    }

    // ─────────────────────────────────────────────────────────
    //  이동 (감속 적용)
    // ─────────────────────────────────────────────────────────

    private void MoveTowards(Vector2 destination, float speed)
    {
        Vector2 dir = destination - (Vector2)transform.position;
        float dist = dir.magnitude;

        if (dist < 0.15f)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // 가까울수록 감속 (오버슈팅 방지)
        float finalSpeed = speed;
        if (dist < 1f)
            finalSpeed = speed * Mathf.Clamp01(dist);

        Vector2 normalizedDir = dir.normalized;
        rb.velocity = normalizedDir * finalSpeed;

        // 4방향 블렌드 트리 — 이동 방향을 Animator에 전달
        UpdateDirection(normalizedDir);
    }

    /// <summary>
    /// 4방향 블렌드 트리용 — 이동/바라보기 방향을 Animator에 전달
    /// 좌우는 같은 Side 클립 + flipX로 처리 (클립 수 절약)
    /// 블렌드 트리 Motion:
    ///   Down(0,-1), Up(0,1), Side(1,0) ← 오른쪽 기준 3개만
    /// 왼쪽 이동 시 MoveX=1(Side 클립 재생) + flipX=true
    /// </summary>
    private void UpdateDirection(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.01f) return;

        // 4방향 스냅: 상하좌우 중 가장 가까운 방향
        lastMoveDir = SnapTo4Direction(dir);

        if (animator != null)
        {
            // 좌우 모두 MoveX=1 (Side 클립)을 사용하고 flipX로 좌우 구분
            float animX = Mathf.Abs(lastMoveDir.x); // 좌든 우든 1
            float animY = lastMoveDir.y;
            animator.SetFloat(AnimMoveX, animX);
            animator.SetFloat(AnimMoveY, animY);
        }

        // flipX: 왼쪽 이동 시 스프라이트 뒤집기
        if (spriteRenderer != null && Mathf.Abs(lastMoveDir.x) > 0.5f)
        {
            spriteRenderer.flipX = (lastMoveDir.x < 0);
        }
    }

    /// <summary>
    /// 벡터를 4방향(상/하/좌/우)으로 스냅
    /// </summary>
    private Vector2 SnapTo4Direction(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return dir.x > 0 ? Vector2.right : Vector2.left;
        else
            return dir.y > 0 ? Vector2.up : Vector2.down;
    }

    private void FlipTowards(Vector2 targetPos)
    {
        Vector2 dir = targetPos - (Vector2)transform.position;
        if (dir.sqrMagnitude > 0.01f)
            UpdateDirection(dir);
    }

    // ─────────────────────────────────────────────────────────
    //  애니메이션 헬퍼
    // ─────────────────────────────────────────────────────────

    private void SetAnimMoving(bool moving)
    {
        if (animator == null) return;
        animator.SetBool(AnimIsMoving, moving);

        // 정지 시에도 마지막 방향 유지 (Idle 블렌드 트리에서 방향 결정)
        if (!moving)
        {
            float animX = Mathf.Abs(lastMoveDir.x);
            float animY = lastMoveDir.y;
            animator.SetFloat(AnimMoveX, animX);
            animator.SetFloat(AnimMoveY, animY);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  플레이어 탐색 — PlayerController가 있는 오브젝트 우선
    // ─────────────────────────────────────────────────────────

    private Transform FindPlayerTransform()
    {
        // 1순위: PlayerController 컴포넌트로 직접 찾기 (가장 확실)
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null)
        {
            Debug.Log($"[CompanionAgent] 플레이어 찾음 (PlayerController): {pc.gameObject.name} @ {pc.transform.position}");
            return pc.transform;
        }

        // 2순위: "Player" 태그 — PlayerController가 붙은 오브젝트 우선 필터
        GameObject[] tagged = GameObject.FindGameObjectsWithTag("Player");
        foreach (var go in tagged)
        {
            if (go.GetComponent<PlayerController>() != null)
            {
                Debug.Log($"[CompanionAgent] 플레이어 찾음 (태그+컴포넌트): {go.name}");
                return go.transform;
            }
        }

        // 3순위: 태그만으로 (PlayerVisual 등 잘못된 오브젝트일 수 있음)
        if (tagged.Length > 0)
        {
            Debug.LogWarning($"[CompanionAgent] ⚠ Player 태그로 찾았지만 PlayerController 없음: {tagged[0].name}");
            return tagged[0].transform;
        }

        Debug.LogError("[CompanionAgent] 플레이어를 찾을 수 없습니다!");
        return null;
    }

    // ─────────────────────────────────────────────────────────
    //  기즈모
    // ─────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        float range = data != null ? data.attackRange : 2f;
        Gizmos.DrawWireSphere(transform.position, range);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, followDistance);
    }
}
