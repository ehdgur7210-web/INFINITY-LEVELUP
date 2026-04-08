using UnityEngine;
using System.Collections;

public class Monster : MonoBehaviour, IHitable
{
    [Header("몬스터 정보")]
    public string monsterName = "Monster";
    public int maxHp = 100;
    public int currentHp;
    public int damage = 10;
    [SerializeField] private float moveSpeed = 2f;

    // ★ PoolManager에 등록한 tag와 일치해야 함
    [SerializeField] private string poolTag = "Monster";
    public string PoolTag => poolTag;

    [Header("이동 설정")]
    [SerializeField] private bool usePlayerTracking = true;
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private Vector2 moveDirection = Vector2.left;

    [Header("공격 설정")]
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private LayerMask targetLayer;
    private float lastAttackTime = 0f;

    [Header("드롭 보상")]
    [SerializeField] private int goldDrop = 10;
    [SerializeField] private int expDrop = 5;
    [SerializeField] private int equipmentTickets = 1;
    [Range(0f, 100f)]
    [Tooltip("장비 티켓 드랍 확률 (%)")]
    [SerializeField] private float equipmentTicketDropChance = 50f;

    [Header("아이템 드롭")]
    [Tooltip("드롭 가능한 아이템 목록")]
    [SerializeField] private ItemDropData[] itemDropTable;

    [Header("사망 파티클")]
    [Tooltip("사망 시 생성할 파티클 프리팹 (없으면 기본 이펙트 사용)")]
    [SerializeField] private GameObject deathParticlePrefab;
    [Tooltip("파티클 지속 시간")]
    [SerializeField] private float deathParticleDuration = 1f;

    // ★ 동시 사망 이펙트/사운드 throttle (전역 쿨다운)
    [Tooltip("이 시간(초) 안에 사망한 다른 몬스터의 사운드/파티클 스킵")]
    [SerializeField] private float deathEffectCooldown = 0.08f;
    private static float _lastDeathEffectTime = -10f;

    // ─── Getter/Setter ───
    public int GoldDrop { get { return goldDrop; } set { goldDrop = value; } }
    public int ExpDrop { get { return expDrop; } set { expDrop = value; } }
    public ItemDropData[] ItemDropTable { get { return itemDropTable; } set { itemDropTable = value; } }

    // ─── 컴포넌트 ───
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Collider2D col;
    private MonsterHealthBar healthBar;

    // ─── 상태 ───
    private bool isDead = false;
    private bool isAttacking = false;
    private Transform currentTarget = null;
    private Transform player = null;
    private Coroutine flashCoroutine;

    // ─── 4방향 ───
    private Vector2 facingDirection = Vector2.down; // 현재 바라보는 방향

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        healthBar = GetComponent<MonsterHealthBar>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.drag = 0f;
            rb.angularDrag = 0f;
        }
    }

    void Start()
    {
        currentHp = maxHp;
        FindPlayer();
    }

    // ★ 풀에서 꺼낼 때(SetActive true) 자동 호출 → 상태 초기화
    void OnEnable()
    {
        isDead = false;
        isAttacking = false;
        currentTarget = null;
        lastAttackTime = 0f;
        flashCoroutine = null;
        currentHp = maxHp;

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 1f;
            spriteRenderer.color = c;
            spriteRenderer.enabled = true;
        }
        if (rb != null) { rb.simulated = true; rb.velocity = Vector2.zero; }
        if (col != null) col.enabled = true;

        // ★ 4방향 애니메이터 초기화
        SetAnimState("Idle");
        UpdateDirectionParams(Vector2.down);

        FindPlayer();
    }

    private float findPlayerTimer = 0f;

    public void Update()
    {
        if (isDead) return;

        // player null이면 1초마다 재탐색
        if (usePlayerTracking && player == null)
        {
            findPlayerTimer += Time.deltaTime;
            if (findPlayerTimer >= 1f)
            {
                findPlayerTimer = 0f;
                FindPlayer();
            }
        }

        DetectTarget();

        if (currentTarget != null) Attack();
        else Move();
    }

    public void SetSpeed(float newSpeed) { moveSpeed = newSpeed; }
    public float GetSpeed() { return moveSpeed; }

    private void FindPlayer()
    {
        if (PlayerStats.Instance != null) { player = PlayerStats.Instance.transform; return; }
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) { player = playerObj.transform; return; }
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null) { player = pc.transform; }
    }

    // ════════════════════════════════════════
    //  이동 (4방향)
    // ════════════════════════════════════════

    private void Move()
    {
        isAttacking = false;
        Vector2 direction = moveDirection;

        if (usePlayerTracking && player != null)
        {
            float dist = Vector2.Distance(transform.position, player.position);
            if (dist <= detectionRange)
            {
                direction = (player.position - transform.position).normalized;
            }
            else
            {
                // 감지 범위 밖 → 대기
                rb.velocity = Vector2.zero;
                SetAnimState("Idle");
                return;
            }
        }

        rb.velocity = direction * moveSpeed;
        UpdateDirectionParams(direction);
        SetAnimState("Move");
    }

    // ════════════════════════════════════════
    //  공격 (4방향)
    // ════════════════════════════════════════

    private void DetectTarget()
    {
        Collider2D[] targets = Physics2D.OverlapCircleAll(transform.position, attackRange, targetLayer);
        currentTarget = targets.Length > 0 ? targets[0].transform : null;
    }

    private void Attack()
    {
        isAttacking = true;
        rb.velocity = Vector2.zero;

        // 공격 방향 = 대상을 향한 방향
        if (currentTarget != null)
        {
            Vector2 dir = (currentTarget.position - transform.position).normalized;
            UpdateDirectionParams(dir);
        }

        SetAnimState("Attack");

        if (Time.time - lastAttackTime < attackCooldown) return;

        PerformAttack();
        lastAttackTime = Time.time;
    }

    private void PerformAttack()
    {
        if (currentTarget == null) return;

        PlayerStats playerStats = currentTarget.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.Hit(damage, (Vector2)transform.position);
            return;
        }

        IHitable hitable = currentTarget.GetComponent<IHitable>();
        if (hitable != null) hitable.Hit(damage);
    }

    // ════════════════════════════════════════
    //  3방향 애니메이션 + flipX (상/하/횡)
    // ════════════════════════════════════════

    /// <summary>
    /// 3방향 판정 + flipX로 좌우 처리
    ///
    /// Animator 파라미터:
    ///   float DirY — 0=횡, 1=위, -1=아래
    ///
    /// SpriteRenderer.flipX로 좌우 반전
    ///
    /// Blend Tree 구조:
    ///   DirY=-1 → Down 모션
    ///   DirY= 0 → Side 모션 (flipX로 좌우)
    ///   DirY= 1 → Up 모션
    /// </summary>
    private void UpdateDirectionParams(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.01f) return;
        facingDirection = dir;

        // ★ 좌우 flip
        if (spriteRenderer != null && Mathf.Abs(dir.x) > 0.1f)
            spriteRenderer.flipX = dir.x < 0;

        if (animator != null)
        {
            if (Mathf.Abs(dir.y) > Mathf.Abs(dir.x))
            {
                // 상하가 더 강함
                animator.SetFloat("DirY", dir.y > 0 ? 1f : -1f);
            }
            else
            {
                // 좌우가 더 강함 → 횡 모션
                animator.SetFloat("DirY", 0f);
            }
        }
    }

    /// <summary>
    /// 상태 전환: "Idle", "Move", "Attack"
    /// Animator에 int State 파라미터 사용 (0=Idle, 1=Move, 2=Attack)
    /// </summary>
    private void SetAnimState(string state)
    {
        if (animator == null) return;

        switch (state)
        {
            case "Idle":    animator.SetInteger("State", 0); break;
            case "Move":    animator.SetInteger("State", 1); break;
            case "Attack":  animator.SetInteger("State", 2); break;
        }

        // bool 파라미터 (기존 호환)
        animator.SetBool("IsIdle", state == "Idle");
        animator.SetBool("IsMoving", state == "Move");
        animator.SetBool("IsAttacking", state == "Attack");
    }

    // ════════════════════════════════════════
    //  피격
    // ════════════════════════════════════════

    public void Hit(float damageAmount) { Hit(damageAmount, 0, null); }

    public void Hit(float damageAmount, int criticalTier) { Hit(damageAmount, criticalTier, null); }

    /// <summary>
    /// 피격 처리 (스킬 히트 이펙트 지원)
    /// hitEffectPrefab이 있으면 몬스터 위치에 해당 이펙트 생성 (번개, 폭발 등)
    /// </summary>
    public void Hit(float damageAmount, int criticalTier, GameObject hitEffectPrefab)
    {
        if (isDead) return;

        currentHp -= (int)damageAmount;

        SoundManager.Instance?.PlayMonsterHit();

        if (DamagePopupManager.Instance != null)
            DamagePopupManager.Instance.ShowDamage(transform.position, damageAmount, criticalTier);

        if (healthBar != null) healthBar.ShowTemporarily();

        // ★ 스킬 히트 이펙트 (번개 등) — 전달된 프리팹이 있으면 몬스터 위치에 생성
        if (hitEffectPrefab != null)
        {
            GameObject fx = Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            Destroy(fx, 2f);
        }

        // 피격 반짝이
        if (spriteRenderer != null)
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                Color r = spriteRenderer.color; r.a = 1f;
                spriteRenderer.color = r;
            }
            flashCoroutine = StartCoroutine(FlashEffect());
        }

        if (currentHp <= 0) Die();
    }

    private IEnumerator FlashEffect()
    {
        int flashCount = 2;
        float flashDuration = 0.06f;
        float minAlpha = 0.3f;

        Color original = spriteRenderer.color;
        original.a = 1f;

        for (int i = 0; i < flashCount; i++)
        {
            Color c = original; c.a = minAlpha;
            spriteRenderer.color = c;
            yield return new WaitForSeconds(flashDuration);
            spriteRenderer.color = original;
            yield return new WaitForSeconds(flashDuration);
        }

        spriteRenderer.color = original;
        flashCoroutine = null;
    }

    public void TakeDamage(float damageAmount) { Hit(damageAmount); }

    // ════════════════════════════════════════
    //  사망 (파티클 이펙트로 사라짐)
    // ════════════════════════════════════════

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        // ★ 동시 사망 throttle: 짧은 시간 내 다른 몬스터가 이미 이펙트 재생했으면 스킵
        bool playEffect = (Time.unscaledTime - _lastDeathEffectTime) >= deathEffectCooldown;
        if (playEffect)
        {
            _lastDeathEffectTime = Time.unscaledTime;
            SoundManager.Instance?.PlayMonsterDeathSound();
        }

        // 반짝이 중단
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
            if (spriteRenderer != null) { Color c = spriteRenderer.color; c.a = 1f; spriteRenderer.color = c; }
        }

        rb.velocity = Vector2.zero;
        rb.simulated = false;
        if (col != null) col.enabled = false;

        TutorialManager.Instance?.OnActionCompleted("KillMonster");

        // ★ 사망 애니메이션 없음 — 파티클 이펙트로 사라짐 (throttle 적용)
        if (playEffect)
            SpawnDeathParticle();

        DropReward();

        if (AchievementSystem.Instance != null)
        {
            AchievementSystem.Instance.UpdateAchievementProgress(AchievementType.KillMonsters, monsterName, 1);
            AchievementSystem.Instance.UpdateAchievementProgress(AchievementType.KillMonsters, "", 1);
        }

        if (QuestManager.Instance != null)
            QuestManager.Instance.OnMonsterKilled(monsterName);

        if (GameManager.Instance != null)
            GameManager.Instance.OnMonsterDeath(transform.position);

        // ★ 스프라이트 즉시 숨기고 짧은 대기 후 풀 반환
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        if (gameObject.activeInHierarchy)
            StartCoroutine(ReturnToPoolAfterDeathEffect());
        else
            ReturnToPoolImmediate();
    }

    /// <summary>사망 파티클 생성</summary>
    private void SpawnDeathParticle()
    {
        if (deathParticlePrefab != null)
        {
            GameObject fx = Instantiate(deathParticlePrefab, transform.position, Quaternion.identity);
            Destroy(fx, deathParticleDuration);
        }
        else
        {
            // ★ 기본 파티클 없으면 간단한 스케일 축소 효과로 대체
            // (프리팹을 Inspector에서 연결하는 것을 권장)
            Debug.Log($"[Monster] {monsterName} 사망 — deathParticlePrefab 미설정");
        }
    }

    /// <summary>파티클 재생 시간 후 풀 반환</summary>
    private IEnumerator ReturnToPoolAfterDeathEffect()
    {
        // 파티클 재생 동안 대기 (스프라이트는 이미 숨겨진 상태)
        yield return new WaitForSeconds(0.3f);

        WaveSpawner spawner = WaveSpawner.Instance;
        if (spawner != null) spawner.OnMonsterKilled(gameObject);

        if (PoolManager.Instance != null)
            PoolManager.Instance.ReturnToPool(poolTag, gameObject);
        else
            Destroy(gameObject);
    }

    private void ReturnToPoolImmediate()
    {
        WaveSpawner spawner = WaveSpawner.Instance;
        if (spawner != null) spawner.OnMonsterKilled(gameObject);

        if (PoolManager.Instance != null)
            PoolManager.Instance.ReturnToPool(poolTag, gameObject);
        else
            Destroy(gameObject);
    }

    // ════════════════════════════════════════
    //  보상 드롭
    // ════════════════════════════════════════

    private void DropReward()
    {
        if (GameManager.Instance != null && goldDrop > 0) GameManager.Instance.AddGold(goldDrop);
        if (GameManager.Instance != null && expDrop > 0) GameManager.Instance.AddExp(expDrop);
        // ★ 장비 티켓 — 50% 확률로만 드랍 (Inspector에서 조정 가능)
        if (ResourceBarManager.Instance != null && equipmentTickets > 0
            && UnityEngine.Random.Range(0f, 100f) < equipmentTicketDropChance)
            ResourceBarManager.Instance.AddEquipmentTickets(equipmentTickets);

        // ★ 경험치 북 자동 드롭 (100%, 모든 몬스터 공통)
        DropExpBooks();

        // 확률 기반 아이템 드롭 (스킬 북 등)
        DropItems();
    }

    /// <summary>
    /// ★ 경험치 북 자동 드롭 — 100% 확률로 인벤토리에 직접 추가
    /// 몬스터 레벨/웨이브에 따라 등급 결정:
    ///   - 초급(Common): 항상 1개
    ///   - 중급(Uncommon): 레벨 20+ 시 30% 추가
    ///   - 고급(Rare): 레벨 40+ 시 15% 추가
    /// </summary>
    /// <summary>경험치 북 아이템 ID (Resources/Items/ 에셋 기준)</summary>
    private const int EXP_BOOK_BEGINNER_ID = 1000;  // 초급경험치북
    private const int EXP_BOOK_INTERMEDIATE_ID = 1001;  // 중급경험치북
    private const int EXP_BOOK_ADVANCED_ID = 1002;  // 고급경험치북

    private void DropExpBooks()
    {
        if (InventoryManager.Instance == null || ItemDatabase.Instance == null) return;

        // ID로 직접 찾기 (rarity 설정 오류에 영향 안 받음)
        ItemData bookBeginner = ItemDatabase.Instance.GetItemByID(EXP_BOOK_BEGINNER_ID);
        ItemData bookIntermediate = ItemDatabase.Instance.GetItemByID(EXP_BOOK_INTERMEDIATE_ID);
        ItemData bookAdvanced = ItemDatabase.Instance.GetItemByID(EXP_BOOK_ADVANCED_ID);

        // 초급 경험치 북: 항상 1개 (100%)
        if (bookBeginner != null)
            InventoryManager.Instance.AddItem(bookBeginner, 1);

        // 중급 경험치 북: 30% 확률
        if (bookIntermediate != null && Random.Range(0f, 100f) <= 30f)
            InventoryManager.Instance.AddItem(bookIntermediate, 1);

        // 고급 경험치 북: 10% 확률
        if (bookAdvanced != null && Random.Range(0f, 100f) <= 10f)
            InventoryManager.Instance.AddItem(bookAdvanced, 1);
    }

    private void DropItems()
    {
        if (itemDropTable == null || itemDropTable.Length == 0) return;
        if (InventoryManager.Instance == null) return;

        foreach (ItemDropData dropData in itemDropTable)
        {
            if (dropData.item == null) continue;
            float roll = Random.Range(0f, 100f);
            if (roll <= dropData.dropChance)
            {
                int dropAmount = Random.Range(dropData.minAmount, dropData.maxAmount + 1);
                InventoryManager.Instance.AddItem(dropData.item, dropAmount);
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowMessage($"{dropData.item.itemName} x{dropAmount} 획득!", Color.green);
            }
        }
    }

    // ════════════════════════════════════════
    //  초기화 (WaveSpawner에서 호출)
    // ════════════════════════════════════════

    public void Initialize(string name, int hp, int atk, float speed, Vector2 direction)
    {
        monsterName = name;
        maxHp = hp;
        currentHp = hp;
        damage = atk;
        moveSpeed = speed;
        moveDirection = direction.normalized;
        isDead = false;
        isAttacking = false;
        currentTarget = null;
        lastAttackTime = 0f;

        if (usePlayerTracking) FindPlayer();
        if (col != null) col.enabled = true;
        if (rb != null) { rb.simulated = true; rb.velocity = Vector2.zero; }

        // 초기 방향 설정
        UpdateDirectionParams(direction);
    }

    public void SetItemDropTable(ItemDropData[] drops) { itemDropTable = drops; }

    #region Gizmos
    void OnDrawGizmosSelected()
    {
        if (usePlayerTracking) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, detectionRange); }
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.green;
        Vector2 dir = moveDirection;
        if (usePlayerTracking && player != null && Application.isPlaying)
            dir = (player.position - transform.position).normalized;
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + dir * 2f);
    }
    #endregion
}

[System.Serializable]
public class ItemDropData
{
    [Tooltip("드롭할 아이템")]
    public ItemData item;
    [Tooltip("드롭 확률 (%)")]
    [Range(0f, 100f)]
    public float dropChance = 10f;
    [Min(1)] public int minAmount = 1;
    [Min(1)] public int maxAmount = 1;

    public ItemDropData(ItemData item, float dropChance, int minAmount, int maxAmount)
    {
        this.item = item;
        this.dropChance = dropChance;
        this.minAmount = minAmount;
        this.maxAmount = maxAmount;
    }
}
