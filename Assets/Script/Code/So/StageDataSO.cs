using UnityEngine;

/// <summary>
/// 📁 스테이지 데이터 ScriptableObject
/// 
/// ✅ 사용법:
///   Project 우클릭 → Create → Game/Stage Data
///   파일명: Stage_1, Stage_2, Stage_3 ...
/// 
/// ✅ WaveSpawner Inspector에서:
///   Stages 배열에 SO 파일만 드래그
/// 
/// ✅ PoolManager에 등록 필요:
///   tag = normalMonsterPrefab.name 으로 등록
///   tag = bossPrefab.name 으로 등록
/// </summary>
[CreateAssetMenu(fileName = "Stage_1", menuName = "Game/Stage Data")]
public class StageDataSO : ScriptableObject
{
    [Header("━━━ 챕터 정보 ━━━")]
    public int chapterNumber = 1;
    public string chapterName = "초원";

    [Header("━━━ 환경 ━━━")]
    public Sprite backgroundSprite;
    public AudioClip bgm;
    public Color ambientColor = Color.white;

    [Header("━━━ 몬스터 프리팹 (PoolManager 태그와 이름 일치!) ━━━")]
    public GameObject normalMonsterPrefab;
    public GameObject bossPrefab;
    public string bossTitle = "강력한";

    [Header("━━━ 보스 등장 설정 ━━━")]
    [Tooltip("true = 매 웨이브마다 보스 등장, false = 마지막 웨이브에서만 보스")]
    public bool bossEveryWave = true;

    [Header("━━━ 난이도 커브 (X축 0=1웨이브, 1=마지막웨이브) ━━━")]
    [Tooltip("몬스터 체력")]
    public AnimationCurve hpCurve = AnimationCurve.Linear(0, 100, 1, 300);
    [Tooltip("몬스터 공격력")]
    public AnimationCurve damageCurve = AnimationCurve.Linear(0, 10, 1, 30);
    [Tooltip("웨이브당 몬스터 수")]
    public AnimationCurve countCurve = AnimationCurve.Linear(0, 5, 1, 15);
    [Tooltip("스폰 간격 (초) - 작을수록 빠름")]
    public AnimationCurve intervalCurve = AnimationCurve.Linear(0, 2f, 1, 0.8f);
    [Tooltip("이동 속도")]
    public AnimationCurve speedCurve = AnimationCurve.Linear(0, 2f, 1, 4f);

    [Header("━━━ 보상 커브 ━━━")]
    public AnimationCurve goldCurve = AnimationCurve.Linear(0, 10, 1, 50);
    public AnimationCurve expCurve = AnimationCurve.Linear(0, 5, 1, 25);

    [Header("━━━ 아이템 드롭 ━━━")]
    public ItemDropData[] itemDropTable;

    [Tooltip("한 번에 동시 스폰할 마리 수 (1=기존방식)")]
    public AnimationCurve spawnBatchCurve = AnimationCurve.Linear(0, 1, 1, 3);

    // ─────────────────────────────────────────
    // ★ 핵심 메서드: t값(0~1)으로 Wave 자동 생성
    // waveInStage    : 이번 스테이지의 몇 번째 웨이브? (1부터 시작)
    // wavesPerStage  : 스테이지당 총 웨이브 수 (기본 10)
    // ─────────────────────────────────────────
    public Wave GenerateWave(int waveInStage, int wavesPerStage)
    {
        // t: 0.0 (첫 웨이브) ~ 1.0 (마지막 웨이브)
        float t = (wavesPerStage <= 1) ? 1f : (float)(waveInStage - 1) / (wavesPerStage - 1);

        // ★ bossEveryWave: 매 웨이브마다 보스 등장 / false: 마지막 웨이브만
        bool isBoss = bossEveryWave || (waveInStage == wavesPerStage);

        Wave wave = new Wave();

        wave.waveName = $"Stage {chapterNumber}-{waveInStage}";
        wave.isBossWave = isBoss;
        wave.monsterPrefab = normalMonsterPrefab;
        wave.bossPrefab = bossPrefab;
        wave.bossTitle = bossTitle;

        // 커브로 자동 계산 🎯
        wave.monsterHp = Mathf.RoundToInt(hpCurve.Evaluate(t));
        wave.monsterDamage = Mathf.RoundToInt(damageCurve.Evaluate(t));
        wave.monsterCount = Mathf.RoundToInt(countCurve.Evaluate(t));
        wave.spawnInterval = intervalCurve.Evaluate(t);
        wave.monsterMoveSpeed = speedCurve.Evaluate(t);
        wave.goldReward = Mathf.RoundToInt(goldCurve.Evaluate(t));
        wave.expReward = Mathf.RoundToInt(expCurve.Evaluate(t));
        wave.itemDropTable = itemDropTable;

        wave.spawnBatchSize = Mathf.Max(3, Mathf.RoundToInt(spawnBatchCurve.Evaluate(t)));
        return wave;
    }

    // 인스펙터에서 미리보기용 (디버그)
    public string GetPreview(int waveInStage, int wavesPerStage)
    {
        Wave w = GenerateWave(waveInStage, wavesPerStage);
        return $"[{w.waveName}] HP:{w.monsterHp} DAM:{w.monsterDamage} " +
               $"COUNT:{w.monsterCount} INTERVAL:{w.spawnInterval:F1}s " +
               $"GOLD:{w.goldReward} {(w.isBossWave ? "BOSS" : "")}";
    }
}