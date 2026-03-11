using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
