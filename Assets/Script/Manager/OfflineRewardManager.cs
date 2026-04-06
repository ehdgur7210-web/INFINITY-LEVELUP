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
            Debug.Log("[ManagerInit] OfflineRewardManager가 생성되었습니다.");
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

        LoadOfflineData(GameDataBridge.CurrentData);

        SaveCurrentTime();
        NotifyUI();

        Debug.Log($"[RewardManager] 초기화 완료. 누적: {accumulatedMinutes:F1}분");
    }

    /// <summary>SaveLoadManager.ApplySaveData 또는 Init에서 호출</summary>
    public void LoadOfflineData(SaveData data)
    {
        if (data == null) return;

        // ★ Init()에서 이미 처리했으면 중복 계산 방지
        if (initialized && accumulatedMinutes > 0)
        {
            Debug.Log($"[RewardManager] 이미 초기화됨 (누적:{accumulatedMinutes:F1}분) → LoadOfflineData 스킵");
            return;
        }

        accumulatedMinutes = data.accumulatedOfflineMinutes;

        // 2배 보상 횟수 복원
        lastAdClaimDate = data.adClaimDate ?? "";
        todayAdClaimCount = data.adClaimCount;
        // 날짜가 바뀌었으면 리셋
        if (lastAdClaimDate != DateTime.Now.ToString("yyyy-MM-dd"))
        {
            todayAdClaimCount = 0;
            lastAdClaimDate = DateTime.Now.ToString("yyyy-MM-dd");
        }

        // 오프라인 경과 시간 합산
        string lastSaveStr = data.lastLogoutTime ?? "";
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
            else
            {
                Debug.LogWarning($"[RewardManager] lastLogoutTime 파싱 실패: '{lastSaveStr}'");
            }
        }
        else
        {
            Debug.LogWarning("[RewardManager] lastLogoutTime이 비어있음 → 오프라인 시간 합산 불가");
        }
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
            // 인메모리에만 누적 시간 갱신 (파일 쓰기는 SaveCurrentTime에서)
            if (GameDataBridge.CurrentData != null)
                GameDataBridge.CurrentData.accumulatedOfflineMinutes = accumulatedMinutes;
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

        SaveData saved = GameDataBridge.CurrentData;
        int savedWave = WaveSpawner.Instance?.CurrentWaveIndex ?? (saved?.offlineCurrentWave ?? 0);
        float waveBonus = 1f + (savedWave * waveRewardBonusPercent / 100f);

        float goldRate      = (saved != null && saved.offlineGoldRate > 0)      ? saved.offlineGoldRate      : goldPerMinute;
        float expRate       = (saved != null && saved.offlineExpRate > 0)        ? saved.offlineExpRate       : expPerMinute;
        float gemRate       = (saved != null && saved.offlineGemRate > 0)        ? saved.offlineGemRate       : gemPerMinute;
        float equipTickRate = (saved != null && saved.offlineEquipTicketRate > 0) ? saved.offlineEquipTicketRate : equipmentTicketsPerMinute;

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
    [Header("2배 보상 (8시간 기준)")]
    [Tooltip("2배 버튼 시 지급할 시간 (시간 단위)")]
    [SerializeField] private float adBonusHours = 8f;
    [Tooltip("하루 최대 2배 보상 사용 횟수")]
    [SerializeField] private int maxAdClaimPerDay = 3;

    private int todayAdClaimCount = 0;
    private string lastAdClaimDate = "";

    public int RemainingAdClaims => maxAdClaimPerDay - GetTodayAdClaimCount();

    public void ClaimReward() => ApplyReward(1f, false);
    public void ClaimRewardWithAd() => ApplyReward(1f, true);

    private int GetTodayAdClaimCount()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (lastAdClaimDate != today)
        {
            todayAdClaimCount = 0;
            lastAdClaimDate = today;
        }
        return todayAdClaimCount;
    }

    private void ApplyReward(float bonusMultiplier, bool isAdClaim)
    {
        // ★ 일반 수령은 최소 누적 시간 필요, 2배 보상은 누적 시간 무관 (고정 8시간분)
        if (!isAdClaim && !IsClaimable) return;

        int finalGold, finalExp, finalGem, finalTicket;
        string label;

        if (isAdClaim)
        {
            // ★ 2배 보상: 시간당 레이트 × 8시간 (누적 시간 무관)
            if (GetTodayAdClaimCount() >= maxAdClaimPerDay)
            {
                UIManager.Instance?.ShowMessage(
                    $"오늘 2배 보상 횟수를 모두 사용했습니다! ({maxAdClaimPerDay}/{maxAdClaimPerDay})", Color.red);
                return;
            }

            float adMinutes = adBonusHours * 60f;

            SaveData saved = GameDataBridge.CurrentData;
            int savedWave = WaveSpawner.Instance?.CurrentWaveIndex ?? (saved?.offlineCurrentWave ?? 0);
            float waveBonus = 1f + (savedWave * waveRewardBonusPercent / 100f);

            float goldRate  = (saved != null && saved.offlineGoldRate > 0) ? saved.offlineGoldRate : goldPerMinute;
            float expRate   = (saved != null && saved.offlineExpRate > 0) ? saved.offlineExpRate : expPerMinute;
            float gemRate   = (saved != null && saved.offlineGemRate > 0) ? saved.offlineGemRate : gemPerMinute;
            float tickRate  = (saved != null && saved.offlineEquipTicketRate > 0) ? saved.offlineEquipTicketRate : equipmentTicketsPerMinute;

            finalGold   = Mathf.RoundToInt(goldRate * adMinutes * rewardMultiplier * waveBonus);
            finalExp    = Mathf.RoundToInt(expRate * adMinutes * rewardMultiplier * waveBonus);
            finalGem    = Mathf.RoundToInt(gemRate * adMinutes * rewardMultiplier * waveBonus);
            finalTicket = Mathf.RoundToInt(tickRate * adMinutes * rewardMultiplier * waveBonus);

            todayAdClaimCount++;
            label = $"2배 보상! ({(int)adBonusHours}시간분)\n남은 횟수: {RemainingAdClaims}/{maxAdClaimPerDay}";
        }
        else
        {
            // ★ 일반 수령: 누적 시간 기반
            OfflineRewardData reward = CalculateCurrentReward();
            finalGold   = Mathf.RoundToInt(reward.goldReward * bonusMultiplier);
            finalExp    = Mathf.RoundToInt(reward.expReward * bonusMultiplier);
            finalGem    = Mathf.RoundToInt(reward.gemReward * bonusMultiplier);
            finalTicket = Mathf.RoundToInt(reward.equipmentTicketReward * bonusMultiplier);
            label = "보상 수령!";
        }

        // ── 지급 ──
        Debug.Log($"[RewardManager] 지급 시도 — 골드:{finalGold} EXP:{finalExp} 젬:{finalGem} 티켓:{finalTicket} | GameManager:{GameManager.Instance != null}");

        if (GameManager.Instance != null)
        {
            if (finalGold > 0) GameManager.Instance.AddGold(finalGold);
            if (finalExp > 0)
            {
                int prevLevel = GameManager.Instance.PlayerLevel;
                GameManager.Instance.AddExp(finalExp);
                int newLevel = GameManager.Instance.PlayerLevel;
                Debug.Log($"[RewardManager] EXP +{finalExp} → Lv.{prevLevel} → Lv.{newLevel}");
            }
            if (finalGem > 0) GameManager.Instance.AddGem(finalGem);
        }
        else
        {
            Debug.LogError("[RewardManager] GameManager.Instance가 null! 보상 지급 불가!");
        }

        if (finalTicket > 0 && ResourceBarManager.Instance != null)
            ResourceBarManager.Instance.AddEquipmentTickets(finalTicket);

        // 일반 수령일 때만 아이템 보상
        if (!isAdClaim)
        {
            OfflineRewardData reward = CalculateCurrentReward();
            if (reward.itemRewards != null && InventoryManager.Instance != null)
                foreach (var item in reward.itemRewards)
                    InventoryManager.Instance.AddItem(item.item, item.amount);
        }

        UIManager.Instance?.ShowMessage(
            $"{label}\n골드:{finalGold} EXP:{finalExp} 젬:{finalGem} 티켓:{finalTicket}",
            Color.yellow);

        OnRewardClaimed?.Invoke(CalculateCurrentReward());

        // ★ 누적 리셋
        accumulatedMinutes = 0f;
        SaveCurrentTime();

        // 2배 횟수 저장
        if (GameDataBridge.CurrentData != null)
        {
            GameDataBridge.CurrentData.adClaimCount = todayAdClaimCount;
            GameDataBridge.CurrentData.adClaimDate = lastAdClaimDate;
        }

        NotifyUI();
        SaveLoadManager.Instance?.SaveGame();

        Debug.Log($"[RewardManager] 수령 완료 — 골드:{finalGold} EXP:{finalExp} 젬:{finalGem} 티켓:{finalTicket}" +
                  (isAdClaim ? $" [2배보상 {todayAdClaimCount}/{maxAdClaimPerDay}]" : ""));
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
        SaveData data = GameDataBridge.CurrentData;
        if (data != null)
        {
            data.lastLogoutTime            = DateTime.Now.ToString("o");
            data.offlineGoldRate           = goldPerMinute;
            data.offlineExpRate            = expPerMinute;
            data.offlineGemRate            = gemPerMinute;
            data.offlineEquipTicketRate    = equipmentTicketsPerMinute;
            data.accumulatedOfflineMinutes = accumulatedMinutes;
            if (WaveSpawner.Instance != null)
                data.offlineCurrentWave = WaveSpawner.Instance.CurrentWaveIndex;
        }
        GameDataBridge.WriteToFile(GameDataBridge.ActiveSlot);
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
            string lastSaveStr = GameDataBridge.CurrentData?.lastLogoutTime ?? "";
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