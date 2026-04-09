using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
// FarmPlantModePanel.cs  ★ 수정 버전
//
// ★ 역할:
//   FarmPlotController에서 밭을 클릭하면 이 패널이 열림.
//   밭의 상태(잠금/빈밭/성장중/수확가능)에 따라 버튼을 자동 조절함.
//
// ★ 버그 수정:
//   1. FarmCharacterMover가 없어도 패널이 열리도록 null 안전 처리
//   2. 성장 중인 밭에서도 물주기/비료/빠른수확 버튼이 표시되도록 수정
//   3. 빈밭 상태 텍스트 명확히 표시
//   4. 잠금 밭에서 해금 비용/레벨 정보 올바르게 표시
//   5. ★ PlantMode_Overlay(부모 오버레이)를 함께 제어하여
//      씨앗 선택 단계에서도 패널이 열려있도록 수정
//
// ★ 이 패널이 표시하는 버튼 (밭 상태별):
//   잠긴 밭    → [해금] 버튼 + 비용/레벨 텍스트
//   빈 밭      → [씨앗심기] 버튼
//   성장 중    → [물주기] [비료] [⚡빠른수확] 버튼
//   수확 가능  → [수확] 버튼
//
// ★ Hierarchy:
//   PlantMode_Overlay                  ← overlayRoot (부모 오버레이 GO)
//     └ FarmPlantModePanel             ← 이 스크립트
//         ├ TitleText    (TMP)         ← titleText       "밭 #1 관리"
//         ├ BtnClose     (Button)      ← closeButton
//         ├ StatusArea
//         │   ├ CropIconImage  (Image) ← cropIconImage   작물/씨앗 아이콘
//         │   ├ StatusText     (TMP)   ← statusText      상태 텍스트
//         │   ├ ProgressBar    (Slider)← progressBar     성장 게이지
//         │   ├ RemainText     (TMP)   ← remainText      남은 시간
//         │   ├ WaterIcon      (GO)    ← waterIcon       물줌 표시
//         │   └ FertIcon       (GO)    ← fertIcon        비료줌 표시
//         ├ ActionRow1
//         │   ├ BtnPlant               ← plantButton     씨앗심기
//         │   │   └ BtnPlantIcon       ← plantBtnIcon
//         │   ├ BtnWater               ← waterButton     물주기
//         │   │   └ BtnWaterIcon       ← waterBtnIcon
//         │   └ BtnFertilize           ← fertilizeButton 비료주기
//         │       └ BtnFertIcon        ← fertBtnIcon
//         ├ ActionRow2
//         │   ├ BtnHarvest             ← harvestButton   수확
//         │   ├ BtnInstantFinish       ← instantFinishButton ⚡빠른수확
//         │   │   └ InstantCostText    ← instantCostText 💎 비용
//         │   └ BtnUnlock              ← unlockButton    해금
//         │       └ UnlockCostText     ← unlockCostText  💰 비용
//         └ FarmItemSelectPopup        ← itemSelectPopup 씨앗/물/비료 선택 팝업
// ═══════════════════════════════════════════════════════════════════

public class FarmPlantModePanel : MonoBehaviour
{
    // 싱글톤 인스턴스 — FarmPlotController에서 Instance?.OpenForPlot()으로 호출
    public static FarmPlantModePanel Instance { get; private set; }

    // ★ 튜토리얼용 public 접근자
    public Button CloseButton => closeButton;

    // ─────────────────────────────────────────────────────────────
    //  Inspector 필드
    // ─────────────────────────────────────────────────────────────

    [Header("===== 패널 =====")]
    [Tooltip("패널 제목 텍스트 (밭 번호 표시)")]
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("닫기 버튼")]
    [SerializeField] private Button closeButton;

    [Header("===== 상태 표시 =====")]
    [Tooltip("현재 작물/씨앗 아이콘")]
    [SerializeField] private Image cropIconImage;
    [Tooltip("밭 상태 텍스트 (빈밭/성장중/수확가능/잠금)")]
    [SerializeField] private TextMeshProUGUI statusText;
    [Tooltip("성장 진행률 슬라이더")]
    [SerializeField] private Slider progressBar;
    [Tooltip("남은 성장 시간 텍스트")]
    [SerializeField] private TextMeshProUGUI remainText;
    [Tooltip("물을 준 경우 표시되는 아이콘 오브젝트")]
    [SerializeField] private GameObject waterIcon;
    [Tooltip("비료를 준 경우 표시되는 아이콘 오브젝트")]
    [SerializeField] private GameObject fertIcon;

    [Header("===== 액션 버튼 =====")]
    [Tooltip("씨앗심기 버튼")]
    [SerializeField] private Button plantButton;
    [Tooltip("씨앗심기 버튼 아이콘")]
    [SerializeField] private Image plantBtnIcon;

    [Tooltip("물주기 버튼")]
    [SerializeField] private Button waterButton;
    [Tooltip("물주기 버튼 아이콘")]
    [SerializeField] private Image waterBtnIcon;

    [Tooltip("비료주기 버튼")]
    [SerializeField] private Button fertilizeButton;
    [Tooltip("비료주기 버튼 아이콘")]
    [SerializeField] private Image fertBtnIcon;

    [Tooltip("수확 버튼 (수확 가능 시 표시)")]
    [SerializeField] private Button harvestButton;

    [Tooltip("⚡빠른수확 버튼 (젬 소모 즉시완료)")]
    [SerializeField] private Button instantFinishButton;
    [Tooltip("빠른수확 비용 텍스트 (💎 N)")]
    [SerializeField] private TextMeshProUGUI instantCostText;

    [Tooltip("해금 버튼 (잠긴 밭)")]
    [SerializeField] private Button unlockButton;
    [Tooltip("해금 비용 텍스트 (💰 N,NNN)")]
    [SerializeField] private TextMeshProUGUI unlockCostText;

    [Header("===== 아이템 선택 팝업 =====")]
    [Tooltip("씨앗/물/비료 선택 팝업 컴포넌트")]
    [SerializeField] private FarmItemSelectPopup itemSelectPopup;

    // ★ 기존 3개 분리 팝업 필드 — itemSelectPopup 통합으로 미사용 (Inspector 참조 보존)
    [HideInInspector] [SerializeField] private GameObject seedPopupPanel;
    [HideInInspector] [SerializeField] private Transform seedPopupContent;
    [HideInInspector] [SerializeField] private GameObject seedSlotPrefab;
    [HideInInspector] [SerializeField] private GameObject waterPopupPanel;
    [HideInInspector] [SerializeField] private Transform waterPopupContent;
    [HideInInspector] [SerializeField] private GameObject waterSlotPrefab;
    [HideInInspector] [SerializeField] private GameObject fertPopupPanel;
    [HideInInspector] [SerializeField] private Transform fertPopupContent;
    [HideInInspector] [SerializeField] private GameObject fertSlotPrefab;

    [Header("===== 물 목록 =====")]
    [Tooltip("사용 가능한 물 ScriptableObject 목록")]
    [SerializeField] private List<WaterData> availableWaters = new List<WaterData>();

    [Header("===== 오버레이 루트 =====")]
    [Tooltip("이 패널을 감싸는 오버레이 GO (PlantMode_Overlay). " +
             "비워두면 부모 오브젝트를 자동으로 사용합니다.")]
    [SerializeField] private GameObject overlayRoot;

    [Header("===== 기본 아이콘 =====")]
    [Tooltip("씨앗 선택 전 기본 아이콘")]
    [SerializeField] private Sprite defaultSeedIcon;
    [Tooltip("물 선택 전 기본 아이콘")]
    [SerializeField] private Sprite defaultWaterIcon;
    [Tooltip("비료 선택 전 기본 아이콘")]
    [SerializeField] private Sprite defaultFertIcon;

    // ─────────────────────────────────────────────────────────────
    //  내부 변수
    // ─────────────────────────────────────────────────────────────

    private CropData selectedSeed = null;   // 현재 선택된 씨앗 (심기 직전까지만 유효)
    private int currentPlotIndex = -1;       // 현재 열린 밭 인덱스

    /// <summary>PlayerStats 우선 → GameManager → GameDataBridge 폴백 (FarmScene 대응)</summary>
    private int GetPlayerLevel()
    {
        if (PlayerStats.Instance != null) return Mathf.Max(1, PlayerStats.Instance.level);
        if (GameManager.Instance != null) return Mathf.Max(1, GameManager.Instance.PlayerLevel);
        int level = GameDataBridge.CurrentData?.playerLevel ?? 1;
        return Mathf.Max(1, level); // ★ playerLevel이 0이어도 최소 1 보장
    }

    /// <summary>재화 차감 (GameManager 우선, 없으면 GameDataBridge 폴백)</summary>
    private bool SpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (GameManager.Instance != null) return GameManager.Instance.SpendGold(amount);
        var data = GameDataBridge.CurrentData;
        if (data == null || data.playerGold < amount) return false;
        data.playerGold -= amount;
        return true;
    }

    private bool SpendGem(long amount)
    {
        if (amount <= 0) return true;
        if (GameManager.Instance != null) return GameManager.Instance.SpendGem(amount);
        var data = GameDataBridge.CurrentData;
        if (data == null || data.playerGem < amount) return false;
        data.playerGem -= amount;
        return true;
    }

    /// <summary>
    /// 실제로 SetActive를 제어할 오버레이 GO.
    /// Inspector에서 overlayRoot를 연결하면 그것을,
    /// 아니면 부모 GO를, 둘 다 없으면 자기 자신을 사용.
    /// ★ 부모가 Canvas인 경우 FarmCanvas 전체를 끄는 버그 방지 →
    ///   Canvas 바로 아래라면 자기 자신(gameObject)만 제어.
    /// </summary>
    private GameObject OverlayGO
    {
        get
        {
            if (overlayRoot != null) return overlayRoot;
            if (transform.parent != null && transform.parent.GetComponent<Canvas>() == null)
                return transform.parent.gameObject;
            return gameObject;
        }
    }

    // ═══ Unity 생명주기 ══════════════════════════════════════════

    private bool _eventsSubscribed = false;

    void Awake()
    {
        // 싱글톤 설정
        if (Instance != null && Instance != this && Instance.gameObject.scene.isLoaded) { Destroy(gameObject); return; }
        Instance = this;
        Debug.Log("[ManagerInit] FarmPlantModePanel가 생성되었습니다.");

        // ★ 시작 시 오버레이 전체를 비활성화 (밭 클릭 전까지는 숨겨져 있음)
        //   이로 인해 Start()가 지연되므로 버튼 리스너는 OpenForPlot()에서 등록
        OverlayGO.SetActive(false);
    }

    void Start()
    {
        SubscribeEvents();
    }

    void OnDestroy()
    {
        FarmManagerExtension.OnPlotStateChangedStatic -= OnPlotStateChanged;
        if (Instance == this) Instance = null;
    }

    /// <summary>이벤트 구독 (한 번만)</summary>
    private void SubscribeEvents()
    {
        if (_eventsSubscribed) return;
        _eventsSubscribed = true;

        FarmManagerExtension.OnPlotStateChangedStatic += OnPlotStateChanged;
        FarmSelectionMemory.OnWaterChanged += (w) => UpdateWaterBtnIcon(w);
        FarmSelectionMemory.OnFertChanged += (f) => UpdateFertBtnIcon(f);

        UpdateWaterBtnIcon(FarmSelectionMemory.SelectedWater);
        UpdateFertBtnIcon(FarmSelectionMemory.SelectedFertilizer);
    }

    // ═══ 버튼 설정 ═══════════════════════════════════════════════

    /// <summary>
    /// ★ 모든 버튼의 리스너를 RemoveAllListeners() 후 재등록.
    /// OpenForPlot()에서 매번 호출하여 씬 전환 후에도 리스너 보장.
    ///
    /// 왜 매번 재등록?
    ///   - Awake에서 OverlayGO.SetActive(false) → Start() 지연 → SetupButtons 미호출
    ///   - 씬 전환 시 Button 오브젝트는 재생성되지만 리스너는 남지 않음
    ///   - AddListener만 반복하면 중복 등록 → RemoveAllListeners 선행 필수
    /// </summary>
    private void RebindAllButtons()
    {
        Debug.Log($"[FarmPlantModePanel] ★ RebindAllButtons — plantButton:{plantButton != null}, closeButton:{closeButton != null}");
        if (closeButton != null) { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(ClosePanel); }
        if (plantButton != null)
        {
            plantButton.onClick.RemoveAllListeners();
            plantButton.onClick.AddListener(OnPlantClicked);
            Debug.Log($"[FarmPlantModePanel] ★ plantButton 리스너 등록 완료 — interactable:{plantButton.interactable}, active:{plantButton.gameObject.activeInHierarchy}");
        }
        else
        {
            Debug.LogError("[FarmPlantModePanel] ★ plantButton이 NULL! Inspector 연결 확인 필요");
        }
        if (waterButton != null) { waterButton.onClick.RemoveAllListeners(); waterButton.onClick.AddListener(OnWaterClicked); }
        if (fertilizeButton != null) { fertilizeButton.onClick.RemoveAllListeners(); fertilizeButton.onClick.AddListener(OnFertilizeClicked); }
        if (harvestButton != null) { harvestButton.onClick.RemoveAllListeners(); harvestButton.onClick.AddListener(OnHarvestClicked); }
        if (instantFinishButton != null) { instantFinishButton.onClick.RemoveAllListeners(); instantFinishButton.onClick.AddListener(OnInstantFinishClicked); }
        if (unlockButton != null) { unlockButton.onClick.RemoveAllListeners(); unlockButton.onClick.AddListener(OnUnlockClicked); }

        SetupItemSelectCallbacks();

    }

    private void SetupItemSelectCallbacks()
    {
        if (itemSelectPopup == null) return;

        // 팝업에서 씨앗 선택 시 → 바로 심기 시도
        itemSelectPopup.OnSeedSelected = (crop) =>
        {
            selectedSeed = crop;
            if (plantBtnIcon != null) plantBtnIcon.sprite = crop.seedIcon ?? defaultSeedIcon;

            // ★ 씨앗 선택 즉시 자동 심기 (2클릭 → 1클릭)
            if (currentPlotIndex >= 0)
            {
                bool ok = FarmManager.Instance?.PlantCrop(currentPlotIndex, selectedSeed) ?? false;
                if (ok)
                {
                    selectedSeed = null;
                    SoundManager.Instance?.PlayButtonClick();
                    TutorialManager.Instance?.OnActionCompleted("SeedPlanted");
                    return; // 심기 성공 → RefreshUI가 이벤트로 자동 호출됨
                }
            }

            // ★ 심기 실패 → selectedSeed 초기화 (다음 클릭 시 팝업 다시 열리도록)
            selectedSeed = null;
            UIManager.Instance?.ShowMessage("심기 실패 — 다시 선택해주세요", Color.yellow);
            OverlayGO.SetActive(true);
            gameObject.SetActive(true);
            SoundManager.Instance?.PlayButtonClick();
        };

        // ★ 팝업에서 물 선택 시 → 저장 + 즉시 적용 (2클릭 → 1클릭)
        itemSelectPopup.OnWaterSelected = (water) =>
        {
            FarmSelectionMemory.SetWater(water);
            ApplyWater(water);
            SoundManager.Instance?.PlayButtonClick();
            TutorialManager.Instance?.OnActionCompleted("WaterApplied");
        };

        // ★ 팝업에서 비료 선택 시 → 저장 + 즉시 적용 (2클릭 → 1클릭)
        itemSelectPopup.OnFertSelected = (fert) =>
        {
            FarmSelectionMemory.SetFertilizer(fert);
            ApplyFertilizer(fert);
            SoundManager.Instance?.PlayButtonClick();
            TutorialManager.Instance?.OnActionCompleted("FertApplied");
        };
    }

    // ═══ 패널 열기/닫기 ══════════════════════════════════════════

    /// <summary>
    /// FarmInventoryUI에서 씨앗 선택 후 호출 가능.
    /// 씨앗을 미리 선택해두면 밭 클릭 시 바로 심기 가능.
    /// </summary>
    public void PreSelectSeed(CropData crop)
    {
        selectedSeed = crop;
        if (plantBtnIcon != null && crop.seedIcon != null)
            plantBtnIcon.sprite = crop.seedIcon;
        UIManager.Instance?.ShowMessage($"[{crop.cropName}] 심을 밭을 선택하세요", Color.green);
    }

    /// <summary>
    /// ★ 핵심 메서드: FarmPlotController에서 밭 클릭 시 이 메서드 호출.
    ///   plotIndex 밭의 현재 상태에 맞게 패널 UI를 구성하고 표시함.
    /// </summary>
    public void OpenForPlot(int plotIndex)
    {
        Debug.Log($"[FarmPlantModePanel] ★ OpenForPlot({plotIndex}) 진입 — Instance:{Instance != null}, gameObject:{gameObject.name}");
        currentPlotIndex = plotIndex;
        selectedSeed = null; // 이전 씨앗 선택 초기화

        // ★ 오버레이(부모) + 자기 자신 모두 활성화 (버튼 바인딩보다 먼저!)
        OverlayGO.SetActive(true);
        gameObject.SetActive(true);
        Debug.Log($"[FarmPlantModePanel] ★ 오버레이/패널 활성화 — OverlayGO:{OverlayGO.activeSelf}, self:{gameObject.activeSelf}");

        // ★ 버튼 리스너 매번 재등록 (Start 지연 + 씬 전환 대응)
        RebindAllButtons();

        // ★ 이벤트 구독 보장 (Start가 안 불렸을 때 대비)
        SubscribeEvents();

        // 씨앗심기 버튼 아이콘 초기화
        if (plantBtnIcon != null && defaultSeedIcon != null)
            plantBtnIcon.sprite = defaultSeedIcon;

        // ★ DDOL 레이캐스트 차단 해제
        ClearDDOLRaycastBlockers();

        RefreshUI();
        SoundManager.Instance?.PlayPanelOpen();
    }

    /// <summary>
    /// ★ DDOL 오브젝트의 레이캐스트 차단을 해제.
    /// SceneTransitionManager의 fadeCanvasGroup(sortingOrder=9999)이
    /// blocksRaycasts=true 상태로 남아있으면 FarmScene의 모든 UI 입력이 먹통이 됨.
    /// FadeIn 코루틴이 정상 완료되지 않았을 때 발생 가능.
    /// </summary>
    private void ClearDDOLRaycastBlockers()
    {
        // SceneTransitionManager의 자식 CanvasGroup 검사
        if (SceneTransitionManager.Instance != null)
        {
            var canvasGroups = SceneTransitionManager.Instance.GetComponentsInChildren<CanvasGroup>(true);
            foreach (var cg in canvasGroups)
            {
                if (cg.blocksRaycasts)
                {
                    cg.blocksRaycasts = false;
                    cg.alpha = 0f;
                }
            }
        }

        // ★ EventSystem 존재 여부 확인 (씬 전환 시 EventSystem이 없으면 모든 UI 먹통)
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem_AutoCreated");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    public void ClosePanel()
    {
        // ★ 진단 로그: 누가 ClosePanel을 호출했는지 stack trace 출력
        //   "수확 버튼 누르면 관리패널이 닫히는 버그" 추적용
        Debug.Log($"[FarmPlantModePanel] ClosePanel 호출됨 ← {System.Environment.StackTrace}");

        CloseActivePopup();
        itemSelectPopup?.Hide();
        currentPlotIndex = -1;

        // ★ 오버레이(부모)를 끄면 자식도 자동으로 숨겨짐
        OverlayGO.SetActive(false);
        SoundManager.Instance?.PlayPanelClose();

        // 튜토리얼 트리거
        TutorialManager.Instance?.OnActionCompleted("PlantModeClosed");
    }

    // ═══ UI 갱신 (밭 상태별 버튼 표시 제어) ═════════════════════

    private void RefreshUI()
    {
        if (currentPlotIndex < 0)
        {
            Debug.LogWarning("[FarmPlantModePanel] RefreshUI 스킵: currentPlotIndex < 0");
            return;
        }

        FarmPlotState plot = FarmManager.Instance?.GetPlot(currentPlotIndex);
        if (plot == null)
        {
            Debug.LogWarning($"[FarmPlantModePanel] RefreshUI 스킵: plot null (FarmManager.Instance={FarmManager.Instance != null}, plotIndex={currentPlotIndex})");
            return;
        }

        if (titleText != null) titleText.text = $"밭 #{currentPlotIndex + 1} 관리";

        // ══════════════════════════════════════════════════════════
        //  잠긴 밭
        // ══════════════════════════════════════════════════════════
        if (!plot.isUnlocked)
        {
            // 모든 액션 버튼 숨기고 해금 버튼만 표시
            SetGroupActive(false,
                plantButton, waterButton, fertilizeButton,
                harvestButton, instantFinishButton);
            SetActive(unlockButton, true);

            // 해금 비용과 필요 레벨 표시
            int cost = FarmManager.Instance?.GetUnlockCost(currentPlotIndex) ?? 0;
            int reqLv = FarmManager.Instance?.GetUnlockRequiredLevel(currentPlotIndex) ?? 1;

            if (statusText != null)
                statusText.text = $"잠긴 밭 (Lv.{reqLv} 필요)";
            if (unlockCostText != null)
                unlockCostText.text = $"{cost:N0}";
            if (progressBar != null) progressBar.value = 0f;
            if (remainText != null) remainText.text = "";

            cropIconImage?.gameObject.SetActive(false);
            waterIcon?.SetActive(false);
            fertIcon?.SetActive(false);

            // 플레이어 레벨 부족 시 해금 버튼 비활성화
            int plyrLv = GetPlayerLevel();
            if (unlockButton != null) unlockButton.interactable = plyrLv >= reqLv;
            return;
        }

        // 해금 버튼은 잠긴 밭에서만 표시
        SetActive(unlockButton, false);

        // ══════════════════════════════════════════════════════════
        //  ★ 빈 밭 (수정됨 — 명확한 상태 텍스트)
        // ══════════════════════════════════════════════════════════
        if (plot.currentCrop == null)
        {
            cropIconImage?.gameObject.SetActive(false);

            if (statusText != null)
                statusText.text = "빈 밭 — 씨앗을 선택해 심으세요";
            if (progressBar != null) progressBar.value = 0f;
            if (remainText != null) remainText.text = "";

            waterIcon?.SetActive(false);
            fertIcon?.SetActive(false);

            // 씨앗심기 버튼만 표시
            SetActive(plantButton, true);
            SetActive(waterButton, false);
            SetActive(fertilizeButton, false);
            SetActive(harvestButton, false);
            SetActive(instantFinishButton, false);

            Debug.Log($"[FarmPlantModePanel] 빈밭 UI — plantButton:{(plantButton != null ? plantButton.gameObject.name + "(active=" + plantButton.gameObject.activeInHierarchy + ")" : "NULL")}");
            return;
        }

        // ══════════════════════════════════════════════════════════
        //  성장 중 / 수확 가능
        // ══════════════════════════════════════════════════════════
        bool isReady = plot.IsReadyToHarvest();
        float remaining = FarmCropExtension.CalcRemainingSeconds(plot);
        float progress = FarmCropExtension.CalcGrowthProgress(plot);

        // 작물 아이콘
        if (cropIconImage != null)
        {
            cropIconImage.gameObject.SetActive(true);
            cropIconImage.sprite = isReady
                ? (plot.currentCrop.harvestIcon ?? plot.currentCrop.seedIcon) // 수확 가능 → 수확 아이콘
                : plot.currentCrop.GetSpriteForStage(plot.GetStage());         // 성장 중 → 단계 아이콘
        }

        // 상태 텍스트
        if (statusText != null)
            statusText.text = isReady
                ? $"✅ {plot.currentCrop.cropName} 수확 가능!"
                : $"{plot.currentCrop.cropName} 성장 중... {progress * 100f:F0}%";

        // 성장 게이지
        if (progressBar != null) progressBar.value = progress;

        // 남은 시간 텍스트
        if (remainText != null)
            remainText.text = isReady ? "" : $"남은 시간: {FormatTime(remaining)}";

        // 물/비료 표시 배지
        waterIcon?.SetActive(plot.isWatered);
        fertIcon?.SetActive(plot.isFertilized);

        // ★ 빠른수확 비용 텍스트 (남은 시간 1분 = 💎 1개)
        if (instantCostText != null)
        {
            int gem = Mathf.Max(1, Mathf.CeilToInt(remaining / 60f));
            instantCostText.text = $"{gem}";
        }

        // ★ 튜토리얼 중: 물/비료를 아직 안 줬으면 수확 불가능으로 간주
        //   → 물주기→비료→수확 순서를 자연스럽게 안내
        bool effectiveReady = isReady;
        bool isTutorial = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;
        if (isReady && isTutorial)
        {
            if (!plot.isWatered || !plot.isFertilized)
                effectiveReady = false; // 물/비료 먼저
        }

        // ★ 버튼 표시 규칙:
        //   씨앗심기   → 숨김 (이미 작물 있음)
        //   물주기     → 물 안 줬고 수확 가능 아닐 때
        //   비료주기   → 비료 안 줬고 수확 가능 아닐 때
        //   수확       → 수확 가능할 때
        //   빠른수확   → 성장 중(수확 가능 아닐 때)
        SetActive(plantButton, false);
        SetActive(waterButton, !effectiveReady && !plot.isWatered);
        SetActive(harvestButton, effectiveReady);

        // ★ 튜토리얼 중: 물 선택 단계에서 비료/빠른수확 비활성, 비료 단계에서 빠른수확 비활성
        if (isTutorial && !plot.isWatered)
        {
            // 물주기 단계 → 비료, 빠른수확 모두 숨김
            SetActive(fertilizeButton, false);
            SetActive(instantFinishButton, false);
        }
        else if (isTutorial && !plot.isFertilized)
        {
            // 비료주기 단계 → 비료만 표시, 빠른수확 숨김
            SetActive(fertilizeButton, true);
            SetActive(instantFinishButton, false);
        }
        else
        {
            // 일반 상태
            SetActive(fertilizeButton, !effectiveReady && !plot.isFertilized);
            SetActive(instantFinishButton, !effectiveReady); // 성장 중이면 항상 표시
        }
    }

    // ═══ 버튼 아이콘 갱신 ════════════════════════════════════════

    private void UpdateWaterBtnIcon(WaterData w)
    {
        if (waterBtnIcon == null) return;
        waterBtnIcon.sprite = w?.icon ?? defaultWaterIcon;
        waterBtnIcon.enabled = true;
    }

    private void UpdateFertBtnIcon(FertilizerData f)
    {
        if (fertBtnIcon == null) return;
        fertBtnIcon.sprite = f?.icon ?? defaultFertIcon;
        fertBtnIcon.enabled = true;
    }

    // ═══ 버튼 클릭 핸들러 ════════════════════════════════════════

    /// <summary>
    /// 씨앗심기 버튼: 씨앗이 선택돼 있으면 바로 심기, 없으면 씨앗 선택 팝업 열기
    /// </summary>
    private void OnPlantClicked()
    {
        Debug.Log($"[FarmPlantModePanel] ★ OnPlantClicked 호출됨 — selectedSeed:{(selectedSeed != null ? selectedSeed.cropName : "null")}, plotIndex:{currentPlotIndex}");
        if (selectedSeed != null)
        {
            // 씨앗이 선택된 상태 → 바로 심기
            bool ok = FarmManager.Instance?.PlantCrop(currentPlotIndex, selectedSeed) ?? false;
            Debug.Log($"[FarmPlantModePanel] ★ 직접 심기 결과: {ok}");
            if (ok)
            {
                selectedSeed = null;
            }
            else
            {
                // ★ 심기 실패 → selectedSeed 초기화 (팝업 다시 열리도록)
                selectedSeed = null;
                UIManager.Instance?.ShowMessage("심기 실패 — 다시 선택해주세요", Color.yellow);
            }
        }
        else
        {
            // 씨앗 미선택 → 팝업 열기
            Debug.Log("[FarmPlantModePanel] ★ selectedSeed null → ShowSeedPopup 호출");
            ShowSeedPopup();
        }
    }

    /// <summary>
    /// 물주기 버튼: 항상 물 선택 팝업 열기 (선택 시 즉시 적용)
    /// </summary>
    private void OnWaterClicked()
    {
        // ★ 런타임 팝업으로 물 선택 (Inspector 연결 불필요)
        ShowWaterPopup();
    }

    /// <summary>
    /// 비료주기 버튼: 항상 비료 선택 팝업 열기 (선택 시 즉시 적용)
    /// </summary>
    private void OnFertilizeClicked()
    {
        // ★ 런타임 팝업으로 비료 선택 (Inspector 연결 불필요)
        ShowFertPopup();
    }

    /// <summary>
    /// 수확 버튼: 캐릭터 이동(있을 경우) 후 수확
    /// </summary>
    private void OnHarvestClicked()
    {
        SoundManager.Instance?.PlayButtonClick();

        if (FarmCharacterMover.Instance != null)
        {
            FarmCharacterMover.Instance.MoveToPlot(currentPlotIndex, () =>
            {
                FarmManager.Instance?.HarvestCrop(currentPlotIndex);
                SoundManager.Instance?.PlayQuestReward();
                TutorialManager.Instance?.OnActionCompleted("HarvestComplete");
            });
        }
        else
        {
            // FarmCharacterMover 없어도 즉시 수확
            FarmManager.Instance?.HarvestCrop(currentPlotIndex);
            SoundManager.Instance?.PlayQuestReward();
            TutorialManager.Instance?.OnActionCompleted("HarvestComplete");
        }
    }

    /// <summary>
    /// ⚡빠른수확 버튼: 젬 소모 후 즉시 수확 가능 상태로 변경
    /// </summary>
    private void OnInstantFinishClicked()
    {
        SoundManager.Instance?.PlayButtonClick();

        FarmPlotState plot = FarmManager.Instance?.GetPlot(currentPlotIndex);
        if (plot == null) return;

        float remain = FarmCropExtension.CalcRemainingSeconds(plot);
        int gemCost = Mathf.Max(1, Mathf.CeilToInt(remain / 60f));

        bool paid = SpendGem(gemCost);
        if (!paid)
        {
            UIManager.Instance?.ShowMessage($"{gemCost} 부족!", Color.red);
            return;
        }

        FarmManager.Instance?.InstantFinish(currentPlotIndex);
        UIManager.Instance?.ShowMessage($"즉시 완료! (-{gemCost})", Color.yellow);
    }

    /// <summary>
    /// 해금 버튼: 골드/젬 지불 후 잠긴 밭 해금
    /// </summary>
    private void OnUnlockClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        FarmManager.Instance?.UnlockPlot(currentPlotIndex);
    }

    // ═══ 물 적용 (인벤토리/재화 차감) ═══════════════════════════

    private void ApplyWater(WaterData water)
    {
        // ★ FarmInventoryUI: Instance 우선 → FindObjectOfType 폴백
        FarmInventoryUI farmInv = FarmInventoryUI.Instance ?? FindObjectOfType<FarmInventoryUI>(true);
        bool paid = farmInv != null && farmInv.ConsumeWater(water.waterID);
        if (!paid)
        {
            // ★ 재화 차감: GameManager 우선 → GameDataBridge 폴백
            if (water.costGem > 0) paid = SpendGem(water.costGem);
            else if (water.costGold > 0) paid = SpendGold(water.costGold);
            else paid = true;
        }

        if (!paid) { UIManager.Instance?.ShowMessage("재화 부족!", Color.red); return; }

        FarmPlotState plot = FarmManager.Instance?.GetPlot(currentPlotIndex);
        if (plot == null) return;
        plot.isWatered = true;
        FarmManagerExtension.InvokePlotChanged(currentPlotIndex);

        string bonus = water.extraSpeedBonus > 0 ? $" +{water.extraSpeedBonus * 100f:F0}%" : "";
        UIManager.Instance?.ShowMessage($"💧 {water.waterName}{bonus}", Color.cyan);
    }

    // ═══ 비료 적용 (인벤토리/재화 차감) ═════════════════════════

    private void ApplyFertilizer(FertilizerData fert)
    {
        if (fert == null) return;

        // ★ FarmInventoryUI: Instance 우선 → FindObjectOfType 폴백
        FarmInventoryUI farmInv = FarmInventoryUI.Instance ?? FindObjectOfType<FarmInventoryUI>(true);
        bool paid = farmInv != null && farmInv.ConsumeFertilizer(fert.fertilizerID);
        if (!paid)
        {
            // ★ 재화 차감: GameManager 우선 → GameDataBridge 폴백
            if (fert.costGem > 0) paid = SpendGem(fert.costGem);
            else if (fert.costGold > 0) paid = SpendGold(fert.costGold);
            else paid = true;
        }

        if (!paid) { UIManager.Instance?.ShowMessage("재화 부족!", Color.red); return; }
        FarmManager.Instance?.FertilizeCrop(currentPlotIndex, fert);
        UIManager.Instance?.ShowMessage($"🌿 {fert.fertilizerName} 적용!", Color.green);
    }

    // ═══ 팝업 열기/닫기 (FarmItemSelectPopup 통합 재사용) ═════════

    private void CloseActivePopup()
    {
        if (itemSelectPopup != null) itemSelectPopup.Hide();
    }

    // ── 씨앗 팝업 ──
    private void ShowSeedPopup()
    {
        var allCrops = FarmManager.Instance?.allCrops;
        Debug.Log($"[FarmPlantModePanel] ShowSeedPopup — allCrops:{(allCrops != null ? allCrops.Count + "개" : "null")}, itemSelectPopup:{itemSelectPopup != null}");

        if (allCrops == null || allCrops.Count == 0)
        {
            UIManager.Instance?.ShowMessage("사용 가능한 씨앗이 없습니다", Color.yellow);
            return;
        }
        if (itemSelectPopup == null)
        {
            Debug.LogError("[FarmPlantModePanel] itemSelectPopup 미연결! Inspector 확인 필요");
            return;
        }

        // ★ 보유 씨앗만 필터링 — 씨앗 없는 작물은 팝업에 표시하지 않음
        FarmInventoryUI farmInv = FarmInventoryUI.Instance;
        if (farmInv == null) farmInv = FindObjectOfType<FarmInventoryUI>(true);

        var ownedCrops = new List<CropData>();
        foreach (var crop in allCrops)
        {
            if (farmInv != null && farmInv.GetSeedCount(crop.cropID) > 0)
                ownedCrops.Add(crop);
        }

        if (ownedCrops.Count == 0)
        {
            UIManager.Instance?.ShowMessage("씨앗이 없습니다! 상점에서 구매하세요.", Color.red);
            return;
        }

        itemSelectPopup.ShowSeeds(ownedCrops);
    }

    // ── 물 팝업 ──
    private void ShowWaterPopup()
    {
        // 물 목록 자동 탐색 (비어있으면)
        if (availableWaters == null || availableWaters.Count == 0)
        {
            var found = Resources.FindObjectsOfTypeAll<WaterData>();
            if (found.Length > 0)
                availableWaters = new List<WaterData>(found);
        }

        // ★ 튜토리얼 중: 최상급 물(waterId 2)을 인벤토리에 자동 추가
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            EnsureTutorialWater();

        Debug.Log($"[FarmPlantModePanel] ShowWaterPopup — waters:{(availableWaters != null ? availableWaters.Count + "개" : "null")}, itemSelectPopup:{itemSelectPopup != null}");

        if (availableWaters == null || availableWaters.Count == 0)
        {
            // 물 데이터 없으면 기본 물주기만 실행
            FarmManager.Instance?.WaterCrop(currentPlotIndex);
            UIManager.Instance?.ShowMessage("물 주기 완료", Color.cyan);
            return;
        }
        if (itemSelectPopup == null)
        {
            Debug.LogError("[FarmPlantModePanel] itemSelectPopup 미연결! Inspector 확인 필요");
            return;
        }

        // ★ FarmItemSelectPopup.ShowWaters() — 콜백은 SetupItemSelectCallbacks에서 등록됨
        itemSelectPopup.ShowWaters(availableWaters);
    }

    // ── 비료 팝업 ──
    private void ShowFertPopup()
    {
        var ferts = FarmManager.Instance?.allFertilizers;

        // ★ 튜토리얼 중: 최상급 비료(fertilizerId 2)를 인벤토리에 자동 추가
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
            EnsureTutorialFertilizer();

        Debug.Log($"[FarmPlantModePanel] ShowFertPopup — ferts:{(ferts != null ? ferts.Count + "개" : "null")}, itemSelectPopup:{itemSelectPopup != null}");

        if (ferts == null || ferts.Count == 0)
        {
            UIManager.Instance?.ShowMessage("사용 가능한 비료가 없습니다", Color.yellow);
            return;
        }
        if (itemSelectPopup == null)
        {
            Debug.LogError("[FarmPlantModePanel] itemSelectPopup 미연결! Inspector 확인 필요");
            return;
        }

        // ★ FarmItemSelectPopup.ShowFertilizers() — 콜백은 SetupItemSelectCallbacks에서 등록됨
        itemSelectPopup.ShowFertilizers(ferts);
    }

    // ═══ 튜토리얼 최상급 물/비료 자동 지급 ══════════════════════════

    /// <summary>
    /// 튜토리얼 중 최상급 물(waterID=2)을 인벤토리에 1개 자동 추가
    /// </summary>
    private void EnsureTutorialWater()
    {
        const int topWaterID = 2;
        FarmInventoryUI farmInv = FarmInventoryUI.Instance ?? FindObjectOfType<FarmInventoryUI>(true);
        if (farmInv == null) return;

        int owned = farmInv.GetWaterCount(topWaterID);
        if (owned <= 0)
        {
            farmInv.AddWater(topWaterID, 3);
            Debug.Log($"[FarmPlantModePanel] 튜토리얼: 최상급 물(ID={topWaterID}) 3개 자동 지급");
        }
    }

    /// <summary>
    /// 튜토리얼 중 최상급 비료(fertilizerID=2)를 인벤토리에 1개 자동 추가
    /// </summary>
    private void EnsureTutorialFertilizer()
    {
        const int topFertID = 2;
        FarmInventoryUI farmInv = FarmInventoryUI.Instance ?? FindObjectOfType<FarmInventoryUI>(true);
        if (farmInv == null) return;

        int owned = farmInv.GetFertCount(topFertID);
        if (owned <= 0)
        {
            farmInv.AddFertilizer(topFertID, 3);
            Debug.Log($"[FarmPlantModePanel] 튜토리얼: 최상급 비료(ID={topFertID}) 3개 자동 지급");
        }
    }

    // ═══ 이벤트 수신 + 유틸 ═════════════════════════════════════

    /// <summary>
    /// FarmManager에서 밭 상태 변경 이벤트 수신 시 UI 즉시 갱신
    /// </summary>
    private void OnPlotStateChanged(int idx)
    {
        if (idx == currentPlotIndex) RefreshUI();
    }

    private void SetActive(Button btn, bool active)
    {
        if (btn != null) btn.gameObject.SetActive(active);
    }

    private void SetGroupActive(bool active, params Button[] btns)
    {
        foreach (var b in btns) SetActive(b, active);
    }

    private string FormatTime(float s)
    {
        int h = (int)(s / 3600);
        int m = (int)((s % 3600) / 60);
        int ss = (int)(s % 60);

        if (h > 0) return $"{h}시간 {m}분";
        if (m > 0) return $"{m}분 {ss:D2}초";
        return $"{ss}초";
    }
}