using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ══════════════════════════════════════════════════════════════════
/// RankingManager — 랭킹 패널 UI (레퍼런스 디자인 전면 개편)
/// ══════════════════════════════════════════════════════════════════
///
/// ★ 레이아웃 구조:
///   1. 상단 타이틀 + 시즌 타이머 배너
///   2. TOP 3 포디움 (2위-1위-3위 배치, 1위 가장 큼)
///   3. 4위~ 스크롤 리스트 (홀짝 행 색상, 카드 스타일)
///   4. 하단 내 순위 고정바 (분홍/빨강 강조, 권외 처리)
///
/// ★ Inspector 연결 가이드:
///   - podiumSlots[0]=2위(왼), [1]=1위(가운데), [2]=3위(오른)
///   - rankEntryPrefab에 RankEntryItem 컴포넌트 부착
///   - seasonEndTime에 시즌 종료 시각 설정 (UTC)
///
/// ★ 프로필 이미지 시스템 기존 유지
/// ══════════════════════════════════════════════════════════════════
/// </summary>
public class RankingManager : MonoBehaviour
{
    public static RankingManager Instance;

    // ═══ 중첩 타입 ════════════════════════════════════════════════

    public enum RankType { CombatPower, Level, Farm }

    public class RankEntry
    {
        public string playerName;
        public int score;
        public int combatPower; // 전투력 (K단위 표시용, 모든 탭에서 사용)
        public int classIndex;  // 0=전사, 1=원거리, 2=마법사
        public bool isMe;
    }

    /// <summary>내 직업 인덱스 (캐릭터 선택 시 외부에서 설정)</summary>
    public static int MyClassIndex = 0;

    // ═══════════════════════════════════════════════════════════════
    //  Inspector 필드
    // ═══════════════════════════════════════════════════════════════

    [Header("===== 패널 루트 =====")]
    public GameObject rankingPanel;

    [Header("===== 상단 타이틀 =====")]
    [Tooltip("패널 제목 (예: '탐험 순위')")]
    public TextMeshProUGUI titleText;
    [Tooltip("부제 설명 텍스트")]
    public TextMeshProUGUI subtitleText;

    [Header("===== 시즌 타이머 배너 =====")]
    [Tooltip("'남은 시간 : X시간 XX분' 텍스트")]
    public TextMeshProUGUI seasonTimerText;
    [Tooltip("'다음 순위 : 시즌 순위' 텍스트")]
    public TextMeshProUGUI nextSeasonText;
    [Tooltip("시즌 종료 시각 (비어있으면 24시간 기본값)")]
    public string seasonEndTime = "";
    [Tooltip("시즌 카운트다운 기본 시간(초). seasonEndTime 미설정 시 사용")]
    [SerializeField] private float defaultSeasonSeconds = 86400f; // 24시간

    [Header("===== 탭 버튼 =====")]
    public Button tabCombatBtn;
    public Button tabLevelBtn;
    public Button tabFarmBtn;
    public Color tabActiveColor = new Color(1f, 0.85f, 0.3f);
    public Color tabInactiveColor = new Color(0.5f, 0.5f, 0.5f);

    [Header("===== 탭 버튼 이미지 (텍스트 대신 이미지 사용 시) =====")]
    [Tooltip("전투력 탭 — 활성/비활성 이미지")]
    [SerializeField] private Sprite tabCombatActiveSprite;
    [SerializeField] private Sprite tabCombatInactiveSprite;
    [Tooltip("탐험 탭 — 활성/비활성 이미지")]
    [SerializeField] private Sprite tabLevelActiveSprite;
    [SerializeField] private Sprite tabLevelInactiveSprite;
    [Tooltip("농장 탭 — 활성/비활성 이미지")]
    [SerializeField] private Sprite tabFarmActiveSprite;
    [SerializeField] private Sprite tabFarmInactiveSprite;

    [Header("===== 타이틀 배너 이미지 (텍스트 대신 이미지 사용 시) =====")]
    [Tooltip("타이틀 Image 컴포넌트 (있으면 텍스트 대신 이미지 사용)")]
    [SerializeField] private Image titleBannerImage;
    [Tooltip("전투력 순위 배너")]
    [SerializeField] private Sprite bannerCombatPower;
    [Tooltip("탐험 순위 배너")]
    [SerializeField] private Sprite bannerLevel;
    [Tooltip("농장 순위 배너")]
    [SerializeField] private Sprite bannerFarm;

    [Header("===== TOP 3 포디움 (0=2위왼쪽, 1=1위가운데, 2=3위오른쪽) =====")]
    [Tooltip("RankPodiumSlot 컴포넌트가 붙은 3개 슬롯")]
    public RankPodiumSlot[] podiumSlots = new RankPodiumSlot[3];

    [Header("===== 4위~ 리스트 =====")]
    [Tooltip("RankListSlot 프리팹 (4위 이하 행)")]
    public GameObject rankListSlotPrefab;
    [Tooltip("4위~ 슬롯 부모 (ScrollView Content)")]
    public RectTransform rankListContainer;
    [Tooltip("최대 표시 순위 수")]
    public int maxDisplayCount = 50;

    [Header("===== 하단 내 순위 고정바 =====")]
    [Tooltip("내 순위 바 루트 (배경 Image)")]
    public Image myRankBarBg;
    public TextMeshProUGUI myRankText;
    public TextMeshProUGUI myNameText;
    public Image myIconImage;
    public TextMeshProUGUI myPowerText;
    public TextMeshProUGUI myScoreText;
    [Tooltip("'랭킹 조건 : St.100' 등 안내 텍스트")]
    public TextMeshProUGUI myRankConditionText;

    [Header("===== 내 순위 바 색상 =====")]
    [SerializeField] private Color myBarInRankColor = new Color(0.95f, 0.3f, 0.4f, 0.85f);
    [SerializeField] private Color myBarOutOfRankColor = new Color(0.85f, 0.25f, 0.35f, 0.9f);
    [Tooltip("권외 시 랭킹 진입 최소 점수 (표시용)")]
    [SerializeField] private int rankEntryThreshold = 100;

    [Header("===== 직업 아이콘 (0=전사 / 1=원거리 / 2=마법사) =====")]
    public Sprite[] classIcons;

    [Header("===== 닫기 =====")]
    public Button closeButton;

    [Header("===== 프로필 이미지 (기능 1) =====")]
    public Image myPortraitImage;

    [Header("===== 프로필 이미지 선택 팝업 (기능 2) =====")]
    public Sprite[] portraitSprites;
    public GameObject portraitPopupPanel;
    public Transform portraitGridParent;
    public GameObject portraitSlotPrefab;
    public Button portraitCloseBtn;
    public Color selectedSlotColor = new Color(0.2f, 0.8f, 1f, 1f);
    public Color normalSlotColor = new Color(1f, 1f, 1f, 0.6f);

    // ═══ 내부 상태 ════════════════════════════════════════════════

    private RankType currentType = RankType.CombatPower;
    private int currentPortraitIndex = 0;
    private List<Image> portraitSlotBorders = new List<Image>();
    private bool isLoadingServerRank;
    private bool isInitialized; // Start 완료 여부 (OnEnable 가드용)

    // 시즌 타이머
    private float seasonRemainingSeconds;
    private bool seasonTimerActive;

    // ═══════════════════════════════════════════════════════════════
    //  Unity 생명주기
    // ═══════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        SetupButtons();
        SetupPortraitSystem();
        InitSeasonTimer();
        if (rankingPanel != null) rankingPanel.SetActive(false);
        isInitialized = true;
    }

    void Update()
    {
        UpdateSeasonTimer();
    }

    /// <summary>
    /// OnEnable 가드: Start 완료 전에는 RefreshRanking을 호출하지 않음.
    /// 패널이 닫혀있을 때도 스킵 — OpenPanel()에서 직접 Refresh 호출.
    /// </summary>
    void OnEnable()
    {
        // Start 전이면 스킵 (첫 프레임 NaN 방지)
        if (!isInitialized) return;
        // 패널이 닫혀있으면 스킵 (비활성 컨테이너에 Instantiate 방지)
        if (rankingPanel != null && !rankingPanel.activeSelf) return;
        RefreshRanking();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  버튼 초기화
    // ═══════════════════════════════════════════════════════════════

    private void SetupButtons()
    {
        tabCombatBtn?.onClick.AddListener(() => OnTabClick(RankType.CombatPower));
        tabLevelBtn?.onClick.AddListener(() => OnTabClick(RankType.Level));
        tabFarmBtn?.onClick.AddListener(() => OnTabClick(RankType.Farm));
        closeButton?.onClick.AddListener(ClosePanel);
        UpdateTabColors();
    }

    private void OnTabClick(RankType type)
    {
        SoundManager.Instance?.PlayButtonClick();
        currentType = type;
        UpdateTabColors();
        RefreshRanking();
    }

    private void UpdateTabColors()
    {
        SetTabVisual(tabCombatBtn, currentType == RankType.CombatPower,
                     tabCombatActiveSprite, tabCombatInactiveSprite);
        SetTabVisual(tabLevelBtn, currentType == RankType.Level,
                     tabLevelActiveSprite, tabLevelInactiveSprite);
        SetTabVisual(tabFarmBtn, currentType == RankType.Farm,
                     tabFarmActiveSprite, tabFarmInactiveSprite);

        // ★ 타이틀 배너 이미지 갱신
        UpdateTitleBanner();
    }

    private void SetTabVisual(Button btn, bool active, Sprite activeSprite, Sprite inactiveSprite)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            // ★ 이미지 모드: Sprite가 설정되어 있으면 이미지 교체
            if (activeSprite != null && inactiveSprite != null)
            {
                img.sprite = active ? activeSprite : inactiveSprite;
                img.color = Color.white; // 이미지 모드에서는 색상 건드리지 않음
            }
            else
            {
                // 폴백: 기존 색상 방식
                img.color = active ? tabActiveColor : tabInactiveColor;
            }
        }

        var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
        {
            // 이미지 모드에서는 텍스트 숨김
            if (activeSprite != null && inactiveSprite != null)
                txt.gameObject.SetActive(false);
            else
                txt.color = active ? Color.black : Color.white;
        }
    }

    /// <summary>★ 타이틀을 이미지 배너로 갱신 (Sprite 미설정 시 텍스트 폴백)</summary>
    private void UpdateTitleBanner()
    {
        Sprite banner = currentType switch
        {
            RankType.CombatPower => bannerCombatPower,
            RankType.Level       => bannerLevel,
            RankType.Farm        => bannerFarm,
            _                    => null
        };

        if (titleBannerImage != null && banner != null)
        {
            // 이미지 모드
            titleBannerImage.sprite = banner;
            titleBannerImage.color = Color.white;
            titleBannerImage.gameObject.SetActive(true);
            if (titleText != null) titleText.gameObject.SetActive(false);
        }
        else
        {
            // 텍스트 폴백
            if (titleBannerImage != null) titleBannerImage.gameObject.SetActive(false);
            if (titleText != null)
            {
                titleText.gameObject.SetActive(true);
                titleText.text = GetTitleForType(currentType);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  시즌 타이머
    // ═══════════════════════════════════════════════════════════════

    private void InitSeasonTimer()
    {
        if (!string.IsNullOrEmpty(seasonEndTime)
            && DateTime.TryParse(seasonEndTime, out DateTime endDt))
        {
            seasonRemainingSeconds = (float)(endDt - DateTime.UtcNow).TotalSeconds;
        }
        else
        {
            seasonRemainingSeconds = defaultSeasonSeconds;
        }

        seasonTimerActive = true;
    }

    private void UpdateSeasonTimer()
    {
        if (!seasonTimerActive || seasonTimerText == null) return;

        // 패널이 닫혀있어도 타이머는 감소
        seasonRemainingSeconds -= Time.deltaTime;
        if (seasonRemainingSeconds < 0f) seasonRemainingSeconds = 0f;

        // 패널이 열려있을 때만 UI 갱신
        if (rankingPanel != null && rankingPanel.activeSelf)
            seasonTimerText.text = $"남은 시간 : {RankingFormatUtil.FormatTimeRemaining(seasonRemainingSeconds)}";
    }

    // ═══════════════════════════════════════════════════════════════
    //  패널 열기 / 닫기
    // ═══════════════════════════════════════════════════════════════

    public void OpenPanel()
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.ShouldBlockNonFocusButtons) return;
        if (rankingPanel == null)
        {
            Debug.LogError("[RankingManager] rankingPanel이 null! Inspector에서 연결 확인 필요");
            return;
        }

        rankingPanel.SetActive(true);

        // 프로필 이미지 복원
        LoadPortraitIndex();
        ApplyPortrait();

        // 타이틀 갱신
        UpdateTitleBanner();
        if (nextSeasonText != null) nextSeasonText.text = "다음 순위 : 시즌 순위";

        // 패널 열 때 서버 점수 즉시 갱신
        if (BackendRankingManager.Instance != null)
            BackendRankingManager.Instance.UpdateAllScores();

        RefreshRanking();
    }

    public void ClosePanel()
    {
        ClosePortraitPopup();
        if (rankingPanel != null) rankingPanel.SetActive(false);
        TopMenuManager.Instance?.ClearBanner();
    }

    private string GetTitleForType(RankType type) => type switch
    {
        RankType.CombatPower => "전투력 순위",
        RankType.Level       => "탐험 순위",
        RankType.Farm        => "농장 순위",
        _                    => "랭킹"
    };

    // ═══════════════════════════════════════════════════════════════
    //  랭킹 데이터 갱신
    // ═══════════════════════════════════════════════════════════════

    public void RefreshRanking()
    {
        UpdateTitleBanner();

        if (BackendRankingManager.Instance != null
            && BackendManager.Instance != null
            && BackendManager.Instance.IsLoggedIn
            && !isLoadingServerRank)
        {
            StartCoroutine(RefreshFromServer());
        }
        else
        {
            RefreshLocal();
        }
    }

    private IEnumerator RefreshFromServer()
    {
        isLoadingServerRank = true;

        bool done = false;
        List<RankEntry> serverEntries = null;
        bool success = false;

        BackendRankingManager.Instance.GetRankList(currentType, (entries, ok) =>
        {
            serverEntries = entries;
            success = ok;
            done = true;
        });

        float timeout = 5f;
        while (!done && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        isLoadingServerRank = false;

        if (success && serverEntries != null && serverEntries.Count > 0)
        {
            // 서버 데이터에 NPC를 합쳐서 포디움이 항상 채워지도록 함
            var npcEntries = GenerateNpcEntries();
            serverEntries.AddRange(npcEntries);
            SortEntries(serverEntries);
            DisplayEntries(serverEntries);
        }
        else
        {
            RefreshLocal();
        }
    }

    private void RefreshLocal()
    {
        var entries = BuildEntries();
        SortEntries(entries);
        DisplayEntries(entries);
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI 표시 — TOP3 포디움 + 4위~ 리스트 + 내 순위 바
    // ═══════════════════════════════════════════════════════════════

    private void DisplayEntries(List<RankEntry> entries)
    {
        // 패널이 비활성이면 UI 조작하지 않음 (NaN 방지)
        if (rankingPanel != null && !rankingPanel.activeSelf) return;

        // Bug6: 내 점수를 현재 로컬 값으로 오버라이드
        OverrideMyScore(entries);

        // ── TOP 3 포디움 ──────────────────────────────────────
        DisplayPodium(entries);

        // ── 4위~ 리스트 ───────────────────────────────────────
        ClearList();
        int myRank = -1;
        int rowIndex = 0;

        for (int i = 0; i < entries.Count && i < maxDisplayCount; i++)
        {
            if (entries[i].isMe) myRank = i + 1;

            // TOP 3은 포디움에 표시했으므로 리스트에서 제외
            if (i < 3) continue;

            SpawnEntry(i + 1, entries[i], rowIndex);
            rowIndex++;
        }

        // 슬롯 생성 후 Layout 강제 갱신 (NaN 방지)
        ForceRebuildLayout();

        // myRank가 top3 내에 있을 수 있으므로 별도 체크
        if (myRank <= 0)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].isMe) { myRank = i + 1; break; }
            }
        }

        // ── 하단 내 순위 바 ──────────────────────────────────
        UpdateMyRankBar(myRank, entries);
    }

    // ── TOP 3 포디움 표시 ─────────────────────────────────────
    private void DisplayPodium(List<RankEntry> entries)
    {
        if (podiumSlots == null || podiumSlots.Length < 3) return;

        // podiumSlots[0]=2위(왼), [1]=1위(가운데), [2]=3위(오른)
        int[] rankMap = { 2, 1, 3 }; // podiumSlots 인덱스 → 실제 순위

        for (int slot = 0; slot < 3; slot++)
        {
            if (podiumSlots[slot] == null) continue;

            int rank = rankMap[slot];
            int entryIdx = rank - 1;

            if (entryIdx < entries.Count)
            {
                var entry = entries[entryIdx];
                Sprite icon = GetClassIcon(entry.classIndex);
                podiumSlots[slot].Setup(rank, entry, icon, currentType, entry.combatPower);
            }
            else
            {
                podiumSlots[slot].Setup(rank, null, null, currentType, 0);
            }
        }
    }

    // ── 4위~ 항목 생성 ────────────────────────────────────────
    private void SpawnEntry(int rank, RankEntry entry, int rowIndex)
    {
        if (rankListSlotPrefab == null || rankListContainer == null) return;

        // 컨테이너가 비활성이면 Instantiate 하지 않음 (NaN 방지)
        if (!rankListContainer.gameObject.activeInHierarchy) return;

        GameObject go = Instantiate(rankListSlotPrefab, rankListContainer);

        // RectTransform 크기 보정: Width/Height가 0이면 기본값 설정
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector2 size = rt.sizeDelta;
            if (size.x == 0f && rt.anchorMin.x == rt.anchorMax.x) size.x = 700f;
            if (size.y == 0f && rt.anchorMin.y == rt.anchorMax.y) size.y = 80f;
            rt.sizeDelta = size;
        }

        var slot = go.GetComponent<RankListSlot>();
        if (slot == null) slot = go.AddComponent<RankListSlot>();

        Sprite icon = GetClassIcon(entry.classIndex);
        slot.Setup(rank, entry, icon, currentType, rowIndex, entry.combatPower);
    }

    private void ClearList()
    {
        if (rankListContainer == null) return;
        foreach (Transform c in rankListContainer) Destroy(c.gameObject);
    }

    /// <summary>
    /// Layout 강제 재빌드 — Instantiate 후 ContentSizeFitter/LayoutGroup이
    /// 다음 프레임까지 기다리면 NaN 발생 가능하므로 즉시 갱신
    /// </summary>
    private void ForceRebuildLayout()
    {
        if (rankListContainer != null && rankListContainer.gameObject.activeInHierarchy)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rankListContainer);
        }
    }

    // ── 하단 내 순위 고정바 ──────────────────────────────────
    private void UpdateMyRankBar(int myRank, List<RankEntry> sorted)
    {
        var me = sorted.Find(e => e.isMe);
        if (me == null) return;

        bool isOutOfRank = (myRank <= 0 || myRank > maxDisplayCount);

        // 배경 색상
        if (myRankBarBg != null)
            myRankBarBg.color = isOutOfRank ? myBarOutOfRankColor : myBarInRankColor;

        // 순위
        if (myRankText != null)
            myRankText.text = isOutOfRank ? "권외" : $"{myRank}위";

        // 닉네임
        if (myNameText != null)
            myNameText.text = me.playerName;

        // 아이콘
        if (myIconImage != null)
        {
            myIconImage.sprite = GetClassIcon(MyClassIndex);
            myIconImage.enabled = myIconImage.sprite != null;
        }

        // 전투력
        if (myPowerText != null)
            myPowerText.text = RankingFormatUtil.FormatPowerShort(me.combatPower);

        // 점수
        if (myScoreText != null)
            myScoreText.text = RankingFormatUtil.FormatScoreLabel(me.score, currentType);

        // 랭킹 조건 안내
        if (myRankConditionText != null)
        {
            if (isOutOfRank)
                myRankConditionText.text = $"랭킹 조건 : St.{rankEntryThreshold}";
            else
                myRankConditionText.text = "";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  데이터 구성
    // ═══════════════════════════════════════════════════════════════

    private void OverrideMyScore(List<RankEntry> entries)
    {
        int currentCp = CombatPowerManager.Instance?.TotalCombatPower ?? 0;
        int currentScore = GetScore(
            currentCp,
            GameManager.Instance?.PlayerLevel ?? 1,
            FarmManager.Instance?.GetCropPoints() ?? 0);

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].isMe)
            {
                entries[i].score = currentScore;
                entries[i].combatPower = currentCp;
                break;
            }
        }

        SortEntries(entries);
    }

    private List<RankEntry> BuildEntries()
    {
        int myCp = CombatPowerManager.Instance?.TotalCombatPower ?? 0;

        var list = new List<RankEntry>();
        list.Add(new RankEntry
        {
            playerName  = GetMyName(),
            score       = GetScore(myCp,
                                   GameManager.Instance?.PlayerLevel ?? 1,
                                   FarmManager.Instance?.GetCropPoints() ?? 0),
            combatPower = myCp,
            classIndex  = MyClassIndex,
            isMe        = true
        });

        list.AddRange(GenerateNpcEntries());
        return list;
    }

    private int GetScore(int cp, int lv, long farm) => currentType switch
    {
        RankType.CombatPower => cp,
        RankType.Level       => lv,
        RankType.Farm        => (int)farm,
        _                    => cp
    };

    // ── NPC 더미 ──────────────────────────────────────────────
    private List<RankEntry> GenerateNpcEntries()
    {
        string[] names =
        {
            "용사123", "흑룡기사", "빛의마법사", "폭풍궁수", "어둠사냥꾼",
            "천상전사", "별빛도적", "철벽방패", "대지술사", "신속암살자",
            "화염기사", "냉기마법사", "독의사냥꾼", "성검전사", "바람소환사",
            "철권권사", "염동력사", "시간술사", "생명의드루이드", "죽음기사",
            "번개무도가", "빙결마법사", "용의후예", "고대의현자", "심연의기사",
            "광기의술사", "해적왕", "대마법사", "성기사", "무한검사",
            "파괴자", "수호천사", "혼돈의마법사", "철의의지", "빛의창기사",
            "야수조련사", "영혼수집가", "연금술사", "검은번개", "하늘의기사",
            "대지의목동", "화산술사", "불꽃검사", "빙하의사냥꾼", "폭풍의마법사",
            "달빛암살자", "별의성기사", "구름의무도가", "태양의전사", "바다의드루이드"
        };

        int iconCount = classIcons != null ? classIcons.Length : 1;
        var list = new List<RankEntry>();

        for (int i = 0; i < names.Length; i++)
        {
            int tier = i + 1;
            int nameHash = Mathf.Abs(names[i].GetHashCode());
            float variation = 0.85f + (nameHash % 30) / 100f;
            float curve = Mathf.Pow(1.12f, tier);

            int cp   = Mathf.RoundToInt(800 * curve * variation);
            int lv   = Mathf.Clamp(Mathf.RoundToInt(tier * 1.8f + (nameHash % 5)), 1, 999);
            int farm = Mathf.RoundToInt(80 * curve * variation);

            list.Add(new RankEntry
            {
                playerName  = names[i],
                score       = GetScore(cp, lv, farm),
                combatPower = cp,
                classIndex  = iconCount > 0 ? (nameHash % iconCount) : 0,
                isMe        = false
            });
        }
        return list;
    }

    private void SortEntries(List<RankEntry> entries)
        => entries.Sort((a, b) => b.score.CompareTo(a.score));

    // ═══════════════════════════════════════════════════════════════
    //  프로필 이미지 시스템 (기존 유지)
    // ═══════════════════════════════════════════════════════════════

    private void SetupPortraitSystem()
    {
        LoadPortraitIndex();

        if (myPortraitImage != null)
        {
            Button portraitBtn = myPortraitImage.GetComponent<Button>();
            if (portraitBtn == null)
                portraitBtn = myPortraitImage.gameObject.AddComponent<Button>();
            portraitBtn.onClick.AddListener(OpenPortraitPopup);
            ApplyPortrait();
        }

        portraitCloseBtn?.onClick.AddListener(ClosePortraitPopup);
        if (portraitPopupPanel != null) portraitPopupPanel.SetActive(false);
    }

    private void OpenPortraitPopup()
    {
        if (portraitPopupPanel == null || portraitSprites == null || portraitSprites.Length == 0)
        {
            Debug.LogWarning("[RankingManager] 프로필 팝업 또는 이미지 배열 미설정");
            return;
        }
        SoundManager.Instance?.PlayButtonClick();
        portraitPopupPanel.SetActive(true);
        BuildPortraitGrid();
    }

    private void ClosePortraitPopup()
    {
        if (portraitPopupPanel != null) portraitPopupPanel.SetActive(false);
    }

    private void BuildPortraitGrid()
    {
        if (portraitGridParent == null) return;

        foreach (Transform child in portraitGridParent) Destroy(child.gameObject);
        portraitSlotBorders.Clear();

        for (int i = 0; i < portraitSprites.Length; i++)
        {
            if (portraitSprites[i] == null) continue;
            int idx = i;

            GameObject slotGO;
            if (portraitSlotPrefab != null)
            {
                slotGO = Instantiate(portraitSlotPrefab, portraitGridParent);
            }
            else
            {
                slotGO = new GameObject($"PortraitSlot_{i}");
                slotGO.transform.SetParent(portraitGridParent, false);
                Image bg = slotGO.AddComponent<Image>();
                bg.color = normalSlotColor;
                RectTransform rt = slotGO.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(100f, 100f);

                GameObject iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(slotGO.transform, false);
                iconGO.AddComponent<Image>();
                RectTransform iconRT = iconGO.GetComponent<RectTransform>();
                iconRT.anchorMin = new Vector2(0.1f, 0.1f);
                iconRT.anchorMax = new Vector2(0.9f, 0.9f);
                iconRT.sizeDelta = Vector2.zero;
                iconRT.offsetMin = Vector2.zero;
                iconRT.offsetMax = Vector2.zero;
            }

            Transform iconChild = slotGO.transform.Find("Icon");
            if (iconChild == null) iconChild = slotGO.transform.Find("Image");
            if (iconChild != null)
            {
                Image childImg = iconChild.GetComponent<Image>();
                if (childImg != null)
                {
                    childImg.sprite = portraitSprites[i];
                    childImg.color = Color.white;
                }
            }
            else
            {
                Image slotImage = slotGO.GetComponentInChildren<Image>();
                if (slotImage != null)
                {
                    slotImage.sprite = portraitSprites[i];
                    slotImage.color = Color.white;
                }
            }

            Image borderImg = slotGO.GetComponent<Image>();
            if (borderImg != null) portraitSlotBorders.Add(borderImg);

            Button btn = slotGO.GetComponent<Button>();
            if (btn == null) btn = slotGO.AddComponent<Button>();
            btn.onClick.AddListener(() => OnPortraitSlotClicked(idx));
        }

        UpdateSlotHighlight();
    }

    private void OnPortraitSlotClicked(int index)
    {
        if (portraitSprites == null || index < 0 || index >= portraitSprites.Length) return;
        SoundManager.Instance?.PlayButtonClick();
        currentPortraitIndex = index;
        ApplyPortrait();
        ApplyPortraitToHUD();
        SavePortraitIndex();
        UpdateSlotHighlight();
        ClosePortraitPopup();
        Debug.Log($"[RankingManager] 프로필 이미지 변경: 인덱스 {index}");
    }

    private void ApplyPortrait()
    {
        if (myPortraitImage == null || portraitSprites == null || portraitSprites.Length == 0) return;
        int idx = Mathf.Clamp(currentPortraitIndex, 0, portraitSprites.Length - 1);
        myPortraitImage.sprite = portraitSprites[idx];
        myPortraitImage.color = Color.white;
        myPortraitImage.enabled = true;
    }

    private void ApplyPortraitToHUD()
    {
        if (UIManager.Instance == null || UIManager.Instance.Character == null) return;
        if (portraitSprites == null || portraitSprites.Length == 0) return;
        int idx = Mathf.Clamp(currentPortraitIndex, 0, portraitSprites.Length - 1);
        UIManager.Instance.Character.sprite = portraitSprites[idx];
        UIManager.Instance.Character.color = Color.white;
    }

    private void UpdateSlotHighlight()
    {
        for (int i = 0; i < portraitSlotBorders.Count; i++)
        {
            if (portraitSlotBorders[i] != null)
                portraitSlotBorders[i].color = (i == currentPortraitIndex)
                    ? selectedSlotColor : normalSlotColor;
        }
    }

    private void SavePortraitIndex()
    {
        if (GameDataBridge.CurrentData != null)
            GameDataBridge.CurrentData.selectedPortraitIndex = currentPortraitIndex;
        SaveLoadManager.Instance?.SaveGame();
    }

    private void LoadPortraitIndex()
    {
        if (GameDataBridge.CurrentData != null)
            currentPortraitIndex = GameDataBridge.CurrentData.selectedPortraitIndex;
        else
            currentPortraitIndex = 0;
    }

    public void RestorePortraitFromSave()
    {
        LoadPortraitIndex();
        ApplyPortrait();
        ApplyPortraitToHUD();
    }

    // ═══════════════════════════════════════════════════════════════
    //  공용 헬퍼
    // ═══════════════════════════════════════════════════════════════

    public Sprite GetClassIcon(int idx)
    {
        if (classIcons == null || classIcons.Length == 0) return null;
        return classIcons[Mathf.Clamp(idx, 0, classIcons.Length - 1)];
    }

    private string GetMyName()
    {
        SaveData data = GameDataBridge.CurrentData;
        if (data != null)
        {
            if (!string.IsNullOrEmpty(data.selectedCharacterName)) return data.selectedCharacterName;
            if (!string.IsNullOrEmpty(data.accountID)) return data.accountID;
        }
        return "모험가";
    }

    public Sprite GetCurrentPortraitSprite()
    {
        if (portraitSprites == null || portraitSprites.Length == 0) return null;
        int idx = Mathf.Clamp(currentPortraitIndex, 0, portraitSprites.Length - 1);
        return portraitSprites[idx];
    }
}
