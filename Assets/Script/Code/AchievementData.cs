using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AchievementType
{
    KillMonsters,       // ���� óġ
    ReachLevel,         // ���� �޼�
    CollectGold,        // ��� ����
    CraftItems,         // ������ ����
    CompleteQuests,     // ����Ʈ �Ϸ�
    EnhanceEquipment,   // ��� ��ȭ
    UseSkills,          // ��ų ���
    KillBoss,
    GachaCount,
    EnhanceHelmet,          // 투구 강화
    EnhanceArmor,           // 갑옷 강화
    EnhanceWeapon,          // 무기 강화
    EnhanceGloves,          // 장갑 강화
    EnhanceBoots,           // 신발 강화
    EquipLevelUp,           // 장비 레벨업
    CompanionAscend,        // 동료 승성
    CompanionLevelUp        // 동료 레벨업
}

// ���� ���
public enum AchievementGrade
{
    Bronze,     // �����
    Silver,     // �ǹ�
    Gold,       // ���
    Platinum    // �÷�Ƽ��
}

// ���� ����
[System.Serializable]
public class AchievementReward
{
    public int gold;                    // ��� ����
    public int gem;                     // ���� ����
    public int exp;                     // ����ġ ����
    public ItemData[] items;            // ������ ����
    public string title;                // Īȣ ����
}

// ���� ������
[CreateAssetMenu(fileName = "New Achievement", menuName = "Game/Achievement")]
public class AchievementData : ScriptableObject
{
    [Header("�⺻ ����")]
    public int achievementID;           // ���� ID
    public string achievementName;      // ���� �̸�

    [TextArea(2, 4)]
    public string description;          // ����

    public Sprite icon;                 // ������

    [Header("���� Ÿ��")]
    public AchievementType type;        // ���� Ÿ��
    public AchievementGrade grade;      // ���� ���

    [Header("��ǥ")]
    public int targetAmount;            // ��ǥ ��ġ
    public string targetID;             // ��� ID (���� �̸� ��)

    [Header("����")]
    public AchievementReward reward;    // ����

    [Header("���� ����")]
    public bool isHidden = false;       // ���� ���� ����
}

// ���� ���� ��Ȳ
[System.Serializable]
public class AchievementProgress
{
    public AchievementData achievement;
    public int currentAmount;
    public bool isCompleted;
    public bool isRewarded;

    public AchievementProgress(AchievementData data)
    {
        achievement = data;
        currentAmount = 0;
        isCompleted = false;
        isRewarded = false;
    }

    public float GetProgressRatio()
    {
        if (achievement == null || achievement.targetAmount <= 0) return 1f;
        return Mathf.Clamp01((float)currentAmount / achievement.targetAmount);
    }
}
