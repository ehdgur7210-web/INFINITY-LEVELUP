using UnityEngine;

/// <summary>
/// 동료 가챠 탭 데이터
/// Inspector에서 탭별 소환 데이터를 설정하는 구조체.
/// CompanionGachaManager.summonTabs[] 배열로 관리.
/// </summary>
[System.Serializable]
public class SummonTabData
{
    [Tooltip("탭 이름 (일반소환, 고급소환, 우정소환 등)")]
    public string tabName;

    [Tooltip("탭 배너 이미지")]
    public Sprite bannerImage;

    [Header("1회 뽑기 비용")]
    [Tooltip("다이아(젬) 비용, 0이면 비활성")]
    public int singleCostDia = 100;
    [Tooltip("동료 뽑기 티켓 비용, 0이면 비활성")]
    public int singleCostTicket = 1;

    [Header("10회 뽑기 비용")]
    [Tooltip("다이아(젬) 비용")]
    public int multiCostDia = 900;
    [Tooltip("동료 뽑기 티켓 비용")]
    public int multiCostTicket = 10;

    [Header("뽑기 풀")]
    [Tooltip("이 탭에서 뽑을 수 있는 동료 목록")]
    public CompanionData[] companionPool;
}
