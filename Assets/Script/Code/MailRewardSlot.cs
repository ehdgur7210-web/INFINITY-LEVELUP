using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MailRewardSlot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI amountText;

    public void Setup(MailReward reward)
    {
        if (reward == null) return;

        // 보상 타입에 따라 표시
        switch (reward.rewardType)
        {
            case MailReward.RewardType.Item:
                if (reward.itemData != null)
                {
                    if (iconImage != null) iconImage.sprite = reward.itemData.itemIcon;
                    if (nameText != null) nameText.text = reward.itemData.itemName;
                    if (backgroundImage != null) backgroundImage.color = GetRarityColor(reward.itemData.rarity);
                }
                break;

            case MailReward.RewardType.Gold:
                if (nameText != null) nameText.text = "골드";
                if (backgroundImage != null) backgroundImage.color = new Color(1f, 0.84f, 0f, 0.5f);
                break;

            case MailReward.RewardType.Gem:
                if (nameText != null) nameText.text = "보석";
                if (backgroundImage != null) backgroundImage.color = new Color(0f, 0.8f, 1f, 0.5f);
                break;

            case MailReward.RewardType.Ticket:
                if (nameText != null) nameText.text = "티켓";
                if (backgroundImage != null) backgroundImage.color = new Color(1f, 0.5f, 0f, 0.5f);
                break;

            case MailReward.RewardType.Crystal:
                if (nameText != null) nameText.text = "크리스탈";
                break;

            case MailReward.RewardType.Essence:
                if (nameText != null) nameText.text = "에센스";
                break;

            case MailReward.RewardType.Fragment:
                if (nameText != null) nameText.text = "파편";
                break;
        }

        // 개수
        if (amountText != null)
        {
            amountText.text = reward.amount > 1 ? $"x{reward.amount}" : "";
        }
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return new Color(0.5f, 0.5f, 0.5f, 0.5f);
            case ItemRarity.Uncommon: return new Color(0f, 1f, 0f, 0.5f);
            case ItemRarity.Rare: return new Color(0f, 0.5f, 1f, 0.6f);
            case ItemRarity.Epic: return new Color(0.6f, 0f, 1f, 0.7f);
            case ItemRarity.Legendary: return new Color(1f, 0.5f, 0f, 0.8f);
            default: return Color.white;
        }
    }
}