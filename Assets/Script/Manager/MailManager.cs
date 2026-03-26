using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 메일 데이터
/// </summary>
[System.Serializable]
public class Mail
{
    public int mailID;
    public string title;
    public string content;
    public DateTime sendDate;
    public bool isRead;
    public bool hasReward;
    public bool isRewardClaimed;
    public List<MailReward> rewards;

    public Mail(int id, string title, string content)
    {
        this.mailID = id;
        this.title = title;
        this.content = content;
        this.sendDate = DateTime.Now;
        this.isRead = false;
        this.hasReward = false;
        this.isRewardClaimed = false;
        this.rewards = new List<MailReward>();
    }
}

/// <summary>
/// 메일 보상
/// </summary>
[System.Serializable]
public class MailReward
{
    public enum RewardType
    {
        Item, Gold, Gem, Ticket, Crystal, Essence, Fragment
    }

    public RewardType rewardType;
    public ItemData itemData;
    public int amount;

    public MailReward(ItemData item, int amount)
    {
        this.rewardType = RewardType.Item;
        this.itemData = item;
        this.amount = amount;
    }

    public MailReward(RewardType type, int amount)
    {
        this.rewardType = type;
        this.amount = amount;
    }
}

// ✅ MailRewardSaveData, MailSaveEntry, MailSaveData 클래스는
// 컴파일 순서 보장을 위해 SaveLoadManager.cs에 정의되어 있습니다.

/// <summary>
/// 쿠폰 코드
/// </summary>
[System.Serializable]
public class CouponCode
{
    public string code;
    public string description;
    public List<MailReward> rewards;
    public DateTime? expiryDate;

    public CouponCode(string code, string description, List<MailReward> rewards, DateTime? expiryDate = null)
    {
        this.code = code.ToUpper();
        this.description = description;
        this.rewards = rewards;
        this.expiryDate = expiryDate;
    }
}

/// <summary>
/// 메일 매니저
/// ✅ 수정 내역:
///   - [아이템 소실 수정] GetMailSaveData() / LoadMailSaveData() 추가
///     → 인벤 초과로 메일에 보낸 아이템이 게임 재시작 후 사라지는 버그 해결
/// </summary>
public class MailManager : MonoBehaviour
{
    public static MailManager Instance;

    public List<Mail> mailList = new List<Mail>();
    private int nextMailID = 1;

    [SerializeField] private List<CouponCode> couponCodes = new List<CouponCode>();
    [SerializeField] private List<string> usedCoupons = new List<string>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

        }
        else
        {
            enabled = false;
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (Instance != this) return;
        InitializeCouponCodes();
    }

    // ─────────────────────────────────────────────────────────
    // ✅ 저장 / 로드
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 메일 데이터를 직렬화 가능한 형태로 수집
    /// (SaveLoadManager.CollectSaveData에서 호출)
    /// </summary>
    public MailSaveData GetMailSaveData()
    {
        var entries = new System.Collections.Generic.List<MailSaveEntry>();

        foreach (Mail mail in mailList)
        {
            var entry = new MailSaveEntry
            {
                mailID = mail.mailID,
                title = mail.title,
                content = mail.content,
                sendDateStr = mail.sendDate.ToString("yyyy-MM-dd HH:mm:ss"),
                isRead = mail.isRead,
                hasReward = mail.hasReward,
                isRewardClaimed = mail.isRewardClaimed,
            };

            // 보상 목록 변환 (ScriptableObject → itemID)
            var rewardList = new System.Collections.Generic.List<MailRewardSaveData>();
            if (mail.rewards != null)
            {
                foreach (var reward in mail.rewards)
                {
                    rewardList.Add(new MailRewardSaveData
                    {
                        rewardType = reward.rewardType,
                        itemID = (reward.itemData != null) ? reward.itemData.itemID : -1,
                        amount = reward.amount
                    });
                }
            }
            entry.rewards = rewardList.ToArray();
            entries.Add(entry);
        }

        Debug.Log($"[MailManager] 메일 저장: {entries.Count}개");
        return new MailSaveData
        {
            mails = entries.ToArray(),
            nextMailID = nextMailID
        };
    }

    /// <summary>
    /// 저장된 메일 데이터 복원
    /// (SaveLoadManager.ApplySaveData에서 호출)
    /// </summary>
    public void LoadMailSaveData(MailSaveData saveData)
    {
        if (saveData == null || saveData.mails == null)
        {
            Debug.Log("[MailManager] 로드할 메일 데이터 없음 — Inspector 기본 메일 유지");
            return;
        }

        // ★ Inspector에 설정한 기본 메일 보존 (ID로 중복 체크)
        var inspectorMails = new List<Mail>(mailList);
        mailList.Clear();
        nextMailID = saveData.nextMailID > 0 ? saveData.nextMailID : 1;

        foreach (var entry in saveData.mails)
        {
            Mail mail = new Mail(entry.mailID, entry.title, entry.content);

            // 날짜 복원
            if (DateTime.TryParse(entry.sendDateStr, out DateTime parsedDate))
                mail.sendDate = parsedDate;

            mail.isRead = entry.isRead;
            mail.hasReward = entry.hasReward;
            mail.isRewardClaimed = entry.isRewardClaimed;

            // 보상 목록 복원 (itemID → ScriptableObject)
            mail.rewards = new List<MailReward>();
            if (entry.rewards != null)
            {
                foreach (var rSave in entry.rewards)
                {
                    if (rSave.rewardType == MailReward.RewardType.Item)
                    {
                        // ItemDatabase에서 아이템 검색
                        ItemData itemData = ItemDatabase.Instance?.GetItemByID(rSave.itemID);
                        if (itemData == null)
                            itemData = ItemDatabase.Instance?.GetEquipmentByID(rSave.itemID);

                        if (itemData != null)
                            mail.rewards.Add(new MailReward(itemData, rSave.amount));
                        else
                            Debug.LogWarning($"[MailManager] 메일 아이템 ID {rSave.itemID} 을 DB에서 찾을 수 없음");
                    }
                    else
                    {
                        mail.rewards.Add(new MailReward(rSave.rewardType, rSave.amount));
                    }
                }
            }

            mailList.Add(mail);
        }

        // ★ Inspector 기본 메일 중 세이브에 없는 것 복원 (보상 미수령 상태로)
        var loadedIDs = new HashSet<int>();
        foreach (var m in mailList) loadedIDs.Add(m.mailID);

        foreach (var im in inspectorMails)
        {
            if (!loadedIDs.Contains(im.mailID))
            {
                mailList.Add(im);
                Debug.Log($"[MailManager] Inspector 기본 메일 복원: [{im.mailID}] {im.title}");
            }
        }

        // nextMailID가 기존 메일과 겹치지 않도록 보장
        foreach (var m in mailList)
        {
            if (m.mailID >= nextMailID) nextMailID = m.mailID + 1;
        }

        Debug.Log($"[MailManager] 메일 로드 완료: {mailList.Count}개 (Inspector:{inspectorMails.Count}, 세이브:{saveData.mails.Length})");
    }

    // ─────────────────────────────────────────────────────────
    // 메일 발송 / 관리
    // ─────────────────────────────────────────────────────────

    // ⭐ 인벤토리 꽉 참 - 아이템 메일로 보내기
    public void SendItemToMail(ItemData item, int amount, string reason = "인벤토리 공간 부족")
    {
        Mail mail = new Mail(nextMailID++, "📦 아이템 보관",
            $"{reason}으로 인해 아이템이 메일로 전송되었습니다.");

        mail.hasReward = true;
        mail.rewards.Add(new MailReward(item, amount));
        mailList.Add(mail);

        Debug.Log($"[MailManager] 메일 전송: {item.itemName} x{amount}");

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessage($"인벤토리가 가득 차서\n{item.itemName}을(를) 메일로 보냈습니다!", Color.yellow);
        }
    }

    // 보상 메일 전송
    public void SendRewardMail(string title, string content, List<MailReward> rewards)
    {
        Mail mail = new Mail(nextMailID++, title, content);
        mail.hasReward = true;
        mail.rewards = rewards;
        mailList.Add(mail);
    }

    // 공지 메일 전송
    public void SendNoticeMail(string title, string content)
    {
        Mail mail = new Mail(nextMailID++, title, content);
        mailList.Add(mail);
    }

    // 메일 읽음
    public void ReadMail(int mailID)
    {
        Mail mail = mailList.Find(m => m.mailID == mailID);
        if (mail != null && !mail.isRead)
        {
            mail.isRead = true;
        }
    }

    // ⭐ 보상 수령
    public bool ClaimMailReward(int mailID)
    {
        Mail mail = mailList.Find(m => m.mailID == mailID);

        if (mail == null || !mail.hasReward || mail.isRewardClaimed)
            return false;

        if (mail.rewards != null)
        {
            foreach (MailReward reward in mail.rewards)
            {
                GiveReward(reward);
            }
        }

        mail.isRewardClaimed = true;
        mail.isRead = true;

        // ★ 서버 우편이면 뒤끝에도 보상 수령 처리
        if (BackendPostManager.Instance != null)
            BackendPostManager.Instance.ClaimServerPost(mailID);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessage("보상을 받았습니다!", Color.green);
        }

        return true;
    }

    // 보상 지급
    private void GiveReward(MailReward reward)
    {
        switch (reward.rewardType)
        {
            case MailReward.RewardType.Item:
                if (reward.itemData != null && InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.AddItem(reward.itemData, reward.amount);
                }
                break;

            case MailReward.RewardType.Gold:
                if (GameManager.Instance != null)
                    GameManager.Instance.AddGold(reward.amount);
                break;

            case MailReward.RewardType.Gem:
                if (GameManager.Instance != null)
                    GameManager.Instance.AddGem(reward.amount);
                break;

            case MailReward.RewardType.Ticket:
                if (ResourceBarManager.Instance != null)
                    ResourceBarManager.Instance.AddEquipmentTickets(reward.amount);
                break;

            case MailReward.RewardType.Crystal:
                if (ResourceBarManager.Instance != null)
                    ResourceBarManager.Instance.AddCrystals(reward.amount);
                break;

            case MailReward.RewardType.Essence:
                if (ResourceBarManager.Instance != null)
                    ResourceBarManager.Instance.AddEssences(reward.amount);
                break;

            case MailReward.RewardType.Fragment:
                if (ResourceBarManager.Instance != null)
                    ResourceBarManager.Instance.AddFragments(reward.amount);
                break;
        }
    }

    // 메일 삭제
    public void DeleteMail(int mailID)
    {
        Mail mail = mailList.Find(m => m.mailID == mailID);

        if (mail != null && (!mail.hasReward || mail.isRewardClaimed))
        {
            mailList.Remove(mail);
        }
    }

    // 쿠폰 코드 사용
    public bool RedeemCouponCode(string code)
    {
        code = code.Trim().ToUpper();

        if (usedCoupons.Contains(code))
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage("이미 사용한 쿠폰입니다!", Color.red);
            return false;
        }

        CouponCode coupon = couponCodes.Find(c => c.code == code);

        if (coupon == null)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage("유효하지 않은 쿠폰 코드입니다!", Color.red);
            return false;
        }

        if (coupon.expiryDate.HasValue && DateTime.Now > coupon.expiryDate.Value)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage("만료된 쿠폰입니다!", Color.red);
            return false;
        }

        SendRewardMail($"🎁 쿠폰 보상: {coupon.description}",
            $"쿠폰 코드 '{code}'를 사용하여 보상을 받았습니다!",
            coupon.rewards);

        usedCoupons.Add(code);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessage($"쿠폰 사용 성공!\n메일을 확인하세요!", Color.green);
        }

        return true;
    }

    // 쿠폰 초기화
    private void InitializeCouponCodes()
    {
        couponCodes.Add(new CouponCode("WELCOME", "웰컴 쿠폰",
            new List<MailReward>
            {
                new MailReward(MailReward.RewardType.Gold, 10000),
                new MailReward(MailReward.RewardType.Gem, 500),
                new MailReward(MailReward.RewardType.Ticket, 10)
            }));

        couponCodes.Add(new CouponCode("NEWYEAR2026", "새해 쿠폰",
            new List<MailReward>
            {
                new MailReward(MailReward.RewardType.Gold, 50000),
                new MailReward(MailReward.RewardType.Gem, 1000)
            }));

        couponCodes.Add(new CouponCode("BETA100", "베타 쿠폰",
            new List<MailReward>
            {
                new MailReward(MailReward.RewardType.Crystal, 100),
                new MailReward(MailReward.RewardType.Essence, 50)
            }));
    }

    // 유틸
    public int GetUnreadMailCount()
    {
        return mailList.FindAll(m => !m.isRead).Count;
    }

    public int GetUnclaimedRewardCount()
    {
        return mailList.FindAll(m => m.hasReward && !m.isRewardClaimed).Count;
    }

    public List<Mail> GetAllMails()
    {
        return new List<Mail>(mailList);
    }
}