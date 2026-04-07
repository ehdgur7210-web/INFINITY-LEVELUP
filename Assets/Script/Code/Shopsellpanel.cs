using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 샵 판매 패널
/// 
/// ✅ 이 컴포넌트를 샵 패널의 판매 영역 GameObject에 붙이기
/// ✅ IDropHandler로 드래그 드롭 감지 (ShopManager 이름/태그 의존 X)
/// ✅ 일괄 판매 기능 (일반템 전체 / 등급 선택)
/// </summary>
public class ShopSellPanel : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("판매 패널 UI")]
    [SerializeField] private Image panelHighlight;      // 드래그 올라올 때 강조 이미지
    [SerializeField] private TextMeshProUGUI sellHintText; // "여기에 드롭하여 판매"

    [Header("일괄 판매 버튼")]
    [SerializeField] private Button sellAllCommonButton;    // 일반템 전체 판매
    [SerializeField] private Button sellAllUncommonButton;  // 고급 이하 전체 판매
    [SerializeField] private Button sellAllNonEquippedButton; // 미장착 전체 판매

    [Header("판매 확인 메시지")]
    [SerializeField] private bool showConfirmDialog = true; // 일괄 판매 전 확인창

    private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    private Color highlightColor = new Color(1f, 0.8f, 0f, 0.6f); // 골드 강조

    void Start()
    {
        SetupButtons();

        if (panelHighlight != null)
            panelHighlight.color = normalColor;

        if (sellHintText != null)
            sellHintText.text = "아이템을 드래그하여 판매";
    }

    private void SetupButtons()
    {
        if (sellAllCommonButton != null)
            sellAllCommonButton.onClick.AddListener(() => TrySellAllByRarity(ItemRarity.Common));

        if (sellAllUncommonButton != null)
            sellAllUncommonButton.onClick.AddListener(() => TrySellAllUpToRarity(ItemRarity.Uncommon));

        if (sellAllNonEquippedButton != null)
            sellAllNonEquippedButton.onClick.AddListener(TrySellAllNonEquipped);
    }

    // ─────────────────────────────────────────
    // ★ 드래그 드롭 감지
    // ─────────────────────────────────────────

    public void OnDrop(PointerEventData eventData)
    {
        GameObject dragged = eventData.pointerDrag;
        if (dragged == null) return;

        InventorySlot slot = dragged.GetComponent<InventorySlot>();
        if (slot == null) return;

        if (slot.itemData == null) return;

        // ✅ 새 시스템: 장착된 아이템은 인벤에 없으므로 체크 불필요

        SellSlotItem(slot);
    }

    /// <summary>
    /// 드래그가 패널 위에 올라올 때 강조
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        if (panelHighlight != null)
            panelHighlight.color = highlightColor;

        if (sellHintText != null)
            sellHintText.text = "여기서 손 떼면 판매!";
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (panelHighlight != null)
            panelHighlight.color = normalColor;

        if (sellHintText != null)
            sellHintText.text = "아이템을 드래그하여 판매";
    }

    // ─────────────────────────────────────────
    // 단일 아이템 판매
    // ─────────────────────────────────────────

    private void SellSlotItem(InventorySlot slot)
    {
        if (slot.itemData == null) return;

        int sellPrice = CalculateSellPrice(slot.itemData, slot.enhanceLevel, slot.itemCount);

        // ★ 단일 아이템 판매 효과음 (드롭 판매)
        SoundManager.Instance?.PlaySellItem();
        GameManager.Instance?.AddGold(sellPrice);
        InventoryManager.Instance?.RemoveItem(slot.itemData, slot.itemCount);

        UIManager.Instance?.ShowMessage(
            $"{slot.itemData.itemName} x{slot.itemCount} 판매!\n+{sellPrice:N0}G",
            Color.yellow
        );

        Debug.Log($"[ShopSellPanel] 판매: {slot.itemData.itemName} x{slot.itemCount} → {sellPrice}G");
    }

    // ─────────────────────────────────────────
    // 일괄 판매
    // ─────────────────────────────────────────

    /// <summary>
    /// 특정 등급 아이템만 전체 판매
    /// </summary>
    private void TrySellAllByRarity(ItemRarity rarity)
    {
        // ★ 판매 버튼 클릭 효과음
        SoundManager.Instance?.PlayButtonClick();
        List<InventorySlot> targets = GetSellableSlots(
            slot => slot.itemData.rarity == rarity
        );

        if (targets.Count == 0)
        {
            UIManager.Instance?.ShowMessage($"판매할 {rarity} 아이템 없음", Color.white);
            return;
        }

        int totalGold = CalculateTotalPrice(targets);

        if (showConfirmDialog && UIManager.Instance != null)
        {
            UIManager.Instance.ShowConfirmDialog(
                $"{rarity} 아이템 {targets.Count}개\n총 {totalGold:N0}G에 판매?",
                () => ExecuteSellAll(targets, totalGold)
            );
        }
        else
        {
            ExecuteSellAll(targets, totalGold);
        }
    }

    /// <summary>
    /// 특정 등급 이하 전체 판매
    /// </summary>
    private void TrySellAllUpToRarity(ItemRarity maxRarity)
    {
        // ★ 판매 버튼 클릭 효과음
        SoundManager.Instance?.PlayButtonClick();
        List<InventorySlot> targets = GetSellableSlots(
            slot => (int)slot.itemData.rarity <= (int)maxRarity
        );

        if (targets.Count == 0)
        {
            UIManager.Instance?.ShowMessage($"{maxRarity} 이하 판매할 아이템 없음", Color.white);
            return;
        }

        int totalGold = CalculateTotalPrice(targets);

        if (showConfirmDialog && UIManager.Instance != null)
        {
            UIManager.Instance.ShowConfirmDialog(
                $"{maxRarity} 이하 아이템 {targets.Count}개\n총 {totalGold:N0}G에 판매?",
                () => ExecuteSellAll(targets, totalGold)
            );
        }
        else
        {
            ExecuteSellAll(targets, totalGold);
        }
    }

    /// <summary>
    /// 미장착 아이템 전체 판매
    /// </summary>
    private void TrySellAllNonEquipped()
    {
        // ★ 판매 버튼 클릭 효과음
        SoundManager.Instance?.PlayButtonClick();
        List<InventorySlot> targets = GetSellableSlots(_ => true); // 장착 필터는 GetSellableSlots 내부에서

        if (targets.Count == 0)
        {
            UIManager.Instance?.ShowMessage("판매할 아이템 없음", Color.white);
            return;
        }

        int totalGold = CalculateTotalPrice(targets);

        if (showConfirmDialog && UIManager.Instance != null)
        {
            UIManager.Instance.ShowConfirmDialog(
                $"미장착 아이템 {targets.Count}개\n총 {totalGold:N0}G에 판매?",
                () => ExecuteSellAll(targets, totalGold)
            );
        }
        else
        {
            ExecuteSellAll(targets, totalGold);
        }
    }

    /// <summary>
    /// 실제 판매 실행
    /// </summary>
    private void ExecuteSellAll(List<InventorySlot> slots, int totalGold)
    {
        int count = slots.Count;

        foreach (var slot in slots)
        {
            if (slot.itemData == null) continue;
            InventoryManager.Instance?.RemoveItem(slot.itemData, slot.itemCount);
        }

        // ★ 일괄 판매 확인(선택) 후 효과음
        SoundManager.Instance?.PlaySellItem();
        GameManager.Instance?.AddGold(totalGold);

        UIManager.Instance?.ShowMessage(
            $"아이템 {count}개 일괄 판매!\n+{totalGold:N0}G",
            Color.yellow
        );

        Debug.Log($"[ShopSellPanel] 일괄 판매 {count}개 → {totalGold}G");
    }

    // ─────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────

    /// <summary>
    /// 판매 가능한 슬롯 목록 (장착 중 제외, 조건 필터)
    /// </summary>
    private List<InventorySlot> GetSellableSlots(System.Func<InventorySlot, bool> filter)
    {
        List<InventorySlot> result = new List<InventorySlot>();

        if (InventoryManager.Instance == null) return result;

        InventorySlot[] allSlots = InventoryManager.Instance.GetAllSlots();
        foreach (var slot in allSlots)
        {
            if (slot == null || slot.itemData == null) continue;
            // ✅ 새 시스템: 인벤에 있는 아이템은 모두 미장착 상태
            if (!filter(slot)) continue;
            result.Add(slot);
        }

        return result;
    }

    private int CalculateTotalPrice(List<InventorySlot> slots)
    {
        int total = 0;
        foreach (var slot in slots)
            total += CalculateSellPrice(slot.itemData, slot.enhanceLevel, slot.itemCount);
        return total;
    }

    /// <summary>
    /// 판매가 계산 (기본가 50% + 강화 보너스)
    /// </summary>
    private int CalculateSellPrice(ItemData item, int enhanceLevel, int count)
    {
        float basePrice = item.buyPrice * 0.5f;
        float enhBonus = 1f + (enhanceLevel * 0.2f); // 강화당 20% 추가
        return Mathf.RoundToInt(basePrice * enhBonus * count);
    }
}