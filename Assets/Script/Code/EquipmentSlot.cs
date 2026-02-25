using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 장비 슬롯
///
/// ✅ 동작:
///   - 드래그 드롭: 인벤 아이템을 이 슬롯에 드롭 → 장착 (인벤에서 제거)
///   - 클릭: 장착된 아이템 → 강화 패널 열기
///   - 해제: 강화 패널의 해제 버튼으로
/// </summary>
public class EquipmentSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("슬롯 설정")]
    public EquipmentType slotType;

    [Header("UI 요소")]
    public Image itemIconImage;
    public Image backgroundImage;
    public TextMeshProUGUI enhanceLevelText;

    public EquipmentData currentEquipment;
    public int currentEnhanceLevel = 0;

    void Start()
    {
        if (itemIconImage != null)
            itemIconImage.gameObject.SetActive(true);
        ClearSlot();
    }

    // ─────────────────────────────────────────
    // 드롭: 인벤 아이템 → 이 슬롯에 드롭 = 장착
    // ─────────────────────────────────────────
    public void OnDrop(PointerEventData eventData)
    {
        GameObject dragged = eventData.pointerDrag;
        if (dragged == null) return;

        InventorySlot inventorySlot = dragged.GetComponent<InventorySlot>();
        if (inventorySlot == null || inventorySlot.itemData == null) return;

        if (inventorySlot.itemData.itemType != ItemType.Equipment)
        {
            UIManager.Instance?.ShowMessage("장비만 착용 가능!", Color.red);
            return;
        }

        EquipmentData equipment = inventorySlot.itemData as EquipmentData;
        if (equipment == null) return;

        if (slotType != equipment.equipmentType)
        {
            UIManager.Instance?.ShowMessage($"{equipment.equipmentType} 슬롯에만 착용 가능!", Color.red);
            return;
        }

        // ✅ 장착: 인벤에서 먼저 제거 후 장착
        if (EquipmentManager.Instance != null)
        {
            InventoryManager.Instance?.RemoveItem(equipment, 1);
            EquipmentManager.Instance.EquipItem(equipment, inventorySlot.enhanceLevel);
            // ★ 장비 장착 효과음
            SoundManager.Instance?.PlayEquip();
        }
    }

    // ─────────────────────────────────────────
    // 클릭: 장착된 아이템 → 강화 패널 열기
    // ─────────────────────────────────────────
    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentEquipment == null) return;
        // ★ 장비 슬롯 클릭 효과음
        SoundManager.Instance?.PlayButtonClick();

        if (EnhancementSystem.Instance != null)
        {
            EnhancementSystem.Instance.SelectEquippedItemForEnhancement(slotType);
        }
    }

    // ─────────────────────────────────────────
    // UI 갱신 (EquipmentManager에서 호출)
    // ─────────────────────────────────────────
    public void EquipItem(EquipmentData equipment, int enhanceLevel = 0)
    {
        currentEquipment = equipment;
        currentEnhanceLevel = enhanceLevel;

        if (itemIconImage != null)
        {
            itemIconImage.gameObject.SetActive(true);
            itemIconImage.sprite = equipment.itemIcon;
            itemIconImage.enabled = true;
            itemIconImage.color = GetEnhanceColor(enhanceLevel);
        }

        if (enhanceLevelText != null)
        {
            if (enhanceLevel > 0)
            {
                enhanceLevelText.text = $"+{enhanceLevel}";
                enhanceLevelText.enabled = true;
                enhanceLevelText.color = GetEnhanceColor(enhanceLevel);
            }
            else
            {
                enhanceLevelText.enabled = false;
            }
        }

        SetBackgroundColorByRarity(equipment.rarity);
    }

    public void UnequipItem()
    {
        currentEquipment = null;
        currentEnhanceLevel = 0;

        if (itemIconImage != null)
        {
            itemIconImage.sprite = null;
            itemIconImage.enabled = false;
            itemIconImage.color = Color.white;
        }

        if (enhanceLevelText != null)
            enhanceLevelText.enabled = false;

        if (backgroundImage != null)
            backgroundImage.color = Color.white;
    }

    public void ClearSlot() => UnequipItem();

    public EquipmentData GetEquippedItem() => currentEquipment;

    public void UpdateEnhanceLevel(int newLevel)
    {
        currentEnhanceLevel = newLevel;

        if (itemIconImage != null)
            itemIconImage.color = GetEnhanceColor(newLevel);

        if (enhanceLevelText != null)
        {
            if (newLevel > 0)
            {
                enhanceLevelText.text = $"+{newLevel}";
                enhanceLevelText.enabled = true;
                enhanceLevelText.color = GetEnhanceColor(newLevel);
            }
            else
            {
                enhanceLevelText.enabled = false;
            }
        }
    }

    private void SetBackgroundColorByRarity(ItemRarity rarity)
    {
        if (backgroundImage == null) return;
        backgroundImage.color = GetRarityColor(rarity);
        backgroundImage.gameObject.SetActive(true);
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return new Color(0.5f, 0.5f, 0.5f, 0.3f);
            case ItemRarity.Uncommon: return new Color(0f, 1f, 0f, 0.4f);
            case ItemRarity.Rare: return new Color(0f, 0.5f, 1f, 0.5f);
            case ItemRarity.Epic: return new Color(0.6f, 0f, 1f, 0.6f);
            case ItemRarity.Legendary: return new Color(1f, 0.5f, 0f, 0.7f);
            default: return new Color(0f, 0f, 0f, 1f);
        }
    }

    private Color GetEnhanceColor(int level)
    {
        if (level >= 15) return new Color(0.8f, 0f, 0f, 1f);
        if (level >= 10) return new Color(0.9f, 0.9f, 0f, 1f);
        if (level >= 5) return new Color(0f, 0.8f, 0f, 1f);
        return Color.white;
    }
}