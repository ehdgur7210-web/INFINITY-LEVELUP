using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// ItemDatabase.cs  【수정본】
//
// [문제] 인트로씬 → 메인씬 전환 시 아이템 데이터가 끊기는 현상
//
// [원인]
//   ItemDatabase가 DontDestroyOnLoad 싱글톤이라서,
//   인트로씬에서 먼저 생성되면 → 아이템이 연결 안 된 채 살아남고,
//   메인씬에 있던 "진짜" ItemDatabase는 중복 판정으로 Destroy됨.
//
// [해결]
//   InitializeDatabase()에서 리스트가 비어있으면
//   Resources 폴더에서 자동으로 로드하도록 폴백 처리 추가.
//   → 어느 씬에서 시작해도 항상 데이터를 가져옴.
// ─────────────────────────────────────────────────────────────────────────────
public class ItemDatabase : MonoBehaviour
{
    // ── 싱글톤 ──────────────────────────────────────────────────────────────
    public static ItemDatabase Instance;

    // ★ SaveLoadManager가 장비 로드 전 준비 여부 확인용
    public bool IsReady { get; private set; } = false;

    [Header("아이템 목록 (Inspector에서 연결하거나, Resources 자동 로드됨)")]
    public List<ItemData> allItems = new List<ItemData>();
    public List<EquipmentData> allEquipments = new List<EquipmentData>();
    public List<QuestData> allQuests = new List<QuestData>();

    // 빠른 검색을 위한 딕셔너리
    private Dictionary<int, ItemData> itemDictionary;
    private Dictionary<int, EquipmentData> equipmentDictionary;
    private Dictionary<int, QuestData> questDictionary;

    // ── Resources 폴더 경로 (실제 폴더 구조와 맞춰주세요) ────────────────────
    // 예) Assets/Resources/Items/ 에 ItemData SO들이 있다면 "Items" 그대로 사용
    private const string ITEMS_RESOURCE_PATH = "Items";
    private const string EQUIPMENTS_RESOURCE_PATH = "Equipments";
    private const string QUESTS_RESOURCE_PATH = "Quests";

    // ────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        // ── 싱글톤 처리 ──────────────────────────────────────────────────────
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 유지
        }
        else
        {
            // ✅ 핵심 수정:
            // 기존에는 중복 인스턴스를 그냥 Destroy했는데,
            // 새로 로드된 씬의 ItemDatabase가 Inspector에 데이터를 갖고 있다면
            // 기존(인트로씬) 인스턴스의 데이터를 덮어쓰고 새것을 없앤다.
            MergeDataFromDuplicate(this); // ✅ 씬 전환 시 데이터 병합
            Destroy(gameObject); // 중복 오브젝트는 제거
            return;
        }

        // 데이터베이스 초기화 (Resources 자동 로드 포함)
        InitializeDatabase();
    }

    // ────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// 중복 인스턴스가 생겼을 때, 새 인스턴스의 데이터를 기존 싱글톤에 병합한다.
    /// (인트로씬 → 메인씬 전환 시, 메인씬 Inspector 데이터를 살리기 위함)
    /// </summary>
    private void MergeDataFromDuplicate(ItemDatabase duplicate)
    {
        bool merged = false;

        // 새 인스턴스에 아이템이 있으면 기존 것을 교체
        if (duplicate.allItems != null && duplicate.allItems.Count > 0)
        {
            Instance.allItems = new List<ItemData>(duplicate.allItems);
            merged = true;
        }
        if (duplicate.allEquipments != null && duplicate.allEquipments.Count > 0)
        {
            Instance.allEquipments = new List<EquipmentData>(duplicate.allEquipments);
            merged = true;
        }
        if (duplicate.allQuests != null && duplicate.allQuests.Count > 0)
        {
            Instance.allQuests = new List<QuestData>(duplicate.allQuests);
            merged = true;
        }

        if (merged)
        {
            // 데이터가 바뀌었으니 딕셔너리 재구성
            Instance.InitializeDatabase();
            Debug.Log("[ItemDatabase] 씬 전환 감지 → 새 씬 데이터로 갱신 완료!");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// 데이터베이스 초기화.
    /// Inspector 연결이 비어 있으면 Resources 폴더에서 자동 로드.
    /// </summary>
    void InitializeDatabase()
    {
        // ── ① Inspector 데이터가 없으면 Resources에서 자동 로드 ────────────
        if (allItems == null || allItems.Count == 0)
        {
            // Resources.LoadAll<T>("폴더명") 으로 해당 폴더의 모든 SO를 가져옴
            ItemData[] loaded = Resources.LoadAll<ItemData>(ITEMS_RESOURCE_PATH);
            allItems = new List<ItemData>(loaded);
            if (allItems.Count > 0)
                Debug.Log($"[ItemDatabase] Resources/{ITEMS_RESOURCE_PATH}에서 아이템 {allItems.Count}개 자동 로드");
            else
                Debug.LogWarning($"[ItemDatabase] Resources/{ITEMS_RESOURCE_PATH} 에서도 아이템을 찾지 못했습니다!");
        }

        if (allEquipments == null || allEquipments.Count == 0)
        {
            EquipmentData[] loaded = Resources.LoadAll<EquipmentData>(EQUIPMENTS_RESOURCE_PATH);
            allEquipments = new List<EquipmentData>(loaded);
            if (allEquipments.Count > 0)
                Debug.Log($"[ItemDatabase] Resources/{EQUIPMENTS_RESOURCE_PATH}에서 장비 {allEquipments.Count}개 자동 로드");
        }

        if (allQuests == null || allQuests.Count == 0)
        {
            QuestData[] loaded = Resources.LoadAll<QuestData>(QUESTS_RESOURCE_PATH);
            allQuests = new List<QuestData>(loaded);
            if (allQuests.Count > 0)
                Debug.Log($"[ItemDatabase] Resources/{QUESTS_RESOURCE_PATH}에서 퀘스트 {allQuests.Count}개 자동 로드");
        }

        // ── ② 딕셔너리 생성 (ID → 데이터 빠른 검색용) ─────────────────────
        BuildDictionaries();
    }

    // ────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// 리스트 → 딕셔너리 변환 (기존 딕셔너리 초기화 후 재구성)
    /// </summary>
    private void BuildDictionaries()
    {
        itemDictionary = new Dictionary<int, ItemData>();
        equipmentDictionary = new Dictionary<int, EquipmentData>();
        questDictionary = new Dictionary<int, QuestData>();

        // ✅ allItems에 EquipmentData가 섞여 있어도 자동으로 분류
        foreach (ItemData item in allItems)
        {
            if (item == null) continue;

            if (item is EquipmentData eq)
            {
                // EquipmentData → equipmentDictionary에 등록
                if (!equipmentDictionary.ContainsKey(eq.itemID))
                    equipmentDictionary.Add(eq.itemID, eq);
            }
            else
            {
                // 일반 아이템 → itemDictionary에 등록
                if (!itemDictionary.ContainsKey(item.itemID))
                    itemDictionary.Add(item.itemID, item);
            }
        }

        // allEquipments에 직접 연결된 것도 추가 (중복 방지)
        foreach (EquipmentData equipment in allEquipments)
        {
            if (equipment != null && !equipmentDictionary.ContainsKey(equipment.itemID))
                equipmentDictionary.Add(equipment.itemID, equipment);
        }

        foreach (QuestData quest in allQuests)
        {
            if (quest != null && !questDictionary.ContainsKey(quest.questID))
                questDictionary.Add(quest.questID, quest);
        }

        // ✅ 명확한 디버그 로그 - 빌드에서도 확인 가능
        Debug.Log($"[ItemDatabase] ===== 초기화 완료 =====");
        Debug.Log($"[ItemDatabase] allItems: {allItems.Count}개 (장비포함)");
        Debug.Log($"[ItemDatabase] allEquipments: {allEquipments.Count}개 (직접연결)");
        Debug.Log($"[ItemDatabase] itemDictionary: {itemDictionary.Count}개");
        Debug.Log($"[ItemDatabase] equipmentDictionary: {equipmentDictionary.Count}개");
        Debug.Log($"[ItemDatabase] questDictionary: {questDictionary.Count}개");

        if (equipmentDictionary.Count == 0)
            Debug.LogError("[ItemDatabase] ❌ 장비 데이터가 0개! 가챠가 동작하지 않습니다!");

        // ★ 초기화 완료 플래그
        IsReady = true;
        Debug.Log("[ItemDatabase] ✅ IsReady = true");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  공개 검색 메서드들 (기존 코드와 동일)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>ID로 일반 아이템 검색</summary>
    public ItemData GetItemByID(int itemID)
    {
        if (itemDictionary.TryGetValue(itemID, out ItemData item))
            return item;

        Debug.LogWarning($"[ItemDatabase] 아이템을 찾을 수 없습니다. ID: {itemID}");
        return null;
    }

    /// <summary>ID로 장비 검색</summary>
    public EquipmentData GetEquipmentByID(int itemID)
    {
        if (equipmentDictionary.TryGetValue(itemID, out EquipmentData equipment))
            return equipment;

        Debug.LogWarning($"[ItemDatabase] 장비를 찾을 수 없습니다. ID: {itemID}");
        return null;
    }

    /// <summary>ID로 퀘스트 검색</summary>
    public QuestData GetQuestByID(int questID)
    {
        if (questDictionary.TryGetValue(questID, out QuestData quest))
            return quest;

        Debug.LogWarning($"[ItemDatabase] 퀘스트를 찾을 수 없습니다. ID: {questID}");
        return null;
    }

    /// <summary>희귀도별 아이템 목록</summary>
    public List<ItemData> GetItemsByRarity(ItemRarity rarity)
        => allItems.Where(item => item != null && item.rarity == rarity).ToList();

    /// <summary>타입별 아이템 목록</summary>
    public List<ItemData> GetItemsByType(ItemType type)
        => allItems.Where(item => item != null && item.itemType == type).ToList();

    /// <summary>장비 타입별 목록</summary>
    public List<EquipmentData> GetEquipmentsByType(EquipmentType type)
        => allEquipments.Where(eq => eq != null && eq.equipmentType == type).ToList();

    /// <summary>플레이어 레벨에 맞는 퀘스트 목록</summary>
    public List<QuestData> GetQuestsForLevel(int playerLevel)
        => allQuests.Where(q =>
            q != null &&
            q.requiredLevel <= playerLevel &&
            q.requiredLevel >= playerLevel - 5
        ).ToList();

    /// <summary>가격 범위로 아이템 검색</summary>
    public List<ItemData> GetItemsByPriceRange(int minPrice, int maxPrice)
        => allItems.Where(item =>
            item != null &&
            item.buyPrice >= minPrice &&
            item.buyPrice <= maxPrice
        ).ToList();

    /// <summary>이름으로 아이템 검색 (부분 일치)</summary>
    public List<ItemData> SearchItemsByName(string searchText)
    {
        searchText = searchText.ToLower();
        return allItems.Where(item =>
            item != null &&
            item.itemName.ToLower().Contains(searchText)
        ).ToList();
    }

    /// <summary>희귀도별 랜덤 아이템 (가챠용)</summary>
    public ItemData GetRandomItemByRarity(ItemRarity rarity)
    {
        List<ItemData> items = GetItemsByRarity(rarity);
        return items.Count > 0 ? items[Random.Range(0, items.Count)] : null;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  에디터 전용 유틸리티
    // ════════════════════════════════════════════════════════════════════════
#if UNITY_EDITOR
    /// <summary>
    /// [에디터 전용] Inspector에서 우클릭 → "Auto Load All Items" 으로 실행.
    /// Resources 폴더의 모든 SO를 Inspector 리스트에 자동 등록.
    /// </summary>
    [ContextMenu("Auto Load All Items")]
    void AutoLoadAllItems()
    {
        allItems = new List<ItemData>(Resources.LoadAll<ItemData>(ITEMS_RESOURCE_PATH));
        allEquipments = new List<EquipmentData>(Resources.LoadAll<EquipmentData>(EQUIPMENTS_RESOURCE_PATH));
        allQuests = new List<QuestData>(Resources.LoadAll<QuestData>(QUESTS_RESOURCE_PATH));

        Debug.Log($"[ItemDatabase] 자동 로드 완료! " +
                  $"아이템:{allItems.Count} / " +
                  $"장비:{allEquipments.Count} / " +
                  $"퀘스트:{allQuests.Count}");

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}