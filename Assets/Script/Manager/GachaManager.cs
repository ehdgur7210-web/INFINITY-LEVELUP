using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EquipmentData 기반 가챠 시스템 (ResourceBarManager 연동)
/// ⭐ 티켓 시스템은 ResourceBarManager에서 관리
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
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        UpdateGachaPool();

        // ⭐ 가챠 패널은 항상 켜져있음
        if (gachaPanel != null)
        {
            gachaPanel.SetActive(true);
        }

        ValidateGachaPool();
    }

    void Update()
    {
        // G키로 토글 기능 제거 (항상 켜져있음)
    }

    void UpdateGachaPool()
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
        {
            Debug.Log($"[GachaManager] 가챠 풀 업데이트: 레벨 {currentLevel}");
        }
    }

    void ValidateGachaPool()
    {
        if (currentGachaPool == null || currentGachaPool.Count == 0)
        {
            Debug.LogWarning("[GachaManager] 현재 레벨의 가챠 풀이 비어있습니다!");
            return;
        }

        float totalProbability = 0f;
        foreach (GachaItem gachaItem in currentGachaPool)
        {
            totalProbability += gachaItem.probability;
        }

        if (Mathf.Abs(totalProbability - 100f) > 0.01f)
        {
            Debug.LogWarning($"[GachaManager] 가챠 확률 총합이 100%가 아닙니다! 현재: {totalProbability}%");
        }
        else
        {
            Debug.Log($"[GachaManager] 가챠 풀 검증 완료! 레벨 {currentLevel}, 총 확률: {totalProbability}%");
        }
    }

    /// <summary>
    /// ⭐ 1회 가챠 (ResourceBarManager 연동)
    /// </summary>
    public void PerformSingleGacha()
    {
        Debug.Log("[GachaManager] ========== 1회 가챠 시작 ==========");
        // ★ 풀 체크 먼저
        if (currentGachaPool == null || currentGachaPool.Count == 0 ||
            !currentGachaPool.Exists(g => g != null && g.equipment != null))
        {
            UIManager.Instance?.ShowMessage("가챠 데이터가 없습니다!", Color.red);
            return;
        }
        // ⭐ ResourceBarManager에서 티켓 체크 및 차감
        if (ResourceBarManager.Instance == null)
        {
            Debug.LogError("[GachaManager] ResourceBarManager가 없습니다!");
            return;
        }

        if (!ResourceBarManager.Instance.SpendEquipmentTickets(ticketCostPerGacha))
        {
            UIManager.Instance?.ShowMessage("장비 티켓이 부족합니다!", Color.red);
            return;
        }

        EquipmentData result = PerformGacha();

        if (result != null)
        {
            Debug.Log($"[GachaManager] 가챠 결과: {result.itemName} ({result.rarity})");

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddItem(result, 1);
            }

            IncrementGachaCount();

            List<EquipmentData> results = new List<EquipmentData> { result };
            ShowGachaResults(results);

            if (GachaUI.Instance != null)
            {
                GachaUI.Instance.UpdateTicketDisplay();
                GachaUI.Instance.UpdateLevelDisplay();
            }
        }

        Debug.Log("[GachaManager] ========== 1회 가챠 완료 ==========");
    }

    /// <summary>
    /// ⭐ 10연차 (ResourceBarManager 연동)
    /// </summary>
    public void PerformTenGacha()
    {
        Debug.Log("[GachaManager] ========== 10연차 가챠 시작 ==========");
        // ★ 풀 체크 먼저
        if (currentGachaPool == null || currentGachaPool.Count == 0 ||
            !currentGachaPool.Exists(g => g != null && g.equipment != null))
        {
            UIManager.Instance?.ShowMessage("가챠 데이터가 없습니다!", Color.red);
            return;
        }
        int requiredTickets = ticketCostPerGacha * 10;

        // ⭐ ResourceBarManager에서 티켓 체크 및 차감
        if (ResourceBarManager.Instance == null)
        {
            Debug.LogError("[GachaManager] ResourceBarManager가 없습니다!");
            return;
        }

        if (!ResourceBarManager.Instance.SpendEquipmentTickets(requiredTickets))
        {
            UIManager.Instance?.ShowMessage($"장비 티켓 {requiredTickets}개가 필요합니다!", Color.red);
            return;
        }

        List<EquipmentData> results = new List<EquipmentData>();

        for (int i = 0; i < 10; i++)
        {
            EquipmentData result = PerformGacha();

            if (result != null)
            {
                results.Add(result);

                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.AddItem(result, 1);
                }

                IncrementGachaCount();
            }
        }

        ShowGachaResults(results);

        if (GachaUI.Instance != null)
        {
            GachaUI.Instance.UpdateTicketDisplay();
            GachaUI.Instance.UpdateLevelDisplay();
        }

        Debug.Log("[GachaManager] ========== 10연차 가챠 완료 ==========");
    }

    /// <summary>
    /// ⭐⭐⭐ 100연차 (ResourceBarManager 연동)
    /// </summary>
    public void PerformHundredGacha()
    {
        Debug.Log("[GachaManager] ========== 100연차 가챠 시작 ==========");
        // ★ 풀 체크 먼저
        if (currentGachaPool == null || currentGachaPool.Count == 0 ||
            !currentGachaPool.Exists(g => g != null && g.equipment != null))
        {
            UIManager.Instance?.ShowMessage("가챠 데이터가 없습니다!", Color.red);
            return;
        }
        int requiredTickets = ticketCostPerGacha * 100;

        // ⭐ ResourceBarManager에서 티켓 체크 및 차감
        if (ResourceBarManager.Instance == null)
        {
            Debug.LogError("[GachaManager] ResourceBarManager가 없습니다!");
            return;
        }

        if (!ResourceBarManager.Instance.SpendEquipmentTickets(requiredTickets))
        {
            UIManager.Instance?.ShowMessage($"장비 티켓 {requiredTickets}개가 필요합니다!", Color.red);
            return;
        }

        List<EquipmentData> results = new List<EquipmentData>();

        // 100번 가챠 실행
        for (int i = 0; i < 100; i++)
        {
            EquipmentData result = PerformGacha();

            if (result != null)
            {
                results.Add(result);

                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.AddItem(result, 1);
                }

                IncrementGachaCount();
            }

            // 진행도 표시 (매 10회마다)
            if ((i + 1) % 10 == 0 && debugMode)
            {
                Debug.Log($"[GachaManager] 100연차 진행: {i + 1}/100");
            }
        }

        // 결과 요약 로그
        int legendaryCount = 0, epicCount = 0, rareCount = 0;
        foreach (var item in results)
        {
            if (item.rarity == ItemRarity.Legendary) legendaryCount++;
            else if (item.rarity == ItemRarity.Epic) epicCount++;
            else if (item.rarity == ItemRarity.Rare) rareCount++;
        }

        Debug.Log($"[GachaManager] 100연차 결과 요약:");
        Debug.Log($"  - 전설: {legendaryCount}개");
        Debug.Log($"  - 영웅: {epicCount}개");
        Debug.Log($"  - 희귀: {rareCount}개");

        ShowGachaResults(results);

        if (GachaUI.Instance != null)
        {
            GachaUI.Instance.UpdateTicketDisplay();
            GachaUI.Instance.UpdateLevelDisplay();
        }

        // 100연차 메시지
        UIManager.Instance?.ShowMessage($"⭐ 100연차 완료! 전설 {legendaryCount}개!", Color.yellow);

        Debug.Log("[GachaManager] ========== 100연차 가챠 완료 ==========");
    }

    public EquipmentData PerformGacha()
    {
        // 가챠 풀이 비어있는지 체크
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
            // ★★★ 핵심 수정: equipment가 null인 항목은 건너뛰기 ★★★
            if (gachaItem == null || gachaItem.equipment == null)
            {
                Debug.LogWarning("[GachaManager] 가챠 풀에 equipment가 비어있는 항목이 있습니다! Inspector에서 확인하세요.");
                continue; // 이 항목을 무시하고 다음으로
            }

            cumulativeProbability += gachaItem.probability;

            if (randomValue <= cumulativeProbability)
            {
                if (debugMode)
                {
                    Debug.Log($"[GachaManager] 뽑기 성공: {gachaItem.equipment.itemName} (확률: {gachaItem.probability}%, 굴림: {randomValue:F2})");
                }
                return gachaItem.equipment;
            }
        }

        // ★★★ 마지막 항목도 null 체크 ★★★
        GachaItem lastItem = currentGachaPool[currentGachaPool.Count - 1];
        if (lastItem != null && lastItem.equipment != null)
        {
            return lastItem.equipment;
        }

        Debug.LogError("[GachaManager] 가챠 결과를 생성할 수 없습니다! 모든 equipment가 null입니다.");
        return null;
    }
    void IncrementGachaCount()
    {
        currentGachaCount++;

        if (debugMode)
        {
            Debug.Log($"[GachaManager] 가챠 횟수: {currentGachaCount}/{gachaCountForLevelUp}");
        }
        if (AchievementSystem.Instance != null)
        {
            AchievementSystem.Instance.UpdateAchievementProgress(
                AchievementType.GachaCount,
                "",  // 모든 가챠
                1
            );
        }

        // ⭐⭐⭐ 퀘스트 시스템 연동!
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.UpdateQuestProgress(QuestType.Gacha, "", 1);
        }

        if (currentGachaCount >= gachaCountForLevelUp)
        {
            if (currentLevel < maxLevel)
            {
                LevelUp();
            }
            else
            {
                // ✅ MAX 레벨에서는 카운트 리셋 → 가챠 무한 반복 가능
                currentGachaCount = 0;
                Debug.Log($"[GachaManager] MAX Lv.{maxLevel} - 카운트 리셋, 무한 가챠 계속!");

                // ✅ MAX 레벨 천장 시스템: 10회마다 전설 1개 보장
                GuaranteedLegendary();
            }
        }

    }

    /// <summary>
    /// ✅ MAX 레벨 천장 시스템 - gachaCountForLevelUp 회마다 전설 1개 보장
    /// </summary>
    private void GuaranteedLegendary()
    {
        List<EquipmentData> legendaries = new List<EquipmentData>();
        foreach (var item in currentGachaPool)
        {
            if (item != null && item.equipment != null &&
                item.equipment.rarity == ItemRarity.Legendary)
            {
                legendaries.Add(item.equipment);
            }
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

        if (GachaUI.Instance != null)
        {
            GachaUI.Instance.UpdateTicketDisplay();
            GachaUI.Instance.UpdateLevelDisplay();
        }
    }
    void LevelUp()
    {
        currentLevel++;
        currentGachaCount = 0;

        Debug.Log($"[GachaManager] ========== 레벨업! ==========");
        Debug.Log($"[GachaManager] 새 레벨: {currentLevel}");

        UpdateGachaPool();
        ValidateGachaPool();

        UIManager.Instance?.ShowMessage($"가챠 레벨 UP! Lv.{currentLevel}", Color.yellow);

        Debug.Log($"[GachaManager] ===================================");
    }

    void ShowGachaResults(List<EquipmentData> results)
    {
        if (GachaResultUI.Instance != null)
        {
            GachaResultUI.Instance.ShowResults(results);
        }
        else
        {
            Debug.Log("=== 가챠 결과 ===");
            foreach (EquipmentData equipment in results)
            {
                Debug.Log($"- {equipment.itemName} ({equipment.rarity})");
            }
        }
    }

    /// <summary>
    /// ⭐ 티켓 체크 (ResourceBarManager 사용)
    /// </summary>
    public bool CanPerformSingleGacha()
    {
        return ResourceBarManager.Instance != null &&
               ResourceBarManager.Instance.HasEquipmentTickets(ticketCostPerGacha);
    }

    public bool CanPerformTenGacha()
    {
        return ResourceBarManager.Instance != null &&
               ResourceBarManager.Instance.HasEquipmentTickets(ticketCostPerGacha * 10);
    }

    public bool CanPerformHundredGacha()
    {
        return ResourceBarManager.Instance != null &&
               ResourceBarManager.Instance.HasEquipmentTickets(ticketCostPerGacha * 100);
    }

    public float GetLevelProgress()
    {
        return (float)currentGachaCount / gachaCountForLevelUp * 100f;
    }

    public int GetRemainingGachaForLevelUp()
    {
        return gachaCountForLevelUp - currentGachaCount;
    }
}