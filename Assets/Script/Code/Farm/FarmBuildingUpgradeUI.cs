using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// FarmBuildingUpgradeUI — 건물 레벨업 패널
///
/// 하우스 / 비닐하우스 / 물레방아 / 풍차 각각 카드형으로 표시
/// 현재 레벨 / 다음 레벨 효과 / 업그레이드 버튼
/// </summary>
public class FarmBuildingUpgradeUI : MonoBehaviour
{
    // ════════════════════════════════════════════════
    //  Inspector 연결
    // ════════════════════════════════════════════════

    [Header("패널")]
    public GameObject upgradePanel;
    public Button closeButton;

    [Header("하우스 카드")]
    public BuildingCardUI houseCard;

    [Header("비닐하우스 카드")]
    public BuildingCardUI greenhouseCard;

    [Header("물레방아 카드")]
    public BuildingCardUI watermillCard;

    [Header("풍차 카드")]
    public BuildingCardUI windmillCard;

    // ════════════════════════════════════════════════
    //  초기화
    // ════════════════════════════════════════════════

    void Awake()
    {
        closeButton?.onClick.AddListener(ClosePanel);

        houseCard?.upgradeButton?.onClick.AddListener(() => TryUpgrade(BuildingType.House));
        greenhouseCard?.upgradeButton?.onClick.AddListener(() => TryUpgrade(BuildingType.Greenhouse));
        watermillCard?.upgradeButton?.onClick.AddListener(() => TryUpgrade(BuildingType.Watermill));
        windmillCard?.upgradeButton?.onClick.AddListener(() => TryUpgrade(BuildingType.Windmill));
    }

    void OnEnable()
    {
        FarmBuildingManager.OnBuildingLevelChanged += OnLevelChanged;
        RefreshAll();
    }

    void OnDisable()
    {
        FarmBuildingManager.OnBuildingLevelChanged -= OnLevelChanged;
    }

    // ════════════════════════════════════════════════
    //  열기/닫기
    // ════════════════════════════════════════════════

    public void OpenPanel()
    {
        upgradePanel?.SetActive(true);
        RefreshAll();
    }

    public void ClosePanel()
    {
        upgradePanel?.SetActive(false);
    }

    // ════════════════════════════════════════════════
    //  전체 갱신
    // ════════════════════════════════════════════════

    private void RefreshAll()
    {
        RefreshCard(BuildingType.House, houseCard);
        RefreshCard(BuildingType.Greenhouse, greenhouseCard);
        RefreshCard(BuildingType.Watermill, watermillCard);
        RefreshCard(BuildingType.Windmill, windmillCard);
    }

    private void RefreshCard(BuildingType type, BuildingCardUI card)
    {
        if (card == null || FarmBuildingManager.Instance == null) return;

        int currentLv = FarmBuildingManager.Instance.GetCurrentLevel(type);
        int maxLv = FarmBuildingManager.Instance.maxBuildingLevel;
        bool isMax = currentLv >= maxLv;

        // 레벨 표시
        if (card.levelText)
            card.levelText.text = isMax ? $"Lv{currentLv} ★MAX★" : $"Lv {currentLv} / {maxLv}";

        // 현재 효과 표시
        if (card.currentEffectText)
            card.currentEffectText.text = GetCurrentEffect(type, currentLv);

        // 다음 레벨 효과
        if (card.nextEffectText)
        {
            if (isMax)
                card.nextEffectText.text = "최대 레벨 달성!";
            else
                card.nextEffectText.text = "→ " + FarmBuildingManager.Instance.GetUpgradeDescription(type);
        }

        // 비용
        if (card.costText)
        {
            if (isMax)
            {
                card.costText.text = "-";
            }
            else
            {
                int nextLv = currentLv + 1;
                string costStr = GetCostString(type, nextLv);
                card.costText.text = costStr;
            }
        }

        // 업그레이드 버튼
        if (card.upgradeButton)
        {
            card.upgradeButton.interactable = !isMax && FarmBuildingManager.Instance.CanUpgrade(type);
            var btnText = card.upgradeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText) btnText.text = isMax ? "MAX" : "업그레이드";
        }

        // 진행 바
        if (card.levelProgressBar)
            card.levelProgressBar.value = (float)currentLv / maxLv;
    }

    private string GetCurrentEffect(BuildingType type, int level)
    {
        if (FarmBuildingManager.Instance == null) return "";

        switch (type)
        {
            case BuildingType.House:
                float houseBonus = FarmBuildingManager.Instance.GetHouseGrowthBonus();
                return $"🏠 텃밭 {level * 2}칸 / 과일나무 {level}칸\n⚡ 생산속도 +{houseBonus * 100f:F0}%";

            case BuildingType.Greenhouse:
                int unlockedCrops = FarmBuildingManager.Instance.GetUnlockedCrops().Count;
                return $"🌿 작물 {unlockedCrops}종 해금됨\n비닐하우스 레벨: {level}";

            case BuildingType.Watermill:
                float waterBonus = FarmBuildingManager.Instance.GetWaterTimeBonus();
                return $"💧 물주기 성장가속 +{waterBonus * 100f:F0}%\n(기본 작물 waterSpeedBonus에 추가)";

            case BuildingType.Windmill:
                float fertBonus = FarmBuildingManager.Instance.GetFertilizerTimeBonus();
                return $"💨 비료 성장가속 +{fertBonus * 100f:F0}%\n(비료 yieldBonus는 별도)";
        }
        return "";
    }

    private string GetCostString(BuildingType type, int level)
    {
        // ★ FIX: FarmBuildingManager의 public API로 Inspector 실제 비용 읽기
        //         (이전 코드는 하드코딩값이라 Inspector 커스텀 설정이 반영 안 됐음)
        if (FarmBuildingManager.Instance == null) return "";

        int gem = FarmBuildingManager.Instance.GetNextUpgradeCostGem(type);
        int gold = FarmBuildingManager.Instance.GetNextUpgradeCostGold(type);

        // 젬 비용이 있으면 젬 우선 표시
        if (gem > 0) return $"💎 {gem:N0}";
        return $"💰 {gold:N0}";
    }

    // ════════════════════════════════════════════════
    //  업그레이드 시도
    // ════════════════════════════════════════════════

    private void TryUpgrade(BuildingType type)
    {
        bool success = FarmBuildingManager.Instance?.TryUpgradeBuilding(type) ?? false;
        if (success)
            RefreshAll();
    }

    // ════════════════════════════════════════════════
    //  이벤트
    // ════════════════════════════════════════════════

    private void OnLevelChanged(BuildingType type, int level)
    {
        RefreshAll();
    }
}