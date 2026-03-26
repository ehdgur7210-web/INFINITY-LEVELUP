using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 장비 패널 슬롯 (캐릭터 장비 화면의 6슬롯)
///
/// 동작:
///   - 드래그 드롭: 인벤 아이템을 이 슬롯에 드롭 → 장착
///   - 클릭: 장착된 아이템 → 강화 패널 열기
///   - 해제: 강화 패널의 해제 버튼으로
///
/// (구 EquipmentSlot → EquipPanelSlot 리네임)
/// </summary>
public class EquipPanelSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("슬롯 설정")]
    public EquipmentType slotType;

    [Header("UI 요소")]
    public Image itemIconImage;
    public Image backgroundImage;
    public TextMeshProUGUI enhanceLevelText;
    [Tooltip("강화 수치 텍스트 (+N, 색상 코딩: +15=빨강/+10=노랑/+5=초록/미만=흰색)")]
    public TextMeshProUGUI enhanceText;
    [Tooltip("장비 이름 텍스트")]
    public TextMeshProUGUI itemNameText;

    public EquipmentData currentEquipment;
    public int currentEnhanceLevel = 0;

    void Awake()
    {
        // EquipmentManager에 자가 등록 (비활성 상태에서도 접근 가능하도록)
        EquipmentManager.Instance?.RegisterPanelSlot(this);
    }

    void Start()
    {
        // Awake 시점에 EquipmentManager.Instance가 null일 수 있으므로 재시도
        EquipmentManager.Instance?.RegisterPanelSlot(this);

        if (itemIconImage != null)
            itemIconImage.gameObject.SetActive(true);
        ClearSlot();
    }

    void OnEnable()
    {
        // 이벤트 구독: 장비 변경 시 자동 갱신
        EquipmentManager.OnEquipmentChanged += OnEquipmentChangedHandler;

        // 패널 활성화 시 현재 장착 상태 즉시 반영
        RefreshFromManager();
    }

    void OnDisable()
    {
        EquipmentManager.OnEquipmentChanged -= OnEquipmentChangedHandler;
    }

    /// <summary>OnEquipmentChanged 이벤트 핸들러</summary>
    private void OnEquipmentChangedHandler(EquipmentType type, EquipmentData eq, int enhLevel)
    {
        if (type != slotType) return;

        if (eq != null)
            EquipItem(eq, enhLevel);
        else
            UnequipItem();
    }

    /// <summary>패널 활성화 시 EquipmentManager에서 현재 장착 데이터를 가져와 표시</summary>
    private void RefreshFromManager()
    {
        if (EquipmentManager.Instance == null) return;

        EquipmentData equipped = EquipmentManager.Instance.GetEquippedItem(slotType);
        if (equipped != null)
        {
            int enhLevel = EquipmentManager.Instance.GetEnhanceLevel(slotType);
            EquipItem(equipped, enhLevel);
        }
        else
        {
            UnequipItem();
        }
    }

    // ─────────────────────────────────────────
    // 드롭: 인벤 아이템 → 이 슬롯에 드롭 = 장착
    // ─────────────────────────────────────────
    public void OnDrop(PointerEventData eventData)
    {
        GameObject dragged = eventData.pointerDrag;
        if (dragged == null) return;

        // 기존 InventorySlot 드래그 호환
        InventorySlot inventorySlot = dragged.GetComponent<InventorySlot>();
        if (inventorySlot != null && inventorySlot.itemData != null)
        {
            TryEquipFromInventorySlot(inventorySlot.itemData, inventorySlot.enhanceLevel);
            return;
        }

        // 새 EquipmentSlot(인벤 장비 슬롯) 드래그 호환
        EquipmentSlot equipSlot = dragged.GetComponent<EquipmentSlot>();
        if (equipSlot != null && equipSlot.itemData != null)
        {
            TryEquipFromInventorySlot(equipSlot.itemData, equipSlot.enhanceLevel);
            return;
        }
    }

    private void TryEquipFromInventorySlot(ItemData itemData, int enhanceLevel)
    {
        if (itemData.itemType != ItemType.Equipment &&
            itemData.itemType != ItemType.Weapon &&
            itemData.itemType != ItemType.Armor &&
            itemData.itemType != ItemType.Accessory)
        {
            UIManager.Instance?.ShowMessage("장비만 착용 가능!", Color.red);
            return;
        }

        EquipmentData equipment = itemData as EquipmentData;
        if (equipment == null) return;

        if (slotType != equipment.equipmentType)
        {
            UIManager.Instance?.ShowMessage($"{equipment.equipmentType} 슬롯에만 착용 가능!", Color.red);
            return;
        }

        if (EquipmentManager.Instance != null)
        {
            InventoryManager.Instance?.RemoveItem(equipment, 1);
            EquipmentManager.Instance.EquipItem(equipment, enhanceLevel);
            SoundManager.Instance?.PlayEquip();
        }
    }

    // ─────────────────────────────────────────
    // 클릭: 장착된 아이템 → 강화 패널 열기
    // ─────────────────────────────────────────
    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentEquipment == null) return;
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

        // 기존 enhanceLevelText
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

        // 새 enhanceText (색상 코딩: +15=빨강/+10=노랑/+5=초록/미만=흰색)
        if (enhanceText != null)
        {
            if (enhanceLevel > 0)
            {
                enhanceText.text = $"+{enhanceLevel}";
                enhanceText.color = GetEnhanceColor(enhanceLevel);
                enhanceText.gameObject.SetActive(true);
            }
            else
            {
                enhanceText.gameObject.SetActive(false);
            }
        }

        // 장비 이름 표시
        if (itemNameText != null)
        {
            itemNameText.text = equipment.itemName;
            itemNameText.color = GetRarityColor(equipment.rarity);
            itemNameText.gameObject.SetActive(true);
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

        if (enhanceText != null)
            enhanceText.gameObject.SetActive(false);

        if (itemNameText != null)
            itemNameText.gameObject.SetActive(false);

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

        // 새 enhanceText 동기화
        if (enhanceText != null)
        {
            if (newLevel > 0)
            {
                enhanceText.text = $"+{newLevel}";
                enhanceText.color = GetEnhanceColor(newLevel);
                enhanceText.gameObject.SetActive(true);
            }
            else
            {
                enhanceText.gameObject.SetActive(false);
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
