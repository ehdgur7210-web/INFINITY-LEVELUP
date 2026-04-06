using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cinemachine;

/// <summary>
/// CompanionCutsceneUI — 동료 스킬 컷씬 연출
///
/// [연출 흐름]
///   1. 시간 정지 (Time.timeScale = 0)
///   2. 카메라 줌인 (동료 위치로)
///   3. 배경 어둡게 + 동료 일러스트 + 스킬 이름 표시
///   4. 이펙트 재생 (1~1.5초)
///   5. 카메라 복귀 + 시간 복귀
///   6. 범위 데미지 적용
///
/// [옵션]
///   OptionUI.CutsceneMode:
///     0 = 항상 표시
///     1 = 스킬당 1번만 (이후 스킵)
///     2 = 끄기
/// </summary>
public class CompanionCutsceneUI : MonoBehaviour
{
    public static CompanionCutsceneUI Instance { get; private set; }

    [Header("===== UI 요소 =====")]
    [SerializeField] private GameObject cutscenePanel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image 배경어둡게;
    [SerializeField] private Image 동료일러스트;
    [SerializeField] private TextMeshProUGUI 스킬이름텍스트;
    [SerializeField] private TextMeshProUGUI 동료이름텍스트;
    [SerializeField] private GameObject 이펙트오브젝트;

    [Header("===== 연출 설정 =====")]
    [SerializeField] private float 줌인크기 = 3f;
    [SerializeField] private float 줌인속도 = 8f;
    [SerializeField] private float 컷씬지속시간 = 1.2f;
    [SerializeField] private float 페이드인시간 = 0.15f;
    [SerializeField] private float 페이드아웃시간 = 0.2f;

    private CinemachineVirtualCamera _virtualCamera;
    private float _originalCameraSize;
    private Transform _originalFollowTarget;
    private bool _isPlaying;

    // 스킬별 1회 표시 기록
    private static System.Collections.Generic.HashSet<string> _shownSkills
        = new System.Collections.Generic.HashSet<string>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (cutscenePanel) cutscenePanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 컷씬 재생 요청
    /// </summary>
    /// <param name="companionName">동료 이름</param>
    /// <param name="skillName">스킬 이름</param>
    /// <param name="portrait">동료 전체 일러스트 (없으면 초상화)</param>
    /// <param name="companionPos">동료 월드 좌표 (줌인 위치)</param>
    /// <param name="onComplete">컷씬 종료 후 콜백 (데미지 적용 등)</param>
    public void PlayCutscene(string companionName, string skillName,
        Sprite portrait, Vector3 companionPos, Action onComplete)
    {
        // 옵션 체크
        int mode = OptionUI.CutsceneMode;
        if (mode == 2) // 끄기
        {
            onComplete?.Invoke();
            return;
        }

        string skillKey = $"{companionName}_{skillName}";
        if (mode == 1 && _shownSkills.Contains(skillKey)) // 1번만
        {
            onComplete?.Invoke();
            return;
        }

        if (_isPlaying)
        {
            onComplete?.Invoke();
            return;
        }

        _shownSkills.Add(skillKey);
        StartCoroutine(CutsceneCoroutine(companionName, skillName, portrait, companionPos, onComplete));
    }

    private IEnumerator CutsceneCoroutine(string companionName, string skillName,
        Sprite portrait, Vector3 companionPos, Action onComplete)
    {
        _isPlaying = true;

        // ── 1. 카메라 저장 & 줌인 준비 ──
        _virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        if (_virtualCamera != null)
        {
            _originalCameraSize = _virtualCamera.m_Lens.OrthographicSize;
            _originalFollowTarget = _virtualCamera.Follow;
        }

        // ── 2. 시간 정지 ──
        Time.timeScale = 0f;

        // ── 3. UI 표시 ──
        if (cutscenePanel) cutscenePanel.SetActive(true);

        if (동료일러스트 != null && portrait != null)
        {
            동료일러스트.sprite = portrait;
            동료일러스트.gameObject.SetActive(true);
        }
        else if (동료일러스트 != null)
        {
            동료일러스트.gameObject.SetActive(false);
        }

        if (스킬이름텍스트) 스킬이름텍스트.text = skillName;
        if (동료이름텍스트) 동료이름텍스트.text = companionName;
        if (이펙트오브젝트) 이펙트오브젝트.SetActive(true);

        // ── 4. 페이드인 + 줌인 (unscaledTime) ──
        float elapsed = 0f;
        while (elapsed < 페이드인시간)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 페이드인시간);

            if (canvasGroup) canvasGroup.alpha = t;
            if (배경어둡게) 배경어둡게.color = new Color(0, 0, 0, t * 0.6f);

            // 카메라 줌인
            if (_virtualCamera != null)
            {
                float targetSize = Mathf.Lerp(_originalCameraSize, 줌인크기, t);
                _virtualCamera.m_Lens.OrthographicSize = targetSize;
            }

            yield return null;
        }

        // ── 5. 컷씬 유지 ──
        float holdElapsed = 0f;
        while (holdElapsed < 컷씬지속시간)
        {
            holdElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // ── 6. 페이드아웃 + 줌아웃 ──
        elapsed = 0f;
        while (elapsed < 페이드아웃시간)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 페이드아웃시간);

            if (canvasGroup) canvasGroup.alpha = 1f - t;
            if (배경어둡게) 배경어둡게.color = new Color(0, 0, 0, (1f - t) * 0.6f);

            // 카메라 복귀
            if (_virtualCamera != null)
            {
                float targetSize = Mathf.Lerp(줌인크기, _originalCameraSize, t);
                _virtualCamera.m_Lens.OrthographicSize = targetSize;
            }

            yield return null;
        }

        // ── 7. 정리 ──
        if (cutscenePanel) cutscenePanel.SetActive(false);
        if (이펙트오브젝트) 이펙트오브젝트.SetActive(false);

        // 카메라 완전 복귀
        if (_virtualCamera != null)
        {
            _virtualCamera.m_Lens.OrthographicSize = _originalCameraSize;
            _virtualCamera.Follow = _originalFollowTarget;

            // 카메라 쉐이크
            var controller = FindObjectOfType<CameraController>();
            if (controller != null)
            {
                controller.SetCameraSizeImmediate(_originalCameraSize);
                controller.ShakeCamera(2f, 0.3f);
            }
        }

        // ── 8. 시간 복귀 ──
        Time.timeScale = 1f;
        _isPlaying = false;

        // ── 9. 콜백 (데미지 적용 등) ──
        onComplete?.Invoke();
    }

    /// <summary>세션 시작 시 1회 표시 기록 초기화</summary>
    public static void ResetShownSkills()
    {
        _shownSkills.Clear();
    }

    public bool IsPlaying => _isPlaying;
}
