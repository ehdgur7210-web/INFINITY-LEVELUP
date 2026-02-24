using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AchievementType
{
    KillMonsters,       // 몬스터 처치
    ReachLevel,         // 레벨 달성
    CollectGold,        // 골드 수집
    CraftItems,         // 아이템 제작
    CompleteQuests,     // 퀘스트 완료
    EnhanceEquipment,   // 장비 강화
    UseSkills,          // 스킬 사용
    KillBoss,             // 보스킬
    GachaCount          //가차횟수
}

// 업적 등급
public enum AchievementGrade
{
    Bronze,     // 브론즈
    Silver,     // 실버
    Gold,       // 골드
    Platinum    // 플래티넘
}

// 업적 보상
[System.Serializable]
public class AchievementReward
{
    public int gold;                    // 골드 보상
    public int gem;                     // 보석 보상
    public int exp;                     // 경험치 보상
    public ItemData[] items;            // 아이템 보상
    public string title;                // 칭호 보상
}

// 업적 데이터
[CreateAssetMenu(fileName = "New Achievement", menuName = "Game/Achievement")]
public class AchievementData : ScriptableObject
{
    [Header("기본 정보")]
    public int achievementID;           // 업적 ID
    public string achievementName;      // 업적 이름

    [TextArea(2, 4)]
    public string description;          // 설명

    public Sprite icon;                 // 아이콘

    [Header("업적 타입")]
    public AchievementType type;        // 업적 타입
    public AchievementGrade grade;      // 업적 등급

    [Header("목표")]
    public int targetAmount;            // 목표 수치
    public string targetID;             // 대상 ID (몬스터 이름 등)

    [Header("보상")]
    public AchievementReward reward;    // 보상

    [Header("숨김 업적")]
    public bool isHidden = false;       // 숨김 업적 여부
}

// 업적 진행 상황
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
        return Mathf.Clamp01((float)currentAmount / achievement.targetAmount);
    }
}
