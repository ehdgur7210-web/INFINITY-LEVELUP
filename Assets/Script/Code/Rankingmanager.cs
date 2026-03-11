using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// RankingManager
/// RankEntryItem.cs 와 연동 (RankEntry, RankType 중첩 타입 포함)
/// </summary>
public class RankingManager : MonoBehaviour
{
    public static RankingManager Instance;

    // ═══ 중첩 타입 (RankEntryItem.cs 에서 참조) ══════════════════

    public enum RankType { CombatPower, Level, Farm }

    public class RankEntry
    {
        public string playerName;
        public int score;
        public int classIndex; // 0=전사, 1=원거리, 2=마법사
        public bool isMe;
    }

    // ★ 내 직업 인덱스 - 캐릭터 선택 시 외부에서 설정
    // 예) RankingManager.MyClassIndex = 0;
    public static int MyClassIndex = 0;

    // ═══ Inspector 필드 ══════════════════════════════════════════

    [Header("===== 패널 =====")]
    public GameObject rankingPanel;

    [Header("===== 탭 버튼 =====")]
    public Button tabCombatBtn;
    public Button tabLevelBtn;
    public Button tabFarmBtn;
    public Color tabActiveColor = new Color(1f, 0.85f, 0.3f);
    public Color tabInactiveColor = new Color(0.5f, 0.5f, 0.5f);

    [Header("===== 리스트 =====")]
    public Transform rankEntryContainer;
    public GameObject rankEntryPrefab;       // RankEntryItem 프리팹
    public int maxDisplayCount = 50;

    [Header("===== 내 순위 고정바 =====")]
    public TextMeshProUGUI myRankText;
    public TextMeshProUGUI myNameText;
    public Image myIconImage;
    public TextMeshProUGUI myScoreText;

    [Header("===== 직업 아이콘 (0=전사 / 1=원거리 / 2=마법사) =====")]
    public Sprite[] classIcons;

    [Header("===== 닫기 =====")]
    public Button closeButton;

    // ═══ 내부 ════════════════════════════════════════════════════

    private RankType currentType = RankType.CombatPower;

    // ═══════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        SetupButtons();
        if (rankingPanel != null) rankingPanel.SetActive(false);
    }

    void OnEnable() => RefreshRanking();

    // ─── 버튼 초기화 ─────────────────────────────────────────────

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
        SetTabColor(tabCombatBtn, currentType == RankType.CombatPower);
        SetTabColor(tabLevelBtn, currentType == RankType.Level);
        SetTabColor(tabFarmBtn, currentType == RankType.Farm);
    }

    private void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = active ? tabActiveColor : tabInactiveColor;
        var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.color = active ? Color.black : Color.white;
    }

    // ─── 패널 열기 / 닫기 ────────────────────────────────────────

    public void OpenPanel()
    {
        if (rankingPanel != null) rankingPanel.SetActive(true);
        RefreshRanking();
    }

    public void ClosePanel()
    {
        if (rankingPanel != null) rankingPanel.SetActive(false);
    }

    // ─── 랭킹 갱신 ───────────────────────────────────────────────

    public void RefreshRanking()
    {
        var entries = BuildEntries();
        SortEntries(entries);
        ClearList();

        int myRank = -1;
        for (int i = 0; i < Mathf.Min(entries.Count, maxDisplayCount); i++)
        {
            if (entries[i].isMe) myRank = i + 1;
            SpawnEntry(i + 1, entries[i]);
        }

        UpdateMyRow(myRank, entries);
    }

    // ─── 엔트리 구성 ─────────────────────────────────────────────

    private List<RankEntry> BuildEntries()
    {
        var list = new List<RankEntry>();

        // 내 데이터
        list.Add(new RankEntry
        {
            playerName = GetMyName(),
            score = GetScore(
                             CombatPowerManager.Instance?.TotalCombatPower ?? 0,
                             GameManager.Instance?.PlayerLevel ?? 1,
                             FarmManager.Instance?.GetCropPoints() ?? 0),
            classIndex = MyClassIndex,
            isMe = true
        });

        // NPC
        list.AddRange(GenerateNpcEntries());
        return list;
    }

    private int GetScore(int cp, int lv, int farm) => currentType switch
    {
        RankType.CombatPower => cp,
        RankType.Level => lv,
        RankType.Farm => farm,
        _ => cp
    };

    // ─── NPC 더미 ────────────────────────────────────────────────

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

        int baseCp = CombatPowerManager.Instance?.TotalCombatPower ?? 100;
        int baseLv = GameManager.Instance?.PlayerLevel ?? 1;
        int baseFarm = FarmManager.Instance?.GetCropPoints() ?? 0;
        int iconCount = classIcons != null ? classIcons.Length : 1;

        var list = new List<RankEntry>();
        for (int i = 0; i < names.Length; i++)
        {
            float ratio = Random.Range(0.3f, 2.5f);
            int cp = Mathf.Max(1, Mathf.RoundToInt(baseCp * ratio));
            int lv = Mathf.Clamp(Mathf.RoundToInt(baseLv * ratio), 1, 999);
            int farm = Mathf.Max(0, Mathf.RoundToInt(baseFarm * ratio));

            list.Add(new RankEntry
            {
                playerName = names[i],
                score = GetScore(cp, lv, farm),
                classIndex = iconCount > 0 ? Random.Range(0, iconCount) : 0,
                isMe = false
            });
        }
        return list;
    }

    // ─── 정렬 ────────────────────────────────────────────────────

    private void SortEntries(List<RankEntry> entries)
        => entries.Sort((a, b) => b.score.CompareTo(a.score));

    // ─── 항목 생성 ───────────────────────────────────────────────

    private void SpawnEntry(int rank, RankEntry entry)
    {
        if (rankEntryPrefab == null || rankEntryContainer == null) return;

        GameObject go = Instantiate(rankEntryPrefab, rankEntryContainer);
        var item = go.GetComponent<RankEntryItem>() ?? go.AddComponent<RankEntryItem>();

        Sprite icon = GetClassIcon(entry.classIndex);
        item.Setup(rank, entry, icon, currentType);
    }

    private void ClearList()
    {
        if (rankEntryContainer == null) return;
        foreach (Transform c in rankEntryContainer) Destroy(c.gameObject);
    }

    // ─── 내 고정 행 ──────────────────────────────────────────────

    private void UpdateMyRow(int myRank, List<RankEntry> sorted)
    {
        var me = sorted.Find(e => e.isMe);
        if (me == null) return;

        if (myRankText != null) myRankText.text = myRank > 0 ? $"{myRank}위" : "-";
        if (myNameText != null) myNameText.text = me.playerName;
        if (myScoreText != null) myScoreText.text = ScoreLabel(me.score);

        if (myIconImage != null)
        {
            myIconImage.sprite = GetClassIcon(MyClassIndex);
            myIconImage.enabled = myIconImage.sprite != null;
        }
    }

    private string ScoreLabel(int score) => currentType switch
    {
        RankType.CombatPower => $"{score:N0}",
        RankType.Level => $"Lv. {score}",
        RankType.Farm => $"{score:N0} p",
        _ => score.ToString()
    };

    // ─── 헬퍼 ────────────────────────────────────────────────────

    public Sprite GetClassIcon(int idx)
    {
        if (classIcons == null || classIcons.Length == 0) return null;
        return classIcons[Mathf.Clamp(idx, 0, classIcons.Length - 1)];
    }

    private string GetMyName()
    {
        string s = PlayerPrefs.GetString("SelectedCharacter", "");
        if (!string.IsNullOrEmpty(s)) return s;
        s = PlayerPrefs.GetString("AccountID", "");
        if (!string.IsNullOrEmpty(s)) return s;
        return "모험가";
    }
}