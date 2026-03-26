using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 공지사항 패널
/// - 공지 버튼 클릭 → 패널 열기
/// - 패널 꺼기 버튼(뒤로가기) → 패널 닫기
/// 
/// [Inspector 연결]
/// - noticePanel    : 공지사항패널 오브젝트
/// - openButton     : 공지사항 열기 버튼 (메인메뉴 등)
/// - closeButton    : 패널 꺼기 버튼
/// - noticeText     : 공지 내용 텍스트 (선택)
/// </summary>
public class NoticePanel : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject noticePanel;   // 공지사항패널

    [Header("버튼")]
    [SerializeField] private Button openButton;        // 공지 열기 버튼
    [SerializeField] private Button closeButton;       // 패널 꺼기 버튼

    [Header("공지 내용 (선택)")]
    [SerializeField] private TextMeshProUGUI noticeText;  // 공지 텍스트
    [TextArea(3, 10)]
    [SerializeField] private string noticeContent = "공지사항 내용을 여기에 입력하세요.";

    void Start()
    {
        // 버튼 이벤트 연결
        if (openButton != null) openButton.onClick.AddListener(OpenPanel);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);

        // 공지 텍스트 설정
        if (noticeText != null)
            noticeText.text = noticeContent;

        // 시작 시 패널 닫기
        ClosePanel();
    }

    // 패널 열기
    public void OpenPanel()
    {
        if (noticePanel != null)
            noticePanel.SetActive(true);
    }

    // 패널 닫기
    public void ClosePanel()
    {
        if (noticePanel != null)
            noticePanel.SetActive(false);
    }

    // 토글 (필요 시 사용)
    public void TogglePanel()
    {
        if (noticePanel != null)
            noticePanel.SetActive(!noticePanel.activeSelf);
    }

    // 공지 내용 외부에서 변경할 때
    public void SetNoticeContent(string content)
    {
        noticeContent = content;
        if (noticeText != null)
            noticeText.text = content;
    }
}