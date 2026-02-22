using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 상점 아이템 슬롯 - 등급별 배경색 자동 적용
/// </summary>
public class ShopSlot : MonoBehaviour
{
    [Header("슬롯 정보")]
    public ItemData itemData;           // 판매할 아이템 데이터
    public int itemPrice;               // 판매 가격

    [Header("UI 참조")]
    public Image itemIconImage;         // 아이템 아이콘
    public Image backgroundImage;       // ⭐ 배경 (등급별 색상)
    public TextMeshProUGUI itemNameText;// 아이템 이름
    public TextMeshProUGUI priceText;   // 가격 텍스트
    public Button buyButton;            // 구매 버튼

    void Start()
    {
        // 구매 버튼 이벤트 연결
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(OnBuyButtonClicked);
        }

        UpdateSlotUI();
    }

    /// <summary>
    /// 슬롯 설정
    /// </summary>
    public void SetupSlot(ItemData item, int price)
    {
        itemData = item;
        itemPrice = price;
        UpdateSlotUI();
    }

    /// <summary>
    /// 슬롯 UI 업데이트
    /// </summary>
    void UpdateSlotUI()
    {
        if (itemData != null)
        {
            // 아이템 아이콘
            if (itemIconImage != null)
            {
                itemIconImage.sprite = itemData.itemIcon;
                itemIconImage.color = Color.white;
            }

            // ⭐⭐⭐ 등급별 배경색 적용
            SetBackgroundColorByRarity(itemData.rarity);

            // 아이템 이름
            if (itemNameText != null)
            {
                itemNameText.text = itemData.itemName;
            }

            // 가격
            if (priceText != null)
            {
                priceText.text = $"{itemPrice} G";
            }
        }
    }

    /// <summary>
    /// ⭐⭐⭐ 등급별 배경색 설정
    /// </summary>
    private void SetBackgroundColorByRarity(ItemRarity rarity)
    {
        if (backgroundImage == null) return;

        Color bgColor = GetRarityColor(rarity);
        backgroundImage.color = bgColor;
        backgroundImage.gameObject.SetActive(true);
    }

    /// <summary>
    /// ⭐ 등급별 색상 반환
    /// </summary>
    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common:
                return new Color(0.5f, 0.5f, 0.5f, 0.3f);     // 회색

            case ItemRarity.Uncommon:
                return new Color(0f, 1f, 0f, 0.4f);           // 초록색

            case ItemRarity.Rare:
                return new Color(0f, 0.5f, 1f, 0.5f);         // 파란색

            case ItemRarity.Epic:
                return new Color(0.6f, 0f, 1f, 0.6f);         // 보라색

            case ItemRarity.Legendary:
                return new Color(1f, 0.5f, 0f, 0.7f);         // 주황색

            default:
                return new Color(1f, 1f, 1f, 0f);             // 투명
        }
    }

    /// <summary>
    /// 구매 버튼 클릭 이벤트
    /// </summary>
    public void OnBuyButtonClicked()
    {
        if (itemData == null) return;
        // ★ 구매 버튼 클릭 효과음
        SoundManager.Instance?.PlayButtonClick();

        // ShopManager를 통해 구매 처리
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.BuyItem(itemData, itemPrice);
        }
    }
}