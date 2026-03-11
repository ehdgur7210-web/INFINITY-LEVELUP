using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 고급 오브젝트 풀링 시스템
/// - 동적 풀 확장
/// - 풀 상태 통계 관리
/// - 자동 정리 기능
/// - 씬 전환 시 병합 옵션
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
            // ★ 핵심: 부모(ManagerRoot)에서 분리 먼저!
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            InitializePools();
        }
        else
        {
            // ✅ 씬 전환 시 새 PoolManager의 풀을 기존 Instance에 병합
            if (pools != null && pools.Count > 0)
            {
                foreach (Pool newPool in pools)
                {
                    if (newPool == null || newPool.prefab == null) continue;

                    bool exists = Instance.pools.Exists(p => p != null && p.tag == newPool.tag);
                    if (!exists)
                    {
                        Instance.pools.Add(newPool);
                        Instance.CreatePool(newPool);
                    }
                }

                Debug.Log("[PoolManager] 씬 전환 감지 → 풀 병합 완료!");
            }

            enabled = false;
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (Instance != this) return;
        Debug.Log($"[PoolManager] 초기화된 풀 개수: {poolDictionary.Count}");

        foreach (var key in poolDictionary.Keys)
        {
            Debug.Log($"[PoolManager] 등록된 풀: {key}");
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
                Debug.LogWarning($"PoolManager: {pool.tag}의 Prefab이 null입니다!");
                continue;
            }

            CreatePool(pool);
        }
    }

    // ✅ 씬 전환 시 새 풀 추가용
    public void CreatePool(Pool pool)
    {
        if (pool == null || pool.prefab == null) return;
        if (poolDictionary == null) InitializePools();
        if (poolDictionary.ContainsKey(pool.tag)) return;

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

        Debug.Log($"풀 생성 완료: {pool.tag} | 초기 크기: {pool.initialSize} | 최대: {pool.maxSize}");
    }

    private GameObject CreateNewObject(GameObject prefab, Transform parent)
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
        Pool settings = poolSettings[tag];

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
            if (settings.allowDynamicGrowth)
            {
                if (settings.maxSize == 0 || pool.Count < settings.maxSize)
                {
                    objectToSpawn = CreateNewObject(settings.prefab, poolParents[tag]);
                    pool.Enqueue(objectToSpawn);

                    if (enableStatistics)
                        poolStats[tag].dynamicGrowthCount++;

                    Debug.Log($"풀 동적 확장: {tag} (현재 크기: {pool.Count})");
                }
                else
                {
                    Debug.LogWarning($"풀 최대 크기 도달: {tag} (최대: {settings.maxSize})");
                    return null;
                }
            }
            else
            {
                Debug.LogWarning($"더 이상 오브젝트를 생성할 수 없습니다: {tag}");
                return null;
            }
        }

        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        objectToSpawn.transform.SetParent(null);
        // ★ DontDestroyOnLoad 씬이 아닌 현재 활성 씬으로 이동 (씬 전환 시 자동 정리)
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
            objectToSpawn,
            UnityEngine.SceneManagement.SceneManager.GetActiveScene()
        );
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

    public void ReturnToPool(string tag, GameObject obj)
    {
        if (obj == null) return;

        if (!poolParents.ContainsKey(tag))
        {
            Transform existingParent = transform.Find($"Pool_{tag}");

            if (existingParent != null)
                poolParents[tag] = existingParent;
            else
            {
                GameObject parentObj = new GameObject($"Pool_{tag}");
                parentObj.transform.SetParent(transform);
                poolParents[tag] = parentObj.transform;
            }
        }

        obj.SetActive(false);
        obj.transform.SetParent(poolParents[tag]);

        if (enableStatistics && poolStats.ContainsKey(tag))
        {
            poolStats[tag].returnCount++;
            poolStats[tag].currentActiveCount--;
        }
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
            poolStats[tag].currentActiveCount = 0;
    }

    public void DisableAllPools()
    {
        foreach (var tag in poolDictionary.Keys.ToList())
            DisableAllInPool(tag);
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
                    Debug.Log($"풀 정리 완료: {tag} - {cleanedCount}개 제거");
            }
        }
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