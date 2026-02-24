using UnityEngine;
using System.Collections;

public class HotbarManager : MonoBehaviour
{
    public static HotbarManager Instance;
    [SerializeField] private RectTransform hotbarRect;
    [SerializeField] private float moveSpeed = 0.3f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private Vector2 originalPosition;
    [SerializeField] private float offsetY = 0f;
    [SerializeField] private bool autoCalculateOffset = false;
    [SerializeField] private RectTransform skillTreePanelRect;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (hotbarRect == null) hotbarRect = GetComponent<RectTransform>();
        if (hotbarRect != null) originalPosition = hotbarRect.anchoredPosition;
    }

    // ✅ 핫바 안 움직임 - 스킬창 위치는 Inspector에서 직접 설정
    public void OnSkillTreeOpened() { }
    public void OnSkillTreeClosed() { }
    public void ResetPositionImmediate() { }
    public void SetOffsetY(float o) { }
    public void SetOriginalPosition(Vector2 p) { }
}