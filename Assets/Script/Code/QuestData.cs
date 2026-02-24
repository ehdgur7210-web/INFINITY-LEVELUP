using UnityEngine;

// 퀘스트 타입
public enum QuestType
{
    Kill,           // 몬스터 처치
    BossKill,       // 보스처치
    Gacha,          // 가챠
    Enhance,        // 강화
    Collect         // 아이템 수집
}

// 퀘스트 상태
public enum QuestStatus
{
    NotStarted,     // 미시작
    InProgress,     // 진행 중
    Completed,      // 완료
    Rewarded        // 보상 수령 완료
}

// 퀘스트 목표
[System.Serializable]
public class QuestObjective
{
    public string objectiveName;        // 목표 이름
    public QuestType objectiveType;     // 목표 타입
    public string targetID;             // 대상 ID (몬스터 이름, 아이템 ID 등)
    public int requiredAmount;          // 요구 개수
    public int currentAmount;           // 현재 달성량

    // 목표 완료 여부
    public bool IsCompleted => currentAmount >= requiredAmount;
}

// 퀘스트 보상
[System.Serializable]
public class QuestReward
{
    public int gold;                    // 골드 보상
    public int exp;                     // 경험치 보상
    public ItemData[] rewardItems;      // 아이템 보상
    public int[] itemCounts;            // 아이템 개수
}

// 퀘스트 데이터
[CreateAssetMenu(fileName = "New Quest", menuName = "Game/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("기본 정보")]
    public int questID;                 // 퀘스트 ID
    public string questName;            // 퀘스트 이름

    [TextArea(3, 6)]
    public string questDescription;     // 퀘스트 설명

    [TextArea(2, 4)]
    public string questObjectiveText;   // 목표 설명

    [Header("퀘스트 설정")]
    public int requiredLevel = 1;       // 요구 레벨
    public QuestData[] prerequisiteQuests; // 선행 퀘스트

    [Header("목표")]
    public QuestObjective[] objectives; // 퀘스트 목표들

    [Header("보상")]
    public QuestReward reward;          // 보상

    [Header("NPC")]
    public string questGiverNPC;        // 퀘스트 제공 NPC
    public string questCompleteNPC;     // 퀘스트 완료 NPC
}

// ⭐⭐⭐ 퀘스트 진행 정보 (QuestManager와 QuestSlot에서 사용)
[System.Serializable]
public class QuestProgress
{
    public QuestData questData;         // 퀘스트 데이터
    public QuestStatus status;          // 퀘스트 상태
    public QuestObjective[] objectives; // 목표 진행 상황 (복사본)

    /// <summary>
    /// 생성자 - QuestData로부터 QuestProgress 생성
    /// </summary>
    public QuestProgress(QuestData data)
    {
        questData = data;
        status = QuestStatus.NotStarted;

        // ⭐ 목표 복사 (원본 ScriptableObject 보존)
        objectives = new QuestObjective[data.objectives.Length];
        for (int i = 0; i < data.objectives.Length; i++)
        {
            objectives[i] = new QuestObjective
            {
                objectiveName = data.objectives[i].objectiveName,
                objectiveType = data.objectives[i].objectiveType,
                targetID = data.objectives[i].targetID,
                requiredAmount = data.objectives[i].requiredAmount,
                currentAmount = 0  // ⭐ 진행도는 0부터 시작
            };
        }
    }

    /// <summary>
    /// 전체 진행도 비율 (0~1)
    /// QuestSlot의 progressBar에 사용
    /// </summary>
    public float GetProgressRatio()
    {
        int totalRequired = 0;
        int totalCurrent = 0;

        foreach (QuestObjective objective in objectives)
        {
            totalRequired += objective.requiredAmount;
            totalCurrent += objective.currentAmount;
        }

        if (totalRequired == 0) return 0f;

        return Mathf.Clamp01((float)totalCurrent / totalRequired);
    }

    /// <summary>
    /// 전체 필요량 계산
    /// QuestSlot의 progressText에 사용
    /// </summary>
    public int GetTotalRequired()
    {
        int total = 0;
        foreach (QuestObjective objective in objectives)
        {
            total += objective.requiredAmount;
        }
        return total;
    }

    /// <summary>
    /// 전체 현재량 계산
    /// QuestSlot의 progressText에 사용
    /// </summary>
    public int GetTotalCurrent()
    {
        int total = 0;
        foreach (QuestObjective objective in objectives)
        {
            total += objective.currentAmount;
        }
        return total;
    }

    /// <summary>
    /// 모든 목표 완료 여부
    /// QuestManager의 CheckQuestCompletion에 사용
    /// </summary>
    public bool IsAllObjectivesCompleted()
    {
        foreach (QuestObjective objective in objectives)
        {
            if (!objective.IsCompleted)
            {
                return false;
            }
        }
        return true;
    }
}
