using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MailSlot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI dateText;
    [SerializeField] private GameObject unreadBadge;
    [SerializeField] private GameObject rewardBadge;
    [SerializeField] private Button button;

    private Mail mail;
    private System.Action onClickCallback;

    public void Setup(Mail mailData, System.Action onClick)
    {
        mail = mailData;
        onClickCallback = onClick;

        // 제목
        if (titleText != null)
        {
            titleText.text = mail.title;
        }

        // 날짜
        if (dateText != null)
        {
            dateText.text = mail.sendDate.ToString("MM/dd");
        }

        // 읽지 않음 뱃지
        if (unreadBadge != null)
        {
            unreadBadge.SetActive(!mail.isRead);
        }

        // 보상 뱃지
        if (rewardBadge != null)
        {
            bool hasReward = mail.hasReward && !mail.isRewardClaimed;
            rewardBadge.SetActive(hasReward);
        }

        // 버튼
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClickCallback?.Invoke());
        }
    }
}
