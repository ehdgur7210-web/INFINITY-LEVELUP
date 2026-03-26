using System;
using System.Collections.Generic;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// CropData.cs
//
// ★ 설명:
//   작물 1종의 모든 데이터를 담는 ScriptableObject
//   이전에 Farmdata.cs + FarmPartialExtensions.cs에 partial로 쪼개져 있던 것을
//   하나의 파일로 통합
//
// ★ 생성 방법:
//   Project창 우클릭 → Create → Farm → Crop Data
// ═══════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "NewCrop", menuName = "Farm/Crop Data")]
public class CropData : ScriptableObject
{
    // ─── 기본 정보 ───────────────────────────────────────────────
    [Header("===== 기본 정보 =====")]
    public int cropID;
    public string cropName;
    [TextArea(1, 3)]
    public string description;

    // ─── 아이콘 / 스프라이트 ─────────────────────────────────────
    [Header("===== 아이콘 / 스프라이트 =====")]
    public Sprite seedIcon;        // 씨앗 아이콘 (상점/UI 표시용)
    public Sprite harvestIcon;     // 수확물 아이콘
    public Sprite[] growthSprites;   // 성장 단계별 스프라이트 (Empty 제외, 5단계)

    // ─── 성장 시간 ───────────────────────────────────────────────
    [Header("===== 성장 시간 =====")]
    [Tooltip("기본 성장 시간 (초). 물/비료/건물 보너스에 따라 단축됨")]
    public float growthTimeSeconds = 300f;

    // ─── 물주기 ──────────────────────────────────────────────────
    [Header("===== 물주기 =====")]
    [Tooltip("물주기 시 성장 시간 단축 비율 (0.3 = 30% 단축)")]
    [Range(0f, 0.9f)]
    public float waterSpeedBonus = 0.3f;
    public int waterCostGem = 1;

    // ─── 씨앗 구매 비용 ──────────────────────────────────────────
    [Header("===== 씨앗 구매 비용 =====")]
    public int seedCostGold = 50;
    public int seedCostGem = 0;    // 0이면 골드로 구매

    // ─── 수확 보상 ───────────────────────────────────────────────
    [Header("===== 수확 보상 =====")]
    public CropHarvestReward[] harvestRewards;

    // ─── 작물 포인트 ─────────────────────────────────────────────
    [Header("===== 작물 포인트 =====")]
    [Tooltip("수확 시 획득하는 작물 포인트")]
    public int cropPointReward = 5;

    // ─── 해금 조건 ───────────────────────────────────────────────
    [Header("===== 해금 조건 =====")]
    public int requiredPlayerLevel = 1;
    [Tooltip("비닐하우스 레벨 조건 (0 = 조건 없음)")]
    public int requiredGreenhouseLevel = 0;

    // ─── 카테고리 ────────────────────────────────────────────────
    [Header("===== 카테고리 =====")]
    public CropType cropType = CropType.Vegetable;

    // ═══ 메서드 ══════════════════════════════════════════════════

    /// <summary>성장 단계에 맞는 스프라이트 반환</summary>
    public Sprite GetSpriteForStage(CropStage stage)
    {
        if (growthSprites == null || growthSprites.Length == 0) return seedIcon;
        int idx = Mathf.Clamp((int)stage - 1, 0, growthSprites.Length - 1);
        return growthSprites[idx];
    }

    /// <summary>
    /// 물주기/비료/건물 보너스를 모두 반영한 실제 성장 시간 반환
    /// FarmCropShopUI, FarmBuildingManager에서 사용
    /// </summary>
    public float GetModifiedGrowthTime(bool isWatered, bool isFertilized,
                                       float waterBuildingBonus, float fertBuildingBonus)
    {
        float time = growthTimeSeconds;
        if (isWatered)
            time *= (1f - waterSpeedBonus - waterBuildingBonus);
        if (isFertilized)
            time *= (1f - fertBuildingBonus);
        return Mathf.Max(10f, time);
    }
}

// ═══════════════════════════════════════════════════════════════════
// 관련 열거형 & 보조 클래스 (CropData와 항상 함께 사용)
// ═══════════════════════════════════════════════════════════════════

/// <summary>작물 성장 단계</summary>
public enum CropStage
{
    Empty = 0,
    Seeded = 1,
    Sprout = 2,
    Growing = 3,
    NearReady = 4,
    ReadyToHarvest = 5
}

/// <summary>
/// 작물 카테고리
/// Herb는 현재 미사용 — 과일/채소만 운영
/// </summary>
public enum CropType
{
    Vegetable = 0,  // 채소
    Fruit = 1,  // 과일
    Greenhouse = 2,  // 비닐하우스 전용
    // Herb    = 3,  // 미사용 (필요 시 주석 해제)
}

/// <summary>수확 보상 데이터</summary>
[Serializable]
public class CropHarvestReward
{
    public ItemData item;
    [Range(1, 99)] public int minAmount = 1;
    [Range(1, 99)] public int maxAmount = 3;
    public int goldReward = 0;
    public int gemReward = 0;
}