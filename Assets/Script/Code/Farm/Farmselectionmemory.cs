// ═══════════════════════════════════════════════════════════════════
// FarmSelectionMemory.cs
//
// ★ 설명:
//   PlantModePanel 또는 ProgressPanel에서 선택한
//   물/비료 등급을 전역으로 기억하는 정적 클래스
//
//   - PlantModePanel에서 선택 → 저장
//   - ProgressPanel 슬롯이 저장된 값 사용
//   - ProgressPanel 내 변경 버튼으로 등급 교체 가능
//   - 두 패널이 항상 동기화됨
// ═══════════════════════════════════════════════════════════════════

using System;
using UnityEngine;

public static class FarmSelectionMemory
{
    // ─── 선택된 물 ───────────────────────────────────────────────
    public static WaterData SelectedWater { get; private set; }
    public static FertilizerData SelectedFertilizer { get; private set; }

    // ─── 변경 이벤트 (UI 갱신용) ─────────────────────────────────
    public static event Action<WaterData> OnWaterChanged;
    public static event Action<FertilizerData> OnFertChanged;

    // ─── 세터 ────────────────────────────────────────────────────
    public static void SetWater(WaterData water)
    {
        SelectedWater = water;
        OnWaterChanged?.Invoke(water);
        Debug.Log($"[FarmSelectionMemory] 물 선택: {water?.waterName ?? "없음"}");
    }

    public static void SetFertilizer(FertilizerData fert)
    {
        SelectedFertilizer = fert;
        OnFertChanged?.Invoke(fert);
        Debug.Log($"[FarmSelectionMemory] 비료 선택: {fert?.fertilizerName ?? "없음"}");
    }

    public static void Clear()
    {
        SelectedWater = null;
        SelectedFertilizer = null;
    }
}