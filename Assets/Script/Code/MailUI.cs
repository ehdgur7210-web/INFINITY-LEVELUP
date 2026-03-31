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

    [Header("메일 패널")]
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
    [SerializeField] private Transform rewardListContent;   // 보상 리스트
    [SerializeField] private GameObject rewardSlotPrefab;   // 보상 슬롯 프리팹
    [SerializeField] private Button claimButton;            // 보상 받기 버튼
    [SerializeField] private Button deleteButton;           // 삭제 버튼
    [SerializeField] private Button backButton;             // 뒤로가기 버튼

    [Header("쿠폰 입력")]
    [SerializeField] private GameObject couponPanel;        // 쿠폰 입력 패널
    [SerializeField] private TMP_InputField couponInput;    // 쿠폰 입력 필드
    [SerializeField] private Button couponSubmitButton;     // 쿠폰 확인 버튼
    [SerializeField] private Button couponCloseButton;      // 닫기 버튼

    [Header("버튼")]
    [SerializeField] private Button openCouponButton;       // 쿠폰 입력 열기
    [SerializeField] private Button closeMailButton;        // 메일창 닫기
    [SerializeField] private Button claimAllButton;         // 전체 받기
    [SerializeField] private Button deleteReadButton;       // ★ 읽은 메일 전체 삭제

    [Header("알림")]
    [SerializeField] private GameObject notificationBadge;  // 빨간 알림 표시
    [SerializeField] private TextMeshProUGUI badgeText;     // 알림 숫자

    private List<GameObject> currentMailSlots = new List<GameObject>();
    private List<GameObject> currentRewardSlots = new List<GameObject>();
    private Mail currentSelectedMail;

    private bool _openRequested = false;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        if (Instance == null)
            Instance = this;
    }

    void Start()
    {
        SetupButtons();

        // ★ OpenMailPanel()이 Start() 전에 호출된 경우 패널을 닫지 않음
        if (!_openRequested)
        {
            if (mailPanel != null) mailPanel.SetActive(false);
        }
        if (mailDetailPanel != null) mailDetailPanel.SetActive(false);
        if (couponPanel != null) couponPanel.SetActive(false);

        UpdateNotificationBadge();
    }

    /// <summary>
    /// 버튼 이벤트 설정
    /// </summary>
    private bool _buttonsSetup = false;
    private void SetupButtons()
    {
        if (_buttonsSetup) return;
        _buttonsSetup = true;

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

        if (deleteReadButton != null)
            deleteReadButton.onClick.AddListener(OnDeleteReadMails);
    }

    #region 메일 패널

    public void OpenMailPanel()
    {
        if (mailPanel != null)
        {
            _openRequested = true; // ★ Start()에서 닫지 않도록 플래그
            mailPanel.SetActive(true);
            SetupButtons(); // ★ Start() 전에 호출되었을 경우 버튼 설정 보장
            ShowMailList();
            RefreshMailList();
            TutorialManager.Instance?.OnActionCompleted("OpenMail");
        }
    }

    public void CloseMailPanel()
    {
        if (mailPanel != null)
            mailPanel.SetActive(false);

        if (mailDetailPanel != null)
            mailDetailPanel.SetActive(false);

        if (couponPanel != null)
            couponPanel.SetActive(false);

        TopMenuManager.Instance?.ClearBanner();
    }

    private void ShowMailList()
    {
        if (mailListPanel != null) mailListPanel.SetActive(true);
        if (mailDetailPanel != null) mailDetailPanel.SetActive(false);
    }

    private void BackToMailList()
    {
        ShowMailList();
    }

    #endregion

    #region 메일 목록

    public void RefreshMailList()
    {
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

        if (mails.Count == 0)
        {
            Debug.Log("[MailUI] 메일이 없습니다.");
            return;
        }

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
    }

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

    private void ShowMailDetail(Mail mail)
    {
        if (mail == null) return;

        SoundManager.Instance?.PlayButtonClick();

        currentSelectedMail = mail;

        MailManager.Instance?.ReadMail(mail.mailID);

        if (mailListPanel != null) mailListPanel.SetActive(false);
        if (mailDetailPanel != null) mailDetailPanel.SetActive(true);

        if (detailTitle != null)
            detailTitle.text = mail.title;

        if (detailContent != null)
            detailContent.text = mail.content;

        if (detailDate != null)
            detailDate.text = mail.sendDate.ToString("yyyy-MM-dd HH:mm");

        ShowRewardList(mail.rewards);
        UpdateDetailButtons(mail);
        UpdateNotificationBadge();
    }

    private void ShowRewardList(List<MailReward> rewards)
    {
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

    private void ClearRewardSlots()
    {
        foreach (GameObject slot in currentRewardSlots)
        {
            if (slot != null) Destroy(slot);
        }
        currentRewardSlots.Clear();
    }

    private void UpdateDetailButtons(Mail mail)
    {
        if (claimButton == null) return;

        if (mail.rewards == null)
            mail.rewards = new List<MailReward>();

        bool hasRewards = mail.hasReward && mail.rewards.Count > 0;
        bool canClaim = hasRewards && !mail.isRewardClaimed;

        claimButton.gameObject.SetActive(hasRewards);
        claimButton.interactable = canClaim;

        TextMeshProUGUI buttonText = claimButton.GetComponentInChildren<TextMeshProUGUI>();

        if (buttonText != null)
            buttonText.text = mail.isRewardClaimed ? "수령 완료" : "받기";

        if (deleteButton != null)
            deleteButton.interactable = !mail.hasReward || mail.isRewardClaimed;
    }

    private void OnClaimReward()
    {
        if (currentSelectedMail == null || MailManager.Instance == null) return;

        bool success = MailManager.Instance.ClaimMailReward(currentSelectedMail.mailID);

        if (success)
        {
            SoundManager.Instance?.PlayMailReward();
            SaveLoadManager.Instance?.SaveGame();
            UpdateDetailButtons(currentSelectedMail);
            RefreshMailList();
        }
    }

    private void OnDeleteMail()
    {
        if (currentSelectedMail == null || MailManager.Instance == null) return;

        MailManager.Instance.DeleteMail(currentSelectedMail.mailID);

        BackToMailList();
        RefreshMailList();
    }

    private void OnClaimAll()
    {
        if (MailManager.Instance == null) return;

        List<Mail> mails = MailManager.Instance.GetAllMails();
        int claimed = 0;

        foreach (Mail mail in mails)
        {
            if (mail.hasReward && !mail.isRewardClaimed)
            {
                if (MailManager.Instance.ClaimMailReward(mail.mailID))
                    claimed++;
            }
        }

        if (claimed > 0)
        {
            SoundManager.Instance?.PlayMailReward();
            SaveLoadManager.Instance?.SaveGame();
            RefreshMailList();

            UIManager.Instance?.ShowMessage($"{claimed}개의 보상을 받았습니다!", Color.green);

            // 튜토리얼 트리거
            TutorialManager.Instance?.OnActionCompleted("MailClaimAll");
        }
    }

    /// <summary>★ 읽은 메일 전체 삭제 (확인 다이얼로그 포함)</summary>
    private void OnDeleteReadMails()
    {
        if (MailManager.Instance == null) return;
        SoundManager.Instance?.PlayButtonClick();

        UIManager.Instance?.ShowConfirmDialog(
            "읽은 메일을 전체 삭제하시겠습니까?\n(보상 미수령 메일은 보존됩니다)",
            onConfirm: () =>
            {
                int deleted = MailManager.Instance.DeleteReadMails();

                if (deleted > 0)
                {
                    SaveLoadManager.Instance?.SaveGame();
                    RefreshMailList();
                    UIManager.Instance?.ShowMessage($"읽은 메일 {deleted}개 삭제 완료!", Color.green);
                }
                else
                {
                    UIManager.Instance?.ShowMessage("삭제할 메일이 없습니다.", Color.yellow);
                }
            }
        );
    }

    #endregion

    #region 쿠폰

    private void OpenCouponPanel()
    {
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

    private void CloseCouponPanel()
    {
        if (couponPanel != null)
            couponPanel.SetActive(false);
    }

    private void OnCouponSubmit()
    {
        if (couponInput == null || MailManager.Instance == null) return;

        string code = couponInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(code))
        {
            UIManager.Instance?.ShowMessage("쿠폰 코드를 입력하세요!", Color.red);
            return;
        }

        bool success = MailManager.Instance.RedeemCouponCode(code);

        if (success)
        {
            SoundManager.Instance?.PlayCouponReward();
            CloseCouponPanel();
            RefreshMailList();
            couponInput.text = "";

            // 튜토리얼 트리거
            TutorialManager.Instance?.OnActionCompleted("CouponUsed");
        }
    }

    #endregion

    #region 알림

    public void UpdateNotificationBadge()
    {
        if (MailManager.Instance == null) return;

        int unreadCount = MailManager.Instance.GetUnreadMailCount();
        int rewardCount = MailManager.Instance.GetUnclaimedRewardCount();

        if (notificationBadge != null)
            notificationBadge.SetActive(unreadCount > 0 || rewardCount > 0);

        if (badgeText != null)
        {
            int total = unreadCount + rewardCount;
            badgeText.text = total > 99 ? "99+" : total.ToString();
        }
    }

    #endregion
}