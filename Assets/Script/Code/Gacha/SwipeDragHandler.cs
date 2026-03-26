using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 배너 영역 좌우 스와이프 감지.
/// IBeginDragHandler/IEndDragHandler로 스와이프 방향 판별.
/// CompanionGachaManager의 탭 전환에 사용.
/// </summary>
public class SwipeDragHandler : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    public System.Action onSwipeLeft;
    public System.Action onSwipeRight;

    private Vector2 startPos;
    private const float SwipeThreshold = 50f;

    public void OnBeginDrag(PointerEventData eventData)
    {
        startPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        float deltaX = eventData.position.x - startPos.x;
        if (Mathf.Abs(deltaX) < SwipeThreshold) return;

        if (deltaX < 0)
            onSwipeLeft?.Invoke();  // 왼쪽으로 스와이프 = 다음 탭
        else
            onSwipeRight?.Invoke(); // 오른쪽으로 스와이프 = 이전 탭
    }
}
