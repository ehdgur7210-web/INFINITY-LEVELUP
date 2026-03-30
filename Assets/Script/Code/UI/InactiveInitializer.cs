using UnityEngine;

/// <summary>
/// 에디터에서 비활성(꺼진) 상태인 오브젝트들을 런타임 시작 시
/// 강제로 활성화 → Awake/Start 호출 → 다시 비활성화
///
/// ★ 사용법:
/// 1. 씬에 빈 오브젝트 생성 (항상 활성 상태)
/// 2. 이 스크립트 붙이기
/// 3. "초기화할 오브젝트들" 배열에 비활성 패널들 드래그
///    (옵션패널, 캐릭터생성패널, PlantMode_Overlay 등)
/// </summary>
public class InactiveInitializer : MonoBehaviour
{
    [Header("런타임 시작 시 강제 초기화할 오브젝트들")]
    [Tooltip("에디터에서 꺼져있어도 Awake/Start가 호출되게 할 오브젝트")]
    [SerializeField] private GameObject[] 초기화할오브젝트들;

    void Awake()
    {
        if (초기화할오브젝트들 == null) return;

        foreach (var go in 초기화할오브젝트들)
        {
            if (go == null) continue;
            if (go.activeSelf) continue; // 이미 켜져있으면 스킵

            // 강제 활성화 → Awake 호출됨
            go.SetActive(true);
            // 바로 비활성화 → 각 스크립트의 Awake에서 자체 숨김 처리
            go.SetActive(false);

            Debug.Log($"[InactiveInitializer] {go.name} 강제 초기화 완료");
        }
    }
}
