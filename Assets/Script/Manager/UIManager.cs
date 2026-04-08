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

    [Header("상단 헤더바")]
    [Tooltip("왼쪽 상단 타이틀 이미지 (패널별 배너 이미지)")]
    [SerializeField] private Image headerTitleImage;
    [Tooltip("왼쪽 상단 레벨 텍스트 (메인 게임 화면용)")]
    [SerializeField] private TextMeshProUGUI headerLevelText;
    [Tooltip("메인 게임 화면에서만 보이는 UI 그룹 (HP/EXP/Stage 등)")]
    [SerializeField] private GameObject gameOnlyUI;

    [Header("패널 배너 이미지")]
    [SerializeField] private Sprite bannerEquip;       // 장착
    [SerializeField] private Sprite bannerSkill;       // 스킬
    [SerializeField] private Sprite bannerCompanion;   // 동료
    [SerializeField] private Sprite bannerCraft;       // 제작
    [SerializeField] private Sprite bannerInventory;   // 가방
    [SerializeField] private Sprite bannerShop;        // 상점
    [SerializeField] private Sprite bannerGacha;       // 뽑기
    [SerializeField] private Sprite bannerRanking;     // 랭킹
    [SerializeField] private Sprite bannerSetting;     // 설정
    [SerializeField] private Sprite bannerMail;        // 우편
    [SerializeField] private Sprite bannerAuction;     // 경매
    [SerializeField] private Sprite bannerEnhance;     // 강화
    [SerializeField] private Sprite bannerAchieve;     // 업적
    [SerializeField] private Sprite bannerFriend;      // 친구
    [SerializeField] private Sprite bannerGuild;       // 길드

    [Header("상단 리소스 UI (항상 고정)")]
    [SerializeField] public TextMeshProUGUI GameId;
    [SerializeField] public TextMeshProUGUI GameName;
    [SerializeField] public Image Character;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI gemText;
    [Tooltip("상단 고정 크롭포인트 텍스트")]
    [SerializeField] private TextMeshProUGUI cropPointText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Slider expSlider;
    [SerializeField] private TextMeshProUGUI expText;

    [Header("플레이어 스탯 UI (장비창)")]
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI defenseText;
    [SerializeField] private TextMeshProUGUI maxHpText;
    [Tooltip("★ 이동속도 → 공격속도로 재활용 (필드명 유지로 Inspector 호환)")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI criticalText;
    [Tooltip("크리티컬 데미지 (%)")]
    [SerializeField] private TextMeshProUGUI criticalDamageText;
    [Tooltip("전투력 (CombatPowerManager.TotalCombatPower)")]
    [SerializeField] private TextMeshProUGUI combatPowerText;

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
            Debug.Log("[ManagerInit] UIManager가 생성되었습니다.");

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

        // 크롭포인트 이벤트 구독
        FarmManager.OnCropPointsChanged += UpdateCropPointUI;
    }

    void OnDisable()
    {
        // 이벤트 구독 해제
        GameManager.OnGoldChanged -= UpdateGoldUI;
        GameManager.OnGemChanged -= UpdateGemUI;
        GameManager.OnItemAcquired -= ShowItemNotification;
        PlayerStats.OnHealthChanged -= UpdateHpUI;
        FarmManager.OnCropPointsChanged -= UpdateCropPointUI;
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
            stageText.color = (stageWave == wavesPerStage) ? Color.red : Color.white;
        }

        // ★ 씬 내 모든 "Stage" 관련 TMP도 동기화 (복사된 텍스트 대응)
        var allTMP = FindObjectsOfType<TMPro.TextMeshProUGUI>(true);
        foreach (var tmp in allTMP)
        {
            if (tmp != stageText && tmp.text.StartsWith("Stage "))
            {
                tmp.text = $"Stage {chapter}-{stageWave}";
                tmp.color = (stageWave == wavesPerStage) ? Color.red : Color.white;
            }
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

        // 크롭포인트 초기값 표시 (FarmManager 없는 MainScene에서도 동작)
        long cp = CropPointService.Value;
        UpdateCropPointUI(cp);

        if (PlayerStats.Instance != null)
        {
            UpdateHpUI(PlayerStats.Instance.currentHealth, PlayerStats.Instance.maxHealth);
        }

        UpdateStatsUI();
    }

    public void UpdateGoldUI(long gold)
    {
        if (goldText == null) return;
        goldText.text = FormatKoreanUnit(gold);
    }

    public void UpdateGemUI(long gem)
    {
        if (gemText == null) return;
        gemText.text = FormatKoreanUnit(gem);
    }

    public void UpdateCropPointUI(long cropPoints)
    {
        if (cropPointText == null) return;
        cropPointText.text = FormatKoreanUnit(cropPoints);
    }

    private Coroutine _hpAnimCoroutine;

    private void UpdateHpUI(float currentHp, float maxHp)
    {
        if (hpSlider != null)
        {
            // ★ 비율(0~1) 방식으로 통일 — maxValue 변경 불필요
            hpSlider.minValue = 0f;
            hpSlider.maxValue = 1f;
            float ratio = maxHp > 0 ? Mathf.Clamp01(currentHp / maxHp) : 0f;

            // 이전 애니메이션 중단 후 새로 시작
            if (_hpAnimCoroutine != null) StopCoroutine(_hpAnimCoroutine);
            _hpAnimCoroutine = StartCoroutine(AnimateSlider(hpSlider, ratio, 0.3f));
        }

        if (hpText != null)
        {
            hpText.text = $"{FormatKoreanUnit((long)currentHp)} / {FormatKoreanUnit((long)maxHp)}";
            hpText.color = (maxHp > 0 && currentHp / maxHp < 0.3f) ? Color.red : Color.white;
        }
    }

    public void UpdateExpUI(int exp, int level)
    {
        currentLevel = level;
        targetDisplayExp = exp;
        isExpAnimating = true;

        if (levelText != null)
            levelText.text = $"Lv.{level}";

        if (headerLevelText != null && headerLevelText.gameObject.activeSelf)
            headerLevelText.text = $"Lv.{level}";

        if (GameManager.Instance != null)
        {
            int requiredExp = GameManager.Instance.GetRequiredExpForLevel(level);

            if (expText != null)
                expText.text = $"{FormatKoreanUnit((long)exp)} / {FormatKoreanUnit((long)requiredExp)}";

            // ★ 슬라이더 즉시 반영 (Smooth 전 초기값)
            if (expSlider != null)
            {
                expSlider.minValue = 0f;
                expSlider.maxValue = 1f;
                float ratio = requiredExp > 0 ? Mathf.Clamp01((float)exp / requiredExp) : 0f;
                expSlider.value = ratio;
            }
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
        else
        {
            expSlider.value = 0f;
        }

        // 목표값에 충분히 가까워지면 애니메이션 종료
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
        currentLevel = newLevel;

        if (expSlider != null)
        {
            expSlider.minValue = 0f;
            expSlider.maxValue = 1f;
            expSlider.value = 0f;
        }

        if (levelText != null)
            levelText.text = $"Lv.{newLevel}";

        if (headerLevelText != null && headerLevelText.gameObject.activeSelf)
            headerLevelText.text = $"Lv.{newLevel}";

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
            defenseText.text = $"방어력:{FormatKoreanUnit((long)totalDefense)}";
            defenseText.color = stats.bonusDefense > 0 ? Color.green : Color.white;
        }

        if (maxHpText != null)
        {
            maxHpText.text = $"체력:{FormatKoreanUnit((long)stats.maxHealth)}";
            maxHpText.color = stats.bonusMaxHp > 0 ? Color.green : Color.white;
        }

        // ★ speedText 슬롯을 공격속도 표시로 재활용 (이동속도 제거)
        if (speedText != null)
        {
            float atkSpeedBonus = stats.bonusAttackSpeed;
            speedText.text = $"공격속도:+{atkSpeedBonus:F0}%";
            speedText.color = atkSpeedBonus > 0 ? Color.green : Color.white;
        }

        if (criticalText != null)
        {
            float totalCritical = stats.criticalRate + stats.bonusCritical;
            criticalText.text = $"크리티컬:{totalCritical:F1}%";
            criticalText.color = stats.bonusCritical > 0 ? Color.green : Color.white;
        }

        // ★ 크리티컬 데미지 (크리티컬 텍스트 바로 아래 슬롯)
        if (criticalDamageText != null)
        {
            criticalDamageText.text = $"크리뎀:{stats.criticalDamage:F0}%";
            criticalDamageText.color = Color.white;
        }

        // ★ 전투력
        if (combatPowerText != null)
        {
            int power = CombatPowerManager.Instance != null
                ? CombatPowerManager.Instance.TotalCombatPower : 0;
            combatPowerText.text = $"전투력:{FormatKoreanUnit(power)}";
            combatPowerText.color = new Color(1f, 0.84f, 0f); // 골드
        }
    }

    #endregion
    private string FormatStat(float value)
    {
        return FormatKoreanUnit((long)value);
    }
    #region 상단 헤더 타이틀

    /// <summary>
    /// 패널 열릴 때 호출 — 왼쪽 상단 배너 이미지를 해당 패널 이미지로 변경
    /// 예: SetHeaderBanner("장착"), SetHeaderBanner("스킬")
    /// </summary>
    public void SetHeaderBanner(string panelName)
    {
        Sprite banner = GetBannerSprite(panelName);

        if (headerTitleImage != null)
        {
            if (banner != null)
            {
                headerTitleImage.sprite = banner;
                headerTitleImage.gameObject.SetActive(true);
            }
            else
            {
                headerTitleImage.gameObject.SetActive(false);
            }
        }

        // 배너가 보이면 레벨 텍스트 숨기기
        if (headerLevelText != null)
            headerLevelText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 패널 닫히고 메인 게임 화면으로 돌아올 때 호출
    /// 배너 이미지 숨기고, 레벨 텍스트 표시
    /// </summary>
    public void ResetHeaderToGame()
    {
        // 배너 이미지 숨기기
        if (headerTitleImage != null)
            headerTitleImage.gameObject.SetActive(false);

        // 레벨 텍스트 보이기
        if (headerLevelText != null)
        {
            headerLevelText.gameObject.SetActive(true);
            if (GameManager.Instance != null)
                headerLevelText.text = $"Lv.{GameManager.Instance.PlayerLevel}";
        }
    }

    /// <summary>패널 이름으로 배너 Sprite 찾기</summary>
    private Sprite GetBannerSprite(string panelName)
    {
        switch (panelName)
        {
            case "장착":   return bannerEquip;
            case "스킬":   return bannerSkill;
            case "동료":   return bannerCompanion;
            case "제작":   return bannerCraft;
            case "가방":   return bannerInventory;
            case "상점":   return bannerShop;
            case "뽑기":   return bannerGacha;
            case "랭킹":   return bannerRanking;
            case "설정":   return bannerSetting;
            case "우편":   return bannerMail;
            case "경매":   return bannerAuction;
            case "강화":   return bannerEnhance;
            case "업적":   return bannerAchieve;
            case "친구":   return bannerFriend;
            case "길드":   return bannerGuild;
            default:       return null;
        }
    }

    #endregion

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

    public static string FormatKoreanUnit(long number)
    {
        if (number < 0) return "-" + FormatKoreanUnit(-number);

        if (number >= 10_000_000_000_000_000L)  // 경 (10^16)
        {
            double val = number / 10_000_000_000_000_000.0;
            return val >= 100 ? $"{val:F0}경" : $"{val:F1}경".TrimEnd('0').TrimEnd('.');
        }
        if (number >= 1_000_000_000_000L)  // 조
        {
            double val = number / 1_000_000_000_000.0;
            return val >= 100 ? $"{val:F0}조" : $"{val:F1}조".TrimEnd('0').TrimEnd('.');
        }
        if (number >= 100_000_000L)  // 억
        {
            double val = number / 100_000_000.0;
            return val >= 100 ? $"{val:F0}억" : $"{val:F1}억".TrimEnd('0').TrimEnd('.');
        }
        if (number >= 10_000L)  // 만
        {
            double val = number / 10_000.0;
            return val >= 100 ? $"{val:F0}만" : $"{val:F1}만".TrimEnd('0').TrimEnd('.');
        }
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