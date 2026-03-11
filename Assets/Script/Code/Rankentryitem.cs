using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
// RankEntryItem.cs
//
// ★ 설명:
//   RankingManager가 ScrollView Content 안에 동적으로 생성하는
//   랭킹 한 줄(Row) 프리팹에 붙이는 컴포넌트
//
// ★ 프리팹 구조:
//   RankEntryItem  ← 이 스크립트 부착
//     ├ Background  (Image)           ← background
//     ├ RankText    (TextMeshProUGUI) ← rankText
//     ├ ClassIcon   (Image)           ← classIcon
//     ├ NameText    (TextMeshProUGUI) ← nameText
//     ├ ScoreText   (TextMeshProUGUI) ← scoreText
//     └ MeMarker    (GameObject)      ← meMarker  ("나" 배지, 내 항목만 활성화)
// ═══════════════════════════════════════════════════════════════════

public class RankEntryItem : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private Image classIcon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject meMarker;

    [Header("===== 순위별 색상 =====")]
    [SerializeField] private Color rank1Color = new Color(1f, 0.84f, 0f, 1f);  // 금
    [SerializeField] private Color rank2Color = new Color(0.75f, 0.75f, 0.75f, 1f);  // 은
    [SerializeField] private Color rank3Color = new Color(0.8f, 0.5f, 0.2f, 1f);  // 동
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color meBgColor = new Color(0.2f, 0.8f, 1f, 0.25f); // 내 항목 배경

    // ═══ 세팅 ════════════════════════════════════════════════════
    public void Setup(int rank, RankingManager.RankEntry entry,
                      Sprite icon, RankingManager.RankType type)
    {
        // ── 순위 텍스트 ──────────────────────────────────────────
        if (rankText != null)
        {
            rankText.text = GetRankLabel(rank);
            rankText.color = rank switch
            {
                1 => rank1Color,
                2 => rank2Color,
                3 => rank3Color,
                _ => normalColor
            };
        }

        // ── 직업 아이콘 ──────────────────────────────────────────
        if (classIcon != null)
        {
            classIcon.sprite = icon;
            classIcon.enabled = icon != null;
        }

        // ── 이름 ─────────────────────────────────────────────────
        if (nameText != null)
        {
            nameText.text = entry.playerName;
            nameText.fontStyle = entry.isMe ? FontStyles.Bold : FontStyles.Normal;
            nameText.color = entry.isMe
                ? new Color(0.2f, 0.9f, 1f, 1f)
                : Color.white;
        }

        // ── 점수 ─────────────────────────────────────────────────
        if (scoreText != null)
        {
            scoreText.text = type switch
            {
                RankingManager.RankType.CombatPower => $"{entry.score:N0}",
                RankingManager.RankType.Level => $"Lv. {entry.score}",
                RankingManager.RankType.Farm => $"{entry.score:N0} p",
                _ => entry.score.ToString()
            };
            // 1~3위 점수 색도 강조
            scoreText.color = rank switch
            {
                1 => rank1Color,
                2 => rank2Color,
                3 => rank3Color,
                _ => normalColor
            };
        }

        // ── "나" 마커 ────────────────────────────────────────────
        if (meMarker != null)
            meMarker.SetActive(entry.isMe);

        // ── 내 항목 배경 강조 ────────────────────────────────────
        if (background != null && entry.isMe)
            background.color = meBgColor;
    }

    // ─── 순위 텍스트 포맷 ────────────────────────────────────────
    private string GetRankLabel(int rank)
    {
        return rank switch
        {
            1 => "1등",
            2 => "2등",
            3 => "3등",
            _ => $"{rank}"
        };
    }
}