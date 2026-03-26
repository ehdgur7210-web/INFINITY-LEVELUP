using UnityEngine;
using System.Collections.Generic;

// =====================================================
// VipData.cs
// VIP 등급별 데이터를 정의하는 ScriptableObject
// 생성: Assets 우클릭 > Create > VIP > VipData
// =====================================================

/// <summary>
/// VIP 혜택 항목 하나를 나타내는 구조체
/// 예) "보스 도전 횟수 +3"
/// </summary>
[System.Serializable]
public class VipBenefitData
{
    [Tooltip("혜택 설명 텍스트 (UI에 표시됨)")]
    public string description; // 예: "보스 도전 횟수 +3"

    [Tooltip("혜택 수치 (계산에 사용)")]
    public int value;          // 예: 3

    [Tooltip("혜택 종류")]
    public VipBenefitType benefitType;
}

/// <summary>
/// VIP 혜택 종류 열거형
/// 새로운 혜택을 추가할 때 여기에 추가
/// </summary>
public enum VipBenefitType
{
    BossCountBonus,         // 보스 도전 횟수 보너스
    DungeonEntryBonus,      // 던전입장 추가 횟수
    GuardianPurchaseBonus,  // 수호신구매 추가 횟수
    DemonStorageBonus,      // 악마의 저장창고 추가 횟수
    ExpItemPurchaseBonus,   // 경험치구매 추가 횟수
    AttackBonus,            // 공격 보너스
    SiegeCountBonus,        // 공성 횟수 보너스
    FreeBossEntry,          // 무료보스 무제한입장
    SecretTrade,            // 비밀거래 개방
    MemberLevelUp,          // 소요레벨 횟수 제한 해제
}

/// <summary>
/// VIP 무료/유료 선물 정보
/// </summary>
/// <summary>
/// 선물 보상 항목 하나 (골드, 다이아, 티켓, 아이템 등)
/// </summary>
[System.Serializable]
public class VipRewardEntry
{
    public VipRewardType rewardType;

    [Tooltip("지급 수량")]
    public int amount;

    [Tooltip("아이템 보상일 때만 사용 (ItemData SO)")]
    public ItemData item;
}

public enum VipRewardType
{
    Gold,               // 골드
    Gem,                // 다이아
    EquipmentTicket,    // 장비 티켓
    CompanionTicket,    // 동료 티켓
    CropPoint,          // 작물 포인트
    Item                // 특정 아이템 (ItemData 참조)
}

[System.Serializable]
public class VipGiftInfo
{
    [Header("무료 선물")]
    [Tooltip("무료 선물 아이콘 스프라이트")]
    public Sprite freeGiftIcon;

    [Tooltip("무료 선물 설명 문구")]
    public string freeGiftDescription;

    [Tooltip("무료 선물 보상 목록")]
    public VipRewardEntry[] freeRewards;

    [Header("유료 선물")]
    [Tooltip("유료 선물 아이콘 스프라이트")]
    public Sprite paidGiftIcon;

    [Tooltip("유료 선물 가격 (다이아)")]
    public int paidGiftPrice;          // 예: 1000

    [Tooltip("유료 선물 원래 가치 (다이아 환산)")]
    public int paidGiftOriginalValue;  // 예: 10000 = "10000다이아 상당"

    [Tooltip("할인율 표시 텍스트 (예: 90%)")]
    public string discountPercent;     // 예: "90%"

    [Tooltip("유료 선물 보상 목록")]
    public VipRewardEntry[] paidRewards;
}

/// <summary>
/// VIP 등급 하나의 모든 데이터를 담는 ScriptableObject
/// 예) VipData_VIP6 이런 이름으로 생성 사용
/// </summary>
[CreateAssetMenu(fileName = "VipData_VIP1", menuName = "VIP/VipData")]
public class VipData : ScriptableObject
{
    [Header("등급 기본 정보")]
    [Tooltip("VIP 등급 번호 (1, 2, 3 ...)")]
    public int vipLevel;               // 1, 2, 3...

    [Tooltip("이 등급으로 올라가기 위해 필요한 VIP 경험치")]
    public int requiredVipExp;         // 예: VIP6 = 2500

    [Tooltip("이 등급 이름 표시용 (예: VIP6)")]
    public string displayName;         // "VIP6"

    [Tooltip("등급 색상 (뱃지, 텍스트 색상)")]
    public Color gradeColor = Color.white;

    [Header("뱃지 이미지")]
    [Tooltip("VIP 등급별 뱃지 스프라이트 (Inspector에서 설정)")]
    public Sprite badgeSprite;

    [Tooltip("VIP 버튼에 표시할 아이콘 (미설정 시 badgeSprite 사용)")]
    public Sprite buttonIcon;

    [Header("혜택 목록")]
    [Tooltip("이 등급에서 제공되는 혜택 목록 (불릿 리스트로 표시됨)")]
    public List<VipBenefitData> benefits = new List<VipBenefitData>();

    [Header("선물 정보")]
    [Tooltip("이 등급의 무료/유료 선물 정보")]
    public VipGiftInfo giftInfo;

    [Header("기간 설정")]
    [Tooltip("VIP 기본 유효기간 (일)")]
    public int defaultDurationDays = 30; // 기본 30일
}
