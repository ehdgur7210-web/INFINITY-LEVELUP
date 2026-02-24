using UnityEngine;

/// <summary>
/// 향상된 플레이어 컨트롤러 (크리티컬 등급 + 데미지 팝업 연동 버전)
/// 
/// ★ 변경사항 (기존 대비):
///   - PerformMeleeAttack(): 크리티컬 등급 판정 → monster.Hit(damage, tier) 호출
///   - Fire(): Bullet2D에 크리티컬 등급 전달
///   - 스킬 공격들: 크리티컬 등급 판정 추가
///   - 나머지는 기존과 동일!
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

    // 컴포넌트
    private Rigidbody2D rb;
    private PlayerStats playerStats;
    private SPUMEquipmentVisualSystem visualSystem;
    private Animator animator;
    private SPUM_Prefabs spumPrefabs;
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

    // SPUM 초기화 완료 여부
    private bool spumInitialized = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerStats = GetComponent<PlayerStats>();
        visualSystem = GetComponent<SPUMEquipmentVisualSystem>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
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

    void Start()
    {
        spumPrefabs = GetComponentInChildren<SPUM_Prefabs>();

        if (spumPrefabs != null)
        {
            animator = spumPrefabs._anim;

            if (!spumPrefabs.allListsHaveItemsExist())
            {
                spumPrefabs.PopulateAnimationLists();
            }
            spumPrefabs.OverrideControllerInit();

            spumInitialized = true;
            Debug.Log("[PlayerController] SPUM 초기화 완료");
        }
        else
        {
            animator = GetComponent<Animator>();
            Debug.LogWarning("[PlayerController] SPUM_Prefabs를 찾을 수 없습니다. 기본 Animator를 사용합니다.");
        }

        PlayerStats.OnPlayerDeath += HandleDeath;

        if (visualSystem != null)
        {
            visualSystem.UpdateAllEquipmentVisuals();
        }

        SetupCamera();
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
        if (PlayerPrefs.HasKey("CharacterType"))
        {
            characterType = (CharacterType)PlayerPrefs.GetInt("CharacterType");

            if (playerStats != null)
            {
                float health = PlayerPrefs.GetFloat("CharacterHealth", 100f);
                float attack = PlayerPrefs.GetFloat("CharacterAttack", 20f);
                float defense = PlayerPrefs.GetFloat("CharacterDefense", 10f);
                float speed = PlayerPrefs.GetFloat("CharacterSpeed", 5f);
                float attackRange = PlayerPrefs.GetFloat("CharacterAttackRange", 10f);
                float attackSpeed = PlayerPrefs.GetFloat("CharacterAttackSpeed", 0.5f);

                moveSpeed = speed;
                aimRange = attackRange;

                if (characterType == CharacterType.Melee)
                {
                    meleeAttackRange = attackRange;
                    meleeAttackCooldown = attackSpeed;
                    meleeDamage = (int)attack;
                }
                else
                {
                    fireRate = attackSpeed;
                }

                Debug.Log($"캐릭터 데이터 로드: {PlayerPrefs.GetString("SelectedCharacter", "Unknown")} ({characterType})");
            }
        }
    }

    void Update()
    {
        if (playerStats != null && playerStats.IsDead)
            return;

        HandleInput();
        UpdateAim();
        UpdateCombat();
        UpdateAnimation();
    }

    void FixedUpdate()
    {
        if (playerStats != null && playerStats.IsDead)
            return;

        //Move();
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
        rb.velocity = moveInput * moveSpeed;

        if (moveInput.x > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (moveInput.x < 0 && isFacingRight)
        {
            Flip();
        }
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
            weaponPivot.rotation = Quaternion.Euler(0, 0, angle);
        }
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

    /// <summary>
    /// ★★★ 근거리 공격 (크리티컬 등급 + 데미지 팝업 추가) ★★★
    /// 
    /// 변경 전: hitable.Hit(meleeDamage);
    /// 변경 후: 크리티컬 등급 판정 → monster.Hit(damage, tier) → 팝업 자동 표시
    /// </summary>
    private void PerformMeleeAttack()
    {
        // 공격 범위 안의 적 감지
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            (Vector2)transform.position + aimDirection * (meleeAttackRange * 0.5f),
            meleeAttackRange * 0.5f,
            enemyLayer
        );

        foreach (Collider2D hit in hits)
        {
            // ★ PlayerStats에서 크리티컬 등급 포함 데미지 계산
            // CalculateDamageWithTier()는 공격력 + 크리티컬 등급을 한번에 처리
            // out int critTier → 0=일반, 1=크리티컬(빨강), 2=슈퍼(주황), 3=울트라(노랑)
            float damage = PlayerStats.Instance.CalculateDamageWithTier(out int critTier);

            // ★ Monster 컴포넌트가 있으면 크리티컬 등급 포함 Hit 호출
            // → Monster.Hit(damage, tier) 안에서 데미지 팝업이 자동으로 표시됨
            Monster monster = hit.GetComponent<Monster>();
            if (monster != null)
            {
                // 크리티컬 등급 포함 버전 (팝업 색상이 등급에 따라 달라짐)
                monster.Hit(damage, critTier);
            }
            else
            {
                // Monster가 아닌 IHitable (기존 호환)
                IHitable hitable = hit.GetComponent<IHitable>();
                if (hitable != null)
                {
                    hitable.Hit(damage);

                    // ★ Monster가 아니어도 팝업은 직접 띄워줌
                    if (DamagePopupManager.Instance != null)
                    {
                        DamagePopupManager.Instance.ShowDamage(
                            hit.transform.position, damage, critTier
                        );
                    }
                }
            }
        }

        // ★ 근거리 공격 효과음
        SoundManager.Instance?.PlayMeleeAttack();
        // 공격 애니메이션
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

    /// <summary>
    /// ★★★ 원거리 공격 (크리티컬 등급을 Bullet에 전달) ★★★
    /// 
    /// 총알이 몬스터에 맞을 때 크리티컬 등급이 적용되도록
    /// Bullet2D에 크리티컬 정보를 미리 전달
    /// </summary>
    private void Fire()
    {
        // ★ 총알 발사 효과음
        SoundManager.Instance?.PlayBulletFire();
        if (PlayerStats.Instance == null)
        {
            Debug.LogError("PlayerStats.Instance가 null입니다! 씬에 PlayerStats 컴포넌트를 확인하세요.");
            return;
        }
        // ★ 발사 시점에 크리티컬 등급 미리 판정
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
                // ★ 총알에 데미지와 크리티컬 등급 전달
                bulletScript.SetDamage((int)damage);
                bulletScript.SetCriticalTier(critTier);  // ← 새로 추가할 메서드
                bulletScript.Initialize(aimDirection);
            }
        }

        TriggerAttackAnimation();

        if (debugMode)
        {
            Debug.Log($"[ATTACK] Ranged 공격! 데미지:{damage:F0} 크리티컬등급:{critTier}");
        }
    }

    /// <summary>
    /// ★ Animator 파라미터 직접 제어
    /// </summary>
    private void TriggerAttackAnimation()
    {
        if (animator == null) return;

        animator.SetTrigger("2_Attack");

        isAttacking = true;
        attackAnimEndTime = Time.time + 0.5f;
    }

    /// <summary>
    /// 애니메이션 상태 업데이트
    /// </summary>
    private void UpdateAnimation()
    {
        if (animator == null) return;

        if (isAttacking && Time.time >= attackAnimEndTime)
        {
            isAttacking = false;
        }

        if (spumInitialized)
        {
            bool isMoving = moveInput.magnitude > 0.1f;
            animator.SetBool("1_Move", isMoving && !isAttacking);
        }
        else
        {
            animator.SetFloat("Speed", moveInput.magnitude);
            animator.SetBool("IsShooting", currentTarget != null);
            animator.SetBool("IsMelee", characterType == CharacterType.Melee);
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        if (spumInitialized && spumPrefabs != null)
        {
            spumPrefabs.transform.localScale = isFacingRight
                ? new Vector3(-1, 1, 1)
                : new Vector3(1, 1, 1);
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
            animator.SetTrigger("4_Death");
        }
    }

    void OnDisable()
    {
        PlayerStats.OnPlayerDeath -= HandleDeath;
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

    /// <summary>
    /// ★ 스킬 근거리 공격 (크리티컬 등급 추가)
    /// </summary>
    public void PerformSkillMelee(SkillData skillData, float damageValue)
    {
        // ★ 스킬 근거리 공격 효과음
        SoundManager.Instance?.PlayMeleeAttack();
        float attackRange = skillData.range > 0 ? skillData.range : meleeAttackRange;
        float attackRadius = skillData.areaRadius > 0 ? skillData.areaRadius : attackRange * 0.5f;

        Vector2 attackCenter = (Vector2)transform.position + aimDirection * (attackRange * 0.5f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, attackRadius, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            // ★ 크리티컬 등급 판정
            int critTier = PlayerStats.Instance.RollCriticalTier();

            // ★ 등급에 따라 데미지 배율 적용
            float finalDmg = damageValue;
            switch (critTier)
            {
                case 1: finalDmg *= (PlayerStats.Instance.criticalDamage / 100f); break;
                case 2: finalDmg *= (PlayerStats.Instance.superCriticalDamage / 100f); break;
                case 3: finalDmg *= (PlayerStats.Instance.ultraCriticalDamage / 100f); break;
            }

            // ★ Monster면 크리티컬 등급 포함 Hit (팝업 자동)
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

    /// <summary>
    /// ★ 스킬 원거리 공격 (크리티컬 등급 추가)
    /// </summary>
    public void PerformSkillRanged(SkillData skillData, float damageValue)
    {
        // ★ 스킬 원거리 공격 효과음
        SoundManager.Instance?.PlayBulletFire();
        // ★ 크리티컬 등급 판정
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
                bulletScript.SetCriticalTier(critTier);  // ★ 크리티컬 등급 전달
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

    /// <summary>
    /// ★ 스킬 마법 공격 (크리티컬 등급 추가)
    /// </summary>
    public void PerformSkillMagic(SkillData skillData, float damageValue)
    {
        // ★ 마법 시전 효과음
        SoundManager.Instance?.PlayMagicCast();
        // ★ 크리티컬 등급 판정
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