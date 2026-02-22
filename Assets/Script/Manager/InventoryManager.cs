using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

/// <summary>
/// 인벤토리 시스템 관리 클래스 - 강화 시스템 추가
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("인벤토리 설정")]
    public int inventorySize = 12;
    public GameObject slotPrefab;
    public Transform slotParent;

    [Header("인벤토리 UI")]
    public GameObject inventoryPanel;

    private List<InventorySlot> slots = new List<InventorySlot>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeInventory();
    }

    void Start()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
        }
    }

    void InitializeInventory()
    {
        if (slotPrefab == null || slotParent == null)
        {
            Debug.LogWarning("슬롯 프리팹 또는 부모가 설정되지 않았습니다!");
            return;
        }

        for (int i = 0; i < inventorySize; i++)
        {
            GameObject slotObj = Instantiate(slotPrefab, slotParent);
            InventorySlot slot = slotObj.GetComponent<InventorySlot>();

            if (slot != null)
            {
                slots.Add(slot);
            }
        }

        Debug.Log($"[InventoryManager] 초기화 완료! 슬롯 개수: {slots.Count}");
    }

    public void ToggleInventory()
    {
        if (inventoryPanel != null)
        {
            bool isActive = inventoryPanel.activeSelf;
            inventoryPanel.SetActive(!isActive);
        }
    }

    /// <summary>
    /// 아이템 추가 (기존 메서드 - 그대로 유지)
    /// </summary>
    public bool AddItem(ItemData item, int count = 1)
    {
        if (item == null)
        {
            Debug.LogWarning("[InventoryManager] 추가할 아이템이 null입니다!");
            return false;
        }

        Debug.Log($"[InventoryManager] 아이템 추가 시도: {item.itemName} x{count}");

        if (!HasSpace())
        {
            if (MailManager.Instance != null)
            {
                MailManager.Instance.SendItemToMail(item, count, "인벤토리가 꽉 찼습니다");
                Debug.Log($"[InventoryManager] 인벤토리 꽉참! 메일로 전송: {item.itemName} x{count}");

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowMessage($"인벤토리가 꽉 찼습니다!\n{item.itemName}이(가) 메일함으로 전송되었습니다.", Color.yellow);
                }

                return true;
            }
            else
            {
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowMessage("인벤토리가 꽉 찼습니다!", Color.red);
                }

                return false;
            }
        }

        if (item.maxStack > 1)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot.itemData == item && slot.itemCount < item.maxStack)
                {
                    int remainingSpace = item.maxStack - slot.itemCount;
                    int amountToAdd = Mathf.Min(count, remainingSpace);

                    slot.AddItem(item, amountToAdd, 0); // ⭐ 강화 레벨 0
                    count -= amountToAdd;

                    Debug.Log($"[InventoryManager] 기존 슬롯에 추가: {item.itemName} x{amountToAdd}");

                    if (count <= 0)
                    {
                        UpdateInventoryUI();
                        return true;
                    }
                }
            }
        }

        while (count > 0)
        {
            InventorySlot emptySlot = GetEmptySlot();

            if (emptySlot == null)
            {
                if (MailManager.Instance != null)
                {
                    MailManager.Instance.SendItemToMail(item, count, "인벤토리가 꽉 찼습니다");
                }
                break;
            }

            int amountToAdd = Mathf.Min(count, item.maxStack);
            emptySlot.AddItem(item, amountToAdd, 0); // ⭐ 강화 레벨 0
            count -= amountToAdd;
        }

        UpdateInventoryUI();
        return true;
    }

    /// <summary>
    /// ⭐⭐⭐ 추가: 강화 레벨 포함해서 아이템 추가
    /// </summary>
    public bool AddItemWithEnhancement(ItemData item, int count, int enhanceLevel)
    {
        if (item == null)
        {
            Debug.LogWarning("[InventoryManager] 추가할 아이템이 null입니다!");
            return false;
        }

        Debug.Log($"[InventoryManager] 강화 아이템 추가: {item.itemName} x{count} +{enhanceLevel}");

        // 빈 슬롯 찾기
        InventorySlot emptySlot = GetEmptySlot();

        if (emptySlot == null)
        {
            // 공간 없으면 메일로
            if (MailManager.Instance != null)
            {
                MailManager.Instance.SendItemToMail(item, count, "인벤토리가 꽉 찼습니다");

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowMessage($"인벤토리가 꽉 찼습니다!\n{item.itemName}이(가) 메일함으로 전송되었습니다.", Color.yellow);
                }
            }
            else
            {
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowMessage("인벤토리가 꽉 찼습니다!", Color.red);
                }
                return false;
            }

            return true;
        }

        // ⭐ 강화 레벨 포함해서 추가
        emptySlot.AddItem(item, count, enhanceLevel);
        Debug.Log($"[InventoryManager] {item.itemName} +{enhanceLevel} 추가 완료!");

        UpdateInventoryUI();
        return true;
    }

    public bool RemoveItem(ItemData item, int count = 1)
    {
        if (item == null) return false;

        int remaining = count;

        foreach (InventorySlot slot in slots)
        {
            if (slot.itemData == item)
            {
                if (slot.itemCount >= remaining)
                {
                    slot.RemoveItem(remaining);
                    Debug.Log($"[InventoryManager] {item.itemName} {count}개 제거");
                    UpdateInventoryUI();
                    return true;
                }
                else
                {
                    remaining -= slot.itemCount;
                    slot.ClearSlot();
                }
            }
        }

        Debug.LogWarning($"[InventoryManager] {item.itemName}이(가) 충분하지 않습니다!");
        return false;
    }

    public int GetItemCount(ItemData item)
    {
        if (item == null) return 0;

        int totalCount = 0;

        foreach (InventorySlot slot in slots)
        {
            if (slot.itemData == item)
            {
                totalCount += slot.itemCount;
            }
        }

        return totalCount;
    }

    public bool HasItem(ItemData item, int count = 1)
    {
        return GetItemCount(item) >= count;
    }

    public List<ItemData> GetAllItems()
    {
        List<ItemData> items = new List<ItemData>();

        foreach (InventorySlot slot in slots)
        {
            if (slot.itemData != null)
            {
                items.Add(slot.itemData);
            }
        }

        return items;
    }

    public bool HasSpace()
    {
        if (slots == null) return false;

        foreach (InventorySlot slot in slots)
        {
            if (slot != null && slot.itemData == null)
            {
                return true;
            }
        }

        return false;
    }

    public int GetEmptySlotCount()
    {
        if (slots == null) return 0;

        int count = 0;
        foreach (InventorySlot slot in slots)
        {
            if (slot != null && slot.itemData == null)
            {
                count++;
            }
        }

        return count;
    }

    private InventorySlot GetEmptySlot()
    {
        foreach (InventorySlot slot in slots)
        {
            if (slot != null && slot.itemData == null)
            {
                return slot;
            }
        }

        return null;
    }
    /// <summary>
    /// ★ 모든 슬롯 반환 (ShopSellPanel에서 일괄 판매용)
    /// </summary>
    public InventorySlot[] GetAllSlots()
    {
        return slots.ToArray();
    }

    private void UpdateInventoryUI()
    {
        // 현재는 각 슬롯이 자체적으로 업데이트
    }

    public void ClearAllItems()
    {
        foreach (InventorySlot slot in slots)
        {
            if (slot != null)
            {
                slot.ClearSlot();
            }
        }

        Debug.Log("[InventoryManager] 인벤토리 전체 초기화 완료");
    }
}
