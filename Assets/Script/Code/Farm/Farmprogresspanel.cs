using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
// FarmProgressPanel.cs
//
// ★ FarmProgressSlot은 별도 파일 FarmProgressSlot.cs 로 분리됨
//
// ★ Hierarchy:
//   FarmProgressPanel
//     ├ TabGroup
//     │   ├ TabBtn_Veggie       ← tabVeggieBtn
//     │   └ TabBtn_Fruit        ← tabFruitBtn
//     ├ VeggieScrollView        ← veggieScrollView (GO)
//     │   └ Viewport > Content  ← veggieSlotGrid
//     ├ FruitScrollView         ← fruitScrollView (GO)
//     │   └ Viewport > Content  ← fruitSlotGrid
//     ├ FarmItemSelectPopup     ← sharedSelectPopup
//     └ BtnClose                ← closeButton
// ═══════════════════════════════════════════════════════════════════


public class FarmProgressPanel : MonoBehaviour
{
    public static FarmProgressPanel Instance { get; private set; }

    public enum TabType { Vegetable, Fruit }

    [Header("===== 탭 버튼 =====")]
    [SerializeField] private Button tabVeggieBtn;
    [SerializeField] private Button tabFruitBtn;
    [SerializeField] private Color tabActiveColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color tabInactiveColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("===== 탭별 스크롤뷰 (Show/Hide용) =====")]
    [SerializeField] private GameObject veggieScrollView;  // 채소 작물 스크롤뷰 루트
    [SerializeField] private GameObject fruitScrollView;   // 과일작물 스크롤뷰 루트

    [Header("===== 탭별 Content (슬롯 생성 위치) =====")]
    [SerializeField] private Transform veggieSlotGrid;  // 채소 작물 > Viewport > Content
    [SerializeField] private Transform fruitSlotGrid;   // 과일작물 > Viewport > Content

    [Header("===== 슬롯 프리팹 (공유) =====")]
    [SerializeField] private GameObject slotPrefab;     // 하나의 프리팹을 채소/과일 모두 재사용

    [Header("===== 공유 선택 팝업 =====")]
    [SerializeField] private FarmItemSelectPopup sharedSelectPopup;

    [Header("===== 물 목록 =====")]
    [SerializeField] private List<WaterData> availableWaters = new List<WaterData>();
    public List<WaterData> AvailableWaters => availableWaters;

    [Header("===== 밭 인덱스 =====")]
    [SerializeField] private int[] vegetablePlotIndices = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
    [SerializeField] private int[] fruitPlotIndices = { 9, 10, 11, 12, 13, 14 };

    [Header("===== 닫기 =====")]
    [SerializeField] private Button closeButton;

    private TabType currentTab = TabType.Vegetable;
    private readonly List<FarmProgressSlot> veggieSlots = new List<FarmProgressSlot>();
    private readonly List<FarmProgressSlot> fruitSlots = new List<FarmProgressSlot>();
    private float refreshTimer = 0f;

    // ─── 현재 탭 슬롯 목록 편의 프로퍼티 ─────────────────────────
    private List<FarmProgressSlot> ActiveSlots
        => currentTab == TabType.Vegetable ? veggieSlots : fruitSlots;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        tabVeggieBtn?.onClick.AddListener(() => SwitchTab(TabType.Vegetable));
        tabFruitBtn?.onClick.AddListener(() => SwitchTab(TabType.Fruit));
        closeButton?.onClick.AddListener(ClosePanel);
        FarmManagerExtension.OnPlotStateChangedStatic += OnPlotStateChanged;

        // ★ 패널 활성화 시 양쪽 모두 미리 빌드
        BuildAllSlots();
        SwitchTab(TabType.Vegetable);
    }

    void OnDestroy()
    {
        FarmManagerExtension.OnPlotStateChangedStatic -= OnPlotStateChanged;
    }

    void Update()
    {
        refreshTimer += Time.deltaTime;
        if (refreshTimer >= 1f) { refreshTimer = 0f; RefreshAllSlots(); }
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        SwitchTab(TabType.Vegetable);
        SoundManager.Instance?.PlayPanelOpen();
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        sharedSelectPopup?.Hide();
        SoundManager.Instance?.PlayPanelClose();
    }

    // ═══ 탭 전환 — 슬롯 재생성 없이 스크롤뷰만 Show/Hide ══════════
    private void SwitchTab(TabType tab)
    {
        currentTab = tab;

        // 스크롤뷰 Show/Hide
        veggieScrollView?.SetActive(tab == TabType.Vegetable);
        fruitScrollView?.SetActive(tab == TabType.Fruit);

        // 탭 버튼 색상
        var vImg = tabVeggieBtn?.GetComponent<Image>();
        var fImg = tabFruitBtn?.GetComponent<Image>();
        if (vImg != null) vImg.color = tab == TabType.Vegetable ? tabActiveColor : tabInactiveColor;
        if (fImg != null) fImg.color = tab == TabType.Fruit ? tabActiveColor : tabInactiveColor;

        var vTxt = tabVeggieBtn?.GetComponentInChildren<TextMeshProUGUI>();
        var fTxt = tabFruitBtn?.GetComponentInChildren<TextMeshProUGUI>();
        if (vTxt != null) vTxt.fontStyle = tab == TabType.Vegetable ? FontStyles.Bold : FontStyles.Normal;
        if (fTxt != null) fTxt.fontStyle = tab == TabType.Fruit ? FontStyles.Bold : FontStyles.Normal;
    }

    // ═══ 양쪽 슬롯 한 번에 미리 빌드 ══════════════════════════════
    private void BuildAllSlots()
    {
        BuildSlotsFor(vegetablePlotIndices, veggieSlotGrid, veggieSlots);
        BuildSlotsFor(fruitPlotIndices, fruitSlotGrid, fruitSlots);
    }

    private void BuildSlotsFor(int[] indices, Transform container, List<FarmProgressSlot> slotList)
    {
        // 기존 슬롯 정리
        slotList.Clear();
        if (container == null) return;
        foreach (Transform c in container) Destroy(c.gameObject);

        if (slotPrefab == null || indices == null) return;

        foreach (int idx in indices)
        {
            var go = Instantiate(slotPrefab, container);
            var slot = go.GetComponent<FarmProgressSlot>();
            if (slot != null)
            {
                // ★ 슬롯이 자체적으로 plotIndex에 해당하는 cropIcon을 FarmManager에서 가져옴
                slot.Setup(idx, sharedSelectPopup);
                slotList.Add(slot);
            }
        }
    }

    private void RefreshAllSlots()
    {
        foreach (var s in veggieSlots) s?.Refresh();
        foreach (var s in fruitSlots) s?.Refresh();
    }

    private void OnPlotStateChanged(int _) => RefreshAllSlots();
}