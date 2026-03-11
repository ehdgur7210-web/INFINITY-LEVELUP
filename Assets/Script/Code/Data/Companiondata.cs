using UnityEngine;

/// <summary>
/// 동료(Companion) 데이터 ScriptableObject
/// Create → INFINITE LevelUp → Companion Data 로 생성
/// </summary>
[CreateAssetMenu(menuName = "INFINITE LevelUp/Companion Data", fileName = "NewCompanion")]
public class CompanionData : ScriptableObject
{
    [Header("기본 정보")]
    public string companionID;          // 고유 ID (예: "comp_001")
    public string companionName;        // 표시 이름
    [TextArea(3, 6)]
    public string description;          // 설명 (팝업에 표시)

    [Header("등급")]
    public CompanionRarity rarity = CompanionRarity.Common;

    [Header("이미지")]
    public Sprite portrait;             // 결과 화면 / 인벤에 표시되는 초상화
    public GameObject worldPrefab;      // 월드에 스폰될 3D/2D 프리팹

    [Header("전투 스탯")]
    public float attackPower = 10f;     // 기본 공격력
    public float attackSpeed = 1f;      // 공격 속도 (초당 횟수)
    public float attackRange = 3f;      // 공격 사거리
    public float moveSpeed = 3.5f;      // 이동 속도

    [Header("스폰 설정")]
    public float spawnRadius = 1.5f;    // 캐릭터 주변 스폰 반경

    [Header("가챠 확률 (CompanionGachaManager에서 사용)")]
    [Range(0f, 100f)]
    public float probability = 10f;
}

public enum CompanionRarity
{
    Common,     // 일반
    Rare,       // 희귀
    Epic,       // 영웅
    Legendary   // 전설
}