using System;
using UnityEngine;

/// <summary>
/// ════════════════════════════════════════════════════════════════════════
/// CropPointService — 작물포인트 단일 소스 of truth
/// ════════════════════════════════════════════════════════════════════════
///
/// ▶ 설계 원칙
///   · cropPoints의 유일한 저장소는 GameDataBridge.CurrentData.cropPoints
///   · FarmManager / ResourceBarManager / SaveData.farmData 등 다른 곳에는
///     cropPoints 필드를 두지 않는다 (있어도 이 서비스를 통해 위임)
///   · 모든 read/write/이벤트가 이 클래스를 거치므로 race가 구조적으로 불가능
///
/// ▶ 사용법
///   · 읽기:  long cp = CropPointService.Value;
///   · 추가:  CropPointService.Add(100);
///   · 차감:  if (CropPointService.Spend(50)) { ... }
///   · 강제 설정: CropPointService.Set(1234);  // 로드 시에만
///   · UI 구독: CropPointService.OnChanged += (newValue) => UpdateUI(newValue);
///
/// ▶ 이벤트
///   · OnChanged(long newValue) — 값이 변경되면 발화 (Add/Spend/Set 모두)
/// ════════════════════════════════════════════════════════════════════════
/// </summary>
public static class CropPointService
{
    /// <summary>현재 작물포인트 값 (GameDataBridge.CurrentData.cropPoints의 단순 프록시)</summary>
    public static long Value
    {
        get => GameDataBridge.CurrentData?.cropPoints ?? 0;
    }

    /// <summary>값 변경 이벤트 (Add/Spend/Set 시 새 값으로 발화)</summary>
    public static event Action<long> OnChanged;

    /// <summary>작물포인트 추가. 음수면 무시. bridge가 null이면 무시.</summary>
    public static void Add(long amount)
    {
        if (amount <= 0) return;
        var cd = GameDataBridge.CurrentData;
        if (cd == null)
        {
            Debug.LogWarning($"[CropPointService] Add({amount}) 무시 — GameDataBridge.CurrentData == null");
            return;
        }
        cd.cropPoints += amount;
        Notify(cd.cropPoints);
    }

    /// <summary>
    /// 작물포인트 차감 시도. 부족하면 false 반환 (UI 메시지는 호출측 책임).
    /// 성공 시 true 반환.
    /// </summary>
    public static bool Spend(long amount)
    {
        if (amount <= 0) return true;
        var cd = GameDataBridge.CurrentData;
        if (cd == null)
        {
            Debug.LogWarning($"[CropPointService] Spend({amount}) 무시 — GameDataBridge.CurrentData == null");
            return false;
        }
        if (cd.cropPoints < amount) return false;
        cd.cropPoints -= amount;
        Notify(cd.cropPoints);
        return true;
    }

    /// <summary>
    /// 직접 값 설정 (로드/마이그레이션 전용 — 일반 게임플레이에서는 Add/Spend 사용).
    /// </summary>
    public static void Set(long value)
    {
        if (value < 0) value = 0;
        var cd = GameDataBridge.CurrentData;
        if (cd == null)
        {
            Debug.LogWarning($"[CropPointService] Set({value}) 무시 — GameDataBridge.CurrentData == null");
            return;
        }
        cd.cropPoints = value;
        Notify(value);
    }

    /// <summary>
    /// 잔액 충분 여부 확인 (차감 전 미리보기용).
    /// </summary>
    public static bool HasEnough(long amount) => Value >= amount;

    /// <summary>
    /// UI/씬 전환 직후 강제 갱신용 — 값 변경 없이 현재 값으로 OnChanged 재발화.
    /// (씬 전환 후 ResourceBar UI 등이 stale 표시 상태일 때)
    /// </summary>
    public static void RefreshUI() => Notify(Value);

    private static void Notify(long newValue)
    {
        try
        {
            OnChanged?.Invoke(newValue);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CropPointService] OnChanged 핸들러에서 예외: {e}");
        }
    }
}
