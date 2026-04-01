using UnityEngine;

/// <summary>
/// ══════════════════════════════════════════════════════════
/// ManagerRoot — 씬 로컬 매니저 부모 오브젝트 (리팩토링)
/// ══════════════════════════════════════════════════════════
///
/// ▶ 기존 문제
///   · DontDestroyOnLoad로 모든 자식 매니저를 씬 간에 유지
///   · 새 씬 로드 시 기존 매니저와 새 매니저가 충돌
///   · Singleton Instance 중복으로 null 참조, 데이터 오염 발생
///
/// ▶ 변경사항
///   · DontDestroyOnLoad 완전 제거
///   · 씬마다 ManagerRoot와 자식 매니저들이 새로 생성됨
///   · 데이터 유지는 GameDataBridge(정적 클래스)가 담당
///   · 씬 전환 전 SaveLoadManager.SaveGame() → JSON 저장
///   · 새 씬 로드 후 SaveLoadManager.Start() → JSON 복원
///
/// ▶ SceneTransitionManager 배치 주의
///   · SceneTransitionManager는 ManagerRoot의 자식이 아닌
///     독립 오브젝트로 씬에 배치해야 합니다
///   · SceneTransitionManager만 DontDestroyOnLoad 유지
/// ══════════════════════════════════════════════════════════
/// </summary>
[DefaultExecutionOrder(-999)] // 모든 매니저 중 가장 먼저 Awake
public class ManagerRoot : MonoBehaviour
{
    public static ManagerRoot Instance { get; private set; }

    void Awake()
    {
        // Canvas 계층 안에 잘못 배치된 경우 차단
        if (IsInsideCanvas())
        {
            Debug.LogWarning($"[ManagerRoot] '{gameObject.name}'이 Canvas 계층 안에 있음 → 초기화 차단. " +
                             "FarmCanvas에는 DestroyOnSceneUnload 컴포넌트를 사용하세요.");
            return;
        }

        // ★ 씬 로컬 싱글톤 — DontDestroyOnLoad 없음
        // 같은 씬에 2개 이상 배치된 경우만 중복 제거
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ManagerRoot] 같은 씬에 ManagerRoot 중복 감지 → 파괴");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Debug.Log("[ManagerInit] ManagerRoot가 생성되었습니다.");
        Debug.Log($"[ManagerRoot] 씬 로컬 등록 완료 (자식 {transform.childCount}개 매니저) — DontDestroyOnLoad 없음");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>자신이 Canvas 계층 안에 있는지 확인</summary>
    private bool IsInsideCanvas()
    {
        Transform t = transform;
        while (t != null)
        {
            if (t.GetComponent<Canvas>() != null) return true;
            t = t.parent;
        }
        return false;
    }
}