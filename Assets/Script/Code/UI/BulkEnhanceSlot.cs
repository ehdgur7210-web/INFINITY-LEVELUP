using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 일괄 강화 탭의 장비 인스턴스 슬롯
/// BulkSlotPrefab에 붙임
/// </summary>
public class BulkEnhanceSlot : MonoBehaviour
{
    [Header("UI 참조 (자동 바인딩 가능)")]
    public Image background;                // 등급 색상
    public Image itemIcon;                  // 장비 아이콘
    public TextMeshProUGUI enhanceText;     // "+5"
    public TextMeshProUGUI itemNameText;    // "갑옷"
    public GameObject equippedBadge;        // "장착중" 표시
    public GameObject selectedFrame;        // 선택 테두리 (현재 미사용)

    private EquipmentData _equipment;
    private InventoryManager.EquipInstance _instance;

    void Awake()
    {
        AutoBind();
    }

    private void AutoBind()
    {
        // 자식 이름으로 자동 바인딩
        if (itemIcon == null)
        {
            var t = transform.Find("아이콘") ?? transform.Find("ItemIcon") ?? transform.Find("Icon");
            if (t != null) itemIcon = t.GetComponent<Image>();
        }
        if (enhanceText == null)
        {
            var t = transform.Find("강화수치") ?? transform.Find("EnhanceText") ?? transform.Find("Enhance");
            if (t != null) enhanceText = t.GetComponent<TextMeshProUGUI>();
        }
        if (itemNameText == null)
        {
            var t = transform.Find("이름") ?? transform.Find("ItemNameText") ?? transform.Find("Name");
            if (t != null) itemNameText = t.GetComponent<TextMeshProUGUI>();
        }
        if (equippedBadge == null)
        {
            var t = transform.Find("장착") ?? transform.Find("EquippedBadge");
            if (t != null) equippedBadge = t.gameObject;
        }
        if (background == null) background = GetComponent<Image>();
    }

    /// <summary>
    /// 슬롯에 인스턴스 데이터 표시
    /// </summary>
    public void Setup(EquipmentData equipment, InventoryManager.EquipInstance instance)
    {
        _equipment = equipment;
        _instance = instance;

        if (equipment == null || instance == null)
        {
            Debug.LogWarning("[BulkEnhanceSlot] Setup with null data");
            return;
        }

        // 아이콘
        if (itemIcon != null)
        {
            itemIcon.sprite = equipment.itemIcon;
            itemIcon.color = Color.white;
            itemIcon.enabled = true;
        }

        // 강화 수치
        if (enhanceText != null)
        {
            enhanceText.text = instance.enhanceLevel > 0 ? $"+{instance.enhanceLevel}" : "";
            enhanceText.color = GetEnhanceColor(instance.enhanceLevel);
        }

        // 아이템 이름
        if (itemNameText != null)
            itemNameText.text = equipment.itemName;

        // 등급 배경색
        if (background != null)
            background.color = GetRarityColor(equipment.rarity);

        // 장착 뱃지
        if (equippedBadge != null)
            equippedBadge.SetActive(instance.isEquipped);

        // 선택 프레임 초기화
        if (selectedFrame != null)
            selectedFrame.SetActive(false);
    }

    /// <summary>현재 슬롯의 인스턴스 ID</summary>
    public string GetInstanceId() => _instance?.instanceId;

    /// <summary>현재 슬롯의 인스턴스</summary>
    public InventoryManager.EquipInstance GetInstance() => _instance;

    /// <summary>현재 슬롯의 장비 데이터</summary>
    public EquipmentData GetEquipment() => _equipment;

    private Color GetRarityColor(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common    => new Color(0.6f, 0.6f, 0.6f, 0.5f),
            ItemRarity.Uncommon  => new Color(0.2f, 0.9f, 0.2f, 0.5f),
            ItemRarity.Rare      => new Color(0.2f, 0.5f, 1f, 0.5f),
            ItemRarity.Epic      => new Color(0.7f, 0.2f, 1f, 0.6f),
            ItemRarity.Legendary => new Color(1f, 0.6f, 0f, 0.7f),
            _                    => Color.white
        };
    }

    private Color GetEnhanceColor(int level)
    {
        if (level >= 20) return new Color(1f, 0.2f, 0.2f, 1f);  // 최강
        if (level >= 15) return new Color(1f, 0.5f, 0f, 1f);    // 주황
        if (level >= 10) return new Color(1f, 0.8f, 0.2f, 1f);  // 노랑
        if (level >= 5)  return new Color(0.4f, 0.9f, 1f, 1f);  // 하늘
        return Color.white;
    }
}
