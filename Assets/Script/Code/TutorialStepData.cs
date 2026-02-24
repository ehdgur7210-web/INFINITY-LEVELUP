using UnityEngine;

[CreateAssetMenu(fileName = "TutorialStep", menuName = "Game/Tutorial Step")]
public class TutorialStepData : ScriptableObject
{
    [Header("텍스트")]
    [TextArea(3, 5)]
    public string message;

    [Header("이미지")]
    public Sprite characterImage;    // 캐릭터 이미지
    public Sprite guideImage;        // 설명 이미지

    [Header("포커스 대상")]
    public string focusTargetName;   // 하이라이트할 UI 오브젝트 이름
    public Vector2 focusSize = new Vector2(200, 200); // 포커스 영역 크기
    public string requiredAction;
    [Header("진행 조건")]
    public TutorialAdvanceType advanceType;
    public float autoAdvanceDelay = 0f;  // 자동 진행 시 대기 시간

    [Header("화살표")]
    public bool showArrow = true;
    public Vector2 arrowOffset;
}

public enum TutorialAdvanceType
{
    ClickAnywhere,      // 아무 곳 클릭
    ClickFocusTarget,   // 포커스 대상 클릭
    AutoAdvance,        // 자동 진행
    WaitForAction       // 특정 행동 대기
}