using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 동료 레벨업 재료 슬롯
///
/// 빈 슬롯 클릭 → 인벤토리에서 재료 아이템 선택 팝업
/// 재료가 있는 슬롯 클릭 → 재료 제거
///
/// [프리팹 Hierarchy]
///   CompanionMaterialSlot (CompanionMaterialSlot.cs + Button)
///   ├── Background (Image) → backgroundImage
///   ├── ItemIcon (Image) → itemIcon
///   ├── ExpText (TextMeshProUGUI) → expText   — "+500 EXP"
///   ├── PlusIcon (GameObject) → plusIcon       — 빈 슬롯일 때 "+" 표시
///   └── CountText (TextMeshProUGUI) → countText — 수량 (선택)
///
/// [Inspector 연결]
///   backgroundImage → Background
///   itemIcon        → ItemIcon
///   expText         → ExpText
///   plusIcon        → PlusIcon
///   countText       → CountText (선택)
/// </summary>
public class CompanionMaterialSlot : MonoBehaviour, IPointerClickHandler
{
    [Header("슬롯 UI")]
    public Image backgroundImage;
    public Image itemIcon;
    public TextMeshProUGUI expText;
    public GameObject plusIcon;
    public TextMeshProUGUI countText;

    [Header("색상")]
    public Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    public Color filledColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);

    // ── 내부 데이터 ──
    private ItemData _item;
    private int _count;
    private int _expValue;
    private int _slotIndex;
    private CompanionDetailPanel _tab;

    // ═══════════════════════════════════════════════════════════════
    //  초기화
    // ═══════════════════════════════════════════════════════════════

    /// <summary>슬롯 초기화 (탭에서 생성 시 호출)</summary>
    public void Init(CompanionDetailPanel tab, int index)
    {
        _tab = tab;
        _slotIndex = index;
        Clear();
    }

    // ═══════════════════════════════════════════════════════════════
    //  재료 설정 / 제거
    // ═══════════════════════════════════════════════════════════════

    /// <summary>재료 아이템 설정</summary>
    public void SetMaterial(ItemData item, int count, int expPerItem)
    {
        _item = item;
        _count = count;
        _expValue = expPerItem * count;

        if (itemIcon != null)
        {
            itemIcon.sprite = item != null ? item.itemIcon : null;
            itemIcon.color = item != null ? Color.white : Color.clear;
            itemIcon.gameObject.SetActive(item != null);
        }

        if (expText != null)
        {
            expText.text = _expValue > 0 ? $"+{_expValue} EXP" : "";
            expText.gameObject.SetActive(_expValue > 0);
        }

        if (countText != null)
        {
            countText.text = count > 1 ? $"x{count}" : "";
            countText.gameObject.SetActive(count > 1);
        }

        if (plusIcon != null)
            plusIcon.SetActive(false);

        if (backgroundImage != null)
            backgroundImage.color = filledColor;
    }

    /// <summary>슬롯 비우기</summary>
    public void Clear()
    {
        _item = null;
        _count = 0;
        _expValue = 0;

        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.color = Color.clear;
            itemIcon.gameObject.SetActive(false);
        }

        if (expText != null)
            expText.gameObject.SetActive(false);

        if (countText != null)
            countText.gameObject.SetActive(false);

        if (plusIcon != null)
            plusIcon.SetActive(true);

        if (backgroundImage != null)
            backgroundImage.color = emptyColor;
    }

    // ═══════════════════════════════════════════════════════════════
    //  클릭 처리
    // ═══════════════════════════════════════════════════════════════

    public void OnPointerClick(PointerEventData eventData)
    {
        SoundManager.Instance?.PlayButtonClick();

        if (_item != null)
        {
            // 재료가 있으면 제거
            Clear();
            _tab?.OnMaterialChanged();
        }
        else
        {
            // 빈 슬롯 → 재료 선택 요청
            _tab?.OnMaterialSlotClicked(_slotIndex);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  데이터 접근
    // ═══════════════════════════════════════════════════════════════

    public bool IsEmpty => _item == null;
    public ItemData GetItem() => _item;
    public int GetCount() => _count;
    public int GetExpValue() => _expValue;
}
