using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 패널 드래그 핸들러
/// - 특정 영역(헤더)을 드래그하여 이동
/// - 버튼 클릭과의 분리
/// </summary>
public class DragPanelHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("타겟")]
    [SerializeField] private RectTransform targetPanel;    // 드래그할 패널

    [Header("드래그 설정")]
    [SerializeField] private bool limitToScreen = true;    // 화면 경계를 벗어나지 않게

    private Vector2 pointerOffset;
    private RectTransform canvasRect;
    private Canvas canvas;

    void Start()
    {
        if (targetPanel == null)
        {
            targetPanel = GetComponentInParent<RectTransform>();
        }

        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvasRect = canvas.GetComponent<RectTransform>();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (targetPanel == null) return;

        // 드래그 시작 위치 저장
        Vector2 pointerPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetPanel,
            eventData.position,
            eventData.pressEventCamera,
            out pointerPos);

        pointerOffset = pointerPos;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetPanel == null) return;

        Vector2 localPointerPosition;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out localPointerPosition))
        {
            // 새 위치 계산
            Vector3 newPosition = localPointerPosition - pointerOffset;

            // 화면 경계를 벗어나지 않게 처리
            if (limitToScreen && canvasRect != null)
            {
                newPosition = ClampToCanvas(newPosition);
            }

            targetPanel.position = canvas.transform.TransformPoint(newPosition);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 드래그 종료 처리 (필요시)
    }

    /// <summary>
    /// 캔버스 영역 내로 위치 제한
    /// </summary>
    private Vector3 ClampToCanvas(Vector3 position)
    {
        Vector3 minPosition = canvasRect.rect.min - targetPanel.rect.min;
        Vector3 maxPosition = canvasRect.rect.max - targetPanel.rect.max;

        position.x = Mathf.Clamp(position.x, minPosition.x, maxPosition.x);
        position.y = Mathf.Clamp(position.y, minPosition.y, maxPosition.y);

        return position;
    }
}
