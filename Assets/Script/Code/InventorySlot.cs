using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 인벤토리 슬롯 (전면 리팩터링)
///
/// [프리팹 Hierarchy]
///   InventorySlot (InventorySlot.cs)
///   ├── Background (Image) — 레어도별 배경색
///   ├── ItemIcon (Image + Button) — 클릭 시 ActionButtons 토글
///   ├── EnhanceText (TextMeshProUGUI) — "+N" (색상 코딩)
///   ├── LevelText (TextMeshProUGUI) — "Lv.N"
///   ├── EquippedText (TextMeshProUGUI) — "E" (장착 중일 때만)
///   ├── CountText (TextMeshProUGUI) — 수량 (2+ 일 때)
///   ├── RarityBorder (Image) — 레어도 테두리
///   ├── LockIcon (GameObject) — 잠금 아이콘
///   ├── LockedNameText (TextMeshProUGUI) — "???"
///   └── ActionButtons (GameObject) — 클릭 시 토글
///       ├── LevelUpButton (Button) — 레벨업
///       └── EquipButton (Button) — 장착
/// </summary>
public class InventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("슬롯 UI")]
    public Image itemIconImage;
    public Image backgroundImage;
    public Image rarityBorderImage;
    public TextMeshProUGUI countText;

    [Header("강화/레벨/장착 표시")]
    [Tooltip("+N 강화 수치 (색상: +15=빨강, +10=노랑, +5=초록, 미만=흰색)")]
    public TextMeshProUGUI enhanceText;
    [Tooltip("Lv.N 아이템 레벨")]
    public TextMeshProUGUI levelText;
    [Tooltip("장착 중 'E' 표시 (장착 시에만 활성화)")]
    public TextMeshProUGUI equippedText;

    [Header("잠금 상태 UI")]
    public GameObject lockIcon;
    public TextMeshProUGUI lockedNameText;

    [Header("액션 버튼 (아이콘 클릭 시 토글)")]
    [Tooltip("LevelUpButton + EquipButton 부모 오브젝트")]
    public GameObject actionButtons;
    public Button levelUpButton;
    public Button equipButton;

    [Header("호환용 (기존 참조 유지)")]
    [Tooltip("E(장착) 뱃지 오브젝트 — equippedText 대체, 둘 다 지원")]
    public GameObject equippedBadge;
    [Tooltip("기존 enhanceLevelText 참조 → enhanceText로 통합")]
    public TextMeshProUGUI enhanceLevelText;
    [Tooltip("기존 itemLevelText 참조 → levelText로 통합")]
    public TextMeshProUGUI itemLevelText;

    [Header("아이템 정보")]
    public ItemData itemData;
    public int itemCount;
    public int enhanceLevel = 0;
    public int itemLevel = 0;
    public bool isUnlocked = false;
    public int slotIndex = -1;

    private GameObject dragIcon;
    private Canvas canvas;
    private RectTransform dragTransform;
    private bool actionButtonsVisible = false;

    // ═══ 잠금 상태 색상 ═══
    private static readonly Color LockedBgColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
    private static readonly Color LockedTint = new Color(0.15f, 0.15f, 0.15f, 0.8f);

    void Awake()
    {
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

        // 호환 처리: 기존 필드가 연결되어 있고 새 필드가 비어있으면 매핑
        if (enhanceText == null && enhanceLevelText != null) enhanceText = enhanceLevelText;
        if (levelText == null && itemLevelText != null) levelText = itemLevelText;
        Debug.Log("iconButton: " + itemIconImage);
        Debug.Log("actionButtons: " + actionButtons);
    }

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();

        // 액션 버튼 초기 숨김
        if (actionButtons != null)
            actionButtons.SetActive(false);

        // 버튼 이벤트 바인딩
        if (levelUpButton != null)
            levelUpButton.onClick.AddListener(OnLevelUpClicked);
        if (equipButton != null)
            equipButton.onClick.AddListener(OnEquipClicked);

        // 아이콘 Button 컴포넌트 클릭 → ActionButtons 토글
        Button iconBtn = itemIconImage?.GetComponent<Button>();
        if (iconBtn != null)
            iconBtn.onClick.AddListener(ToggleActionButtons);
    }

    // ═══════════════════════════════════════════════════════════════
    //  슬롯 설정
    // ═══════════════════════════════════════════════════════════════

    /// <summary>잠금 상태로 슬롯 설정 (장비 탭 전용)</summary>
    public void SetupLocked(ItemData item)
    {
        itemData = item;
        itemCount = 0;
        enhanceLevel = 0;
        itemLevel = 0;
        isUnlocked = false;
        HideActionButtons();
        UpdateSlotUI();
    }

    /// <summary>해금 상태로 슬롯 설정 (보유 아이템)</summary>
    public void SetupUnlocked(ItemData item, int count, int enhance, int level)
    {
        itemData = item;
        itemCount = count;
        enhanceLevel = enhance;
        itemLevel = level;
        isUnlocked = true;
        HideActionButtons();
        UpdateSlotUI();
    }

    public void AddItem(ItemData item, int count = 1, int enhance = 0)
    {
        itemData = item;
        itemCount += count;
        enhanceLevel = enhance;
        isUnlocked = true;
        HideActionButtons();
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
        itemLevel = 0;
        isUnlocked = false;
        HideActionButtons();
        UpdateSlotUI();
    }

    public void UpdateEnhanceLevel(int newLevel)
    {
        enhanceLevel = newLevel;
        UpdateSlotUI();
    }

    public void UpdateItemLevel(int newLevel)
    {
        itemLevel = newLevel;
        UpdateSlotUI();
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI 갱신
    // ═══════════════════════════════════════════════════════════════

    private void UpdateSlotUI()
    {
        if (itemData != null && isUnlocked)
        {
            // ── 해금 상태 ──
            ShowIcon(true, itemData.itemIcon, GetEnhanceColor(enhanceLevel));

            // 배경: 레어도별 불투명 색상
            if (backgroundImage != null)
            {
                backgroundImage.color = GetRarityBgColor(itemData.rarity);
                backgroundImage.gameObject.SetActive(true);
            }

            // 레어도 테두리
            if (rarityBorderImage != null)
            {
                rarityBorderImage.color = itemData.GetRarityColor();
                rarityBorderImage.gameObject.SetActive(true);
            }

            // 수량
            if (countText != null)
            {
                if (itemCount > 1) { countText.text = itemCount.ToString(); countText.gameObject.SetActive(true); }
                else countText.gameObject.SetActive(false);
            }

            // 강화 수치 (+N) — 색상 코딩
            UpdateEnhanceText();

            // 아이템 레벨 (Lv.N)
            UpdateLevelText();

            // 장착 표시 (E)
            UpdateEquippedDisplay();

            ShowLockUI(false);
        }
        else if (itemData != null && !isUnlocked)
        {
            // ── 잠금 상태 ──
            ShowIcon(true, itemData.itemIcon, LockedTint);

            if (backgroundImage != null)
            {
                backgroundImage.color = LockedBgColor;
                backgroundImage.gameObject.SetActive(true);
            }

            if (rarityBorderImage != null)
                rarityBorderImage.gameObject.SetActive(false);

            if (countText != null) countText.gameObject.SetActive(false);
            SetTextActive(enhanceText, false);
            SetTextActive(levelText, false);
            SetTextActive(equippedText, false);
            if (equippedBadge != null) equippedBadge.SetActive(false);

            ShowLockUI(true);
        }
        else
        {
            // ── 빈 슬롯 ──
            ShowIcon(false, null, Color.clear);

            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                backgroundImage.gameObject.SetActive(true);
            }

            if (rarityBorderImage != null)
                rarityBorderImage.gameObject.SetActive(false);

            if (countText != null) countText.gameObject.SetActive(false);
            SetTextActive(enhanceText, false);
            SetTextActive(levelText, false);
            SetTextActive(equippedText, false);
            if (equippedBadge != null) equippedBadge.SetActive(false);

            ShowLockUI(false);
        }
    }

    /// <summary>강화 텍스트 갱신 — +N, 색상 코딩 (+15=빨강/+10=노랑/+5=초록/미만=흰색)</summary>
    private void UpdateEnhanceText()
    {
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
    }

    /// <summary>레벨 텍스트 갱신 — Lv.N</summary>
    private void UpdateLevelText()
    {
        if (levelText != null)
        {
            if (itemLevel > 0)
            {
                levelText.text = $"Lv.{itemLevel}";
                levelText.gameObject.SetActive(true);
            }
            else
            {
                levelText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>장착 표시 갱신 — "E" 텍스트 또는 뱃지</summary>
    private void UpdateEquippedDisplay()
    {
        bool isEquipped = false;
        if (itemData != null && EquipmentManager.Instance != null)
            isEquipped = EquipmentManager.Instance.IsItemEquipped(itemData.itemID);

        // 새 equippedText
        if (equippedText != null)
        {
            equippedText.text = "E";
            equippedText.gameObject.SetActive(isEquipped);
        }

        // 기존 equippedBadge 호환
        if (equippedBadge != null)
            equippedBadge.SetActive(isEquipped);
    }

    private void ShowIcon(bool show, Sprite sprite, Color color)
    {
        if (itemIconImage == null) return;
        if (show && sprite != null)
        {
            itemIconImage.sprite = sprite;
            itemIconImage.color = color;
            itemIconImage.gameObject.SetActive(true);
        }
        else
        {
            itemIconImage.sprite = null;
            itemIconImage.color = new Color(1f, 1f, 1f, 0f);
            itemIconImage.gameObject.SetActive(false);
        }
    }

    private void ShowLockUI(bool locked)
    {
        if (lockIcon != null) lockIcon.SetActive(locked);
        if (lockedNameText != null)
        {
            lockedNameText.gameObject.SetActive(locked);
            if (locked) lockedNameText.text = "???";
        }
    }

    private void SetTextActive(TextMeshProUGUI text, bool active)
    {
        if (text != null) text.gameObject.SetActive(active);
    }

    // ═══════════════════════════════════════════════════════════════
    //  클릭 — 아이콘 클릭 시 ActionButtons 토글
    // ═══════════════════════════════════════════════════════════════

    public void OnPointerClick(PointerEventData eventData)
    {
        if (itemData == null || !isUnlocked) return;

        // 장비 아이템만 액션 버튼 토글
        if (itemData is EquipmentData)
        {
            ToggleActionButtons();
            return;
        }

        // 비장비 아이템: 기존 동작 (상세 팝업 등)
        if (eventData != null && EquipmentDetailPopup.Instance != null)
            EquipmentDetailPopup.Instance.Open(this);
    }

    /// <summary>ActionButtons(LevelUpButton, EquipButton) 토글</summary>
    private void ToggleActionButtons()
    {
        Debug.Log($"[InventorySlot] ToggleActionButtons 호출됨 / actionButtons: {actionButtons} / visible: {actionButtonsVisible}");
        if (actionButtons == null) return;

        actionButtonsVisible = !actionButtonsVisible;
        actionButtons.SetActive(actionButtonsVisible);

        if (actionButtonsVisible)
            SoundManager.Instance?.PlayButtonClick();
    }

    /// <summary>ActionButtons 숨기기</summary>
    public void HideActionButtons()
    {
        actionButtonsVisible = false;
        if (actionButtons != null)
            actionButtons.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════
    //  액션 버튼 핸들러
    // ═══════════════════════════════════════════════════════════════

    /// <summary>레벨업 버튼 → InventoryManager.OpenLevelUpPanel</summary>
    private void OnLevelUpClicked()
    {
        // ★ 튜토리얼 중 레벨업 무조건 차단 (장착만 허용)
        if (TutorialManager.Instance != null && TutorialManager.Instance.ShouldBlockNonFocusButtons)
        {
            Debug.Log("[InventorySlot] 튜토리얼 중 레벨업 차단");
            return;
        }

        if (itemData == null || !isUnlocked) return;
        SoundManager.Instance?.PlayButtonClick();

        InventoryItemData invData = CreateInventoryItemData();
        InventoryManager.Instance?.OpenLevelUpPanel(invData, slotIndex);

        HideActionButtons();
    }

    /// <summary>장착 버튼 → EquipmentManager.EquipFromInventory</summary>
    private void OnEquipClicked()
    {
        if (itemData == null || !isUnlocked) return;
        SoundManager.Instance?.PlayEquip();

        InventoryItemData invData = CreateInventoryItemData();
        EquipmentManager.Instance?.EquipFromInventory(invData, slotIndex);

        HideActionButtons();
    }

    /// <summary>현재 슬롯 데이터로 InventoryItemData 생성</summary>
    private InventoryItemData CreateInventoryItemData()
    {
        return new InventoryItemData
        {
            itemID = itemData.itemID,
            count = itemCount,
            slotIndex = slotIndex,
            enhanceLevel = enhanceLevel,
            isUnlocked = isUnlocked,
            itemLevel = itemLevel
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  드래그 앤 드롭
    // ═══════════════════════════════════════════════════════════════

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (itemData == null || !isUnlocked) return;

        HideActionButtons();

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

        ShopManager shop = dropTarget.GetComponent<ShopManager>()
                        ?? dropTarget.GetComponentInParent<ShopManager>();
        if (shop != null || dropTarget.name.Contains("Shop"))
        {
            TrySellItem();
        }
    }

    private void TrySellItem()
    {
        if (itemData == null || !isUnlocked) return;

        if (itemCount <= 1)
        {
            UIManager.Instance?.ShowMessage("마지막 1개는 판매할 수 없습니다!", Color.red);
            return;
        }

        int sellCount = itemCount - 1;
        int sellPrice = Mathf.RoundToInt(itemData.buyPrice * 0.5f * sellCount);
        GameManager.Instance?.AddGold(sellPrice);
        InventoryManager.Instance?.RemoveItem(itemData, sellCount);
        UIManager.Instance?.ShowMessage($"{itemData.itemName} {sellCount}개 판매! +{sellPrice}G", Color.green);
    }

    // ═══════════════════════════════════════════════════════════════
    //  색상 유틸
    // ═══════════════════════════════════════════════════════════════

    /// <summary>강화 색상: +15=빨강, +10=노랑, +5=초록, 미만=흰색</summary>
    public static Color GetEnhanceColor(int level)
    {
        if (level >= 15) return new Color(1f, 0f, 0f, 1f);
        if (level >= 10) return new Color(1f, 1f, 0f, 1f);
        if (level >= 5)  return new Color(0f, 1f, 0f, 1f);
        return Color.white;
    }

    /// <summary>레어도별 슬롯 배경색</summary>
    public static Color GetRarityBgColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common:    return new Color(0.25f, 0.25f, 0.25f, 1f);
            case ItemRarity.Uncommon:  return new Color(0.15f, 0.30f, 0.15f, 1f);
            case ItemRarity.Rare:      return new Color(0.10f, 0.20f, 0.45f, 1f);
            case ItemRarity.Epic:      return new Color(0.35f, 0.10f, 0.45f, 1f);
            case ItemRarity.Legendary: return new Color(0.50f, 0.35f, 0.05f, 1f);
            default:                   return new Color(0.25f, 0.25f, 0.25f, 1f);
        }
    }
}
