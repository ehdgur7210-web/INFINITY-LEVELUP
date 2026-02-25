using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WaveSpawner : MonoBehaviour
{
    public static WaveSpawner Instance { get; private set; }

    public static System.Action<int, StageDataSO> OnChapterChanged;
    public static System.Action<int, int> OnWaveStarted;
    public static System.Action<int, int> OnWaveCleared;

    [Header("스폰 위치")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Vector2 moveDirection = Vector2.left;

    [Header("★ 스테이지 데이터")]
    [SerializeField] private StageDataSO[] stages;
    [SerializeField] private int wavesPerStage = 10;
    [SerializeField] private float timeBetweenWaves = 5f;

    [Header("마지막 챕터 무한 반복?")]
    [SerializeField] private bool loopLastChapter = false;

    public int CurrentWaveIndex { get; set; } = 0;
    public int CurrentChapter => (CurrentWaveIndex / wavesPerStage) + 1;
    public int CurrentStageWave => (CurrentWaveIndex % wavesPerStage) + 1;
    public string StageLabel => $"{CurrentChapter}-{CurrentStageWave}";
    public int RemainingMonsters { get; private set; } = 0;
    public bool IsSpawning { get; private set; } = false;

    private Wave currentWave;
    private List<GameObject> aliveMonsters = new List<GameObject>();
    private int lastChapter = 0;

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

    private IEnumerator StartWave()
    {
        StageDataSO stageData = GetCurrentStageData();
        if (stageData == null)
        {
            Debug.Log("모든 스테이지 클리어!");
            OnAllWavesCompleted();
            yield break;
        }

        if (CurrentChapter != lastChapter)
        {
            lastChapter = CurrentChapter;
            OnChapterChanged?.Invoke(CurrentChapter, stageData);
        }

        currentWave = stageData.GenerateWave(CurrentStageWave, wavesPerStage);

        if (UIManager.Instance != null)
        {
            string msg = currentWave.isBossWave ? $" {StageLabel} 보스 웨이브!" : $" Stage {StageLabel}";
            Color color = currentWave.isBossWave ? Color.red : Color.yellow;
            UIManager.Instance.ShowMessage(msg, color);
            UIManager.Instance.UpdateStageUI(CurrentChapter, CurrentStageWave);
        }

        OnWaveStarted?.Invoke(CurrentChapter, CurrentStageWave);

        // 상태 초기화
        IsSpawning = true;
        RemainingMonsters = currentWave.monsterCount;
        aliveMonsters.Clear();

        // ── ★ 수정된 스폰 로직: 배치(Batch) 스폰 적용 ──
        int spawnedCount = 0;
        while (spawnedCount < currentWave.monsterCount)
        {
            // 이번 주기에서 스폰할 마리 수 계산
            int batchSize = Mathf.Min(currentWave.spawnBatchSize, currentWave.monsterCount - spawnedCount);

            for (int i = 0; i < batchSize; i++)
            {
                // 보스 웨이브이고, 전체 몬스터 중 가장 마지막 한 마리일 때 보스 생성
                bool isLastMonster = (spawnedCount == currentWave.monsterCount - 1);
                bool spawnBoss = currentWave.isBossWave && isLastMonster && currentWave.bossPrefab != null;

                if (spawnBoss) SpawnBoss(currentWave);
                else SpawnMonster(currentWave);

                spawnedCount++;
            }

            Debug.Log($"[Stage {StageLabel}] {batchSize}마리 스폰됨 (진행: {spawnedCount}/{currentWave.monsterCount})");

            // 아직 더 스폰할 몬스터가 있다면 간격만큼 대기
            if (spawnedCount < currentWave.monsterCount)
                yield return new WaitForSeconds(currentWave.spawnInterval);
        }

        IsSpawning = false;
        yield return new WaitUntil(() => RemainingMonsters <= 0);

        OnWaveCleared?.Invoke(CurrentChapter, CurrentStageWave);
        CurrentWaveIndex++;
        yield return new WaitForSeconds(timeBetweenWaves);
        StartCoroutine(StartWave());
    }

    private void SpawnMonster(Wave wave)
    {
        if (wave.monsterPrefab == null) return;

        // 스폰 지점 랜덤 선택 및 겹침 방지를 위한 미세 위치 조정
        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Vector3 spawnPos = sp.position + new Vector3(0, Random.Range(-0.5f, 0.5f), 0);

        string poolTag = wave.monsterPrefab.name;
        GameObject monster = PoolManager.Instance.SpawnFromPool(poolTag, spawnPos);

        if (monster == null) return;

        aliveMonsters.Add(monster);
        Monster monsterScript = monster.GetComponent<Monster>();
        if (monsterScript != null)
        {
            monsterScript.Initialize(poolTag, wave.monsterHp, wave.monsterDamage, wave.monsterMoveSpeed, moveDirection);
            monsterScript.GoldDrop = wave.goldReward;
            monsterScript.ExpDrop = wave.expReward;
            if (wave.itemDropTable != null && wave.itemDropTable.Length > 0)
                monsterScript.SetItemDropTable(wave.itemDropTable);
        }
    }

    private void SpawnBoss(Wave wave)
    {
        if (wave.bossPrefab == null) return;

        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        string poolTag = wave.bossPrefab.name;
        GameObject boss = PoolManager.Instance.SpawnFromPool(poolTag, sp.position);

        if (boss == null) { RemainingMonsters--; return; }

        aliveMonsters.Add(boss);
        BossMonster bossScript = boss.GetComponent<BossMonster>();
        if (bossScript != null)
        {
            bossScript.InitializeBoss(wave.monsterPrefab != null ? wave.monsterPrefab.name : poolTag,
                wave.monsterHp * 5, wave.monsterDamage * 2, wave.monsterMoveSpeed * 0.8f, moveDirection, wave.bossTitle);
            bossScript.GoldDrop = wave.goldReward;
            bossScript.ExpDrop = wave.expReward;
            if (wave.itemDropTable != null && wave.itemDropTable.Length > 0)
                bossScript.SetItemDropTable(wave.itemDropTable);
        }
    }

    public void OnMonsterKilled(GameObject monster)
    {
        if (aliveMonsters.Remove(monster))
        {
            RemainingMonsters = Mathf.Max(0, RemainingMonsters - 1);
        }
    }

    private StageDataSO GetCurrentStageData()
    {
        if (stages == null || stages.Length == 0) return null;
        int idx = CurrentChapter - 1;
        if (idx < stages.Length) return stages[idx];
        if (loopLastChapter) return stages[stages.Length - 1];
        return null;
    }

    private void OnAllWavesCompleted()
    {
        if (UIManager.Instance != null) UIManager.Instance.ShowMessage("모든 웨이브 클리어!", Color.yellow);
        if (GameManager.Instance != null) { GameManager.Instance.AddGold(10000); GameManager.Instance.AddExp(5000); }
    }

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

    public void RestartCurrentStage()
    {
        int chapterStartIndex = (CurrentChapter - 1) * wavesPerStage;
        RestartFromWaveIndex(chapterStartIndex);
    }

    public void RestartFromWaveIndex(int waveIndex)
    {
        StopAllCoroutines();
        KillAllAliveMonsters();
        aliveMonsters.Clear();
        RemainingMonsters = 0;
        IsSpawning = false;
        lastChapter = 0;
        CurrentWaveIndex = waveIndex;
        StartCoroutine(DelayedRestartWave());
    }

    private IEnumerator DelayedRestartWave()
    {
        yield return new WaitForSeconds(1.5f);
        StartCoroutine(StartWave());
    }

    private void CreateDefaultSpawnPoint()
    {
        GameObject sp = new GameObject("SpawnPoint");
        sp.transform.parent = transform;
        sp.transform.position = new Vector3(10f, 0f, 0f);
        spawnPoints = new Transform[] { sp.transform };
    }
}