using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 메인 메뉴 UI 관리
/// - 새 게임, 이어하기, 설정, 종료
/// - 저장 슬롯 선택
/// - 설정 화면
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("메뉴 패널")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject loadGamePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;
    // ★ 2025-01-30 추가: 로그인/서버/캐릭터 선택 패널 ★
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject serverSelectionPanel;
    [SerializeField] private GameObject characterSelectionPanel;

    [Header("메인 메뉴 버튼")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("저장 슬롯")]
    [SerializeField] private GameObject saveSlotPrefab;
    [SerializeField] private Transform saveSlotContainer;

    [Header("설정")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown qualityDropdown;

    [Header("확인 다이얼로그")]
    [SerializeField] private GameObject confirmDialog;
    [SerializeField] private TextMeshProUGUI confirmDialogText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    private System.Action currentConfirmAction;

    void Start()
    {
        InitializeMainMenu();
        SetupButtons();
        CheckContinueButton();
    }

    /// <summary>
    /// 메인 메뉴 초기화
    /// </summary>
    private void InitializeMainMenu()
    {
        // ★ 2025-01-30 추가: 로그인 패널이 있으면 로그인 화면 먼저 표시 ★
        if (loginPanel != null)
        {
            ShowPanel(loginPanel);

            // 로그인 관련 패널도 숨기기
            if (serverSelectionPanel != null) serverSelectionPanel.SetActive(false);
            if (characterSelectionPanel != null) characterSelectionPanel.SetActive(false);

            LoadSettings();
            return;
        }

        // 모든 패널 숨기기
        ShowPanel(mainMenuPanel);

        if (loadGamePanel != null) loadGamePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (confirmDialog != null) confirmDialog.SetActive(false);

        // 설정 로드
        LoadSettings();
    }

    /// <summary>
    /// 버튼 이벤트 설정
    /// </summary>
    private void SetupButtons()
    {
        // 메인 메뉴 버튼
        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGameClicked);

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);

        if (loadGameButton != null)
            loadGameButton.onClick.AddListener(OnLoadGameClicked);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(OnCreditsClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        // 설정 슬라이더
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChanged);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        // 풀스크린 토글
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);

        // 그래픽 품질
        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);

        // 확인 다이얼로그
        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(OnConfirmYes);

        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(OnConfirmNo);
    }

    #region 메인 메뉴 버튼 이벤트

    /// <summary>
    /// 새 게임 버튼
    /// </summary>
    private void OnNewGameClicked()
    {
        // ★ 새 게임 버튼 효과음
        SoundManager.Instance?.PlayButtonClick();
        ShowConfirmDialog(
    "새 게임을 시작하시겠습니까?\n(저장되지 않은 데이터는 사라집니다)",
    () => StartNewGame()
);
    }

    /// <summary>
    /// 이어하기 버튼 (슬롯 0에서 로드)
    /// </summary>
    private void OnContinueClicked()
    {
        // ★ 이어하기 버튼 효과음
        SoundManager.Instance?.PlayButtonClick();
        if (SaveLoadManager.Instance != null)
        {
            if (SaveLoadManager.Instance.LoadGame(0))
            {
                // 로드 성공 - 게임플레이 씬으로
                if (SceneTransitionManager.Instance != null)
                {
                    SceneTransitionManager.Instance.LoadGameplay();
                }
            }
            else
            {
                Debug.LogWarning("저장 데이터를 불러올 수 없습니다.");
            }
        }
    }

    /// <summary>
    /// 게임 불러오기 버튼
    /// </summary>
    private void OnLoadGameClicked()
    {
        // ★ 불러오기 버튼 효과음
        SoundManager.Instance?.PlayButtonClick();
        ShowPanel(loadGamePanel);
        CreateSaveSlots();
    }

    /// <summary>
    /// 설정 버튼
    /// </summary>
    private void OnSettingsClicked()
    {
        // ★ 설정 버튼 효과음
        SoundManager.Instance?.PlayButtonClick();
        ShowPanel(settingsPanel);
    }

    /// <summary>
    /// 크레딧 버튼
    /// </summary>
    private void OnCreditsClicked()
    {
        // ★ 크레딧 버튼 효과음
        SoundManager.Instance?.PlayButtonClick();
        ShowPanel(creditsPanel);
    }

    /// <summary>
    /// 종료 버튼
    /// </summary>
    private void OnQuitClicked()
    {
        // ★ 종료 버튼 효과음
        SoundManager.Instance?.PlayButtonClick();
        ShowConfirmDialog(
    "게임을 종료하시겠습니까?",
    () => QuitGame()
);
    }

    #endregion

    #region 게임 시작/로드

    /// <summary>
    /// 새 게임 시작
    /// </summary>
    private void StartNewGame()
    {
        // 게임 데이터 초기화
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetGameData();
        }

        // 게임플레이 씬으로 전환
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadGameplay();
        }
    }

    /// <summary>
    /// 저장 슬롯 생성
    /// </summary>
    private void CreateSaveSlots()
    {
        if (SaveLoadManager.Instance == null || saveSlotPrefab == null || saveSlotContainer == null)
            return;

        // 기존 슬롯 제거
        foreach (Transform child in saveSlotContainer)
        {
            Destroy(child.gameObject);
        }

        // 모든 저장 슬롯 가져오기
        SaveSlotInfo[] slots = SaveLoadManager.Instance.GetAllSaveSlots();

        for (int i = 0; i < slots.Length; i++)
        {
            int slotIndex = i; // 클로저 문제 방지
            SaveSlotInfo slotInfo = slots[i];

            // 슬롯 UI 생성
            GameObject slotObj = Instantiate(saveSlotPrefab, saveSlotContainer);
            SaveSlotUI slotUI = slotObj.GetComponent<SaveSlotUI>();

            if (slotUI != null)
            {
                if (slotInfo != null && !slotInfo.IsEmpty)
                {
                    // 저장 데이터가 있는 슬롯
                    slotUI.SetupSlot(
                        slotIndex,
                        slotInfo.saveTime,
                        slotInfo.playerLevel,
                        slotInfo.currentScene,
                        () => LoadFromSlot(slotIndex),
                        () => DeleteSlot(slotIndex)
                    );
                }
                else
                {
                    // 빈 슬롯
                    slotUI.SetupEmptySlot(slotIndex);
                }
            }
        }
    }

    /// <summary>
    /// 특정 슬롯에서 로드
    /// </summary>
    private void LoadFromSlot(int slotIndex)
    {
        if (SaveLoadManager.Instance != null)
        {
            if (SaveLoadManager.Instance.LoadGame(slotIndex))
            {
                if (SceneTransitionManager.Instance != null)
                {
                    SceneTransitionManager.Instance.LoadGameplay();
                }
            }
        }
    }

    /// <summary>
    /// 슬롯 삭제
    /// </summary>
    private void DeleteSlot(int slotIndex)
    {
        ShowConfirmDialog(
            $"슬롯 {slotIndex + 1}을 삭제하시겠습니까?",
            () => {
                if (SaveLoadManager.Instance != null)
                {
                    SaveLoadManager.Instance.DeleteSave(slotIndex);
                    CreateSaveSlots(); // 슬롯 목록 새로고침
                }
            }
        );
    }

    /// <summary>
    /// 이어하기 버튼 활성화 체크
    /// </summary>
    private void CheckContinueButton()
    {
        if (continueButton == null || SaveLoadManager.Instance == null)
            return;

        // 슬롯 0에 저장 데이터가 있는지 확인
        bool hasSaveData = SaveLoadManager.Instance.DoesSaveExist(0);
        continueButton.interactable = hasSaveData;
    }

    #endregion

    #region 설정

    /// <summary>
    /// 설정 로드
    /// </summary>
    private void LoadSettings()
    {
        // PlayerPrefs에서 설정 로드
        if (masterVolumeSlider != null)
            masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);

        if (bgmVolumeSlider != null)
            bgmVolumeSlider.value = PlayerPrefs.GetFloat("BGMVolume", 0.7f);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.8f);

        if (fullscreenToggle != null)
            fullscreenToggle.isOn = Screen.fullScreen;

        if (qualityDropdown != null)
            qualityDropdown.value = QualitySettings.GetQualityLevel();
    }

    /// <summary>
    /// 마스터 볼륨 변경
    /// </summary>
    private void OnMasterVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    /// <summary>
    /// BGM 볼륨 변경
    /// </summary>
    private void OnBGMVolumeChanged(float value)
    {
        // AudioManager가 있으면 BGM 볼륨 설정
        PlayerPrefs.SetFloat("BGMVolume", value);
    }

    /// <summary>
    /// SFX 볼륨 변경
    /// </summary>
    private void OnSFXVolumeChanged(float value)
    {
        // AudioManager가 있으면 SFX 볼륨 설정
        PlayerPrefs.SetFloat("SFXVolume", value);
    }

    /// <summary>
    /// 풀스크린 토글
    /// </summary>
    private void OnFullscreenToggled(bool isOn)
    {
        Screen.fullScreen = isOn;
    }

    /// <summary>
    /// 그래픽 품질 변경
    /// </summary>
    private void OnQualityChanged(int index)
    {
        QualitySettings.SetQualityLevel(index);
        PlayerPrefs.SetInt("GraphicsQuality", index);
    }

    #endregion

    #region UI 패널 관리

    /// <summary>
    /// 패널 표시
    /// </summary>
    private void ShowPanel(GameObject panel)
    {
        // 모든 패널 숨기기
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (loadGamePanel != null) loadGamePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        // ★ 2025-01-30 추가 ★
        if (loginPanel != null) loginPanel.SetActive(false);
        if (serverSelectionPanel != null) serverSelectionPanel.SetActive(false);
        if (characterSelectionPanel != null) characterSelectionPanel.SetActive(false);

        // 선택한 패널만 표시
        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    /// <summary>
    /// 메인 메뉴로 돌아가기
    /// </summary>
    public void BackToMainMenu()
    {
        ShowPanel(mainMenuPanel);
    }

    #endregion

    #region 확인 다이얼로그

    /// <summary>
    /// 확인 다이얼로그 표시
    /// </summary>
    private void ShowConfirmDialog(string message, System.Action onConfirm)
    {
        if (confirmDialog == null) return;

        confirmDialogText.text = message;
        currentConfirmAction = onConfirm;
        confirmDialog.SetActive(true);
    }

    /// <summary>
    /// 확인 버튼
    /// </summary>
    private void OnConfirmYes()
    {
        // ★ 확인 버튼 효과음
        SoundManager.Instance?.PlayButtonClick();
        currentConfirmAction?.Invoke();
        confirmDialog.SetActive(false);
        currentConfirmAction = null;
    }

    /// <summary>
    /// 취소 버튼
    /// </summary>
    private void OnConfirmNo()
    {
        // ★ 취소 버튼 효과음
        SoundManager.Instance?.PlayButtonClick();
        confirmDialog.SetActive(false);
        currentConfirmAction = null;
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// 게임 종료
    /// </summary>
    private void QuitGame()
    {
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.QuitGame();
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    #endregion
}

/// <summary>
/// 저장 슬롯 UI 컴포넌트
/// </summary>
public class SaveSlotUI : MonoBehaviour
{
    [Header("UI 요소")]
    [SerializeField] private TextMeshProUGUI slotNumberText;
    [SerializeField] private TextMeshProUGUI saveTimeText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI sceneText;
    [SerializeField] private GameObject emptySlotPanel;
    [SerializeField] private GameObject dataSlotPanel;

    [Header("버튼")]
    [SerializeField] private Button loadButton;
    [SerializeField] private Button deleteButton;

    private int slotIndex;

    /// <summary>
    /// 데이터가 있는 슬롯 설정
    /// </summary>
    public void SetupSlot(int index, string saveTime, int level, string scene,
                          System.Action onLoad, System.Action onDelete)
    {
        slotIndex = index;

        // UI 표시
        if (emptySlotPanel != null) emptySlotPanel.SetActive(false);
        if (dataSlotPanel != null) dataSlotPanel.SetActive(true);

        // 텍스트 설정
        if (slotNumberText != null)
            slotNumberText.text = $"슬롯 {index + 1}";

        if (saveTimeText != null)
            saveTimeText.text = saveTime;

        if (levelText != null)
            levelText.text = $"Lv. {level}";

        if (sceneText != null)
            sceneText.text = scene;

        // 버튼 이벤트
        if (loadButton != null)
        {
            loadButton.onClick.RemoveAllListeners();
            loadButton.onClick.AddListener(() => onLoad?.Invoke());
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => onDelete?.Invoke());
        }
    }

    /// <summary>
    /// 빈 슬롯 설정
    /// </summary>
    public void SetupEmptySlot(int index)
    {
        slotIndex = index;

        // UI 표시
        if (emptySlotPanel != null) emptySlotPanel.SetActive(true);
        if (dataSlotPanel != null) dataSlotPanel.SetActive(false);

        if (slotNumberText != null)
            slotNumberText.text = $"슬롯 {index + 1} - 비어있음";
    }
}