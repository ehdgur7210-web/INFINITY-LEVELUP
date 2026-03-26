using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// FertilizerData.cs
//
// ★ 설명:
//   비료 1종의 데이터를 담는 ScriptableObject
//   이전에 Farmdata.cs 안에 CropData와 같이 있던 것을 분리
//
// ★ 생성 방법:
//   Project창 우클릭 → Create → Farm → Fertilizer Data
// ═══════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "NewFertilizer", menuName = "Farm/Fertilizer Data")]
public class FertilizerData : ScriptableObject
{
    // ─── 기본 정보 ───────────────────────────────────────────────
    [Header("===== 기본 정보 =====")]
    public int fertilizerID;
    public string fertilizerName;
    [TextArea(1, 2)]
    public string description;
    public Sprite icon;

    // ─── 효과 ────────────────────────────────────────────────────
    [Header("===== 효과 =====")]
    [Tooltip("성장 시간 단축 비율 (0.2 = 20% 단축)")]
    [Range(0f, 0.9f)]
    public float speedBonus = 0.2f;

    [Tooltip("수확량 증가 비율 (0.5 = 50% 증가)")]
    [Range(0f, 2f)]
    public float yieldBonus = 0.5f;

    [Tooltip("작물 포인트 보너스 (0.3 = 30% 증가)")]
    [Range(0f, 1f)]
    public float cropPointBonus = 0.3f;

    // ─── 구매 비용 ───────────────────────────────────────────────
    [Header("===== 구매 비용 =====")]
    public int costGold = 100;
    public int costGem = 0;

    // ─── 해금 조건 ───────────────────────────────────────────────
    [Header("===== 해금 조건 =====")]
    public int requiredPlayerLevel = 1;
}