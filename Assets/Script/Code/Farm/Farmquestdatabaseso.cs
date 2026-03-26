using System.Collections.Generic;
using UnityEngine;

// ════════════════════════════════════════════════════════════
//  FarmQuestDatabaseSO — 농장 퀘스트 전체 데이터베이스 SO
//
//  [생성 방법]
//  Project 우클릭 → Create → Farm / Quest Database
//
//  [사용법]
//  1. FarmQuestDatabase SO 에셋 1개 생성
//  2. questTemplates 리스트에 FarmQuestTemplateSO 에셋들을 추가
//  3. FarmQuestManager Inspector → questDatabase 슬롯에 연결
//
//  [장점]
//  - Manager 코드/Hierarchy 건드리지 않고 에셋만 수정
//  - 퀘스트 추가/삭제/순서변경 → Database SO만 열면 됨
//  - 난이도별 필터, 랜덤 가중치 등 확장 용이
// ════════════════════════════════════════════════════════════
[CreateAssetMenu(fileName = "FarmQuestDatabase", menuName = "Farm/Quest Database")]
public class FarmQuestDatabaseSO : ScriptableObject
{
    [Header("── 퀘스트 템플릿 목록 ────────────────")]
    [Tooltip("여기에 FarmQuestTemplateSO 에셋들을 추가하세요.\n" +
             "순서는 상관없음 — 매 갱신마다 난이도/레벨 필터 후 랜덤 선택됩니다.")]
    public List<FarmQuestTemplateSO> questTemplates = new List<FarmQuestTemplateSO>();

    [Header("── 갱신 설정 ────────────────────────")]
    [Tooltip("동시에 활성화할 퀘스트 수 (Inspector에서 자유롭게 설정)")]
    [Min(1)]
    public int maxActiveQuests = 5;

    [Tooltip("퀘스트 갱신 주기 (시간 단위)")]
    [Min(0.1f)]
    public float refreshHours = 1f;

    [Header("── 난이도 비율 (합계 = 100) ──────────")]
    [Tooltip("Easy 비율 (%)")]
    [Range(0, 100)] public int easyWeight = 40;
    [Tooltip("Normal 비율 (%)")]
    [Range(0, 100)] public int normalWeight = 40;
    [Tooltip("Hard 비율 (%)")]
    [Range(0, 100)] public int hardWeight = 15;
    [Tooltip("Elite 비율 (%)")]
    [Range(0, 100)] public int eliteWeight = 5;

    // ─────────────────────────────────────
    //  조회 API
    // ─────────────────────────────────────

    /// <summary>
    /// 플레이어 레벨에 맞는 퀘스트 템플릿 목록 반환
    /// (requiredAmountPerTenLevels 스케일 적용된 결과만 포함)
    /// </summary>
    public List<FarmQuestTemplateSO> GetAvailableTemplates(int playerLevel)
    {
        var result = new List<FarmQuestTemplateSO>();
        foreach (var t in questTemplates)
        {
            if (t == null) continue;
            result.Add(t);
        }
        return result;
    }

    /// <summary>
    /// 난이도 가중치 기반으로 maxActiveQuests개 랜덤 선택
    /// </summary>
    public List<FarmQuestTemplateSO> PickRandomQuests(int playerLevel)
    {
        var available = GetAvailableTemplates(playerLevel);
        if (available.Count == 0) return available;

        // 난이도별 분류
        var byDifficulty = new Dictionary<QuestDifficulty, List<FarmQuestTemplateSO>>
        {
            { QuestDifficulty.Easy,   new List<FarmQuestTemplateSO>() },
            { QuestDifficulty.Normal, new List<FarmQuestTemplateSO>() },
            { QuestDifficulty.Hard,   new List<FarmQuestTemplateSO>() },
            { QuestDifficulty.Elite,  new List<FarmQuestTemplateSO>() },
        };
        foreach (var t in available)
            byDifficulty[t.difficulty].Add(t);

        // 가중치 기반 뽑기
        var selected = new List<FarmQuestTemplateSO>();
        int totalWeight = easyWeight + normalWeight + hardWeight + eliteWeight;
        if (totalWeight <= 0) totalWeight = 1;

        int needed = Mathf.Min(maxActiveQuests, available.Count);
        var shuffledAll = new List<FarmQuestTemplateSO>(available);
        Shuffle(shuffledAll);

        // 가중치대로 우선순위 할당 후 개수 채우기
        AddByWeight(selected, byDifficulty[QuestDifficulty.Easy],
                    Mathf.RoundToInt(needed * easyWeight / (float)totalWeight));
        AddByWeight(selected, byDifficulty[QuestDifficulty.Normal],
                    Mathf.RoundToInt(needed * normalWeight / (float)totalWeight));
        AddByWeight(selected, byDifficulty[QuestDifficulty.Hard],
                    Mathf.RoundToInt(needed * hardWeight / (float)totalWeight));
        AddByWeight(selected, byDifficulty[QuestDifficulty.Elite],
                    Mathf.RoundToInt(needed * eliteWeight / (float)totalWeight));

        // 모자라면 나머지로 채움
        foreach (var t in shuffledAll)
        {
            if (selected.Count >= needed) break;
            if (!selected.Contains(t)) selected.Add(t);
        }

        return selected;
    }

    // ─────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────
    private void AddByWeight(List<FarmQuestTemplateSO> target,
                             List<FarmQuestTemplateSO> source, int count)
    {
        Shuffle(source);
        foreach (var t in source)
        {
            if (target.Count >= count) break;
            if (!target.Contains(t)) target.Add(t);
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("가중치 합계 확인")]
    private void ValidateWeights()
    {
        int total = easyWeight + normalWeight + hardWeight + eliteWeight;
        Debug.Log($"[FarmQuestDatabase] 가중치 합계: {total} (Easy:{easyWeight} / Normal:{normalWeight} / Hard:{hardWeight} / Elite:{eliteWeight})");
        if (total != 100) Debug.LogWarning("[FarmQuestDatabase] 합계가 100이 아닙니다!");
    }
#endif
}