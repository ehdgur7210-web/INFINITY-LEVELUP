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

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        closeButton?.onClick.AddListener(ClosePanel);
        tabSeeds?.onClick.AddListener(() => SwitchTab(InvTab.Seeds));
        tabHarvested?.onClick.AddListener(() => SwitchTab(InvTab.Harvested));
        useSeedButton?.onClick.AddListener(EnterPlantMode);
        dropItemButton?.onClick.AddListener(DropSelectedItem);
    }

    void OnEnable()
    {
        FarmManagerExtension.OnCropPointsChanged += RefreshCropPoints;
        FarmManagerExtension.OnHarvestCompleteStatic += OnHarvestComplete;
        RefreshAll();
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
        inventoryPanel?.SetActive(true);
        RefreshAll();
    }

    public void ClosePanel()
    {
        inventoryPanel?.SetActive(false);
        ExitPlantMode();
    }

    /// <summary>
    /// ★ FIX: FarmSceneController.OpenCropProcess에서 호출
    ///         패널을 열고 수확탭으로 바로 전환
    /// </summary>
    public void SwitchToHarvestedTab()
    {
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
        RefreshCropPoints(FarmManager.Instance?.GetCropPoints() ?? 0);
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
        seedContainer?.gameObject.SetActive(tab == InvTab.Seeds);
        harvestContainer?.gameObject.SetActive(tab == InvTab.Harvested);
        RefreshCurrentTab();
        selectedItemDetail?.SetActive(false);
    }

    // ════════════════════════════════════════════════
    //  씨앗 목록
    // ════════════════════════════════════════════════

    private void RefreshSeedList()
    {
        if (seedContainer == null || inventorySlotPrefab == null) return;

        foreach (Transform child in seedContainer)
            Destroy(child.gameObject);

        if (FarmManager.Instance == null) return;

        foreach (var pair in seedCounts)
        {
            if (pair.Value <= 0) continue;

            CropData crop = FarmManager.Instance.GetCropByID(pair.Key);
            if (crop == null) continue;

            GameObject go = Instantiate(inventorySlotPrefab, seedContainer);
            SetupInventorySlot(go, crop, pair.Value, true);
        }

        // 보유 씨앗이 없으면 안내 메시지 (선택)
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

        if (FarmManager.Instance == null) return;

        foreach (var pair in harvestCounts)
        {
            if (pair.Value <= 0) continue;

            CropData crop = FarmManager.Instance.GetCropByID(pair.Key);
            if (crop == null) continue;

            GameObject go = Instantiate(inventorySlotPrefab, harvestContainer);
            SetupInventorySlot(go, crop, pair.Value, false);
        }
    }

    private void SetupInventorySlot(GameObject go, CropData crop, int count, bool isSeed)
    {
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

        // ★ Button 없으면 자동 추가
        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();

        CropData capturedCrop = crop;
        bool capturedIsSeed = isSeed;
        btn.onClick.AddListener(() => SelectItem(capturedCrop, capturedIsSeed));

        // ★ ScrollRect 안에서도 클릭이 동작하도록 EventTrigger 추가
        var trigger = go.GetComponent<EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener((_) => SelectItem(capturedCrop, capturedIsSeed));
        trigger.triggers.Add(entry);
    }

    // ════════════════════════════════════════════════
    //  아이템 선택
    // ════════════════════════════════════════════════

    private void SelectItem(CropData crop, bool isSeed)
    {
        selectedCrop = crop;
        selectedItemDetail?.SetActive(true);

        if (selectedItemIcon) selectedItemIcon.sprite = isSeed ? crop.seedIcon : crop.harvestIcon;
        if (selectedItemName) selectedItemName.text = crop.cropName;

        int count = isSeed ? GetSeedCount(crop.cropID)
                           : (harvestCounts.ContainsKey(crop.cropID) ? harvestCounts[crop.cropID] : 0);
        if (selectedItemCount) selectedItemCount.text = $"보유: {count}개";
        if (selectedItemDesc) selectedItemDesc.text = crop.description;

        if (useSeedButton) useSeedButton.gameObject.SetActive(isSeed);
        if (dropItemButton) dropItemButton.gameObject.SetActive(true);
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
        plantModeGuide?.SetActive(false);
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

        selectedItemDetail?.SetActive(false);
        RefreshCurrentTab();
    }

    // ════════════════════════════════════════════════
    //  이벤트 수신
    // ════════════════════════════════════════════════

    private void RefreshCropPoints(int points)
    {
        if (cropPointsText)
            cropPointsText.text = $"🌱 작물 포인트: {points}";
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

        RefreshCurrentTab();
    }
}