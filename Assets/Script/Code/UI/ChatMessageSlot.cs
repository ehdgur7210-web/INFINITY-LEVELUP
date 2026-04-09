using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 채팅 메시지 슬롯 — messagePrefab에 붙임 (선택)
///
/// 이 컴포넌트를 안 붙여도 ChatSystem이 폴백으로 동작.
/// 붙이면 깔끔하게 아이콘+이름+레벨+본문이 분리 표시됨.
///
/// [Inspector 연결]
///   iconImage   : 발신자 캐릭터 아이콘 (Image)
///   nameText    : 발신자 닉네임 (TMP)
///   levelText   : 발신자 레벨 (TMP)
///   messageText : 메시지 본문 (TMP)
///   timeText    : 시간 (TMP, 선택)
/// </summary>
public class ChatMessageSlot : MonoBehaviour
{
    [Header("아이콘")]
    [SerializeField] private Image iconImage;

    [Header("텍스트")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI timeText;

    /// <summary>
    /// 채팅 메시지 적용
    /// </summary>
    public void Setup(string senderName, int senderLevel, int classIndex,
                      string message, Color nameColor, string timestamp)
    {
        if (iconImage != null)
        {
            Sprite sp = ProfileIconDatabase.GetIcon(classIndex);
            if (sp != null)
            {
                iconImage.sprite = sp;
                iconImage.preserveAspect = true;
                iconImage.enabled = true;
                iconImage.color = Color.white;
            }
            else
            {
                // ★ 스프라이트 없으면 흰 박스 안 보이게 숨김
                iconImage.enabled = false;
            }
        }

        if (nameText != null)
        {
            nameText.text = senderName;
            nameText.color = nameColor;
        }

        if (levelText != null)
            levelText.text = senderLevel > 0 ? $"Lv.{senderLevel}" : "";

        if (messageText != null)
            messageText.text = message;

        if (timeText != null)
            timeText.text = timestamp ?? "";
    }

    /// <summary>
    /// 시스템 메시지용 셋업 — 아이콘/이름/레벨/시간 모두 숨기고 메시지 본문만 노란색으로 표시
    /// </summary>
    public void SetupSystem(string systemMessage)
    {
        if (iconImage != null)
            iconImage.enabled = false;

        if (nameText != null)
            nameText.text = "";

        if (levelText != null)
            levelText.text = "";

        if (timeText != null)
            timeText.text = "";

        if (messageText != null)
        {
            messageText.text = $"<color=#FFD700>{systemMessage}</color>";
            messageText.alignment = TMPro.TextAlignmentOptions.Center;
        }
    }

    /// <summary>
    /// 구조 요청 메시지용 셋업 — 아이콘 숨김, 메시지 본문에 발신자 + 전투력 + 구조 표시
    /// </summary>
    public void SetupRescue(string senderName, int combatPower, Color nameColor, string formattedMessage)
    {
        if (iconImage != null)
            iconImage.enabled = false;

        if (nameText != null)
            nameText.text = "";

        if (levelText != null)
            levelText.text = "";

        if (timeText != null)
            timeText.text = "";

        if (messageText != null)
        {
            messageText.text = formattedMessage;
            messageText.alignment = TMPro.TextAlignmentOptions.Center;
        }
    }
}
