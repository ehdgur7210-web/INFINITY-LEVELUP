using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// VIP 하단 탭 버튼 프리팹용 컴포넌트
///
/// [프리팹 구조]
///   VipTabButton (Button + Image + VipTabButton.cs)
///   └── Text (TextMeshProUGUI) — "VIP6"
///
/// VipUI.BuildTabButtons()에서 Instantiate 후
/// GetComponentInChildren<TextMeshProUGUI>()로 텍스트를 설정하고
/// Button.onClick으로 SelectTab()을 연결합니다.
/// </summary>
public class VipTabButton : MonoBehaviour
{
    [Header("UI 요소")]
    [Tooltip("탭 버튼 텍스트 (VIP1, VIP2 등)")]
    public TextMeshProUGUI tabText;

    [Tooltip("선택 상태 표시 이미지 (선택, 하이라이트용)")]
    public Image highlightImage;

    [Tooltip("VIP 등급 아이콘 이미지 (VipData.buttonIcon 표시용)")]
    public Image iconImage;

    /// <summary>탭 버튼 초기 설정</summary>
    public void Setup(int vipLevel, Sprite icon = null)
    {
        if (tabText != null)
            tabText.text = $"VIP{vipLevel}";

        if (iconImage != null && icon != null)
        {
            iconImage.sprite = icon;
            iconImage.color = Color.white;
            iconImage.enabled = true;
        }
        else if (iconImage != null)
        {
            iconImage.enabled = false;
        }
    }

    /// <summary>선택 상태 시각적 표시</summary>
    public void SetSelected(bool selected, Color selectedColor, Color normalColor)
    {
        if (highlightImage != null)
            highlightImage.color = selected ? selectedColor : normalColor;

        if (tabText != null)
            tabText.color = selected ? Color.white : new Color(0.7f, 0.7f, 0.7f);
    }
}
