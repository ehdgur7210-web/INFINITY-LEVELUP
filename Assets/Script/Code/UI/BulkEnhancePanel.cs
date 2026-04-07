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

    [Header("하단 정보")]
    [SerializeField] private TextMeshProUGUI infoText;       // "강화수치 +5 / 50개 / 비용 5,000G"
    [SerializeField] private Button bulkEnhanceButton;       // 일괄 강화 버튼
    [SerializeField] private Button autoEnhanceButton;       // 자동 강화 버튼 (Phase 2용)

    [Header("설정")]
    [SerializeField] private int maxEnhanceLevel = 20;       // +0 ~ +20
    [SerializeField] private int maxBulkCount = 50;          // 한 번에 강화할 최대 개수

    // 런타임 상태
    private List<BulkFilterButton> _filterButtons = new List<BulkFilterButton>();
    private List<BulkEnhanceSlot> _slots = new List<BulkEnhanceSlot>();
    private int _currentFilter = 0;  // 현재 선택된 강화수치
    private List<DisplayEntry> _currentDisplayList = new List<DisplayEntry>();

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
            autoEnhanceButton.onClick.AddListener(OnAutoEnhanceClicked);

        BuildFilterButtons();
    }

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

        // 기존 슬롯 정리
        foreach (var s in _slots)
            if (s != null) Destroy(s.gameObject);
        _slots.Clear();

        foreach (var entry in _currentDisplayList)
        {
            GameObject go = Instantiate(slotPrefab, slotContainer);
            BulkEnhanceSlot slot = go.GetComponent<BulkEnhanceSlot>();
            if (slot == null) slot = go.AddComponent<BulkEnhanceSlot>();
            slot.Setup(entry.equipment, entry.instance);
            _slots.Add(slot);
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

        // 비용 확인 (CropPoint)
        long currentCp = FarmManager.Instance != null ? FarmManager.Instance.GetCropPoints() : 0;
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
        // CropPoint 사전 검증 (확인 다이얼로그 사이에 잔량이 변했을 수 있음)
        long curCp = FarmManager.Instance != null ? FarmManager.Instance.GetCropPoints() : 0;
        if (totalCpCost > 0 && (FarmManager.Instance == null || curCp < totalCpCost))
        {
            UIManager.Instance?.ShowMessage("작물 포인트 부족", Color.red);
            return;
        }

        // 골드 차감
        if (GameManager.Instance == null || !GameManager.Instance.SpendGold(totalGoldCost))
        {
            UIManager.Instance?.ShowMessage("골드 차감 실패", Color.red);
            return;
        }

        // CropPoint 차감
        if (totalCpCost > 0)
        {
            FarmManager.Instance.SpendCropPoints(totalCpCost);
        }

        // 성공률 계산용
        int success = 0, fail = 0;

        // 처음 N개에 대해 강화 시도
        for (int i = 0; i < targetCount && i < _currentDisplayList.Count; i++)
        {
            var entry = _currentDisplayList[i];
            if (entry == null || entry.instance == null) continue;

            bool ok = RollEnhanceSuccess(_currentFilter);

            if (ok)
            {
                entry.instance.enhanceLevel++;
                success++;

                // 업적: 전체 강화 카운트 + 부위별 카운트
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

        // 사운드 (1회만 — 다중 인스턴스 강화는 결과 우세 기준으로 재생)
        if (success > 0)
            SoundManager.Instance?.PlayEnhanceSuccess();
        else if (fail > 0)
            SoundManager.Instance?.PlayEnhanceFail();

        // 결과 메시지
        UIManager.Instance?.ShowMessage(
            $"일괄강화완료!\n성공:{success}\n실패:{fail}",
            success > fail ? Color.green : Color.yellow);

        // 저장
        SaveLoadManager.Instance?.SaveGame();

        // UI 갱신
        RefreshFilterButtons();
        SelectFilter(_currentFilter);
        CombatPowerManager.Instance?.Recalculate();
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
    // 자동 강화 (Phase 2 — 추후 구현)
    // ═══════════════════════════════════════════════

    private void OnAutoEnhanceClicked()
    {
        UIManager.Instance?.ShowMessage("자동강화는준비중입니다.", Color.yellow);
        // TODO: 목표 강화수치까지 자동 루프 강화
    }
}
