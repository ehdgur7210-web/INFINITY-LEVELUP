using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ★ FarmCanvas, FarmScene 전용 UI 오브젝트에 부착
///
/// FarmScene → 다른 씬으로 넘어갈 때 이 오브젝트를 자동 파괴
/// → FarmCanvas가 MainGame에 따라오는 현상 완전 해결
///
/// 사용법:
///   Unity Hierarchy에서 FarmCanvas 오브젝트 선택
///   → Add Component → DestroyOnSceneUnload
/// </summary>
public class DestroyOnSceneUnload : MonoBehaviour
{
    [Tooltip("이 씬에서만 살아있어야 하는 씬 이름 (비워두면 무조건 씬 이동 시 파괴)")]
    [SerializeField] private string ownerSceneName = "FarmScene";

    void OnEnable()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnSceneUnloaded(Scene unloadedScene)
    {
        // ownerSceneName이 비어있으면 무조건 파괴
        // 지정했으면 해당 씬이 언로드될 때만 파괴
        if (string.IsNullOrEmpty(ownerSceneName) ||
            unloadedScene.name == ownerSceneName)
        {
            Debug.Log($"[DestroyOnSceneUnload] {gameObject.name} 파괴 ({unloadedScene.name} 언로드)");
            Destroy(gameObject);
        }
    }
}