using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 동료(Companion) 데이터 ScriptableObject
/// Create 메뉴: INFINITE LevelUp → Companion Data 로 생성
/// </summary>
[CreateAssetMenu(menuName = "INFINITE LevelUp/Companion Data", fileName = "NewCompanion")]
public class CompanionData : ScriptableObject
{
    [Header("기본 정보")]
    public string companionID;          // 동료 ID (예: "comp_001")
    public string companionName;        // 표시 이름
    [TextArea(3, 6)]
    public string description;          // 설명 (팝업에 표시)

    [Header("등급")]
    public CompanionRarity rarity = CompanionRarity.Common;

    [Header("별 등급 (승성)")]
    [Tooltip("초기 별 수 (가챠 획득 시 기본값). 승성으로 증가")]
    [Range(1, 12)]
    public int baseStars = 1;
    [Tooltip("이 동료의 최대 별 수")]
    public int maxStars = 12;

    [Header("이미지")]
    public Sprite portrait;             // 슬롯 / 승성 / 상세에 표시되는 스프라이트 아이콘
    [Tooltip("레벨업 패널에 표시할 원본 일러스트 (없으면 portrait 사용)")]
    public Sprite fullIllust;           // ★ 레벨업 화면 전용 원본 이미지
    public GameObject worldPrefab;      // 게임월드에 소환할 3D/2D 프리팹

    [Header("전투 스탯")]
    public float maxHealth = 500f;      // 최대 체력
    public float attackPower = 10f;     // 기본 공격력
    public float attackSpeed = 1f;      // 공격 속도 (초당 횟수)
    public float attackRange = 3f;      // 공격 사거리
    public float moveSpeed = 3.5f;      // 이동 속도
    public float defense = 5f;          // 방어력 (데미지 경감)

    [Header("스킬")]
    public List<CompanionSkillInfo> skills = new List<CompanionSkillInfo>();

    [Header("소환 설정")]
    public float spawnRadius = 1.5f;    // 캐릭터 주변 소환 반경
    [Tooltip("사망 후 재소환 쿨타임 (초)")]
    public float summonCooldown = 60f;  // 사망 후 쿨타임

    [Header("가챠 확률 (CompanionGachaManager에서 사용)")]
    [Range(0f, 100f)]
    public float probability = 10f;

    [Header("★ 전설 전용 연출 영상")]
    [Tooltip("Legendary 등급 캐릭터만 사용. 이 클립이 있으면 뽑기 시 MP4 연출 재생")]
    public VideoClip legendaryVideoClip;
}

public enum CompanionRarity
{
    Common,     // 일반
    Rare,       // 희귀
    Epic,       // 에픽
    Legendary   // 전설
}