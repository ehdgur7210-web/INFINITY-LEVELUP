using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
// RankEntryItem.cs — 4위~ 일반 랭킹 리스트 행
//
// ★ 레퍼런스 디자인 적용:
//   - 라운드 카드 스타일 배경
//   - 홀수/짝수 행 색상 분리
//   - 원형 캐릭터 썸네일 (Mask 적용)
//   - 전투력 K단위 표기
//   - 내 항목은 골드 하이라이트
//
// ★ 프리팹 구조:
//   RankEntryItem (이 스크립트, Image=카드 배경)
//     ├ RankText      (TMP_Text)        ← rankText   ("4", "5" ...)
//     ├ Thumbnail     (Image+Mask)      ← thumbnail  (원형 캐릭터 이미지)
//     │   └ Icon      (Image)           ← classIcon  (실제 스프라이트)
//     ├ NameText      (TMP_Text)        ← nameText   (닉네임)
//     ├ PowerText     (TMP_Text)        ← powerText  ("12.5K", K단위)
//     ├ ScoreText     (TMP_Text)        ← scoreText  ("St.XXX")
//     └ MeMarker      (GameObject)      ← meMarker   ("나" 배지)
// ═══════════════════════════════════════════════════════════════════

public class RankEntryItem : MonoBehaviour
{
    [Header("===== UI 요소 =====")]
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private Image classIcon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI powerText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject meMarker;

    [Header("===== 행 색상 =====")]
    [SerializeField] private Color oddRowColor = new Color(0.18f, 0.2f, 0.28f, 0.9f);
    [SerializeField] private Color evenRowColor = new Color(0.14f, 0.16f, 0.22f, 0.9f);
    [SerializeField] private Color meRowColor = new Color(1f, 0.85f, 0.2f, 0.2f);

    [Header("===== 텍스트 색상 =====")]
    [SerializeField] private Color normalTextColor = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color meTextColor = new Color(1f, 0.92f, 0.5f);
    [SerializeField] private Color rankHighColor = new Color(1f, 0.85f, 0.3f);

    // ═══ 세팅 ════════════════════════════════════════════════════

    /// <summary>
    /// 리스트 행 세팅 (4위~)
    /// rowIndex: 리스트 내 인덱스 (0-based, 홀짝 행 색상용)
    /// combatPower: 전투력 (K단위 표시용, 별도 전달)
    /// </summary>
    public void Setup(int rank, RankingManager.RankEntry entry,
                      Sprite icon, RankingManager.RankType type,
                      int rowIndex, int combatPower)
    {
        // ── 배경 (홀짝/내 항목) ────────────────────────────────
        if (background != null)
        {
            if (entry.isMe)
                background.color = meRowColor;
            else
                background.color = (rowIndex % 2 == 0) ? evenRowColor : oddRowColor;
        }

        // ── 순위 텍스트 ────────────────────────────────────────
        if (rankText != null)
        {
            rankText.text = rank.ToString();
            rankText.color = (rank <= 10) ? rankHighColor : normalTextColor;
        }

        // ── 캐릭터 아이콘 (원형) ──────────────────────────────
        if (classIcon != null)
        {
            classIcon.sprite = icon;
            classIcon.enabled = icon != null;
        }

        // ── 닉네임 ─────────────────────────────────────────────
        if (nameText != null)
        {
            nameText.text = entry.playerName;
            nameText.fontStyle = entry.isMe ? FontStyles.Bold : FontStyles.Normal;
            nameText.color = entry.isMe ? meTextColor : normalTextColor;
        }

        // ── 전투력 (K 단위) ─────────────────────────────────────
        if (powerText != null)
            powerText.text = RankingFormatUtil.FormatPowerShort(combatPower);

        // ── 점수 (St.XXX) ──────────────────────────────────────
        if (scoreText != null)
            scoreText.text = RankingFormatUtil.FormatScoreWithPrefix(entry.score, type);

        // ── "나" 마커 ──────────────────────────────────────────
        if (meMarker != null)
            meMarker.SetActive(entry.isMe);
    }

    // ★ 하위 호환: 기존 4인자 Setup도 유지 (레거시 호출 방지)
    public void Setup(int rank, RankingManager.RankEntry entry,
                      Sprite icon, RankingManager.RankType type)
    {
        Setup(rank, entry, icon, type, rank, 0);
    }
}
