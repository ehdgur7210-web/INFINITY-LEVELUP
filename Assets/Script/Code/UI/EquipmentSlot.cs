using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 인벤토리 장비 전용 슬롯
///
/// 상태 2종:
///   - Locked:   아이템 실루엣(어둡게) + 자물쇠 아이콘 + "???" 텍스트
///   - Unlocked: 아이템 아이콘 + 레어도 배경 + 강화/레벨 표시 + E뱃지
///
/// 클릭 → EquipmentSlotActionPanel 열기 (착용/레벨업/강화)
/// 드래그 → 장비 패널 슬롯(EquipPanelSlot)에 드롭하여 장착
///
/// [프리팹 Hierarchy]
///   EquipmentSlot (Button + EquipmentSlot.cs)
///   ├── Background (Image) — 레어도별 배경색
///   ├── ItemIcon (Image) — 장비 아이콘
///   ├── EnhanceLevel (TextMeshProUGUI) — "+N" 우하단
///   ├── ItemLevel (TextMeshProUGUI) — "Lv.N" 좌하단
///   ├── EquippedBadge (Image + Text "E") — 장착 중 표시
///   ├── LockIcon (Image) — 자물쇠
///   ├── LockedName (TextMeshProUGUI) — "???"
///   └── RarityBorder (Image) — 레어도 테두리
///
/// [Inspector 연결]
///   itemIconImage      → ItemIcon
///   backgroundImage    → Background
///   enhanceLevelText   → EnhanceLevel
///   itemLevelText      → ItemLevel
///   equippedBadge      → EquippedBadge
///   lockIcon           → LockIcon
///   lockedNameText     → LockedName
///   rarityBorderImage  → RarityBorder
/// </summary>
public class EquipmentSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("슬롯 UI")]
    public Image itemIconImage;
    public Image backgroundImage;
    public TextMeshProUGUI enhanceLevelText;
    public TextMeshProUGUI itemLevelText;

    [Header("잠금 상태 UI")]
    public GameObject lockIcon;
    public TextMeshProUGUI lockedNameText;
    public Image rarityBorderImage;

    [Header("장착 뱃지")]
    public GameObject equippedBadge;

    [Header("수량 (선택)")]
    public TextMeshProUGUI countText;

    [Header("아이템 정보")]
    public ItemData itemData;
    public int itemCount;
    public int enhanceLevel = 0;
    public int itemLevel = 0;
    public bool isUnlocked = false;

    private GameObject dragIcon;
    private Canvas canvas;
    private RectTransform dragTransform;

    // ═══ 색상 ═══
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
    }

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
    }

    // ═══════════════════════════════════════════════════════════════
    //  슬롯 설정
    // ═══════════════════════════════════════════════════════════════

    /// <summary>해금 상태로 슬롯 설정</summary>
    public void SetupUnlocked(EquipmentData item, int count, int enhance, int level)
    {
        itemData = item;
        itemCount = count;
        enhanceLevel = enhance;
        itemLevel = level;
        isUnlocked = true;
        UpdateSlotUI();
    }

    /// <summary>잠금 상태로 슬롯 설정</summary>
    public void SetupLocked(EquipmentData item)
    {
        itemData = item;
        itemCount = 0;
        enhanceLevel = 0;
        itemLevel = 0;
        isUnlocked = false;
        UpdateSlotUI();
    }

    public void ClearSlot()
    {
        itemData = null;
        itemCount = 0;
        enhanceLevel = 0;
        itemLevel = 0;
        isUnlocked = false;
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

            if (backgroundImage != null)
            {
                backgroundImage.color = GetRarityBgColor(itemData.rarity);
                backgroundImage.gameObject.SetActive(true);
            }

            if (rarityBorderImage != null)
            {
                rarityBorderImage.color = itemData.GetRarityColor();
                rarityBorderImage.gameObject.SetActive(true);
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

            if (itemLevelText != null)
            {
                if (itemLevel > 0)
                {
                    itemLevelText.text = $"Lv.{itemLevel}";
                    itemLevelText.gameObject.SetActive(true);
                }
                else itemLevelText.gameObject.SetActive(false);
            }

            UpdateEquippedBadge();
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

            if (rarityBorderImage != null) rarityBorderImage.gameObject.SetActive(false);
            if (countText != null) countText.gameObject.SetActive(false);
            if (enhanceLevelText != null) enhanceLevelText.gameObject.SetActive(false);
            if (itemLevelText != null) itemLevelText.gameObject.SetActive(false);
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

            if (rarityBorderImage != null) rarityBorderImage.gameObject.SetActive(false);
            if (countText != null) countText.gameObject.SetActive(false);
            if (enhanceLevelText != null) enhanceLevelText.gameObject.SetActive(false);
            if (itemLevelText != null) itemLevelText.gameObject.SetActive(false);
            if (equippedBadge != null) equippedBadge.SetActive(false);
            ShowLockUI(false);
        }
    }

    private void UpdateEquippedBadge()
    {
        if (equippedBadge == null) return;
        bool isEquipped = false;
        if (itemData != null && EquipmentManager.Instance != null)
            isEquipped = EquipmentManager.Instance.IsEquippedByID(itemData.itemID);
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

    // ═══════════════════════════════════════════════════════════════
    //  클릭 → EquipmentSlotActionPanel
    // ═══════════════════════════════════════════════════════════════

    public void OnPointerClick(PointerEventData eventData)
    {
        if (itemData == null || !isUnlocked) return;

        if (!(itemData is EquipmentData eq)) return;

        SoundManager.Instance?.PlayButtonClick();
        // 액션 패널 탐색 — Instance가 없으면 비활성 오브젝트에서 찾기
        if (EquipmentSlotActionPanel.Instance == null)
        {
            var found = FindObjectOfType<EquipmentSlotActionPanel>(true);
            if (found != null)
                found.gameObject.SetActive(true);
        }

        if (EquipmentSlotActionPanel.Instance != null)
        {
            EquipmentSlotActionPanel.Instance.Open(this, eq);
        }
        else
        {
            Debug.LogWarning("[EquipmentSlot] EquipmentSlotActionPanel이 씬에 없습니다. Inspector에서 배치해주세요.");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  드래그 앤 드롭
    // ═══════════════════════════════════════════════════════════════

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (itemData == null || !isUnlocked) return;

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
    }

    // ═══════════════════════════════════════════════════════════════
    //  색상 유틸
    // ═══════════════════════════════════════════════════════════════

    public static Color GetEnhanceColor(int level)
    {
        if (level >= 15) return new Color(1f, 0f, 0f, 1f);
        if (level >= 10) return new Color(1f, 1f, 0f, 1f);
        if (level >= 5) return new Color(0f, 1f, 0f, 1f);
        return Color.white;
    }

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
