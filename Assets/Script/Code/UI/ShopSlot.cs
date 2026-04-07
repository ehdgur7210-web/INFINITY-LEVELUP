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

    // ★ 교환 패키지 모드 (null이면 일반 아이템 모드)
    [HideInInspector] public ShopPackage packageData;

    [Header("UI 참조")]
    public Image itemIconImage;         // 아이템 아이콘
    public Image backgroundImage;       // ⭐ 배경 (등급별 색상)
    public TextMeshProUGUI itemNameText;// 아이템 이름
    public TextMeshProUGUI priceText;   // 가격 텍스트
    public Button buyButton;            // 구매 버튼

    [Header("가격 아이콘 (선택)")]
    [Tooltip("가격 옆에 표시되는 화폐 아이콘 — 패키지면 다이아, 일반이면 골드로 자동 전환")]
    public Image priceIconImage;
    [Tooltip("골드 스프라이트 (일반 아이템 구매용)")]
    public Sprite goldIconSprite;
    [Tooltip("다이아 스프라이트 (패키지 구매용)")]
    public Sprite gemIconSprite;

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
    /// 슬롯 설정 (일반 아이템)
    /// </summary>
    public void SetupSlot(ItemData item, int price)
    {
        itemData = item;
        packageData = null;
        itemPrice = price;
        UpdateSlotUI();
    }

    /// <summary>
    /// 슬롯 설정 (교환 패키지 — 젬으로 동료티켓/골드 등 구매)
    /// </summary>
    public void SetupPackage(ShopPackage pkg)
    {
        packageData = pkg;
        itemData = null;
        itemPrice = pkg != null ? pkg.gemCost : 0;
        UpdateSlotUI();
    }

    /// <summary>
    /// 슬롯 UI 업데이트
    /// </summary>
    void UpdateSlotUI()
    {
        // ★ 패키지 모드 (젬 교환)
        if (packageData != null)
        {
            if (itemIconImage != null)
            {
                itemIconImage.sprite = packageData.icon;
                itemIconImage.color = Color.white;
            }

            // 패키지 배경: 보라색
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.45f, 0.2f, 0.85f, 0.55f);
                backgroundImage.gameObject.SetActive(true);
            }

            if (itemNameText != null)
                itemNameText.text = packageData.packageName;

            // ★ 가격 텍스트는 숫자만 (아이콘은 별도 Image)
            if (priceText != null)
                priceText.text = $"{packageData.gemCost}";

            // ★ 가격 아이콘 → 다이아 스프라이트로 교체
            if (priceIconImage != null && gemIconSprite != null)
            {
                priceIconImage.sprite = gemIconSprite;
                priceIconImage.gameObject.SetActive(true);
            }
            return;
        }

        // 일반 아이템 모드
        if (itemData != null)
        {
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
                priceText.text = $"{itemPrice}";
            }

            // ★ 가격 아이콘 → 골드 스프라이트로 교체
            if (priceIconImage != null && goldIconSprite != null)
            {
                priceIconImage.sprite = goldIconSprite;
                priceIconImage.gameObject.SetActive(true);
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
        SoundManager.Instance?.PlayButtonClick();

        // ★ 패키지 모드
        if (packageData != null)
        {
            string msg = $"{packageData.packageName}\n{packageData.gemCost}다이아로구매하시겠습니까?";
            UIManager.Instance?.ShowConfirmDialog(
                msg,
                onConfirm: () => ShopManager.Instance?.BuyPackage(packageData),
                onCancel: null
            );
            return;
        }

        if (itemData == null) return;

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