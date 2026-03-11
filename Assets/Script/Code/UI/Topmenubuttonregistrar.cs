using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ★ 각 메뉴 버튼 오브젝트에 붙이는 컴포넌트
///
/// 씬 로드마다 Start()에서 TopMenuManager에 자신을 자동 재등록
///
/// [사용법]
/// 각 버튼 오브젝트 선택 → Add Component → TopMenuButtonRegistrar
/// ButtonType 문자열 입력:
///   inventory / shop / equipment / skill / mail / achieve
///   craft / enhancement / auction / settings / toggle
/// </summary>
[RequireComponent(typeof(Button))]
public class TopMenuButtonRegistrar : MonoBehaviour
{
    [Tooltip("inventory / shop / equipment / skill / mail / achieve / craft / enhancement / auction / settings / toggle")]
    [SerializeField] private string buttonType;

    void Start()
    {
        if (TopMenuManager.Instance == null) return;
        Button btn = GetComponent<Button>();
        TopMenuManager.Instance.RebindButton(buttonType, btn);
    }
}