/// <summary>
/// 캐릭터 타입 열거형
/// - PlayerController, CharacterSelectManager, CharacterCreateManager에서 공통 사용
/// </summary>
public enum CharacterType
{
    Melee = 0,   // 근거리 전사
    Ranged = 1,   // 원거리 궁수
    Magic = 2    // 마법사 ← 추가
}