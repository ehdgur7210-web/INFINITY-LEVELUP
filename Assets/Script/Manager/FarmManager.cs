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

    // ★★ cropPoints는 더 이상 FarmManager 인스턴스 필드가 아님.
    //    GameDataBridge.CurrentData.cropPoints가 유일한 source of truth (CropPointService 통해 접근).
    //    호환성을 위해 cropPoints 프로퍼티와 OnCropPointsChanged 이벤트는 유지 — 모두 서비스에 위임.
    public long cropPoints
    {
        get => CropPointService.Value;
        set => CropPointService.Set(value);
    }

    /// <summary>(레거시) FarmManager.OnCropPointsChanged — 내부적으로 CropPointService.OnChanged를 구독해 재발화.</summary>
    public static event Action<long> OnCropPointsChanged
    {
        add { CropPointService.OnChanged += value; }
        remove { CropPointService.OnChanged -= value; }
    }

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
        Debug.Log($"[FarmManager] ★ Awake 진입! this={gameObject.name} / activeInHierarchy:{gameObject.activeInHierarchy} / Instance현재:{(Instance != null ? Instance.gameObject?.name ?? "DESTROYED" : "null")}");

        // ★ cropPoints는 이제 CropPointService(GameDataBridge)가 소유하므로 Awake에서 0으로 리셋할 필요 없음.
        //   FarmManager는 단지 작물 농사 로직만 담당.

        // ★ 파괴된 Instance 정리 (씬 전환 시 OnDestroy 누락 대비)
        if (Instance != null)
        {
            bool isDestroyed = false;
            try { _ = Instance.gameObject.name; } catch { isDestroyed = true; }
            if (isDestroyed || Instance.gameObject == null)
            {
                Debug.Log("[FarmManager] 파괴된 Instance 감지 → null로 초기화");
                Instance = null;
            }
        }

        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] FarmManager가 생성되었습니다.");
            Debug.Log($"[FarmManager] ★ Instance 설정 완료: {gameObject.name} / allCrops:{allCrops?.Count ?? 0}개 / scene:{gameObject.scene.name}");
        }
        else if (Instance != this)
        {
            // ★★ 중복 FarmManager 차단: 기존 Instance의 allCrops가 더 많거나 같으면 새 인스턴스를 파괴
            //    (allCrops:1짜리 더미가 allCrops:8짜리 정상 인스턴스를 밀어내는 사고 방지)
            int newAllCrops = allCrops?.Count ?? 0;
            int oldAllCrops = Instance.allCrops?.Count ?? 0;
            if (oldAllCrops >= newAllCrops && oldAllCrops > 0)
            {
                Debug.LogWarning($"[FarmManager] ⚠ 중복 FarmManager 감지 → 새 인스턴스 파괴 " +
                    $"(기존 allCrops:{oldAllCrops} >= 새 allCrops:{newAllCrops})");
                // 중복 GameObject 파괴 (자식 컴포넌트면 enabled만 끔)
                if (gameObject.scene.IsValid() && gameObject != Instance.gameObject)
                {
                    Destroy(gameObject);
                }
                else
                {
                    enabled = false;
                }
                return;
            }

            // 새 인스턴스가 더 정상적인 데이터를 가지면 교체 (드물게 정상 케이스)
            FarmManager oldInstance = Instance;
            Debug.LogWarning($"[FarmManager] Instance 교체 (새 allCrops:{newAllCrops} > 기존 allCrops:{oldAllCrops}): {oldInstance.gameObject.name} → {gameObject.name}");
            if (oldInstance != null) oldInstance.enabled = false;
            Instance = this;
        }

        InitializePlots();
        RegisterHarvestItemsInDatabase();
    }

    /// <summary>
    /// 수확 보상 아이템을 ItemDatabase에 등록.
    /// Farm ItemData SO가 Resources/Items 외부(Data/Farm)에 있어서
    /// ItemDatabase 자동 로드에 포함되지 않으므로 런타임 등록 필요.
    /// </summary>
    private void RegisterHarvestItemsInDatabase()
    {
        if (allCrops == null) return;

        // ItemDatabase가 아직 초기화 안 됐으면 Start에서 재시도
        if (ItemDatabase.Instance == null || !ItemDatabase.Instance.IsReady)
        {
            StartCoroutine(RetryRegisterHarvestItems());
            return;
        }

        int registered = 0;
        foreach (var crop in allCrops)
        {
            if (crop?.harvestRewards == null) continue;
            foreach (var reward in crop.harvestRewards)
            {
                if (reward?.item == null) continue;
                ItemDatabase.Instance.RegisterItem(reward.item);
                registered++;
            }
        }
        if (registered > 0)
            Debug.Log($"[FarmManager] 수확 아이템 {registered}개 ItemDatabase에 등록 완료");
    }

    private IEnumerator RetryRegisterHarvestItems()
    {
        float timeout = 3f;
        while ((ItemDatabase.Instance == null || !ItemDatabase.Instance.IsReady) && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        if (ItemDatabase.Instance != null && ItemDatabase.Instance.IsReady)
            RegisterHarvestItemsInDatabase();
        else
            Debug.LogWarning("[FarmManager] ItemDatabase 초기화 대기 타임아웃 — 수확 아이템 미등록");
    }

    void OnDestroy()
    {
        // ★ cropPoints는 이제 CropPointService가 소유하므로 sync/저장 필요 없음.
        //   (어차피 GameDataBridge에 항상 최신 값 — 씬 전환 시 매니저 필드 sync race 자체가 사라짐)
        Debug.Log($"[FarmManager] ★ OnDestroy: cp={CropPointService.Value} / isInstance:{Instance == this} / scene:{gameObject.scene.name}");
        if (Instance == this)
        {
            Instance = null;
        }
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

        // 재화 차감 (GameManager 없으면 GameDataBridge 직접 차감)
        if (totalGemCost > 0)
        {
            if (!SpendGemSafe(totalGemCost)) return false;
        }
        else if (totalGoldCost > 0)
        {
            if (!SpendGoldSafe(totalGoldCost)) return false;
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
        if (!ValidatePlot(plotIndex, out FarmPlotState plot))
            return false;
        if (cropData == null)
            return false;

        if (plot.currentCrop != null && !plot.isHarvested)
        {
            UIManager.Instance?.ShowMessage("이미 작물이 심어져 있습니다!", Color.yellow);
            return false;
        }

        // ★ 레벨 조회: PlayerStats → GameManager → GameDataBridge 폴백
        //   GameDataBridge.playerLevel이 0일 수 있으므로 최소 1 보장
        int playerLevel = 1;
        if (PlayerStats.Instance != null)
            playerLevel = PlayerStats.Instance.level;
        else if (GameManager.Instance != null)
            playerLevel = GameManager.Instance.PlayerLevel;
        else if (GameDataBridge.CurrentData != null)
            playerLevel = GameDataBridge.CurrentData.playerLevel;
        if (playerLevel <= 0) playerLevel = 1; // ★ 최소 레벨 1 보장

        if (playerLevel < cropData.requiredPlayerLevel)
        {
            UIManager.Instance?.ShowMessage($"레벨 {cropData.requiredPlayerLevel} 필요!", Color.red);
            return false;
        }

        // ★ 씨앗 차감: 인벤토리 보유분 소모 (씨앗 없으면 심기 불가)
        FarmInventoryUI farmInvUI = FarmInventoryUI.Instance;
        if (farmInvUI == null)
            farmInvUI = FindObjectOfType<FarmInventoryUI>(true);

        bool paid = farmInvUI != null && farmInvUI.ConsumeSeed(cropData.cropID);

        if (!paid)
        {
            // ★ 씨앗이 없으면 심기 불가 — 상점에서 먼저 구매해야 함
            UIManager.Instance?.ShowMessage("씨앗이 없습니다! 상점에서 구매하세요.", Color.red);
            Debug.Log($"[FarmManager] 씨앗 부족: {cropData.cropName} (cropID:{cropData.cropID})");
            return false;
        }

        plot.currentCrop = cropData;
        plot.plantTime = DateTime.Now;
        plot.isWatered = false;
        plot.isFertilized = false;
        plot.isHarvested = false;
        plot.fertilizerID = -1;

        OnPlotStateChanged?.Invoke(plotIndex);

        // ★ 심기 후 자동 저장
        SaveLoadManager.Instance?.SaveGame();

        UIManager.Instance?.ShowMessage($"{cropData.cropName} 심기 완료!", Color.green);
        Debug.Log($"[FarmManager] 플롯{plotIndex} ← {cropData.cropName} 심기 (씨앗 차감 완료)");
        TutorialManager.Instance?.OnActionCompleted("PlantSeed");
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

        if (gemCost > 0 && !SpendGemSafe(gemCost)) return false;

        plot.isWatered = true;
        OnPlotStateChanged?.Invoke(plotIndex);

        float bonus = plot.currentCrop.waterSpeedBonus * 100f;
        UIManager.Instance?.ShowMessage($"물주기 완료! 성장 +{bonus:F0}% 가속!", Color.cyan);
        Debug.Log($"[FarmManager] 플롯{plotIndex} 물주기 (가속 +{bonus:F0}%)");
        TutorialManager.Instance?.OnActionCompleted("WaterCrop");
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
            if (!SpendGoldSafe(fertilizer.costGold)) return false;
        }
        if (fertilizer.costGem > 0)
        {
            if (!SpendGemSafe(fertilizer.costGem)) return false;
        }

        plot.isFertilized = true;
        plot.fertilizerID = fertilizer.fertilizerID;
        plot.currentFertilizer = fertilizer;

        OnPlotStateChanged?.Invoke(plotIndex);

        UIManager.Instance?.ShowMessage(
            $"비료 적용! 수확량 +{fertilizer.yieldBonus * 100f:F0}%", Color.green);
        Debug.Log($"[FarmManager] 플롯{plotIndex} 비료({fertilizer.fertilizerName}) 적용");
        TutorialManager.Instance?.OnActionCompleted("FertilizeCrop");
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

        if (!SpendGemSafe(gemCost)) return false;

        float totalSeconds = plot.currentCrop.growthTimeSeconds;
        totalSeconds *= plot.GetSpeedMultiplier();
        plot.plantTime = DateTime.Now.AddSeconds(-totalSeconds);

        OnPlotStateChanged?.Invoke(plotIndex);
        UIManager.Instance?.ShowMessage($"즉시 완성! ({gemCost})", Color.yellow);
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
                    AddGoldSafe(Mathf.RoundToInt(reward.goldReward * yieldMultiplier));

                if (reward.gemReward > 0)
                    AddGemSafe(reward.gemReward);

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

                    // ── 농장 인벤토리 (FarmScene UI) ──
                    // ★ Instance가 null이면 비활성 GO에서 탐색해서 일시 활성화 (Awake 트리거 → 데이터 추가)
                    //   단, 자동 오픈 버그 방지를 위해 "원래 비활성이었는지" 기억해두고 끝나면 되돌림.
                    bool wasForcedActive = false;
                    if (FarmInventoryUI.Instance == null)
                    {
                        var found = FindObjectOfType<FarmInventoryUI>(true);
                        if (found != null)
                        {
                            wasForcedActive = !found.gameObject.activeSelf;
                            found.gameObject.SetActive(true);
                            Debug.Log($"[FarmManager] FarmInventoryUI 비활성 상태에서 탐색 성공: {found.gameObject.name} (강제활성:{wasForcedActive})");
                        }
                    }

                    if (FarmInventoryUI.Instance != null)
                    {
                        FarmInventoryUI.Instance.AddHarvest(crop.cropID, amount);

                        // ★ 사용자가 직접 인벤을 열어둔 상태일 때만 수확탭으로 자동 전환.
                        //   처음 수확이라 우리가 강제로 활성화한 케이스에서는 탭 전환하지 않고 즉시 다시 닫음
                        //   (자동 오픈 버그 방지 — 첫 수확 시 인벤 패널이 갑자기 뜨던 문제).
                        if (wasForcedActive)
                        {
                            FarmInventoryUI.Instance.gameObject.SetActive(false);
                        }
                        else
                        {
                            FarmInventoryUI.Instance.SwitchToHarvestedTab();
                        }
                        Debug.Log($"[FarmManager] FarmInventoryUI.AddHarvest: cropID={crop.cropID}, amount={amount}");
                    }
                    else
                    {
                        Debug.LogWarning($"[FarmManager] FarmInventoryUI.Instance null — 수확물 '{crop.cropName}' x{amount} UI에 추가 실패");
                    }

                    // ── 메인 인벤토리 기타 탭 연동 ──
                    if (InventoryManager.Instance != null)
                    {
                        InventoryManager.Instance.AddItem(reward.item, amount);
                        Debug.Log($"[Farm→Inven] 수확 아이템 추가: {reward.item.itemName} x{amount}");
                    }
                    else
                    {
                        // FarmScene에서는 InventoryManager가 없으므로
                        // GameDataBridge.CurrentData.inventoryItems에 직접 추가
                        AddItemToSaveData(reward.item, amount);
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
        AddExpSafe(expReward);

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

        // ★ 수확 후 자동 저장 (FarmScene에 SaveLoadManager 없을 수 있음)
        SaveLoadManager.Instance?.SaveGame();
        Debug.Log("[FarmManager] 수확 완료 → SaveGame");

        UIManager.Instance?.ShowMessage(
            $"{crop.cropName} 수확! EXP+{expReward} 작물포인트+{pointsEarned}", Color.green);
        Debug.Log($"[FarmManager] 플롯{plotIndex} 수확 완료 ({crop.cropName}, 포인트+{pointsEarned})");
        TutorialManager.Instance?.OnActionCompleted("Harvest");
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
    //  SaveData 직접 아이템 추가 (FarmScene용)
    // ════════════════════════════════════════════════

    /// <summary>
    /// InventoryManager가 없는 씬(FarmScene)에서
    /// GameDataBridge.CurrentData.inventoryItems에 직접 아이템 추가.
    /// 메인씬 복귀 시 LoadInventoryData()로 자동 복원됨.
    /// </summary>
    private void AddItemToSaveData(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return;

        SaveData current = GameDataBridge.CurrentData;
        if (current == null) return;

        // 기존 배열을 리스트로 변환
        List<InventoryItemData> list = current.inventoryItems != null
            ? new List<InventoryItemData>(current.inventoryItems)
            : new List<InventoryItemData>();

        // 같은 itemID가 있으면 수량 합산
        bool found = false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].itemID == item.itemID)
            {
                list[i].count += amount;
                found = true;
                Debug.Log($"[Farm→SaveData] 기존 아이템 수량 합산: {item.itemName} +{amount} (총 {list[i].count})");
                break;
            }
        }

        // 없으면 새 엔트리 추가
        if (!found)
        {
            list.Add(new InventoryItemData
            {
                itemID = item.itemID,
                count = amount,
                slotIndex = -1,
                enhanceLevel = 0,
                isUnlocked = true,
                itemLevel = 1
            });
            Debug.Log($"[Farm→SaveData] 신규 아이템 추가: {item.itemName} x{amount}");
        }

        current.inventoryItems = list.ToArray();
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

    /// <summary>(레거시) FarmManager.AddCropPoints — CropPointService에 위임. 즉시 저장.</summary>
    public void AddCropPoints(long amount)
    {
        if (amount <= 0) return;
        CropPointService.Add(amount);
        SaveLoadManager.Instance?.SaveGame();
        Debug.Log($"[FarmManager] 작물포인트 +{amount} (총 {CropPointService.Value})");
    }

    /// <summary>(레거시) FarmManager.SpendCropPoints — CropPointService에 위임. 즉시 저장.</summary>
    public bool SpendCropPoints(long amount)
    {
        if (amount <= 0) return true;
        if (!CropPointService.Spend(amount))
        {
            UIManager.Instance?.ShowMessage($"작물 포인트가 부족합니다! (필요:{amount} / 보유:{CropPointService.Value})", Color.red);
            return false;
        }
        SaveLoadManager.Instance?.SaveGame();
        Debug.Log($"[FarmManager] CropPoint -{amount} → {CropPointService.Value}");
        return true;
    }

    /// <summary>(레거시) FarmManager.GetCropPoints — CropPointService.Value에 위임.</summary>
    public long GetCropPoints() => CropPointService.Value;

    // ════════════════════════════════════════════════
    //  안전한 재화 차감 (FarmScene에서 GameManager 없을 때 대비)
    // ════════════════════════════════════════════════

    /// <summary>
    /// GameManager가 있으면 SpendGold, 없으면 GameDataBridge에서 직접 차감.
    /// </summary>
    private bool SpendGoldSafe(int amount)
    {
        if (amount <= 0) return true;

        if (GameManager.Instance != null)
            return GameManager.Instance.SpendGold(amount);

        SaveData data = GameDataBridge.CurrentData;
        if (data == null) return false;

        if (data.playerGold < amount)
        {
            UIManager.Instance?.ShowMessage("골드가 부족합니다!", Color.red);
            return false;
        }
        data.playerGold -= amount;
        Debug.Log($"[FarmManager] SpendGoldSafe: -{amount} (잔여 {data.playerGold})");
        return true;
    }

    /// <summary>
    /// GameManager가 있으면 SpendGem, 없으면 GameDataBridge에서 직접 차감.
    /// </summary>
    private bool SpendGemSafe(long amount)
    {
        if (amount <= 0) return true;

        if (GameManager.Instance != null)
            return GameManager.Instance.SpendGem(amount);

        SaveData data = GameDataBridge.CurrentData;
        if (data == null) return false;

        if (data.playerGem < amount)
        {
            UIManager.Instance?.ShowMessage("보석이 부족합니다!", Color.red);
            return false;
        }
        data.playerGem -= amount;
        Debug.Log($"[FarmManager] SpendGemSafe: -{amount} (잔여 {data.playerGem})");
        return true;
    }

    /// <summary>
    /// GameManager가 있으면 AddGold, 없으면 GameDataBridge에서 직접 추가.
    /// </summary>
    private void AddGoldSafe(int amount)
    {
        if (amount <= 0) return;
        if (GameManager.Instance != null)
            GameManager.Instance.AddGold(amount);
        else
        {
            SaveData data = GameDataBridge.CurrentData;
            if (data != null) data.playerGold += amount;
        }
    }

    /// <summary>
    /// GameManager가 있으면 AddGem, 없으면 GameDataBridge에서 직접 추가.
    /// </summary>
    private void AddGemSafe(long amount)
    {
        if (amount <= 0) return;
        if (GameManager.Instance != null)
            GameManager.Instance.AddGem(amount);
        else
        {
            SaveData data = GameDataBridge.CurrentData;
            if (data != null) data.playerGem += amount;
        }
    }

    /// <summary>
    /// GameManager가 있으면 AddExp, 없으면 무시 (경험치는 GameDataBridge에 없음).
    /// </summary>
    private void AddExpSafe(int amount)
    {
        if (amount <= 0) return;
        GameManager.Instance?.AddExp(amount);
    }

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

        if (!SpendGoldSafe(cost)) return false;

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
        => allCrops?.Find(c => c != null && c.cropID == cropID);

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
        // ★ cropPoints는 GameDataBridge.CurrentData.cropPoints가 source of truth.
        //   FarmSaveData.cropPoints 필드는 구버전 호환을 위해 같은 값을 같이 기록만 함 (load 시 무시).
        data.cropPoints = CropPointService.Value;
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
        // FarmInventoryUI는 FarmScene 전용 — MainScene에서는 Instance가 null
        // → null이면 기존 저장 데이터를 유지하여 씨앗/수확물 초기화 방지
        if (FarmInventoryUI.Instance != null)
        {
            data.inventoryData = FarmInventoryUI.Instance.GetSaveData();
            int seedCount = 0, harvestCount = 0;
            if (data.inventoryData?.seeds != null)
                foreach (var s in data.inventoryData.seeds) seedCount += s.count;
            if (data.inventoryData?.harvests != null)
                foreach (var h in data.inventoryData.harvests) harvestCount += h.count;
            Debug.Log($"[Farm] 인벤토리 저장: 씨앗 {seedCount}개 / 수확물 {harvestCount}개");
        }
        else
        {
            data.inventoryData = GameDataBridge.CurrentData?.farmData?.inventoryData;
            Debug.Log("[Farm] FarmInventoryUI 없음 (MainScene) → 기존 인벤토리 데이터 유지");
        }

        return data;
    }

    public void LoadFarmSaveData(FarmSaveData data)
    {
        if (data == null || data.plots == null) return;

        totalHarvestCount = data.totalHarvestCount;
        // ★★ 마이그레이션: 예전 save에서는 cropPoints가 farmData.cropPoints에만 있었을 수 있음.
        //    GameDataBridge.CurrentData.cropPoints가 0이고 data.cropPoints > 0이면 채택.
        //    (CropPointService가 Set 시 OnChanged 발화 → ResourceBar UI 자동 갱신)
        if (CropPointService.Value == 0 && data.cropPoints > 0)
        {
            CropPointService.Set(data.cropPoints);
            Debug.Log($"[FarmManager:LoadMigration] farmData.cropPoints({data.cropPoints}) → top-level로 마이그레이션");
        }

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
        {
            if (FarmInventoryUI.Instance != null)
            {
                ApplyInventoryData(data.inventoryData);
            }
            else
            {
                // ★ FarmScene이 아니면 FarmInventoryUI는 존재하지 않음 → 대기 불필요
                string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (currentScene == "FarmScene")
                {
                    // FarmScene인데 FarmInventoryUI가 아직 초기화 안 됨 → 지연 로드
                    Debug.Log("[Farm] FarmScene — FarmInventoryUI 미초기화 → 지연 로드 예약");
                    pendingInventoryData = data.inventoryData;
                    StartCoroutine(WaitAndLoadFarmInventory());
                }
                else
                {
                    // MainScene 등 — FarmInventoryUI 없으므로 데이터만 보존
                    // TransferFarmHarvestToInventory()가 메인 인벤토리로 전달 담당
                    Debug.Log($"[Farm] {currentScene} — FarmInventoryUI 없음, 데이터 보존 (harvests:{data.inventoryData.harvests?.Count ?? 0})");
                }
            }
        }
        else
        {
            Debug.LogWarning("[Farm] inventoryData가 null — 씨앗/수확물 데이터 없음");
        }

        Debug.Log($"[FarmManager] 농장 로드: {data.plots.Length}칸, 포인트:{CropPointService.Value}");
    }

    // ── 지연 로드 (FarmInventoryUI 초기화 대기) ──
    private FarmInventorySaveData pendingInventoryData;

    private IEnumerator WaitAndLoadFarmInventory()
    {
        float timeout = 5f;
        while (FarmInventoryUI.Instance == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (FarmInventoryUI.Instance != null && pendingInventoryData != null)
        {
            ApplyInventoryData(pendingInventoryData);
        }
        else
        {
            Debug.LogWarning("[Farm] FarmInventoryUI 대기 타임아웃 — 씨앗/수확물 로드 실패");
        }
        pendingInventoryData = null;
    }

    private void ApplyInventoryData(FarmInventorySaveData invData)
    {
        FarmInventoryUI.Instance.LoadSaveData(invData);
        int seedCount = 0, harvestCount = 0;
        if (invData.seeds != null)
            foreach (var s in invData.seeds) seedCount += s.count;
        if (invData.harvests != null)
            foreach (var h in invData.harvests) harvestCount += h.count;
        Debug.Log($"[Farm] 씨앗 로드: {seedCount} / 수확물 로드: {harvestCount}");

        // ★ 씨앗 상점 UI가 열려있으면 보유 수량 갱신
        if (FarmCropShopUI.Instance != null)
        {
            FarmCropShopUI.Instance.RefreshAfterDataLoad();
            Debug.Log("[Farm] FarmCropShopUI 보유수 갱신 알림 완료");
        }
    }
}