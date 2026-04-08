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
    public float companionTicketsPerMinute = 0.05f; // 분당 0.05개 = 20분에 1개
    public int cropPointsPerMinute = 2;             // 분당 2 작물 포인트

    [Header("보상 배율")]
    [SerializeField] private float rewardMultiplier = 1.0f;
    [SerializeField] private float adBonusMultiplier = 2.0f;

    [Header("웨이브 연동 보너스 (%)")]
    [SerializeField] private float waveRewardBonusPercent = 5f;

    [Header("★ 레벨 연동 보너스 (%) — Lv 71이면 71 * 1 = 71% 추가")]
    [Tooltip("플레이어 레벨 1당 보상 % 증가. 0이면 비활성화. 추천: 1.0")]
    [SerializeField] private float levelRewardBonusPercent = 1.0f;

    [Header("★ cropPoint 안전 최소값")]
    [Tooltip("계산 결과가 0이어도 minutes > 0이면 최소 이 값만큼 지급. 0이면 비활성화.")]
    [SerializeField] private int cropPointMinimumIfTimeElapsed = 1;

    [Header("아이템 보상")]
    [SerializeField] private OfflineItemReward[] offlineItemRewards;

    [Header("★ 오프라인 장비 드랍")]
    [Tooltip("등급별 풀 (Common/Uncommon/Rare 각각의 EquipmentData 배열을 등록)")]
    [SerializeField] private OfflineEquipmentDrop[] equipmentDrops;

    [Tooltip("분당 1회 굴림 — 이 확률로 성공하면 등급 룰렛 후 1개 드랍")]
    [Range(0f, 100f)]
    [SerializeField] private float equipmentDropChancePerMinute = 80f;

    [Tooltip("세션당 최대 장비 드랍 수 (인벤 폭주 방지)")]
    [SerializeField] private int maxEquipmentDropsPerSession = 300;

    [Header("설정")]
    [SerializeField] private float maxAccumulateHours = 24f;
    [SerializeField] private float minClaimMinutes = 1f;


    // ── 런타임 상태 ──────────────────────────────
    private float accumulatedMinutes = 0f;   // 현재 누적 시간
    private float tickTimer = 0f;   // Update() 내 1초 카운터
    private float uiNotifyTimer = 0f;   // UI 이벤트 발행 간격
    private bool initialized = false;

    // ★ 드랍 결과 캐싱 — 매 갱신마다 랜덤이 다시 굴려지지 않도록
    //    같은 분(minute) 안에서는 캐시 반환, 새 분으로 넘어가면 새로 굴림
    private int cachedDropMinute = -1;
    private List<OfflineItemRewardResult> cachedItemDrops;
    private List<OfflineEquipmentDropResult> cachedEquipmentDrops;

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
                Instance.companionTicketsPerMinute = companionTicketsPerMinute;
                Instance.cropPointsPerMinute = cropPointsPerMinute;
                if (equipmentDrops != null && equipmentDrops.Length > 0)
                    Instance.equipmentDrops = equipmentDrops;
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

        // ★ 레벨 기반 보너스 — 골드/젬은 stage에 따라 offlineGoldRate가 갱신되지만
        //   cropPointsPerMinute는 static config라 레벨이 올라도 그대로. 형평성을 위해 levelBonus 곱셈.
        int playerLv = (saved != null) ? Mathf.Max(1, saved.playerLevel) : 1;
        float levelBonus = 1f + (playerLv * levelRewardBonusPercent / 100f);

        reward.goldReward = Mathf.RoundToInt(goldRate * minutes * rewardMultiplier * waveBonus);
        reward.expReward = Mathf.RoundToInt(expRate * minutes * rewardMultiplier * waveBonus);
        reward.gemReward = Mathf.RoundToInt(gemRate * minutes * rewardMultiplier * waveBonus);
        reward.companionTicketReward = Mathf.RoundToInt(companionTicketsPerMinute * minutes * rewardMultiplier * waveBonus);
        reward.cropPointReward = Mathf.RoundToInt(cropPointsPerMinute * minutes * rewardMultiplier * waveBonus * levelBonus);

        // ★ 안전 최소값 — minutes>0 인데 RoundToInt가 0이면 최소 cropPointMinimumIfTimeElapsed 보장
        if (reward.cropPointReward == 0 && minutes > 0f && cropPointsPerMinute > 0 && cropPointMinimumIfTimeElapsed > 0)
            reward.cropPointReward = cropPointMinimumIfTimeElapsed;

        // ★ 드랍 결과 캐시 — 같은 분(minute) 안에서는 동일 결과 반환
        int currentMinute = Mathf.FloorToInt(minutes);
        if (cachedDropMinute != currentMinute || cachedItemDrops == null || cachedEquipmentDrops == null)
        {
            cachedItemDrops = CalculateItemRewards(minutes, waveBonus);
            cachedEquipmentDrops = CalculateEquipmentDrops(minutes);
            cachedDropMinute = currentMinute;
        }
        reward.itemRewards = cachedItemDrops;
        reward.equipmentDropResults = cachedEquipmentDrops;
        reward.baseMultiplier = rewardMultiplier;
        reward.waveBonus = waveBonus;
        reward.currentWave = savedWave;
        return reward;
    }

    // ═══════════════════════════════════════════════
    // ★ 오프라인 장비 드랍 계산
    //   - 분당 1회 굴림
    //   - equipmentDropChancePerMinute 확률로 성공
    //   - 성공 시 가중치 룰렛으로 등급 결정 → 풀에서 랜덤 선택
    // ═══════════════════════════════════════════════
    private List<OfflineEquipmentDropResult> CalculateEquipmentDrops(float minutes)
    {
        var grouped = new Dictionary<EquipmentData, int>();

        if (equipmentDrops == null || equipmentDrops.Length == 0)
            return new List<OfflineEquipmentDropResult>();

        // 가중치 합 계산
        float totalWeight = 0f;
        foreach (var d in equipmentDrops)
            if (d != null && d.pool != null && d.pool.Length > 0) totalWeight += Mathf.Max(0f, d.weight);

        if (totalWeight <= 0f)
            return new List<OfflineEquipmentDropResult>();

        int totalRolls = Mathf.FloorToInt(minutes); // 분당 1회
        int successCount = 0;

        for (int i = 0; i < totalRolls && successCount < maxEquipmentDropsPerSession; i++)
        {
            // 1) 드랍 시도
            if (UnityEngine.Random.Range(0f, 100f) > equipmentDropChancePerMinute) continue;

            // 2) 등급 룰렛
            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            OfflineEquipmentDrop selected = null;
            foreach (var d in equipmentDrops)
            {
                if (d == null || d.pool == null || d.pool.Length == 0) continue;
                cumulative += Mathf.Max(0f, d.weight);
                if (roll <= cumulative) { selected = d; break; }
            }
            if (selected == null) continue;

            // 3) 풀에서 랜덤 1개
            EquipmentData picked = selected.pool[UnityEngine.Random.Range(0, selected.pool.Length)];
            if (picked == null) continue;

            if (grouped.ContainsKey(picked)) grouped[picked]++;
            else grouped[picked] = 1;
            successCount++;
        }

        var results = new List<OfflineEquipmentDropResult>(grouped.Count);
        foreach (var kvp in grouped)
            results.Add(new OfflineEquipmentDropResult { equipment = kvp.Key, amount = kvp.Value });
        return results;
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

        int finalGold, finalExp, finalGem, finalCompTicket, finalCropPoint;
        string label;

        if (isAdClaim)
        {
            // ★ 2배 보상: 시간당 레이트 × 8시간 (누적 시간 무관)
            //    아이템/장비는 미지급 — 8시간분 화폐만 지급
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

            // ★ 레벨 기반 보너스 (cropPoint 전용 — gold/exp/gem은 별도 stage rate가 있음)
            int playerLv = (saved != null) ? Mathf.Max(1, saved.playerLevel) : 1;
            float levelBonus = 1f + (playerLv * levelRewardBonusPercent / 100f);

            finalGold       = Mathf.RoundToInt(goldRate * adMinutes * rewardMultiplier * waveBonus);
            finalExp        = Mathf.RoundToInt(expRate * adMinutes * rewardMultiplier * waveBonus);
            finalGem        = Mathf.RoundToInt(gemRate * adMinutes * rewardMultiplier * waveBonus);
            finalCompTicket = Mathf.RoundToInt(companionTicketsPerMinute * adMinutes * rewardMultiplier * waveBonus);
            finalCropPoint  = Mathf.RoundToInt(cropPointsPerMinute * adMinutes * rewardMultiplier * waveBonus * levelBonus);
            if (finalCropPoint == 0 && cropPointsPerMinute > 0 && cropPointMinimumIfTimeElapsed > 0)
                finalCropPoint = cropPointMinimumIfTimeElapsed;

            todayAdClaimCount++;
            label = $"2배 보상! ({(int)adBonusHours}시간분)\n남은 횟수: {RemainingAdClaims}/{maxAdClaimPerDay}";
        }
        else
        {
            // ★ 일반 수령: 누적 시간 기반
            OfflineRewardData reward = CalculateCurrentReward();
            finalGold       = Mathf.RoundToInt(reward.goldReward * bonusMultiplier);
            finalExp        = Mathf.RoundToInt(reward.expReward * bonusMultiplier);
            finalGem        = Mathf.RoundToInt(reward.gemReward * bonusMultiplier);
            finalCompTicket = Mathf.RoundToInt(reward.companionTicketReward * bonusMultiplier);
            finalCropPoint  = Mathf.RoundToInt(reward.cropPointReward * bonusMultiplier);
            label = "보상 수령!";
        }

        // ── 지급 ──
        Debug.Log($"[RewardManager] 지급 시도 — 골드:{finalGold} EXP:{finalExp} 젬:{finalGem} 동료T:{finalCompTicket} CP:{finalCropPoint}");

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

        if (ResourceBarManager.Instance != null && finalCompTicket > 0)
            ResourceBarManager.Instance.AddCompanionTickets(finalCompTicket);

        // ★ cropPoint 지급 — 단일 source of truth (CropPointService)에 위임.
        //   FarmManager / ResourceBarManager / GameDataBridge 분기 폴백이 더 이상 필요 없음.
        if (finalCropPoint > 0)
        {
            long cpBefore = CropPointService.Value;
            CropPointService.Add(finalCropPoint);
            Debug.Log($"[RewardManager:CP] +{finalCropPoint} (이전:{cpBefore} → 지금:{CropPointService.Value})");
        }

        // ★ 클레임된 보상 데이터 (이벤트로 UI에 전달)
        OfflineRewardData claimedReward = new OfflineRewardData
        {
            goldReward = finalGold,
            expReward = finalExp,
            gemReward = finalGem,
            companionTicketReward = finalCompTicket,
            cropPointReward = finalCropPoint,
            itemRewards = new List<OfflineItemRewardResult>(),
            equipmentDropResults = new List<OfflineEquipmentDropResult>()
        };

        // ★ 일반 수령: 누적 시간 기반 / 2배 보상: 8시간분 새로 굴림
        //    둘 다 아이템 + 장비 드랍 지급
        {
            List<OfflineItemRewardResult> itemList;
            List<OfflineEquipmentDropResult> equipList;

            if (isAdClaim)
            {
                // 2배 보상: 8시간분 새로 굴려서 지급 (캐시 우회)
                float adMinutes = adBonusHours * 60f;
                SaveData savedAd = GameDataBridge.CurrentData;
                int savedWaveAd = WaveSpawner.Instance?.CurrentWaveIndex ?? (savedAd?.offlineCurrentWave ?? 0);
                float waveBonusAd = 1f + (savedWaveAd * waveRewardBonusPercent / 100f);

                itemList = CalculateItemRewards(adMinutes, waveBonusAd);
                equipList = CalculateEquipmentDrops(adMinutes);
            }
            else
            {
                // 일반 수령: 캐시된 결과 사용
                OfflineRewardData reward = CalculateCurrentReward();
                itemList = reward.itemRewards;
                equipList = reward.equipmentDropResults;
            }

            if (itemList != null && InventoryManager.Instance != null)
                foreach (var item in itemList)
                {
                    InventoryManager.Instance.AddItem(item.item, item.amount);
                    claimedReward.itemRewards.Add(item);
                }

            if (equipList != null && InventoryManager.Instance != null)
                foreach (var eq in equipList)
                    if (eq.equipment != null && eq.amount > 0)
                    {
                        InventoryManager.Instance.AddItem(eq.equipment, eq.amount, false);
                        claimedReward.equipmentDropResults.Add(eq);
                    }

            // 장비 추가 후 인벤 갱신 1회
            InventoryManager.Instance?.RefreshEquipDisplay();
        }

        // ★ 토스트 메시지 제거 — 대신 OnRewardClaimed 이벤트로 UI가 스크롤에 표시
        OnRewardClaimed?.Invoke(claimedReward);

        // ★ 누적 리셋 + 캐시 초기화 (다음 누적부터 새로 굴림)
        accumulatedMinutes = 0f;
        cachedDropMinute = -1;
        cachedItemDrops = null;
        cachedEquipmentDrops = null;
        SaveCurrentTime();

        // 2배 횟수 저장
        if (GameDataBridge.CurrentData != null)
        {
            GameDataBridge.CurrentData.adClaimCount = todayAdClaimCount;
            GameDataBridge.CurrentData.adClaimDate = lastAdClaimDate;
        }

        NotifyUI();
        SaveLoadManager.Instance?.SaveGame();

        Debug.Log($"[RewardManager] 수령 완료 — 골드:{finalGold} EXP:{finalExp} 젬:{finalGem} 동료T:{finalCompTicket} CP:{finalCropPoint}" +
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
    public int companionTicketReward;
    public int cropPointReward;
    public List<OfflineItemRewardResult> itemRewards;
    public List<OfflineEquipmentDropResult> equipmentDropResults;
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

// ═══════════════════════════════════════════════════════════
// ★ 오프라인 장비 드랍 — 등급별 풀
// ═══════════════════════════════════════════════════════════

[System.Serializable]
public class OfflineEquipmentDrop
{
    [Tooltip("이 풀의 등급 (Common/Uncommon/Rare 권장)")]
    public ItemRarity rarity = ItemRarity.Common;

    [Tooltip("가중치 (예: 70/25/5)")]
    [Range(0f, 100f)] public float weight = 70f;

    [Tooltip("이 등급에서 뽑힐 수 있는 장비들")]
    public EquipmentData[] pool;
}

[System.Serializable]
public class OfflineEquipmentDropResult
{
    public EquipmentData equipment;
    public int amount;
}