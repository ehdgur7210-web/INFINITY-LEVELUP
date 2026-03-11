using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

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

    [Header("같이 슬라이드할 패널들 (HotSkillPanel, OffLine 등)")]
    public RectTransform[] linkedPanels;  // Inspector에서 HotSkillPanel, OffLine 연결

    [Header("채팅 연동")]
    [Tooltip("인벤토리 닫힐 때 채팅창 표시, 열릴 때 숨김")]
    public ChatSystem chatSystem;

    [Header("슬라이드 애니메이션 설정")]
    [Tooltip("패널이 열렸을 때 InvenPanel Y 위치")]
    public float openPosY = 31f;
    [Tooltip("패널이 닫혔을 때 InvenPanel Y 위치")]
    public float closedPosY = -350f;
    [Tooltip("슬라이드 속도 (초)")]
    public float slideDuration = 0.25f;

    private RectTransform panelRect;
    private bool isPanelOpen = true;
    private Coroutine slideCoroutine;

    // linkedPanels의 시작 Y값 저장 (각자 상대적으로 이동)
    private float[] linkedOpenPosY;
    private float[] linkedClosedPosY;

    private List<InventorySlot> slots = new List<InventorySlot>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        InitializeInventory();
    }

    void Start()
    {
        if (Instance != this) return;
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
            panelRect = inventoryPanel.GetComponent<RectTransform>();

            if (panelRect != null)
            {
                Vector2 pos = panelRect.anchoredPosition;
                pos.y = openPosY;
                panelRect.anchoredPosition = pos;
                isPanelOpen = true;
            }
        }

        // ★ linkedPanels의 열림/닫힘 Y값 계산
        // 이동 delta = closedPosY - openPosY (InvenPanel 기준)
        float deltaY = closedPosY - openPosY;

        if (linkedPanels != null)
        {
            linkedOpenPosY = new float[linkedPanels.Length];
            linkedClosedPosY = new float[linkedPanels.Length];

            for (int i = 0; i < linkedPanels.Length; i++)
            {
                if (linkedPanels[i] == null) continue;
                float currentY = linkedPanels[i].anchoredPosition.y;
                linkedOpenPosY[i] = currentY;           // 현재 Y = 열린 위치
                linkedClosedPosY[i] = currentY + deltaY;  // 같은 delta만큼 이동
            }
        }
    }

    // ★ MainGameInitializer에서 씬 복귀 시 UI 재연결
    public void RefreshPanelRef()
    {
        if (inventoryPanel == null) return;
        panelRect = inventoryPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            Vector2 pos = panelRect.anchoredPosition;
            pos.y = openPosY;
            panelRect.anchoredPosition = pos;
            isPanelOpen = true;
        }
        float deltaY = closedPosY - openPosY;
        if (linkedPanels != null)
        {
            linkedOpenPosY = new float[linkedPanels.Length];
            linkedClosedPosY = new float[linkedPanels.Length];
            for (int i = 0; i < linkedPanels.Length; i++)
            {
                if (linkedPanels[i] == null) continue;
                linkedOpenPosY[i] = linkedPanels[i].anchoredPosition.y;
                linkedClosedPosY[i] = linkedOpenPosY[i] + deltaY;
            }
        }
        Debug.Log("[InventoryManager] UI 참조 재연결 완료");
    }

    // ★ slotParent 재등록 시 슬롯 재생성
    public void RebuildSlots()
    {
        if (slotParent == null || slotPrefab == null) return;
        slots.Clear();
        foreach (Transform child in slotParent)
            Destroy(child.gameObject);
        for (int i = 0; i < inventorySize; i++)
        {
            GameObject slotObj = Instantiate(slotPrefab, slotParent);
            InventorySlot slot = slotObj.GetComponent<InventorySlot>();
            if (slot != null) slots.Add(slot);
        }
        Debug.Log($"[InventoryManager] 슬롯 재생성 완료 ({slots.Count}개)");
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

    // ─────────────────────────────────────────
    // ★ 슬라이드 토글 (SetActive 대신 Y 이동)
    // HotSkillPanel, OffLine 등 자식 오브젝트도 함께 이동됨
    // ─────────────────────────────────────────
    public void ToggleInventory()
    {
        if (panelRect == null)
        {
            if (inventoryPanel != null)
                panelRect = inventoryPanel.GetComponent<RectTransform>();
            // ★ Bug Fix: panelRect를 못 찾으면 return하되 isPanelOpen은 바꾸지 않음
            // (이전 코드: isPanelOpen 토글 전에 return → 다음 호출 시 상태 역전 버그)
            if (panelRect == null)
            {
                Debug.LogWarning("[InventoryManager] panelRect를 찾을 수 없어 토글을 건너뜁니다.");
                return;
            }
        }

        isPanelOpen = !isPanelOpen;
        float targetY = isPanelOpen ? openPosY : closedPosY;

        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);

        slideCoroutine = StartCoroutine(SlidePanel(targetY));

        // ★ 채팅창 연동
        // 인벤 닫힘(내려감) → 채팅 표시
        // 인벤 열림(올라옴) → 채팅 숨김
        if (chatSystem != null)
        {
            if (!isPanelOpen) chatSystem.ShowChat();
            else chatSystem.HideChat();
        }
        else if (ChatSystem.Instance != null)
        {
            if (!isPanelOpen) ChatSystem.Instance.ShowChat();
            else ChatSystem.Instance.HideChat();
        }

        Debug.Log($"[InventoryManager] 인벤 패널 {(isPanelOpen ? "열기" : "닫기")} → Y={targetY}");
    }

    public void OpenInventory()
    {
        if (isPanelOpen) return;
        ToggleInventory();
    }

    public void CloseInventory()
    {
        if (!isPanelOpen) return;
        ToggleInventory();
    }

    // EaseOut 슬라이드 코루틴 - InvenPanel + linkedPanels 전부 이동
    private IEnumerator SlidePanel(float targetY)
    {
        float startY = panelRect.anchoredPosition.y;
        bool isOpening = (targetY == openPosY);

        // linkedPanels 시작값 / 목표값
        float[] linkedStartY = null;
        float[] linkedTargetY = null;

        if (linkedPanels != null && linkedOpenPosY != null)
        {
            linkedStartY = new float[linkedPanels.Length];
            linkedTargetY = new float[linkedPanels.Length];
            for (int i = 0; i < linkedPanels.Length; i++)
            {
                if (linkedPanels[i] == null) continue;
                linkedStartY[i] = linkedPanels[i].anchoredPosition.y;
                linkedTargetY[i] = isOpening ? linkedOpenPosY[i] : linkedClosedPosY[i];
            }
        }

        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic

            // InvenPanel 이동
            Vector2 pos = panelRect.anchoredPosition;
            pos.y = Mathf.Lerp(startY, targetY, eased);
            panelRect.anchoredPosition = pos;

            // linkedPanels 이동 (앵커 고정 문제를 코드로 강제 이동)
            if (linkedPanels != null && linkedStartY != null)
            {
                for (int i = 0; i < linkedPanels.Length; i++)
                {
                    if (linkedPanels[i] == null) continue;
                    Vector2 lp = linkedPanels[i].anchoredPosition;
                    lp.y = Mathf.Lerp(linkedStartY[i], linkedTargetY[i], eased);
                    linkedPanels[i].anchoredPosition = lp;
                }
            }

            yield return null;
        }

        // 최종값 정확히 고정
        Vector2 finalPos = panelRect.anchoredPosition;
        finalPos.y = targetY;
        panelRect.anchoredPosition = finalPos;

        if (linkedPanels != null && linkedTargetY != null)
        {
            for (int i = 0; i < linkedPanels.Length; i++)
            {
                if (linkedPanels[i] == null) continue;
                Vector2 lp = linkedPanels[i].anchoredPosition;
                lp.y = linkedTargetY[i];
                linkedPanels[i].anchoredPosition = lp;
            }
        }

        slideCoroutine = null;
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

    // ─────────────────────────────────────────
    // 저장/로드용 메서드 (SaveLoadManager 연동)
    // ─────────────────────────────────────────
    public InventoryItemData[] GetInventoryData()
    {
        List<InventoryItemData> dataList = new List<InventoryItemData>();

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.itemData == null) continue;

            dataList.Add(new InventoryItemData
            {
                itemID = slot.itemData.itemID,
                count = slot.itemCount,
                slotIndex = i
            });
        }

        Debug.Log($"[InventoryManager] 저장 데이터 수집: {dataList.Count}개");
        return dataList.ToArray();
    }

    public void LoadInventoryData(InventoryItemData[] items)
    {
        if (items == null || items.Length == 0)
        {
            Debug.Log("[InventoryManager] 로드할 인벤토리 데이터 없음");
            return;
        }

        // 기존 슬롯 초기화
        ClearAllItems();

        foreach (InventoryItemData data in items)
        {
            ItemData itemData = ItemDatabase.Instance?.GetItemByID(data.itemID);
            if (itemData == null)
            {
                // ItemDatabase에 없으면 장비쪽도 확인
                EquipmentData eqData = ItemDatabase.Instance?.GetEquipmentByID(data.itemID);
                if (eqData != null) itemData = eqData;
            }

            if (itemData == null)
            {
                Debug.LogWarning($"[InventoryManager] 아이템 ID {data.itemID} 를 찾을 수 없음!");
                continue;
            }

            // 지정 슬롯에 직접 배치
            if (data.slotIndex >= 0 && data.slotIndex < slots.Count)
            {
                slots[data.slotIndex].AddItem(itemData, data.count, 0);
            }
            else
            {
                AddItem(itemData, data.count);
            }
        }

        Debug.Log($"[InventoryManager] 인벤토리 로드 완료: {items.Length}개");
    }
}