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
        closeButton?.onClick.AddListener(CloseShop);

        tabVegetable?.onClick.AddListener(() => SwitchTab(ShopTab.Vegetable));
        tabFruit?.onClick.AddListener(() => SwitchTab(ShopTab.Fruit));
        tabWater?.onClick.AddListener(() => SwitchTab(ShopTab.Water));
        tabFertilizer?.onClick.AddListener(() => SwitchTab(ShopTab.Fertilizer));

        buyButton?.onClick.AddListener(() => TryBuyCrop(selectedCrop, 1));
        buyX5Button?.onClick.AddListener(() => TryBuyCrop(selectedCrop, 5));
        buyX10Button?.onClick.AddListener(() => TryBuyCrop(selectedCrop, 10));

        buySupplyButton?.onClick.AddListener(() => TryBuySupply(1));
        buySupplyX5Button?.onClick.AddListener(() => TryBuySupply(5));
        buySupplyX10Button?.onClick.AddListener(() => TryBuySupply(10));
    }

    void OnEnable()
    {
        FarmBuildingManager.OnBuildingLevelChanged += OnBuildingLevelChanged;
        BuildAllSlots();   // 열릴 때 전체 슬롯 한 번 빌드
        RefreshBuildingInfo();
    }

    void OnDisable()
    {
        FarmBuildingManager.OnBuildingLevelChanged -= OnBuildingLevelChanged;
    }

    // ═══ 열기/닫기 ═══════════════════════════════════════════════
    public void OpenShop()
    {
        shopPanel?.SetActive(true);
        SwitchTab(ShopTab.Vegetable);
        SoundManager.Instance?.PlayPanelOpen();
    }

    public void CloseShop()
    {
        shopPanel?.SetActive(false);
        // ★ 닫을 때 마지막 선택 초기화 (다음에 열면 빈 상태)
        lastVeggieCrop = null;
        lastFruitCrop = null;
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

        bool isSeedTab = tab == ShopTab.Vegetable || tab == ShopTab.Fruit;
        bool isSupplyTab = tab == ShopTab.Water || tab == ShopTab.Fertilizer;

        // 탭 유형이 바뀔 때만 반대 패널 닫기
        if (isSeedTab) supplyDetailPanel?.SetActive(false);
        if (isSupplyTab) detailPanel?.SetActive(false);

        // 마지막 선택 복원 (없으면 해당 패널도 닫기)
        switch (tab)
        {
            case ShopTab.Vegetable:
                if (lastVeggieCrop != null) SelectCrop(lastVeggieCrop);
                else detailPanel?.SetActive(false);
                break;
            case ShopTab.Fruit:
                if (lastFruitCrop != null) SelectCrop(lastFruitCrop);
                else detailPanel?.SetActive(false);
                break;
            case ShopTab.Water:
                if (lastWater != null) SelectWater(lastWater);
                else supplyDetailPanel?.SetActive(false);
                break;
            case ShopTab.Fertilizer:
                if (lastFert != null) SelectFert(lastFert);
                else supplyDetailPanel?.SetActive(false);
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

    // ─── 채소 씨앗 슬롯 ──────────────────────────────────────────
    private void BuildVeggieSlots()
    {
        ClearSlots(veggieSlots, veggieContainer);
        if (FarmManager.Instance == null || cropSlotPrefab == null || veggieContainer == null) return;

        int ghLv = FarmBuildingManager.Instance != null ? FarmBuildingManager.Instance.GreenhouseLevel : 1;
        int plyrLv = PlayerStats.Instance != null ? PlayerStats.Instance.level : 1;

        foreach (var crop in FarmManager.Instance.allCrops)
        {
            if (crop == null || crop.cropType != CropType.Vegetable) continue;
            bool unlocked = crop.requiredGreenhouseLevel <= ghLv && crop.requiredPlayerLevel <= plyrLv;
            int owned = FarmInventoryUI.Instance?.GetSeedCount(crop.cropID) ?? 0;

            var go = Instantiate(cropSlotPrefab, veggieContainer);
            var cap = crop;

            var slot = go.GetComponent<FarmCropSlotUI>();
            slot?.Setup(cap, unlocked, owned, null);

            // ★ root Button 직접 사용
            var btn = go.GetComponent<Button>();
            if (btn == null) btn = slot?.selectButton;
            btn?.onClick.AddListener(() => SelectCrop(cap));

            veggieSlots.Add(go);
        }
    }

    // ─── 과일 씨앗 슬롯 ──────────────────────────────────────────
    private void BuildFruitSlots()
    {
        ClearSlots(fruitSlots, fruitContainer);
        if (FarmManager.Instance == null || cropSlotPrefab == null || fruitContainer == null) return;

        int ghLv = FarmBuildingManager.Instance != null ? FarmBuildingManager.Instance.GreenhouseLevel : 1;
        int plyrLv = PlayerStats.Instance != null ? PlayerStats.Instance.level : 1;

        foreach (var crop in FarmManager.Instance.allCrops)
        {
            if (crop == null || crop.cropType != CropType.Fruit) continue;
            bool unlocked = crop.requiredGreenhouseLevel <= ghLv && crop.requiredPlayerLevel <= plyrLv;
            int owned = FarmInventoryUI.Instance?.GetSeedCount(crop.cropID) ?? 0;

            var go = Instantiate(cropSlotPrefab, fruitContainer);
            var cap = crop;

            var slot = go.GetComponent<FarmCropSlotUI>();
            slot?.Setup(cap, unlocked, owned, null);

            // ★ root Button 직접 사용
            var btn = go.GetComponent<Button>();
            if (btn == null) btn = slot?.selectButton;
            btn?.onClick.AddListener(() => SelectCrop(cap));

            fruitSlots.Add(go);
        }
    }

    // ─── 물 슬롯 ─────────────────────────────────────────────────
    private void BuildWaterSlots()
    {
        ClearSlots(waterSlots, waterContainer);
        if (supplySlotPrefab == null || waterContainer == null) return;

        var waters = availableWaters;
        if (waters == null || waters.Count == 0) return;

        int plyrLv = PlayerStats.Instance != null ? PlayerStats.Instance.level : 1;

        foreach (var water in waters)
        {
            if (water == null) continue;
            bool unlocked = water.requiredPlayerLevel <= plyrLv;
            int owned = FarmInventoryUI.Instance?.GetWaterCount(water.waterID) ?? 0;

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

        int plyrLv = PlayerStats.Instance != null ? PlayerStats.Instance.level : 1;

        foreach (var fert in FarmManager.Instance.allFertilizers)
        {
            if (fert == null) continue;
            bool unlocked = fert.requiredPlayerLevel <= plyrLv;
            int owned = FarmInventoryUI.Instance?.GetFertCount(fert.fertilizerID) ?? 0;

            var go = Instantiate(supplySlotPrefab, fertContainer);
            var cap = fert;

            // ★ SetupFertilizer에 콜백 직접 전달 → selectButton(선택 버튼)에 바로 등록됨
            var slot = go.GetComponent<FarmSupplySlotUI>();
            slot?.SetupFertilizer(fert, owned, unlocked, () => SelectFert(cap));

            fertSlots.Add(go);
        }
    }

    // ─── 슬롯 보유수 갱신 (구매 후) ──────────────────────────────
    private void RefreshOwnedCounts()
    {
        // 채소 슬롯
        int vi = 0;
        foreach (var crop in GetCropsByType(CropType.Vegetable))
        {
            if (vi >= veggieSlots.Count) break;
            veggieSlots[vi]?.GetComponent<FarmCropSlotUI>()
                ?.UpdateOwned(FarmInventoryUI.Instance?.GetSeedCount(crop.cropID) ?? 0);
            vi++;
        }
        // 과일 슬롯
        int fi = 0;
        foreach (var crop in GetCropsByType(CropType.Fruit))
        {
            if (fi >= fruitSlots.Count) break;
            fruitSlots[fi]?.GetComponent<FarmCropSlotUI>()
                ?.UpdateOwned(FarmInventoryUI.Instance?.GetSeedCount(crop.cropID) ?? 0);
            fi++;
        }
        // 물 슬롯
        for (int i = 0; i < waterSlots.Count && i < availableWaters.Count; i++)
            waterSlots[i]?.GetComponent<FarmSupplySlotUI>()
                ?.UpdateOwned(FarmInventoryUI.Instance?.GetWaterCount(availableWaters[i].waterID) ?? 0);

        // 비료 슬롯
        if (FarmManager.Instance != null)
        {
            int idx = 0;
            foreach (var fert in FarmManager.Instance.allFertilizers)
            {
                if (idx >= fertSlots.Count) break;
                fertSlots[idx]?.GetComponent<FarmSupplySlotUI>()
                    ?.UpdateOwned(FarmInventoryUI.Instance?.GetFertCount(fert.fertilizerID) ?? 0);
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

        if (cropIcon) cropIcon.sprite = crop.seedIcon;
        if (cropNameText) cropNameText.text = crop.cropName;
        if (cropDescText) cropDescText.text = crop.description;

        float wb = FarmBuildingManager.Instance?.GetWaterTimeBonus() ?? 0f;
        float fb = FarmBuildingManager.Instance?.GetFertilizerTimeBonus() ?? 0f;
        if (growthTimeText)
            growthTimeText.text = $"⏱ 기본 {FormatTime(crop.growthTimeSeconds)}\n"
                                + $"💧 물주기 {FormatTime(crop.GetModifiedGrowthTime(true, false, wb, 0f))}\n"
                                + $"✨ 비료+물 {FormatTime(crop.GetModifiedGrowthTime(true, true, wb, fb))}";

        if (costText) costText.text = crop.seedCostGem > 0 ? $"💎 {crop.seedCostGem}" : $"💰 {crop.seedCostGold}";

        int owned = FarmInventoryUI.Instance?.GetSeedCount(crop.cropID) ?? 0;
        if (ownedCountText) ownedCountText.text = $"보유 {owned}개";

        int ghLv = FarmBuildingManager.Instance?.GreenhouseLevel ?? 1;
        int plyrLv = PlayerStats.Instance?.level ?? 1;
        bool unlocked = crop.requiredGreenhouseLevel <= ghLv && crop.requiredPlayerLevel <= plyrLv;

        if (lockReasonText)
        {
            lockReasonText.gameObject.SetActive(!unlocked);
            if (!unlocked)
            {
                string r = "";
                if (crop.requiredGreenhouseLevel > ghLv) r += $"비닐하우스 Lv{crop.requiredGreenhouseLevel} 필요 ";
                if (crop.requiredPlayerLevel > plyrLv) r += $"플레이어 Lv{crop.requiredPlayerLevel} 필요";
                lockReasonText.text = "🔒 " + r;
            }
        }

        if (buyButton) buyButton.interactable = unlocked;
        if (buyX5Button) buyX5Button.interactable = unlocked;
        if (buyX10Button) buyX10Button.interactable = unlocked;

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
        if (supplyCostText) supplyCostText.text = water.costGem > 0 ? $"💎 {water.costGem}" : $"💰 {water.costGold}";

        int plyrLv = PlayerStats.Instance?.level ?? 1;
        bool unlocked = water.requiredPlayerLevel <= plyrLv;
        int owned = FarmInventoryUI.Instance?.GetWaterCount(water.waterID) ?? 0;

        if (supplyOwnedText) supplyOwnedText.text = $"보유 {owned}개";
        if (supplyLockText)
        {
            supplyLockText.gameObject.SetActive(!unlocked);
            if (!unlocked) supplyLockText.text = $"🔒 플레이어 Lv.{water.requiredPlayerLevel} 필요";
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
        if (supplyCostText) supplyCostText.text = fert.costGem > 0 ? $"💎 {fert.costGem}" : $"💰 {fert.costGold}";

        int plyrLv = PlayerStats.Instance?.level ?? 1;
        bool unlocked = fert.requiredPlayerLevel <= plyrLv;
        int owned = FarmInventoryUI.Instance?.GetFertCount(fert.fertilizerID) ?? 0;

        if (supplyOwnedText) supplyOwnedText.text = $"보유 {owned}개";
        if (supplyLockText)
        {
            supplyLockText.gameObject.SetActive(!unlocked);
            if (!unlocked) supplyLockText.text = $"🔒 플레이어 Lv.{fert.requiredPlayerLevel} 필요";
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
        bool ok = FarmManager.Instance?.BuySeed(crop, amount) ?? false;
        if (!ok) return;

        FarmInventoryUI.Instance?.AddSeed(crop.cropID, amount);
        int owned = FarmInventoryUI.Instance?.GetSeedCount(crop.cropID) ?? 0;
        if (ownedCountText) ownedCountText.text = $"보유 {owned}개";
        RefreshOwnedCounts();
        SoundManager.Instance?.PlayPurchaseSound();
    }

    private void TryBuySupply(int amount)
    {
        if (selectedWater != null) TryBuyWater(selectedWater, amount);
        else if (selectedFert != null) TryBuyFert(selectedFert, amount);
    }

    private void TryBuyWater(WaterData water, int amount)
    {
        bool paid = water.costGem > 0
            ? GameManager.Instance?.SpendGem(water.costGem * amount) ?? false
            : GameManager.Instance?.SpendGold(water.costGold * amount) ?? false;

        if (!paid) { UIManager.Instance?.ShowMessage("재화 부족!", Color.red); return; }

        FarmInventoryUI.Instance?.AddWater(water.waterID, amount);
        int owned = FarmInventoryUI.Instance?.GetWaterCount(water.waterID) ?? 0;
        if (supplyOwnedText) supplyOwnedText.text = $"보유 {owned}개";
        RefreshOwnedCounts();
        UIManager.Instance?.ShowMessage($"💧 {water.waterName} x{amount} 구매!", Color.cyan);
        SoundManager.Instance?.PlayPurchaseSound();
    }

    private void TryBuyFert(FertilizerData fert, int amount)
    {
        bool paid = fert.costGem > 0
            ? GameManager.Instance?.SpendGem(fert.costGem * amount) ?? false
            : GameManager.Instance?.SpendGold(fert.costGold * amount) ?? false;

        if (!paid) { UIManager.Instance?.ShowMessage("재화 부족!", Color.red); return; }

        FarmInventoryUI.Instance?.AddFertilizer(fert.fertilizerID, amount);
        int owned = FarmInventoryUI.Instance?.GetFertCount(fert.fertilizerID) ?? 0;
        if (supplyOwnedText) supplyOwnedText.text = $"보유 {owned}개";
        RefreshOwnedCounts();
        UIManager.Instance?.ShowMessage($"🌿 {fert.fertilizerName} x{amount} 구매!", Color.green);
        SoundManager.Instance?.PlayPurchaseSound();
    }

    // ═══ 유틸 ════════════════════════════════════════════════════
    private void HideAllDetailPanels()
    {
        detailPanel?.SetActive(false);
        supplyDetailPanel?.SetActive(false);
    }

    private void ClearSlots(List<GameObject> list, Transform container)
    {
        foreach (var go in list) if (go != null) Destroy(go);
        list.Clear();
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

    private string FormatTime(float s)
    {
        int h = (int)(s / 3600), m = (int)((s % 3600) / 60), ss = (int)(s % 60);
        if (h > 0) return $"{h}시간 {m}분";
        if (m > 0) return $"{m}분 {ss}초";
        return $"{ss}초";
    }
}