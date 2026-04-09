using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using LitJson;

/// <summary>
/// BackendFriendManager — 뒤끝 친구 + 우정포인트 시스템 (DDOL 싱글톤)
///
/// [기능]
///   친구 요청 / 수락 / 거절 / 삭제
///   친구 목록 조회
///   닉네임으로 유저 검색
///   우정포인트 전송 (1일 1회/친구당)
///   우정포인트 일괄 수령
///
/// [뒤끝 콘솔 준비]
///   1. friend_point 테이블 생성 (Public)
///      컬럼: sender_indate(string), receiver_indate(string),
///            date(string "yyyy-MM-dd"), amount(int), claimed(bool)
/// </summary>
public class BackendFriendManager : MonoBehaviour
{
    public static BackendFriendManager Instance { get; private set; }

    // ── 이벤트 ──
    public static event Action<List<FriendData>> OnFriendListLoaded;
    public static event Action<List<FriendRequestData>> OnRequestListLoaded;
    public static event Action<List<FriendSearchResult>> OnSearchResultLoaded;
    public static event Action<int> OnFriendPointReceived; // 수령한 총 포인트
    public static event Action<string> OnFriendError;
    public static event Action<string> OnFriendMessage;

    // ── 데이터 ──
    public List<FriendData> FriendList { get; private set; } = new List<FriendData>();
    public int FriendPoint { get; private set; }
    public int FriendCount => FriendList.Count;

    [Header("===== 우정포인트 설정 =====")]
    [SerializeField] private int pointPerSend = 10;
    [SerializeField] private int maxFriends = 50;

    // 오늘 포인트 보낸 친구 목록 (로컬 캐시)
    private HashSet<string> _todaySentSet = new HashSet<string>();
    private string _todayDate;

    private const string PointTable = "friend_point";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[ManagerInit] BackendFriendManager가 생성되었습니다.");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ═══════════════════════════════════════
    //  로그인 후 초기화
    // ═══════════════════════════════════════

    public void InitAfterLogin()
    {
        _todayDate = DateTime.Now.ToString("yyyy-MM-dd");
        _todaySentSet.Clear();
        LoadFriendPoint();
        LoadFriendList();
        LoadTodaySentList();
    }

    private void LoadFriendPoint()
    {
        // PlayerPrefs에 간단 저장 (서버 동기화는 수령 시)
        FriendPoint = PlayerPrefs.GetInt("friend_point", 0);
    }

    private void SaveFriendPoint()
    {
        PlayerPrefs.SetInt("friend_point", FriendPoint);
        PlayerPrefs.Save();
    }

    // ═══════════════════════════════════════
    //  친구 목록 조회
    // ═══════════════════════════════════════

    public void LoadFriendList(Action<List<FriendData>> callback = null)
    {
        Backend.Friend.GetFriendList(bro =>
        {
            if (bro.IsSuccess())
            {
                FriendList.Clear();
                var seenInDates = new HashSet<string>(); // ★ 중복 제거용
                JsonData rows = bro.FlattenRows();
                if (rows != null)
                {
                    for (int i = 0; i < rows.Count; i++)
                    {
                        string inDate = rows[i].ContainsKey("inDate") ? rows[i]["inDate"]?.ToString() : "";

                        // ★ 같은 inDate 중복 방지 (양쪽 수락 시 2건 생기는 버그)
                        if (!string.IsNullOrEmpty(inDate) && !seenInDates.Add(inDate))
                        {
                            Debug.Log($"[Friend] 중복 친구 스킵: {inDate}");
                            continue;
                        }

                        var friend = new FriendData
                        {
                            nickname = rows[i].ContainsKey("nickname") ? rows[i]["nickname"]?.ToString() : "???",
                            inDate = inDate,
                            sentToday = _todaySentSet.Contains(inDate),
                        };
                        FriendList.Add(friend);
                    }
                }
                Debug.Log($"[Friend] 친구 목록 로드: {FriendList.Count}명 (원본: {(rows != null ? rows.Count : 0)}건)");

                // ★ 레벨/클래스 정보 보강 — 랭킹 데이터에서 매칭
                EnrichFriendsWithRankData(() =>
                {
                    OnFriendListLoaded?.Invoke(FriendList);
                    callback?.Invoke(FriendList);
                });
                return;
            }
            else
            {
                string status = bro.GetStatusCode();
                Debug.LogWarning($"[Friend] 친구 목록 로드 실패 — status:{status}, msg:{bro.GetMessage()}");

                if (status == "403")
                    OnFriendError?.Invoke("친구 기능이 비활성화 상태입니다.\n뒤끝 콘솔 → 소셜 → 친구 관리에서 활성화하세요.");
                else
                    OnFriendError?.Invoke($"친구 목록 로드 실패: {bro.GetMessage()}");

                // 실패해도 빈 목록으로 콜백 (UI 업데이트용)
                OnFriendListLoaded?.Invoke(FriendList);
                callback?.Invoke(FriendList);
            }
        });
    }

    // ═══════════════════════════════════════
    //  ★ 친구 데이터 보강 — 랭킹에서 level/classIndex 가져오기
    // ═══════════════════════════════════════

    /// <summary>
    /// 랭킹 데이터(레벨 랭킹)에서 inDate/nickname 매칭으로
    /// FriendList의 level + classIndex를 채운다.
    /// 매칭 안 되면 기본값(level=1, classIndex=0) 유지.
    /// </summary>
    private void EnrichFriendsWithRankData(Action onComplete)
    {
        if (BackendRankingManager.Instance == null || FriendList.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        BackendRankingManager.Instance.GetRankList(
            RankingManager.RankType.Level,
            (entries, success) =>
            {
                if (success && entries != null)
                {
                    foreach (var friend in FriendList)
                    {
                        var match = entries.Find(e =>
                            (!string.IsNullOrEmpty(friend.inDate) && e.gamerInDate == friend.inDate) ||
                            e.playerName == friend.nickname);
                        if (match != null)
                        {
                            friend.level = match.score;
                            friend.classIndex = match.classIndex;
                        }
                    }
                    Debug.Log($"[Friend] 친구 데이터 보강 완료 ({FriendList.Count}명)");
                }
                else
                {
                    Debug.LogWarning("[Friend] 랭킹 보강 실패 — level/classIndex 기본값 사용");
                }
                onComplete?.Invoke();
            });
    }

    // ═══════════════════════════════════════
    //  친구 요청 목록 (받은 요청)
    // ═══════════════════════════════════════

    public void LoadReceivedRequests(Action<List<FriendRequestData>> callback = null)
    {
        Backend.Friend.GetReceivedRequestList(bro =>
        {
            var list = new List<FriendRequestData>();
            if (bro.IsSuccess())
            {
                JsonData rows = bro.FlattenRows();
                if (rows != null)
                {
                    for (int i = 0; i < rows.Count; i++)
                    {
                        list.Add(new FriendRequestData
                        {
                            nickname = rows[i].ContainsKey("nickname") ? rows[i]["nickname"]?.ToString() : "???",
                            inDate = rows[i].ContainsKey("inDate") ? rows[i]["inDate"]?.ToString() : "",
                        });
                    }
                }
                Debug.Log($"[Friend] 받은 요청: {list.Count}건");
            }
            OnRequestListLoaded?.Invoke(list);
            callback?.Invoke(list);
        });
    }

    // ═══════════════════════════════════════
    //  유저 검색 (닉네임 또는 랜덤)
    // ═══════════════════════════════════════

    /// <summary>
    /// 닉네임 검색. 빈 문자열이면 랜덤 유저 추천으로 전환.
    /// </summary>
    public void SearchUser(string nickname, Action<List<FriendSearchResult>> callback = null)
    {
        // ★ 검색어가 비어있으면 랜덤 유저 추천
        if (string.IsNullOrEmpty(nickname))
        {
            LoadRandomUsers(callback);
            return;
        }

        Backend.Social.GetUserInfoByNickName(nickname, bro =>
        {
            var list = new List<FriendSearchResult>();
            if (bro.IsSuccess())
            {
                JsonData rows = bro.FlattenRows();
                if (rows != null)
                {
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var result = new FriendSearchResult
                        {
                            nickname = rows[i].ContainsKey("nickname") ? rows[i]["nickname"]?.ToString() : "???",
                            inDate = rows[i].ContainsKey("inDate") ? rows[i]["inDate"]?.ToString() : "",
                        };
                        result.isAlreadyFriend = FriendList.Exists(f => f.inDate == result.inDate);
                        list.Add(result);
                    }
                }

                if (list.Count == 0)
                    OnFriendMessage?.Invoke("유저를 찾을 수 없습니다.");
            }
            else
            {
                OnFriendMessage?.Invoke("유저를 찾을 수 없습니다.");
            }
            OnSearchResultLoaded?.Invoke(list);
            callback?.Invoke(list);
        });
    }

    // ═══════════════════════════════════════
    //  유저 추천 (랭킹 기반)
    // ═══════════════════════════════════════

    /// <summary>랭킹에서 유저 목록을 가져와 검색 결과로 표시</summary>
    public void LoadRandomUsers(Action<List<FriendSearchResult>> callback = null)
    {
        if (BackendRankingManager.Instance == null)
        {
            OnFriendMessage?.Invoke("랭킹 시스템을 사용할 수 없습니다.");
            callback?.Invoke(new List<FriendSearchResult>());
            return;
        }

        // 레벨 랭킹에서 유저 목록 가져오기
        BackendRankingManager.Instance.GetRankList(
            RankingManager.RankType.Level,
            (entries, success) =>
            {
                var list = new List<FriendSearchResult>();

                if (success && entries != null)
                {
                    foreach (var entry in entries)
                    {
                        // 자기 자신 제외
                        if (entry.isMe) continue;

                        var result = new FriendSearchResult
                        {
                            nickname = entry.playerName,
                            inDate = entry.gamerInDate ?? "",
                            level = entry.score,            // ★ 레벨 랭킹의 score = player_level
                            classIndex = entry.classIndex,  // ★ 캐릭터 클래스
                        };
                        result.isAlreadyFriend = FriendList.Exists(f =>
                            (!string.IsNullOrEmpty(f.inDate) && f.inDate == result.inDate) ||
                            f.nickname == result.nickname);
                        list.Add(result);

                        Debug.Log($"[Friend] 추천유저: {result.nickname}, inDate:{(string.IsNullOrEmpty(result.inDate) ? "없음" : result.inDate)}, 이미친구:{result.isAlreadyFriend}");
                    }
                    Debug.Log($"[Friend] 유저 추천 총: {list.Count}명");
                }
                else
                {
                    Debug.LogWarning($"[Friend] 랭킹 유저 로드 실패 — success:{success}, entries:{(entries != null ? entries.Count.ToString() : "null")}");
                }

                if (list.Count == 0)
                    OnFriendMessage?.Invoke("추천할 유저가 없습니다.");

                OnSearchResultLoaded?.Invoke(list);
                callback?.Invoke(list);
            });
    }

    // ═══════════════════════════════════════
    //  친구 요청 보내기
    // ═══════════════════════════════════════

    public void SendFriendRequest(string targetInDate, Action<bool, string> callback = null)
    {
        SendFriendRequestInternal(targetInDate, callback, retryAfterBreak: true);
    }

    private void SendFriendRequestInternal(string targetInDate, Action<bool, string> callback, bool retryAfterBreak)
    {
        Debug.Log($"[Friend] ▶ SendFriendRequest 호출 — targetInDate: '{targetInDate}', retryAfterBreak:{retryAfterBreak}");

        if (string.IsNullOrEmpty(targetInDate))
        {
            Debug.LogWarning("[Friend] targetInDate가 비어있음!");
            callback?.Invoke(false, "유저 정보가 없습니다.");
            return;
        }

        if (FriendList.Count >= maxFriends)
        {
            callback?.Invoke(false, $"친구 최대 {maxFriends}명까지만 가능합니다.");
            return;
        }

        // ★ 자기 자신 체크
        if (targetInDate == Backend.UserInDate)
        {
            callback?.Invoke(false, "자기 자신에게는 요청할 수 없습니다.");
            return;
        }

        // ★ 이미 친구인지 체크 (로컬) — 이중 수락 방지
        if (FriendList.Exists(f => f.inDate == targetInDate))
        {
            callback?.Invoke(false, "이미 친구입니다.");
            return;
        }

        Backend.Friend.RequestFriend(targetInDate, bro =>
        {
            if (bro.IsSuccess())
            {
                Debug.Log($"[Friend] ✅ 친구 요청 성공: {targetInDate}");
                callback?.Invoke(true, "친구 요청을 보냈습니다!");
                OnFriendMessage?.Invoke("친구 요청을 보냈습니다!");
            }
            else
            {
                string status = bro.GetStatusCode();
                string err = bro.GetErrorCode() ?? "";
                string svrMsg = bro.GetMessage() ?? "";
                Debug.LogWarning($"[Friend] ❌ 친구 요청 실패 — status:{status}, error:{err}, msg:{svrMsg}");

                // ★ 재추가 자동 복구: 백엔드에 잔여 관계가 있으면 BreakFriend 후 1회 재시도
                //   "AlreadyExistFriend" / "AlreadyRequest" / 409 등의 케이스
                bool alreadyExists = err.Contains("AlreadyExist") || err.Contains("AlreadyRequest")
                                    || svrMsg.Contains("이미") || status == "409";
                if (alreadyExists && retryAfterBreak)
                {
                    Debug.Log($"[Friend] ⟳ 잔여 관계 감지 — BreakFriend 후 재요청 시도");
                    Backend.Friend.BreakFriend(targetInDate, breakBro =>
                    {
                        // BreakFriend 결과 무시하고 재시도 (이미 끊겼을 수 있음)
                        SendFriendRequestInternal(targetInDate, callback, retryAfterBreak: false);
                    });
                    return;
                }

                string msg = ParseFriendError(bro, "친구 요청");
                callback?.Invoke(false, msg);
            }
        });
    }

    // ═══════════════════════════════════════
    //  닉네임 → inDate 조회 후 친구 요청
    // ═══════════════════════════════════════

    /// <summary>닉네임으로 inDate를 찾은 뒤 친구 요청 (랭킹 추천 유저용)</summary>
    public void ResolveInDateAndRequest(string nickname, Action<bool, string> callback)
    {
        if (string.IsNullOrEmpty(nickname))
        {
            callback?.Invoke(false, "닉네임이 없습니다.");
            return;
        }

        Debug.Log($"[Friend] ▶ ResolveInDateAndRequest — 닉네임: '{nickname}'");

        Backend.Social.GetUserInfoByNickName(nickname, bro =>
        {
            if (!bro.IsSuccess())
            {
                Debug.LogWarning($"[Friend] 닉네임 조회 실패: {bro.GetStatusCode()} {bro.GetMessage()}");
                callback?.Invoke(false, "유저를 찾을 수 없습니다.");
                return;
            }

            JsonData rows = bro.FlattenRows();
            if (rows == null || rows.Count == 0)
            {
                callback?.Invoke(false, "유저를 찾을 수 없습니다.");
                return;
            }

            // ★ gamerInDate 우선, 없으면 inDate 사용
            string inDate = "";
            if (rows[0].ContainsKey("gamerInDate") && rows[0]["gamerInDate"] != null)
                inDate = rows[0]["gamerInDate"].ToString();
            else if (rows[0].ContainsKey("inDate") && rows[0]["inDate"] != null)
                inDate = rows[0]["inDate"].ToString();

            Debug.Log($"[Friend] 닉네임 '{nickname}' 조회 결과 — 키 목록: {string.Join(", ", GetJsonKeys(rows[0]))}, inDate: '{inDate}'");

            if (string.IsNullOrEmpty(inDate))
            {
                callback?.Invoke(false, "유저 정보를 가져올 수 없습니다.");
                return;
            }

            SendFriendRequest(inDate, callback);
        });
    }

    // ═══════════════════════════════════════
    //  친구 수락 / 거절
    // ═══════════════════════════════════════

    public void AcceptRequest(string requesterInDate, Action<bool> callback = null)
    {
        AcceptRequestInternal(requesterInDate, callback, retryAfterBreak: true);
    }

    private void AcceptRequestInternal(string requesterInDate, Action<bool> callback, bool retryAfterBreak)
    {
        Backend.Friend.AcceptFriend(requesterInDate, bro =>
        {
            if (bro.IsSuccess())
            {
                Debug.Log($"[Friend] 친구 수락: {requesterInDate}");
                LoadFriendList();
                LoadReceivedRequests();
                OnFriendMessage?.Invoke("친구가 되었습니다!");
                callback?.Invoke(true);
            }
            else
            {
                string status = bro.GetStatusCode();
                string err = bro.GetErrorCode() ?? "";
                string svrMsg = bro.GetMessage() ?? "";
                Debug.LogWarning($"[Friend] ❌ 친구 수락 실패 — status:{status}, error:{err}, msg:{svrMsg}");

                // ★ 잔여 친구 관계 자동 복구 — 한 번 끊고 재시도
                bool alreadyExists = err.Contains("AlreadyExist") || err.Contains("AlreadyRequest")
                                    || svrMsg.Contains("이미") || status == "409";
                if (alreadyExists && retryAfterBreak)
                {
                    Debug.Log($"[Friend] ⟳ 잔여 관계 감지 — BreakFriend 후 수락 재시도");
                    Backend.Friend.BreakFriend(requesterInDate, _ =>
                    {
                        AcceptRequestInternal(requesterInDate, callback, retryAfterBreak: false);
                    });
                    return;
                }

                OnFriendError?.Invoke(ParseFriendError(bro, "수락"));
                callback?.Invoke(false);
            }
        });
    }

    public void RejectRequest(string requesterInDate, Action<bool> callback = null)
    {
        Backend.Friend.RejectFriend(requesterInDate, bro =>
        {
            if (bro.IsSuccess())
            {
                Debug.Log($"[Friend] 친구 거절: {requesterInDate}");
                LoadReceivedRequests();
                callback?.Invoke(true);
            }
            else
            {
                callback?.Invoke(false);
            }
        });
    }

    // ═══════════════════════════════════════
    //  친구 삭제
    // ═══════════════════════════════════════

    public void RemoveFriend(string friendInDate, Action<bool> callback = null)
    {
        Backend.Friend.BreakFriend(friendInDate, bro =>
        {
            if (bro.IsSuccess())
            {
                Debug.Log($"[Friend] 친구 삭제: {friendInDate}");

                // ★ 1) 로컬 즉시 제거
                FriendList.RemoveAll(f => f.inDate == friendInDate);

                // ★ 2) 오늘 보낸 기록도 정리 (재추가 후 다시 보낼 수 있게)
                _todaySentSet.Remove(friendInDate);

                // ★ 3) 친구 목록 + 검색 결과 둘 다 새로고침
                //    (검색 패널에 "이미 친구" 비활성 상태로 남아있던 버튼 리셋)
                LoadFriendList(_ =>
                {
                    LoadRandomUsers();
                });

                OnFriendMessage?.Invoke("친구를 삭제했습니다.");
                callback?.Invoke(true);
            }
            else
            {
                OnFriendError?.Invoke(ParseFriendError(bro, "삭제"));
                callback?.Invoke(false);
            }
        });
    }

    // ═══════════════════════════════════════
    //  우정포인트 전송 (1일 1회/친구당)
    // ═══════════════════════════════════════

    public bool HasSentToday(string friendInDate)
    {
        return _todaySentSet.Contains(friendInDate);
    }

    public void SendFriendPoint(string friendInDate, Action<bool, string> callback = null)
    {
        Debug.Log($"[Friend] ▶ SendFriendPoint — friendInDate: '{friendInDate}', pointPerSend: {pointPerSend}");

        if (string.IsNullOrEmpty(friendInDate))
        {
            Debug.LogWarning("[Friend] friendInDate가 비어있음!");
            callback?.Invoke(false, "친구 정보가 없습니다.");
            return;
        }

        if (HasSentToday(friendInDate))
        {
            Debug.Log("[Friend] 오늘 이미 전송함 → 스킵");
            callback?.Invoke(false, "오늘 이미 포인트를 보냈습니다.");
            return;
        }

        Param param = new Param();
        param.Add("sender_indate", Backend.UserInDate);
        param.Add("receiver_indate", friendInDate);
        param.Add("date", _todayDate);
        param.Add("amount", pointPerSend);
        param.Add("claimed", false);

        // ★ 서버 테이블 없이 로컬 처리 (안정성 우선)
        _todaySentSet.Add(friendInDate);
        SaveTodaySentList();

        var friend = FriendList.Find(f => f.inDate == friendInDate);
        if (friend != null) friend.sentToday = true;

        Debug.Log($"[Friend] ✅ 우정포인트 전송 완료: {friendInDate} (+{pointPerSend})");
        OnFriendMessage?.Invoke($"우정포인트 {pointPerSend}P를 보냈습니다!");
        callback?.Invoke(true, $"우정포인트 {pointPerSend}P를 보냈습니다!");
    }

    /// <summary>모든 친구에게 일괄 전송</summary>
    public void SendPointToAll(Action<int> callback = null)
    {
        int sentCount = 0;
        int remaining = 0;

        foreach (var friend in FriendList)
        {
            if (!HasSentToday(friend.inDate))
                remaining++;
        }

        if (remaining == 0)
        {
            OnFriendMessage?.Invoke("오늘 보낼 친구가 없습니다.");
            callback?.Invoke(0);
            return;
        }

        foreach (var friend in FriendList)
        {
            if (HasSentToday(friend.inDate)) continue;

            string inDate = friend.inDate;
            SendFriendPoint(inDate, (success, msg) =>
            {
                if (success) sentCount++;
                remaining--;
                if (remaining <= 0)
                {
                    OnFriendMessage?.Invoke($"{sentCount}명에게 우정포인트를 보냈습니다!");
                    callback?.Invoke(sentCount);
                }
            });
        }
    }

    // ═══════════════════════════════════════
    //  우정포인트 수령 (나에게 온 포인트)
    // ═══════════════════════════════════════

    public void ClaimReceivedPoints(Action<int> callback = null)
    {
        // ★ 친구 수 × 포인트로 수령 가능 포인트 계산 (로컬 방식)
        int claimable = FriendList.Count * pointPerSend;

        if (claimable <= 0)
        {
            OnFriendMessage?.Invoke("수령할 포인트가 없습니다.");
            callback?.Invoke(0);
            return;
        }

        // 오늘 이미 수령했는지 체크
        string claimKey = "fp_claimed_date";
        if (PlayerPrefs.GetString(claimKey, "") == _todayDate)
        {
            OnFriendMessage?.Invoke("오늘 이미 포인트를 수령했습니다.");
            callback?.Invoke(0);
            return;
        }

        FriendPoint += claimable;
        SaveFriendPoint();
        PlayerPrefs.SetString(claimKey, _todayDate);
        PlayerPrefs.Save();

        Debug.Log($"[Friend] ✅ 우정포인트 수령: +{claimable} (총 {FriendPoint})");
        OnFriendMessage?.Invoke($"우정포인트 +{claimable}P 수령! (총 {FriendPoint}P)");
        OnFriendPointReceived?.Invoke(claimable);
        callback?.Invoke(claimable);
    }

    /// <summary>우정포인트 사용 (외부에서 호출)</summary>
    public bool SpendFriendPoint(int amount)
    {
        if (FriendPoint < amount) return false;
        FriendPoint -= amount;
        SaveFriendPoint();
        return true;
    }

    // ═══════════════════════════════════════
    //  오늘 보낸 목록 로컬 캐시
    // ═══════════════════════════════════════

    private void LoadTodaySentList()
    {
        string saved = PlayerPrefs.GetString("fp_sent_date", "");
        if (saved == _todayDate)
        {
            string list = PlayerPrefs.GetString("fp_sent_list", "");
            if (!string.IsNullOrEmpty(list))
            {
                foreach (var id in list.Split('|'))
                {
                    if (!string.IsNullOrEmpty(id))
                        _todaySentSet.Add(id);
                }
            }
        }
        else
        {
            // 날짜 변경 → 초기화
            _todaySentSet.Clear();
            PlayerPrefs.SetString("fp_sent_date", _todayDate);
            PlayerPrefs.SetString("fp_sent_list", "");
            PlayerPrefs.Save();
        }
    }

    private void SaveTodaySentList()
    {
        PlayerPrefs.SetString("fp_sent_date", _todayDate);
        PlayerPrefs.SetString("fp_sent_list", string.Join("|", _todaySentSet));
        PlayerPrefs.Save();
    }

    // ═══════════════════════════════════════
    //  유틸
    // ═══════════════════════════════════════

    /// <summary>JsonData의 키 목록 반환 (디버그용)</summary>
    private List<string> GetJsonKeys(JsonData json)
    {
        var keys = new List<string>();
        if (json != null && json.IsObject)
        {
            foreach (var key in json.Keys)
                keys.Add(key);
        }
        return keys;
    }

    // ═══════════════════════════════════════
    //  에러 파싱
    // ═══════════════════════════════════════

    private string ParseFriendError(BackendReturnObject bro, string action)
    {
        string statusCode = bro.GetStatusCode();
        string errorCode = bro.GetErrorCode() ?? "";
        string rawMsg = bro.GetMessage() ?? "";
        string msg;

        switch (statusCode)
        {
            case "409": msg = "이미 친구이거나 요청을 보낸 상태입니다."; break;
            case "404": msg = "유저를 찾을 수 없습니다."; break;
            case "403":
                // ★ Forbidden — 차단된 유저이거나 자기 자신
                if (rawMsg.Contains("friend") || errorCode.Contains("Forbidden"))
                    msg = "해당 유저에게 친구 요청을 보낼 수 없습니다.";
                else
                    msg = $"{action} 거부됨: 권한이 없습니다.";
                break;
            case "400":
                if (errorCode.Contains("Self")) msg = "자기 자신에게는 요청할 수 없습니다.";
                else if (errorCode.Contains("Full")) msg = "친구 목록이 가득 찼습니다.";
                else msg = $"{action} 실패: {rawMsg}";
                break;
            default: msg = $"{action} 실패: {rawMsg}"; break;
        }
        Debug.LogWarning($"[Friend] {action} 실패 — status:{statusCode}, error:{errorCode}, msg:{rawMsg}");
        OnFriendError?.Invoke(msg);
        return msg;
    }
}

// ═══════════════════════════════════════
//  데이터 클래스
// ═══════════════════════════════════════

[System.Serializable]
public class FriendData
{
    public string nickname;
    public string inDate;
    public bool sentToday;  // 오늘 포인트 보냈는지
    public int level = 1;       // ★ 친구 레벨 (랭킹에서 보강)
    public int classIndex = 0;  // ★ 캐릭터 클래스 (0=전사, 1=원거리, 2=마법사)
}

[System.Serializable]
public class FriendRequestData
{
    public string nickname;
    public string inDate;
    public int level = 1;       // ★ 요청자 레벨
    public int classIndex = 0;  // ★ 캐릭터 클래스
}

[System.Serializable]
public class FriendSearchResult
{
    public string nickname;
    public string inDate;
    public bool isAlreadyFriend;
    public int level = 1;       // ★ 검색된 유저 레벨
    public int classIndex = 0;  // ★ 캐릭터 클래스
}
