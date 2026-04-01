using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 장비 슬롯 클릭 시 표시되는 액션 패널
///
/// 장비 아이콘 + 이름 + 레어도 표시
/// 버튼 3개:
///   [착용하기 / 해제하기] — 현재 착용 상태에 따라 텍스트 변경
///   [레벨업] — EquipmentLevelUpPanel 열기
///   [강화]  — EnhancementSystem 강화 패널 열기
///   [닫기]
///
/// [프리팹 Hierarchy]
///   EquipmentSlotActionPanel (Panel + EquipmentSlotActionPanel.cs)
///   ├── Dimmer (Image, 반투명 배경, 클릭 시 닫기)
///   └── ActionCard (Panel)
///       ├── Header
///       │   ├── EquipIcon (Image) — 장비 아이콘
///       │   ├── EquipName (TextMeshProUGUI) — 장비 이름
///       │   └── RarityText (TextMeshProUGUI) — 레어도
///       ├── StatsText (TextMeshProUGUI) — 간단 스탯 표시
///       ├── ButtonArea
///       │   ├── EquipOrUnequipButton (Button + Text)
///       │   ├── LevelUpButton (Button + Text "레벨업")
///       │   └── EnhanceButton (Button + Text "강화")
///       └── CloseButton (Button + Text "닫기")
///
/// [Inspector 연결]
///   actionPanel         → ActionCard (또는 전체 Panel)
///   equipIcon           → EquipIcon
///   equipNameText       → EquipName
///   rarityText          → RarityText
///   statsText           → StatsText
///   equipOrUnequipButton → EquipOrUnequipButton
///   equipOrUnequipText  → EquipOrUnequipButton > Text
///   levelUpButton       → LevelUpButton
///   enhanceButton       → EnhanceButton
///   closeButton         → CloseButton
/// </summary>
public class EquipmentSlotActionPanel : MonoBehaviour
{
    public static EquipmentSlotActionPanel Instance;

    [Header("패널")]
    public GameObject actionPanel;

    [Header("장비 정보 표시")]
    public Image equipIcon;
    public TextMeshProUGUI equipNameText;
    public TextMeshProUGUI rarityText;
    public TextMeshProUGUI statsText;

    [Header("버튼")]
    public Button equipOrUnequipButton;
    public TextMeshProUGUI equipOrUnequipText;
    public Button levelUpButton;
    public Button enhanceButton;
    public Button closeButton;

    // ── 내부 상태 ──
    private EquipmentSlot currentSlot;
    private EquipmentData currentEquip;
    private bool isCurrentlyEquipped;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] EquipmentSlotActionPanel가 생성되었습니다.");
        }
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (actionPanel != null) actionPanel.SetActive(false);

        if (equipOrUnequipButton != null) equipOrUnequipButton.onClick.AddListener(OnEquipOrUnequipClicked);
        if (levelUpButton != null) levelUpButton.onClick.AddListener(OnLevelUpClicked);
        if (enhanceButton != null) enhanceButton.onClick.AddListener(OnEnhanceClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    // ═══════════════════════════════════════════════════════════════
    //  열기 / 닫기
    // ═══════════════════════════════════════════════════════════════

    /// <summary>장비 슬롯 클릭 시 호출</summary>
    public void Open(EquipmentSlot slot, EquipmentData equip)
    {
        if (slot == null || equip == null) return;

        currentSlot = slot;
        currentEquip = equip;

        if (actionPanel != null) actionPanel.SetActive(true);

        RefreshUI();
    }

    public void Close()
    {
        if (actionPanel != null) actionPanel.SetActive(false);
        currentSlot = null;
        currentEquip = null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI 갱신
    // ═══════════════════════════════════════════════════════════════

    private void RefreshUI()
    {
        if (currentEquip == null || currentSlot == null) return;

        // ── 아이콘 ──
        if (equipIcon != null)
        {
            equipIcon.sprite = currentEquip.itemIcon;
            equipIcon.color = Color.white;
        }

        // ── 이름 ──
        if (equipNameText != null)
        {
            string enhStr = currentSlot.enhanceLevel > 0 ? $" +{currentSlot.enhanceLevel}" : "";
            string lvStr = currentSlot.itemLevel > 0 ? $" Lv.{currentSlot.itemLevel}" : "";
            equipNameText.text = $"{currentEquip.itemName}{enhStr}{lvStr}";
            equipNameText.color = currentEquip.GetRarityColor();
        }

        // ── 레어도 ──
        if (rarityText != null)
        {
            rarityText.text = currentEquip.rarity.ToString();
            rarityText.color = currentEquip.GetRarityColor();
        }

        // ── 간단 스탯 ──
        if (statsText != null)
        {
            EquipmentStats stats = currentEquip.GetLeveledStats(currentSlot.itemLevel);
            float enhMult = 1f + (currentSlot.enhanceLevel * 0.1f);
            string s = "";
            int atk = Mathf.RoundToInt(stats.attack * enhMult);
            int def = Mathf.RoundToInt(stats.defense * enhMult);
            int hp = Mathf.RoundToInt(stats.health * enhMult);
            if (atk > 0) s += $"공격력 {atk}  ";
            if (def > 0) s += $"방어력 {def}  ";
            if (hp > 0) s += $"체력 {hp}";
            statsText.text = s;
        }

        // ── 착용/해제 버튼 ──
        isCurrentlyEquipped = EquipmentManager.Instance != null &&
                              EquipmentManager.Instance.IsEquippedByID(currentEquip.itemID);

        if (equipOrUnequipText != null)
            equipOrUnequipText.text = isCurrentlyEquipped ? "해제하기" : "착용하기";

        if (equipOrUnequipButton != null)
        {
            // 잠금 상태이거나 수량 0이면 버튼 비활성화 (해제는 항상 가능)
            equipOrUnequipButton.interactable = isCurrentlyEquipped || (currentSlot.isUnlocked && currentSlot.itemCount > 0);
        }

        // ── 레벨업 버튼 ──
        if (levelUpButton != null)
        {
            bool canLevelUp = currentSlot.itemLevel < currentEquip.maxItemLevel;
            levelUpButton.interactable = canLevelUp;
        }

        // ── 강화 버튼 ──
        if (enhanceButton != null)
            enhanceButton.interactable = currentSlot.isUnlocked;
    }

    // ═══════════════════════════════════════════════════════════════
    //  버튼 동작
    // ═══════════════════════════════════════════════════════════════

    private void OnEquipOrUnequipClicked()
    {
        if (currentSlot == null || currentEquip == null) return;
        SoundManager.Instance?.PlayEquip();

        if (isCurrentlyEquipped)
        {
            // 해제
            EquipmentManager.Instance?.UnequipItem(currentEquip.equipmentType);
            UIManager.Instance?.ShowMessage($"{currentEquip.itemName} 해제!", Color.white);
        }
        else
        {
            // 착용
            if (EquipmentManager.Instance != null)
            {
                InventoryManager.Instance?.RemoveItem(currentEquip, 1);
                EquipmentManager.Instance.EquipItem(
                    currentEquip, currentSlot.enhanceLevel, currentSlot.itemLevel);
                UIManager.Instance?.ShowMessage($"{currentEquip.itemName} 장착!", Color.green);
            }
            else
            {
                Debug.LogError("[EquipmentSlotActionPanel] EquipmentManager.Instance가 null!");
                UIManager.Instance?.ShowMessage("장비 시스템을 찾을 수 없습니다", Color.red);
            }
        }

        // 인벤토리 UI 갱신
        InventoryManager.Instance?.RefreshEquipDisplay();

        // 전투력 재계산
        CombatPowerManager.Instance?.Recalculate();

        Close();
    }

    private void OnLevelUpClicked()
    {
        if (currentSlot == null || currentEquip == null) return;
        SoundManager.Instance?.PlayButtonClick();

        // Instance가 없으면 비활성 오브젝트에서 탐색
        if (EquipmentLevelUpPanel.Instance == null)
        {
            var found = FindObjectOfType<EquipmentLevelUpPanel>(true);
            if (found != null)
                found.gameObject.SetActive(true);
        }

        if (EquipmentLevelUpPanel.Instance != null)
        {
            EquipmentLevelUpPanel.Instance.Open(currentSlot, currentEquip, currentSlot.itemLevel);
            Close();
        }
        else
        {
            Debug.LogWarning("[EquipmentSlotActionPanel] EquipmentLevelUpPanel이 씬에 없습니다. Inspector에서 배치해주세요.");
            UIManager.Instance?.ShowMessage("레벨업 패널을 찾을 수 없습니다", Color.red);
        }
    }

    private void OnEnhanceClicked()
    {
        if (currentSlot == null || currentEquip == null) return;
        SoundManager.Instance?.PlayButtonClick();

        // EnhancementSystem 강화 패널 열기
        if (EnhancementSystem.Instance != null)
        {
            // InventorySlot을 기대하는 기존 API와 호환하기 위해
            // EquipmentSlot의 데이터를 InventorySlot 형태로 전달
            InventorySlot legacySlot = currentSlot.GetComponent<InventorySlot>();
            if (legacySlot != null)
            {
                EnhancementSystem.Instance.SelectItemForEnhancement(legacySlot);
            }
            else
            {
                // EquipmentSlot → EnhancementSystem 직접 연동
                EnhancementSystem.Instance.SelectItemForEnhancementDirect(
                    currentEquip, currentSlot.enhanceLevel, currentSlot.itemLevel,
                    currentSlot.itemCount);
            }
            Close();
        }
        else
        {
            UIManager.Instance?.ShowMessage("강화 시스템을 찾을 수 없습니다", Color.red);
        }
    }
}
