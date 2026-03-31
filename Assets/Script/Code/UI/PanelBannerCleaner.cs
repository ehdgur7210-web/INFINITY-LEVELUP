using UnityEngine;

/// <summary>
/// PanelBannerCleaner — 패널이 비활성화(닫힘)될 때 상단 배너를 자동 복원
///
/// ★ 사용법:
///   배너가 변경되는 패널(장비/상점/스킬/제작/강화 등)에 이 컴포넌트를 붙이면
///   패널이 SetActive(false) 되는 순간 자동으로 배너가 레벨 텍스트로 복원됩니다.
///
///   Inspector의 OnClick에서 직접 SetActive(false) 호출하는 닫기 버튼도 커버됩니다.
/// </summary>
public class PanelBannerCleaner : MonoBehaviour
{
    [Tooltip("이 패널이 닫힐 때 배너를 복원할지 여부")]
    [SerializeField] private bool clearOnDisable = true;

    private bool wasActiveLastFrame = false;

    void OnEnable()
    {
        wasActiveLastFrame = true;
    }

    void OnDisable()
    {
        // 패널이 활성→비활성으로 전환될 때만 배너 복원
        // (씬 언로드 등으로 비활성화되는 경우는 TopMenuManager가 null이므로 안전)
        if (clearOnDisable && wasActiveLastFrame)
        {
            TopMenuManager.Instance?.ClearBanner();
        }
        wasActiveLastFrame = false;
    }
}
