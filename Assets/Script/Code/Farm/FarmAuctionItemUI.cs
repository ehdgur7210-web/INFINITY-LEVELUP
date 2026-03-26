using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI; 

public class FarmAuctionItemUI : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI qtyText;
    public TextMeshProUGUI currentBidText;
    public TextMeshProUGUI timeText;
    public Button buyoutBtn;

    private AuctionFarmCategory.FarmAuctionListing listing;
    private AuctionFarmCategory manager;

    public void Setup(AuctionFarmCategory.FarmAuctionListing l, AuctionFarmCategory mgr)
    {
        listing = l;
        manager = mgr;

        if (iconImage != null) iconImage.sprite = l.icon;
        if (nameText != null) nameText.text = l.itemName;
        if (qtyText != null) qtyText.text = $"x{l.quantity}";
        if (currentBidText != null) currentBidText.text = $"{l.currentBid:N0}G";
        if (timeText != null) timeText.text = FormatTime(l.remainingTime);

        if (buyoutBtn != null)
        {
            buyoutBtn.GetComponentInChildren<TextMeshProUGUI>()?.SetText(
                l.buyoutPrice > 0 ? $"즉시구매 {l.buyoutPrice:N0}G" : "즉시구매 없음");
            buyoutBtn.onClick.AddListener(OnBuyout);
            buyoutBtn.interactable = l.buyoutPrice > 0;
        }
    }

    private void Update()
    {
        if (listing == null) return;
        if (timeText != null)
            timeText.text = FormatTime(listing.remainingTime);
    }

    private void OnBuyout()
    {
        if (listing == null || listing.buyoutPrice <= 0) return;

        if (!GameManager.Instance.SpendGold(listing.buyoutPrice))
        {
            UIManager.Instance?.ShowMessage("골드가 부족합니다!", Color.red);
            return;
        }

        listing.isActive = false;
        FarmInventoryConnector.Instance?.AddFarmCrop(
            listing.itemName, listing.icon, listing.quantity, listing.itemType, listing.startingBid, listing.itemID);

        UIManager.Instance?.ShowMessage($"{listing.itemName} x{listing.quantity} 구매 완료!", Color.cyan);
        manager?.RefreshFarmAuctionList();
    }

    private string FormatTime(float seconds)
    {
        if (seconds <= 0) return "종료";
        int m = (int)(seconds / 60);
        int s = (int)(seconds % 60);
        return m > 0 ? $"{m}분 {s:D2}초" : $"{s}초";
    }
}