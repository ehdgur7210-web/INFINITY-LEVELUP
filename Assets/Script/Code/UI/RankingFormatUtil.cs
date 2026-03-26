/// <summary>
/// ══════════════════════════════════════════════════════════
/// RankingFormatUtil — 랭킹 UI 공용 포맷 유틸리티
/// ══════════════════════════════════════════════════════════
/// K/M 단위 변환, 점수 포맷, 시간 포맷 등
/// RankingManager, RankPodiumSlot, RankEntryItem 공용
/// ══════════════════════════════════════════════════════════
/// </summary>
public static class RankingFormatUtil
{
    /// <summary>숫자를 K/M 단위로 축약 (1,234 → "1.2K", 1,234,567 → "1.2M")</summary>
    public static string FormatK(int value)
    {
        if (value >= 1_000_000)
            return $"{value / 1_000_000f:F1}M";
        if (value >= 1_000)
            return $"{value / 1_000f:F1}K";
        return value.ToString();
    }

    /// <summary>전투력 표시용 ("12.5K" 또는 "890")</summary>
    public static string FormatPowerShort(int power)
    {
        if (power <= 0) return "0";
        return FormatK(power);
    }

    /// <summary>랭킹 타입별 점수 텍스트 (St.XXX 스타일)</summary>
    public static string FormatScoreWithPrefix(int score, RankingManager.RankType type)
    {
        return type switch
        {
            RankingManager.RankType.CombatPower => $"St.{FormatK(score)}",
            RankingManager.RankType.Level       => $"Lv.{score}",
            RankingManager.RankType.Farm        => $"St.{FormatK(score)}",
            _ => score.ToString()
        };
    }

    /// <summary>점수 라벨 (내 순위 바 등에서 사용)</summary>
    public static string FormatScoreLabel(int score, RankingManager.RankType type)
    {
        return type switch
        {
            RankingManager.RankType.CombatPower => FormatK(score),
            RankingManager.RankType.Level       => $"Lv. {score}",
            RankingManager.RankType.Farm        => $"{FormatK(score)} p",
            _ => score.ToString()
        };
    }

    /// <summary>초 → "X시간 XX분" 포맷</summary>
    public static string FormatTimeRemaining(float totalSeconds)
    {
        if (totalSeconds <= 0) return "종료";
        int hours = (int)(totalSeconds / 3600f);
        int minutes = (int)((totalSeconds % 3600f) / 60f);
        if (hours > 0) return $"{hours}시간 {minutes:D2}분";
        return $"{minutes}분";
    }
}
