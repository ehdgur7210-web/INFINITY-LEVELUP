using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// FarmSceneController — 농장 씬 총괄 컨트롤러
///
/// 역할:
///   - 하단 메뉴 버튼 (퀘스트/작물상점/작물처리/수확인벤/메인게임) 연결
///   - 건물 클릭 이벤트 연결 (하우스/비닐하우스/물레방아/풍차)
///   - FarmManager / FarmBuildingManager와 이벤트 연동
///
/// ★ FarmPlotUI 제거됨 → 각 밭 오브젝트에 FarmPlotController 사용
/// ★ 심기 모드 제거됨 → FarmPlantModePanel에서 처리
/// </summary>
public class FarmSceneController : MonoBehaviour
{
    public static FarmSceneController Instance { get; private set; }

    // ════════════════════════════════════════════════
    //  Inspector 연결 - 하단 메뉴
    // ════════════════════════════════════════════════

    [Header("하단 메뉴 버튼")]
    public Button btnQuest;
    public Button btnCropShop;
    public Button btnCropProcess;
    public Button btnHarvestInven;
    public Button btnMainGame;

    [Header("패널 연결")]
    public FarmCropShopUI cropShopUI;
    public FarmInventoryUI farmInventoryUI;
    public FarmProgressPanel farmProgressPanel;   // ★ 추가 — 작물진행도 패널
    public FarmBuildingUpgradeUI buildingUpgradeUI;
    public FarmQuestPanelUI questPanelUI;

    // ════════════════════════════════════════════════
    //  Inspector 연결 - 건물 버튼
    // ════════════════════════════════════════════════

    [Header("건물 클릭 버튼")]
    public Button houseBuildingBtn;
    public Button greenhouseBuildingBtn;
    public Button watermillBuildingBtn;
    public Button windmillBuildingBtn;

    [Header("건물 레벨 표시 텍스트 (선택)")]
    public TextMeshProUGUI houseLevelLabel;
    public TextMeshProUGUI greenhouseLevelLabel;
    public TextMeshProUGUI watermillLevelLabel;
    public TextMeshProUGUI windmillLevelLabel;

    // ════════════════════════════════════════════════
    //  Inspector 연결 - 상단 리소스
    // ════════════════════════════════════════════════

    [Header("리소스 표시")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI gemText;
    public TextMeshProUGUI cropPointText;

    // ════════════════════════════════════════════════
    //  초기화
    // ════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        SetupButtons();
        RefreshBuildingLabels();
        RefreshResources();
    }

    void OnEnable()
    {
        FarmBuildingManager.OnBuildingLevelChanged += OnBuildingLevelChanged;
        FarmBuildingManager.OnNewPlotUnlocked += OnNewPlotUnlocked;
        FarmManagerExtension.OnCropPointsChanged += OnCropPointsChanged;
        GameManager.OnGoldChanged += OnGoldChanged;
        GameManager.OnGemChanged += OnGemChanged;
    }

    void OnDisable()
    {
        FarmBuildingManager.OnBuildingLevelChanged -= OnBuildingLevelChanged;
        FarmBuildingManager.OnNewPlotUnlocked -= OnNewPlotUnlocked;
        FarmManagerExtension.OnCropPointsChanged -= OnCropPointsChanged;
        GameManager.OnGoldChanged -= OnGoldChanged;
        GameManager.OnGemChanged -= OnGemChanged;
    }

    // ════════════════════════════════════════════════
    //  버튼 연결
    // ════════════════════════════════════════════════

    private void SetupButtons()
    {
        // 하단 메뉴
        btnQuest?.onClick.AddListener(OpenQuestPanel);
        btnCropShop?.onClick.AddListener(OpenCropShop);
        btnCropProcess?.onClick.AddListener(OpenProgressPanel);    // 작물진행도
        btnHarvestInven?.onClick.AddListener(OpenHarvestInventory); // 수확인벤
        btnMainGame?.onClick.AddListener(GoToMainGame);

        // 건물 클릭
        houseBuildingBtn?.onClick.AddListener(() => OpenBuildingUpgrade(BuildingType.House));
        greenhouseBuildingBtn?.onClick.AddListener(() => OpenBuildingUpgrade(BuildingType.Greenhouse));
        watermillBuildingBtn?.onClick.AddListener(() => OpenBuildingUpgrade(BuildingType.Watermill));
        windmillBuildingBtn?.onClick.AddListener(() => OpenBuildingUpgrade(BuildingType.Windmill));
    }

    // ════════════════════════════════════════════════
    //  패널 열기
    // ════════════════════════════════════════════════

    public void OpenQuestPanel()
    {
        questPanelUI?.gameObject.SetActive(true);
        questPanelUI?.RefreshQuestList();
    }

    public void OpenCropShop()
    {
        cropShopUI?.OpenShop();
    }

    // ★ 작물진행도 버튼 → FarmProgressPanel
    public void OpenProgressPanel()
    {
        if (farmProgressPanel == null)
        {
            Debug.LogWarning("[FarmSceneController] farmProgressPanel이 연결되지 않았습니다!");
            return;
        }
        farmProgressPanel.OpenPanel();
    }

    // ★ 수확인벤 버튼 → FarmInventoryUI
    public void OpenHarvestInventory()
    {
        if (farmInventoryUI == null)
        {
            Debug.LogWarning("[FarmSceneController] farmInventoryUI가 연결되지 않았습니다!");
            return;
        }
        farmInventoryUI.OpenPanel();
    }

    // 기존 호환용 (다른 곳에서 호출될 수 있으므로 유지)
    public void OpenCropProcess()
    {
        OpenProgressPanel();
    }

    public void OpenBuildingUpgrade(BuildingType type)
    {
        buildingUpgradeUI?.OpenPanel();
        Debug.Log($"[FarmSceneController] 건물 업그레이드 패널 열기: {type}");
    }

    public void GoToMainGame()
    {
        SaveLoadManager.Instance?.SaveGame(0);
        SceneTransitionManager.Instance?.LoadScene("MainScene");
    }

    // ════════════════════════════════════════════════
    //  전체 수확
    // ════════════════════════════════════════════════

    public void HarvestAll()
    {
        int count = FarmManager.Instance?.HarvestAll() ?? 0;
        Debug.Log($"[FarmSceneController] 전체 수확: {count}칸");
    }

    // ════════════════════════════════════════════════
    //  건물 레벨 라벨 갱신
    // ════════════════════════════════════════════════

    private void RefreshBuildingLabels()
    {
        if (FarmBuildingManager.Instance == null) return;
        if (houseLevelLabel) houseLevelLabel.text = $"Lv{FarmBuildingManager.Instance.HouseLevel}";
        if (greenhouseLevelLabel) greenhouseLevelLabel.text = $"Lv{FarmBuildingManager.Instance.GreenhouseLevel}";
        if (watermillLevelLabel) watermillLevelLabel.text = $"Lv{FarmBuildingManager.Instance.WatermillLevel}";
        if (windmillLevelLabel) windmillLevelLabel.text = $"Lv{FarmBuildingManager.Instance.WindmillLevel}";
    }

    private void RefreshResources()
    {
        if (GameManager.Instance != null)
        {
            if (goldText) goldText.text = $"💰 {GameManager.Instance.PlayerGold:N0}";
            if (gemText) gemText.text = $"💎 {GameManager.Instance.PlayerGem}";
        }
        int pts = FarmManager.Instance?.GetCropPoints() ?? 0;
        if (cropPointText) cropPointText.text = $"🌱 {pts}";
    }

    // ════════════════════════════════════════════════
    //  이벤트 수신
    // ════════════════════════════════════════════════

    private void OnBuildingLevelChanged(BuildingType type, int level)
    {
        RefreshBuildingLabels();
    }

    private void OnNewPlotUnlocked(int plotIndex, PlotType plotType)
    {
        // FarmPlotController가 직접 FarmManagerExtension 이벤트를 구독하므로
        // 여기서는 로그만 남김
        Debug.Log($"[FarmSceneController] 플롯 {plotIndex} 해금 ({plotType})");
    }

    private void OnCropPointsChanged(int points)
    {
        if (cropPointText) cropPointText.text = $"🌱 {points}";
    }

    private void OnGoldChanged(int gold)
    {
        if (goldText) goldText.text = $"💰 {gold:N0}";
    }

    private void OnGemChanged(int gem)
    {
        if (gemText) gemText.text = $"💎 {gem}";
    }
}