using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 가챠 UI 관리 (1회/10연차/100연차)
/// </summary>
public class GachaUI : MonoBehaviour
{
    public static GachaUI Instance;

    [Header("UI 참조")]
    public Button lampButton;                   // 램프 버튼 (1회 가챠)
    public Button tenGachaButton;               // 10연차 버튼
    public Button hundredGachaButton;           // ⭐ 100연차 버튼 (NEW!)
    public TextMeshProUGUI ticketText;          // 티켓 텍스트
    public TextMeshProUGUI levelText;           // 레벨 텍스트
    public TextMeshProUGUI progressText;        // 진행도 텍스트
    public Slider progressSlider;               // 진행도 슬라이더

    [Header("램프 애니메이션")]
    public Animator lampAnimator;
    public string clickTrigger = "Click";

    [Header("효과음")]
    public AudioClip lampClickSound;
    public AudioClip gachaSound;
    public AudioClip hundredGachaSound;         // ⭐ 100연차 전용 사운드 (NEW!)

    private AudioSource audioSource;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] GachaUI가 생성되었습니다.");
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Start()
    {
        // 버튼 이벤트 연결
        if (lampButton != null)
        {
            lampButton.onClick.AddListener(OnLampButtonClicked);
        }

        if (tenGachaButton != null)
        {
            tenGachaButton.onClick.AddListener(OnTenGachaButtonClicked);
        }

        // ⭐ 100연차 버튼 연결
        if (hundredGachaButton != null)
        {
            hundredGachaButton.onClick.AddListener(OnHundredGachaButtonClicked);
        }

        UpdateTicketDisplay();
        UpdateLevelDisplay();
    }

    void Update()
    {

        UpdateButtonStates();
    }

    /// <summary>
    /// 램프 버튼 클릭 (1회)
    /// </summary>
    void OnLampButtonClicked()
    {
        Debug.Log("[GachaUI] 램프 버튼 클릭!");
        // ★ 가챠 효과음 (SoundManager 우선, 없으면 로컬 AudioSource)
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayGachaRoll();
        else if (lampClickSound != null && audioSource != null)
            audioSource.PlayOneShot(lampClickSound);

        if (lampAnimator != null)
        {
            lampAnimator.SetTrigger(clickTrigger);
        }

        if (GachaManager.Instance != null)
        {
            GachaManager.Instance.PerformSingleGacha();
        }
    }

    /// <summary>
    /// 10연차 버튼 클릭
    /// </summary>
    void OnTenGachaButtonClicked()
    {
        Debug.Log("[GachaUI] 10연차 버튼 클릭!");
        // ★ 10연차 가챠 효과음
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayGachaRoll();
        else if (gachaSound != null && audioSource != null)
            audioSource.PlayOneShot(gachaSound);

        if (GachaManager.Instance != null)
        {
            GachaManager.Instance.PerformTenGacha();
        }
    }

    /// <summary>
    /// ⭐⭐⭐ 100연차 버튼 클릭 (NEW!)
    /// </summary>
    void OnHundredGachaButtonClicked()
    {
        Debug.Log("[GachaUI] 100연차 버튼 클릭!");
        // ★ 100연차 가챠 효과음
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayGachaRoll();
        else if (hundredGachaSound != null && audioSource != null)
            audioSource.PlayOneShot(hundredGachaSound);
        else if (gachaSound != null && audioSource != null)
            audioSource.PlayOneShot(gachaSound);

        if (GachaManager.Instance != null)
        {
            GachaManager.Instance.PerformHundredGacha();
        }
    }

    /// <summary>
    /// 티켓 표시 업데이트
    /// </summary>
    public void UpdateTicketDisplay()
    {
        // 1. 매니저가 존재하는지 확인
        if (ResourceBarManager.Instance == null) return;

        // 2. 텍스트 UI가 할당되어 있는지 확인
        if (ticketText != null)
        {
            // ResourceBarManager에 있는 현재 장비 티켓 값을 가져와서 표시
            int currentTickets = ResourceBarManager.Instance.equipmentTickets;
            ticketText.text = $"{currentTickets}";

            // 만약 ResourceBarManager 자체의 UI도 갱신해야 한다면 호출
            ResourceBarManager.Instance.UpdateEquipmentTicketUI();
        }
    }

    /// <summary>
    /// 레벨 표시 업데이트
    /// </summary>
    public void UpdateLevelDisplay()
    {
        if (GachaManager.Instance == null) return;

        if (levelText != null)
        {
            levelText.text = $"Lv.{GachaManager.Instance.currentLevel}";
        }

        if (progressText != null)
        {
            int current = GachaManager.Instance.currentGachaCount;
            int required = GachaManager.Instance.gachaCountForLevelUp;
            progressText.text = $"{current}/{required}";
        }

        if (progressSlider != null)
        {
            int current = GachaManager.Instance.currentGachaCount;
            int required = GachaManager.Instance.gachaCountForLevelUp;
            progressSlider.maxValue = required;
            progressSlider.value = current;
        }
    }

    /// <summary>
    /// 버튼 상태 업데이트 (티켓 체크)
    /// </summary>
    void UpdateButtonStates()
    {
        if (GachaManager.Instance == null) return;

        // 1회 가챠 버튼
        if (lampButton != null)
        {
            lampButton.interactable = GachaManager.Instance.CanPerformSingleGacha();
        }

        // 10연차 버튼
        if (tenGachaButton != null)
        {
            tenGachaButton.interactable = GachaManager.Instance.CanPerformTenGacha();
        }

        // ⭐ 100연차 버튼
        if (hundredGachaButton != null)
        {
            hundredGachaButton.interactable = GachaManager.Instance.CanPerformHundredGacha();
        }
    }
}