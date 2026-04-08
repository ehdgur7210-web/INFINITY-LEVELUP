using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Video;

/// <summary>
/// EquipmentData 기반 가챠 시스템 (ResourceBarManager 연동)
/// ⭐ 티켓 시스템은 ResourceBarManager에서 관리
///
/// ★ 패치 내용:
///   - 레벨 1→2 전환 시 Gold + CropPoint 추가 비용 소모
///   - GachaEquipmentTierManager 연동 (원자/분자/DNA 티어)
/// </summary>
public class GachaManager : MonoBehaviour
{
    public static GachaManager Instance;

    [System.Serializable]
    public class GachaItem
    {
        public EquipmentData equipment;
        public float probability;
    }

    [Header("가챠 설정")]
    public int ticketCostPerGacha = 1;      // 1회당 티켓 소모량

    [Header("다연차 횟수 (풀별 설정)")]
    [Tooltip("다연차 버튼 1 횟수 (기본 10연차)")]
    public int multiGachaCount = 10;
    [Tooltip("다연차 버튼 2 횟수 (기본 100연차)")]
    public int bulkGachaCount = 100;

    [Header("VIP 경험치 보상")]
    [Tooltip("다연차 1 완료 시 VIP 경험치")]
    public int vipExpPerMultiGacha = 50;
    [Tooltip("다연차 2 완료 시 VIP 경험치")]
    public int vipExpPerBulkGacha = 200;

    [Header("가챠 풀 (레벨별)")]
    public List<GachaItem> gachaPoolLv1 = new List<GachaItem>();
    public List<GachaItem> gachaPoolLv2 = new List<GachaItem>();
    public List<GachaItem> gachaPoolLv3 = new List<GachaItem>();
    public List<GachaItem> gachaPoolLv4 = new List<GachaItem>();
    public List<GachaItem> gachaPoolLv5 = new List<GachaItem>();

    [Header("레벨 시스템")]
    public int currentLevel = 1;
    public int maxLevel = 5;
    public int gachaCountForLevelUp = 10;
    public int currentGachaCount = 0;

    // ★ 레벨별 업그레이드 비용 (각 레벨에서 다음 레벨로 가는 비용)
    [Header("레벨별 업그레이드 비용")]
    [Tooltip("인덱스 0 = 레벨 1→2, 인덱스 1 = 레벨 2→3, ... 인덱스 N = 레벨 N+1→N+2\n비어있거나 해당 인덱스의 비용이 0이면 무료")]
    public GachaLevelUpCost[] gachaLevelUpCosts = new GachaLevelUpCost[]
    {
        new GachaLevelUpCost { goldCost = 500,    cropPointCost = 100 },  // 1→2
        new GachaLevelUpCost { goldCost = 2000,   cropPointCost = 300 },  // 2→3
        new GachaLevelUpCost { goldCost = 5000,   cropPointCost = 500 },  // 3→4
        new GachaLevelUpCost { goldCost = 10000,  cropPointCost = 1000 }, // 4→5
    };

    // 하위 호환 (legacy 필드 — gachaLevelUpCosts[0]에서 자동 매핑)
    public int lv1to2GoldCost => gachaLevelUpCosts != null && gachaLevelUpCosts.Length > 0 ? gachaLevelUpCosts[0].goldCost : 500;
    public int lv1to2CropPointCost => gachaLevelUpCosts != null && gachaLevelUpCosts.Length > 0 ? gachaLevelUpCosts[0].cropPointCost : 100;

    [System.Serializable]
    public class GachaLevelUpCost
    {
        public int goldCost;
        public int cropPointCost;
    }

    [Header("UI 참조")]
    public GameObject gachaPanel;
    public GameObject lampButton;

    // ════════════════════════════════════════════════════════
    //  ★ Timeline 연출 시스템
    // ════════════════════════════════════════════════════════

    [Header("★ 가챠 연출 (Timeline)")]
    [Tooltip("가챠 연출 애니메이션 패널 (PlayableDirector 포함)")]
    public GameObject gachaAnimationPanel;

    [Tooltip("Timeline 재생용 PlayableDirector")]
    public PlayableDirector gachaDirector;

    [Header("★ 가챠 결과 표시 패널 (Timeline 종료 후)")]
    [Tooltip("결과 표시 패널 루트")]
    public GameObject gachaResultPanel;

    [Tooltip("결과 장비 아이콘")]
    public UnityEngine.UI.Image resultItemIcon;

    [Tooltip("결과 장비 이름")]
    public TMPro.TextMeshProUGUI resultItemNameText;

    [Tooltip("결과 장비 등급")]
    public TMPro.TextMeshProUGUI resultRarityText;

    [Tooltip("결과 확인 버튼")]
    public UnityEngine.UI.Button resultConfirmButton;

    [Tooltip("등급별 배경색 (Common, Uncommon, Rare, Epic, Legendary 순)")]
    public Color[] rarityColors = new Color[]
    {
        new Color(0.8f, 0.8f, 0.8f),   // Common
        new Color(0.3f, 0.8f, 0.3f),   // Uncommon
        new Color(0.3f, 0.5f, 1f),     // Rare
        new Color(0.7f, 0.3f, 0.9f),   // Epic
        new Color(1f, 0.8f, 0.2f)      // Legendary
    };

    [Tooltip("1회 뽑기에만 연출 적용 (10연/100연은 기존 방식)")]
    public bool animationOnlyForSingle = true;

    // ════════════════════════════════════════════════════════
    //  ★ MP4 동영상 뽑기 연출
    // ════════════════════════════════════════════════════════

    [Header("★ 가챠 연출 (Video)")]
    [Tooltip("VideoPlayer 컴포넌트 (RawImage + RenderTexture 또는 Camera 모드)")]
    public VideoPlayer gachaVideoPlayer;

    [Tooltip("기본 가챠 연출 동영상 (등급별 미설정 시 사용)")]
    public VideoClip defaultGachaClip;

    [Tooltip("등급별 가챠 연출 동영상 (Common, Uncommon, Rare, Epic, Legendary 순)")]
    public VideoClip[] rarityGachaClips;

    [Tooltip("영상 스킵 버튼 (선택사항)")]
    public UnityEngine.UI.Button videoSkipButton;

    [Tooltip("연출 모드: Video 우선 사용 (false면 Timeline 우선)")]
    public bool preferVideoOverTimeline = true;

    // 연출 중 임시 저장할 결과 데이터
    private List<EquipmentData> pendingResults;
    private bool isPlayingAnimation = false;

    [Header("디버그")]
    public bool debugMode = true;

    private List<GachaItem> currentGachaPool;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] GachaManager가 생성되었습니다.");
            transform.SetParent(null);
        }
        else
        {
            // ✅ 새 씬(MainScene)의 GachaManager에 Inspector 데이터가 있으면
            //    기존(Intro씬) 인스턴스에 덮어쓰고 새것은 제거
            bool hasData = (gachaPoolLv1 != null && gachaPoolLv1.Count > 0) ||
                           (gachaPoolLv2 != null && gachaPoolLv2.Count > 0) ||
                           (gachaPoolLv3 != null && gachaPoolLv3.Count > 0) ||
                           (gachaPoolLv4 != null && gachaPoolLv4.Count > 0) ||
                           (gachaPoolLv5 != null && gachaPoolLv5.Count > 0);

            if (hasData)
            {
                Instance.gachaPoolLv1 = new List<GachaItem>(gachaPoolLv1);
                Instance.gachaPoolLv2 = new List<GachaItem>(gachaPoolLv2);
                Instance.gachaPoolLv3 = new List<GachaItem>(gachaPoolLv3);
                Instance.gachaPoolLv4 = new List<GachaItem>(gachaPoolLv4);
                Instance.gachaPoolLv5 = new List<GachaItem>(gachaPoolLv5);
                Instance.UpdateGachaPool();
                Instance.ValidateGachaPool();
                Debug.Log("[GachaManager] 씬 전환 감지 → 가챠 풀 갱신 완료!");
            }
            else
            {
                Debug.LogWarning("[GachaManager] 새 씬의 가챠 풀이 비어있습니다! Inspector에서 데이터를 연결해주세요.");
            }

            enabled = false;
            Destroy(gameObject);
            return;
        }
    }

    private bool isInitialized = false;

    void Start()
    {
        if (Instance != this) return;
        Init();
    }

    /// <summary>
    /// 게임 시작 시 호출 — 가챠 풀 설정 + UI 슬롯 풀링 초기화
    /// </summary>
    public void Init()
    {
        if (isInitialized) return;

        UpdateGachaPool();

        if (gachaPanel != null)
            gachaPanel.SetActive(true);

        ValidateGachaPool();
        SetupAnimationSystem();

        // ★ GachaResultUI 슬롯 풀 초기화 (Awake가 아닌 게임 시작 시 1회)
        InitGachaResultPool();

        isInitialized = true;
        Debug.Log("[GachaManager] Init 완료 — 슬롯 풀링 포함");
    }

    private void InitGachaResultPool()
    {
        // GachaResultUI가 비활성이면 찾아서 활성화
        if (GachaResultUI.Instance == null)
        {
            var found = FindObjectOfType<GachaResultUI>(true);
            if (found != null)
            {
                found.gameObject.SetActive(true);
                Debug.Log("[GachaManager] GachaResultUI 강제 활성화 → 풀 초기화용");
            }
        }

        if (GachaResultUI.Instance != null)
        {
            GachaResultUI.Instance.InitSlotPool();

            // ★ 풀 초기화 후 CanvasGroup으로 숨김 (SetActive(false) 사용 금지 — Start() 미호출 방지)
            GameObject rp = GachaResultUI.Instance.resultPanel;
            if (rp != null)
            {
                if (rp == GachaResultUI.Instance.gameObject)
                {
                    CanvasGroup cg = rp.GetComponent<CanvasGroup>();
                    if (cg == null) cg = rp.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                    cg.blocksRaycasts = false;
                    cg.interactable = false;
                }
                else
                {
                    rp.SetActive(false);
                }
            }
        }
        else
        {
            Debug.LogWarning("[GachaManager] GachaResultUI를 찾을 수 없어 슬롯 풀 초기화 불가");
        }
    }

    void Update() { }

    // ★ MainGameInitializer에서 씬 복귀 시 호출
    public void BindGachaUI(GameObject panel, GameObject lamp = null)
    {
        gachaPanel = panel;
        if (lamp != null) lampButton = lamp;
        if (gachaPanel != null) gachaPanel.SetActive(true);
        Debug.Log("[GachaManager] UI 재연결 완료");
    }

    /// <summary>
    /// ★ 연출 UI 재연결 (씬 복귀 시 호출)
    /// </summary>
    public void BindAnimationUI(GameObject animPanel, PlayableDirector director,
        GameObject resultPanel, UnityEngine.UI.Image icon,
        TMPro.TextMeshProUGUI nameText, TMPro.TextMeshProUGUI rarText,
        UnityEngine.UI.Button confirmBtn,
        VideoPlayer videoPlayer = null, UnityEngine.UI.Button skipBtn = null)
    {
        // 기존 이벤트 해제
        if (gachaDirector != null)
            gachaDirector.stopped -= OnTimelineStopped;
        if (gachaVideoPlayer != null)
            gachaVideoPlayer.loopPointReached -= OnVideoFinished;

        gachaAnimationPanel = animPanel;
        gachaDirector = director;
        gachaResultPanel = resultPanel;
        resultItemIcon = icon;
        resultItemNameText = nameText;
        resultRarityText = rarText;
        resultConfirmButton = confirmBtn;

        if (videoPlayer != null) gachaVideoPlayer = videoPlayer;
        if (skipBtn != null) videoSkipButton = skipBtn;

        SetupAnimationSystem();
        Debug.Log("[GachaManager] 연출 UI 재연결 완료");
    }

    public void UpdateGachaPool()
    {
        switch (currentLevel)
        {
            case 1: currentGachaPool = gachaPoolLv1; break;
            case 2: currentGachaPool = gachaPoolLv2; break;
            case 3: currentGachaPool = gachaPoolLv3; break;
            case 4: currentGachaPool = gachaPoolLv4; break;
            case 5: currentGachaPool = gachaPoolLv5; break;
            default: currentGachaPool = gachaPoolLv1; break;
        }

        if (debugMode)
            Debug.Log($"[GachaManager] 가챠 풀 업데이트: 레벨 {currentLevel}");
    }

    public void ValidateGachaPool()
    {
        if (currentGachaPool == null || currentGachaPool.Count == 0)
        {
            Debug.LogWarning("[GachaManager] 현재 레벨의 가챠 풀이 비어있습니다!");
            return;
        }

        float totalProbability = 0f;
        foreach (GachaItem gachaItem in currentGachaPool)
            totalProbability += gachaItem.probability;

        if (Mathf.Abs(totalProbability - 100f) > 0.01f)
            Debug.LogWarning($"[GachaManager] 가챠 확률 총합이 100%가 아닙니다! 현재: {totalProbability}%");
        else
            Debug.Log($"[GachaManager] 가챠 풀 검증 완료! 레벨 {currentLevel}, 총 확률: {totalProbability}%");
    }

    // ════════════════════════════════════════════════════════
    //  뽑기 공개 메서드
    // ════════════════════════════════════════════════════════

    /// <summary>1회 가챠 (ResourceBarManager 연동)</summary>
    /// <summary>
    /// ★ 단일 가챠 비활성화 — 다연차만 사용.
    /// 기존 버튼이 호출해도 안전하게 다연차로 폴백.
    /// </summary>
    public void PerformSingleGacha()
    {
        // 단일 가챠 비활성화 — 조용히 무시 (공지 메시지 출력 안 함)
    }

    /// <summary>다연차 1 (multiGachaCount 횟수만큼)</summary>
    public void PerformTenGacha()
    {
        Debug.Log($"[GachaManager] ========== {multiGachaCount}연차 가챠 시작 ==========");

        if (!CheckGachaPool()) return;
        if (!SpendTickets(ticketCostPerGacha * multiGachaCount)) return;

        List<EquipmentData> results = new List<EquipmentData>();
        for (int i = 0; i < multiGachaCount; i++)
        {
            EquipmentData result = PerformGacha();
            if (result != null)
            {
                results.Add(result);
                InventoryManager.Instance?.AddItem(result, 1, false);
                IncrementGachaCount();
            }
        }

        if (vipExpPerMultiGacha > 0)
            VipManager.Instance?.AddVipExp(vipExpPerMultiGacha);

        SaveLoadManager.Instance?.SaveGame();
        ShowGachaResults(results);
        RefreshGachaUI();
        InventoryManager.Instance?.RefreshEquipDisplay();

        Debug.Log($"[GachaManager] ========== {multiGachaCount}연차 가챠 완료 ==========");
    }

    /// <summary>다연차 2 (bulkGachaCount 횟수만큼)</summary>
    public void PerformHundredGacha()
    {
        Debug.Log($"[GachaManager] ========== {bulkGachaCount}연차 가챠 시작 ==========");

        if (!CheckGachaPool()) return;
        if (!SpendTickets(ticketCostPerGacha * bulkGachaCount)) return;

        UIManager.Instance?.ShowMessage($"{bulkGachaCount}연차 뽑기 중...", Color.white);
        StartCoroutine(BulkGachaCoroutine());
    }

    /// <summary>대량 연차 코루틴 — 프레임 분산으로 멈춤 방지</summary>
    private System.Collections.IEnumerator BulkGachaCoroutine()
    {
        var results = new List<EquipmentData>(bulkGachaCount);
        int batchSize = Mathf.Max(25, bulkGachaCount / 4);

        // Phase 1: 뽑기 (batchSize개씩 프레임 분산)
        for (int i = 0; i < bulkGachaCount; i++)
        {
            EquipmentData result = PerformGacha();
            if (result != null) results.Add(result);
            if ((i + 1) % batchSize == 0) yield return null;
        }

        // Phase 2: 인벤토리 일괄 추가
        if (InventoryManager.Instance != null)
        {
            for (int i = 0; i < results.Count; i++)
            {
                InventoryManager.Instance.AddItem(results[i], 1, false);
                if ((i + 1) % batchSize == 0) yield return null;
            }
        }

        // Phase 3: 카운트 + 레벨업
        currentGachaCount += results.Count;
        AchievementSystem.Instance?.UpdateAchievementProgress(AchievementType.GachaCount, "", results.Count);
        QuestManager.Instance?.UpdateQuestProgress(QuestType.Gacha, "", results.Count);
        while (currentGachaCount >= gachaCountForLevelUp && currentLevel < maxLevel)
        {
            if (!TryPayLevelUpCost(currentLevel)) { currentGachaCount = gachaCountForLevelUp; break; }
            LevelUp();
        }

        yield return null;

        // Phase 4: 결과 표시
        int leg = 0, epic = 0, rare = 0;
        foreach (var item in results)
        {
            if (item.rarity == ItemRarity.Legendary) leg++;
            else if (item.rarity == ItemRarity.Epic) epic++;
            else if (item.rarity == ItemRarity.Rare) rare++;
        }

        if (vipExpPerBulkGacha > 0)
            VipManager.Instance?.AddVipExp(vipExpPerBulkGacha);

        SaveLoadManager.Instance?.SaveGame();
        ShowGachaResults(results);
        RefreshGachaUI();
        InventoryManager.Instance?.RefreshEquipDisplay();

        UIManager.Instance?.ShowMessage($"{bulkGachaCount}연차 완료! 전설 {leg}개!", Color.yellow);
        Debug.Log($"[GachaManager] {bulkGachaCount}연차 결과: 전설{leg} / 영웅{epic} / 희귀{rare}");
    }

    // ════════════════════════════════════════════════════════
    //  내부 뽑기 로직
    // ════════════════════════════════════════════════════════

    public EquipmentData PerformGacha()
    {
        if (currentGachaPool == null || currentGachaPool.Count == 0)
        {
            Debug.LogWarning("[GachaManager] 가챠 풀이 비어있습니다!");
            return null;
        }

        bool hasValidItem = currentGachaPool.Exists(g => g != null && g.equipment != null);
        if (!hasValidItem)
        {
            Debug.LogError("[GachaManager] 가챠 풀에 유효한 equipment가 없습니다! Inspector 확인!");
            UIManager.Instance?.ShowMessage("가챠 데이터 오류!", Color.red);
            return null;
        }

        float randomValue = Random.Range(0f, 100f);
        float cumulativeProbability = 0f;

        foreach (GachaItem gachaItem in currentGachaPool)
        {
            if (gachaItem == null || gachaItem.equipment == null)
            {
                Debug.LogWarning("[GachaManager] equipment가 비어있는 항목이 있습니다!");
                continue;
            }

            cumulativeProbability += gachaItem.probability;

            if (randomValue <= cumulativeProbability)
            {
                if (debugMode)
                    Debug.Log($"[GachaManager] 뽑기: {gachaItem.equipment.itemName} " +
                              $"(확률: {gachaItem.probability}%, 굴림: {randomValue:F2})");
                return gachaItem.equipment;
            }
        }

        // 폴백: 마지막 항목
        GachaItem lastItem = currentGachaPool[currentGachaPool.Count - 1];
        if (lastItem != null && lastItem.equipment != null)
            return lastItem.equipment;

        Debug.LogError("[GachaManager] 가챠 결과를 생성할 수 없습니다! 모든 equipment가 null입니다.");
        return null;
    }

    // ════════════════════════════════════════════════════════
    //  가챠 카운트 증가 + 레벨업 처리
    //  ★ 패치: 레벨 1→2 전환 시 Gold + CropPoint 비용 확인
    // ════════════════════════════════════════════════════════

    void IncrementGachaCount()
    {
        currentGachaCount++;

        if (debugMode)
            Debug.Log($"[GachaManager] 가챠 횟수: {currentGachaCount}/{gachaCountForLevelUp}");

        // 업적 연동
        AchievementSystem.Instance?.UpdateAchievementProgress(AchievementType.GachaCount, "", 1);

        // 퀘스트 연동
        QuestManager.Instance?.UpdateQuestProgress(QuestType.Gacha, "", 1);

        if (currentGachaCount >= gachaCountForLevelUp)
        {
            if (currentLevel < maxLevel)
            {
                // ★ 레벨업 비용 확인 (레벨 1→2는 Gold + CropPoint 필요)
                if (!TryPayLevelUpCost(currentLevel))
                {
                    // 비용 부족 → 레벨업 보류. 카운트는 최대치에서 유지.
                    currentGachaCount = gachaCountForLevelUp;
                    Debug.Log("[GachaManager] 레벨업 비용 부족 - 비용 충전 후 다시 뽑으면 레벨업됩니다.");
                    return;
                }
                LevelUp();
            }
            else
            {
                // MAX 레벨: 카운트 리셋 후 전설 보장
                currentGachaCount = 0;
                Debug.Log($"[GachaManager] MAX Lv.{maxLevel} - 카운트 리셋, 무한 가챠 계속!");
                GuaranteedLegendary();
            }
        }
    }

    /// <summary>
    /// 레벨업 비용 지불 — gachaLevelUpCosts 배열 우선 사용
    /// fromLevel 1 → index 0, fromLevel 2 → index 1, ...
    /// 배열 범위 밖이거나 비용 0이면 무료
    /// </summary>
    private bool TryPayLevelUpCost(int fromLevel)
    {
        int costIndex = fromLevel - 1; // fromLevel 1 → index 0

        // 배열 범위 밖이면 무료
        if (gachaLevelUpCosts == null || costIndex < 0 || costIndex >= gachaLevelUpCosts.Length)
            return true;

        GachaLevelUpCost cost = gachaLevelUpCosts[costIndex];

        // 비용이 둘 다 0이면 무료
        if (cost.goldCost <= 0 && cost.cropPointCost <= 0)
            return true;

        long currentGold = GameManager.Instance != null ? GameManager.Instance.PlayerGold : 0;
        long currentCP = FarmManager.Instance != null ? FarmManager.Instance.GetCropPoints() : 0;

        // 골드 확인
        if (cost.goldCost > 0)
        {
            if (GameManager.Instance == null || currentGold < cost.goldCost)
            {
                UIManager.Instance?.ShowConfirmDialog(
                    $"가챠레벨업에골드가부족합니다.\n필요:{cost.goldCost:N0}G\n보유:{UIManager.FormatKoreanUnit(currentGold)}G",
                    onConfirm: null);
                return false;
            }
        }

        // CropPoint 확인
        if (cost.cropPointCost > 0)
        {
            if (FarmManager.Instance == null || currentCP < cost.cropPointCost)
            {
                UIManager.Instance?.ShowConfirmDialog(
                    $"가챠레벨업을하기위해선\n작물포인트가필요합니다.\n필요:{cost.cropPointCost}CP\n보유:{currentCP}CP",
                    onConfirm: null);
                return false;
            }
        }

        // 차감
        if (cost.goldCost > 0) GameManager.Instance.SpendGold(cost.goldCost);
        if (cost.cropPointCost > 0) FarmManager.Instance.SpendCropPoints(cost.cropPointCost);

        UIManager.Instance?.ShowMessage(
            $"레벨 {fromLevel}→{fromLevel + 1} 업그레이드!\n-{cost.goldCost:N0}G / -{cost.cropPointCost}CP",
            Color.cyan);
        return true;
    }

    // ════════════════════════════════════════════════════════
    //  천장 보장 / 레벨업
    // ════════════════════════════════════════════════════════

    private void GuaranteedLegendary()
    {
        List<EquipmentData> legendaries = new List<EquipmentData>();
        foreach (var item in currentGachaPool)
        {
            if (item != null && item.equipment != null &&
                item.equipment.rarity == ItemRarity.Legendary)
                legendaries.Add(item.equipment);
        }

        if (legendaries.Count == 0)
        {
            Debug.LogWarning("[GachaManager] 전설 장비가 없어 천장 보장 불가!");
            return;
        }

        EquipmentData guaranteed = legendaries[Random.Range(0, legendaries.Count)];
        InventoryManager.Instance?.AddItem(guaranteed, 1, false);
        InventoryManager.Instance?.RefreshEquipDisplay();

        Debug.Log($"[GachaManager] ⭐ 천장 보장! 전설 지급: {guaranteed.itemName}");
        UIManager.Instance?.ShowMessage($"천장 달성! [{guaranteed.itemName}] 획득!", Color.yellow);

        ShowGachaResults(new List<EquipmentData> { guaranteed });
        RefreshGachaUI();
    }

    void LevelUp()
    {
        currentLevel++;
        currentGachaCount = 0;

        Debug.Log($"[GachaManager] ========== 레벨업! ==========");
        Debug.Log($"[GachaManager] 새 레벨: {currentLevel}");

        UpdateGachaPool();
        ValidateGachaPool();

        // 티어 표시 갱신 (GachaEquipmentTierManager가 Update에서 감지하므로 자동 반영)
        string tierMsg = GetTierDisplayName();
        UIManager.Instance?.ShowMessage($"가챠 레벨 UP! Lv.{currentLevel}\n{tierMsg}", Color.yellow);

        Debug.Log($"[GachaManager] ===================================");
    }

    private string GetTierDisplayName()
    {
        if (GachaEquipmentTierManager.Instance != null)
            return GachaEquipmentTierManager.Instance.GetTierName(
                       GachaEquipmentTierManager.Instance.CurrentTier);

        // 티어매니저 없을 때 기본 표시
        if (currentLevel >= 20) return "🧬 DNA 장비 뽑기";
        if (currentLevel >= 10) return "🔬 분자 장비 뽑기";
        return "⚛️ 원자 장비 뽑기";
    }

    // ════════════════════════════════════════════════════════
    //  헬퍼 메서드
    // ════════════════════════════════════════════════════════

    private bool CheckGachaPool()
    {
        if (currentGachaPool == null || currentGachaPool.Count == 0 ||
            !currentGachaPool.Exists(g => g != null && g.equipment != null))
        {
            UIManager.Instance?.ShowMessage("가챠 데이터가 없습니다!", Color.red);
            return false;
        }
        return true;
    }

    private bool SpendTickets(int amount)
    {
        if (ResourceBarManager.Instance == null)
        {
            Debug.LogError("[GachaManager] ResourceBarManager가 없습니다!");
            return false;
        }

        if (!ResourceBarManager.Instance.SpendEquipmentTickets(amount))
        {
            int held = ResourceBarManager.Instance.GetEquipmentTickets();
            UIManager.Instance?.ShowConfirmDialog(
                $"장비티켓이부족합니다.\n필요:{amount}개\n보유:{held}개",
                onConfirm: null);
            return false;
        }
        return true;
    }

    private void RefreshGachaUI()
    {
        if (GachaUI.Instance != null)
        {
            GachaUI.Instance.UpdateTicketDisplay();
            GachaUI.Instance.UpdateLevelDisplay();
        }
    }

    void ShowGachaResults(List<EquipmentData> results)
    {
        // ★ 연출(Video/Timeline) 가능하면 연출 시스템으로 분기
        if (CanUseAnimation(results.Count))
        {
            PlayGachaAnimation(results);
            return;
        }

        // 연출 미사용 시 기존 GachaResultUI 사용
        ShowGachaResultsFallback(results);
    }

    /// <summary>기존 GachaResultUI를 통한 결과 표시 (다연차 또는 Timeline 미설정 시)</summary>
    private void ShowGachaResultsFallback(List<EquipmentData> results)
    {
        // ★ Instance가 null이면 비활성 GO에서 찾아서 활성화
        if (GachaResultUI.Instance == null)
        {
            var found = FindObjectOfType<GachaResultUI>(true); // true = 비활성 포함
            if (found != null)
            {
                found.gameObject.SetActive(true); // Awake() → Instance 등록
                Debug.Log("[GachaManager] GachaResultUI GO가 비활성 → 강제 활성화");
            }
        }

        if (GachaResultUI.Instance != null)
            GachaResultUI.Instance.ShowResults(results);
        else
        {
            Debug.LogWarning("[GachaManager] GachaResultUI를 찾을 수 없음!");
            foreach (EquipmentData equipment in results)
                Debug.Log($"- {equipment.itemName} ({equipment.rarity})");
        }
    }

    // ════════════════════════════════════════════════════════
    //  공개 체크 메서드
    // ════════════════════════════════════════════════════════

    public bool CanPerformSingleGacha()
        => ResourceBarManager.Instance != null &&
           ResourceBarManager.Instance.HasEquipmentTickets(ticketCostPerGacha);

    public bool CanPerformTenGacha()
        => ResourceBarManager.Instance != null &&
           ResourceBarManager.Instance.HasEquipmentTickets(ticketCostPerGacha * multiGachaCount);

    public bool CanPerformHundredGacha()
        => ResourceBarManager.Instance != null &&
           ResourceBarManager.Instance.HasEquipmentTickets(ticketCostPerGacha * bulkGachaCount);

    public float GetLevelProgress()
        => (float)currentGachaCount / gachaCountForLevelUp * 100f;

    public int GetRemainingGachaForLevelUp()
        => gachaCountForLevelUp - currentGachaCount;

    // ★ 레벨업 비용 미리보기 (UI 표시용)
    public string GetLevelUpCostText()
    {
        int costIdx = currentLevel - 1;

        // 배열 범위 밖 → 비용 없음
        if (gachaLevelUpCosts == null || costIdx < 0 || costIdx >= gachaLevelUpCosts.Length)
            return $"자동 레벨업 (뽑기 {gachaCountForLevelUp}회 달성 시)";

        var cost = gachaLevelUpCosts[costIdx];
        if (cost.goldCost <= 0 && cost.cropPointCost <= 0)
            return $"자동 레벨업 (뽑기 {gachaCountForLevelUp}회 달성 시)";

        return $"Lv.{currentLevel}→{currentLevel + 1}: {cost.goldCost:N0}G + {cost.cropPointCost}CP\n" +
               $"(뽑기 {gachaCountForLevelUp}회 달성 시 자동)";
    }

    // ════════════════════════════════════════════════════════
    //  ★ Timeline 가챠 연출 시스템
    // ════════════════════════════════════════════════════════

    private void SetupAnimationSystem()
    {
        // 결과 확인 버튼 연결
        if (resultConfirmButton != null)
            resultConfirmButton.onClick.AddListener(OnResultConfirmClicked);

        // 초기 패널 비활성화
        if (gachaAnimationPanel != null)
            gachaAnimationPanel.SetActive(false);
        if (gachaResultPanel != null)
            gachaResultPanel.SetActive(false);

        // PlayableDirector 종료 이벤트 연결
        if (gachaDirector != null)
            gachaDirector.stopped += OnTimelineStopped;

        // ★ VideoPlayer 설정
        if (gachaVideoPlayer != null)
        {
            gachaVideoPlayer.playOnAwake = false;
            gachaVideoPlayer.isLooping = false;
            gachaVideoPlayer.loopPointReached += OnVideoFinished;
        }

        // 스킵 버튼 연결
        if (videoSkipButton != null)
            videoSkipButton.onClick.AddListener(OnVideoSkipClicked);

        Debug.Log("[GachaManager] 연출 시스템 초기화 완료 " +
                  $"(Video:{gachaVideoPlayer != null}, Timeline:{gachaDirector != null})");
    }

    /// <summary>
    /// ★ 가챠 연출 시작 (Video 또는 Timeline)
    /// 뽑기 버튼 클릭 → 연출 패널 활성화 → 영상/Timeline 재생 → 종료 시 결과 표시
    /// </summary>
    public void PlayGachaAnimation(List<EquipmentData> results)
    {
        if (results == null || results.Count == 0) return;

        pendingResults = results;
        isPlayingAnimation = true;

        // 연출 패널 활성화
        if (gachaAnimationPanel != null)
            gachaAnimationPanel.SetActive(true);

        // 결과 패널은 아직 숨김
        if (gachaResultPanel != null)
            gachaResultPanel.SetActive(false);

        // 스킵 버튼 표시
        if (videoSkipButton != null)
            videoSkipButton.gameObject.SetActive(true);

        // ★ Video 우선 모드: VideoPlayer가 있고 클립이 있으면 영상 재생
        if (preferVideoOverTimeline && CanUseVideo(results))
        {
            PlayGachaVideo(results);
            return;
        }

        // ★ Timeline 모드
        if (gachaDirector != null)
        {
            gachaDirector.time = 0;
            gachaDirector.Play();
            Debug.Log("[GachaManager] ★ Timeline 가챠 연출 시작");
            return;
        }

        // ★ Video 폴백 (preferVideoOverTimeline == false이지만 Timeline 없을 때)
        if (CanUseVideo(results))
        {
            PlayGachaVideo(results);
            return;
        }

        // 둘 다 없으면 바로 결과 표시
        Debug.LogWarning("[GachaManager] 연출 수단 없음 → 즉시 결과 표시");
        ShowResult();
    }

    /// <summary>
    /// ★ PlayableDirector.stopped 이벤트 콜백
    /// Timeline 재생이 끝나면 자동 호출
    /// </summary>
    private void OnTimelineStopped(PlayableDirector director)
    {
        if (director == gachaDirector)
        {
            Debug.Log("[GachaManager] Timeline 재생 완료 → ShowResult 호출");
            ShowResult();
        }
    }

    /// <summary>
    /// ★ Signal Receiver 연동용 — Timeline Signal에서 호출 가능
    /// 결과창(GachaResultPanel)에 뽑힌 장비 정보를 표시
    /// </summary>
    public void ShowResult()
    {
        if (pendingResults == null || pendingResults.Count == 0)
        {
            Debug.LogWarning("[GachaManager] ShowResult: pendingResults가 비어있습니다.");
            return;
        }

        isPlayingAnimation = false;

        // VideoPlayer 정지 (재생 중이면)
        if (gachaVideoPlayer != null && gachaVideoPlayer.isPlaying)
            gachaVideoPlayer.Stop();

        // 스킵 버튼 숨김
        if (videoSkipButton != null)
            videoSkipButton.gameObject.SetActive(false);

        // 연출 패널 숨김
        if (gachaAnimationPanel != null)
            gachaAnimationPanel.SetActive(false);

        // 1회 뽑기의 경우 단일 결과 패널 표시
        if (pendingResults.Count == 1 && gachaResultPanel != null)
        {
            EquipmentData result = pendingResults[0];

            if (gachaResultPanel != null)
                gachaResultPanel.SetActive(true);

            // 아이콘
            if (resultItemIcon != null)
            {
                resultItemIcon.sprite = result.itemIcon;
                resultItemIcon.color = Color.white;
            }

            // 이름
            if (resultItemNameText != null)
                resultItemNameText.text = result.itemName;

            // 등급
            if (resultRarityText != null)
            {
                resultRarityText.text = GetRarityDisplayText(result.rarity);
                resultRarityText.color = GetRarityColor(result.rarity);
            }

            // 효과음
            SoundManager.Instance?.PlaySFX(
                result.rarity >= ItemRarity.Epic ? "GachaRare" : "GachaReveal");

            Debug.Log($"[GachaManager] ★ 결과 표시: {result.itemName} ({result.rarity})");
        }
        else
        {
            // 다연차 결과는 기존 GachaResultUI로 전달
            ShowGachaResultsFallback(pendingResults);
        }
    }

    /// <summary>
    /// ★ 결과 확인 버튼 클릭 → 패널 닫기 + 저장
    /// </summary>
    private void OnResultConfirmClicked()
    {
        SoundManager.Instance?.PlayButtonClick();

        // 모든 연출 정지
        if (gachaVideoPlayer != null && gachaVideoPlayer.isPlaying)
            gachaVideoPlayer.Stop();
        if (gachaDirector != null && gachaDirector.state == PlayState.Playing)
            gachaDirector.Stop();

        if (gachaResultPanel != null)
            gachaResultPanel.SetActive(false);
        if (gachaAnimationPanel != null)
            gachaAnimationPanel.SetActive(false);
        if (videoSkipButton != null)
            videoSkipButton.gameObject.SetActive(false);

        isPlayingAnimation = false;
        pendingResults = null;

        // 게임 저장
        SaveLoadManager.Instance?.SaveGame();
        Debug.Log("[GachaManager] ★ 결과 확인 → 패널 닫기 + SaveGame 완료");
    }

    /// <summary>연출(Video 또는 Timeline)을 사용할 수 있는지 확인</summary>
    private bool CanUseAnimation(int resultCount)
    {
        // 연출 수단이 하나도 없으면 불가
        bool hasVideo = gachaVideoPlayer != null &&
                        (defaultGachaClip != null || (rarityGachaClips != null && rarityGachaClips.Length > 0));
        bool hasTimeline = gachaDirector != null;
        bool hasPanel = gachaAnimationPanel != null;

        if (!hasPanel && !hasTimeline && !hasVideo)
            return false;

        // animationOnlyForSingle이 true면 1회 뽑기에서만 사용
        if (animationOnlyForSingle && resultCount > 1)
            return false;

        return true;
    }

    /// <summary>VideoPlayer로 영상 재생이 가능한지 확인</summary>
    private bool CanUseVideo(List<EquipmentData> results)
    {
        if (gachaVideoPlayer == null) return false;

        // 등급별 클립 또는 기본 클립이 있는지 확인
        VideoClip clip = GetVideoClipForResult(results);
        return clip != null;
    }

    /// <summary>결과 등급에 맞는 VideoClip 선택</summary>
    private VideoClip GetVideoClipForResult(List<EquipmentData> results)
    {
        if (results == null || results.Count == 0)
            return defaultGachaClip;

        // 결과 중 가장 높은 등급 기준으로 클립 선택
        ItemRarity highestRarity = ItemRarity.Common;
        foreach (var r in results)
        {
            if (r != null && r.rarity > highestRarity)
                highestRarity = r.rarity;
        }

        // 등급별 클립 배열에서 선택
        if (rarityGachaClips != null)
        {
            int idx = (int)highestRarity;
            if (idx >= 0 && idx < rarityGachaClips.Length && rarityGachaClips[idx] != null)
                return rarityGachaClips[idx];
        }

        // 폴백: 기본 클립
        return defaultGachaClip;
    }

    // ════════════════════════════════════════════════════════
    //  ★ MP4 동영상 재생
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// ★ VideoPlayer로 MP4 가챠 연출 재생
    /// loopPointReached 이벤트로 영상 종료 시점 감지 → ShowResult() 호출
    /// </summary>
    private void PlayGachaVideo(List<EquipmentData> results)
    {
        VideoClip clip = GetVideoClipForResult(results);
        if (clip == null)
        {
            Debug.LogWarning("[GachaManager] 재생할 VideoClip이 없습니다 → 즉시 결과 표시");
            ShowResult();
            return;
        }

        gachaVideoPlayer.clip = clip;
        gachaVideoPlayer.time = 0;
        gachaVideoPlayer.Play();

        Debug.Log($"[GachaManager] ★ MP4 가챠 연출 시작: {clip.name} " +
                  $"({clip.length:F1}초)");
    }

    /// <summary>
    /// ★ VideoPlayer.loopPointReached 콜백
    /// 영상이 끝까지 재생되면 자동 호출 → ShowResult()
    /// </summary>
    private void OnVideoFinished(VideoPlayer vp)
    {
        if (vp != gachaVideoPlayer) return;
        if (!isPlayingAnimation) return;

        Debug.Log("[GachaManager] ★ MP4 영상 재생 완료 → ShowResult 호출");
        isPlayingAnimation = false;
        ShowResult();
    }

    /// <summary>
    /// ★ 영상 스킵 버튼 클릭 → 즉시 영상 중단 + 결과 표시
    /// </summary>
    private void OnVideoSkipClicked()
    {
        if (!isPlayingAnimation) return;

        Debug.Log("[GachaManager] ★ 영상 스킵!");

        // VideoPlayer 정지
        if (gachaVideoPlayer != null && gachaVideoPlayer.isPlaying)
            gachaVideoPlayer.Stop();

        // Timeline 정지
        if (gachaDirector != null && gachaDirector.state == PlayState.Playing)
            gachaDirector.Stop();

        isPlayingAnimation = false;
        ShowResult();
    }

    // ── 등급 표시 헬퍼 ──────────────────────────────────────

    private string GetRarityDisplayText(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common:    return "★ 일반";
            case ItemRarity.Uncommon:  return "★★ 고급";
            case ItemRarity.Rare:      return "★★★ 희귀";
            case ItemRarity.Epic:      return "★★★★ 영웅";
            case ItemRarity.Legendary: return "★★★★★ 전설";
            default: return rarity.ToString();
        }
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        int idx = (int)rarity;
        if (rarityColors != null && idx >= 0 && idx < rarityColors.Length)
            return rarityColors[idx];

        // 기본 색상
        switch (rarity)
        {
            case ItemRarity.Legendary: return Color.yellow;
            case ItemRarity.Epic:      return new Color(0.7f, 0.3f, 0.9f);
            case ItemRarity.Rare:      return new Color(0.3f, 0.5f, 1f);
            default: return Color.white;
        }
    }

    void OnDestroy()
    {
        if (gachaDirector != null)
            gachaDirector.stopped -= OnTimelineStopped;
        if (gachaVideoPlayer != null)
            gachaVideoPlayer.loopPointReached -= OnVideoFinished;
    }
}