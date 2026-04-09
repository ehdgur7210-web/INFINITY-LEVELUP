using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 일괄 강화 패널
/// - 강화수치 필터 버튼 동적 생성 (+0 ~ +20, 21개)
/// - 같은 강화수치 인스턴스를 필터링해서 표시
/// - 일괄 강화: 표시된 모든 인스턴스를 한 번에 강화 시도
/// - 정렬: 강화수치 내림차순 (고강화 우상단)
/// </summary>
public class BulkEnhancePanel : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject panelRoot;          // 일괄강화 패널 루트
    [SerializeField] private Button closeButton;            // 닫기 X 버튼

    [Header("탭 버튼 (선택)")]
    [SerializeField] private Button singleTabButton;        // 단일 강화 탭
    [SerializeField] private Button bulkTabButton;          // 일괄 강화 탭
    [SerializeField] private GameObject singlePanel;        // 단일 강화 UI 묶음
    [SerializeField] private GameObject bulkPanel;          // 일괄 강화 UI 묶음

    [Header("필터 버튼 영역")]
    [Tooltip("FilterButtonPrefab이 생성될 부모 (Content)")]
    [SerializeField] private Transform filterButtonContainer;
    [SerializeField] private GameObject filterButtonPrefab;

    [Header("장비 슬롯 영역")]
    [Tooltip("BulkSlotPrefab이 생성될 부모 (Content)")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject slotPrefab;

    [Header("등급 필터 (선택)")]
    [Tooltip("등급 선택용 드롭다운 (전체/Common/Uncommon/Rare/Epic/Legendary). 비어두면 항상 '전체' 사용")]
    [SerializeField] private TMP_Dropdown rarityDropdown;

    [Header("부위 필터 (선택)")]
    [Tooltip("부위 선택용 드롭다운 (전체/왼손/오른손/투구/갑옷/장갑/신발). 비어두면 항상 '전체' 사용")]
    [SerializeField] private TMP_Dropdown equipTypeDropdown;

    [Header("하단 정보")]
    [SerializeField] private TextMeshProUGUI infoText;       // "강화수치 +5 / 50개 / 비용 5,000G"
    [SerializeField] private Button bulkEnhanceButton;       // 일괄 강화 버튼
    [SerializeField] private Button autoEnhanceButton;       // 자동 강화 버튼

    [Header("설정")]
    [SerializeField] private int maxEnhanceLevel = 20;       // +0 ~ +20
    [SerializeField] private int maxBulkCount = 50;          // 한 번에 강화할 최대 개수
    [Tooltip("자동 강화 1회 패스 사이 대기 시간(초)")]
    [SerializeField] private float autoEnhanceDelay = 0.15f;
    [Tooltip("자동 강화 안전 상한 (무한 루프 방지)")]
    [SerializeField] private int autoEnhanceMaxPasses = 200;

    // 런타임 상태
    private List<BulkFilterButton> _filterButtons = new List<BulkFilterButton>();
    private List<BulkEnhanceSlot> _slots = new List<BulkEnhanceSlot>();
    private int _currentFilter = 0;                  // 현재 선택된 강화수치
    private ItemRarity? _currentRarity = null;       // null = 전체
    private EquipmentType? _currentEquipType = null; // null = 전체
    private List<DisplayEntry> _currentDisplayList = new List<DisplayEntry>();

    // 자동 강화 상태
    private bool _autoEnhanceRunning = false;
    private Coroutine _autoEnhanceCoroutine;
    private string _autoEnhanceButtonOriginalLabel;

    private class DisplayEntry
    {
        public EquipmentData equipment;
        public InventoryManager.EquipInstance instance;
    }

    private bool _initialized = false;

    void Start()
    {
        EnsureInitialized();

        // 시작 시 패널 닫기 (자기 자신이 아닐 때만)
        if (panelRoot != null && panelRoot != gameObject)
            panelRoot.SetActive(false);
    }

    /// <summary>OpenPanel이 Start보다 먼저 호출될 수 있어 idempotent 초기화</summary>
    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);

        if (singleTabButton != null)
            singleTabButton.onClick.AddListener(() => SwitchTab(false));

        if (bulkTabButton != null)
            bulkTabButton.onClick.AddListener(() => SwitchTab(true));

        if (bulkEnhanceButton != null)
            bulkEnhanceButton.onClick.AddListener(OnBulkEnhanceClicked);

        if (autoEnhanceButton != null)
        {
            autoEnhanceButton.onClick.AddListener(OnAutoEnhanceClicked);
            var lbl = autoEnhanceButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl != null) _autoEnhanceButtonOriginalLabel = lbl.text;
        }

        // 등급 드롭다운 초기화
        if (rarityDropdown != null)
        {
            rarityDropdown.ClearOptions();
            rarityDropdown.AddOptions(new List<string>
            {
                "전체 등급", "노멀", "언커먼", "레어", "에픽", "레전더리"
            });
            rarityDropdown.onValueChanged.RemoveAllListeners();
            rarityDropdown.onValueChanged.AddListener(OnRarityDropdownChanged);
            rarityDropdown.value = 0;
            rarityDropdown.RefreshShownValue();
        }

        // 부위 드롭다운 초기화
        if (equipTypeDropdown != null)
        {
            equipTypeDropdown.ClearOptions();
            equipTypeDropdown.AddOptions(new List<string>
            {
                "전체 부위", "왼손무기", "오른손무기", "투구", "갑옷", "장갑", "신발"
            });
            equipTypeDropdown.onValueChanged.RemoveAllListeners();
            equipTypeDropdown.onValueChanged.AddListener(OnEquipTypeDropdownChanged);
            equipTypeDropdown.value = 0;
            equipTypeDropdown.RefreshShownValue();
        }

        BuildFilterButtons();
    }

    private void OnRarityDropdownChanged(int idx)
    {
        // 0 = 전체, 1~5 = ItemRarity 0~4
        _currentRarity = idx == 0 ? (ItemRarity?)null : (ItemRarity)(idx - 1);
        RefreshFilterButtons();
        SelectFilter(_currentFilter);
    }

    private void OnEquipTypeDropdownChanged(int idx)
    {
        // 0 = 전체, 1~6 = EquipmentType 0~5 (WeaponLeft~Boots)
        _currentEquipType = idx == 0 ? (EquipmentType?)null : (EquipmentType)(idx - 1);
        RefreshFilterButtons();
        SelectFilter(_currentFilter);
    }

    /// <summary>장비가 현재 등급 필터 조건을 만족하는지</summary>
    private bool MatchesRarity(EquipmentData eq)
    {
        if (_currentRarity == null) return true;
        return eq != null && eq.rarity == _currentRarity.Value;
    }

    /// <summary>장비가 현재 부위 필터 조건을 만족하는지</summary>
    private bool MatchesEquipType(EquipmentData eq)
    {
        if (_currentEquipType == null) return true;
        return eq != null && eq.equipmentType == _currentEquipType.Value;
    }

    /// <summary>등급 + 부위 통합 필터</summary>
    private bool MatchesAllFilters(EquipmentData eq) => MatchesRarity(eq) && MatchesEquipType(eq);

    // ═══════════════════════════════════════════════
    // 패널 열기/닫기
    // ═══════════════════════════════════════════════

    public void OpenPanel()
    {
        EnsureInitialized();  // Start보다 먼저 호출돼도 안전
        if (panelRoot != null) panelRoot.SetActive(true);
        SwitchTab(true);  // 일괄 탭으로 시작 (선택)
        RefreshFilterButtons();
        SelectFilter(_currentFilter);
    }

    public void ClosePanel()
    {
        SoundManager.Instance?.PlayButtonClick();

        // 닫기 전: 현재 필터 수치 중 최고 강화 인스턴스를 단일강화 패널에 올리기
        PushHighestEnhancedToSinglePanel();

        // 일괄강화 패널을 닫고 단일강화 패널을 표시 (메인 씬으로 빠지지 않도록)
        if (bulkPanel != null) bulkPanel.SetActive(false);

        if (singlePanel != null)
        {
            singlePanel.SetActive(true);
        }
        else
        {
            // 단일 패널 참조가 없으면 EnhancementSystem 경로로 폴백
            EnhancementSystem.Instance?.ShowEnhancementUI();
        }

        // 일괄강화 패널 자체 루트 닫기 (단, singlePanel과 동일 루트면 닫지 않음)
        if (panelRoot != null && panelRoot != singlePanel)
            panelRoot.SetActive(false);
    }

    /// <summary>현재 인벤토리에서 가장 높은 강화수치를 가진 장비를 단일강화 패널의 선택 대상으로 푸시</summary>
    private void PushHighestEnhancedToSinglePanel()
    {
        if (EnhancementSystem.Instance == null || ItemDatabase.Instance == null) return;

        EquipmentData bestEq = null;
        InventoryManager.EquipInstance bestIns = null;

        foreach (var eq in ItemDatabase.Instance.allEquipments)
        {
            if (eq == null) continue;
            if (!MatchesAllFilters(eq)) continue;
            var instances = GetInstancesForEquipment(eq);
            if (instances == null) continue;
            foreach (var ins in instances)
            {
                if (ins == null) continue;
                if (bestIns == null || ins.enhanceLevel > bestIns.enhanceLevel)
                {
                    bestIns = ins;
                    bestEq = eq;
                }
            }
        }

        if (bestEq != null && bestIns != null)
        {
            int count = InventoryManager.Instance != null
                ? InventoryManager.Instance.GetItemCount(bestEq) : 1;
            EnhancementSystem.Instance.SelectItemForEnhancementDirect(
                bestEq, bestIns.enhanceLevel, bestIns.itemLevel, count);
            Debug.Log($"[BulkEnhancePanel] 단일강화로 푸시: {bestEq.itemName} +{bestIns.enhanceLevel}");
        }
    }

    private void SwitchTab(bool bulkTab)
    {
        if (singlePanel != null) singlePanel.SetActive(!bulkTab);
        if (bulkPanel != null) bulkPanel.SetActive(bulkTab);

        if (bulkTab)
        {
            RefreshFilterButtons();
            SelectFilter(_currentFilter);
        }
    }

    // ═══════════════════════════════════════════════
    // 필터 버튼 생성/갱신
    // ═══════════════════════════════════════════════

    /// <summary>처음 한 번만 호출 — +0 ~ +maxEnhanceLevel 버튼 생성</summary>
    private void BuildFilterButtons()
    {
        if (filterButtonContainer == null || filterButtonPrefab == null)
        {
            Debug.LogWarning("[BulkEnhancePanel] filterButtonContainer 또는 filterButtonPrefab이 비어있음");
            return;
        }

        // 기존 버튼 정리
        foreach (var fb in _filterButtons)
            if (fb != null) Destroy(fb.gameObject);
        _filterButtons.Clear();

        for (int lv = 0; lv <= maxEnhanceLevel; lv++)
        {
            GameObject go = Instantiate(filterButtonPrefab, filterButtonContainer);
            BulkFilterButton fb = go.GetComponent<BulkFilterButton>();
            if (fb == null) fb = go.AddComponent<BulkFilterButton>();
            int captureLevel = lv;
            fb.Setup(lv, 0, OnFilterClicked);
            _filterButtons.Add(fb);
        }
    }

    /// <summary>각 필터 버튼의 보유 개수 갱신</summary>
    private void RefreshFilterButtons()
    {
        var counts = CountInstancesByLevel();
        for (int i = 0; i < _filterButtons.Count; i++)
        {
            int count = counts.ContainsKey(i) ? counts[i] : 0;
            _filterButtons[i].Setup(i, count, OnFilterClicked);
        }
        UpdateFilterSelection();
    }

    /// <summary>강화수치별 인스턴스 개수 집계</summary>
    private Dictionary<int, int> CountInstancesByLevel()
    {
        var dict = new Dictionary<int, int>();

        if (InventoryManager.Instance == null || ItemDatabase.Instance == null) return dict;

        foreach (var eq in ItemDatabase.Instance.allEquipments)
        {
            if (eq == null) continue;
            if (!MatchesAllFilters(eq)) continue;
            var instances = GetInstancesForEquipment(eq);
            if (instances == null) continue;
            foreach (var ins in instances)
            {
                if (ins == null) continue;
                int lv = Mathf.Clamp(ins.enhanceLevel, 0, maxEnhanceLevel);
                if (!dict.ContainsKey(lv)) dict[lv] = 0;
                dict[lv]++;
            }
        }

        return dict;
    }

    /// <summary>InventoryManager에서 특정 장비의 인스턴스 리스트 가져오기 (리플렉션 사용)</summary>
    private List<InventoryManager.EquipInstance> GetInstancesForEquipment(EquipmentData eq)
    {
        if (InventoryManager.Instance == null || eq == null) return null;

        // 리플렉션으로 private equipUnlockMap 접근
        var inv = InventoryManager.Instance;
        var field = typeof(InventoryManager).GetField("equipUnlockMap",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return null;

        var map = field.GetValue(inv) as System.Collections.IDictionary;
        if (map == null || !map.Contains(eq.itemID)) return null;

        var data = map[eq.itemID];
        var instancesField = data.GetType().GetField("instances");
        if (instancesField == null) return null;

        return instancesField.GetValue(data) as List<InventoryManager.EquipInstance>;
    }

    private void OnFilterClicked(int enhanceLevel)
    {
        _currentFilter = enhanceLevel;
        SelectFilter(enhanceLevel);
    }

    private void UpdateFilterSelection()
    {
        for (int i = 0; i < _filterButtons.Count; i++)
            _filterButtons[i].SetSelected(i == _currentFilter);
    }

    // ═══════════════════════════════════════════════
    // 슬롯 빌드
    // ═══════════════════════════════════════════════

    /// <summary>특정 강화수치를 선택해서 인스턴스 표시</summary>
    private void SelectFilter(int enhanceLevel)
    {
        _currentFilter = enhanceLevel;
        UpdateFilterSelection();

        // 디스플레이 리스트 빌드 (해당 강화수치만)
        _currentDisplayList.Clear();

        if (ItemDatabase.Instance != null)
        {
            foreach (var eq in ItemDatabase.Instance.allEquipments)
            {
                if (eq == null) continue;
                if (!MatchesAllFilters(eq)) continue;
                var instances = GetInstancesForEquipment(eq);
                if (instances == null) continue;
                foreach (var ins in instances)
                {
                    if (ins != null && ins.enhanceLevel == enhanceLevel)
                    {
                        _currentDisplayList.Add(new DisplayEntry { equipment = eq, instance = ins });
                    }
                }
            }
        }

        // 정렬: 강화수치 같지만 등급 높은 순으로 (혹은 이름 순)
        _currentDisplayList.Sort((a, b) =>
        {
            int rc = ((int)b.equipment.rarity).CompareTo((int)a.equipment.rarity);
            if (rc != 0) return rc;
            return a.equipment.itemID.CompareTo(b.equipment.itemID);
        });

        BuildSlots();
        UpdateInfo();
    }

    /// <summary>
    /// ★ 슬롯 풀링 방식 — 기존 슬롯 재사용, 부족하면 추가 생성, 남으면 비활성화.
    /// Destroy/Instantiate 폭주 제거.
    /// </summary>
    private void BuildSlots()
    {
        if (slotContainer == null)
        {
            Debug.LogError("[BulkEnhancePanel] ❗ Slot Container가 비어있습니다. Inspector에서 슬롯이 생성될 부모(Transform)를 할당하세요.");
            return;
        }
        if (slotPrefab == null)
        {
            Debug.LogError("[BulkEnhancePanel] ❗ Slot Prefab이 비어있습니다. Inspector에서 슬롯 프리팹을 할당하세요.");
            return;
        }

        int needed = _currentDisplayList.Count;

        // 부족한 만큼만 새로 생성 (기존 슬롯은 그대로 재사용)
        while (_slots.Count < needed)
        {
            GameObject go = Instantiate(slotPrefab, slotContainer);
            BulkEnhanceSlot slot = go.GetComponent<BulkEnhanceSlot>();
            if (slot == null) slot = go.AddComponent<BulkEnhanceSlot>();
            _slots.Add(slot);
        }

        // 필요한 슬롯은 데이터 갱신 + 활성화
        for (int i = 0; i < needed; i++)
        {
            var slot = _slots[i];
            if (slot == null) continue;
            if (!slot.gameObject.activeSelf) slot.gameObject.SetActive(true);
            slot.Setup(_currentDisplayList[i].equipment, _currentDisplayList[i].instance);
        }

        // 남는 슬롯은 비활성화 (다음 호출에서 재사용 가능)
        for (int i = needed; i < _slots.Count; i++)
        {
            if (_slots[i] != null && _slots[i].gameObject.activeSelf)
                _slots[i].gameObject.SetActive(false);
        }
    }

    private void UpdateInfo()
    {
        if (infoText == null) return;

        int displayCount = Mathf.Min(_currentDisplayList.Count, maxBulkCount);
        int totalGoldCost = CalculateBulkCost(_currentFilter, displayCount);
        int totalCpCost = CalculateBulkCropCost(_currentFilter, displayCount);

        infoText.text = $"강화수치 +{_currentFilter} | {displayCount}개 | 비용 {totalGoldCost:N0}G / {totalCpCost}CP";
    }

    private int CalculateBulkCost(int enhanceLevel, int count)
    {
        if (EnhancementSystem.Instance == null) return 0;
        // 단일 강화 비용 × N
        var costMethod = typeof(EnhancementSystem).GetMethod("CalculateEnhanceCost",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (costMethod == null) return 100 * count;
        int singleCost = (int)costMethod.Invoke(EnhancementSystem.Instance, new object[] { enhanceLevel });
        return singleCost * count;
    }

    /// <summary>일괄 강화에 필요한 총 CropPoint 비용</summary>
    private int CalculateBulkCropCost(int enhanceLevel, int count)
    {
        if (EnhancementCropCostPatch.Instance == null || count <= 0) return 0;
        int singleCp = EnhancementCropCostPatch.Instance.GetCropPointCost(enhanceLevel);
        return singleCp * count;
    }

    // ═══════════════════════════════════════════════
    // 일괄 강화 실행
    // ═══════════════════════════════════════════════

    private void OnBulkEnhanceClicked()
    {
        SoundManager.Instance?.PlayButtonClick();

        if (_currentDisplayList == null || _currentDisplayList.Count == 0)
        {
            UIManager.Instance?.ShowMessage("강화할 장비가 없습니다.", Color.yellow);
            return;
        }

        if (_currentFilter >= maxEnhanceLevel)
        {
            UIManager.Instance?.ShowMessage("최대 강화 수치입니다!", Color.yellow);
            return;
        }

        int targetCount = Mathf.Min(_currentDisplayList.Count, maxBulkCount);
        int totalGoldCost = CalculateBulkCost(_currentFilter, targetCount);
        int totalCpCost = CalculateBulkCropCost(_currentFilter, targetCount);

        // 비용 확인 (Gold)
        long currentGold = GameManager.Instance?.PlayerGold ?? 0;
        if (currentGold < totalGoldCost)
        {
            UIManager.Instance?.ShowConfirmDialog(
                $"골드가부족합니다.\n필요:{totalGoldCost:N0}G\n보유:{UIManager.FormatKoreanUnit(currentGold)}G",
                onConfirm: null);
            return;
        }

        // 비용 확인 (CropPoint) — FarmManager 없는 MainScene에서도 동작
        long currentCp = CropPointService.Value;
        if (totalCpCost > 0 && currentCp < totalCpCost)
        {
            UIManager.Instance?.ShowConfirmDialog(
                $"작물포인트가부족합니다.\n필요:{totalCpCost}CP\n보유:{currentCp}CP",
                onConfirm: null);
            return;
        }

        // 확인 다이얼로그
        UIManager.Instance?.ShowConfirmDialog(
            $"+{_currentFilter}장비{targetCount}개를\n일괄강화하시겠습니까?\n비용:{totalGoldCost:N0}G / {totalCpCost}CP",
            onConfirm: () => ExecuteBulkEnhance(targetCount, totalGoldCost, totalCpCost));
    }

    private void ExecuteBulkEnhance(int targetCount, int totalGoldCost, int totalCpCost)
    {
        if (!DoBulkEnhancePass(targetCount, totalGoldCost, totalCpCost, out int success, out int fail, showResultMessage: true))
            return;

        // UI 갱신
        SaveLoadManager.Instance?.SaveGame();
        RefreshFilterButtons();
        SelectFilter(_currentFilter);
        CombatPowerManager.Instance?.Recalculate();

        // ★ 인벤토리 장비 슬롯 강화수치 표시 즉시 갱신
        InventoryManager.Instance?.RefreshEquipDisplay();
    }

    /// <summary>
    /// 단일 일괄강화 패스 — 비용 차감 + 강화 시도. 자동강화에서도 재사용.
    /// </summary>
    /// <returns>차감 성공 여부 (false면 자원 부족 등)</returns>
    private bool DoBulkEnhancePass(int targetCount, int totalGoldCost, int totalCpCost,
        out int success, out int fail, bool showResultMessage)
    {
        success = 0;
        fail = 0;

        // CropPoint 검증 (FarmManager 없는 MainScene에서도 동작)
        long curCp = CropPointService.Value;
        if (totalCpCost > 0 && curCp < totalCpCost)
        {
            UIManager.Instance?.ShowMessage("작물 포인트 부족", Color.red);
            return false;
        }

        // 골드 차감
        if (GameManager.Instance == null || !GameManager.Instance.SpendGold(totalGoldCost))
        {
            UIManager.Instance?.ShowMessage("골드 차감 실패", Color.red);
            return false;
        }

        // CropPoint 차감
        if (totalCpCost > 0)
        {
            CropPointService.Spend(totalCpCost);
        }

        // 강화 시도
        for (int i = 0; i < targetCount && i < _currentDisplayList.Count; i++)
        {
            var entry = _currentDisplayList[i];
            if (entry == null || entry.instance == null) continue;

            bool ok = RollEnhanceSuccess(_currentFilter);

            if (ok)
            {
                entry.instance.enhanceLevel++;
                success++;

                // 업적: 전체 강화 카운트
                AchievementSystem.Instance?.UpdateAchievementProgress(
                    AchievementType.EnhanceEquipment, "", 1);
            }
            else
            {
                if (entry.instance.enhanceLevel > 0)
                    entry.instance.enhanceLevel--;
                fail++;
            }
        }

        // 사운드
        if (success > 0)
            SoundManager.Instance?.PlayEnhanceSuccess();
        else if (fail > 0)
            SoundManager.Instance?.PlayEnhanceFail();

        if (showResultMessage)
        {
            UIManager.Instance?.ShowMessage(
                $"일괄강화완료!\n성공:{success}\n실패:{fail}",
                success > fail ? Color.green : Color.yellow);
        }

        return true;
    }

    private bool RollEnhanceSuccess(int level)
    {
        if (EnhancementSystem.Instance == null) return Random.Range(0f, 100f) < 70f;

        // EnhancementSystem의 successRates 배열 사용
        var ratesField = typeof(EnhancementSystem).GetField("successRates");
        if (ratesField == null) return Random.Range(0f, 100f) < 70f;

        var rates = ratesField.GetValue(EnhancementSystem.Instance) as float[];
        if (rates == null || level >= rates.Length) return Random.Range(0f, 100f) < 30f;

        return Random.Range(0f, 100f) < rates[level];
    }

    // ═══════════════════════════════════════════════
    // 자동 강화 — 자원 소진/리스트 비움까지 반복
    // ═══════════════════════════════════════════════

    private void OnAutoEnhanceClicked()
    {
        SoundManager.Instance?.PlayButtonClick();

        if (_autoEnhanceRunning)
        {
            StopAutoEnhance("사용자 중지");
            return;
        }

        if (_currentDisplayList == null || _currentDisplayList.Count == 0)
        {
            UIManager.Instance?.ShowMessage("강화할 장비가 없습니다.", Color.yellow);
            return;
        }

        if (_currentFilter >= maxEnhanceLevel)
        {
            UIManager.Instance?.ShowMessage("최대 강화 수치입니다!", Color.yellow);
            return;
        }

        // 확인 다이얼로그
        UIManager.Instance?.ShowConfirmDialog(
            $"+{_currentFilter}장비를\n자원이 소진될 때까지\n자동 강화합니다.\n\n시작하시겠습니까?",
            onConfirm: StartAutoEnhance);
    }

    private void StartAutoEnhance()
    {
        if (_autoEnhanceRunning) return;
        _autoEnhanceRunning = true;
        SetAutoEnhanceButtonLabel("중지");
        _autoEnhanceCoroutine = StartCoroutine(AutoEnhanceLoop());
    }

    private void StopAutoEnhance(string reason)
    {
        if (!_autoEnhanceRunning) return;
        _autoEnhanceRunning = false;

        if (_autoEnhanceCoroutine != null)
        {
            StopCoroutine(_autoEnhanceCoroutine);
            _autoEnhanceCoroutine = null;
        }

        SetAutoEnhanceButtonLabel(_autoEnhanceButtonOriginalLabel ?? "자동강화");

        if (!string.IsNullOrEmpty(reason))
            Debug.Log($"[BulkEnhancePanel] 자동강화 종료: {reason}");
    }

    private void SetAutoEnhanceButtonLabel(string text)
    {
        if (autoEnhanceButton == null) return;
        var lbl = autoEnhanceButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (lbl != null) lbl.text = text;
    }

    private IEnumerator AutoEnhanceLoop()
    {
        int totalSuccess = 0, totalFail = 0, passes = 0;
        string stopReason = "완료";

        while (_autoEnhanceRunning && passes < autoEnhanceMaxPasses)
        {
            // 강화할 인스턴스 없음
            if (_currentDisplayList == null || _currentDisplayList.Count == 0)
            {
                stopReason = "강화할 장비 없음";
                break;
            }

            // 최대 강화 도달
            if (_currentFilter >= maxEnhanceLevel)
            {
                stopReason = "최대 강화 수치 도달";
                break;
            }

            int targetCount = Mathf.Min(_currentDisplayList.Count, maxBulkCount);
            int totalGoldCost = CalculateBulkCost(_currentFilter, targetCount);
            int totalCpCost = CalculateBulkCropCost(_currentFilter, targetCount);

            // 자원 사전 확인 (FarmManager 없는 MainScene에서도 동작)
            long currentGold = GameManager.Instance?.PlayerGold ?? 0;
            long currentCp = CropPointService.Value;

            if (currentGold < totalGoldCost)
            {
                stopReason = "골드 부족";
                break;
            }
            if (totalCpCost > 0 && currentCp < totalCpCost)
            {
                stopReason = "작물포인트 부족";
                break;
            }

            // 1패스 실행
            if (!DoBulkEnhancePass(targetCount, totalGoldCost, totalCpCost,
                                    out int passSuccess, out int passFail, showResultMessage: false))
            {
                stopReason = "패스 실패";
                break;
            }

            totalSuccess += passSuccess;
            totalFail += passFail;
            passes++;

            // ★★★ 패스 종료 후 _currentDisplayList를 재필터링!
            //   성공한 아이템은 enhanceLevel이 _currentFilter보다 커져서 더 이상 이 필터에 속하지 않음.
            //   실패한 아이템도 _currentFilter > 0 이면 enhanceLevel--로 이탈함.
            //   재필터 안 하면 다음 패스에서 이미 강화된 아이템이 또 강화돼 폭주(+2/+3/...)함.
            //   슬롯/필터버튼 재생성은 종료 시점에 한 번만 수행 (Instantiate 폭주 방지).
            _currentDisplayList.RemoveAll(e =>
                e == null || e.instance == null || e.instance.enhanceLevel != _currentFilter);

            // 무한 루프 방지: 진전이 없는 경우(전부 실패하고 +0이라 강화수치 변화 없음) 종료
            if (passSuccess == 0 && _currentFilter == 0)
            {
                stopReason = "진전 없음 (+0 실패 반복)";
                break;
            }

            yield return new WaitForSeconds(autoEnhanceDelay);
        }

        if (passes >= autoEnhanceMaxPasses)
            stopReason = "안전 상한 도달";

        // ★ 자동강화 종료 시 1회만 UI 전체 갱신
        //   _currentDisplayList를 명시적으로 비워서 SelectFilter가 새로 빌드하게 함
        //   (stale 데이터 잔존으로 다음 manual 일괄강화가 한 번 빈 클릭 되는 버그 방지)
        _currentDisplayList.Clear();
        RefreshFilterButtons();
        SelectFilter(_currentFilter);
        CombatPowerManager.Instance?.Recalculate();
        InventoryManager.Instance?.RefreshEquipDisplay();

        // 결과 + 저장
        SaveLoadManager.Instance?.SaveGame();

        UIManager.Instance?.ShowMessage(
            $"자동강화 종료\n사유: {stopReason}\n총 성공: {totalSuccess}\n총 실패: {totalFail}",
            Color.cyan);

        StopAutoEnhance(stopReason);
    }

    void OnDisable()
    {
        // 패널이 닫힐 때 자동강화 중이면 강제 종료
        if (_autoEnhanceRunning)
            StopAutoEnhance("패널 비활성화");
    }
}
