using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// FarmInventoryUI — 작물 인벤토리 패널
///
/// 기능:
///   - 보유 씨앗 목록 표시
///   - 수확한 작물 목록
///   - 작물 포인트 표시
///   - 플롯 선택 후 직접 심기 연동
///   - 씨앗 버리기 / 사용하기
/// </summary>
public class FarmInventoryUI : MonoBehaviour
{
    // ════════════════════════════════════════════════
    //  Inspector 연결
    // ════════════════════════════════════════════════

    [Header("패널")]
    public GameObject inventoryPanel;
    public Button closeButton;

    [Header("탭")]
    public Button tabSeeds;
    public Button tabHarvested;

    [Header("슬롯 컨테이너")]
    public Transform seedContainer;
    public Transform harvestContainer;
    public GameObject inventorySlotPrefab;

    [Header("작물 포인트")]
    public TextMeshProUGUI cropPointsText;

    [Header("선택된 아이템 상세")]
    public GameObject selectedItemDetail;
    public Image selectedItemIcon;
    public TextMeshProUGUI selectedItemName;
    public TextMeshProUGUI selectedItemCount;
    public TextMeshProUGUI selectedItemDesc;
    public Button useSeedButton;     // 선택한 씨앗 심기 (플롯 선택 모드)
    public Button dropItemButton;    // 버리기

    [Header("심기 모드")]
    [Tooltip("심기 버튼 클릭 후 플롯 선택 안내 메시지")]
    public GameObject plantModeGuide;
    public TextMeshProUGUI plantModeText;

    // ════════════════════════════════════════════════
    //  런타임
    // ════════════════════════════════════════════════

    public static FarmInventoryUI Instance { get; private set; }

    private enum InvTab { Seeds, Harvested }
    private InvTab currentTab = InvTab.Seeds;

    // 씨앗 데이터: cropID → 보유 수
    private Dictionary<int, int> seedCounts = new Dictionary<int, int>();
    // 수확 데이터: cropID → 수확량
    private Dictionary<int, int> harvestCounts = new Dictionary<int, int>();
    // ★ 물 데이터: waterID → 보유 수
    private Dictionary<int, int> waterCounts = new Dictionary<int, int>();
    // ★ 비료 데이터: fertID → 보유 수
    private Dictionary<int, int> fertCounts = new Dictionary<int, int>();

    private CropData selectedCrop;
    private bool isPlantMode = false;

    // ════════════════════════════════════════════════
    //  초기화
    // ════════════════════════════════════════════════

    // ★ 데이터 로드 가드 — false면 GetSaveData가 빈 dict 대신 GameDataBridge 폴백 사용
    //   InventoryManager.IsInventoryLoaded와 동일 패턴 (덮어쓰기 방지)
    public bool IsLoaded { get; private set; } = false;

    void Awake()
    {
        // ★ 싱글톤 체크 — scene-local 인스턴스 우선
        //   DontDestroyOnLoad에 있는 인스턴스가 Missing 참조를 가지고 있으면
        //   FarmScene 안의 진짜 인스턴스가 파괴되는 버그 방지
        if (Instance != null && Instance != this)
        {
            bool oldIsDDOL = Instance.gameObject.scene.name == "DontDestroyOnLoad";
            bool oldHasValidRefs = Instance.seedContainer != null && Instance.harvestContainer != null;
            bool newIsScene = gameObject.scene.name != "DontDestroyOnLoad";

            // 기존이 DDOL이거나 참조 깨져있고, 새 것이 scene-local이면 → 새 것을 채택
            if ((oldIsDDOL || !oldHasValidRefs) && newIsScene)
            {
                Debug.LogWarning($"[FarmInventoryUI] 기존 Instance 교체 — old:{Instance.gameObject.scene.name}(refs:{oldHasValidRefs}) → new:{gameObject.scene.name}");
                Destroy(Instance.gameObject);
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            Instance = this;
        }

        Debug.Log("[ManagerInit] FarmInventoryUI가 생성되었습니다.");

        // ★ 상세 패널 하위 요소 자동 탐색 (Inspector 미연결 시 안전장치)
        AutoFindDetailComponents();

        closeButton?.onClick.AddListener(ClosePanel);
        tabSeeds?.onClick.AddListener(() => SwitchTab(InvTab.Seeds));
        tabHarvested?.onClick.AddListener(() => SwitchTab(InvTab.Harvested));

        // ★ useSeedButton / dropItemButton은 null이어도 상세 패널 표시에 영향 없음
        if (useSeedButton != null) useSeedButton.onClick.AddListener(EnterPlantMode);
        if (dropItemButton != null) dropItemButton.onClick.AddListener(DropSelectedItem);

        // ★ Awake 시점에 즉시 GameDataBridge에서 데이터 선로드
        //   (OnEnable이 panel 비활성화 상태에서 안 불릴 수 있음 — 데이터 손실 방지)
        TryLoadFromSaveData();
    }

    /// <summary>
    /// ★ selectedItemDetail 하위에서 아이콘/이름/수량/설명/버튼을 자동 탐색
    ///   Inspector 미연결 시에도 상세 패널이 동작하도록 보장
    /// </summary>
    private void AutoFindDetailComponents()
    {
        // selectedItemDetail 자체가 없으면 이름으로 탐색
        if (selectedItemDetail == null)
        {
            selectedItemDetail = FindChildGO(transform, "SelectedItemDetail", "ItemDetail",
                                              "selectedItemDetail", "DetailPanel", "상세");
            if (selectedItemDetail != null)
                Debug.Log($"[FarmInventoryUI] ★ selectedItemDetail 자동 탐색: {selectedItemDetail.name}");
        }

        if (selectedItemDetail == null) return; // 패널 자체가 없으면 포기

        Transform dp = selectedItemDetail.transform;

        if (selectedItemIcon == null)
            selectedItemIcon = FindChild<Image>(dp, "Icon", "ItemIcon", "SelectedItemIcon", "아이콘");
        if (selectedItemName == null)
            selectedItemName = FindChild<TextMeshProUGUI>(dp, "ItemName", "Name", "SelectedItemName", "이름");
        if (selectedItemCount == null)
            selectedItemCount = FindChild<TextMeshProUGUI>(dp, "ItemCount", "Count", "SelectedItemCount", "수량", "보유");
        if (selectedItemDesc == null)
            selectedItemDesc = FindChild<TextMeshProUGUI>(dp, "ItemDesc", "Description", "Desc", "설명");
        if (useSeedButton == null)
        {
            useSeedButton = FindChild<Button>(dp, "UseSeedButton", "PlantButton", "심기", "사용");
            if (useSeedButton != null)
            {
                useSeedButton.onClick.AddListener(EnterPlantMode);
                Debug.Log($"[FarmInventoryUI] ★ useSeedButton 자동 탐색: {useSeedButton.gameObject.name}");
            }
        }
        if (dropItemButton == null)
        {
            dropItemButton = FindChild<Button>(dp, "DropItemButton", "DropButton", "버리기", "삭제");
            if (dropItemButton != null)
            {
                dropItemButton.onClick.AddListener(DropSelectedItem);
                Debug.Log($"[FarmInventoryUI] ★ dropItemButton 자동 탐색: {dropItemButton.gameObject.name}");
            }
        }
    }

    /// <summary>이름 목록으로 자식 GO 탐색</summary>
    private GameObject FindChildGO(Transform parent, params string[] names)
    {
        foreach (string n in names)
        {
            Transform found = parent.Find(n);
            if (found != null) return found.gameObject;
        }
        // 재귀 탐색
        foreach (string n in names)
        {
            foreach (Transform child in parent)
            {
                if (child.name == n) return child.gameObject;
                GameObject deep = FindChildGO(child, n);
                if (deep != null) return deep;
            }
        }
        return null;
    }

    /// <summary>이름 목록으로 자식 컴포넌트 탐색</summary>
    private T FindChild<T>(Transform parent, params string[] names) where T : Component
    {
        foreach (string n in names)
        {
            Transform found = parent.Find(n);
            if (found != null)
            {
                T comp = found.GetComponent<T>();
                if (comp != null) return comp;
            }
        }
        // 직계 자식 전체 검색
        foreach (Transform child in parent)
        {
            foreach (string n in names)
            {
                if (child.name.Contains(n))
                {
                    T comp = child.GetComponent<T>();
                    if (comp != null) return comp;
                }
            }
        }
        return null;
    }

    void OnEnable()
    {
        FarmManagerExtension.OnCropPointsChanged += RefreshCropPoints;
        FarmManagerExtension.OnHarvestCompleteStatic += OnHarvestComplete;

        // ★ seedCounts가 비어있으면 SaveData에서 로드 (구매 후 AddSeedToSaveData 경로 복구)
        TryLoadFromSaveData();

        RefreshAll();
    }

    /// <summary>
    /// seedCounts/harvestCounts가 비어있을 때 GameDataBridge.CurrentData에서 복구.
    /// FarmCropShopUI.AddSeedToSaveData()로 저장된 씨앗이 seedCounts에 없을 때 대응.
    /// </summary>
    private void TryLoadFromSaveData()
    {
        // ★ 디버그: GameDataBridge 상태 확인
        bool hasBridge = GameDataBridge.CurrentData != null;
        bool hasFarm = GameDataBridge.CurrentData?.farmData != null;
        bool hasInv = GameDataBridge.CurrentData?.farmData?.inventoryData != null;
        int seedsInBridge = GameDataBridge.CurrentData?.farmData?.inventoryData?.seeds?.Count ?? -1;
        int harvestsInBridge = GameDataBridge.CurrentData?.farmData?.inventoryData?.harvests?.Count ?? -1;
        Debug.Log($"[FarmInventoryUI] TryLoadFromSaveData — Bridge:{hasBridge}, farmData:{hasFarm}, invData:{hasInv}, " +
                  $"bridge씨앗:{seedsInBridge}, bridge수확:{harvestsInBridge}, " +
                  $"local씨앗:{seedCounts.Count}, local수확:{harvestCounts.Count}, IsLoaded:{IsLoaded}");

        var invData = GameDataBridge.CurrentData?.farmData?.inventoryData;
        if (invData == null)
        {
            // CurrentData 자체는 있지만 farmData가 비어있을 수도 있음 → load 가드 켜지 X
            // (다음 OnEnable에서 다시 시도)
            return;
        }

        if (seedCounts.Count == 0 && invData.seeds != null)
        {
            foreach (var s in invData.seeds)
                if (s.count > 0) seedCounts[s.cropID] = s.count;
            if (seedCounts.Count > 0)
                Debug.Log($"[FarmInventoryUI] SaveData에서 씨앗 {seedCounts.Count}종 복구");
        }

        if (harvestCounts.Count == 0 && invData.harvests != null)
        {
            foreach (var h in invData.harvests)
                if (h.count > 0) harvestCounts[h.cropID] = h.count;
            if (harvestCounts.Count > 0)
                Debug.Log($"[FarmInventoryUI] SaveData에서 수확물 {harvestCounts.Count}종 복구");
        }

        // ★ 로드 완료 — 이제 GetSaveData()가 dict 내용을 진실로 인정
        IsLoaded = true;
    }

    void OnDisable()
    {
        FarmManagerExtension.OnCropPointsChanged -= RefreshCropPoints;
        FarmManagerExtension.OnHarvestCompleteStatic -= OnHarvestComplete;
        ExitPlantMode();
    }

    // ════════════════════════════════════════════════
    //  열기 / 닫기
    // ════════════════════════════════════════════════

    public void OpenPanel()
    {
        // ★ inventoryPanel 자체가 destroyed면 자기 자신을 사용
        if (inventoryPanel == null)
            inventoryPanel = gameObject;

        // ★ 부모 계층 전체 활성화 — 부모가 비활성이면 자식 SetActive(true)해도 안 보임
        EnsureActiveUpTo(inventoryPanel.transform);
        inventoryPanel.SetActive(true);

        // ★ CanvasGroup 차단 해제 (alpha 0 / blocksRaycasts false 등)
        var cg = inventoryPanel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }

        // ★ 파괴된 자식 참조 자동 복구
        TryRebindIfDestroyed();

        RefreshAll();
    }

    public void ClosePanel()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
        FarmSceneController.Instance?.ResetBanner();
        ExitPlantMode();

        // 튜토리얼 트리거
        TutorialManager.Instance?.OnActionCompleted("FarmInventoryClosed");
    }

    /// <summary>
    /// 씬 전환/패널 재생성 후 자식 참조가 destroyed이면 다시 찾는다.
    /// MissingReferenceException 방지.
    /// </summary>
    private void TryRebindIfDestroyed()
    {
        if (seedContainer == null)
        {
            var found = FindChildGO(transform, "SeedContainer", "씨앗", "Seeds");
            if (found != null) seedContainer = found.transform;
        }
        if (harvestContainer == null)
        {
            var found = FindChildGO(transform, "HarvestContainer", "수확", "Harvested");
            if (found != null) harvestContainer = found.transform;
        }
        if (selectedItemDetail == null)
            AutoFindDetailComponents();
    }

    /// <summary>
    /// ★ FIX: FarmSceneController.OpenCropProcess에서 호출
    ///         패널을 열고 수확탭으로 바로 전환
    /// </summary>
    public void SwitchToHarvestedTab()
    {
        // ★ 파괴된 참조 복구 후 전환 (HarvestCrop 콜백에서 호출됨)
        TryRebindIfDestroyed();
        SwitchTab(InvTab.Harvested);
    }

    // ════════════════════════════════════════════════
    //  씨앗 수량 관리 (외부에서 호출)
    // ════════════════════════════════════════════════

    public void AddSeed(int cropID, int amount = 1)
    {
        if (!seedCounts.ContainsKey(cropID)) seedCounts[cropID] = 0;
        seedCounts[cropID] += amount;
        RefreshCurrentTab();
    }

    public bool ConsumeSeed(int cropID, int amount = 1)
    {
        if (!seedCounts.ContainsKey(cropID) || seedCounts[cropID] < amount)
            return false;
        seedCounts[cropID] -= amount;
        if (seedCounts[cropID] <= 0) seedCounts.Remove(cropID);
        RefreshCurrentTab();
        return true;
    }

    public int GetSeedCount(int cropID)
        => seedCounts.ContainsKey(cropID) ? seedCounts[cropID] : 0;

    // ════════════════════════════════════════════════
    //  물 수량 관리 ★ 신규
    // ════════════════════════════════════════════════

    public void AddWater(int waterID, int amount = 1)
    {
        if (!waterCounts.ContainsKey(waterID)) waterCounts[waterID] = 0;
        waterCounts[waterID] += amount;
    }

    public bool ConsumeWater(int waterID, int amount = 1)
    {
        if (!waterCounts.ContainsKey(waterID) || waterCounts[waterID] < amount)
            return false;
        waterCounts[waterID] -= amount;
        if (waterCounts[waterID] <= 0) waterCounts.Remove(waterID);
        return true;
    }

    public int GetWaterCount(int waterID)
        => waterCounts.ContainsKey(waterID) ? waterCounts[waterID] : 0;

    // ════════════════════════════════════════════════
    //  비료 수량 관리 ★ 신규
    // ════════════════════════════════════════════════

    public void AddFertilizer(int fertID, int amount = 1)
    {
        if (!fertCounts.ContainsKey(fertID)) fertCounts[fertID] = 0;
        fertCounts[fertID] += amount;
    }

    public bool ConsumeFertilizer(int fertID, int amount = 1)
    {
        if (!fertCounts.ContainsKey(fertID) || fertCounts[fertID] < amount)
            return false;
        fertCounts[fertID] -= amount;
        if (fertCounts[fertID] <= 0) fertCounts.Remove(fertID);
        return true;
    }

    public int GetFertCount(int fertID)
        => fertCounts.ContainsKey(fertID) ? fertCounts[fertID] : 0;

    // ════════════════════════════════════════════════
    //  전체 갱신
    // ════════════════════════════════════════════════

    private void RefreshAll()
    {
        RefreshCropPoints(CropPointService.Value);

        // ★ 탭에 맞게 컨테이너 활성/비활성 보장 (파괴된 참조 체크)
        if (seedContainer != null) seedContainer.gameObject.SetActive(currentTab == InvTab.Seeds);
        if (harvestContainer != null) harvestContainer.gameObject.SetActive(currentTab == InvTab.Harvested);
        if (seedContainer != null) EnsureContainerInteractable(seedContainer);
        if (harvestContainer != null) EnsureContainerInteractable(harvestContainer);

        RefreshCurrentTab();
        selectedItemDetail?.SetActive(false);
    }

    private void RefreshCurrentTab()
    {
        if (currentTab == InvTab.Seeds)
            RefreshSeedList();
        else
            RefreshHarvestList();
    }

    private void SwitchTab(InvTab tab)
    {
        currentTab = tab;

        // ★ Unity의 파괴된 객체는 C# `?.` 연산자로 감지 안 됨 → 명시적 != null 체크 사용
        //   (씬 전환으로 컨테이너가 destroy됐을 때 MissingReferenceException 방지)
        if (seedContainer != null)
            seedContainer.gameObject.SetActive(tab == InvTab.Seeds);
        if (harvestContainer != null)
            harvestContainer.gameObject.SetActive(tab == InvTab.Harvested);

        // ★ 컨테이너에 CanvasGroup이 있으면 클릭 차단 해제
        EnsureContainerInteractable(seedContainer);
        EnsureContainerInteractable(harvestContainer);

        RefreshCurrentTab();

        if (selectedItemDetail != null)
            selectedItemDetail.SetActive(false);

        // 튜토리얼 트리거
        if (tab == InvTab.Harvested)
            TutorialManager.Instance?.OnActionCompleted("CheckHarvestTab");
    }

    /// <summary>
    /// ★ 컨테이너 또는 부모에 CanvasGroup이 있을 때 interactable/blocksRaycasts 보장
    /// </summary>
    private void EnsureContainerInteractable(Transform container)
    {
        if (container == null) return;

        // 1) CanvasGroup 차단 해제
        var groups = container.GetComponentsInParent<CanvasGroup>(true);
        foreach (var cg in groups)
        {
            if (!cg.interactable || !cg.blocksRaycasts)
            {
                Debug.LogWarning($"[FarmInventoryUI] ★ CanvasGroup '{cg.gameObject.name}' interactable/blocksRaycasts 강제 활성화!");
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        // 2) ★ ScrollRect Viewport의 Image.raycastTarget 차단 해제
        //    Viewport Image가 raycastTarget=true이면 그 아래 Content 슬롯의 클릭을 가로챔.
        //    Mask 기능은 raycastTarget과 무관하게 동작하므로 false로 해도 안전.
        var scrollRect = container.GetComponentInParent<ScrollRect>();
        if (scrollRect != null && scrollRect.viewport != null)
        {
            var vpImage = scrollRect.viewport.GetComponent<Image>();
            if (vpImage != null && vpImage.raycastTarget)
            {
                vpImage.raycastTarget = false;
                Debug.LogWarning($"[FarmInventoryUI] ★ ScrollRect Viewport '{scrollRect.viewport.name}' Image.raycastTarget → false (클릭 차단 해제)");
            }
        }

        // 3) ★ 컨테이너의 형제(sibling) 중 raycastTarget=true인 Image가
        //    컨테이너 위를 덮어 클릭을 가로채는 경우 해제
        if (container.parent != null)
        {
            foreach (Transform sibling in container.parent)
            {
                if (sibling == container) continue;
                // 컨테이너보다 뒤에 그려지는(siblingIndex가 큰) 형제만 차단 가능
                if (sibling.GetSiblingIndex() <= container.GetSiblingIndex()) continue;

                var sibImg = sibling.GetComponent<Image>();
                if (sibImg != null && sibImg.raycastTarget && sibImg.enabled)
                {
                    // Button이 없는 단순 오버레이 Image만 해제 (Button이 있으면 의도적 UI)
                    if (sibling.GetComponent<Button>() == null &&
                        sibling.GetComponent<ScrollRect>() == null)
                    {
                        sibImg.raycastTarget = false;
                        Debug.LogWarning($"[FarmInventoryUI] ★ 형제 오버레이 '{sibling.name}' Image.raycastTarget → false");
                    }
                }
            }
        }
    }

    // ════════════════════════════════════════════════
    //  씨앗 목록
    // ════════════════════════════════════════════════

    private void RefreshSeedList()
    {
        if (seedContainer == null || inventorySlotPrefab == null) return;

        foreach (Transform child in seedContainer)
            Destroy(child.gameObject);

        if (FarmManager.Instance == null)
        {
            Debug.LogWarning("[FarmInventoryUI] RefreshSeedList: FarmManager.Instance == null → 씨앗 표시 불가");
            return;
        }

        Debug.Log($"[FarmInventoryUI] RefreshSeedList: seedCounts.Count={seedCounts.Count}");
        int slotCount = 0;
        foreach (var pair in seedCounts)
        {
            if (pair.Value <= 0) continue;

            CropData crop = FarmManager.Instance.GetCropByID(pair.Key);
            if (crop == null)
            {
                // ★ Resources 폴백
                var allCropAssets = Resources.FindObjectsOfTypeAll<CropData>();
                foreach (var c in allCropAssets)
                {
                    if (c != null && c.cropID == pair.Key)
                    {
                        crop = c;
                        Debug.LogWarning($"[FarmInventoryUI] cropID={pair.Key} → Resources 폴백 매칭: {c.cropName}");
                        break;
                    }
                }
            }

            if (crop == null)
            {
                Debug.LogWarning($"[FarmInventoryUI] ★ cropID={pair.Key} → CropData 못 찾음 → 씨앗 슬롯 스킵 ({pair.Value}개 화면 누락)");
                continue;
            }

            GameObject go = Instantiate(inventorySlotPrefab, seedContainer);
            SetupInventorySlot(go, crop, pair.Value, true);
            slotCount++;
        }

        // ★ 슬롯 생성 후 컨테이너 클릭 경로 재점검
        if (slotCount > 0)
            EnsureContainerInteractable(seedContainer);

        Debug.Log($"[FarmInventoryUI] RefreshSeedList 완료: {slotCount}개 슬롯 생성");

        if (seedCounts.Count == 0)
            Debug.Log("[FarmInventoryUI] 보유 씨앗 없음");
    }

    // ════════════════════════════════════════════════
    //  수확물 목록
    // ════════════════════════════════════════════════

    private void RefreshHarvestList()
    {
        if (harvestContainer == null || inventorySlotPrefab == null) return;

        foreach (Transform child in harvestContainer)
            Destroy(child.gameObject);

        if (FarmManager.Instance == null)
        {
            Debug.LogWarning("[FarmInventoryUI] RefreshHarvestList: FarmManager.Instance == null → 수확물 표시 불가");
            return;
        }

        Debug.Log($"[FarmInventoryUI] RefreshHarvestList: harvestCounts.Count={harvestCounts.Count}");
        int rendered = 0;
        foreach (var pair in harvestCounts)
        {
            if (pair.Value <= 0) continue;

            CropData crop = FarmManager.Instance.GetCropByID(pair.Key);
            if (crop == null)
            {
                // ★ FarmManager에 없으면 Resources에서 직접 검색 (폴백)
                var allCropAssets = Resources.FindObjectsOfTypeAll<CropData>();
                foreach (var c in allCropAssets)
                {
                    if (c != null && c.cropID == pair.Key)
                    {
                        crop = c;
                        Debug.LogWarning($"[FarmInventoryUI] cropID={pair.Key} → Resources 폴백 매칭: {c.cropName}");
                        break;
                    }
                }
            }

            if (crop == null)
            {
                Debug.LogWarning($"[FarmInventoryUI] ★ cropID={pair.Key} → CropData 못 찾음 → 슬롯 스킵 (수확물 {pair.Value}개 화면 누락)");
                continue;
            }

            GameObject go = Instantiate(inventorySlotPrefab, harvestContainer);
            SetupInventorySlot(go, crop, pair.Value, false);
            rendered++;
        }
        Debug.Log($"[FarmInventoryUI] RefreshHarvestList 완료: {rendered}개 슬롯 생성");
    }

    private void SetupInventorySlot(GameObject go, CropData crop, int count, bool isSeed)
    {
        // ── 비주얼 설정 ──
        var icon = go.transform.Find("Icon")?.GetComponent<Image>();
        var nameTxt = go.transform.Find("SeedName")?.GetComponent<TextMeshProUGUI>();
        var countTxt = go.transform.Find("수량")?.GetComponent<TextMeshProUGUI>();

        if (nameTxt == null || countTxt == null)
        {
            var all = go.GetComponentsInChildren<TextMeshProUGUI>();
            if (nameTxt == null && all.Length > 0) nameTxt = all[0];
            if (countTxt == null && all.Length > 1) countTxt = all[1];
        }

        if (icon != null) icon.sprite = isSeed ? crop.seedIcon : crop.harvestIcon;
        if (nameTxt != null) nameTxt.text = crop.cropName;
        if (countTxt != null) countTxt.text = $"x{count}";

        // ── raycast 설정 ──
        // 루트에 Graphic(Image)이 없으면 IPointerDownHandler가 이벤트를 받지 못함
        var rootImg = go.GetComponent<Image>();
        if (rootImg == null)
        {
            rootImg = go.AddComponent<Image>();
            rootImg.color = new Color(1, 1, 1, 0);
        }
        rootImg.raycastTarget = true;

        // 하위 Image의 raycastTarget을 꺼서 루트만 이벤트를 받도록
        var childImages = go.GetComponentsInChildren<Image>(true);
        foreach (var childImg in childImages)
        {
            if (childImg.gameObject == go) continue;
            childImg.raycastTarget = false;
        }

        // ── 기존 Button 제거 (onClick은 dragThreshold에 의해 씹히므로 사용 안 함) ──
        var oldBtn = go.GetComponent<Button>();
        if (oldBtn != null) Destroy(oldBtn);

        var oldTrigger = go.GetComponent<EventTrigger>();
        if (oldTrigger != null) Destroy(oldTrigger);

        // ── FarmSlotTag (IPointerDownHandler) 기반 클릭 처리 ──
        var slotTag = go.GetComponent<FarmSlotTag>();
        if (slotTag == null) slotTag = go.AddComponent<FarmSlotTag>();
        slotTag.crop = crop;
        slotTag.isSeed = isSeed;

        // ★ OnPointerDown에서 호출될 콜백 등록
        //   Button.onClick과 달리 dragThreshold 영향 없이 터치 즉시 발동
        slotTag.onSlotClicked = () =>
        {
            // 소속 컨테이너로 isSeed 동적 판별
            bool seedFlag = slotTag.isSeed;
            if (seedContainer != null && go.transform.IsChildOf(seedContainer))
                seedFlag = true;
            else if (harvestContainer != null && go.transform.IsChildOf(harvestContainer))
                seedFlag = false;

            Debug.Log($"[FarmInventoryUI] 슬롯 터치: {slotTag.crop.cropName} (isSeed={seedFlag}, tab={currentTab})");
            SelectItem(slotTag.crop, seedFlag);
        };
    }

    // ════════════════════════════════════════════════
    //  아이템 선택
    // ════════════════════════════════════════════════

    private void SelectItem(CropData crop, bool isSeed)
    {
        if (crop == null)
        {
            Debug.LogWarning("[FarmInventoryUI] SelectItem: crop이 null!");
            return;
        }

        Debug.Log($"[FarmInventoryUI] SelectItem 호출: {crop.cropName}, isSeed={isSeed}");
        selectedCrop = crop;

        // ──────────────────────────────────────────
        //  1단계: 상세 패널 + 모든 부모 활성화 (버튼 유무와 완전 독립)
        // ──────────────────────────────────────────
        if (selectedItemDetail != null)
        {
            // ★ FIX: 부모가 비활성이면 자식 SetActive(true)해도 화면에 안 보임
            //   → 상세 패널부터 inventoryPanel까지 모든 부모를 강제 활성화
            EnsureActiveUpTo(selectedItemDetail.transform);
            selectedItemDetail.SetActive(true);
            Debug.Log($"[FarmInventoryUI] 상세 패널 활성화 — activeInHierarchy={selectedItemDetail.activeInHierarchy}");
        }
        else
        {
            Debug.LogWarning("[FarmInventoryUI] selectedItemDetail이 null — 개별 요소로 상세 표시 시도");
        }

        // ──────────────────────────────────────────
        //  2단계: 상세 정보 텍스트/아이콘 표시 (각각 독립 null 체크)
        // ──────────────────────────────────────────
        int count = isSeed ? GetSeedCount(crop.cropID)
                           : (harvestCounts.ContainsKey(crop.cropID) ? harvestCounts[crop.cropID] : 0);

        if (selectedItemIcon != null)
        {
            Sprite sp = isSeed ? crop.seedIcon : crop.harvestIcon;
            selectedItemIcon.sprite = sp;
            selectedItemIcon.enabled = sp != null;
        }

        if (selectedItemName != null)
            selectedItemName.text = crop.cropName;

        if (selectedItemCount != null)
            selectedItemCount.text = $"보유: {count}개";

        if (selectedItemDesc != null)
            selectedItemDesc.text = !string.IsNullOrEmpty(crop.description)
                ? crop.description
                : $"{crop.cropName} — 성장시간 {crop.growthTimeSeconds}초";

        // ──────────────────────────────────────────
        //  3단계: 액션 버튼 (null이면 스킵 — 상세 표시에 영향 없음)
        // ──────────────────────────────────────────
        if (useSeedButton != null)
            useSeedButton.gameObject.SetActive(isSeed);
        // useSeedButton이 null이어도 상세 패널 표시는 이미 완료됨

        if (dropItemButton != null)
            dropItemButton.gameObject.SetActive(true);
        // dropItemButton이 null이어도 상세 패널 표시는 이미 완료됨

        Debug.Log($"[FarmInventoryUI] ✅ 상세 팝업 표시 완료: {crop.cropName}, 보유 {count}개, " +
                  $"useSeedBtn={useSeedButton != null}, dropBtn={dropItemButton != null}");
    }

    // ════════════════════════════════════════════════
    //  심기 모드 (플롯 선택)
    // ════════════════════════════════════════════════

    private void EnterPlantMode()
    {
        if (selectedCrop == null) return;

        ClosePanel();

        // ★ PlantMode_Overlay 열고 씨앗 미리 선택
        // PlantMode_Overlay는 씬에서 활성화 상태여야 Instance가 세팅됨
        if (FarmPlantModePanel.Instance != null)
        {
            FarmPlantModePanel.Instance.PreSelectSeed(selectedCrop);
        }
        else
        {
            Debug.LogWarning("[FarmInventoryUI] FarmPlantModePanel.Instance가 null입니다. " +
                             "씬에서 PlantMode_Overlay GO를 활성화 상태로 두세요.");
        }
    }

    private void ExitPlantMode()
    {
        isPlantMode = false;
        if (plantModeGuide != null && plantModeGuide.gameObject != null)
        {
            try { plantModeGuide.SetActive(false); }
            catch (MissingReferenceException) { plantModeGuide = null; }
        }
    }

    // ════════════════════════════════════════════════
    //  아이템 버리기
    // ════════════════════════════════════════════════

    private void DropSelectedItem()
    {
        if (selectedCrop == null) return;

        if (currentTab == InvTab.Seeds)
        {
            seedCounts.Remove(selectedCrop.cropID);
            UIManager.Instance?.ShowMessage($"{selectedCrop.cropName} 씨앗 버림", Color.gray);
        }
        else
        {
            harvestCounts.Remove(selectedCrop.cropID);
            UIManager.Instance?.ShowMessage($"{selectedCrop.cropName} 수확물 버림", Color.gray);
        }

        if (selectedItemDetail != null) selectedItemDetail.SetActive(false);
        RefreshCurrentTab();
    }

    // ════════════════════════════════════════════════
    //  이벤트 수신
    // ════════════════════════════════════════════════

    private void RefreshCropPoints(long points)
    {
        if (cropPointsText)
            cropPointsText.text = $"작물 포인트: {UIManager.FormatKoreanUnit(points)}";
    }

    private void OnHarvestComplete(int plotIndex, List<CropHarvestReward> rewards)
    {
        // ★ FarmManager.HarvestCrop()에서 AddHarvest()를 직접 호출하므로
        //   여기서는 UI 갱신만 수행 (itemName 역추적 로직 제거 — 오류 원인이었음)
        RefreshCurrentTab();
    }

    /// <summary>
    /// 수확물 인벤토리에 직접 추가 (FarmManager 등 외부에서 호출 가능)
    /// </summary>
    public void AddHarvest(int cropID, int amount = 1)
    {
        if (!harvestCounts.ContainsKey(cropID)) harvestCounts[cropID] = 0;
        harvestCounts[cropID] += amount;
        Debug.Log($"[FarmInventoryUI] AddHarvest cropID={cropID} amount={amount} → 총 {harvestCounts[cropID]}개");

        // ★ 탭과 무관하게 수확 목록을 항상 갱신 (씨앗탭 선택 중이어도 데이터는 반영)
        RefreshHarvestList();

        // ★ GameDataBridge에 즉시 동기화 → 앱 종료 시에도 수확물 보존
        SyncHarvestsToGameDataBridge();

        // ★ 디스크 저장 (FarmScene에 SaveLoadManager 없을 수 있으므로 null 체크)
        SaveLoadManager.Instance?.SaveGame();
        Debug.Log("[FarmInventoryUI] 수확 저장 완료 → GameDataBridge+SaveGame");
    }

    /// <summary>
    /// harvestCounts 딕셔너리를 GameDataBridge.CurrentData.farmData.inventoryData.harvests에 동기화
    /// </summary>
    public void SyncHarvestsToGameDataBridge()
    {
        if (GameDataBridge.CurrentData?.farmData == null) return;

        if (GameDataBridge.CurrentData.farmData.inventoryData == null)
            GameDataBridge.CurrentData.farmData.inventoryData = new FarmInventorySaveData();

        var harvests = new List<FarmItemCount>();
        foreach (var pair in harvestCounts)
        {
            if (pair.Value > 0)
                harvests.Add(new FarmItemCount { cropID = pair.Key, count = pair.Value });
        }
        GameDataBridge.CurrentData.farmData.inventoryData.harvests = harvests;
    }

    /// <summary>
    /// 수확물 소비 (판매 등)
    /// </summary>
    public bool ConsumeHarvest(int cropID, int amount = 1)
    {
        if (!harvestCounts.ContainsKey(cropID) || harvestCounts[cropID] < amount)
            return false;
        harvestCounts[cropID] -= amount;
        if (harvestCounts[cropID] <= 0) harvestCounts.Remove(cropID);
        RefreshCurrentTab();
        return true;
    }

    public int GetHarvestCount(int cropID)
        => harvestCounts.ContainsKey(cropID) ? harvestCounts[cropID] : 0;

    // ════════════════════════════════════════════════
    //  저장/로드 (FarmSaveData에 추가 가능)
    // ════════════════════════════════════════════════

    public FarmInventorySaveData GetSaveData()
    {
        // ★ 로드 안 된 상태에서 호출되면 빈 데이터 대신 GameDataBridge 폴백
        //   (Awake/OnEnable 전에 SaveGame이 호출되면 빈 dict로 덮어쓰기 방지)
        if (!IsLoaded)
        {
            var existing = GameDataBridge.CurrentData?.farmData?.inventoryData;
            if (existing != null)
            {
                Debug.LogWarning("[FarmInventoryUI] GetSaveData 호출됐지만 IsLoaded=false → 기존 데이터 보존");
                return existing;
            }
        }

        var data = new FarmInventorySaveData();
        data.seeds = new List<FarmItemCount>();
        data.harvests = new List<FarmItemCount>();

        foreach (var pair in seedCounts)
            data.seeds.Add(new FarmItemCount { cropID = pair.Key, count = pair.Value });
        foreach (var pair in harvestCounts)
            data.harvests.Add(new FarmItemCount { cropID = pair.Key, count = pair.Value });

        return data;
    }

    public void LoadSaveData(FarmInventorySaveData data)
    {
        if (data == null) return;

        seedCounts.Clear();
        harvestCounts.Clear();

        if (data.seeds != null)
            foreach (var item in data.seeds)
                seedCounts[item.cropID] = item.count;

        if (data.harvests != null)
            foreach (var item in data.harvests)
                harvestCounts[item.cropID] = item.count;

        // ★ 명시적 로드 완료
        IsLoaded = true;
        RefreshCurrentTab();
    }

    // ════════════════════════════════════════════════
    //  유틸: 부모 계층 강제 활성화
    // ════════════════════════════════════════════════

    /// <summary>
    /// ★ target부터 inventoryPanel(또는 루트)까지 모든 부모 GO를 활성화.
    ///   부모가 비활성이면 자식 SetActive(true)해도 화면에 표시되지 않기 때문.
    /// </summary>
    private void EnsureActiveUpTo(Transform target)
    {
        if (target == null) return;

        // inventoryPanel의 Transform을 경계로 사용 (그 위는 건드리지 않음)
        Transform boundary = inventoryPanel != null ? inventoryPanel.transform : null;

        Transform current = target.parent;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
                Debug.LogWarning($"[FarmInventoryUI] ★ 비활성 부모 강제 활성화: {current.name}");
            }

            // inventoryPanel에 도달하면 중단
            if (boundary != null && current == boundary) break;
            current = current.parent;
        }
    }
}

/// <summary>
/// ★ 인벤토리 슬롯에 부착 — IPointerDownHandler로 드래그 판정을 우회하여 확실한 클릭 감지.
///   Button.onClick은 EventSystem의 dragThreshold 때문에 터치 시 씹힐 수 있지만,
///   OnPointerDown은 손가락이 닿는 즉시 발생하므로 영향을 받지 않음.
/// </summary>
public class FarmSlotTag : MonoBehaviour, IPointerDownHandler
{
    [HideInInspector] public CropData crop;
    [HideInInspector] public bool isSeed;

    /// <summary>슬롯 클릭 시 FarmInventoryUI가 등록하는 콜백</summary>
    public System.Action onSlotClicked;

    public void OnPointerDown(PointerEventData eventData)
    {
        // 왼쪽 클릭 / 첫 번째 터치만 처리
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (onSlotClicked != null)
        {
            onSlotClicked.Invoke();
        }
        else
        {
            Debug.LogWarning($"[FarmSlotTag] onSlotClicked 콜백이 null! ({gameObject.name})");
        }
    }
}