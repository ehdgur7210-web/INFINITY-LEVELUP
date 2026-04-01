using UnityEngine;

/// <summary>
/// Cartoon Coffee VFX 투사체 — 몬스터 타겟 추적 + 데미지 연동
///
/// 사용법:
///   1. 프리팹에 이 스크립트 + Rigidbody2D(Kinematic) + CircleCollider2D(isTrigger) 부착
///   2. Inspector에서 impactObject, monsterLayer 설정
///   3. 코드에서 Initialize(direction, damage) 호출
///   4. 자동으로 가장 가까운 몬스터를 추적하며 날아감
/// </summary>
public class Projectile : MonoBehaviour
{
    [Header("이펙트")]
    public GameObject impactObject = null;
    public GameObject muzzleFlashObject = null;
    public GameObject chargingObject = null;
    public bool isChargeable = false;
    public bool rotateSprite = true;
    public bool muzzleFlash = true;
    public Color muzzleFlashColor = Color.white;
    public Color chargeColor = Color.white;

    [Header("이동")]
    public float moveSpeed = 10f;
    public float lifetime = 3f;
    public float moveAngle = 0;
    public float spriteAngle = 0;
    public float angleRandomness = 5;
    public bool explodeAtScreenEdge = true;

    [Header("타겟팅")]
    [SerializeField] private bool autoTargeting = true;
    [SerializeField] private float targetSearchRange = 15f;
    [SerializeField] private float trackingStrength = 5f;
    [SerializeField] private LayerMask monsterLayer;

    [Header("회전 연출")]
    public float rotationSpeed = 0;
    public float rotationRange = 0;

    [Header("원점")]
    public Vector2 bulletOriginPoint = new Vector2(.36f, 0);
    public Vector2 muzzleFlashOriginPoint = new Vector2(0, 0);
    public Vector2 chargeOriginPoint = new Vector2(0, 0);

    // 내부 상태
    private Vector2 moveDirection;
    private int damage;
    private int criticalTier;
    private Transform currentTarget;
    private float spawnTime;
    private bool isActive = false;
    private bool rotateClockwise = false;
    private Camera cachedCamera;

    /// <summary>
    /// 투사체 초기화 (발사 시 호출)
    /// </summary>
    public void Initialize(Vector2 direction, int dmg, int critTier = 0)
    {
        moveDirection = direction.normalized;
        damage = dmg;
        criticalTier = critTier;
        spawnTime = Time.time;
        isActive = true;
        currentTarget = null;

        if (rotateSprite)
        {
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        if (autoTargeting)
        {
            FindClosestTarget();
        }
    }

    void Start()
    {
        cachedCamera = Camera.main;

        // Initialize가 호출되지 않은 경우 (에디터 테스트용) 기본 활성화
        if (!isActive)
        {
            spawnTime = Time.time;
            isActive = true;
            moveDirection = transform.right;

            if (autoTargeting)
            {
                FindClosestTarget();
            }
        }
    }

    void Update()
    {
        if (!isActive) return;

        // 수명 체크
        if (Time.time - spawnTime >= lifetime)
        {
            Impact();
            return;
        }

        // 타겟 추적
        if (autoTargeting)
        {
            UpdateTarget();
            TrackTarget();
        }

        // 이동
        Move();

        // 회전 연출
        RotateProjectile();

        // 화면 밖 체크
        CheckIfOffScreen();
    }

    private void Move()
    {
        if (currentTarget != null)
        {
            // 타겟이 있으면 타겟 방향으로 이동
            moveDirection = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
        }

        transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);

        // 이동 방향으로 회전 (rotateSprite가 켜져있을 때)
        if (rotateSprite && moveDirection.magnitude > 0.01f)
        {
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private void FindClosestTarget()
    {
        Collider2D[] monsters = Physics2D.OverlapCircleAll(
            transform.position, targetSearchRange, monsterLayer);

        float closestDist = Mathf.Infinity;
        Transform closest = null;

        foreach (Collider2D col in monsters)
        {
            // 죽은 몬스터 스킵
            Monster monster = col.GetComponent<Monster>();
            if (monster != null && monster.currentHp <= 0)
                continue;

            float dist = Vector2.Distance(transform.position, col.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = col.transform;
            }
        }

        currentTarget = closest;
    }

    private void UpdateTarget()
    {
        if (currentTarget == null)
        {
            FindClosestTarget();
            return;
        }

        // 타겟이 파괴/비활성화되었으면 재탐색
        if (!currentTarget.gameObject.activeInHierarchy)
        {
            currentTarget = null;
            FindClosestTarget();
            return;
        }

        // 타겟이 너무 멀어졌으면 재탐색
        float dist = Vector2.Distance(transform.position, currentTarget.position);
        if (dist > targetSearchRange * 1.5f)
        {
            FindClosestTarget();
        }
    }

    private void TrackTarget()
    {
        if (currentTarget == null) return;

        Vector2 toTarget = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
        moveDirection = Vector2.Lerp(moveDirection, toTarget, trackingStrength * Time.deltaTime).normalized;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;

        // 플레이어/총알 무시
        if (other.CompareTag("Player") || other.CompareTag("Bullet") || other.CompareTag("Projectile"))
            return;

        // 몬스터 충돌
        if (other.CompareTag("Monster"))
        {
            Monster monster = other.GetComponent<Monster>();
            if (monster != null)
            {
                monster.Hit(damage, criticalTier);
            }
            else
            {
                IHitable hitable = other.GetComponent<IHitable>();
                if (hitable != null)
                {
                    hitable.Hit(damage);
                    if (DamagePopupManager.Instance != null)
                    {
                        DamagePopupManager.Instance.ShowDamage(
                            other.transform.position, damage, criticalTier);
                    }
                }
            }

            Impact();
        }
    }

    private void Impact()
    {
        if (!isActive) return;
        isActive = false;

        // 임팩트 이펙트 생성
        if (impactObject != null)
        {
            GameObject impact = Instantiate(impactObject, transform.position, transform.rotation);
            Destroy(impact, 2f);
        }

        // 풀 반환 또는 파괴
        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.ReturnToPool(gameObject.name.Replace("(Clone)", "").Trim(), gameObject);
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void CheckIfOffScreen()
    {
        if (cachedCamera == null) return;

        Vector3 screenPos = cachedCamera.WorldToScreenPoint(transform.position);
        if (screenPos.x < -100 || screenPos.x > Screen.width + 100 ||
            screenPos.y < -100 || screenPos.y > Screen.height + 100)
        {
            Impact();
        }
    }

    private void RotateProjectile()
    {
        if (rotationRange <= 0 || rotationSpeed <= 0) return;

        float rotateAmount = rotationSpeed * Time.deltaTime * 60f;

        if (!rotateClockwise)
        {
            transform.Rotate(0, 0, rotateAmount);
            if (transform.rotation.z * Mathf.Rad2Deg >= rotationRange)
                rotateClockwise = true;
        }
        else
        {
            transform.Rotate(0, 0, -rotateAmount);
            if (transform.rotation.z * Mathf.Rad2Deg <= -rotationRange)
                rotateClockwise = false;
        }
    }

    void OnDisable()
    {
        isActive = false;
        currentTarget = null;
    }

    #region Gizmos
    void OnDrawGizmosSelected()
    {
        if (autoTargeting)
        {
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawWireSphere(transform.position, targetSearchRange);
        }

        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
    #endregion
}