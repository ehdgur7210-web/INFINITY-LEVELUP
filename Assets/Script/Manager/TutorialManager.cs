using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 튜토리얼 매니저 — DDOL (씬 간 유지)
///
/// ★ 씬 전환 지원:
///   - DontDestroyOnLoad로 Canvas + UI 전체 유지
///   - SceneManager.sceneLoaded 이벤트로 씬 전환 감지
///   - isSceneTransitionStep인 단계에서 씬이 바뀌면 자동 진행
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("━━━ 튜토리얼 데이터 (단계별) ━━━")]
    public List<TutorialStepData> phase1Steps = new List<TutorialStepData>();
    public List<TutorialStepData> phase2Steps = new List<TutorialStepData>();
    public List<TutorialStepData> phase3Steps = new List<TutorialStepData>();

    [Header("━━━ UI 참조 ━━━")]
    public GameObject tutorialPanel;
    public TutorialFocusMask focusMask;

    [Header("━━━ 팁 텍스트 ━━━")]
    public RectTransform tipPanel;
    public TextMeshProUGUI tipText;
    public float tipOffset = 80f;

    [Header("━━━ 팁 기본 위치 (포커스 없을 때) ━━━")]
    [Tooltip("포커스 대상이 없을 때 팁 위치 (앵커 기준)")]
    public Vector2 defaultTipPosition = new Vector2(0f, 200f);

    // ── 내부 상태 ──
    private List<TutorialStepData> _activeSteps;
    private int _currentStep = 0;
    private bool _isTutorialActive = false;
    private int _currentPhase = 0;
    private Coroutine _waitCoroutine;

    public bool IsTutorialActive => _isTutorialActive;
    public int CurrentPhase => _currentPhase;

    /// <summary>
    /// 현재 튜토리얼이 ClickFocusTarget 단계인지 확인.
    /// true이면 포커스 대상 외 버튼을 차단해야 함.
    /// WaitForAction/ClickAnywhere/AutoAdvance 단계에서는 false → 자유 조작 허용.
    /// </summary>
    public bool ShouldBlockNonFocusButtons
    {
        get
        {
            if (!_isTutorialActive || _activeSteps == null || _currentStep >= _activeSteps.Count)
                return false;
            return _activeSteps[_currentStep].advanceType == TutorialAdvanceType.ClickFocusTarget;
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }
        else
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        if (Instance != this) return;

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        int savedPhase = GameDataBridge.CurrentData?.tutorialPhase ?? 0;
        int savedStep = GameDataBridge.CurrentData?.tutorialStep ?? -1;

        if (savedPhase == 0 && savedStep <= 0)
        {
            StartCoroutine(DelayedStart(1.5f, 1));
        }
        else if (savedStep >= 0 && savedPhase > 0 && savedPhase < 99)
        {
            StartCoroutine(ResumeFromSave(savedPhase, savedStep));
        }
    }

    void OnDestroy()
    {
        if (_isTutorialActive && GameDataBridge.CurrentData != null)
            GameDataBridge.CurrentData.tutorialStep = _currentStep;
        if (Instance == this) Instance = null;
    }

    // ════════════════════════════════════════
    //  씬 전환 감지 (DDOL이므로 여기서 처리)
    // ════════════════════════════════════════

    // ★ 씬 전환 코루틴 중복 실행 방지
    private bool _sceneAdvancing = false;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_isTutorialActive || _activeSteps == null) return;
        if (_currentStep >= _activeSteps.Count) return;

        TutorialStepData step = _activeSteps[_currentStep];

        // 씬 전환 단계 → 자동으로 다음 스텝 진행
        if (step.isSceneTransitionStep)
        {
            if (_sceneAdvancing) return; // ★ 중복 방지
            _sceneAdvancing = true;
            Debug.Log($"[Tutorial] 씬 전환 감지 ({scene.name}) → Step {_currentStep} 자동 진행");
            StartCoroutine(AdvanceAfterSceneLoad());
            return;
        }

        // 씬 전환이 아닌 스텝에서 씬이 바뀐 경우 → 현재 스텝 다시 표시
        StartCoroutine(RefreshAfterSceneLoad());
    }

    private IEnumerator AdvanceAfterSceneLoad()
    {
        // ★ 이전 스텝의 코루틴/리스너 완전 정리 (씬 전환 전 스텝의 잔여물 제거)
        StopWaitCoroutine();
        if (_activeSteps != null && _currentStep < _activeSteps.Count)
            CleanupCurrentStepListeners(_activeSteps[_currentStep]);
        _focusButtonBound = false;
        _isAdvancing = false;
        RestoreCanvasRaycast();

        yield return new WaitForSeconds(1.5f);

        // 방치보상 패널 닫기
        var offlineUI = FindObjectOfType<OfflineRewardUI>(true);
        if (offlineUI != null) offlineUI.ClosePanel();

        // 현재 스텝 보상 지급 후 다음으로
        if (_activeSteps != null && _currentStep < _activeSteps.Count)
            GiveStepRewards(_activeSteps[_currentStep]);

        _currentStep++;
        _sceneAdvancing = false; // ★ 중복 방지 해제
        ShowStep(_currentStep);
    }

    private IEnumerator RefreshAfterSceneLoad()
    {
        yield return new WaitForSeconds(1.0f);
        if (_isTutorialActive && _activeSteps != null && _currentStep < _activeSteps.Count)
            ShowStep(_currentStep);
    }

    // ════════════════════════════════════════
    //  시작 / 재개
    // ════════════════════════════════════════

    private IEnumerator DelayedStart(float delay, int phase)
    {
        yield return new WaitForSeconds(delay);
        StartPhase(phase);
    }

    private IEnumerator ResumeFromSave(int phase, int step)
    {
        yield return new WaitForSeconds(1.5f);

        List<TutorialStepData> steps = GetPhaseSteps(phase);
        if (steps == null || step >= steps.Count)
        {
            Debug.LogWarning($"[Tutorial] 재개 실패: Phase {phase} Step {step}");
            yield break;
        }

        _currentPhase = phase;
        _activeSteps = steps;
        _currentStep = step;
        _isTutorialActive = true;

        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);

        var offlineUI = FindObjectOfType<OfflineRewardUI>(true);
        if (offlineUI != null) offlineUI.ClosePanel();

        ShowStep(_currentStep);
        Debug.Log($"[Tutorial] ★ Phase {phase} Step {step + 1} 재개");
    }

    // ════════════════════════════════════════
    //  Phase 시작/종료
    // ════════════════════════════════════════

    public void StartPhase(int phase)
    {
        List<TutorialStepData> steps = GetPhaseSteps(phase);
        if (steps == null || steps.Count == 0)
        {
            Debug.LogWarning($"[Tutorial] Phase {phase} 데이터 없음");
            return;
        }

        _currentPhase = phase;
        _activeSteps = steps;
        _currentStep = 0;
        _isTutorialActive = true;

        if (GameDataBridge.CurrentData != null)
            GameDataBridge.CurrentData.tutorialStep = 0;

        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);

        var offlineUI = FindObjectOfType<OfflineRewardUI>(true);
        if (offlineUI != null) offlineUI.ClosePanel();

        ShowStep(_currentStep);
        Debug.Log($"[Tutorial] ★ Phase {phase} 시작 ({steps.Count}단계)");
    }

    private void EndCurrentPhase()
    {
        StopWaitCoroutine();
        RestoreCanvasRaycast(); // ★ 월드 타겟 모드 복원
        _isTutorialActive = false;

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        focusMask?.ClearFocus();
        HideTip();

        if (GameDataBridge.CurrentData != null)
        {
            GameDataBridge.CurrentData.tutorialPhase = _currentPhase;
            GameDataBridge.CurrentData.tutorialStep = -1;

            if (_currentPhase >= 3)
            {
                GameDataBridge.CurrentData.tutorialPhase = 99;
                GameDataBridge.CurrentData.tutorialCompleted = true;
            }
        }

        SaveLoadManager.Instance?.SaveGame();
        Debug.Log($"[Tutorial] Phase {_currentPhase} 완료!");

        if (_currentPhase == 2)
            StartCoroutine(DelayedStart(2f, 3));
    }

    // ════════════════════════════════════════
    //  외부 트리거
    // ════════════════════════════════════════

    public void OnPlayerLevelUp(int newLevel)
    {
        if (_isTutorialActive) return;
        int savedPhase = GameDataBridge.CurrentData?.tutorialPhase ?? 0;

        if (savedPhase == 1 && newLevel >= 5)
            StartCoroutine(DelayedStart(1f, 2));
    }

    // ════════════════════════════════════════
    //  단계 표시
    // ════════════════════════════════════════

    private void ShowStep(int stepIndex)
    {
        if (_activeSteps == null || stepIndex >= _activeSteps.Count)
        {
            EndCurrentPhase();
            return;
        }

        TutorialStepData step = _activeSteps[stepIndex];

        // ★ null 스텝 방어 (Inspector에서 빈 슬롯 또는 Missing SO)
        if (step == null)
        {
            Debug.LogWarning($"[Tutorial] Step {stepIndex} 데이터가 null → 스킵");
            _currentStep++;
            ShowStep(_currentStep);
            return;
        }

        if (step.delayBeforeShow > 0f)
        {
            StartCoroutine(DelayedShowStep(step.delayBeforeShow, stepIndex));
            return;
        }

        DoShowStep(step, stepIndex);
    }

    private IEnumerator DelayedShowStep(float delay, int stepIndex)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (!_isTutorialActive || _currentStep != stepIndex) yield break;
        DoShowStep(_activeSteps[stepIndex], stepIndex);
    }

    private void DoShowStep(TutorialStepData step, int stepIndex)
    {
        _stepShownFrame = Time.frameCount; // ★ 이 프레임의 클릭은 무시됨

        if (GameDataBridge.CurrentData != null)
            GameDataBridge.CurrentData.tutorialStep = stepIndex;

        SetupFocus(step);
        ShowTip(step);

        switch (step.advanceType)
        {
            case TutorialAdvanceType.ClickFocusTarget:
                // ★ SetupFocus에서 이미 버튼 리스너 등록 성공 → Retry 불필요
                if (!_focusButtonBound && _waitCoroutine == null)
                    _waitCoroutine = StartCoroutine(RetryFocusTargetCoroutine(step));
                break;
            case TutorialAdvanceType.AutoAdvance:
                // ★ 자동 진행 — 전체 차단 (포커스 없으면)
                if (string.IsNullOrEmpty(step.focusTargetName))
                    focusMask?.BlockAll();
                _waitCoroutine = StartCoroutine(AutoAdvanceCoroutine(step.autoAdvanceDelay));
                break;
            case TutorialAdvanceType.ClickAnywhere:
                // ★ 아무데나 클릭 — 전체 차단 (Input.GetMouseButtonDown으로 감지하므로 UI 차단해도 OK)
                if (string.IsNullOrEmpty(step.focusTargetName))
                    focusMask?.BlockAll();
                _waitCoroutine = StartCoroutine(WaitForAnyClick());
                break;
            case TutorialAdvanceType.WaitForAction:
                // ★ 액션 대기 — 게임 내 상호작용이 필요하므로 Canvas 레이캐스트 해제
                // 팁 텍스트만 표시하고, 터치는 게임UI/월드까지 완전 통과
                focusMask?.ClearFocus();
                DisableCanvasRaycast();
                break;
        }

        Debug.Log($"[Tutorial] Phase{_currentPhase} Step {stepIndex + 1}/{_activeSteps.Count}: {step.tipMessage}");
    }

    /// <summary>
    /// ClickFocusTarget에서 타겟을 못 찾았을 때 최대 5초간 재시도 후 ClickAnywhere 폴백
    /// </summary>
    private IEnumerator RetryFocusTargetCoroutine(TutorialStepData step)
    {
        float elapsed = 0f;
        while (elapsed < 5f)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            elapsed += 0.5f;

            if (!_isTutorialActive) yield break;

            GameObject target = FindTargetByName(step.focusTargetName);
            if (target == null) continue;

            // 타겟 발견 → 포커스 설정
            RectTransform rt = target.GetComponent<RectTransform>();
            if (rt != null && focusMask != null)
                focusMask.SetFocus(rt, step.focusPadding);

            Button btn = target.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(OnFocusTargetClicked); // ★ 중복 방지
                btn.onClick.AddListener(OnFocusTargetClicked);
                _focusButtonBound = true;
                Debug.Log($"[Tutorial] 재시도 성공: {step.focusTargetName}");
                _waitCoroutine = null;
                yield break;
            }
            else
            {
                // Button 없으면 ClickAnywhere 폴백
                Debug.LogWarning($"[Tutorial] 재시도 성공했으나 Button 없음 → ClickAnywhere 폴백");
                _waitCoroutine = StartCoroutine(WaitForAnyClick());
                yield break;
            }
        }

        // 5초 경과 → ClickAnywhere 폴백
        Debug.LogWarning($"[Tutorial] '{step.focusTargetName}' 5초 내 못 찾음 → ClickAnywhere 폴백");
        _waitCoroutine = StartCoroutine(WaitForAnyClick());
    }

    // ════════════════════════════════════════
    //  포커스 시스템
    // ════════════════════════════════════════

    // SetupFocus에서 버튼 리스너 등록 성공 여부 (Retry 코루틴 중복 방지)
    private bool _focusButtonBound = false;
    // 월드 오브젝트 포커스 시 Canvas 레이캐스트 차단 해제
    private bool _worldTargetMode = false;
    private UnityEngine.UI.GraphicRaycaster _tutorialRaycaster;

    private void SetupFocus(TutorialStepData step)
    {
        _focusButtonBound = false;

        // ★ 이전 월드 타겟 모드 복원
        RestoreCanvasRaycast();

        if (string.IsNullOrEmpty(step.focusTargetName))
        {
            focusMask?.ClearFocus();
            return;
        }

        GameObject target = FindTargetByName(step.focusTargetName);
        if (target == null)
        {
            Debug.LogWarning($"[Tutorial] 포커스 대상 없음: {step.focusTargetName}");
            focusMask?.ClearFocus();
            return;
        }

        RectTransform rt = target.GetComponent<RectTransform>();

        // ★ 월드 오브젝트 판별: RectTransform이 없으면 월드 오브젝트
        bool isWorldObject = (rt == null);

        if (rt != null && focusMask != null)
            focusMask.SetFocus(rt, step.focusPadding);

        if (step.advanceType == TutorialAdvanceType.ClickFocusTarget)
        {
            Button btn = target.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(OnFocusTargetClicked);
                btn.onClick.AddListener(OnFocusTargetClicked);
                _focusButtonBound = true;
                _stepShownFrame = Time.frameCount; // ★ 등록 프레임 기록
            }
            else if (isWorldObject)
            {
                // ★ 월드 오브젝트: Canvas 레이캐스트 비활성화하여 터치가 월드까지 도달
                DisableCanvasRaycast();
                focusMask?.ClearFocus(); // 오버레이 마스크도 해제
                Debug.Log($"[Tutorial] '{step.focusTargetName}' = 월드 오브젝트 → Canvas 레이캐스트 해제, WaitForAction 대기");
            }
            else
            {
                Debug.LogWarning($"[Tutorial] '{step.focusTargetName}'에 Button 없음 → ClickAnywhere 폴백");
                _waitCoroutine = StartCoroutine(WaitForAnyClick());
            }
        }
    }

    /// <summary>1프레임 지연 후 버튼 리스너 등록 — 이전 클릭 이벤트 잔여 방지</summary>
    private IEnumerator DelayedAddClickListener(Button btn)
    {
        yield return null; // 1프레임 대기
        if (btn != null && _isTutorialActive)
        {
            btn.onClick.RemoveListener(OnFocusTargetClicked);
            btn.onClick.AddListener(OnFocusTargetClicked);
        }
    }

    /// <summary>TutorialCanvas의 GraphicRaycaster를 비활성화 (월드 터치 허용)</summary>
    private void DisableCanvasRaycast()
    {
        _worldTargetMode = true;

        if (_tutorialRaycaster == null && tutorialPanel != null)
        {
            Canvas canvas = tutorialPanel.GetComponentInParent<Canvas>();
            if (canvas != null)
                _tutorialRaycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        if (_tutorialRaycaster != null)
            _tutorialRaycaster.enabled = false;
    }

    /// <summary>Canvas 레이캐스트 복원</summary>
    private void RestoreCanvasRaycast()
    {
        if (_worldTargetMode)
        {
            _worldTargetMode = false;
            if (_tutorialRaycaster != null)
                _tutorialRaycaster.enabled = true;
        }
    }

    // ★ 재진입 방지 + 같은 프레임 클릭 무시
    private bool _isAdvancing = false;
    private int _stepShownFrame = -1;

    private void OnFocusTargetClicked()
    {
        if (_isAdvancing) return;
        if (!_isTutorialActive) return;
        if (_activeSteps == null || _currentStep >= _activeSteps.Count) return;

        // ★ 스텝이 표시된 프레임과 같은 프레임의 클릭은 무시 (이전 스텝 잔여 클릭 방지)
        if (Time.frameCount <= _stepShownFrame) return;

        _isAdvancing = true;

        TutorialStepData step = _activeSteps[_currentStep];

        // 리스너 정리 — 현재 스텝의 모든 리스너 제거
        CleanupCurrentStepListeners(step);

        // 씬 전환 스텝: 클릭은 처리하되, 다음 스텝은 sceneLoaded에서 진행
        if (step.isSceneTransitionStep)
        {
            Debug.Log($"[Tutorial] 씬 전환 단계 클릭 → sceneLoaded 대기");
            _isAdvancing = false;
            return;
        }

        NextStep();
        _isAdvancing = false; // ★ 락 해제
    }

    /// <summary>현재 스텝의 버튼 리스너 완전 제거</summary>
    private void CleanupCurrentStepListeners(TutorialStepData step)
    {
        if (string.IsNullOrEmpty(step?.focusTargetName)) return;

        GameObject target = FindTargetByName(step.focusTargetName);
        if (target == null) return;

        Button btn = target.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveListener(OnFocusTargetClicked);
            // ★ 혹시 남아있을 수 있는 중복 리스너까지 완전 제거
            btn.onClick.RemoveListener(OnFocusTargetClicked);
        }
    }

    // ════════════════════════════════════════
    //  팁 텍스트
    // ════════════════════════════════════════

    private void ShowTip(TutorialStepData step)
    {
        if (tipPanel == null || tipText == null) return;

        if (string.IsNullOrEmpty(step.tipMessage))
        {
            HideTip();
            return;
        }

        tipText.text = step.tipMessage;
        tipPanel.gameObject.SetActive(true);

        if (!string.IsNullOrEmpty(step.focusTargetName))
        {
            GameObject target = FindTargetByName(step.focusTargetName);
            if (target != null)
            {
                RectTransform targetRT = target.GetComponent<RectTransform>();
                if (targetRT != null)
                {
                    PositionTipNearTarget(targetRT, step.tipPosition);
                    return;
                }
            }
        }

        // 포커스 대상 없으면 Inspector에서 설정한 기본 위치 사용
        tipPanel.anchoredPosition = defaultTipPosition;
    }

    private void PositionTipNearTarget(RectTransform target, TipPosition position)
    {
        if (tipPanel == null) return;

        Canvas canvas = tipPanel.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        RectTransform canvasRT = canvas.GetComponent<RectTransform>();
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, target.position);
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPoint, cam, out localPoint);

        float targetHalf = target.rect.height * 0.5f;
        float tipHalf = tipPanel.rect.height * 0.5f;

        if (position == TipPosition.Above)
            localPoint.y += targetHalf + tipOffset + tipHalf;
        else
            localPoint.y -= targetHalf + tipOffset + tipHalf;

        // ★ 화면 안에 클램핑 (Canvas 범위 내로 제한)
        Vector2 canvasSize = canvasRT.rect.size;
        float tipWidth = tipPanel.rect.width * 0.5f;

        float minX = -canvasSize.x * 0.5f + tipWidth + 10f;
        float maxX =  canvasSize.x * 0.5f - tipWidth - 10f;
        float minY = -canvasSize.y * 0.5f + tipHalf + 10f;
        float maxY =  canvasSize.y * 0.5f - tipHalf - 10f;

        localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
        localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

        tipPanel.anchoredPosition = localPoint;
    }

    private void HideTip()
    {
        if (tipPanel != null)
            tipPanel.gameObject.SetActive(false);
    }

    // ════════════════════════════════════════
    //  진행 + 보상
    // ════════════════════════════════════════

    public void NextStep()
    {
        StopWaitCoroutine();
        RestoreCanvasRaycast(); // ★ 월드 타겟 모드 복원

        // ★ 현재 스텝 리스너 정리 (다음 스텝 전에 반드시)
        if (_activeSteps != null && _currentStep < _activeSteps.Count)
        {
            CleanupCurrentStepListeners(_activeSteps[_currentStep]);
            GiveStepRewards(_activeSteps[_currentStep]);
        }

        _focusButtonBound = false; // ★ 다음 스텝을 위해 초기화
        _currentStep++;
        ShowStep(_currentStep);
    }

    public void OnActionCompleted(string actionName)
    {
        if (!_isTutorialActive) return;
        if (_activeSteps == null || _currentStep >= _activeSteps.Count) return;

        TutorialStepData step = _activeSteps[_currentStep];
        if (step.advanceType == TutorialAdvanceType.WaitForAction)
        {
            if (string.IsNullOrEmpty(step.requiredAction) || step.requiredAction == actionName)
            {
                Debug.Log($"[Tutorial] 액션 완료: {actionName}");
                NextStep();
            }
        }
    }

    private void GiveStepRewards(TutorialStepData step)
    {
        if (step.rewards == null || step.rewards.Length == 0) return;

        foreach (var r in step.rewards)
        {
            if (r.amount <= 0) continue;

            switch (r.rewardType)
            {
                case TutorialRewardType.Gold:
                    if (GameManager.Instance != null) GameManager.Instance.AddGold(r.amount);
                    else if (GameDataBridge.CurrentData != null) GameDataBridge.CurrentData.playerGold += r.amount;
                    UIManager.Instance?.ShowMessage($"+{r.amount:N0} 골드", Color.yellow);
                    break;
                case TutorialRewardType.Gem:
                    if (GameManager.Instance != null) GameManager.Instance.AddGem(r.amount);
                    else if (GameDataBridge.CurrentData != null) GameDataBridge.CurrentData.playerGem += r.amount;
                    UIManager.Instance?.ShowMessage($"+{r.amount:N0} 다이아", Color.cyan);
                    break;
                case TutorialRewardType.EquipmentTicket:
                    if (ResourceBarManager.Instance != null) ResourceBarManager.Instance.AddEquipmentTickets(r.amount);
                    else if (GameDataBridge.CurrentData != null) GameDataBridge.CurrentData.equipmentTickets += r.amount;
                    UIManager.Instance?.ShowMessage($"+{r.amount} 장비 티켓", Color.green);
                    break;
                case TutorialRewardType.CompanionTicket:
                    if (ResourceBarManager.Instance != null) ResourceBarManager.Instance.AddCompanionTickets(r.amount);
                    else if (GameDataBridge.CurrentData != null) GameDataBridge.CurrentData.companionTickets += r.amount;
                    UIManager.Instance?.ShowMessage($"+{r.amount} 동료 티켓", Color.green);
                    break;
                case TutorialRewardType.CropPoint:
                    if (GameDataBridge.CurrentData != null) GameDataBridge.CurrentData.cropPoints += r.amount;
                    UIManager.Instance?.ShowMessage($"+{r.amount} 작물 포인트", Color.green);
                    break;
                case TutorialRewardType.Item:
                    if (r.item != null && InventoryManager.Instance != null)
                    {
                        InventoryManager.Instance.AddItem(r.item, r.amount);
                        UIManager.Instance?.ShowMessage($"+{r.amount} {r.item.itemName}", Color.green);
                    }
                    break;
            }
            Debug.Log($"[Tutorial] 보상: {r.rewardType} x{r.amount}");
        }
    }

    private IEnumerator AutoAdvanceCoroutine(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (_isTutorialActive) NextStep();
    }

    private IEnumerator WaitForAnyClick()
    {
        // ★ 스텝 표시 프레임이 지날 때까지 대기
        while (Time.frameCount <= _stepShownFrame)
            yield return null;
        while (_isTutorialActive)
        {
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                NextStep();
                yield break;
            }
            yield return null;
        }
    }

    private void StopWaitCoroutine()
    {
        if (_waitCoroutine != null)
        {
            StopCoroutine(_waitCoroutine);
            _waitCoroutine = null;
        }
    }

    // ════════════════════════════════════════
    //  유틸
    // ════════════════════════════════════════

    /// <summary>
    /// 주어진 GameObject가 현재 튜토리얼 스텝의 포커스 대상인지 확인.
    /// TopMenuManager 등에서 튜토리얼 중 포커스 외 버튼 차단에 사용.
    /// </summary>
    public bool IsCurrentFocusTarget(GameObject go)
    {
        if (!_isTutorialActive || _activeSteps == null || _currentStep >= _activeSteps.Count)
            return false;
        TutorialStepData step = _activeSteps[_currentStep];
        if (string.IsNullOrEmpty(step.focusTargetName) || go == null)
            return false;
        return go.name == step.focusTargetName;
    }

    private List<TutorialStepData> GetPhaseSteps(int phase)
    {
        switch (phase)
        {
            case 1: return phase1Steps;
            case 2: return phase2Steps;
            case 3: return phase3Steps;
            default: return null;
        }
    }

    public bool IsAllCompleted()
    {
        return (GameDataBridge.CurrentData?.tutorialPhase ?? 0) >= 99;
    }

    public bool IsTutorialCompleted()
    {
        return GameDataBridge.CurrentData?.tutorialCompleted ?? false;
    }

    public void SetTutorialCompleted(bool completed)
    {
        if (GameDataBridge.CurrentData != null)
            GameDataBridge.CurrentData.tutorialCompleted = completed;
        if (completed)
        {
            _isTutorialActive = false;
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
        }
    }

    private GameObject FindTargetByName(string targetName)
    {
        if (string.IsNullOrEmpty(targetName)) return null;

        // 1. 직접 검색 (활성 오브젝트)
        GameObject found = GameObject.Find(targetName);
        if (found != null) return found;

        // 2. 씬 루트 DFS (비활성 자식 포함)
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform result = FindInChildren(root.transform, targetName);
            if (result != null) return result.gameObject;
        }

        // 3. ★ FarmPlot 특수 검색: "FarmPlot:N" → plotIndex=N인 FarmPlotController
        if (targetName.StartsWith("FarmPlot:"))
        {
            string idxStr = targetName.Substring("FarmPlot:".Length);
            int idx;
            if (int.TryParse(idxStr, out idx))
            {
                var plots = FindObjectsOfType<FarmPlotController>(true);
                foreach (var p in plots)
                {
                    if (p.plotIndex == idx) return p.gameObject;
                }
            }
        }

        return null;
    }

    private Transform FindInChildren(Transform parent, string targetName)
    {
        if (parent.name == targetName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindInChildren(parent.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }

#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T) && !_isTutorialActive) StartPhase(1);
        if (Input.GetKeyDown(KeyCode.Y) && !_isTutorialActive) StartPhase(2);
        if (Input.GetKeyDown(KeyCode.U) && !_isTutorialActive) StartPhase(3);
    }
#endif
}
