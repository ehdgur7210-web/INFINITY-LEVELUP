using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EquipmentType 확장 메서드
/// 장비 타입을 상점 카테고리로 자동 변환
/// </summary>
public static class EquipmentTypeExtensions
{
    /// <summary>
    /// EquipmentType을 상점 카테고리(ItemType)로 변환
    /// 할인 시스템에서 사용
    /// </summary>
    /// <example>
    /// EquipmentType.Helmet.ToShopCategory() → ItemType.Armor
    /// EquipmentType.Weapon.ToShopCategory() → ItemType.Weapon
    /// </example>
    public static ItemType ToShopCategory(this EquipmentType equipType)
    {
        switch (equipType)
        {
            // 무기류 - Weapon 할인 적용
            case EquipmentType.WeaponLeft:
            case EquipmentType.WeaponRight:
                return ItemType.Weapon;

            // 방어구류 - 모두 Armor 할인 적용
            case EquipmentType.Helmet:
            case EquipmentType.Armor:
            case EquipmentType.Gloves:
            case EquipmentType.Boots:
                return ItemType.Armor;

            // 기본값
            default:
                return ItemType.Misc;
        }
    }
}