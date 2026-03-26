/// <summary>
/// 동료 세이브 데이터 (JSON 직렬화용)
/// SaveData.companions 리스트에 저장
/// </summary>
[System.Serializable]
public class CompanionSaveData
{
    public string companionID;
    public int count;
    public int level = 1;
    public int exp = 0;      // 현재 레벨 내 경험치
    public int stars = -1;   // -1 = CompanionData.baseStars 사용 (초기 상태)
    public CompanionSkillLevelEntry[] skillLevels;  // 스킬 레벨 저장
}

/// <summary>
/// 동료 스킬 레벨 저장 엔트리 (JSON 직렬화용)
/// </summary>
[System.Serializable]
public class CompanionSkillLevelEntry
{
    public string skillID;
    public int level = 1;
}
