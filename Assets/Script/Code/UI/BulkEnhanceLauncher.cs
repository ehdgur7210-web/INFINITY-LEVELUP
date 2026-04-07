using UnityEngine;

/// <summary>
/// BulkEnhanceLauncher
/// ─────────────────────────────────────────────────────────────
/// 일괄강화 패널은 평소 비활성 상태이므로, 그 위에 부착된 BulkEnhancePanel
/// 컴포넌트의 메서드는 Unity Button OnClick(persistent) 이벤트로 직접 호출할 수 없다.
/// (Unity 정책: persistent callback은 비활성 GameObject의 메서드를 호출하지 않음)
///
/// 이 런처는 ⚠항상 활성인 GameObject⚠ (예: EnhancementPanel, UIRoot 등)에 부착하여
/// 일괄강화 패널을 켜 주는 진입점 역할을 한다.
///
/// 사용법:
/// 1) 항상 활성인 게임오브젝트에 이 컴포넌트 부착 (예: EnhancementPanel)
/// 2) Inspector에서 bulkPanelRoot, bulkEnhancePanel 두 필드 연결
/// 3) "일괄강화탭버튼"의 OnClick →
///        BulkEnhanceLauncher.OpenBulkPanel 으로 다시 연결
///    ("강화탭버튼"의 OnClick은 그대로 단일 패널 토글에 사용하면 됨)
/// </summary>
public class BulkEnhanceLauncher : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("일괄강화 패널 루트 GameObject (비활성 상태여도 OK)")]
    public GameObject bulkPanelRoot;

    [Tooltip("BulkEnhancePanel 컴포넌트 (비활성 GameObject 위에 있어도 OK)")]
    public BulkEnhancePanel bulkEnhancePanel;

    [Header("선택")]
    [Tooltip("열 때 단일 강화 패널을 끌지 여부")]
    public GameObject singleEnhancePanelToHide;

    /// <summary>일괄강화 탭 버튼의 OnClick에 연결</summary>
    public void OpenBulkPanel()
    {
        if (bulkPanelRoot == null)
        {
            Debug.LogError("[BulkEnhanceLauncher] bulkPanelRoot가 비어있습니다. Inspector에서 연결해주세요.");
            return;
        }

        // 단일 패널 닫기 (선택)
        if (singleEnhancePanelToHide != null && singleEnhancePanelToHide.activeSelf)
            singleEnhancePanelToHide.SetActive(false);

        // 일괄강화 패널 활성화 — 이 시점부터 BulkEnhancePanel.Start()가 실행됨
        if (!bulkPanelRoot.activeSelf)
            bulkPanelRoot.SetActive(true);

        // BulkEnhancePanel.OpenPanel() 직접 호출
        // (Start보다 먼저 호출될 수 있으므로, BulkEnhancePanel 쪽에서도 idempotent 보장 필요)
        if (bulkEnhancePanel != null)
            bulkEnhancePanel.OpenPanel();
        else
            Debug.LogWarning("[BulkEnhanceLauncher] bulkEnhancePanel 참조가 비어있습니다.");
    }

    /// <summary>외부에서 닫기 호출이 필요할 때</summary>
    public void CloseBulkPanel()
    {
        if (bulkEnhancePanel != null && bulkEnhancePanel.gameObject.activeInHierarchy)
            bulkEnhancePanel.ClosePanel();
        else if (bulkPanelRoot != null)
            bulkPanelRoot.SetActive(false);
    }
}
