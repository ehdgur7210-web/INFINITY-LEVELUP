using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 메일 UI 매니저
/// - 메일 목록 표시
/// - 메일 상세 보기
/// - 쿠폰 코드 입력
/// - 보상 수령
/// </summary>
public class MailUI : MonoBehaviour
{
    public static MailUI Instance;

    [Header("메인 패널")]
    [SerializeField] private GameObject mailPanel;          // 전체 메일 패널

    [Header("메일 목록")]
    [SerializeField] private GameObject mailListPanel;      // 메일 목록 패널
    [SerializeField] private Transform mailListContent;     // Scroll View Content
    [SerializeField] private GameObject mailSlotPrefab;     // 메일 슬롯 프리팹

    [Header("메일 상세")]
    [SerializeField] private GameObject mailDetailPanel;    // 메일 상세 패널
    [SerializeField] private TextMeshProUGUI detailTitle;   // 제목
    [SerializeField] private TextMeshProUGUI detailContent; // 내용
    [SerializeField] private TextMeshProUGUI detailDate;    // 날짜
    [SerializeField] private Transform rewardListContent;   // 보상 목록
    [SerializeField] private GameObject rewardSlotPrefab;   // 보상 슬롯 프리팹
    [SerializeField] private Button claimButton;            // 수령 버튼
    [SerializeField] private Button deleteButton;           // 삭제 버튼
    [SerializeField] private Button backButton;             // 뒤로 가기

    [Header("쿠폰 입력")]
    [SerializeField] private GameObject couponPanel;        // 쿠폰 입력 패널
    [SerializeField] private TMP_InputField couponInput;    // 쿠폰 입력 필드
    [SerializeField] private Button couponSubmitButton;     // 제출 버튼
    [SerializeField] private Button couponCloseButton;      // 닫기 버튼

    [Header("버튼")]
    [SerializeField] private Button openCouponButton;       // 쿠폰 입력 열기
    [SerializeField] private Button closeMailButton;        // 메일창 닫기
    [SerializeField] private Button claimAllButton;         // 모두 받기

    [Header("알림")]
    [SerializeField] private GameObject notificationBadge;  // 빨간 점 알림
    [SerializeField] private TextMeshProUGUI badgeText;     // 알림 숫자

    private List<GameObject> currentMailSlots = new List<GameObject>();
    private List<GameObject> currentRewardSlots = new List<GameObject>();
    private Mail currentSelectedMail;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        SetupButtons();

        // 초기 상태
        if (mailPanel != null) mailPanel.SetActive(false);
        if (mailDetailPanel != null) mailDetailPanel.SetActive(false);
        if (couponPanel != null) couponPanel.SetActive(false);

        UpdateNotificationBadge();
    }

    /// <summary>
    /// 버튼 이벤트 설정
    /// </summary>
    private void SetupButtons()
    {
        if (closeMailButton != null)
            closeMailButton.onClick.AddListener(CloseMailPanel);

        if (openCouponButton != null)
            openCouponButton.onClick.AddListener(OpenCouponPanel);

        if (couponSubmitButton != null)
            couponSubmitButton.onClick.AddListener(OnCouponSubmit);

        if (couponCloseButton != null)
            couponCloseButton.onClick.AddListener(CloseCouponPanel);

        if (claimButton != null)
            claimButton.onClick.AddListener(OnClaimReward);

        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDeleteMail);

        if (backButton != null)
            backButton.onClick.AddListener(BackToMailList);

        if (claimAllButton != null)
            claimAllButton.onClick.AddListener(OnClaimAll);
    }

    #region 메일 패널 열기/닫기

    /// <summary>
    /// 메일함 열기
    /// </summary>
    public void OpenMailPanel()
    {
        if (mailPanel != null)
        {
            mailPanel.SetActive(true);
            ShowMailList();
            RefreshMailList();
        }
    }

    /// <summary>
    /// 메일함 닫기
    /// </summary>
    public void CloseMailPanel()
    {
        if (mailPanel != null)
        {
            mailPanel.SetActive(false);
        }

        if (mailDetailPanel != null)
        {
            mailDetailPanel.SetActive(false);
        }

        if (couponPanel != null)
        {
            couponPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 메일 목록 표시
    /// </summary>
    private void ShowMailList()
    {
        if (mailListPanel != null) mailListPanel.SetActive(true);
        if (mailDetailPanel != null) mailDetailPanel.SetActive(false);
    }

    /// <summary>
    /// 메일 목록으로 돌아가기
    /// </summary>
    private void BackToMailList()
    {
        ShowMailList();
    }

    #endregion

    #region 메일 목록

    /// <summary>
    /// 메일 목록 새로고침
    /// </summary>
    public void RefreshMailList()
    {
        // 기존 슬롯 제거
        ClearMailSlots();

        if (MailManager.Instance == null)
        {
            Debug.LogWarning("[MailUI] MailManager가 없습니다!");
            return;
        }

        if (mailListContent == null || mailSlotPrefab == null)
        {
            Debug.LogWarning("[MailUI] Mail List Content 또는 Prefab이 없습니다!");
            return;
        }

        List<Mail> mails = MailManager.Instance.GetAllMails();

        // 메일이 없으면
        if (mails.Count == 0)
        {
            Debug.Log("[MailUI] 메일이 없습니다.");
            return;
        }

        // 메일 슬롯 생성
        foreach (Mail mail in mails)
        {
            GameObject slotObj = Instantiate(mailSlotPrefab, mailListContent);
            MailSlot slot = slotObj.GetComponent<MailSlot>();

            if (slot != null)
            {
                slot.Setup(mail, () => ShowMailDetail(mail));
            }

            currentMailSlots.Add(slotObj);
        }

        UpdateNotificationBadge();

        Debug.Log($"[MailUI] 메일 {mails.Count}개 표시");
    }

    /// <summary>
    /// 메일 슬롯 제거
    /// </summary>
    private void ClearMailSlots()
    {
        foreach (GameObject slot in currentMailSlots)
        {
            if (slot != null) Destroy(slot);
        }
        currentMailSlots.Clear();
    }

    #endregion

    #region 메일 상세

    /// <summary>
    /// 메일 상세 보기
    /// </summary>
    private void ShowMailDetail(Mail mail)
    {
        if (mail == null) return;
        // ★ 우편 클릭 효과음
        SoundManager.Instance?.PlayButtonClick();

        Debug.Log($"[Mail 상세] 제목:{mail.title} | hasReward:{mail.hasReward} | isRewardClaimed:{mail.isRewardClaimed} | 보상수:{mail.rewards?.Count} | 날짜:{mail.sendDate}");

        currentSelectedMail = mail;

        // 읽음 처리
        if (MailManager.Instance != null)
        {
            MailManager.Instance.ReadMail(mail.mailID);
        }

        // 패널 전환
        if (mailListPanel != null) mailListPanel.SetActive(false);
        if (mailDetailPanel != null) mailDetailPanel.SetActive(true);

        // 제목
        if (detailTitle != null)
        {
            detailTitle.text = mail.title;
        }

        // 내용
        if (detailContent != null)
        {
            detailContent.text = mail.content;
        }

        // 날짜
        if (detailDate != null)
        {
            detailDate.text = mail.sendDate.ToString("yyyy-MM-dd HH:mm");
        }

        // 보상 목록
        ShowRewardList(mail.rewards);

        // 버튼 상태
        UpdateDetailButtons(mail);

        UpdateNotificationBadge();
    }

    /// <summary>
    /// 보상 목록 표시
    /// </summary>
    private void ShowRewardList(List<MailReward> rewards)
    {
        // 기존 보상 슬롯 제거
        ClearRewardSlots();

        if (rewardListContent == null || rewardSlotPrefab == null) return;

        foreach (MailReward reward in rewards)
        {
            GameObject slotObj = Instantiate(rewardSlotPrefab, rewardListContent);
            MailRewardSlot slot = slotObj.GetComponent<MailRewardSlot>();

            if (slot != null)
            {
                slot.Setup(reward);
            }

            currentRewardSlots.Add(slotObj);
        }
    }

    /// <summary>
    /// 보상 슬롯 제거
    /// </summary>
    private void ClearRewardSlots()
    {
        foreach (GameObject slot in currentRewardSlots)
        {
            if (slot != null) Destroy(slot);
        }
        currentRewardSlots.Clear();
    }

    /// <summary>
    /// 상세 보기 버튼 업데이트
    /// </summary>
    private void UpdateDetailButtons(Mail mail)
    {
        if (claimButton == null) return;

        // ★ rewards null 방어
        if (mail.rewards == null) mail.rewards = new List<MailReward>();

        bool hasRewards = mail.hasReward && mail.rewards.Count > 0;
        bool canClaim = hasRewards && !mail.isRewardClaimed;

        Debug.Log($"[버튼상태] hasReward:{mail.hasReward} rewards.Count:{mail.rewards.Count} canClaim:{canClaim}");

        claimButton.gameObject.SetActive(hasRewards);
        claimButton.interactable = canClaim;

        TextMeshProUGUI buttonText = claimButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
            buttonText.text = mail.isRewardClaimed ? "수령 완료" : "받기";

        if (deleteButton != null)
            deleteButton.interactable = !mail.hasReward || mail.isRewardClaimed;
    }

    /// <summary>
    /// 보상 수령 버튼 클릭
    /// </summary>
    private void OnClaimReward()
    {
        if (currentSelectedMail == null || MailManager.Instance == null) return;

        bool success = MailManager.Instance.ClaimMailReward(currentSelectedMail.mailID);

        if (success)
        {
            // ★ 우편 보상 수령 효과음
            SoundManager.Instance?.PlayMailReward();
            UpdateDetailButtons(currentSelectedMail);
            RefreshMailList();
        }
    }

    /// <summary>
    /// 삭제 버튼 클릭
    /// </summary>
    private void OnDeleteMail()
    {
        if (currentSelectedMail == null || MailManager.Instance == null) return;

        MailManager.Instance.DeleteMail(currentSelectedMail.mailID);

        BackToMailList();
        RefreshMailList();
    }

    /// <summary>
    /// 모두 받기 버튼
    /// </summary>
    private void OnClaimAll()
    {
        if (MailManager.Instance == null) return;

        // 모든 미수령 보상 수령
        List<Mail> mails = MailManager.Instance.GetAllMails();
        int claimed = 0;

        foreach (Mail mail in mails)
        {
            if (mail.hasReward && !mail.isRewardClaimed)
            {
                if (MailManager.Instance.ClaimMailReward(mail.mailID))
                {
                    claimed++;
                }
            }
        }

        if (claimed > 0)
        {
            // ★ 우편 일괄 보상 수령 효과음
            SoundManager.Instance?.PlayMailReward();
            RefreshMailList();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMessage($"{claimed}개 보상을 받았습니다!", Color.green);
            }
        }
    }

    #endregion

    #region 쿠폰

    /// <summary>
    /// 쿠폰 입력 창 열기
    /// </summary>
    private void OpenCouponPanel()
    {
        // ★ 쿠폰 버튼 클릭 효과음
        SoundManager.Instance?.PlayButtonClick();
        if (couponPanel != null)
        {
            couponPanel.SetActive(true);

            if (couponInput != null)
            {
                couponInput.text = "";
                couponInput.ActivateInputField();
            }
        }
    }

    /// <summary>
    /// 쿠폰 입력 창 닫기
    /// </summary>
    private void CloseCouponPanel()
    {
        if (couponPanel != null)
        {
            couponPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 쿠폰 제출
    /// </summary>
    private void OnCouponSubmit()
    {
        if (couponInput == null || MailManager.Instance == null) return;

        string code = couponInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(code))
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMessage("쿠폰 코드를 입력하세요!", Color.red);
            }
            return;
        }

        bool success = MailManager.Instance.RedeemCouponCode(code);

        if (success)
        {
            // ★ 쿠폰 보상 수령 효과음
            SoundManager.Instance?.PlayCouponReward();
            CloseCouponPanel();
            RefreshMailList();
            couponInput.text = "";
        }
    }

    #endregion

    #region 알림 뱃지

    /// <summary>
    /// 알림 뱃지 업데이트
    /// </summary>
    public void UpdateNotificationBadge()
    {
        if (MailManager.Instance == null) return;

        int unreadCount = MailManager.Instance.GetUnreadMailCount();
        int rewardCount = MailManager.Instance.GetUnclaimedRewardCount();

        if (notificationBadge != null)
        {
            notificationBadge.SetActive(unreadCount > 0 || rewardCount > 0);
        }

        if (badgeText != null)
        {
            int total = unreadCount + rewardCount;
            badgeText.text = total > 99 ? "99+" : total.ToString();
        }
    }

    #endregion
}