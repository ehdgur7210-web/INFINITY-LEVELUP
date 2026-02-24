using UnityEngine;

/// <summary>
/// ============================================================
/// SkillProjectileHandler — 스킬 투사체 발사 전담 핸들러
/// ============================================================
/// 역할:
///   - 스킬별 투사체 생성 및 발사 로직 분리
///   - Melee/Ranged/Magic 스타일별 처리
///   - PoolManager 연동
///   - PlayerController의 단일 책임 원칙 준수
/// ============================================================
/// </summary>
public class SkillProjectileHandler : MonoBehaviour
{
    public static SkillProjectileHandler Instance { get; private set; }

    [Header("기본 투사체 프리팹")]
    [SerializeField] private GameObject defaultBulletPrefab;
    [SerializeField] private GameObject defaultFireballPrefab;
    [SerializeField] private GameObject defaultSlashEffectPrefab;

    [Header("발사 위치")]
    [SerializeField] private Transform firePoint;

    [Header("레이어 설정")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("디버깅")]
    [SerializeField] private bool debugMode = true;

    private Vector2 aimDirection = Vector2.right;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // FirePoint 자동 생성
        if (firePoint == null)
        {
            GameObject fp = new GameObject("SkillFirePoint");
            fp.transform.SetParent(transform);
            fp.transform.localPosition = new Vector3(0.5f, 0, 0);
            firePoint = fp.transform;
        }

        // 기본 프리팹 체크
        ValidatePrefabs();
    }

    private void ValidatePrefabs()
    {
        if (defaultBulletPrefab == null)
        {
            Debug.LogWarning("[SkillProjectileHandler] defaultBulletPrefab이 할당되지 않았습니다!");
        }

        if (defaultFireballPrefab == null)
        {
            Debug.LogWarning("[SkillProjectileHandler] defaultFireballPrefab이 할당되지 않았습니다!");
        }
    }

    /// <summary>
    /// 조준 방향 업데이트 (PlayerController에서 호출)
    /// </summary>
    public void UpdateAimDirection(Vector2 direction)
    {
        aimDirection = direction.normalized;
    }

    /// <summary>
    /// 근거리 스킬 실행 (검기 이펙트)
    /// </summary>
    public void ExecuteMeleeSkill(SkillData skillData, float damageValue)
    {
        if (skillData == null) return;

        float attackRange = skillData.range > 0 ? skillData.range : 2f;
        float attackRadius = skillData.areaRadius > 0 ? skillData.areaRadius : 1f;

        Vector2 attackCenter = (Vector2)transform.position + aimDirection * (attackRange * 0.5f);

        // 범위 내 적 탐지 및 데미지
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, attackRadius, enemyLayer);

        int hitCount = 0;
        foreach (Collider2D hit in hits)
        {
            IHitable hitable = hit.GetComponent<IHitable>();
            if (hitable != null)
            {
                hitable.Hit((int)damageValue);
                hitCount++;
            }
        }

        // 검기 이펙트 생성
        SpawnMeleeEffect(skillData, attackCenter);

        if (debugMode)
        {
            Debug.Log($"[SkillProjectileHandler] Melee: {skillData.skillName} — {hitCount}개 적에게 {(int)damageValue} 데미지");
        }
    }

    /// <summary>
    /// 원거리 스킬 실행 (총알 발사)
    /// </summary>
    public void ExecuteRangedSkill(SkillData skillData, float damageValue)
    {
        if (skillData == null) return;

        GameObject bullet = null;

        // 1. SkillData의 프리팹 우선 사용
        if (skillData.attackEffectPrefab != null)
        {
            bullet = SpawnProjectile(skillData.attackEffectPrefab, "SkillBullet");
        }
        // 2. PoolManager에서 가져오기
        else if (PoolManager.Instance != null)
        {
            bullet = PoolManager.Instance.SpawnFromPool("Bullet", firePoint.position, Quaternion.identity);
        }
        // 3. 기본 프리팹 사용
        else if (defaultBulletPrefab != null)
        {
            bullet = Instantiate(defaultBulletPrefab, firePoint.position, Quaternion.identity);
        }

        if (bullet != null)
        {
            Bullet2D bulletScript = bullet.GetComponent<Bullet2D>();
            if (bulletScript != null)
            {
                bulletScript.SetDamage((int)damageValue);
                bulletScript.Initialize(aimDirection);
            }

            if (debugMode)
            {
                Debug.Log($"[SkillProjectileHandler] Ranged: {skillData.skillName} — 데미지 {(int)damageValue}");
            }
        }
        else
        {
            Debug.LogError("[SkillProjectileHandler] 총알 생성 실패!");
        }
    }

    /// <summary>
    /// 마법 스킬 실행 (파이어볼 발사)
    /// </summary>
    public void ExecuteMagicSkill(SkillData skillData, float damageValue)
    {
        Debug.Log($"[SkillProjectileHandler] ========== ExecuteMagicSkill 시작 ==========");
        Debug.Log($"[SkillProjectileHandler] 스킬: {skillData?.skillName}, 데미지: {damageValue}");

        if (skillData == null)
        {
            Debug.LogError("[SkillProjectileHandler] skillData가 null!");
            return;
        }

        if (firePoint == null)
        {
            Debug.LogError("[SkillProjectileHandler] firePoint가 null!");
            return;
        }

        GameObject fireball = null;

        // 1. SkillData의 프리팹 우선 사용
        if (skillData.attackEffectPrefab != null)
        {
            Debug.Log($"[SkillProjectileHandler] SkillData 프리팹 사용: {skillData.attackEffectPrefab.name}");
            fireball = SpawnProjectile(skillData.attackEffectPrefab, "Fireball");
        }
        // 2. PoolManager에서 가져오기
        else if (PoolManager.Instance != null)
        {
            Debug.Log($"[SkillProjectileHandler] PoolManager에서 Fireball 가져오기");
            fireball = PoolManager.Instance.SpawnFromPool("Fireball", firePoint.position, Quaternion.identity);
        }
        // 3. 기본 프리팹 사용
        else if (defaultFireballPrefab != null)
        {
            Debug.Log($"[SkillProjectileHandler] 기본 Fireball 프리팹 사용");
            fireball = Instantiate(defaultFireballPrefab, firePoint.position, Quaternion.identity);
        }
        // 4. 런타임 생성 (최후의 수단)
        else
        {
            Debug.LogWarning($"[SkillProjectileHandler] 프리팹 없음 - 런타임 생성");
            fireball = CreateRuntimeFireball();
        }

        if (fireball != null)
        {
            FireballProjectile fb = fireball.GetComponent<FireballProjectile>();

            // 1. 변수를 if문 밖(상위)에서 미리 선언합니다.
            float explosionRadius = skillData.areaRadius > 0 ? skillData.areaRadius : 1.5f;

            if (fb != null)
            {
                // ★ 기존 FireballProjectile의 Initialize 시그니처: (Vector2 dir, int dmg, float radius)
                fb.Initialize(aimDirection, (int)damageValue, explosionRadius);
            }
            else
            {
                Debug.LogWarning($"[SkillProjectileHandler] {fireball.name}에 FireballProjectile 컴포넌트 없음!");
            }

            if (debugMode)
            {
                // 2. 이제 여기서도 explosionRadius를 인식할 수 있습니다!
                Debug.Log($"[SkillProjectileHandler] Magic: {skillData.skillName} - 데미지 {(int)damageValue}, 범위 {explosionRadius}");
            }
        }
        else
        {
            Debug.LogError("[SkillProjectileHandler] ❌ Fireball 생성 실패!");
            Debug.LogError($"  - attackEffectPrefab: {skillData.attackEffectPrefab}");
            Debug.LogError($"  - PoolManager.Instance: {PoolManager.Instance}");
            Debug.LogError($"  - defaultFireballPrefab: {defaultFireballPrefab}");
        }

        Debug.Log($"[SkillProjectileHandler] ========== ExecuteMagicSkill 종료 ==========");
    }

    /// <summary>
    /// 투사체 스폰 헬퍼
    /// </summary>
    private GameObject SpawnProjectile(GameObject prefab, string poolTag)
    {
        GameObject projectile = null;

        // Pool에서 먼저 시도
        if (PoolManager.Instance != null)
        {
            projectile = PoolManager.Instance.SpawnFromPool(poolTag, firePoint.position, Quaternion.identity);
        }

        // Pool에 없으면 Instantiate
        if (projectile == null)
        {
            projectile = Instantiate(prefab, firePoint.position, Quaternion.identity);
        }

        return projectile;
    }

    /// <summary>
    /// 근거리 이펙트 생성
    /// </summary>
    private void SpawnMeleeEffect(SkillData skillData, Vector2 position)
    {
        GameObject effect = null;

        // 1. SkillData의 프리팹 사용
        if (skillData.attackEffectPrefab != null)
        {
            effect = Instantiate(skillData.attackEffectPrefab, position, Quaternion.identity);
        }
        // 2. 기본 슬래시 이펙트
        else if (defaultSlashEffectPrefab != null)
        {
            effect = Instantiate(defaultSlashEffectPrefab, position, Quaternion.identity);
        }
        // 3. 런타임 생성
        else
        {
            effect = CreateRuntimeSlashEffect(position);
        }

        if (effect != null)
        {
            Destroy(effect, 1.5f);
        }
    }

    /// <summary>
    /// 런타임 슬래시 이펙트 생성 (폴백용)
    /// </summary>
    private GameObject CreateRuntimeSlashEffect(Vector2 position)
    {
        GameObject effect = new GameObject("RuntimeSlashEffect");
        effect.transform.position = position;

        SpriteRenderer sr = effect.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 0.9f, 0.2f, 0.8f);
        sr.sortingOrder = 100;

        // 간단한 원형 스프라이트
        Texture2D tex = new Texture2D(64, 64);
        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 64; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
                float alpha = Mathf.Clamp01(1f - (dist / 28f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        sr.transform.localScale = Vector3.one * 2f;

        // 페이드 아웃 애니메이션
        var fadeOut = effect.AddComponent<FadeOutEffect>();
        fadeOut.duration = 0.4f;

        Destroy(effect, 0.5f);
        return effect;
    }

    /// <summary>
    /// 런타임 파이어볼 생성 (폴백용)
    /// </summary>
    private GameObject CreateRuntimeFireball()
    {
        GameObject fireball = new GameObject("RuntimeFireball");
        fireball.transform.position = firePoint.position;
        fireball.tag = "Projectile";

        // 스프라이트
        SpriteRenderer sr = fireball.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 0.5f, 0f);
        sr.sortingOrder = 50;

        Texture2D tex = new Texture2D(32, 32);
        for (int x = 0; x < 32; x++)
        {
            for (int y = 0; y < 32; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                float alpha = Mathf.Clamp01(1f - (dist / 14f));
                tex.SetPixel(x, y, new Color(1f, 0.7f, 0f, alpha));
            }
        }
        tex.Apply();

        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        sr.transform.localScale = Vector3.one * 0.8f;

        // 물리
        Rigidbody2D rb = fireball.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        CircleCollider2D col = fireball.AddComponent<CircleCollider2D>();
        col.radius = 0.3f;
        col.isTrigger = true;

        // ★ 기존 FireballProjectile 스크립트 추가
        fireball.AddComponent<FireballProjectile>();

        Debug.Log("[SkillProjectileHandler] 런타임 파이어볼 생성됨 (프리팹 할당 권장)");
        return fireball;
    }

    #region 공개 설정 메서드

    public void SetFirePoint(Transform newFirePoint)
    {
        firePoint = newFirePoint;
    }

    public void SetDefaultBullet(GameObject prefab)
    {
        defaultBulletPrefab = prefab;
    }

    public void SetDefaultFireball(GameObject prefab)
    {
        defaultFireballPrefab = prefab;
    }

    public void SetEnemyLayer(LayerMask layer)
    {
        enemyLayer = layer;
    }

    #endregion
}

/// <summary>
/// 페이드 아웃 이펙트 헬퍼 컴포넌트
/// </summary>
public class FadeOutEffect : MonoBehaviour
{
    public float duration = 0.5f;
    private SpriteRenderer sr;
    private float elapsed = 0f;
    private Color startColor;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            startColor = sr.color;
        }
    }

    void Update()
    {
        if (sr == null) return;

        elapsed += Time.deltaTime;
        float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / duration);
        sr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
    }
}