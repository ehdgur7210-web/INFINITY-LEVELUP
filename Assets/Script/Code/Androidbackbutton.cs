using UnityEngine;

/// <summary>
/// 안드로이드 뒤로가기 버튼 두 번 종료
/// 아무 GameObject에나 붙여주세요 (GameManager 또는 빈 오브젝트)
/// </summary>
public class AndroidBackButton : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("두 번 눌러야 종료되도록 설정")]
    public bool requireDoublePress = true;

    [Tooltip("두 번 누르기 대기 시간 (초)")]
    public float doublePressTime = 2f;

    private float lastBackPressTime = -10f;

    void Update()
    {
        // 안드로이드 뒤로가기 버튼 (에디터에서는 Escape로 테스트)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBackPressed();
        }
    }

    private void OnBackPressed()
    {
        if (!requireDoublePress)
        {
            QuitApp();
            return;
        }

        // 두 번 누르기 체크
        float timeSinceLast = Time.realtimeSinceStartup - lastBackPressTime;

        if (timeSinceLast <= doublePressTime)
        {
            // 두 번째 누름 → 종료
            QuitApp();
        }
        else
        {
            // 첫 번째 누름 → 안내 메시지
            lastBackPressTime = Time.realtimeSinceStartup;
            UIManager.Instance?.ShowMessage("한 번 더 누르면 종료됩니다.", Color.white);
            Debug.Log("[AndroidBackButton] 뒤로가기 1회 - 한 번 더 누르면 종료");
        }
    }

    private void QuitApp()
    {
        Debug.Log("[AndroidBackButton] 앱 종료");

        // 저장 후 종료
        GameManager.Instance?.SaveGameData();
        SaveLoadManager.Instance?.SaveGame();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
