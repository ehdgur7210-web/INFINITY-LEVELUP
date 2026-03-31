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
                return new Color(0f, 0f, 0f, 1f);             // 투명
        }
    }

    /// <summary>
    /// 구매 버튼 클릭 이벤트 — 확인 다이얼로그 표시
    /// </summary>
    public void OnBuyButtonClicked()
    {
        if (itemData == null) return;
        SoundManager.Instance?.PlayButtonClick();

        // ★ 구매 확인 다이얼로그 — 아이템 이름 + 가격 표시
        string rarityName = GetRarityDisplayName(itemData.rarity);
        string message = $"<color={GetRarityHexColor(itemData.rarity)}>[{rarityName}] {itemData.itemName}</color>\n\n" +
                         $"{itemPrice:N0} G 로 구매하시겠습니까?";

        UIManager.Instance?.ShowConfirmDialog(
            message,
            onConfirm: () =>
            {
                if (ShopManager.Instance != null)
                    ShopManager.Instance.BuyItem(itemData, itemPrice);
            },
            onCancel: () =>
            {
                // 취소 — 아무것도 안 함
                Debug.Log($"[ShopSlot] 구매 취소: {itemData.itemName}");
            }
        );
    }

    private string GetRarityDisplayName(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common    => "일반",
            ItemRarity.Uncommon  => "고급",
            ItemRarity.Rare      => "희귀",
            ItemRarity.Epic      => "영웅",
            ItemRarity.Legendary => "전설",
            _                    => "일반"
        };
    }

    private string GetRarityHexColor(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common    => "#CCCCCC",
            ItemRarity.Uncommon  => "#00FF00",
            ItemRarity.Rare      => "#4488FF",
            ItemRarity.Epic      => "#BB44FF",
            ItemRarity.Legendary => "#FF8800",
            _                    => "#FFFFFF"
        };
    }
}