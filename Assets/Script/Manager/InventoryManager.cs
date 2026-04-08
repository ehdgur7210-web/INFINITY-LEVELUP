using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 인벤토리 시스템 — 3탭 (장비 / 동료 / 기타)
///
/// [장비 탭]
///   ItemDatabase의 전체 장비를 슬롯에 표시.
///   미보유 → 잠금(Locked) 상태 (실루엣 + 자물쇠).
///   보유 → 해금(Unlocked) 상태 (아이콘 + 수량 + 레어도).
///
/// [판매 규칙]
///   수량 1개 → 판매 불가.
///   수량 2+ → 최대 (보유-1)개 판매 가능.
///
/// [동료/기타 탭]
///   기존 필터 방식 유지 (보유 아이템만 표시).
///
/// [탭 구조]
///   탭별 독립 Container (ScrollView Content).
///   탭 전환 시 해당 Container만 활성화, 나머지 비활성화.
///   슬롯은 활성 Container에만 존재.
///
/// [레이아웃]
///   GridLayoutGroup / ContentSizeFitter는 Inspector에서 직접 설정.
///   코드에서 레이아웃 강제 갱신하지 않음.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    // ★★ 데이터 손실 방지: 로드 완료 플래그
    //    EquipmentManager.IsEquipmentLoaded와 동일한 패턴.
    //    LoadInventoryData가 한 번이라도 정상 호출되어 완료되면 true.
    //    SaveLoadManager.CollectSaveData에서 이 값이 false면 GameDataBridge 폴백 사용
    //    → MainScene 진입 직후, ApplySaveData가 아직 안 돈 시점에 SaveGame이 발화해도
    //       빈 InventoryManager로 JSON을 덮어쓰지 않음.
    public bool IsInventoryLoaded { get; private set; } = false;

    // ★★ 추가 방어: dirty 플래그 — 로드 후 실제로 변경(추가/제거/판매/장착 등)이 일어났는지 추적
    //    false: 변경 없음 → CollectSaveData가 GameDataBridge 폴백을 우선 사용 (메모리 신뢰 안 함)
    //    true:  변경 있음 → 메모리 상태가 진짜 최신이므로 메모리를 사용
    //    LoadInventoryData 직후에는 false로 리셋되고, AddItem/RemoveItem 등이 호출되면 true로 변경됨
    public bool DirtyAfterLoad { get; private set; } = false;
    public void MarkDirty() { DirtyAfterLoad = true; }

    // ★★ 추가 방어: ItemDatabase에 없는 stale ID도 데이터 보존
    //    (몬스터 드롭 아이템이 ItemDatabase에 등록되지 않은 채 저장된 경우 등)
    //    LoadInventoryData에서 찾을 수 없는 itemID는 이 리스트에 보관 → GetInventoryData에서 다시 포함
    //    → 로드/저장 사이클에서 손실되지 않음
    private List<InventoryItemData> pendingUnknownItems = new List<InventoryItemData>();

    // ═══ 탭 타입 ════════════════════════════════════════════════
    public enum InvenTabType { Equip, Companion, Etc }

    [Header("인벤토리 설정")]
    public int inventorySize = 30;

    [Header("슬롯 프리팹 (3종 분리)")]
    [Tooltip("장비 탭 전용 슬롯 프리팹 (EquipmentSlot 컴포넌트)")]
    public GameObject equipSlotPrefab;
    [Tooltip("동료 탭 전용 슬롯 프리팹 (CompanionInventorySlot 컴포넌트)")]
    public GameObject companionSlotPrefab;
    [Tooltip("기타 탭 전용 슬롯 프리팹 (EtcSlot 컴포넌트)")]
    public GameObject etcSlotPrefab;

    [Header("호환용 (3종 미설정 시 폴백)")]
    public GameObject slotPrefab;

    [Header("인벤토리 UI")]
    public GameObject inventoryPanel;

    [Header("탭 버튼 (Inspector 연결)")]
    [Tooltip("순서: 장비, 동료, 기타 (이미지 전용, 텍스트 없음)")]
    public Button[] tabButtons;

    [Header("탭별 컨테이너 (Inspector 연결)")]
    [Tooltip("장비 탭 스크롤뷰 Content")]
    public RectTransform equipmentContainer;
    [Tooltip("동료 탭 스크롤뷰 Content")]
    public RectTransform companionContainer;
    [Tooltip("기타 탭 스크롤뷰 Content")]
    public RectTransform etcContainer;

    [Header("같이 슬라이드할 패널들 (HotSkillPanel, OffLine 등)")]
    public RectTransform[] linkedPanels;

    [Header("채팅 연동")]
    [Tooltip("인벤토리 닫힐 때 채팅창 표시, 열릴 때 숨김")]
    public ChatSystem chatSystem;

    [Header("슬라이드 애니메이션 설정")]
    [Tooltip("패널이 열렸을 때 InvenPanel Y 위치")]
    public float openPosY = 31f;
    [Tooltip("패널이 닫혔을 때 InvenPanel Y 위치")]
    public float closedPosY = -350f;
    [Tooltip("슬라이드 속도 (초)")]
    public float slideDuration = 0.25f;

    // ═══ 탭 색상 ════════════════════════════════════════════════
    private static readonly Color TabSelectedColor = new Color(1f, 0.85f, 0.2f, 1f);
    private static readonly Color TabDeselectedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    private RectTransform panelRect;
    [HideInInspector] public bool isPanelOpen = true;
    private Coroutine slideCoroutine;

    private float[] linkedOpenPosY;
    private float[] linkedClosedPosY;

    // ═══ 슬롯 (탭별 분리 — 3종 전용 컴포넌트) ═══════════════════
    private List<EquipmentSlot> equipSlots = new List<EquipmentSlot>();
    private List<CompanionInventorySlot> companionSlotList = new List<CompanionInventorySlot>();
    private List<EtcSlot> etcSlotList = new List<EtcSlot>();

    // 기존 호환용 (제거 예정)
    private List<InventorySlot> companionSlots = new List<InventorySlot>();
    private List<InventorySlot> etcSlots = new List<InventorySlot>();

    // ═══ 내부 데이터 ═══════════════════════════════════════════
    private Dictionary<int, EquipUnlockData> equipUnlockMap = new Dictionary<int, EquipUnlockData>();
    private List<GeneralItemEntry> generalItemData = new List<GeneralItemEntry>();

    private class EquipUnlockData
    {
        public bool isUnlocked;

        // ★ 인스턴스 리스트 — 각 장비를 개별로 추적 (강화수치/아이템레벨 제각각)
        public List<EquipInstance> instances = new List<EquipInstance>();

        // ─── 호환 프로퍼티 (기존 코드 깨지지 않도록 read-only) ───
        public int count => instances?.Count ?? 0;

        /// <summary>가장 높은 강화 수치 (UI 표시용 — 인벤 슬롯에서 대표값)</summary>
        public int enhanceLevel
        {
            get
            {
                if (instances == null || instances.Count == 0) return 0;
                int max = 0;
                foreach (var ins in instances)
                    if (ins.enhanceLevel > max) max = ins.enhanceLevel;
                return max;
            }
        }

        /// <summary>가장 높은 아이템 레벨</summary>
        public int itemLevel
        {
            get
            {
                if (instances == null || instances.Count == 0) return 0;
                int max = 0;
                foreach (var ins in instances)
                    if (ins.itemLevel > max) max = ins.itemLevel;
                return max;
            }
        }
    }

    /// <summary>장비 인스턴스 — 개별 장비 1개의 강화수치/아이템레벨/장착여부</summary>
    [System.Serializable]
    public class EquipInstance
    {
        public string instanceId;   // GUID, 식별용
        public int enhanceLevel;
        public int itemLevel;
        public bool isEquipped;     // 현재 슬롯에 장착 중인지

        public static EquipInstance Create(int enhance = 0, int level = 0)
        {
            return new EquipInstance
            {
                instanceId = System.Guid.NewGuid().ToString(),
                enhanceLevel = enhance,
                itemLevel = level,
                isEquipped = false
            };
        }
    }

    private class GeneralItemEntry
    {
        public ItemData item;
        public int count;
        public int enhanceLevel;
        public int itemLevel;
        public bool isUnlocked;
    }

    // ═══ 현재 탭 ════════════════════════════════════════════════
    private InvenTabType currentTab = InvenTabType.Equip;

    // ═══ 장비 슬롯 빌드 상태 ══════════════════════════════════
    private bool equipDataReady = false;
    private bool equipSlotsBuilt = false;

    // ═══ 탭별 필터 ══════════════════════════════════════════════
    private static readonly HashSet<ItemType> CompanionFilter = new HashSet<ItemType>
    {
        ItemType.Companion
    };

    private static readonly HashSet<ItemType> EquipFilter = new HashSet<ItemType>
    {
        ItemType.Weapon, ItemType.Armor, ItemType.Accessory, ItemType.Equipment
    };

    private static readonly HashSet<ItemType> EtcFilter = new HashSet<ItemType>
    {
        ItemType.Material,          // ★ 동료 경험치 북 등 재료
        ItemType.Consumable,        // ★ 소모품
        ItemType.FarmVegetable, ItemType.FarmFruit,
        ItemType.GachaTicket_5Star, ItemType.GachaTicket_3to5Star,
        ItemType.OfflineReward_2h, ItemType.OfflineReward_4h,
        ItemType.OfflineReward_8h, ItemType.OfflineReward_12h,
        ItemType.Misc,              // ★ 기타 아이템
    };

    // ═══════════════════════════════════════════════════════════════
    //  Unity 생명주기
    // ═══════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] InventoryManager가 생성되었습니다.");
        }
        else
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (Instance != this) return;

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
            panelRect = inventoryPanel.GetComponent<RectTransform>();

            if (panelRect != null)
            {
                Vector2 pos = panelRect.anchoredPosition;
                pos.y = openPosY;
                panelRect.anchoredPosition = pos;
                isPanelOpen = true;
            }
        }

        float deltaY = closedPosY - openPosY;
        if (linkedPanels != null)
        {
            linkedOpenPosY = new float[linkedPanels.Length];
            linkedClosedPosY = new float[linkedPanels.Length];
            for (int i = 0; i < linkedPanels.Length; i++)
            {
                if (linkedPanels[i] == null) continue;
                float currentY = linkedPanels[i].anchoredPosition.y;
                linkedOpenPosY[i] = currentY;
                linkedClosedPosY[i] = currentY + deltaY;
            }
        }

        SetupTabButtons();

        // ItemDatabase 준비 대기 후 장비 슬롯 빌드 + 탭 선택
        StartCoroutine(WaitForItemDatabase());
    }

    /// <summary>ItemDatabase 준비 완료 대기 → 장비 슬롯 빌드 → 기본 탭 선택</summary>
    private IEnumerator WaitForItemDatabase()
    {
        float waited = 0f;

        // IsReady 또는 allEquipments에 데이터가 있을 때까지 대기 (최대 10초)
        while (true)
        {
            if (ItemDatabase.Instance != null)
            {
                // IsReady가 true이거나, allEquipments에 데이터가 이미 있으면 통과
                if (ItemDatabase.Instance.IsReady)
                    break;
                if (ItemDatabase.Instance.allEquipments != null &&
                    ItemDatabase.Instance.allEquipments.Count > 0)
                    break;
            }

            waited += Time.deltaTime;
            if (waited > 10f)
            {
                Debug.LogError("[InventoryManager] ItemDatabase 대기 타임아웃 (10초)! 장비 슬롯 생성 불가");
                yield break;
            }

            yield return null;
        }

        equipDataReady = true;
        BuildEquipSlots();
        SelectTab(InvenTabType.Equip);

        Debug.Log($"[InventoryManager] ItemDatabase 준비 완료 (대기 {waited:F1}초) → 장비 슬롯: {equipSlots.Count}개");
    }

    // ═══════════════════════════════════════════════════════════════
    //  장비 슬롯 빌드 — 단순 반복문
    // ═══════════════════════════════════════════════════════════════

    // ── 장비 슬롯 풀링 ──────────────────────────────────────────
    private List<GameObject> equipSlotPool = new List<GameObject>();
    private int equipSlotPoolUsed = 0;

    private GameObject GetOrCreateEquipSlot(GameObject prefab)
    {
        // 풀에 여유 슬롯이 있으면 재사용
        if (equipSlotPoolUsed < equipSlotPool.Count)
        {
            GameObject go = equipSlotPool[equipSlotPoolUsed];
            equipSlotPoolUsed++;
            go.SetActive(true);
            return go;
        }

        // 풀 부족 → 새로 생성
        GameObject newGo = Instantiate(prefab, equipmentContainer, false);
        equipSlotPool.Add(newGo);
        equipSlotPoolUsed++;
        return newGo;
    }

    private void ReturnAllEquipSlotsToPool()
    {
        for (int i = 0; i < equipSlotPoolUsed; i++)
        {
            if (i < equipSlotPool.Count && equipSlotPool[i] != null)
                equipSlotPool[i].SetActive(false);
        }
        equipSlotPoolUsed = 0;
    }

    private void BuildEquipSlots()
    {
        GameObject prefab = equipSlotPrefab != null ? equipSlotPrefab : slotPrefab;
        if (prefab == null || equipmentContainer == null) return;
        if (ItemDatabase.Instance == null) return;

        // ★ 풀로 반환 (Destroy 대신)
        ReturnAllEquipSlotsToPool();
        equipSlots.Clear();

        // 전체 장비 수집 (allEquipments + allItems 중 EquipmentData + equipmentDictionary)
        List<EquipmentData> allEquips = new List<EquipmentData>();
        HashSet<int> ids = new HashSet<int>();

        // ★ 1순위: allEquipments 리스트
        if (ItemDatabase.Instance.allEquipments != null)
        {
            foreach (var eq in ItemDatabase.Instance.allEquipments)
            {
                if (eq != null && !ids.Contains(eq.itemID))
                {
                    allEquips.Add(eq);
                    ids.Add(eq.itemID);
                }
            }
        }

        // ★ 2순위: allItems 안의 EquipmentData (Resources/Items에 섞여 있는 경우)
        if (ItemDatabase.Instance.allItems != null)
        {
            foreach (var item in ItemDatabase.Instance.allItems)
            {
                if (item is EquipmentData eqd && !ids.Contains(eqd.itemID))
                {
                    allEquips.Add(eqd);
                    ids.Add(eqd.itemID);
                }
            }
        }

        // ★ 3순위: equipmentDictionary (런타임 RegisterItem으로 등록된 것 포함)
        var eqDict = ItemDatabase.Instance.GetType().GetField("equipmentDictionary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(ItemDatabase.Instance) as System.Collections.IDictionary;
        if (eqDict != null)
        {
            foreach (System.Collections.DictionaryEntry kv in eqDict)
            {
                var eqd = kv.Value as EquipmentData;
                if (eqd != null && !ids.Contains(eqd.itemID))
                {
                    allEquips.Add(eqd);
                    ids.Add(eqd.itemID);
                }
            }
        }

        Debug.Log($"[InventoryManager] BuildEquipSlots: 수집된 장비 {allEquips.Count}개 (allEquipments:{ItemDatabase.Instance.allEquipments?.Count ?? 0}, allItems:{ItemDatabase.Instance.allItems?.Count ?? 0})");


        // 정렬: 레어도(높→낮) → 타입 → ID
        allEquips.Sort((a, b) =>
        {
            int rc = ((int)a.rarity).CompareTo((int)b.rarity);
            if (rc != 0) return rc;
            int tc = ((int)a.equipmentType).CompareTo((int)b.equipmentType);
            if (tc != 0) return tc;
            return a.itemID.CompareTo(b.itemID);
        });

        // ★ 풀에서 슬롯 가져오기 (Instantiate 대신 재사용)
        // ★ allEquips는 List<EquipmentData>이므로 EquipFilter 체크 불필요
        //   (외부 SO가 itemType을 잘못 설정해도 EquipmentData면 무조건 표시)
        foreach (var eq in allEquips)
        {
            if (eq == null) continue;

            GameObject slotObj = GetOrCreateEquipSlot(prefab);
            slotObj.transform.localPosition = Vector3.zero;
            slotObj.transform.localScale = Vector3.one;
            slotObj.transform.SetAsLastSibling();

            // ★★ 두 컴포넌트가 같은 prefab에 공존할 수 있음 (EquipmentSlot 신규 + InventorySlot legacy)
            //    각각 개별 UI 요소와 클릭 핸들러를 가지므로 둘 다 동일한 데이터로 동기화해야 함.
            //    한 쪽만 갱신하면:
            //    - 다른 쪽 stale 상태 → 시각적 충돌 (잠금 표시 덧씌움 등)
            //    - 클릭 핸들러가 stale 데이터로 동작 → 장착/사용 버튼 오작동
            bool isUnlockedAndOwned = equipUnlockMap.TryGetValue(eq.itemID, out EquipUnlockData unlockData)
                                      && unlockData.isUnlocked && unlockData.count > 0;

            // ── 신규 EquipmentSlot 컴포넌트 ──
            EquipmentSlot newSlot = slotObj.GetComponent<EquipmentSlot>();
            if (newSlot != null)
            {
                if (isUnlockedAndOwned)
                    newSlot.SetupUnlocked(eq, unlockData.count, unlockData.enhanceLevel, unlockData.itemLevel);
                else
                    newSlot.SetupLocked(eq);
                equipSlots.Add(newSlot);
            }

            // ── 기존 InventorySlot 컴포넌트 (있으면 같이 갱신) ──
            //    ★ continue 제거: 둘 다 있는 prefab에서 양쪽 모두 동기화해야
            //       클릭 이벤트(InventorySlot에 연결됨)가 정상 동작
            InventorySlot legacySlot = slotObj.GetComponent<InventorySlot>();
            if (legacySlot != null)
            {
                if (isUnlockedAndOwned)
                    legacySlot.SetupUnlocked(eq, unlockData.count, unlockData.enhanceLevel, unlockData.itemLevel);
                else
                    legacySlot.SetupLocked(eq);
            }
        }

        equipSlotsBuilt = true;

        // LayoutGroup 강제 리빌드 — 슬롯 위치 재계산
        LayoutRebuilder.ForceRebuildLayoutImmediate(equipmentContainer);

        Debug.Log($"[InventoryManager] 장비슬롯 빌드: {equipSlots.Count}개 (풀: {equipSlotPool.Count}개)");
    }

    // ═══════════════════════════════════════════════════════════════
    //  동료 슬롯 빌드 (CompanionInventoryManager 데이터 기반)
    // ═══════════════════════════════════════════════════════════════

    private void BuildCompanionSlots()
    {
        RectTransform container = GetContainer(InvenTabType.Companion);
        if (container == null) return;

        // 기존 슬롯 제거
        foreach (Transform child in container)
            Destroy(child.gameObject);
        companionSlotList.Clear();

        var invMgr = CompanionInventoryManager.Instance;
        if (invMgr == null)
        {
            Debug.LogWarning("[InventoryManager] CompanionInventoryManager.Instance null — 동료 슬롯 생성 불가");
            return;
        }

        GameObject prefab = companionSlotPrefab != null ? companionSlotPrefab : slotPrefab;
        if (prefab == null) return;

        var list = invMgr.GetCompanionList();
        int created = 0;

        foreach (var entry in list)
        {
            if (entry == null || entry.data == null) continue;

            GameObject slotObj = Instantiate(prefab, container);
            CompanionInventorySlot cSlot = slotObj.GetComponent<CompanionInventorySlot>();
            if (cSlot == null) cSlot = slotObj.AddComponent<CompanionInventorySlot>();
            cSlot.SetupFromCompanionData(entry.data, entry.count, entry.level, created);
            companionSlotList.Add(cSlot);
            created++;
        }

        // 빈 슬롯 채우기
        int emptyNeeded = Mathf.Max(0, inventorySize - created);
        for (int i = 0; i < emptyNeeded; i++)
        {
            GameObject slotObj = Instantiate(prefab, container);
            CompanionInventorySlot cSlot = slotObj.GetComponent<CompanionInventorySlot>();
            if (cSlot == null) cSlot = slotObj.AddComponent<CompanionInventorySlot>();
            cSlot.ClearSlot();
            companionSlotList.Add(cSlot);
        }

        Debug.Log($"[InventoryManager] 동료 슬롯 빌드: {created}명 + 빈 {emptyNeeded}개");
    }

    // ═══════════════════════════════════════════════════════════════
    //  기타 슬롯 빌드
    // ═══════════════════════════════════════════════════════════════

    private void BuildGeneralSlots(InvenTabType tab)
    {
        RectTransform container = GetContainer(tab);
        if (container == null) return;

        HashSet<ItemType> filter = tab == InvenTabType.Companion ? CompanionFilter : EtcFilter;

        // 기존 슬롯 전부 제거
        foreach (Transform child in container)
            Destroy(child.gameObject);

        if (tab == InvenTabType.Companion)
        {
            companionSlotList.Clear();
            companionSlots.Clear();
        }
        else
        {
            etcSlotList.Clear();
            etcSlots.Clear();
        }

        // 프리팹 결정
        GameObject prefab;
        if (tab == InvenTabType.Companion)
            prefab = companionSlotPrefab != null ? companionSlotPrefab : slotPrefab;
        else
            prefab = etcSlotPrefab != null ? etcSlotPrefab : slotPrefab;

        if (prefab == null) return;

        // 보유 아이템 중 필터에 맞는 것만 슬롯 생성
        int created = 0;

        foreach (var entry in generalItemData)
        {
            if (entry.item == null) continue;
            if (!filter.Contains(entry.item.itemType)) continue;

            GameObject slotObj = Instantiate(prefab, container);

            if (tab == InvenTabType.Companion)
            {
                CompanionInventorySlot cSlot = slotObj.GetComponent<CompanionInventorySlot>();
                if (cSlot == null) cSlot = slotObj.AddComponent<CompanionInventorySlot>();
                cSlot.Setup(entry.item, entry.count, companionSlotList.Count);
                companionSlotList.Add(cSlot);
                created++;
                continue;
            }
            else
            {
                EtcSlot eSlot = slotObj.GetComponent<EtcSlot>();
                MiscInventorySlot mSlot = eSlot == null ? slotObj.GetComponent<MiscInventorySlot>() : null;
                InventorySlot legacySlot = (eSlot == null && mSlot == null) ? slotObj.GetComponent<InventorySlot>() : null;

                // ★ 프리팹에 슬롯 컴포넌트가 하나도 없으면 EtcSlot을 자동 추가
                if (eSlot == null && mSlot == null && legacySlot == null)
                {
                    eSlot = slotObj.AddComponent<EtcSlot>();
                    Debug.LogWarning($"[InventoryManager] ★ 기타Slot 프리팹에 슬롯 컴포넌트 없음 → EtcSlot 자동 추가: {entry.item?.itemName}");
                }

                if (eSlot != null)
                {
                    eSlot.Setup(entry.item, entry.count);
                    etcSlotList.Add(eSlot);
                    created++;
                    continue;
                }
                if (mSlot != null)
                {
                    mSlot.Setup(entry.item, entry.count);
                    created++;
                    continue;
                }
                if (legacySlot != null)
                {
                    legacySlot.SetupUnlocked(entry.item, entry.count, entry.enhanceLevel, entry.itemLevel);
                    etcSlots.Add(legacySlot);
                    created++;
                }
            }
        }

        // 빈 슬롯 추가 (최소 inventorySize개)
        int emptyNeeded = Mathf.Max(0, inventorySize - created);
        for (int i = 0; i < emptyNeeded; i++)
        {
            GameObject slotObj = Instantiate(prefab, container);

            if (tab == InvenTabType.Companion)
            {
                CompanionInventorySlot cSlot = slotObj.GetComponent<CompanionInventorySlot>();
                if (cSlot == null) cSlot = slotObj.AddComponent<CompanionInventorySlot>();
                cSlot.ClearSlot();
                companionSlotList.Add(cSlot);
                continue;
            }
            else
            {
                EtcSlot eSlot = slotObj.GetComponent<EtcSlot>();
                if (eSlot == null) eSlot = slotObj.AddComponent<EtcSlot>(); // ★ 자동 추가
                eSlot.ClearSlot();
                etcSlotList.Add(eSlot);
            }
        }

        Debug.Log($"[InventoryManager] {tab} 슬롯 생성: {created}개 + 빈 {emptyNeeded}개");
    }

    // ═══════════════════════════════════════════════════════════════
    //  탭 시스템
    // ═══════════════════════════════════════════════════════════════

    private void SetupTabButtons()
    {
        if (tabButtons == null || tabButtons.Length < 3) return;

        tabButtons[0].onClick.AddListener(() => SelectTab(InvenTabType.Equip));
        tabButtons[1].onClick.AddListener(() => SelectTab(InvenTabType.Companion));
        tabButtons[2].onClick.AddListener(() => SelectTab(InvenTabType.Etc));
    }

    public void SelectTab(InvenTabType tab)
    {
        currentTab = tab;
        UpdateTabVisuals();
        ActivateContainer(tab);
        Debug.Log($"[InventoryManager] 탭 전환: {tab}");
    }

    private void UpdateTabVisuals()
    {
        if (tabButtons == null) return;

        for (int i = 0; i < tabButtons.Length && i < 3; i++)
        {
            InvenTabType tabType = (InvenTabType)i;
            bool isSelected = tabType == currentTab;

            Image btnImg = tabButtons[i].GetComponent<Image>();
            if (btnImg != null)
                btnImg.color = isSelected ? TabSelectedColor : TabDeselectedColor;
        }
    }

    /// <summary>해당 탭 Container만 활성화, 나머지 비활성화 + 슬롯 빌드</summary>
    private void ActivateContainer(InvenTabType tab)
    {
        // 모든 컨테이너 비활성화
        SetContainerActive(equipmentContainer, false);
        SetContainerActive(companionContainer, false);
        SetContainerActive(etcContainer, false);

        // 해당 탭 컨테이너 활성화
        RectTransform activeContainer = GetContainer(tab);
        SetContainerActive(activeContainer, true);

        // 슬롯 빌드/갱신
        switch (tab)
        {
            case InvenTabType.Equip:
                // ★ 장비 탭은 열 때마다 항상 최신 데이터로 빌드
                if (equipDataReady)
                    BuildEquipSlots();
                break;
            case InvenTabType.Companion:
                BuildCompanionSlots();
                break;
            case InvenTabType.Etc:
                BuildGeneralSlots(InvenTabType.Etc);
                break;
        }
    }

    private void SetContainerActive(RectTransform container, bool active)
    {
        if (container == null) return;

        // Content → Viewport → ScrollView(ScrollRect) 구조를 탐색
        Transform current = container.parent;
        Transform scrollViewObj = null;

        for (int i = 0; i < 3 && current != null; i++)
        {
            if (current.GetComponent<ScrollRect>() != null)
            {
                scrollViewObj = current;
                break;
            }
            current = current.parent;
        }

        if (scrollViewObj != null)
            scrollViewObj.gameObject.SetActive(active);
        else
            container.gameObject.SetActive(active);
    }

    private RectTransform GetContainer(InvenTabType tab)
    {
        switch (tab)
        {
            case InvenTabType.Equip: return equipmentContainer;
            case InvenTabType.Companion: return companionContainer;
            case InvenTabType.Etc: return etcContainer;
            default: return equipmentContainer;
        }
    }

    /// <summary>장비 슬롯의 해금 상태 갱신 (슬롯 재생성 없이 데이터만 업데이트)</summary>
    private void RefreshEquipSlotDisplay()
    {
        foreach (var slot in equipSlots)
        {
            if (slot == null || slot.itemData == null) continue;

            EquipmentData eq = slot.itemData as EquipmentData;
            if (eq == null) continue;

            if (equipUnlockMap.TryGetValue(eq.itemID, out EquipUnlockData data) && data.isUnlocked)
            {
                // count=0이어도 현재 장착 중이면 해금 상태 유지 (해제 가능)
                bool isEquipped = EquipmentManager.Instance != null
                    && EquipmentManager.Instance.IsEquippedByID(eq.itemID);

                if (data.count > 0 || isEquipped)
                    slot.SetupUnlocked(eq, data.count, data.enhanceLevel, data.itemLevel);
                else
                    slot.SetupLocked(eq);
            }
            else
            {
                slot.SetupLocked(eq);
            }
        }
    }

    /// <summary>외부에서 장비 슬롯 UI 강제 갱신 (강화/레벨업/가챠 후 호출)</summary>
    public void RefreshEquipDisplay()
    {
        // ★ 장비 슬롯 재빌드 플래그 설정 — 다음에 장비 탭 열 때 재빌드
        equipSlotsBuilt = false;

        // ★ 현재 장비 탭이면 즉시 갱신
        if (currentTab == InvenTabType.Equip)
            BuildEquipSlots();

        // ★ 장비 패널(EquipPanelSlot)도 즉시 갱신
        EquipmentManager.Instance?.CacheEquipPanelSlots();
    }

    /// <summary>강제 전체 인벤토리 갱신 (가챠 후 등)</summary>
    public void ForceRefreshAll()
    {
        equipSlotsBuilt = false;
        BuildEquipSlots();
    }

    /// <summary>장비 탭 슬롯 부모 반환 (튜토리얼 InvenSlot:N 용)</summary>
    public Transform GetEquipSlotParent() => equipmentContainer;

    /// <summary>현재 탭의 인벤토리 UI를 갱신 (외부 호출용)</summary>
    public void RefreshInventoryUI()
    {
        // inventoryPanel이 None(null)이면 자동 탐색
        if (inventoryPanel == null)
        {
            inventoryPanel = GetComponentInChildren<RectTransform>(true)?.gameObject;

            if (inventoryPanel == null)
            {
                GameObject found = GameObject.Find("InvenPanel")
                                ?? GameObject.Find("InventoryPanel")
                                ?? GameObject.Find("Inventory Panel");
                if (found != null) inventoryPanel = found;
            }

            if (inventoryPanel != null)
            {
                panelRect = inventoryPanel.GetComponent<RectTransform>();
                Debug.Log($"[InventoryManager] inventoryPanel 자동 탐색 성공: {inventoryPanel.name}");
            }
            else
            {
                Debug.LogWarning("[InventoryManager] inventoryPanel을 찾을 수 없습니다! UI 갱신만 진행합니다.");
            }
        }

        ActivateContainer(currentTab);
    }

    // ═══════════════════════════════════════════════════════════════
    //  패널 참조 / 슬롯 재생성
    // ═══════════════════════════════════════════════════════════════

    public void RefreshPanelRef()
    {
        if (inventoryPanel == null) return;
        panelRect = inventoryPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            Vector2 pos = panelRect.anchoredPosition;
            pos.y = openPosY;
            panelRect.anchoredPosition = pos;
            isPanelOpen = true;
        }
        float deltaY = closedPosY - openPosY;
        if (linkedPanels != null)
        {
            linkedOpenPosY = new float[linkedPanels.Length];
            linkedClosedPosY = new float[linkedPanels.Length];
            for (int i = 0; i < linkedPanels.Length; i++)
            {
                if (linkedPanels[i] == null) continue;
                linkedOpenPosY[i] = linkedPanels[i].anchoredPosition.y;
                linkedClosedPosY[i] = linkedOpenPosY[i] + deltaY;
            }
        }

        SetupTabButtons();
        SelectTab(InvenTabType.Equip);

        Debug.Log("[InventoryManager] UI 참조 재연결 완료");
    }

    public void RebuildSlots()
    {
        if (slotPrefab == null) return;

        equipSlotsBuilt = false;
        ActivateContainer(currentTab);

        Debug.Log($"[InventoryManager] 슬롯 재생성 완료 (장비:{equipSlots.Count} / 동료:{companionSlots.Count} / 기타:{etcSlots.Count})");
    }

    // ═══════════════════════════════════════════════════════════════
    //  슬라이드 토글
    // ═══════════════════════════════════════════════════════════════

    public void ToggleInventory()
    {
        // ★ 튜토리얼 차단 — 단, 인벤토리 버튼이 포커스 대상이면 허용
        if (TutorialManager.Instance != null && TutorialManager.Instance.ShouldBlockNonFocusButtons)
        {
            var step = TutorialManager.Instance.GetCurrentStep();
            string fn = step?.focusTargetName ?? "";
            if (fn != "MenuInventoryBtn" && !fn.StartsWith("InvenSlot:"))
                return;
        }
        if (panelRect == null)
        {
            if (inventoryPanel != null)
                panelRect = inventoryPanel.GetComponent<RectTransform>();
            if (panelRect == null)
            {
                Debug.LogWarning("[InventoryManager] panelRect를 찾을 수 없어 토글을 건너뜁니다.");
                return;
            }
        }

        isPanelOpen = !isPanelOpen;
        float targetY = isPanelOpen ? openPosY : closedPosY;

        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);

        slideCoroutine = StartCoroutine(SlidePanel(targetY));

        bool tutorialActive = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;
        var chat = chatSystem != null ? chatSystem : ChatSystem.Instance;
        if (chat != null)
        {
            if (!isPanelOpen)
                chat.ShowChat();
            else if (!tutorialActive)
                chat.HideChat();
        }

        Debug.Log($"[InventoryManager] 인벤 패널 {(isPanelOpen ? "열기" : "닫기")} → Y={targetY}");

        if (isPanelOpen)
            TutorialManager.Instance?.OnActionCompleted("OpenInventory");
    }

    public void OpenInventory()
    {
        if (isPanelOpen) return;
        ForceSlide(true);
    }

    public void CloseInventory()
    {
        if (!isPanelOpen) return;
        ForceSlide(false);
    }

    /// <summary>튜토리얼 차단 무시하고 강제 슬라이드 (내부/튜토리얼 호출용)</summary>
    private void ForceSlide(bool open)
    {
        if (panelRect == null)
        {
            if (inventoryPanel != null)
                panelRect = inventoryPanel.GetComponent<RectTransform>();
            if (panelRect == null) return;
        }

        isPanelOpen = open;
        float targetY = open ? openPosY : closedPosY;

        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);

        // ★ 튜토리얼 중에는 슬라이드 애니메이션 없이 즉시 이동 (내려갔다 올라오는 현상 방지)
        bool tutorialActive = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;
        if (tutorialActive)
        {
            Vector2 pos = panelRect.anchoredPosition;
            pos.y = targetY;
            panelRect.anchoredPosition = pos;

            // 연결 패널도 즉시 이동
            if (linkedPanels != null && linkedOpenPosY != null)
            {
                for (int i = 0; i < linkedPanels.Length; i++)
                {
                    if (linkedPanels[i] == null) continue;
                    Vector2 lp = linkedPanels[i].anchoredPosition;
                    lp.y = open ? linkedOpenPosY[i] : linkedClosedPosY[i];
                    linkedPanels[i].anchoredPosition = lp;
                }
            }
        }
        else
        {
            slideCoroutine = StartCoroutine(SlidePanel(targetY));
        }

        var chat = chatSystem != null ? chatSystem : ChatSystem.Instance;
        if (chat != null)
        {
            if (!open) chat.ShowChat();
            else if (!tutorialActive) chat.HideChat();
        }

        Debug.Log($"[InventoryManager] 강제 슬라이드 {(open ? "열기" : "닫기")} → Y={targetY}" +
                  (tutorialActive ? " (즉시 이동)" : ""));

        if (open)
            TutorialManager.Instance?.OnActionCompleted("OpenInventory");
    }

    private IEnumerator SlidePanel(float targetY)
    {
        float startY = panelRect.anchoredPosition.y;
        bool isOpening = (targetY == openPosY);

        float[] linkedStartY = null;
        float[] linkedTargetY = null;

        if (linkedPanels != null && linkedOpenPosY != null)
        {
            linkedStartY = new float[linkedPanels.Length];
            linkedTargetY = new float[linkedPanels.Length];
            for (int i = 0; i < linkedPanels.Length; i++)
            {
                if (linkedPanels[i] == null) continue;
                linkedStartY[i] = linkedPanels[i].anchoredPosition.y;
                linkedTargetY[i] = isOpening ? linkedOpenPosY[i] : linkedClosedPosY[i];
            }
        }

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            Vector2 pos = panelRect.anchoredPosition;
            pos.y = Mathf.Lerp(startY, targetY, eased);
            panelRect.anchoredPosition = pos;

            if (linkedPanels != null && linkedStartY != null)
            {
                for (int i = 0; i < linkedPanels.Length; i++)
                {
                    if (linkedPanels[i] == null) continue;
                    Vector2 lp = linkedPanels[i].anchoredPosition;
                    lp.y = Mathf.Lerp(linkedStartY[i], linkedTargetY[i], eased);
                    linkedPanels[i].anchoredPosition = lp;
                }
            }

            yield return null;
        }

        Vector2 finalPos = panelRect.anchoredPosition;
        finalPos.y = targetY;
        panelRect.anchoredPosition = finalPos;

        if (linkedPanels != null && linkedTargetY != null)
        {
            for (int i = 0; i < linkedPanels.Length; i++)
            {
                if (linkedPanels[i] == null) continue;
                Vector2 lp = linkedPanels[i].anchoredPosition;
                lp.y = linkedTargetY[i];
                linkedPanels[i].anchoredPosition = lp;
            }
        }

        slideCoroutine = null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  아이템 추가
    // ═══════════════════════════════════════════════════════════════

    public bool AddItem(ItemData item, int count = 1)
    {
        return AddItem(item, count, true);
    }

    /// <summary>아이템 추가 (refreshUI=false로 대량 추가 시 UI 갱신 생략)</summary>
    public bool AddItem(ItemData item, int count, bool refreshUI)
    {
        if (item == null)
        {
            Debug.LogWarning("[InventoryManager] 추가할 아이템이 null입니다!");
            return false;
        }

        if (refreshUI)
            Debug.Log($"[InventoryManager] 아이템 추가: {item.itemName} x{count}");

        // ★★ ItemDatabase에 자동 등록 (장비/일반 모두) — 안 하면 다음 LoadInventoryData에서
        //    "ID 찾을 수 없음" 으로 드롭되어 영구 손실 발생
        if (ItemDatabase.Instance != null)
            ItemDatabase.Instance.RegisterItem(item);

        // ★ dirty 플래그 설정 (CollectSaveData가 메모리를 신뢰하도록)
        DirtyAfterLoad = true;

        if (item is EquipmentData)
            return AddEquipItem(item, count, 0, 0, refreshUI);

        return AddGeneralItem(item, count, 0, 0);
    }

    public bool AddItemWithEnhancement(ItemData item, int count, int enhanceLevel)
    {
        if (item == null) return false;

        Debug.Log($"[InventoryManager] 아이템 추가: {item.itemName} x{count} +{enhanceLevel}");

        if (item is EquipmentData)
            return AddEquipItem(item, count, enhanceLevel, 0);

        return AddGeneralItem(item, count, enhanceLevel, 0);
    }

    private bool AddEquipItem(ItemData item, int count, int enhance, int level, bool refreshUI = true)
    {
        int id = item.itemID;

        // ★ ItemDatabase에 미등록된 장비면 자동 등록 (오프라인 보상 등에서 외부 SO가 들어올 때)
        if (item is EquipmentData equipData && ItemDatabase.Instance != null && ItemDatabase.Instance.allEquipments != null)
        {
            bool foundInDB = false;
            foreach (var eq in ItemDatabase.Instance.allEquipments)
            {
                if (eq != null && eq.itemID == id) { foundInDB = true; break; }
            }
            if (!foundInDB)
            {
                ItemDatabase.Instance.allEquipments.Add(equipData);
                Debug.Log($"[InventoryManager] ItemDatabase에 자동 등록: {item.itemName} (ID:{id})");
            }
        }

        if (!equipUnlockMap.TryGetValue(id, out EquipUnlockData data))
        {
            data = new EquipUnlockData { isUnlocked = true };
            equipUnlockMap[id] = data;
        }

        // ★ count만큼 인스턴스를 새로 추가 (각각 enhance/level은 인자값으로)
        data.isUnlocked = true;
        for (int i = 0; i < count; i++)
        {
            data.instances.Add(EquipInstance.Create(enhance, level));
        }

        Debug.Log($"[InventoryManager] 장비 해금/추가: {item.itemName} x{count} (총 {equipUnlockMap[id].count}개)");

        // refreshUI=false면 데이터만 갱신, UI 재빌드는 호출자가 나중에 처리
        equipSlotsBuilt = false;
        if (refreshUI)
            BuildEquipSlots();

        return true;
    }

    private bool AddGeneralItem(ItemData item, int count, int enhance, int level)
    {
        if (item.maxStack > 1)
        {
            foreach (var entry in generalItemData)
            {
                if (entry.item == item && entry.count < item.maxStack)
                {
                    int space = item.maxStack - entry.count;
                    int add = Mathf.Min(count, space);
                    entry.count += add;
                    count -= add;
                    if (count <= 0)
                    {
                        RefreshCurrentTabIfGeneral();
                        return true;
                    }
                }
            }
        }

        while (count > 0)
        {
            if (generalItemData.Count >= inventorySize)
            {
                if (MailManager.Instance != null)
                {
                    MailManager.Instance.SendItemToMail(item, count, "인벤토리가 꽉 찼습니다");
                    UIManager.Instance?.ShowMessage($"인벤토리가 꽉 찼습니다!\n{item.itemName}이(가) 메일함으로 전송되었습니다.", Color.yellow);
                    return true;
                }
                UIManager.Instance?.ShowMessage("인벤토리가 꽉 찼습니다!", Color.red);
                return false;
            }

            int add = Mathf.Min(count, item.maxStack);
            generalItemData.Add(new GeneralItemEntry
            {
                item = item,
                count = add,
                enhanceLevel = enhance,
                itemLevel = level,
                isUnlocked = true
            });
            count -= add;
        }

        RefreshCurrentTabIfGeneral();
        return true;
    }

    private void RefreshCurrentTabIfGeneral()
    {
        // ★ 동료 탭은 리빌드하지 않음 — 아이템 추가(경험치 북 등)로 동료 슬롯이
        //   재생성되면 열려있는 동료 상세 패널이 사라지는 버그 방지
        if (currentTab == InvenTabType.Etc)
            BuildGeneralSlots(InvenTabType.Etc);
    }

    // ═══════════════════════════════════════════════════════════════
    //  아이템 제거
    // ═══════════════════════════════════════════════════════════════

    public bool RemoveItem(ItemData item, int count = 1)
    {
        if (item == null) return false;

        if (item is EquipmentData)
            return RemoveEquipItem(item, count);

        return RemoveGeneralItem(item, count);
    }

    private bool RemoveEquipItem(ItemData item, int count)
    {
        int id = item.itemID;
        if (!equipUnlockMap.TryGetValue(id, out EquipUnlockData data))
        {
            Debug.LogWarning($"[InventoryManager] 장비 {item.itemName} 미보유!");
            return false;
        }

        if (data.count < count)
        {
            Debug.LogWarning($"[InventoryManager] 장비 {item.itemName} 수량 부족! ({data.count} < {count})");
            return false;
        }

        // ★ 인스턴스 리스트의 끝에서부터 count개 제거
        //   (강화 수치 낮은 것부터 제거하고 싶다면 정렬 후 제거)
        for (int i = 0; i < count && data.instances.Count > 0; i++)
        {
            data.instances.RemoveAt(data.instances.Count - 1);
        }
        Debug.Log($"[InventoryManager] 장비 제거: {item.itemName} x{count} (남은: {data.count})");

        if (currentTab == InvenTabType.Equip)
            RefreshEquipSlotDisplay();

        return true;
    }

    private bool RemoveGeneralItem(ItemData item, int count)
    {
        int remaining = count;
        for (int i = generalItemData.Count - 1; i >= 0; i--)
        {
            var entry = generalItemData[i];
            if (entry.item != item) continue;

            if (entry.count > remaining)
            {
                entry.count -= remaining;
                remaining = 0;
                break;
            }
            else
            {
                remaining -= entry.count;
                generalItemData.RemoveAt(i);
                if (remaining <= 0) break;
            }
        }

        if (remaining > 0)
        {
            Debug.LogWarning($"[InventoryManager] {item.itemName} 부족!");
            return false;
        }

        RefreshCurrentTabIfGeneral();
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  조회
    // ═══════════════════════════════════════════════════════════════

    public int GetItemCount(ItemData item)
    {
        if (item == null) return 0;

        if (item is EquipmentData)
        {
            if (equipUnlockMap.TryGetValue(item.itemID, out EquipUnlockData data))
                return data.count;
            return 0;
        }

        int total = 0;
        foreach (var entry in generalItemData)
            if (entry.item == item) total += entry.count;
        return total;
    }

    public bool HasItem(ItemData item, int count = 1)
    {
        return GetItemCount(item) >= count;
    }

    public List<ItemData> GetAllItems()
    {
        List<ItemData> items = new List<ItemData>();

        foreach (var kv in equipUnlockMap)
        {
            if (kv.Value.isUnlocked && kv.Value.count > 0)
            {
                ItemData item = ItemDatabase.Instance?.GetEquipmentByID(kv.Key);
                if (item != null) items.Add(item);
            }
        }

        foreach (var entry in generalItemData)
            if (entry.item != null) items.Add(entry.item);

        return items;
    }

    public bool HasSpace()
    {
        return generalItemData.Count < inventorySize;
    }

    public int GetEmptySlotCount()
    {
        return Mathf.Max(0, inventorySize - generalItemData.Count);
    }

    public InventorySlot[] GetAllSlots()
    {
        List<InventorySlot> all = new List<InventorySlot>();
        // 기존 호환용 슬롯
        all.AddRange(companionSlots);
        all.AddRange(etcSlots);
        return all.ToArray();
    }

    /// <summary>장비 슬롯 전체 반환 (새 EquipmentSlot 타입)</summary>
    public List<EquipmentSlot> GetEquipSlots() => equipSlots;

    /// <summary>동료 슬롯 전체 반환 (CompanionInventorySlot 타입)</summary>
    public List<CompanionInventorySlot> GetCompanionSlots() => companionSlotList;

    /// <summary>기타 슬롯 전체 반환 (새 EtcSlot 타입)</summary>
    public List<EtcSlot> GetEtcSlots() => etcSlotList;

    private void UpdateInventoryUI()
    {
        ActivateContainer(currentTab);
    }

    public void ClearAllItems()
    {
        equipUnlockMap.Clear();
        generalItemData.Clear();

        // 장비 풀 오브젝트 전부 파괴
        foreach (var go in equipSlotPool) if (go != null) Destroy(go);
        equipSlotPool.Clear();
        equipSlotPoolUsed = 0;

        foreach (var slot in companionSlotList) if (slot != null) Destroy(slot.gameObject);
        foreach (var slot in etcSlotList) if (slot != null) Destroy(slot.gameObject);
        foreach (var slot in companionSlots) if (slot != null) Destroy(slot.gameObject);
        foreach (var slot in etcSlots) if (slot != null) Destroy(slot.gameObject);
        equipSlots.Clear();
        companionSlotList.Clear();
        etcSlotList.Clear();
        companionSlots.Clear();
        etcSlots.Clear();

        equipSlotsBuilt = false;

        Debug.Log("[InventoryManager] 인벤토리 전체 초기화 완료");
    }

    // ═══════════════════════════════════════════════════════════════
    //  장비 해금 데이터 동기화
    // ═══════════════════════════════════════════════════════════════

    public void SyncEquipSlotToMap(int itemID, int count, int enhance, int level)
    {
        if (equipUnlockMap.TryGetValue(itemID, out EquipUnlockData data))
        {
            // ★ 인스턴스 리스트 기반으로 동기화
            //   count가 줄면 끝에서부터 제거, 늘면 새 인스턴스 추가
            //   enhance/level은 첫 번째 인스턴스에만 반영 (단일 강화 호환)
            while (data.instances.Count > count)
                data.instances.RemoveAt(data.instances.Count - 1);
            while (data.instances.Count < count)
                data.instances.Add(EquipInstance.Create(enhance, level));

            // 첫 번째 인스턴스의 enhance/level 갱신 (기존 단일 강화 시스템과 호환)
            if (data.instances.Count > 0)
            {
                data.instances[0].enhanceLevel = enhance;
                data.instances[0].itemLevel = level;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  인덱스 기반 제거
    // ═══════════════════════════════════════════════════════════════

    /// <summary>슬롯 인덱스로 장비 아이템 1개 제거</summary>
    public bool RemoveItemAt(int slotIndex)
    {
        // 장비(equipUnlockMap) — slotIndex는 의미 없으므로 무시, itemID 기반 제거
        // 일반 아이템(generalItemData) — slotIndex가 리스트 인덱스
        if (slotIndex >= 0 && slotIndex < generalItemData.Count)
        {
            var entry = generalItemData[slotIndex];
            if (entry.count > 1)
                entry.count--;
            else
                generalItemData.RemoveAt(slotIndex);

            RefreshCurrentTabIfGeneral();
            Debug.Log($"[InventoryManager] RemoveItemAt({slotIndex}) 완료");
            return true;
        }

        Debug.LogWarning($"[InventoryManager] RemoveItemAt({slotIndex}) — 유효하지 않은 인덱스");
        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    //  레벨업 패널 열기
    // ═══════════════════════════════════════════════════════════════

    /// <summary>인벤토리에서 장비 레벨업 패널 열기</summary>
    public void OpenLevelUpPanel(InventoryItemData invItem, int slotIndex)
    {
        // ★ 튜토리얼 중 레벨업 패널 열기 차단
        if (TutorialManager.Instance != null && TutorialManager.Instance.ShouldBlockNonFocusButtons)
        {
            Debug.Log("[InventoryManager] 튜토리얼 중 레벨업 패널 차단");
            return;
        }

        if (invItem == null) return;

        Debug.Log($"[InventoryManager] OpenLevelUpPanel 호출: itemID={invItem.itemID}, slotIndex={slotIndex}");

        // EquipmentData 조회
        EquipmentData equipment = ItemDatabase.Instance?.GetEquipmentByID(invItem.itemID);
        if (equipment == null)
        {
            Debug.LogWarning($"[InventoryManager] OpenLevelUpPanel: itemID={invItem.itemID} 장비 없음");
            UIManager.Instance?.ShowMessage("장비 정보를 찾을 수 없습니다!", Color.red);
            return;
        }

        // EquipmentSlot 찾기
        EquipmentSlot targetSlot = FindEquipSlotByItemID(invItem.itemID);

        // ★ EquipmentSlot이 없으면 임시 슬롯 생성 (InventorySlot 폴백 시)
        if (targetSlot == null)
        {
            Debug.Log($"[InventoryManager] EquipmentSlot 없음 → 임시 슬롯 생성: {equipment.itemName}");
            GameObject tempGO = new GameObject("TempEquipSlot", typeof(RectTransform));
            tempGO.transform.SetParent(transform, false);
            targetSlot = tempGO.AddComponent<EquipmentSlot>();
            targetSlot.SetupUnlocked(equipment, invItem.count, invItem.enhanceLevel, invItem.itemLevel);
        }

        // ★ EquipmentLevelUpPanel 자동 탐색/생성
        if (EquipmentLevelUpPanel.Instance == null)
        {
            var found = FindObjectOfType<EquipmentLevelUpPanel>(true);
            if (found != null)
            {
                found.gameObject.SetActive(true);
                Debug.Log("[InventoryManager] EquipmentLevelUpPanel 비활성 → 활성화");
            }
        }

        // EquipmentLevelUpPanel으로 열기
        if (EquipmentLevelUpPanel.Instance != null)
        {
            Debug.Log($"[InventoryManager] EquipmentLevelUpPanel.Open: {equipment.itemName} Lv.{invItem.itemLevel}");
            EquipmentLevelUpPanel.Instance.Open(targetSlot, equipment, invItem.itemLevel);
            return;
        }

        // 폴백: EquipmentSlotActionPanel
        if (EquipmentSlotActionPanel.Instance != null)
        {
            Debug.Log($"[InventoryManager] 폴백 → EquipmentSlotActionPanel.Open: {equipment.itemName}");
            EquipmentSlotActionPanel.Instance.Open(targetSlot, equipment);
            return;
        }

        Debug.LogError("[InventoryManager] EquipmentLevelUpPanel/EquipmentSlotActionPanel 모두 없음!");
        UIManager.Instance?.ShowMessage("레벨업 패널을 찾을 수 없습니다", Color.red);
    }

    /// <summary>장비 슬롯 리스트에서 itemID로 슬롯 찾기</summary>
    private EquipmentSlot FindEquipSlotByItemID(int itemID)
    {
        foreach (var slot in equipSlots)
        {
            if (slot != null && slot.itemData != null && slot.itemData.itemID == itemID)
                return slot;
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  판매 (수량 제한: 최소 1개 남김)
    // ═══════════════════════════════════════════════════════════════

    public int GetMaxSellCount(ItemData item)
    {
        int have = GetItemCount(item);
        return Mathf.Max(0, have - 1);
    }

    public bool CanSell(ItemData item)
    {
        return GetMaxSellCount(item) > 0;
    }

    // ═══════════════════════════════════════════════════════════════
    //  저장/로드
    // ═══════════════════════════════════════════════════════════════

    public InventoryItemData[] GetInventoryData()
    {
        List<InventoryItemData> dataList = new List<InventoryItemData>();

        foreach (var kv in equipUnlockMap)
        {
            if (!kv.Value.isUnlocked) continue;

            // ★★ 빈 entry 자동 복구: instances가 비어있으면 즉시 1개 채워서 저장
            //    (이전 버그로 instances 손실된 케이스를 SAVE 시점에 복구)
            //    이렇게 안 하면 SAVE에 count=0/instances=[]가 들어가서 LOAD에서 SetupLocked → 어두운 슬롯
            if (kv.Value.instances == null || kv.Value.instances.Count == 0)
            {
                if (kv.Value.instances == null)
                    kv.Value.instances = new List<EquipInstance>();
                kv.Value.instances.Add(EquipInstance.Create(0, 1));
                Debug.LogWarning($"[Inv-SAVE] ID:{kv.Key} 빈 instances 감지 → 기본 1개 자동 복구 후 저장");
            }

            // ★ 인스턴스 리스트를 SaveData 형식으로 변환
            var saveInstances = new List<EquipInstanceData>();
            foreach (var ins in kv.Value.instances)
            {
                saveInstances.Add(new EquipInstanceData
                {
                    instanceId = ins.instanceId,
                    enhanceLevel = ins.enhanceLevel,
                    itemLevel = ins.itemLevel,
                    isEquipped = ins.isEquipped
                });
            }

            dataList.Add(new InventoryItemData
            {
                itemID = kv.Key,
                count = kv.Value.count,        // 호환용 (count = instances.Count)
                slotIndex = -1,
                enhanceLevel = kv.Value.enhanceLevel,  // 호환용 (max)
                isUnlocked = true,
                itemLevel = kv.Value.itemLevel, // 호환용 (max)
                instances = saveInstances       // ★ 신규 형식
            });
        }

        for (int i = 0; i < generalItemData.Count; i++)
        {
            var entry = generalItemData[i];
            if (entry.item == null) continue;

            dataList.Add(new InventoryItemData
            {
                itemID = entry.item.itemID,
                count = entry.count,
                slotIndex = i,
                enhanceLevel = entry.enhanceLevel,
                isUnlocked = entry.isUnlocked,
                itemLevel = entry.itemLevel
            });
        }

        // ★★ pending unknown 아이템도 포함 — ItemDatabase에 없어서 로드 실패한 아이템 보존
        //    다음 세션에서 해당 SO가 등록되면 자연스럽게 복원됨
        if (pendingUnknownItems != null && pendingUnknownItems.Count > 0)
        {
            dataList.AddRange(pendingUnknownItems);
            Debug.Log($"[InventoryManager] 저장 데이터 수집: {dataList.Count}개 (장비 해금: {equipUnlockMap.Count}, 보존된 unknown: {pendingUnknownItems.Count})");
        }
        else
        {
            Debug.Log($"[InventoryManager] 저장 데이터 수집: {dataList.Count}개 (장비 해금: {equipUnlockMap.Count})");
        }
        return dataList.ToArray();
    }

    public void LoadInventoryData(InventoryItemData[] items)
    {
        if (items == null || items.Length == 0)
        {
            // ★ 빈 데이터로 호출돼도 "처음 로드된 적 있음" 신호로만 사용 — ClearAllItems 하지 않음
            //   (빈 배열로 ClearAllItems하면 진짜 데이터 손실!)
            Debug.Log("[InventoryManager] 로드할 인벤토리 데이터 없음 → 기존 상태 유지");
            IsInventoryLoaded = true;
            return;
        }

        // ★★ ItemDatabase 미준비 시 즉시 로드 금지 → 코루틴으로 대기 후 재시도
        //    그렇지 않으면 모든 아이템이 "ID 찾을 수 없음"으로 드롭되어 인벤이 비워짐
        if (ItemDatabase.Instance == null || !ItemDatabase.Instance.IsReady)
        {
            Debug.LogWarning($"[InventoryManager] ItemDatabase 미준비 → LoadInventoryData {items.Length}개 대기 후 재시도");
            StartCoroutine(WaitForItemDBAndLoad(items));
            return;
        }

        ClearAllItems();
        pendingUnknownItems.Clear();

        int staleSkipped = 0;
        foreach (InventoryItemData data in items)
        {
            ItemData itemData = ItemDatabase.Instance?.GetItemByID(data.itemID);
            if (itemData == null)
            {
                EquipmentData eqData = ItemDatabase.Instance?.GetEquipmentByID(data.itemID);
                if (eqData != null) itemData = eqData;
            }

            if (itemData == null)
            {
                // ★★ 데이터 손실 방지: ItemDatabase에 없는 ID도 보존
                //    pendingUnknownItems에 보관 → GetInventoryData에서 다시 포함됨
                //    → 다음 세션에서 ItemDatabase가 해당 아이템을 알게 되면 정상 복원됨
                pendingUnknownItems.Add(data);
                staleSkipped++;
                continue;
            }

            if (itemData is EquipmentData)
            {
                var unlock = new EquipUnlockData { isUnlocked = data.isUnlocked };

                // ★ 신규 형식 (instances 있음) → 그대로 복원
                if (data.instances != null && data.instances.Count > 0)
                {
                    foreach (var insData in data.instances)
                    {
                        unlock.instances.Add(new EquipInstance
                        {
                            instanceId = string.IsNullOrEmpty(insData.instanceId)
                                ? System.Guid.NewGuid().ToString()
                                : insData.instanceId,
                            enhanceLevel = insData.enhanceLevel,
                            itemLevel = insData.itemLevel,
                            isEquipped = insData.isEquipped
                        });
                    }
                }
                // ★ 기존 형식 (instances 없음) → count만큼 인스턴스 자동 생성 (마이그레이션)
                else if (data.count > 0)
                {
                    for (int i = 0; i < data.count; i++)
                    {
                        unlock.instances.Add(EquipInstance.Create(data.enhanceLevel, data.itemLevel));
                    }
                    Debug.Log($"[InventoryManager] 마이그레이션: ID {data.itemID} → {data.count}개 인스턴스 (+{data.enhanceLevel})");
                }

                // ★★ 진단: 로드된 unlock 상태 출력 (왜 0개가 되는지 추적)
                Debug.Log($"[Inv-LOAD] ID:{data.itemID} ({itemData.itemName}) " +
                    $"isUnlocked={data.isUnlocked}, savedCount={data.count}, savedInstances={(data.instances?.Count ?? 0)}, " +
                    $"→ runtime instances={unlock.instances.Count}");

                // ★★ 데이터 손실 방지: 잠금해제 됐는데 instances가 비어있으면 최소 1개 자동 생성
                //    (이전 버그로 instances 손실되어 빈 unlock 상태로 저장된 케이스 복구)
                if (unlock.isUnlocked && unlock.instances.Count == 0)
                {
                    unlock.instances.Add(EquipInstance.Create(0, 1));
                    Debug.LogWarning($"[Inv-LOAD] ID:{data.itemID} ({itemData.itemName}) — 빈 unlock 감지 → 기본 인스턴스 1개 자동 복구");
                }

                equipUnlockMap[data.itemID] = unlock;
            }
            else
            {
                generalItemData.Add(new GeneralItemEntry
                {
                    item = itemData,
                    count = data.count,
                    enhanceLevel = data.enhanceLevel,
                    itemLevel = data.itemLevel,
                    isUnlocked = data.isUnlocked
                });
            }
        }

        // ★★ Main → Farm → Main 재진입 race fix:
        //    재진입 시 ItemDatabase가 이미 DDOL ready → WaitForItemDatabase가 즉시 break →
        //    BuildEquipSlots가 빈 equipUnlockMap으로 호출됨 → 모든 슬롯이 잠금으로 표시.
        //    이제 LoadInventoryData가 끝난 시점에서:
        //    1) ItemDB가 ready면 equipDataReady를 강제로 true로 보장
        //    2) ActivateContainer 게이트를 우회해 BuildEquipSlots를 직접 호출
        //    3) 다음 프레임에 한 번 더 BuildEquipSlots → SelectTab(Equip)으로 currentTab/UI 동기화
        equipSlotsBuilt = false;
        if (ItemDatabase.Instance != null && ItemDatabase.Instance.IsReady)
            equipDataReady = true;

        if (equipDataReady && equipmentContainer != null)
        {
            BuildEquipSlots(); // ★ 새 데이터로 즉시 재빌드
            Debug.Log($"[InventoryManager] LoadInventoryData 완료 후 BuildEquipSlots 강제 실행 (재진입 race fix)");
        }

        // 다음 프레임에 한 번 더 — 컨테이너/탭 상태가 늦게 바인딩되는 케이스 대비
        StartCoroutine(PostLoadRebuildNextFrame());

        // ★ 동료/기타 탭 UI도 함께 갱신 (재진입 시 빈 UI 방지)
        if (CompanionInventoryManager.Instance != null)
            CompanionInventoryManager.Instance.RefreshUI();

        // ★ 로드 완료 플래그 — 이후 SaveGame이 호출돼도 정상 데이터로 저장 가능
        IsInventoryLoaded = true;
        if (staleSkipped > 0)
            Debug.LogWarning($"[InventoryManager] 인벤토리 로드 완료: 정상 {items.Length - staleSkipped}개 / stale ID {staleSkipped}개 스킵 (다음 저장 시 자동 정리)");
        else
            Debug.Log($"[InventoryManager] 인벤토리 로드 완료: {items.Length}개 (IsInventoryLoaded=true)");
    }

    /// <summary>
    /// ★ LoadInventoryData 직후 다음 프레임에 한 번 더 BuildEquipSlots 보장.
    /// 이유: equipmentContainer가 Awake/Start 늦게 바인딩되거나, 다른 스크립트가
    /// currentTab을 바꿀 수 있는 race를 방어. 동일 프레임 1차 빌드 + 다음 프레임 재빌드.
    /// </summary>
    private IEnumerator PostLoadRebuildNextFrame()
    {
        yield return null;
        if (this == null) yield break;
        if (ItemDatabase.Instance != null && ItemDatabase.Instance.IsReady)
            equipDataReady = true;
        if (equipDataReady && equipmentContainer != null)
        {
            equipSlotsBuilt = false;
            BuildEquipSlots();
            // 현재 탭이 장비라면 컨테이너 활성화도 재확인
            if (currentTab == InvenTabType.Equip)
                ActivateContainer(InvenTabType.Equip);
            Debug.Log($"[InventoryManager] PostLoadRebuildNextFrame: BuildEquipSlots 재실행 (해금:{equipUnlockMap.Count}개)");
        }
    }

    /// <summary>ItemDatabase 준비될 때까지 대기 후 LoadInventoryData 재호출</summary>
    private IEnumerator WaitForItemDBAndLoad(InventoryItemData[] items)
    {
        float waited = 0f;
        while (waited < 10f)
        {
            if (ItemDatabase.Instance != null && ItemDatabase.Instance.IsReady)
                break;
            waited += Time.deltaTime;
            yield return null;
        }

        if (ItemDatabase.Instance == null || !ItemDatabase.Instance.IsReady)
        {
            Debug.LogError($"[InventoryManager] ItemDatabase 10초 대기 타임아웃 — 인벤 로드 실패. 데이터 보호를 위해 IsInventoryLoaded=false 유지");
            // ★ IsInventoryLoaded를 false로 유지 → SaveLoadManager가 GameDataBridge 폴백 사용
            //   → 현재 메모리(빈 상태)로 JSON 덮어쓰기 차단
            yield break;
        }

        Debug.Log($"[InventoryManager] ItemDatabase 준비 완료 ({waited:F1}초 대기) → LoadInventoryData 재호출 ({items.Length}개)");
        LoadInventoryData(items);
    }

    // ═══════════════════════════════════════════════════════════════
    //  ★ 디버그: 재료 아이템 추가 (Inspector ContextMenu)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>모든 Material 타입 아이템을 10개씩 추가 (테스트용)</summary>
    [ContextMenu("Debug: Add 10 Material Items")]
    private void DebugAddMaterialItems()
    {
        if (ItemDatabase.Instance == null)
        {
            Debug.LogWarning("[InventoryManager] ItemDatabase 없음");
            return;
        }

        int added = 0;
        foreach (var item in ItemDatabase.Instance.allItems)
        {
            if (item != null && item.itemType == ItemType.Material)
            {
                AddItem(item, 10);
                added++;
                Debug.Log($"[Debug] Material 추가: {item.itemName} x10");
            }
        }
        Debug.Log($"[InventoryManager] 디버그: Material {added}종 x10개 추가 완료");
    }
}
