using UnityEngine;
using TMPro;

/// <summary>
/// 텍스트가 영역을 넘으면 자동으로 왼쪽 슬라이드 → 원위치 반복
///
/// ★ 사용법:
/// 1. TMP 텍스트가 있는 오브젝트에 이 스크립트 추가
/// 2. 부모에 RectMask2D 컴포넌트 추가 (넘치는 텍스트 잘라줌)
/// 3. 끝!
///
/// ★ 구조:
///   QuestTextArea (RectMask2D ← 부모에 추가)
///     └── QuestText (TMP + MarqueeText)
/// </summary>
public class MarqueeText : MonoBehaviour
{
    [Header("슬라이드 설정")]
    [Tooltip("슬라이드 속도 (px/초)")]
    [SerializeField] private float scrollSpeed = 50f;

    [Tooltip("끝까지 갔다가 원위치 전 대기 시간")]
    [SerializeField] private float pauseAtEnd = 1.5f;

    [Tooltip("원위치 후 다시 시작 전 대기 시간")]
    [SerializeField] private float pauseAtStart = 2f;

    private TextMeshProUGUI tmp;
    private RectTransform textRect;
    private RectTransform parentRect;

    private float originalX;
    private float overflowAmount;
    private float timer;
    private bool needsScroll;

    private enum State { WaitStart, Scrolling, WaitEnd, Returning }
    private State state = State.WaitStart;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        textRect = GetComponent<RectTransform>();
        parentRect = transform.parent?.GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        ResetScroll();
    }

    void Update()
    {
        if (!needsScroll || tmp == null || parentRect == null) return;

        switch (state)
        {
            case State.WaitStart:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                    state = State.Scrolling;
                break;

            case State.Scrolling:
                float newX = textRect.anchoredPosition.x - scrollSpeed * Time.deltaTime;
                if (newX <= originalX - overflowAmount)
                {
                    newX = originalX - overflowAmount;
                    state = State.WaitEnd;
                    timer = pauseAtEnd;
                }
                textRect.anchoredPosition = new Vector2(newX, textRect.anchoredPosition.y);
                break;

            case State.WaitEnd:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                    state = State.Returning;
                break;

            case State.Returning:
                textRect.anchoredPosition = new Vector2(originalX, textRect.anchoredPosition.y);
                state = State.WaitStart;
                timer = pauseAtStart;
                break;
        }
    }

    /// <summary>텍스트 변경 후 호출하면 오버플로 재계산</summary>
    public void Refresh()
    {
        ResetScroll();
    }

    private void ResetScroll()
    {
        if (tmp == null || textRect == null || parentRect == null) return;

        // 원위치로 복원
        originalX = textRect.anchoredPosition.x;
        textRect.anchoredPosition = new Vector2(originalX, textRect.anchoredPosition.y);

        // 텍스트 너비 vs 부모 너비
        tmp.ForceMeshUpdate();
        float textWidth = tmp.preferredWidth;
        float parentWidth = parentRect.rect.width;

        overflowAmount = textWidth - parentWidth;
        needsScroll = overflowAmount > 5f; // 5px 이상 넘칠 때만

        state = State.WaitStart;
        timer = pauseAtStart;
    }
}
