using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 받은 친구 요청 슬롯 — requestItemPrefab에 붙임
///
/// [Inspector 연결]
///   iconImage     : 캐릭터아이콘 (Image)
///   nameText      : 네임 (TMP)
///   levelText     : 레벨 (TMP)
///   acceptButton  : 수락 (Button)
///   rejectButton  : 거절 (Button)
/// </summary>
public class RequestItemSlot : MonoBehaviour
{
    [Header("아이콘")]
    [SerializeField] private Image iconImage;

    [Header("텍스트")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("버튼")]
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button rejectButton;

    public Button AcceptButton => acceptButton;
    public Button RejectButton => rejectButton;

    public void Setup(FriendRequestData req)
    {
        if (nameText != null) nameText.text = req.nickname;

        if (levelText != null)
            levelText.text = $"Lv.{req.level}";
        else if (nameText != null)
            nameText.text = $"Lv.{req.level} {req.nickname}";

        if (iconImage != null)
        {
            Sprite sp = ProfileIconDatabase.GetIcon(req.classIndex);
            if (sp != null)
            {
                iconImage.sprite = sp;
                iconImage.preserveAspect = true;
                iconImage.enabled = true;
            }
        }
    }
}
