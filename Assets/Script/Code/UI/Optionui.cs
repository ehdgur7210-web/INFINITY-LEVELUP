using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ============================================================
/// OptionUI - 게임 옵션/설정 패널 관리
/// ============================================================
/// 
/// 【역할】
/// - BGM 볼륨 슬라이더 조절
/// - SFX(효과음) 볼륨 슬라이더 조절
/// - BGM/SFX 음소거 토글
/// - 뒤로가기(닫기) 버튼
/// - SoundManager와 연동하여 설정 저장/로드
/// 
/// 【Unity 설정 방법】
/// 1. Canvas 아래에 "OptionPanel" 오브젝트 생성
/// 2. 이 스크립트를 OptionPanel 또는 별도 매니저에 붙이기
/// 3. Inspector에서 각 UI 요소를 연결:
///    - optionPanel: 옵션 패널 최상위 오브젝트
///    - bgmSlider: BGM 볼륨 조절 Slider
///    - sfxSlider: SFX 볼륨 조절 Slider
///    - bgmVolumeText: BGM 볼륨 수치 표시 Text
///    - sfxVolumeText: SFX 볼륨 수치 표시 Text
///    - bgmMuteToggle: BGM 음소거 Toggle
///    - sfxMuteToggle: SFX 음소거 Toggle
///    - backButton: 뒤로가기(닫기) Button
/// 
/// 【추천 UI 구조 (Hierarchy)】
/// Canvas
///   └─ OptionPanel (이 스크립트 부착)
///       ├─ Background (반투명 검은 배경 Image)
///       ├─ OptionWindow (실제 옵션 창 Image)
///       │   ├─ TitleText ("설정" TextMeshPro)
///       │   ├─ BGM_Section
///       │   │   ├─ BGM_Label ("BGM" Text)
///       │   │   ├─ BGM_Slider (Slider)
///       │   │   ├─ BGM_VolumeText ("100%" Text)
///       │   │   └─ BGM_MuteToggle (Toggle)
///       │   ├─ SFX_Section
///       │   │   ├─ SFX_Label ("효과음" Text)
///       │   │   ├─ SFX_Slider (Slider)
///       │   │   ├─ SFX_VolumeText ("100%" Text)
///       │   │   └─ SFX_MuteToggle (Toggle)
///       │   └─ BackButton ("닫기" Button)
///       └─ (선택) CloseAreaButton (배경 터치 시 닫기용 투명 버튼)
/// ============================================================
/// </summary>
public class OptionUI : MonoBehaviour
{
    // ===== 싱글톤 =====
    public static OptionUI Instance { get; private set; }

    // ───────────────────────────────────────────
    // [Inspector에서 연결할 UI 요소들]
    // ───────────────────────────────────────────

    [Header("===== 옵션 패널 =====")]
    [Tooltip("옵션 패널의 최상위 게임 오브젝트")]
    [SerializeField] private GameObject optionPanel;

    [Header("===== BGM (배경음악) 설정 =====")]
    [Tooltip("BGM 볼륨 조절 슬라이더 (0~1)")]
    [SerializeField] private Slider bgmSlider;

    [Tooltip("BGM 볼륨 수치 표시 텍스트 (예: '75%')")]
    [SerializeField] private TextMeshProUGUI bgmVolumeText;

    [Tooltip("BGM 음소거 토글 버튼")]
    [SerializeField] private Toggle bgmMuteToggle;

    [Header("===== SFX (효과음) 설정 =====")]
    [Tooltip("SFX 볼륨 조절 슬라이더 (0~1)")]
    [SerializeField] private Slider sfxSlider;

    [Tooltip("SFX 볼륨 수치 표시 텍스트 (예: '100%')")]
    [SerializeField] private TextMeshProUGUI sfxVolumeText;

    [Tooltip("SFX 음소거 토글 버튼")]
    [SerializeField] private Toggle sfxMuteToggle;

    [Header("===== 버튼 =====")]
    [Tooltip("뒤로가기(닫기) 버튼")]
    [SerializeField] private Button backButton;

    [Tooltip("(선택) 배경 클릭 시 닫기용 버튼")]
    [SerializeField] private Button backgroundCloseButton;

    [Header("===== 애니메이션 설정 =====")]
    [Tooltip("패널 열기/닫기 애니메이션 사용 여부")]
    [SerializeField] private bool useAnimation = true;

    [Tooltip("애니메이션 속도")]
    [SerializeField] private float animationSpeed = 8f;

    // ───────────────────────────────────────────
    // [내부 변수]
    // ───────────────────────────────────────────
    private bool isAnimating = false;   // 현재 애니메이션 중인지
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

        // CanvasGroup 확보 (페이드 애니메이션용)
        if (optionPanel != null)
        {
            canvasGroup = optionPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = optionPanel.AddComponent<CanvasGroup>();
            }
        }

        // 시작 시 옵션 패널 숨기기
        if (optionPanel != null)
        {
            optionPanel.SetActive(false);
        }
    }

    // ==========================================================
    //  UI 이벤트 연결
    // ==========================================================

    /// <summary>
    /// 모든 UI 요소에 이벤트 리스너 연결
    /// 슬라이더 값 변경, 토글 변경, 버튼 클릭 등
    /// </summary>
    private void SetupUIEvents()
    {
        // ── BGM 슬라이더 이벤트 ──
        if (bgmSlider != null)
        {
            // 슬라이더 범위 설정 (0 = 무음, 1 = 최대)
            bgmSlider.minValue = 0f;
            bgmSlider.maxValue = 1f;

            // 슬라이더 값이 변경될 때마다 OnBGMVolumeChanged 호출
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        // ── SFX 슬라이더 이벤트 ──
        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        // ── BGM 음소거 토글 이벤트 ──
        if (bgmMuteToggle != null)
        {
            bgmMuteToggle.onValueChanged.AddListener(OnBGMMuteChanged);
        }

        // ── SFX 음소거 토글 이벤트 ──
        if (sfxMuteToggle != null)
        {
            sfxMuteToggle.onValueChanged.AddListener(OnSFXMuteChanged);
        }

        // ── 뒤로가기 버튼 이벤트 ──
        if (backButton != null)
        {
            backButton.onClick.AddListener(CloseOptionPanel);
        }

        // ── 배경 클릭 닫기 (선택사항) ──
        if (backgroundCloseButton != null)
        {
            backgroundCloseButton.onClick.AddListener(CloseOptionPanel);
        }
    }

    // ==========================================================
    //  옵션 패널 열기/닫기
    // ==========================================================

    /// <summary>
    /// 옵션 패널 열기
    /// SoundManager에서 현재 설정값을 가져와 UI에 반영
    /// </summary>
    public void OpenOptionPanel()
    {
        if (optionPanel == null) return;

        // 패널 활성화
        optionPanel.SetActive(true);

        // SoundManager에서 현재 설정값 가져와서 UI에 반영
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
    //  SoundManager ↔ UI 동기화
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

        // ── BGM 슬라이더에 현재 볼륨 반영 ──
        if (bgmSlider != null)
        {
            // 이벤트 임시 해제 → 값 설정 → 이벤트 재연결
            // (값 설정 시 이벤트가 발생하는 것을 방지)
            bgmSlider.onValueChanged.RemoveListener(OnBGMVolumeChanged);
            bgmSlider.value = SoundManager.Instance.GetBGMVolume();
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        // ── SFX 슬라이더에 현재 볼륨 반영 ──
        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
            sfxSlider.value = SoundManager.Instance.GetSFXVolume();
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        // ── 음소거 토글 상태 반영 ──
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

        // ── 볼륨 텍스트 갱신 ──
        UpdateVolumeTexts();
    }

    // ==========================================================
    //  UI 이벤트 핸들러 (슬라이더/토글 변경 시 호출됨)
    // ==========================================================

    /// <summary>
    /// BGM 슬라이더 값 변경 시 호출
    /// </summary>
    /// <param name="value">새 볼륨 값 (0~1)</param>
    private void OnBGMVolumeChanged(float value)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetBGMVolume(value);
        }

        // 볼륨 텍스트 갱신 (예: "75%")
        UpdateBGMVolumeText(value);
    }

    /// <summary>
    /// SFX 슬라이더 값 변경 시 호출
    /// </summary>
    /// <param name="value">새 볼륨 값 (0~1)</param>
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
    //  볼륨 텍스트 갱신
    // ==========================================================

    /// <summary>
    /// BGM + SFX 볼륨 텍스트 모두 갱신
    /// </summary>
    private void UpdateVolumeTexts()
    {
        if (bgmSlider != null) UpdateBGMVolumeText(bgmSlider.value);
        if (sfxSlider != null) UpdateSFXVolumeText(sfxSlider.value);
    }

    /// <summary>
    /// BGM 볼륨 텍스트 갱신 (예: "75%")
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
    /// SFX 볼륨 텍스트 갱신
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
    /// 페이드인 (투명 → 불투명) 애니메이션
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
    /// 페이드아웃 (불투명 → 투명) 애니메이션
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
}