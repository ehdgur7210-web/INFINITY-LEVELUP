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

    // ── 영역 포커스 ──
    private bool _isAreaFocusActive = false;
    private RectTransform _areaFocusRect = null;

    // ── 숨긴 오브젝트 복원용 ──
    private List<GameObject> _hiddenObjects = new List<GameObject>();

    // ── 마지막으로 장착된 슬롯 타입 (강화 스텝 자동 포커스용) ──
    private EquipmentType? _lastEquippedType = null;

    // ── GC 절감용 캐시 ──
    // GetRootGameObjects() 는 호출마다 배열 할당 → List 재사용 오버로드 사용
    private readonly List<GameObject> _rootObjectsCache = new List<GameObject>();
    // Enum.GetValues() 는 boxing + 배열 할당 → static 미리 캐시
    private static readonly EquipmentType[] _allEquipTypes =
        (EquipmentType[])System.Enum.GetValues(typeof(EquipmentType));

    public bool IsTutorialActive => _isTutorialActive;
    public int CurrentPhase => _currentPhase;

    /// <summary>현재 진행 중인 튜토리얼 스텝 데이터 (외부 참조용)</summary>
    public TutorialStepData GetCurrentStep()
    {
        if (!_isTutorialActive || _activeSteps == null || _currentStep >= _activeSteps.Count)
            return null;
        return _activeSteps[_currentStep];
    }

    /// <summary>
    /// 튜토리얼 진행 중 포커스 대상 외 버튼 차단 여부.
    /// ★ ClickFocusTarget + WaitForAction 모두 차단 (튜토리얼 꼬임 방지)
    /// ★ ClickAnywhere/AutoAdvance만 자유 조작 허용
    /// </summary>
    public bool ShouldBlockNonFocusButtons
    {
        get
        {
            if (!_isTutorialActive || _activeSteps == null || _currentStep >= _activeSteps.Count)
                return false;

            var step = _activeSteps[_currentStep];

            // ClickFocusTarget은 항상 차단
            if (step.advanceType == TutorialAdvanceType.ClickFocusTarget)
                return true;

            // ★ WaitForAction은 항상 차단 (포커스 유무 무관, 다른 버튼 누르면 튜토리얼 꼬임)
            if (step.advanceType == TutorialAdvanceType.WaitForAction)
                return true;

            // ★ 영역 포커스 활성 시에도 영역 밖 차단
            if (step.useAreaFocus)
                return true;

            return false;
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] TutorialManager가 생성되었습니다.");
            // ★ transform.root 대신 자기 자신만 DDOL (부모 Canvas 전체 이동 방지)
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
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
        // ★ 안전망: 사용자가 너무 빨리 장착해서 EquipItem 액션이 누락되는 케이스 방지.
        //   장비 상태 변경 이벤트를 직접 듣고, 현재 스텝이 EquipItem 대기면 즉시 advance.
        EquipmentManager.OnEquipmentChanged += OnEquipmentChangedTutorialHook;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EquipmentManager.OnEquipmentChanged -= OnEquipmentChangedTutorialHook;
    }

    /// <summary>
    /// 장비가 장착/해제될 때마다 호출되는 안전망 훅.
    /// 현재 튜토리얼 스텝이 EquipItem WaitForAction이면 OnActionCompleted를 우회 호출.
    /// </summary>
    private void OnEquipmentChangedTutorialHook(EquipmentType type, EquipmentData equip, int enhLevel)
    {
        if (!_isTutorialActive || equip == null) return;
        if (_activeSteps == null || _currentStep >= _activeSteps.Count) return;

        // ★ 장착된 슬롯 타입 기록 (강화 스텝에서 EquipPanelSlot:LastEquipped로 참조)
        _lastEquippedType = type;

        var step = _activeSteps[_currentStep];

        // ★ WaitForAction + EquipItem 스텝
        if (step.advanceType == TutorialAdvanceType.WaitForAction
            && step.requiredAction == "EquipItem")
        {
            Debug.Log("[Tutorial] OnEquipmentChanged 안전망 트리거 → EquipItem 완료 처리");
            OnActionCompleted("EquipItem");
            return;
        }

        // ★ 장비 장착 스텝인데 advanceType이 ClickFocusTarget인 경우 (Unity가 asset 리버트)
        //   focusTargetName에 "EquipButton"이 포함되어 있으면 장착 감지 시 자동 advance
        if (step.advanceType == TutorialAdvanceType.ClickFocusTarget
            && !string.IsNullOrEmpty(step.focusTargetName)
            && step.focusTargetName.Contains("EquipButton"))
        {
            Debug.Log("[Tutorial] OnEquipmentChanged — EquipButton 스텝 자동 advance (asset 리버트 안전망)");
            NextStep();
        }
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
        // ★ 씬 변경 시 LateUpdate 캐시 초기화
        _cachedLevelUpPanel = null;
        _levelUpPanelSearched = false;
        _cachedOfflineRewardUI = null;

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
        _isAreaFocusActive = false;
        _areaFocusRect = null;
        RestoreCanvasRaycast();
        RestoreHiddenObjects();

        yield return new WaitForSeconds(1.5f);

        // 방치보상 패널 닫기
        if (_cachedOfflineRewardUI == null)
            _cachedOfflineRewardUI = FindObjectOfType<OfflineRewardUI>(true);
        if (_cachedOfflineRewardUI != null) _cachedOfflineRewardUI.ClosePanel();

        // ★ 보상은 DoShowStep(스텝 시작 시)에서 지급 → 여기서 제거

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

        if (_cachedOfflineRewardUI == null)
            _cachedOfflineRewardUI = FindObjectOfType<OfflineRewardUI>(true);
        if (_cachedOfflineRewardUI != null) _cachedOfflineRewardUI.ClosePanel();

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
        _rewardedSteps.Clear(); // ★ 보상 지급 기록 초기화

        if (GameDataBridge.CurrentData != null)
            GameDataBridge.CurrentData.tutorialStep = 0;

        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);

        if (_cachedOfflineRewardUI == null)
            _cachedOfflineRewardUI = FindObjectOfType<OfflineRewardUI>(true);
        if (_cachedOfflineRewardUI != null) _cachedOfflineRewardUI.ClosePanel();

        ShowStep(_currentStep);
        Debug.Log($"[Tutorial] ★ Phase {phase} 시작 ({steps.Count}단계)");
    }

    private void EndCurrentPhase()
    {
        StopWaitCoroutine();
        RestoreCanvasRaycast(); // ★ 월드 타겟 모드 복원
        RestoreHiddenObjects(); // ★ 숨긴 오브젝트 복원
        _isAreaFocusActive = false;
        _areaFocusRect = null;
        _isTutorialActive = false;

        focusMask?.ClearFocus(); // ★ tutorialPanel 비활성화 전에 먼저 해제
        HideTip();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        if (GameDataBridge.CurrentData != null)
        {
            GameDataBridge.CurrentData.tutorialPhase = _currentPhase;
            GameDataBridge.CurrentData.tutorialStep = -1;

            // ★ phase1에 모든 튜토리얼이 통합되었으므로 phase1 완료 = 전체 완료
            if (_currentPhase >= 1)
            {
                GameDataBridge.CurrentData.tutorialPhase = 99;
                GameDataBridge.CurrentData.tutorialCompleted = true;
            }
        }

        SaveLoadManager.Instance?.SaveGame();
        Debug.Log($"[Tutorial] Phase {_currentPhase} 완료! 튜토리얼 전체 종료.");
    }

    // ════════════════════════════════════════
    //  외부 트리거
    // ════════════════════════════════════════

    public void OnPlayerLevelUp(int newLevel)
    {
        // ★ phase1에 모든 튜토리얼 통합 — 레벨업 트리거 불필요
        // 추후 phase2를 따로 만들 경우 여기에 추가
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

        // ★ 보상은 스텝 시작 시 지급 (뽑기 등 보상이 필요한 액션 전에 지급되어야 함)
        GiveStepRewards(step);

        // ★ 이전 스텝에서 숨긴 오브젝트 복원
        RestoreHiddenObjects();

        // ★ 매 스텝마다 강화/레벨업 패널 강제 닫기
        ForceCloseBlockedPanels();

        // ★ 이 스텝에서 숨길 오브젝트 처리
        HideStepTargets(step);

        // ★ 채팅 관련 스텝이면 인벤토리 닫고 채팅 미니바 강제 표시
        bool isChatStep = (!string.IsNullOrEmpty(step.requiredAction) && step.requiredAction.StartsWith("Chat"))
                       || (!string.IsNullOrEmpty(step.focusTargetName) &&
                           (step.focusTargetName.StartsWith("Chat") || step.focusTargetName == "ChatExpandBtn"
                            || step.focusTargetName == "ChatCollapseBtn" || step.focusTargetName == "ChatInputField"));
        if (isChatStep)
        {
            // 인벤토리 닫기 (채팅 바가 가려지지 않게)
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.CloseInventory();
            // 채팅 강제 표시
            if (ChatSystem.Instance != null)
                ChatSystem.Instance.ShowChat();
        }

        // ★ 동료뽑기 관련 스텝: 단계별 인벤토리 제어
        string focusName = step.focusTargetName ?? "";

        // ★ CompanionGachaBtn 스텝: 100뽑기 결과 닫기 후 인벤토리/가챠 유지
        if (focusName == "CompanionGachaBtn")
        {
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.OpenInventory();
            if (ChatSystem.Instance != null)
                ChatSystem.Instance.HideChat();
            Debug.Log("[Tutorial] 동료뽑기 버튼 스텝 — 인벤토리 열기 유지 + 채팅 숨김");
        }

        // 동료 뽑기 진입/결과 단계: 인벤토리 닫기
        // ★ CompanionGachaBtn 제외 — 가챠 결과 확인 직후 인벤토리가 갑자기 닫히는 문제 방지
        //   (동료뽑기 패널이 실제로 열릴 때 인벤토리가 자연스럽게 가려짐)
        bool isCompanionCloseInven = focusName == "CompanionSinglePullBtn"
                                  || focusName == "CompanionResultCloseBtn" || focusName == "CompanionAutoBtn";
        // 동료 슬롯/핫바 단계: 인벤토리 유지 (결과 닫기에서 이미 열림)
        bool isCompanionKeepInven = focusName.StartsWith("CompanionSlot:")
                                 || focusName.Contains("HotbarRegisterButton");

        if (isCompanionCloseInven)
        {
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.CloseInventory();
            if (MailUI.Instance != null && MailUI.Instance.gameObject.activeInHierarchy)
                MailUI.Instance.CloseMailPanel();
            if (ChatSystem.Instance != null)
                ChatSystem.Instance.HideChat();
            Debug.Log("[Tutorial] 동료뽑기 스텝 — 인벤토리/메일/채팅 닫기");
        }
        else if (isCompanionKeepInven)
        {
            // ★ 인벤토리 열기 + 동료 탭 전환 (결과 닫기 후 인벤토리가 닫혀있을 수 있음)
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OpenInventory();
                InventoryManager.Instance.SelectTab(InventoryManager.InvenTabType.Companion);
            }
            if (ChatSystem.Instance != null)
                ChatSystem.Instance.HideChat();
            Debug.Log("[Tutorial] 동료슬롯 스텝 — 인벤토리 열기 + 동료탭 전환");
        }

        // ★ 우편/메뉴 관련 스텝: 채팅 숨기기 + 인벤토리 닫기 + 메뉴 접기
        bool isMailOrMenuStep = focusName == "MailCloseBtn" || focusName.StartsWith("Menu")
                             || focusName.StartsWith("Mail");
        if (isMailOrMenuStep)
        {
            if (ChatSystem.Instance != null)
                ChatSystem.Instance.HideChat();
            // 인벤토리 닫기 (MenuInventoryBtn 제외 — 닫혀있어야 클릭으로 열 수 있음)
            if (focusName != "MenuInventoryBtn" && InventoryManager.Instance != null)
                InventoryManager.Instance.CloseInventory();
            // ★ 메뉴 토글 스텝: 메뉴 접어놓기 (클릭으로 열게)
            if (focusName == "MenuToggleBtn" && TopMenuManager.Instance != null)
                TopMenuManager.Instance.CollapseMenu();
        }

        // ★ 강화 관련 스텝: 채팅 숨기기 + 장비 탭 강제 전환
        bool isEnhanceRelated = focusName == "EnhanceActionBtn" || focusName == "EnhancePanel"
                             || focusName.Contains("BtnClose")
                             || focusName.StartsWith("InvenSlot:")
                             || focusName.StartsWith("EquipPanelSlot:");

        // ★ 강화 스텝 진입 전 장비 미착용 체크 → 장착 스텝으로 강제 이동
        // equipStep != stepIndex 가드: InvenSlot 스텝 자신은 리다이렉트 제외 (무한재귀 방지)
        if (isEnhanceRelated && !IsAnyEquipmentEquipped())
        {
            int equipStep = FindEquipStepIndex();
            if (equipStep >= 0 && equipStep != stepIndex)
            {
                Debug.Log($"[Tutorial] 강화 스텝 진입 but 장비 미착용 → Step {equipStep}(장착)으로 되돌림");
                // ★ 전환 중 다른 버튼 클릭 방지 — 즉시 전체 차단
                focusMask?.BlockAll();
                // ★ Hide Targets로 숨겨진 오브젝트(EquipButton 등) 먼저 복원
                RestoreHiddenObjects();
                UIManager.Instance?.ShowMessage("먼저 장비를 장착해주세요!", Color.yellow);
                _currentStep = equipStep;
                ShowStep(_currentStep);
                return;
            }
        }

        if (isEnhanceRelated)
        {
            // 채팅 숨기기 (화면 겹침 방지)
            if (ChatSystem.Instance != null)
                ChatSystem.Instance.HideChat();
            // 장비 탭 강제 전환 (인벤토리가 열려있으면)
            if (InventoryManager.Instance != null && InventoryManager.Instance.isPanelOpen)
                InventoryManager.Instance.SelectTab(InventoryManager.InvenTabType.Equip);
            // ★ EquipPanelSlot/강화 스텝에서만 슬롯 갱신 (InvenSlot 초반 스텝에서는 불필요)
            if ((focusName.StartsWith("EquipPanelSlot:") || focusName == "EnhanceActionBtn")
                && EquipmentManager.Instance != null)
                EquipmentManager.Instance.RefreshAllPanelSlots();
        }

        SetupFocus(step);
        ShowTip(step);

        // ★ 장착 스텝(EquipButton) 진입 시 이미 장비가 장착되어 있으면 즉시 advance
        if (step.advanceType == TutorialAdvanceType.ClickFocusTarget
            && !string.IsNullOrEmpty(step.focusTargetName)
            && step.focusTargetName.Contains("EquipButton")
            && IsAnyEquipmentEquipped())
        {
            Debug.Log("[Tutorial] EquipButton 스텝 진입 시 이미 장착됨 → 즉시 advance");
            NextStep();
            return;
        }

        // ★ 장착 버튼 포커스 스텝 → LevelUpButton 자동 숨김 (장착 강제)
        //   Inspector Hide Targets에 추가 안 해도 코드에서 자동 처리
        if (!string.IsNullOrEmpty(step.focusTargetName)
            && step.focusTargetName.Contains("EquipButton"))
        {
            SceneManager.GetActiveScene().GetRootGameObjects(_rootObjectsCache);
            foreach (GameObject root in _rootObjectsCache)
                HideAllContainsInChildren(root.transform, "LevelUpButton");
        }

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
                // ★ 액션 대기 — 포커스/영역이 있으면 해당 영역만 구멍 뚫기
                if (!string.IsNullOrEmpty(step.focusTargetName) || step.useAreaFocus)
                {
                    // SetupFocus에서 이미 포커스 마스크 설정됨 → 유지
                    // 포커스 영역 내 클릭만 허용, 나머지 차단
                    Debug.Log($"[Tutorial] WaitForAction + 포커스 마스크: {step.focusTargetName}");
                }
                else
                {
                    // ★ 포커스 대상 없는 WaitForAction → 전체 차단 (BlockAll)
                    //   OnActionCompleted는 코드에서 호출되므로 UI 차단해도 동작함
                    //   단, 닫기 버튼 등은 영역 포커스(areaTargetName)로 허용해야 함
                    focusMask?.BlockAll();
                    Debug.Log($"[Tutorial] WaitForAction 전체 차단 (액션: {step.requiredAction})");
                }

                // ★ 안전망: 사용자가 너무 빨리 장착해서 OnActionCompleted("EquipItem")가
                //   이전 스텝(딜레이/전환 중)에 도착해 누락된 케이스 방지.
                //   현재 장비 상태를 직접 확인 → 이미 뭔가 장착되어 있으면 즉시 advance.
                //   (10연뽑 후 장착 P1_07에도 동일하게 적용됨)
                if (step.requiredAction == "EquipItem" && IsAnyEquipmentEquipped())
                {
                    Debug.Log("[Tutorial] EquipItem 안전망 — 이미 장비 장착됨 → 즉시 advance");
                    NextStep();
                    return;
                }
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
        while (elapsed < 0.5f)
        {
            yield return null; // ★ 1프레임 대기 (즉시 재시도)
            elapsed += Time.unscaledDeltaTime;

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
    // ★ 현재 포커스 대상 오브젝트 (이름 비교 실패 시 참조 비교용)
    private GameObject _currentFocusTargetObj = null;
    // ★ 방금 완료된 스텝의 포커스 대상 (같은 프레임 내 onClick 리스너 허용용)
    private GameObject _justCompletedFocusTarget = null;
    private int _justCompletedFrame = -1;
    // 월드 오브젝트 포커스 시 Canvas 레이캐스트 차단 해제
    private bool _worldTargetMode = false;
    private UnityEngine.UI.GraphicRaycaster _tutorialRaycaster;

    private void SetupFocus(TutorialStepData step)
    {
        _focusButtonBound = false;
        _currentFocusTargetObj = null;
        _isAreaFocusActive = false;
        _areaFocusRect = null;

        // ★ 이전 월드 타겟 모드 복원
        RestoreCanvasRaycast();

        if (string.IsNullOrEmpty(step.focusTargetName) && string.IsNullOrEmpty(step.areaTargetName))
        {
            focusMask?.ClearFocus();
            return;
        }

        // ★ 영역 포커스: areaTargetName이 있으면 해당 영역에 구멍, 없으면 focusTargetName 사용
        if (step.useAreaFocus)
        {
            string areaName = !string.IsNullOrEmpty(step.areaTargetName) ? step.areaTargetName : step.focusTargetName;
            GameObject areaObj = FindTargetByName(areaName);
            RectTransform areaRT = areaObj?.GetComponent<RectTransform>();

            // focusTargetName이 있으면 손가락 포인터를 해당 오브젝트로 지정
            RectTransform fingerRT = null;
            if (!string.IsNullOrEmpty(step.focusTargetName))
            {
                GameObject focusObj = FindTargetByName(step.focusTargetName);
                if (focusObj != null)
                {
                    _currentFocusTargetObj = focusObj;
                    fingerRT = focusObj.GetComponent<RectTransform>();
                }
            }

            if (areaRT != null && focusMask != null)
            {
                if (fingerRT != null && fingerRT != areaRT)
                {
                    // ★ 구멍은 영역, 손가락은 포커스 타겟 (닫기 버튼 등)
                    focusMask.SetFocusWithFingerTarget(areaRT, step.focusPadding, fingerRT);
                }
                else
                {
                    focusMask.SetFocus(areaRT, step.focusPadding);
                }
                _isAreaFocusActive = true;
                _areaFocusRect = areaRT;
                Debug.Log($"[Tutorial] 영역 포커스 설정: {areaName}" +
                          (fingerRT != null ? $" (손가락→{step.focusTargetName})" : ""));
            }
            else
            {
                // ★ 영역 타겟을 못 찾으면 마스크 해제 (클릭 차단 방지)
                Debug.LogWarning($"[Tutorial] 영역 포커스 타겟 못 찾음: {areaName} → 마스크 해제");
                focusMask?.ClearFocus();
                DisableCanvasRaycast();
            }

            // 영역 포커스는 ClickFocusTarget에서 버튼 리스너를 등록하지 않음 (영역 내 자유 클릭)
            // WaitForAction으로 진행하거나, 영역 내 특정 버튼으로 진행
            if (step.advanceType == TutorialAdvanceType.ClickFocusTarget && _currentFocusTargetObj != null)
            {
                Button btn = _currentFocusTargetObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveListener(OnFocusTargetClicked);
                    btn.onClick.AddListener(OnFocusTargetClicked);
                    _focusButtonBound = true;
                }
            }
            return;
        }

        // ★ 일반 포커스 (기존 로직)
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

        _currentFocusTargetObj = target;

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
        Debug.Log($"[Tutorial] OnFocusTargetClicked: step={_currentStep}, target={_currentFocusTargetObj?.name}, frame={Time.frameCount}");

        // ★ 방금 완료된 포커스 대상 기록 (같은 프레임 내 다른 onClick 리스너 허용)
        _justCompletedFocusTarget = _currentFocusTargetObj;
        _justCompletedFrame = Time.frameCount;

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
        _isAdvancing = false;
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

        // ★ SetupFocus에서 이미 찾은 _currentFocusTargetObj 재사용 (FindTargetByName 중복 호출 제거)
        GameObject tipTarget = _currentFocusTargetObj;
        if (tipTarget == null && !string.IsNullOrEmpty(step.focusTargetName))
            tipTarget = FindTargetByName(step.focusTargetName);

        if (tipTarget != null)
        {
            RectTransform targetRT = tipTarget.GetComponent<RectTransform>();
            if (targetRT != null)
            {
                PositionTipNearTarget(targetRT, step.tipPosition);
                return;
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
        RestoreHiddenObjects(); // ★ 숨긴 오브젝트 복원

        // ★ 영역 포커스 초기화
        _isAreaFocusActive = false;
        _areaFocusRect = null;

        // ★ 현재 스텝 리스너 정리 (다음 스텝 전에 반드시)
        if (_activeSteps != null && _currentStep < _activeSteps.Count)
        {
            CleanupCurrentStepListeners(_activeSteps[_currentStep]);
            // ★ 보상은 DoShowStep(스텝 시작 시)에서 지급 → 여기서 제거
        }

        _focusButtonBound = false; // ★ 다음 스텝을 위해 초기화
        _currentStep++;

        // ★ 스텝 전환 사이에 다른 UI 클릭 방지
        //   (delayBeforeShow 동안 마스크가 사라져서 자유 클릭되는 버그 방지)
        //   다음 스텝의 SetupFocus가 적절한 마스크로 덮어쓰므로 안전.
        if (focusMask != null && _currentStep < (_activeSteps?.Count ?? 0))
            focusMask.BlockAll();

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

    /// <summary>
    /// ★ 안전망 헬퍼: 어느 슬롯이든 장비가 장착되어 있는지 확인.
    /// 사용자가 너무 빨리 장착해서 OnActionCompleted("EquipItem")가 누락된
    /// 케이스를 잡기 위해 현재 장착 상태를 직접 폴링.
    /// </summary>
    private bool IsAnyEquipmentEquipped()
    {
        if (EquipmentManager.Instance == null) return false;
        foreach (EquipmentType t in _allEquipTypes) // ★ static 캐시 사용 (Enum.GetValues GC 제거)
        {
            if (EquipmentManager.Instance.IsEquipped(t)) return true;
        }
        return false;
    }

    /// <summary>
    /// 장착 플로우의 시작 스텝 인덱스 반환.
    /// 우선순위: InvenSlot(아이템 클릭) → EquipButton(장착 버튼) → EquipItem(액션)
    /// 없으면 -1.
    /// </summary>
    private int FindEquipStepIndex()
    {
        if (_activeSteps == null) return -1;

        int invenSlotStep = -1;
        int equipButtonStep = -1;

        for (int i = 0; i < _activeSteps.Count; i++)
        {
            var s = _activeSteps[i];
            if (s == null) continue;

            // InvenSlot: → 인벤토리에서 아이템 클릭하는 스텝 (장착 플로우 시작점)
            if (invenSlotStep < 0
                && !string.IsNullOrEmpty(s.focusTargetName)
                && s.focusTargetName.StartsWith("InvenSlot:"))
            {
                invenSlotStep = i;
            }

            // EquipButton → 장착 버튼 클릭 스텝
            if (equipButtonStep < 0
                && ((!string.IsNullOrEmpty(s.focusTargetName) && s.focusTargetName.Contains("EquipButton"))
                    || (!string.IsNullOrEmpty(s.requiredAction) && s.requiredAction == "EquipItem")))
            {
                equipButtonStep = i;
            }
        }

        // InvenSlot 스텝이 있으면 거기서부터 (포커스가 아이템에 잡힘)
        // 없으면 EquipButton 스텝
        if (invenSlotStep >= 0) return invenSlotStep;
        return equipButtonStep;
    }

    // ★ 이미 보상을 지급한 스텝 인덱스 (중복 지급 방지)
    private HashSet<int> _rewardedSteps = new HashSet<int>();

    private void GiveStepRewards(TutorialStepData step)
    {
        if (step.rewards == null || step.rewards.Length == 0) return;

        // ★ 이미 지급한 스텝은 건너뛰기
        int stepIdx = _activeSteps != null ? _activeSteps.IndexOf(step) : -1;
        if (stepIdx >= 0 && !_rewardedSteps.Add(stepIdx))
        {
            Debug.Log($"[Tutorial] 보상 중복 방지: Step {stepIdx} 이미 지급됨");
            return;
        }

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
                    // ★ ResourceBarManager + GameDataBridge 양쪽 모두 업데이트 (씬 전환 후 유실 방지)
                    if (ResourceBarManager.Instance != null) ResourceBarManager.Instance.AddEquipmentTickets(r.amount);
                    if (GameDataBridge.CurrentData != null) GameDataBridge.CurrentData.equipmentTickets += (ResourceBarManager.Instance == null ? r.amount : 0);
                    Debug.Log($"[Tutorial] ★ 장비 티켓 +{r.amount} 지급 (ResourceBar:{ResourceBarManager.Instance != null})");
                    UIManager.Instance?.ShowMessage($"+{r.amount} 장비 티켓", Color.green);
                    break;
                case TutorialRewardType.CompanionTicket:
                    if (ResourceBarManager.Instance != null) ResourceBarManager.Instance.AddCompanionTickets(r.amount);
                    if (GameDataBridge.CurrentData != null) GameDataBridge.CurrentData.companionTickets += (ResourceBarManager.Instance == null ? r.amount : 0);
                    Debug.Log($"[Tutorial] ★ 동료 티켓 +{r.amount} 지급 (ResourceBar:{ResourceBarManager.Instance != null})");
                    UIManager.Instance?.ShowMessage($"+{r.amount} 동료 티켓", Color.green);
                    break;
                case TutorialRewardType.CropPoint:
                    CropPointService.Add(r.amount);
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
                // ★ 클릭 감지 즉시 전체 차단 — 같은 클릭이 AUTO/가챠 버튼에도 전달되는 것 방지
                focusMask?.BlockAll();
                yield return null; // 1프레임 대기 후 NextStep (UI 이벤트 처리 분리)
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
        if (go == null)
            return false;

        // ★ 방금 완료된 스텝의 포커스 대상이면 같은 프레임 내에서 허용
        //   (OnFocusTargetClicked → NextStep 후 같은 onClick에서 실행되는 핸들러 보호)
        if (_justCompletedFocusTarget != null && Time.frameCount == _justCompletedFrame)
        {
            if (go == _justCompletedFocusTarget || go.transform.IsChildOf(_justCompletedFocusTarget.transform))
                return true;
        }

        if (!_isTutorialActive || _activeSteps == null || _currentStep >= _activeSteps.Count)
            return false;

        // ★ 영역 포커스 모드: 포커스 영역 내 모든 오브젝트 허용
        if (_isAreaFocusActive && _areaFocusRect != null && focusMask != null)
        {
            RectTransform goRT = go.GetComponent<RectTransform>();
            if (goRT != null)
            {
                // 오브젝트의 스크린 좌표가 포커스 영역 안에 있는지 체크
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, goRT.position);
                if (focusMask.IsPointInsideFocusArea(screenPos))
                    return true;
            }
        }

        // ★ 오브젝트 참조 비교 (이름이 달라도 같은 오브젝트면 허용)
        if (_currentFocusTargetObj != null && go == _currentFocusTargetObj)
            return true;
        // ★ 부모 중에 포커스 대상이 있으면 허용 (자식 버튼 클릭 시)
        if (_currentFocusTargetObj != null && go.transform.IsChildOf(_currentFocusTargetObj.transform))
            return true;
        TutorialStepData step = _activeSteps[_currentStep];
        if (string.IsNullOrEmpty(step.focusTargetName))
            return false;
        return go.name == step.focusTargetName;
    }

    // ════════════════════════════════════════
    //  오브젝트 숨기기/복원
    // ════════════════════════════════════════

    /// <summary>현재 스텝이 강화 관련 단계인지 (강화패널 닫기 예외 처리용)</summary>
    private bool IsEnhanceRelatedStep()
    {
        if (_activeSteps == null || _currentStep >= _activeSteps.Count) return false;
        var step = _activeSteps[_currentStep];
        string fn = step.focusTargetName ?? "";
        return fn == "EnhanceActionBtn" || fn == "EnhancePanel" || fn.Contains("BtnClose")
            || fn.StartsWith("EquipPanelSlot:");
    }

    /// <summary>튜토리얼 중 열리면 안 되는 패널 강제 닫기 (매 스텝마다 호출)</summary>
    private void ForceCloseBlockedPanels()
    {
        bool isEnhanceStep = IsEnhanceRelatedStep();

        // 강화 패널 — 강화 관련 단계에서는 닫지 않음
        if (!isEnhanceStep)
        {
            // ★ FindObjectOfType 대신 Instance 사용 (GC 절감)
            var enhance = EnhancementSystem.Instance;
            if (enhance != null && enhance.enhancementPanel != null && enhance.enhancementPanel.activeSelf)
                enhance.enhancementPanel.SetActive(false);
        }

        // 레벨업 패널 — 캐시 사용
        if (!_levelUpPanelSearched)
        {
            _cachedLevelUpPanel = EquipmentLevelUpPanel.Instance ?? FindObjectOfType<EquipmentLevelUpPanel>(true);
            _levelUpPanelSearched = true;
        }
        if (_cachedLevelUpPanel != null && _cachedLevelUpPanel.gameObject.activeSelf)
            _cachedLevelUpPanel.gameObject.SetActive(false);

        // 동료 뽑기 패널 — Instance 사용 (FindObjectOfType 제거)
        var compGacha = CompanionGachaManager.Instance;
        if (compGacha != null && compGacha.companionGachaPanel != null
            && compGacha.companionGachaPanel.activeSelf
            && !(_activeSteps[_currentStep].focusTargetName == "CompanionGachaBtn"
              || _activeSteps[_currentStep].focusTargetName == "CompanionSinglePullBtn"))
        {
            compGacha.companionGachaPanel.SetActive(false);
        }
    }

    /// <summary>이 스텝의 hideTargets에 지정된 오브젝트 숨기기</summary>
    private void HideStepTargets(TutorialStepData step)
    {
        if (step.hideTargets == null || step.hideTargets.Length == 0) return;

        foreach (string targetName in step.hideTargets)
        {
            if (string.IsNullOrEmpty(targetName)) continue;

            // ★ "AllContains:키워드" → 해당 키워드를 포함하는 모든 활성 오브젝트 숨기기
            // ★ FindObjectsOfType<Transform> 대신 씬 루트 DFS로 GC 절감
            if (targetName.StartsWith("AllContains:"))
            {
                string keyword = targetName.Substring("AllContains:".Length);
                int before = _hiddenObjects.Count;
                // ★ GetRootGameObjects(List) 오버로드로 배열 할당 제거
                SceneManager.GetActiveScene().GetRootGameObjects(_rootObjectsCache);
                foreach (GameObject root in _rootObjectsCache)
                    HideAllContainsInChildren(root.transform, keyword);
                continue;
            }

            GameObject obj = FindTargetByName(targetName);
            if (obj != null && obj.activeSelf)
            {
                obj.SetActive(false);
                _hiddenObjects.Add(obj);
                Debug.Log($"[Tutorial] 숨김: {targetName}");
            }
        }
    }

    /// <summary>
    /// AllContains용 DFS — FindObjectsOfType&lt;Transform&gt; 대신 씬 루트부터 순회 (GC 절감)
    /// </summary>
    private void HideAllContainsInChildren(Transform parent, string keyword)
    {
        if (parent == null) return;
        if (parent.gameObject.activeInHierarchy && parent.name.Contains(keyword))
        {
            parent.gameObject.SetActive(false);
            _hiddenObjects.Add(parent.gameObject);
        }
        for (int i = 0; i < parent.childCount; i++)
            HideAllContainsInChildren(parent.GetChild(i), keyword);
    }

    /// <summary>숨긴 오브젝트 전부 복원</summary>
    private void RestoreHiddenObjects()
    {
        foreach (var obj in _hiddenObjects)
            if (obj != null) obj.SetActive(true);
        _hiddenObjects.Clear();
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
            focusMask?.ClearFocus(); // ★ 마스크 먼저 해제
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
        }
    }

    private GameObject FindTargetByName(string targetName)
    {
        if (string.IsNullOrEmpty(targetName)) return null;

        // ═══ 특수 패턴 처리 ═══

        // "FarmPlot:N" → plotIndex=N인 FarmPlotController
        // ★ FindObjectsOfType<FarmPlotController> 제거 → 씬 DFS로 교체
        if (targetName.StartsWith("FarmPlot:"))
        {
            string idxStr = targetName.Substring("FarmPlot:".Length);
            if (int.TryParse(idxStr, out int idx))
            {
                SceneManager.GetActiveScene().GetRootGameObjects(_rootObjectsCache);
                foreach (GameObject root in _rootObjectsCache)
                {
                    var plot = FindFarmPlotInChildren(root.transform, idx);
                    if (plot != null)
                    {
                        var img = plot.GetComponentInChildren<Image>();
                        if (img != null && img.GetComponent<RectTransform>() != null)
                            return img.gameObject;
                        return plot.gameObject;
                    }
                }
            }
            return null;
        }

        // "MailCloseBtn" → 메일 닫기 버튼
        if (targetName == "MailCloseBtn")
        {
            var mail = MailUI.Instance ?? FindObjectOfType<MailUI>(true);
            if (mail != null && mail.CloseMailButton != null) return mail.CloseMailButton.gameObject;
            return null;
        }

        // "MailClaimAllBtn" → 메일 모두받기 버튼
        if (targetName == "MailClaimAllBtn")
        {
            var mail = MailUI.Instance ?? FindObjectOfType<MailUI>(true);
            if (mail != null && mail.ClaimAllButton != null) return mail.ClaimAllButton.gameObject;
            return null;
        }

        // "MailCouponBtn" → 메일 쿠폰 버튼
        if (targetName == "MailCouponBtn")
        {
            var mail = MailUI.Instance ?? FindObjectOfType<MailUI>(true);
            if (mail != null && mail.OpenCouponButton != null) return mail.OpenCouponButton.gameObject;
            return null;
        }

        // "MailCouponInput" → 쿠폰 입력 필드
        if (targetName == "MailCouponInput")
        {
            var mail = MailUI.Instance ?? FindObjectOfType<MailUI>(true);
            if (mail != null && mail.CouponInput != null) return mail.CouponInput.gameObject;
            return null;
        }

        // "MailCouponPanel" → 쿠폰 입력 패널 전체
        if (targetName == "MailCouponPanel")
        {
            var mail = MailUI.Instance ?? FindObjectOfType<MailUI>(true);
            if (mail != null && mail.CouponPanel != null) return mail.CouponPanel;
            return null;
        }

        // "ChatExpandBtn" → 채팅 펼치기 버튼
        if (targetName == "ChatExpandBtn")
        {
            var chat = ChatSystem.Instance ?? FindObjectOfType<ChatSystem>(true);
            if (chat != null && chat.expandButton != null) return chat.expandButton.gameObject;
            return null;
        }

        // "ChatCollapseBtn" → 채팅 축소 버튼
        if (targetName == "ChatCollapseBtn")
        {
            var chat = ChatSystem.Instance ?? FindObjectOfType<ChatSystem>(true);
            if (chat != null && chat.collapseButton != null) return chat.collapseButton.gameObject;
            return null;
        }

        // "ChatInputField" → 채팅 입력 필드
        if (targetName == "ChatInputField")
        {
            var chat = ChatSystem.Instance ?? FindObjectOfType<ChatSystem>(true);
            if (chat != null && chat.chatInputField != null) return chat.chatInputField.gameObject;
            return null;
        }

        // "MenuToggleBtn" → 메뉴 토글 버튼
        if (targetName == "MenuToggleBtn")
        {
            var mgr = TopMenuManager.Instance;
            if (mgr != null && mgr.ToggleButton != null) return mgr.ToggleButton.gameObject;
            return null;
        }

        // "MenuInventoryBtn" → 메뉴 인벤토리 버튼
        if (targetName == "MenuInventoryBtn")
        {
            var mgr = TopMenuManager.Instance;
            if (mgr != null && mgr.InventoryButton != null) return mgr.InventoryButton.gameObject;
            return null;
        }

        // "MenuMailBtn" → 메뉴 메일 버튼
        if (targetName == "MenuMailBtn")
        {
            var mgr = TopMenuManager.Instance;
            if (mgr != null && mgr.MailButton != null) return mgr.MailButton.gameObject;
            return null;
        }

        // "MenuEnhanceBtn" → 메뉴 강화 버튼
        if (targetName == "MenuEnhanceBtn")
        {
            var mgr = TopMenuManager.Instance;
            if (mgr != null && mgr.EnhancementButton != null) return mgr.EnhancementButton.gameObject;
            return null;
        }

        // "CompanionGachaBtn" → 동료뽑기 버튼
        // ★ FindObjectsOfType<Button> 제거 → 씬 루트 DFS로 교체
        if (targetName == "CompanionGachaBtn")
        {
            SceneManager.GetActiveScene().GetRootGameObjects(_rootObjectsCache);
            foreach (GameObject root in _rootObjectsCache)
            {
                Transform result = FindInChildrenContains(root.transform, "동료뽑");
                if (result != null)
                {
                    Button btn = result.GetComponent<Button>() ?? result.GetComponentInParent<Button>();
                    if (btn != null) return btn.gameObject;
                }
            }
            return null;
        }

        // "CompanionSinglePullBtn" → 동료 1회 뽑기 버튼
        if (targetName == "CompanionSinglePullBtn")
        {
            var mgr = CompanionGachaManager.Instance ?? FindObjectOfType<CompanionGachaManager>(true);
            if (mgr != null && mgr.singlePullBtn != null) return mgr.singlePullBtn.gameObject;
            return null;
        }

        // "EnhanceActionBtn" → 강화 실행 버튼
        if (targetName == "EnhanceActionBtn")
        {
            var sys = EnhancementSystem.Instance ?? FindObjectOfType<EnhancementSystem>(true);
            if (sys != null && sys.enhanceButton != null) return sys.enhanceButton.gameObject;
            return null;
        }

        // "EnhancePanel" → 강화 패널
        if (targetName == "EnhancePanel")
        {
            var sys = EnhancementSystem.Instance ?? FindObjectOfType<EnhancementSystem>(true);
            if (sys != null && sys.enhancementPanel != null) return sys.enhancementPanel;
            return null;
        }

        // "PlantModePanel" → 작물관리 패널 (FarmPlantModePanel 자체)
        if (targetName == "PlantModePanel")
        {
            var panel = FarmPlantModePanel.Instance ?? FindObjectOfType<FarmPlantModePanel>(true);
            if (panel != null) return panel.gameObject;
            return null;
        }

        // "PlantModeCloseBtn" → 작물관리 닫기 버튼 (closeButton 필드 직접 참조)
        if (targetName == "PlantModeCloseBtn")
        {
            var panel = FarmPlantModePanel.Instance ?? FindObjectOfType<FarmPlantModePanel>(true);
            if (panel != null && panel.CloseButton != null)
                return panel.CloseButton.gameObject;
            return null;
        }

        // "CompanionResultPanel" → 동료 뽑기 결과 패널
        if (targetName == "CompanionResultPanel")
        {
            var mgr = CompanionGachaManager.Instance ?? FindObjectOfType<CompanionGachaManager>(true);
            if (mgr != null && mgr.resultPanel != null) return mgr.resultPanel;
            return null;
        }

        // "CompanionResultCloseBtn" → 동료 뽑기 결과 닫기 버튼
        if (targetName == "CompanionResultCloseBtn")
        {
            var mgr = CompanionGachaManager.Instance ?? FindObjectOfType<CompanionGachaManager>(true);
            if (mgr != null && mgr.resultCloseBtn != null) return mgr.resultCloseBtn.gameObject;
            return null;
        }

        // "CompanionAutoBtn" → 동료 오토 버튼
        if (targetName == "CompanionAutoBtn")
        {
            var hotbarMgr = CompanionHotbarManager.Instance ?? FindObjectOfType<CompanionHotbarManager>(true);
            if (hotbarMgr != null && hotbarMgr.autoButton != null)
                return hotbarMgr.autoButton.gameObject;
            return null;
        }

        // "HundredGachaBtn" → 100연차 버튼
        if (targetName == "HundredGachaBtn")
        {
            var gachaUI = GachaUI.Instance ?? FindObjectOfType<GachaUI>(true);
            if (gachaUI != null && gachaUI.hundredGachaButton != null)
                return gachaUI.hundredGachaButton.gameObject;
            return null;
        }

        // "CompanionSlot:N" → 동료 인벤토리 N번째 활성 슬롯
        if (targetName.StartsWith("CompanionSlot:"))
        {
            string idxStr = targetName.Substring("CompanionSlot:".Length);
            if (int.TryParse(idxStr, out int idx) && InventoryManager.Instance != null)
            {
                var parent = InventoryManager.Instance.companionContainer;
                if (parent != null)
                {
                    int count = 0;
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        var child = parent.GetChild(i);
                        if (child.gameObject.activeSelf)
                        {
                            if (count == idx) return child.gameObject;
                            count++;
                        }
                    }
                }
            }
            return null;
        }

        // "EquipLevelUpPanel" → 장비 레벨업 패널 전체 (튜토리얼 중 숨김용)
        if (targetName == "EquipLevelUpPanel")
        {
            // ★ _cachedLevelUpPanel 재사용 (FindObjectOfType 제거)
            var panel = EquipmentLevelUpPanel.Instance
                        ?? (_cachedLevelUpPanel != null ? _cachedLevelUpPanel : FindObjectOfType<EquipmentLevelUpPanel>(true));
            if (panel != null) return panel.gameObject;
            return null;
        }

        // "GachaResultCloseBtn" → 가챠 결과 닫기 버튼 (포커스 포인터용)
        if (targetName == "GachaResultCloseBtn")
        {
            var resultUI = GachaResultUI.Instance ?? FindObjectOfType<GachaResultUI>(true);
            if (resultUI != null && resultUI.closeButton != null)
                return resultUI.closeButton.gameObject;
            return null;
        }

        // "CropShopPanel" → 작물상점 전체 패널 (영역 포커스용)
        if (targetName == "CropShopPanel")
        {
            var shop = FarmCropShopUI.Instance ?? FindObjectOfType<FarmCropShopUI>(true);
            if (shop != null && shop.shopPanel != null) return shop.shopPanel;
            if (shop != null) return shop.gameObject;
            return null;
        }

        // "CropShopCloseBtn" → 작물상점 닫기 버튼
        if (targetName == "CropShopCloseBtn")
        {
            var shop = FarmCropShopUI.Instance ?? FindObjectOfType<FarmCropShopUI>(true);
            if (shop != null && shop.closeButton != null) return shop.closeButton.gameObject;
            return null;
        }

        // "CropShopDetail" → 작물상점 디테일+구매 패널
        if (targetName == "CropShopDetail")
        {
            var shop = FarmCropShopUI.Instance ?? FindObjectOfType<FarmCropShopUI>(true);
            if (shop != null && shop.detailPanel != null) return shop.detailPanel;
            return null;
        }

        // "EquipPanelSlot:타입" → 캐릭터 장비 패널 슬롯
        // ★ FindObjectsOfType<EquipPanelSlot> 제거 → EquipmentManager 또는 씬 DFS로 교체
        if (targetName.StartsWith("EquipPanelSlot:"))
        {
            string typeStr = targetName.Substring("EquipPanelSlot:".Length);

            // ★ "LastEquipped" → 마지막으로 장착한 슬롯 타입 사용
            EquipmentType eqType;
            if (typeStr == "LastEquipped")
            {
                if (_lastEquippedType == null)
                {
                    Debug.LogWarning("[Tutorial] EquipPanelSlot:LastEquipped — _lastEquippedType=null, 장착된 첫 슬롯으로 폴백");
                    // 장착 이력 없으면 현재 장착된 슬롯 중 첫 번째 반환
                    if (EquipmentManager.Instance != null)
                    {
                        foreach (var t in _allEquipTypes)
                        {
                            var s = EquipmentManager.Instance.GetEquipPanelSlot(t);
                            if (s != null && s.currentEquipment != null)
                            {
                                Debug.Log($"[Tutorial] 폴백 슬롯: {t} → {s.gameObject.name}");
                                return s.gameObject;
                            }
                        }
                    }
                    return null;
                }
                eqType = _lastEquippedType.Value;
                Debug.Log($"[Tutorial] EquipPanelSlot:LastEquipped → eqType={eqType}");
            }
            else if (!System.Enum.TryParse<EquipmentType>(typeStr, out eqType))
            {
                Debug.LogWarning($"[Tutorial] EquipPanelSlot 타입 파싱 실패: '{typeStr}'");
                return null;
            }

            // eqType 결정됨 → 슬롯 조회
            if (EquipmentManager.Instance != null)
            {
                var slot = EquipmentManager.Instance.GetEquipPanelSlot(eqType);
                if (slot != null)
                {
                    Debug.Log($"[Tutorial] EquipPanelSlot 찾음: {eqType} → {slot.gameObject.name}, worldPos={slot.transform.position}");
                    return slot.gameObject;
                }
                Debug.LogWarning($"[Tutorial] EquipPanelSlot {eqType} — GetEquipPanelSlot 반환 null (등록된 슬롯 수={EquipmentManager.Instance.GetRegisteredSlotCount()})");
            }
            // 폴백: 씬 DFS
            SceneManager.GetActiveScene().GetRootGameObjects(_rootObjectsCache);
            foreach (GameObject root in _rootObjectsCache)
            {
                var slot = FindEquipPanelSlotInChildren(root.transform, eqType);
                if (slot != null)
                {
                    Debug.Log($"[Tutorial] EquipPanelSlot DFS 폴백 찾음: {slot.gameObject.name}");
                    return slot.gameObject;
                }
            }
            Debug.LogWarning($"[Tutorial] EquipPanelSlot {eqType} — 씬 전체 DFS에서도 못 찾음");
            return null;
        }

        // "EquipSlotParent" → 장비 슬롯 부모 (영역 포커스용)
        if (targetName == "EquipSlotParent")
        {
            if (InventoryManager.Instance != null)
            {
                var parent = InventoryManager.Instance.GetEquipSlotParent();
                if (parent != null) return parent.gameObject;
            }
            return null;
        }

        // "InvenSlot:N" → 인벤토리 N번째 활성 슬롯 (0부터)
        if (targetName.StartsWith("InvenSlot:"))
        {
            string idxStr = targetName.Substring("InvenSlot:".Length);
            if (int.TryParse(idxStr, out int idx) && InventoryManager.Instance != null)
            {
                var parent = InventoryManager.Instance.GetEquipSlotParent();
                if (parent != null)
                {
                    int count = 0;
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        var child = parent.GetChild(i);
                        if (child.gameObject.activeSelf)
                        {
                            if (count == idx) return child.gameObject;
                            count++;
                        }
                    }
                }
            }
            return null;
        }

        // "ChildOf:부모이름/자식이름" → 특정 부모 아래 자식 찾기
        if (targetName.StartsWith("ChildOf:"))
        {
            string path = targetName.Substring("ChildOf:".Length);
            string[] parts = path.Split('/');
            if (parts.Length == 2)
            {
                GameObject parentObj = GameObject.Find(parts[0]);
                if (parentObj != null)
                {
                    Transform child = FindInChildren(parentObj.transform, parts[1]);
                    if (child != null) return child.gameObject;
                }
            }
            return null;
        }

        // "Contains:키워드" → 이름에 키워드가 포함된 첫 번째 활성 오브젝트
        if (targetName.StartsWith("Contains:"))
        {
            string keyword = targetName.Substring("Contains:".Length);
            // ★ List 재사용 (GetRootGameObjects 배열 할당 제거)
            SceneManager.GetActiveScene().GetRootGameObjects(_rootObjectsCache);
            foreach (GameObject root in _rootObjectsCache)
            {
                Transform result = FindInChildrenContains(root.transform, keyword);
                if (result != null) return result.gameObject;
            }
            // DontDestroyOnLoad: tutorialPanel 부모 등 DDOL 오브젝트만 직접 탐색
            // FindObjectsOfType<Transform> 제거 → 씬 루트만으로 충분한 경우가 대부분
            return null;
        }

        // ═══ 기본 이름 검색 ═══

        // 1. 직접 검색 (활성 오브젝트)
        GameObject found = GameObject.Find(targetName);
        if (found != null) return found;

        // 2. 씬 루트 DFS (비활성 자식 포함) — List 재사용
        SceneManager.GetActiveScene().GetRootGameObjects(_rootObjectsCache);
        foreach (GameObject root in _rootObjectsCache)
        {
            Transform result = FindInChildren(root.transform, targetName);
            if (result != null) return result.gameObject;
        }

        return null;
    }

    private FarmPlotController FindFarmPlotInChildren(Transform parent, int plotIndex)
    {
        var plot = parent.GetComponent<FarmPlotController>();
        if (plot != null && plot.plotIndex == plotIndex) return plot;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindFarmPlotInChildren(parent.GetChild(i), plotIndex);
            if (found != null) return found;
        }
        return null;
    }

    private EquipPanelSlot FindEquipPanelSlotInChildren(Transform parent, EquipmentType eqType)
    {
        var slot = parent.GetComponent<EquipPanelSlot>();
        if (slot != null && slot.slotType == eqType) return slot;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindEquipPanelSlotInChildren(parent.GetChild(i), eqType);
            if (found != null) return found;
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

    private Transform FindInChildrenContains(Transform parent, string keyword)
    {
        if (parent.gameObject.activeInHierarchy && parent.name.Contains(keyword)) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindInChildrenContains(parent.GetChild(i), keyword);
            if (found != null) return found;
        }
        return null;
    }

    // ── LateUpdate 캐싱 ──
    private EquipmentLevelUpPanel _cachedLevelUpPanel;
    private bool _levelUpPanelSearched = false;
    private OfflineRewardUI _cachedOfflineRewardUI;

    /// <summary>매 프레임 차단 패널 감시 — 튜토리얼 중 열리면 안 되는 패널 즉시 닫기</summary>
    void LateUpdate()
    {
        if (!_isTutorialActive || !ShouldBlockNonFocusButtons) return;

        bool isEnhanceStep = IsEnhanceRelatedStep();

        // 강화 패널 — 강화 관련 단계에서는 닫지 않음
        if (!isEnhanceStep)
        {
            var enhance = EnhancementSystem.Instance;
            if (enhance != null && enhance.enhancementPanel != null && enhance.enhancementPanel.activeSelf)
                enhance.enhancementPanel.SetActive(false);
        }

        // 레벨업 패널 (강화 단계가 아닐 때만)
        if (!isEnhanceStep)
        {
            // ★ FindObjectOfType 캐싱 (매 프레임 호출 방지)
            if (!_levelUpPanelSearched)
            {
                _cachedLevelUpPanel = EquipmentLevelUpPanel.Instance ?? FindObjectOfType<EquipmentLevelUpPanel>(true);
                _levelUpPanelSearched = true;
            }
            if (_cachedLevelUpPanel != null && _cachedLevelUpPanel.gameObject.activeSelf)
                _cachedLevelUpPanel.gameObject.SetActive(false);
        }
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
