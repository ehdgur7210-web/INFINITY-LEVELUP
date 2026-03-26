using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 포커스 마스크 (손가락 애니메이션 포함)
///
/// ★ 화면 밖 방지:
///   - 대상이 하단에 있으면 손가락을 위에 표시
///   - 대상이 좌측/우측 끝이면 반대쪽에 표시
///
/// 씬 구조:
///   FocusMask → Image(투명 A:0, Raycast=true) + 이 스크립트
///     └ black → Image(검정 A:178, Raycast=false)
///     └ focusRect → 빈 RectTransform (Image 없음!)
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

    // 내부 상태
    private bool isFocusActive = false;
    private RectTransform currentTarget;
    private Vector3 fingerBasePosition;
    private Vector2 actualFingerOffset;  // 화면 위치에 따라 자동 결정

    void Start()
    {
        HideAllElements();
    }

    private void HideAllElements()
    {
        if (overlayImage != null) overlayImage.enabled = false;
        if (focusRect != null) focusRect.gameObject.SetActive(false);
        if (fingerIcon != null) fingerIcon.gameObject.SetActive(false);
    }

    public void SetFocus(RectTransform target, Vector2 padding)
    {
        if (target == null) { ClearFocus(); return; }
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        isFocusActive = true;
        currentTarget = target;

        if (overlayImage != null) overlayImage.enabled = true;

        // 포커스 영역
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null : (canvas != null ? canvas.worldCamera : null);

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        Vector2 focusSize = new Vector2(
            Mathf.Abs(max.x - min.x) + padding.x,
            Mathf.Abs(max.y - min.y) + padding.y
        );

        if (focusRect != null)
        {
            focusRect.gameObject.SetActive(true);
            focusRect.position = target.position;
            focusRect.sizeDelta = focusSize;
        }

        // ★ 손가락 위치 자동 결정 (화면 위치 기반)
        if (fingerIcon != null)
        {
            fingerIcon.gameObject.SetActive(true);
            actualFingerOffset = CalculateBestFingerOffset(target, cam);
            fingerBasePosition = target.position + (Vector3)actualFingerOffset;
            fingerIcon.position = fingerBasePosition;

            // 손가락 회전 (가리키는 방향)
            UpdateFingerRotation();
        }
    }

    /// <summary>대상의 화면 위치에 따라 손가락 offset 자동 결정</summary>
    private Vector2 CalculateBestFingerOffset(RectTransform target, Camera cam)
    {
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, target.position);
        float screenH = Screen.height;
        float screenW = Screen.width;

        // 화면 하단 30% 영역이면 → 손가락을 위에
        if (screenPos.y < screenH * 0.30f)
            return new Vector2(0f, fingerDistance);

        // 화면 상단 20% 영역이면 → 손가락을 아래에
        if (screenPos.y > screenH * 0.80f)
            return new Vector2(0f, -fingerDistance);

        // 화면 좌측 20% 영역이면 → 손가락을 오른쪽에
        if (screenPos.x < screenW * 0.20f)
            return new Vector2(fingerDistance, 0f);

        // 화면 우측 20% 영역이면 → 손가락을 왼쪽에
        if (screenPos.x > screenW * 0.80f)
            return new Vector2(-fingerDistance, 0f);

        // 기본: 아래에
        return new Vector2(0f, -fingerDistance);
    }

    /// <summary>손가락 아이콘을 대상 방향으로 회전</summary>
    private void UpdateFingerRotation()
    {
        if (fingerIcon == null) return;

        // offset 방향에 따라 회전 (0=아래 가리킴이 기본)
        float angle = 0f;
        if (actualFingerOffset.y > 0f) angle = 180f;       // 위에서 아래로 가리킴
        else if (actualFingerOffset.x > 0f) angle = 90f;   // 오른쪽에서 왼쪽으로
        else if (actualFingerOffset.x < 0f) angle = -90f;  // 왼쪽에서 오른쪽으로

        fingerIcon.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void ClearFocus()
    {
        isFocusActive = false;
        currentTarget = null;
        HideAllElements();

        // 회전 초기화
        if (fingerIcon != null)
            fingerIcon.localRotation = Quaternion.identity;
    }

    /// <summary>전체 화면 클릭 차단 (포커스 대상 없이 오버레이만 활성)</summary>
    public void BlockAll()
    {
        isFocusActive = true;
        currentTarget = null;

        if (overlayImage != null) overlayImage.enabled = true;
        if (focusRect != null) focusRect.gameObject.SetActive(false); // 포커스 영역 없음 = 전체 차단
        if (fingerIcon != null) fingerIcon.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isFocusActive || fingerIcon == null || !fingerIcon.gameObject.activeSelf)
            return;

        float t = Time.unscaledTime * pulseSpeed;

        // 크기 펄스
        float scaleValue = Mathf.Lerp(
            pulseMinScale, pulseMaxScale,
            (Mathf.Sin(t * Mathf.PI) + 1f) / 2f
        );
        fingerIcon.localScale = Vector3.one * scaleValue;

        // 바운스 (offset 방향으로)
        float bounce = Mathf.Sin(t * Mathf.PI) * bounceAmount;
        Vector2 bounceDir = actualFingerOffset.normalized;

        // 대상 따라가기
        if (currentTarget != null)
        {
            fingerBasePosition = currentTarget.position + (Vector3)actualFingerOffset;

            if (focusRect != null)
                focusRect.position = currentTarget.position;
        }

        // 최종 위치 = 기본 + 바운스 방향
        fingerIcon.position = fingerBasePosition + new Vector3(bounceDir.x * bounce, bounceDir.y * bounce, 0f);
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!isFocusActive) return false;

        if (focusRect != null && focusRect.gameObject.activeSelf)
        {
            bool isInsideFocus = RectTransformUtility.RectangleContainsScreenPoint(
                focusRect, screenPoint, eventCamera);
            return !isInsideFocus;
        }

        return false;
    }
}
