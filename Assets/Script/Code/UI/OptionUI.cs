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
///    - 옵션패널: 옵션 패널 최상위 오브젝트
///    - 배경음악슬라이더: BGM 음량 조절 Slider
///    - 효과음슬라이더: SFX 음량 조절 Slider
///    - 배경음악볼륨텍스트: BGM 음량 수치 표시 Text
///    - 효과음볼륨텍스트: SFX 음량 수치 표시 Text
///    - 배경음악음소거: BGM 음소거 Toggle
///    - 효과음음소거: SFX 음소거 Toggle
///    - 닫기버튼: 뒤로가기(닫기) Button
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
    [SerializeField] private GameObject 옵션패널;

    [Header("===== 배경음악 설정 =====")]
    [Tooltip("배경음악 음량 슬라이더")]
    [SerializeField] private Slider 배경음악슬라이더;

    [Tooltip("배경음악 음량 텍스트")]
    [SerializeField] private TextMeshProUGUI 배경음악볼륨텍스트;

    [Tooltip("배경음악 음소거 토글")]
    [SerializeField] private Toggle 배경음악음소거;

    [Header("===== 효과음 설정 =====")]
    [Tooltip("효과음 음량 슬라이더")]
    [SerializeField] private Slider 효과음슬라이더;

    [Tooltip("효과음 음량 텍스트")]
    [SerializeField] private TextMeshProUGUI 효과음볼륨텍스트;

    [Tooltip("효과음 음소거 토글")]
    [SerializeField] private Toggle 효과음음소거;

    [Header("===== 기본 버튼 =====")]
    [Tooltip("닫기 버튼")]
    [SerializeField] private Button 닫기버튼;

    [Tooltip("배경 클릭 닫기 버튼 (선택)")]
    [SerializeField] private Button 배경닫기버튼;

    [Header("===== 게임 설정 =====")]
    [Tooltip("데미지 팝업 표시 ON/OFF")]
    [SerializeField] private Toggle 데미지팝업토글;

    [Tooltip("데미지 특수효과 ON/OFF")]
    [SerializeField] private Toggle 데미지효과토글;

    [Tooltip("데미지 소수점 자릿수 (정수/1자리/2자리)")]
    [SerializeField] private TMP_Dropdown 소수점드롭다운;

    [Tooltip("프레임 수 선택 (30/60/120)")]
    [SerializeField] private TMP_Dropdown 프레임드롭다운;

    [Tooltip("해상도 선택 (낮음/중/고)")]
    [SerializeField] private TMP_Dropdown 해상도드롭다운;

    [Header("===== 로그아웃 / 종료 =====")]
    [Tooltip("로그아웃 버튼")]
    [SerializeField] private Button 로그아웃버튼;

    [Tooltip("앱 종료 버튼")]
    [SerializeField] private Button 종료버튼;

    [Header("===== 애니메이션 설정 =====")]
    [Tooltip("패널 열기/닫기 애니메이션 사용")]
    [SerializeField] private bool 애니메이션사용 = true;

    [Tooltip("애니메이션 속도")]
    [SerializeField] private float 애니메이션속도 = 8f;

    // ─────────────────────────────────────────────────────────────────────────
    // [게임 설정 — 전역 접근용 static]
    // ─────────────────────────────────────────────────────────────────────────
    public static bool DamagePopupEnabled { get; private set; } = true;
    public static bool DamageEffectEnabled { get; private set; } = true;
    public static int DamageDecimalPlaces { get; private set; } = 0;  // 0=정수
    public static int TargetFrameRate { get; private set; } = 60;
    public static int ResolutionLevel { get; private set; } = 2;       // 0=낮음, 1=중, 2=고

    private static readonly int[] frameRateOptions = { 30, 60, 120 };
    private static readonly float[] resolutionScales = { 0.5f, 0.75f, 1.0f };

    // ─────────────────────────────────────────────────────────────────────────
    // [내부 상태]
    // ─────────────────────────────────────────────────────────────────────────
    private bool isAnimating = false;
    private CanvasGroup canvasGroup;

    // ==========================================================
    //  Unity 라이프사이클
    // ==========================================================

    void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] OptionUI가 생성되었습니다.");
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // ★ 에디터에서 꺼놓아도 런타임에 초기화되도록
        // 이 오브젝트가 비활성이었다가 누군가 활성화해준 경우 대응
        if (옵션패널 != null)
            옵션패널.SetActive(false);
    }

    void Start()
    {
        // UI 이벤트 연결
        SetupUIEvents();

        // CanvasGroup 확인 (페이드 애니메이션용)
        if (옵션패널 != null)
        {
            canvasGroup = 옵션패널.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = 옵션패널.AddComponent<CanvasGroup>();
            }
        }

        // 게임 설정 로드 (PlayerPrefs)
        LoadGameSettings();

        // 시작 시 옵션 패널 숨김
        if (옵션패널 != null)
        {
            옵션패널.SetActive(false);
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
        if (배경음악슬라이더 != null)
        {
            // 슬라이더 범위 설정 (0 = 무음, 1 = 최대)
            배경음악슬라이더.minValue = 0f;
            배경음악슬라이더.maxValue = 1f;

            // 슬라이더 값이 변하면 OnBGMVolumeChanged 호출
            배경음악슬라이더.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        // 연결 SFX 슬라이더 이벤트 연결
        if (효과음슬라이더 != null)
        {
            효과음슬라이더.minValue = 0f;
            효과음슬라이더.maxValue = 1f;
            효과음슬라이더.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        // 연결 BGM 음소거 토글 이벤트 연결
        if (배경음악음소거 != null)
        {
            배경음악음소거.onValueChanged.AddListener(OnBGMMuteChanged);
        }

        // 연결 SFX 음소거 토글 이벤트 연결
        if (효과음음소거 != null)
        {
            효과음음소거.onValueChanged.AddListener(OnSFXMuteChanged);
        }

        // 연결 뒤로가기 버튼 이벤트 연결
        if (닫기버튼 != null)
        {
            닫기버튼.onClick.AddListener(CloseOptionPanel);
        }

        // 연결 배경 클릭 닫기 (선택사항) 연결
        if (배경닫기버튼 != null)
        {
            배경닫기버튼.onClick.AddListener(CloseOptionPanel);
        }

        // 로그아웃 버튼 이벤트 연결
        if (로그아웃버튼 != null)
            로그아웃버튼.onClick.AddListener(OnLogoutButtonClicked);

        // 앱 종료 버튼
        if (종료버튼 != null)
            종료버튼.onClick.AddListener(OnQuitButtonClicked);

        // 데미지 팝업 토글
        if (데미지팝업토글 != null)
            데미지팝업토글.onValueChanged.AddListener(OnDamagePopupChanged);

        // 데미지 특수효과 토글
        if (데미지효과토글 != null)
            데미지효과토글.onValueChanged.AddListener(OnDamageEffectChanged);

        // 소수점 자릿수 드롭다운
        if (소수점드롭다운 != null)
        {
            소수점드롭다운.ClearOptions();
            소수점드롭다운.AddOptions(new System.Collections.Generic.List<string> { "정수", "소수점 1자리", "소수점 2자리" });
            소수점드롭다운.onValueChanged.AddListener(OnDecimalChanged);
        }

        // 프레임 수 드롭다운
        if (프레임드롭다운 != null)
        {
            프레임드롭다운.ClearOptions();
            프레임드롭다운.AddOptions(new System.Collections.Generic.List<string> { "30 FPS", "60 FPS", "120 FPS" });
            프레임드롭다운.onValueChanged.AddListener(OnFrameRateChanged);
        }

        // 해상도 드롭다운
        if (해상도드롭다운 != null)
        {
            해상도드롭다운.ClearOptions();
            해상도드롭다운.AddOptions(new System.Collections.Generic.List<string> { "낮음", "중", "고" });
            해상도드롭다운.onValueChanged.AddListener(OnResolutionChanged);
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
        if (옵션패널 == null) return;

        // 패널 활성화
        옵션패널.SetActive(true);

        // SoundManager에서 현재 설정값을 가져와서 UI에 반영
        LoadCurrentSettings();

        // 게임 설정 UI 동기화
        LoadGameSettingsToUI();

        // 패널 열기 효과음
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayPanelOpen();
        }

        // 페이드인 애니메이션
        if (애니메이션사용 && canvasGroup != null)
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
        if (옵션패널 == null) return;

        // 패널 닫기 효과음
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayPanelClose();
        }

        // 페이드아웃 애니메이션
        if (애니메이션사용 && canvasGroup != null)
        {
            StartCoroutine(FadeOut());
        }
        else
        {
            옵션패널.SetActive(false);
        }

        TopMenuManager.Instance?.ClearBanner();
        Debug.Log("[OptionUI] 옵션 패널 닫힘");
    }

    /// <summary>
    /// 옵션 패널 토글 (열려있으면 닫고, 닫혀있으면 열기)
    /// TopMenuManager의 설정 버튼에서 호출
    /// </summary>
    public void ToggleOptionPanel()
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.ShouldBlockNonFocusButtons) return;
        if (옵션패널 == null) return;

        if (옵션패널.activeSelf)
        {
            CloseOptionPanel();
        }
        else
        {
            OpenOptionPanel();
        }
    }

    public void ShowOptionPanel() => OpenOptionPanel();
    public void HideOptionPanel() => CloseOptionPanel();

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
        if (배경음악슬라이더 != null)
        {
            // 이벤트 임시 제거 후 값 설정 뒤 이벤트 재연결
            // (값 설정 중 이벤트가 발생하는 것을 방지)
            배경음악슬라이더.onValueChanged.RemoveListener(OnBGMVolumeChanged);
            배경음악슬라이더.value = SoundManager.Instance.GetBGMVolume();
            배경음악슬라이더.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        // 연결 SFX 슬라이더에 현재 설정값 반영 시작
        if (효과음슬라이더 != null)
        {
            효과음슬라이더.onValueChanged.RemoveListener(OnSFXVolumeChanged);
            효과음슬라이더.value = SoundManager.Instance.GetSFXVolume();
            효과음슬라이더.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        // 연결 음소거 토글 상태 반영 시작
        if (배경음악음소거 != null)
        {
            배경음악음소거.onValueChanged.RemoveListener(OnBGMMuteChanged);
            배경음악음소거.isOn = !SoundManager.Instance.IsBGMMuted(); // 토글 ON = 소리 켜짐
            배경음악음소거.onValueChanged.AddListener(OnBGMMuteChanged);
        }

        if (효과음음소거 != null)
        {
            효과음음소거.onValueChanged.RemoveListener(OnSFXMuteChanged);
            효과음음소거.isOn = !SoundManager.Instance.IsSFXMuted();
            효과음음소거.onValueChanged.AddListener(OnSFXMuteChanged);
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
        if (배경음악슬라이더 != null)
        {
            배경음악슬라이더.interactable = isOn;
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

        if (효과음슬라이더 != null)
        {
            효과음슬라이더.interactable = isOn;
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
        if (배경음악슬라이더 != null) UpdateBGMVolumeText(배경음악슬라이더.value);
        if (효과음슬라이더 != null) UpdateSFXVolumeText(효과음슬라이더.value);
    }

    /// <summary>
    /// BGM 음량 텍스트 갱신 (예: "75%")
    /// </summary>
    private void UpdateBGMVolumeText(float value)
    {
        if (배경음악볼륨텍스트 != null)
        {
            int percentage = Mathf.RoundToInt(value * 100f);
            배경음악볼륨텍스트.text = $"{percentage}%";
        }
    }

    /// <summary>
    /// SFX 음량 텍스트 갱신
    /// </summary>
    private void UpdateSFXVolumeText(float value)
    {
        if (효과음볼륨텍스트 != null)
        {
            int percentage = Mathf.RoundToInt(value * 100f);
            효과음볼륨텍스트.text = $"{percentage}%";
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
            canvasGroup.alpha += Time.unscaledDeltaTime * 애니메이션속도;
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
            canvasGroup.alpha -= Time.unscaledDeltaTime * 애니메이션속도;
            yield return null;
        }

        canvasGroup.alpha = 0f;
        옵션패널.SetActive(false);
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

    // ==========================================================
    //  게임 설정 핸들러
    // ==========================================================

    private void OnDamagePopupChanged(bool isOn)
    {
        DamagePopupEnabled = isOn;
        PlayerPrefs.SetInt("opt_damagePopup", isOn ? 1 : 0);
        Debug.Log($"[OptionUI] 데미지 팝업: {(isOn ? "ON" : "OFF")}");
    }

    private void OnDamageEffectChanged(bool isOn)
    {
        DamageEffectEnabled = isOn;
        PlayerPrefs.SetInt("opt_damageEffect", isOn ? 1 : 0);
        Debug.Log($"[OptionUI] 데미지 특수효과: {(isOn ? "ON" : "OFF")}");
    }

    private void OnDecimalChanged(int index)
    {
        DamageDecimalPlaces = index; // 0=정수, 1=소수점1, 2=소수점2
        PlayerPrefs.SetInt("opt_decimal", index);
        Debug.Log($"[OptionUI] 소수점 자릿수: {index}");
    }

    private void OnFrameRateChanged(int index)
    {
        if (index < 0 || index >= frameRateOptions.Length) return;
        TargetFrameRate = frameRateOptions[index];
        Application.targetFrameRate = TargetFrameRate;
        PlayerPrefs.SetInt("opt_frameRate", index);
        Debug.Log($"[OptionUI] 프레임: {TargetFrameRate} FPS");
    }

    private void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= resolutionScales.Length) return;
        ResolutionLevel = index;
        float scale = resolutionScales[index];
        int w = (int)(Screen.currentResolution.width * scale);
        int h = (int)(Screen.currentResolution.height * scale);
        Screen.SetResolution(w, h, true);
        PlayerPrefs.SetInt("opt_resolution", index);
        Debug.Log($"[OptionUI] 해상도: {(index == 0 ? "낮음" : index == 1 ? "중" : "고")} ({w}x{h})");
    }

    // ==========================================================
    //  앱 종료
    // ==========================================================

    private void OnQuitButtonClicked()
    {
        SoundManager.Instance?.PlayButtonClick();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowConfirmDialog(
                "게임을 종료하시겠습니까?\n현재 데이터가 저장됩니다.",
                OnQuitConfirmed
            );
        }
        else
        {
            OnQuitConfirmed();
        }
    }

    private void OnQuitConfirmed()
    {
        SaveLoadManager.Instance?.SaveGame();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ==========================================================
    //  게임 설정 저장/로드 (PlayerPrefs)
    // ==========================================================

    private void LoadGameSettings()
    {
        DamagePopupEnabled = PlayerPrefs.GetInt("opt_damagePopup", 1) == 1;
        DamageEffectEnabled = PlayerPrefs.GetInt("opt_damageEffect", 1) == 1;
        DamageDecimalPlaces = PlayerPrefs.GetInt("opt_decimal", 0);

        int frameIdx = PlayerPrefs.GetInt("opt_frameRate", 1); // 기본 60FPS
        if (frameIdx >= 0 && frameIdx < frameRateOptions.Length)
        {
            TargetFrameRate = frameRateOptions[frameIdx];
            Application.targetFrameRate = TargetFrameRate;
        }

        int resIdx = PlayerPrefs.GetInt("opt_resolution", 2); // 기본 고
        ResolutionLevel = Mathf.Clamp(resIdx, 0, 2);
    }

    private void LoadGameSettingsToUI()
    {
        // 토글
        if (데미지팝업토글 != null)
        {
            데미지팝업토글.onValueChanged.RemoveListener(OnDamagePopupChanged);
            데미지팝업토글.isOn = DamagePopupEnabled;
            데미지팝업토글.onValueChanged.AddListener(OnDamagePopupChanged);
        }

        if (데미지효과토글 != null)
        {
            데미지효과토글.onValueChanged.RemoveListener(OnDamageEffectChanged);
            데미지효과토글.isOn = DamageEffectEnabled;
            데미지효과토글.onValueChanged.AddListener(OnDamageEffectChanged);
        }

        // 드롭다운
        if (소수점드롭다운 != null)
        {
            소수점드롭다운.onValueChanged.RemoveListener(OnDecimalChanged);
            소수점드롭다운.value = DamageDecimalPlaces;
            소수점드롭다운.onValueChanged.AddListener(OnDecimalChanged);
        }

        if (프레임드롭다운 != null)
        {
            프레임드롭다운.onValueChanged.RemoveListener(OnFrameRateChanged);
            int idx = System.Array.IndexOf(frameRateOptions, TargetFrameRate);
            프레임드롭다운.value = idx >= 0 ? idx : 1;
            프레임드롭다운.onValueChanged.AddListener(OnFrameRateChanged);
        }

        if (해상도드롭다운 != null)
        {
            해상도드롭다운.onValueChanged.RemoveListener(OnResolutionChanged);
            해상도드롭다운.value = ResolutionLevel;
            해상도드롭다운.onValueChanged.AddListener(OnResolutionChanged);
        }
    }

    /// <summary>데미지 표시용 포맷 문자열 (다른 스크립트에서 사용)</summary>
    public static string FormatDamage(float damage)
    {
        return DamageDecimalPlaces switch
        {
            1 => damage.ToString("F1"),
            2 => damage.ToString("F2"),
            _ => ((int)damage).ToString()
        };
    }
}
