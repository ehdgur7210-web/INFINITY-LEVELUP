using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FarmQuestManager — 농장 시간제 퀘스트 시스템 (SO Database 방식)
///
/// ★ 변경 사항:
///   - 기존 List&lt;FarmQuestTemplate&gt; → FarmQuestDatabaseSO 단일 에셋으로 교체
///   - maxActiveQuests, refreshHours 설정도 Database SO로 이전
///   - 난이도 가중치 기반 랜덤 퀘스트 선택
///   - 특별 아이템 보상 지원 (FarmQuestTemplateSO.specialItemReward)
/// </summary>
[DefaultExecutionOrder(-40)]
public class FarmQuestManager : MonoBehaviour
{
    public static FarmQuestManager Instance { get; private set; }

    // ════════════════════════════════════════════════
    //  Inspector
    // ════════════════════════════════════════════════

    [Header("★ 퀘스트 데이터베이스 SO (여기만 연결하면 됩니다)")]
    [Tooltip("FarmQuestDatabaseSO 에셋을 드래그하세요.\n" +
             "퀘스트 추가/수정은 이 에셋만 열면 됩니다.")]
    public FarmQuestDatabaseSO questDatabase;

    // 이벤트
    public static event Action OnQuestsRefreshed;
    public static event Action<FarmQuestState> OnQuestCompleted;
    public static event Action<int, int> OnQuestProgress; // questIndex, newCount

    // ════════════════════════════════════════════════
    //  런타임 상태
    // ════════════════════════════════════════════════

    private List<FarmQuestState> activeQuests = new List<FarmQuestState>();
    private DateTime nextRefreshTime;

    // ── 무한 반복 퀘스트 ──
    [Header("무한 반복 퀘스트")]
    [SerializeField] private bool 무한반복모드 = true;
    private int repeatCycle = 0; // 사이클 (0부터, 필요 수량 = 사이클+1)

    // 작물 ID 순서 (밀→쌀→당근→양배추→토마토→딸기→수박→포도)
    private static readonly int[] CropOrder = { 0, 1, 2, 3, 4, 11, 12, 13 };
    private static readonly string[] CropNames = { "밀", "쌀", "당근", "양배추", "토마토", "딸기", "수박", "포도" };

    // ════════════════════════════════════════════════
    //  Unity 생명주기
    // ════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        Debug.Log("[ManagerInit] FarmQuestManager가 생성되었습니다.");

        float hours = questDatabase != null ? questDatabase.refreshHours : 1f;
        nextRefreshTime = DateTime.Now.AddHours(hours);
    }

    void Start()
    {
        if (Instance != this) return;
        if (activeQuests.Count == 0)
        {
            if (무한반복모드)
                GenerateRepeatQuests();
            else
                GenerateNewQuests();
        }
    }

    void Update()
    {
        if (FarmManager.Instance == null) return;
        if (DateTime.Now >= nextRefreshTime)
            GenerateNewQuests();
    }

    // ════════════════════════════════════════════════
    //  퀘스트 생성
    // ════════════════════════════════════════════════

    private void GenerateNewQuests()
    {
        activeQuests.Clear();

        int playerLevel = PlayerStats.Instance != null ? PlayerStats.Instance.level : 1;

        List<FarmQuestTemplateSO> picked;

        if (questDatabase != null && questDatabase.questTemplates.Count > 0)
        {
            // ★ Database SO의 가중치 기반 랜덤 선택
            picked = questDatabase.PickRandomQuests(playerLevel);
        }
        else
        {
            // Database가 없으면 FarmManager.allCrops 기반 자동 생성 (폴백)
            AutoGenerateQuests(playerLevel);
            goto Finalize;
        }

        for (int i = 0; i < picked.Count; i++)
        {
            var state = CreateStateFromSO(picked[i], i, playerLevel);
            if (state != null) activeQuests.Add(state);
        }

    Finalize:
        float refreshHours = questDatabase != null ? questDatabase.refreshHours : 1f;
        nextRefreshTime = DateTime.Now.AddHours(refreshHours);

        OnQuestsRefreshed?.Invoke();
        Debug.Log($"[FarmQuestManager] 퀘스트 {activeQuests.Count}개 갱신 (다음:{nextRefreshTime:HH:mm})");
    }

    private FarmQuestState CreateStateFromSO(FarmQuestTemplateSO so, int index, int playerLevel)
    {
        if (so == null) return null;

        CropData crop = FarmManager.Instance?.GetCropByID(so.targetCropID);

        return new FarmQuestState
        {
            questIndex = index,
            questTitle = so.questTitle,
            questDescription = so.questDescription,
            targetCropID = so.targetCropID,
            targetCropName = crop != null ? crop.cropName : "작물",
            targetCropIcon = so.questIcon != null ? so.questIcon
                              : (crop != null ? crop.seedIcon : null),
            requiredAmount = so.GetRequiredAmount(playerLevel),
            currentAmount = 0,
            cropPointReward = so.GetCropPointReward(playerLevel),
            goldReward = so.GetGoldReward(),
            difficulty = so.difficulty,
            specialItemReward = so.specialItemReward,
            specialItemCount = so.specialItemCount,
            isCompleted = false,
            isSubmitted = false
        };
    }

    // Database 없을 때 폴백 자동 생성
    private void AutoGenerateQuests(int playerLevel)
    {
        if (FarmManager.Instance == null) return;
        var crops = FarmManager.Instance.allCrops;
        if (crops == null || crops.Count == 0) return;

        var pool = new List<CropData>(crops);
        Shuffle(pool);

        int max = questDatabase != null ? questDatabase.maxActiveQuests : 3;
        int count = Mathf.Min(max, pool.Count);

        for (int i = 0; i < count; i++)
        {
            var crop = pool[i];
            if (crop == null) continue;  // ★ null 항목 건너뜀
            if (crop.requiredPlayerLevel > playerLevel) continue;
            int required = Mathf.Max(1, 2 + i + (playerLevel / 5));
            int reward = required * Mathf.Max(3, crop.cropPointReward);
            activeQuests.Add(new FarmQuestState
            {
                questIndex = i,
                questTitle = $"{crop.cropName} 납품",
                questDescription = $"{crop.cropName}을(를) {required}개 수확하세요!",
                targetCropID = crop.cropID,
                targetCropName = crop.cropName,
                targetCropIcon = crop.seedIcon,
                requiredAmount = required,
                currentAmount = 0,
                cropPointReward = reward,
                goldReward = reward * 20,
                difficulty = QuestDifficulty.Normal,
                isCompleted = false,
                isSubmitted = false
            });
        }
    }

    // ════════════════════════════════════════════════
    //  수확 이벤트 수신
    // ════════════════════════════════════════════════

    public void OnCropHarvested(int cropID, int amount)
    {
        for (int i = 0; i < activeQuests.Count; i++)
        {
            var quest = activeQuests[i];
            if (quest.isSubmitted || quest.isCompleted) continue;
            if (quest.targetCropID != cropID) continue;

            quest.currentAmount = Mathf.Min(quest.currentAmount + amount, quest.requiredAmount);
            OnQuestProgress?.Invoke(i, quest.currentAmount);

            if (quest.currentAmount >= quest.requiredAmount)
            {
                quest.isCompleted = true;
                Debug.Log($"[FarmQuestManager] 퀘스트 달성: {quest.questTitle}");
                UIManager.Instance?.ShowMessage($"퀘스트 달성! {quest.questTitle}", Color.yellow);
            }
        }
    }

    // ════════════════════════════════════════════════
    //  보상 수령
    // ════════════════════════════════════════════════

    public bool SubmitQuest(int questIndex)
    {
        if (questIndex < 0 || questIndex >= activeQuests.Count) return false;
        var quest = activeQuests[questIndex];

        if (!quest.isCompleted)
        {
            UIManager.Instance?.ShowMessage(
                $"퀘스트 미완료! ({quest.currentAmount}/{quest.requiredAmount})", Color.red);
            return false;
        }
        if (quest.isSubmitted)
        {
            UIManager.Instance?.ShowMessage("이미 보상을 받았습니다!", Color.yellow);
            return false;
        }

        quest.isSubmitted = true;

        if (quest.cropPointReward > 0)
            CropPointService.Add(quest.cropPointReward);

        if (quest.goldReward > 0)
            GameManager.Instance?.AddGold(quest.goldReward);

        // ★ 특별 아이템 보상
        if (quest.specialItemReward != null && quest.specialItemCount > 0)
            InventoryManager.Instance?.AddItem(quest.specialItemReward, quest.specialItemCount);

        OnQuestCompleted?.Invoke(quest);
        UIManager.Instance?.ShowMessage(
            $"보상 수령! +{quest.cropPointReward} +{quest.goldReward}", Color.green);

        // ★ 무한 반복: 모든 퀘스트 수령 완료 시 다음 사이클
        if (무한반복모드 && AllQuestsSubmitted())
        {
            repeatCycle++;
            Debug.Log($"[FarmQuestManager] 사이클 {repeatCycle} 시작! (필요 수량: {repeatCycle + 1}개)");
            UIManager.Instance?.ShowMessage($"농장 퀘스트 사이클 {repeatCycle + 1} 시작! (수량 +1)", new Color(1f, 0.84f, 0f));
            GenerateRepeatQuests();
        }

        return true;
    }

    private bool AllQuestsSubmitted()
    {
        foreach (var q in activeQuests)
            if (!q.isSubmitted) return false;
        return activeQuests.Count > 0;
    }

    private void GenerateRepeatQuests()
    {
        activeQuests.Clear();
        int amount = repeatCycle + 1; // 사이클마다 +1
        float rewardScale = 1f + repeatCycle * 0.15f; // 사이클마다 보상 15% 증가

        for (int i = 0; i < CropOrder.Length; i++)
        {
            int cropID = CropOrder[i];
            string cropName = i < CropNames.Length ? CropNames[i] : "작물";

            CropData crop = FarmManager.Instance?.GetCropByID(cropID);
            if (crop != null) cropName = crop.cropName;

            int baseReward = Mathf.Max(500, crop != null ? crop.cropPointReward : 500);

            activeQuests.Add(new FarmQuestState
            {
                questIndex = i,
                questTitle = $"{cropName} {amount}개 채집",
                questDescription = $"{cropName}을(를) {amount}개 수확하세요!",
                targetCropID = cropID,
                targetCropName = cropName,
                targetCropIcon = crop != null ? crop.seedIcon : null,
                requiredAmount = amount,
                currentAmount = 0,
                cropPointReward = (int)(baseReward * amount * rewardScale),
                goldReward = (int)(1000 * amount * rewardScale),
                difficulty = QuestDifficulty.Normal,
                isCompleted = false,
                isSubmitted = false
            });
        }

        OnQuestsRefreshed?.Invoke();
    }

    // ════════════════════════════════════════════════
    //  조회 API
    // ════════════════════════════════════════════════

    public List<FarmQuestState> GetActiveQuests() => activeQuests;
    public FarmQuestState GetQuest(int i)
        => (i >= 0 && i < activeQuests.Count) ? activeQuests[i] : null;

    public float GetSecondsUntilRefresh()
        => Mathf.Max(0f, (float)(nextRefreshTime - DateTime.Now).TotalSeconds);

    public int GetReadyToSubmitCount()
    {
        int n = 0;
        foreach (var q in activeQuests)
            if (q.isCompleted && !q.isSubmitted) n++;
        return n;
    }

    // ════════════════════════════════════════════════
    //  저장 / 로드
    // ════════════════════════════════════════════════

    public FarmQuestSaveData GetSaveData()
    {
        var data = new FarmQuestSaveData
        {
            nextRefreshTimeISO = nextRefreshTime.ToString("o"),
            repeatCycle = repeatCycle,
            quests = new FarmQuestStateSave[activeQuests.Count]
        };
        for (int i = 0; i < activeQuests.Count; i++)
        {
            var q = activeQuests[i];
            data.quests[i] = new FarmQuestStateSave
            {
                questIndex = q.questIndex,
                questTitle = q.questTitle,
                questDescription = q.questDescription,
                targetCropID = q.targetCropID,
                requiredAmount = q.requiredAmount,
                currentAmount = q.currentAmount,
                cropPointReward = q.cropPointReward,
                goldReward = q.goldReward,
                difficulty = (int)q.difficulty,
                isCompleted = q.isCompleted,
                isSubmitted = q.isSubmitted
            };
        }
        return data;
    }

    public void LoadSaveData(FarmQuestSaveData data)
    {
        if (data == null) return;

        repeatCycle = data.repeatCycle;

        if (!string.IsNullOrEmpty(data.nextRefreshTimeISO))
            DateTime.TryParse(data.nextRefreshTimeISO,
                null, System.Globalization.DateTimeStyles.RoundtripKind,
                out nextRefreshTime);

        if (!무한반복모드 && DateTime.Now >= nextRefreshTime) { GenerateNewQuests(); return; }

        activeQuests.Clear();
        if (data.quests != null)
        {
            foreach (var saved in data.quests)
            {
                CropData crop = FarmManager.Instance?.GetCropByID(saved.targetCropID);

                // ★ templateSO 에서 questIcon / specialItem 복원
                FarmQuestTemplateSO so = FindTemplateByCropID(saved.targetCropID);

                activeQuests.Add(new FarmQuestState
                {
                    questIndex = saved.questIndex,
                    questTitle = saved.questTitle,
                    questDescription = saved.questDescription,
                    targetCropID = saved.targetCropID,
                    targetCropName = crop != null ? crop.cropName : "작물",
                    targetCropIcon = so?.questIcon != null ? so.questIcon
                                       : (crop != null ? crop.seedIcon : null),
                    requiredAmount = saved.requiredAmount,
                    currentAmount = saved.currentAmount,
                    cropPointReward = saved.cropPointReward,
                    goldReward = saved.goldReward,
                    difficulty = (QuestDifficulty)saved.difficulty,
                    specialItemReward = so?.specialItemReward,
                    specialItemCount = so?.specialItemCount ?? 0,
                    isCompleted = saved.isCompleted,
                    isSubmitted = saved.isSubmitted
                });
            }
        }
        Debug.Log($"[FarmQuestManager] 로드: {activeQuests.Count}개 (갱신:{nextRefreshTime:HH:mm})");
    }

    private FarmQuestTemplateSO FindTemplateByCropID(int cropID)
    {
        if (questDatabase == null) return null;
        foreach (var t in questDatabase.questTemplates)
            if (t != null && t.targetCropID == cropID) return t;
        return null;
    }

    // ─────────────────────────────────────
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

// ════════════════════════════════════════════════
//  퀘스트 런타임 상태 (기존 FarmQuestState 확장)
// ════════════════════════════════════════════════
[Serializable]
public class FarmQuestState
{
    public int questIndex;
    public string questTitle;
    public string questDescription;
    public int targetCropID;
    public string targetCropName;
    public Sprite targetCropIcon;       // UI용 (저장 안 함)
    public int requiredAmount;
    public int currentAmount;
    public int cropPointReward;
    public int goldReward;
    public QuestDifficulty difficulty;
    public ItemData specialItemReward;  // UI용 (저장 안 함)
    public int specialItemCount;
    public bool isCompleted;
    public bool isSubmitted;

    public float GetProgressRate()
        => requiredAmount <= 0 ? 0f : Mathf.Clamp01((float)currentAmount / requiredAmount);
}

// ════════════════════════════════════════════════
//  저장 데이터 구조체
// ════════════════════════════════════════════════
[Serializable]
public class FarmQuestStateSave
{
    public int questIndex;
    public string questTitle;
    public string questDescription;
    public int targetCropID;
    public int requiredAmount;
    public int currentAmount;
    public int cropPointReward;
    public int goldReward;
    public int difficulty;   // QuestDifficulty enum → int 저장
    public bool isCompleted;
    public bool isSubmitted;
}

[Serializable]
public class FarmQuestSaveData
{
    public string nextRefreshTimeISO;
    public int repeatCycle;
    public FarmQuestStateSave[] quests;
}