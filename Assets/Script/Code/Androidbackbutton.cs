using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 통합 뒤로가기/ESC 핸들러
///
/// - 로그인 씬: 종료 확인 다이얼로그 (UIManager.ShowConfirmDialog)
/// - 게임 씬: 열린 패널 스택(PanelBackButton.OpenStack)에 패널이 있으면
///            가장 최근에 열린 패널을 닫음. 없으면 종료 확인 다이얼로그.
///
/// UIManager.Instance가 없으면 두 번 누르기(legacy) 방식으로 폴백.
/// </summary>
public class AndroidBackButton : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("로그인 씬 이름 (이 씬에서는 ESC=종료 확인)")]
    [SerializeField] private string loginSceneName = "LoginScene";

    [Tooltip("UIManager가 없을 때 두 번 누르기 폴백 사용")]
    [SerializeField] private bool fallbackDoublePress = true;

    [Tooltip("두 번 누르기 대기 시간 (초)")]
    [SerializeField] private float doublePressTime = 2f;

    private float lastBackPressTime = -10f;
    private bool _quitDialogOpen = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBackPressed();
        }
    }

    private void OnBackPressed()
    {
        // 종료 확인 다이얼로그가 이미 떠 있으면 무시 (중복 방지)
        if (_quitDialogOpen) return;

        string sceneName = SceneManager.GetActiveScene().name;
        bool isLogin = sceneName == loginSceneName;

        // ── 게임 씬: 열린 패널이 있으면 닫기 ──
        if (!isLogin && PanelBackButton.OpenStack.Count > 0)
        {
            // 스택에서 활성화된 마지막 패널 찾기
            for (int i = PanelBackButton.OpenStack.Count - 1; i >= 0; i--)
            {
                var panel = PanelBackButton.OpenStack[i];
                if (panel != null && panel.TargetPanel != null && panel.TargetPanel.activeInHierarchy)
                {
                    panel.OnBackButtonClicked();
                    return;
                }
            }
        }

        // ── 패널 없음 또는 로그인 씬: 종료 확인 ──
        ShowQuitConfirm();
    }

    private void ShowQuitConfirm()
    {
        if (UIManager.Instance != null)
        {
            _quitDialogOpen = true;
            UIManager.Instance.ShowConfirmDialog(
                "게임을 종료하시겠습니까?",
                onConfirm: () => { _quitDialogOpen = false; QuitApp(); },
                onCancel: () => { _quitDialogOpen = false; }
            );
            return;
        }

        // ── 폴백: UIManager 없음 (LoginScene 등) → 두 번 누르기 ──
        if (!fallbackDoublePress)
        {
            QuitApp();
            return;
        }

        float timeSinceLast = Time.realtimeSinceStartup - lastBackPressTime;
        if (timeSinceLast <= doublePressTime)
        {
            QuitApp();
        }
        else
        {
            lastBackPressTime = Time.realtimeSinceStartup;
            Debug.Log("[AndroidBackButton] 뒤로가기 1회 - 한 번 더 누르면 종료");
        }
    }

    private void QuitApp()
    {
        Debug.Log("[AndroidBackButton] 앱 종료");

        GameManager.Instance?.SaveGameData();
        SaveLoadManager.Instance?.SaveGame();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
