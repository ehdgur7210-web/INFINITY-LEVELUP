using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 유저 검색 결과 슬롯 — searchResultItemPrefab에 붙임
///
/// [Inspector 연결]
///   iconImage   : 캐릭터아이콘 (Image)
///   nameText    : 네임 (TMP)
///   levelText   : 레벨 (TMP)
///   addButton   : 친구추가 (Button)
///   addBtnText  : 친구추가 버튼 안 텍스트 (선택)
/// </summary>
public class SearchResultSlot : MonoBehaviour
{
    [Header("아이콘")]
    [SerializeField] private Image iconImage;

    [Header("텍스트")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("버튼")]
    [SerializeField] private Button addButton;
    [SerializeField] private TextMeshProUGUI addBtnText;

    public Button AddButton => addButton;
    public TextMeshProUGUI AddBtnText => addBtnText;

    public void Setup(FriendSearchResult result)
    {
        if (nameText != null) nameText.text = result.nickname;

        if (levelText != null)
            levelText.text = $"Lv.{result.level}";
        else if (nameText != null)
            nameText.text = $"Lv.{result.level} {result.nickname}";

        if (iconImage != null)
        {
            Sprite sp = ProfileIconDatabase.GetIcon(result.classIndex);
            if (sp != null)
            {
                iconImage.sprite = sp;
                iconImage.preserveAspect = true;
                iconImage.enabled = true;
            }
        }
    }
}
