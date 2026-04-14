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

    /// <summary>타겟이 속한 Canvas의 카메라 반환 (Overlay면 null)</summary>
    private Camera GetTargetCamera(RectTransform target)
    {
        Canvas c = target.GetComponentInParent<Canvas>();
        if (c == null) return null;
        Canvas root = c.rootCanvas;
        if (root.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return root.worldCamera;
    }

    /// <summary>스크린 좌표 → focusRect 배치 (어느 Canvas든 정확한 위치)</summary>
    private void PositionFocusRectToScreen(Vector2 screenPos)
    {
        PositionRectToScreen(focusRect, screenPos);
    }

    /// <summary>스크린 좌표 → RectTransform 배치</summary>
    private void PositionRectToScreen(RectTransform rect, Vector2 screenPos)
    {
        if (rect == null) return;
        RectTransform parentRect = rect.parent as RectTransform;
        if (parentRect == null)
        {
            // 부모 없으면 world 좌표 직접 설정
            rect.position = new Vector3(screenPos.x, screenPos.y, 0f);
            return;
        }
        // 부모의 로컬 좌표로 변환
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, screenPos, _uiCamera, out Vector2 localPos))
        {
            rect.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        }
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

        // ★ 타겟이 어느 Canvas(Camera/Overlay)에 있든 스크린 좌표로 변환
        Camera targetCam = GetTargetCamera(target);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(targetCam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(targetCam, corners[2]);

        Vector2 focusSize = new Vector2(
            Mathf.Abs(max.x - min.x) + padding.x,
            Mathf.Abs(max.y - min.y) + padding.y
        );

        // focusRect (레이캐스트 판정용)
        // ★ world position 직접 대입 대신 스크린→로컬 변환으로 올바르게 배치
        if (focusRect != null)
        {
            focusRect.gameObject.SetActive(true);
            focusRect.sizeDelta = focusSize;
            PositionFocusRectToScreen((min + max) * 0.5f);
        }

        // ★ 셰이더 구멍 업데이트 — focusRect 기반 (cross-canvas 좌표 문제 완전 회피)
        UpdateHoleShaderFromFocusRect(logOnce: true);

        // 손가락 위치 자동 결정
        if (fingerIcon != null)
        {
            fingerIcon.gameObject.SetActive(true);
            Camera targetCam2 = GetTargetCamera(target);
            actualFingerOffset = CalculateBestFingerOffset(target, targetCam2);
            // ★ 스크린 좌표 기반으로 손가락 위치 설정
            Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(targetCam2, target.position);
            Vector2 fingerScreen = targetScreen + actualFingerOffset;
            PositionRectToScreen(fingerIcon, fingerScreen);
            fingerBasePosition = fingerIcon.position;
            UpdateFingerRotation();
        }
    }

    /// <summary>
    /// 셰이더 구멍을 focusRect 기반으로 업데이트.
    /// ★ focusRect는 TutorialFocusMask와 같은 Canvas에 있으므로
    ///   cross-canvas 좌표 불일치가 발생하지 않는다.
    /// ★ Y축: WorldToScreenPoint는 y=0이 아래, DX 셰이더 screenUV.y=0이 위 →
    ///   DX 플랫폼에서만 cy를 반전. GL/Vulkan은 동일 방향이므로 반전 불필요.
    /// </summary>
    private void UpdateHoleShaderFromFocusRect(bool logOnce = false)
    {
        Material mat = GetMaterialInstance();
        if (mat == null || focusRect == null || !focusRect.gameObject.activeSelf) return;

        Vector3[] corners = new Vector3[4];
        focusRect.GetWorldCorners(corners);

        // focusRect와 TutorialFocusMask는 같은 Canvas → _uiCamera 사용
        Vector2 min = RectTransformUtility.WorldToScreenPoint(_uiCamera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(_uiCamera, corners[2]);

        float sw = Screen.width;
        float sh = Screen.height;

        float cx = (min.x + max.x) * 0.5f / sw;
        float cy = (min.y + max.y) * 0.5f / sh;

        // ★ ComputeScreenPos는 모든 플랫폼(DX/GL/Metal)에서 y=0=하단으로 일정.
        //   graphicsUVStartsAtTop은 렌더텍스처 텍스처 UV 컨벤션 플래그이며
        //   screen-space UV에는 적용하면 안 됨 → flip 없음.

        Vector2 size = new Vector2(
            Mathf.Abs(max.x - min.x) / sw,
            Mathf.Abs(max.y - min.y) / sh
        );

        if (logOnce)
            Debug.Log($"[FocusMask] shaderUV center=({cx:F3},{cy:F3}) size=({size.x:F3},{size.y:F3}) uvStartsAtTop={SystemInfo.graphicsUVStartsAtTop} screen={sw}x{sh}");

        mat.SetVector(_HoleCenterID, new Vector4(cx, cy, 0, 0));
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

    /// <summary>구멍은 areaTarget에, 손가락은 fingerTarget에 표시</summary>
    public void SetFocusWithFingerTarget(RectTransform areaTarget, Vector2 padding, RectTransform fingerTarget)
    {
        if (areaTarget == null) { ClearFocus(); return; }
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        isFocusActive = true;
        currentTarget = areaTarget;
        currentPadding = padding;

        if (overlayImage != null) overlayImage.enabled = true;
        if (_uiCamera == null) CacheCamera();

        // 포커스 영역 (구멍) — areaTarget 기준
        Vector3[] corners = new Vector3[4];
        areaTarget.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(_uiCamera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(_uiCamera, corners[2]);
        Vector2 focusSize = new Vector2(
            Mathf.Abs(max.x - min.x) + padding.x,
            Mathf.Abs(max.y - min.y) + padding.y
        );

        if (focusRect != null)
        {
            focusRect.gameObject.SetActive(true);
            focusRect.sizeDelta = focusSize;
            Camera areaCam = GetTargetCamera(areaTarget);
            Vector2 sMin2 = RectTransformUtility.WorldToScreenPoint(areaCam, corners[0]);
            Vector2 sMax2 = RectTransformUtility.WorldToScreenPoint(areaCam, corners[2]);
            PositionFocusRectToScreen((sMin2 + sMax2) * 0.5f);
        }

        UpdateHoleShaderFromFocusRect(logOnce: true);

        // 손가락 — fingerTarget 기준
        RectTransform ft = fingerTarget != null ? fingerTarget : areaTarget;
        if (fingerIcon != null)
        {
            fingerIcon.gameObject.SetActive(true);
            Camera ftCam = GetTargetCamera(ft);
            actualFingerOffset = CalculateBestFingerOffset(ft, ftCam);
            Vector2 ftScreen = RectTransformUtility.WorldToScreenPoint(ftCam, ft.position);
            PositionRectToScreen(fingerIcon, ftScreen + actualFingerOffset);
            fingerBasePosition = fingerIcon.position;
            UpdateFingerRotation();
        }
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
            Camera targetCam = GetTargetCamera(currentTarget);
            Vector3[] corners = new Vector3[4];
            currentTarget.GetWorldCorners(corners);
            Vector2 sMin = RectTransformUtility.WorldToScreenPoint(targetCam, corners[0]);
            Vector2 sMax = RectTransformUtility.WorldToScreenPoint(targetCam, corners[2]);
            Vector2 sCenter = (sMin + sMax) * 0.5f;

            // ★ focusRect: 스크린 좌표 기반으로 이동
            if (focusRect != null)
                PositionFocusRectToScreen(sCenter);

            // 셰이더 구멍 추적 — focusRect 기반
            UpdateHoleShaderFromFocusRect();

            // 손가락 추적
            if (fingerIcon != null && fingerIcon.gameObject.activeSelf)
            {
                Vector2 fingerScreen = sCenter + actualFingerOffset;
                PositionRectToScreen(fingerIcon, fingerScreen);
                fingerBasePosition = fingerIcon.position;
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

    /// <summary>주어진 스크린 좌표가 현재 포커스 영역 안에 있는지 확인</summary>
    public bool IsPointInsideFocusArea(Vector2 screenPoint)
    {
        if (!isFocusActive || focusRect == null || !focusRect.gameObject.activeSelf)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            focusRect, screenPoint, _uiCamera);
    }

    /// <summary>현재 포커스 영역의 RectTransform 반환</summary>
    public RectTransform FocusRect => focusRect;

    void OnDestroy()
    {
        if (_matInstance != null)
            Destroy(_matInstance);
    }
}
