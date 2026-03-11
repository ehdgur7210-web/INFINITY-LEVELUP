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

    [Header("이동 설정")]
    [SerializeField] private bool usePlayerTracking = true;
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private Vector2 moveDirection = Vector2.left;
    [SerializeField] private bool flipSpriteByDirection = true;

    [Header("공격 설정")]
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private LayerMask targetLayer;
    private float lastAttackTime = 0f;

    [Header("드롭 보상")]
    [SerializeField] private int goldDrop = 10;
    [SerializeField] private int expDrop = 5;
    [SerializeField] private int equipmentTickets = 1;

    [Header("아이템 드롭")]
    [Tooltip("드롭 가능한 아이템 목록")]
    [SerializeField] private ItemDropData[] itemDropTable;
    [SerializeField] private GameObject Effect;

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
        if (flipSpriteByDirection && spriteRenderer != null)
            spriteRenderer.flipX = (moveDirection.x < 0);
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
        }
        if (rb != null) { rb.simulated = true; rb.velocity = Vector2.zero; }
        if (col != null) col.enabled = true;

        // ★ 애니메이터 파라미터 초기화
        if (animator != null)
        {
            animator.ResetTrigger("Die");
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsIdle", true);
            animator.SetBool("IsAttacking", false);
        }

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
        // 1순위: PlayerStats.Instance (싱글톤)
        if (PlayerStats.Instance != null)
        {
            player = PlayerStats.Instance.transform;
            return;
        }

        // 2순위: "Player" 태그
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            return;
        }

        // 3순위: PlayerController 컴포넌트 탐색 (태그 없어도 찾음)
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null)
        {
            player = pc.transform;
            return;
        }

        Debug.LogWarning($"[{monsterName}] 플레이어를 찾을 수 없음!");
    }

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
                if (animator != null)
                {
                    animator.SetBool("IsMoving", true);
                    animator.SetBool("IsIdle", false);
                    animator.SetBool("IsAttacking", false);
                }
            }
            else
            {
                rb.velocity = Vector2.zero;
                if (animator != null)
                {
                    animator.SetBool("IsMoving", false);
                    animator.SetBool("IsIdle", true);
                    animator.SetBool("IsAttacking", false);
                }
                return;
            }
        }

        rb.velocity = direction * moveSpeed;

        if (flipSpriteByDirection && spriteRenderer != null)
        {
            if (direction.x > 0.1f) spriteRenderer.flipX = false;
            else if (direction.x < -0.1f) spriteRenderer.flipX = true;
        }

        if (animator != null)
        {
            animator.SetBool("IsMoving", true);
            animator.SetBool("IsAttacking", false);
        }
    }

    private void DetectTarget()
    {
        Collider2D[] targets = Physics2D.OverlapCircleAll(transform.position, attackRange, targetLayer);
        currentTarget = targets.Length > 0 ? targets[0].transform : null;
    }

    private void Attack()
    {
        isAttacking = true;
        rb.velocity = Vector2.zero;

        if (flipSpriteByDirection && spriteRenderer != null && currentTarget != null)
        {
            float dirX = currentTarget.position.x - transform.position.x;
            if (dirX > 0.1f) spriteRenderer.flipX = false;
            else if (dirX < -0.1f) spriteRenderer.flipX = true;
        }

        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsAttacking", true);
        }

        if (Time.time - lastAttackTime < attackCooldown) return;

        PerformAttack();
        lastAttackTime = Time.time;
    }

    private void PerformAttack()
    {
        if (currentTarget == null) return;
        IHitable hitable = currentTarget.GetComponent<IHitable>();
        if (hitable != null) hitable.Hit(damage);
    }

    public void Hit(float damageAmount) { Hit(damageAmount, 0); }

    public void Hit(float damageAmount, int criticalTier)
    {
        if (isDead) return;

        currentHp -= (int)damageAmount;

        // ★ 몬스터 피격 효과음
        SoundManager.Instance?.PlayMonsterHit();

        if (DamagePopupManager.Instance != null)
            DamagePopupManager.Instance.ShowDamage(transform.position, damageAmount, criticalTier);

        if (healthBar != null) healthBar.ShowTemporarily();

        // ★ 피격 반짝이
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

        Debug.Log($"{monsterName}: {damageAmount} 데미지! (남은 HP: {currentHp}) [등급: {criticalTier}]");
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

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        // ★ 몬스터 사망 효과음
        SoundManager.Instance?.PlayMonsterDeathSound();

        Debug.Log($"[Die 시작] {monsterName}"); // ← 이게 찍히는지 확인!
        // 반짝이 중단 & 색상 복구
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
            if (spriteRenderer != null) { Color c = spriteRenderer.color; c.a = 1f; spriteRenderer.color = c; }
        }

        rb.velocity = Vector2.zero;
        rb.simulated = false;
        if (col != null) col.enabled = false;

        // ★ 모든 Bool 끄고 Die 트리거
        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsIdle", false);
            animator.SetBool("IsAttacking", false);
            animator.SetTrigger("Die");
        }

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

        SpawnHitEffect(transform.position);
        Debug.Log("[Die] StartCoroutine 직전");
        // ★ WaveSpawner 호출 + 풀 반환을 애니메이션 끝난 후 처리
        if (!gameObject.activeInHierarchy)
        {
            PoolManager.Instance?.ReturnToPool(poolTag, gameObject); // ← poolTag 추가
            return;
        }

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(ReturnToPoolAfterAnim());
        }
        else
        {
            // 씬 전환 중 비활성화된 경우 → 코루틴 없이 즉시 반환
            WaveSpawner spawner = FindObjectOfType<WaveSpawner>();
            if (spawner != null) spawner.OnMonsterKilled(gameObject);

            if (PoolManager.Instance != null)
                PoolManager.Instance.ReturnToPool(poolTag, gameObject);
            else
                Destroy(gameObject);
        }
    }

    private IEnumerator ReturnToPoolAfterAnim()
    {
        Debug.Log($"[Die 1] 코루틴 시작 - {monsterName}");
        yield return null;
        yield return null;
        Debug.Log($"[Die 2] 2프레임 후");

        if (animator != null)
        {
            int prevHash = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            Debug.Log($"[Die 3] 현재 상태 해시: {prevHash}");

            float elapsed = 0f;
            while (animator.GetCurrentAnimatorStateInfo(0).fullPathHash == prevHash || animator.IsInTransition(0))
            {
                elapsed += Time.deltaTime;
                if (elapsed > 3f) { Debug.LogWarning("[Die] 상태전환 타임아웃!"); break; }
                yield return null;
            }
            Debug.Log($"[Die 4] 상태 전환 완료. normalizedTime: {animator.GetCurrentAnimatorStateInfo(0).normalizedTime}");

            elapsed = 0f;
            while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            {
                elapsed += Time.deltaTime;
                if (elapsed > 5f) { Debug.LogWarning("[Die] 재생완료 타임아웃!"); break; }
                yield return null;
            }
            Debug.Log("[Die 5] 애니메이션 재생 완료!");
        }

        Debug.Log("[Die 6] 풀 반환 직전");
        WaveSpawner spawner = FindObjectOfType<WaveSpawner>();
        if (spawner != null) spawner.OnMonsterKilled(gameObject);

        if (PoolManager.Instance != null)
            PoolManager.Instance.ReturnToPool(poolTag, gameObject);
        else
            Destroy(gameObject);
    }

    void SpawnHitEffect(Vector2 position)
    {
        if (Effect != null)
        {
            GameObject effect = Instantiate(Effect, position, Quaternion.identity);
            Destroy(effect, 0.5f);
        }
    }

    private void DropReward()
    {
        if (GameManager.Instance != null && goldDrop > 0) GameManager.Instance.AddGold(goldDrop);
        if (GameManager.Instance != null && expDrop > 0) GameManager.Instance.AddExp(expDrop);
        if (ResourceBarManager.Instance != null && equipmentTickets>0) ResourceBarManager.Instance.AddEquipmentTickets(equipmentTickets);
        DropItems();
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
        if (flipSpriteByDirection && spriteRenderer != null)
            spriteRenderer.flipX = (moveDirection.x < 0);
        if (col != null) col.enabled = true;
        if (rb != null) { rb.simulated = true; rb.velocity = Vector2.zero; }
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