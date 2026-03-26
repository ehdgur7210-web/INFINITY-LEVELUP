using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 인벤토리 기타 전용 슬롯
///
/// 소비 아이템, 재료, 농장 작물, 가챠 티켓 등 비장비/비동료 아이템 표시.
/// 클릭 시 아이템 사용(소비) 또는 상세 정보 표시.
///
/// [프리팹 Hierarchy]
///   EtcSlot (Button + EtcSlot.cs)
///   ├── Background (Image) — 레어도별 배경색
///   ├── ItemIcon (Image) — 아이템 아이콘
///   ├── ItemName (TextMeshProUGUI) — 아이템 이름
///   └── CountText (TextMeshProUGUI) — "x수량"
///
/// [Inspector 연결]
///   itemIconImage → ItemIcon
///   itemNameText  → ItemName
///   countText     → CountText
///   backgroundImage → Background
/// </summary>
public class EtcSlot : MonoBehaviour, IPointerClickHandler
{
    [Header("슬롯 UI")]
    public Image itemIconImage;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI countText;
    public Image backgroundImage;

    [Header("아이템 정보 (런타임)")]
    [HideInInspector] public ItemData itemData;
    [HideInInspector] public int itemCount;

    // ═══════════════════════════════════════════════════════════════
    //  자동 바인딩 (Inspector 미연결 시 자식에서 자동 탐색)
    // ═══════════════════════════════════════════════════════════════

    void Awake()
    {
        AutoBindComponents();
    }

    /// <summary>
    /// Inspector 미연결 시 자식 오브젝트에서 UI 컴포넌트를 자동 탐색.
    /// 프리팹에서 수동 연결이 누락되어도 동작 보장.
    /// </summary>
    private void AutoBindComponents()
    {
        if (itemIconImage == null)
        {
            Transform iconTr = transform.Find("ItemIcon") ?? transform.Find("Icon") ?? transform.Find("Image");
            if (iconTr != null)
                itemIconImage = iconTr.GetComponent<Image>();
            // 직계 자식 Image 중 Background가 아닌 첫 번째
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

        // itemNameText/countText 둘 다 못 찾으면 TMP 순서대로 할당
        if (itemNameText == null || countText == null)
        {
            var allTmp = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var tmp in allTmp)
            {
                // 이미 할당된 것 스킵
                if (tmp == itemNameText || tmp == countText) continue;

                if (itemNameText == null)
                {
                    itemNameText = tmp;
                    continue;
                }
                if (countText == null)
                {
                    countText = tmp;
                    break;
                }
            }
        }

        if (backgroundImage == null)
        {
            Transform bgTr = transform.Find("Background") ?? transform.Find("BG") ?? transform.Find("Bg");
            if (bgTr != null)
                backgroundImage = bgTr.GetComponent<Image>();
            // 폴백: 자기 자신의 Image (루트가 배경 역할)
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  슬롯 설정
    // ═══════════════════════════════════════════════════════════════

    /// <summary>아이템 데이터로 슬롯 세팅</summary>
    public void Setup(ItemData item, int count)
    {
        // ★ Awake 전에 Setup이 호출될 수 있으므로 바인딩 보장
        AutoBindComponents();

        itemData = item;
        itemCount = count;
        UpdateSlotUI();
    }

    /// <summary>슬롯 초기화 (빈 슬롯)</summary>
    public void ClearSlot()
    {
        itemData = null;
        itemCount = 0;
        UpdateSlotUI();
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI 갱신
    // ═══════════════════════════════════════════════════════════════

    private void UpdateSlotUI()
    {
        if (itemData != null)
        {
            // ── 아이콘 ──
            if (itemIconImage != null)
            {
                itemIconImage.sprite = itemData.itemIcon;
                itemIconImage.color = Color.white;
                itemIconImage.gameObject.SetActive(true);
            }

            // ── 이름 ──
            if (itemNameText != null)
            {
                itemNameText.text = itemData.itemName;
                itemNameText.color = itemData.GetRarityColor();
                itemNameText.gameObject.SetActive(true);
            }

            // ── 수량 ──
            if (countText != null)
            {
                countText.text = $"x{itemCount}";
                countText.gameObject.SetActive(true);
            }

            // ── 배경 ──
            if (backgroundImage != null)
            {
                backgroundImage.color = GetCategoryBgColor(itemData.itemType);
                backgroundImage.gameObject.SetActive(true);
            }
        }
        else
        {
            // ── 빈 슬롯 ──
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
    //  클릭 → 사용/상세
    // ═══════════════════════════════════════════════════════════════

    public void OnPointerClick(PointerEventData eventData)
    {
        if (itemData == null) return;

        SoundManager.Instance?.PlayButtonClick();

        // ── 선택권 분기 ──
        if (IsSelectionTicket(itemData))
        {
            if (SelectionPanel.Instance != null)
            {
                SelectionPanel.Instance.Open(itemData, -1);
                return;
            }
        }

        // ── 소비 아이템 → ItemDetailPanel 우선, 폴백 즉시 사용 ──
        if (itemData.isConsumable && itemData.itemType == ItemType.Consumable)
        {
            if (ItemDetailPanel.Instance != null)
            {
                ItemDetailPanel.Instance.Open(itemData, itemCount, -1, true);
                return;
            }
            UseConsumable();
            return;
        }

        // 가챠 티켓 → 가챠 사용
        if (itemData.itemType == ItemType.GachaTicket_5Star ||
            itemData.itemType == ItemType.GachaTicket_3to5Star)
        {
            UseGachaTicket();
            return;
        }

        // 오프라인 보상 아이템 → 사용
        if (itemData.itemType == ItemType.OfflineReward_2h ||
            itemData.itemType == ItemType.OfflineReward_4h ||
            itemData.itemType == ItemType.OfflineReward_8h ||
            itemData.itemType == ItemType.OfflineReward_12h)
        {
            UseOfflineReward();
            return;
        }

        // ── 그 외 → ItemDetailPanel 우선, 폴백 메시지 ──
        if (ItemDetailPanel.Instance != null)
        {
            ItemDetailPanel.Instance.Open(itemData, itemCount, -1, false);
            return;
        }

        UIManager.Instance?.ShowMessage(
            $"{itemData.itemName}\n{itemData.itemDescription}",
            itemData.GetRarityColor());
    }

    /// <summary>선택권 아이템인지 판별</summary>
    private bool IsSelectionTicket(ItemData item)
    {
        if (item.itemType.ToString() == "SelectionTicket") return true;
        if (!string.IsNullOrEmpty(item.itemName) && item.itemName.Contains("선택권")) return true;
        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    //  사용 처리
    // ═══════════════════════════════════════════════════════════════

    private void UseConsumable()
    {
        if (PlayerStats.Instance == null) return;

        itemData.consumableEffect.ApplyEffect(PlayerStats.Instance);
        InventoryManager.Instance?.RemoveItem(itemData, 1);

        UIManager.Instance?.ShowMessage($"{itemData.itemName} 사용!", Color.green);
        Debug.Log($"[EtcSlot] 소비 아이템 사용: {itemData.itemName}");
    }

    private void UseGachaTicket()
    {
        UIManager.Instance?.ShowMessage($"{itemData.itemName} — 가챠에서 사용하세요", Color.yellow);
    }

    private void UseOfflineReward()
    {
        // 시간에 따른 오프라인 보상 누적
        float hours = 0f;
        switch (itemData.itemType)
        {
            case ItemType.OfflineReward_2h: hours = 2f; break;
            case ItemType.OfflineReward_4h: hours = 4f; break;
            case ItemType.OfflineReward_8h: hours = 8f; break;
            case ItemType.OfflineReward_12h: hours = 12f; break;
        }

        // OfflineRewardManager에 누적 시간 추가
        UIManager.Instance?.ShowMessage($"{itemData.itemName} — 오프라인 보상 {hours}시간\n자동 적용됩니다", Color.green);
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
