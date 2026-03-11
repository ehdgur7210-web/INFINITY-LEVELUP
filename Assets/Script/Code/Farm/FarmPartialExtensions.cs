using System;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// FarmPartialExtensions.cs
//
// ★ CropData partial → CropData.cs 로 완전 이전됨 (삭제)
//
// ★ 이 파일에 남아있는 것:
//   - FarmPlotState partial : GetGrowthProgress(), GetTotalGrowthSeconds()
//     (FarmPlotUI.cs 에서 사용하는 편의 메서드)
// ═══════════════════════════════════════════════════════════════════

public partial class FarmPlotState
{
    /// <summary>
    /// GetProgress()의 별칭 — FarmPlotUI.cs에서 GetGrowthProgress()로 호출하는 경우 대응
    /// </summary>
    public float GetGrowthProgress() => GetProgress();

    /// <summary>
    /// 건물 보너스까지 반영한 총 성장 시간 반환
    /// FarmCropShopUI에서 예상 시간 표시 시 사용
    /// </summary>
    public float GetTotalGrowthSeconds()
    {
        if (currentCrop == null) return 0f;
        float wb = FarmBuildingManager.Instance?.GetWaterTimeBonus() ?? 0f;
        float fb = FarmBuildingManager.Instance?.GetFertilizerTimeBonus() ?? 0f;
        return currentCrop.GetModifiedGrowthTime(isWatered, isFertilized, wb, fb);
    }
}