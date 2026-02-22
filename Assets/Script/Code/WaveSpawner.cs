using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 웨이브 스포너 (SO + PoolManager 버전)
/// 
/// ✅ 변경 포인트:
///   - Wave[] 배열 직접 관리 → StageDataSO[]로 교체
///   - Instantiate() → PoolManager.SpawnFromPool()
///   - 몬스터 사망 시 Monster.Die()에서 PoolManager.ReturnToPool() 호출 필요!
/// 
/// ✅ Inspector 설정:
///   1. Stages 배열에 StageDataSO 파일 드래그
///   2. Waves Per Stage = 10 (스테이지당 웨이브 수)
///   3. PoolManager에 몬스터 프리팹 태그 등록 (이름 일치!)
/// 
/// ✅ 스폰 흐름:
///   SO에서 Wave 자동 생성 → spawnInterval마다 1마리 스폰 (PoolManager 사용)
///   → 전부 스폰 → 전멸 대기 → 다음 웨이브 → 10웨이브마다 챕터 증가
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    public static WaveSpawner Instance { get; private set; }

    // 챕터 변경 이벤트 (BackgroundManager 등에서 구독)
    public static System.Action<int, StageDataSO> OnChapterChanged;
    public static System.Action<int, int> OnWaveStarted;   // (chapter, stageWave)
    public static System.Action<int, int> OnWaveCleared;

    // ─────────────────────────────────────────
    [Header("스폰 위치")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Vector2 moveDirection = Vector2.left;

    [Header("★ 스테이지 데이터 (챕터당 SO 파일 1개)")]
    [SerializeField] private StageDataSO[] stages;          // 챕터 수만큼만!
    [SerializeField] private int wavesPerStage = 10;   // 스테이지당 웨이브 수
    [SerializeField] private float timeBetweenWaves = 5f;

    [Header("마지막 챕터 무한 반복?")]
    [SerializeField] private bool loopLastChapter = false;
    // ─────────────────────────────────────────

    // ── 현재 상태 (읽기 전용 프로퍼티) ──
    public int CurrentWaveIndex { get; set; } = 0;  // ★ 재시작을 위해 setter 허용
    public int CurrentChapter => (CurrentWaveIndex / wavesPerStage) + 1;
    public int CurrentStageWave => (CurrentWaveIndex % wavesPerStage) + 1;
    public string StageLabel => $"{CurrentChapter}-{CurrentStageWave}";
    public int RemainingMonsters { get; private set; } = 0;
    public bool IsSpawning { get; private set; } = false;

    // ── 내부 변수 ──
    private Wave currentWave;
    private List<GameObject> aliveMonsters = new List<GameObject>();
    private int lastChapter = 0;

    // ─────────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            CreateDefaultSpawnPoint();

        StartCoroutine(StartWave());
    }

    // ─────────────────────────────────────────
    // ★★★ 핵심 코루틴: 웨이브 시작
    // ─────────────────────────────────────────
    private IEnumerator StartWave()
    {
        // ── 스테이지 데이터 가져오기 ──
        StageDataSO stageData = GetCurrentStageData();
        if (stageData == null)
        {
            Debug.Log(" 모든 스테이지 클리어!");
            OnAllWavesCompleted();
            yield break;
        }

        // ── 챕터 변경 감지 → 이벤트 발행 (배경 전환 등) ──
        if (CurrentChapter != lastChapter)
        {
            lastChapter = CurrentChapter;
            Debug.Log($" 챕터 변경! → {stageData.chapterName} ({CurrentChapter}챕터)");
            OnChapterChanged?.Invoke(CurrentChapter, stageData);
        }

        // ── SO에서 Wave 자동 생성 ──
        currentWave = stageData.GenerateWave(CurrentStageWave, wavesPerStage);

        Debug.Log($"=== Stage {StageLabel} 시작! " +
                  $"[{stageData.GetPreview(CurrentStageWave, wavesPerStage)}] ===");

        // ── UI 업데이트 ──
        if (UIManager.Instance != null)
        {
            string msg = currentWave.isBossWave ? $" {StageLabel} 보스 웨이브!" : $" Stage {StageLabel}";
            Color color = currentWave.isBossWave ? Color.red : Color.yellow;
            UIManager.Instance.ShowMessage(msg, color);
            UIManager.Instance.UpdateStageUI(CurrentChapter, CurrentStageWave);
        }

        OnWaveStarted?.Invoke(CurrentChapter, CurrentStageWave);

        // ── 상태 초기화 ──
        IsSpawning = true;
        RemainingMonsters = currentWave.monsterCount;
        aliveMonsters.Clear();

        // ── ★ 연속 스폰 (PoolManager 사용) ──
        for (int i = 0; i < currentWave.monsterCount; i++)
        {
            bool spawnBoss = currentWave.isBossWave
                             && i == currentWave.monsterCount - 1
                             && currentWave.bossPrefab != null;

            if (spawnBoss) SpawnBoss(currentWave);
            else SpawnMonster(currentWave);

            Debug.Log($"[Stage {StageLabel}] 몬스터 {i + 1}/{currentWave.monsterCount} 스폰");

            if (i < currentWave.monsterCount - 1)
                yield return new WaitForSeconds(currentWave.spawnInterval);
        }

        // ── 스폰 완료 → 전멸 대기 ──
        IsSpawning = false;
        Debug.Log($"Stage {StageLabel} 스폰 완료! 남은 몬스터: {RemainingMonsters}마리");

        yield return new WaitUntil(() => RemainingMonsters <= 0);

        // ── 웨이브 클리어 ──
        Debug.Log($" Stage {StageLabel} 클리어!");
        OnWaveCleared?.Invoke(CurrentChapter, CurrentStageWave);

        if (UIManager.Instance != null)
        {
            string msg = currentWave.isBossWave ? "보스 처치! " : $"Stage {StageLabel} 완료!";
            UIManager.Instance.ShowMessage(msg, Color.green);
        }

        // ── 다음 웨이브 진행 ──
        CurrentWaveIndex++;
        yield return new WaitForSeconds(timeBetweenWaves);
        StartCoroutine(StartWave());
    }

    // ─────────────────────────────────────────
    // ★ 일반 몬스터 스폰 (PoolManager 사용)
    // ─────────────────────────────────────────
    private void SpawnMonster(Wave wave)
    {
        if (wave.monsterPrefab == null) return;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        string poolTag = wave.monsterPrefab.name;

        GameObject monster = PoolManager.Instance.SpawnFromPool(poolTag, spawnPoint.position);

        if (monster == null)
        {
            Debug.LogError($"[WaveSpawner] '{poolTag}' 스폰 실패! " +
                           $"PoolManager Inspector에 tag='{poolTag}' 등록 확인!");

            return;
        }

        aliveMonsters.Add(monster);

        Monster monsterScript = monster.GetComponent<Monster>();
        if (monsterScript != null)
        {
            monsterScript.Initialize(
                poolTag,
                wave.monsterHp,
                wave.monsterDamage,
                wave.monsterMoveSpeed,
                moveDirection
            );
            monsterScript.GoldDrop = wave.goldReward;
            monsterScript.ExpDrop = wave.expReward;

            if (wave.itemDropTable != null && wave.itemDropTable.Length > 0)
                monsterScript.SetItemDropTable(wave.itemDropTable);
        }
    }

    // ─────────────────────────────────────────
    // 보스 스폰 (PoolManager 사용)
    // ─────────────────────────────────────────
    private void SpawnBoss(Wave wave)
    {
        if (wave.bossPrefab == null) { Debug.LogWarning("bossPrefab이 null!"); return; }

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // ★ PoolManager 사용
        string poolTag = wave.bossPrefab.name;
        GameObject boss = PoolManager.Instance.SpawnFromPool(poolTag, spawnPoint.position);

        if (boss == null)
        {
            Debug.LogWarning($"PoolManager에서 '{poolTag}' 보스 스폰 실패!");
            RemainingMonsters--;
            return;
        }

        aliveMonsters.Add(boss);

        BossMonster bossScript = boss.GetComponent<BossMonster>();
        if (bossScript != null)
        {
            int bossHp = wave.monsterHp * 5;
            int bossDamage = wave.monsterDamage * 2;
            float bossSpeed = wave.monsterMoveSpeed * 0.8f;

            bossScript.InitializeBoss(
                wave.monsterPrefab != null ? wave.monsterPrefab.name : poolTag,
                bossHp, bossDamage, bossSpeed,
                moveDirection, wave.bossTitle
            );
            bossScript.GoldDrop = wave.goldReward;
            bossScript.ExpDrop = wave.expReward;

            if (wave.itemDropTable != null && wave.itemDropTable.Length > 0)
                bossScript.SetItemDropTable(wave.itemDropTable);
        }

        Debug.Log($"👑 보스 스폰: {wave.bossTitle} [{poolTag}]");
    }

    // ─────────────────────────────────────────
    // ★ 몬스터 사망 시 Monster.Die()에서 호출
    //   Monster.Die() 안에서:
    //     WaveSpawner.Instance?.OnMonsterKilled(gameObject);
    //     PoolManager.Instance?.ReturnToPool("태그", gameObject);
    // ─────────────────────────────────────────
    public void OnMonsterKilled(GameObject monster)
    {
        aliveMonsters.Remove(monster);
        RemainingMonsters = Mathf.Max(0, RemainingMonsters - 1);
        Debug.Log($" 몬스터 처치! 남은: {RemainingMonsters}마리");
    }

    // ─────────────────────────────────────────
    // 현재 챕터 SO 반환
    // ─────────────────────────────────────────
    private StageDataSO GetCurrentStageData()
    {
        if (stages == null || stages.Length == 0) return null;

        int idx = CurrentChapter - 1;

        if (idx < stages.Length) return stages[idx];
        if (loopLastChapter) return stages[stages.Length - 1]; // 마지막 챕터 무한 반복
        return null;                                                  // 게임 클리어
    }

    private void OnAllWavesCompleted()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage("모든 웨이브 클리어!", Color.yellow);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(10000);
            GameManager.Instance.AddExp(5000);
        }
    }

    // ─────────────────────────────────────────
    // 유틸리티
    // ─────────────────────────────────────────
    public void KillAllAliveMonsters()
    {
        List<GameObject> temp = new List<GameObject>(aliveMonsters);
        foreach (GameObject m in temp)
        {
            if (m == null) continue;
            Monster script = m.GetComponent<Monster>();
            if (script != null) script.Hit(999999);
        }
    }

    public void SkipCurrentWave() => KillAllAliveMonsters();

    // ─────────────────────────────────────────
    // ★ 현재 스테이지(챕터) 처음부터 재시작
    //   플레이어 사망 시 호출
    // ─────────────────────────────────────────
    public void RestartCurrentStage()
    {
        // 현재 챕터의 첫 번째 웨이브 인덱스로 되돌림
        int chapterStartIndex = (CurrentChapter - 1) * wavesPerStage;
        RestartFromWaveIndex(chapterStartIndex);

        Debug.Log($"[WaveSpawner] 챕터 {CurrentChapter} 처음부터 재시작! (wave index: {chapterStartIndex})");
    }

    public void RestartFromWaveIndex(int waveIndex)
    {
        // 진행 중인 코루틴 전부 중단
        StopAllCoroutines();

        // 살아있는 몬스터 즉시 제거
        KillAllAliveMonsters();
        aliveMonsters.Clear();
        RemainingMonsters = 0;
        IsSpawning = false;
        lastChapter = 0; // 챕터 변경 이벤트 재발생 허용

        // 웨이브 인덱스 리셋
        CurrentWaveIndex = waveIndex;

        // 짧은 딜레이 후 웨이브 재시작
        StartCoroutine(DelayedRestartWave());
    }

    private IEnumerator DelayedRestartWave()
    {
        yield return new WaitForSeconds(1.5f);
        StartCoroutine(StartWave());
    }

    public StageDataSO GetCurrentStageDataPublic() => GetCurrentStageData();

    private void CreateDefaultSpawnPoint()
    {
        GameObject sp = new GameObject("SpawnPoint");
        sp.transform.parent = transform;
        sp.transform.position = new Vector3(10f, 0f, 0f);
        spawnPoints = new Transform[] { sp.transform };
    }

    void OnDrawGizmos()
    {
        if (spawnPoints == null) return;
        Gizmos.color = Color.cyan;
        foreach (Transform sp in spawnPoints)
        {
            if (sp == null) continue;
            Gizmos.DrawWireSphere(sp.position, 0.5f);
            Gizmos.DrawLine(sp.position, sp.position + (Vector3)moveDirection.normalized * 2f);
        }
    }
}