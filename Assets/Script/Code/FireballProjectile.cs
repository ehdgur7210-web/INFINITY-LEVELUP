using UnityEngine;

/// <summary>
/// 파이어볼 발사체 (Magic 스킬용)
/// - 직진 이동
/// - 적 충돌 시 범위 데미지
/// - 폭발 이펙트
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class FireballProjectile : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float lifetime = 3f;

    [Header("충돌")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("폭발 이펙트")]
    [SerializeField] private GameObject explosionEffectPrefab;

    // 내부 상태
    private Vector2 direction;
    private int damage;
    private float explosionRadius;
    private bool hasExploded = false;
    private float spawnTime;
    private Rigidbody2D rb;
    private CircleCollider2D col;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CircleCollider2D>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
        }

        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    void OnEnable()
    {
        hasExploded = false;
        spawnTime = Time.time;
    }

    void Update()
    {
        if (hasExploded) return;

        // 이동
        if (rb != null)
        {
            rb.velocity = direction * moveSpeed;
        }
        else
        {
            transform.Translate(direction * moveSpeed * Time.deltaTime, Space.World);
        }

        // 수명 체크
        if (Time.time - spawnTime >= lifetime)
        {
            Explode();
        }
    }

    /// <summary>
    /// 파이어볼 초기화
    /// </summary>
    public void Initialize(Vector2 dir, int dmg, float radius)
    {
        // ★ 마법 발사 효과음
        SoundManager.Instance?.PlayMagicCast();
        direction = dir.normalized;
        damage = dmg;
        explosionRadius = radius;
        spawnTime = Time.time;
        hasExploded = false;

        // 회전 (발사 방향으로)
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        if (rb != null)
        {
            rb.velocity = direction * moveSpeed;
        }

        Debug.Log($"[Fireball] 초기화 - 방향:{dir}, 데미지:{dmg}, 범위:{radius}, 위치:{transform.position}");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasExploded) return;

        // 플레이어나 다른 총알 무시
        if (other.CompareTag("Player") || other.CompareTag("Bullet"))
            return;

        // 적과 충돌 시 폭발
        if (other.CompareTag("Monster") || ((1 << other.gameObject.layer) & enemyLayer) != 0)
        {
            Debug.Log($"[Fireball] 적과 충돌: {other.name}");
            Explode();
        }
    }

    /// <summary>
    /// 폭발 처리
    /// </summary>
    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // ★ 파이어볼 폭발 효과음
        SoundManager.Instance?.PlaySFX("FireballExplode");

        Debug.Log($"[Fireball] 폭발 시작! 위치:{transform.position}, 범위:{explosionRadius}");

        // 범위 내 모든 적에게 데미지
        Collider2D[] enemies = Physics2D.OverlapCircleAll(
            transform.position,
            explosionRadius,
            enemyLayer
        );

        Debug.Log($"[Fireball] 범위 내 적 발견: {enemies.Length}개");

        foreach (Collider2D enemy in enemies)
        {
            IHitable hitable = enemy.GetComponent<IHitable>();
            if (hitable != null)
            {
                hitable.Hit(damage);
                Debug.Log($"[Fireball] {enemy.name}에게 {damage} 데미지");
            }
        }

        // 폭발 이펙트
        if (explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
        else
        {
            CreateDefaultExplosion();
        }

        // 파괴 또는 풀 반환
        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.ReturnToPool("Fireball", gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 기본 폭발 이펙트 생성
    /// </summary>
    private void CreateDefaultExplosion()
    {
        GameObject explosion = new GameObject("FireballExplosion");
        explosion.transform.position = transform.position;

        SpriteRenderer sr = explosion.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 0.3f, 0f, 0.8f);
        sr.sortingOrder = 10;

        // 원형 스프라이트 생성
        Texture2D tex = new Texture2D(64, 64);
        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 64; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
                if (dist < 28)
                {
                    float alpha = 1f - (dist / 28f);
                    tex.SetPixel(x, y, new Color(1f, 0.5f, 0f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        sr.transform.localScale = Vector3.one * explosionRadius;

        // 폭발 애니메이션
        explosion.AddComponent<ExplosionEffect>();

        Destroy(explosion, 1f);
    }

    void OnDrawGizmos()
    {
        // 폭발 범위 표시
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}