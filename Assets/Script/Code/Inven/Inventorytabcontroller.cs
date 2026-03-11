using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인벤토리 탭 컨트롤러
/// - Tab 키로 탭 전환 (장비 → 농장 → 동료 → ...)
/// - 탭별 슬롯 그리드를 독립적으로 관리
/// - Inspector에서 탭 루트 오브젝트들을 연결
///
/// [사용법]
/// InventoryPanel 아래에 TabHeader, TabContent 구조 만들고
/// 각 탭 버튼/컨텐츠 오브젝트를 Inspector에 연결
/// </summary>
public class InventoryTabController : MonoBehaviour
{
    public static InventoryTabController Instance;

    public enum InvTab { Equipment = 0, Farm = 1, Companion = 2 }

    [Header("탭 버튼 (UI Button)")]
    public Button equipmentTabBtn;
    public Button farmTabBtn;
    public Button companionTabBtn;

    [Header("탭 컨텐츠 루트 오브젝트")]
    public GameObject equipmentContent;   // 기존 슬롯들이 있는 루트
    public GameObject farmContent;        // 농장 아이템 슬롯 루트
    public GameObject companionContent;   // 동료 슬롯 루트

    [Header("탭 버튼 색상")]
    public Color activeTabColor = new Color(1f, 0.8f, 0.2f);
    public Color inactiveTabColor = new Color(0.3f, 0.3f, 0.3f);

    [Header("탭 버튼 텍스트")]
    public TextMeshProUGUI equipmentTabText;
    public TextMeshProUGUI farmTabText;
    public TextMeshProUGUI companionTabText;

    private InvTab currentTab = InvTab.Equipment;

    // 탭 변경 이벤트 (다른 매니저들이 구독)
    public static event System.Action<InvTab> OnTabChanged;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        // 버튼 이벤트 연결
        if (equipmentTabBtn != null)
            equipmentTabBtn.onClick.AddListener(() => SwitchTab(InvTab.Equipment));
        if (farmTabBtn != null)
            farmTabBtn.onClick.AddListener(() => SwitchTab(InvTab.Farm));
        if (companionTabBtn != null)
            companionTabBtn.onClick.AddListener(() => SwitchTab(InvTab.Companion));

        // 초기 탭
        SwitchTab(InvTab.Equipment);
    }

    void Update()
    {
        // Tab 키로 탭 순환
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            int next = ((int)currentTab + 1) % 3;
            SwitchTab((InvTab)next);
        }
    }

    /// <summary>
    /// 특정 탭으로 전환
    /// </summary>
    public void SwitchTab(InvTab tab)
    {
        currentTab = tab;

        // 컨텐츠 표시/숨김
        SetContentActive(equipmentContent, tab == InvTab.Equipment);
        SetContentActive(farmContent, tab == InvTab.Farm);
        SetContentActive(companionContent, tab == InvTab.Companion);

        // 버튼 색상 갱신
        RefreshTabButtonColors();

        // 이벤트 발행 (FarmInventoryConnector, CompanionInventoryManager 등이 수신)
        OnTabChanged?.Invoke(tab);

        Debug.Log($"[InventoryTabController] 탭 전환 → {tab}");
    }

    public InvTab CurrentTab => currentTab;

    // ─────────────────────────────────────────
    //  헬퍼
    // ─────────────────────────────────────────

    private void SetContentActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

    private void RefreshTabButtonColors()
    {
        SetBtnColor(equipmentTabBtn, currentTab == InvTab.Equipment);
        SetBtnColor(farmTabBtn, currentTab == InvTab.Farm);
        SetBtnColor(companionTabBtn, currentTab == InvTab.Companion);

        if (equipmentTabText != null)
            equipmentTabText.color = currentTab == InvTab.Equipment ? activeTabColor : inactiveTabColor;
        if (farmTabText != null)
            farmTabText.color = currentTab == InvTab.Farm ? activeTabColor : inactiveTabColor;
        if (companionTabText != null)
            companionTabText.color = currentTab == InvTab.Companion ? activeTabColor : inactiveTabColor;
    }

    private void SetBtnColor(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = active ? activeTabColor : inactiveTabColor;
    }
}