using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 선택권 전용 패널
///
/// 선택권 아이템 사용 시:
///   ItemDatabase에서 레어리티 조건(Legendary 이상)에 맞는 EquipmentData 목록 표시
///   선택 확인 시 해당 장비를 InventoryManager에 추가 + 선택권 소비
///
/// [프리팹 Hierarchy]
///   SelectionPanel (Panel + SelectionPanel.cs)
///   ├── Dimmer (Image, 반투명 배경)
///   └── ContentPanel
///       ├── TitleText (TextMeshProUGUI) — "장비 선택"
///       ├── DescriptionText (TextMeshProUGUI) — 선택권 설명
///       ├── ScrollView
///       │   └── Content (GridLayout)
///       │       └── SelectionSlot (프리팹, 동적 생성)
///       │           ├── SlotBg (Image)
///       │           ├── SlotIcon (Image)
///       │           ├── SlotName (TextMeshProUGUI)
///       │           ├── SlotRarity (TextMeshProUGUI)
///       │           └── SelectButton / Toggle
///       ├── ConfirmButton (Button) — 선택 확인
///       └── CloseButton (Button)
/// </summary>
public class SelectionPanel : MonoBehaviour
{
    public static SelectionPanel Instance;

    [Header("패널")]
    public GameObject selectionPanel;

    [Header("텍스트")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    [Header("슬롯 목록")]
    [Tooltip("슬롯이 생성될 Content (ScrollView > Viewport > Content)")]
    public RectTransform slotContainer;
    [Tooltip("선택 슬롯 프리팹 (SelectionSlotUI 컴포넌트 포함)")]
    public GameObject slotPrefab;

    [Header("버튼")]
    public Button confirmButton;
    public TextMeshProUGUI confirmButtonText;
    public Button closeButton;

    [Header("설정")]
    [Tooltip("선택 가능한 최소 레어리티 (기본: Legendary)")]
    public ItemRarity minimumRarity = ItemRarity.Legendary;

    // ── 내부 상태 ──
    private ItemData ticketItem;
    private int ticketSlotIndex;
    private List<EquipmentData> availableEquipments = new List<EquipmentData>();
    private List<SelectionSlotUI> slotInstances = new List<SelectionSlotUI>();
    private EquipmentData selectedEquipment;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (selectionPanel != null) selectionPanel.SetActive(false);

        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    // ═══════════════════════════════════════════════════════════════
    //  열기 / 닫기
    // ═══════════════════════════════════════════════════════════════

    /// <summary>선택권 아이템으로 패널 열기</summary>
    public void Open(ItemData ticket, int slotIndex)
    {
        if (ticket == null) return;

        ticketItem = ticket;
        ticketSlotIndex = slotIndex;
        selectedEquipment = null;

        if (selectionPanel != null) selectionPanel.SetActive(true);

        // 레어리티 조건 장비 수집
        BuildEquipmentList();
        BuildSlots();
        RefreshUI();
    }

    public void Close()
    {
        if (selectionPanel != null) selectionPanel.SetActive(false);
        ticketItem = null;
        selectedEquipment = null;
        ClearSlots();
    }

    // ═══════════════════════════════════════════════════════════════
    //  장비 목록 구축
    // ═══════════════════════════════════════════════════════════════

    /// <summary>ItemDatabase에서 minimumRarity 이상 장비 수집</summary>
    private void BuildEquipmentList()
    {
        availableEquipments.Clear();

        if (ItemDatabase.Instance == null)
        {
            Debug.LogWarning("[SelectionPanel] ItemDatabase.Instance가 null!");
            return;
        }

        foreach (var eq in ItemDatabase.Instance.allEquipments)
        {
            if (eq == null) continue;
            if ((int)eq.rarity >= (int)minimumRarity)
                availableEquipments.Add(eq);
        }

        // allItems에서도 EquipmentData 추가 수집 (중복 제거)
        HashSet<int> ids = new HashSet<int>();
        foreach (var eq in availableEquipments)
            ids.Add(eq.itemID);

        foreach (var item in ItemDatabase.Instance.allItems)
        {
            if (item is EquipmentData eqd && !ids.Contains(eqd.itemID))
            {
                if ((int)eqd.rarity >= (int)minimumRarity)
                {
                    availableEquipments.Add(eqd);
                    ids.Add(eqd.itemID);
                }
            }
        }

        // 정렬: 레어도(높→낮) → 타입 → ID
        availableEquipments.Sort((a, b) =>
        {
            int rc = ((int)b.rarity).CompareTo((int)a.rarity);
            if (rc != 0) return rc;
            int tc = ((int)a.equipmentType).CompareTo((int)b.equipmentType);
            if (tc != 0) return tc;
            return a.itemID.CompareTo(b.itemID);
        });

        Debug.Log($"[SelectionPanel] {minimumRarity}+ 장비 {availableEquipments.Count}개 수집");
    }

    // ═══════════════════════════════════════════════════════════════
    //  슬롯 생성/제거
    // ═══════════════════════════════════════════════════════════════

    private void ClearSlots()
    {
        foreach (var slot in slotInstances)
        {
            if (slot != null) Destroy(slot.gameObject);
        }
        slotInstances.Clear();
    }

    private void BuildSlots()
    {
        ClearSlots();

        if (slotContainer == null || slotPrefab == null)
        {
            Debug.LogWarning("[SelectionPanel] slotContainer 또는 slotPrefab이 null!");
            return;
        }

        foreach (var eq in availableEquipments)
        {
            GameObject slotObj = Instantiate(slotPrefab, slotContainer);

            SelectionSlotUI slotUI = slotObj.GetComponent<SelectionSlotUI>();
            if (slotUI != null)
            {
                slotUI.Setup(eq, OnSlotSelected);
                slotInstances.Add(slotUI);
            }
            else
            {
                // SelectionSlotUI가 없으면 직접 구성
                SelectionSlotUI newSlot = slotObj.AddComponent<SelectionSlotUI>();
                newSlot.AutoSetup(eq, OnSlotSelected);
                slotInstances.Add(newSlot);
            }
        }
    }

    /// <summary>슬롯 선택 콜백</summary>
    private void OnSlotSelected(EquipmentData equipment)
    {
        SoundManager.Instance?.PlayButtonClick();
        selectedEquipment = equipment;

        // 선택 하이라이트 갱신
        foreach (var slot in slotInstances)
        {
            if (slot != null)
                slot.SetSelected(slot.equipment == selectedEquipment);
        }

        // 확인 버튼 활성화
        if (confirmButton != null)
            confirmButton.interactable = true;

        if (confirmButtonText != null)
            confirmButtonText.text = $"{equipment.itemName} 선택";
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI 갱신
    // ═══════════════════════════════════════════════════════════════

    private void RefreshUI()
    {
        if (titleText != null)
            titleText.text = "장비 선택";

        if (descriptionText != null)
        {
            string ticketName = ticketItem != null ? ticketItem.itemName : "선택권";
            descriptionText.text = $"{ticketName}을(를) 사용하여\n원하는 장비를 선택하세요.";
        }

        // 확인 버튼 — 미선택 시 비활성화
        if (confirmButton != null)
            confirmButton.interactable = selectedEquipment != null;

        if (confirmButtonText != null)
            confirmButtonText.text = "장비를 선택하세요";

        if (availableEquipments.Count == 0)
        {
            if (descriptionText != null)
                descriptionText.text = "선택 가능한 장비가 없습니다.";
            if (confirmButton != null)
                confirmButton.interactable = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  선택 확인
    // ═══════════════════════════════════════════════════════════════

    private void OnConfirmClicked()
    {
        if (selectedEquipment == null || ticketItem == null) return;

        SoundManager.Instance?.PlayButtonClick();

        // 1. 선택한 장비를 인벤토리에 추가
        bool added = false;
        if (InventoryManager.Instance != null)
            added = InventoryManager.Instance.AddItem(selectedEquipment, 1);

        if (!added)
        {
            UIManager.Instance?.ShowMessage("인벤토리에 장비를 추가할 수 없습니다!", Color.red);
            return;
        }

        // 2. 선택권 1개 소비
        InventoryManager.Instance?.RemoveItem(ticketItem, 1);

        // 3. 완료 메시지
        UIManager.Instance?.ShowMessage(
            $"<color=#{ColorUtility.ToHtmlStringRGB(selectedEquipment.GetRarityColor())}>" +
            $"{selectedEquipment.itemName}</color> 획득!",
            Color.green);

        Debug.Log($"[SelectionPanel] 선택 완료: {selectedEquipment.itemName} (선택권: {ticketItem.itemName} 소비)");

        SaveLoadManager.Instance?.SaveGame();
        Close();
    }
}

// ═══════════════════════════════════════════════════════════════════
//  선택 슬롯 UI (SelectionPanel 내부 슬롯)
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 선택 패널 내 장비 슬롯 UI
///
/// [프리팹 Hierarchy]
///   SelectionSlot (SelectionSlotUI.cs)
///   ├── SlotBg (Image) — 배경/선택 하이라이트
///   ├── SlotIcon (Image) — 장비 아이콘
///   ├── SlotName (TextMeshProUGUI) — 장비 이름
///   ├── SlotRarity (TextMeshProUGUI) — 레어리티
///   └── SlotStats (TextMeshProUGUI) — 간단 스탯 (선택)
/// </summary>
public class SelectionSlotUI : MonoBehaviour
{
    [Header("UI 요소")]
    public Image slotBg;
    public Image slotIcon;
    public TextMeshProUGUI slotName;
    public TextMeshProUGUI slotRarity;
    public TextMeshProUGUI slotStats;
    public Button selectButton;

    [HideInInspector] public EquipmentData equipment;

    private System.Action<EquipmentData> onSelected;
    private bool isSelected = false;

    private static readonly Color NormalBg = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    private static readonly Color SelectedBg = new Color(1f, 0.85f, 0.2f, 0.6f);

    /// <summary>Inspector에서 연결된 UI로 세팅</summary>
    public void Setup(EquipmentData eq, System.Action<EquipmentData> callback)
    {
        equipment = eq;
        onSelected = callback;

        if (slotIcon != null)
        {
            slotIcon.sprite = eq.itemIcon;
            slotIcon.color = Color.white;
        }

        if (slotName != null)
        {
            slotName.text = eq.itemName;
            slotName.color = eq.GetRarityColor();
        }

        if (slotRarity != null)
        {
            slotRarity.text = eq.rarity.ToString();
            slotRarity.color = eq.GetRarityColor();
        }

        if (slotStats != null)
        {
            string s = "";
            if (eq.equipmentStats.attack > 0) s += $"ATK {eq.equipmentStats.attack} ";
            if (eq.equipmentStats.defense > 0) s += $"DEF {eq.equipmentStats.defense} ";
            if (eq.equipmentStats.health > 0) s += $"HP {eq.equipmentStats.health}";
            slotStats.text = s;
        }

        if (slotBg != null)
            slotBg.color = NormalBg;

        // 버튼 바인딩
        if (selectButton != null)
            selectButton.onClick.AddListener(() => onSelected?.Invoke(equipment));
        else
        {
            // Button 컴포넌트가 없으면 자동 추가
            Button btn = GetComponent<Button>();
            if (btn == null) btn = gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onSelected?.Invoke(equipment));
        }

        SetSelected(false);
    }

    /// <summary>UI 컴포넌트 자동 탐색 후 세팅 (프리팹에 SelectionSlotUI가 없을 때)</summary>
    public void AutoSetup(EquipmentData eq, System.Action<EquipmentData> callback)
    {
        // 자식에서 컴포넌트 자동 탐색
        Image[] images = GetComponentsInChildren<Image>(true);
        if (images.Length > 0) slotBg = images[0];
        if (images.Length > 1) slotIcon = images[1];

        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts.Length > 0) slotName = texts[0];
        if (texts.Length > 1) slotRarity = texts[1];
        if (texts.Length > 2) slotStats = texts[2];

        selectButton = GetComponent<Button>();

        Setup(eq, callback);
    }

    /// <summary>선택 하이라이트 설정</summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (slotBg != null)
            slotBg.color = selected ? SelectedBg : NormalBg;
    }
}
