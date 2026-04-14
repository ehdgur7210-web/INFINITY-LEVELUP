using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ���� ���� UI
/// - ���� ���� ǥ��
/// - ���൵ ��
/// - ���� ���� ��ư
/// </summary>
public class AchievementSlot : MonoBehaviour
{
    [Header("UI ���")]
    public Image iconImage;                      // ���� ������
    public TextMeshProUGUI nameText;             // ���� �̸�
    public TextMeshProUGUI descriptionText;      // ����
    public TextMeshProUGUI progressText;         // ���൵ �ؽ�Ʈ (3/10)
    public Slider progressBar;                   // ���൵ ��
    public TextMeshProUGUI rewardText;           // ���� ����
    public Button claimButton;                   // ���� ���� ��ư
    public GameObject completedMark;             // �Ϸ� ǥ��
    public Image gradeImage;                     // ��� ǥ��

    [Header("��� ����")]
    public Color bronzeColor = new Color(0.8f, 0.5f, 0.2f);
    public Color silverColor = new Color(0.75f, 0.75f, 0.75f);
    public Color goldColor = new Color(1f, 0.84f, 0f);
    public Color platinumColor = new Color(0.9f, 0.9f, 1f);

    private AchievementProgress currentProgress;

    void Start()
    {
        if (claimButton != null)
        {
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(OnClaimButtonClicked);
        }
    }

    void OnDestroy()
    {
        if (claimButton != null)
            claimButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// ���� ���� ����
    /// </summary>
    public void SetupSlot(AchievementProgress progress)
    {
        currentProgress = progress;

        // ������
        if (iconImage != null && progress.achievement.icon != null)
        {
            iconImage.sprite = progress.achievement.icon;
            iconImage.enabled = true;
        }

        // �̸�
        if (nameText != null)
        {
            nameText.text = progress.achievement.achievementName;
        }

        // ����
        if (descriptionText != null)
        {
            descriptionText.text = progress.achievement.description;
        }

        // ���൵ �ؽ�Ʈ
        if (progressText != null)
        {
            progressText.text = $"{progress.currentAmount}/{progress.achievement.targetAmount}";
        }

        // ���൵ ��
        if (progressBar != null)
        {
            progressBar.value = progress.GetProgressRatio();
        }

        // ���� ����
        if (rewardText != null)
        {
            AchievementReward reward = progress.achievement.reward;
            string rewardInfo = "";

            if (reward.gold > 0)
                rewardInfo += $"��� {reward.gold} ";
            if (reward.gem > 0)
                rewardInfo += $"���� {reward.gem} ";
            if (reward.exp > 0)
                rewardInfo += $"EXP {reward.exp} ";

            rewardText.text = rewardInfo;
        }

        // ��� ����
        if (gradeImage != null)
        {
            gradeImage.color = GetGradeColor(progress.achievement.grade);
        }

        // ���� ���� ��ư
        if (claimButton != null)
        {
            if (progress.isCompleted && !progress.isRewarded)
            {
                claimButton.gameObject.SetActive(true);
                claimButton.interactable = true;
            }
            else
            {
                claimButton.gameObject.SetActive(false);
            }
        }

        // �Ϸ� ǥ��
        if (completedMark != null)
        {
            completedMark.SetActive(progress.isRewarded);
        }
    }

    /// <summary>
    /// ��޺� ���� ��ȯ
    /// </summary>
    Color GetGradeColor(AchievementGrade grade)
    {
        switch (grade)
        {
            case AchievementGrade.Bronze: return bronzeColor;
            case AchievementGrade.Silver: return silverColor;
            case AchievementGrade.Gold: return goldColor;
            case AchievementGrade.Platinum: return platinumColor;
            default: return Color.white;
        }
    }

    /// <summary>
    /// ���� ���� ��ư Ŭ��
    /// </summary>
    void OnClaimButtonClicked()
    {
        if (currentProgress != null && AchievementSystem.Instance != null)
        {
            // �� ���� ���� ��ư Ŭ�� (AchievementSystem.ClaimAchievementReward ���ο����� �︲)
            SoundManager.Instance?.PlayButtonClick();
            AchievementSystem.Instance.ClaimAchievementReward(currentProgress);
            SetupSlot(currentProgress); // UI ����
        }
    }
}