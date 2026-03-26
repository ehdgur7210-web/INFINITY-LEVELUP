using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// VIP 혜택 항목 프리팹용 컴포넌트
///
/// [프리팹 구조]
///   VipBenefitItem (HorizontalLayoutGroup)
///   ├── Icon (Image) — 혜택 아이콘 (선택)
///   └── Text (TextMeshProUGUI) — "• 보스 도전 횟수 +3"
///
/// VipUI.RefreshBenefitList()에서 Instantiate 후
/// GetComponentInChildren<TextMeshProUGUI>()로 텍스트를 설정합니다.
/// </summary>
public class VipBenefitItem : MonoBehaviour
{
    [Header("UI 요소")]
    [Tooltip("혜택 아이콘 (선택, 없으면 텍스트만 표시)")]
    public Image benefitIcon;

    [Tooltip("혜택 설명 텍스트")]
    public TextMeshProUGUI benefitText;

    /// <summary>혜택 데이터로 UI 설정</summary>
    public void Setup(VipBenefitData benefit, Sprite icon = null)
    {
        if (benefitText != null)
            benefitText.text = $"• {benefit.description}";

        if (benefitIcon != null)
        {
            if (icon != null)
            {
                benefitIcon.sprite = icon;
                benefitIcon.color = Color.white;
                benefitIcon.gameObject.SetActive(true);
            }
            else
            {
                benefitIcon.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>텍스트만으로 간단 설정</summary>
    public void Setup(string text)
    {
        if (benefitText != null)
            benefitText.text = text;
    }
}
