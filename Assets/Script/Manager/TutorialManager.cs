using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 튜토리얼 매니저
/// 
/// ★ 변경사항:
///   - 화살표/손가락은 TutorialFocusMask가 자동 처리
///   - SetupArrow()는 포커스 없는 단계에서만 arrowIndicator 표시
///   - 포커스 있는 단계에서는 FocusMask의 fingerIcon이 자동으로 이동+펄스
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("튜토리얼 데이터")]
    public List<TutorialStepData> tutorialSteps = new List<TutorialStepData>();

    [Header("UI 참조")]
    public GameObject tutorialPanel;
    public TutorialFocusMask focusMask;
    public Image overlayBackground;

    [Header("다이얼로그")]
    public GameObject dialogBox;
    public TextMeshProUGUI messageText;
    public Image characterImage;
    public Image guideImage;
    public Button nextButton;

    [Header("화살표 (포커스 없는 단계에서만 사용)")]
    public RectTransform arrowIndicator;

    [Header("설정")]
    public float typingSpeed = 0.03f;
    public Color overlayColor = new Color(0, 0, 0, 0.7f);

    private int currentStep = 0;
    private bool isTyping = false;
    private bool isTutorialActive = false;
    private Coroutine typingCoroutine;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            enabled = false; // Start() 차단
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (Instance != this) return; // ★ 이미 있음

        // ★ 개발 중: 튜토리얼 완전 비활성화
        isTutorialActive = false;
        PlayerPrefs.SetInt("TutorialCompleted", 1);
        PlayerPrefs.Save();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextClicked);

        // AutoStartTutorial 계속 주석 유지
        //if (!IsTutorialCompleted())
        //    StartCoroutine(AutoStartTutorial());
    }

    private IEnumerator AutoStartTutorial()
    {
        yield return new WaitForSeconds(1f);
        StartTutorial();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            StartTutorial();
        }
    }

    // ============================================
    // 튜토리얼 시작/종료
    // ============================================

    public void StartTutorial()
    {
        if (tutorialSteps.Count == 0) return;

        currentStep = 0;
        isTutorialActive = true;
        tutorialPanel.SetActive(true);

        if (overlayBackground != null)
            overlayBackground.color = overlayColor;

        ShowStep(currentStep);
        Debug.Log("[Tutorial] 튜토리얼 시작");
    }

    public void EndTutorial()
    {
        isTutorialActive = false;
        tutorialPanel.SetActive(false);
        focusMask?.ClearFocus();

        // ★ 화살표도 숨기기
        if (arrowIndicator != null)
            arrowIndicator.gameObject.SetActive(false);

        PlayerPrefs.SetInt("TutorialCompleted", 1);
        PlayerPrefs.Save();

        Time.timeScale = 1f;
        Debug.Log("[Tutorial] 튜토리얼 완료!");
    }

    public bool IsTutorialCompleted()
    {
        return PlayerPrefs.GetInt("TutorialCompleted", 0) == 1;
    }

    // ============================================
    // 단계 표시
    // ============================================

    private void ShowStep(int stepIndex)
    {
        if (stepIndex >= tutorialSteps.Count)
        {
            EndTutorial();
            return;
        }

        TutorialStepData step = tutorialSteps[stepIndex];

        // 1. 텍스트 (타이핑 효과)
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeText(step.message));

        // 2. 캐릭터 이미지
        if (characterImage != null)
        {
            if (step.characterImage != null)
            {
                characterImage.sprite = step.characterImage;
                characterImage.gameObject.SetActive(true);
            }
            else
            {
                characterImage.gameObject.SetActive(false);
            }
        }

        // 3. 가이드 이미지
        if (guideImage != null)
        {
            if (step.guideImage != null)
            {
                guideImage.sprite = step.guideImage;
                guideImage.gameObject.SetActive(true);
            }
            else
            {
                guideImage.gameObject.SetActive(false);
            }
        }

        // 4. ★ 포커스 설정 (손가락 이동 + 펄스 애니메이션은 FocusMask가 자동 처리)
        SetupFocus(step);

        // 5. ★ 화살표 (포커스 없는 단계에서만 별도 표시)
        SetupArrow(step);

        // 6. 진행 버튼
        nextButton.gameObject.SetActive(
            step.advanceType == TutorialAdvanceType.ClickAnywhere);

        // 7. 자동 진행
        if (step.advanceType == TutorialAdvanceType.AutoAdvance)
        {
            StartCoroutine(AutoAdvance(step.autoAdvanceDelay));
        }

        Debug.Log($"[Tutorial] Step {stepIndex + 1}/{tutorialSteps.Count}: {step.message}");
    }

    // ============================================
    // 포커스 시스템
    // ============================================

    private void SetupFocus(TutorialStepData step)
    {
        if (string.IsNullOrEmpty(step.focusTargetName))
        {
            // 포커스 대상 없음 → 마스크 해제, 오버레이 켜기
            focusMask?.ClearFocus();

            if (overlayBackground != null)
                overlayBackground.gameObject.SetActive(true);
            return;
        }

        // 포커스 있으면 오버레이 끄기 (FocusMask가 어둡게 처리)
        if (overlayBackground != null)
            overlayBackground.gameObject.SetActive(false);

        // ★ 포커스 대상 찾기 (비활성 오브젝트도 찾을 수 있도록!)
        // GameObject.Find()는 활성 오브젝트만 찾아서 실패할 수 있음
        GameObject target = FindTargetByName(step.focusTargetName);
        if (target != null)
        {
            RectTransform rt = target.GetComponent<RectTransform>();
            if (rt != null && focusMask != null)
            {
                // ★ FocusMask.SetFocus() → 포커스 영역 + 손가락 이동 + 펄스 애니메이션 자동!
                focusMask.SetFocus(rt, step.focusSize);
            }

            // 포커스 대상 클릭으로 진행하는 경우
            if (step.advanceType == TutorialAdvanceType.ClickFocusTarget)
            {
                Button btn = target.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(OnFocusTargetClicked);
                }
            }
        }
        else
        {
            Debug.LogWarning($"[Tutorial] 포커스 대상을 찾을 수 없음: {step.focusTargetName}");
            focusMask?.ClearFocus();
        }
    }

    private void OnFocusTargetClicked()
    {
        if (!isTutorialActive) return;

        TutorialStepData step = tutorialSteps[currentStep];
        if (!string.IsNullOrEmpty(step.focusTargetName))
        {
            GameObject target = FindTargetByName(step.focusTargetName);
            if (target != null)
            {
                Button btn = target.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.RemoveListener(OnFocusTargetClicked);
            }
        }

        NextStep();
    }

    /// <summary>
    /// ★★★ 이름으로 오브젝트 찾기 (비활성 포함!) ★★★
    /// 
    /// GameObject.Find()는 활성 오브젝트만 찾음 → 부모가 꺼져있으면 못 찾음
    /// 이 메서드는 씬의 모든 오브젝트(비활성 포함)를 검색함
    /// </summary>
    private GameObject FindTargetByName(string targetName)
    {
        // 1. 먼저 활성 오브젝트에서 찾기 (빠름)
        GameObject found = GameObject.Find(targetName);
        if (found != null) return found;

        // 2. 못 찾으면 비활성 포함 전체 검색
        // 씬의 모든 루트 오브젝트 순회
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            // 자식 중에서 이름이 일치하는 것 찾기 (비활성 포함)
            Transform result = FindInChildren(root.transform, targetName);
            if (result != null)
            {
                Debug.Log($"[Tutorial] 대상 찾음 (비활성 포함 검색): {targetName}");
                return result.gameObject;
            }
        }

        Debug.LogWarning($"[Tutorial] 대상을 찾을 수 없음: {targetName}");
        return null;
    }

    /// <summary>
    /// 재귀적으로 자식 검색 (비활성 오브젝트도 찾음)
    /// </summary>
    private Transform FindInChildren(Transform parent, string targetName)
    {
        // 이 오브젝트 이름이 맞으면 반환
        if (parent.name == targetName)
            return parent;

        // 모든 자식 검색 (비활성 포함)
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindInChildren(parent.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// ★ 화살표 설정
    /// 포커스가 있는 단계 → FocusMask의 fingerIcon이 처리하므로 arrowIndicator 숨김
    /// 포커스가 없는 단계 → arrowIndicator를 직접 표시
    /// </summary>
    private void SetupArrow(TutorialStepData step)
    {
        if (arrowIndicator == null) return;

        // ★ 포커스 대상이 있으면 → FocusMask의 fingerIcon이 제어 중!
        // ArrowIndicator와 fingerIcon이 같은 오브젝트이므로 여기서 끄면 안 됨!
        if (!string.IsNullOrEmpty(step.focusTargetName))
        {
            return;  // SetActive 안 건드림!
        }

        // 포커스 없는 단계에서 화살표 표시
        if (step.showArrow)
        {
            arrowIndicator.gameObject.SetActive(true);
            arrowIndicator.anchoredPosition = step.arrowOffset;
        }
        else
        {
            arrowIndicator.gameObject.SetActive(false);
        }
    }

    // ============================================
    // 진행 처리
    // ============================================

    private void OnNextClicked()
    {
        if (isTyping)
        {
            StopCoroutine(typingCoroutine);
            messageText.text = tutorialSteps[currentStep].message;
            isTyping = false;
            return;
        }

        NextStep();
    }

    public void NextStep()
    {
        currentStep++;
        ShowStep(currentStep);
    }

    /// <summary>
    /// 외부에서 특정 행동 완료 시 호출
    /// 예: TopMenuManager.ToggleMenu()에서 OnActionCompleted("ToggleMenu") 호출
    /// </summary>
    public void OnActionCompleted(string actionName)
    {
        if (!isTutorialActive) return;

        TutorialStepData step = tutorialSteps[currentStep];
        if (step.advanceType == TutorialAdvanceType.WaitForAction)
        {
            if (string.IsNullOrEmpty(step.requiredAction) || step.requiredAction == actionName)
            {
                Debug.Log($"[Tutorial] 행동 완료: {actionName} → 다음 단계!");
                NextStep();
            }
        }
    }

    // ============================================
    // 효과
    // ============================================

    private IEnumerator TypeText(string text)
    {
        isTyping = true;
        messageText.text = "";

        foreach (char c in text)
        {
            messageText.text += c;
            yield return new WaitForSecondsRealtime(typingSpeed);
        }

        isTyping = false;
    }

    private IEnumerator AutoAdvance(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        NextStep();
    }
}