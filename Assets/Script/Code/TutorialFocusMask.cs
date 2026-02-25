using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 포커스 마스크 (손가락 펄스 애니메이션 포함)
/// 
/// ★ 구조 (Hierarchy):
///   FocusMask          ← 이 스크립트 붙임 (항상 활성화 상태 유지!)
///     ├ black          ← 어두운 오버레이 (Image) → overlayImage
///     ├ focusRect      ← 밝게 할 영역 (빈 RectTransform)
///     └ ArrowIndicator ← 손가락 아이콘 (Image) → fingerIcon
/// 
/// ★ 핵심 변경:
///   - FocusMask gameObject는 SetActive(false) 안 함!
///   - 대신 overlayImage, focusRect, fingerIcon 개별적으로 켜고 끔
///   - 손가락은 포커스 대상 위치에서 커졌다 작아졌다 반복
/// </summary>
public class TutorialFocusMask : MonoBehaviour, ICanvasRaycastFilter
{
    [Header("포커스 영역")]
    [SerializeField] private RectTransform focusRect;       // 밝게 할 영역
    [SerializeField] private Image overlayImage;            // 어두운 배경 이미지

    [Header("손가락 아이콘")]
    [SerializeField] private RectTransform fingerIcon;      // ★ ArrowIndicator (손가락 이미지)
    [SerializeField] private Vector2 fingerOffset = new Vector2(0f, -60f);  // 대상 기준 손가락 오프셋

    [Header("펄스 애니메이션")]
    [SerializeField] private float pulseSpeed = 3f;         // 펄스 속도 (빠를수록 빨리 깜빡)
    [SerializeField] private float pulseMinScale = 0.7f;    // 최소 크기
    [SerializeField] private float pulseMaxScale = 1.3f;    // 최대 크기
    [SerializeField] private float bounceAmount = 15f;      // 위아래 흔들림 (픽셀)

    // 내부 상태
    private bool isFocusActive = false;
    private RectTransform currentTarget;            // 현재 포커스 대상
    private Vector3 fingerBasePosition;             // 손가락 기본 위치

    void Start()
    {
        // ★ 시작할 때 모든 요소 숨기기 (FocusMask 자체는 켜둠!)
        HideAllElements();
    }

    /// <summary>
    /// ★ 모든 하위 요소 숨기기 (gameObject.SetActive(false) 안 씀!)
    /// </summary>
    private void HideAllElements()
    {
        if (overlayImage != null)
            overlayImage.enabled = false;

        if (focusRect != null)
            focusRect.gameObject.SetActive(false);

        if (fingerIcon != null)
            fingerIcon.gameObject.SetActive(false);
    }

    /// <summary>
    /// ★ 포커스 설정 + 손가락 이동
    /// </summary>
    public void SetFocus(RectTransform target, Vector2 padding)
    {
        if (target == null)
        {
            ClearFocus();
            return;
        }
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        isFocusActive = true;
        currentTarget = target;

        // ─── 1. 어두운 오버레이 켜기 ───
        if (overlayImage != null)
            overlayImage.enabled = true;

        // ─── 2. 포커스 영역 계산 ───
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null : (canvas != null ? canvas.worldCamera : null);

        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        Vector2 focusSize = new Vector2(
            Mathf.Abs(max.x - min.x) + padding.x,
            Mathf.Abs(max.y - min.y) + padding.y
        );

        // 포커스 Rect 위치/크기
        if (focusRect != null)
        {
            focusRect.gameObject.SetActive(true);
            focusRect.position = target.position;
            focusRect.sizeDelta = focusSize;
        }

        // ─── 3. ★ 손가락 아이콘 켜기 + 위치 이동 ───
        if (fingerIcon != null)
        {
            fingerIcon.gameObject.SetActive(true);
            fingerBasePosition = target.position + (Vector3)fingerOffset;
            fingerIcon.position = fingerBasePosition;
        }

        Debug.Log($"[FocusMask] 포커스 설정: {target.name}");
    }

    /// <summary>
    /// ★ 포커스 해제 (gameObject.SetActive(false) 안 함!)
    /// 하위 요소만 끔
    /// </summary>
    public void ClearFocus()
    {
        isFocusActive = false;
        currentTarget = null;

        // ★ gameObject는 안 끄고, 하위 요소만 숨김
        HideAllElements();

        Debug.Log("[FocusMask] 포커스 해제");
    }

    void Update()
    {
        // 포커스 비활성이면 아무것도 안 함
        if (!isFocusActive || fingerIcon == null || !fingerIcon.gameObject.activeSelf)
            return;

        // ─── ★ 손가락 펄스 애니메이션 ───
        // Time.unscaledTime → TimeScale=0이어도 애니메이션 동작
        float t = Time.unscaledTime * pulseSpeed;

        // 1. 스케일 펄스: 커졌다 작아졌다 반복
        float scaleValue = Mathf.Lerp(
            pulseMinScale,
            pulseMaxScale,
            (Mathf.Sin(t * Mathf.PI) + 1f) / 2f    // 0~1 사이 반복
        );
        fingerIcon.localScale = Vector3.one * scaleValue;

        // 2. 위아래 바운스: 손가락이 살짝 위아래로 흔들림
        float bounce = Mathf.Sin(t * Mathf.PI) * bounceAmount;

        // 3. 대상이 움직이면 따라가기 (메뉴 슬라이드 등)
        if (currentTarget != null)
        {
            fingerBasePosition = currentTarget.position + (Vector3)fingerOffset;

            // 포커스 영역도 따라가기
            if (focusRect != null)
            {
                focusRect.position = currentTarget.position;
            }
        }

        // 최종 손가락 위치 = 기본 위치 + 바운스
        fingerIcon.position = fingerBasePosition + new Vector3(0f, bounce, 0f);
    }

    /// <summary>
    /// ★ 포커스 영역은 클릭 통과, 나머지는 차단
    /// </summary>
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!isFocusActive) return true;

        if (focusRect != null && focusRect.gameObject.activeSelf)
        {
            // 포커스 영역 안이면 클릭 통과 (false = 레이캐스트 안 받음 = 통과)
            return !RectTransformUtility.RectangleContainsScreenPoint(
                focusRect, screenPoint, eventCamera);
        }

        return true;
    }
}