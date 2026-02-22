using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ============================================================
/// SettingsButtonHandler - 어디서든 설정(옵션)창을 여는 범용 버튼
/// ============================================================
/// 
/// 【역할】
/// 로그인, 서버선택, 캐릭터선택, 게임 씬 등
/// 어떤 화면이든 이 스크립트를 설정 버튼에 붙이면
/// OptionUI 옵션창을 열 수 있음
/// 
/// 【사용법 - 매우 간단!】
/// 1. 각 화면에 설정(톱니바퀴) 버튼 만들기
/// 2. 이 스크립트를 그 버튼에 붙이기
/// 3. 끝! (Inspector에서 따로 연결할 것 없음)
/// 
/// 【적용할 화면 목록】
/// - 로그인 화면 (LoginSystem)
/// - 서버 선택 화면 (ServerSelectionSystem)
/// - 캐릭터 선택 화면 (CharacterSelectionSystem)
/// - 게임 메인 화면 (TopMenuManager - 이미 적용됨)
/// ============================================================
/// </summary>
public class SettingsButtonHandler : MonoBehaviour
{
    // ───────────────────────────────────────────
    // [Inspector 설정]
    // ───────────────────────────────────────────

    [Header("===== 설정 =====")]
    [Tooltip("버튼 컴포넌트 (비워두면 자기 자신의 Button을 자동으로 찾음)")]
    [SerializeField] private Button settingsButton;

    [Tooltip("버튼 클릭 시 효과음 재생 여부")]
    [SerializeField] private bool playSound = true;

    /// <summary>
    /// 초기화 - 버튼 이벤트 자동 연결
    /// </summary>
    void Start()
    {
        // 버튼이 비어있으면 자기 자신에서 Button 컴포넌트를 찾음
        if (settingsButton == null)
        {
            settingsButton = GetComponent<Button>();
        }

        // 버튼 클릭 이벤트 연결
        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }
        else
        {
            Debug.LogWarning($"[SettingsButtonHandler] {gameObject.name}에 Button 컴포넌트가 없습니다!");
        }
    }

    /// <summary>
    /// 설정 버튼 클릭 시 호출
    /// OptionUI의 옵션 패널을 토글(열기/닫기)함
    /// </summary>
    private void OnSettingsClicked()
    {
        // 효과음 재생
        if (playSound && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayButtonClick();
        }

        // OptionUI 옵션창 토글
        if (OptionUI.Instance != null)
        {
            OptionUI.Instance.ToggleOptionPanel();
        }
        else
        {
            Debug.LogWarning("[SettingsButtonHandler] OptionUI 인스턴스를 찾을 수 없습니다! " +
                             "SoundManager와 OptionUI가 씬에 있는지 확인하세요.");
        }
    }
}