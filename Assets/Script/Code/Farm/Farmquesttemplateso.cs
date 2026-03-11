using UnityEngine;

// ════════════════════════════════════════════════════════════
//  FarmQuestTemplateSO — 농장 퀘스트 1개 정의 ScriptableObject
//
//  [생성 방법]
//  Project 우클릭 → Create → Farm / Quest Template
//
//  [필드 설명]
//  - questTitle        : 퀘스트 이름
//  - questDescription  : 설명 텍스트
//  - targetCropID      : 대상 작물 ID (CropData.cropID 와 일치)
//  - questIcon         : 퀘스트 아이콘 (없으면 작물 아이콘 사용)
//  - difficulty        : 난이도 — 보상/요구량 스케일에 영향
//  - baseRequiredAmount         : 기본 요구 개수
//  - requiredAmountPerTenLevels : 플레이어 10레벨마다 추가 요구량
//  - baseCropPointReward        : 기본 작물 포인트 보상
//  - cropPointRewardPerTenLevels: 플레이어 10레벨마다 추가 보상
//  - goldReward                 : 골드 보상 (고정)
//  - specialItemReward          : 특별 아이템 보상 (선택)
//  - specialItemCount           : 특별 아이템 수량
// ════════════════════════════════════════════════════════════
[CreateAssetMenu(fileName = "FarmQuestTemplate_New", menuName = "Farm/Quest Template")]
public class FarmQuestTemplateSO : ScriptableObject
{
    [Header("── 기본 정보 ──────────────────────")]
    public string questTitle = "작물 납품 퀘스트";

    [TextArea(1, 3)]
    public string questDescription = "작물을 수확해서 납품하세요!";

    [Tooltip("UI에 표시할 퀘스트 고유 아이콘 (비워두면 작물 아이콘 사용)")]
    public Sprite questIcon;

    [Header("── 대상 작물 ──────────────────────")]
    [Tooltip("CropData.cropID 값과 일치해야 합니다")]
    public int targetCropID;

    [Header("── 난이도 ──────────────────────────")]
    public QuestDifficulty difficulty = QuestDifficulty.Normal;

    [Header("── 요구량 (레벨 스케일) ──────────")]
    [Min(1)]
    public int baseRequiredAmount = 3;

    [Tooltip("플레이어 10레벨마다 요구량 증가분")]
    [Min(0)]
    public int requiredAmountPerTenLevels = 1;

    [Header("── 보상 (레벨 스케일) ──────────────")]
    [Min(1)]
    public int baseCropPointReward = 10;

    [Tooltip("플레이어 10레벨마다 작물 포인트 보상 증가분")]
    [Min(0)]
    public int cropPointRewardPerTenLevels = 5;

    [Min(0)]
    public int goldReward = 200;

    [Tooltip("특별 아이템 보상 (선택, 비워두면 없음)")]
    public ItemData specialItemReward;

    [Min(0)]
    public int specialItemCount = 1;

    // ─────────────────────────────────────
    // 계산 메서드
    // ─────────────────────────────────────

    /// <summary>플레이어 레벨에 따른 최종 요구량</summary>
    public int GetRequiredAmount(int playerLevel)
    {
        int extra = (playerLevel / 10) * requiredAmountPerTenLevels;
        return Mathf.Max(1, baseRequiredAmount + extra);
    }

    /// <summary>플레이어 레벨에 따른 최종 작물 포인트 보상</summary>
    public int GetCropPointReward(int playerLevel)
    {
        int extra = (playerLevel / 10) * cropPointRewardPerTenLevels;
        return Mathf.Max(1, baseCropPointReward + extra);
    }

    /// <summary>최종 골드 보상 (난이도 배율 적용)</summary>
    public int GetGoldReward()
    {
        float multiplier = difficulty switch
        {
            QuestDifficulty.Easy => 0.7f,
            QuestDifficulty.Normal => 1.0f,
            QuestDifficulty.Hard => 1.5f,
            QuestDifficulty.Elite => 2.5f,
            _ => 1.0f
        };
        return Mathf.RoundToInt(goldReward * multiplier);
    }
}

// ────────────────────────────────────────
//  퀘스트 난이도 열거형
// ────────────────────────────────────────
public enum QuestDifficulty
{
    Easy,
    Normal,
    Hard,
    Elite
}