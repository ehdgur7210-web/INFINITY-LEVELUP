using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// OfflineRewardManager
/// - 오프라인/온라인 구분 없이 시간 누적
/// - Update()로 매초 누적 (코루틴 재시작 문제 없음)
/// </summary>
public class OfflineRewardManager : MonoBehaviour
{
    public static OfflineRewardManager Instance { get; private set; }

    [Header("보상 레이트 (분당)")]
    public int goldPerMinute = 5;
    public int expPerMinute = 3;
    public int gemPerMinute = 3;
    public float equipmentTicketsPerMinute = 0.1f;  // 분당 0.1개 = 10분에 1개

    [Header("보상 배율")]
    [SerializeField] private float rewardMultiplier = 1.0f;
    [SerializeField] private float adBonusMultiplier = 2.0f;

    [Header("웨이브 연동 보너스 (%)")]
    [SerializeField] private float waveRewardBonusPercent = 5f;

    [Header("아이템 보상")]
    [SerializeField] private OfflineItemReward[] offlineItemRewards;

    [Header("설정")]
    [SerializeField] private float maxAccumulateHours = 24f;
    [SerializeField] private float minClaimMinutes = 1f;

    // ── PlayerPrefs 키 ───────────────────────────
    private const string LAST_SAVE_TIME_KEY = "LastLogoutTime";
    private const string OFFLINE_GOLD_RATE_KEY = "OfflineGoldRate";
    private const string OFFLINE_EXP_RATE_KEY = "OfflineExpRate";
    private const string OFFLINE_GEM_RATE_KEY = "OfflineGemRate";
    private const string OFFLINE_EQUIP_TICKET_KEY = "OfflineEquipTicketRate";  // ★ 수정: 잘린 상수 완성
    private const string CURRENT_WAVE_KEY = "CurrentWaveIndex";
    private const string ACCUMULATED_MINS_KEY = "AccumulatedMinutes";

    // ── 런타임 상태 ──────────────────────────────
    private float accumulatedMinutes = 0f;   // 현재 누적 시간
    private float tickTimer = 0f;   // Update() 내 1초 카운터
    private float uiNotifyTimer = 0f;   // UI 이벤트 발행 간격
    private bool initialized = false;

    // ── 이벤트 ──────────────────────────────────
    public static event Action<OfflineRewardData> OnRewardUpdated;
    public static event Action<OfflineRewardData> OnRewardClaimed;

    // ── 읽기 프로퍼티 ────────────────────────────
    public float AccumulatedMinutes => accumulatedMinutes;
    public float MaxAccumulateMinutes => maxAccumulateHours * 60f;
    public bool IsClaimable => accumulatedMinutes >= minClaimMinutes;

    // ═══════════════════════════════════════════════
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

        }
        else
        {
            // 새 씬의 OfflineRewardManager에 아이템 보상 데이터가 있으면 덮어쓰기
            if (offlineItemRewards != null && offlineItemRewards.Length > 0)
            {
                Instance.offlineItemRewards = offlineItemRewards;
                Instance.goldPerMinute = goldPerMinute;
                Instance.expPerMinute = expPerMinute;
                Instance.gemPerMinute = gemPerMinute;
                Instance.equipmentTicketsPerMinute = equipmentTicketsPerMinute;
                Debug.Log("[OfflineRewardManager] 씬 전환 감지 → 보상 데이터 갱신 완료!");
            }
            enabled = false;
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (Instance != this) return;
        // 1프레임 대기 후 초기화 (다른 매니저 Start 완료 후)
        StartCoroutine(InitDelayed());
    }

    private IEnumerator InitDelayed()
    {
        yield return null;
        yield return null;
        Init();
    }

    private void Init()
    {
        if (initialized) return;  // ★ 중복 초기화 방지
        initialized = true;

        // 저장된 누적 시간 불러오기
        accumulatedMinutes = PlayerPrefs.GetFloat(ACCUMULATED_MINS_KEY, 0f);

        // 오프라인 경과 시간 합산
        string lastSaveStr = PlayerPrefs.GetString(LAST_SAVE_TIME_KEY, "");
        if (!string.IsNullOrEmpty(lastSaveStr))
        {
            DateTime lastSave;
            if (DateTime.TryParse(lastSaveStr, out lastSave))
            {
                double offlineMins = (DateTime.Now - lastSave).TotalMinutes;
                if (offlineMins > 0.1)
                {
                    float add = (float)Math.Min(offlineMins, MaxAccumulateMinutes);
                    accumulatedMinutes += add;
                    accumulatedMinutes = Mathf.Min(accumulatedMinutes, MaxAccumulateMinutes);
                    Debug.Log($"[RewardManager] 오프라인 {offlineMins:F1}분 추가 → 총 {accumulatedMinutes:F1}분");
                }
            }
        }

        SaveCurrentTime();
        NotifyUI();

        Debug.Log($"[RewardManager] 초기화 완료. 누적: {accumulatedMinutes:F1}분");
    }

    // ═══════════════════════════════════════════════
    // ★ Update()로 매초 누적 - 코루틴 재시작 문제 없음
    // ═══════════════════════════════════════════════
    void Update()
    {
        if (!initialized) return;

        // ── 1초마다 1/60분 누적 ──
        tickTimer += Time.unscaledDeltaTime;  // timeScale 0이어도 동작
        if (tickTimer >= 1f)
        {
            tickTimer -= 1f;
            if (accumulatedMinutes < MaxAccumulateMinutes)
            {
                accumulatedMinutes += 1f / 60f;
            }
        }

        // ── 3초마다 UI 이벤트 발행 (매초마다 하면 부하) ──
        uiNotifyTimer += Time.unscaledDeltaTime;
        if (uiNotifyTimer >= 3f)
        {
            uiNotifyTimer = 0f;
            NotifyUI();
            // PlayerPrefs 자동 저장
            PlayerPrefs.SetFloat(ACCUMULATED_MINS_KEY, accumulatedMinutes);
        }
    }

    // ═══════════════════════════════════════════════
    // 보상 계산
    // ═══════════════════════════════════════════════
    public OfflineRewardData CalculateCurrentReward()
    {
        return CalculateReward(accumulatedMinutes);
    }

    private OfflineRewardData CalculateReward(float minutes)
    {
        var reward = new OfflineRewardData();
        reward.offlineDuration = TimeSpan.FromMinutes(minutes);
        reward.effectiveMinutes = minutes;

        int savedWave = WaveSpawner.Instance?.CurrentWaveIndex
                          ?? PlayerPrefs.GetInt(CURRENT_WAVE_KEY, 0);
        float waveBonus = 1f + (savedWave * waveRewardBonusPercent / 100f);

        float goldRate = PlayerPrefs.GetFloat(OFFLINE_GOLD_RATE_KEY, goldPerMinute);
        float expRate = PlayerPrefs.GetFloat(OFFLINE_EXP_RATE_KEY, expPerMinute);
        float gemRate = PlayerPrefs.GetFloat(OFFLINE_GEM_RATE_KEY, gemPerMinute);
        float equipTickRate = PlayerPrefs.GetFloat(OFFLINE_EQUIP_TICKET_KEY, equipmentTicketsPerMinute);  // ★ 추가

        reward.goldReward = Mathf.RoundToInt(goldRate * minutes * rewardMultiplier * waveBonus);
        reward.expReward = Mathf.RoundToInt(expRate * minutes * rewardMultiplier * waveBonus);
        reward.gemReward = Mathf.RoundToInt(gemRate * minutes * rewardMultiplier * waveBonus);
        reward.equipmentTicketReward = Mathf.RoundToInt(equipTickRate * minutes * rewardMultiplier * waveBonus);  // ★ 추가

        reward.itemRewards = CalculateItemRewards(minutes, waveBonus);
        reward.baseMultiplier = rewardMultiplier;
        reward.waveBonus = waveBonus;
        reward.currentWave = savedWave;
        return reward;
    }

    private List<OfflineItemRewardResult> CalculateItemRewards(float minutes, float waveBonus)
    {
        var results = new List<OfflineItemRewardResult>();
        if (offlineItemRewards == null) return results;
        foreach (var ir in offlineItemRewards)
        {
            if (ir.item == null) continue;
            int rolls = Mathf.Max(1, Mathf.FloorToInt(minutes / ir.rollIntervalMinutes));
            int total = 0;
            for (int i = 0; i < rolls; i++)
                if (UnityEngine.Random.Range(0f, 100f) <= ir.dropChance * waveBonus)
                    total += UnityEngine.Random.Range(ir.minAmount, ir.maxAmount + 1);
            if (total > 0)
            {
                total = Mathf.Min(total, ir.maxTotalPerSession);
                results.Add(new OfflineItemRewardResult { item = ir.item, amount = total });
            }
        }
        return results;
    }

    // ═══════════════════════════════════════════════
    // 수령
    // ═══════════════════════════════════════════════
    public void ClaimReward() => ApplyReward(1f);
    public void ClaimRewardWithAd() => ApplyReward(adBonusMultiplier);

    private void ApplyReward(float bonusMultiplier)
    {
        if (!IsClaimable) return;

        OfflineRewardData reward = CalculateCurrentReward();

        int finalGold = Mathf.RoundToInt(reward.goldReward * bonusMultiplier);
        int finalExp = Mathf.RoundToInt(reward.expReward * bonusMultiplier);
        int finalGem = Mathf.RoundToInt(reward.gemReward * bonusMultiplier);
        int finalTicket = Mathf.RoundToInt(reward.equipmentTicketReward * bonusMultiplier);  // ★ 추가

        if (GameManager.Instance != null)
        {
            if (finalGold > 0) GameManager.Instance.AddGold(finalGold);
            if (finalExp > 0) GameManager.Instance.AddExp(finalExp);
            if (finalGem > 0) GameManager.Instance.AddGem(finalGem);
        }

        // ★ 추가: 장비 티켓 지급
        if (finalTicket > 0 && ResourceBarManager.Instance != null)
        {
            ResourceBarManager.Instance.AddEquipmentTickets(finalTicket);
            Debug.Log($"[RewardManager] 장비 티켓 +{finalTicket} 지급 완료");
        }

        if (reward.itemRewards != null && InventoryManager.Instance != null)
            foreach (var item in reward.itemRewards)
                InventoryManager.Instance.AddItem(item.item,
                    Mathf.RoundToInt(item.amount * bonusMultiplier));

        string multi = bonusMultiplier > 1f ? $" (x{bonusMultiplier})" : "";
        UIManager.Instance?.ShowMessage(
            $"보상 수령!{multi}\n골드:{finalGold} EXP:{finalExp} 젬:{finalGem} 장비티켓:{finalTicket}",  // ★ 티켓 표시 추가
            Color.yellow);

        reward.appliedMultiplier = bonusMultiplier;
        OnRewardClaimed?.Invoke(reward);

        // ★ 누적 리셋
        accumulatedMinutes = 0f;
        SaveCurrentTime();
        NotifyUI();

        Debug.Log($"[RewardManager] 수령 완료 - 골드:{finalGold} EXP:{finalExp} 젬:{finalGem} 장비티켓:{finalTicket}");
    }

    // ═══════════════════════════════════════════════
    // 저장 / 유틸
    // ═══════════════════════════════════════════════
    private void NotifyUI()
    {
        OnRewardUpdated?.Invoke(CalculateCurrentReward());
    }

    public void SaveCurrentTime()
    {
        PlayerPrefs.SetString(LAST_SAVE_TIME_KEY, DateTime.Now.ToString("o"));
        PlayerPrefs.SetFloat(OFFLINE_GOLD_RATE_KEY, goldPerMinute);
        PlayerPrefs.SetFloat(OFFLINE_EXP_RATE_KEY, expPerMinute);
        PlayerPrefs.SetFloat(OFFLINE_GEM_RATE_KEY, gemPerMinute);
        PlayerPrefs.SetFloat(OFFLINE_EQUIP_TICKET_KEY, equipmentTicketsPerMinute);  // ★ 추가
        PlayerPrefs.SetFloat(ACCUMULATED_MINS_KEY, accumulatedMinutes);
        if (WaveSpawner.Instance != null)
            PlayerPrefs.SetInt(CURRENT_WAVE_KEY, WaveSpawner.Instance.CurrentWaveIndex);
        PlayerPrefs.Save();
    }

    public void SaveLogoutTime() => SaveCurrentTime();  // 하위 호환

    public void UpgradeOfflineRate(int addGold, int addExp, int addGem = 0)
    {
        goldPerMinute += addGold;
        expPerMinute += addExp;
        gemPerMinute += addGem;
    }

    void OnApplicationPause(bool pause)
    {
        if (pause) SaveCurrentTime();
        else
        {
            // 백그라운드에서 경과한 시간 추가
            string lastSaveStr = PlayerPrefs.GetString(LAST_SAVE_TIME_KEY, "");
            if (!string.IsNullOrEmpty(lastSaveStr))
            {
                DateTime lastSave;
                if (DateTime.TryParse(lastSaveStr, out lastSave))
                {
                    double offlineMins = (DateTime.Now - lastSave).TotalMinutes;
                    if (offlineMins > 0.1)
                    {
                        accumulatedMinutes += (float)Math.Min(offlineMins, MaxAccumulateMinutes);
                        accumulatedMinutes = Mathf.Min(accumulatedMinutes, MaxAccumulateMinutes);
                    }
                }
            }
            SaveCurrentTime();
            NotifyUI();
        }
    }

    void OnApplicationQuit() => SaveCurrentTime();
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
    public int equipmentTicketReward;  // ★ 추가
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