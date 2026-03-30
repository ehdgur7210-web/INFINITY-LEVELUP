using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 슬라이드 토글 — ON이면 핸들이 왼쪽, OFF이면 오른쪽으로 슬라이드
///
/// [사용법]
///   1. 배경 Image 오브젝트에 이 컴포넌트 부착
///   2. handle 에 움직일 핸들(동그라미/아이콘) RectTransform 연결
///   3. onPosX / offPosX 에 로컬 X 좌표 설정 (ON=왼쪽, OFF=오른쪽)
///   4. OnValueChanged 이벤트에 ToggleAutoSkill 등 연결
///   5. onBackground / offBackground 로 배경색 변경 (선택)
/// </summary>
public class SlideToggle : MonoBehaviour
{
    [Header("===== 핸들 =====")]
    [SerializeField] private RectTransform handle;
    [SerializeField] private float onPosX = -30f;
    [SerializeField] private float offPosX = 30f;
    [SerializeField] private float slideDuration = 0.15f;

    [Header("===== 배경 색상 (선택) =====")]
    [SerializeField] private Image background;
    [SerializeField] private Color onColor = new Color(0.3f, 0.85f, 0.4f, 1f);
    [SerializeField] private Color offColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("===== ON/OFF 아이콘 (선택) =====")]
    [SerializeField] private GameObject onIcon;
    [SerializeField] private GameObject offIcon;

    [Header("===== 이벤트 =====")]
    public UnityEvent OnToggleOn;
    public UnityEvent OnToggleOff;
    public UnityEvent OnToggle;

    [Header("===== 상태 =====")]
    [SerializeField] private bool isOn = true;
    public bool IsOn => isOn;

    private Coroutine _slideCoroutine;

    private void Start()
    {
        // 초기 위치 즉시 설정 (애니메이션 없이)
        ApplyState(false);

        // 버튼 클릭 연결
        var button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(Toggle);
    }

    /// <summary>토글 실행 (버튼 onClick에 연결)</summary>
    public void Toggle()
    {
        isOn = !isOn;
        ApplyState(true);

        if (isOn)
            OnToggleOn?.Invoke();
        else
            OnToggleOff?.Invoke();

        OnToggle?.Invoke();
    }

    /// <summary>외부에서 상태 직접 설정 (이벤트 발생 안 함)</summary>
    public void SetState(bool on, bool animate = true)
    {
        if (isOn == on) return;
        isOn = on;
        ApplyState(animate);
    }

    private void ApplyState(bool animate)
    {
        float targetX = isOn ? onPosX : offPosX;

        // 핸들 슬라이드
        if (handle != null)
        {
            if (animate && gameObject.activeInHierarchy)
            {
                if (_slideCoroutine != null)
                    StopCoroutine(_slideCoroutine);
                _slideCoroutine = StartCoroutine(SlideCoroutine(targetX));
            }
            else
            {
                var pos = handle.anchoredPosition;
                pos.x = targetX;
                handle.anchoredPosition = pos;
            }
        }

        // 배경 색상
        if (background != null)
            background.color = isOn ? onColor : offColor;

        // 아이콘 전환
        if (onIcon != null) onIcon.SetActive(isOn);
        if (offIcon != null) offIcon.SetActive(!isOn);
    }

    private IEnumerator SlideCoroutine(float targetX)
    {
        Vector2 startPos = handle.anchoredPosition;
        float startX = startPos.x;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            var pos = handle.anchoredPosition;
            pos.x = Mathf.Lerp(startX, targetX, t);
            handle.anchoredPosition = pos;
            yield return null;
        }

        var finalPos = handle.anchoredPosition;
        finalPos.x = targetX;
        handle.anchoredPosition = finalPos;
        _slideCoroutine = null;
    }
}
