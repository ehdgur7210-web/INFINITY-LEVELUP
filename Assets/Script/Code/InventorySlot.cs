using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 인벤토리 슬롯
///
/// ✅ 동작:
///   - 클릭: 장비 아이템 → 바로 장착 (인벤에서 제거)
///   - 드래그: 장비 슬롯에 드롭 → 장착 / 상점 드롭 → 판매
///   - 장착된 아이템은 인벤에 없음 (별개 관리)
/// </summary>
public class InventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("슬롯 UI")]
    public Image itemIconImage;
    public TextMeshProUGUI countText;
    public Image backgroundImage;
    public TextMeshProUGUI enhanceLevelText;

    [Header("아이템 정보")]
    public ItemData itemData;
    public int itemCount;
    public int enhanceLevel = 0;

    private GameObject dragIcon;
    private Canvas canvas;
    private RectTransform dragTransform;

    void Awake()
    {
        // BackGround가 슬롯 크기를 넘치지 않도록 강제 stretch
        if (backgroundImage != null)
        {
            RectTransform bgRect = backgroundImage.GetComponent<RectTransform>();
            if (bgRect != null)
            {
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
                bgRect.sizeDelta = Vector2.zero;
                bgRect.anchoredPosition = Vector2.zero;
            }
        }
    }

    void Start()
    {
        ClearSlot();
        canvas = GetComponentInParent<Canvas>();
    }

    // ─────────────────────────────────────────
    // 슬롯 데이터 설정
    // ─────────────────────────────────────────
    public void AddItem(ItemData item, int count = 1, int enhance = 0)
    {
        itemData = item;
        itemCount += count;
        enhanceLevel = enhance;
        UpdateSlotUI();
    }

    public void RemoveItem(int count = 1)
    {
        itemCount -= count;
        if (itemCount <= 0) ClearSlot();
        else UpdateSlotUI();
    }

    public void ClearSlot()
    {
        itemData = null;
        itemCount = 0;
        enhanceLevel = 0;
        UpdateSlotUI();
    }

    public void UpdateEnhanceLevel(int newLevel)
    {
        enhanceLevel = newLevel;
        UpdateSlotUI();
    }

    // ─────────────────────────────────────────
    // UI 갱신
    // ─────────────────────────────────────────
    private void UpdateSlotUI()
    {
        if (itemData != null)
        {
            if (itemIconImage != null)
            {
                itemIconImage.sprite = itemData.itemIcon;
                itemIconImage.gameObject.SetActive(true);
                itemIconImage.color = GetEnhanceColor(enhanceLevel);
            }

            // 등급 배경 (sprite는 건드리지 않고 color tint만)
            if (backgroundImage != null)
            {
                backgroundImage.color = GetRarityColor(itemData.rarity);
                backgroundImage.gameObject.SetActive(true);
            }

            if (countText != null)
            {
                if (itemCount > 1) { countText.text = itemCount.ToString(); countText.gameObject.SetActive(true); }
                else countText.gameObject.SetActive(false);
            }

            if (enhanceLevelText != null)
            {
                if (enhanceLevel > 0)
                {
                    enhanceLevelText.text = $"+{enhanceLevel}";
                    enhanceLevelText.gameObject.SetActive(true);
                    enhanceLevelText.color = GetEnhanceColor(enhanceLevel);
                }
                else enhanceLevelText.gameObject.SetActive(false);
            }
        }
        else
        {
            // 빈 슬롯: 아이콘 숨기고 배경 원본 표시
            if (itemIconImage != null)
            {
                itemIconImage.sprite = null;
                itemIconImage.color = new Color(1f, 1f, 1f, 0f);
                itemIconImage.gameObject.SetActive(false);
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = Color.white; // 원본 스프라이트 그대로
                backgroundImage.gameObject.SetActive(true);
            }

            if (countText != null) countText.gameObject.SetActive(false);
            if (enhanceLevelText != null) enhanceLevelText.gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────
    // 클릭: 장비 아이템이면 바로 장착
    // ─────────────────────────────────────────
    public void OnPointerClick(PointerEventData eventData)
    {
        if (itemData == null) return;

        if (itemData is EquipmentData eq && EquipmentManager.Instance != null)
        {
            // 인벤에서 제거 후 장착
            InventoryManager.Instance?.RemoveItem(eq, 1);
            EquipmentManager.Instance.EquipItem(eq, enhanceLevel);
        }
    }

    // ─────────────────────────────────────────
    // 드래그 앤 드롭
    // ─────────────────────────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (itemData == null) return;

        dragIcon = new GameObject("DragIcon");
        dragIcon.transform.SetParent(canvas.transform);
        dragIcon.transform.SetAsLastSibling();

        Image iconImage = dragIcon.AddComponent<Image>();
        iconImage.sprite = itemData.itemIcon;
        iconImage.raycastTarget = false;
        iconImage.color = GetEnhanceColor(enhanceLevel) * new Color(1, 1, 1, 0.7f);

        dragTransform = dragIcon.GetComponent<RectTransform>();
        dragTransform.sizeDelta = new Vector2(60, 60);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIcon != null) dragTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIcon != null) { Destroy(dragIcon); dragIcon = null; }

        GameObject dropTarget = eventData.pointerEnter;
        if (dropTarget == null) return;

        // 상점 드롭 → 판매
        ShopManager shop = dropTarget.GetComponent<ShopManager>()
                        ?? dropTarget.GetComponentInParent<ShopManager>();
        if (shop != null || dropTarget.name.Contains("Shop") || dropTarget.CompareTag("Shop"))
        {
            TrySellItem();
        }

        // EquipmentSlot 드롭은 EquipmentSlot.OnDrop()에서 처리
    }

    private void TrySellItem()
    {
        if (itemData == null) return;

        int sellPrice = Mathf.RoundToInt(itemData.buyPrice * 0.5f);
        GameManager.Instance?.AddGold(sellPrice);
        UIManager.Instance?.ShowMessage($"{itemData.itemName} 판매! +{sellPrice}G", Color.green);
        InventoryManager.Instance?.RemoveItem(itemData, 1);
    }

    // ─────────────────────────────────────────
    // 색상 유틸
    // ─────────────────────────────────────────
    private Color GetEnhanceColor(int level)
    {
        if (level >= 15) return new Color(1f, 0f, 0f, 1f);
        if (level >= 10) return new Color(1f, 1f, 0f, 1f);
        if (level >= 5) return new Color(0f, 1f, 0f, 1f);
        return Color.white;
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
            default: return Color.white;
        }
    }
}