using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ══════════════════════════════════════════════════════════
/// RankPodiumSlot — TOP 3 포디움 슬롯 컴포넌트
/// ══════════════════════════════════════════════════════════
///
/// 프리팹 계층구조:
///   PodiumSlot (이 스크립트)
///     ├ PodiumBase       (Image)           ← podiumBase (금/은/동 포디움 배경)
///     ├ RankBadge        (Image)           ← rankBadge  (원형 순위 뱃지)
///     │   └ RankNumber   (TMP_Text)        ← rankNumber ("1", "2", "3")
///     ├ CharacterFrame   (Image)           ← characterFrame (원형 프레임)
///     │   └ CharacterImg (Image)           ← characterImage (캐릭터 아이콘)
///     ├ CrownIcon        (Image)           ← crownIcon (1위 전용 왕관, 2~3위 비활성)
///     ├ NameText         (TMP_Text)        ← nameText
///     ├ PowerText        (TMP_Text)        ← powerText  ("전투력 12.5K")
///     └ ScoreText        (TMP_Text)        ← scoreText  ("St.XXX")
/// ══════════════════════════════════════════════════════════
/// </summary>
public class RankPodiumSlot : MonoBehaviour
{
    [Header("===== UI 요소 =====")]
    [SerializeField] private Image podiumBase;
    [SerializeField] private Image rankBadge;
    [SerializeField] private TextMeshProUGUI rankNumber;
    [SerializeField] private Image characterFrame;
    [SerializeField] private Image characterImage;
    [SerializeField] private GameObject crownIcon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI powerText;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("===== 순위 색상 =====")]
    [SerializeField] private Color goldColor = new Color(1f, 0.84f, 0f);
    [SerializeField] private Color silverColor = new Color(0.78f, 0.78f, 0.82f);
    [SerializeField] private Color bronzeColor = new Color(0.8f, 0.52f, 0.25f);

    [Header("===== 포디움 배경 색상 =====")]
    [SerializeField] private Color goldPodiumColor = new Color(1f, 0.92f, 0.6f, 0.35f);
    [SerializeField] private Color silverPodiumColor = new Color(0.85f, 0.85f, 0.9f, 0.3f);
    [SerializeField] private Color bronzePodiumColor = new Color(0.9f, 0.75f, 0.55f, 0.3f);

    /// <summary>빈 슬롯 표시</summary>
    private bool isEmpty;

    /// <summary>
    /// RectTransform 크기 검증 — 크기 0이면 NaN 발생 방지
    /// </summary>
    void Awake()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector2 size = rt.sizeDelta;
            if (size.x == 0f && Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x))
                size.x = 200f;
            if (size.y == 0f && Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y))
                size.y = 250f;
            rt.sizeDelta = size;
        }
    }

    /// <summary>포디움 슬롯 데이터 세팅</summary>
    public void Setup(int rank, RankingManager.RankEntry entry, Sprite classIcon,
                      RankingManager.RankType rankType, int combatPower)
    {
        isEmpty = (entry == null);

        // 순위 뱃지
        Color badgeColor = GetRankColor(rank);
        if (rankBadge != null) rankBadge.color = badgeColor;
        if (rankNumber != null) rankNumber.text = rank.ToString();

        // 포디움 배경
        if (podiumBase != null)
            podiumBase.color = GetPodiumColor(rank);

        // 왕관 (1위만)
        if (crownIcon != null) crownIcon.SetActive(rank == 1 && !isEmpty);

        if (isEmpty)
        {
            SetEmpty();
            return;
        }

        // 캐릭터 이미지
        if (characterImage != null)
        {
            characterImage.sprite = classIcon;
            characterImage.enabled = classIcon != null;
            characterImage.color = Color.white;
        }
        if (characterFrame != null)
            characterFrame.color = badgeColor;

        // 닉네임
        if (nameText != null)
        {
            nameText.text = entry.playerName;
            if (entry.isMe)
            {
                nameText.fontStyle = FontStyles.Bold;
                nameText.color = new Color(0.3f, 0.95f, 1f);
            }
            else
            {
                nameText.fontStyle = FontStyles.Normal;
                nameText.color = Color.white;
            }
        }

        // 전투력 (K 단위)
        if (powerText != null)
            powerText.text = RankingFormatUtil.FormatPowerShort(combatPower);

        // 점수
        if (scoreText != null)
            scoreText.text = RankingFormatUtil.FormatScoreWithPrefix(entry.score, rankType);
    }

    private void SetEmpty()
    {
        if (characterImage != null) { characterImage.sprite = null; characterImage.enabled = false; }
        if (nameText != null) nameText.text = "---";
        if (powerText != null) powerText.text = "";
        if (scoreText != null) scoreText.text = "";
    }

    private Color GetRankColor(int rank) => rank switch
    {
        1 => goldColor,
        2 => silverColor,
        3 => bronzeColor,
        _ => Color.gray
    };

    private Color GetPodiumColor(int rank) => rank switch
    {
        1 => goldPodiumColor,
        2 => silverPodiumColor,
        3 => bronzePodiumColor,
        _ => new Color(0.5f, 0.5f, 0.5f, 0.2f)
    };
}
