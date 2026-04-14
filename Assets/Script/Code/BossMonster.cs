using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 보스 몬스터 (아이템 드롭 지원)
/// 
/// ★ 체력바는 BossMonsterHealthBar 컴포넌트가 담당합니다.
///    이 오브젝트에 MonsterHealthBar 대신 BossMonsterHealthBar를 붙여주세요.
/// </summary>
public class BossMonster : Monster
{
    [Header("보스 전용 설정")]
    [SerializeField] private string bossTitle = "강력한";
    [SerializeField] private bool hasPhases = false;
    [SerializeField] private int[] phaseThresholds;          // 페이즈 전환 체력 퍼센트 (예: 50, 25)
    [SerializeField] private float[] phaseSpeedMultipliers;  // 각 페이즈별 속도 배율
    private int currentPhase = 0;

    [Header("특수 공격")]
    [SerializeField] private bool useSpecialAttack = true;
    [SerializeField] private float specialAttackInterval = 5f;
    [SerializeField] private int specialAttackDamage = 50;
    [SerializeField] private GameObject specialAttackEffect;
    private float lastSpecialAttackTime = 0f;

    // ★ 체력바 관련 변수/메서드 전부 제거
    // → BossMonsterHealthBar 컴포넌트가 대신 처리함

    [Header("보스 보상")]
    [SerializeField] private int bossGoldMultiplier = 5;
    [SerializeField] private int bossExpMultiplier = 10;

    [Header("보스 확정 드롭")]
    [Tooltip("보스가 100% 드롭하는 아이템들")]
    [SerializeField] private ItemData[] guaranteedDrops;
    [SerializeField] private float guaranteedDropChance = 100f;

    [Header("보스 등장 연출")]
    [SerializeField] private GameObject appearEffect;
    [SerializeField] private AudioClip bossMusic;
    [SerializeField] private string bossWarningMessage = "보스 등장!";

    // ───────────────────────────────────────────
    void Start()
    {
        currentHp = maxHp;
        PlayBossEntrance();
        // ★ CreateBossHealthBar() 제거 → BossMonsterHealthBar 컴포넌트가 Start()에서 자동 처리
    }

    // ───────────────────────────────────────────
    void Update()
    {
        base.Update(); // 부모(Monster)의 Update 실행 (이동, AI 등)

        // 페이즈 전환 체크
        if (hasPhases)
            CheckPhaseTransition();

        // 특수 공격 타이머
        if (useSpecialAttack && Time.time - lastSpecialAttackTime >= specialAttackInterval)
        {
            PerformSpecialAttack();
            lastSpecialAttackTime = Time.time;
        }

        // ★ UpdateBossUI() 제거 → BossMonsterHealthBar가 Update에서 자동 처리
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// 보스 등장 연출 재생
    /// </summary>
    private void PlayBossEntrance()
    {
        Debug.Log($"보스 등장: {bossTitle} {monsterName}");

        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage(bossWarningMessage, Color.red);

        if (appearEffect != null)
            Instantiate(appearEffect, transform.position, Quaternion.identity);
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// 체력 비율에 따라 페이즈 전환 체크
    /// </summary>
    private void CheckPhaseTransition()
    {
        if (phaseThresholds == null || currentPhase >= phaseThresholds.Length) return;

        float hpPercent = ((float)currentHp / maxHp) * 100f;
        if (hpPercent <= phaseThresholds[currentPhase])
            TransitionToNextPhase();
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// 다음 페이즈로 전환 (속도 증가, 연출 등)
    /// </summary>
    private void TransitionToNextPhase()
    {
        currentPhase++;
        Debug.Log($"보스 페이즈 {currentPhase} 돌입!");

        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage($"페이즈 {currentPhase}!", Color.red);

        // 페이즈 속도 배율 적용
        int idx = currentPhase - 1;
        if (phaseSpeedMultipliers != null && idx >= 0 && idx < phaseSpeedMultipliers.Length)
            SetSpeed(GetSpeed() * phaseSpeedMultipliers[idx]);

        if (appearEffect != null)
            Instantiate(appearEffect, transform.position, Quaternion.identity);
    }

    // ★ PerformSpecialAttack NonAlloc 버퍼 (정적 공유 — 보스는 동시 다발 특수공격 없음)
    private static readonly Collider2D[] _specialAttackBuffer = new Collider2D[16];

    // ───────────────────────────────────────────
    /// <summary>
    /// 주변 플레이어에게 특수 공격 (범위 데미지)
    /// </summary>
    private void PerformSpecialAttack()
    {
        // ★ NonAlloc: 정적 버퍼 재사용 → OverlapCircleAll의 네이티브 ALLOC_TEMP 제거
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, 3f, _specialAttackBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D target = _specialAttackBuffer[i];
            if (target == null) continue;
            if (target.CompareTag("Player"))
            {
                PlayerStats ps = target.GetComponent<PlayerStats>();
                if (ps != null)
                {
                    ps.Hit(specialAttackDamage, (Vector2)transform.position);
                }
                else
                {
                    IHitable hitable = target.GetComponent<IHitable>();
                    if (hitable != null)
                        hitable.Hit(specialAttackDamage);
                }
            }
        }

        if (specialAttackEffect != null)
            Instantiate(specialAttackEffect, transform.position, Quaternion.identity);
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// ★ 보스 사망 처리 - base.Die()로 애니메이션 + 풀 반환까지 처리
    /// </summary>
    protected override void Die()
    {
        Debug.Log($"보스 {monsterName} 처치!");

        // ─── 업적 업데이트 ───
        if (AchievementSystem.Instance != null)
        {
            AchievementSystem.Instance.UpdateAchievementProgress(AchievementType.KillBoss, monsterName, 1);
            AchievementSystem.Instance.UpdateAchievementProgress(AchievementType.KillBoss, "", 1);
        }

        // ─── 퀘스트 업데이트 ───
        if (QuestManager.Instance != null)
            QuestManager.Instance.UpdateQuestProgress(QuestType.BossKill, monsterName, 1);

        // ─── 보스 전용 보상 지급 ───
        DropBossReward();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage($"{monsterName} 처치!", Color.yellow);

        // ★ 체력바 즉시 제거 (풀 반환 전에 확실히 정리)
        var hpBar = GetComponent<BossMonsterHealthBar>();
        if (hpBar != null)
            hpBar.DestroyBar();

        // ★ base.Die() 호출 → 부모의 Die 실행 (애니메이션, 풀 반환 등)
        base.Die();
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// 보스 전용 보상 지급 (골드, 경험치, 아이템)
    /// </summary>
    private void DropBossReward()
    {
        int bossGold = GoldDrop * bossGoldMultiplier;
        int bossExp = ExpDrop * bossExpMultiplier;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(bossGold);
            GameManager.Instance.AddExp(bossExp);
        }

        // 확정 드롭 아이템 지급
        if (guaranteedDrops != null && guaranteedDrops.Length > 0)
        {
            foreach (ItemData item in guaranteedDrops)
            {
                if (item != null && Random.Range(0f, 100f) <= guaranteedDropChance)
                {
                    if (InventoryManager.Instance != null)
                    {
                        InventoryManager.Instance.AddItem(item, 1);

                        if (UIManager.Instance != null)
                            UIManager.Instance.ShowMessage(GetDropMsg(item.itemName, 1), Color.yellow);
                    }
                }
            }
        }

        // 드롭 테이블 아이템 지급 (확률 1.5배 보정)
        if (ItemDropTable != null && ItemDropTable.Length > 0)
            DropItemsFromTable();
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// 드롭 테이블에서 아이템 드롭 (보스는 드롭률 1.5배)
    /// </summary>
    private void DropItemsFromTable()
    {
        if (ItemDropTable == null || InventoryManager.Instance == null) return;

        foreach (ItemDropData dropData in ItemDropTable)
        {
            if (dropData.item == null) continue;

            // 보스 드롭률 1.5배 보정 (최대 100%)
            float boostedChance = Mathf.Min(dropData.dropChance * 1.5f, 100f);
            if (Random.Range(0f, 100f) <= boostedChance)
            {
                // 드롭 수량도 1.5배 (올림 처리)
                int dropAmount = Mathf.CeilToInt(Random.Range(dropData.minAmount, dropData.maxAmount + 1) * 1.5f);
                InventoryManager.Instance.AddItem(dropData.item, dropAmount);

                if (UIManager.Instance != null)
                    UIManager.Instance.ShowMessage(GetDropMsg(dropData.item.itemName, dropAmount), Color.green);
            }
        }
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// 오브젝트 풀에서 재사용할 때 초기화 (보스 전용)
    /// </summary>
    public void InitializeBoss(string name, int hp, int atk, float speed, Vector2 direction, string title = "강력한")
    {
        Initialize(name, hp, atk, speed, direction); // 부모(Monster) 초기화
        bossTitle = title;
        monsterName = $"{bossTitle} {name}";

        // ★ 체력바 이름 실시간 갱신
        var hpBar = GetComponent<BossMonsterHealthBar>();
        if (hpBar != null)
            hpBar.UpdateBossName(monsterName);
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// 씬 뷰에서 특수 공격 범위 시각화 (개발용)
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 3f); // 특수 공격 범위
    }
}