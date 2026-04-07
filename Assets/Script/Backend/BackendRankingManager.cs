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
    [Tooltip("뒤끝 콘솔 > 랭킹 > 길드 랭킹 UUID (Guild Rank용, 선택)")]
    [SerializeField] private string guildRankUUID = "";

    [Header("랭킹 테이블")]
    [Tooltip("BackendGameDataManager와 동일한 테이블명")]
    [SerializeField] private string tableName = "gamedata";

    [Header("설정")]
    [SerializeField] private int maxRankCount = 50;

    /// <summary>서버 통신 중 여부</summary>
    public bool IsBusy { get; private set; }

    // ══════════════════════════════════════════════════════
    //  ★ 호출 한도 보호 — 쓰로틀링 + 변경 감지 + 403 잠금
    // ══════════════════════════════════════════════════════

    [Header("호출 빈도 제한 (배치 전송)")]
    [Tooltip("랭킹 서버 전송 주기(초). 이 주기로만 서버에 등록. 기본 1800초 = 30분")]
    [SerializeField] private float batchInterval = 1800f;

    [Tooltip("403 호출 한도 초과 시 잠금 시간(초). 이 시간 동안 모든 랭킹 호출 차단")]
    [SerializeField] private float quotaLockDuration = 600f;

    [Tooltip("게임 시작 후 첫 전송까지 대기 시간(초). 로그인 직후 폭주 방지")]
    [SerializeField] private float initialDelay = 60f;

    /// <summary>마지막 갱신 시 점수 — 변경 없으면 호출 스킵</summary>
    private int _lastCombat = -1;
    private int _lastLevel = -1;
    private long _lastFarm = -1;

    /// <summary>점수 변경됨 → 다음 배치 주기에 전송 필요</summary>
    private bool _isDirty = false;

    /// <summary>403 호출 한도 초과 → 이 시각까지 모든 랭킹 호출 차단</summary>
    private float _quotaLockedUntil = 0f;

    /// <summary>배치 전송 코루틴 핸들</summary>
    private Coroutine _batchCoroutine;

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
        Debug.Log("[ManagerInit] BackendRankingManager가 생성되었습니다.");
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
        Debug.Log($"[BackendRanking] 배치 전송 모드: {batchInterval}초마다 (초기 {initialDelay}초 대기)");

        // ★ 배치 전송 코루틴 시작
        _batchCoroutine = StartCoroutine(BatchUpdateLoop());
    }

    void OnDestroy()
    {
        if (_batchCoroutine != null)
        {
            StopCoroutine(_batchCoroutine);
            _batchCoroutine = null;
        }
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// ★ 배치 전송 루프 — 일정 주기로만 서버 호출
    /// 점수가 바뀌었을 때만(_isDirty) 실제 전송, 아니면 스킵
    /// </summary>
    private IEnumerator BatchUpdateLoop()
    {
        // 게임 시작 직후엔 다른 매니저 초기화 충돌 방지로 잠시 대기
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            if (_isDirty && Time.realtimeSinceStartup >= _quotaLockedUntil)
            {
                Debug.Log("[BackendRanking] ▶ 배치 전송 시도 (변경 감지됨)");
                DoUpdateAllScoresInternal();
            }
            yield return new WaitForSeconds(batchInterval);
        }
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
        // ★ 즉시 전송하지 않고 더티 플래그만 세움
        //   실제 전송은 BatchUpdateLoop가 batchInterval(기본 30분) 주기로 처리
        //   이렇게 하면 강화/저장 등으로 자주 호출돼도 호출 한도에 안 걸림

        if (BackendManager.Instance == null || !BackendManager.Instance.IsLoggedIn)
            return;

        // 점수 변경 감지: 동일하면 더티 표시조차 안 함
        int curCombat = CombatPowerManager.Instance?.TotalCombatPower ?? 0;
        int curLevel = GameManager.Instance?.PlayerLevel ?? 1;
        long curFarm = FarmManager.Instance?.GetCropPoints() ?? 0;
        if (curCombat == _lastCombat && curLevel == _lastLevel && curFarm == _lastFarm)
            return;

        _isDirty = true;
        // 실제 전송은 BatchUpdateLoop에서 처리
    }

    /// <summary>
    /// 배치 루프에서 호출하는 실제 전송 로직.
    /// 외부에서 호출 금지 — UpdateAllScores를 사용하세요.
    /// </summary>
    private void DoUpdateAllScoresInternal()
    {
        if (BackendManager.Instance == null || !BackendManager.Instance.IsLoggedIn)
            return;

        // 전송 시점의 최신 점수로 갱신
        _lastCombat = CombatPowerManager.Instance?.TotalCombatPower ?? 0;
        _lastLevel = GameManager.Instance?.PlayerLevel ?? 1;
        _lastFarm = FarmManager.Instance?.GetCropPoints() ?? 0;
        _isDirty = false; // 전송 시작 → 더티 해제 (실패 시 콜백에서 복원)

        // ✅ Bug5 수정: RowInDate 없으면 SaveToServer로 행 먼저 생성 후 갱신
        string rowInDate = BackendGameDataManager.Instance?.RowInDate;
        Debug.Log($"[BackendRanking] ▶ DoUpdateAllScoresInternal — RowInDate:{(rowInDate ?? "null")}");

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
        long farm = FarmManager.Instance?.GetCropPoints() ?? 0;

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
        { UpdateScore(farmRankUUID, "farm_score", (int)farm, "농장"); updated++; }
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
                // 428 Precondition Required: 서버 랭킹 집계 중 → 다음 배치 주기에 자연 재시도
                Debug.Log($"[BackendRanking] {label} 랭킹 집계 중 (428), 다음 배치 주기에 재시도");
                _isDirty = true; // 더티 복원 → 다음 BatchUpdateLoop에서 재전송
            }
            else if (callback.GetStatusCode() == "503"
                  || callback.GetErrorCode() == "ThrottlingException"
                  || callback.GetErrorCode() == "ProtocolError")
            {
                // 503/Throttling/ProtocolError: 뒤끝 서버 일시 장애 → 다음 배치 주기에 자연 재시도
                Debug.LogWarning($"[BackendRanking] ⚠ {label} 랭킹 일시 실패 (서버 혼잡): {callback.GetErrorCode()}");
                _isDirty = true;
            }
            else if (callback.GetStatusCode() == "403"
                  || (callback.GetMessage() != null && callback.GetMessage().Contains("call limit exceeded")))
            {
                // ★ 403 호출 한도 초과 → 잠금 활성화 (이후 모든 호출 차단)
                _quotaLockedUntil = Time.realtimeSinceStartup + quotaLockDuration;
                Debug.LogWarning($"[BackendRanking] ⚠ 호출 한도 초과 — {quotaLockDuration}초 동안 모든 랭킹 갱신 잠금. (뒤끝 무료 티어 일일 한도 초과 가능성)");
            }
            else
            {
                Debug.LogError($"[BackendRanking] ❌ {label} 랭킹 갱신 실패: statusCode={callback.GetStatusCode()}, errorCode={callback.GetErrorCode()}, msg={callback.GetMessage()}");
            }
        });
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
                long localFarm = FarmManager.Instance?.GetCropPoints() ?? 0;
                score = currentRankType switch
                {
                    RankingManager.RankType.CombatPower => localCp,
                    RankingManager.RankType.Level       => localLv,
                    RankingManager.RankType.Farm        => (int)localFarm,
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

            // ★ gamerInDate 저장 (친구 요청 등에 활용)
            string gamerInDate = "";
            if (row.ContainsKey("gamerInDate") && row["gamerInDate"] != null)
                gamerInDate = row["gamerInDate"].ToString();

            entries.Add(new RankingManager.RankEntry
            {
                playerName = nickname,
                score = score,
                combatPower = combatPower,
                classIndex = classIndex,
                isMe = isMe,
                gamerInDate = gamerInDate
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
        RankingManager.RankType.Guild => guildRankUUID,
        _ => ""
    };

    /// <summary>길드 랭킹 조회 — 뒤끝 길드 랭킹 UUID가 없으면 멤버 전투력 합산으로 로컬 생성</summary>
    public void GetGuildRankList(Action<List<RankingManager.RankEntry>, bool> onComplete)
    {
        if (!string.IsNullOrEmpty(guildRankUUID))
        {
            // 뒤끝 길드 랭킹 UUID가 설정되어 있으면 서버에서 조회
            GetRankList(RankingManager.RankType.Guild, onComplete);
            return;
        }

        // UUID 미설정 시 길드 목록에서 전투력 기준 정렬
        Backend.Guild.GetGuildListV3(50, bro =>
        {
            if (!bro.IsSuccess())
            {
                onComplete?.Invoke(null, false);
                return;
            }

            var entries = new List<RankingManager.RankEntry>();
            JsonData rows = bro.FlattenRows();
            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var entry = new RankingManager.RankEntry();
                    entry.playerName = rows[i].ContainsKey("guildName") ? rows[i]["guildName"]?.ToString() : "???";
                    entry.combatPower = 0;
                    if (rows[i].ContainsKey("goods"))
                    {
                        var goods = rows[i]["goods"];
                        if (goods.ContainsKey("totalCombatPower"))
                            entry.combatPower = (int)(long)goods["totalCombatPower"];
                    }
                    entry.score = entry.combatPower;
                    entry.isMe = BackendGuildManager.Instance != null
                              && BackendGuildManager.Instance.IsInGuild
                              && rows[i].ContainsKey("inDate")
                              && rows[i]["inDate"]?.ToString() == BackendGuildManager.Instance.MyGuild.guildInDate;
                    entries.Add(entry);
                }
            }

            // 전투력 내림차순 정렬
            entries.Sort((a, b) => b.score.CompareTo(a.score));
            onComplete?.Invoke(entries, true);
        });
    }
}