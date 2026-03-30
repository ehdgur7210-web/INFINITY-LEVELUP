using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 보스 전용 상단 고정 멀티바 체력바 (프리팹 방식)
///
/// ★ 사용법:
/// 1. BossMonster 프리팹에 이 스크립트를 붙임
/// 2. bossBarPrefab에 보스 체력바 프리팹 연결
/// 3. 보스 등장 시 자동 생성, 사망/비활성 시 자동 제거
///
/// ★ 프리팹 구조 (직접 만들어야 함):
///   BossBarUI (Panel)
///     ├── BossIcon (Image)         ← bossIconImage
///     ├── BossName (TMP)           ← nameText
///     ├── BarBg (Image, 어두운 배경)
///     │   ├── BarBack (Image)      ← barFillBack
///     │   └── BarFront (Image)     ← barFillFront
///     └── BarCount (TMP, "x69")    ← barCountText
///
/// ★ 멀티바: hpPerBar(기본 1000) 마다 1줄
/// </summary>
public class BossMonsterHealthBar : MonsterHealthBar
{
    [Header("보스 체력바 프리팹")]
    [Tooltip("Canvas 위에 생성할 보스 체력바 UI 프리팹")]
    [SerializeField] private GameObject bossBarPrefab;

    [Header("보스 멀티바 설정")]
    [Tooltip("1줄(바) 당 HP량 — 나중에 딜이 높아지면 이 값을 올리면 됨")]
    [SerializeField] private int hpPerBar = 1000;

    [Header("보스 표시 설정")]
    [SerializeField] private string bossTitle = "강력한";

    // ── 멀티바 색상 (남은 바 수에 따라 순환) ──
    private static readonly Color[] barColors = new Color[]
    {
        new Color(1.0f, 0.65f, 0.0f),  // 주황
        new Color(0.9f, 0.2f, 0.2f),   // 빨강
        new Color(0.7f, 0.3f, 0.9f),   // 보라
        new Color(0.2f, 0.5f, 1.0f),   // 파랑
        new Color(0.2f, 0.85f, 0.3f),  // 초록
        new Color(1.0f, 0.85f, 0.0f),  // 노랑
    };

    // ── UI 참조 (프리팹에서 자동 탐색) ──
    private Canvas targetCanvas;
    private GameObject bossBarRoot;
    private Image bossIconImage;
    private Image barFillFront;
    private Image barFillBack;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI barCountText;

    // ── 상태 ──
    private int totalBars;
    private int currentBarIndex;

    // ══════════════════════════════════════════════════════
    protected override void CreateHealthBar()
    {
        monster = GetComponent<Monster>();

        // 메인 Canvas 찾기
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (var c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.scene.name != null)
            {
                targetCanvas = c;
                break;
            }
        }
        if (targetCanvas == null && canvases.Length > 0)
            targetCanvas = canvases[0];

        if (targetCanvas == null)
        {
            Debug.LogWarning("[BossHP] Canvas를 찾을 수 없습니다!");
            return;
        }

        // 멀티바 계산
        int maxHp = monster != null ? monster.maxHp : 1000;
        totalBars = Mathf.CeilToInt((float)maxHp / hpPerBar);
        currentBarIndex = totalBars - 1;

        // 프리팹으로 UI 생성
        if (bossBarPrefab != null)
        {
            bossBarRoot = Instantiate(bossBarPrefab, targetCanvas.transform);
            healthBarInstance = bossBarRoot;
            FindUIReferences();
        }
        else
        {
            Debug.LogWarning("[BossHP] bossBarPrefab이 없습니다! Inspector에서 연결하세요.");
            return;
        }

        // 이름 설정
        if (nameText != null)
        {
            string bossName = monster != null ? monster.monsterName : "Boss";
            nameText.text = bossName;
        }

        // 아이콘 설정 (SpriteRenderer에서 가져오기)
        if (bossIconImage != null)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
                bossIconImage.sprite = sr.sprite;
        }

        UpdateMultiBar();
        Debug.Log($"[BossHP] 보스 체력바 생성 — HP:{maxHp}, 바당HP:{hpPerBar}, 총바수:{totalBars}");
    }

    // ══════════════════════════════════════════════════════
    /// <summary>프리팹 안에서 UI 참조 자동 탐색</summary>
    private void FindUIReferences()
    {
        if (bossBarRoot == null) return;

        // 이름으로 찾기
        Transform t;

        t = bossBarRoot.transform.Find("BossIcon");
        if (t != null) bossIconImage = t.GetComponent<Image>();

        t = bossBarRoot.transform.Find("BossName");
        if (t != null) nameText = t.GetComponent<TextMeshProUGUI>();

        t = bossBarRoot.transform.Find("BarCount");
        if (t != null) barCountText = t.GetComponent<TextMeshProUGUI>();

        // BarFront, BarBack는 깊은 곳에 있을 수 있으므로 재귀 탐색
        barFillFront = FindDeepChild<Image>(bossBarRoot.transform, "BarFront");
        barFillBack = FindDeepChild<Image>(bossBarRoot.transform, "BarBack");

        if (barFillFront == null)
            Debug.LogWarning("[BossHP] BarFront를 찾지 못했습니다!");
        if (barFillBack == null)
            Debug.LogWarning("[BossHP] BarBack를 찾지 못했습니다!");
    }

    private T FindDeepChild<T>(Transform parent, string childName) where T : Component
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                T comp = child.GetComponent<T>();
                if (comp != null) return comp;
            }
            T found = FindDeepChild<T>(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    // ══════════════════════════════════════════════════════
    /// <summary>멀티바 상태 계산 및 UI 업데이트</summary>
    private void UpdateMultiBar()
    {
        if (monster == null || bossBarRoot == null) return;

        int hp = Mathf.Max(0, monster.currentHp);

        // 현재 바 인덱스 (0-based)
        currentBarIndex = hp > 0 ? (hp - 1) / hpPerBar : 0;
        int remainingBars = hp > 0 ? currentBarIndex + 1 : 0;

        // 현재 바 내에서의 채움 비율
        float fillInBar;
        if (hp <= 0)
        {
            fillInBar = 0f;
        }
        else
        {
            int hpInCurrentBar = hp - (currentBarIndex * hpPerBar);
            fillInBar = (float)hpInCurrentBar / hpPerBar;
        }

        // 앞바 채움
        if (barFillFront != null)
        {
            barFillFront.fillAmount = fillInBar;
            barFillFront.color = GetBarColor(currentBarIndex);
        }

        // 뒷바 색상
        if (barFillBack != null)
        {
            if (currentBarIndex > 0)
            {
                barFillBack.gameObject.SetActive(true);
                barFillBack.color = GetBarColor(currentBarIndex - 1);
            }
            else
            {
                barFillBack.gameObject.SetActive(false);
            }
        }

        // 바 카운트 텍스트
        if (barCountText != null)
        {
            if (remainingBars > 1)
            {
                barCountText.text = $"x{remainingBars}";
                barCountText.color = GetBarColor(currentBarIndex);
            }
            else
            {
                barCountText.text = "";
            }
        }
    }

    // ══════════════════════════════════════════════════════
    private Color GetBarColor(int barIndex)
    {
        if (barIndex < 0) barIndex = 0;
        return barColors[barIndex % barColors.Length];
    }

    // ══════════════════════════════════════════════════════
    protected override void UpdateHealthBarPosition()
    {
        // 상단 고정 — 위치 이동 안 함
    }

    // ══════════════════════════════════════════════════════
    public override void UpdateBar(float hpRatio)
    {
        UpdateMultiBar();
    }

    // ══════════════════════════════════════════════════════
    public void SetHpPerBar(int newHpPerBar)
    {
        hpPerBar = Mathf.Max(1, newHpPerBar);
        if (monster != null)
            totalBars = Mathf.CeilToInt((float)monster.maxHp / hpPerBar);
        UpdateMultiBar();
    }

    public int GetHpPerBar() => hpPerBar;

    // ══════════════════════════════════════════════════════
    void OnDisable()
    {
        if (bossBarRoot != null)
        {
            Destroy(bossBarRoot);
            bossBarRoot = null;
            healthBarInstance = null;
        }
    }

    protected override void OnDestroy()
    {
        if (bossBarRoot != null)
        {
            Destroy(bossBarRoot);
            bossBarRoot = null;
            healthBarInstance = null;
        }
        base.OnDestroy();
    }
}
