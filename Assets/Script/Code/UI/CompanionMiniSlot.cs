using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 동료 상세 패널 우측 목록용 미니 슬롯
///
/// CompanionDetailPanel의 companionSlotPrefab에 붙이는 컴포넌트.
/// 클릭 시 해당 동료로 전환.
///
/// [프리팹 Hierarchy]
///   CompanionMiniSlot (Image + Button + CompanionMiniSlot.cs)
///   ├── Portrait (Image) → portrait
///   ├── Name (TextMeshProUGUI) → nameText
///   ├── Level (TextMeshProUGUI) → levelText        — "Lv.5"
///   ├── Stars (TextMeshProUGUI) → starsText        — "★★★"
///   └── (Outline 컴포넌트) — 선택 시 enabled
///
/// [Inspector 연결]
///   portrait     → Portrait (Image)
///   nameText     → Name (TMP)
///   levelText    → Level (TMP)
///   starsText    → Stars (TMP)
///   background   → 자기 자신 Image (레어리티 배경색)
///   선택 표시는 Outline 컴포넌트 자동 탐색 (Inspector 연결 불필요)
/// </summary>
public class CompanionMiniSlot : MonoBehaviour
{
    [Header("슬롯 UI")]
    public Image portrait;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI starsText;
    public Image background;

    // 내부 데이터
    private CompanionData _data;
    private int _level;
    private bool _isSelected;
    private Outline _outline;

    // 레어리티 색상
    private static readonly Color[] RarityColors =
    {
        new Color(0.25f, 0.25f, 0.25f, 0.8f),  // Common
        new Color(0.1f, 0.2f, 0.45f, 0.8f),    // Rare
        new Color(0.35f, 0.1f, 0.45f, 0.8f),   // Epic
        new Color(0.5f, 0.35f, 0.05f, 0.8f),   // Legendary
    };

    // ═══════════════════════════════════════════════════════════════
    //  설정
    // ═══════════════════════════════════════════════════════════════

    /// <summary>슬롯에 동료 데이터 표시</summary>
    public void Setup(CompanionData data, int level, int stars)
    {
        _data = data;
        _level = level;

        // Outline 컴포넌트 캐싱
        if (_outline == null)
            _outline = GetComponent<Outline>();

        if (portrait != null)
        {
            portrait.sprite = data != null ? data.portrait : null;
            portrait.color = data != null ? Color.white : Color.clear;
        }

        if (nameText != null)
            nameText.text = data != null ? data.companionName : "";

        if (levelText != null)
            levelText.text = data != null ? $"Lv.{Mathf.Max(1, level)}" : "";

        if (starsText != null)
        {
            starsText.text = stars > 0 ? StarSpriteUtil.GetColoredStars(stars) : "";
        }

        if (background != null && data != null)
        {
            int ri = (int)data.rarity;
            background.color = ri < RarityColors.Length ? RarityColors[ri] : RarityColors[0];
        }

        SetSelected(false);
    }

    /// <summary>선택 상태 표시 (Outline on/off)</summary>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;

        if (_outline == null)
            _outline = GetComponent<Outline>();

        if (_outline != null)
            _outline.enabled = selected;

        // 선택 시 배경 알파 강조
        if (background != null)
        {
            Color c = background.color;
            c.a = selected ? 1f : 0.8f;
            background.color = c;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  데이터 접근
    // ═══════════════════════════════════════════════════════════════

    public CompanionData GetData() => _data;
    public int GetLevel() => _level;
    public bool IsSelected => _isSelected;

    // ═══════════════════════════════════════════════════════════════
    //  유틸
    // ═══════════════════════════════════════════════════════════════

}
