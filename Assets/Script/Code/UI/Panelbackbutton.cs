using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ============================================================
/// PanelBackButton - 패널 뒤로가기(닫기) 범용 컴포넌트
/// ============================================================
/// 
/// 【역할】
/// 어떤 패널이든 이 스크립트를 붙이면 뒤로가기 기능 자동 추가
/// - 지정된 패널을 닫아줌
/// - SoundManager 효과음 자동 재생
/// - ESC 키로도 닫을 수 있음
/// 
/// 【사용법 - 매우 간단!】
/// 1. 뒤로가기 버튼 오브젝트에 이 스크립트를 붙이기
/// 2. Inspector에서:
///    - backButton: 뒤로가기 버튼 (자기 자신의 Button 컴포넌트 자동 감지)
///    - targetPanel: 닫을 패널의 GameObject
/// 3. 끝! 버튼 클릭하면 패널이 닫힘
/// 
/// 【적용할 패널 목록】
/// - 장비창 (Equipment Panel)
/// - 강화창 (Enhancement Panel)
/// - 조합창 (Crafting Panel)
/// - 스킬트리 (SkillTree Panel)
/// - 상점 (Shop Panel)
/// - 경매장 (Auction Panel)
/// - 업적창 (Achievement Panel)
/// - 인벤토리 (Inventory Panel)
/// - 메일창 (Mail Panel)
/// ============================================================
/// </summary>
public class PanelBackButton : MonoBehaviour
{
    [Header("===== 기본 설정 =====")]
    [Tooltip("뒤로가기 버튼 (비워두면 자기 자신의 Button 컴포넌트를 사용)")]
    [SerializeField] private Button backButton;

    [Tooltip("이 버튼을 누르면 닫힐 패널")]
    [SerializeField] private GameObject targetPanel;

    [Header("===== 추가 옵션 =====")]
    [Tooltip("ESC 키로도 닫을 수 있게 할지")]
    [SerializeField] private bool closeOnEscape = true;

    [Tooltip("닫을 때 효과음 재생할지")]
    [SerializeField] private bool playSoundOnClose = true;

    [Tooltip("닫을 때 재생할 효과음 이름 (SoundManager에 등록된 이름)")]
    [SerializeField] private string closeSoundName = "PanelClose";

    /// <summary>
    /// 초기화 - 버튼 이벤트 연결
    /// </summary>
    void Start()
    {
        // backButton이 비어있으면 자기 자신의 Button 컴포넌트 사용
        if (backButton == null)
        {
            backButton = GetComponent<Button>();
        }

        // 버튼 클릭 이벤트 연결
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonClicked);
        }
        else
        {
            Debug.LogWarning($"[PanelBackButton] {gameObject.name}에 Button 컴포넌트가 없습니다!");
        }
    }

    /// <summary>
    /// 매 프레임 ESC 키 체크
    /// </summary>
    void Update()
    {
        // ESC 키로 닫기 (패널이 활성화되어 있을 때만)
        if (closeOnEscape && Input.GetKeyDown(KeyCode.Escape))
        {
            if (targetPanel != null && targetPanel.activeSelf)
            {
                OnBackButtonClicked();
            }
        }
    }

    /// <summary>
    /// 뒤로가기 버튼 클릭 시 호출
    /// 대상 패널을 비활성화(닫기)함
    /// </summary>
    public void OnBackButtonClicked()
    {
        // 효과음 재생
        if (playSoundOnClose && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(closeSoundName);
        }

        // 패널 닫기
        if (targetPanel != null)
        {
            targetPanel.SetActive(false);
            Debug.Log($"[PanelBackButton] '{targetPanel.name}' 패널 닫힘");
        }
        else
        {
            Debug.LogWarning("[PanelBackButton] targetPanel이 설정되지 않았습니다!");
        }
    }
}