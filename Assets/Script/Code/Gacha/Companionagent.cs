using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CompanionAgent
/// ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
/// ÇÖ¹Ù¿¡¼­ ¼ÒÈ¯µÈ µ¿·á°¡ Ä³¸¯ÅÍ ÁÖº¯À» µû¶ó´Ù´Ï¸ç
/// °¡Àå °¡±î¿î ¸ó½ºÅÍ¸¦ Å½»öÇÏ°í °ø°Ý.
///
/// [Prefab ¼³Á¤]
///   ÀÌ ÄÄÆ÷³ÍÆ®¸¦ µ¿·á ÇÁ¸®ÆÕ ·çÆ®¿¡ ºÙÀÓ
///   Collider (isTrigger ¾Æ´Ñ °Í), Rigidbody or CharacterController ÇÊ¿ä
///
/// [ÅÂ±×]
///   ¸ó½ºÅÍ¿¡ "Enemy" ÅÂ±× ºÎ¿© ÇÊ¿ä
///   ÇÃ·¹ÀÌ¾î¿¡ "Player" ÅÂ±× ºÎ¿© ÇÊ¿ä
/// </summary>
public class CompanionAgent : MonoBehaviour
{
    [Header("µ¿·á µ¥ÀÌÅÍ (·±Å¸ÀÓ ÁÖÀÔ)")]
    public CompanionData data;

    [Header("ÀÌµ¿ ¼³Á¤")]
    [Tooltip("ÇÃ·¹ÀÌ¾î¿ÍÀÇ ÃÖ´ë °Å¸® (ÀÌ ÀÌ»ó ¸Ö¾îÁö¸é ÇÃ·¹ÀÌ¾î¿¡°Ô ±ÍÈ¯)")]
    public float maxDistanceFromPlayer = 8f;
    [Tooltip("ÇÃ·¹ÀÌ¾î ÁÖº¯ ´ë±â ¹Ý°æ")]
    public float idleRadius = 2f;

    [Header("Å½Áö")]
    [Tooltip("¸ó½ºÅÍ Å½Áö ¹Ý°æ")]
    public float detectionRadius = 10f;
    public LayerMask enemyLayer;       // Enemy ·¹ÀÌ¾î ¼³Á¤

    [Header("½Ã°¢ È¿°ú")]
    public GameObject summonEffect;    // ¼ÒÈ¯ ½Ã ÀÌÆåÆ® ÇÁ¸®ÆÕ
    public GameObject attackEffect;    // °ø°Ý ½Ã ÀÌÆåÆ® ÇÁ¸®ÆÕ
    public float summonEffectDuration = 1f;

    // ¦¡¦¡¦¡ ³»ºÎ »óÅÂ ¦¡¦¡¦¡
    private enum AgentState { Idle, Chase, Attack, Return }
    private AgentState state = AgentState.Idle;

    private Transform player;
    private Transform target;       // ÇöÀç Å¸°Ù ¸ó½ºÅÍ
    private float attackCooldown;

    private Vector3 idleOffset;     // ÇÃ·¹ÀÌ¾î ÁÖº¯ ´ë±â À§Ä¡ ¿ÀÇÁ¼Â

    // ÀÌµ¿
    private CharacterController cc;
    private Rigidbody rb;

    // µ¿·á »ýÁ¸ ¿©ºÎ
    private bool isActive = true;

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  ÃÊ±âÈ­
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        // ÇÃ·¹ÀÌ¾î Å½»ö
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) player = playerGO.transform;

        // ´ë±â ¿ÀÇÁ¼Â ·£´ý ¼³Á¤ (¿©·¯ µ¿·á°¡ °ãÄ¡Áö ¾Ê°Ô)
        float angle = Random.Range(0f, 360f);
        idleOffset = new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad) * idleRadius,
            0f,
            Mathf.Sin(angle * Mathf.Deg2Rad) * idleRadius
        );

        // ¼ÒÈ¯ ÀÌÆåÆ®
        if (summonEffect != null)
        {
            GameObject fx = Instantiate(summonEffect, transform.position, Quaternion.identity);
            Destroy(fx, summonEffectDuration);
        }

        Debug.Log($"[CompanionAgent] {(data != null ? data.companionName : "µ¿·á")} ¼ÒÈ¯ ¿Ï·á");
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  ¸ÞÀÎ ·çÇÁ
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    void Update()
    {
        if (!isActive || player == null) return;

        attackCooldown -= Time.deltaTime;

        // ÇÃ·¹ÀÌ¾î¿Í ³Ê¹« ¸Ö¸é ±ÍÈ¯
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        if (distToPlayer > maxDistanceFromPlayer)
        {
            state = AgentState.Return;
            target = null;
        }

        switch (state)
        {
            case AgentState.Idle:
                DoIdleBehavior();
                break;
            case AgentState.Chase:
                DoChase();
                break;
            case AgentState.Attack:
                DoAttack();
                break;
            case AgentState.Return:
                DoReturn();
                break;
        }
    }

    // ¦¡¦¡¦¡ Idle: ÇÃ·¹ÀÌ¾î ¿·À» µû¶ó´Ù´Ï¸ç ¸ó½ºÅÍ Å½»ö ¦¡¦¡¦¡
    private void DoIdleBehavior()
    {
        // ÇÃ·¹ÀÌ¾î µû¶ó°¡±â (¿ÀÇÁ¼Â Æ÷ÇÔ)
        Vector3 followPos = player.position + idleOffset;
        MoveTowards(followPos, data != null ? data.moveSpeed * 0.6f : 2f);

        // ÁÖ±âÀûÀ¸·Î ¸ó½ºÅÍ Å½»ö
        Transform nearest = FindNearestEnemy();
        if (nearest != null)
        {
            target = nearest;
            state = AgentState.Chase;
        }
    }

    // ¦¡¦¡¦¡ Chase: ¸ó½ºÅÍ Ãß°Ý ¦¡¦¡¦¡
    private void DoChase()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            target = null;
            state = AgentState.Idle;
            return;
        }

        float distToTarget = Vector3.Distance(transform.position, target.position);
        float range = data != null ? data.attackRange : 2f;

        if (distToTarget <= range)
        {
            state = AgentState.Attack;
        }
        else
        {
            MoveTowards(target.position, data != null ? data.moveSpeed : 3.5f);
        }
    }

    // ¦¡¦¡¦¡ Attack: ¸ó½ºÅÍ °ø°Ý ¦¡¦¡¦¡
    private void DoAttack()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            target = null;
            state = AgentState.Idle;
            return;
        }

        float distToTarget = Vector3.Distance(transform.position, target.position);
        float range = data != null ? data.attackRange : 2f;

        // »ç°Å¸® ¹þ¾î³ª¸é ´Ù½Ã Ãß°Ý
        if (distToTarget > range + 0.5f)
        {
            state = AgentState.Chase;
            return;
        }

        // °ø°Ý Äð´Ù¿î
        if (attackCooldown <= 0f)
        {
            PerformAttack();
            float atkSpeed = data != null ? data.attackSpeed : 1f;
            attackCooldown = 1f / Mathf.Max(0.1f, atkSpeed);
        }

        // Å¸°Ù ¹æÇâÀ¸·Î È¸Àü
        LookAt(target.position);
    }

    // ¦¡¦¡¦¡ Return: ÇÃ·¹ÀÌ¾î¿¡°Ô ±ÍÈ¯ ¦¡¦¡¦¡
    private void DoReturn()
    {
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        if (distToPlayer < idleRadius + 0.5f)
        {
            state = AgentState.Idle;
            return;
        }
        MoveTowards(player.position, data != null ? data.moveSpeed * 1.5f : 5f);
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  °ø°Ý ½ÇÇà
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    private void PerformAttack()
    {
        float dmg = data != null ? data.attackPower : 10f;

        // IDamageable ÀÎÅÍÆäÀÌ½º·Î µ¥¹ÌÁö Ã³¸®
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(dmg);
        }
        else
        {
            // IDamageable ¾øÀ» °æ¿ì: ÇÁ·ÎÁ§Æ®ÀÇ ¸ó½ºÅÍ Å¬·¡½º ÀÌ¸§À¸·Î ±³Ã¼
            // ¿¹: target.GetComponent<EnemyController>()?.TakeDamage((int)dmg);
            Debug.Log($"[CompanionAgent] {target.name} ¿¡ IDamageable ¾øÀ½ - µ¥¹ÌÁö ½ºÅµ");
        }

        // °ø°Ý ÀÌÆåÆ®
        if (attackEffect != null)
        {
            Vector3 effectPos = Vector3.Lerp(transform.position, target.position, 0.5f);
            GameObject fx = Instantiate(attackEffect, effectPos, Quaternion.identity);
            Destroy(fx, 0.5f);
        }

        Debug.Log($"[CompanionAgent] {(data != null ? data.companionName : "µ¿·á")} °ø°Ý! {dmg} µ¥¹ÌÁö ¡æ {target.name}");
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  ¸ó½ºÅÍ Å½»ö
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    private Transform FindNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer);
        if (hits.Length == 0)
        {
            // LayerMask ¹Ì¼³Á¤ ½Ã ÅÂ±×·Î ´ëÃ¼
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            Transform best = null;
            float bestDist = float.MaxValue;
            foreach (var e in enemies)
            {
                if (e == null || !e.activeInHierarchy) continue;
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < detectionRadius && d < bestDist)
                {
                    bestDist = d;
                    best = e.transform;
                }
            }
            return best;
        }

        Transform nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var col in hits)
        {
            if (col == null) continue;
            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = col.transform;
            }
        }
        return nearest;
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  ÀÌµ¿ ÇïÆÛ
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    private void MoveTowards(Vector3 destination, float speed)
    {
        Vector3 dir = (destination - transform.position);
        dir.y = 0f;
        if (dir.magnitude < 0.1f) return;
        dir.Normalize();

        if (cc != null && cc.enabled)
        {
            cc.Move(dir * speed * Time.deltaTime);
        }
        else if (rb != null)
        {
            rb.MovePosition(transform.position + dir * speed * Time.deltaTime);
        }
        else
        {
            transform.position += dir * speed * Time.deltaTime;
        }

        LookAt(destination);
    }

    private void LookAt(Vector3 pos)
    {
        Vector3 dir = pos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                                                  Quaternion.LookRotation(dir),
                                                  10f * Time.deltaTime);
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  ¼ÒÈ¯ ÇØÁ¦
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    public void Dismiss()
    {
        isActive = false;
        // ¼ÒÈ¯ ÇØÁ¦ ÀÌÆåÆ® (¿É¼Ç)
        if (summonEffect != null)
        {
            Instantiate(summonEffect, transform.position, Quaternion.identity);
        }
        Destroy(gameObject, 0.2f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        float range = data != null ? data.attackRange : 2f;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}

// ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
//  IDamageable ÀÎÅÍÆäÀÌ½º (ÀÌ¹Ì ÇÁ·ÎÁ§Æ®¿¡ ÀÖÀ¸¸é »èÁ¦)
// ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
#if !IDAMAGEABLE_DEFINED
public interface IDamageable
{
    void TakeDamage(float damage);
}
#endif