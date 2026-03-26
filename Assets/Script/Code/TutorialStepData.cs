using UnityEngine;

/// <summary>
/// 튜토리얼 단계 데이터 (손가락 포인팅 방식)
/// </summary>
[CreateAssetMenu(fileName = "TutorialStep", menuName = "Game/Tutorial Step")]
public class TutorialStepData : ScriptableObject
{
    [Header("━━━ 안내 텍스트 ━━━")]
    [TextArea(2, 3)]
    public string tipMessage;

    [Header("━━━ 포커스 대상 ━━━")]
    [Tooltip("하이라이트할 UI 오브젝트 이름 (Hierarchy 이름과 일치)")]
    public string focusTargetName;

    [Tooltip("포커스 영역 여백")]
    public Vector2 focusPadding = new Vector2(40, 40);

    [Header("━━━ 진행 방식 ━━━")]
    public TutorialAdvanceType advanceType = TutorialAdvanceType.ClickFocusTarget;

    [Tooltip("WaitForAction일 때 기다릴 액션 이름")]
    public string requiredAction;

    [Tooltip("AutoAdvance일 때 대기 시간 (초)")]
    public float autoAdvanceDelay = 2f;

    [Header("━━━ 팁 위치 ━━━")]
    public TipPosition tipPosition = TipPosition.Above;

    [Header("━━━ 씬 전환 ━━━")]
    [Tooltip("이 단계 완료 후 씬이 전환되면 체크 (다음 스텝은 새 씬에서 자동 재개)")]
    public bool isSceneTransitionStep = false;

    [Header("━━━ 표시 지연 ━━━")]
    [Tooltip("이 단계를 표시하기 전 대기 시간 (초). 씬 전환 직후 UI 초기화 대기용")]
    public float delayBeforeShow = 0f;

    [Header("━━━ 보상 지급 (이 단계 완료 시) ━━━")]
    [Tooltip("이 단계 완료 시 지급할 보상 (비어있으면 보상 없음)")]
    public TutorialReward[] rewards;
}

/// <summary>튜토리얼 단계 완료 시 지급할 보상</summary>
[System.Serializable]
public class TutorialReward
{
    public TutorialRewardType rewardType;
    public int amount;
    [Tooltip("Item 타입일 때만 사용")]
    public ItemData item;
}

public enum TutorialRewardType
{
    Gold,
    Gem,
    EquipmentTicket,
    CompanionTicket,
    CropPoint,
    Item
}

public enum TutorialAdvanceType
{
    ClickFocusTarget,
    ClickAnywhere,
    AutoAdvance,
    WaitForAction
}

public enum TipPosition
{
    Above,
    Below
}
