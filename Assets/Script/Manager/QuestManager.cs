using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    [Header("사용 가능한 퀘스트")]
    public List<QuestData> availableQuests = new List<QuestData>();

    [Header("진행 중인 퀘스트")]
    public QuestProgress currentQuest;

    [Header("완료된 퀘스트")]
    public List<int> completedQuestIDs = new List<int>();

    [Header("퀘스트 UI")]
    public GameObject questPanel;
    public QuestSlot questSlot;

    [Header("플레이어 참조 (비워도 됨 - Instance 자동 사용)")]
    public PlayerStats playerStats;

    private int currentQuestIndex = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        SetPanelActive(true);

        if (availableQuests == null || availableQuests.Count == 0)
        {
            Debug.LogError("[QuestManager] availableQuests가 비어있음!");
            return;
        }

        // ✅ 1프레임 뒤에 시작 → PlayerStats.level 초기화 보장
        Invoke(nameof(DelayedStart), 0.1f);
    }

    private void DelayedStart()
    {
        Debug.Log("[QuestManager] DelayedStart - 퀘스트 시작");
        StartNextQuest();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
            ToggleQuestPanel();
    }

    // ─────────────────────────────────────────
    // 퀘스트 시작
    // ─────────────────────────────────────────
    void StartNextQuest()
    {
        if (currentQuestIndex >= availableQuests.Count)
        {
            Debug.Log("[QuestManager] 모든 퀘스트 완료!");
            questSlot?.ClearSlot();
            UIManager.Instance?.ShowMessage("🎉 모든 퀘스트 완료!", Color.yellow);
            return;
        }

        QuestData nextQuest = availableQuests[currentQuestIndex];
        if (nextQuest == null) { currentQuestIndex++; StartNextQuest(); return; }

        // ✅ 레벨 체크: PlayerStats.Instance 우선, 없으면 playerStats 필드, 둘 다 없으면 스킵
        int playerLevel = 999;
        if (PlayerStats.Instance != null)
            playerLevel = PlayerStats.Instance.level;
        else if (playerStats != null)
            playerLevel = playerStats.level;

        if (playerLevel < nextQuest.requiredLevel)
        {
            Debug.Log($"[QuestManager] 레벨 부족 ({playerLevel} < {nextQuest.requiredLevel}) → 1초 후 재시도");
            Invoke(nameof(StartNextQuest), 1f);
            return;
        }

        // 퀘스트 시작
        currentQuest = new QuestProgress(nextQuest);
        currentQuest.status = QuestStatus.InProgress;

        Debug.Log($"[QuestManager] ✅ 퀘스트 시작: {nextQuest.questName} (인덱스: {currentQuestIndex})");
        SetPanelActive(true);
        UpdateQuestUI();
        UIManager.Instance?.ShowMessage($"📜 새 퀘스트: {nextQuest.questName}", Color.cyan);
    }

    // ─────────────────────────────────────────
    // ✅ 몬스터 처치 (핵심 - 직접 카운트)
    // ─────────────────────────────────────────
    public void OnMonsterKilled(string killedMonsterName)
    {
        if (currentQuest == null)
        {
            Debug.LogWarning("[Quest] currentQuest == null");
            return;
        }
        if (currentQuest.status != QuestStatus.InProgress)
        {
            Debug.LogWarning($"[Quest] status = {currentQuest.status} (InProgress 아님)");
            return;
        }

        bool counted = false;

        foreach (QuestObjective obj in currentQuest.objectives)
        {
            if (obj.objectiveType != QuestType.Kill) continue;
            if (obj.IsCompleted) continue;

            // ✅ targetID 비어있으면 모든 몬스터 카운트
            // ✅ targetID 있으면 대소문자 무시 비교
            bool match = string.IsNullOrEmpty(obj.targetID) ||
                         string.Equals(obj.targetID.Trim(), killedMonsterName.Trim(),
                                       System.StringComparison.OrdinalIgnoreCase);

            if (match)
            {
                obj.currentAmount = Mathf.Min(obj.currentAmount + 1, obj.requiredAmount);
                Debug.Log($"[Quest] ✅ {killedMonsterName} 처치! {obj.currentAmount}/{obj.requiredAmount}");
                counted = true;
            }
        }

        if (counted)
        {
            UpdateQuestUI();
            if (CheckAllCompleted())
                CompleteQuest();
        }
    }

    // ─────────────────────────────────────────
    // 가챠/강화 퀘스트 진행
    // ─────────────────────────────────────────
    public void UpdateQuestProgress(QuestType type, string targetID, int amount = 1)
    {
        if (currentQuest == null || currentQuest.status != QuestStatus.InProgress) return;

        bool counted = false;
        foreach (QuestObjective obj in currentQuest.objectives)
        {
            if (obj.objectiveType != type) continue;
            if (obj.IsCompleted) continue;

            bool match = string.IsNullOrEmpty(obj.targetID) ||
                         string.IsNullOrEmpty(targetID) ||
                         string.Equals(obj.targetID.Trim(), targetID.Trim(),
                                       System.StringComparison.OrdinalIgnoreCase);
            if (match)
            {
                obj.currentAmount = Mathf.Min(obj.currentAmount + amount, obj.requiredAmount);
                counted = true;
            }
        }

        if (counted)
        {
            UpdateQuestUI();
            if (CheckAllCompleted()) CompleteQuest();
        }
    }

    public void OnItemCollected(string itemID, int count)
        => UpdateQuestProgress(QuestType.Collect, itemID, count);

    // ─────────────────────────────────────────
    // 완료 체크 & 처리
    // ─────────────────────────────────────────
    bool CheckAllCompleted()
    {
        foreach (var obj in currentQuest.objectives)
            if (!obj.IsCompleted) return false;
        return true;
    }

    void CompleteQuest()
    {
        currentQuest.status = QuestStatus.Completed;
        Debug.Log($"[QuestManager] 🎉 퀘스트 완료: {currentQuest.questData.questName}");
        UIManager.Instance?.ShowMessage($"✅ 퀘스트 완료: {currentQuest.questData.questName}", Color.green);
        UpdateQuestUI();
    }

    // ─────────────────────────────────────────
    // 보상 수령
    // ─────────────────────────────────────────
    public bool ClaimQuestReward(QuestData quest)
    {
        if (currentQuest == null) return false;
        if (currentQuest.questData.questID != quest.questID) return false;
        if (currentQuest.status != QuestStatus.Completed) return false;

        // ★ 퀘스트 보상 수령 효과음
        SoundManager.Instance?.PlayQuestReward();

        QuestReward r = quest.reward;
        if (r.gold > 0) GameManager.Instance?.AddGold(r.gold);
        if (r.exp > 0) GameManager.Instance?.AddExp(r.exp);

        if (r.rewardItems != null)
        {
            for (int i = 0; i < r.rewardItems.Length; i++)
            {
                if (r.rewardItems[i] == null) continue;
                int cnt = (r.itemCounts != null && i < r.itemCounts.Length) ? r.itemCounts[i] : 1;
                InventoryManager.Instance?.AddItem(r.rewardItems[i], cnt);
            }
        }

        currentQuest.status = QuestStatus.Rewarded;
        completedQuestIDs.Add(quest.questID);

        Debug.Log($"[QuestManager] 🎁 보상 수령: {quest.questName}");
        UIManager.Instance?.ShowMessage("🎁 보상 획득!", Color.yellow);

        AchievementSystem.Instance?.UpdateAchievementProgress(AchievementType.CompleteQuests, "", 1);
        return true;
    }

    public void ShowNextQuest()
    {
        currentQuest = null;
        currentQuestIndex++;
        StartNextQuest();
    }

    // ─────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────
    void UpdateQuestUI()
    {
        SetPanelActive(true);
        questSlot?.SetupSlot(currentQuest);
    }

    public void ToggleQuestPanel()
    {
        if (questPanel == null) return;
        bool next = !questPanel.activeSelf;
        questPanel.SetActive(next);
        if (next) UpdateQuestUI();
    }

    void SetPanelActive(bool active)
    {
        if (questPanel != null) questPanel.SetActive(active);
        if (questSlot != null)
        {
            questSlot.gameObject.SetActive(active);
            if (questSlot.transform.parent != null)
                questSlot.transform.parent.gameObject.SetActive(active);
        }
    }
}