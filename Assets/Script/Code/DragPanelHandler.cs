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

    // ★ 캔버스 로컬 좌표계 기준으로 통일 (panel 로컬 좌표와 혼용하던 버그 수정)
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
        if (targetPanel == null || canvasRect == null) return;

        // ★ 포인터와 패널 위치를 모두 캔버스 로컬 좌표계로 변환
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 pointerOnCanvas);

        Vector2 panelOnCanvas = canvasRect.InverseTransformPoint(targetPanel.position);

        // 오프셋 = 패널 위치 - 포인터 위치 (캔버스 로컬 기준)
        pointerOffset = panelOnCanvas - pointerOnCanvas;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetPanel == null || canvasRect == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPointerPosition))
            return;

        // ★ 오프셋을 더해서 패널의 새 위치 계산 (캔버스 로컬 좌표)
        Vector2 newPos = localPointerPosition + pointerOffset;

        if (limitToScreen)
            newPos = ClampToCanvas(newPos);

        // 캔버스 로컬 → 월드 좌표로 변환
        targetPanel.position = canvasRect.TransformPoint(newPos);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 드래그 종료 처리 (필요시)
    }

    /// <summary>
    /// 캔버스 영역 내로 위치 제한 (피벗 고려)
    /// </summary>
    private Vector2 ClampToCanvas(Vector2 pos)
    {
        Rect cr = canvasRect.rect;
        Vector2 pivot = targetPanel.pivot;
        float w = targetPanel.rect.width;
        float h = targetPanel.rect.height;

        float minX = cr.xMin + w * pivot.x;
        float maxX = cr.xMax - w * (1f - pivot.x);
        float minY = cr.yMin + h * pivot.y;
        float maxY = cr.yMax - h * (1f - pivot.y);

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        return pos;
    }
}
