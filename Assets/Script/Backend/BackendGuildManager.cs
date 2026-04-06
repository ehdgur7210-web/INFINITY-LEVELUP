using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using LitJson;

/// <summary>
/// BackendGuildManager — 뒤끝 길드 API 래핑 (DDOL 싱글톤)
///
/// [기능]
///   길드 생성/해산, 검색, 가입 신청/승인/거절, 탈퇴, 추방
///   멤버 목록 조회, 길드 정보 조회/수정
///   길드 출석(일일), 미션, 랭킹용 전투력 합산
///
/// [뒤끝 콘솔 준비]
///   1. 길드 기능 활성화 (뒤끝 콘솔 > 소셜 > 길드)
///   2. guild_attendance 테이블 생성 (Public)
///      컬럼: owner_inDate(string), guild_indate(string),
///            date(string "yyyy-MM-dd"), mission_kill(int), mission_stage(int),
///            rewarded(bool)
/// </summary>
public class BackendGuildManager : MonoBehaviour
{
    public static BackendGuildManager Instance { get; private set; }

    // ── 이벤트 ──
    public static event Action<GuildInfo> OnGuildInfoLoaded;
    public static event Action<List<GuildInfo>> OnGuildListLoaded;
    public static event Action<List<GuildMemberData>> OnMemberListLoaded;
    public static event Action<List<GuildApplicantData>> OnApplicantListLoaded;
    public static event Action<string> OnGuildError;
    public static event Action OnGuildJoined;
    public static event Action OnGuildLeft;
    public static event Action OnGuildCreated;
    public static event Action<bool> OnAttendanceChecked; // true=보상 지급

    // ── 데이터 ──
    public GuildInfo MyGuild { get; private set; }
    public bool IsInGuild => MyGuild != null && !string.IsNullOrEmpty(MyGuild.guildInDate);
    public GuildMemberRole MyRole { get; private set; } = GuildMemberRole.Member;

    // 출석/미션
    public bool TodayAttended { get; private set; }
    public int MissionKillCount { get; set; }
    public int MissionStageCount { get; set; }
    private string _attendanceRowInDate;

    [Header("===== 길드 설정 =====")]
    [SerializeField] private int maxGuildMembers = 30;
    [SerializeField] private int attendanceGoldReward = 5000;
    [SerializeField] private int missionKillTarget = 100;
    [SerializeField] private int missionStageTarget = 5;
    [SerializeField] private int missionGoldReward = 10000;
    [SerializeField] private int missionGemReward = 50;

    // ── 출석 테이블 ──
    private const string AttendanceTable = "guild_attendance";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[ManagerInit] BackendGuildManager가 생성되었습니다.");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ═══════════════════════════════════════
    //  길드 생성
    // ═══════════════════════════════════════

    public void CreateGuild(string guildName, int goodsAmount = 0, Action<bool, string> callback = null)
    {
        if (IsInGuild) { callback?.Invoke(false, "이미 길드에 가입되어 있습니다."); return; }

        Param param = new Param();
        param.Add("description", "");
        param.Add("level", 1);
        param.Add("totalCombatPower", 0);

        Backend.Guild.CreateGuildV3(guildName, maxGuildMembers, param, bro =>
        {
            if (bro.IsSuccess())
            {
                Debug.Log($"[Guild] 길드 생성 성공: {guildName}");
                LoadMyGuildInfo(() => {
                    OnGuildCreated?.Invoke();
                    callback?.Invoke(true, "길드가 생성되었습니다!");
                });
            }
            else
            {
                string msg = ParseGuildError(bro, "길드 생성");
                OnGuildError?.Invoke(msg);
                callback?.Invoke(false, msg);
            }
        });
    }

    // ═══════════════════════════════════════
    //  길드 정보 조회 (내 길드)
    // ═══════════════════════════════════════

    public void LoadMyGuildInfo(Action onComplete = null)
    {
        Backend.Guild.GetMyGuildInfoV3(bro =>
        {
            if (bro.IsSuccess())
            {
                JsonData json = bro.GetFlattenJSON();
                MyGuild = ParseGuildInfo(json);
                MyRole = ParseMyRole(json);
                Debug.Log($"[Guild] 내 길드 정보 로드: {MyGuild.guildName} (멤버 {MyGuild.memberCount}명)");
                OnGuildInfoLoaded?.Invoke(MyGuild);
            }
            else
            {
                string statusCode = bro.GetStatusCode();
                if (statusCode == "404")
                {
                    MyGuild = null;
                    MyRole = GuildMemberRole.Member;
                    Debug.Log("[Guild] 가입된 길드 없음");
                }
                else
                {
                    Debug.LogWarning($"[Guild] 길드 정보 로드 실패: {bro.GetMessage()}");
                }
            }
            onComplete?.Invoke();
        });
    }

    // ═══════════════════════════════════════
    //  길드 검색
    // ═══════════════════════════════════════

    public void SearchGuilds(string keyword, Action<List<GuildInfo>> callback = null)
    {
        // 키워드가 있으면 이름으로 검색, 없으면 전체 목록
        if (!string.IsNullOrEmpty(keyword))
        {
            Backend.Guild.GetGuildIndateByGuildNameV3(keyword, bro =>
            {
                var list = new List<GuildInfo>();
                if (bro.IsSuccess())
                {
                    // 이름 검색은 inDate를 반환 → 해당 길드 정보 조회
                    JsonData json = bro.GetFlattenJSON();
                    string guildInDate = json.ContainsKey("guildInDate") ? json["guildInDate"]?.ToString() : "";
                    if (!string.IsNullOrEmpty(guildInDate))
                    {
                        Backend.Guild.GetGuildInfoV3(guildInDate, infoBro =>
                        {
                            if (infoBro.IsSuccess())
                            {
                                list.Add(ParseGuildInfo(infoBro.GetFlattenJSON()));
                            }
                            OnGuildListLoaded?.Invoke(list);
                            callback?.Invoke(list);
                        });
                        return;
                    }
                }
                OnGuildListLoaded?.Invoke(list);
                callback?.Invoke(list);
            });
        }
        else
        {
            Backend.Guild.GetGuildListV3(20, bro =>
            {
                var list = new List<GuildInfo>();
                if (bro.IsSuccess())
                {
                    JsonData rows = bro.FlattenRows();
                    if (rows != null)
                    {
                        for (int i = 0; i < rows.Count; i++)
                            list.Add(ParseGuildInfo(rows[i]));
                    }
                }
                Debug.Log($"[Guild] 길드 목록: {list.Count}개");
                OnGuildListLoaded?.Invoke(list);
                callback?.Invoke(list);
            });
        }
    }

    // ═══════════════════════════════════════
    //  가입 신청
    // ═══════════════════════════════════════

    public void ApplyToGuild(string guildInDate, Action<bool, string> callback = null)
    {
        if (IsInGuild) { callback?.Invoke(false, "이미 길드에 가입되어 있습니다."); return; }

        Backend.Guild.ApplyGuildV3(guildInDate, bro =>
        {
            if (bro.IsSuccess())
            {
                Debug.Log("[Guild] 가입 신청 완료");
                callback?.Invoke(true, "가입 신청이 완료되었습니다.");
            }
            else
            {
                string msg = ParseGuildError(bro, "가입 신청");
                OnGuildError?.Invoke(msg);
                callback?.Invoke(false, msg);
            }
        });
    }

    // ═══════════════════════════════════════
    //  가입 신청 목록 (길드장/부관용)
    // ═══════════════════════════════════════

    public void LoadApplicants(Action<List<GuildApplicantData>> callback = null)
    {
        if (!IsInGuild || MyRole == GuildMemberRole.Member) return;

        Backend.Guild.GetApplicantsV3(bro =>
        {
            if (bro.IsSuccess())
            {
                var list = new List<GuildApplicantData>();
                JsonData rows = bro.FlattenRows();
                if (rows != null)
                {
                    for (int i = 0; i < rows.Count; i++)
                    {
                        list.Add(new GuildApplicantData
                        {
                            nickname = rows[i]["nickname"]?.ToString() ?? "???",
                            inDate = rows[i]["inDate"]?.ToString() ?? "",
                        });
                    }
                }
                OnApplicantListLoaded?.Invoke(list);
                callback?.Invoke(list);
            }
            else
            {
                callback?.Invoke(new List<GuildApplicantData>());
            }
        });
    }

    // ═══════════════════════════════════════
    //  가입 승인 / 거절
    // ═══════════════════════════════════════

    public void ApproveApplicant(string applicantInDate, Action<bool> callback = null)
    {
        Backend.Guild.ApproveApplicantV3(applicantInDate, bro =>
        {
            if (bro.IsSuccess())
            {
                Debug.Log($"[Guild] 가입 승인: {applicantInDate}");
                LoadMyGuildInfo();
                LoadApplicants();
                callback?.Invoke(true);
            }
            else
            {
                OnGuildError?.Invoke(ParseGuildError(bro, "가입 승인"));
                callback?.Invoke(false);
            }
        });
    }

    public void RejectApplicant(string applicantInDate, Action<bool> callback = null)
    {
        Backend.Guild.RejectApplicantV3(applicantInDate, bro =>
        {
            if (bro.IsSuccess())
            {
                Debug.Log($"[Guild] 가입 거절: {applicantInDate}");
                LoadApplicants();
                callback?.Invoke(true);
            }
            else
            {
                callback?.Invoke(false);
            }
        });
    }

    // ═══════════════════════════════════════
    //  멤버 목록
    // ═══════════════════════════════════════

    public void LoadMembers(Action<List<GuildMemberData>> callback = null)
    {
        if (!IsInGuild) return;

        Backend.Guild.GetGuildMemberListV3(MyGuild.guildInDate, bro =>
        {
            if (bro.IsSuccess())
            {
                var list = new List<GuildMemberData>();
                JsonData rows = bro.FlattenRows();
                if (rows != null)
                {
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var member = new GuildMemberData
                        {
                            nickname = rows[i]["nickname"]?.ToString() ?? "???",
                            inDate = rows[i]["inDate"]?.ToString() ?? "",
                            role = ParseMemberRole(rows[i]),
                        };
                        list.Add(member);
                    }
                }
                Debug.Log($"[Guild] 멤버 목록: {list.Count}명");
                OnMemberListLoaded?.Invoke(list);
                callback?.Invoke(list);
            }
            else
            {
                callback?.Invoke(new List<GuildMemberData>());
            }
        });
    }

    // ═══════════════════════════════════════
    //  탈퇴
    // ═══════════════════════════════════════

    public void LeaveGuild(Action<bool, string> callback = null)
    {
        if (!IsInGuild) { callback?.Invoke(false, "가입된 길드가 없습니다."); return; }

        Backend.Guild.WithdrawGuildV3(bro =>
        {
            if (bro.IsSuccess())
            {
                Debug.Log("[Guild] 길드 탈퇴 완료");
                MyGuild = null;
                MyRole = GuildMemberRole.Member;
                TodayAttended = false;
                OnGuildLeft?.Invoke();
                callback?.Invoke(true, "길드를 탈퇴했습니다.");
            }
            else
            {
                string msg = ParseGuildError(bro, "탈퇴");
                callback?.Invoke(false, msg);
            }
        });
    }

    // ═══════════════════════════════════════
    //  추방 (길드장/부관)
    // ═══════════════════════════════════════

    public void ExpelMember(string memberInDate, Action<bool> callback = null)
    {
        if (MyRole == GuildMemberRole.Member) { callback?.Invoke(false); return; }

        Backend.Guild.ExpelMemberV3(memberInDate, bro =>
        {
            if (bro.IsSuccess())
            {
                Debug.Log($"[Guild] 멤버 추방: {memberInDate}");
                LoadMyGuildInfo();
                LoadMembers();
                callback?.Invoke(true);
            }
            else
            {
                OnGuildError?.Invoke(ParseGuildError(bro, "추방"));
                callback?.Invoke(false);
            }
        });
    }

    // ═══════════════════════════════════════
    //  길드 정보 수정 (길드장)
    // ═══════════════════════════════════════

    public void UpdateGuildDescription(string description, Action<bool> callback = null)
    {
        if (MyRole != GuildMemberRole.Master) { callback?.Invoke(false); return; }

        Param param = new Param();
        param.Add("description", description);

        Backend.Guild.ModifyGuildV3(param, bro =>
        {
            if (bro.IsSuccess())
            {
                MyGuild.description = description;
                callback?.Invoke(true);
            }
            else
            {
                callback?.Invoke(false);
            }
        });
    }

    // ═══════════════════════════════════════
    //  길드 해산 (길드장)
    // ═══════════════════════════════════════

    public void DisbandGuild(Action<bool, string> callback = null)
    {
        if (MyRole != GuildMemberRole.Master) { callback?.Invoke(false, "길드장만 해산할 수 있습니다."); return; }

        // 길드장이 탈퇴하면 길드 해산 (뒤끝 SDK에 DeleteGuild 없음)
        Backend.Guild.WithdrawGuildV3(bro =>
        {
            if (bro.IsSuccess())
            {
                Debug.Log("[Guild] 길드 해산 완료");
                MyGuild = null;
                MyRole = GuildMemberRole.Member;
                OnGuildLeft?.Invoke();
                callback?.Invoke(true, "길드가 해산되었습니다.");
            }
            else
            {
                callback?.Invoke(false, ParseGuildError(bro, "해산"));
            }
        });
    }

    // ═══════════════════════════════════════
    //  부관 임명/해임 (길드장)
    // ═══════════════════════════════════════

    public void SetViceMaster(string memberInDate, bool appoint, Action<bool> callback = null)
    {
        if (MyRole != GuildMemberRole.Master) { callback?.Invoke(false); return; }

        if (appoint)
        {
            Backend.Guild.NominateViceMasterV3(memberInDate, bro =>
            {
                callback?.Invoke(bro.IsSuccess());
                if (bro.IsSuccess()) LoadMembers();
            });
        }
        else
        {
            Backend.Guild.ReleaseViceMasterV3(memberInDate, bro =>
            {
                callback?.Invoke(bro.IsSuccess());
                if (bro.IsSuccess()) LoadMembers();
            });
        }
    }

    // ═══════════════════════════════════════
    //  길드 전투력 갱신
    // ═══════════════════════════════════════

    public void UpdateGuildCombatPower()
    {
        if (!IsInGuild) return;
        int cp = CombatPowerManager.Instance?.TotalCombatPower ?? 0;

        // 길드 goods에 전투력 기여 (ContributeGoodsV3 사용)
        Param param = new Param();
        param.Add("totalCombatPower", cp);

        Backend.Guild.ModifyGuildV3(param, bro =>
        {
            if (bro.IsSuccess())
                Debug.Log($"[Guild] 전투력 갱신: {cp}");
        });
    }

    // ═══════════════════════════════════════
    //  출석 체크
    // ═══════════════════════════════════════

    public void CheckAttendance(Action<bool, string> callback = null)
    {
        if (!IsInGuild) { callback?.Invoke(false, "길드에 가입되어 있지 않습니다."); return; }
        if (TodayAttended) { callback?.Invoke(false, "오늘은 이미 출석했습니다."); return; }

        string today = DateTime.Now.ToString("yyyy-MM-dd");

        // 오늘 출석 기록 확인
        Where where = new Where();
        where.Equal("date", today);
        where.Equal("guild_indate", MyGuild.guildInDate);

        Backend.GameData.GetMyData(AttendanceTable, where, bro =>
        {
            if (bro.IsSuccess())
            {
                JsonData rows = bro.FlattenRows();
                if (rows != null && rows.Count > 0)
                {
                    TodayAttended = true;
                    _attendanceRowInDate = rows[0]["inDate"]?.ToString();
                    MissionKillCount = rows[0].ContainsKey("mission_kill") ? (int)rows[0]["mission_kill"] : 0;
                    MissionStageCount = rows[0].ContainsKey("mission_stage") ? (int)rows[0]["mission_stage"] : 0;
                    callback?.Invoke(false, "오늘은 이미 출석했습니다.");
                    return;
                }
            }

            // 출석 기록 생성 + 보상 지급
            Param param = new Param();
            param.Add("guild_indate", MyGuild.guildInDate);
            param.Add("date", today);
            param.Add("mission_kill", 0);
            param.Add("mission_stage", 0);
            param.Add("rewarded", false);

            Backend.GameData.Insert(AttendanceTable, param, insertBro =>
            {
                if (insertBro.IsSuccess())
                {
                    TodayAttended = true;
                    MissionKillCount = 0;
                    MissionStageCount = 0;
                    _attendanceRowInDate = insertBro.GetInDate();

                    // 출석 보상 지급
                    if (GameManager.Instance != null)
                        GameManager.Instance.AddGold(attendanceGoldReward);

                    Debug.Log($"[Guild] 출석 완료! 보상: 골드 {attendanceGoldReward}");
                    OnAttendanceChecked?.Invoke(true);
                    callback?.Invoke(true, $"출석 완료! 골드 +{UIManager.FormatKoreanUnit(attendanceGoldReward)}");
                }
                else
                {
                    callback?.Invoke(false, "출석 처리 실패");
                }
            });
        });
    }

    // ═══════════════════════════════════════
    //  미션 진행도 업데이트
    // ═══════════════════════════════════════

    public void AddMissionKill(int count = 1)
    {
        if (!IsInGuild || !TodayAttended) return;
        MissionKillCount += count;
        SaveMissionProgress();
    }

    public void AddMissionStage(int count = 1)
    {
        if (!IsInGuild || !TodayAttended) return;
        MissionStageCount += count;
        SaveMissionProgress();
    }

    private void SaveMissionProgress()
    {
        if (string.IsNullOrEmpty(_attendanceRowInDate)) return;

        Param param = new Param();
        param.Add("mission_kill", MissionKillCount);
        param.Add("mission_stage", MissionStageCount);

        Backend.GameData.UpdateV2(AttendanceTable, _attendanceRowInDate, Backend.UserInDate, param, bro =>
        {
            if (!bro.IsSuccess())
                Debug.LogWarning($"[Guild] 미션 진행도 저장 실패: {bro.GetMessage()}");
        });
    }

    public bool IsMissionComplete => MissionKillCount >= missionKillTarget && MissionStageCount >= missionStageTarget;

    public void ClaimMissionReward(Action<bool, string> callback = null)
    {
        if (!IsMissionComplete) { callback?.Invoke(false, "미션을 아직 완료하지 못했습니다."); return; }

        if (string.IsNullOrEmpty(_attendanceRowInDate)) { callback?.Invoke(false, "출석 데이터 오류"); return; }

        // 보상 지급
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(missionGoldReward);
            GameManager.Instance.AddGem(missionGemReward);
        }

        // 보상 수령 표시
        Param param = new Param();
        param.Add("rewarded", true);

        Backend.GameData.UpdateV2(AttendanceTable, _attendanceRowInDate, Backend.UserInDate, param, bro =>
        {
            string msg = $"미션 보상 수령!\n골드 +{UIManager.FormatKoreanUnit(missionGoldReward)}\n젬 +{missionGemReward}";
            callback?.Invoke(true, msg);
        });
    }

    // 미션 목표치 getter
    public int GetMissionKillTarget() => missionKillTarget;
    public int GetMissionStageTarget() => missionStageTarget;

    // ═══════════════════════════════════════
    //  로그인 시 초기화
    // ═══════════════════════════════════════

    public void InitAfterLogin()
    {
        LoadMyGuildInfo(() =>
        {
            if (IsInGuild)
            {
                UpdateGuildCombatPower();
                LoadTodayAttendance();
            }
        });
    }

    private void LoadTodayAttendance()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        Where where = new Where();
        where.Equal("date", today);
        where.Equal("guild_indate", MyGuild.guildInDate);

        Backend.GameData.GetMyData(AttendanceTable, where, bro =>
        {
            if (bro.IsSuccess())
            {
                JsonData rows = bro.FlattenRows();
                if (rows != null && rows.Count > 0)
                {
                    TodayAttended = true;
                    _attendanceRowInDate = rows[0]["inDate"]?.ToString();
                    MissionKillCount = rows[0].ContainsKey("mission_kill") ? (int)rows[0]["mission_kill"] : 0;
                    MissionStageCount = rows[0].ContainsKey("mission_stage") ? (int)rows[0]["mission_stage"] : 0;
                }
            }
        });
    }

    // ═══════════════════════════════════════
    //  파싱 헬퍼
    // ═══════════════════════════════════════

    private GuildInfo ParseGuildInfo(JsonData json)
    {
        var info = new GuildInfo();
        info.guildName = json.ContainsKey("guildName") ? json["guildName"]?.ToString() : "";
        info.guildInDate = json.ContainsKey("inDate") ? json["inDate"]?.ToString() : "";
        info.masterNickname = json.ContainsKey("masterNickname") ? json["masterNickname"]?.ToString() : "";
        info.memberCount = json.ContainsKey("memberCount") ? (int)json["memberCount"] : 0;
        info.description = "";

        if (json.ContainsKey("goods"))
        {
            JsonData goods = json["goods"];
            if (goods.ContainsKey("description"))
                info.description = goods["description"]?.ToString() ?? "";
            if (goods.ContainsKey("level"))
                info.guildLevel = (int)goods["level"];
            if (goods.ContainsKey("totalCombatPower"))
                info.totalCombatPower = (long)goods["totalCombatPower"];
        }

        return info;
    }

    private GuildMemberRole ParseMyRole(JsonData json)
    {
        if (json.ContainsKey("position"))
        {
            string pos = json["position"]?.ToString();
            if (pos == "master") return GuildMemberRole.Master;
            if (pos == "viceMaster") return GuildMemberRole.ViceMaster;
        }
        return GuildMemberRole.Member;
    }

    private GuildMemberRole ParseMemberRole(JsonData memberJson)
    {
        if (memberJson.ContainsKey("position"))
        {
            string pos = memberJson["position"]?.ToString();
            if (pos == "master") return GuildMemberRole.Master;
            if (pos == "viceMaster") return GuildMemberRole.ViceMaster;
        }
        return GuildMemberRole.Member;
    }

    private string ParseGuildError(BackendReturnObject bro, string action)
    {
        string statusCode = bro.GetStatusCode();
        string msg;
        switch (statusCode)
        {
            case "409": msg = "이미 존재하는 길드 이름입니다."; break;
            case "404": msg = "길드를 찾을 수 없습니다."; break;
            case "403": msg = "권한이 없습니다."; break;
            case "400":
                string errorCode = bro.GetErrorCode();
                if (errorCode.Contains("Already")) msg = "이미 가입 신청했거나 가입된 상태입니다.";
                else if (errorCode.Contains("Full")) msg = "길드 인원이 가득 찼습니다.";
                else msg = $"{action} 실패: {bro.GetMessage()}";
                break;
            default: msg = $"{action} 실패: {bro.GetMessage()}"; break;
        }
        Debug.LogWarning($"[Guild] {action} 실패 — {statusCode}: {bro.GetMessage()}");
        return msg;
    }
}

// ═══════════════════════════════════════
//  데이터 클래스
// ═══════════════════════════════════════

[System.Serializable]
public class GuildInfo
{
    public string guildName;
    public string guildInDate;
    public string masterNickname;
    public string description;
    public int memberCount;
    public int guildLevel;
    public long totalCombatPower;
}

[System.Serializable]
public class GuildMemberData
{
    public string nickname;
    public string inDate;
    public GuildMemberRole role;
    public int combatPower;
}

[System.Serializable]
public class GuildApplicantData
{
    public string nickname;
    public string inDate;
}

public enum GuildMemberRole
{
    Member = 0,
    ViceMaster = 1,
    Master = 2
}
