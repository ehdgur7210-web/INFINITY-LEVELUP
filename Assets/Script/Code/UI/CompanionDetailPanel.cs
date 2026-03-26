using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
// CompanionDetailPanel.cs
//
// 동료 레벨업 패널 (슬롯 클릭 시 바로 열림)
//
// ★ 레이아웃:
//   ┌─────────────────────────────────────────────────┐
//   │ [X닫기]                                         │
//   │                                                 │
//   │  좌측: 이름 / Lv.5 / ★★★                      │
//   │        [━━━━━━━ EXP 바 ━━━━━━━]                 │
//   │        공격력: 100 → 120                        │
//   │        공격속도: 1.0 → 1.05                     │
//   │                                                 │
//   │  중앙: 동료 대형 초상화                          │
//   │                                                 │
//   │  우측: 동료 목록 (CompanionMiniSlot 스크롤)      │
//   │                                                 │
//   ├─────────────────────────────────────────────────┤
//   │  재료 슬롯 (가로 스크롤)                         │
//   │  [재료1] [재료2] [재료3]  획득 EXP: +1500       │
//   │                          비용: 5,000골드         │
//   ├─────────────────────────────────────────────────┤
//   │       [레벨업]        [맥스 레벨업]              │
//   └─────────────────────────────────────────────────┘
//
// ★ 사용법:
//   CompanionDetailPanel.Instance.Open(companionData);
// ═══════════════════════════════════════════════════════════════════

public class CompanionDetailPanel : MonoBehaviour
{
    public static CompanionDetailPanel Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────
    //  Inspector 필드
    // ─────────────────────────────────────────────────────────────

    [Header("===== 패널 =====")]
    [SerializeField] private Button closeButton;

    [Header("===== 서브 탭 (스킬/상세/승성 오버레이 패널) =====")]
    [Tooltip("레벨업 화면의 탭 버튼 3개 (스킬 / 상세 / 승성)")]
    [SerializeField] private Button skillTabButton;
    [SerializeField] private Button detailTabButton;
    [SerializeField] private Button ascensionTabButton;
    [Tooltip("각 탭의 오버레이 패널 (CompanionDetailPanel과 같은 크기, 위에 덮임)")]
    [SerializeField] private GameObject skillPanel;
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private GameObject ascensionPanel;
    [Tooltip("각 패널 안의 닫기(뒤로가기) 버튼")]
    [SerializeField] private Button skillCloseButton;
    [SerializeField] private Button detailCloseButton;
    [SerializeField] private Button ascensionCloseButton;

    [Header("===== 동료 정보 (좌측 상단) =====")]
    [SerializeField] private Image companionIcon;
    [SerializeField] private TextMeshProUGUI companionNameText;
    [SerializeField] private TextMeshProUGUI companionLevelText;
    [SerializeField] private TextMeshProUGUI companionGradeText;

    [Header("===== 경험치 바 =====")]
    [SerializeField] private Slider expSlider;
    [SerializeField] private TextMeshProUGUI expText;               // "1250 / 3000"

    [Header("===== 스탯 미리보기 =====")]
    [SerializeField] private TextMeshProUGUI atkPreviewText;        // "공격력: 100 → 120"
    [SerializeField] private TextMeshProUGUI spdPreviewText;        // "공격속도: 1.0 → 1.05"

    [Header("===== 재료 슬롯 =====")]
    [SerializeField] private Transform materialSlotParent;
    [SerializeField] private GameObject materialSlotPrefab;
    [SerializeField] private int maxMaterialSlots = 3;

    [Header("===== 재료 경험치/비용 =====")]
    [SerializeField] private TextMeshProUGUI totalExpGainText;      // "획득 경험치: +1500"
    [SerializeField] private TextMeshProUGUI goldCostText;          // "비용: 5,000 골드"

    [Header("===== 버튼 =====")]
    [SerializeField] private Button levelUpButton;
    [SerializeField] private Button maxLevelUpButton;

    [Header("===== 우측 동료 목록 =====")]
    [SerializeField] private GameObject companionListPanel;
    [SerializeField] private Transform companionListContent;
    [SerializeField] private GameObject companionSlotPrefab;

    [Header("===== 레벨업 설정 =====")]
    [SerializeField] private int maxLevel = 100;
    [SerializeField] private int baseExpRequired = 100;
    [SerializeField] private float expScale = 1.5f;
    [SerializeField] private int baseGoldCost = 500;
    [SerializeField] private float statGrowthRate = 0.1f;

    // ─────────────────────────────────────────────────────────────
    //  내부 변수
    // ─────────────────────────────────────────────────────────────

    private CompanionData _currentCompanion;
    private int _currentLevel = 1;
    private int _currentExp = 0;
    private readonly List<CompanionMaterialSlot> _materialSlots = new List<CompanionMaterialSlot>();
    private readonly List<GameObject> _spawnedCompanionSlots = new List<GameObject>();
    private bool _materialSlotsBuilt = false;

    // ─────────────────────────────────────────────────────────────
    //  Unity 라이프사이클
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (levelUpButton != null)
            levelUpButton.onClick.AddListener(OnLevelUpClicked);
        if (maxLevelUpButton != null)
            maxLevelUpButton.onClick.AddListener(OnMaxLevelUpClicked);

        // 서브 탭 열기 버튼
        if (skillTabButton != null)
            skillTabButton.onClick.AddListener(() => OpenSubPanelWithRefresh(skillPanel));
        if (detailTabButton != null)
            detailTabButton.onClick.AddListener(() => OpenSubPanelWithRefresh(detailPanel));
        if (ascensionTabButton != null)
            ascensionTabButton.onClick.AddListener(() => OpenSubPanelWithRefresh(ascensionPanel));

        // 서브 탭 닫기 버튼 (레벨업 화면으로 복귀)
        if (skillCloseButton != null)
            skillCloseButton.onClick.AddListener(CloseAllSubPanels);
        if (detailCloseButton != null)
            detailCloseButton.onClick.AddListener(CloseAllSubPanels);
        if (ascensionCloseButton != null)
            ascensionCloseButton.onClick.AddListener(CloseAllSubPanels);

        // 시작 시 서브 패널 숨김
        CloseAllSubPanels();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─────────────────────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────────────────────

    /// <summary>동료 레벨업 패널을 엽니다. 슬롯 클릭 시 바로 호출.</summary>
    public void Open(CompanionData companion)
    {
        if (companion == null) return;

        _currentCompanion = companion;
        _currentLevel = GetCompanionLevel(companion);
        _currentExp = GetCurrentExp(companion);

        gameObject.SetActive(true);

        BuildMaterialSlots();
        ClearAllMaterialSlots();
        BuildCompanionList();
        RefreshUI();
        CloseAllSubPanels(); // 항상 레벨업 화면으로 시작

        SoundManager.Instance?.PlayPanelOpen();
    }

    /// <summary>패널을 닫습니다.</summary>
    public void Close()
    {
        SaveLoadManager.Instance?.SaveGame();
        gameObject.SetActive(false);
        SoundManager.Instance?.PlayPanelClose();
    }

    /// <summary>서브 패널 열기 + 탭 데이터 Refresh (레벨업 위에 덮임)</summary>
    private void OpenSubPanelWithRefresh(GameObject panel)
    {
        if (panel == null) return;
        CloseAllSubPanels();
        panel.SetActive(true);

        // 각 탭 컴포넌트에 현재 동료 데이터 전달
        if (_currentCompanion != null)
        {
            if (panel == skillPanel)
            {
                var tab = panel.GetComponent<CompanionSkillUpTab>();
                tab?.Refresh(_currentCompanion, _currentLevel);
            }
            else if (panel == detailPanel)
            {
                var tab = panel.GetComponent<CompanionDetailTab>();
                tab?.Refresh(_currentCompanion, _currentLevel);
            }
            else if (panel == ascensionPanel)
            {
                var tab = panel.GetComponent<CompanionAscensionTab>();
                tab?.Refresh(_currentCompanion, _currentLevel);
            }
        }

        SoundManager.Instance?.PlayButtonClick();
    }

    /// <summary>모든 서브 패널 닫기 → 레벨업 화면으로 복귀</summary>
    public void CloseAllSubPanels()
    {
        if (skillPanel != null) skillPanel.SetActive(false);
        if (detailPanel != null) detailPanel.SetActive(false);
        if (ascensionPanel != null) ascensionPanel.SetActive(false);
        SoundManager.Instance?.PlayButtonClick();
    }

    /// <summary>우측 목록에서 다른 동료 클릭 시 전환.</summary>
    public void SwitchCompanion(CompanionData companion)
    {
        if (companion == null) return;
        _currentCompanion = companion;
        _currentLevel = GetCompanionLevel(companion);
        _currentExp = GetCurrentExp(companion);
        ClearAllMaterialSlots();
        RefreshUI();
        RefreshOpenSubTab();
    }

    // ─────────────────────────────────────────────────────────────
    //  UI 갱신
    // ─────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        if (_currentCompanion == null) return;

        // 동료 정보
        if (companionIcon != null && _currentCompanion.portrait != null)
        {
            companionIcon.sprite = _currentCompanion.portrait;
            companionIcon.color = Color.white;
        }
        if (companionNameText != null)
            companionNameText.text = _currentCompanion.companionName;
        if (companionLevelText != null)
            companionLevelText.text = $"Lv.{_currentLevel}";
        if (companionGradeText != null)
        {
            int stars = GetCompanionStars(_currentCompanion);
            companionGradeText.text = StarSpriteUtil.GetColoredStars(Mathf.Max(1, stars));
            companionGradeText.color = StarSpriteUtil.GetStarColor(stars);
        }

        // 경험치 바
        int required = GetRequiredExp(_currentLevel);
        if (expSlider != null)
            expSlider.value = required > 0 ? (float)_currentExp / required : 0f;
        if (expText != null)
            expText.text = $"{_currentExp:N0} / {required:N0}";

        // 스탯 미리보기
        float currentAtk = GetScaledStat(_currentCompanion.attackPower, _currentLevel);
        float nextAtk = GetScaledStat(_currentCompanion.attackPower, _currentLevel + 1);
        if (atkPreviewText != null)
            atkPreviewText.text = $"공격력: {currentAtk:N0} → <color=#00FF00>{nextAtk:N0}</color>";

        float currentSpd = GetScaledStat(_currentCompanion.attackSpeed, _currentLevel);
        float nextSpd = GetScaledStat(_currentCompanion.attackSpeed, _currentLevel + 1);
        if (spdPreviewText != null)
            spdPreviewText.text = $"공격속도: {currentSpd:F2} → <color=#00FF00>{nextSpd:F2}</color>";

        // 재료 경험치/비용
        int totalExpGain = GetTotalMaterialExp();
        if (totalExpGainText != null)
            totalExpGainText.text = totalExpGain > 0
                ? $"획득 경험치: <color=#00FF00>+{totalExpGain:N0}</color>"
                : "재료를 넣어주세요";

        int goldCost = GetGoldCost(_currentLevel);
        if (goldCostText != null)
            goldCostText.text = $"비용: {goldCost:N0} 골드";

        // 버튼 상태
        bool canLevel = _currentLevel < maxLevel && totalExpGain > 0;
        if (levelUpButton != null) levelUpButton.interactable = canLevel;
        if (maxLevelUpButton != null) maxLevelUpButton.interactable = canLevel;
    }

    // ─────────────────────────────────────────────────────────────
    //  재료 슬롯
    // ─────────────────────────────────────────────────────────────

    private void BuildMaterialSlots()
    {
        if (_materialSlotsBuilt) return;
        if (materialSlotParent == null || materialSlotPrefab == null) return;

        for (int i = 0; i < maxMaterialSlots; i++)
        {
            GameObject go = Instantiate(materialSlotPrefab, materialSlotParent);
            CompanionMaterialSlot slot = go.GetComponent<CompanionMaterialSlot>();
            if (slot == null) slot = go.AddComponent<CompanionMaterialSlot>();
            slot.Init(this, i);
            _materialSlots.Add(slot);
        }
        _materialSlotsBuilt = true;
    }

    /// <summary>빈 재료 슬롯 클릭 시 호출 (CompanionMaterialSlot에서)</summary>
    public void OnMaterialSlotClicked(int slotIndex)
    {
        if (InventoryManager.Instance == null)
        {
            UIManager.Instance?.ShowMessage("인벤토리를 찾을 수 없습니다", Color.red);
            return;
        }

        ItemData[] materials = FindMaterialItems();
        if (materials.Length == 0)
        {
            UIManager.Instance?.ShowMessage("사용 가능한 재료가 없습니다", Color.yellow);
            return;
        }

        if (slotIndex >= 0 && slotIndex < _materialSlots.Count)
        {
            var slot = _materialSlots[slotIndex];
            if (slot.IsEmpty)
            {
                foreach (var mat in materials)
                {
                    if (!IsMaterialAlreadyUsed(mat))
                    {
                        int count = InventoryManager.Instance.GetItemCount(mat);
                        int expPerItem = GetMaterialExpValue(mat);
                        slot.SetMaterial(mat, count, expPerItem);
                        OnMaterialChanged();
                        return;
                    }
                }
                UIManager.Instance?.ShowMessage("모든 재료가 이미 투입되어 있습니다", Color.yellow);
            }
        }
    }

    /// <summary>재료 변경 시 호출</summary>
    public void OnMaterialChanged()
    {
        RefreshUI();
    }

    private void ClearAllMaterialSlots()
    {
        foreach (var slot in _materialSlots)
            if (slot != null) slot.Clear();
    }

    // ─────────────────────────────────────────────────────────────
    //  레벨업
    // ─────────────────────────────────────────────────────────────

    private void OnLevelUpClicked()
    {
        if (_currentCompanion == null || _currentLevel >= maxLevel) return;

        int totalExp = GetTotalMaterialExp();
        if (totalExp <= 0)
        {
            UIManager.Instance?.ShowMessage("재료를 넣어주세요!", Color.yellow);
            return;
        }

        int goldCost = GetGoldCost(_currentLevel);
        if (!TrySpendGold(goldCost)) return;

        ConsumeMaterials();

        _currentExp += totalExp;
        int required = GetRequiredExp(_currentLevel);
        while (_currentExp >= required && _currentLevel < maxLevel)
        {
            _currentExp -= required;
            _currentLevel++;
            required = GetRequiredExp(_currentLevel);
        }

        SaveCompanionData();
        SaveLoadManager.Instance?.SaveGame();
        ClearAllMaterialSlots();
        RefreshUI();
        UpdateSelectionHighlight(_currentCompanion);

        SoundManager.Instance?.PlayQuestReward();
        UIManager.Instance?.ShowMessage($"{_currentCompanion.companionName} Lv.{_currentLevel} 달성!", Color.green);
    }

    private void OnMaxLevelUpClicked()
    {
        if (_currentCompanion == null || _currentLevel >= maxLevel) return;

        int totalExp = GetTotalMaterialExp();
        if (totalExp <= 0)
        {
            UIManager.Instance?.ShowMessage("재료를 넣어주세요!", Color.yellow);
            return;
        }

        // 총 골드 비용 미리 계산
        int tempLevel = _currentLevel;
        int tempExp = _currentExp + totalExp;
        int totalGold = 0;

        while (tempLevel < maxLevel)
        {
            int req = GetRequiredExp(tempLevel);
            if (tempExp < req) break;
            totalGold += GetGoldCost(tempLevel);
            tempExp -= req;
            tempLevel++;
        }

        if (totalGold <= 0 || tempLevel == _currentLevel)
        {
            UIManager.Instance?.ShowMessage("경험치가 부족합니다!", Color.yellow);
            return;
        }

        if (!TrySpendGold(totalGold)) return;

        ConsumeMaterials();

        _currentExp += totalExp;
        int startLevel = _currentLevel;
        while (_currentLevel < maxLevel)
        {
            int req = GetRequiredExp(_currentLevel);
            if (_currentExp < req) break;
            _currentExp -= req;
            _currentLevel++;
        }

        SaveCompanionData();
        SaveLoadManager.Instance?.SaveGame();
        ClearAllMaterialSlots();
        RefreshUI();
        UpdateSelectionHighlight(_currentCompanion);

        int gained = _currentLevel - startLevel;
        SoundManager.Instance?.PlayQuestReward();
        UIManager.Instance?.ShowMessage(
            $"{_currentCompanion.companionName} Lv.{_currentLevel} 달성! (+{gained}레벨)", Color.green);
    }

    // ─────────────────────────────────────────────────────────────
    //  우측 동료 목록
    // ─────────────────────────────────────────────────────────────

    private void BuildCompanionList()
    {
        foreach (var go in _spawnedCompanionSlots)
            if (go != null) Destroy(go);
        _spawnedCompanionSlots.Clear();

        if (companionListContent == null || companionSlotPrefab == null) return;

        var invMgr = CompanionInventoryManager.Instance;
        if (invMgr == null) return;

        var saveDataList = invMgr.GetSaveData();
        if (saveDataList == null || saveDataList.Length == 0) return;

        foreach (var save in saveDataList)
        {
            if (save == null) continue;

            CompanionData data = invMgr.FindCompanionData(save.companionID);
            if (data == null) continue;

            GameObject slotGO = Instantiate(companionSlotPrefab, companionListContent);
            _spawnedCompanionSlots.Add(slotGO);

            CompanionMiniSlot miniSlot = slotGO.GetComponent<CompanionMiniSlot>();
            if (miniSlot == null) miniSlot = slotGO.AddComponent<CompanionMiniSlot>();

            int stars = GetCompanionStars(data);
            miniSlot.Setup(data, save.level, stars);

            bool isSelected = _currentCompanion != null && data.companionID == _currentCompanion.companionID;
            miniSlot.SetSelected(isSelected);

            CompanionData captured = data;
            Button btn = slotGO.GetComponent<Button>();
            if (btn == null) btn = slotGO.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                SwitchCompanion(captured);
                UpdateSelectionHighlight(captured);
            });
        }
    }

    private void UpdateSelectionHighlight(CompanionData selected)
    {
        foreach (var go in _spawnedCompanionSlots)
        {
            if (go == null) continue;
            var miniSlot = go.GetComponent<CompanionMiniSlot>();
            if (miniSlot == null) continue;

            bool isThis = miniSlot.GetData() != null
                       && selected != null
                       && miniSlot.GetData().companionID == selected.companionID;
            miniSlot.SetSelected(isThis);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  헬퍼
    // ─────────────────────────────────────────────────────────────

    private bool TrySpendGold(int amount)
    {
        if (GameManager.Instance != null)
        {
            if (!GameManager.Instance.SpendGold(amount))
            {
                UIManager.Instance?.ShowMessage($"골드가 부족합니다! ({amount:N0} 필요)", Color.red);
                return false;
            }
            return true;
        }

        if (GameDataBridge.CurrentData != null)
        {
            if (GameDataBridge.CurrentData.playerGold < amount)
            {
                UIManager.Instance?.ShowMessage($"골드가 부족합니다! ({amount:N0} 필요)", Color.red);
                return false;
            }
            GameDataBridge.CurrentData.playerGold -= amount;
            return true;
        }

        return false;
    }

    private void ConsumeMaterials()
    {
        foreach (var slot in _materialSlots)
        {
            if (slot == null || slot.IsEmpty) continue;
            if (slot.GetItem() != null && InventoryManager.Instance != null)
                InventoryManager.Instance.RemoveItem(slot.GetItem(), slot.GetCount());
        }
    }

    private bool IsMaterialAlreadyUsed(ItemData item)
    {
        foreach (var slot in _materialSlots)
            if (slot != null && !slot.IsEmpty && slot.GetItem() == item)
                return true;
        return false;
    }

    private ItemData[] FindMaterialItems()
    {
        var result = new List<ItemData>();
        if (InventoryManager.Instance == null) return result.ToArray();

        var allItems = InventoryManager.Instance.GetAllItems();
        if (allItems == null) return result.ToArray();

        foreach (var item in allItems)
        {
            if (item != null && item.itemType == ItemType.Material
                && InventoryManager.Instance.GetItemCount(item) > 0)
                result.Add(item);
        }
        return result.ToArray();
    }

    private int GetTotalMaterialExp()
    {
        int total = 0;
        foreach (var slot in _materialSlots)
            if (slot != null && !slot.IsEmpty)
                total += slot.GetExpValue();
        return total;
    }

    private int GetMaterialExpValue(ItemData item)
    {
        if (item == null) return 0;
        switch (item.rarity)
        {
            case ItemRarity.Common:    return 50;
            case ItemRarity.Uncommon:  return 150;
            case ItemRarity.Rare:      return 500;
            case ItemRarity.Epic:      return 1500;
            case ItemRarity.Legendary: return 5000;
            default:                   return 50;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  공식
    // ─────────────────────────────────────────────────────────────

    private int GetRequiredExp(int level)
    {
        return Mathf.RoundToInt(baseExpRequired * Mathf.Pow(level, expScale));
    }

    private int GetGoldCost(int level)
    {
        return baseGoldCost * level;
    }

    private float GetScaledStat(float baseStat, int level)
    {
        return baseStat * (1f + statGrowthRate * (level - 1));
    }

    /// <summary>현재 열려있는 서브 탭이 있으면 Refresh 호출</summary>
    private void RefreshOpenSubTab()
    {
        if (_currentCompanion == null) return;

        if (skillPanel != null && skillPanel.activeSelf)
        {
            var tab = skillPanel.GetComponent<CompanionSkillUpTab>();
            tab?.Refresh(_currentCompanion, _currentLevel);
        }
        else if (detailPanel != null && detailPanel.activeSelf)
        {
            var tab = detailPanel.GetComponent<CompanionDetailTab>();
            tab?.Refresh(_currentCompanion, _currentLevel);
        }
        else if (ascensionPanel != null && ascensionPanel.activeSelf)
        {
            var tab = ascensionPanel.GetComponent<CompanionAscensionTab>();
            tab?.Refresh(_currentCompanion, _currentLevel);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  데이터 연동
    // ─────────────────────────────────────────────────────────────

    private void SaveCompanionData()
    {
        if (_currentCompanion == null) return;
        var invMgr = CompanionInventoryManager.Instance;
        if (invMgr == null) return;

        var entryList = invMgr.GetCompanionList();
        if (entryList == null) return;

        foreach (var entry in entryList)
        {
            if (entry != null && entry.data != null && entry.data.companionID == _currentCompanion.companionID)
            {
                entry.level = _currentLevel;
                entry.exp = _currentExp;
                break;
            }
        }
    }

    private int GetCompanionLevel(CompanionData data)
    {
        if (data == null) return 1;
        var invMgr = CompanionInventoryManager.Instance;
        if (invMgr == null) return 1;

        var entryList = invMgr.GetCompanionList();
        if (entryList == null) return 1;

        foreach (var entry in entryList)
        {
            if (entry != null && entry.data != null && entry.data.companionID == data.companionID)
                return Mathf.Max(1, entry.level);
        }
        return 1;
    }

    private int GetCurrentExp(CompanionData companion)
    {
        if (companion == null) return 0;
        var invMgr = CompanionInventoryManager.Instance;
        if (invMgr == null) return 0;

        var entryList = invMgr.GetCompanionList();
        if (entryList == null) return 0;

        foreach (var entry in entryList)
        {
            if (entry != null && entry.data != null && entry.data.companionID == companion.companionID)
                return Mathf.Max(0, entry.exp);
        }
        return 0;
    }

    private int GetCompanionStars(CompanionData data)
    {
        if (data == null) return 1;
        var invMgr = CompanionInventoryManager.Instance;
        if (invMgr == null) return data.baseStars;

        var entryList = invMgr.GetCompanionList();
        if (entryList == null) return data.baseStars;

        foreach (var entry in entryList)
        {
            if (entry != null && entry.data != null && entry.data.companionID == data.companionID)
                return entry.stars > 0 ? entry.stars : data.baseStars;
        }
        return data.baseStars;
    }

}
