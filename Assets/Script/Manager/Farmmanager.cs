using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// FarmManager — 농장 경영 시스템
///
/// 기능:
///   - 다수 농장 칸(플롯) 관리 (잠금 해제 가능)
///   - 씨앗 심기 / 물주기 / 비료주기 / 수확 / 채집
///   - 실제 시간(DateTime) 기반 성장 → 오프라인도 시간 흐름
///   - 작물 포인트 시스템 → 강화 재료에 사용
///   - 퀘스트 연동 (FarmQuestManager)
///   - SaveLoadManager를 통한 저장/로드
/// </summary>
[DefaultExecutionOrder(-50)]
public class FarmManager : MonoBehaviour
{
    public static FarmManager Instance { get; private set; }

    // ════════════════════════════════════════════════
    //  Inspector 설정
    // ════════════════════════════════════════════════

    [Header("농장 기본 설정")]
    [Tooltip("총 농장 칸 수")]
    public int totalPlots = 9;
    [Tooltip("게임 시작 시 기본 열린 칸")]
    public int startingUnlockedPlots = 3;

    [Header("칸 잠금 해제 비용")]
    public FarmPlotUnlockData[] plotUnlockCosts;

    [Header("등록된 작물 목록")]
    public List<CropData> allCrops = new List<CropData>();

    [Header("비료 목록")]
    public List<FertilizerData> allFertilizers = new List<FertilizerData>();

    [Header("채집 포인트 목록 (씬에 배치된 FarmGatherPoint)")]
    // 자동 탐색 - Inspector 설정 불필요

    [Header("물주기 기본 비용")]
    public int defaultWaterCostGem = 1;

    [Header("작물 포인트")]
    public int cropPoints = 0;
    public static event Action<int> OnCropPointsChanged;

    [Header("UI 이벤트")]
    public UnityEvent<int> OnPlotStateChanged;
    public UnityEvent<int, List<CropHarvestReward>> OnHarvestComplete;

    // ════════════════════════════════════════════════
    //  런타임 상태
    // ════════════════════════════════════════════════

    private List<FarmPlotState> plots = new List<FarmPlotState>();
    private int totalHarvestCount = 0;

    private float uiRefreshTimer = 0f;
    private const float UI_REFRESH_INTERVAL = 1f;

    // ════════════════════════════════════════════════
    //  Unity 생명주기
    // ════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

        }
        else
        {
            // 씬 재진입 시 Inspector 데이터 갱신
            if (allCrops != null && allCrops.Count > 0)
                Instance.allCrops = new List<CropData>(allCrops);
            if (allFertilizers != null && allFertilizers.Count > 0)
                Instance.allFertilizers = new List<FertilizerData>(allFertilizers);
            if (plotUnlockCosts != null && plotUnlockCosts.Length > 0)
                Instance.plotUnlockCosts = plotUnlockCosts;
            enabled = false;
            Destroy(gameObject);
            return;
        }

        InitializePlots();
    }

    void Update()
    {
        if (Instance != this) return;
        uiRefreshTimer += Time.deltaTime;
        if (uiRefreshTimer >= UI_REFRESH_INTERVAL)
        {
            uiRefreshTimer = 0f;
            CheckAndNotifyReadyPlots();
        }
    }

    // ════════════════════════════════════════════════
    //  초기화
    // ════════════════════════════════════════════════

    private void InitializePlots()
    {
        plots.Clear();
        for (int i = 0; i < totalPlots; i++)
        {
            plots.Add(new FarmPlotState
            {
                plotIndex = i,
                isUnlocked = (i < startingUnlockedPlots),
                currentCrop = null,
                isWatered = false,
                isFertilized = false,
                isHarvested = false
            });
        }
        Debug.Log($"[FarmManager] 초기화: {totalPlots}칸 ({startingUnlockedPlots}칸 해제)");
    }

    // ════════════════════════════════════════════════
    //  씨앗 구매
    // ════════════════════════════════════════════════

    public bool BuySeed(CropData cropData, int amount = 1)
    {
        if (cropData == null) return false;

        int playerLevel = PlayerStats.Instance != null ? PlayerStats.Instance.level : 1;
        if (playerLevel < cropData.requiredPlayerLevel)
        {
            UIManager.Instance?.ShowMessage($"레벨 {cropData.requiredPlayerLevel} 필요!", Color.red);
            return false;
        }

        int totalGoldCost = cropData.seedCostGold * amount;
        int totalGemCost = cropData.seedCostGem * amount;

        if (totalGemCost > 0)
        {
            if (!GameManager.Instance.SpendGem(totalGemCost)) return false;
        }
        else
        {
            if (!GameManager.Instance.SpendGold(totalGoldCost)) return false;
        }

        Debug.Log($"[FarmManager] 씨앗 구매: {cropData.cropName} x{amount}");
        UIManager.Instance?.ShowMessage($"{cropData.cropName} 씨앗 x{amount} 구매!", Color.green);
        return true;
    }

    // ════════════════════════════════════════════════
    //  씨앗 심기 (Plant)
    // ════════════════════════════════════════════════

    public bool PlantCrop(int plotIndex, CropData cropData)
    {
        if (!ValidatePlot(plotIndex, out FarmPlotState plot)) return false;
        if (cropData == null) return false;

        if (plot.currentCrop != null && !plot.isHarvested)
        {
            UIManager.Instance?.ShowMessage("이미 작물이 심어져 있습니다!", Color.yellow);
            return false;
        }

        int playerLevel = PlayerStats.Instance != null ? PlayerStats.Instance.level : 1;
        if (playerLevel < cropData.requiredPlayerLevel)
        {
            UIManager.Instance?.ShowMessage($"레벨 {cropData.requiredPlayerLevel} 필요!", Color.red);
            return false;
        }

        // 씨앗 비용 차감
        if (cropData.seedCostGem > 0)
        {
            if (!GameManager.Instance.SpendGem(cropData.seedCostGem)) return false;
        }
        else if (cropData.seedCostGold > 0)
        {
            if (!GameManager.Instance.SpendGold(cropData.seedCostGold)) return false;
        }

        plot.currentCrop = cropData;
        plot.plantTime = DateTime.Now;
        plot.isWatered = false;
        plot.isFertilized = false;
        plot.isHarvested = false;
        plot.fertilizerID = -1;

        OnPlotStateChanged?.Invoke(plotIndex);
        UIManager.Instance?.ShowMessage($"{cropData.cropName} 심기 완료!", Color.green);
        Debug.Log($"[FarmManager] 플롯{plotIndex} ← {cropData.cropName} 심기");
        return true;
    }

    // ════════════════════════════════════════════════
    //  물주기 (Water)
    // ════════════════════════════════════════════════

    public bool WaterCrop(int plotIndex)
    {
        if (!ValidatePlot(plotIndex, out FarmPlotState plot)) return false;

        if (plot.currentCrop == null || plot.isHarvested)
        {
            UIManager.Instance?.ShowMessage("심어진 작물이 없습니다!", Color.yellow);
            return false;
        }
        if (plot.isWatered)
        {
            UIManager.Instance?.ShowMessage("이미 물을 줬습니다!", Color.yellow);
            return false;
        }
        if (plot.IsReadyToHarvest())
        {
            UIManager.Instance?.ShowMessage("이미 수확 가능합니다!", Color.yellow);
            return false;
        }

        int gemCost = plot.currentCrop.waterCostGem > 0
            ? plot.currentCrop.waterCostGem
            : defaultWaterCostGem;

        if (gemCost > 0 && !GameManager.Instance.SpendGem(gemCost)) return false;

        plot.isWatered = true;
        OnPlotStateChanged?.Invoke(plotIndex);

        float bonus = plot.currentCrop.waterSpeedBonus * 100f;
        UIManager.Instance?.ShowMessage($"물주기 완료! 성장 +{bonus:F0}% 가속!", Color.cyan);
        Debug.Log($"[FarmManager] 플롯{plotIndex} 물주기 (가속 +{bonus:F0}%)");
        return true;
    }

    // ════════════════════════════════════════════════
    //  비료주기 (Fertilize) ★ 신규
    // ════════════════════════════════════════════════

    /// <summary>
    /// 비료주기: 수확량 증가 + 추가 성장 가속
    /// 물주기와 동시 적용 가능, 각각 독립 효과
    /// </summary>
    public bool FertilizeCrop(int plotIndex, FertilizerData fertilizer)
    {
        if (!ValidatePlot(plotIndex, out FarmPlotState plot)) return false;

        if (fertilizer == null)
        {
            UIManager.Instance?.ShowMessage("비료를 선택하세요!", Color.yellow);
            return false;
        }
        if (plot.currentCrop == null || plot.isHarvested)
        {
            UIManager.Instance?.ShowMessage("심어진 작물이 없습니다!", Color.yellow);
            return false;
        }
        if (plot.isFertilized)
        {
            UIManager.Instance?.ShowMessage("이미 비료를 줬습니다!", Color.yellow);
            return false;
        }
        if (plot.IsReadyToHarvest())
        {
            UIManager.Instance?.ShowMessage("이미 수확 가능합니다!", Color.yellow);
            return false;
        }

        // 비료 비용 차감 (골드 또는 아이템)
        if (fertilizer.costGold > 0)
        {
            if (!GameManager.Instance.SpendGold(fertilizer.costGold)) return false;
        }
        if (fertilizer.costGem > 0)
        {
            if (!GameManager.Instance.SpendGem(fertilizer.costGem)) return false;
        }

        plot.isFertilized = true;
        plot.fertilizerID = fertilizer.fertilizerID;
        plot.currentFertilizer = fertilizer;

        OnPlotStateChanged?.Invoke(plotIndex);

        UIManager.Instance?.ShowMessage(
            $"비료 적용! 수확량 +{fertilizer.yieldBonus * 100f:F0}%", Color.green);
        Debug.Log($"[FarmManager] 플롯{plotIndex} 비료({fertilizer.fertilizerName}) 적용");
        return true;
    }

    // ════════════════════════════════════════════════
    //  즉시 완성 (보석)
    // ════════════════════════════════════════════════

    public bool InstantFinish(int plotIndex)
    {
        if (!ValidatePlot(plotIndex, out FarmPlotState plot)) return false;

        if (plot.currentCrop == null || plot.isHarvested || plot.IsReadyToHarvest())
        {
            UIManager.Instance?.ShowMessage("즉시 완성 불가 상태입니다!", Color.yellow);
            return false;
        }

        float remainMins = plot.GetRemainingSeconds() / 60f;
        int gemCost = Mathf.Max(1, Mathf.CeilToInt(remainMins));

        if (!GameManager.Instance.SpendGem(gemCost)) return false;

        float totalSeconds = plot.currentCrop.growthTimeSeconds;
        totalSeconds *= plot.GetSpeedMultiplier();
        plot.plantTime = DateTime.Now.AddSeconds(-totalSeconds);

        OnPlotStateChanged?.Invoke(plotIndex);
        UIManager.Instance?.ShowMessage($"즉시 완성! ({gemCost}💎)", Color.yellow);
        return true;
    }

    // ════════════════════════════════════════════════
    //  수확 (Harvest)
    // ════════════════════════════════════════════════

    public bool HarvestCrop(int plotIndex)
    {
        if (!ValidatePlot(plotIndex, out FarmPlotState plot)) return false;

        if (!plot.IsReadyToHarvest())
        {
            float remain = plot.GetRemainingSeconds();
            if (remain > 0)
                UIManager.Instance?.ShowMessage($"아직 {FormatTime(remain)} 남았습니다!", Color.yellow);
            else
                UIManager.Instance?.ShowMessage("수확할 작물이 없습니다!", Color.yellow);
            return false;
        }

        CropData crop = plot.currentCrop;
        float yieldMultiplier = 1f;

        // 비료 수확량 보너스
        if (plot.isFertilized && plot.currentFertilizer != null)
            yieldMultiplier += plot.currentFertilizer.yieldBonus;

        List<CropHarvestReward> rewards = new List<CropHarvestReward>();

        if (crop.harvestRewards != null)
        {
            foreach (var reward in crop.harvestRewards)
            {
                rewards.Add(reward);

                // ★ 진단 로그 — reward 상태 전체 출력
                Debug.Log($"[FarmManager] 수확 reward 확인: item={reward.item?.itemName ?? "NULL"} " +
                          $"min={reward.minAmount} max={reward.maxAmount} " +
                          $"gold={reward.goldReward} gem={reward.gemReward}");

                if (reward.goldReward > 0)
                    GameManager.Instance?.AddGold(Mathf.RoundToInt(reward.goldReward * yieldMultiplier));

                if (reward.gemReward > 0)
                    GameManager.Instance?.AddGem(reward.gemReward);

                if (reward.item != null)
                {
                    // ★ int Random.Range는 max exclusive → minAmount=maxAmount=1이면 Range(1,2)=1 정상
                    int amount = Mathf.RoundToInt(
                        UnityEngine.Random.Range(reward.minAmount, reward.maxAmount + 1) * yieldMultiplier
                    );
                    Debug.Log($"[FarmManager] amount 계산: Range({reward.minAmount},{reward.maxAmount + 1}) * {yieldMultiplier} = {amount}");

                    if (amount <= 0)
                    {
                        Debug.LogWarning($"[FarmManager] amount가 0이하! minAmount={reward.minAmount} maxAmount={reward.maxAmount} — CropData SO에서 수량 확인 필요");
                        amount = 1; // ★ 최소 1개 보장
                    }

                    if (FarmInventoryUI.Instance != null)
                    {
                        FarmInventoryUI.Instance.AddHarvest(crop.cropID, amount);
                        FarmInventoryUI.Instance.SwitchToHarvestedTab();
                    }
                    else
                    {
                        Debug.LogWarning("[FarmManager] FarmInventoryUI.Instance가 null — 씬에 FarmInventoryUI가 있는지 확인!");
                    }
                }
                else
                {
                    Debug.LogWarning($"[FarmManager] {crop.cropName} reward.item이 null! CropData SO → Harvest Rewards → Item 슬롯을 연결하세요.");
                }
            }
        }

        // 경험치
        int expReward = Mathf.Max(1, Mathf.RoundToInt(crop.growthTimeSeconds * 0.1f));
        GameManager.Instance?.AddExp(expReward);

        // ★ 작물 포인트 지급 (수확 시)
        int pointsEarned = Mathf.Max(1, crop.cropPointReward);
        if (plot.isFertilized) pointsEarned = Mathf.RoundToInt(pointsEarned * yieldMultiplier);
        AddCropPoints(pointsEarned);

        // 퀘스트 진행 알림
        FarmQuestManager.Instance?.OnCropHarvested(crop.cropID, 1);

        // 업적
        totalHarvestCount++;
        AchievementSystem.Instance?.UpdateAchievementProgress(AchievementType.CraftItems, "Farm", 1);

        // 플롯 초기화
        plot.currentCrop = null;
        plot.isWatered = false;
        plot.isFertilized = false;
        plot.isHarvested = false;
        plot.fertilizerID = -1;
        plot.currentFertilizer = null;
        plot.plantTime = default;

        OnPlotStateChanged?.Invoke(plotIndex);
        OnHarvestComplete?.Invoke(plotIndex, rewards);

        UIManager.Instance?.ShowMessage(
            $"{crop.cropName} 수확! EXP+{expReward} 작물포인트+{pointsEarned}", Color.green);
        Debug.Log($"[FarmManager] 플롯{plotIndex} 수확 완료 ({crop.cropName}, 포인트+{pointsEarned})");
        return true;
    }

    public int HarvestAll()
    {
        int count = 0;
        for (int i = 0; i < plots.Count; i++)
            if (plots[i].IsReadyToHarvest())
                if (HarvestCrop(i)) count++;

        if (count > 0)
            UIManager.Instance?.ShowMessage($"전체 수확: {count}칸!", Color.green);
        return count;
    }

    // ════════════════════════════════════════════════
    //  채집 (Gather) ★ 신규
    // ════════════════════════════════════════════════

    /// <summary>
    /// 씬에 배치된 채집 포인트에서 자원 획득
    /// FarmGatherPoint 컴포넌트에서 호출
    /// </summary>
    public void OnGatherComplete(FarmGatherResult result)
    {
        if (result == null) return;

        // 아이템 추가
        if (result.item != null && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(result.item, result.amount);
        }

        // 작물 포인트 추가
        if (result.cropPoints > 0)
            AddCropPoints(result.cropPoints);

        // 퀘스트 연동
        if (result.cropIDForQuest >= 0)
            FarmQuestManager.Instance?.OnCropHarvested(result.cropIDForQuest, result.amount);

        UIManager.Instance?.ShowMessage(
            $"채집! {result.item?.itemName ?? "자원"} x{result.amount}" +
            (result.cropPoints > 0 ? $" | 작물포인트+{result.cropPoints}" : ""), Color.green);

        Debug.Log($"[FarmManager] 채집 완료: {result.item?.itemName} x{result.amount}");
    }

    // ════════════════════════════════════════════════
    //  작물 포인트 (Crop Points) ★ 신규
    // ════════════════════════════════════════════════

    public void AddCropPoints(int amount)
    {
        if (amount <= 0) return;
        cropPoints += amount;
        OnCropPointsChanged?.Invoke(cropPoints);
        Debug.Log($"[FarmManager] 작물포인트 +{amount} (총 {cropPoints})");
    }

    public bool SpendCropPoints(int amount)
    {
        if (amount <= 0 || cropPoints < amount)
        {
            UIManager.Instance?.ShowMessage($"작물 포인트가 부족합니다! (필요:{amount} / 보유:{cropPoints})", Color.red);
            return false;
        }
        cropPoints = Mathf.Max(0, cropPoints - amount);
        OnCropPointsChanged?.Invoke(cropPoints);
        Debug.Log($"[FarmManager] CropPoint -{amount} → {cropPoints}");
        return true;
    }

    public int GetCropPoints() => cropPoints;

    // ════════════════════════════════════════════════
    //  칸 잠금 해제
    // ════════════════════════════════════════════════

    public bool UnlockPlot(int plotIndex)
    {
        if (!IsValidIndex(plotIndex)) return false;

        FarmPlotState plot = plots[plotIndex];
        if (plot.isUnlocked)
        {
            UIManager.Instance?.ShowMessage("이미 해제된 칸입니다!", Color.yellow);
            return false;
        }

        int cost = GetUnlockCost(plotIndex);
        int requiredLv = GetUnlockRequiredLevel(plotIndex);
        int playerLevel = PlayerStats.Instance != null ? PlayerStats.Instance.level : 1;

        if (playerLevel < requiredLv)
        {
            UIManager.Instance?.ShowMessage($"레벨 {requiredLv} 필요!", Color.red);
            return false;
        }

        if (!GameManager.Instance.SpendGold(cost)) return false;

        plot.isUnlocked = true;
        OnPlotStateChanged?.Invoke(plotIndex);
        UIManager.Instance?.ShowMessage($"농장 칸 {plotIndex + 1} 해제!", Color.green);
        return true;
    }

    public int GetUnlockCost(int plotIndex)
    {
        if (plotUnlockCosts != null)
            foreach (var d in plotUnlockCosts)
                if (d.plotIndex == plotIndex) return d.unlockCostGold;
        return 500 + plotIndex * 200;
    }

    public int GetUnlockRequiredLevel(int plotIndex)
    {
        if (plotUnlockCosts != null)
            foreach (var d in plotUnlockCosts)
                if (d.plotIndex == plotIndex) return d.requiredPlayerLevel;
        return 1 + plotIndex * 2;
    }

    // ════════════════════════════════════════════════
    //  조회 API
    // ════════════════════════════════════════════════

    public FarmPlotState GetPlot(int plotIndex)
        => IsValidIndex(plotIndex) ? plots[plotIndex] : null;

    public List<FarmPlotState> GetAllPlots() => plots;

    public int GetReadyToHarvestCount()
    {
        int count = 0;
        foreach (var p in plots)
            if (p.IsReadyToHarvest()) count++;
        return count;
    }

    public CropData GetCropByID(int cropID)
        => allCrops.Find(c => c.cropID == cropID);

    public FertilizerData GetFertilizerByID(int id)
        => allFertilizers.Find(f => f.fertilizerID == id);

    // ════════════════════════════════════════════════
    //  내부 유틸
    // ════════════════════════════════════════════════

    private bool IsValidIndex(int index) => index >= 0 && index < plots.Count;

    private bool ValidatePlot(int plotIndex, out FarmPlotState plot)
    {
        plot = null;
        if (!IsValidIndex(plotIndex))
        {
            Debug.LogWarning($"[FarmManager] 잘못된 플롯 인덱스: {plotIndex}");
            return false;
        }
        plot = plots[plotIndex];
        if (!plot.isUnlocked)
        {
            UIManager.Instance?.ShowMessage("잠긴 칸입니다! 잠금을 해제하세요.", Color.red);
            return false;
        }
        return true;
    }

    private void CheckAndNotifyReadyPlots()
    {
        foreach (var plot in plots)
            if (plot.IsReadyToHarvest())
                OnPlotStateChanged?.Invoke(plot.plotIndex);
    }

    private string FormatTime(float seconds)
    {
        int h = (int)(seconds / 3600);
        int m = (int)((seconds % 3600) / 60);
        int s = (int)(seconds % 60);
        if (h > 0) return $"{h}시간 {m}분";
        if (m > 0) return $"{m}분 {s}초";
        return $"{s}초";
    }

    // ════════════════════════════════════════════════
    //  저장 / 로드
    // ════════════════════════════════════════════════

    public FarmSaveData GetFarmSaveData()
    {
        FarmSaveData data = new FarmSaveData();
        data.totalHarvestCount = totalHarvestCount;
        data.cropPoints = cropPoints;
        data.plots = new FarmPlotSaveData[plots.Count];

        for (int i = 0; i < plots.Count; i++)
        {
            FarmPlotState p = plots[i];
            data.plots[i] = new FarmPlotSaveData
            {
                plotIndex = p.plotIndex,
                isUnlocked = p.isUnlocked,
                cropID = p.currentCrop != null ? p.currentCrop.cropID : -1,
                plantTimeISO = p.currentCrop != null ? p.plantTime.ToString("o") : "",
                isWatered = p.isWatered,
                isFertilized = p.isFertilized,
                fertilizerID = p.fertilizerID,
                isHarvested = p.isHarvested
            };
        }

        // ★ 씨앗/수확물 인벤토리 저장
        data.inventoryData = FarmInventoryUI.Instance?.GetSaveData();

        return data;
    }

    public void LoadFarmSaveData(FarmSaveData data)
    {
        if (data == null || data.plots == null) return;

        totalHarvestCount = data.totalHarvestCount;
        cropPoints = data.cropPoints;
        OnCropPointsChanged?.Invoke(cropPoints);

        foreach (var savedPlot in data.plots)
        {
            if (!IsValidIndex(savedPlot.plotIndex)) continue;

            FarmPlotState plot = plots[savedPlot.plotIndex];
            plot.isUnlocked = savedPlot.isUnlocked;
            plot.isHarvested = savedPlot.isHarvested;
            plot.isWatered = savedPlot.isWatered;
            plot.isFertilized = savedPlot.isFertilized;
            plot.fertilizerID = savedPlot.fertilizerID;

            if (savedPlot.fertilizerID >= 0)
                plot.currentFertilizer = GetFertilizerByID(savedPlot.fertilizerID);

            if (savedPlot.cropID >= 0)
            {
                plot.currentCrop = GetCropByID(savedPlot.cropID);
                if (!string.IsNullOrEmpty(savedPlot.plantTimeISO))
                    DateTime.TryParse(savedPlot.plantTimeISO,
                        null, System.Globalization.DateTimeStyles.RoundtripKind,
                        out plot.plantTime);
            }
            else
            {
                plot.currentCrop = null;
                plot.plantTime = default;
            }

            OnPlotStateChanged?.Invoke(savedPlot.plotIndex);
        }

        // ★ 씨앗/수확물 인벤토리 로드
        if (data.inventoryData != null)
            FarmInventoryUI.Instance?.LoadSaveData(data.inventoryData);

        Debug.Log($"[FarmManager] 농장 로드: {data.plots.Length}칸, 포인트:{cropPoints}");
    }
}