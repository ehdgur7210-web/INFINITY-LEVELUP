using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 포커스 마스크 (구멍 뚫린 어두운 오버레이 + 손가락 애니메이션)
///
/// ★ 셰이더 기반 구멍 뚫기:
///   - 화면 전체를 어둡게 하고 타겟 영역만 밝게 보여줌
///   - TutorialHoleMask 셰이더로 둥근 사각형 구멍 렌더링
///   - 구멍 위치/크기가 타겟을 실시간 추적
///
/// ★ 화면 밖 방지:
///   - 대상이 하단에 있으면 손가락을 위에 표시
///   - 대상이 좌측/우측 끝이면 반대쪽에 표시
///
/// 씬 구조:
///   FocusMask → Image(전체화면, TutorialHoleMask 머티리얼) + 이 스크립트
///     └ focusRect → 빈 RectTransform (레이캐스트 판정용)
///     └ ArrowIndicator → Image(손가락, Raycast=false)
/// </summary>
public class TutorialFocusMask : MonoBehaviour, ICanvasRaycastFilter
{
    [Header("포커스 영역")]
    [SerializeField] private RectTransform focusRect;
    [SerializeField] private Image overlayImage;

    [Header("손가락 아이콘")]
    [SerializeField] private RectTransform fingerIcon;
    [SerializeField] private float fingerDistance = 60f;

    [Header("애니메이션")]
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseMinScale = 0.7f;
    [SerializeField] private float pulseMaxScale = 1.3f;
    [SerializeField] private float bounceAmount = 15f;

    [Header("구멍 뚫기 셰이더")]
    [Tooltip("TutorialHoleMask 셰이더를 사용하는 머티리얼 (없으면 기존 방식)")]
    [SerializeField] private Material holeMaskMaterial;
    [SerializeField] private float cornerRadius = 0.015f;
    [SerializeField] private float edgeSoftness = 0.005f;

    // 셰이더 프로퍼티 ID (캐싱)
    private static readonly int _HoleCenterID = Shader.PropertyToID("_HoleCenter");
    private static readonly int _HoleSizeID   = Shader.PropertyToID("_HoleSize");
    private static readonly int _HoleRadiusID = Shader.PropertyToID("_HoleRadius");
    private static readonly int _EdgeSoftnessID = Shader.PropertyToID("_EdgeSoftness");

    // 내부 상태
    private bool isFocusActive = false;
    private RectTransform currentTarget;
    private Vector3 fingerBasePosition;
    private Vector2 actualFingerOffset;
    private Vector2 currentPadding;
    private Material _matInstance; // 인스턴스 머티리얼 (원본 보호)
    private Camera _uiCamera;

    void Start()
    {
        HideAllElements();
        CacheCamera();
    }

    private void CacheCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            _uiCamera = canvas.worldCamera;
    }

    private void HideAllElements()
    {
        if (overlayImage != null) overlayImage.enabled = false;
        if (focusRect != null) focusRect.gameObject.SetActive(false);
        if (fingerIcon != null) fingerIcon.gameObject.SetActive(false);
    }

    /// <summary>셰이더 머티리얼 인스턴스 가져오기</summary>
    private Material GetMaterialInstance()
    {
        if (_matInstance != null) return _matInstance;
        if (holeMaskMaterial == null) return null;

        _matInstance = new Material(holeMaskMaterial);
        if (overlayImage != null)
            overlayImage.material = _matInstance;
        return _matInstance;
    }

    public void SetFocus(RectTransform target, Vector2 padding)
    {
        if (target == null) { ClearFocus(); return; }
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        isFocusActive = true;
        currentTarget = target;
        currentPadding = padding;

        if (overlayImage != null) overlayImage.enabled = true;

        // 포커스 영역 계산
        if (_uiCamera == null) CacheCamera();

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(_uiCamera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(_uiCamera, corners[2]);

        Vector2 focusSize = new Vector2(
            Mathf.Abs(max.x - min.x) + padding.x,
            Mathf.Abs(max.y - min.y) + padding.y
        );

        // focusRect (레이캐스트 판정용)
        if (focusRect != null)
        {
            focusRect.gameObject.SetActive(true);
            focusRect.position = target.position;
            focusRect.sizeDelta = focusSize;
        }

        // ★ 셰이더 구멍 업데이트
        UpdateHoleShader(target, padding);

        // 손가락 위치 자동 결정
        if (fingerIcon != null)
        {
            fingerIcon.gameObject.SetActive(true);
            actualFingerOffset = CalculateBestFingerOffset(target, _uiCamera);
            fingerBasePosition = target.position + (Vector3)actualFingerOffset;
            fingerIcon.position = fingerBasePosition;
            UpdateFingerRotation();
        }
    }

    /// <summary>셰이더 머티리얼에 구멍 위치/크기 전달</summary>
    private void UpdateHoleShader(RectTransform target, Vector2 padding)
    {
        Material mat = GetMaterialInstance();
        if (mat == null) return;

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(_uiCamera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(_uiCamera, corners[2]);

        // 스크린 UV 좌표 (0~1)
        float sw = Screen.width;
        float sh = Screen.height;

        Vector2 center = new Vector2(
            (min.x + max.x) * 0.5f / sw,
            (min.y + max.y) * 0.5f / sh
        );

        Vector2 size = new Vector2(
            (Mathf.Abs(max.x - min.x) + padding.x) / sw,
            (Mathf.Abs(max.y - min.y) + padding.y) / sh
        );

        mat.SetVector(_HoleCenterID, new Vector4(center.x, center.y, 0, 0));
        mat.SetVector(_HoleSizeID, new Vector4(size.x, size.y, 0, 0));
        mat.SetFloat(_HoleRadiusID, cornerRadius);
        mat.SetFloat(_EdgeSoftnessID, edgeSoftness);
    }

    private Vector2 CalculateBestFingerOffset(RectTransform target, Camera cam)
    {
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, target.position);
        float screenH = Screen.height;
        float screenW = Screen.width;

        if (screenPos.y < screenH * 0.30f)
            return new Vector2(0f, fingerDistance);
        if (screenPos.y > screenH * 0.80f)
            return new Vector2(0f, -fingerDistance);
        if (screenPos.x < screenW * 0.20f)
            return new Vector2(fingerDistance, 0f);
        if (screenPos.x > screenW * 0.80f)
            return new Vector2(-fingerDistance, 0f);

        return new Vector2(0f, -fingerDistance);
    }

    private void UpdateFingerRotation()
    {
        if (fingerIcon == null) return;

        float angle = 0f;
        if (actualFingerOffset.y > 0f) angle = 180f;
        else if (actualFingerOffset.x > 0f) angle = 90f;
        else if (actualFingerOffset.x < 0f) angle = -90f;

        fingerIcon.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void ClearFocus()
    {
        isFocusActive = false;
        currentTarget = null;
        HideAllElements();

        if (fingerIcon != null)
            fingerIcon.localRotation = Quaternion.identity;
    }

    /// <summary>전체 화면 클릭 차단 (포커스 대상 없이 오버레이만 활성)</summary>
    public void BlockAll()
    {
        isFocusActive = true;
        currentTarget = null;

        if (overlayImage != null) overlayImage.enabled = true;
        if (focusRect != null) focusRect.gameObject.SetActive(false);
        if (fingerIcon != null) fingerIcon.gameObject.SetActive(false);

        // 셰이더: 구멍 크기 0으로 → 전체 어둡게
        Material mat = GetMaterialInstance();
        if (mat != null)
        {
            mat.SetVector(_HoleSizeID, Vector4.zero);
        }
    }

    void Update()
    {
        if (!isFocusActive) return;

        // 타겟 추적 (구멍 위치 실시간 업데이트)
        if (currentTarget != null)
        {
            // focusRect 추적
            if (focusRect != null)
                focusRect.position = currentTarget.position;

            // 셰이더 구멍 추적
            UpdateHoleShader(currentTarget, currentPadding);

            // 손가락 추적
            if (fingerIcon != null && fingerIcon.gameObject.activeSelf)
            {
                fingerBasePosition = currentTarget.position + (Vector3)actualFingerOffset;
            }
        }

        // 손가락 애니메이션
        if (fingerIcon != null && fingerIcon.gameObject.activeSelf)
        {
            float t = Time.unscaledTime * pulseSpeed;

            float scaleValue = Mathf.Lerp(
                pulseMinScale, pulseMaxScale,
                (Mathf.Sin(t * Mathf.PI) + 1f) / 2f
            );
            fingerIcon.localScale = Vector3.one * scaleValue;

            float bounce = Mathf.Sin(t * Mathf.PI) * bounceAmount;
            Vector2 bounceDir = actualFingerOffset.normalized;

            fingerIcon.position = fingerBasePosition + new Vector3(bounceDir.x * bounce, bounceDir.y * bounce, 0f);
        }
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!isFocusActive) return false;

        if (focusRect != null && focusRect.gameObject.activeSelf)
        {
            bool isInsideFocus = RectTransformUtility.RectangleContainsScreenPoint(
                focusRect, screenPoint, eventCamera);
            return !isInsideFocus; // 구멍 안쪽은 클릭 통과, 바깥은 차단
        }

        return false;
    }

    void OnDestroy()
    {
        if (_matInstance != null)
            Destroy(_matInstance);
    }
}
