using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 단일 퀘스트 슬롯 UI
/// - 한 번에 하나의 퀘스트만 표시
/// - 클리어 시 다음 퀘스트로 자동 전환
///
/// RewardIcon 프리팹 구조:
///   RewardIcon
///     ├─ Image        (배경)
///     ├─ RewardImage  (★ 보상 아이콘 이미지)
///     └─ Reward       (텍스트: 수량/금액)
/// </summary>
public class QuestSlot : MonoBehaviour
{
    [Header("UI 요소")]
    public TextMeshProUGUI questTitleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI objectiveText;

    [Header("진행도")]
    public TextMeshProUGUI progressText;
    public Slider progressBar;

    [Header("보상 아이콘")]
    public Transform rewardParent;
    public GameObject rewardIconPrefab;

    [Header("보상 기본 아이콘 (Inspector에서 설정)")]
    [Tooltip("골드 아이콘 스프라이트")]
    public Sprite goldIconSprite;
    [Tooltip("경험치 아이콘 스프라이트")]
    public Sprite expIconSprite;

    [Header("버튼")]
    public Button claimButton;

    [Header("디버그")]
    public bool debugMode = false;

    private QuestProgress currentQuest;

    void OnEnable()
    {
        if (claimButton != null)
        {
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(OnClaimButtonClicked);
        }
    }

    /// <summary>
    /// 퀘스트 슬롯 설정
    /// </summary>
    public void SetupSlot(QuestProgress quest)
    {
        Debug.Log($"[QuestSlot] SetupSlot 호출 - quest: {(quest != null ? quest.questData.questName : "null")}");

        if (quest == null)
        {
            Debug.LogWarning("[QuestSlot] quest가 null이어서 슬롯 비활성화");
            return;
        }

        currentQuest = quest;

        gameObject.SetActive(true);

        if (transform.parent != null)
            transform.parent.gameObject.SetActive(true);

        // 1. 퀘스트 제목
        if (questTitleText != null)
        {
            questTitleText.text = quest.questData.questName;
            questTitleText.gameObject.SetActive(true);
        }

        // 2. 설명
        if (descriptionText != null)
        {
            descriptionText.text = quest.questData.questDescription;
            descriptionText.gameObject.SetActive(true);
        }

        // 3. 목표 텍스트
        if (objectiveText != null)
        {
            objectiveText.text = "보상";
            objectiveText.gameObject.SetActive(true);
        }

        // 4. 진행도 텍스트
        if (progressText != null)
        {
            int current = GetTotalCurrentAmount(quest);
            int required = GetTotalRequiredAmount(quest);
            progressText.text = $"{current}/{required}";
            progressText.gameObject.SetActive(true);
        }

        // 5. 진행도 바
        if (progressBar != null)
        {
            float progress = GetQuestProgressRatio(quest);
            progressBar.value = progress;
            progressBar.gameObject.SetActive(true);
        }

        // 6. ★ 보상 아이콘 (수정됨)
        SetupRewardIcons(quest);

        // 7. 보상 버튼
        if (claimButton != null)
        {
            if (quest.status == QuestStatus.Completed)
            {
                claimButton.gameObject.SetActive(true);
                claimButton.interactable = true;
            }
            else
            {
                claimButton.gameObject.SetActive(false);
            }
        }

        Debug.Log($"[QuestSlot] ✅ 설정 완료!");
    }

    /// <summary>
    /// ★ 보상 아이콘 설정 (수정됨)
    /// </summary>
    void SetupRewardIcons(QuestProgress quest)
    {
        if (rewardParent == null || rewardIconPrefab == null)
        {
            Debug.LogWarning("[SetupRewardIcons] rewardParent 또는 rewardIconPrefab이 null!");
            return;
        }

        // 기존 아이콘 제거
        foreach (Transform child in rewardParent)
        {
            Destroy(child.gameObject);
        }

        QuestReward reward = quest.questData.reward;

        // ★ 골드 보상 아이콘
        if (reward.gold > 0)
        {
            CreateRewardIcon(goldIconSprite, $"{reward.gold:N0}");
        }

        // ★ 경험치 보상 아이콘
        if (reward.exp > 0)
        {
            CreateRewardIcon(expIconSprite, $"{reward.exp:N0}");
        }

        // ★ 아이템 보상 아이콘 (QuestData의 아이템 아이콘 연동)
        if (reward.rewardItems != null)
        {
            for (int i = 0; i < reward.rewardItems.Length; i++)
            {
                if (reward.rewardItems[i] == null) continue;

                int count = (reward.itemCounts != null && i < reward.itemCounts.Length)
                    ? reward.itemCounts[i] : 1;

                // ★ 아이템의 itemIcon을 RewardImage에 세팅
                CreateRewardIcon(reward.rewardItems[i].itemIcon, $"x{count}");
            }
        }
    }

    /// <summary>
    /// ★ 보상 아이콘 생성 (통합)
    /// 
    /// RewardIcon 프리팹 구조:
    ///   RewardIcon
    ///     ├─ Image        (배경)
    ///     ├─ RewardImage  ← 여기에 아이콘 스프라이트 세팅
    ///     └─ Reward       ← 여기에 수량 텍스트 세팅
    /// </summary>
    void CreateRewardIcon(Sprite iconSprite, string amountText)
    {
        if (rewardIconPrefab == null || rewardParent == null) return;

        GameObject iconObj = Instantiate(rewardIconPrefab, rewardParent);

        // ★ RewardImage 찾아서 아이콘 스프라이트 세팅
        Transform rewardImageTr = iconObj.transform.Find("RewardImage");
        if (rewardImageTr != null)
        {
            Image rewardImage = rewardImageTr.GetComponent<Image>();
            if (rewardImage != null)
            {
                if (iconSprite != null)
                {
                    rewardImage.sprite = iconSprite;
                    rewardImage.color = Color.white; // 스프라이트 보이게
                    Debug.Log($"[RewardIcon] ✅ RewardImage에 아이콘 설정 완료");
                }
                else
                {
                    // 스프라이트 없으면 숨기기
                    rewardImage.color = new Color(1, 1, 1, 0);
                    Debug.LogWarning("[RewardIcon] ⚠️ iconSprite가 null - 아이콘 숨김");
                }
            }
        }
        else
        {
            Debug.LogWarning("[RewardIcon] ⚠️ 'RewardImage' 자식 오브젝트를 찾을 수 없음!");
        }

        // ★ Reward 텍스트 찾아서 수량 세팅
        Transform rewardTextTr = iconObj.transform.Find("Reward");
        if (rewardTextTr != null)
        {
            TextMeshProUGUI text = rewardTextTr.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = amountText;
            }
        }
        else
        {
            // fallback: 아무 TMP 컴포넌트라도 찾기
            TextMeshProUGUI fallbackText = iconObj.GetComponentInChildren<TextMeshProUGUI>();
            if (fallbackText != null)
            {
                fallbackText.text = amountText;
            }
        }
    }

    // ══════════════════════════════════════
    //  진행도 계산
    // ══════════════════════════════════════

    float GetQuestProgressRatio(QuestProgress quest)
    {
        int totalRequired = GetTotalRequiredAmount(quest);
        int totalCurrent = GetTotalCurrentAmount(quest);
        if (totalRequired == 0) return 0f;
        return Mathf.Clamp01((float)totalCurrent / totalRequired);
    }

    int GetTotalRequiredAmount(QuestProgress quest)
    {
        int total = 0;
        foreach (QuestObjective objective in quest.objectives)
            total += objective.requiredAmount;
        return total;
    }

    int GetTotalCurrentAmount(QuestProgress quest)
    {
        int total = 0;
        foreach (QuestObjective objective in quest.objectives)
            total += objective.currentAmount;
        return total;
    }

    // ══════════════════════════════════════
    //  버튼
    // ══════════════════════════════════════

    void OnClaimButtonClicked()
    {
        if (currentQuest != null && QuestManager.Instance != null)
        {
            // ★ 퀘스트 보상 버튼 클릭 효과음
            SoundManager.Instance?.PlayButtonClick();
            bool claimed = QuestManager.Instance.ClaimQuestReward(currentQuest.questData);
            if (claimed)
            {
                Debug.Log($"[QuestSlot] 보상 수령: {currentQuest.questData.questName}");
                QuestManager.Instance.ShowNextQuest();
            }
        }
    }

    public void ClearSlot()
    {
        currentQuest = null;
    }

    void LateUpdate()
    {
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError($"[QuestSlot] ❌ 누가 나를 껐다! 스택: {System.Environment.StackTrace}");
        }
    }
}