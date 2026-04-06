using UnityEngine;

/// <summary>
/// 별(★)을 TMP Sprite Asset 태그로 변환하는 유틸리티.
///
/// [5성 사이클 시스템]
///   매 5성마다 다음 등급 스프라이트로 승급:
///     1~5★  → sprite 0 × 1~5개 (금색)
///     6~10★ → sprite 1 × 1~5개 (보라)
///     11~15★ → sprite 2 × 1~5개 (빨강)
///     16~20★ → sprite 3 × 1~5개 (특수)
///
/// [Sprite Asset 등록 순서]
///   index 0 = 금색 별  (기본, 1~5★)
///   index 1 = 보라 별  (6~10★)
///   index 2 = 빨강 별  (11~15★)
///   index 3 = 특수 별  (16★+)
///
/// [사용법]
///   StarSpriteUtil.GetColoredStars(7)
///     → "<sprite=1><sprite=1>" (보라 별 2개, 7★ = 6+1 → tier1 × 2개)
///
/// [Sprite Asset 미설정 시]
///   useSpriteMode = false → 폰트 "★" + <color> 태그로 폴백
/// </summary>
public static class StarSpriteUtil
{
    /// <summary>true면 스프라이트 이미지, false면 폰트 문자 "★" + color 태그</summary>
    public static bool useSpriteMode = true;

    /// <summary>TMP Sprite Asset 이름 (Assets/TextMesh Pro/Resources/Sprite Assets/ 내 에셋)</summary>
    public static string spriteAssetName = "별";

    /// <summary>한 등급당 최대 별 수</summary>
    public const int StarsPerTier = 5;

    // Sprite Asset 인덱스 (별 색상별)
    public static int spriteGold   = 0;  // 금색 별  (1~5★)
    public static int spritePurple = 1;  // 보라 별  (6~10★)
    public static int spriteRed    = 2;  // 빨강 별  (11~15★)
    public static int spriteSpecial = 3; // 특수 별  (16★+)

    // 등급별 폴백 색상
    private static readonly string[] FallbackColors =
    {
        "#FFD700",  // 금색
        "#CC66FF",  // 보라
        "#FF4444",  // 빨강
        "#00FFFF",  // 특수 (시안)
    };

    // 등급별 Color (UI 텍스트 색상용)
    private static readonly Color[] TierColors =
    {
        new Color(1f, 0.85f, 0.1f),    // 금색
        new Color(0.7f, 0.3f, 1f),      // 보라
        Color.red,                        // 빨강
        Color.cyan,                       // 특수
    };

    /// <summary>총 별 수 → 현재 등급 (0부터)</summary>
    public static int GetTier(int totalStars)
    {
        if (totalStars <= 0) return 0;
        return (totalStars - 1) / StarsPerTier;
    }

    /// <summary>총 별 수 → 현재 등급에서 표시할 별 개수 (1~5)</summary>
    public static int GetDisplayCount(int totalStars)
    {
        if (totalStars <= 0) return 0;
        return ((totalStars - 1) % StarsPerTier) + 1;
    }

    /// <summary>총 별 수 → 해당 등급의 sprite index</summary>
    public static int GetSpriteIndex(int totalStars)
    {
        int tier = GetTier(totalStars);
        switch (tier)
        {
            case 0:  return spriteGold;
            case 1:  return spritePurple;
            case 2:  return spriteRed;
            default: return spriteSpecial;
        }
    }

    /// <summary>총 별 수 → 등급에 맞는 Color (UI 텍스트용)</summary>
    public static Color GetStarColor(int totalStars)
    {
        int tier = GetTier(totalStars);
        if (tier < TierColors.Length) return TierColors[tier];
        return TierColors[TierColors.Length - 1];
    }

    /// <summary>기본 별 N개 (금색)</summary>
    public static string GetStars(int count)
    {
        return GetStarsWithIndex(count, spriteGold);
    }

    /// <summary>
    /// 5성 사이클 별 표시.
    ///   7★ → 보라 별 2개 (tier1, displayCount=2)
    ///  12★ → 빨강 별 2개 (tier2, displayCount=2)
    /// </summary>
    public static string GetColoredStars(int totalStars)
    {
        if (totalStars <= 0) return "";

        int tier = GetTier(totalStars);
        int displayCount = GetDisplayCount(totalStars);
        int spriteIdx = GetSpriteIndex(totalStars);

        if (useSpriteMode)
            return GetStarsWithIndex(displayCount, spriteIdx);

        // 폰트 문자 폴백
        int colorIdx = Mathf.Min(tier, FallbackColors.Length - 1);
        return $"<color={FallbackColors[colorIdx]}>{new string('\u2605', displayCount)}</color>";
    }

    /// <summary>특정 sprite index로 별 N개</summary>
    public static string GetStarsWithIndex(int count, int spriteIndex)
    {
        if (count <= 0) return "";

        if (!useSpriteMode)
            return new string('\u2605', count);

        // ★ Sprite Asset 이름 명시 — 기본 EmojiOne 대신 "별" 에셋 사용
        string tag = $"<sprite=\"{spriteAssetName}\" index={spriteIndex}>";
        var sb = new System.Text.StringBuilder(tag.Length * count);
        for (int i = 0; i < count; i++)
            sb.Append(tag);
        return sb.ToString();
    }
}
