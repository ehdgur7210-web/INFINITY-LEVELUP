using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using LitJson;

/// <summary>
/// ══════════════════════════════════════════════════════════
/// BackendRankingManager — 뒤끝 랭킹 서버 연동
/// ══════════════════════════════════════════════════════════
///
/// [수정 내역]
///   Bug4: param.Add("score", score)
///         → gamedata 테이블에 "score" 컬럼이 없어서 업데이트 실패
///         → 뒤끝 랭킹은 gamedata 테이블의 실제 컬럼을 기준으로 집계됨
///         → 각 랭킹별 컬럼명 매핑:
///              전투력 랭킹 → "combat_power"  (테이블에 컬럼 추가 필요)
///              레벨 랭킹   → "player_level"  (기존 컬럼 사용)
///              농장 랭킹   → "farm_score"    (테이블에 컬럼 추가 필요)
///
///   Bug5: RowInDate가 null/empty일 때 UpdateUserScore 호출 시 실패
///         → gamedata 테이블에 행이 없으면 UpdateUserScore 자체가 불가
///         → RowInDate 없을 때 SaveToServer 먼저 호출하여 행 생성 후 점수 갱신
///
/// ══════════════════════════════════════════════════════════
///
/// ⭐ 뒤끝 콘솔 설정 (Bug4 수정 위해 필수):
///   1. gamedata 테이블에 컬럼 2개 추가:
///      - combat_power (int32, 기본값 0)
///      - farm_score   (int32, 기본값 0)
///      (player_level은 기존 컬럼 사용)
///
///   2. 랭킹 생성 시 "랭킹 기준 컬럼" 설정:
///      - combat_power_rank → 기준 컬럼: combat_power
///      - level_rank        → 기준 컬럼: player_level
///      - farm_rank         → 기준 컬럼: farm_score
///
///   3. 각 랭킹의 UUID를 Inspector에 입력
/// ══════════════════════════════════════════════════════════
/// </summary>
public class BackendRankingManager : MonoBehaviour
{
    public static BackendRankingManager Instance { get; private set; }

    [Header("뒤끝 랭킹 UUID (콘솔에서 복사)")]
    [Tooltip("전투력 랭킹 UUID")]
    [SerializeField] private string combatPowerRankUUID = "";
    [Tooltip("레벨 랭킹 UUID")]
    [SerializeField] private string levelRankUUID = "";
    [Tooltip("농장 랭킹 UUID")]
    [SerializeField] private string farmRankUUID = "";

    [Header("랭킹 테이블")]
    [Tooltip("BackendGameDataManager와 동일한 테이블명")]
    [SerializeField] private string tableName = "gamedata";

    [Header("설정")]
    [SerializeField] private int maxRankCount = 50;

    /// <summary>서버 통신 중 여부</summary>
    public bool IsBusy { get; private set; }

    /// <summary>현재 조회 중인 랭킹 타입 (ParseRankList에서 내 점수 오버라이드에 사용)</summary>
    private RankingManager.RankType currentRankType;

    /// <summary>랭킹 데이터 로드 완료 시 발생</summary>
    public static event Action<List<RankingManager.RankEntry>> OnRankListLoaded;

    /// <summary>내 순위 로드 완료 시 발생 (-1이면 미등록)</summary>
    public static event Action<int> OnMyRankLoaded;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // ★ UUID 설정 검증 — Inspector에서 미입력 시 경고
        if (string.IsNullOrEmpty(combatPowerRankUUID))
            Debug.LogWarning("[BackendRanking] ⚠ combatPowerRankUUID가 비어있음! Inspector에서 뒤끝 콘솔 UUID를 입력하세요.");
        if (string.IsNullOrEmpty(levelRankUUID))
            Debug.LogWarning("[BackendRanking] ⚠ levelRankUUID가 비어있음! Inspector에서 뒤끝 콘솔 UUID를 입력하세요.");
        if (string.IsNullOrEmpty(farmRankUUID))
            Debug.LogWarning("[BackendRanking] ⚠ farmRankUUID가 비어있음! Inspector에서 뒤끝 콘솔 UUID를 입력하세요.");

        Debug.Log($"[BackendRanking] 초기화 — UUID: combat={combatPowerRankUUID}, level={levelRankUUID}, farm={farmRankUUID}");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ══════════════════════════════════════════════════════
    //  점수 갱신
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 현재 플레이어의 모든 랭킹 점수를 서버에 갱신합니다.
    /// SaveLoadManager.SaveGame() 내부에서 자동 호출됩니다.
    /// </summary>
    public void UpdateAllScores()
    {
        // ★ null 안전성
        if (BackendManager.Instance == null || !BackendManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[BackendRanking] ⚠ 로그인 안 됨 → 랭킹 갱신 스킵");
            return;
        }

        // ✅ Bug5 수정: RowInDate 없으면 SaveToServer로 행 먼저 생성 후 갱신
        string rowInDate = BackendGameDataManager.Instance?.RowInDate;
        Debug.Log($"[BackendRanking] ▶ UpdateAllScores 호출 — RowInDate:{(rowInDate ?? "null")}");

        if (string.IsNullOrEmpty(rowInDate))
        {
            Debug.LogWarning("[BackendRanking] gamedata 행 없음 → SaveToServer 후 랭킹 갱신");
            if (BackendGameDataManager.Instance != null)
            {
                BackendGameDataManager.Instance.SaveToServer(success =>
                {
                    if (success)
                        DoUpdateAllScores();
                    else
                        Debug.LogWarning("[BackendRanking] SaveToServer 실패 → 랭킹 갱신 스킵");
                });
            }
            else
            {
                Debug.LogWarning("[BackendRanking] ⚠ BackendGameDataManager.Instance == null → 랭킹 갱신 불가");
            }
            return;
        }

        DoUpdateAllScores();
    }

    /// <summary>실제 점수 갱신 실행 (RowInDate 확보된 후)</summary>
    private void DoUpdateAllScores()
    {
        int combatPower = CombatPowerManager.Instance?.TotalCombatPower ?? 0;
        int level = GameManager.Instance?.PlayerLevel ?? 1;
        int farm = FarmManager.Instance?.GetCropPoints() ?? 0;

        Debug.Log($"[BackendRanking] ▶ DoUpdateAllScores — 전투력:{combatPower}, 레벨:{level}, 농장:{farm}");

        // ✅ Bug4 수정: 각 랭킹에 맞는 실제 gamedata 컬럼명 사용
        int updated = 0;
        if (!string.IsNullOrEmpty(combatPowerRankUUID))
        { UpdateScore(combatPowerRankUUID, "combat_power", combatPower, "전투력"); updated++; }
        else
            Debug.LogWarning("[BackendRanking] ⚠ 전투력 UUID 비어있음 → 스킵");

        if (!string.IsNullOrEmpty(levelRankUUID))
        { UpdateScore(levelRankUUID, "player_level", level, "레벨"); updated++; }
        else
            Debug.LogWarning("[BackendRanking] ⚠ 레벨 UUID 비어있음 → 스킵");

        if (!string.IsNullOrEmpty(farmRankUUID))
        { UpdateScore(farmRankUUID, "farm_score", farm, "농장"); updated++; }
        else
            Debug.LogWarning("[BackendRanking] ⚠ 농장 UUID 비어있음 → 스킵");

        if (updated == 0)
            Debug.LogError("[BackendRanking] ❌ 모든 랭킹 UUID가 비어있어 점수 갱신 0건! Inspector에서 UUID를 설정하세요.");
    }

    /// <summary>
    /// 특정 랭킹의 점수를 gamedata 테이블 컬럼에 업데이트합니다.
    /// 뒤끝 랭킹은 gamedata 테이블의 컬럼 값을 기준으로 자동 집계됩니다.
    /// </summary>
    private void UpdateScore(string rankUUID, string scoreColumn, int score, string label)
    {
        string rowInDate = BackendGameDataManager.Instance?.RowInDate ?? "";

        // ✅ Bug5 수정: RowInDate 빈 값이면 업데이트 스킵 (UpdateUserScore 서버 오류 방지)
        if (string.IsNullOrEmpty(rowInDate))
        {
            Debug.LogWarning($"[BackendRanking] {label} 랭킹 갱신 스킵: RowInDate 없음");
            return;
        }

        Debug.Log($"[BackendRanking] ▶ UpdateUserScore 호출 — {label}: {score}, UUID:{rankUUID.Substring(0, Mathf.Min(8, rankUUID.Length))}..., 컬럼:{scoreColumn}, RowInDate:{rowInDate}");

        // ✅ Bug4 수정: scoreColumn은 실제 gamedata 테이블 컬럼명 ("combat_power" 등)
        Param param = new Param();
        param.Add(scoreColumn, score);

        Backend.URank.User.UpdateUserScore(rankUUID, tableName, rowInDate, param, callback =>
        {
            if (callback.IsSuccess())
            {
                Debug.Log($"[BackendRanking] ✅ {label} 랭킹 점수 갱신 완료: {score}");
            }
            else if (callback.GetStatusCode() == "428")
            {
                // 428 Precondition Required: 서버 랭킹 집계 중 → 정상 응답, 재시도
                Debug.Log($"[BackendRanking] {label} 랭킹 집계 중 (428), 3초 후 재시도...");
                StartCoroutine(RetryUpdateAfterDelay(3f));
            }
            else
            {
                Debug.LogError($"[BackendRanking] ❌ {label} 랭킹 갱신 실패: statusCode={callback.GetStatusCode()}, errorCode={callback.GetErrorCode()}, msg={callback.GetMessage()}");
            }
        });
    }

    /// <summary>랭킹 집계 중(428) 시 재시도 코루틴</summary>
    private IEnumerator RetryUpdateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        UpdateAllScores();
    }

    // ══════════════════════════════════════════════════════
    //  랭킹 목록 조회
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 서버에서 랭킹 목록을 가져옵니다.
    /// RankingManager.RefreshFromServer() 코루틴에서 호출됩니다.
    /// </summary>
    public void GetRankList(RankingManager.RankType rankType,
                            Action<List<RankingManager.RankEntry>, bool> onComplete)
    {
        string uuid = GetUUID(rankType);

        if (string.IsNullOrEmpty(uuid) || !BackendManager.Instance.IsLoggedIn)
        {
            onComplete?.Invoke(null, false);
            return;
        }

        IsBusy = true;
        currentRankType = rankType; // ★ Bug6: ParseRankList에서 내 점수 오버라이드에 사용

        Backend.URank.User.GetRankList(uuid, maxRankCount, callback =>
        {
            IsBusy = false;

            if (!callback.IsSuccess())
            {
                Debug.LogWarning($"[BackendRanking] 랭킹 목록 조회 실패: {callback.GetMessage()}");
                onComplete?.Invoke(null, false);
                return;
            }

            var entries = ParseRankList(callback);
            Debug.Log($"[BackendRanking] {rankType} 랭킹 로드 완료: {entries.Count}명");
            OnRankListLoaded?.Invoke(entries);
            onComplete?.Invoke(entries, true);
        });
    }

    /// <summary>내 순위를 서버에서 조회합니다.</summary>
    public void GetMyRank(RankingManager.RankType rankType, Action<int> onComplete)
    {
        string uuid = GetUUID(rankType);

        if (string.IsNullOrEmpty(uuid) || !BackendManager.Instance.IsLoggedIn)
        {
            onComplete?.Invoke(-1);
            return;
        }

        Backend.URank.User.GetMyRank(uuid, callback =>
        {
            if (!callback.IsSuccess())
            {
                Debug.LogWarning($"[BackendRanking] 내 순위 조회 실패: {callback.GetMessage()}");
                onComplete?.Invoke(-1);
                return;
            }

            JsonData rows = callback.FlattenRows();
            int rank = -1;
            if (rows != null && rows.Count > 0 && rows[0].ContainsKey("rank"))
                rank = int.Parse(rows[0]["rank"].ToString()) + 1; // 0-based → 1-based

            Debug.Log($"[BackendRanking] 내 {rankType} 순위: {rank}위");
            OnMyRankLoaded?.Invoke(rank);
            onComplete?.Invoke(rank);
        });
    }

    // ══════════════════════════════════════════════════════
    //  JSON 파싱
    // ══════════════════════════════════════════════════════

    private List<RankingManager.RankEntry> ParseRankList(BackendReturnObject callback)
    {
        var entries = new List<RankingManager.RankEntry>();
        JsonData rows = callback.FlattenRows();
        if (rows == null) return entries;

        string myInDate = Backend.UserInDate;

        for (int i = 0; i < rows.Count; i++)
        {
            JsonData row = rows[i];

            string nickname = "모험가";
            if (row.ContainsKey("nickname") && row["nickname"] != null)
                nickname = row["nickname"].ToString();

            int score = 0;
            if (row.ContainsKey("score") && row["score"] != null)
                int.TryParse(row["score"].ToString(), out score);

            bool isMe = row.ContainsKey("gamerInDate")
                        && row["gamerInDate"] != null
                        && row["gamerInDate"].ToString() == myInDate;

            int classIndex = 0;
            if (row.ContainsKey("characterClassType") && row["characterClassType"] != null)
                int.TryParse(row["characterClassType"].ToString(), out classIndex);

            if (isMe)
            {
                string myName = GameDataBridge.CurrentData?.selectedCharacterName;
                if (!string.IsNullOrEmpty(myName)) nickname = myName;

                // ★ Bug6 수정: 내 점수를 현재 로컬 값으로 오버라이드
                // 서버 값은 마지막 SaveGame() 시점이므로 레벨업 등이 미반영될 수 있음
                int localCp   = CombatPowerManager.Instance?.TotalCombatPower ?? 0;
                int localLv   = GameManager.Instance?.PlayerLevel ?? 1;
                int localFarm = FarmManager.Instance?.GetCropPoints() ?? 0;
                score = currentRankType switch
                {
                    RankingManager.RankType.CombatPower => localCp,
                    RankingManager.RankType.Level       => localLv,
                    RankingManager.RankType.Farm        => localFarm,
                    _ => score
                };
            }
            else if (row.ContainsKey("character_name") && row["character_name"] != null)
            {
                string charName = row["character_name"].ToString();
                if (!string.IsNullOrEmpty(charName)) nickname = charName;
            }

            // combat_power 컬럼에서 전투력 읽기 (K단위 표시용)
            int combatPower = 0;
            if (isMe)
            {
                combatPower = CombatPowerManager.Instance?.TotalCombatPower ?? 0;
            }
            else if (row.ContainsKey("combat_power") && row["combat_power"] != null)
            {
                int.TryParse(row["combat_power"].ToString(), out combatPower);
            }

            entries.Add(new RankingManager.RankEntry
            {
                playerName = nickname,
                score = score,
                combatPower = combatPower,
                classIndex = classIndex,
                isMe = isMe
            });
        }

        return entries;
    }

    // ══════════════════════════════════════════════════════
    //  유틸
    // ══════════════════════════════════════════════════════

    private string GetUUID(RankingManager.RankType type) => type switch
    {
        RankingManager.RankType.CombatPower => combatPowerRankUUID,
        RankingManager.RankType.Level => levelRankUUID,
        RankingManager.RankType.Farm => farmRankUUID,
        _ => ""
    };
}