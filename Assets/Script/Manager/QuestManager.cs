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
        else { enabled = false; Destroy(gameObject); return; }
    }

    void Start()
    {
        if (Instance != this) return;
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

        // ★ 로드된 진행도가 있으면 복원 (StartNextQuest가 새 퀘스트를 만든 직후)
        if (pendingObjectiveAmounts != null && currentQuest != null)
        {
            for (int i = 0; i < currentQuest.objectives.Length && i < pendingObjectiveAmounts.Length; i++)
                currentQuest.objectives[i].currentAmount = pendingObjectiveAmounts[i];
            pendingObjectiveAmounts = null;

            // ★ 저장된 상태 복원
            if (pendingQuestStatus >= 0)
            {
                QuestStatus savedStatus = (QuestStatus)pendingQuestStatus;
                Debug.Log($"[QuestManager] 퀘스트 상태 복원: {savedStatus}");
                pendingQuestStatus = -1;

                // ★ Rewarded 상태면 이미 보상 받은 퀘스트 → 다음 퀘스트로
                if (savedStatus == QuestStatus.Rewarded)
                {
                    Debug.Log("[QuestManager] 이미 보상 받은 퀘스트 → 다음 퀘스트로 이동");
                    currentQuestIndex++;
                    pendingObjectiveAmounts = null;
                    StartNextQuest();
                    return;
                }

                currentQuest.status = savedStatus;
            }
            else
            {
                // ★ 상태가 저장되지 않은 경우 목표 달성 여부로 판단
                if (CheckAllCompleted())
                {
                    currentQuest.status = QuestStatus.Completed;
                    Debug.Log("[QuestManager] 목표 달성 확인 → Completed 상태 설정");
                }
            }

            UpdateQuestUI();
            Debug.Log("[QuestManager] 퀘스트 진행도 복원 완료");
        }
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
            CancelInvoke(nameof(StartNextQuest));
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

        // ★ 완료 상태 즉시 저장 (씬 전환 시 유실 방지)
        SaveLoadManager.Instance?.SaveGame();
    }

    // ─────────────────────────────────────────
    // 보상 수령
    // ─────────────────────────────────────────
    public bool ClaimQuestReward(QuestData quest)
    {
        if (currentQuest == null)
        {
            Debug.LogWarning("[QuestManager] ClaimQuestReward 실패: currentQuest == null");
            return false;
        }
        if (currentQuest.questData.questID != quest.questID)
        {
            Debug.LogWarning($"[QuestManager] ClaimQuestReward 실패: ID 불일치 (현재:{currentQuest.questData.questID} != 요청:{quest.questID})");
            return false;
        }
        if (currentQuest.status != QuestStatus.Completed)
        {
            Debug.LogWarning($"[QuestManager] ClaimQuestReward 실패: 상태가 Completed가 아님 (현재:{currentQuest.status})");
            return false;
        }

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

        // ★ 보상 수령 후 즉시 저장
        SaveLoadManager.Instance?.SaveGame();

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

    /// <summary>
    /// 씬 재로드 후 MainScenePanelBinder가 패널 연결 후 호출
    /// 현재 진행중인 퀘스트를 새 패널에 다시 표시
    /// </summary>
    public void RefreshUI()
    {
        if (currentQuest != null)
        {
            // 이미 진행중인 퀘스트가 있으면 UI만 갱신
            SetPanelActive(true);
            questSlot?.SetupSlot(currentQuest);
            Debug.Log("[QuestManager] 씬 재로드 후 퀘스트 UI 갱신");
        }
        else if (availableQuests != null && availableQuests.Count > 0)
        {
            // 아직 퀘스트 시작 안 됐으면 시작
            StartNextQuest();
        }
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

    // ─────────────────────────────────────────
    // 저장/로드용 메서드 (SaveLoadManager 연동)
    // ─────────────────────────────────────────
    public QuestSaveData GetQuestData()
    {
        QuestSaveData data = new QuestSaveData();

        // 완료된 퀘스트 ID 목록
        data.completedQuestIDs = completedQuestIDs.ToArray();

        // 현재 진행중인 퀘스트 인덱스 저장
        data.activeQuestIDs = new int[] { currentQuestIndex };

        // ★ 현재 퀘스트 목표 진행도(currentAmount) 저장 - ScriptableObject 참조 없이 순수 int[]로
        if (currentQuest != null && currentQuest.objectives != null)
        {
            data.currentObjectiveAmounts = new int[currentQuest.objectives.Length];
            for (int i = 0; i < currentQuest.objectives.Length; i++)
                data.currentObjectiveAmounts[i] = currentQuest.objectives[i].currentAmount;

            // ★ 퀘스트 상태 저장 (Completed 상태 유지를 위해 필수)
            data.currentQuestStatus = (int)currentQuest.status;
        }

        Debug.Log($"[QuestManager] 저장: 완료 {completedQuestIDs.Count}개, 현재인덱스 {currentQuestIndex}, 상태 {currentQuest?.status}");
        return data;
    }

    // ★ 로드 시 진행도/상태 복원을 위한 임시 저장 필드
    private int[] pendingObjectiveAmounts = null;
    private int pendingQuestStatus = -1;

    public void LoadQuestData(QuestSaveData data)
    {
        if (data == null)
        {
            Debug.Log("[QuestManager] 로드할 퀘스트 데이터 없음");
            return;
        }

        // 완료된 퀘스트 복원
        if (data.completedQuestIDs != null)
        {
            completedQuestIDs = new List<int>(data.completedQuestIDs);
        }

        // 진행 인덱스 복원
        if (data.activeQuestIDs != null && data.activeQuestIDs.Length > 0)
        {
            currentQuestIndex = data.activeQuestIDs[0];
        }

        // ★ 목표 진행도 임시 보관 (DelayedStart 후 적용)
        pendingObjectiveAmounts = data.currentObjectiveAmounts;

        // ★ 퀘스트 상태 임시 보관 (DelayedStart 후 적용)
        pendingQuestStatus = data.currentQuestStatus;

        Debug.Log($"[QuestManager] 로드: 완료 {completedQuestIDs.Count}개, 인덱스 {currentQuestIndex}, 상태 {pendingQuestStatus}");

        // 퀘스트 재시작 (UI 갱신)
        currentQuest = null;

        // ★ 핵심 수정: Start()에서 예약된 기존 DelayedStart를 취소한 뒤 재예약
        // 취소하지 않으면 DelayedStart가 2번 실행되어 두 번째 호출에서
        // StartNextQuest()가 새 퀘스트를 만들며 복원된 진행도를 덮어씀
        CancelInvoke(nameof(DelayedStart));
        Invoke(nameof(DelayedStart), 0.2f);
    }
}