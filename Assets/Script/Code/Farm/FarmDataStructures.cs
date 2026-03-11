using System;
using System.Collections.Generic;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// FarmDataStructures.cs
//
// ★ CropType enum → CropData.cs 로 이전됨 (여기서 삭제)
// ★ PlotType enum → FarmPartialExtensions.cs 에 있던 것도 삭제
//
// ★ 이 파일에 남아있는 것:
//   - FarmBuildingSaveData : 건물 레벨 저장 데이터
// ═══════════════════════════════════════════════════════════════════

/// <summary>밭 타입 (채소밭 / 과일나무밭)</summary>
public enum PlotType
{
    Vegetable = 0,  // 채소밭
    FruitTree = 1   // 과일나무밭
}

[Serializable]
public class FarmBuildingSaveData
{
    public int houseLevel = 1; // 집
    public int greenhouseLevel = 1; // 비닐하우스
    public int watermillLevel = 1; // 물레방아
    public int windmillLevel = 1; // 풍차
}