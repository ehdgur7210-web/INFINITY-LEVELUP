using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// =====================================================
// VipUI.cs
// VIP 패널의 모든 UI를 제어하는 스크립트
// - VipManager 이벤트를 구독해서 자동 갱신
// - 탭 시스템 (VIP5, VIP6, VIP7 ...)
// - 경험치 바 애니메이션
// - 선물 수령 버튼 상태 관리
// =====================================================
public class VipUI : MonoBehaviour
{
    // ─────────────────────────────────────────
    // 인스펙터 연결 필드
    // ─────────────────────────────────────────

    [Header("패널 루트")]
    [Tooltip("VIP 전체 패널 (열고 닫을 때 사용)")]
    [SerializeField] private GameObject vipPanel;

    [Tooltip("닫기 버튼")]
    [SerializeField] private Button closeButton;

    [Header("상단 헤더 - VIP 정보")]
    // [SerializeField] private TextMeshProUGUI vipLevelBadgeText; // 비사용 (이미지로 교체)
    [Tooltip("VIP 등급 뱃지 이미지")]
    [SerializeField] private Image vipLevelBadgeImage;

    [Tooltip("VIP 등급 이름 텍스트 (예: 'VIP6') — 뱃지 옆에 표시")]
    [SerializeField] private TextMeshProUGUI vipLevelNameText;

    [Tooltip("VIP 유효기한 텍스트 (예: '37일 15시간 52분')")]
    [SerializeField] private TextMeshProUGUI expireTimeText;

    [Tooltip("VIP 경험치 슬라이더")]
    [SerializeField] private Slider vipExpSlider;

    [Tooltip("VIP 경험치 텍스트 (예: '1054/2500')")]
    [SerializeField] private TextMeshProUGUI vipExpText;

    [Header("혜택 목록 영역")]
    [Tooltip("혜택 항목 하나의 프리팹 (TextMeshPro가 포함되어야 함)")]
    [SerializeField] private GameObject benefitItemPrefab;

    [Tooltip("혜택 목록이 들어갈 부모 오브젝트 (ScrollView Content)")]
    [SerializeField] private Transform benefitListParent;

    [Tooltip("기간연장 버튼")]
    [SerializeField] private Button extendButton;

    [Header("선물 영역")]
    [Tooltip("무료 선물 아이콘 이미지")]
    [SerializeField] private Image freeGiftIcon;

    [Tooltip("무료 선물 수령 버튼")]
    [SerializeField] private Button freeGiftClaimButton;

    [Tooltip("수령완료 텍스트 (버튼 안)")]
    [SerializeField] private TextMeshProUGUI freeGiftButtonText;

    [Tooltip("유료 선물 아이콘 이미지")]
    [SerializeField] private Image paidGiftIcon;

    [Tooltip("유료 선물 설명 텍스트 (예: '10000다이아 상당')")]
    [SerializeField] private TextMeshProUGUI paidGiftDescText;

    [Tooltip("유료 선물 가격 버튼 (예: '1000다이아')")]
    [SerializeField] private Button paidGiftBuyButton;

    [Tooltip("유료 선물 가격 텍스트")]
    [SerializeField] private TextMeshProUGUI paidGiftPriceText;

    [Tooltip("할인 뱃지 오브젝트 (90% OFF 등)")]
    [SerializeField] private GameObject discountBadge;

    [Tooltip("할인율 텍스트")]
    [SerializeField] private TextMeshProUGUI discountText;

    [Header("VIP 진입 버튼 (메인 화면)")]
    [Tooltip("메인 화면의 VIP 버튼 아이콘 — 현재 등급 뱃지로 자동 변경됨")]
    [SerializeField] private Image vipEntryButtonIcon;

    [Header("하단 탭")]
    [Tooltip("탭 버튼 컨테이너 (버튼들이 자식으로 있는 부모)")]
    [SerializeField] private Transform tabParent;

    [Tooltip("탭 버튼 프리팹 (Button + TextMeshPro 포함)")]
    [SerializeField] private GameObject tabButtonPrefab;

    [Tooltip("현재 선택된 탭의 색상")]
    [SerializeField] private Color selectedTabColor = new Color(0.3f, 0.7f, 1f);

    [Tooltip("선택되지 않은 탭의 색상")]
    [SerializeField] private Color normalTabColor = Color.white;

    [Header("경험치 바 애니메이션")]
    [Tooltip("경험치 바가 채워지는 속도 (높을수록 빠름)")]
    [SerializeField] private float expFillSpeed = 2f;

    // ─────────────────────────────────────────
    // 내부 변수
    // ─────────────────────────────────────────

    /// <summary>현재 탭에서 보고 있는 VIP 등급 (탭 클릭 시 변경)</summary>
    private int _selectedTabVipLevel = 1;

    /// <summary>생성된 탭 버튼 목록 (색상 변경에 사용)</summary>
    private List<Button> _tabButtons = new List<Button>();

    /// <summary>생성된 혜택 아이템 목록 (재사용 목적으로 캐싱)</summary>
    private List<GameObject> _benefitItems = new List<GameObject>();

    /// <summary>경험치 바 코루틴 참조 (중복 실행 방지)</summary>
    private Coroutine _expBarCoroutine;

    /// <summary>만료 시간 실시간 갱신 코루틴</summary>
    private Coroutine _timerCoroutine;

    // ─────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────

    private void OnEnable()
    {
        Debug.Log("[VipUI] OnEnable 호출");
        VipManager.OnVipDataChanged += RefreshUI;
        VipManager.OnFreeGiftClaimed += OnFreeGiftClaimedHandler;

        // 탭이 아직 생성 안 됐으면 재시도
        if (_tabButtons.Count == 0)
            BuildTabButtons();

        // 탭이 있으면 UI 갱신
        if (_tabButtons.Count > 0)
            RefreshUI();
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        VipManager.OnVipDataChanged -= RefreshUI;
        VipManager.OnFreeGiftClaimed -= OnFreeGiftClaimedHandler;
    }

    private bool _started = false;

    private void Start()
    {
        Debug.Log("[VipUI] Start 호출");
        _started = true;
        SetupButtons();
        BuildTabButtons();

        if (VipManager.Instance != null)
            _selectedTabVipLevel = Mathf.Max(1, VipManager.Instance.CurrentVipLevel);

        RefreshUI();


        // ★ 패널 닫기 — vipPanel과 자기 자신이 같은 오브젝트면 자기를 끄지 않음
        if (vipPanel != null && vipPanel != gameObject)
            vipPanel.SetActive(false);
    }

    // ─────────────────────────────────────────
    // 패널 열기 / 닫기
    // ─────────────────────────────────────────

    /// <summary>
    /// VIP 패널을 엽니다.
    /// 외부(버튼 등)에서 호출하세요.
    /// </summary>
    public void OpenPanel()
    {
        // ★ 튜토리얼 ClickFocusTarget 단계에서 VIP 패널 열기 차단
        if (TutorialManager.Instance != null && TutorialManager.Instance.ShouldBlockNonFocusButtons)
            return;

        // ★ 동시 클릭 방지 (동료 버튼과 VIP 버튼 겹침 대응)
        if (UIClickGuard.IsBlocked) return;
        UIClickGuard.Consume();

        Debug.Log("[VipUI] OpenPanel 호출");

        if (vipPanel == null) return;

        // VipUI 자신의 GO도 활성화 (코루틴 실행에 필요)
        gameObject.SetActive(true);
        vipPanel.SetActive(true);

        // Start가 아직 안 불렸으면 수동 초기화
        if (!_started)
        {
            Debug.Log("[VipUI] Start 미호출 → 수동 초기화");
            _started = true;
            SetupButtons();
        }

        // 탭이 없으면 생성
        if (_tabButtons.Count == 0)
        {
            Debug.Log("[VipUI] 탭 없음 → BuildTabButtons");
            BuildTabButtons();
        }

        // 내 등급 탭으로 자동 이동
        if (VipManager.Instance != null)
        {
            int myLevel = Mathf.Max(1, VipManager.Instance.CurrentVipLevel);
            SelectTab(myLevel);
        }

        RefreshUI();

        // ★ 만료 시간 실시간 갱신 시작
        if (_timerCoroutine != null)
            StopCoroutine(_timerCoroutine);
        _timerCoroutine = StartCoroutine(UpdateExpireTimeLoop());
    }

    /// <summary>VIP 패널을 닫습니다.</summary>
    public void ClosePanel()
    {
        // ★ 실시간 타이머 정지
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
        }

        if (vipPanel != null)
        {
            vipPanel.SetActive(false);
        }
    }

    // ─────────────────────────────────────────
    // 전체 UI 갱신
    // ─────────────────────────────────────────

    /// <summary>
    /// VipManager 데이터를 읽어서 전체 UI를 새로 그립니다.
    /// VipManager.OnVipDataChanged 이벤트 발생 시 자동 호출됩니다.
    /// </summary>
    private void RefreshUI()
    {
        if (VipManager.Instance == null) return;

        RefreshHeader();
        RefreshBenefitList(_selectedTabVipLevel);
        RefreshGiftSection(_selectedTabVipLevel);
        RefreshTabHighlight();
    }

    // ─────────────────────────────────────────
    // 헤더 갱신 (등급 뱃지 + 유효기한 + 경험치 바)
    // ─────────────────────────────────────────

    /// <summary>상단 헤더 영역 UI를 갱신합니다.</summary>
    private void RefreshHeader()
    {
        VipManager vm = VipManager.Instance;

        // VIP 등급 뱃지 이미지 — 선택된 탭 등급의 뱃지 표시
        if (vipLevelBadgeImage != null)
        {
            // ★ 선택 탭 등급 사용 (CurrentVipLevel=0이면 1로 폴백)
            int badgeLevel = _selectedTabVipLevel > 0 ? _selectedTabVipLevel : Mathf.Max(1, vm.CurrentVipLevel);
            VipData badgeData = vm.GetVipData(badgeLevel);
            if (badgeData != null && badgeData.badgeSprite != null)
            {
                vipLevelBadgeImage.sprite = badgeData.badgeSprite;
                vipLevelBadgeImage.color = Color.white;
            }
            else
            {
                vipLevelBadgeImage.sprite = null;
                vipLevelBadgeImage.color = badgeData != null && badgeData.gradeColor != default
                    ? badgeData.gradeColor
                    : Color.white;
            }
        }

        // VIP 등급 이름 (뱃지 옆) — 선택된 탭 등급 표시
        if (vipLevelNameText != null)
        {
            VipData selectedData = vm.GetVipData(_selectedTabVipLevel);
            if (selectedData != null && !string.IsNullOrEmpty(selectedData.displayName))
            {
                vipLevelNameText.text = selectedData.displayName;
                vipLevelNameText.color = selectedData.gradeColor;
            }
            else
            {
                vipLevelNameText.text = $"VIP{_selectedTabVipLevel}";
                vipLevelNameText.color = Color.white;
            }
        }

        // 유효기한
        if (expireTimeText != null)
        {
            string timeStr = vm.GetRemainingTimeString();
            expireTimeText.text = timeStr;
            expireTimeText.color = vm.IsVipActive() ? Color.white : Color.red;
        }

        // ★ 메인 화면 VIP 진입 버튼 아이콘 — VipData의 buttonIcon 우선, 없으면 badgeSprite
        if (vipEntryButtonIcon != null)
        {
            int entryLevel = Mathf.Max(1, vm.CurrentVipLevel);
            VipData entryData = vm.GetVipData(entryLevel);
            if (entryData != null)
            {
                Sprite icon = entryData.buttonIcon != null ? entryData.buttonIcon : entryData.badgeSprite;
                if (icon != null)
                {
                    vipEntryButtonIcon.sprite = icon;
                    vipEntryButtonIcon.color = Color.white;
                }
            }
        }

        // 경험치 바 + 텍스트
        RefreshExpBar();
    }

    /// <summary>경험치 바 애니메이션을 시작합니다.</summary>
    private void RefreshExpBar()
    {
        VipManager vm = VipManager.Instance;

        // 경험치 텍스트 업데이트
        if (vipExpText != null)
        {
            VipData nextData = vm.GetNextVipData();
            int maxExp = nextData != null ? nextData.requiredVipExp : vm.CurrentVipExp;
            vipExpText.text = $"{vm.CurrentVipExp}/{maxExp}";
        }

        // 경험치 바 애니메이션
        if (vipExpSlider != null)
        {
            float targetFill = vm.GetExpProgress();

            // 이미 실행 중인 코루틴 중단 후 새로 시작
            if (_expBarCoroutine != null)
                StopCoroutine(_expBarCoroutine);

            _expBarCoroutine = StartCoroutine(AnimateExpBar(targetFill));
        }
    }

    /// <summary>경험치 바를 부드럽게 채우는 코루틴</summary>
    private IEnumerator AnimateExpBar(float targetValue)
    {
        float startValue = vipExpSlider.value;
        float elapsed = 0f;
        float duration = Mathf.Abs(targetValue - startValue) / expFillSpeed + 0.1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            vipExpSlider.value = Mathf.Lerp(startValue, targetValue, elapsed / duration);
            yield return null;
        }

        vipExpSlider.value = targetValue;
    }

    // ─────────────────────────────────────────
    // 혜택 목록 갱신
    // ─────────────────────────────────────────

    /// <summary>
    /// 선택된 VIP 등급의 혜택 목록을 다시 그립니다.
    /// </summary>
    /// <param name="vipLevel">표시할 VIP 등급</param>
    private void RefreshBenefitList(int vipLevel)
    {
        if (benefitListParent == null || benefitItemPrefab == null) return;

        // 기존 항목 모두 제거
        foreach (GameObject item in _benefitItems)
        {
            Destroy(item);
        }
        _benefitItems.Clear();

        // VipData에서 혜택 목록 가져오기
        VipData data = VipManager.Instance?.GetVipData(vipLevel);
        if (data == null) return;

        // 혜택 항목 하나씩 생성
        foreach (VipBenefitData benefit in data.benefits)
        {
            GameObject item = Instantiate(benefitItemPrefab, benefitListParent);

            // 프리팹 안의 TextMeshPro에 텍스트 설정
            TextMeshProUGUI text = item.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = $"• {benefit.description}";
            }

            _benefitItems.Add(item);
        }
    }

    // ─────────────────────────────────────────
    // 선물 영역 갱신
    // ─────────────────────────────────────────

    /// <summary>무료/유료 선물 영역 UI를 갱신합니다.</summary>
    private void RefreshGiftSection(int vipLevel)
    {
        VipData data = VipManager.Instance?.GetVipData(vipLevel);
        if (data == null) return;

        // ── 무료 선물 ──
        if (freeGiftIcon != null && data.giftInfo.freeGiftIcon != null)
        {
            freeGiftIcon.sprite = data.giftInfo.freeGiftIcon;
        }

        // ★ 등급별 무료 선물 수령 체크
        bool isActive = VipManager.Instance.IsVipActive();
        bool canClaimLevel = vipLevel <= VipManager.Instance.CurrentVipLevel;
        bool isFreeClaimed = VipManager.Instance.IsFreeLevelClaimed(vipLevel);

        if (freeGiftClaimButton != null)
        {
            // 수령 가능: VIP 활성 + 해당 등급 이하 + 미수령
            bool canClaim = isActive && canClaimLevel && !isFreeClaimed;
            freeGiftClaimButton.gameObject.SetActive(!isFreeClaimed); // ★ 수령 완료 시 버튼 숨김
            freeGiftClaimButton.interactable = canClaim;
        }

        if (freeGiftButtonText != null)
        {
            if (!isActive)
                freeGiftButtonText.text = "기간만료";
            else if (!canClaimLevel)
                freeGiftButtonText.text = "등급부족";
            else
                freeGiftButtonText.text = "수령하기";
        }

        // ── 유료 선물 ──
        if (paidGiftIcon != null && data.giftInfo.paidGiftIcon != null)
        {
            paidGiftIcon.sprite = data.giftInfo.paidGiftIcon;
        }

        if (paidGiftDescText != null)
        {
            paidGiftDescText.text = $"{data.giftInfo.paidGiftOriginalValue:N0}다이아 상당";
        }

        if (paidGiftPriceText != null)
        {
            paidGiftPriceText.text = $"{data.giftInfo.paidGiftPrice:N0}다이아";
        }

        // 할인 뱃지
        if (discountBadge != null)
        {
            discountBadge.SetActive(!string.IsNullOrEmpty(data.giftInfo.discountPercent));
        }

        if (discountText != null)
        {
            discountText.text = data.giftInfo.discountPercent + " OFF";
        }

        // ★ 등급별 유료 구매 체크
        bool isPaidClaimed = VipManager.Instance.IsPaidLevelClaimed(vipLevel);
        if (paidGiftBuyButton != null)
        {
            bool canBuy = isActive && canClaimLevel && !isPaidClaimed;
            paidGiftBuyButton.gameObject.SetActive(!isPaidClaimed); // ★ 구매 완료 시 버튼 숨김
            paidGiftBuyButton.interactable = canBuy;
        }
    }

    // ─────────────────────────────────────────
    // 탭 버튼 생성 및 하이라이트
    // ─────────────────────────────────────────

    private bool _tabsBuilt = false;
    private Coroutine _retryCoroutine;

    /// <summary>
    /// VipManager의 전체 등급 수만큼 탭 버튼을 동적으로 생성합니다.
    /// </summary>
    private void BuildTabButtons()
    {
        if (tabButtonPrefab == null) return;

        int maxLevel = VipManager.Instance?.GetMaxVipLevel() ?? 0;

        if (maxLevel == 0)
        {
            if (_retryCoroutine == null && gameObject.activeInHierarchy)
                _retryCoroutine = StartCoroutine(RetryBuildTabButtons());
            return;
        }

        if (_tabsBuilt && _tabButtons.Count == maxLevel) return;

        // tabParent 폴백
        if (tabParent == null || !tabParent.gameObject.scene.isLoaded)
            tabParent = transform;

        // 가로 스크롤 활성화 — VIP 아이콘 위에서 드래그로 탭 이동 가능
        ScrollRect scrollRect = tabParent.GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = tabParent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.enabled = true;
        }

        // 실제 버튼 부모: Content
        Transform buttonParent = tabParent;
        var content = tabParent.Find("Viewport/Content");
        if (content != null)
            buttonParent = content;

        // Viewport stretch 보정
        Transform viewport = tabParent.Find("Viewport");
        RectTransform viewportRT = viewport != null ? viewport as RectTransform : null;
        if (viewportRT != null)
        {
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
        }

        // 기존 탭 전부 제거
        foreach (Button btn in _tabButtons)
            if (btn != null) Destroy(btn.gameObject);
        _tabButtons.Clear();

        // 탭 생성
        for (int i = 1; i <= maxLevel; i++)
        {
            int level = i;
            GameObject tabObj = Instantiate(tabButtonPrefab, buttonParent);
            Button tabBtn = tabObj.GetComponent<Button>();
            if (tabBtn == null) tabBtn = tabObj.AddComponent<Button>();

            Image btnImg = tabObj.GetComponent<Image>();
            if (btnImg == null) btnImg = tabObj.AddComponent<Image>();
            btnImg.raycastTarget = true;
            tabBtn.targetGraphic = btnImg;
            tabBtn.interactable = true;

            // ★ VipTabButton 컴포넌트로 아이콘 + 텍스트 설정
            VipData tabVipData = VipManager.Instance?.GetVipData(level);
            Sprite tabIcon = null;
            if (tabVipData != null)
                tabIcon = tabVipData.buttonIcon != null ? tabVipData.buttonIcon : tabVipData.badgeSprite;

            VipTabButton vipTab = tabObj.GetComponent<VipTabButton>();
            if (vipTab != null)
            {
                vipTab.Setup(level, tabIcon);
            }
            else
            {
                // VipTabButton 컴포넌트 없으면 기존 방식 폴백
                TextMeshProUGUI tabText = tabObj.GetComponentInChildren<TextMeshProUGUI>();
                if (tabText != null)
                    tabText.text = $"VIP{level}";
            }

            tabBtn.onClick.RemoveAllListeners();
            tabBtn.onClick.AddListener(() =>
            {
                Debug.Log($"[VipUI] 탭 버튼 클릭: VIP{level}");
                SelectTab(level);
            });

            _tabButtons.Add(tabBtn);
        }

        selectedTabColor = new Color(1f, 0.85f, 0.2f);
        _tabsBuilt = true;
        RefreshTabHighlight();

        Debug.Log($"[VipUI] BuildTabButtons 완료: {_tabButtons.Count}개 탭");
    }


    /// <summary>
    /// 탭을 선택합니다. 선택된 탭의 색상을 변경하고 해당 등급 UI를 표시합니다.
    /// </summary>
    /// <param name="vipLevel">선택할 VIP 등급</param>
    private void SelectTab(int vipLevel)
    {
        _selectedTabVipLevel = vipLevel;
        Debug.Log($"[VipUI] 탭 선택: VIP{vipLevel}");

        // 헤더 등급 이름 갱신
        RefreshHeader();

        // 혜택 목록 + 선물 영역 갱신
        RefreshBenefitList(vipLevel);
        RefreshGiftSection(vipLevel);

        // 탭 하이라이트 갱신
        RefreshTabHighlight();

        SoundManager.Instance?.PlayButtonClick();
    }

    /// <summary>현재 선택된 탭만 하이라이트 색상으로 표시합니다.</summary>
    private void RefreshTabHighlight()
    {
        for (int i = 0; i < _tabButtons.Count; i++)
        {
            if (_tabButtons[i] == null) continue;

            bool isSelected = (i + 1) == _selectedTabVipLevel;

            // Image 색상 직접 변경 (즉시 반영)
            Image btnImg = _tabButtons[i].GetComponent<Image>();
            if (btnImg != null)
                btnImg.color = isSelected ? selectedTabColor : normalTabColor;

            // 텍스트 색상도 변경
            TextMeshProUGUI btnText = _tabButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
                btnText.color = isSelected ? Color.white : new Color(0.7f, 0.7f, 0.7f);
        }
    }

    // ─────────────────────────────────────────
    // 버튼 이벤트 연결
    // ─────────────────────────────────────────

    private void SetupButtons()
    {
        // 닫기 버튼
        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);

        // 무료 선물 수령 버튼
        if (freeGiftClaimButton != null)
        {
            freeGiftClaimButton.onClick.AddListener(() =>
            {
                VipManager.Instance?.ClaimFreeGift(_selectedTabVipLevel);
            });
        }

        // 유료 선물 구매 버튼
        if (paidGiftBuyButton != null)
        {
            paidGiftBuyButton.onClick.AddListener(() =>
            {
                VipManager.Instance?.PurchaseVipGift(_selectedTabVipLevel);
            });
        }

        // 기간 연장 버튼 (30일, 500 다이아)
        if (extendButton != null)
        {
            extendButton.onClick.AddListener(() =>
            {
                VipManager.Instance?.ExtendVipDuration(30, 500);
            });
        }
    }

    // ─────────────────────────────────────────
    // 이벤트 핸들러
    // ─────────────────────────────────────────

    /// <summary>무료 선물 수령 완료 시 호출 (버튼 상태 즉시 갱신)</summary>
    private void OnFreeGiftClaimedHandler()
    {
        RefreshGiftSection(_selectedTabVipLevel);
    }

    /// <summary>만료 시간을 매초 실시간 갱신하는 코루틴</summary>
    private IEnumerator UpdateExpireTimeLoop()
    {
        while (vipPanel != null && vipPanel.activeSelf)
        {
            if (VipManager.Instance != null && expireTimeText != null)
            {
                string timeStr = VipManager.Instance.GetRemainingTimeString();
                expireTimeText.text = timeStr;
                expireTimeText.color = VipManager.Instance.IsVipActive() ? Color.white : Color.red;
            }
            yield return new WaitForSecondsRealtime(1f);
        }
        _timerCoroutine = null;
    }

    /// <summary>VipManager 준비 안 됐을 때 대기 후 탭 생성 재시도 (1회만)</summary>
    private IEnumerator RetryBuildTabButtons()
    {
        for (int i = 0; i < 60; i++)
        {
            yield return null;
            if (_tabsBuilt) { _retryCoroutine = null; yield break; } // 이미 빌드됨
            if (VipManager.Instance != null && VipManager.Instance.GetMaxVipLevel() > 0)
            {
                Debug.Log($"[VipUI] RetryBuildTabButtons 성공 ({i + 1}프레임 후)");
                _retryCoroutine = null;
                BuildTabButtons();
                RefreshUI();
                yield break;
            }
        }
        _retryCoroutine = null;
        Debug.LogWarning("[VipUI] RetryBuildTabButtons 타임아웃");
    }
}