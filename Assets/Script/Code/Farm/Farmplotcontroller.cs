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

    [Header("===== 이펙트 (미연결 가능) =====")]
    [SerializeField] private GameObject readyEffect;
    [SerializeField] private GameObject selectedFrame;

    private float refreshTimer = 0f;
    private Button plotButton;

    void Awake()
    {
        plotButton = GetComponent<Button>();
        if (plotButton == null) plotButton = gameObject.AddComponent<Button>();
        plotButton.transition = Selectable.Transition.None;
        plotButton.onClick.AddListener(OnPlotClicked);

        if (btnPlant != null) btnPlant.onClick.AddListener(OnPlantClicked);
        if (btnHarvest != null) btnHarvest.onClick.AddListener(OnHarvestClicked);

        if (buttonsPanel != null)
        {
            var blocker = buttonsPanel.GetComponent<PanelClickBlocker>();
            if (blocker == null) buttonsPanel.AddComponent<PanelClickBlocker>();
        }

        if (buttonsPanel != null) buttonsPanel.SetActive(false);
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
        FarmPlotState plot = FarmManager.Instance?.GetPlot(plotIndex);
        if (plot == null || !plot.isUnlocked) return;

        SoundManager.Instance?.PlayButtonClick();
        ShowSelectedFrame();

        if (buttonsPanel == null)
        {
            if (plot.IsReadyToHarvest()) DoHarvest();
            else if (plot.currentCrop == null) DoPlant();
            return;
        }

        if (buttonsPanel.activeSelf)
        {
            buttonsPanel.SetActive(false);
            return;
        }

        bool empty = plot.currentCrop == null;
        bool ready = plot.IsReadyToHarvest();
        if (!empty && !ready) return;

        if (btnPlant != null) btnPlant.gameObject.SetActive(empty);
        if (btnHarvest != null) btnHarvest.gameObject.SetActive(ready);

        buttonsPanel.SetActive(true);
        FarmPlotManager.CloseOthers(this);
    }

    private void OnPlantClicked()
    {
        if (buttonsPanel != null) buttonsPanel.SetActive(false);
        SoundManager.Instance?.PlayButtonClick();
        DoPlant();
    }

    private void DoPlant()
    {
        if (FarmCharacterMover.Instance != null)
            FarmCharacterMover.Instance.MoveToPlot(plotIndex, () =>
                FarmPlantModePanel.Instance?.OpenForPlot(plotIndex));
        else
            FarmPlantModePanel.Instance?.OpenForPlot(plotIndex);
    }

    private void OnHarvestClicked()
    {
        if (buttonsPanel != null) buttonsPanel.SetActive(false);
        SoundManager.Instance?.PlayButtonClick();
        DoHarvest();
    }

    private void DoHarvest()
    {
        if (FarmCharacterMover.Instance != null)
            FarmCharacterMover.Instance.MoveToPlot(plotIndex, () =>
            {
                FarmManager.Instance?.HarvestCrop(plotIndex);
                SoundManager.Instance?.PlayQuestReward();
            });
        else
        {
            FarmManager.Instance?.HarvestCrop(plotIndex);
            SoundManager.Instance?.PlayQuestReward();
        }
    }

    public void CloseButtonsPanel()
    {
        if (buttonsPanel != null) buttonsPanel.SetActive(false);
    }

    private void RefreshAll() { RefreshSprite(); RefreshUnlockState(); }

    private void RefreshSprite()
    {
        FarmPlotState plot = FarmManager.Instance?.GetPlot(plotIndex);
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
        FarmPlotState plot = FarmManager.Instance?.GetPlot(plotIndex);
        if (plotButton != null) plotButton.interactable = plot != null && plot.isUnlocked;
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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
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