using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class CharacterData
{
    public string characterName;
    public CharacterType characterType;
    public string description;

    [Header("기본 스탯")]
    public float baseHealth;
    public float baseAttack;
    public float baseDefense;
    public float baseSpeed;
    public float attackRange;
    public float attackSpeed;

    [Header("비주얼")]
    public Sprite previewSprite;
    public GameObject characterPrefab; // 실제 게임에서 사용할 프리팹
}

/// <summary>
/// 캐릭터 타입
/// </summary>
public enum CharacterType
{
    Melee,   // 근거리
    Ranged   // 원거리
}
public class CharacterButton : MonoBehaviour
{
    [Header("UI 요소")]
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private TextMeshProUGUI characterTypeText;
    [SerializeField] private Image characterIcon;
    [SerializeField] private Button button;

    private CharacterData characterData;
    private System.Action onClickCallback;

    private void Awake()
    {
        // button이 할당되지 않았다면 자동으로 찾기
        if (button == null)
        {
            button = GetComponent<Button>();
            Debug.Log($"[CharacterButton] Button 자동 할당: {button != null}");
        }
    }

    /// <summary>
    /// 버튼 설정
    /// </summary>
    public void SetupButton(CharacterData data, System.Action onClick)
    {
        Debug.Log($"[CharacterButton] ===== SetupButton 시작: {data.characterName} =====");

        characterData = data;
        onClickCallback = onClick;

        // 텍스트 설정
        if (characterNameText != null)
        {
            characterNameText.text = data.characterName;
            Debug.Log($"[CharacterButton] 이름 텍스트 설정: {data.characterName}");
        }
        else
            Debug.LogWarning($"[CharacterButton] characterNameText가 null! ({data.characterName})");

        if (characterTypeText != null)
        {
            string typeText = data.characterType == CharacterType.Melee ? "근거리" : "원거리";
            characterTypeText.text = typeText;
            Color typeColor = data.characterType == CharacterType.Melee ?
                new Color(1f, 0.5f, 0.5f) : new Color(0.5f, 0.5f, 1f);
            characterTypeText.color = typeColor;
            Debug.Log($"[CharacterButton] 타입 텍스트 설정: {typeText}");
        }

        // 아이콘 설정
        if (characterIcon != null && data.previewSprite != null)
        {
            characterIcon.sprite = data.previewSprite;
            Debug.Log($"[CharacterButton] 아이콘 설정 완료");
        }

        // 버튼 이벤트
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);
            Debug.Log($"[CharacterButton] 버튼 리스너 등록: {data.characterName}");
        }
        else
        {
            Debug.LogError($"[CharacterButton] Button이 null! ({data.characterName})");
        }

        Debug.Log($"[CharacterButton] ===== SetupButton 완료: {data.characterName} =====");
    }

    private void OnButtonClicked()
    {
        Debug.Log($"[CharacterButton] OnButtonClicked 호출됨");
        // ★ 캐릭터 버튼 클릭 효과음
        SoundManager.Instance?.PlayButtonClick();

        if (characterData != null)
        {
            Debug.Log($"[CharacterButton] 캐릭터 데이터: {characterData.characterName}");
        }
        else
        {
            Debug.LogError("[CharacterButton] characterData가 null입니다!");
        }

        if (onClickCallback != null)
        {
            Debug.Log($"[CharacterButton] 콜백 실행");
            onClickCallback.Invoke();
        }
        else
        {
            Debug.LogError("[CharacterButton] onClickCallback이 null입니다!");
        }
    }
}