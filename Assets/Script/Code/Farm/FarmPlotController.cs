using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class FarmPlotController : MonoBehaviour
{
    [Header("===== 밭 설정 =====")]
    [SerializeField] public int plotIndex = 0;

    [Header("===== 빈밭 기본 스프라이트 =====")]
    [Tooltip("작물이 없을 때 표시할 빈 밭 이미지 (SpriteRenderer용)")]
    [SerializeField] private Sprite emptyPlotSprite;
    [Tooltip("작물이 없을 때 표시할 빈 밭 이미지 (Image용)")]
    [SerializeField] private Sprite emptyPlotSpriteUI;

    [Header("===== 작물 아이콘 (둘 중 하나만 연결) =====")]
    [SerializeField] private SpriteRenderer cropSpriteRenderer;
    [SerializeField] private Image cropImage;

    [Header("===== 상태 텍스트 (미연결 가능) =====")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("===== 버튼 패널 =====")]
    [Tooltip("ButtonsPanel GO")]
    [SerializeField] private GameObject buttonsPanel;
    [Tooltip("씨앗심기 버튼")]
    [SerializeField] private Button btnPlant;
    [Tooltip("수확 버튼")]
    [SerializeField] private Button btnHarvest;

    [Header("===== 수확 버튼 (밭 하단, 수확 가능 시만 표시) =====")]
    [Tooltip("수확 완료 시에만 활성화되는 독립 버튼")]
    [SerializeField] private Button harvestButton;

    [Header("===== 이펙트 (미연결 가능) =====")]
    [SerializeField] private GameObject readyEffect;
    [SerializeField] private GameObject selectedFrame;

    private float refreshTimer = 0f;
    private Button plotButton;

    /// <summary>FarmManager.Instance null 시 FindObjectOfType 폴백</summary>
    private FarmManager GetFarmManager()
    {
        FarmManager fm = FarmManager.Instance ?? FindObjectOfType<FarmManager>();
        if (fm != null && FarmManager.Instance == null)
            Debug.LogWarning($"[FarmPlotController] FarmManager.Instance null → FindObjectOfType로 발견: {fm.gameObject.name}");
        return fm;
    }

    void Awake()
    {
        plotButton = GetComponent<Button>();
        if (plotButton == null) plotButton = gameObject.AddComponent<Button>();
        plotButton.transition = Selectable.Transition.None;
        plotButton.onClick.AddListener(OnPlotClicked);

        if (btnPlant != null) btnPlant.onClick.AddListener(OnPlantClicked);
        if (btnHarvest != null) btnHarvest.onClick.AddListener(OnHarvestClicked);
        if (harvestButton != null) harvestButton.onClick.AddListener(OnDirectHarvestClicked);

        if (buttonsPanel != null)
        {
            var blocker = buttonsPanel.GetComponent<PanelClickBlocker>();
            if (blocker == null) buttonsPanel.AddComponent<PanelClickBlocker>();
        }

        if (buttonsPanel != null) buttonsPanel.SetActive(false);
        if (harvestButton != null) harvestButton.gameObject.SetActive(false);
    }

    void Start()
    {
        FarmManagerExtension.OnPlotStateChangedStatic += OnPlotStateChanged;
        RefreshAll();
    }

    void OnDestroy()
    {
        FarmManagerExtension.OnPlotStateChangedStatic -= OnPlotStateChanged;
    }

    void Update()
    {
        refreshTimer += Time.deltaTime;
        if (refreshTimer >= 2f) { refreshTimer = 0f; RefreshSprite(); }
    }

    private void OnPlotClicked()
    {
        var fm = GetFarmManager();
        Debug.Log($"[FarmPlotController] 밭 #{plotIndex} 클릭 — FarmManager:{fm != null}");

        FarmPlotState plot = fm?.GetPlot(plotIndex);
        if (plot == null)
        {
            Debug.LogWarning($"[FarmPlotController] plot null! FarmManager.Instance={FarmManager.Instance}, plotIndex={plotIndex}");
            return;
        }

        SoundManager.Instance?.PlayButtonClick();
        ShowSelectedFrame();

        // ★ 밭 상태(잠금/빈밭/성장중/수확가능)와 무관하게
        //   항상 FarmPlantModePanel을 열어 관리 패널을 표시.
        //   FarmPlantModePanel.RefreshUI()가 상태별 버튼을 자동 구성함.
        OpenManagePanel();
    }

    /// <summary>
    /// ★ 밭 클릭 → FarmPlantModePanel 열기 (모든 상태 대응)
    ///   FarmCharacterMover가 있으면 밭까지 이동 후 패널 표시.
    /// </summary>
    private void OpenManagePanel()
    {
        Debug.Log($"[FarmPlotController] ★ OpenManagePanel — plotIndex:{plotIndex}, FarmPlantModePanel.Instance:{FarmPlantModePanel.Instance != null}, FarmCharacterMover.Instance:{FarmCharacterMover.Instance != null}");
        if (buttonsPanel != null) buttonsPanel.SetActive(false);

        // ★ FarmPlantModePanel.Instance가 null이면 씬에서 탐색
        //   오버레이가 비활성 상태여서 Awake가 안 불린 경우 대비
        if (FarmPlantModePanel.Instance == null)
        {
            var found = FindObjectOfType<FarmPlantModePanel>(true); // includeInactive
            if (found != null)
            {
                Debug.Log($"[FarmPlotController] ★ FarmPlantModePanel 비활성 탐색 성공: {found.gameObject.name}");
                found.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogError("[FarmPlotController] ★ FarmPlantModePanel을 씬에서 찾을 수 없습니다!");
                return;
            }
        }

        if (FarmCharacterMover.Instance != null)
        {
            Debug.Log($"[FarmPlotController] ★ FarmCharacterMover 이동 시작 → plotIndex:{plotIndex}");
            FarmCharacterMover.Instance.MoveToPlot(plotIndex, () =>
            {
                Debug.Log($"[FarmPlotController] ★ 이동 완료 → OpenForPlot({plotIndex}) 호출");
                FarmPlantModePanel.Instance?.OpenForPlot(plotIndex);
                TutorialManager.Instance?.OnActionCompleted("PlotOpened");
            });
        }
        else
        {
            Debug.Log($"[FarmPlotController] ★ FarmCharacterMover 없음 → 즉시 OpenForPlot({plotIndex})");
            FarmPlantModePanel.Instance?.OpenForPlot(plotIndex);
            TutorialManager.Instance?.OnActionCompleted("PlotOpened");
        }
    }

    // ── 하위 buttonsPanel 버튼 호출 (Inspector 연결 호환용) ──

    private void OnPlantClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        OpenManagePanel();
    }

    private void OnHarvestClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        OpenManagePanel();
    }

    /// <summary>
    /// ★ 밭 하단 독립 수확 버튼 클릭 → 바로 수확 실행
    /// </summary>
    private void OnDirectHarvestClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        GetFarmManager()?.HarvestCrop(plotIndex);
        SoundManager.Instance?.PlayQuestReward();
        // 수확 완료 후 버튼 즉시 숨김 (OnPlotStateChanged에서도 갱신됨)
        if (harvestButton != null) harvestButton.gameObject.SetActive(false);
    }

    public void CloseButtonsPanel()
    {
        if (buttonsPanel != null) buttonsPanel.SetActive(false);
    }

    private void RefreshAll() { RefreshSprite(); RefreshUnlockState(); }

    private void RefreshSprite()
    {
        FarmPlotState plot = GetFarmManager()?.GetPlot(plotIndex);
        bool hasSR = cropSpriteRenderer != null;
        bool hasImg = cropImage != null;

        // ★ 작물 없음 → 빈밭 스프라이트 표시
        if (plot == null || plot.currentCrop == null)
        {
            if (hasSR)
            {
                // emptyPlotSprite가 연결돼 있으면 표시, 없으면 숨김
                bool hasEmpty = emptyPlotSprite != null;
                cropSpriteRenderer.sprite = emptyPlotSprite;
                cropSpriteRenderer.enabled = hasEmpty;
            }
            if (hasImg)
            {
                bool hasEmpty = emptyPlotSpriteUI != null;
                cropImage.sprite = emptyPlotSpriteUI;
                cropImage.enabled = hasEmpty;
            }
            if (readyEffect != null) readyEffect.SetActive(false);
            if (harvestButton != null) harvestButton.gameObject.SetActive(false);
            if (statusText != null)
            {
                // ★ 잠금 상태면 "잠김", 아니면 "빈 밭" 표시
                bool locked = plot != null && !plot.isUnlocked;
                statusText.gameObject.SetActive(true);
                statusText.text = locked ? "🔒 잠김" : "빈 밭";
            }
            return;
        }

        // ★ 작물 있음 → 성장 단계 스프라이트 표시
        Sprite sp = plot.currentCrop.GetSpriteForStage(plot.GetStage());
        if (hasSR)
        {
            // 작물 스프라이트가 없으면 빈밭 스프라이트라도 유지
            cropSpriteRenderer.sprite = sp != null ? sp : emptyPlotSprite;
            cropSpriteRenderer.enabled = true;
        }
        if (hasImg)
        {
            cropImage.sprite = sp != null ? sp : emptyPlotSpriteUI;
            cropImage.enabled = true;
        }

        bool isReady = plot.IsReadyToHarvest();
        if (readyEffect != null) readyEffect.SetActive(isReady);
        if (harvestButton != null) harvestButton.gameObject.SetActive(isReady);

        if (statusText != null)
        {
            statusText.gameObject.SetActive(true);
            statusText.text = isReady
                ? "✅ 수확 가능!"
                : $"{plot.currentCrop.cropName} {FarmCropExtension.CalcGrowthProgress(plot) * 100f:F0}%";
        }
    }

    private void RefreshUnlockState()
    {
        // ★ 잠긴 밭도 클릭 가능 (관리 패널에서 해금 버튼 표시)
        if (plotButton != null) plotButton.interactable = true;
    }

    private void ShowSelectedFrame()
    {
        if (selectedFrame == null) return;
        selectedFrame.SetActive(true);
        CancelInvoke(nameof(HideSelectedFrame));
        Invoke(nameof(HideSelectedFrame), 0.5f);
    }

    private void HideSelectedFrame()
    {
        if (selectedFrame != null) selectedFrame.SetActive(false);
    }

    private void OnPlotStateChanged(int idx)
    {
        if (idx == plotIndex) RefreshAll();
    }
}

// ★ ButtonsPanel 빈 영역 클릭 차단
public class PanelClickBlocker : MonoBehaviour,
    IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    public void OnPointerClick(PointerEventData e) { e.Use(); }
    public void OnPointerDown(PointerEventData e) { e.Use(); }
    public void OnPointerUp(PointerEventData e) { e.Use(); }
}

public static class FarmPlotManager
{
    private static FarmPlotController _current;
    public static void CloseOthers(FarmPlotController opened)
    {
        if (_current != null && _current != opened) _current.CloseButtonsPanel();
        _current = opened;
    }
}

public class FarmCharacterMover : MonoBehaviour
{
    public static FarmCharacterMover Instance { get; private set; }

    [Header("===== 이동 설정 =====")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float arriveDistance = 0.3f;
    [SerializeField] private Vector3 plotOffset = new Vector3(0f, -0.5f, 0f);

    [Header("===== 밭 위치 (plotIndex 순서) =====")]
    [SerializeField] private Transform[] plotTransforms;

    [Header("===== Animator =====")]
    [SerializeField] private Animator characterAnimator;

    private static readonly int ANIM_IS_WALKING = Animator.StringToHash("isWalking");
    private Coroutine moveCoroutine;
    public bool IsMoving { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this && Instance.gameObject.scene.isLoaded) { Destroy(gameObject); return; }
        Instance = this;
        Debug.Log("[ManagerInit] FarmCharacterMover가 생성되었습니다.");
        if (characterAnimator == null)
            characterAnimator = GetComponentInChildren<Animator>();
    }

    public void MoveToPlot(int plotIndex, Action onArrived = null)
    {
        if (plotTransforms == null || plotIndex >= plotTransforms.Length
            || plotTransforms[plotIndex] == null)
        { onArrived?.Invoke(); return; }

        Vector3 target = plotTransforms[plotIndex].position + plotOffset;
        if (Vector3.Distance(transform.position, target) <= arriveDistance)
        { onArrived?.Invoke(); return; }

        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(WalkTo(target, onArrived));
    }

    private IEnumerator WalkTo(Vector3 target, Action onArrived)
    {
        IsMoving = true;
        SetWalking(true);

        float dir = target.x - transform.position.x;
        if (Mathf.Abs(dir) > 0.01f)
        {
            var s = transform.localScale;
            s.x = dir > 0 ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
            transform.localScale = s;
        }

        while (Vector3.Distance(transform.position, target) > arriveDistance)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }

        SetWalking(false);
        IsMoving = false;
        moveCoroutine = null;
        onArrived?.Invoke();
    }

    private void SetWalking(bool w)
    {
        if (characterAnimator != null) characterAnimator.SetBool(ANIM_IS_WALKING, w);
    }
}