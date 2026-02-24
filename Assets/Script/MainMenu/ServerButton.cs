using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ServerButton : MonoBehaviour
{
    [Header("UI 요소")]
    [SerializeField] private TextMeshProUGUI serverNameText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI populationText;
    [SerializeField] private Image statusIcon;
    [SerializeField] private Button button;

    private ServerInfo serverInfo;
    private System.Action onClickCallback;

    /// <summary>
    /// 버튼 설정
    /// </summary>
    public void SetupButton(ServerInfo info, System.Action onClick)
    {
        serverInfo = info;
        onClickCallback = onClick;

        // 텍스트 설정
        if (serverNameText != null)
            serverNameText.text = info.serverName;

        if (statusText != null)
            statusText.text = GetStatusText(info.serverStatus);

        if (populationText != null)
            populationText.text = $"{info.currentPlayers}/{info.maxPlayers}";

        // 상태 아이콘 색상
        if (statusIcon != null)
            statusIcon.color = GetStatusColor(info.serverStatus);

        // 버튼 이벤트
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);

            // 점검중이면 버튼 비활성화
            button.interactable = info.serverStatus != ServerStatus.Maintenance;
        }
    }

    private void OnButtonClicked()
    {
        onClickCallback?.Invoke();
    }

    private string GetStatusText(ServerStatus status)
    {
        switch (status)
        {
            case ServerStatus.Normal: return "정상";
            case ServerStatus.Crowded: return "혼잡";
            case ServerStatus.Maintenance: return "점검중";
            default: return "알 수 없음";
        }
    }

    private Color GetStatusColor(ServerStatus status)
    {
        switch (status)
        {
            case ServerStatus.Normal: return Color.green;
            case ServerStatus.Crowded: return Color.yellow;
            case ServerStatus.Maintenance: return Color.red;
            default: return Color.gray;
        }
    }
}