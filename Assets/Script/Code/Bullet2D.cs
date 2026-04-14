using UnityEngine;

/// <summary>
/// 2D 총알 (크리티컬 등급 + 데미지 팝업 연동 버전)
/// 
/// ★ 변경사항:
///   - criticalTier 변수 추가 (크리티컬 등급 저장)
///   - SetCriticalTier() 메서드 추가 (PlayerController에서 호출)
///   - OnTriggerEnter2D(): monster.Hit(damage, criticalTier) 호출 → 팝업 자동
///   - Explode(): 범위 폭발도 크리티컬 등급 적용
/// </summary>
public class Bullet2D : MonoBehaviour
{
    [Header("총알 설정")]
    public float speed = 20f;
    public int damage = 10;
    public float lifetime = 3f;
    public float radius;

    [Header("타겟팅 설정")]
    [SerializeField] private bool autoTargeting = true;
    [SerializeField] private float targetSearchRange = 15f;
    [SerializeField] private float trackingStrength = 5f;
    [SerializeField] private LayerMask monsterLayer;

    [Header("이펙트")]
    public GameObject hitEffectPrefab;
    public TrailRenderer trailRenderer;

    private Rigidbody2D rb;
    private float spawnTime;
    private bool isActive = false;
    private Transform currentTarget;

    // ★ NonAlloc 정적 버퍼 (다수의 발사체 공유 — Update는 단일 스레드라 안전)
    private static readonly Collider2D[] _bulletBuffer = new Collider2D[16];

    // ★★★ 크리티컬 등급 저장 변수 ★★★
    // PlayerController.Fire()에서 SetCriticalTier()로 설정됨
    // 0=일반(흰), 1=크리티컬(빨강), 2=슈퍼(주황), 3=울트라(노랑)
    private int criticalTier = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            Debug.LogError($"[Bullet2D] Rigidbody2D가 없습니다! {gameObject.name}");
        }
        else
        {
            rb.gravityScale = 0f;
        }

        if (trailRenderer == null)
        {
            trailRenderer = GetComponent<TrailRenderer>();
        }
    }

    public void Initialize(Vector2 direction)
    {
        if (rb != null)
        {
            isActive = true;
            spawnTime = Time.time;
            currentTarget = null;

            rb.velocity = direction.normalized * speed;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            if (autoTargeting)
            {
                FindClosestTarget();
            }
        }
    }

    public void SetDamage(int newDamage)
    {
        damage = newDamage;
    }

    /// <summary>
    /// ★★★ 크리티컬 등급 설정 (PlayerController에서 호출) ★★★
    /// 
    /// 총알이 발사될 때 크리티컬 등급이 미리 결정되고,
    /// 몬스터에 맞을 때 이 등급에 맞는 색상의 데미지 팝업이 표시됨
    /// 
    /// tier 값:
    ///   0 = 일반 공격 (흰색 팝업)
    ///   1 = 크리티컬 (빨간색 팝업)
    ///   2 = 슈퍼 크리티컬 (주황색 팝업)
    ///   3 = 울트라 크리티컬 (노란색 팝업)
    /// </summary>
    public void SetCriticalTier(int tier)
    {
        criticalTier = tier;
    }

    void OnEnable()
    {
        spawnTime = Time.time;
        currentTarget = null;
        criticalTier = 0;  // ★ 풀에서 꺼낼 때 초기화

        if (trailRenderer != null)
        {
            trailRenderer.Clear();
        }

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    void Update()
    {
        if (!isActive) return;

        if (Time.time >= spawnTime + lifetime)
        {
            ReturnToPool();
            return;
        }

        if (autoTargeting)
        {
            UpdateTarget();
            TrackTarget();
        }
    }

    /// <summary>
    /// ★ 범위 폭발 (크리티컬 등급 적용)
    /// </summary>
    private void Explode()
    {
        isActive = false;

        // ★ NonAlloc: 정적 버퍼 재사용
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, radius, _bulletBuffer, monsterLayer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = _bulletBuffer[i];
            // ★ Monster면 크리티컬 등급 포함 Hit → 팝업 자동
            Monster monster = hit.GetComponent<Monster>();
            if (monster != null)
            {
                monster.Hit(damage, criticalTier);
            }
            else
            {
                IHitable hitable = hit.GetComponent<IHitable>();
                if (hitable != null)
                {
                    hitable.Hit(damage);

                    // ★ Monster 아니어도 팝업 직접 표시
                    if (DamagePopupManager.Instance != null)
                    {
                        DamagePopupManager.Instance.ShowDamage(
                            hit.transform.position, damage, criticalTier
                        );
                    }
                }
            }
        }
    }

    private void FindClosestTarget()
    {
        // ★ NonAlloc: 매 프레임 발사체마다 호출 → 정적 버퍼 재사용
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, targetSearchRange, _bulletBuffer, monsterLayer);

        float closestDistance = Mathf.Infinity;
        Transform closestMonster = null;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = _bulletBuffer[i];
            Monster monsterScript = col.GetComponent<Monster>();
            if (monsterScript != null && monsterScript.currentHp <= 0)
                continue;

            float distance = Vector2.Distance(transform.position, col.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestMonster = col.transform;
            }
        }

        currentTarget = closestMonster;
    }

    private void UpdateTarget()
    {
        if (currentTarget == null)
        {
            FindClosestTarget();
            return;
        }

        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
        if (distanceToTarget > targetSearchRange * 1.5f)
        {
            FindClosestTarget();
        }
    }

    private void TrackTarget()
    {
        if (currentTarget == null || rb == null) return;

        Vector2 direction = (currentTarget.position - transform.position).normalized;

        Vector2 currentVelocity = rb.velocity;
        Vector2 newVelocity = Vector2.Lerp(currentVelocity.normalized, direction, trackingStrength * Time.deltaTime);

        rb.velocity = newVelocity * speed;

        float angle = Mathf.Atan2(newVelocity.y, newVelocity.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    /// <summary>
    /// ★★★ 충돌 처리 (크리티컬 등급 + 데미지 팝업 연동) ★★★
    /// 
    /// 변경 전: hitable.Hit(damage);
    /// 변경 후: monster.Hit(damage, criticalTier); → 팝업 자동 표시
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;

        // 플레이어/총알과는 충돌 무시
        if (other.CompareTag("Player") || other.CompareTag("Bullet"))
            return;

        // 몬스터와 충돌
        if (other.CompareTag("Monster"))
        {
            // ★ Monster 컴포넌트가 있으면 크리티컬 등급 포함 Hit
            Monster monster = other.GetComponent<Monster>();
            if (monster != null)
            {
                // criticalTier에 맞는 색상의 데미지 팝업이 자동으로 표시됨
                // 0=흰색, 1=빨강, 2=주황, 3=노랑
                monster.Hit(damage, criticalTier);
                Debug.Log($"[Bullet2D] {other.name}에게 {damage} 데미지! (크리티컬 등급: {criticalTier})");
            }
            else
            {
                // Monster가 아닌 IHitable (기존 호환)
                IHitable hitable = other.GetComponent<IHitable>();
                if (hitable != null)
                {
                    hitable.Hit(damage);

                    // ★ 팝업은 직접 띄워줌
                    if (DamagePopupManager.Instance != null)
                    {
                        DamagePopupManager.Instance.ShowDamage(
                            other.transform.position, damage, criticalTier
                        );
                    }
                }
            }

            // 충돌 이펙트
            SpawnHitEffect(other.ClosestPoint(transform.position));

            // 풀로 반환
            ReturnToPool();
        }
    }

    void SpawnHitEffect(Vector2 position)
    {
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 0.5f);
        }
    }

    void ReturnToPool()
    {
        isActive = false;
        currentTarget = null;
        criticalTier = 0;  // ★ 초기화

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.ReturnToPool("Bullet", gameObject);

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Stop()
    {
        ReturnToPool();
    }

    #region Gizmos

    void OnDrawGizmos()
    {
        if (isActive)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.1f);

            if (autoTargeting)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, targetSearchRange);
            }

            if (rb != null && rb.velocity.magnitude > 0.1f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position,
                    (Vector2)transform.position + rb.velocity.normalized * 0.5f);
            }

            if (currentTarget != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, currentTarget.position);
                Gizmos.DrawWireSphere(currentTarget.position, 0.3f);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, targetSearchRange);
    }

    #endregion
}