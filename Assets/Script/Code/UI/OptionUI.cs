using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ============================================================
/// OptionUI - 게임 옵션/설정 패널 관리
/// ============================================================
///
/// 기능 목록
/// - BGM 음량 슬라이더 조절
/// - SFX(효과음) 음량 슬라이더 조절
/// - BGM/SFX 음소거 토글
/// - 뒤로가기(닫기) 버튼
/// - SoundManager와 연동하여 설정 저장/로드
///
/// Unity 설치 방법
/// 1. Canvas 하위에 "OptionPanel" 오브젝트 생성
/// 2. 이 스크립트를 OptionPanel 또는 상위 매니저에 붙이기
/// 3. Inspector에서 각 UI 요소를 연결:
///    - optionPanel: 옵션 패널 최상위 오브젝트
///    - bgmSlider: BGM 음량 조절 Slider
///    - sfxSlider: SFX 음량 조절 Slider
///    - bgmVolumeText: BGM 음량 수치 표시 Text
///    - sfxVolumeText: SFX 음량 수치 표시 Text
///    - bgmMuteToggle: BGM 음소거 Toggle
///    - sfxMuteToggle: SFX 음소거 Toggle
///    - backButton: 뒤로가기(닫기) Button
///
/// 권장 UI 구조 (Hierarchy)
/// Canvas
///   └── OptionPanel (이 스크립트 부착)
///       ├── Background (반투명 배경용 Image)
///       ├── OptionWindow (실제 옵션 창 Image)
///       │   ├── TitleText ("설정" TextMeshPro)
///       │   ├── BGM_Section
///       │   │   ├── BGM_Label ("BGM" Text)
///       │   │   ├── BGM_Slider (Slider)
///       │   │   ├── BGM_VolumeText ("100%" Text)
///       │   │   └── BGM_MuteToggle (Toggle)
///       │   ├── SFX_Section
///       │   │   ├── SFX_Label ("효과음" Text)
///       │   │   ├── SFX_Slider (Slider)
///       │   │   ├── SFX_VolumeText ("100%" Text)
///       │   │   └── SFX_MuteToggle (Toggle)
///       │   └── BackButton ("닫기" Button)
///       └── (선택) CloseAreaButton (빈 곳 클릭 시 닫기용 버튼)
/// ============================================================
/// </summary>
public class OptionUI : MonoBehaviour
{
    // ===== 싱글톤 =====
    public static OptionUI Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // [Inspector에서 연결할 UI 요소들]
    // ─────────────────────────────────────────────────────────────────────────

    [Header("===== 옵션 패널 =====")]
    [Tooltip("옵션 패널의 최상위 루트 오브젝트")]
    [SerializeField] private GameObject optionPanel;

    [Header("===== BGM (배경음악) 설정 =====")]
    [Tooltip("BGM 음량 조절 슬라이더 (0~1)")]
    [SerializeField] private Slider bgmSlider;

    [Tooltip("BGM 음량 수치 표시 텍스트 (예: '75%')")]
    [SerializeField] private TextMeshProUGUI bgmVolumeText;

    [Tooltip("BGM 음소거 토글 버튼")]
    [SerializeField] private Toggle bgmMuteToggle;

    [Header("===== SFX (효과음) 설정 =====")]
    [Tooltip("SFX 음량 조절 슬라이더 (0~1)")]
    [SerializeField] private Slider sfxSlider;

    [Tooltip("SFX 음량 수치 표시 텍스트 (예: '100%')")]
    [SerializeField] private TextMeshProUGUI sfxVolumeText;

    [Tooltip("SFX 음소거 토글 버튼")]
    [SerializeField] private Toggle sfxMuteToggle;

    [Header("===== 버튼 =====")]
    [Tooltip("뒤로가기(닫기) 버튼")]
    [SerializeField] private Button backButton;

    [Tooltip("(선택) 배경 클릭 시 닫기용 버튼")]
    [SerializeField] private Button backgroundCloseButton;

    [Header("===== 로그아웃 =====")]
    [Tooltip("로그아웃 버튼")]
    [SerializeField] private Button logoutButton;

    [Header("===== 애니메이션 설정 =====")]
    [Tooltip("패널 열기/닫기 애니메이션 사용 여부")]
    [SerializeField] private bool useAnimation = true;

    [Tooltip("애니메이션 속도")]
    [SerializeField] private float animationSpeed = 8f;

    // ─────────────────────────────────────────────────────────────────────────
    // [내부 상태]
    // ─────────────────────────────────────────────────────────────────────────
    private bool isAnimating = false;   // 현재 애니메이션 진행중
    private CanvasGroup canvasGroup;    // 페이드 애니메이션용

    // ==========================================================
    //  Unity 라이프사이클
    // ==========================================================

    void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // UI 이벤트 연결
        SetupUIEvents();

        // CanvasGroup 확인 (페이드 애니메이션용)
        if (optionPanel != null)
        {
            canvasGroup = optionPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = optionPanel.AddComponent<CanvasGroup>();
            }
        }

        // 시작 시 옵션 패널 숨김
        if (optionPanel != null)
        {
            optionPanel.SetActive(false);
        }
    }

    // ==========================================================
    //  UI 이벤트 연결
    // ==========================================================

    /// <summary>
    /// 모든 UI 요소에 이벤트 리스너를 연결
    /// 슬라이더 값 변경, 토글 변경, 버튼 클릭 등
    /// </summary>
    private void SetupUIEvents()
    {
        // 연결 BGM 슬라이더 이벤트 연결
        if (bgmSlider != null)
        {
            // 슬라이더 범위 설정 (0 = 무음, 1 = 최대)
            bgmSlider.minValue = 0f;
            bgmSlider.maxValue = 1f;

            // 슬라이더 값이 변하면 OnBGMVolumeChanged 호출
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        // 연결 SFX 슬라이더 이벤트 연결
        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        // 연결 BGM 음소거 토글 이벤트 연결
        if (bgmMuteToggle != null)
        {
            bgmMuteToggle.onValueChanged.AddListener(OnBGMMuteChanged);
        }

        // 연결 SFX 음소거 토글 이벤트 연결
        if (sfxMuteToggle != null)
        {
            sfxMuteToggle.onValueChanged.AddListener(OnSFXMuteChanged);
        }

        // 연결 뒤로가기 버튼 이벤트 연결
        if (backButton != null)
        {
            backButton.onClick.AddListener(CloseOptionPanel);
        }

        // 연결 배경 클릭 닫기 (선택사항) 연결
        if (backgroundCloseButton != null)
        {
            backgroundCloseButton.onClick.AddListener(CloseOptionPanel);
        }

        // 로그아웃 버튼 이벤트 연결
        if (logoutButton != null)
        {
            logoutButton.onClick.AddListener(OnLogoutButtonClicked);
        }
    }

    // ==========================================================
    //  옵션 패널 열기/닫기
    // ==========================================================

    /// <summary>
    /// 옵션 패널 열기
    /// SoundManager에서 현재 설정값을 읽어와 UI에 반영
    /// </summary>
    public void OpenOptionPanel()
    {
        if (optionPanel == null) return;

        // 패널 활성화
        optionPanel.SetActive(true);

        // SoundManager에서 현재 설정값을 가져와서 UI에 반영
        LoadCurrentSettings();

        // 패널 열기 효과음
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayPanelOpen();
        }

        // 페이드인 애니메이션
        if (useAnimation && canvasGroup != null)
        {
            StartCoroutine(FadeIn());
        }

        Debug.Log("[OptionUI] 옵션 패널 열림");
    }

    /// <summary>
    /// 옵션 패널 닫기 (뒤로가기 버튼)
    /// </summary>
    public void CloseOptionPanel()
    {
        if (optionPanel == null) return;

        // 패널 닫기 효과음
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayPanelClose();
        }

        // 페이드아웃 애니메이션
        if (useAnimation && canvasGroup != null)
        {
            StartCoroutine(FadeOut());
        }
        else
        {
            optionPanel.SetActive(false);
        }

        Debug.Log("[OptionUI] 옵션 패널 닫힘");
    }

    /// <summary>
    /// 옵션 패널 토글 (열려있으면 닫고, 닫혀있으면 열기)
    /// TopMenuManager의 설정 버튼에서 호출
    /// </summary>
    public void ToggleOptionPanel()
    {
        if (optionPanel == null) return;

        if (optionPanel.activeSelf)
        {
            CloseOptionPanel();
        }
        else
        {
            OpenOptionPanel();
        }
    }

    // ==========================================================
    //  SoundManager 와 UI 동기화
    // ==========================================================

    /// <summary>
    /// SoundManager의 현재 설정값을 UI에 반영
    /// 옵션 패널을 열 때 호출됨
    /// </summary>
    private void LoadCurrentSettings()
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogWarning("[OptionUI] SoundManager가 없습니다!");
            return;
        }

        // 연결 BGM 슬라이더에 현재 설정값 반영 시작
        if (bgmSlider != null)
        {
            // 이벤트 임시 제거 후 값 설정 뒤 이벤트 재연결
            // (값 설정 중 이벤트가 발생하는 것을 방지)
            bgmSlider.onValueChanged.RemoveListener(OnBGMVolumeChanged);
            bgmSlider.value = SoundManager.Instance.GetBGMVolume();
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        // 연결 SFX 슬라이더에 현재 설정값 반영 시작
        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
            sfxSlider.value = SoundManager.Instance.GetSFXVolume();
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        // 연결 음소거 토글 상태 반영 시작
        if (bgmMuteToggle != null)
        {
            bgmMuteToggle.onValueChanged.RemoveListener(OnBGMMuteChanged);
            bgmMuteToggle.isOn = !SoundManager.Instance.IsBGMMuted(); // 토글 ON = 소리 켜짐
            bgmMuteToggle.onValueChanged.AddListener(OnBGMMuteChanged);
        }

        if (sfxMuteToggle != null)
        {
            sfxMuteToggle.onValueChanged.RemoveListener(OnSFXMuteChanged);
            sfxMuteToggle.isOn = !SoundManager.Instance.IsSFXMuted();
            sfxMuteToggle.onValueChanged.AddListener(OnSFXMuteChanged);
        }

        // 연결 음량 텍스트 초기 갱신
        UpdateVolumeTexts();
    }

    // ==========================================================
    //  UI 이벤트 핸들러 (슬라이더/토글 변경 시 호출됨)
    // ==========================================================

    /// <summary>
    /// BGM 슬라이더 값 변경 시 호출
    /// </summary>
    /// <param name="value">새 음량 값 (0~1)</param>
    private void OnBGMVolumeChanged(float value)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetBGMVolume(value);
        }

        // 음량 텍스트 갱신 (예: "75%")
        UpdateBGMVolumeText(value);
    }

    /// <summary>
    /// SFX 슬라이더 값 변경 시 호출
    /// </summary>
    /// <param name="value">새 음량 값 (0~1)</param>
    private void OnSFXVolumeChanged(float value)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetSFXVolume(value);
        }

        UpdateSFXVolumeText(value);
    }

    /// <summary>
    /// BGM 음소거 토글 변경 시 호출
    /// </summary>
    /// <param name="isOn">true = 소리 켜짐, false = 음소거</param>
    private void OnBGMMuteChanged(bool isOn)
    {
        if (SoundManager.Instance != null)
        {
            // 토글 ON = 소리 켜짐 = mute false
            SoundManager.Instance.SetBGMMute(!isOn);
        }

        // 음소거 시 슬라이더 비활성화 (시각적 피드백)
        if (bgmSlider != null)
        {
            bgmSlider.interactable = isOn;
        }
    }

    /// <summary>
    /// SFX 음소거 토글 변경 시 호출
    /// </summary>
    /// <param name="isOn">true = 소리 켜짐, false = 음소거</param>
    private void OnSFXMuteChanged(bool isOn)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetSFXMute(!isOn);
        }

        if (sfxSlider != null)
        {
            sfxSlider.interactable = isOn;
        }
    }

    // ==========================================================
    //  음량 텍스트 갱신
    // ==========================================================

    /// <summary>
    /// BGM + SFX 음량 텍스트 모두 갱신
    /// </summary>
    private void UpdateVolumeTexts()
    {
        if (bgmSlider != null) UpdateBGMVolumeText(bgmSlider.value);
        if (sfxSlider != null) UpdateSFXVolumeText(sfxSlider.value);
    }

    /// <summary>
    /// BGM 음량 텍스트 갱신 (예: "75%")
    /// </summary>
    private void UpdateBGMVolumeText(float value)
    {
        if (bgmVolumeText != null)
        {
            int percentage = Mathf.RoundToInt(value * 100f);
            bgmVolumeText.text = $"{percentage}%";
        }
    }

    /// <summary>
    /// SFX 음량 텍스트 갱신
    /// </summary>
    private void UpdateSFXVolumeText(float value)
    {
        if (sfxVolumeText != null)
        {
            int percentage = Mathf.RoundToInt(value * 100f);
            sfxVolumeText.text = $"{percentage}%";
        }
    }

    // ==========================================================
    //  페이드 애니메이션
    // ==========================================================

    /// <summary>
    /// 페이드인 (서서히 나타남) 애니메이션
    /// </summary>
    private System.Collections.IEnumerator FadeIn()
    {
        isAnimating = true;
        canvasGroup.alpha = 0f;

        while (canvasGroup.alpha < 1f)
        {
            canvasGroup.alpha += Time.unscaledDeltaTime * animationSpeed;
            yield return null;
        }

        canvasGroup.alpha = 1f;
        isAnimating = false;
    }

    /// <summary>
    /// 페이드아웃 (서서히 사라짐) 애니메이션
    /// 완료 후 패널 비활성화
    /// </summary>
    private System.Collections.IEnumerator FadeOut()
    {
        isAnimating = true;

        while (canvasGroup.alpha > 0f)
        {
            canvasGroup.alpha -= Time.unscaledDeltaTime * animationSpeed;
            yield return null;
        }

        canvasGroup.alpha = 0f;
        optionPanel.SetActive(false);
        isAnimating = false;
    }

    // ==========================================================
    //  로그아웃
    // ==========================================================

    /// <summary>로그아웃 버튼 클릭 시 확인 다이얼로그 표시</summary>
    private void OnLogoutButtonClicked()
    {
        SoundManager.Instance?.PlayButtonClick();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowConfirmDialog(
                "로그아웃 하시겠습니까?\n현재 데이터가 저장됩니다.",
                OnLogoutConfirmed
            );
        }
        else
        {
            // UIManager 없으면 바로 실행
            OnLogoutConfirmed();
        }
    }

    /// <summary>로그아웃 확인 → 저장 → 로그아웃 → 로그인 씬 이동</summary>
    private void OnLogoutConfirmed()
    {
        // 1. 현재 데이터 저장
        SaveLoadManager.Instance?.SaveGame();

        // 2. 뒤끝 로그아웃
        if (BackendManager.Instance != null)
        {
            BackendManager.Instance.Logout();
        }

        // 3. 옵션 패널 닫기
        CloseOptionPanel();

        // 4. 로그인 씬으로 이동
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadScene("LoginScene");
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene");
        }

        Debug.Log("[OptionUI] 로그아웃 → LoginScene 이동");
    }
}
