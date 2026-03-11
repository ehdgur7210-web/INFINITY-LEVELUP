using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// WaterData.cs
//
// ★ 물 타입 ScriptableObject
//   예: 수돗물, 청정수, 영양제
//
// ★ 생성: Create → Farm → Water Data
// ═══════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "NewWater", menuName = "Farm/Water Data")]
public class WaterData : ScriptableObject
{
    [Header("===== 기본 정보 =====")]
    public int waterID;
    public string waterName;
    [TextArea(1, 2)]
    public string description;
    public Sprite icon;

    [Header("===== 성장 단축 효과 =====")]
    [Tooltip("추가 성장 시간 단축 비율 (0.1 = 10% 추가 단축). 기본 물은 0")]
    [Range(0f, 0.5f)]
    public float extraSpeedBonus = 0f;

    [Header("===== 구매 비용 =====")]
    public int costGold = 0;
    public int costGem = 1;

    [Header("===== 해금 조건 =====")]
    public int requiredPlayerLevel = 1;
}