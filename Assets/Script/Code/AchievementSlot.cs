using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 업적 슬롯 UI
/// - 업적 정보 표시
/// - 진행도 바
/// - 보상 수령 버튼
/// </summary>
public class AchievementSlot : MonoBehaviour
{
    [Header("UI 요소")]
    public Image iconImage;                      // 업적 아이콘
    public TextMeshProUGUI nameText;             // 업적 이름
    public TextMeshProUGUI descriptionText;      // 설명
    public TextMeshProUGUI progressText;         // 진행도 텍스트 (3/10)
    public Slider progressBar;                   // 진행도 바
    public TextMeshProUGUI rewardText;           // 보상 정보
    public Button claimButton;                   // 보상 수령 버튼
    public GameObject completedMark;             // 완료 표시
    public Image gradeImage;                     // 등급 표시

    [Header("등급 색상")]
    public Color bronzeColor = new Color(0.8f, 0.5f, 0.2f);
    public Color silverColor = new Color(0.75f, 0.75f, 0.75f);
    public Color goldColor = new Color(1f, 0.84f, 0f);
    public Color platinumColor = new Color(0.9f, 0.9f, 1f);

    private AchievementProgress currentProgress;

    void Start()
    {
        if (claimButton != null)
        {
            claimButton.onClick.AddListener(OnClaimButtonClicked);
        }
    }

    /// <summary>
    /// 업적 슬롯 설정
    /// </summary>
    public void SetupSlot(AchievementProgress progress)
    {
        currentProgress = progress;

        // 아이콘
        if (iconImage != null && progress.achievement.icon != null)
        {
            iconImage.sprite = progress.achievement.icon;
            iconImage.enabled = true;
        }

        // 이름
        if (nameText != null)
        {
            nameText.text = progress.achievement.achievementName;
        }

        // 설명
        if (descriptionText != null)
        {
            descriptionText.text = progress.achievement.description;
        }

        // 진행도 텍스트
        if (progressText != null)
        {
            progressText.text = $"{progress.currentAmount}/{progress.achievement.targetAmount}";
        }

        // 진행도 바
        if (progressBar != null)
        {
            progressBar.value = progress.GetProgressRatio();
        }

        // 보상 정보
        if (rewardText != null)
        {
            AchievementReward reward = progress.achievement.reward;
            string rewardInfo = "";

            if (reward.gold > 0)
                rewardInfo += $"골드 {reward.gold} ";
            if (reward.gem > 0)
                rewardInfo += $"보석 {reward.gem} ";
            if (reward.exp > 0)
                rewardInfo += $"EXP {reward.exp} ";

            rewardText.text = rewardInfo;
        }

        // 등급 색상
        if (gradeImage != null)
        {
            gradeImage.color = GetGradeColor(progress.achievement.grade);
        }

        // 보상 수령 버튼
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

        // 완료 표시
        if (completedMark != null)
        {
            completedMark.SetActive(progress.isRewarded);
        }
    }

    /// <summary>
    /// 등급별 색상 반환
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
    /// 보상 수령 버튼 클릭
    /// </summary>
    void OnClaimButtonClicked()
    {
        if (currentProgress != null && AchievementSystem.Instance != null)
        {
            // ★ 업적 보상 버튼 클릭 (AchievementSystem.ClaimAchievementReward 내부에서도 울림)
            SoundManager.Instance?.PlayButtonClick();
            AchievementSystem.Instance.ClaimAchievementReward(currentProgress);
            SetupSlot(currentProgress); // UI 갱신
        }
    }
}