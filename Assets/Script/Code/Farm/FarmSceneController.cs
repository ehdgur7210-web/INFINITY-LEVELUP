using System.Collections;
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
    //  Inspector 연결 - FarmCanvas 핵심 오브젝트
    // ════════════════════════════════════════════════

    [Header("★ FarmCanvas (씬 전환 후 하위 활성화 보장)")]
    [Tooltip("FarmCanvas 루트. 비워두면 자동 탐색")]
    [SerializeField] private Canvas farmCanvas;

    [Tooltip("항상 활성화해야 할 FarmCanvas 하위 오브젝트들")]
    [SerializeField] private GameObject[] alwaysActiveObjects;

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
        EnsureFarmCanvasActive();
        SetupButtons();
        RefreshBuildingLabels();
        RefreshResources();

        // ★ SaveLoadManager.ApplySaveData 이후(2프레임 뒤)에도 한 번 더 보장
        StartCoroutine(EnsureCanvasAfterLoad());
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
    //  FarmCanvas 하위 활성화 보장
    // ════════════════════════════════════════════════

    /// <summary>
    /// ★ 씬 전환 후 FarmCanvas와 핵심 하위 오브젝트가 활성 상태인지 확인하고
    ///   비활성이면 강제로 켠다.
    ///   원인: FarmPlantModePanel 등이 OverlayGO 잘못 참조 시 Canvas 전체를 끌 수 있음.
    /// </summary>
    private void EnsureFarmCanvasActive()
    {
        // farmCanvas 자동 탐색
        if (farmCanvas == null)
            farmCanvas = GetComponentInParent<Canvas>();
        if (farmCanvas == null)
            farmCanvas = FindObjectOfType<Canvas>();

        // Canvas 자체가 꺼져 있으면 강제 활성화
        if (farmCanvas != null && !farmCanvas.gameObject.activeSelf)
        {
            farmCanvas.gameObject.SetActive(true);
            Debug.LogWarning("[FarmSceneController] ★ FarmCanvas가 비활성 상태 → 강제 활성화!");
        }

        // Inspector에 등록된 항상-활성 오브젝트 켜기
        if (alwaysActiveObjects != null)
        {
            foreach (GameObject go in alwaysActiveObjects)
            {
                if (go != null && !go.activeSelf)
                {
                    go.SetActive(true);
                    Debug.LogWarning($"[FarmSceneController] ★ {go.name} 비활성 → 강제 활성화!");
                }
            }
        }

        // Inspector 미연결 시 이름 기반 안전장치
        if (farmCanvas != null && (alwaysActiveObjects == null || alwaysActiveObjects.Length == 0))
        {
            ActivateChildByName(farmCanvas.transform, "HUD_Root");
            ActivateChildByName(farmCanvas.transform, "FarmGrid");
            ActivateChildByName(farmCanvas.transform, "ButtonMenu");
        }
    }

    private void ActivateChildByName(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null && !child.gameObject.activeSelf)
        {
            child.gameObject.SetActive(true);
            Debug.LogWarning($"[FarmSceneController] ★ {childName} 비활성 → 강제 활성화!");
        }
    }

    /// <summary>
    /// SaveLoadManager.AutoLoadOnStart()가 2프레임 후 ApplySaveData를 실행하므로
    /// 그 이후에도 FarmCanvas 상태를 한 번 더 보장.
    /// </summary>
    private IEnumerator EnsureCanvasAfterLoad()
    {
        // SaveLoadManager.AutoLoadOnStart가 2프레임 대기하므로 3프레임 후 실행
        yield return null;
        yield return null;
        yield return null;

        EnsureFarmCanvasActive();
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

        // 튜토리얼 트리거
        TutorialManager.Instance?.OnActionCompleted("OpenCropShop");
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

        // 튜토리얼 트리거
        TutorialManager.Instance?.OnActionCompleted("OpenFarmInventory");
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
        SaveLoadManager.Instance?.SaveGame();

        // 튜토리얼 트리거
        TutorialManager.Instance?.OnActionCompleted("GoToMainGame");

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