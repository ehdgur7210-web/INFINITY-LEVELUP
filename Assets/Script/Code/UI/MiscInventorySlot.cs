using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 기타 인벤토리 슬롯 — 아이콘 클릭 시 아이템 타입에 따라 패널 분기
///
/// 분기 로직:
///   1. SelectionTicket (ItemType 또는 이름에 "선택권" 포함)
///      → SelectionPanel 오픈 (Legendary 이상 장비 선택)
///   2. Consumable
///      → ItemDetailPanel 오픈 (사용 버튼 활성화)
///   3. 그 외 (Material 등)
///      → ItemDetailPanel 오픈 (사용 버튼 비활성화)
///
/// [프리팹 Hierarchy]
///   MiscInventorySlot (MiscInventorySlot.cs)
///   ├── Background (Image) — 카테고리별 배경색
///   ├── ItemIcon (Image + Button) — 클릭 시 패널 분기
///   ├── ItemName (TextMeshProUGUI) — 아이템 이름
///   └── CountText (TextMeshProUGUI) — "x수량"
/// </summary>
public class MiscInventorySlot : MonoBehaviour, IPointerClickHandler
{
    [Header("슬롯 UI")]
    public Image itemIconImage;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI countText;
    public Image backgroundImage;

    [Header("아이템 정보 (런타임)")]
    [HideInInspector] public ItemData itemData;
    [HideInInspector] public int itemCount;
    [HideInInspector] public int slotIndex = -1;

    // ═══════════════════════════════════════════════════════════════
    //  자동 바인딩 (Inspector 미연결 시 자식에서 자동 탐색)
    // ═══════════════════════════════════════════════════════════════

    void Awake()
    {
        AutoBindComponents();
    }

    private void AutoBindComponents()
    {
        if (itemIconImage == null)
        {
            Transform iconTr = transform.Find("ItemIcon") ?? transform.Find("Icon") ?? transform.Find("Image");
            if (iconTr != null)
                itemIconImage = iconTr.GetComponent<Image>();
            if (itemIconImage == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.Contains("Background") || child.name.Contains("BG")) continue;
                    var img = child.GetComponent<Image>();
                    if (img != null) { itemIconImage = img; break; }
                }
            }
        }

        if (itemNameText == null)
        {
            Transform nameTr = transform.Find("ItemName") ?? transform.Find("Name") ?? transform.Find("Text");
            if (nameTr != null)
                itemNameText = nameTr.GetComponent<TextMeshProUGUI>();
        }

        if (countText == null)
        {
            Transform countTr = transform.Find("CountText") ?? transform.Find("Count") ?? transform.Find("수량");
            if (countTr != null)
                countText = countTr.GetComponent<TextMeshProUGUI>();
        }

        if (itemNameText == null || countText == null)
        {
            var allTmp = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var tmp in allTmp)
            {
                if (tmp == itemNameText || tmp == countText) continue;
                if (itemNameText == null) { itemNameText = tmp; continue; }
                if (countText == null) { countText = tmp; break; }
            }
        }

        if (backgroundImage == null)
        {
            Transform bgTr = transform.Find("Background") ?? transform.Find("BG") ?? transform.Find("Bg");
            if (bgTr != null)
                backgroundImage = bgTr.GetComponent<Image>();
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  슬롯 설정
    // ═══════════════════════════════════════════════════════════════

    /// <summary>아이템 데이터로 슬롯 세팅</summary>
    public void Setup(ItemData item, int count, int index = -1)
    {
        AutoBindComponents();

        itemData = item;
        itemCount = count;
        slotIndex = index;

        UpdateSlotUI();
    }

    /// <summary>슬롯 초기화 (빈 슬롯)</summary>
    public void ClearSlot()
    {
        itemData = null;
        itemCount = 0;
        slotIndex = -1;
        UpdateSlotUI();
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI 갱신
    // ═══════════════════════════════════════════════════════════════

    private void UpdateSlotUI()
    {
        if (itemData != null)
        {
            if (itemIconImage != null)
            {
                itemIconImage.sprite = itemData.itemIcon;
                itemIconImage.color = Color.white;
                itemIconImage.gameObject.SetActive(true);
            }

            if (itemNameText != null)
            {
                itemNameText.text = itemData.itemName;
                itemNameText.color = itemData.GetRarityColor();
                itemNameText.gameObject.SetActive(true);
            }

            if (countText != null)
            {
                countText.text = $"x{itemCount}";
                countText.gameObject.SetActive(true);
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = GetCategoryBgColor(itemData.itemType);
                backgroundImage.gameObject.SetActive(true);
            }
        }
        else
        {
            if (itemIconImage != null)
            {
                itemIconImage.sprite = null;
                itemIconImage.color = new Color(1f, 1f, 1f, 0f);
                itemIconImage.gameObject.SetActive(false);
            }
            if (itemNameText != null) itemNameText.gameObject.SetActive(false);
            if (countText != null) countText.gameObject.SetActive(false);

            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                backgroundImage.gameObject.SetActive(true);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  클릭 → 타입 분기
    // ═══════════════════════════════════════════════════════════════

    public void OnPointerClick(PointerEventData eventData)
    {
        if (itemData == null || itemCount <= 0) return;

        SoundManager.Instance?.PlayButtonClick();

        // ── 1. 선택권 분기 ──
        if (IsSelectionTicket(itemData))
        {
            OpenSelectionPanel();
            return;
        }

        // ── 2. 그 외 → ItemDetailPanel ──
        OpenItemDetailPanel();
    }

    /// <summary>선택권 아이템인지 판별</summary>
    private bool IsSelectionTicket(ItemData item)
    {
        // ItemType 기반 (SelectionTicket enum이 있으면 사용)
        if (item.itemType.ToString() == "SelectionTicket")
            return true;

        // 이름 기반 폴백
        if (!string.IsNullOrEmpty(item.itemName) && item.itemName.Contains("선택권"))
            return true;

        return false;
    }

    /// <summary>선택권 → SelectionPanel 오픈</summary>
    private void OpenSelectionPanel()
    {
        if (SelectionPanel.Instance != null)
        {
            SelectionPanel.Instance.Open(itemData, slotIndex);
        }
        else
        {
            Debug.LogWarning("[MiscInventorySlot] SelectionPanel.Instance가 null!");
            UIManager.Instance?.ShowMessage("선택 패널을 찾을 수 없습니다", Color.red);
        }
    }

    /// <summary>일반/소비 아이템 → ItemDetailPanel 오픈</summary>
    private void OpenItemDetailPanel()
    {
        if (ItemDetailPanel.Instance != null)
        {
            bool isConsumable = itemData.isConsumable &&
                                itemData.itemType == ItemType.Consumable;
            ItemDetailPanel.Instance.Open(itemData, itemCount, slotIndex, isConsumable);
        }
        else
        {
            // 폴백: 기존 동작 (메시지 표시)
            UIManager.Instance?.ShowMessage(
                $"{itemData.itemName}\n{itemData.itemDescription}",
                itemData.GetRarityColor());
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  카테고리별 배경색
    // ═══════════════════════════════════════════════════════════════

    private Color GetCategoryBgColor(ItemType type)
    {
        switch (type)
        {
            case ItemType.Consumable:           return new Color(0.15f, 0.35f, 0.15f, 0.9f);
            case ItemType.Material:             return new Color(0.30f, 0.25f, 0.15f, 0.9f);
            case ItemType.FarmVegetable:        return new Color(0.10f, 0.35f, 0.10f, 0.9f);
            case ItemType.FarmFruit:            return new Color(0.40f, 0.15f, 0.25f, 0.9f);
            case ItemType.GachaTicket_5Star:
            case ItemType.GachaTicket_3to5Star: return new Color(0.45f, 0.30f, 0.05f, 0.9f);
            case ItemType.OfflineReward_2h:
            case ItemType.OfflineReward_4h:
            case ItemType.OfflineReward_8h:
            case ItemType.OfflineReward_12h:    return new Color(0.15f, 0.20f, 0.40f, 0.9f);
            default:                            return new Color(0.25f, 0.25f, 0.25f, 0.9f);
        }
    }
}
