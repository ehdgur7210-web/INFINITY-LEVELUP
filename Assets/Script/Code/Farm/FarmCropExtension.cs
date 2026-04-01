using System;
using System.Collections.Generic;
using UnityEngine;

// ════════════════════════════════════════════════════════════
//  FarmCropExtension.cs
//
//  기존 CropData 수정 없이 비닐하우스 레벨 / 작물 타입을
//  별도 ScriptableObject로 관리합니다.
//
//  사용법:
//    FarmCropExtension.Instance.GetGreenhouseLevel(cropID)
//    FarmCropExtension.Instance.GetCropCategory(cropID)
// ════════════════════════════════════════════════════════════

/// <summary>
/// 작물 카테고리 (FarmCropShopUI 탭 분류용)
/// </summary>
public enum FarmCropCategory
{
    Vegetable,    // 채소 (기본)
    Fruit,        // 과일나무
    Greenhouse,   // 비닐하우스 전용
    Herb          // 허브
}

// ────────────────────────────────────────────────────────────
//  개별 작물 확장 데이터
// ────────────────────────────────────────────────────────────
[Serializable]
public class CropExtData
{
    [Tooltip("기존 CropData의 cropID와 일치시키세요")]
    public int cropID;

    [Tooltip("이 작물을 해금하려면 필요한 비닐하우스 레벨 (0 = 기본 해금)")]
    public int requiredGreenhouseLevel = 0;

    [Tooltip("작물 카테고리 (상점 탭 분류)")]
    public FarmCropCategory category = FarmCropCategory.Vegetable;

    [Tooltip("물주기 성장 가속 보너스 (0.2 = 20%). 기존 CropData에 waterSpeedBonus가 없으면 이 값 사용)")]
    public float waterSpeedBonus = 0.2f;
}

// ────────────────────────────────────────────────────────────
//  ScriptableObject — 작물 확장 데이터 모음
// ────────────────────────────────────────────────────────────
[CreateAssetMenu(fileName = "FarmCropExtension", menuName = "Farm/Crop Extension Registry")]
public class FarmCropExtensionData : ScriptableObject
{
    [Tooltip("cropID별 확장 데이터 목록")]
    public List<CropExtData> cropExtensions = new List<CropExtData>();

    private Dictionary<int, CropExtData> _lookup;

    private Dictionary<int, CropExtData> Lookup
    {
        get
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<int, CropExtData>();
                foreach (var e in cropExtensions)
                    _lookup[e.cropID] = e;
            }
            return _lookup;
        }
    }

    public int GetGreenhouseLevel(int cropID)
        => Lookup.TryGetValue(cropID, out var d) ? d.requiredGreenhouseLevel : 0;

    public FarmCropCategory GetCategory(int cropID)
        => Lookup.TryGetValue(cropID, out var d) ? d.category : FarmCropCategory.Vegetable;

    public float GetWaterSpeedBonus(int cropID)
        => Lookup.TryGetValue(cropID, out var d) ? d.waterSpeedBonus : 0.2f;

    public CropExtData GetExtData(int cropID)
        => Lookup.TryGetValue(cropID, out var d) ? d : null;
}

// ────────────────────────────────────────────────────────────
//  싱글톤 접근 컴포넌트 (씬에 배치 or ManagerRoot에 추가)
// ────────────────────────────────────────────────────────────
public class FarmCropExtension : MonoBehaviour
{
    public static FarmCropExtension Instance { get; private set; }

    [Header("Inspector에서 FarmCropExtensionData ScriptableObject 연결")]
    public FarmCropExtensionData data;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] FarmCropExtension가 생성되었습니다.");
            DontDestroyOnLoad(transform.root.gameObject);
        }
        else Destroy(gameObject);
    }

    public int GetGreenhouseLevel(int cropID)
        => data != null ? data.GetGreenhouseLevel(cropID) : 0;

    public FarmCropCategory GetCategory(int cropID)
        => data != null ? data.GetCategory(cropID) : FarmCropCategory.Vegetable;

    public float GetWaterSpeedBonus(int cropID)
        => data != null ? data.GetWaterSpeedBonus(cropID) : 0.2f;

    /// <summary>
    /// 기존 FarmPlotState.GetGrowthProgress() 대체 계산
    /// FarmPlotState에 GetGrowthProgress()가 없을 경우 사용
    /// </summary>
    public static float CalcGrowthProgress(FarmPlotState plot)
    {
        if (plot == null || plot.currentCrop == null) return 0f;

        float growthTime = plot.currentCrop.growthTimeSeconds;

        // 물주기 가속
        if (plot.isWatered)
        {
            float waterBonus = 0.2f; // 기본값
            if (Instance != null)
                waterBonus = Instance.GetWaterSpeedBonus(plot.currentCrop.cropID);
            float buildingBonus = FarmBuildingManager.Instance?.GetWaterTimeBonus() ?? 0f;
            growthTime *= Mathf.Max(0.1f, 1f - waterBonus - buildingBonus);
        }

        // 비료 가속
        if (plot.isFertilized)
        {
            float fertBonus = FarmBuildingManager.Instance?.GetFertilizerTimeBonus() ?? 0f;
            growthTime *= Mathf.Max(0.1f, 1f - fertBonus);
        }

        // 하우스 기본 속도 보너스
        float houseBonus = FarmBuildingManager.Instance?.GetHouseGrowthBonus() ?? 0f;
        growthTime *= Mathf.Max(0.1f, 1f - houseBonus);

        growthTime = Mathf.Max(10f, growthTime);

        float elapsed = (float)(DateTime.Now - plot.plantTime).TotalSeconds;
        return Mathf.Clamp01(elapsed / growthTime);
    }

    /// <summary>
    /// 기존 FarmPlotState.GetRemainingSeconds() 대체 계산
    /// </summary>
    public static float CalcRemainingSeconds(FarmPlotState plot)
    {
        if (plot == null || plot.currentCrop == null) return 0f;

        float growthTime = plot.currentCrop.growthTimeSeconds;

        if (plot.isWatered)
        {
            float waterBonus = Instance != null ? Instance.GetWaterSpeedBonus(plot.currentCrop.cropID) : 0.2f;
            float buildingBonus = FarmBuildingManager.Instance?.GetWaterTimeBonus() ?? 0f;
            growthTime *= Mathf.Max(0.1f, 1f - waterBonus - buildingBonus);
        }
        if (plot.isFertilized)
        {
            float fertBonus = FarmBuildingManager.Instance?.GetFertilizerTimeBonus() ?? 0f;
            growthTime *= Mathf.Max(0.1f, 1f - fertBonus);
        }

        float houseBonus = FarmBuildingManager.Instance?.GetHouseGrowthBonus() ?? 0f;
        growthTime *= Mathf.Max(0.1f, 1f - houseBonus);
        growthTime = Mathf.Max(10f, growthTime);

        float elapsed = (float)(DateTime.Now - plot.plantTime).TotalSeconds;
        return Mathf.Max(0f, growthTime - elapsed);
    }
}