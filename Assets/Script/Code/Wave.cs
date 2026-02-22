

using UnityEngine;

[System.Serializable]
public class Wave
{
    [Header("웨이브 정보")]
    public string waveName = "Wave 1";
    public bool isBossWave = false;

    [Header("일반 몬스터")]
    public GameObject monsterPrefab;
    public int monsterCount = 10;

    [Header("보스 몬스터")]
    public GameObject bossPrefab;
    public string bossTitle = "좀쌘";

    [Header("몬스터 스탯")]
    public float spawnInterval = 2f;
    public float monsterMoveSpeed = 2f;
    public int monsterHp = 100;
    public int monsterDamage = 10;

    [Header("보상")]
    public int goldReward = 10;
    public int expReward = 5;

    [Header("아이템 드롭")]
    public ItemDropData[] itemDropTable;
    public int spawnBatchSize = 5; // 한 번에 동시 스폰할 마리 수
}