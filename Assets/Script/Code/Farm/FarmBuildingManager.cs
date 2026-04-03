using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FarmBuildingManager — 농장 건물 레벨업 시스템
///
/// 🏠 하우스      : 레벨업 → 텃밭 2칸 + 과일나무 1칸 해금 + 전체 성장속도 증가
/// 🌿 비닐하우스  : 레벨업 → CropData.requiredGreenhouseLevel 작물 해금
/// 💧 물레방아    : 레벨업 → 물주기 성장시간 단축 보너스 증가
/// 💨 풍차        : 레벨업 → 비료 성장시간 단축 보너스 증가
///
/// ※ FarmPlotState.GetSpeedMultiplier()에서 이 매니저를 참조합니다.
///    → FarmBuildingManager.Instance?.GetWaterTimeBonus()
///    → FarmBuildingManager.Instance?.GetFertilizerTimeBonus()
/// </summary>
[DefaultExecutionOrder(-45)]
public class FarmBuildingManager : MonoBehaviour
{
    public static FarmBuildingManager Instance { get; private set; }

    // ════════════════════════════════════════════════
    //  Inspector 설정
    // ════════════════════════════════════════════════

    [Header("최대 레벨")]
    public int maxBuildingLevel = 10;

    [Header("🏠 하우스 레벨별 설정")]
    public List<HouseLevelData> houseLevels = new List<HouseLevelData>();

    [Header("🌿 비닐하우스 레벨별 설정")]
    public List<GreenhouseLevelData> greenhouseLevels = new List<GreenhouseLevelData>();

    [Header("💧 물레방아 레벨별 설정")]
    public List<WatermillLevelData> watermillLevels = new List<WatermillLevelData>();

    [Header("💨 풍차 레벨별 설정")]
    public List<WindmillLevelData> windmillLevels = new List<WindmillLevelData>();

    // ────────────────────────────────────────────────
    //  이벤트
    // ────────────────────────────────────────────────

    public static event Action<BuildingType, int> OnBuildingLevelUp;
    public static event Action<BuildingType, int> OnBuildingLevelChanged;
    public static event Action<int, PlotType> OnNewPlotUnlocked;
    public static event Action<CropData> OnNewCropUnlocked;

    // ════════════════════════════════════════════════
    //  런타임 상태
    // ════════════════════════════════════════════════

    private int houseLevel = 1;
    private int greenhouseLevel = 1;
    private int watermillLevel = 1;
    private int windmillLevel = 1;

    public int HouseLevel => houseLevel;
    public int GreenhouseLevel => greenhouseLevel;
    public int WatermillLevel => watermillLevel;
    public int WindmillLevel => windmillLevel;

    // ════════════════════════════════════════════════
    //  Unity 생명주기
    // ════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] FarmBuildingManager가 생성되었습니다.");
            // ★ 기존: DontDestroyOnLoad(transform.root.gameObject) → FarmCanvas 전체를 DDOL로 만들어
            //   FarmManager 등 씬 로컬 매니저가 씬에서 사라지는 버그 발생
            //   → 자기 자신만 부모에서 분리 후 DDOL로 유지
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            AutoFillDefaultLevels();
        }
        else
        {
            // 씬 재진입 시 Inspector 데이터 갱신
            if (houseLevels.Count > 0) Instance.houseLevels = new List<HouseLevelData>(houseLevels);
            if (greenhouseLevels.Count > 0) Instance.greenhouseLevels = new List<GreenhouseLevelData>(greenhouseLevels);
            if (watermillLevels.Count > 0) Instance.watermillLevels = new List<WatermillLevelData>(watermillLevels);
            if (windmillLevels.Count > 0) Instance.windmillLevels = new List<WindmillLevelData>(windmillLevels);
            Destroy(gameObject);
        }
    }

    // ════════════════════════════════════════════════
    //  기본값 자동 생성 (Inspector가 비어있을 때)
    // ════════════════════════════════════════════════

    private void AutoFillDefaultLevels()
    {
        if (houseLevels.Count == 0)
        {
            for (int i = 1; i <= maxBuildingLevel; i++)
            {
                houseLevels.Add(new HouseLevelData
                {
                    level = i,
                    upgradeCostGold = 500 * i * i,
                    upgradeCostGem = 0,
                    requiredPlayerLevel = i * 2,
                    newVegetablePlotsCount = 2,
                    newFruitTreePlotsCount = 1,
                    growthSpeedBonus = 0.03f * i,
                    description = $"하우스 Lv{i}: 텃밭 2칸 + 과일나무 1칸 해금, 생산속도 +{3 * i}%"
                });
            }
        }
        if (greenhouseLevels.Count == 0)
        {
            for (int i = 1; i <= maxBuildingLevel; i++)
            {
                greenhouseLevels.Add(new GreenhouseLevelData
                {
                    level = i,
                    upgradeCostGold = 600 * i * i,
                    upgradeCostGem = 0,
                    requiredPlayerLevel = i * 2 + 1,
                    unlockedGreenhouseLevel = i,
                    description = $"비닐하우스 Lv{i}: 새 작물 해금"
                });
            }
        }
        if (watermillLevels.Count == 0)
        {
            for (int i = 1; i <= maxBuildingLevel; i++)
            {
                watermillLevels.Add(new WatermillLevelData
                {
                    level = i,
                    upgradeCostGold = 400 * i * i,
                    upgradeCostGem = 0,
                    requiredPlayerLevel = i * 2,
                    waterTimeReductionBonus = 0.05f * i,
                    description = $"물레방아 Lv{i}: 물주기 가속 +{5 * i}%"
                });
            }
        }
        if (windmillLevels.Count == 0)
        {
            for (int i = 1; i <= maxBuildingLevel; i++)
            {
                windmillLevels.Add(new WindmillLevelData
                {
                    level = i,
                    upgradeCostGold = 400 * i * i,
                    upgradeCostGem = 0,
                    requiredPlayerLevel = i * 2,
                    fertilizerTimeReductionBonus = 0.05f * i,
                    description = $"풍차 Lv{i}: 비료 가속 +{5 * i}%"
                });
            }
        }
    }

    // ════════════════════════════════════════════════
    //  건물 레벨업
    // ════════════════════════════════════════════════

    public bool TryUpgradeBuilding(BuildingType type)
    {
        int currentLv = GetCurrentLevel(type);
        if (currentLv >= maxBuildingLevel)
        {
            UIManager.Instance?.ShowMessage("최대 레벨입니다!", Color.yellow);
            return false;
        }

        int nextLv = currentLv + 1;
        int costGold = GetUpgradeCostGold(type, nextLv);
        int costGem = GetUpgradeCostGem(type, nextLv);
        int requiredPL = GetRequiredPlayerLevel(type, nextLv);

        int playerLevel = PlayerStats.Instance != null ? PlayerStats.Instance.level : 1;
        if (playerLevel < requiredPL)
        {
            UIManager.Instance?.ShowMessage($"플레이어 레벨 {requiredPL} 필요!", Color.red);
            return false;
        }

        // ★ FIX: GameManager.Instance null 체크 추가 (씬 전환 중 NullReferenceException 방지)
        if (GameManager.Instance == null)
        {
            Debug.LogError("[FarmBuildingManager] GameManager.Instance가 없습니다!");
            return false;
        }

        if (costGem > 0)
        {
            if (!GameManager.Instance.SpendGem(costGem)) return false;
        }
        else if (costGold > 0)
        {
            if (!GameManager.Instance.SpendGold(costGold)) return false;
        }

        SetLevel(type, nextLv);
        ApplyLevelUpEffects(type, nextLv);

        OnBuildingLevelUp?.Invoke(type, nextLv);
        OnBuildingLevelChanged?.Invoke(type, nextLv);

        UIManager.Instance?.ShowMessage($"{GetBuildingName(type)} Lv{nextLv} 업그레이드!", Color.green);
        Debug.Log($"[FarmBuildingManager] {GetBuildingName(type)} → Lv{nextLv}");

        SaveLoadManager.Instance?.SaveGame();
        return true;
    }

    // ════════════════════════════════════════════════
    //  레벨업 효과 적용
    // ════════════════════════════════════════════════

    private void ApplyLevelUpEffects(BuildingType type, int newLevel)
    {
        switch (type)
        {
            case BuildingType.House: ApplyHouseLevelUp(newLevel); break;
            case BuildingType.Greenhouse: ApplyGreenhouseLevelUp(newLevel); break;
            case BuildingType.Watermill:
                Debug.Log($"[FarmBuildingManager] 물레방아 Lv{newLevel}: 물주기 단축 {GetWaterTimeBonus() * 100f:F0}%");
                break;
            case BuildingType.Windmill:
                Debug.Log($"[FarmBuildingManager] 풍차 Lv{newLevel}: 비료 단축 {GetFertilizerTimeBonus() * 100f:F0}%");
                break;
        }
    }

    private void ApplyHouseLevelUp(int newLevel)
    {
        if (FarmManager.Instance == null) return;
        HouseLevelData data = GetHouseLevelData(newLevel);
        if (data == null) return;

        int unlockedVeg = 0, unlockedFruit = 0;

        foreach (var plot in FarmManager.Instance.GetAllPlots())
        {
            if (plot.isUnlocked) continue;

            // PlotType이 FarmPlotState에 없으면 기본적으로 Vegetable로 처리
            bool isFruit = false;
#if FARM_PLOT_HAS_PLOTTYPE
            isFruit = (plot.plotType == PlotType.FruitTree);
#endif
            if (!isFruit && unlockedVeg < data.newVegetablePlotsCount)
            {
                plot.isUnlocked = true;
                unlockedVeg++;
                OnNewPlotUnlocked?.Invoke(plot.plotIndex, PlotType.Vegetable);
                FarmManager.Instance.OnPlotStateChanged?.Invoke(plot.plotIndex);
            }
            else if (isFruit && unlockedFruit < data.newFruitTreePlotsCount)
            {
                plot.isUnlocked = true;
                unlockedFruit++;
                OnNewPlotUnlocked?.Invoke(plot.plotIndex, PlotType.FruitTree);
                FarmManager.Instance.OnPlotStateChanged?.Invoke(plot.plotIndex);
            }

            if (unlockedVeg >= data.newVegetablePlotsCount && unlockedFruit >= data.newFruitTreePlotsCount)
                break;
        }

        UIManager.Instance?.ShowMessage(
            $"🏠 하우스 Lv{newLevel}: 텃밭 {unlockedVeg}칸 + 과일나무 {unlockedFruit}칸 해금!", Color.green);
    }

    private void ApplyGreenhouseLevelUp(int newLevel)
    {
        if (FarmManager.Instance == null) return;

        var unlocked = new List<string>();
        foreach (var crop in FarmManager.Instance.allCrops)
        {
            if (crop.requiredGreenhouseLevel == newLevel)
            {
                unlocked.Add(crop.cropName);
                OnNewCropUnlocked?.Invoke(crop);
            }
        }

        if (unlocked.Count > 0)
            UIManager.Instance?.ShowMessage($"🌿 새 작물 해금: {string.Join(", ", unlocked)}!", Color.cyan);
    }

    // ════════════════════════════════════════════════
    //  보너스 조회 API
    // ════════════════════════════════════════════════

    /// <summary>물레방아 레벨 → 물주기 성장시간 단축 비율 (FarmPlotState에서 사용)</summary>
    public float GetWaterTimeBonus()
    {
        var d = GetWatermillLevelData(watermillLevel);
        return d != null ? Mathf.Clamp01(d.waterTimeReductionBonus) : 0f;
    }

    /// <summary>풍차 레벨 → 비료 성장시간 단축 비율 (FarmPlotState에서 사용)</summary>
    public float GetFertilizerTimeBonus()
    {
        var d = GetWindmillLevelData(windmillLevel);
        return d != null ? Mathf.Clamp01(d.fertilizerTimeReductionBonus) : 0f;
    }

    /// <summary>하우스 레벨 → 기본 성장속도 보너스 (모든 작물에 적용)</summary>
    public float GetHouseGrowthBonus()
    {
        var d = GetHouseLevelData(houseLevel);
        return d != null ? d.growthSpeedBonus : 0f;
    }

    /// <summary>현재 비닐하우스 레벨 기준 해금된 작물 목록</summary>
    public List<CropData> GetUnlockedCrops()
    {
        if (FarmManager.Instance == null) return new List<CropData>();
        return FarmManager.Instance.allCrops.FindAll(c => c.requiredGreenhouseLevel <= greenhouseLevel);
    }

    // ════════════════════════════════════════════════
    //  업그레이드 가능 여부
    // ════════════════════════════════════════════════

    public bool CanUpgrade(BuildingType type)
    {
        int currentLv = GetCurrentLevel(type);
        if (currentLv >= maxBuildingLevel) return false;

        int nextLv = currentLv + 1;
        int costGold = GetUpgradeCostGold(type, nextLv);
        int costGem = GetUpgradeCostGem(type, nextLv);
        int requiredPL = GetRequiredPlayerLevel(type, nextLv);

        int playerLevel = PlayerStats.Instance != null ? PlayerStats.Instance.level : 1;
        if (playerLevel < requiredPL) return false;

        // ★ FIX: GameManager.Instance null 체크 추가
        if (GameManager.Instance == null) return false;

        if (costGem > 0) return GameManager.Instance.PlayerGem >= costGem;
        return GameManager.Instance.PlayerGold >= costGold;
    }

    public string GetUpgradeDescription(BuildingType type)
    {
        int currentLv = GetCurrentLevel(type);
        if (currentLv >= maxBuildingLevel) return "최대 레벨!";
        int nextLv = currentLv + 1;

        switch (type)
        {
            case BuildingType.House:
                return GetHouseLevelData(nextLv)?.description ?? $"하우스 Lv{nextLv}";
            case BuildingType.Greenhouse:
                return GetGreenhouseLevelData(nextLv)?.description ?? $"비닐하우스 Lv{nextLv}";
            case BuildingType.Watermill:
                var wd = GetWatermillLevelData(nextLv);
                return wd != null ? wd.description : $"물레방아 Lv{nextLv}: 물주기 단축 +{5}%";
            case BuildingType.Windmill:
                var wnd = GetWindmillLevelData(nextLv);
                return wnd != null ? wnd.description : $"풍차 Lv{nextLv}: 비료 단축 +{5}%";
        }
        return "";
    }

    // ════════════════════════════════════════════════
    //  내부 헬퍼
    // ════════════════════════════════════════════════

    public int GetCurrentLevel(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.House: return houseLevel;
            case BuildingType.Greenhouse: return greenhouseLevel;
            case BuildingType.Watermill: return watermillLevel;
            case BuildingType.Windmill: return windmillLevel;
        }
        return 1;
    }

    private void SetLevel(BuildingType type, int level)
    {
        switch (type)
        {
            case BuildingType.House: houseLevel = level; break;
            case BuildingType.Greenhouse: greenhouseLevel = level; break;
            case BuildingType.Watermill: watermillLevel = level; break;
            case BuildingType.Windmill: windmillLevel = level; break;
        }
    }

    private int GetUpgradeCostGold(BuildingType type, int level)
    {
        switch (type)
        {
            case BuildingType.House: return GetHouseLevelData(level)?.upgradeCostGold ?? 500 * level * level;
            case BuildingType.Greenhouse: return GetGreenhouseLevelData(level)?.upgradeCostGold ?? 600 * level * level;
            case BuildingType.Watermill: return GetWatermillLevelData(level)?.upgradeCostGold ?? 400 * level * level;
            case BuildingType.Windmill: return GetWindmillLevelData(level)?.upgradeCostGold ?? 400 * level * level;
        }
        return 9999;
    }

    private int GetUpgradeCostGem(BuildingType type, int level)
    {
        switch (type)
        {
            case BuildingType.House: return GetHouseLevelData(level)?.upgradeCostGem ?? 0;
            case BuildingType.Greenhouse: return GetGreenhouseLevelData(level)?.upgradeCostGem ?? 0;
            case BuildingType.Watermill: return GetWatermillLevelData(level)?.upgradeCostGem ?? 0;
            case BuildingType.Windmill: return GetWindmillLevelData(level)?.upgradeCostGem ?? 0;
        }
        return 0;
    }

    private int GetRequiredPlayerLevel(BuildingType type, int level)
    {
        switch (type)
        {
            case BuildingType.House: return GetHouseLevelData(level)?.requiredPlayerLevel ?? level * 2;
            case BuildingType.Greenhouse: return GetGreenhouseLevelData(level)?.requiredPlayerLevel ?? level * 2 + 1;
            case BuildingType.Watermill: return GetWatermillLevelData(level)?.requiredPlayerLevel ?? level * 2;
            case BuildingType.Windmill: return GetWindmillLevelData(level)?.requiredPlayerLevel ?? level * 2;
        }
        return 1;
    }

    private HouseLevelData GetHouseLevelData(int lv) => houseLevels.Find(d => d.level == lv);
    private GreenhouseLevelData GetGreenhouseLevelData(int lv) => greenhouseLevels.Find(d => d.level == lv);
    private WatermillLevelData GetWatermillLevelData(int lv) => watermillLevels.Find(d => d.level == lv);
    private WindmillLevelData GetWindmillLevelData(int lv) => windmillLevels.Find(d => d.level == lv);

    private string GetBuildingName(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.House: return "🏠 하우스";
            case BuildingType.Greenhouse: return "🌿 비닐하우스";
            case BuildingType.Watermill: return "💧 물레방아";
            case BuildingType.Windmill: return "💨 풍차";
        }
        return "건물";
    }

    // ★ FIX: FarmBuildingUpgradeUI가 실제 Inspector 설정 비용을 읽을 수 있도록 public으로 변경
    /// <summary>다음 레벨 업그레이드 골드 비용 공개 API (UI 표시용)</summary>
    public int GetNextUpgradeCostGold(BuildingType type)
    {
        int nextLv = GetCurrentLevel(type) + 1;
        return GetUpgradeCostGold(type, nextLv);
    }

    /// <summary>다음 레벨 업그레이드 젬 비용 공개 API (UI 표시용)</summary>
    public int GetNextUpgradeCostGem(BuildingType type)
    {
        int nextLv = GetCurrentLevel(type) + 1;
        return GetUpgradeCostGem(type, nextLv);
    }

    // ════════════════════════════════════════════════
    //  저장 / 로드
    // ════════════════════════════════════════════════

    public FarmBuildingSaveData GetSaveData() => new FarmBuildingSaveData
    {
        houseLevel = houseLevel,
        greenhouseLevel = greenhouseLevel,
        watermillLevel = watermillLevel,
        windmillLevel = windmillLevel
    };

    public void LoadSaveData(FarmBuildingSaveData data)
    {
        if (data == null) return;
        houseLevel = Mathf.Max(1, data.houseLevel);
        greenhouseLevel = Mathf.Max(1, data.greenhouseLevel);
        watermillLevel = Mathf.Max(1, data.watermillLevel);
        windmillLevel = Mathf.Max(1, data.windmillLevel);

        OnBuildingLevelChanged?.Invoke(BuildingType.House, houseLevel);
        OnBuildingLevelChanged?.Invoke(BuildingType.Greenhouse, greenhouseLevel);
        OnBuildingLevelChanged?.Invoke(BuildingType.Watermill, watermillLevel);
        OnBuildingLevelChanged?.Invoke(BuildingType.Windmill, windmillLevel);

        Debug.Log($"[FarmBuildingManager] 로드 완료 - 하우스:{houseLevel} 비닐:{greenhouseLevel} 물레방아:{watermillLevel} 풍차:{windmillLevel}");
    }
}