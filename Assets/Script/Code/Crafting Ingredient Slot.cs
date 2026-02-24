using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 조합 재료 슬롯 (완전판)
/// - 일반 아이템
/// - 골드 표시 가능
/// </summary>
public class CraftingIngredientSlot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI amountText;

    [Header("골드 아이콘 (선택)")]
    [SerializeField] private Sprite goldIconSprite;  // Inspector에서 골드 아이콘 연결

    /// <summary>
    /// 일반 아이템 슬롯 설정
    /// </summary>
    public void SetupSlot(ItemData item, int required, int current)
    {
        if (item == null) return;

        // 아이콘
        if (itemIcon != null)
        {
            itemIcon.sprite = item.itemIcon;
            itemIcon.color = Color.white;
            itemIcon.gameObject.SetActive(true);
        }

        // 이름
        if (itemNameText != null)
        {
            itemNameText.text = item.itemName;
            itemNameText.color = Color.white;
        }

        // 개수 (스크린샷 스타일)
        SetAmountText(required, current);

        // 배경색
        SetBackgroundColor(current >= required);
    }

    /// <summary>
    /// ⭐ 골드 슬롯 설정 (오버로드)
    /// </summary>
    public void SetupSlotForGold(int required, int current)
    {
        // 골드 아이콘
        if (itemIcon != null)
        {
            if (goldIconSprite != null)
            {
                itemIcon.sprite = goldIconSprite;
            }
            itemIcon.color = new Color(1f, 0.84f, 0f);  // 금색
            itemIcon.gameObject.SetActive(true);
        }

        // 이름
        if (itemNameText != null)
        {
            itemNameText.text = "골드";
            itemNameText.color = new Color(1f, 0.84f, 0f);  // 금색
        }

        // 개수
        SetAmountText(required, current);

        // 배경색
        SetBackgroundColor(current >= required);
    }

    /// <summary>
    /// 개수 텍스트 설정 (공통)
    /// </summary>
    private void SetAmountText(int required, int current)
    {
        if (amountText == null) return;

        bool hasEnough = current >= required;

        // "x100 (150/100)" 형식
        string amountString = $"x{required} ({current}/{required})";

        // 색상 (충분하면 초록, 부족하면 빨강)
        if (hasEnough)
        {
            amountText.text = $"<color=#00FF00>{amountString}</color>";
        }
        else
        {
            amountText.text = $"<color=#FF0000>{amountString}</color>";
        }
    }

    /// <summary>
    /// 배경색 설정 (공통)
    /// </summary>
    private void SetBackgroundColor(bool hasEnough)
    {
        if (backgroundImage == null) return;

        if (hasEnough)
        {
            // 충분: 약간 초록빛
            backgroundImage.color = new Color(0.2f, 0.3f, 0.2f, 0.5f);
        }
        else
        {
            // 부족: 약간 빨간빛
            backgroundImage.color = new Color(0.3f, 0.2f, 0.2f, 0.5f);
        }
    }
}