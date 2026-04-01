using System;
using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using LitJson;

/// <summary>
/// ══════════════════════════════════════════════════════════
/// BackendPostManager — 뒤끝 우편(Post) 시스템 연동
/// ══════════════════════════════════════════════════════════
///
/// [수정 내역]
///   Bug1: Backend.UPost.User.GetPostList
///         → Backend.UPost.GetPostList 로 수정
///         (뒤끝 SDK 5.x 에 User 서브네임스페이스 없음)
///
///   Bug2: PostType.Admin / PostType.Rank
///         → PostType.admin / PostType.rank 로 수정
///         (뒤끝 SDK PostType enum은 소문자)
///
///   Bug3: callback.GetFlattenJSON()["postList"]
///         → callback.GetReturnValuetoJSON()["postList"] 로 수정
///         (GetFlattenJSON은 GameData 행 평탄화용 — 우편 목록엔 사용 불가)
/// ══════════════════════════════════════════════════════════
/// </summary>
public class BackendPostManager : MonoBehaviour
{
    public static BackendPostManager Instance { get; private set; }

    /// <summary>서버 우편 로드 완료 여부</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>서버 우편 로드 완료 시 발생 (새 우편 수)</summary>
    public static event Action<int> OnServerPostsLoaded;

    /// <summary>서버 우편 보상 수령 완료 시 발생</summary>
    public static event Action OnPostRewardClaimed;

    // 서버 우편 캐시 (mailID → 뒤끝 우편 정보 매핑)
    private readonly Dictionary<int, ServerPostInfo> _serverPostMap
        = new Dictionary<int, ServerPostInfo>();

    private class ServerPostInfo
    {
        public string inDate;        // 뒤끝 우편 고유 ID
        public PostType postType;    // admin / rank
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Debug.Log("[ManagerInit] BackendPostManager가 생성되었습니다.");
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ══════════════════════════════════════════════════════
    //  서버 우편 로드
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 서버에서 관리자 우편(admin) + 랭킹 보상 우편(rank)을 모두 로드합니다.
    /// 로그인 성공 후 BackendManager에서 자동 호출됩니다.
    /// </summary>
    public void LoadServerPosts()
    {
        if (!BackendManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[BackendPost] 로그인되지 않음 → 우편 로드 스킵");
            return;
        }

        _serverPostMap.Clear();
        int totalNew = 0;

        // ① 관리자 우편(admin) 로드
        // ✅ Bug2 수정: PostType.admin (소문자)
        LoadPostsByType(PostType.Admin, count =>
        {
            totalNew += count;

            // ② 랭킹 보상 우편(rank) 로드
            // ✅ Bug2 수정: PostType.rank (소문자)
            LoadPostsByType(PostType.Rank, rankCount =>
            {
                totalNew += rankCount;
                IsLoaded = true;

                if (totalNew > 0)
                {
                    Debug.Log($"[BackendPost] 서버 우편 {totalNew}개 수신 완료");
                    OnServerPostsLoaded?.Invoke(totalNew);
                }
                else
                {
                    Debug.Log("[BackendPost] 새 서버 우편 없음");
                }
            });
        });
    }

    /// <summary>
    /// 특정 타입의 우편 목록을 뒤끝 서버에서 가져옵니다.
    /// </summary>
    private void LoadPostsByType(PostType postType, Action<int> onComplete)
    {
        // ✅ Bug1 수정: Backend.UPost.GetPostList (User 서브네임스페이스 제거)
        Backend.UPost.GetPostList(postType, callback =>
        {
            if (!callback.IsSuccess())
            {
                Debug.LogWarning($"[BackendPost] {postType} 우편 로드 실패: {callback.GetMessage()}");
                onComplete?.Invoke(0);
                return;
            }

            // ✅ Bug3 수정: GetReturnValuetoJSON()으로 우편 목록 파싱
            //   GetFlattenJSON() = GameData 테이블 행 평탄화 전용 메서드
            //   우편 API 응답은 GetReturnValuetoJSON()에 담겨 있음
            JsonData returnJson = callback.GetReturnValuetoJSON();

            // 응답 구조 안전 체크
            if (returnJson == null || !returnJson.ContainsKey("postList"))
            {
                Debug.Log($"[BackendPost] {postType} 우편 없음 (postList 키 없음)");
                onComplete?.Invoke(0);
                return;
            }

            JsonData postList = returnJson["postList"];

            if (postList == null || postList.Count == 0)
            {
                Debug.Log($"[BackendPost] {postType} 우편 0개");
                onComplete?.Invoke(0);
                return;
            }

            int newCount = 0;

            for (int i = 0; i < postList.Count; i++)
            {
                JsonData post = postList[i];
                Mail mail = ConvertToMail(post, postType);

                if (mail != null && MailManager.Instance != null)
                {
                    // 중복 방지: inDate 기반 mailID로 체크
                    bool exists = MailManager.Instance.mailList.Exists(
                        m => m.mailID == mail.mailID);

                    if (!exists)
                    {
                        MailManager.Instance.mailList.Insert(0, mail);
                        newCount++;
                    }
                }
            }

            Debug.Log($"[BackendPost] {postType} 우편 {newCount}개 추가");
            onComplete?.Invoke(newCount);
        });
    }

    // ══════════════════════════════════════════════════════
    //  서버 우편 → Mail 변환
    // ══════════════════════════════════════════════════════

    private Mail ConvertToMail(JsonData post, PostType postType)
    {
        try
        {
            string inDate = post.ContainsKey("inDate") ? post["inDate"].ToString() : "";
            string title = post.ContainsKey("title") ? post["title"].ToString() : "서버 우편";
            string content = post.ContainsKey("content") ? post["content"].ToString() : "";

            // 고유 mailID 생성 (inDate+title 복합 해시 — 충돌 최소화)
            int mailID = Mathf.Abs((inDate + "_" + title).GetHashCode());

            Mail mail = new Mail(mailID, title, content);

            // 발송일 파싱
            if (post.ContainsKey("sentAt"))
            {
                if (DateTime.TryParse(post["sentAt"].ToString(), out DateTime sentDate))
                    mail.sendDate = sentDate;
            }

            // 보상 아이템 파싱
            if (post.ContainsKey("items") && post["items"] != null)
            {
                JsonData items = post["items"];
                for (int j = 0; j < items.Count; j++)
                {
                    MailReward reward = ParseRewardItem(items[j]);
                    if (reward != null)
                    {
                        mail.rewards.Add(reward);
                        mail.hasReward = true;
                    }
                }
            }

            // 서버 우편 정보 캐시 (보상 수령 시 inDate 필요)
            _serverPostMap[mailID] = new ServerPostInfo
            {
                inDate = inDate,
                postType = postType
            };

            return mail;
        }
        catch (Exception e)
        {
            Debug.LogError($"[BackendPost] 우편 파싱 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 뒤끝 우편 보상 아이템 JSON → MailReward 변환
    /// 뒤끝 우편 보상 구조: { "item": { "itemName": "...", "itemCount": N } }
    /// </summary>
    private MailReward ParseRewardItem(JsonData item)
    {
        if (item == null) return null;

        string itemName = "";
        int itemCount = 1;

        if (item.ContainsKey("item"))
        {
            JsonData itemData = item["item"];
            if (itemData.ContainsKey("itemName"))
                itemName = itemData["itemName"].ToString();
            if (itemData.ContainsKey("itemCount"))
                itemCount = int.Parse(itemData["itemCount"].ToString());
        }

        // 보상 타입 판별 (이름 키워드 기반)
        string low = itemName.ToLower();

        if (low.Contains("gold") || low.Contains("골드"))
            return new MailReward(MailReward.RewardType.Gold, itemCount);
        if (low.Contains("gem") || low.Contains("다이아") || low.Contains("보석"))
            return new MailReward(MailReward.RewardType.Gem, itemCount);
        if (low.Contains("ticket") || low.Contains("티켓"))
            return new MailReward(MailReward.RewardType.Ticket, itemCount);
        if (low.Contains("crystal") || low.Contains("크리스탈"))
            return new MailReward(MailReward.RewardType.Crystal, itemCount);
        if (low.Contains("essence") || low.Contains("에센스"))
            return new MailReward(MailReward.RewardType.Essence, itemCount);
        if (low.Contains("fragment") || low.Contains("파편"))
            return new MailReward(MailReward.RewardType.Fragment, itemCount);

        // 매칭 없으면 수량 있을 때 골드로 기본 처리
        if (itemCount > 0)
            return new MailReward(MailReward.RewardType.Gold, itemCount);

        return null;
    }

    // ══════════════════════════════════════════════════════
    //  서버 우편 보상 수령
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 서버 우편의 보상을 뒤끝 서버에서 수령합니다.
    /// MailManager.ClaimMailReward() 내부에서 자동 호출됩니다.
    /// </summary>
    /// <param name="mailID">메일 ID (inDate 해시)</param>
    /// <param name="onComplete">완료 콜백 (성공 여부)</param>
    public void ClaimServerPost(int mailID, Action<bool> onComplete = null)
    {
        // 로컬 메일이면 서버 수령 불필요
        if (!_serverPostMap.ContainsKey(mailID))
        {
            onComplete?.Invoke(true);
            return;
        }

        ServerPostInfo info = _serverPostMap[mailID];

        // ✅ Bug1 수정: Backend.UPost.ReceivePostItem (User 서브네임스페이스 제거)
        Backend.UPost.ReceivePostItem(info.postType, info.inDate, callback =>
        {
            if (callback.IsSuccess())
            {
                Debug.Log($"[BackendPost] 서버 우편 보상 수령 완료 (inDate: {info.inDate})");
                _serverPostMap.Remove(mailID);
                SaveLoadManager.Instance?.SaveGame();
                OnPostRewardClaimed?.Invoke();
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogWarning($"[BackendPost] 보상 수령 실패: {callback.GetMessage()}");
                onComplete?.Invoke(false);
            }
        });
    }

    /// <summary>해당 mailID가 서버 우편인지 확인합니다.</summary>
    public bool IsServerPost(int mailID) => _serverPostMap.ContainsKey(mailID);
}