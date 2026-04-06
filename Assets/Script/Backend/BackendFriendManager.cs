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
                JsonData rows = bro.FlattenRows();
                if (rows != null)
                {
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var friend = new FriendData
                        {
                            nickname = rows[i].ContainsKey("nickname") ? rows[i]["nickname"]?.ToString() : "???",
                            inDate = rows[i].ContainsKey("inDate") ? rows[i]["inDate"]?.ToString() : "",
                            sentToday = _todaySentSet.Contains(rows[i].ContainsKey("inDate") ? rows[i]["inDate"]?.ToString() : ""),
                        };
                        FriendList.Add(friend);
                    }
                }
                Debug.Log($"[Friend] 친구 목록 로드: {FriendList.Count}명");
                OnFriendListLoaded?.Invoke(FriendList);
                callback?.Invoke(FriendList);
            }
            else
            {
                Debug.LogWarning($"[Friend] 친구 목록 로드 실패: {bro.GetMessage()}");
                callback?.Invoke(FriendList);
            }
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
    //  유저 검색 (닉네임)
    // ═══════════════════════════════════════

    public void SearchUser(string nickname, Action<List<FriendSearchResult>> callback = null)
    {
        if (string.IsNullOrEmpty(nickname))
        {
            OnFriendError?.Invoke("닉네임을 입력하세요.");
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
                        // 이미 친구인지 확인
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
    //  친구 요청 보내기
    // ═══════════════════════════════════════

    public void SendFriendRequest(string targetInDate, Action<bool, string> callback = null)
    {
        if (FriendList.Count >= maxFriends)
        {
            callback?.Invoke(false, $"친구 최대 {maxFriends}명까지만 가능합니다.");
            return;
        }

        Backend.Friend.RequestFriend(targetInDate, bro =>
        {
            if (bro.IsSuccess())
            {
                Debug.Log($"[Friend] 친구 요청 전송: {targetInDate}");
                callback?.Invoke(true, "친구 요청을 보냈습니다!");
                OnFriendMessage?.Invoke("친구 요청을 보냈습니다!");
            }
            else
            {
                string msg = ParseFriendError(bro, "친구 요청");
                callback?.Invoke(false, msg);
            }
        });
    }

    // ═══════════════════════════════════════
    //  친구 수락 / 거절
    // ═══════════════════════════════════════

    public void AcceptRequest(string requesterInDate, Action<bool> callback = null)
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
                FriendList.RemoveAll(f => f.inDate == friendInDate);
                LoadFriendList();
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
        if (HasSentToday(friendInDate))
        {
            callback?.Invoke(false, "오늘 이미 포인트를 보냈습니다.");
            return;
        }

        Param param = new Param();
        param.Add("sender_indate", Backend.UserInDate);
        param.Add("receiver_indate", friendInDate);
        param.Add("date", _todayDate);
        param.Add("amount", pointPerSend);
        param.Add("claimed", false);

        Backend.GameData.Insert(PointTable, param, bro =>
        {
            if (bro.IsSuccess())
            {
                _todaySentSet.Add(friendInDate);
                SaveTodaySentList();

                // 보낸 친구 상태 갱신
                var friend = FriendList.Find(f => f.inDate == friendInDate);
                if (friend != null) friend.sentToday = true;

                Debug.Log($"[Friend] 우정포인트 전송: {friendInDate} (+{pointPerSend})");
                OnFriendMessage?.Invoke($"우정포인트 {pointPerSend}P를 보냈습니다!");
                callback?.Invoke(true, $"우정포인트 {pointPerSend}P를 보냈습니다!");
            }
            else
            {
                callback?.Invoke(false, "포인트 전송 실패");
            }
        });
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
        Where where = new Where();
        where.Equal("receiver_indate", Backend.UserInDate);
        where.Equal("claimed", false);

        Backend.GameData.Get(PointTable, where, bro =>
        {
            if (!bro.IsSuccess())
            {
                callback?.Invoke(0);
                return;
            }

            JsonData rows = bro.FlattenRows();
            if (rows == null || rows.Count == 0)
            {
                OnFriendMessage?.Invoke("수령할 포인트가 없습니다.");
                callback?.Invoke(0);
                return;
            }

            int totalPoints = 0;
            int processed = 0;
            int total = rows.Count;

            for (int i = 0; i < rows.Count; i++)
            {
                int amount = rows[i].ContainsKey("amount") ? (int)rows[i]["amount"] : pointPerSend;
                string rowInDate = rows[i]["inDate"]?.ToString();
                string ownerInDate = rows[i].ContainsKey("owner_inDate") ? rows[i]["owner_inDate"]?.ToString() : "";
                totalPoints += amount;

                // claimed = true로 변경
                Param updateParam = new Param();
                updateParam.Add("claimed", true);

                Backend.GameData.UpdateV2(PointTable, rowInDate, ownerInDate, updateParam, updateBro =>
                {
                    processed++;
                    if (processed >= total)
                    {
                        FriendPoint += totalPoints;
                        SaveFriendPoint();

                        Debug.Log($"[Friend] 우정포인트 수령: +{totalPoints} (총 {FriendPoint})");
                        OnFriendMessage?.Invoke($"우정포인트 +{totalPoints}P 수령! (총 {FriendPoint}P)");
                        OnFriendPointReceived?.Invoke(totalPoints);
                        callback?.Invoke(totalPoints);
                    }
                });
            }
        });
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
    //  에러 파싱
    // ═══════════════════════════════════════

    private string ParseFriendError(BackendReturnObject bro, string action)
    {
        string statusCode = bro.GetStatusCode();
        string msg;
        switch (statusCode)
        {
            case "409": msg = "이미 친구이거나 요청을 보낸 상태입니다."; break;
            case "404": msg = "유저를 찾을 수 없습니다."; break;
            case "400":
                string errorCode = bro.GetErrorCode();
                if (errorCode.Contains("Self")) msg = "자기 자신에게는 요청할 수 없습니다.";
                else if (errorCode.Contains("Full")) msg = "친구 목록이 가득 찼습니다.";
                else msg = $"{action} 실패: {bro.GetMessage()}";
                break;
            default: msg = $"{action} 실패: {bro.GetMessage()}"; break;
        }
        Debug.LogWarning($"[Friend] {action} 실패 — {statusCode}: {bro.GetMessage()}");
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
}

[System.Serializable]
public class FriendRequestData
{
    public string nickname;
    public string inDate;
}

[System.Serializable]
public class FriendSearchResult
{
    public string nickname;
    public string inDate;
    public bool isAlreadyFriend;
}
