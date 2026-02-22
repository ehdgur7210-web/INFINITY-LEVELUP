using UnityEngine;

/// <summary>
/// 장비 타입 열거형 (최종 버전)
/// ⭐ Weapon → WeaponLeft, WeaponRight 분리
/// ⭐ Accessory 제거
/// ⭐ 총 6개 타입
/// </summary>
public enum EquipmentType
{
    WeaponLeft,  // 왼손 무기
    WeaponRight, // 오른손 무기
    Helmet,      // 투구
    Armor,       // 갑옷
    Gloves,      // 장갑
    Boots        // 신발
}

/// <summary>
/// 장비 아이템 데이터
/// </summary>
[CreateAssetMenu(fileName = "New Equipment", menuName = "Game/Equipment Data")]
public class EquipmentData : ItemData
{
    [Header("장비 정보")]
    public EquipmentType equipmentType;     // 장비 타입

    [Header("스탯 보너스")]
    public int attackBonus = 0;             // 공격력 증가
    public int defenseBonus = 0;            // 방어력 증가
    public int hpBonus = 0;                 // 체력 증가
    public int speedBonus = 0;              // 이동속도 증가
    public int criticalBonus = 0;           // 치명타 확률 증가 (%)

    [Header("특수 효과")]
    public bool hasSpecialEffect = false;   // 특수 효과 여부
    [TextArea(2, 4)]
    public string specialEffectDescription; // 특수 효과 설명

    [Header("요구 레벨")]
    public int requiredLevel = 1;           // 장착 요구 레벨

    [Header("시각 효과")]
    public GameObject equipmentModel;       // 장비 3D 모델
    public Material equipmentMaterial;      // 장비 재질
}

/// <summary>
/// 2D 스프라이트 기반 장비 외형 시스템
/// </summary>
[CreateAssetMenu(fileName = "New Equipment Visual", menuName = "Game/Equipment Visual Data")]
public class EquipmentVisualData : EquipmentData
{
    [Header("2D 비주얼 설정")]
    public Sprite equipmentSprite;          // 장비 스프라이트
    public Color spriteColor = Color.white; // 스프라이트 색상

    [Header("위치 설정")]
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localRotation = Vector3.zero;
    public Vector3 localScale = Vector3.one;

    [Header("애니메이션")]
    public RuntimeAnimatorController animatorOverride; // 애니메이션 오버라이드 (선택사항)
}