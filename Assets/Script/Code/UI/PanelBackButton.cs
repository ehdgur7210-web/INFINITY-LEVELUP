using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ============================================================
/// PanelBackButton - 패널 뒤로가기(닫기) 범용 컴포넌트
/// ============================================================
///
/// 사용법
/// 어떤 패널이든 이 스크립트를 붙이면 뒤로가기 기능이 자동 추가
/// - 지정한 패널을 닫아줌 (버튼 클릭 시)
/// - SoundManager 효과음 자동 재생
/// - ESC/뒤로가기는 AndroidBackButton이 통제 (이 컴포넌트는 OnEnable에 자동 등록)
///
/// 설치법
/// 1. 뒤로가기 버튼에 이 스크립트 부착
/// 2. Inspector에서:
///    - backButton: 뒤로가기 버튼 (자기 자신의 Button 컴포넌트 자동 사용)
///    - targetPanel: 닫을 패널의 GameObject
/// ============================================================
/// </summary>
public class PanelBackButton : MonoBehaviour
{
    [Header("===== 기본 설정 =====")]
    [Tooltip("뒤로가기 버튼 (비어있으면 자기 자신의 Button 컴포넌트를 사용)")]
    [SerializeField] private Button backButton;

    [Tooltip("이 버튼이 닫아줄 대상 패널")]
    [SerializeField] private GameObject targetPanel;

    [Header("===== 추가 옵션 =====")]
    [Tooltip("닫힐 때 효과음을 재생할지 여부")]
    [SerializeField] private bool playSoundOnClose = true;

    [Tooltip("닫힐 때 재생할 효과음 이름 (SoundManager에 등록된 이름)")]
    [SerializeField] private string closeSoundName = "PanelClose";

    // ─────────────────────────────────────────────────────────
    // ★ 전역 열린 패널 스택 — AndroidBackButton이 가장 최근 것부터 닫음
    // ─────────────────────────────────────────────────────────
    private static readonly List<PanelBackButton> _openStack = new List<PanelBackButton>();
    public static IReadOnlyList<PanelBackButton> OpenStack => _openStack;

    public GameObject TargetPanel => targetPanel;

    void Start()
    {
        if (backButton == null) backButton = GetComponent<Button>();
        if (backButton != null) backButton.onClick.AddListener(OnBackButtonClicked);
        else Debug.LogWarning($"[PanelBackButton] {gameObject.name}에 Button 컴포넌트가 없습니다!");
    }

    void OnEnable()
    {
        // ★ 패널이 켜지면 스택 push (자기 자신이 아니라 targetPanel이 켜질 때만)
        // 보통 backButton은 패널 자식이므로 함께 활성/비활성화됨
        if (targetPanel != null && targetPanel.activeInHierarchy)
        {
            if (!_openStack.Contains(this)) _openStack.Add(this);
        }
    }

    void OnDisable()
    {
        _openStack.Remove(this);
    }

    /// <summary>
    /// 뒤로가기 버튼 클릭 또는 AndroidBackButton에서 호출
    /// </summary>
    public void OnBackButtonClicked()
    {
        if (playSoundOnClose && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(closeSoundName);

        if (targetPanel != null)
        {
            targetPanel.SetActive(false);
            _openStack.Remove(this);
            Debug.Log($"[PanelBackButton] '{targetPanel.name}' 패널 닫힘");
        }
        else
        {
            Debug.LogWarning("[PanelBackButton] targetPanel이 지정되지 않았습니다!");
        }

        // ★ 배너를 레벨 텍스트로 복원
        TopMenuManager.Instance?.ClearBanner();
    }
}
