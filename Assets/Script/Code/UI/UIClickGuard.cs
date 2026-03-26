using UnityEngine;

/// <summary>
/// 전역 UI 클릭 가드 — 동일 프레임/짧은 간격 내 여러 버튼이 동시에 눌리는 것을 방지
/// 버튼 핸들러 맨 앞에서 UIClickGuard.Consume()을 호출하면
/// 쿨다운 내 다른 버튼 클릭은 UIClickGuard.IsBlocked → true 로 차단됨
/// </summary>
public static class UIClickGuard
{
    private static float _lastConsumeTime = -1f;
    private const float COOLDOWN = 0.3f;

    /// <summary>클릭 이벤트를 소비하고, 쿨다운 시작. 이미 쿨다운 중이면 false 반환 (차단)</summary>
    public static bool Consume()
    {
        if (Time.unscaledTime - _lastConsumeTime < COOLDOWN)
            return false; // 쿨다운 중 — 이 클릭은 무시해야 함
        _lastConsumeTime = Time.unscaledTime;
        return true; // 정상 클릭
    }

    /// <summary>현재 쿨다운 중인지 확인 (소비하지 않음)</summary>
    public static bool IsBlocked => Time.unscaledTime - _lastConsumeTime < COOLDOWN;
}
