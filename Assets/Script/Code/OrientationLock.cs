using UnityEngine;

/// <summary>
/// 앱 시작 시 자동으로 가로(LandscapeLeft) 고정.
/// Player Settings의 Default Orientation이 잘못 설정돼 있어도 코드에서 강제로 잡아줌.
/// 어떤 씬에서 시작해도(IntroVideo/LoginScene/MainScene/FarmScene) 무조건 적용됨.
///
/// ⚠ Editor(Game View)에서는 적용 안 함 — Editor의 Game View Aspect 설정을 따름.
///    (안 그러면 Editor에서 화면이 회전돼서 거꾸로 보임)
/// </summary>
public static class OrientationLock
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceLandscape()
    {
#if UNITY_ANDROID || UNITY_IOS
        // 자동 회전 꺼서 폰을 돌려도 화면이 안 흔들리게
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        // 가로 고정 (LandscapeLeft = 홈 버튼이 오른쪽)
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        Debug.Log("[OrientationLock] 화면을 LandscapeLeft로 고정 (모바일)");
#else
        // Editor / Standalone PC 빌드는 Player Settings/Game View 설정 그대로 사용
        Debug.Log("[OrientationLock] Editor/PC에서는 Game View 설정 사용 (강제 회전 안 함)");
#endif
    }
}
