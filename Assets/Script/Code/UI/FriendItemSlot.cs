using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 친구 목록 슬롯 — friendItemPrefab에 붙임
///
/// [Inspector 연결]
///   iconImage    : 캐릭터아이콘 (Image)
///   nameText     : 네임 (TMP)
///   levelText    : 레벨 (TMP)  ← 없으면 nameText 안에 같이 표시
///   pointButton  : 포인트전송 (Button)
///   pointBtnText : 포인트전송 버튼 안 텍스트 (선택)
///   deleteButton : 삭제 (Button)
/// </summary>
public class FriendItemSlot : MonoBehaviour
{
    [Header("아이콘")]
    [SerializeField] private Image iconImage;

    [Header("텍스트")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("버튼")]
    [SerializeField] private Button pointButton;
    [SerializeField] private TextMeshProUGUI pointBtnText;
    [SerializeField] private Button deleteButton;

    public Button PointButton => pointButton;
    public Button DeleteButton => deleteButton;
    public TextMeshProUGUI PointBtnText => pointBtnText;

    /// <summary>친구 데이터 적용</summary>
    public void Setup(FriendData friend)
    {
        if (nameText != null) nameText.text = friend.nickname;

        if (levelText != null)
            levelText.text = $"Lv.{friend.level}";
        else if (nameText != null)
            nameText.text = $"Lv.{friend.level} {friend.nickname}";

        if (iconImage != null)
        {
            Sprite sp = ProfileIconDatabase.GetIcon(friend.classIndex);
            if (sp != null)
            {
                iconImage.sprite = sp;
                iconImage.preserveAspect = true;
                iconImage.enabled = true;
            }
        }
    }
}
