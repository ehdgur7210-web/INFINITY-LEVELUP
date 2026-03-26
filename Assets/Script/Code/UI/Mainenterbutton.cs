using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 씬 복귀 버튼
/// - FarmScene의 아무 버튼에나 이 컴포넌트를 붙이면 됨
/// </summary>
public class MainEnterButton : MonoBehaviour
{
    void Start()
    {
        GetComponent<Button>()?.onClick.AddListener(OnClick);
    }

    public void OnClick()
    {
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadMainScene();
        }
        else
        {
            Debug.LogError("[MainEnterButton] SceneTransitionManager.Instance가 null!");
        }
    }
}