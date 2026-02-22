using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 개선된 오브젝트 풀링 시스템
/// - 동적 풀 확장
/// - 풀 통계 및 모니터링
/// - 자동 정리 기능
/// - 프리로드 옵션
/// </summary>
public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    [System.Serializable]
    public class Pool
    {
        public string tag;
        public GameObject prefab;
        public int initialSize = 10;
        public int maxSize = 100;
        public bool allowDynamicGrowth = true;
        public bool preloadOnStart = true;
    }

    [Header("풀 설정")]
    [SerializeField] private List<Pool> pools = new List<Pool>();

    [Header("최적화 설정")]
    [SerializeField] private bool enableStatistics = true;
    [SerializeField] private bool autoCleanup = true;
    [SerializeField] private float cleanupInterval = 60f;

    private Dictionary<string, Queue<GameObject>> poolDictionary;
    private Dictionary<string, Pool> poolSettings;
    private Dictionary<string, Transform> poolParents;
    private Dictionary<string, PoolStatistics> poolStats;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePools();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Debug.Log($"[PoolManager] 초기화된 풀 개수: {poolDictionary.Count}");
        foreach (var key in poolDictionary.Keys)
        {
            Debug.Log($"[PoolManager] 풀 등록됨: {key}");
        }

        if (autoCleanup)
        {
            InvokeRepeating(nameof(CleanupInactivePools), cleanupInterval, cleanupInterval);
        }
    }

    private void InitializePools()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        poolSettings = new Dictionary<string, Pool>();
        poolParents = new Dictionary<string, Transform>();
        poolStats = new Dictionary<string, PoolStatistics>();

        foreach (Pool pool in pools)
        {
            if (pool.prefab == null)
            {
                Debug.LogWarning($"PoolManager: {pool.tag}의 prefab이 null입니다!");
                continue;
            }

            GameObject parentObj = new GameObject($"Pool_{pool.tag}");
            parentObj.transform.SetParent(transform);
            poolParents[pool.tag] = parentObj.transform;

            Queue<GameObject> objectPool = new Queue<GameObject>();

            if (pool.preloadOnStart)
            {
                for (int i = 0; i < pool.initialSize; i++)
                {
                    GameObject obj = CreateNewObject(pool.prefab, parentObj.transform);
                    objectPool.Enqueue(obj);
                }
            }

            poolDictionary[pool.tag] = objectPool;
            poolSettings[pool.tag] = pool;

            if (enableStatistics)
            {
                poolStats[pool.tag] = new PoolStatistics();
            }

            Debug.Log($"풀 생성: {pool.tag} - 초기 크기: {pool.initialSize}, 최대: {pool.maxSize}");
        }
    }

    public GameObject CreateNewObject(GameObject prefab, Transform parent)
    {
        GameObject obj = Instantiate(prefab, parent);
        obj.SetActive(false);
        return obj;
    }

    public GameObject SpawnFromPool(string tag, Vector2 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"풀을 찾을 수 없습니다: {tag}");
            return null;
        }

        GameObject objectToSpawn = null;
        Queue<GameObject> pool = poolDictionary[tag];
        Pool poolSettings = this.poolSettings[tag];

        int checkedCount = 0;
        int poolSize = pool.Count;

        while (checkedCount < poolSize)
        {
            GameObject obj = pool.Dequeue();

            if (obj != null && !obj.activeInHierarchy)
            {
                objectToSpawn = obj;
                pool.Enqueue(obj);
                break;
            }
            else if (obj != null)
            {
                pool.Enqueue(obj);
            }

            checkedCount++;
        }

        if (objectToSpawn == null)
        {
            if (poolSettings.allowDynamicGrowth)
            {
                if (poolSettings.maxSize == 0 || pool.Count < poolSettings.maxSize)
                {
                    objectToSpawn = CreateNewObject(poolSettings.prefab, poolParents[tag]);
                    pool.Enqueue(objectToSpawn);

                    if (enableStatistics)
                    {
                        poolStats[tag].dynamicGrowthCount++;
                    }

                    Debug.Log($"풀 동적 확장: {tag} (현재 크기: {pool.Count})");
                }
                else
                {
                    Debug.LogWarning($"풀 최대 크기 도달: {tag} (최대: {poolSettings.maxSize})");
                    return null;
                }
            }
            else
            {
                Debug.LogWarning($"사용 가능한 오브젝트 없음: {tag}");
                return null;
            }
        }

        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        objectToSpawn.transform.SetParent(null);
        objectToSpawn.SetActive(true);

        if (enableStatistics)
        {
            poolStats[tag].spawnCount++;
            poolStats[tag].currentActiveCount++;
        }

        return objectToSpawn;
    }

    public GameObject SpawnFromPool(string tag, Vector2 position)
    {
        return SpawnFromPool(tag, position, Quaternion.identity);
    }

    /// <summary>
    /// ★ 수정: poolParents가 없으면 재생성 시도
    /// </summary>
    public void ReturnToPool(string tag, GameObject obj)
    {
        Debug.Log($"[PoolManager] ReturnToPool 호출 - tag: {tag}, obj: {obj?.name}");

        if (obj == null)
        {
            Debug.LogWarning("[PoolManager] obj가 null입니다");
            return;
        }

        // ★ poolParents가 없으면 다시 찾기/생성 시도
        if (!poolParents.ContainsKey(tag))
        {
            Debug.LogWarning($"[PoolManager] poolParents에 {tag}가 없음! 재생성 시도...");

            // Pool_{tag} 오브젝트 찾기
            Transform existingParent = transform.Find($"Pool_{tag}");
            if (existingParent != null)
            {
                poolParents[tag] = existingParent;
                Debug.Log($"[PoolManager] 기존 Pool_{tag} 발견하여 등록 완료");
            }
            else
            {
                // 없으면 새로 생성
                GameObject parentObj = new GameObject($"Pool_{tag}");
                parentObj.transform.SetParent(transform);
                poolParents[tag] = parentObj.transform;
                Debug.Log($"[PoolManager] Pool_{tag} 새로 생성하여 등록 완료");
            }
        }

        if (poolParents.ContainsKey(tag))
        {
            Debug.Log($"[PoolManager] {obj.name} 비활성화 및 풀로 반환");
            obj.SetActive(false);
            obj.transform.SetParent(poolParents[tag]);

            if (enableStatistics && poolStats.ContainsKey(tag))
            {
                poolStats[tag].returnCount++;
                poolStats[tag].currentActiveCount--;
            }
        }
        else
        {
            Debug.LogWarning($"[PoolManager] 풀을 찾을 수 없습니다: {tag}");
            Debug.Log($"[PoolManager] 등록된 풀 목록: {string.Join(", ", poolParents.Keys)}");
        }
    }

    public void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;

        foreach (var kvp in poolParents)
        {
            if (obj.transform.parent == kvp.Value)
            {
                ReturnToPool(kvp.Key, obj);
                return;
            }
        }

        string prefabName = obj.name.Replace("(Clone)", "").Trim();
        foreach (var kvp in poolSettings)
        {
            if (kvp.Value.prefab.name == prefabName)
            {
                ReturnToPool(kvp.Key, obj);
                return;
            }
        }

        Debug.LogWarning($"오브젝트의 풀을 찾을 수 없습니다: {obj.name}");
    }

    public void DisableAllInPool(string tag)
    {
        if (!poolDictionary.ContainsKey(tag)) return;

        foreach (GameObject obj in poolDictionary[tag])
        {
            if (obj != null)
            {
                obj.SetActive(false);
                obj.transform.SetParent(poolParents[tag]);
            }
        }

        if (enableStatistics)
        {
            poolStats[tag].currentActiveCount = 0;
        }
    }

    public void DisableAllPools()
    {
        foreach (var tag in poolDictionary.Keys.ToList())
        {
            DisableAllInPool(tag);
        }
    }

    private void CleanupInactivePools()
    {
        foreach (var kvp in poolDictionary)
        {
            string tag = kvp.Key;
            Queue<GameObject> pool = kvp.Value;
            Pool settings = poolSettings[tag];

            if (pool.Count > settings.initialSize)
            {
                int excessCount = pool.Count - settings.initialSize;
                int cleanedCount = 0;

                List<GameObject> tempList = pool.ToList();
                pool.Clear();

                foreach (GameObject obj in tempList)
                {
                    if (obj != null && !obj.activeInHierarchy && cleanedCount < excessCount)
                    {
                        Destroy(obj);
                        cleanedCount++;
                    }
                    else if (obj != null)
                    {
                        pool.Enqueue(obj);
                    }
                }

                if (cleanedCount > 0)
                {
                    Debug.Log($"풀 정리: {tag} - {cleanedCount}개 제거");
                }
            }
        }
    }

    public void PrintPoolStatistics()
    {
        if (!enableStatistics) return;

        Debug.Log("=== 풀 통계 ===");
        foreach (var kvp in poolStats)
        {
            string tag = kvp.Key;
            PoolStatistics stats = kvp.Value;
            int poolSize = poolDictionary[tag].Count;

            Debug.Log($"[{tag}] 크기: {poolSize}, " +
                      $"스폰: {stats.spawnCount}, " +
                      $"반환: {stats.returnCount}, " +
                      $"활성: {stats.currentActiveCount}, " +
                      $"동적확장: {stats.dynamicGrowthCount}");
        }
    }

    public PoolInfo GetPoolInfo(string tag)
    {
        if (!poolDictionary.ContainsKey(tag)) return null;

        PoolInfo info = new PoolInfo
        {
            tag = tag,
            totalSize = poolDictionary[tag].Count,
            activeCount = enableStatistics ? poolStats[tag].currentActiveCount : 0,
            inactiveCount = poolDictionary[tag].Count(obj => obj != null && !obj.activeInHierarchy)
        };

        return info;
    }

    void OnDestroy()
    {
        CancelInvoke();
    }
}

public class PoolStatistics
{
    public int spawnCount = 0;
    public int returnCount = 0;
    public int currentActiveCount = 0;
    public int dynamicGrowthCount = 0;
}

public class PoolInfo
{
    public string tag;
    public int totalSize;
    public int activeCount;
    public int inactiveCount;
}
