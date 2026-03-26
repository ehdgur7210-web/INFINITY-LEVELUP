using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

// 업적 타입

// 업적 시스템 관리
public class AchievementSystem : MonoBehaviour
{
    public static AchievementSystem Instance;

    [Header("모든 업적")]
    public List<AchievementData> allAchievements = new List<AchievementData>();

    [Header("업적 진행 상황")]
    public List<AchievementProgress> achievementProgress = new List<AchievementProgress>();

    [Header("업적 UI")]
    public GameObject achievementPanel;
    public Transform achievementListParent;
    public GameObject achievementSlotPrefab;

    [Header("헤더 UI")]
    public TMPro.TextMeshProUGUI completionText;  // ⭐ 완료율 텍스트

    [Header("알림 UI")]
    public GameObject achievementNotification;
    public TMPro.TextMeshProUGUI notificationText;

    [Header("버튼")]
    public Button closeButton;  // ⭐ 닫기 버튼

    private Dictionary<int, AchievementProgress> progressDictionary;

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

        InitializeAchievements();
    }

    void Start()
    {
        if (achievementPanel != null)
        {
            achievementPanel.SetActive(false);
        }

        if (achievementNotification != null)
        {
            achievementNotification.SetActive(false);
        }

        // ⭐ 닫기 버튼 이벤트 연결
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseAchievementUI);
        }
    }

    void Update()
    {
        // A 키로 업적 UI 토글
        if (Input.GetKeyDown(KeyCode.Z))
        {
            ToggleAchievementUI();
        }

        // ESC 키로 닫기
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (achievementPanel != null && achievementPanel.activeSelf)
            {
                CloseAchievementUI();
            }
        }
    }

    /// <summary>
    /// 업적 시스템 초기화
    /// </summary>
    void InitializeAchievements()
    {
        progressDictionary = new Dictionary<int, AchievementProgress>();

        foreach (AchievementData achievement in allAchievements)
        {
            AchievementProgress progress = new AchievementProgress(achievement);
            achievementProgress.Add(progress);
            progressDictionary[achievement.achievementID] = progress;
        }

        Debug.Log($"업적 시스템 초기화: {allAchievements.Count}개");
    }

    /// <summary>
    /// 업적 UI 토글
    /// </summary>
    public void ToggleAchievementUI()
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.ShouldBlockNonFocusButtons) return;
        if (achievementPanel != null)
        {
            bool isActive = achievementPanel.activeSelf;

            if (isActive)
            {
                CloseAchievementUI();
            }
            else
            {
                OpenAchievementUI();
            }
        }
    }

    /// <summary>
    /// ⭐ 업적 UI 열기
    /// </summary>
    public void OpenAchievementUI()
    {
        // 튜토리얼 진행 중이면 업적 패널 안 열기
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            return;

        if (achievementPanel != null)
        {
            achievementPanel.SetActive(true);
            UpdateAchievementUI();
            UpdateCompletionText();
        }
    }

    /// <summary>
    /// ⭐ 업적 UI 닫기
    /// </summary>
    public void CloseAchievementUI()
    {
        if (achievementPanel != null)
        {
            achievementPanel.SetActive(false);
        }
    }

    /// <summary>
    /// ⭐ 완료율 텍스트 업데이트
    /// </summary>
    void UpdateCompletionText()
    {
        if (completionText != null)
        {
            int completed = GetCompletedCount();
            int total = allAchievements.Count;
            float rate = GetCompletionRate();

            completionText.text = $"{completed}/{total} 완료 ({rate:F1}%)";
        }
    }

    /// <summary>
    /// 업적 진행 업데이트
    /// </summary>
    public void UpdateAchievementProgress(AchievementType type, string targetID, int amount)
    {
        Debug.Log($"=== 업적 업데이트 호출 ===");
        Debug.Log($"Type: {type}, TargetID: '{targetID}', Amount: {amount}");

        foreach (AchievementProgress progress in achievementProgress)
        {
            // 이미 완료되었으면 스킵
            if (progress.isCompleted) continue;

            // 타입이 일치하는지 확인
            if (progress.achievement.type != type) continue;

            // TargetID가 있는 경우 확인
            if (!string.IsNullOrEmpty(progress.achievement.targetID))
            {
                if (progress.achievement.targetID != targetID) continue;
            }

            // 진행도 증가
            int oldAmount = progress.currentAmount;
            progress.currentAmount += amount;

            Debug.Log($" {progress.achievement.achievementName}: {oldAmount} → {progress.currentAmount}/{progress.achievement.targetAmount}");

            // 목표 달성 체크
            if (progress.currentAmount >= progress.achievement.targetAmount)
            {
                progress.currentAmount = progress.achievement.targetAmount;
                Debug.Log($" 목표 달성! CompleteAchievement 호출");
                CompleteAchievement(progress);
            }
        }
    }

    /// <summary>
    /// 업적 완료
    /// </summary>
    void CompleteAchievement(AchievementProgress progress)
    {
        if (progress.isCompleted) return;

        progress.isCompleted = true;

        Debug.Log($" 업적 달성! {progress.achievement.achievementName}");

        // 알림 표시
        ShowAchievementNotification(progress.achievement);

        //  업적 UI 자동 열기
        if (achievementPanel != null && !achievementPanel.activeSelf)
        {
            OpenAchievementUI();
        }
        else if (achievementPanel != null && achievementPanel.activeSelf)
        {
            UpdateAchievementUI();
            UpdateCompletionText();
        }
    }
    /// <summary>
    /// 업적 보상 수령
    /// </summary>
    public void ClaimAchievementReward(AchievementProgress progress)
    {
        if (!progress.isCompleted || progress.isRewarded) return;
        // ★ 업적 보상 수령 효과음
        SoundManager.Instance?.PlayAchievementReward();

        AchievementReward reward = progress.achievement.reward;

        // 골드 지급
        if (reward.gold > 0 && GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(reward.gold);
        }

        // 보석 지급
        if (reward.gem > 0 && GameManager.Instance != null)
        {
            GameManager.Instance.AddGem(reward.gem);
        }

        // 경험치 지급
        if (reward.exp > 0 && GameManager.Instance != null)
        {
            GameManager.Instance.AddExp(reward.exp);
        }

        // 아이템 지급
        if (reward.items != null && reward.items.Length > 0)
        {
            foreach (ItemData item in reward.items)
            {
                if (item != null && InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.AddItem(item, 1);
                }
            }
        }

        // 칭호 지급 (TitleSystem과 연동 필요)
        if (!string.IsNullOrEmpty(reward.title))
        {
            Debug.Log($"칭호 획득: {reward.title}");
        }

        progress.isRewarded = true;

        Debug.Log($"업적 보상 수령: {progress.achievement.achievementName}");

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessage("업적 보상 획득!", Color.green);
        }

        // UI 갱신
        if (achievementPanel != null && achievementPanel.activeSelf)
        {
            UpdateAchievementUI();
            UpdateCompletionText();
        }

        SaveLoadManager.Instance?.SaveGame();
    }

    /// <summary>
    /// 업적 알림 표시
    /// </summary>
    void ShowAchievementNotification(AchievementData achievement)
    {
        // 튜토리얼 진행 중이면 알림 안 띄움
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            return;

        if (achievementNotification != null && notificationText != null)
        {
            notificationText.text = $" 업적 달성!\n{achievement.achievementName}";
            achievementNotification.SetActive(true);

            // 3초 후 자동 숨김
            Invoke("HideNotification", 3f);
        }
    }

    void HideNotification()
    {
        if (achievementNotification != null)
        {
            achievementNotification.SetActive(false);
        }
    }

    /// <summary>
    /// 업적 UI 업데이트
    /// </summary>
    void UpdateAchievementUI()
    {
        if (achievementListParent == null || achievementSlotPrefab == null) return;

        // 기존 슬롯 제거
        foreach (Transform child in achievementListParent)
        {
            Destroy(child.gameObject);
        }

        // 새 슬롯 생성
        foreach (AchievementProgress progress in achievementProgress)
        {
            // 숨김 업적이고 미완성이면 스킵 (선택사항)
            // if (progress.achievement.isHidden && !progress.isCompleted) continue;

            GameObject slotObj = Instantiate(achievementSlotPrefab, achievementListParent);
            AchievementSlot slot = slotObj.GetComponent<AchievementSlot>();

            if (slot != null)
            {
                slot.SetupSlot(progress);
            }
        }

        Debug.Log($"업적 UI 업데이트: {achievementProgress.Count}개");
    }

    /// <summary>
    /// 완료된 업적 개수
    /// </summary>
    public int GetCompletedCount()
    {
        int count = 0;
        foreach (AchievementProgress progress in achievementProgress)
        {
            if (progress.isCompleted) count++;
        }
        return count;
    }

    /// <summary>
    /// 전체 완료율
    /// </summary>
    public float GetCompletionRate()
    {
        if (allAchievements.Count == 0) return 0f;
        return (float)GetCompletedCount() / allAchievements.Count * 100f;
    }

    // ─────────────────────────────────────────
    // ★ Fix: 저장/로드 (SaveLoadManager 연동)
    // ─────────────────────────────────────────
    public AchievementSaveEntry[] GetAchievementSaveData()
    {
        var result = new System.Collections.Generic.List<AchievementSaveEntry>();
        foreach (var progress in achievementProgress)
        {
            if (progress.achievement == null) continue;
            result.Add(new AchievementSaveEntry
            {
                achievementID = progress.achievement.achievementID,
                isCompleted = progress.isCompleted,
                isRewarded = progress.isRewarded,
                currentCount = progress.currentAmount
            });
        }
        Debug.Log($"[AchievementSystem] 저장: {result.Count}개 업적");
        return result.ToArray();
    }

    public void LoadAchievementSaveData(AchievementSaveEntry[] entries)
    {
        if (entries == null) return;

        foreach (var entry in entries)
        {
            if (!progressDictionary.TryGetValue(entry.achievementID, out AchievementProgress progress))
                continue;

            progress.isCompleted = entry.isCompleted;
            progress.isRewarded = entry.isRewarded;
            progress.currentAmount = entry.currentCount;
        }

        UpdateAchievementUI();
        Debug.Log($"[AchievementSystem] 로드: {entries.Length}개 업적 복원");
    }
}