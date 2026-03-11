using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EquipmentData 기반 가챠 시스템 (ResourceBarManager 연동)
/// ⭐ 티켓 시스템은 ResourceBarManager에서 관리
///
/// ★ 패치 내용:
///   - 레벨 1→2 전환 시 Gold + CropPoint 추가 비용 소모
///   - GachaEquipmentTierManager 연동 (원자/분자/DNA 티어)
/// </summary>
public class GachaManager : MonoBehaviour
{
    public static GachaManager Instance;

    [System.Serializable]
    public class GachaItem
    {
        public EquipmentData equipment;
        public float probability;
    }

    [Header("가챠 설정")]
    public int ticketCostPerGacha = 1;      // 1회당 티켓 소모량

    [Header("가챠 풀 (레벨별)")]
    public List<GachaItem> gachaPoolLv1 = new List<GachaItem>();
    public List<GachaItem> gachaPoolLv2 = new List<GachaItem>();
    public List<GachaItem> gachaPoolLv3 = new List<GachaItem>();
    public List<GachaItem> gachaPoolLv4 = new List<GachaItem>();
    public List<GachaItem> gachaPoolLv5 = new List<GachaItem>();

    [Header("레벨 시스템")]
    public int currentLevel = 1;
    public int maxLevel = 5;
    public int gachaCountForLevelUp = 10;
    public int currentGachaCount = 0;

    // ★ 레벨 1→2 전환 비용 (GachaEquipmentTierManager가 없을 경우 여기서 직접 사용)
    [Header("레벨 1→2 업그레이드 비용 (티어매니저 미사용 시 직접 설정)")]
    public int lv1to2GoldCost = 500;
    public int lv1to2CropPointCost = 100;

    [Header("UI 참조")]
    public GameObject gachaPanel;
    public GameObject lampButton;

    [Header("디버그")]
    public bool debugMode = true;

    private List<GachaItem> currentGachaPool;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
        }
        else
        {
            // ✅ 새 씬(MainScene)의 GachaManager에 Inspector 데이터가 있으면
            //    기존(Intro씬) 인스턴스에 덮어쓰고 새것은 제거
            bool hasData = (gachaPoolLv1 != null && gachaPoolLv1.Count > 0) ||
                           (gachaPoolLv2 != null && gachaPoolLv2.Count > 0) ||
                           (gachaPoolLv3 != null && gachaPoolLv3.Count > 0) ||
                           (gachaPoolLv4 != null && gachaPoolLv4.Count > 0) ||
                           (gachaPoolLv5 != null && gachaPoolLv5.Count > 0);

            if (hasData)
            {
                Instance.gachaPoolLv1 = new List<GachaItem>(gachaPoolLv1);
                Instance.gachaPoolLv2 = new List<GachaItem>(gachaPoolLv2);
                Instance.gachaPoolLv3 = new List<GachaItem>(gachaPoolLv3);
                Instance.gachaPoolLv4 = new List<GachaItem>(gachaPoolLv4);
                Instance.gachaPoolLv5 = new List<GachaItem>(gachaPoolLv5);
                Instance.UpdateGachaPool();
                Instance.ValidateGachaPool();
                Debug.Log("[GachaManager] 씬 전환 감지 → 가챠 풀 갱신 완료!");
            }
            else
            {
                Debug.LogWarning("[GachaManager] 새 씬의 가챠 풀이 비어있습니다! Inspector에서 데이터를 연결해주세요.");
            }

            enabled = false;
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (Instance != this) return;
        UpdateGachaPool();

        if (gachaPanel != null)
            gachaPanel.SetActive(true);

        ValidateGachaPool();
    }

    void Update() { }

    // ★ MainGameInitializer에서 씬 복귀 시 호출
    public void BindGachaUI(GameObject panel, GameObject lamp = null)
    {
        gachaPanel = panel;
        if (lamp != null) lampButton = lamp;
        if (gachaPanel != null) gachaPanel.SetActive(true);
        Debug.Log("[GachaManager] UI 재연결 완료");
    }

    public void UpdateGachaPool()
    {
        switch (currentLevel)
        {
            case 1: currentGachaPool = gachaPoolLv1; break;
            case 2: currentGachaPool = gachaPoolLv2; break;
            case 3: currentGachaPool = gachaPoolLv3; break;
            case 4: currentGachaPool = gachaPoolLv4; break;
            case 5: currentGachaPool = gachaPoolLv5; break;
            default: currentGachaPool = gachaPoolLv1; break;
        }

        if (debugMode)
            Debug.Log($"[GachaManager] 가챠 풀 업데이트: 레벨 {currentLevel}");
    }

    public void ValidateGachaPool()
    {
        if (currentGachaPool == null || currentGachaPool.Count == 0)
        {
            Debug.LogWarning("[GachaManager] 현재 레벨의 가챠 풀이 비어있습니다!");
            return;
        }

        float totalProbability = 0f;
        foreach (GachaItem gachaItem in currentGachaPool)
            totalProbability += gachaItem.probability;

        if (Mathf.Abs(totalProbability - 100f) > 0.01f)
            Debug.LogWarning($"[GachaManager] 가챠 확률 총합이 100%가 아닙니다! 현재: {totalProbability}%");
        else
            Debug.Log($"[GachaManager] 가챠 풀 검증 완료! 레벨 {currentLevel}, 총 확률: {totalProbability}%");
    }

    // ════════════════════════════════════════════════════════
    //  뽑기 공개 메서드
    // ════════════════════════════════════════════════════════

    /// <summary>1회 가챠 (ResourceBarManager 연동)</summary>
    public void PerformSingleGacha()
    {
        Debug.Log("[GachaManager] ========== 1회 가챠 시작 ==========");

        if (!CheckGachaPool()) return;
        if (!SpendTickets(ticketCostPerGacha)) return;

        EquipmentData result = PerformGacha();
        if (result != null)
        {
            Debug.Log($"[GachaManager] 가챠 결과: {result.itemName} ({result.rarity})");
            InventoryManager.Instance?.AddItem(result, 1);
            IncrementGachaCount();

            ShowGachaResults(new List<EquipmentData> { result });
            RefreshGachaUI();
        }

        Debug.Log("[GachaManager] ========== 1회 가챠 완료 ==========");
    }

    /// <summary>10연차 (ResourceBarManager 연동)</summary>
    public void PerformTenGacha()
    {
        Debug.Log("[GachaManager] ========== 10연차 가챠 시작 ==========");

        if (!CheckGachaPool()) return;
        if (!SpendTickets(ticketCostPerGacha * 10)) return;

        List<EquipmentData> results = new List<EquipmentData>();
        for (int i = 0; i < 10; i++)
        {
            EquipmentData result = PerformGacha();
            if (result != null)
            {
                results.Add(result);
                InventoryManager.Instance?.AddItem(result, 1);
                IncrementGachaCount();
            }
        }

        ShowGachaResults(results);
        RefreshGachaUI();

        Debug.Log("[GachaManager] ========== 10연차 가챠 완료 ==========");
    }

    /// <summary>100연차 (ResourceBarManager 연동)</summary>
    public void PerformHundredGacha()
    {
        Debug.Log("[GachaManager] ========== 100연차 가챠 시작 ==========");

        if (!CheckGachaPool()) return;
        if (!SpendTickets(ticketCostPerGacha * 100)) return;

        List<EquipmentData> results = new List<EquipmentData>();
        for (int i = 0; i < 100; i++)
        {
            EquipmentData result = PerformGacha();
            if (result != null)
            {
                results.Add(result);
                InventoryManager.Instance?.AddItem(result, 1);
                IncrementGachaCount();
            }

            if ((i + 1) % 10 == 0 && debugMode)
                Debug.Log($"[GachaManager] 100연차 진행: {i + 1}/100");
        }

        int legendaryCount = 0, epicCount = 0, rareCount = 0;
        foreach (var item in results)
        {
            if (item.rarity == ItemRarity.Legendary) legendaryCount++;
            else if (item.rarity == ItemRarity.Epic) epicCount++;
            else if (item.rarity == ItemRarity.Rare) rareCount++;
        }
        Debug.Log($"[GachaManager] 100연차 결과: 전설 {legendaryCount} / 영웅 {epicCount} / 희귀 {rareCount}");

        ShowGachaResults(results);
        RefreshGachaUI();

        UIManager.Instance?.ShowMessage($"⭐ 100연차 완료! 전설 {legendaryCount}개!", Color.yellow);
        Debug.Log("[GachaManager] ========== 100연차 가챠 완료 ==========");
    }

    // ════════════════════════════════════════════════════════
    //  내부 뽑기 로직
    // ════════════════════════════════════════════════════════

    public EquipmentData PerformGacha()
    {
        if (currentGachaPool == null || currentGachaPool.Count == 0)
        {
            Debug.LogWarning("[GachaManager] 가챠 풀이 비어있습니다!");
            return null;
        }

        bool hasValidItem = currentGachaPool.Exists(g => g != null && g.equipment != null);
        if (!hasValidItem)
        {
            Debug.LogError("[GachaManager] 가챠 풀에 유효한 equipment가 없습니다! Inspector 확인!");
            UIManager.Instance?.ShowMessage("가챠 데이터 오류!", Color.red);
            return null;
        }

        float randomValue = Random.Range(0f, 100f);
        float cumulativeProbability = 0f;

        foreach (GachaItem gachaItem in currentGachaPool)
        {
            if (gachaItem == null || gachaItem.equipment == null)
            {
                Debug.LogWarning("[GachaManager] equipment가 비어있는 항목이 있습니다!");
                continue;
            }

            cumulativeProbability += gachaItem.probability;

            if (randomValue <= cumulativeProbability)
            {
                if (debugMode)
                    Debug.Log($"[GachaManager] 뽑기: {gachaItem.equipment.itemName} " +
                              $"(확률: {gachaItem.probability}%, 굴림: {randomValue:F2})");
                return gachaItem.equipment;
            }
        }

        // 폴백: 마지막 항목
        GachaItem lastItem = currentGachaPool[currentGachaPool.Count - 1];
        if (lastItem != null && lastItem.equipment != null)
            return lastItem.equipment;

        Debug.LogError("[GachaManager] 가챠 결과를 생성할 수 없습니다! 모든 equipment가 null입니다.");
        return null;
    }

    // ════════════════════════════════════════════════════════
    //  가챠 카운트 증가 + 레벨업 처리
    //  ★ 패치: 레벨 1→2 전환 시 Gold + CropPoint 비용 확인
    // ════════════════════════════════════════════════════════

    void IncrementGachaCount()
    {
        currentGachaCount++;

        if (debugMode)
            Debug.Log($"[GachaManager] 가챠 횟수: {currentGachaCount}/{gachaCountForLevelUp}");

        // 업적 연동
        AchievementSystem.Instance?.UpdateAchievementProgress(AchievementType.GachaCount, "", 1);

        // 퀘스트 연동
        QuestManager.Instance?.UpdateQuestProgress(QuestType.Gacha, "", 1);

        if (currentGachaCount >= gachaCountForLevelUp)
        {
            if (currentLevel < maxLevel)
            {
                // ★ 레벨업 비용 확인 (레벨 1→2는 Gold + CropPoint 필요)
                if (!TryPayLevelUpCost(currentLevel))
                {
                    // 비용 부족 → 레벨업 보류. 카운트는 최대치에서 유지.
                    currentGachaCount = gachaCountForLevelUp;
                    Debug.Log("[GachaManager] 레벨업 비용 부족 - 비용 충전 후 다시 뽑으면 레벨업됩니다.");
                    return;
                }
                LevelUp();
            }
            else
            {
                // MAX 레벨: 카운트 리셋 후 전설 보장
                currentGachaCount = 0;
                Debug.Log($"[GachaManager] MAX Lv.{maxLevel} - 카운트 리셋, 무한 가챠 계속!");
                GuaranteedLegendary();
            }
        }
    }

    /// <summary>
    /// 레벨업 비용 지불
    /// - GachaEquipmentTierManager가 있으면 위임, 없으면 직접 처리
    /// </summary>
    private bool TryPayLevelUpCost(int fromLevel)
    {
        // GachaEquipmentTierManager에 위임
        if (GachaEquipmentTierManager.Instance != null)
            return GachaEquipmentTierManager.Instance.TryPayLevelUpCost(fromLevel);

        // 티어매니저 없을 때 직접 처리 (레벨 1→2만 비용 적용)
        if (fromLevel != 1) return true;

        int goldCost = lv1to2GoldCost;
        int cpCost = lv1to2CropPointCost;

        if (GameManager.Instance == null || GameManager.Instance.PlayerGold < goldCost)
        {
            UIManager.Instance?.ShowMessage(
                $"레벨업 비용 부족! 골드 {goldCost:N0}G 필요", Color.red);
            return false;
        }

        if (FarmManager.Instance == null || FarmManager.Instance.GetCropPoints() < cpCost)
        {
            UIManager.Instance?.ShowMessage(
                $"레벨업 비용 부족! 작물 포인트 {cpCost}CP 필요", Color.red);
            return false;
        }

        GameManager.Instance.SpendGold(goldCost);
        FarmManager.Instance.SpendCropPoints(cpCost);

        UIManager.Instance?.ShowMessage(
            $"레벨 1→2 업그레이드! -{goldCost:N0}G / -{cpCost}CP", Color.cyan);
        return true;
    }

    // ════════════════════════════════════════════════════════
    //  천장 보장 / 레벨업
    // ════════════════════════════════════════════════════════

    private void GuaranteedLegendary()
    {
        List<EquipmentData> legendaries = new List<EquipmentData>();
        foreach (var item in currentGachaPool)
        {
            if (item != null && item.equipment != null &&
                item.equipment.rarity == ItemRarity.Legendary)
                legendaries.Add(item.equipment);
        }

        if (legendaries.Count == 0)
        {
            Debug.LogWarning("[GachaManager] 전설 장비가 없어 천장 보장 불가!");
            return;
        }

        EquipmentData guaranteed = legendaries[Random.Range(0, legendaries.Count)];
        InventoryManager.Instance?.AddItem(guaranteed, 1);

        Debug.Log($"[GachaManager] ⭐ 천장 보장! 전설 지급: {guaranteed.itemName}");
        UIManager.Instance?.ShowMessage($"⭐ 천장 달성! [{guaranteed.itemName}] 획득!", Color.yellow);

        ShowGachaResults(new List<EquipmentData> { guaranteed });
        RefreshGachaUI();
    }

    void LevelUp()
    {
        currentLevel++;
        currentGachaCount = 0;

        Debug.Log($"[GachaManager] ========== 레벨업! ==========");
        Debug.Log($"[GachaManager] 새 레벨: {currentLevel}");

        UpdateGachaPool();
        ValidateGachaPool();

        // 티어 표시 갱신 (GachaEquipmentTierManager가 Update에서 감지하므로 자동 반영)
        string tierMsg = GetTierDisplayName();
        UIManager.Instance?.ShowMessage($"가챠 레벨 UP! Lv.{currentLevel}\n{tierMsg}", Color.yellow);

        Debug.Log($"[GachaManager] ===================================");
    }

    private string GetTierDisplayName()
    {
        if (GachaEquipmentTierManager.Instance != null)
            return GachaEquipmentTierManager.Instance.GetTierName(
                       GachaEquipmentTierManager.Instance.CurrentTier);

        // 티어매니저 없을 때 기본 표시
        if (currentLevel >= 20) return "🧬 DNA 장비 뽑기";
        if (currentLevel >= 10) return "🔬 분자 장비 뽑기";
        return "⚛️ 원자 장비 뽑기";
    }

    // ════════════════════════════════════════════════════════
    //  헬퍼 메서드
    // ════════════════════════════════════════════════════════

    private bool CheckGachaPool()
    {
        if (currentGachaPool == null || currentGachaPool.Count == 0 ||
            !currentGachaPool.Exists(g => g != null && g.equipment != null))
        {
            UIManager.Instance?.ShowMessage("가챠 데이터가 없습니다!", Color.red);
            return false;
        }
        return true;
    }

    private bool SpendTickets(int amount)
    {
        if (ResourceBarManager.Instance == null)
        {
            Debug.LogError("[GachaManager] ResourceBarManager가 없습니다!");
            return false;
        }

        if (!ResourceBarManager.Instance.SpendEquipmentTickets(amount))
        {
            UIManager.Instance?.ShowMessage($"장비 티켓 {amount}개가 필요합니다!", Color.red);
            return false;
        }
        return true;
    }

    private void RefreshGachaUI()
    {
        if (GachaUI.Instance != null)
        {
            GachaUI.Instance.UpdateTicketDisplay();
            GachaUI.Instance.UpdateLevelDisplay();
        }
    }

    void ShowGachaResults(List<EquipmentData> results)
    {
        if (GachaResultUI.Instance != null)
            GachaResultUI.Instance.ShowResults(results);
        else
        {
            Debug.Log("=== 가챠 결과 ===");
            foreach (EquipmentData equipment in results)
                Debug.Log($"- {equipment.itemName} ({equipment.rarity})");
        }
    }

    // ════════════════════════════════════════════════════════
    //  공개 체크 메서드
    // ════════════════════════════════════════════════════════

    public bool CanPerformSingleGacha()
        => ResourceBarManager.Instance != null &&
           ResourceBarManager.Instance.HasEquipmentTickets(ticketCostPerGacha);

    public bool CanPerformTenGacha()
        => ResourceBarManager.Instance != null &&
           ResourceBarManager.Instance.HasEquipmentTickets(ticketCostPerGacha * 10);

    public bool CanPerformHundredGacha()
        => ResourceBarManager.Instance != null &&
           ResourceBarManager.Instance.HasEquipmentTickets(ticketCostPerGacha * 100);

    public float GetLevelProgress()
        => (float)currentGachaCount / gachaCountForLevelUp * 100f;

    public int GetRemainingGachaForLevelUp()
        => gachaCountForLevelUp - currentGachaCount;

    // ★ 레벨업 비용 미리보기 (UI 표시용)
    public string GetLevelUpCostText()
    {
        if (currentLevel != 1) return "자동 레벨업 (뽑기 10회 달성 시)";

        if (GachaEquipmentTierManager.Instance != null)
            return $"{GachaEquipmentTierManager.Instance.lv1to2GoldCost:N0}G + " +
                   $"{GachaEquipmentTierManager.Instance.lv1to2CropPointCost}CP";

        return $"{lv1to2GoldCost:N0}G + {lv1to2CropPointCost}CP";
    }
}