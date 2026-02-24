using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// 캐릭터 선택 시스템
/// - 근거리/원거리 캐릭터 선택
/// - 캐릭터 정보 표시
/// - 캐릭터 미리보기
/// </summary>
public class CharacterSelectionSystem : MonoBehaviour
{

    [Header("캐릭터 선택 패널")]
    [SerializeField] private GameObject characterSelectionPanel;

    [Header("캐릭터 목록")]
    [SerializeField] private Transform characterListContainer;
    [SerializeField] private GameObject characterButtonPrefab;

    [Header("캐릭터 미리보기")]
    [SerializeField] private Image characterPreviewImage;
    [SerializeField] private GameObject characterPreview3D; // 3D 모델 미리보기 (선택사항)

    [Header("캐릭터 정보")]
    [SerializeField] private TextMeshProUGUI characterNameText;
    [SerializeField] private TextMeshProUGUI characterTypeText;
    [SerializeField] private TextMeshProUGUI characterDescriptionText;
    [SerializeField] private TextMeshProUGUI characterStatsText;

    [Header("버튼")]
    [SerializeField] private Button selectCharacterButton;
    [SerializeField] private Button backButton;

    [Header("캐릭터 데이터")]
    [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

    private CharacterData selectedCharacter;

    void Start()
    {
        InitializeCharacterSelection();
        SetupButtons();
        CreateCharacterList();
    }

    /// <summary>
    /// 캐릭터 선택 초기화
    /// </summary>
    private void InitializeCharacterSelection()
    {
        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(true);

        // 기본 캐릭터 데이터 생성
        if (characters.Count == 0)
        {
            CreateDefaultCharacters();
        }
    }

    /// <summary>
    /// 기본 캐릭터 생성
    /// </summary>
    private void CreateDefaultCharacters()
    {
        // 근거리 전사
        characters.Add(new CharacterData
        {
            characterName = "전사",
            characterType = CharacterType.Melee,
            description = "강력한 근접 공격으로 적을 제압하는 전사입니다.",
            baseHealth = 150,
            baseAttack = 25,
            baseDefense = 15,
            baseSpeed = 4f,
            attackRange = 1.5f,
            attackSpeed = 1.2f,
            previewSprite = null // 에디터에서 설정
        });

        // 원거리 궁수
        characters.Add(new CharacterData
        {
            characterName = "궁수",
            characterType = CharacterType.Ranged,
            description = "빠른 원거리 공격으로 적을 저격하는 궁수입니다.",
            baseHealth = 100,
            baseAttack = 20,
            baseDefense = 8,
            baseSpeed = 5.5f,
            attackRange = 10f,
            attackSpeed = 0.5f,
            previewSprite = null
        });

        // 근거리 기사
        characters.Add(new CharacterData
        {
            characterName = "기사",
            characterType = CharacterType.Melee,
            description = "높은 방어력으로 아군을 보호하는 기사입니다.",
            baseHealth = 180,
            baseAttack = 20,
            baseDefense = 25,
            baseSpeed = 3.5f,
            attackRange = 2f,
            attackSpeed = 1.5f,
            previewSprite = null
        });

        // 원거리 마법사
        characters.Add(new CharacterData
        {
            characterName = "마법사",
            characterType = CharacterType.Ranged,
            description = "강력한 마법으로 광역 피해를 입히는 마법사입니다.",
            baseHealth = 80,
            baseAttack = 30,
            baseDefense = 5,
            baseSpeed = 4.5f,
            attackRange = 12f,
            attackSpeed = 0.8f,
            previewSprite = null
        });
    }

    /// <summary>
    /// 버튼 설정
    /// </summary>
    private void SetupButtons()
    {
        if (selectCharacterButton != null)
        {
            selectCharacterButton.onClick.AddListener(OnSelectCharacterClicked);
            selectCharacterButton.interactable = false;
        }

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    /// <summary>
    /// 캐릭터 목록 생성
    /// </summary>
    private void CreateCharacterList()
    {
        Debug.Log("[CharacterSelection] CreateCharacterList 시작");

        if (characterListContainer == null)
        {
            Debug.LogError("[CharacterSelection] characterListContainer가 null입니다!");
            return;
        }

        if (characterButtonPrefab == null)
        {
            Debug.LogError("[CharacterSelection] characterButtonPrefab이 null입니다!");
            return;
        }

        Debug.Log($"[CharacterSelection] 캐릭터 수: {characters.Count}");

        // 캐릭터 데이터 확인
        for (int i = 0; i < characters.Count; i++)
        {
            Debug.Log($"[CharacterSelection] 캐릭터[{i}]: 이름={characters[i].characterName}, 타입={characters[i].characterType}");
        }

        // 기존 버튼 제거
        foreach (Transform child in characterListContainer)
        {
            Destroy(child.gameObject);
        }

        // 캐릭터 버튼 생성
        for (int i = 0; i < characters.Count; i++)
        {
            int index = i;
            CharacterData character = characters[index];

            Debug.Log($"[CharacterSelection] === 버튼 생성 시작: [{index}] {character.characterName} ===");

            GameObject buttonObj = Instantiate(characterButtonPrefab, characterListContainer);
            buttonObj.name = $"CharacterButton_{character.characterName}"; // 이름 설정

            Debug.Log($"[CharacterSelection] 버튼 오브젝트 생성: {buttonObj.name}");

            CharacterButton charButton = buttonObj.GetComponent<CharacterButton>();
            Debug.Log($"[CharacterSelection] CharacterButton 컴포넌트: {charButton != null}");

            if (charButton != null)
            {
                charButton.SetupButton(character, () => {
                    Debug.Log($"[CharacterSelection] ★★★ 콜백 실행: {character.characterName} (타입: {character.characterType}) ★★★");
                    OnCharacterSelected(character);
                });
            }
        }

        Debug.Log($"[CharacterSelection] CreateCharacterList 완료. 생성된 버튼 수: {characterListContainer.childCount}");
    }

    /// <summary>
    /// 캐릭터 선택
    /// </summary>
    private void OnCharacterSelected(CharacterData character)
    {
        Debug.Log($"[CharacterSelection] 캐릭터 선택됨: {character.characterName}");
        // ★ 캐릭터 선택 효과음
        SoundManager.Instance?.PlayCharacterSelect();
        selectedCharacter = character;

        // InfoPanel 활성화 (혹시 꺼져있을 수 있음)
        if (characterNameText != null && characterNameText.transform.parent != null)
        {
            GameObject infoPanel = characterNameText.transform.parent.gameObject;
            if (!infoPanel.activeSelf)
            {
                Debug.Log("[CharacterSelection] InfoPanel 활성화");
                infoPanel.SetActive(true);
            }
        }

        // 캐릭터 정보 표시
        UpdateCharacterInfo(character);

        // 선택 버튼 활성화
        if (selectCharacterButton != null)
        {
            selectCharacterButton.interactable = true;
            Debug.Log("[CharacterSelection] 선택 버튼 활성화됨");
        }
    }
    /// <summary>
    /// 캐릭터 정보 업데이트
    /// </summary>
    private void UpdateCharacterInfo(CharacterData character)
    {
        Debug.Log($"[CharacterSelection] UpdateCharacterInfo 시작: {character.characterName}");

        // 캐릭터 이름
        if (characterNameText != null)
        {
            characterNameText.text = character.characterName;
            Debug.Log($"[CharacterSelection] 이름 설정: {character.characterName}");
        }
        else
        {
            Debug.LogError("[CharacterSelection] characterNameText가 null입니다!");
        }

        // 캐릭터 타입
        if (characterTypeText != null)
        {
            string typeText = character.characterType == CharacterType.Melee ? "근거리" : "원거리";
            Color typeColor = character.characterType == CharacterType.Melee ?
                new Color(1f, 0.5f, 0.5f) : new Color(1f, 0.5f, 1f);

            characterTypeText.text = typeText;
            characterTypeText.color = typeColor;
            Debug.Log($"[CharacterSelection] 타입 설정: {typeText}");
        }
        else
        {
            Debug.LogError("[CharacterSelection] characterTypeText가 null입니다!");
        }

        // 캐릭터 설명
        if (characterDescriptionText != null)
        {
            characterDescriptionText.text = character.description;
            Debug.Log($"[CharacterSelection] 설명 설정: {character.description}");
        }
        else
        {
            Debug.LogError("[CharacterSelection] characterDescriptionText가 null입니다!");
        }

        // 캐릭터 스탯
        if (characterStatsText != null)
        {
            characterStatsText.text =
                $"체력: {character.baseHealth}\n" +
                $"공격력: {character.baseAttack}\n" +
                $"방어력: {character.baseDefense}\n" +
                $"이동속도: {character.baseSpeed}\n" +
                $"공격범위: {character.attackRange}\n" +
                $"공격속도: {character.attackSpeed}";
            Debug.Log($"[CharacterSelection] 스탯 설정 완료");
        }
        else
        {
            Debug.LogError("[CharacterSelection] characterStatsText가 null입니다!");
        }

        // 미리보기 이미지
        if (characterPreviewImage != null && character.previewSprite != null)
        {
            characterPreviewImage.sprite = character.previewSprite;
            characterPreviewImage.gameObject.SetActive(true);
            Debug.Log($"[CharacterSelection] 미리보기 이미지 설정 완료");
        }
        else if (characterPreviewImage != null)
        {
            Debug.LogWarning($"[CharacterSelection] previewSprite가 null입니다: {character.characterName}");
        }

        Debug.Log("[CharacterSelection] UpdateCharacterInfo 완료");
    }

    /// <summary>
    /// 캐릭터 선택 확정
    /// </summary>
    private void OnSelectCharacterClicked()
    {
        // ★ 인게임 입장 효과음
        SoundManager.Instance?.PlayGameStart();
        if (selectedCharacter == null)
            return;

        // 선택한 캐릭터 정보 저장
        PlayerPrefs.SetString("SelectedCharacter", selectedCharacter.characterName);
        PlayerPrefs.SetInt("CharacterType", (int)selectedCharacter.characterType);
        PlayerPrefs.SetFloat("CharacterHealth", selectedCharacter.baseHealth);
        PlayerPrefs.SetFloat("CharacterAttack", selectedCharacter.baseAttack);
        PlayerPrefs.SetFloat("CharacterDefense", selectedCharacter.baseDefense);
        PlayerPrefs.SetFloat("CharacterSpeed", selectedCharacter.baseSpeed);
        PlayerPrefs.SetFloat("CharacterAttackRange", selectedCharacter.attackRange);
        PlayerPrefs.SetFloat("CharacterAttackSpeed", selectedCharacter.attackSpeed);
        PlayerPrefs.Save();

        Debug.Log($"캐릭터 선택: {selectedCharacter.characterName} ({selectedCharacter.characterType})");

        // 게임 시작
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadGameplay();
        }
        else
        {
            // SceneTransitionManager가 없으면 직접 씬 로드
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
        }
    }

    /// <summary>
    /// 뒤로가기
    /// </summary>
    private void OnBackClicked()
    {
        // 캐릭터 선택 패널 숨기기
        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(false);

        // 서버 선택 화면으로 돌아가기
        ServerSelectionSystem serverSystem = FindObjectOfType<ServerSelectionSystem>();
        if (serverSystem != null)
        {
            serverSystem.ShowServerSelectionPanel();
        }
    }
}