using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// UI 관리 매니저 (TopMenuManager 연동 버전)
/// - 이벤트 기반 UI 업데이트
/// - UI 애니메이션
/// - 메시지 및 알림 시스템
/// - UI 풀링 시스템
/// - 스탯 UI 표시 (장비 시스템)
/// 
/// ⭐ 변경사항:
/// - 인벤토리/상점/가챠 버튼 제거 (TopMenuManager로 이동)
/// - 버튼 관련 필드 제거
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("상단 리소스 UI")]
    [SerializeField] public TextMeshProUGUI GameId;
    [SerializeField] public TextMeshProUGUI GameName;
    [SerializeField] public Image Character;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI gemText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Slider expSlider;
    [SerializeField] private TextMeshProUGUI expText;

    [Header("플레이어 스탯 UI (장비창)")]
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI defenseText;
    [SerializeField] private TextMeshProUGUI maxHpText;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI criticalText;

    [Header("경험치 바 애니메이션 설정")]
    [SerializeField] private float expFillSpeed = 3f;

    [Header("메시지 시스템")]
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float messageDuration = 2f;
    [SerializeField] private float messageFadeTime = 0.3f;

    [Header("알림 팝업 (획득 아이템 등)")]
    [SerializeField] private GameObject notificationPrefab;
    [SerializeField] private Transform notificationParent;
    [SerializeField] private int maxNotifications = 5;

    [Header("확인 다이얼로그")]
    [SerializeField] private GameObject confirmDialogPanel;
    [SerializeField] private TextMeshProUGUI confirmDialogText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("로딩 화면")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider loadingBar;
    [SerializeField] private TextMeshProUGUI loadingText;

    // ⭐ 버튼 필드 제거됨 (TopMenuManager로 이동)
    // [SerializeField] private Button inventoryButton;
    // [SerializeField] private Button shopButton;
    // [SerializeField] private Button gachaButton;
    // [SerializeField] private Button questButton;
    // [SerializeField] private Button settingsButton;
    // ─────────────────────────────────────────────────────
    // UIManager.cs 에 추가할 코드 (복붙용)
    // ─────────────────────────────────────────────────────

    // 1) 필드 추가 (Header 아무데나 추가)
    [Header("스테이지 UI")]
    [SerializeField] private TextMeshProUGUI stageText;         // "Stage 2-3"
    [SerializeField] private TextMeshProUGUI waveProgressText;  // "3 / 10"
    [SerializeField] private Slider waveProgressSlider; // 진행 게이지

    // 2) 메서드 추가 (아무 region에나 추가)
    /// <summary>
    /// WaveSpawner에서 호출 - 스테이지 텍스트 & 게이지 업데이트
    /// </summary>

    // 부드러운 경험치 바를 위한 변수
    private float currentDisplayExp;
    private float targetDisplayExp;
    private bool isExpAnimating = false; // ✅ 경험치 애니메이션 중일 때만 Update 실행
    private int currentLevel;

    // UI 풀링
    private Queue<GameObject> notificationPool = new Queue<GameObject>();
    private List<GameObject> activeNotifications = new List<GameObject>();

    // 현재 활성화된 다이얼로그 콜백
    private System.Action currentConfirmAction;
    private System.Action currentCancelAction;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeUI();
    }

    void Start()
    {
        if (PlayerStats.Instance != null)
        {
            UpdateHpUI(PlayerStats.Instance.currentHealth, PlayerStats.Instance.maxHealth);
        }

        UpdateStatsUI();
    }

    void Update()
    {
        if (isExpAnimating)
            UpdateExpBarSmooth();
    }

    void OnEnable()
    {
        // GameManager 이벤트 구독
        GameManager.OnGoldChanged += UpdateGoldUI;
        GameManager.OnGemChanged += UpdateGemUI;
        GameManager.OnItemAcquired += ShowItemNotification;

        // PlayerStats 이벤트 구독
        PlayerStats.OnHealthChanged += UpdateHpUI;
    }

    void OnDisable()
    {
        // 이벤트 구독 해제
        GameManager.OnGoldChanged -= UpdateGoldUI;
        GameManager.OnGemChanged -= UpdateGemUI;
        GameManager.OnItemAcquired -= ShowItemNotification;
        PlayerStats.OnHealthChanged -= UpdateHpUI;
    }

    /// <summary>
    /// UI 초기화
    /// </summary>
    private void InitializeUI()
    {
        // ⭐ 버튼 이벤트 연결 (다이얼로그 버튼만)
        SetupButtons();

        // 초기 UI 업데이트
        RefreshAllUI();

        // 있어보이는 UI 설정
        SetupCoolUI();

        // 메시지 패널 비활성화
        if (messagePanel != null)
        {
            messagePanel.SetActive(false);
        }

        // 확인 다이얼로그 비활성화
        if (confirmDialogPanel != null)
        {
            confirmDialogPanel.SetActive(false);
        }

        // 로딩 패널 비활성화
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        // 알림 풀 생성
        InitializeNotificationPool();
    }
    public void UpdateStageUI(int chapter, int stageWave, int wavesPerStage = 10)
    {
        if (stageText != null)
        {
            stageText.text = $"Stage {chapter}-{stageWave}";
            stageText.color = (stageWave == wavesPerStage) ? Color.red : Color.white; // 보스웨이브=빨강
        }

        if (waveProgressText != null)
            waveProgressText.text = $"{stageWave} / {wavesPerStage}";

        if (waveProgressSlider != null)
            StartCoroutine(AnimateSlider(waveProgressSlider, (float)stageWave / wavesPerStage, 0.5f));
    }
    /// <summary>
    /// 있어보이는 UI 설정 (GameId, GameName, Character)
    /// </summary>
    private void SetupCoolUI()
    {
        if (GameId != null)
        {
            string randomId = GenerateCoolGameId();
            GameId.text = $"ID: {randomId}";
            GameId.color = new Color(1f, 0.84f, 0f); // 골드 색상
        }

        if (GameName != null)
        {
            GameName.text = "INFINITY";
            GameName.color = new Color(0f, 0.95f, 1f); // 시안 색상
        }

        if (Character != null)
        {
            StartCoroutine(CharacterPulseEffect());
        }
    }

    private string GenerateCoolGameId()
    {
        int randomNum = Random.Range(1000, 9999);
        string prefix = "TDF";
        return $"{prefix}-{randomNum}";
    }

    private IEnumerator CharacterPulseEffect()
    {
        if (Character == null) yield break;

        while (true)
        {
            float duration = 2f;
            float elapsed = 0f;
            Color startColor = Character.color;
            Color brightColor = new Color(startColor.r * 1.2f, startColor.g * 1.2f, startColor.b * 1.2f, startColor.a);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Character.color = Color.Lerp(startColor, brightColor, elapsed / duration);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Character.color = Color.Lerp(brightColor, startColor, elapsed / duration);
                yield return null;
            }
        }
    }

    /// <summary>
    /// ⭐ 버튼 이벤트 연결 (다이얼로그 버튼만)
    /// 인벤토리/상점/가챠 버튼은 TopMenuManager에서 관리
    /// </summary>
    private void SetupButtons()
    {
        // ★ Inspector 미연결 시 confirmDialogPanel에서 자동 탐색
        if (confirmDialogPanel != null)
        {
            if (confirmButton == null || cancelButton == null || confirmDialogText == null)
            {
                Button[] btns = confirmDialogPanel.GetComponentsInChildren<Button>(true);
                foreach (Button btn in btns)
                {
                    string name = btn.gameObject.name.ToLower();
                    if (confirmButton == null && (name.Contains("confirm") || name.Contains("ok") || name.Contains("yes") || name.Contains("확인")))
                        confirmButton = btn;
                    else if (cancelButton == null && (name.Contains("cancel") || name.Contains("no") || name.Contains("취소") || name.Contains("닫")))
                        cancelButton = btn;
                }
                if (confirmDialogText == null)
                    confirmDialogText = confirmDialogPanel.GetComponentInChildren<TextMeshProUGUI>(true);

                if (confirmButton != null || cancelButton != null)
                    Debug.Log($"[UIManager] 다이얼로그 버튼 자동 탐색 — confirm:{confirmButton?.name}, cancel:{cancelButton?.name}");
            }
        }

        // ⭐ 확인 다이얼로그 버튼 연결
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() =>
            {
                PlayButtonSound();
                OnConfirmButtonClicked();
            });
        }
        else
        {
            Debug.LogWarning("[UIManager] confirmButton이 연결되지 않았습니다! Inspector에서 확인하세요.");
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() =>
            {
                PlayButtonSound();
                OnCancelButtonClicked();
            });
        }

        // ⭐ 삭제된 코드:
        // inventoryButton, shopButton, gachaButton 등은
        // TopMenuManager에서 관리하므로 여기서 제거
    }

    #region 리소스 UI 업데이트

    public void RefreshAllUI()
    {
        if (GameManager.Instance == null) return;

        UpdateGoldUI(GameManager.Instance.PlayerGold);
        UpdateGemUI(GameManager.Instance.PlayerGem);
        UpdateExpUI(GameManager.Instance.PlayerExp, GameManager.Instance.PlayerLevel);

        if (PlayerStats.Instance != null)
        {
            UpdateHpUI(PlayerStats.Instance.currentHealth, PlayerStats.Instance.maxHealth);
        }

        UpdateStatsUI();
    }

    public void UpdateGoldUI(int gold)
    {
        if (goldText == null) return;
        goldText.text = FormatNumber(gold);
    }

    public void UpdateGemUI(int gem)
    {
        if (gemText == null) return;
        gemText.text = FormatNumber(gem);
    }

    private void UpdateHpUI(float currentHp, float maxHp)
    {
        if (hpSlider != null)
        {
            // ✅ 핵심 수정: maxValue를 먼저 맞추고, 비율 대신 실제 값으로 애니메이션
            hpSlider.maxValue = maxHp;
            StartCoroutine(AnimateSlider(hpSlider, currentHp, 0.3f));
        }

        if (hpText != null)
        {
            hpText.text = $"{(int)currentHp} / {(int)maxHp}";
            hpText.color = currentHp / maxHp < 0.3f ? Color.red : Color.white;
        }
    }

    public void UpdateExpUI(int exp, int level)
    {
        currentLevel = level;
        targetDisplayExp = exp;
        isExpAnimating = true; // ✅ 애니메이션 시작

        if (levelText != null)
        {
            levelText.text = $"Lv.{level}";
        }

        if (expText != null && GameManager.Instance != null)
        {
            int requiredExp = GameManager.Instance.GetRequiredExpForLevel(level);
            expText.text = $"{exp} / {requiredExp}";
        }
    }

    private void UpdateExpBarSmooth()
    {
        if (expSlider == null || GameManager.Instance == null) return;

        currentDisplayExp = Mathf.Lerp(currentDisplayExp, targetDisplayExp,
                                       Time.deltaTime * expFillSpeed);

        int requiredExp = GameManager.Instance.GetRequiredExpForLevel(currentLevel);

        if (requiredExp > 0)
        {
            float fillAmount = currentDisplayExp / (float)requiredExp;
            expSlider.value = Mathf.Clamp01(fillAmount);
        }

        // ✅ 목표값에 충분히 가까워지면 애니메이션 종료
        if (Mathf.Abs(currentDisplayExp - targetDisplayExp) < 0.5f)
        {
            currentDisplayExp = targetDisplayExp;
            isExpAnimating = false;
        }
    }

    public void OnLevelUp(int newLevel)
    {
        currentDisplayExp = 0f;
        targetDisplayExp = 0f;

        if (expSlider != null)
        {
            expSlider.value = 0f;
        }

        if (levelText != null)
        {
            levelText.text = $"Lv.{newLevel}";
        }

        ShowMessage($"레벨 업! Lv.{newLevel}", new Color(1f, 0.84f, 0f));
    }

    public void UpdateStatsUI()
    {
        if (PlayerStats.Instance == null) return;

        PlayerStats stats = PlayerStats.Instance;

        if (attackText != null)
        {
            float totalAttack = stats.attackPower + stats.bonusAttack;
            attackText.text = $"공격력:{FormatStat(totalAttack)}";
            attackText.color = stats.bonusAttack > 0 ? Color.green : Color.white;
        }

        if (defenseText != null)
        {
            float totalDefense = stats.defense + stats.bonusDefense;
            defenseText.text = $"방어력:{totalDefense:F0}";
            defenseText.color = stats.bonusDefense > 0 ? Color.green : Color.white;
        }

        if (maxHpText != null)
        {
            maxHpText.text = $"체력:{stats.maxHealth:F0}";
            maxHpText.color = stats.bonusMaxHp > 0 ? Color.green : Color.white;
        }

        if (speedText != null)
        {
            float baseSpeed = 5f;
            float totalSpeed = baseSpeed + stats.bonusSpeed;
            speedText.text = $"이동속도:{totalSpeed:F1}";
            speedText.color = stats.bonusSpeed > 0 ? Color.green : Color.white;
        }

        if (criticalText != null)
        {
            float totalCritical = stats.criticalRate + stats.bonusCritical;
            criticalText.text = $"크리티컬:{totalCritical:F1}%";
            criticalText.color = stats.bonusCritical > 0 ? Color.green : Color.white;
        }
    }

    #endregion
    private string FormatStat(float value)
    {
        if (value >= 1000000f) return $"{value / 1000000f:F1}M";
        if (value >= 1000f) return $"{value / 1000f:F1}K";
        return $"{value:F0}";
    }
    #region 메시지 시스템

    public void ShowMessage(string message, Color? color = null)
    {
        if (messagePanel == null || messageText == null) return;

        StopAllCoroutines();
        StartCoroutine(ShowMessageCoroutine(message, color ?? Color.red));
    }

    private IEnumerator ShowMessageCoroutine(string message, Color color)
    {
        messageText.text = message;
        messageText.color = color;
        messagePanel.SetActive(true);

        CanvasGroup canvasGroup = messagePanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = messagePanel.AddComponent<CanvasGroup>();
        }

        yield return StartCoroutine(FadeCanvasGroup(canvasGroup, 0f, 1f, messageFadeTime));
        yield return new WaitForSeconds(messageDuration);
        yield return StartCoroutine(FadeCanvasGroup(canvasGroup, 1f, 0f, messageFadeTime));

        messagePanel.SetActive(false);
    }

    #endregion

    #region 알림 시스템

    private void InitializeNotificationPool()
    {
        if (notificationPrefab == null || notificationParent == null) return;

        for (int i = 0; i < maxNotifications; i++)
        {
            GameObject notification = Instantiate(notificationPrefab, notificationParent);
            notification.SetActive(false);
            notificationPool.Enqueue(notification);
        }
    }

    private void ShowItemNotification(string itemName, int count)
    {
        if (notificationPool.Count == 0) return;

        GameObject notification = notificationPool.Dequeue();
        notification.SetActive(true);

        TextMeshProUGUI notifText = notification.GetComponentInChildren<TextMeshProUGUI>();
        if (notifText != null)
        {
            notifText.text = $"{itemName} x{count} 획득!";
        }

        activeNotifications.Add(notification);
        StartCoroutine(ReturnNotificationToPool(notification, 3f));
    }

    private IEnumerator ReturnNotificationToPool(GameObject notification, float delay)
    {
        yield return new WaitForSeconds(delay);

        CanvasGroup cg = notification.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            yield return FadeCanvasGroup(cg, 1f, 0f, 0.3f);
        }

        notification.SetActive(false);
        activeNotifications.Remove(notification);
        notificationPool.Enqueue(notification);
    }

    #endregion

    #region 확인 다이얼로그

    public void ShowConfirmDialog(string message, System.Action onConfirm, System.Action onCancel = null)
    {
        if (confirmDialogPanel == null) return;

        confirmDialogText.text = message;
        currentConfirmAction = onConfirm;
        currentCancelAction = onCancel;

        confirmDialogPanel.SetActive(true);

        // ★ 다이얼로그를 최상위로 올려서 다른 캔버스에 가려지지 않게
        confirmDialogPanel.transform.SetAsLastSibling();

        // ★ 확인 팝업 전용 Canvas 보장 — VIP 등 다른 패널 위에 표시
        Canvas dialogCanvas = confirmDialogPanel.GetComponent<Canvas>();
        if (dialogCanvas == null)
            dialogCanvas = confirmDialogPanel.AddComponent<Canvas>();
        dialogCanvas.overrideSorting = true;
        dialogCanvas.sortingOrder = 10000; // 최상위

        // GraphicRaycaster 보장 (없으면 버튼 클릭 불가)
        if (confirmDialogPanel.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            confirmDialogPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    private void OnConfirmButtonClicked()
    {
        currentConfirmAction?.Invoke();
        CloseConfirmDialog();
    }

    private void OnCancelButtonClicked()
    {
        currentCancelAction?.Invoke();
        CloseConfirmDialog();
    }

    private void CloseConfirmDialog()
    {
        if (confirmDialogPanel != null)
        {
            // ★ 전용 Canvas sortingOrder 복원
            Canvas dialogCanvas = confirmDialogPanel.GetComponent<Canvas>();
            if (dialogCanvas != null)
                dialogCanvas.overrideSorting = false;

            confirmDialogPanel.SetActive(false);
        }

        currentConfirmAction = null;
        currentCancelAction = null;
    }

    #endregion

    #region 로딩 화면

    public void ShowLoading(string message = "로딩 중...")
    {
        if (loadingPanel == null) return;

        loadingPanel.SetActive(true);
        if (loadingText != null)
        {
            loadingText.text = message;
        }
    }

    public void HideLoading()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    public void UpdateLoadingProgress(float progress)
    {
        if (loadingBar != null)
        {
            loadingBar.value = Mathf.Clamp01(progress);
        }
    }

    #endregion

    #region 유틸리티 및 애니메이션

    private string FormatNumber(int number)
    {
        return number.ToString("N0");
    }

    private void AnimateTextScale(Transform textTransform)
    {
        StartCoroutine(ScalePunchCoroutine(textTransform));
    }

    private IEnumerator ScalePunchCoroutine(Transform target)
    {
        Vector3 originalScale = target.localScale;
        Vector3 originalPosition = target.localPosition;
        Vector3 punchScale = originalScale * 1.05f;

        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(originalScale, punchScale, elapsed / duration);
            target.localPosition = originalPosition;
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(punchScale, originalScale, elapsed / duration);
            target.localPosition = originalPosition;
            yield return null;
        }

        target.localScale = originalScale;
        target.localPosition = originalPosition;
    }

    private IEnumerator AnimateSlider(Slider slider, float targetValue, float duration)
    {
        float startValue = slider.value;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            slider.value = Mathf.Lerp(startValue, targetValue, elapsed / duration);
            yield return null;
        }

        slider.value = targetValue;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        canvasGroup.alpha = startAlpha;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = endAlpha;
    }

    private void PlayButtonSound()
    {
        // TODO: AudioManager를 통해 버튼 클릭 사운드 재생
        // AudioManager.Instance.PlaySFX("ButtonClick");
    }

    #endregion
}