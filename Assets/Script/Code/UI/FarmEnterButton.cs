using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 농장 씬 이동 버튼
/// - MainScene의 아무 버튼에나 이 컴포넌트를 붙이면 됨
/// - SceneTransitionManager는 DontDestroyOnLoad라 Instance로 접근 가능
/// </summary>
public class FarmEnterButton : MonoBehaviour
{
    void Start()
    {
        GetComponent<Button>()?.onClick.AddListener(OnClick);
    }

    public void OnClick()
    {
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadFarmScene();
        }
        else
        {
            Debug.LogError("[FarmEnterButton] SceneTransitionManager.Instance가 null! " +
                           "IntroScene에 SceneTransitionManager가 배치되어 있는지 확인하세요.");
        }
    }
}