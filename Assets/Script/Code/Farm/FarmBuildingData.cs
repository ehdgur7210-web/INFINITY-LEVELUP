using System;
using System.Collections.Generic;
using UnityEngine;

// ════════════════════════════════════════════════════════════
//  FarmBuildingData.cs
//  ★ FarmDataStructures.cs에 없는 신규 타입만 정의합니다.
//    (PlotType, CropType, FarmBuildingSaveData는 FarmDataStructures에 있음)
// ════════════════════════════════════════════════════════════

/// <summary>건물 종류</summary>
public enum BuildingType { House, Greenhouse, Watermill, Windmill }

[Serializable]
public class HouseLevelData
{
    public int level;
    public int upgradeCostGold;
    public int upgradeCostGem;
    public int requiredPlayerLevel;
    [Tooltip("이 레벨업 시 자동 해금되는 텃밭 칸 수")]
    public int newVegetablePlotsCount = 2;
    [Tooltip("이 레벨업 시 자동 해금되는 과일나무 칸 수")]
    public int newFruitTreePlotsCount = 1;
    [Tooltip("전체 작물 성장속도 보너스 (0.03 = 3%)")]
    public float growthSpeedBonus = 0.03f;
    [TextArea(1, 2)] public string description;
}

[Serializable]
public class GreenhouseLevelData
{
    public int level;
    public int upgradeCostGold;
    public int upgradeCostGem;
    public int requiredPlayerLevel;
    [Tooltip("CropData.requiredGreenhouseLevel == 이 값인 작물이 해금됨")]
    public int unlockedGreenhouseLevel;
    [TextArea(1, 2)] public string description;
}

[Serializable]
public class WatermillLevelData
{
    public int level;
    public int upgradeCostGold;
    public int upgradeCostGem;
    public int requiredPlayerLevel;
    [Tooltip("물주기 성장시간 단축 비율 (0.05 = 5%)")]
    public float waterTimeReductionBonus = 0.05f;
    [TextArea(1, 2)] public string description;
}

[Serializable]
public class WindmillLevelData
{
    public int level;
    public int upgradeCostGold;
    public int upgradeCostGem;
    public int requiredPlayerLevel;
    [Tooltip("비료 성장시간 단축 비율 (0.05 = 5%)")]
    public float fertilizerTimeReductionBonus = 0.05f;
    [TextArea(1, 2)] public string description;
}

[Serializable]
public class FarmInventorySaveData
{
    public List<FarmItemCount> seeds = new List<FarmItemCount>();
    public List<FarmItemCount> harvests = new List<FarmItemCount>();
}

[Serializable]
public class FarmItemCount
{
    public int cropID;
    public int count;
}

[Serializable]
public class BuildingCardUI
{
    public TMPro.TextMeshProUGUI levelText;
    public UnityEngine.UI.Slider levelProgressBar;
    public TMPro.TextMeshProUGUI currentEffectText;
    public TMPro.TextMeshProUGUI nextEffectText;
    public TMPro.TextMeshProUGUI costText;
    public UnityEngine.UI.Button upgradeButton;
    public UnityEngine.UI.Image buildingImage;
    public Sprite[] levelSprites;
}

[Serializable]
public class QuestSlotUI : MonoBehaviour
{
    public UnityEngine.UI.Image cropIcon;
    public TMPro.TextMeshProUGUI titleText;
    public TMPro.TextMeshProUGUI descText;
    public TMPro.TextMeshProUGUI progressText;
    public UnityEngine.UI.Slider progressBar;
    public TMPro.TextMeshProUGUI rewardText;
    public UnityEngine.UI.Button submitButton;
    public GameObject completedBadge;
    public GameObject completionEffect;
}