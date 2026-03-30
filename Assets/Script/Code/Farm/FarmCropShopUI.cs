using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
// FarmCropShopUI.cs  (탭별 컨테이너 분리 버전)
//
// ★ 탭 구조: [채소] [과일] [물] [비료]
//
// Inspector 연결:
//   tabVegetable     ← "채소" 버튼
//   tabFruit         ← "과일" 버튼
//   tabWater         ← "물" 버튼
//   tabFertilizer    ← "비료" 버튼
//
//   veggieScrollView ← 채소씨앗스크롤 (GameObject)
//   fruitScrollView  ← 과일씨앗스크롤 (GameObject)
//   waterScrollView  ← 물스크롤 (GameObject)
//   fertScrollView   ← 비료스크롤 (GameObject)
//
//   veggieContainer  ← 채소씨앗스크롤 > Viewport > Content
//   fruitContainer   ← 과일씨앗스크롤 > Viewport > Content
//   waterContainer   ← 물스크롤 > Viewport > Content
//   fertContainer    ← 비료스크롤 > Viewport > Content
//
//   cropSlotPrefab   → FarmCropSlotUI 컴포넌트 (씨앗용)
//   supplySlotPrefab → FarmSupplySlotUI 컴포넌트 (물/비료용)
// ═══════════════════════════════════════════════════════════════════

public class FarmCropShopUI : MonoBehaviour
{
    private enum ShopTab { Vegetable, Fruit, Water, Fertilizer }

    // ─── 패널 ──────────────────────────────────────────────────
    [Header("===== 패널 =====")]
    public GameObject shopPanel;
    public Button closeButton;

    // ─── 탭 버튼 ───────────────────────────────────────────────
    [Header("===== 탭 버튼 =====")]
    public Button tabVegetable;
    public Button tabFruit;
    public Button tabWater;
    public Button tabFertilizer;

    // ─── 탭별 스크롤뷰 Content (각각 따로) ───────────────────────
    [Header("===== 탭별 컨테이너 (각 스크롤뷰 Content) =====")]
    public Transform veggieContainer;   // 채소씨앗스크롤 > Viewport > Content
    public Transform fruitContainer;    // 과일씨앗스크롤 > Viewport > Content
    public Transform waterContainer;    // 물스크롤 > Viewport > Content
    public Transform fertContainer;     // 비료스크롤 > Viewport > Content

    // ─── 탭별 스크롤뷰 루트 (탭 전환 시 Show/Hide) ───────────────
    [Header("===== 탭별 스크롤뷰 오브젝트 (Show/Hide용) =====")]
    public GameObject veggieScrollView;  // 채소씨앗스크롤
    public GameObject fruitScrollView;   // 과일씨앗스크롤
    public GameObject waterScrollView;   // 물스크롤
    public GameObject fertScrollView;    // 비료스크롤

    // ─── 슬롯 프리팹 ──────────────────────────────────────────
    [Header("===== 슬롯 프리팹 =====")]
    public GameObject cropSlotPrefab;    // FarmCropSlotUI 컴포넌트
    public GameObject supplySlotPrefab;  // FarmSupplySlotUI 컴포넌트

    // ─── 씨앗 상세 패널 ────────────────────────────────────────
    [Header("===== 씨앗 상세 패널 =====")]
    public GameObject detailPanel;
    public Image cropIcon;
    public TextMeshProUGUI cropNameText;
    public TextMeshProUGUI cropDescText;
    public TextMeshProUGUI growthTimeText;
    public TextMeshProUGUI waterBonusText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI lockReasonText;
    public TextMeshProUGUI ownedCountText;
    public Button buyButton;
    public Button buyX5Button;
    public Button buyX10Button;

    // ─── 물/비료 상세 패널 ─────────────────────────────────────
    [Header("===== 물/비료 상세 패널 =====")]
    public GameObject supplyDetailPanel;
    public Image supplyIcon;
    public TextMeshProUGUI supplyNameText;
    public TextMeshProUGUI supplyDescText;
    public TextMeshProUGUI supplyCostText;
    public TextMeshProUGUI supplyEffectText;
    public TextMeshProUGUI supplyLockText;
    public TextMeshProUGUI supplyOwnedText;
    public Button buySupplyButton;
    public Button buySupplyX5Button;
    public Button buySupplyX10Button;

    // ─── 상태 바 ───────────────────────────────────────────────
    [Header("===== 상태 바 =====")]
    public TextMeshProUGUI greenhouseLevelText;
    public TextMeshProUGUI watermillBonusText;
    public TextMeshProUGUI windmillBonusText;

    // ─── 물 데이터 목록 (Inspector에서 직접 할당) ─────────────────
    [Header("===== 물 데이터 =====")]
    public List<WaterData> availableWaters = new List<WaterData>();

    // ─── 싱글톤 (FarmManager에서 알림용) ─────────────────────────
    public static FarmCropShopUI Instance { get; private set; }

    // ─── 런타임 ───────────────────────────────────────────────
    private ShopTab currentTab = ShopTab.Vegetable;
    private CropData selectedCrop;
    private WaterData selectedWater;
    private FertilizerData selectedFert;

    // ★ 탭별 마지막 선택 기억 (탭 전환 후 복원용)
    private CropData lastVeggieCrop;
    private CropData lastFruitCrop;
    private WaterData lastWater;
    private FertilizerData lastFert;

    // 탭별 스폰된 슬롯 목록 (Clear 용)
    private List<GameObject> veggieSlots = new List<GameObject>();
    private List<GameObject> fruitSlots = new List<GameObject>();
    private List<GameObject> waterSlots = new List<GameObject>();
    private List<GameObject> fertSlots = new List<GameObject>();

    // ═══ 초기화 ══════════════════════════════════════════════════
    void Awake()
    {
        Instance = this;
        closeButton?.onClick.AddListener(CloseShop);

        tabVegetable?.onClick.AddListener(() => SwitchTab(ShopTab.Vegetable));
        tabFruit?.onClick.AddListener(() => SwitchTab(ShopTab.Fruit));
        tabWater?.onClick.AddListener(() => SwitchTab(ShopTab.Water));
        tabFertilizer?.onClick.AddListener(() => SwitchTab(ShopTab.Fertilizer));

        // ★ buyButton 자동 탐색 (Inspector 미연결 대비)
        if (buyButton == null && detailPanel != null)
        {
            var btns = detailPanel.GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                string n = b.gameObject.name.ToLower();
                if (n.Contains("buy") && !n.Contains("x5") && !n.Contains("x10") && !n.Contains("supply"))
                { buyButton = b; Debug.LogWarning($"[FarmCropShop] Awake: buyButton 자동 탐색 → {b.gameObject.name}"); break; }
            }
        }

        // ★ SelectCrop에서 리스너를 재등록하므로, Awake에서는 등록하지 않음
        //    (RemoveAllListeners 후 재등록 패턴 사용)

        buySupplyButton?.onClick.AddListener(() => TryBuySupply(1));
        buySupplyX5Button?.onClick.AddListener(() => TryBuySupply(5));
        buySupplyX10Button?.onClick.AddListener(() => TryBuySupply(10));

        // ★ detailPanel이 스크롤뷰 자식이면 shopPanel 직속으로 이동
        //   (과일 탭 전환 시 veggieScrollView.SetActive(false) → detailPanel도 숨겨지는 버그 방지)
        ReparentDetailPanelIfNeeded(detailPanel);

        Debug.Log($"[FarmCropShop] Awake 완료 — buyButton:{buyButton != null} / detailPanel:{detailPanel != null} / cropSlotPrefab:{cropSlotPrefab != null}");
    }

    /// <summary>
    /// detailPanel이 스크롤뷰(veggieScrollView/fruitScrollView 등)의 자식이면
    /// shopPanel 직속으로 이동시켜 탭 전환 시에도 항상 표시 가능하게 함.
    /// </summary>
    private void ReparentDetailPanelIfNeeded(GameObject panel)
    {
        if (panel == null || shopPanel == null) return;

        // 이미 shopPanel 직속 자식이면 패스
        if (panel.transform.parent == shopPanel.transform) return;

        // 부모 체인에 스크롤뷰가 있는지 확인
        Transform p = panel.transform.parent;
        bool isInsideScrollView = false;
        while (p != null && p != shopPanel.transform)
        {
            if (p.gameObject == veggieScrollView || p.gameObject == fruitScrollView
                || p.gameObject == waterScrollView || p.gameObject == fertScrollView)
            {
                isInsideScrollView = true;
                break;
            }
            p = p.parent;
        }

        if (isInsideScrollView)
        {
            Debug.LogWarning($"[FarmCropShop] {panel.name}이 스크롤뷰 자식임 → shopPanel 직속으로 이동 (탭 전환 시 표시 보장)");
            panel.transform.SetParent(shopPanel.transform, false);
            // 최상단에 표시되도록 마지막 자식으로 이동
            panel.transform.SetAsLastSibling();
        }
    }

    private Coroutine retryCoroutine;

    void OnEnable()
    {
        FarmBuildingManager.OnBuildingLevelChanged += OnBuildingLevelChanged;
        RefreshBuildingInfo();

        // ★ FarmManager가 아직 초기화되지 않았거나 allCrops가 비어있으면 대기 후 빌드
        bool farmReady = FarmManager.Instance != null && FarmManager.Instance.allCrops.Count > 0;

        if (farmReady)
        {
            Debug.Log($"[FarmCropShop] OnEnable: FarmManager 준비됨 — 즉시 빌드 (allCrops:{FarmManager.Instance.allCrops.Count}개)");
            BuildAllSlots();
            // ★ SaveLoadManager가 아직 ApplyInventoryData를 안 불렀을 수 있으므로 지연 갱신 예약
            if (retryCoroutine != null) StopCoroutine(retryCoroutine);
            retryCoroutine = StartCoroutine(DelayedRefreshOwned());
        }
        else
        {
            Debug.LogWarning($"[FarmCropShop] OnEnable: FarmManager 미준비 (Instance:{FarmManager.Instance != null}) — 대기 후 빌드");
            if (retryCoroutine != null) StopCoroutine(retryCoroutine);
            retryCoroutine = StartCoroutine(WaitAndBuildSlots());
        }
    }

    /// <summary>
    /// FarmManager.Instance와 allCrops가 준비될 때까지 최대 2초(60프레임) 대기 후 빌드.
    /// 인트로→로그인→메인→팜씬 순서 진입 시 초기화 타이밍 차이 대응.
    /// </summary>
    private System.Collections.IEnumerator WaitAndBuildSlots()
    {
        int maxFrames = 60;
        int waited = 0;

        // ── FarmManager.Instance가 null이면 대기 ──
        while (FarmManager.Instance == null && waited < maxFrames)
        {
            waited++;
            yield return null;
        }

        if (FarmManager.Instance == null)
        {
            Debug.LogError($"[FarmCropShop] {waited}프레임 대기 후에도 FarmManager.Instance가 null! 씬에 FarmManager가 있는지 확인하세요.");
            retryCoroutine = null;
            yield break;
        }

        // ── allCrops가 비어있으면 추가 대기 ──
        while (FarmManager.Instance.allCrops.Count == 0 && waited < maxFrames)
        {
            waited++;
            yield return null;
        }

        if (FarmManager.Instance.allCrops.Count == 0)
        {
            Debug.LogError($"[FarmCropShop] {waited}프레임 대기 후에도 allCrops가 0개! Inspector에서 CropData SO를 allCrops에 연결하세요.");
            retryCoroutine = null;
            yield break;
        }

        Debug.Log($"[FarmCropShop] {waited}프레임 대기 후 빌드 시작 — allCrops:{FarmManager.Instance.allCrops.Count}개");
        BuildAllSlots();
        RefreshBuildingInfo();

        // ★ SaveLoadManager.ApplyInventoryData가 아직 완료 안 됐을 수 있으므로
        //   FarmInventoryUI 데이터 로드 후 보유수 재갱신 대기
        int extraWait = 0;
        while (GetFarmInventoryUI() == null && extraWait < 120)
        {
            extraWait++;
            yield return null;
        }
        // LoadSaveData가 ApplySaveData에서 불리므로 추가 5프레임 대기
        for (int i = 0; i < 5; i++) yield return null;

        RefreshOwnedCounts();
        Debug.Log($"[FarmCropShop] 보유수 갱신 완료 (추가 {extraWait + 5}프레임 대기)");

        retryCoroutine = null;
    }

    /// <summary>즉시 빌드 후 SaveLoadManager의 ApplyInventoryData 완료를 기다렸다가 보유수 갱신</summary>
    private System.Collections.IEnumerator DelayedRefreshOwned()
    {
        // SaveLoadManager: Start()+2프레임 후 ApplyInventoryData → FarmInventoryUI.LoadSaveData
        // 넉넉히 10프레임 대기
        for (int i = 0; i < 10; i++) yield return null;
        RefreshOwnedCounts();
        Debug.Log("[FarmCropShop] DelayedRefreshOwned 완료");
        retryCoroutine = null;
    }

    void OnDisable()
    {
        FarmBuildingManager.OnBuildingLevelChanged -= OnBuildingLevelChanged;
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }
    }

    // ═══ 열기/닫기 ═══════════════════════════════════════════════
    public void OpenShop()
    {
        shopPanel?.SetActive(true);

        // ★ 슬롯이 비어있으면 재빌드 (초기화 타이밍 문제 대응)
        if (veggieSlots.Count == 0 && fruitSlots.Count == 0)
        {
            bool farmReady = FarmManager.Instance != null && FarmManager.Instance.allCrops.Count > 0;
            if (farmReady)
            {
                Debug.Log("[FarmCropShop] OpenShop: 슬롯 비어있음 — 즉시 재빌드");
                BuildAllSlots();
            }
            else if (retryCoroutine == null)
            {
                Debug.Log("[FarmCropShop] OpenShop: 슬롯 비어있고 FarmManager 미준비 — 대기 후 빌드");
                retryCoroutine = StartCoroutine(WaitAndBuildSlots());
            }
        }

        SwitchTab(ShopTab.Vegetable);
        SoundManager.Instance?.PlayPanelOpen();
    }

    public void CloseShop()
    {
        shopPanel?.SetActive(false);
        FarmSceneController.Instance?.ResetBanner();
        // ★ 닫을 때 마지막 선택 초기화 (다음에 열면 빈 상태)
        lastVeggieCrop = null;
        lastFruitCrop = null;

        // 튜토리얼 트리거
        TutorialManager.Instance?.OnActionCompleted("CropShopClosed");
        lastWater = null;
        lastFert = null;
        HideAllDetailPanels();
        SoundManager.Instance?.PlayPanelClose();
    }

    // ═══ 탭 전환 ═════════════════════════════════════════════════
    private void SwitchTab(ShopTab tab)
    {
        currentTab = tab;
        UpdateScrollViews();
        UpdateTabColors();

        // ★ 탭 전환 시 모든 상세 패널 먼저 닫기 (채소↔과일 전환 시 이전 데이터 잔류 방지)
        HideAllDetailPanels();

        // 마지막 선택 복원 (있을 때만 해당 패널 다시 열기)
        switch (tab)
        {
            case ShopTab.Vegetable:
                if (lastVeggieCrop != null) SelectCrop(lastVeggieCrop);
                break;
            case ShopTab.Fruit:
                if (lastFruitCrop != null) SelectCrop(lastFruitCrop);
                break;
            case ShopTab.Water:
                if (lastWater != null) SelectWater(lastWater);
                break;
            case ShopTab.Fertilizer:
                if (lastFert != null) SelectFert(lastFert);
                break;
        }
    }

    private void UpdateScrollViews()
    {
        veggieScrollView?.SetActive(currentTab == ShopTab.Vegetable);
        fruitScrollView?.SetActive(currentTab == ShopTab.Fruit);
        waterScrollView?.SetActive(currentTab == ShopTab.Water);
        fertScrollView?.SetActive(currentTab == ShopTab.Fertilizer);
    }

    private void UpdateTabColors()
    {
        Color on = new Color(1f, 0.85f, 0.2f, 1f);
        Color off = new Color(0.55f, 0.55f, 0.55f, 1f);
        SetTabColor(tabVegetable, currentTab == ShopTab.Vegetable, on, off);
        SetTabColor(tabFruit, currentTab == ShopTab.Fruit, on, off);
        SetTabColor(tabWater, currentTab == ShopTab.Water, on, off);
        SetTabColor(tabFertilizer, currentTab == ShopTab.Fertilizer, on, off);
    }

    private void SetTabColor(Button btn, bool active, Color on, Color off)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img) img.color = active ? on : off;
    }

    // ═══ 전체 슬롯 빌드 (OnEnable 한 번만) ═══════════════════════
    private void BuildAllSlots()
    {
        BuildVeggieSlots();
        BuildFruitSlots();
        BuildWaterSlots();
        BuildFertSlots();
    }

    // ─── 씨앗 슬롯 공통 빌드 (보유 씨앗 최상단 정렬) ─────────────
    private void BuildCropSlots(CropType type, List<GameObject> slots, Transform container)
    {
        ClearSlots(slots, container);
        if (FarmManager.Instance == null || cropSlotPrefab == null || container == null)
        {
            Debug.LogWarning($"[FarmCropShop] {type} 슬롯 빌드 실패 — FarmManager:{FarmManager.Instance != null} / prefab:{cropSlotPrefab != null} / container:{container != null}");
            return;
        }

        // ★ 타입별 필터링
        var crops = new List<CropData>();
        foreach (var c in FarmManager.Instance.allCrops)
            if (c != null && c.cropType == type) crops.Add(c);

        // ★ 정렬: 보유 씨앗 > 0 인 것 먼저, 그 안에서 보유 수량 내림차순
        var farmInv = GetFarmInventoryUI();
        crops.Sort((a, b) =>
        {
            int ownedA = farmInv?.GetSeedCount(a.cropID) ?? 0;
            int ownedB = farmInv?.GetSeedCount(b.cropID) ?? 0;
            // 1) 보유 여부 (보유 > 미보유)
            int hasA = ownedA > 0 ? 1 : 0;
            int hasB = ownedB > 0 ? 1 : 0;
            if (hasA != hasB) return hasB.CompareTo(hasA);
            // 2) 보유 수량 내림차순
            if (ownedA != ownedB) return ownedB.CompareTo(ownedA);
            // 3) cropID 오름차순 (원래 순서 유지)
            return a.cropID.CompareTo(b.cropID);
        });

        foreach (var crop in crops)
        {
            bool unlocked = IsUnlocked(crop.requiredPlayerLevel, crop.requiredGreenhouseLevel);
            int owned = farmInv?.GetSeedCount(crop.cropID) ?? 0;

            var go = Instantiate(cropSlotPrefab, container);
            var cap = crop;

            var slot = go.GetComponent<FarmCropSlotUI>();
            slot?.Setup(cap, unlocked, owned, () => SelectCrop(cap));

            var rootBtn = go.GetComponent<Button>();
            if (rootBtn != null && (slot == null || rootBtn != slot.selectButton))
                rootBtn.onClick.AddListener(() => SelectCrop(cap));

            slots.Add(go);
        }
        Debug.Log($"[FarmCropShop] {type} 슬롯 {slots.Count}개 생성 (보유 씨앗 우선 정렬)");
    }

    // ─── 채소 씨앗 슬롯 ──────────────────────────────────────────
    private void BuildVeggieSlots() => BuildCropSlots(CropType.Vegetable, veggieSlots, veggieContainer);

    // ─── 과일 씨앗 슬롯 ──────────────────────────────────────────
    private void BuildFruitSlots() => BuildCropSlots(CropType.Fruit, fruitSlots, fruitContainer);

    // ─── 물 슬롯 ─────────────────────────────────────────────────
    private void BuildWaterSlots()
    {
        ClearSlots(waterSlots, waterContainer);
        if (supplySlotPrefab == null || waterContainer == null) return;

        var waters = availableWaters;
        if (waters == null || waters.Count == 0) return;

        foreach (var water in waters)
        {
            if (water == null) continue;
            bool unlocked = IsUnlocked(water.requiredPlayerLevel);
            int owned = GetFarmInventoryUI()?.GetWaterCount(water.waterID) ?? 0;

            var go = Instantiate(supplySlotPrefab, waterContainer);
            var cap = water;

            var slot = go.GetComponent<FarmSupplySlotUI>();

            // ★ selectButton(선택 버튼)에 직접 콜백 등록 — 이게 실제로 클릭되는 버튼
            slot?.SetupWater(water, owned, unlocked, () => SelectWater(cap));

            // ★ 루트 Button에도 동시 등록 (슬롯 배경 클릭 시에도 동작)
            var rootBtn = go.GetComponent<Button>();
            if (rootBtn != null && rootBtn != slot?.selectButton)
                rootBtn.onClick.AddListener(() => SelectWater(cap));

            waterSlots.Add(go);
        }
    }

    // ─── 비료 슬롯 ───────────────────────────────────────────────
    private void BuildFertSlots()
    {
        ClearSlots(fertSlots, fertContainer);
        if (FarmManager.Instance == null || supplySlotPrefab == null || fertContainer == null) return;

        foreach (var fert in FarmManager.Instance.allFertilizers)
        {
            if (fert == null) continue;
            bool unlocked = IsUnlocked(fert.requiredPlayerLevel);
            int owned = GetFarmInventoryUI()?.GetFertCount(fert.fertilizerID) ?? 0;

            var go = Instantiate(supplySlotPrefab, fertContainer);
            var cap = fert;

            // ★ SetupFertilizer에 콜백 직접 전달 → selectButton(선택 버튼)에 바로 등록됨
            var slot = go.GetComponent<FarmSupplySlotUI>();
            slot?.SetupFertilizer(fert, owned, unlocked, () => SelectFert(cap));

            fertSlots.Add(go);
        }
    }

    // ─── FarmInventoryUI 탐색 (Instance 우선, 없으면 FindObjectOfType 폴백) ──
    private FarmInventoryUI GetFarmInventoryUI()
    {
        if (FarmInventoryUI.Instance != null) return FarmInventoryUI.Instance;
        var found = FindObjectOfType<FarmInventoryUI>(true);
        if (found != null)
            Debug.Log("[FarmCropShop] FarmInventoryUI.Instance null → FindObjectOfType로 발견");
        return found;
    }

    /// <summary>SaveLoadManager의 ApplyInventoryData 완료 후 호출 — 슬롯 재빌드 + 보유수 갱신</summary>
    public void RefreshAfterDataLoad()
    {
        Debug.Log("[FarmCropShop] RefreshAfterDataLoad 호출 — 슬롯 재빌드 (보유 씨앗 정렬 반영)");
        BuildAllSlots();
        // 상세 패널이 열려있으면 보유 수량도 갱신
        if (selectedCrop != null)
        {
            int owned = GetFarmInventoryUI()?.GetSeedCount(selectedCrop.cropID) ?? 0;
            if (ownedCountText) ownedCountText.text = $"보유 {owned}개";
        }
    }

    // ─── 슬롯 보유수 갱신 (구매 후) ──────────────────────────────
    private void RefreshOwnedCounts()
    {
        var farmInv = GetFarmInventoryUI();

        // ★ 채소/과일 슬롯: 슬롯의 LinkedCrop에서 직접 cropID 참조 (정렬 순서 무관)
        RefreshCropSlotOwned(veggieSlots, farmInv);
        RefreshCropSlotOwned(fruitSlots, farmInv);
        // 물 슬롯
        for (int i = 0; i < waterSlots.Count && i < availableWaters.Count; i++)
            waterSlots[i]?.GetComponent<FarmSupplySlotUI>()
                ?.UpdateOwned(farmInv?.GetWaterCount(availableWaters[i].waterID) ?? 0);

        // 비료 슬롯
        if (FarmManager.Instance != null)
        {
            int idx = 0;
            foreach (var fert in FarmManager.Instance.allFertilizers)
            {
                if (idx >= fertSlots.Count) break;
                fertSlots[idx]?.GetComponent<FarmSupplySlotUI>()
                    ?.UpdateOwned(farmInv?.GetFertCount(fert.fertilizerID) ?? 0);
                idx++;
            }
        }
    }

    // ═══ 씨앗 선택 → 상세 패널 ═══════════════════════════════════
    private void SelectCrop(CropData crop)
    {
        selectedCrop = crop;
        selectedWater = null;
        selectedFert = null;

        if (currentTab == ShopTab.Vegetable) lastVeggieCrop = crop;
        else if (currentTab == ShopTab.Fruit) lastFruitCrop = crop;

        // ★ 반대 패널만 닫기 (detailPanel은 건드리지 않음 → 깜빡임 방지)
        supplyDetailPanel?.SetActive(false);
        if (detailPanel == null) return;
        detailPanel.SetActive(true);

        if (cropIcon)
        {
            Sprite icon = crop.seedIcon;
            if (icon == null) icon = (crop.growthSprites != null && crop.growthSprites.Length > 0) ? crop.growthSprites[0] : null;
            if (icon == null) icon = crop.harvestIcon;
            cropIcon.sprite = icon;
            cropIcon.enabled = icon != null;
        }
        if (cropNameText) cropNameText.text = crop.cropName;
        if (cropDescText) cropDescText.text = crop.description;

        float wb = FarmBuildingManager.Instance?.GetWaterTimeBonus() ?? 0f;
        float fb = FarmBuildingManager.Instance?.GetFertilizerTimeBonus() ?? 0f;
        if (growthTimeText)
            growthTimeText.text = $"⏱ 기본 {FormatTime(crop.growthTimeSeconds)}\n"
                                + $"💧 물주기 {FormatTime(crop.GetModifiedGrowthTime(true, false, wb, 0f))}\n"
                                + $"✨ 비료+물 {FormatTime(crop.GetModifiedGrowthTime(true, true, wb, fb))}";

        if (costText) costText.text = crop.seedCostGem > 0 ? $"{crop.seedCostGem}" : $"{crop.seedCostGold}";

        int owned = GetFarmInventoryUI()?.GetSeedCount(crop.cropID) ?? 0;
        if (ownedCountText) ownedCountText.text = $"보유 {owned}개";

        bool unlocked = IsUnlocked(crop.requiredPlayerLevel, crop.requiredGreenhouseLevel);

        if (lockReasonText)
        {
            lockReasonText.gameObject.SetActive(!unlocked);
            if (!unlocked)
            {
                string r = "";
                int ghLv = FarmBuildingManager.Instance != null ? FarmBuildingManager.Instance.GreenhouseLevel : 1;
                if (crop.requiredGreenhouseLevel > ghLv) r += $"비닐하우스 Lv{crop.requiredGreenhouseLevel} 필요 ";
                if (crop.requiredPlayerLevel > 1)
                {
                    int plyrLv = GameManager.Instance != null ? GameManager.Instance.PlayerLevel : (GameDataBridge.CurrentData?.playerLevel ?? 1);
                    if (crop.requiredPlayerLevel > plyrLv) r += $"플레이어 Lv{crop.requiredPlayerLevel} 필요";
                }
                lockReasonText.text = r;
            }
        }

        // ★ buyButton이 Inspector에서 연결 안 됐으면 자동 탐색
        if (buyButton == null && detailPanel != null)
        {
            var btns = detailPanel.GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                string n = b.gameObject.name.ToLower();
                if (n.Contains("buy") && !n.Contains("x5") && !n.Contains("x10"))
                { buyButton = b; Debug.LogWarning($"[FarmCropShop] buyButton 자동 탐색: {b.gameObject.name}"); break; }
            }
        }
        if (buyX5Button == null && detailPanel != null)
        {
            var btns = detailPanel.GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                string n = b.gameObject.name.ToLower();
                if (n.Contains("x5") || n.Contains("5"))
                { buyX5Button = b; break; }
            }
        }
        if (buyX10Button == null && detailPanel != null)
        {
            var btns = detailPanel.GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                string n = b.gameObject.name.ToLower();
                if (n.Contains("x10") || n.Contains("10"))
                { buyX10Button = b; break; }
            }
        }

        // ★ 구매 버튼 리스너 재등록 (선택된 crop을 확실히 캡처)
        if (buyButton)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => TryBuyCrop(crop, 1));
            buyButton.interactable = unlocked;
            Debug.Log($"[FarmCropShop] buyButton 리스너 등록: {crop.cropName} x1 / interactable={unlocked}");
        }
        else
        {
            Debug.LogError("[FarmCropShop] buyButton이 null! Inspector에서 buyButton을 연결하거나, detailPanel 하위에 'Buy' 이름의 Button을 배치하세요.");
        }
        if (buyX5Button)
        {
            buyX5Button.onClick.RemoveAllListeners();
            buyX5Button.onClick.AddListener(() => TryBuyCrop(crop, 5));
            buyX5Button.interactable = unlocked;
        }
        if (buyX10Button)
        {
            buyX10Button.onClick.RemoveAllListeners();
            buyX10Button.onClick.AddListener(() => TryBuyCrop(crop, 10));
            buyX10Button.interactable = unlocked;
        }

        Debug.Log($"[FarmCropShop] SelectCrop: {crop.cropName} / buyBtn:{buyButton != null} / unlocked:{unlocked} / detailPanel:{detailPanel != null}");
        SoundManager.Instance?.PlayButtonClick();
    }

    // ═══ 물 선택 → 상세 패널 ════════════════════════════════════
    private void SelectWater(WaterData water)
    {
        selectedWater = water;
        selectedCrop = null;
        selectedFert = null;
        lastWater = water;

        // ★ 반대 패널만 닫기
        detailPanel?.SetActive(false);
        if (supplyDetailPanel == null) return;
        supplyDetailPanel.SetActive(true);

        if (supplyIcon) supplyIcon.sprite = water.icon;
        if (supplyNameText) supplyNameText.text = water.waterName;
        if (supplyDescText) supplyDescText.text = water.description;
        if (supplyEffectText)
            supplyEffectText.text = water.extraSpeedBonus > 0
                ? $"💧 성장 {water.extraSpeedBonus * 100f:F0}% 단축"
                : "💧 기본 물주기";
        if (supplyCostText) supplyCostText.text = water.costGem > 0 ? $"{water.costGem}" : $"{water.costGold}";

        bool unlocked = IsUnlocked(water.requiredPlayerLevel);
        int owned = GetFarmInventoryUI()?.GetWaterCount(water.waterID) ?? 0;

        if (supplyOwnedText) supplyOwnedText.text = $"보유 {owned}개";
        if (supplyLockText)
        {
            supplyLockText.gameObject.SetActive(!unlocked);
            if (!unlocked) supplyLockText.text = $"플레이어 Lv.{water.requiredPlayerLevel} 필요";
        }

        if (buySupplyButton) buySupplyButton.interactable = unlocked;
        if (buySupplyX5Button) buySupplyX5Button.interactable = unlocked;
        if (buySupplyX10Button) buySupplyX10Button.interactable = unlocked;

        SoundManager.Instance?.PlayButtonClick();
    }

    // ═══ 비료 선택 → 상세 패널 ════════════════════════════════════
    private void SelectFert(FertilizerData fert)
    {
        Debug.Log($"SelectFert 호출됨: {fert?.fertilizerName}");

        selectedFert = fert;
        selectedCrop = null;
        selectedWater = null;
        lastFert = fert;

        // ★ 반대 패널만 닫기
        detailPanel?.SetActive(false);
        if (supplyDetailPanel == null) return;
        supplyDetailPanel.SetActive(true);

        if (supplyIcon) supplyIcon.sprite = fert.icon;
        if (supplyNameText) supplyNameText.text = fert.fertilizerName;
        if (supplyDescText) supplyDescText.text = fert.description;
        if (supplyEffectText)
            supplyEffectText.text = $"🌿 수확량 +{fert.yieldBonus * 100f:F0}%  속도 +{fert.speedBonus * 100f:F0}%";
        if (supplyCostText) supplyCostText.text = fert.costGem > 0 ? $"{fert.costGem}" : $"{fert.costGold}";

        bool unlocked = IsUnlocked(fert.requiredPlayerLevel);
        int owned = GetFarmInventoryUI()?.GetFertCount(fert.fertilizerID) ?? 0;

        if (supplyOwnedText) supplyOwnedText.text = $"보유 {owned}개";
        if (supplyLockText)
        {
            supplyLockText.gameObject.SetActive(!unlocked);
            if (!unlocked) supplyLockText.text = $"플레이어 Lv.{fert.requiredPlayerLevel} 필요";
        }

        if (buySupplyButton) buySupplyButton.interactable = unlocked;
        if (buySupplyX5Button) buySupplyX5Button.interactable = unlocked;
        if (buySupplyX10Button) buySupplyX10Button.interactable = unlocked;

        SoundManager.Instance?.PlayButtonClick();
    }

    // ═══ 구매 ════════════════════════════════════════════════════
    private void TryBuyCrop(CropData crop, int amount)
    {
        if (crop == null) return;

        // ── 1) 재화 차감 (SpendCurrency: GameManager 우선 → GameDataBridge 폴백) ──
        bool useGem = crop.seedCostGem > 0;
        int totalGem = crop.seedCostGem * amount;
        long totalGold = crop.seedCostGold * amount;

        if (!SpendCurrency(useGem, totalGem, totalGold))
        {
            UIManager.Instance?.ShowMessage(useGem ? "보석이 부족합니다!" : "골드가 부족합니다!", Color.red);
            return;
        }

        // ── 2) 씨앗 인벤토리 추가 ──
        var farmInvUI = GetFarmInventoryUI();
        if (farmInvUI != null)
        {
            farmInvUI.AddSeed(crop.cropID, amount);
        }
        else
        {
            // FarmInventoryUI 없을 때 GameDataBridge.CurrentData에 직접 기록
            AddSeedToSaveData(crop.cropID, amount);
            Debug.LogWarning($"[CropShop] FarmInventoryUI 없음 → SaveData에 직접 기록: {crop.cropName} x{amount}");
        }

        // ── 3) UI 갱신 ──
        int owned = farmInvUI?.GetSeedCount(crop.cropID) ?? amount;
        if (ownedCountText) ownedCountText.text = $"보유 {owned}개";

        // ★ 구매 후 보유수 갱신 + 기존 슬롯 재정렬 (Destroy 타이밍 문제 방지)
        RefreshOwnedCounts();
        if (currentTab == ShopTab.Vegetable) ResortSlots(veggieSlots);
        else if (currentTab == ShopTab.Fruit) ResortSlots(fruitSlots);

        UIManager.Instance?.ShowMessage($"{crop.cropName} x{amount} 구매!", Color.green);
        SoundManager.Instance?.PlayPurchaseSound();

        SaveLoadManager.Instance?.SaveGame();

        // ★ 튜토리얼 트리거: 수량별 + 레거시 "BuySeed" 모두 전송
        TutorialManager.Instance?.OnActionCompleted($"{amount}개구매");
        TutorialManager.Instance?.OnActionCompleted("BuySeed");

        // ★ 튜토리얼 중이면 물+비료 자동 지급 (첫 구매 시 1회만)
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
        {
            if (farmInvUI != null)
            {
                // 물이 없으면 기본 물 5개 지급
                if (availableWaters != null && availableWaters.Count > 0)
                {
                    var firstWater = availableWaters[0];
                    if (farmInvUI.GetWaterCount(firstWater.waterID) <= 0)
                    {
                        farmInvUI.AddWater(firstWater.waterID, 5);
                        Debug.Log($"[Tutorial] 물 자동 지급: {firstWater.waterName} x5");
                    }
                }

                // 비료가 없으면 기본 비료 5개 지급
                var allFerts = FarmManager.Instance?.allFertilizers;
                if (allFerts != null && allFerts.Count > 0)
                {
                    var firstFert = allFerts[0];
                    if (farmInvUI.GetFertCount(firstFert.fertilizerID) <= 0)
                    {
                        farmInvUI.AddFertilizer(firstFert.fertilizerID, 5);
                        Debug.Log($"[Tutorial] 비료 자동 지급: {firstFert.fertilizerName} x5");
                    }
                }
            }
        }
    }

    /// <summary>FarmInventoryUI가 없을 때 GameDataBridge.CurrentData에 씨앗 직접 기록</summary>
    private void AddSeedToSaveData(int cropID, int amount)
    {
        SaveData save = GameDataBridge.CurrentData;
        if (save == null) return;

        if (save.farmData == null) save.farmData = new FarmSaveData();
        if (save.farmData.inventoryData == null) save.farmData.inventoryData = new FarmInventorySaveData();
        if (save.farmData.inventoryData.seeds == null) save.farmData.inventoryData.seeds = new System.Collections.Generic.List<FarmItemCount>();

        foreach (var s in save.farmData.inventoryData.seeds)
        {
            if (s.cropID == cropID)
            {
                s.count += amount;
                return;
            }
        }
        save.farmData.inventoryData.seeds.Add(new FarmItemCount { cropID = cropID, count = amount });
    }

    private void TryBuySupply(int amount)
    {
        if (selectedWater != null) TryBuyWater(selectedWater, amount);
        else if (selectedFert != null) TryBuyFert(selectedFert, amount);
    }

    private void TryBuyWater(WaterData water, int amount)
    {
        Debug.Log($"[CropShop] 구매 버튼 클릭: {water.waterName} / 수량: {amount}");
        bool paid = SpendCurrency(water.costGem > 0, water.costGem * amount, water.costGold * amount);
        if (!paid) { UIManager.Instance?.ShowMessage("재화 부족!", Color.red); return; }

        var farmInvUI = GetFarmInventoryUI();
        farmInvUI?.AddWater(water.waterID, amount);
        int owned = farmInvUI?.GetWaterCount(water.waterID) ?? 0;
        if (supplyOwnedText) supplyOwnedText.text = $"보유 {owned}개";
        RefreshOwnedCounts();
        Debug.Log($"[CropShop] 구매 결과: 보유골드: {GetCurrentGold()} / {water.waterName} 보유: {owned}");
        UIManager.Instance?.ShowMessage($"💧 {water.waterName} x{amount} 구매!", Color.cyan);
        SoundManager.Instance?.PlayPurchaseSound();

        SaveLoadManager.Instance?.SaveGame();
    }

    private void TryBuyFert(FertilizerData fert, int amount)
    {
        Debug.Log($"[CropShop] 구매 버튼 클릭: {fert.fertilizerName} / 수량: {amount}");
        bool paid = SpendCurrency(fert.costGem > 0, fert.costGem * amount, fert.costGold * amount);
        if (!paid) { UIManager.Instance?.ShowMessage("재화 부족!", Color.red); return; }

        var farmInvUI = GetFarmInventoryUI();
        farmInvUI?.AddFertilizer(fert.fertilizerID, amount);
        int owned = farmInvUI?.GetFertCount(fert.fertilizerID) ?? 0;
        if (supplyOwnedText) supplyOwnedText.text = $"보유 {owned}개";
        RefreshOwnedCounts();
        Debug.Log($"[CropShop] 구매 결과: 보유골드: {GetCurrentGold()} / {fert.fertilizerName} 보유: {owned}");
        UIManager.Instance?.ShowMessage($"🌿 {fert.fertilizerName} x{amount} 구매!", Color.green);
        SoundManager.Instance?.PlayPurchaseSound();

        SaveLoadManager.Instance?.SaveGame();
    }

    /// <summary>재화 차감 (GameManager 우선, 없으면 GameDataBridge 폴백)</summary>
    private bool SpendCurrency(bool useGem, int gemAmount, long goldAmount)
    {
        // GameManager가 있으면 사용
        if (GameManager.Instance != null)
        {
            return useGem
                ? GameManager.Instance.SpendGem(gemAmount)
                : GameManager.Instance.SpendGold(goldAmount);
        }

        // FarmScene 폴백: GameDataBridge 직접 차감
        SaveData data = GameDataBridge.CurrentData;
        if (data == null) return false;

        if (useGem)
        {
            if (data.playerGem < gemAmount) return false;
            data.playerGem -= gemAmount;
        }
        else
        {
            if (data.playerGold < goldAmount) return false;
            data.playerGold -= goldAmount;
        }
        return true;
    }

    private long GetCurrentGold()
    {
        return GameManager.Instance != null ? GameManager.Instance.playerGold : (GameDataBridge.CurrentData?.playerGold ?? 0);
    }

    // ═══ 유틸 ════════════════════════════════════════════════════
    private void HideAllDetailPanels()
    {
        detailPanel?.SetActive(false);
        supplyDetailPanel?.SetActive(false);
    }

    private void ClearSlots(List<GameObject> list, Transform container)
    {
        // ★ Destroy는 프레임 끝에 실행됨 → 즉시 비활성화하여 GridLayout 중복 배치 방지
        foreach (var go in list)
        {
            if (go != null)
            {
                go.SetActive(false);
                Destroy(go);
            }
        }
        list.Clear();
    }

    /// <summary>기존 슬롯의 sibling 순서를 보유 수량 기준으로 재정렬 (Destroy+Instantiate 없음)</summary>
    private void ResortSlots(List<GameObject> slots)
    {
        if (slots == null || slots.Count <= 1) return;

        var farmInv = GetFarmInventoryUI();

        // 보유 수량 기준 정렬 (보유 > 0 먼저, 수량 내림차순, cropID 오름차순)
        slots.Sort((a, b) =>
        {
            var slotA = a?.GetComponent<FarmCropSlotUI>();
            var slotB = b?.GetComponent<FarmCropSlotUI>();
            int ownedA = (slotA?.LinkedCrop != null) ? (farmInv?.GetSeedCount(slotA.LinkedCrop.cropID) ?? 0) : 0;
            int ownedB = (slotB?.LinkedCrop != null) ? (farmInv?.GetSeedCount(slotB.LinkedCrop.cropID) ?? 0) : 0;

            int hasA = ownedA > 0 ? 1 : 0;
            int hasB = ownedB > 0 ? 1 : 0;
            if (hasA != hasB) return hasB.CompareTo(hasA);
            if (ownedA != ownedB) return ownedB.CompareTo(ownedA);

            int idA = slotA?.LinkedCrop?.cropID ?? 0;
            int idB = slotB?.LinkedCrop?.cropID ?? 0;
            return idA.CompareTo(idB);
        });

        // sibling 순서 적용 (Instantiate/Destroy 없이 순서만 변경)
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null) slots[i].transform.SetSiblingIndex(i);
    }

    private List<CropData> GetCropsByType(CropType type)
    {
        var result = new List<CropData>();
        if (FarmManager.Instance == null) return result;
        foreach (var c in FarmManager.Instance.allCrops)
            if (c.cropType == type) result.Add(c);
        return result;
    }

    private void RefreshBuildingInfo()
    {
        if (FarmBuildingManager.Instance == null) return;
        int ghLv = FarmBuildingManager.Instance.GreenhouseLevel;
        float wb = FarmBuildingManager.Instance.GetWaterTimeBonus();
        float fb = FarmBuildingManager.Instance.GetFertilizerTimeBonus();
        if (greenhouseLevelText) greenhouseLevelText.text = $"비닐하우스 Lv{ghLv}";
        if (watermillBonusText) watermillBonusText.text = $"💧 물주기 가속 +{wb * 100f:F0}%";
        if (windmillBonusText) windmillBonusText.text = $"💨 비료 가속 +{fb * 100f:F0}%";
    }

    private void OnBuildingLevelChanged(BuildingType type, int level)
    {
        RefreshBuildingInfo();
        BuildAllSlots();  // 레벨 바뀌면 잠금 상태 전체 재빌드
    }

    /// <summary>작물/물/비료의 잠금 해제 여부 판정 (requiredPlayerLevel <= 1이면 무조건 해제)</summary>
    private bool IsUnlocked(int requiredPlayerLevel, int requiredGreenhouseLevel = 0)
    {
        // 온실 레벨 체크
        if (requiredGreenhouseLevel > 0)
        {
            int ghLv = FarmBuildingManager.Instance != null ? FarmBuildingManager.Instance.GreenhouseLevel : 1;
            if (ghLv < requiredGreenhouseLevel) return false;
        }

        // 플레이어 레벨 체크 (1 이하면 무조건 해제)
        if (requiredPlayerLevel <= 1) return true;

        // GameManager 우선 → GameDataBridge 폴백
        if (GameManager.Instance != null)
            return GameManager.Instance.PlayerLevel >= requiredPlayerLevel;

        return (GameDataBridge.CurrentData?.playerLevel ?? 1) >= requiredPlayerLevel;
    }

    /// <summary>슬롯 리스트의 보유수 갱신 (LinkedCrop에서 cropID 직접 참조)</summary>
    private void RefreshCropSlotOwned(List<GameObject> slots, FarmInventoryUI farmInv)
    {
        foreach (var go in slots)
        {
            if (go == null) continue;
            var slot = go.GetComponent<FarmCropSlotUI>();
            if (slot?.LinkedCrop == null) continue;
            slot.UpdateOwned(farmInv?.GetSeedCount(slot.LinkedCrop.cropID) ?? 0);
        }
    }

    private string FormatTime(float s)
    {
        int h = (int)(s / 3600), m = (int)((s % 3600) / 60), ss = (int)(s % 60);
        if (h > 0) return $"{h}시간 {m}분";
        if (m > 0) return $"{m}분 {ss}초";
        return $"{ss}초";
    }
}