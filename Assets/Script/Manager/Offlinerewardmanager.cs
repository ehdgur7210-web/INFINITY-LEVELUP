using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// OfflineRewardManager
/// ✅ 오프라인 보상: 게임 복귀 시 방치 시간 계산 후 팝업
/// ✅ 온라인 보상: 게임 플레이 중 X분마다 자동 누적 → 수령 버튼 활성화
/// </summary>
public class OfflineRewardManager : MonoBehaviour
{
    public static OfflineRewardManager Instance { get; private set; }

    // ═══════════════════════════════════════════════════════
    // 공통 보상 레이트 (오프라인/온라인 동일하게 사용)
    // ═══════════════════════════════════════════════════════
    [Header("보상 레이트 (분당)")]
    [SerializeField] private int goldPerMinute = 5;
    [SerializeField] private int expPerMinute = 3;
    [SerializeField] private int gemPerMinute = 3;

    [Header("보상 배율")]
    [SerializeField] private float rewardMultiplier = 1.0f;
    [SerializeField] private float adBonusMultiplier = 2.0f;

    [Header("웨이브 연동 보너스")]
    [SerializeField] private float waveRewardBonusPercent = 5f;

    [Header("아이템 방치 보상")]
    [SerializeField] private OfflineItemReward[] offlineItemRewards;

    // ═══════════════════════════════════════════════════════
    // 오프라인 전용 설정
    // ═══════════════════════════════════════════════════════
    [Header("── 오프라인 설정 ──")]
    [SerializeField] private float maxOfflineHours = 24f;
    [SerializeField] private float minOfflineMinutes = 1f;
    [SerializeField] private GameObject offlineRewardPopupPrefab;

    // ═══════════════════════════════════════════════════════
    // 온라인 전용 설정
    // ═══════════════════════════════════════════════════════
    [Header("── 온라인 보상 설정 ──")]
    [Tooltip("온라인 보상이 쌓이는 간격 (분)")]
    [SerializeField] private float onlineAccumulateIntervalMinutes = 2f;  // 2분마다 누적
    [Tooltip("수령 버튼 활성화 최소 누적 시간 (분)")]
    [SerializeField] private float onlineClaimMinMinutes = 2f;
    [Tooltip("최대 누적 가능 시간 (분) - 이 이상 쌓이지 않음")]
    [SerializeField] private float onlineMaxAccumulateMinutes = 120f;
    [Tooltip("온라인 보상 레이트 배율 (오프라인 대비)")]
    [SerializeField] private float onlineRateMultiplier = 1.0f;

    // ═══════════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════════
    private const string LAST_LOGOUT_TIME_KEY = "LastLogoutTime";
    private const string OFFLINE_GOLD_RATE_KEY = "OfflineGoldRate";
    private const string OFFLINE_EXP_RATE_KEY = "OfflineExpRate";
    private const string OFFLINE_GEM_RATE_KEY = "OfflineGemRate";
    private const string CURRENT_WAVE_KEY = "CurrentWaveIndex";

    // 오프라인
    private OfflineRewardData lastCalculatedReward;
    private bool hasCheckedOnStart = false;

    // 온라인 누적
    private float onlineAccumulatedMinutes = 0f;   // 현재 누적된 분
    private bool isOnlineAccumulating = false;
    private Coroutine onlineCoroutine;

    // 이벤트
    public static event Action<OfflineRewardData> OnOfflineRewardCalculated;
    public static event Action<OfflineRewardData> OnOfflineRewardClaimed;
    public static event Action<float> OnOnlineRewardAccumulated;  // 현재 누적 분 전달
    public static event Action OnOnlineRewardReady;        // 수령 가능 알림

    // 읽기 프로퍼티
    public float OnlineAccumulatedMinutes => onlineAccumulatedMinutes;
    public bool IsOnlineRewardReady => onlineAccumulatedMinutes >= onlineClaimMinMinutes;
    public float OnlineMaxMinutes => onlineMaxAccumulateMinutes;

    // ═══════════════════════════════════════════════════════
    // Unity 생명주기
    // ═══════════════════════════════════════════════════════
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        hasCheckedOnStart = true;
        StartCoroutine(CheckOfflineRewardDelayed());
        StartOnlineAccumulation();  // ★ 온라인 보상 누적 시작
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!hasCheckedOnStart) return;
        StartCoroutine(CheckOfflineRewardDelayed());
    }

    private IEnumerator CheckOfflineRewardDelayed()
    {
        yield return null;
        yield return null;
        CheckOfflineReward();
    }

    // ═══════════════════════════════════════════════════════
    // ★ 온라인 보상 시스템
    // ═══════════════════════════════════════════════════════

    public void StartOnlineAccumulation()
    {
        if (isOnlineAccumulating) return;
        isOnlineAccumulating = true;

        if (onlineCoroutine != null) StopCoroutine(onlineCoroutine);
        onlineCoroutine = StartCoroutine(OnlineAccumulateCoroutine());
        Debug.Log("[OnlineReward] 온라인 보상 누적 시작");
    }

    public void StopOnlineAccumulation()
    {
        isOnlineAccumulating = false;
        if (onlineCoroutine != null)
        {
            StopCoroutine(onlineCoroutine);
            onlineCoroutine = null;
        }
    }

    /// <summary>
    /// 매 interval마다 누적 시간 증가
    /// </summary>
    private IEnumerator OnlineAccumulateCoroutine()
    {
        float intervalSeconds = onlineAccumulateIntervalMinutes * 60f;

        while (isOnlineAccumulating)
        {
            yield return new WaitForSeconds(intervalSeconds);

            // 최대치 미만일 때만 누적
            if (onlineAccumulatedMinutes < onlineMaxAccumulateMinutes)
            {
                onlineAccumulatedMinutes += onlineAccumulateIntervalMinutes;
                onlineAccumulatedMinutes = Mathf.Min(onlineAccumulatedMinutes, onlineMaxAccumulateMinutes);

                Debug.Log($"[OnlineReward] 누적: {onlineAccumulatedMinutes:F1}분");
                OnOnlineRewardAccumulated?.Invoke(onlineAccumulatedMinutes);

                // 수령 가능 최초 도달 시 이벤트 발행
                if (onlineAccumulatedMinutes >= onlineClaimMinMinutes)
                    OnOnlineRewardReady?.Invoke();
            }
        }
    }

    /// <summary>
    /// ★ 온라인 보상 수령 (UI 수령 버튼에서 호출)
    /// </summary>
    public void ClaimOnlineReward()
    {
        if (onlineAccumulatedMinutes < onlineClaimMinMinutes)
        {
            Debug.Log("[OnlineReward] 아직 수령 불가");
            return;
        }

        float minutes = onlineAccumulatedMinutes;
        onlineAccumulatedMinutes = 0f;   // 리셋

        OfflineRewardData reward = CalculateReward(minutes, TimeSpan.FromMinutes(minutes), isOnline: true);
        ApplyReward(reward, 1f);

        OnOnlineRewardAccumulated?.Invoke(0f);
        Debug.Log($"[OnlineReward] 수령 완료: {minutes:F1}분치");
    }

    /// <summary>
    /// ★ 온라인 보상 미리보기 (수령 버튼 툴팁용)
    /// </summary>
    public OfflineRewardData PreviewOnlineReward()
    {
        if (onlineAccumulatedMinutes <= 0f) return null;
        return CalculateReward(onlineAccumulatedMinutes,
                               TimeSpan.FromMinutes(onlineAccumulatedMinutes),
                               isOnline: true);
    }

    // ═══════════════════════════════════════════════════════
    // 오프라인 보상 시스템 (기존 유지)
    // ═══════════════════════════════════════════════════════

    public void CheckOfflineReward()
    {
        string lastLogoutStr = PlayerPrefs.GetString(LAST_LOGOUT_TIME_KEY, "");
        if (string.IsNullOrEmpty(lastLogoutStr)) { SaveLogoutTime(); return; }

        DateTime lastLogoutTime;
        if (!DateTime.TryParse(lastLogoutStr, out lastLogoutTime)) { SaveLogoutTime(); return; }

        TimeSpan offlineDuration = DateTime.Now - lastLogoutTime;
        double offlineMinutes = offlineDuration.TotalMinutes;

        Debug.Log($"[OfflineReward] 오프라인: {offlineDuration.Hours}h {offlineDuration.Minutes}m");

        if (offlineMinutes < minOfflineMinutes) return;

        double effectiveMinutes = Math.Min(offlineMinutes, maxOfflineHours * 60.0);
        OfflineRewardData reward = CalculateReward(effectiveMinutes,
                                                   offlineDuration,
                                                   isOnline: false);
        lastCalculatedReward = reward;
        OnOfflineRewardCalculated?.Invoke(reward);
        ShowOfflineRewardPopup(reward);
    }

    // ═══════════════════════════════════════════════════════
    // 보상 계산 (오프라인/온라인 공용)
    // ═══════════════════════════════════════════════════════

    private OfflineRewardData CalculateReward(double effectiveMinutes,
                                              TimeSpan actualDuration,
                                              bool isOnline)
    {
        OfflineRewardData reward = new OfflineRewardData();
        reward.offlineDuration = actualDuration;
        reward.effectiveMinutes = effectiveMinutes;
        reward.isOnlineReward = isOnline;

        int savedWave = WaveSpawner.Instance?.CurrentWaveIndex
                        ?? PlayerPrefs.GetInt(CURRENT_WAVE_KEY, 0);
        float waveBonus = 1f + (savedWave * waveRewardBonusPercent / 100f);

        float rateMulti = isOnline ? onlineRateMultiplier : 1f;

        float goldRate = isOnline ? goldPerMinute
                       : PlayerPrefs.GetFloat(OFFLINE_GOLD_RATE_KEY, goldPerMinute);
        float expRate = isOnline ? expPerMinute
                       : PlayerPrefs.GetFloat(OFFLINE_EXP_RATE_KEY, expPerMinute);
        float gemRate = isOnline ? gemPerMinute
                       : PlayerPrefs.GetFloat(OFFLINE_GEM_RATE_KEY, gemPerMinute);

        reward.goldReward = Mathf.RoundToInt((float)(goldRate * effectiveMinutes
                                             * rewardMultiplier * waveBonus * rateMulti));
        reward.expReward = Mathf.RoundToInt((float)(expRate * effectiveMinutes
                                             * rewardMultiplier * waveBonus * rateMulti));
        reward.gemReward = Mathf.RoundToInt((float)(gemRate * effectiveMinutes
                                             * rewardMultiplier * waveBonus * rateMulti));

        reward.itemRewards = CalculateItemRewards(effectiveMinutes, waveBonus);
        reward.baseMultiplier = rewardMultiplier;
        reward.waveBonus = waveBonus;
        reward.currentWave = savedWave;

        string tag = isOnline ? "온라인" : "오프라인";
        Debug.Log($"[{tag}Reward] 골드:{reward.goldReward} EXP:{reward.expReward} 젬:{reward.gemReward}");
        return reward;
    }

    private List<OfflineItemRewardResult> CalculateItemRewards(double minutes, float waveBonus)
    {
        var results = new List<OfflineItemRewardResult>();
        if (offlineItemRewards == null) return results;

        foreach (var itemReward in offlineItemRewards)
        {
            if (itemReward.item == null) continue;
            int rollCount = Mathf.Max(1, Mathf.FloorToInt((float)(minutes / itemReward.rollIntervalMinutes)));
            int totalDropped = 0;

            for (int i = 0; i < rollCount; i++)
                if (UnityEngine.Random.Range(0f, 100f) <= itemReward.dropChance * waveBonus)
                    totalDropped += UnityEngine.Random.Range(itemReward.minAmount, itemReward.maxAmount + 1);

            if (totalDropped > 0)
            {
                totalDropped = Mathf.Min(totalDropped, itemReward.maxTotalPerSession);
                results.Add(new OfflineItemRewardResult { item = itemReward.item, amount = totalDropped });
            }
        }
        return results;
    }

    // ═══════════════════════════════════════════════════════
    // 보상 적용
    // ═══════════════════════════════════════════════════════

    public void ClaimReward() => ApplyReward(lastCalculatedReward, 1f);
    public void ClaimRewardWithAd() => ApplyReward(lastCalculatedReward, adBonusMultiplier);

    private void ApplyReward(OfflineRewardData reward, float bonusMultiplier)
    {
        if (reward == null) return;

        int finalGold = Mathf.RoundToInt(reward.goldReward * bonusMultiplier);
        int finalExp = Mathf.RoundToInt(reward.expReward * bonusMultiplier);
        int finalGem = Mathf.RoundToInt(reward.gemReward * bonusMultiplier);

        if (GameManager.Instance != null)
        {
            if (finalGold > 0) GameManager.Instance.AddGold(finalGold);
            if (finalExp > 0) GameManager.Instance.AddExp(finalExp);
            if (finalGem > 0) GameManager.Instance.AddGem(finalGem);
        }

        if (reward.itemRewards != null && InventoryManager.Instance != null)
            foreach (var item in reward.itemRewards)
            {
                int amt = Mathf.RoundToInt(item.amount * bonusMultiplier);
                InventoryManager.Instance.AddItem(item.item, amt);
            }

        string tag = reward.isOnlineReward ? "온라인" : "방치";
        string multi = bonusMultiplier > 1f ? $" (x{bonusMultiplier})" : "";
        UIManager.Instance?.ShowMessage(
            $"{tag} 보상 수령!{multi}\n골드:{finalGold} EXP:{finalExp} 젬:{finalGem}",
            Color.yellow);

        reward.appliedMultiplier = bonusMultiplier;
        OnOfflineRewardClaimed?.Invoke(reward);

        if (!reward.isOnlineReward)
            lastCalculatedReward = null;

        // 수령 후 로그아웃 시간 갱신 (다음 오프라인 계산 기준점 리셋)
        SaveLogoutTime();
    }

    // ═══════════════════════════════════════════════════════
    // 저장 / 유틸
    // ═══════════════════════════════════════════════════════

    public void SaveLogoutTime()
    {
        string now = DateTime.Now.ToString("o");
        PlayerPrefs.SetString(LAST_LOGOUT_TIME_KEY, now);
        PlayerPrefs.SetFloat(OFFLINE_GOLD_RATE_KEY, goldPerMinute);
        PlayerPrefs.SetFloat(OFFLINE_EXP_RATE_KEY, expPerMinute);
        PlayerPrefs.SetFloat(OFFLINE_GEM_RATE_KEY, gemPerMinute);

        if (WaveSpawner.Instance != null)
            PlayerPrefs.SetInt(CURRENT_WAVE_KEY, WaveSpawner.Instance.CurrentWaveIndex);

        PlayerPrefs.Save();
    }

    private void ShowOfflineRewardPopup(OfflineRewardData reward)
    {
        OfflineRewardUI existingUI = FindObjectOfType<OfflineRewardUI>(true);
        if (existingUI != null) { existingUI.ShowReward(reward); return; }

        if (offlineRewardPopupPrefab != null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                GameObject popup = Instantiate(offlineRewardPopupPrefab, canvas.transform);
                popup.GetComponent<OfflineRewardUI>()?.ShowReward(reward);
            }
        }
        else
        {
            Debug.LogWarning("[OfflineReward] UI 없음 - 자동 수령");
            ClaimReward();
        }
    }

    public void UpgradeOfflineRate(int addGold, int addExp, int addGem = 0)
    {
        goldPerMinute += addGold;
        expPerMinute += addExp;
        gemPerMinute += addGem;
    }

    public void SetRewardMultiplier(float multiplier)
        => rewardMultiplier = Mathf.Max(0.1f, multiplier);

    public string GetRateInfo()
    {
        int waveIndex = WaveSpawner.Instance?.CurrentWaveIndex ?? 0;
        float waveBonus = 1f + (waveIndex * waveRewardBonusPercent / 100f);
        return $"분당 골드:{Mathf.RoundToInt(goldPerMinute * rewardMultiplier * waveBonus)} " +
               $"EXP:{Mathf.RoundToInt(expPerMinute * rewardMultiplier * waveBonus)} " +
               $"젬:{Mathf.RoundToInt(gemPerMinute * rewardMultiplier * waveBonus)}";
    }

    void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            StopOnlineAccumulation();
            SaveLogoutTime();
        }
        else
        {
            CheckOfflineReward();
            StartOnlineAccumulation();
        }
    }

    void OnApplicationQuit()
    {
        SaveLogoutTime();
    }
}

// ═══════════════════════════════════════════════════════════
// 데이터 클래스
// ═══════════════════════════════════════════════════════════

[System.Serializable]
public class OfflineRewardData
{
    public TimeSpan offlineDuration;
    public double effectiveMinutes;
    public int goldReward;
    public int expReward;
    public int gemReward;
    public bool isOnlineReward;                    // ★ 온라인/오프라인 구분
    public List<OfflineItemRewardResult> itemRewards;
    public float baseMultiplier;
    public float waveBonus;
    public int currentWave;
    public float appliedMultiplier = 1f;

    public string GetDurationString()
    {
        if (offlineDuration.TotalHours >= 1)
            return $"{(int)offlineDuration.TotalHours}시간 {offlineDuration.Minutes}분";
        if (offlineDuration.TotalMinutes >= 1)
            return $"{(int)offlineDuration.TotalMinutes}분";
        return $"{offlineDuration.Seconds}초";
    }
}

[System.Serializable]
public class OfflineItemRewardResult
{
    public ItemData item;
    public int amount;
}

[System.Serializable]
public class OfflineItemReward
{
    public ItemData item;
    [Range(0f, 100f)] public float dropChance = 10f;
    public float rollIntervalMinutes = 10f;
    [Min(1)] public int minAmount = 1;
    [Min(1)] public int maxAmount = 1;
    public int maxTotalPerSession = 10;
}