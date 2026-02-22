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
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        InitializeCouponCodes();
    }

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

        foreach (MailReward reward in mail.rewards)
        {
            GiveReward(reward);
        }

        mail.isRewardClaimed = true;
        mail.isRead = true;

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

    // ⭐⭐⭐ 쿠폰 코드 입력
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
            UIManager.Instance.ShowMessage($"쿠폰 사용 완료!\n메일을 확인하세요!", Color.green);
        }

        return true;
    }

    // 쿠폰 초기화
    private void InitializeCouponCodes()
    {
        couponCodes.Add(new CouponCode("ㄱ", "ㄱ 쿠폰",
            new List<MailReward>
            {
                new MailReward(MailReward.RewardType.Gold, 10000),
                new MailReward(MailReward.RewardType.Gem, 500),
                new MailReward(MailReward.RewardType.Ticket, 10)
            }));

        couponCodes.Add(new CouponCode("NEWYEAR2026", "ㄴ",
            new List<MailReward>
            {
                new MailReward(MailReward.RewardType.Gold, 50000),
                new MailReward(MailReward.RewardType.Gem, 1000)
            }));

        couponCodes.Add(new CouponCode("BETA100", "ㄷ",
            new List<MailReward>
            {
                new MailReward(MailReward.RewardType.Crystal, 100),
                new MailReward(MailReward.RewardType.Essence, 50)
            }));
    }

    // 유틸리티
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