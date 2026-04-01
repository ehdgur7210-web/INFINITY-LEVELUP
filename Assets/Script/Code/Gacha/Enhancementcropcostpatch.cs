using UnityEngine;
using TMPro;

/// <summary>
/// EnhancementCropCostPatch
/// ════════════════════════════════════════════════════════════════════════════════════════════════
/// 장비 강화 시 Gold 외에 CropPoint(작물 포인트)도 소모.
///
/// [사용법 - EnhancementSystem.cs 내부]
///   if (!EnhancementCropCostPatch.Instance.PayEnhanceCost(level, goldCost))
///       return;  // 강화 실패
///
/// [정적 호출]
///   EnhancementCropCostPatch.PatchEnhancement(level, goldCost)
///
/// 주의: FarmManager.SpendCropPoints() 와 GameManager.SpendGold() 는
///   이미 이 컴포넌트에서 처리하므로 이 클래스에서만 호출합니다.
/// </summary>
public class EnhancementCropCostPatch : MonoBehaviour
{
    public static EnhancementCropCostPatch Instance;

    [Header("강화 단계별 CropPoint 기본 비용")]
    [Tooltip("+0→+1 강화에 필요한 기본 CP")]
    public int cropCostPerLevelBase = 5;

    [Tooltip("강화 단계마다 CP 비용 배율 (기준^단계 형식)")]
    public float cropCostScalePerLevel = 1.5f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        Debug.Log("[ManagerInit] EnhancementCropCostPatch가 생성되었습니다.");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    //  비용 계산
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 강화 단계별 CropPoint 비용
    /// (+0→+1) = base, (+1→+2) = base * scale, ...
    /// </summary>
    public int GetCropPointCost(int currentEnhanceLevel)
    {
        int level = Mathf.Max(0, currentEnhanceLevel);
        return Mathf.RoundToInt(cropCostPerLevelBase * Mathf.Pow(cropCostScalePerLevel, level));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    //  강화 가능 여부
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gold + CropPoint 강화 가능 여부 확인
    /// </summary>
    public bool CanEnhance(int currentEnhanceLevel, int goldCost)
    {
        int cpCost = GetCropPointCost(currentEnhanceLevel);

        if (GameManager.Instance == null || GameManager.Instance.PlayerGold < goldCost)
        {
            UIManager.Instance?.ShowMessage(
                $"골드 부족! {goldCost:N0}G 필요 (현재 {UIManager.FormatKoreanUnit(GameManager.Instance?.PlayerGold ?? 0)}G)", Color.red);
            return false;
        }

        long curCp = FarmManager.Instance != null ? FarmManager.Instance.GetCropPoints() : 0;
        if (curCp < cpCost)
        {
            UIManager.Instance?.ShowMessage(
                $"작물 포인트 부족! {cpCost}CP 필요 (현재 {curCp}CP)", Color.red);
            return false;
        }

        return true;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    //  강화 실행 (Gold + CropPoint 차감)
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gold + CropPoint 차감.
    /// 성공 시 true, 실패 시 false (메시지 표시 포함).
    /// </summary>
    public bool PayEnhanceCost(int currentEnhanceLevel, int goldCost)
    {
        if (!CanEnhance(currentEnhanceLevel, goldCost)) return false;

        int cpCost = GetCropPointCost(currentEnhanceLevel);

        GameManager.Instance.SpendGold(goldCost);
        FarmManager.Instance.SpendCropPoints(cpCost);

        Debug.Log($"[EnhancementCropCostPatch] 강화 비용: -{goldCost:N0}G / -{cpCost}CP " +
                  $"(+{currentEnhanceLevel}→+{currentEnhanceLevel + 1})");
        return true;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    //  정적 편의 메서드
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 정적 호출용.
    /// Instance가 없으면 Gold만 차감 (폴백).
    /// </summary>
    public static bool PatchEnhancement(int currentLevel, int goldCost)
    {
        if (Instance != null)
            return Instance.PayEnhanceCost(currentLevel, goldCost);

        // 패치 없을 시 폴백으로 Gold만 처리
        Debug.LogWarning("[EnhancementCropCostPatch] Instance가 없어 Gold만 차감합니다.");
        if (GameManager.Instance == null || !GameManager.Instance.SpendGold(goldCost))
        {
            UIManager.Instance?.ShowMessage("골드 부족!", Color.red);
            return false;
        }
        return true;
    }
}