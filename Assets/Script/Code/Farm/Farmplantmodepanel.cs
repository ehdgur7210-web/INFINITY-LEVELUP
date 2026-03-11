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
//
// ★ 이 패널이 표시하는 버튼 (밭 상태별):
//   잠긴 밭    → [해금] 버튼 + 비용/레벨 텍스트
//   빈 밭      → [씨앗심기] 버튼
//   성장 중    → [물주기] [비료] [⚡빠른수확] 버튼
//   수확 가능  → [수확] 버튼
//
// ★ Hierarchy:
//   FarmPlantModePanel                 ← 이 스크립트, 비활성 상태로 씬에 배치
//     ├ TitleText    (TMP)             ← titleText       "밭 #1 관리"
//     ├ BtnClose     (Button)          ← closeButton
//     ├ StatusArea
//     │   ├ CropIconImage  (Image)     ← cropIconImage   작물/씨앗 아이콘
//     │   ├ StatusText     (TMP)       ← statusText      상태 텍스트
//     │   ├ ProgressBar    (Slider)    ← progressBar     성장 게이지
//     │   ├ RemainText     (TMP)       ← remainText      남은 시간
//     │   ├ WaterIcon      (GO)        ← waterIcon       물줌 표시
//     │   └ FertIcon       (GO)        ← fertIcon        비료줌 표시
//     ├ ActionRow1
//     │   ├ BtnPlant                   ← plantButton     씨앗심기
//     │   │   └ BtnPlantIcon (Image)   ← plantBtnIcon
//     │   ├ BtnWater                   ← waterButton     물주기
//     │   │   └ BtnWaterIcon (Image)   ← waterBtnIcon
//     │   └ BtnFertilize               ← fertilizeButton 비료주기
//     │       └ BtnFertIcon  (Image)   ← fertBtnIcon
//     ├ ActionRow2
//     │   ├ BtnHarvest                 ← harvestButton   수확
//     │   ├ BtnInstantFinish           ← instantFinishButton ⚡빠른수확
//     │   │   └ InstantCostText (TMP)  ← instantCostText 💎 비용
//     │   └ BtnUnlock                  ← unlockButton    해금
//     │       └ UnlockCostText (TMP)   ← unlockCostText  💰 비용
//     └ FarmItemSelectPopup            ← itemSelectPopup 씨앗/물/비료 선택 팝업
// ═══════════════════════════════════════════════════════════════════

public class FarmPlantModePanel : MonoBehaviour
{
    // 싱글톤 인스턴스 — FarmPlotController에서 Instance?.OpenForPlot()으로 호출
    public static FarmPlantModePanel Instance { get; private set; }

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

    [Header("===== 물 목록 =====")]
    [Tooltip("사용 가능한 물 ScriptableObject 목록")]
    [SerializeField] private List<WaterData> availableWaters = new List<WaterData>();

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

    // ═══ Unity 생명주기 ══════════════════════════════════════════

    void Awake()
    {
        // 싱글톤 설정
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 시작 시 패널 비활성화 (밭 클릭 전까지는 숨겨져 있음)
        gameObject.SetActive(false);
    }

    void Start()
    {
        SetupButtons();
        SetupItemSelectCallbacks();

        // 밭 상태 변경 이벤트 구독 (심기/수확/물주기 등 발생 시 UI 자동 갱신)
        FarmManagerExtension.OnPlotStateChangedStatic += OnPlotStateChanged;

        // FarmSelectionMemory에서 선택된 물/비료 변경 시 버튼 아이콘 갱신
        FarmSelectionMemory.OnWaterChanged += (w) => UpdateWaterBtnIcon(w);
        FarmSelectionMemory.OnFertChanged += (f) => UpdateFertBtnIcon(f);

        // 초기 아이콘 설정
        UpdateWaterBtnIcon(FarmSelectionMemory.SelectedWater);
        UpdateFertBtnIcon(FarmSelectionMemory.SelectedFertilizer);
    }

    void OnDestroy()
    {
        FarmManagerExtension.OnPlotStateChangedStatic -= OnPlotStateChanged;
    }

    // ═══ 버튼 설정 ═══════════════════════════════════════════════

    private void SetupButtons()
    {
        closeButton?.onClick.AddListener(ClosePanel);
        plantButton?.onClick.AddListener(OnPlantClicked);
        waterButton?.onClick.AddListener(OnWaterClicked);
        fertilizeButton?.onClick.AddListener(OnFertilizeClicked);
        harvestButton?.onClick.AddListener(OnHarvestClicked);
        instantFinishButton?.onClick.AddListener(OnInstantFinishClicked);
        unlockButton?.onClick.AddListener(OnUnlockClicked);
    }

    private void SetupItemSelectCallbacks()
    {
        if (itemSelectPopup == null) return;

        // 팝업에서 씨앗 선택 시 → plantBtnIcon 갱신
        itemSelectPopup.OnSeedSelected = (crop) =>
        {
            selectedSeed = crop;
            if (plantBtnIcon != null) plantBtnIcon.sprite = crop.seedIcon ?? defaultSeedIcon;
            SoundManager.Instance?.PlayButtonClick();
        };

        // 팝업에서 물 선택 시 → FarmSelectionMemory에 저장 (FarmProgressPanel 슬롯과 동기화)
        itemSelectPopup.OnWaterSelected = (water) =>
        {
            FarmSelectionMemory.SetWater(water);
            SoundManager.Instance?.PlayButtonClick();
        };

        // 팝업에서 비료 선택 시 → FarmSelectionMemory에 저장
        itemSelectPopup.OnFertSelected = (fert) =>
        {
            FarmSelectionMemory.SetFertilizer(fert);
            SoundManager.Instance?.PlayButtonClick();
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
        UIManager.Instance?.ShowMessage($"🌱 [{crop.cropName}] 심을 밭을 선택하세요", Color.green);
    }

    /// <summary>
    /// ★ 핵심 메서드: FarmPlotController에서 밭 클릭 시 이 메서드 호출.
    ///   plotIndex 밭의 현재 상태에 맞게 패널 UI를 구성하고 표시함.
    /// </summary>
    public void OpenForPlot(int plotIndex)
    {
        currentPlotIndex = plotIndex;
        selectedSeed = null; // 이전 씨앗 선택 초기화

        // 씨앗심기 버튼 아이콘 초기화
        if (plantBtnIcon != null && defaultSeedIcon != null)
            plantBtnIcon.sprite = defaultSeedIcon;

        // 패널 활성화 → RefreshUI가 상태에 맞는 버튼 표시
        gameObject.SetActive(true);
        RefreshUI();
        SoundManager.Instance?.PlayPanelOpen();
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        itemSelectPopup?.Hide();
        currentPlotIndex = -1;
        SoundManager.Instance?.PlayPanelClose();
    }

    // ═══ UI 갱신 (밭 상태별 버튼 표시 제어) ═════════════════════

    private void RefreshUI()
    {
        if (currentPlotIndex < 0) return;

        FarmPlotState plot = FarmManager.Instance?.GetPlot(currentPlotIndex);
        if (plot == null) return;

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
                statusText.text = $"🔒 잠긴 밭 (Lv.{reqLv} 필요)";
            if (unlockCostText != null)
                unlockCostText.text = $"💰 {cost:N0}";
            if (progressBar != null) progressBar.value = 0f;
            if (remainText != null) remainText.text = "";

            cropIconImage?.gameObject.SetActive(false);
            waterIcon?.SetActive(false);
            fertIcon?.SetActive(false);

            // 플레이어 레벨 부족 시 해금 버튼 비활성화
            int plyrLv = PlayerStats.Instance?.level ?? 1;
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
                statusText.text = "🌱 빈 밭 — 씨앗을 선택해 심으세요";
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
            instantCostText.text = $"💎 {gem}";
        }

        // ★ 버튼 표시 규칙:
        //   씨앗심기   → 숨김 (이미 작물 있음)
        //   물주기     → 물 안 줬고 수확 가능 아닐 때
        //   비료주기   → 비료 안 줬고 수확 가능 아닐 때
        //   수확       → 수확 가능할 때
        //   빠른수확   → 성장 중(수확 가능 아닐 때)
        SetActive(plantButton, false);
        SetActive(waterButton, !isReady && !plot.isWatered);
        SetActive(fertilizeButton, !isReady && !plot.isFertilized);
        SetActive(harvestButton, isReady);
        SetActive(instantFinishButton, !isReady); // ★ 성장 중이면 항상 표시
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
        if (selectedSeed != null)
        {
            // 씨앗이 선택된 상태 → 바로 심기
            bool ok = FarmManager.Instance?.PlantCrop(currentPlotIndex, selectedSeed) ?? false;
            if (ok) { selectedSeed = null; }
        }
        else
        {
            // 씨앗 미선택 → 씨앗 선택 팝업 열기
            itemSelectPopup?.ShowSeeds(FarmManager.Instance?.allCrops);
        }
    }

    /// <summary>
    /// 물주기 버튼: FarmSelectionMemory에 저장된 물 사용, 없으면 팝업 열기
    /// </summary>
    private void OnWaterClicked()
    {
        WaterData water = FarmSelectionMemory.SelectedWater;
        if (water != null)
            ApplyWater(water);
        else if (availableWaters.Count > 0)
            itemSelectPopup?.ShowWaters(availableWaters);
        else
            FarmManager.Instance?.WaterCrop(currentPlotIndex); // 기본 물주기
    }

    /// <summary>
    /// 비료주기 버튼: FarmSelectionMemory에 저장된 비료 사용, 없으면 팝업 열기
    /// </summary>
    private void OnFertilizeClicked()
    {
        FertilizerData fert = FarmSelectionMemory.SelectedFertilizer;
        if (fert == null)
        {
            // 비료 미선택 → 비료 선택 팝업 열기
            var ferts = FarmManager.Instance?.allFertilizers;
            if (ferts != null && ferts.Count > 0)
                itemSelectPopup?.ShowFertilizers(ferts);
            return;
        }

        // 인벤토리 우선 차감 → 없으면 재화로 즉시 구매
        bool paid = FarmInventoryUI.Instance?.ConsumeFertilizer(fert.fertilizerID) ?? false;
        if (!paid)
        {
            if (fert.costGem > 0) paid = GameManager.Instance?.SpendGem(fert.costGem) ?? false;
            else if (fert.costGold > 0) paid = GameManager.Instance?.SpendGold(fert.costGold) ?? false;
            else paid = true;
        }

        if (!paid) { UIManager.Instance?.ShowMessage("재화 부족!", Color.red); return; }
        FarmManager.Instance?.FertilizeCrop(currentPlotIndex, fert);
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
            });
        }
        else
        {
            // FarmCharacterMover 없어도 즉시 수확
            FarmManager.Instance?.HarvestCrop(currentPlotIndex);
            SoundManager.Instance?.PlayQuestReward();
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

        bool paid = GameManager.Instance?.SpendGem(gemCost) ?? false;
        if (!paid)
        {
            UIManager.Instance?.ShowMessage($"💎 {gemCost} 부족!", Color.red);
            return;
        }

        FarmManager.Instance?.InstantFinish(currentPlotIndex);
        UIManager.Instance?.ShowMessage($"⚡ 즉시 완료! (💎 -{gemCost})", Color.yellow);
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
        bool paid = FarmInventoryUI.Instance?.ConsumeWater(water.waterID) ?? false;
        if (!paid)
        {
            if (water.costGem > 0) paid = GameManager.Instance?.SpendGem(water.costGem) ?? false;
            else if (water.costGold > 0) paid = GameManager.Instance?.SpendGold(water.costGold) ?? false;
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