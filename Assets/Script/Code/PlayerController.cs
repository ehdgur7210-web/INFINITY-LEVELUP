using UnityEngine;

/// <summary>
/// 향상된 플레이어 컨트롤러 (크리티컬 등급 + 데미지 팝업 연동 버전)
/// 
/// ★ 변경사항 (기존 대비):
///   - PerformMeleeAttack(): 크리티컬 등급 판정 → monster.Hit(damage, tier) 호출
///   - Fire(): Bullet2D에 크리티컬 등급 전달
///   - 스킬 공격들: 크리티컬 등급 판정 추가
///   - 나머지는 기존과 동일!
/// 
/// ★ 버그 수정:
///   - Flip(): localScale.x 반전 추가 (빙글빙글 회전 버그 수정)
///   - weaponPivot.rotation → localRotation 변경 (부모 오브젝트 회전 방지)
///   - FixedUpdate(): Move() 주석 해제 (이동 버그 수정)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerController : MonoBehaviour
{
    [Header("캐릭터 타입")]
    [SerializeField] private CharacterType characterType = CharacterType.Ranged;

    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("근거리 공격 설정")]
    [SerializeField] private float meleeAttackRange = 2f;
    [SerializeField] private float meleeAttackCooldown = 1.5f;
    [SerializeField] private int meleeDamage = 25;
    [SerializeField] private LayerMask enemyLayer;

    [Header("원거리 공격 설정")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 0.3f;
    [SerializeField] private bool autoFire = true;

    [Header("조준 설정")]
    [SerializeField] private bool autoAim = true;
    [SerializeField] private float aimRange = 15f;
    [SerializeField] private Transform weaponPivot;

    [Header("디버깅")]
    [SerializeField] private bool debugMode = false;

    // ═══ 4방향 애니메이션 ═══
    // Animator 파라미터:
    //   Direction (int): 0=Down, 1=Side, 2=Up
    //   IsMoving (bool): 이동 중
    //   Attack (trigger): 공격
    //   Death (trigger): 사망
    //   DeathDirection (int): 사망 시 방향 (0=Down, 1=Side, 2=Up)
    public enum FacingDirection { Down = 0, Side = 1, Up = 2 }

    /// <summary>현재 바라보는 방향 (애니메이션용)</summary>
    public FacingDirection CurrentDirection { get; private set; } = FacingDirection.Side;

    /// <summary>마지막으로 피격당한 방향 (사망 애니메이션용)</summary>
    [HideInInspector] public FacingDirection lastDamageDirection = FacingDirection.Down;

    // 컴포넌트
    private Rigidbody2D rb;
    private PlayerStats playerStats;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    public CameraController cameraController;

    // 상태
    private Vector2 moveInput;
    private Vector2 aimDirection = Vector2.right;
    private Transform currentTarget = null;
    private bool isFacingRight = true;
    private float nextFireTime = 0f;
    private float nextMeleeTime = 0f;

    // 애니메이션 상태 관리
    private float attackAnimEndTime = 0f;
    private bool isAttacking = false;

    // ★ 씬별 이동 모드 (false = 방치형/키네틱, true = 농장씬 이동 가능)
    private bool isMovementEnabled = false;

    void Awake()
    {
        // ★ 플레이어는 씬 간 유지 (DontDestroyOnLoad)
        // SceneTransitionManager에 등록하여 부모 계층에서 분리
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.RegisterPlayer(gameObject);
        }
        else
        {
            // SceneTransitionManager가 아직 없으면 직접 DontDestroyOnLoad
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        rb = GetComponent<Rigidbody2D>();
        playerStats = GetComponent<PlayerStats>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // localScale.x가 음수면 정규화 (flipX 방식으로 전환했으므로)
        Vector3 s = transform.localScale;
        if (s.x < 0f) { s.x = Mathf.Abs(s.x); transform.localScale = s; }

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            // 기본은 방치형 (키네틱)
            rb.isKinematic = true;
        }

        if (firePoint == null)
        {
            GameObject fp = new GameObject("FirePoint");
            fp.transform.SetParent(transform);
            fp.transform.localPosition = new Vector3(0.5f, 0, 0);
            firePoint = fp.transform;
        }

        if (weaponPivot == null)
        {
            weaponPivot = firePoint;
        }

        LoadCharacterData();
    }

    /// <summary>
    /// 씬 진입 시 이동 모드 전환
    /// true  = 농장씬 (WASD 이동 가능)
    /// false = 메인씬 (방치형, 키네틱)
    /// </summary>
    public void SetMovementMode(bool canMove)
    {
        isMovementEnabled = canMove;

        if (rb != null)
        {
            rb.isKinematic = !canMove;
            if (!canMove)
            {
                rb.velocity = Vector2.zero;
            }
        }

        // 농장씬에서는 autoFire/autoAim 비활성화 (전투 없음)
        if (canMove)
        {
            autoFire = false;
            autoAim = false;
        }
        else
        {
            // 메인씬 복귀 시 원래 설정 복원
            autoFire = true;
            autoAim = true;
        }

        Debug.Log($"[PlayerController] 이동 모드: {(canMove ? "농장(이동가능)" : "메인(방치형)")}");
    }

    void Start()
    {
        PlayerStats.OnPlayerDeath += HandleDeath;
        SetupCamera();

        // enemyLayer 미설정 시 자동 탐색
        if (enemyLayer.value == 0)
        {
            int monsterLayer = LayerMask.NameToLayer("Monster");
            if (monsterLayer < 0) monsterLayer = LayerMask.NameToLayer("Enemy");
            if (monsterLayer >= 0)
            {
                enemyLayer = 1 << monsterLayer;
                Debug.LogWarning($"[PlayerController] enemyLayer 미설정 → '{LayerMask.LayerToName(monsterLayer)}' 레이어 자동 할당");
            }
            else
            {
                Debug.LogError("[PlayerController] enemyLayer 미설정! Inspector에서 적 레이어를 지정하세요.");
            }
        }

        // Animator 초기 방향 설정 (Side = 횡스크롤 기본)
        if (animator != null)
            animator.SetFloat("Direction", (float)CurrentDirection);
    }

    void SetupCamera()
    {
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<CameraController>();
        }

        if (cameraController != null)
        {
            cameraController.SetFollowTarget(transform);
        }
    }

    private void LoadCharacterData()
    {
        SaveData data = GameDataBridge.CurrentData;
        if (data == null) return;

        characterType = (CharacterType)data.characterClassType;

        if (playerStats != null)
        {
            float health      = data.charBaseHealth  > 0f ? data.charBaseHealth  : 100f;
            float attack      = data.charBaseAttack  > 0f ? data.charBaseAttack  : 20f;
            float defense     = data.charBaseDefense > 0f ? data.charBaseDefense : 10f;
            float speed       = data.charBaseSpeed   > 0f ? data.charBaseSpeed   : 5f;
            float attackRange = data.charAttackRange > 0f ? data.charAttackRange : 10f;
            float attackSpeed = data.charAttackSpeed > 0f ? data.charAttackSpeed : 0.5f;

            moveSpeed = speed;
            aimRange  = attackRange;

            if (characterType == CharacterType.Melee)
            {
                meleeAttackRange    = attackRange;
                meleeAttackCooldown = attackSpeed;
                meleeDamage         = (int)attack;
            }
            else
            {
                fireRate = attackSpeed;
            }

            Debug.Log($"캐릭터 데이터 로드: {data.selectedCharacterName} ({characterType})");
        }
    }

    void Update()
    {
        if (playerStats != null && playerStats.IsDead)
            return;

        if (isMovementEnabled)
        {
            // 농장씬: 이동 + 조준 + 전투 모두 활성
            HandleInput();
            UpdateAim();
            UpdateCombat();
        }
        else
        {
            // 메인씬 방치형: weaponPivot 회전/Flip 없이 조준방향만 계산 후 전투
            UpdateAimIdleOnly();
            UpdateCombat();
        }

        UpdateAnimation();
    }

    void FixedUpdate()
    {
        if (playerStats != null && playerStats.IsDead)
            return;

        Move(); // ★ Bug Fix: 주석 해제 (이동 작동 안 하던 버그 수정)
    }

    private void HandleInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(horizontal, vertical).normalized;

        if (!autoFire)
        {
            if (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0))
            {
                if (characterType == CharacterType.Melee)
                    TryMeleeAttack();
                else
                    TryFire();
            }
        }
    }

    private void Move()
    {
        // 방치형 모드에서는 이동 불가
        if (!isMovementEnabled)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        rb.velocity = moveInput * moveSpeed;

        // 4방향: 이동 방향은 UpdateAnimation()에서 처리
    }

    private void UpdateAim()
    {
        if (autoAim)
        {
            FindClosestEnemy();

            if (currentTarget != null)
            {
                aimDirection = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
            }
        }
        else
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            aimDirection = (mousePos - (Vector2)transform.position).normalized;
        }

        if (SkillProjectileHandler.Instance != null)
        {
            SkillProjectileHandler.Instance.UpdateAimDirection(aimDirection);
        }

        if (characterType == CharacterType.Ranged && weaponPivot != null)
        {
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            // ★ Bug Fix: rotation → localRotation (부모 오브젝트까지 같이 돌던 버그 수정)
            weaponPivot.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }

    /// <summary>
    /// 방치형 모드용 조준 업데이트
    /// - 가장 가까운 적 탐색 + aimDirection 갱신
    /// - weaponPivot 회전 없음 → 캐릭터 뒤집힘/회전 방지
    /// </summary>
    private void UpdateAimIdleOnly()
    {
        FindClosestEnemy();

        if (currentTarget != null)
        {
            aimDirection = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;

            // 4방향: 적 방향으로 애니메이션 방향 갱신
            UpdateFacingDirection(aimDirection);
        }

        if (SkillProjectileHandler.Instance != null)
            SkillProjectileHandler.Instance.UpdateAimDirection(aimDirection);

        // weaponPivot 회전을 0으로 고정 (뒤집힘/회전 방지)
        if (weaponPivot != null)
            weaponPivot.localRotation = Quaternion.identity;
    }


    private void FindClosestEnemy()
    {
        Collider2D[] enemies = Physics2D.OverlapCircleAll(
            transform.position,
            aimRange,
            enemyLayer
        );

        if (enemies.Length == 0)
        {
            currentTarget = null;
            return;
        }

        float closestDistance = Mathf.Infinity;
        Transform closest = null;

        foreach (Collider2D enemy in enemies)
        {
            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = enemy.transform;
            }
        }

        currentTarget = closest;
    }

    private void UpdateCombat()
    {
        if (!autoFire || currentTarget == null)
            return;

        if (characterType == CharacterType.Melee)
        {
            float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
            if (distanceToTarget <= meleeAttackRange)
            {
                TryMeleeAttack();
            }
        }
        else
        {
            TryFire();
        }
    }

    private void TryMeleeAttack()
    {
        if (Time.time < nextMeleeTime)
            return;

        PerformMeleeAttack();
        nextMeleeTime = Time.time + meleeAttackCooldown;
    }

    private void PerformMeleeAttack()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            (Vector2)transform.position + aimDirection * (meleeAttackRange * 0.5f),
            meleeAttackRange * 0.5f,
            enemyLayer
        );

        foreach (Collider2D hit in hits)
        {
            float damage = PlayerStats.Instance.CalculateDamageWithTier(out int critTier);

            Monster monster = hit.GetComponent<Monster>();
            if (monster != null)
            {
                monster.Hit(damage, critTier);
            }
            else
            {
                IHitable hitable = hit.GetComponent<IHitable>();
                if (hitable != null)
                {
                    hitable.Hit(damage);

                    if (DamagePopupManager.Instance != null)
                    {
                        DamagePopupManager.Instance.ShowDamage(
                            hit.transform.position, damage, critTier
                        );
                    }
                }
            }
        }

        SoundManager.Instance?.PlayMeleeAttack();
        TriggerAttackAnimation();

        if (debugMode)
        {
            Debug.Log($"[ATTACK] Melee 공격 실행!");
        }
    }

    private void TryFire()
    {
        if (Time.time < nextFireTime)
            return;

        Fire();
        nextFireTime = Time.time + fireRate;
    }

    private void Fire()
    {
        SoundManager.Instance?.PlayBulletFire();

        if (PlayerStats.Instance == null)
        {
            Debug.LogError("PlayerStats.Instance가 null입니다! 씬에 PlayerStats 컴포넌트를 확인하세요.");
            return;
        }

        float damage = PlayerStats.Instance.CalculateDamageWithTier(out int critTier);

        GameObject bullet = null;

        if (PoolManager.Instance != null)
        {
            bullet = PoolManager.Instance.SpawnFromPool(
                "Bullet",
                firePoint.position,
                Quaternion.identity
            );
        }

        if (bullet == null && bulletPrefab != null)
        {
            bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        }

        if (bullet != null)
        {
            Bullet2D bulletScript = bullet.GetComponent<Bullet2D>();
            if (bulletScript != null)
            {
                bulletScript.SetDamage((int)damage);
                bulletScript.SetCriticalTier(critTier);
                bulletScript.Initialize(aimDirection);
            }
        }

        TriggerAttackAnimation();

        if (debugMode)
        {
            Debug.Log($"[ATTACK] Ranged 공격! 데미지:{damage:F0} 크리티컬등급:{critTier}");
        }
    }

    private void TriggerAttackAnimation()
    {
        if (animator == null) return;

        // 공격 전 방향 갱신
        if (currentTarget != null)
        {
            Vector2 dir = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
            UpdateFacingDirection(dir);
            animator.SetFloat("Direction", (float)CurrentDirection);
        }

        animator.SetTrigger("Attack");

        isAttacking = true;
        attackAnimEndTime = Time.time + 0.5f;
    }

    private void UpdateAnimation()
    {
        if (isAttacking && Time.time >= attackAnimEndTime)
            isAttacking = false;

        // ── 4방향 계산 ──
        // 적이 있으면 적 방향, 이동 중이면 이동 방향, 없으면 유지
        Vector2 dirRef = Vector2.zero;
        if (currentTarget != null)
            dirRef = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
        else if (moveInput.magnitude > 0.1f)
            dirRef = moveInput;

        if (dirRef.magnitude > 0.1f)
            UpdateFacingDirection(dirRef);

        // ── Animator 파라미터 전달 ──
        if (animator == null) return;

        animator.SetFloat("Direction", (float)CurrentDirection);
        bool isMoving = moveInput.magnitude > 0.1f;
        animator.SetBool("IsMoving", isMoving && !isAttacking);
    }

    /// <summary>
    /// 방향 벡터로부터 FacingDirection을 결정하고 flipX를 적용합니다.
    /// 1행=위(Up), 2행=사이드(Side, 기본 왼쪽), 4행=아래(Down)
    /// 좌우는 spriteRenderer.flipX로 구분합니다.
    /// </summary>
    public void UpdateFacingDirection(Vector2 dir)
    {
        // 각도 기반 3방향 판정
        // 15도 기준 → 위/아래 각 75도, 사이드 30도 (횡스크롤에서도 위/아래 잘 걸리도록)
        float angle = Mathf.Atan2(dir.y, Mathf.Abs(dir.x)) * Mathf.Rad2Deg;
        // angle: 90=정위, -90=정아래, 0=정옆

        if (angle > 15f)
            CurrentDirection = FacingDirection.Up;      // 15~90도: 위
        else if (angle < -15f)
            CurrentDirection = FacingDirection.Down;     // -15~-90도: 아래
        else
            CurrentDirection = FacingDirection.Side;     // -15~15도: 옆

        // 좌우 flipX 처리
        if (Mathf.Abs(dir.x) > 0.01f && spriteRenderer != null)
        {
            // 2행이 왼쪽 기본 → 오른쪽이면 flipX = true
            spriteRenderer.flipX = dir.x > 0f;
            isFacingRight = dir.x > 0f;
        }
    }

    /// <summary>공격자 위치로부터 피격 방향을 기록 (사망 애니메이션용)</summary>
    public void SetDamageDirection(Vector2 attackerPosition)
    {
        Vector2 dir = (attackerPosition - (Vector2)transform.position).normalized;

        float angle = Mathf.Atan2(dir.y, Mathf.Abs(dir.x)) * Mathf.Rad2Deg;
        if (angle > 15f)
            lastDamageDirection = FacingDirection.Up;
        else if (angle < -15f)
            lastDamageDirection = FacingDirection.Down;
        else
            lastDamageDirection = FacingDirection.Side;

        // 죽음 시 공격자 방향으로 flipX
        if (Mathf.Abs(dir.x) > 0.01f && spriteRenderer != null)
            spriteRenderer.flipX = dir.x > 0f;
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        // flipX 방식으로 전환 (localScale 반전은 4방향 애니메이션과 충돌)
        if (spriteRenderer != null)
            spriteRenderer.flipX = isFacingRight;
        else
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (isFacingRight ? 1f : -1f);
            transform.localScale = scale;
        }
    }

    public void SetCharacterType(CharacterType newType)
    {
        characterType = newType;
        Debug.Log($"캐릭터 타입 변경: {newType}");
    }

    private void HandleDeath()
    {
        if (animator != null)
        {
            // 피격 방향으로 사망 애니메이션
            animator.SetFloat("Direction", (float)lastDamageDirection);
            animator.SetTrigger("Death");
        }
    }

    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        PlayerStats.OnPlayerDeath -= HandleDeath;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>씬 전환 시 PlayerController 자동 on/off — FarmScene에서는 비활성</summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        bool isMainScene = scene.name == "MainScene";

        // 전투 관련 초기화
        if (isMainScene)
        {
            // MainScene 복귀 → 전투 재개
            autoFire = true;
            currentTarget = null;
            if (rb != null) rb.velocity = Vector2.zero;
            Debug.Log("[PlayerController] MainScene 복귀 — 전투 재개");
        }
        else
        {
            // FarmScene 등 → 전투 중지, 총알 발사 중지
            autoFire = false;
            currentTarget = null;
            if (rb != null) rb.velocity = Vector2.zero;
            Debug.Log($"[PlayerController] {scene.name} 진입 — 전투 중지");
        }
    }

    #region Gizmos

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aimRange);

        if (characterType == CharacterType.Melee)
        {
            Gizmos.color = Color.red;
            Vector3 attackPos = transform.position + (Vector3)aimDirection * (meleeAttackRange * 0.5f);
            Gizmos.DrawWireSphere(attackPos, meleeAttackRange * 0.5f);
        }

        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }

    #endregion

    #region 스킬 공격 (SkillManager에서 호출)

    public void PerformSkillMelee(SkillData skillData, float damageValue)
    {
        SoundManager.Instance?.PlayMeleeAttack();

        float attackRange = skillData.range > 0 ? skillData.range : meleeAttackRange;
        float attackRadius = skillData.areaRadius > 0 ? skillData.areaRadius : attackRange * 0.5f;

        Vector2 attackCenter = (Vector2)transform.position + aimDirection * (attackRange * 0.5f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, attackRadius, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            int critTier = PlayerStats.Instance.RollCriticalTier();

            float finalDmg = damageValue;
            switch (critTier)
            {
                case 1: finalDmg *= (PlayerStats.Instance.criticalDamage / 100f); break;
                case 2: finalDmg *= (PlayerStats.Instance.superCriticalDamage / 100f); break;
                case 3: finalDmg *= (PlayerStats.Instance.ultraCriticalDamage / 100f); break;
            }

            Monster monster = hit.GetComponent<Monster>();
            if (monster != null)
            {
                monster.Hit(finalDmg, critTier);
            }
            else
            {
                IHitable hitable = hit.GetComponent<IHitable>();
                if (hitable != null)
                {
                    hitable.Hit(finalDmg);
                    if (DamagePopupManager.Instance != null)
                        DamagePopupManager.Instance.ShowDamage(hit.transform.position, finalDmg, critTier);
                }
            }
        }

        SpawnSkillEffect(skillData, attackCenter);
        TriggerAttackAnimation();

        Debug.Log($"[SkillMelee] {skillData.skillName} — {hits.Length}개 적 공격");
    }

    public void PerformSkillRanged(SkillData skillData, float damageValue)
    {
        SoundManager.Instance?.PlayBulletFire();

        int critTier = PlayerStats.Instance.RollCriticalTier();
        float finalDmg = damageValue;
        switch (critTier)
        {
            case 1: finalDmg *= (PlayerStats.Instance.criticalDamage / 100f); break;
            case 2: finalDmg *= (PlayerStats.Instance.superCriticalDamage / 100f); break;
            case 3: finalDmg *= (PlayerStats.Instance.ultraCriticalDamage / 100f); break;
        }

        GameObject bullet = null;

        if (PoolManager.Instance != null)
        {
            bullet = PoolManager.Instance.SpawnFromPool(
                "Bullet",
                firePoint.position,
                Quaternion.identity
            );
        }

        if (bullet == null && bulletPrefab != null)
        {
            bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        }

        if (bullet != null)
        {
            Bullet2D bulletScript = bullet.GetComponent<Bullet2D>();
            if (bulletScript != null)
            {
                bulletScript.SetDamage((int)finalDmg);
                bulletScript.SetCriticalTier(critTier);
                bulletScript.Initialize(aimDirection);
            }
        }

        if (skillData.attackEffectPrefab != null)
        {
            GameObject muzzleEffect = Instantiate(skillData.attackEffectPrefab, firePoint.position, Quaternion.identity);
            Destroy(muzzleEffect, 1f);
        }

        TriggerAttackAnimation();
    }

    public void PerformSkillMagic(SkillData skillData, float damageValue)
    {
        SoundManager.Instance?.PlayMagicCast();

        int critTier = PlayerStats.Instance.RollCriticalTier();
        float finalDmg = damageValue;
        switch (critTier)
        {
            case 1: finalDmg *= (PlayerStats.Instance.criticalDamage / 100f); break;
            case 2: finalDmg *= (PlayerStats.Instance.superCriticalDamage / 100f); break;
            case 3: finalDmg *= (PlayerStats.Instance.ultraCriticalDamage / 100f); break;
        }

        GameObject fireball = null;

        if (PoolManager.Instance != null)
        {
            fireball = PoolManager.Instance.SpawnFromPool(
                "Fireball",
                firePoint.position,
                Quaternion.identity
            );
        }

        if (fireball == null && skillData.attackEffectPrefab != null)
        {
            fireball = Instantiate(skillData.attackEffectPrefab, firePoint.position, Quaternion.identity);
        }

        if (fireball == null)
        {
            fireball = CreateDefaultFireball();
        }

        if (fireball != null)
        {
            FireballProjectile fb = fireball.GetComponent<FireballProjectile>();
            if (fb != null)
            {
                fb.Initialize(aimDirection, (int)finalDmg, skillData.areaRadius > 0 ? skillData.areaRadius : 1.5f);
            }
        }

        TriggerAttackAnimation();
    }

    private void SpawnSkillEffect(SkillData skillData, Vector2 position)
    {
        if (skillData.attackEffectPrefab != null)
        {
            GameObject effect = Instantiate(skillData.attackEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 1.5f);
        }
        else
        {
            CreateDefaultSlashEffect(position);
        }
    }

    private GameObject CreateDefaultSlashEffect(Vector2 position)
    {
        GameObject effect = new GameObject("DefaultSlashEffect");
        effect.transform.position = position;

        SpriteRenderer sr = effect.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 0.9f, 0.2f);

        Texture2D tex = new Texture2D(32, 32);
        for (int x = 0; x < 32; x++)
            for (int y = 0; y < 32; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                tex.SetPixel(x, y, dist < 14 ? Color.white : Color.clear);
            }
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        sr.transform.localScale = Vector3.one * 1.5f;

        Destroy(effect, 0.4f);
        return effect;
    }

    private GameObject CreateDefaultFireball()
    {
        GameObject fireball = new GameObject("DefaultFireball");
        fireball.transform.position = firePoint.position;

        SpriteRenderer sr = fireball.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 0.5f, 0f);

        Texture2D tex = new Texture2D(32, 32);
        for (int x = 0; x < 32; x++)
            for (int y = 0; y < 32; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                tex.SetPixel(x, y, dist < 14 ? Color.white : Color.clear);
            }
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        sr.transform.localScale = Vector3.one * 0.6f;

        Rigidbody2D newRb = fireball.AddComponent<Rigidbody2D>();
        newRb.gravityScale = 0f;

        fireball.AddComponent<FireballProjectile>();

        return fireball;
    }

    #endregion
}