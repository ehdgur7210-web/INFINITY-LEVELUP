using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ���� �� ���� ��ư
/// - FarmScene�� �ƹ� ��ư���� �� ������Ʈ�� ���̸� ��
/// </summary>
public class MainEnterButton : MonoBehaviour
{
    // ★ 중복 호출 방지: Start()에서 AddListener 제거.
    //   Inspector OnClick UnityEvent에 FarmSceneController.GoToMainGame이
    //   이미 등록되어 있어 코드로도 OnClick을 추가하면 한 번 클릭에 두 번 호출되어
    //   씬 전환이 두 번 트리거되고 cropPoints/인벤토리가 0으로 유실되는 버그가 발생함.
    //
    //   사용 방식 (둘 중 하나만 선택):
    //   (A) Inspector OnClick → FarmSceneController.GoToMainGame() 만 등록 (권장)
    //   (B) Inspector OnClick → MainEnterButton.OnClick() 만 등록 (이 컴포넌트 사용 시)

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