using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// CompanionGachaManager — 동료 가챠 시스템 (탭 기반 소환 UI)
///
/// [구성]
///   - Inspector SummonTabData[] 배열로 탭 데이터 관리
///   - 상단 탭 바 (가로 스크롤) + 중앙 배너 + 하단 구매 버튼
///   - 좌우 스와이프로 탭 전환
///   - 뽑기 연출 (VideoPlayer, LegendaryRevealGO) 기존 로직 유지
///
/// [화폐]
///   - 다이아: GameManager.SpendGem() → GameDataBridge 폴백
///   - 동료티켓: ResourceBarManager.SpendCompanionTickets()
/// </summary>
public class CompanionGachaManager : MonoBehaviour
{
    public static CompanionGachaManager Instance;

    // ═══════════════════════════════════════════════════════════════
    //  소환 탭 데이터 (Inspector 설정)
    // ═══════════════════════════════════════════════════════════════

    [Header("소환 탭 데이터")]
    public SummonTabData[] summonTabs;

    // 기존 호환용 — summonTabs가 비어있으면 이 풀 사용
    [Header("레거시: 동료 가챠 풀 (탭 미설정 시 폴백)")]
    public List<CompanionData> companionPool = new List<CompanionData>();

    [Header("레거시: 가챠 비용")]
    public int singlePullCost = 1;
    public int tenPullCost = 10;

    [Header("VIP 경험치 보상")]
    [Tooltip("10연차 완료 시 VIP 경험치")]
    public int vipExpPerTenPull = 50;

    // ═══════════════════════════════════════════════════════════════
    //  UI 참조 (Inspector 연결)
    // ═══════════════════════════════════════════════════════════════

    [Header("동료 가챠 패널")]
    public GameObject companionGachaPanel;
    public Button singlePullBtn;
    public Button tenPullBtn;
    public Button closePanelBtn;
    public TextMeshProUGUI ticketCountText;

    [Header("결과 화면")]
    public GameObject resultPanel;
    public Transform resultGrid;                    // 좌측: 뽑기 슬롯 목록
    public GameObject resultItemPrefab;
    public Button resultCloseBtn;
    public GameObject resultBackground;

    [Header("다시뽑기 버튼 (선택)")]
    [Tooltip("결과창의 1회 다시뽑기 버튼")]
    public Button rePullSingleBtn;
    [Tooltip("결과창의 10회 다시뽑기 버튼")]
    public Button rePullTenBtn;

    [Header("상세 팝업 (resultPanel 자식, 최상단 SortOrder)")]
    [Tooltip("resultPanel 자식으로 배치. 슬롯 클릭 시 표시, 아무 곳 터치 시 닫힘")]
    public GameObject detailOverlay;                // 상세 팝업 루트 GO
    public GameObject detailBackground;             // 반투명 배경 (Raycast Target = true, 터치로 닫기)
    public Image detailPortrait;
    public TextMeshProUGUI detailNameText;
    public TextMeshProUGUI detailRarityText;
    public TextMeshProUGUI detailDescText;
    public TextMeshProUGUI detailStatsText;
    public TextMeshProUGUI detailStarsText;

    // ═══════════════════════════════════════════════════════════════
    //  ★ 전설 캐릭터 뽑기 연출 (수정 금지 영역)
    // ═══════════════════════════════════════════════════════════════

    [Header("★ 가챠 연출 카메라")]
    [Tooltip("가챠 연출 전용 카메라 (Clear Flags: Solid Color, Culling Mask: Nothing). 없으면 자동 생성")]
    public Camera gachaCamera;

    [Header("★ 전설 연출 패널")]
    public GameObject legendaryAnimPanel;
    public VideoPlayer legendaryVideoPlayer;
    public GameObject legendaryRevealGO;
    public TextMeshProUGUI legendaryRevealText;
    public Image legendaryRevealPortrait;
    public GameObject tapToContinueGO;

    private List<CompanionData> pendingLegendaryResults;
    private bool isLegendaryAnimPlaying = false;
    private bool isWaitingForTap = false;

    private int activeTabIndex = 0;

    // ── 내부 ──
    private CompanionData selectedCompanion;

    private readonly Color[] rarityColors =
    {
        Color.gray,
        new Color(0.3f, 0.5f, 1f),
        new Color(0.7f, 0.2f, 1f),
        new Color(1f, 0.8f, 0.1f)
    };
    private readonly string[] rarityNames = { "일반", "희귀", "에픽", "전설" };

    // ═══════════════════════════════════════════════════════════════
    //  초기화
    // ═══════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        Debug.Log("[ManagerInit] CompanionGachaManager가 생성되었습니다.");
    }

    void Start()
    {
        SetupUI();
        SetupLegendarySystem();
        CloseAll();
    }

    void Update()
    {
        // ★ 전설 등장 화면에서 탭 대기 중일 때
        if (isWaitingForTap && (Input.GetMouseButtonDown(0) || Input.touchCount > 0))
        {
            isWaitingForTap = false;
            OnLegendaryTapToContinue();
        }
    }

    private void SetupUI()
    {
        // 기존 버튼은 탭 UI 미생성 시 폴백으로 동작
        if (singlePullBtn != null) singlePullBtn.onClick.AddListener(PerformSinglePull);
        if (tenPullBtn != null) tenPullBtn.onClick.AddListener(PerformTenPull);
        if (closePanelBtn != null) closePanelBtn.onClick.AddListener(CloseGachaPanel);
        if (resultCloseBtn != null) resultCloseBtn.onClick.AddListener(CloseResultPanel);

        // ★ 결과창 다시뽑기 버튼 — 티켓 부족 시 SpendCompanionTickets()가 ConfirmDialog 표시
        if (rePullSingleBtn != null)
        {
            rePullSingleBtn.onClick.RemoveAllListeners();
            rePullSingleBtn.onClick.AddListener(() =>
            {
                SoundManager.Instance?.PlayButtonClick();
                PerformSinglePull();
            });
        }
        if (rePullTenBtn != null)
        {
            rePullTenBtn.onClick.RemoveAllListeners();
            rePullTenBtn.onClick.AddListener(() =>
            {
                SoundManager.Instance?.PlayButtonClick();
                PerformTenPull();
            });
        }

        if (resultBackground != null)
        {
            Button bgBtn = resultBackground.GetComponent<Button>()
                           ?? resultBackground.AddComponent<Button>();
            bgBtn.onClick.AddListener(CloseResultPanel);
        }

        // 상세 오버레이: 배경 터치로 닫기
        if (detailBackground != null)
        {
            Button bgBtn = detailBackground.GetComponent<Button>()
                           ?? detailBackground.AddComponent<Button>();
            bgBtn.onClick.AddListener(CloseDetailOverlay);
        }
        if (detailOverlay != null)
            detailOverlay.SetActive(false);
    }

    private void CloseAll()
    {
        if (companionGachaPanel != null) companionGachaPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (detailOverlay != null) detailOverlay.SetActive(false);
        if (legendaryAnimPanel != null) legendaryAnimPanel.SetActive(false);

        // 전설 연출 중이었으면 카메라 복원
        if (isLegendaryAnimPlaying || isWaitingForTap)
        {
            isLegendaryAnimPlaying = false;
            isWaitingForTap = false;
            ShowMainUI();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  패널 열기 / 닫기
    // ═══════════════════════════════════════════════════════════════

    public void OpenGachaPanel()
    {
        // ★ 튜토리얼 중 차단 (동료뽑기 관련 포커스 단계는 허용)
        if (TutorialManager.Instance != null && TutorialManager.Instance.ShouldBlockNonFocusButtons)
        {
            var step = TutorialManager.Instance.GetCurrentStep();
            string fn = step?.focusTargetName ?? "";
            bool isCompanionStep = fn == "CompanionGachaBtn" || fn == "CompanionSinglePullBtn"
                                || fn == "CompanionResultCloseBtn";
            if (!isCompanionStep && !TutorialManager.Instance.IsCurrentFocusTarget(companionGachaPanel))
            {
                Debug.Log("[CompanionGacha] 튜토리얼 중 차단");
                return;
            }
        }

        // ★ 동시 클릭 방지 (VIP 버튼과 겹침 대응)
        if (UIClickGuard.IsBlocked) return;
        UIClickGuard.Consume();
        if (companionGachaPanel == null) return;

        companionGachaPanel.SetActive(true);
        RefreshTicketUI();
        Debug.Log("[CompanionGachaManager] 동료 가챠 패널 열기");
    }

    public void CloseGachaPanel()
    {
        if (companionGachaPanel != null) companionGachaPanel.SetActive(false);
    }

    private Camera _mainCamera;
    private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();

    /// <summary>
    /// 전설 연출: 메인 카메라 + 게임 UI 캔버스 전부 숨기고 가챠 카메라만 켜기
    /// (Screen Space - Overlay 캔버스는 카메라와 무관하게 렌더링되므로 직접 꺼야 함)
    /// </summary>
    private void HideMainUI()
    {
        _mainCamera = Camera.main;

        // 가챠 카메라 없으면 자동 생성
        if (gachaCamera == null)
        {
            var go = new GameObject("GachaCamera_Auto");
            go.transform.SetParent(transform);
            gachaCamera = go.AddComponent<Camera>();
            gachaCamera.clearFlags = CameraClearFlags.SolidColor;
            gachaCamera.backgroundColor = Color.black;
            gachaCamera.cullingMask = 0;
            gachaCamera.depth = -100;
            gachaCamera.enabled = false;
        }

        // 메인 카메라 끄기
        if (_mainCamera != null) _mainCamera.enabled = false;
        gachaCamera.enabled = true;

        // 게임 UI 캔버스 전부 숨기기 (전설 연출 패널의 루트 캔버스만 유지)
        _hiddenCanvases.Clear();
        Canvas legendaryRootCanvas = legendaryAnimPanel != null
            ? legendaryAnimPanel.GetComponentInParent<Canvas>()
            : null;

        foreach (var canvas in FindObjectsOfType<Canvas>())
        {
            if (canvas == null || !canvas.enabled) continue;
            // 전설 연출 패널의 루트 캔버스만 유지
            if (legendaryRootCanvas != null && canvas == legendaryRootCanvas) continue;

            canvas.enabled = false;
            _hiddenCanvases.Add(canvas);
        }

        // VideoPlayer는 EnsureVideoRendersToRawImage()에서 RenderTexture 모드로 전환됨
    }

    /// <summary>메인 카메라 + 게임 UI 캔버스 복원</summary>
    private void ShowMainUI()
    {
        // 가챠 카메라 끄기
        if (gachaCamera != null) gachaCamera.enabled = false;

        // 메인 카메라 복원
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            foreach (var cam in Camera.allCameras)
            {
                if (cam != gachaCamera) { _mainCamera = cam; break; }
            }
            if (_mainCamera == null)
                _mainCamera = FindObjectOfType<Camera>();
        }
        if (_mainCamera != null) _mainCamera.enabled = true;

        // 숨겼던 캔버스 복원
        foreach (var canvas in _hiddenCanvases)
        {
            if (canvas != null) canvas.enabled = true;
        }
        _hiddenCanvases.Clear();

        // VideoPlayer 카메라 원복
        if (legendaryVideoPlayer != null && _mainCamera != null)
            legendaryVideoPlayer.targetCamera = _mainCamera;
    }


    // ═══════════════════════════════════════════════════════════════
    //  구매 처리
    // ═══════════════════════════════════════════════════════════════

    /// <summary>탭 기반 구매 버튼 클릭</summary>
    private void OnPurchaseClicked(int count, bool useTicket)
    {
        if (!(summonTabs != null && summonTabs.Length > 0)) return;
        SummonTabData tab = summonTabs[activeTabIndex];
        if (tab == null) return;

        // 비용 계산
        int cost;
        if (count == 1)
            cost = useTicket ? tab.singleCostTicket : tab.singleCostDia;
        else
            cost = useTicket ? tab.multiCostTicket : tab.multiCostDia;

        if (cost <= 0)
        {
            UIManager.Instance?.ShowMessage("이 결제 수단은 사용할 수 없습니다!", Color.yellow);
            return;
        }

        // 화폐 차감
        if (useTicket)
        {
            if (!SpendCompanionTickets(cost)) return;
        }
        else
        {
            if (!SpendDiamond(cost)) return;
        }

        // 풀 교체 → 뽑기 실행
        List<CompanionData> pool = GetActivePool();
        if (pool == null || pool.Count == 0)
        {
            UIManager.Instance?.ShowMessage("뽑기 풀이 비어있습니다!", Color.red);
            return;
        }

        // 임시로 companionPool 교체 후 기존 Draw 로직 사용
        var backupPool = companionPool;
        companionPool = pool;

        if (count == 1)
            PerformSinglePull_Internal();
        else
            PerformTenPull_Internal();

        companionPool = backupPool;
        RefreshTicketUI();
    }

    /// <summary>현재 활성 탭의 동료 풀</summary>
    private List<CompanionData> GetActivePool()
    {
        if ((summonTabs != null && summonTabs.Length > 0) && activeTabIndex < summonTabs.Length)
        {
            var tab = summonTabs[activeTabIndex];
            if (tab?.companionPool != null && tab.companionPool.Length > 0)
                return new List<CompanionData>(tab.companionPool);
        }
        return companionPool;
    }

    // ═══════════════════════════════════════════════════════════════
    //  화폐 차감
    // ═══════════════════════════════════════════════════════════════

    /// <summary>다이아(젬) 차감 — GameManager → GameDataBridge 폴백</summary>
    private bool SpendDiamond(int amount)
    {
        if (GameManager.Instance != null)
        {
            if (!GameManager.Instance.SpendGem(amount))
            {
                UIManager.Instance?.ShowMessage("재화가 부족합니다!", Color.red);
                return false;
            }
            return true;
        }

        // GameDataBridge 폴백
        if (GameDataBridge.CurrentData != null && GameDataBridge.CurrentData.playerGem >= amount)
        {
            GameDataBridge.CurrentData.playerGem -= amount;
            return true;
        }

        UIManager.Instance?.ShowMessage("재화가 부족합니다!", Color.red);
        return false;
    }

    /// <summary>동료 뽑기 티켓 차감 — 부족 시 ConfirmDialog 팝업</summary>
    private bool SpendCompanionTickets(int amount)
    {
        if (ResourceBarManager.Instance != null)
        {
            if (!ResourceBarManager.Instance.SpendCompanionTickets(amount))
            {
                int held = ResourceBarManager.Instance.companionTickets;
                ShowCompanionTicketShortageDialog(amount, held);
                return false;
            }
            RefreshTicketUI();
            return true;
        }

        // GameDataBridge 폴백
        int bridgeHeld = GameDataBridge.CurrentData?.companionTickets ?? 0;
        if (GameDataBridge.CurrentData != null && bridgeHeld >= amount)
        {
            GameDataBridge.CurrentData.companionTickets -= amount;
            RefreshTicketUI();
            return true;
        }

        ShowCompanionTicketShortageDialog(amount, bridgeHeld);
        return false;
    }

    private void ShowCompanionTicketShortageDialog(int needed, int held)
    {
        UIManager.Instance?.ShowConfirmDialog(
            $"동료티켓이부족합니다.\n필요:{needed}개\n보유:{held}개",
            onConfirm: null);
    }

    // ═══════════════════════════════════════════════════════════════
    //  기존 호환: 레거시 뽑기 (Inspector singlePullBtn/tenPullBtn용)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>기존 1회 뽑기 (티켓 기반, 레거시)</summary>
    public void PerformSinglePull()
    {
        if ((summonTabs != null && summonTabs.Length > 0))
        {
            // 탭 기반이면 첫 번째 탭의 티켓 비용으로 처리
            OnPurchaseClicked(1, true);
            return;
        }

        if (!SpendTicketsLegacy(singlePullCost)) return;
        PerformSinglePull_Internal();
    }

    /// <summary>기존 10회 뽑기 (티켓 기반, 레거시)</summary>
    public void PerformTenPull()
    {
        if ((summonTabs != null && summonTabs.Length > 0))
        {
            OnPurchaseClicked(10, true);
            return;
        }

        if (!SpendTicketsLegacy(tenPullCost)) return;
        PerformTenPull_Internal();
    }

    // ═══════════════════════════════════════════════════════════════
    //  뽑기 실행 (내부)
    // ═══════════════════════════════════════════════════════════════

    private void PerformSinglePull_Internal()
    {
        CompanionData result = DrawCompanion();
        if (result == null)
        {
            Debug.LogError("[CompanionGacha] ★ DrawCompanion 결과 null! companionPool 비어있음?");
            return;
        }

        Debug.Log($"[CompanionGacha] ★ 1회 뽑기 결과: {result.companionName} ({result.rarity})");

        if (CompanionInventoryManager.Instance != null)
        {
            CompanionInventoryManager.Instance.AddCompanion(result);
            Debug.Log($"[CompanionGacha] ★ 인벤토리 추가 완료. 현재 보유: {CompanionInventoryManager.Instance.GetSaveData()?.Length ?? 0}종");
        }
        else
        {
            Debug.LogError("[CompanionGacha] ★ CompanionInventoryManager.Instance가 null! 씬에 배치되어 있는지 확인");
        }

        SaveLoadManager.Instance?.SaveGame();

        if (ShouldPlayLegendaryAnimation(result))
            PlayLegendaryAnimation(new List<CompanionData> { result });
        else
            ShowResult(new List<CompanionData> { result });
    }

    private void PerformTenPull_Internal()
    {
        List<CompanionData> results = new List<CompanionData>();

        if (CompanionInventoryManager.Instance == null)
            Debug.LogError("[CompanionGacha] ★ CompanionInventoryManager.Instance가 null! 10연차 인벤 추가 불가");

        for (int i = 0; i < 10; i++)
        {
            CompanionData r = DrawCompanion();
            if (r == null) { Debug.LogWarning($"[CompanionGacha] ★ 10연차 {i}번째 Draw null"); continue; }
            results.Add(r);
            CompanionInventoryManager.Instance?.AddCompanion(r);
        }

        Debug.Log($"[CompanionGacha] ★ 10연차 완료: {results.Count}개 뽑힘, 인벤 보유: {CompanionInventoryManager.Instance?.GetSaveData()?.Length ?? 0}종");

        // ★ VIP 경험치 지급
        if (vipExpPerTenPull > 0)
            VipManager.Instance?.AddVipExp(vipExpPerTenPull);

        SaveLoadManager.Instance?.SaveGame();

        CompanionData firstLegendary = results.Find(c => ShouldPlayLegendaryAnimation(c));
        if (firstLegendary != null)
            PlayLegendaryAnimation(results, firstLegendary);
        else
            ShowResult(results);

        Debug.Log($"[CompanionGachaManager] 10회 완료 ({results.Count}개)");
    }

    // ═══════════════════════════════════════════════════════════════
    //  확률 기반 추첨
    // ═══════════════════════════════════════════════════════════════

    private CompanionData DrawCompanion()
    {
        if (companionPool == null || companionPool.Count == 0)
        {
            UIManager.Instance?.ShowMessage("동료 풀이 비어있습니다!", Color.red);
            return null;
        }

        float total = 0f;
        foreach (var c in companionPool)
            if (c != null) total += c.probability;

        if (total <= 0f)
            return companionPool[Random.Range(0, companionPool.Count)];

        float roll = Random.Range(0f, total);
        float cumul = 0f;

        foreach (var c in companionPool)
        {
            if (c == null) continue;
            cumul += c.probability;
            if (roll <= cumul) return c;
        }

        return companionPool[companionPool.Count - 1];
    }

    // ═══════════════════════════════════════════════════════════════
    //  레거시 티켓 소모
    // ═══════════════════════════════════════════════════════════════

    private bool SpendTicketsLegacy(int cost)
    {
        if (ResourceBarManager.Instance == null)
        {
            Debug.LogError("[CompanionGachaManager] ResourceBarManager 없음!");
            return false;
        }

        if (!ResourceBarManager.Instance.SpendCompanionTickets(cost))
        {
            ShowCompanionTicketShortageDialog(cost, ResourceBarManager.Instance.companionTickets);
            return false;
        }

        RefreshTicketUI();
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  티켓 UI 갱신
    // ═══════════════════════════════════════════════════════════════

    private void RefreshTicketUI()
    {
        if (ticketCountText == null) return;

        int tickets = 0;
        if (ResourceBarManager.Instance != null)
            tickets = ResourceBarManager.Instance.companionTickets;
        else if (GameDataBridge.CurrentData != null)
            tickets = GameDataBridge.CurrentData.companionTickets;

        ticketCountText.text = $"티켓: {tickets}";
    }

    // ═══════════════════════════════════════════════════════════════
    //  결과 화면
    // ═══════════════════════════════════════════════════════════════

    private void ShowResult(List<CompanionData> results)
    {
        Debug.Log($"[CompanionGacha] ★ ShowResult 호출 — results: {results?.Count ?? 0}개, resultPanel: {(resultPanel != null ? "O" : "NULL")}, resultGrid: {(resultGrid != null ? "O" : "NULL")}, resultItemPrefab: {(resultItemPrefab != null ? "O" : "NULL")}");

        if (resultPanel == null || resultGrid == null) return;

        // ★ 반드시 패널 먼저 활성화 (자식 코루틴이 동작하려면 부모가 active여야 함)
        CloseGachaPanel();
        if (detailOverlay != null) detailOverlay.SetActive(false);
        resultPanel.SetActive(true);

        foreach (Transform child in resultGrid)
            Destroy(child.gameObject);

        // ★ 개별 슬롯 생성 (그룹핑 없음)
        float staggerDelay = 0.08f;
        for (int i = 0; i < results.Count; i++)
        {
            var companion = results[i];
            if (companion == null || resultItemPrefab == null) continue;

            GameObject go = Instantiate(resultItemPrefab, resultGrid, false);
            go.SetActive(true);
            CompanionResultItem item = go.GetComponent<CompanionResultItem>()
                                      ?? go.AddComponent<CompanionResultItem>();
            item.Setup(companion, this, staggerDelay * i, 1);
        }

        // resultGrid에 GridLayoutGroup 없으면 자동 추가
        if (resultGrid.GetComponent<GridLayoutGroup>() == null
            && resultGrid.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() == null
            && resultGrid.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>() == null)
        {
            var grid = resultGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(160f, 200f);
            grid.spacing = new Vector2(10f, 10f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperCenter;
            Debug.Log("[CompanionGacha] ★ GridLayoutGroup 자동 추가");
        }

        // 레이아웃 강제 리빌드
        LayoutRebuilder.ForceRebuildLayoutImmediate(resultGrid as RectTransform);
    }

    public void CloseResultPanel()
    {
        CloseDetailOverlay();
        if (resultPanel != null) resultPanel.SetActive(false);

        // 저장
        Debug.Log("[CompanionGacha] ★ CloseResultPanel — SaveGame 호출");
        SaveLoadManager.Instance?.SaveGame();

        // 메인 UI 복원
        ShowMainUI();

        // ★ 튜토리얼 중: 인벤토리 상태 건드리지 않음 (TutorialManager가 제어)
        bool tutorialActive = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;
        if (!tutorialActive)
        {
            // 동료탭 자동 전환
            if (InventoryManager.Instance != null)
            {
                Debug.Log("[CompanionGacha] ★ 동료탭 자동 전환 시도");
                InventoryManager.Instance.SelectTab(InventoryManager.InvenTabType.Companion);
            }
        }
        else
        {
            // ★ 튜토리얼 중: 인벤토리 갱신만 (열기/닫기/탭전환 안 함)
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.ForceRefreshAll();
            Debug.Log("[CompanionGacha] ★ 튜토리얼 중 — 인벤토리 갱신만, 탭전환 안 함");
        }

        // 튜토리얼 트리거
        TutorialManager.Instance?.OnActionCompleted("CompanionGachaComplete");
        TutorialManager.Instance?.OnActionCompleted("CompanionResultClosed");
    }

    // ═══════════════════════════════════════════════════════════════
    //  상세 팝업
    // ═══════════════════════════════════════════════════════════════

    /// <summary>결과 슬롯 클릭 → 상세 오버레이 표시 (화면 아무 곳 터치로 닫힘)</summary>
    public void ShowDetailPopup(CompanionData data)
    {
        if (data == null) return;

        selectedCompanion = data;

        if (detailPortrait != null)
        {
            detailPortrait.sprite = data.portrait;
            detailPortrait.color = Color.white;
        }
        if (detailNameText != null) detailNameText.text = data.companionName;

        int ri = (int)data.rarity;

        if (detailRarityText != null)
        {
            detailRarityText.text = ri < rarityNames.Length ? rarityNames[ri] : data.rarity.ToString();
            detailRarityText.color = ri < rarityColors.Length ? rarityColors[ri] : Color.white;
        }

        if (detailDescText != null) detailDescText.text = data.description;

        if (detailStatsText != null)
        {
            detailStatsText.text =
                $"공격력  : {data.attackPower}\n" +
                $"공격속도: {data.attackSpeed}/s\n" +
                $"사거리  : {data.attackRange}m\n" +
                $"이동속도: {data.moveSpeed}";
        }

        if (detailStarsText != null)
        {
            int stars = data.baseStars;
            detailStarsText.text = StarSpriteUtil.GetColoredStars(Mathf.Max(1, stars));
            detailStarsText.color = StarSpriteUtil.GetStarColor(stars);
        }

        // 오버레이 표시 (최상단)
        if (detailOverlay != null)
        {
            detailOverlay.SetActive(true);
            detailOverlay.transform.SetAsLastSibling();
        }
    }

    /// <summary>상세 오버레이 닫기 (아무 곳 터치 시)</summary>
    public void CloseDetailOverlay()
    {
        if (detailOverlay != null) detailOverlay.SetActive(false);
        selectedCompanion = null;
    }


    // ═══════════════════════════════════════════════════════════════
    //  ★ 전설 캐릭터 뽑기 연출 (수정 금지)
    // ═══════════════════════════════════════════════════════════════

    private void SetupLegendarySystem()
    {
        if (legendaryVideoPlayer != null)
        {
            legendaryVideoPlayer.playOnAwake = false;
            legendaryVideoPlayer.isLooping = false;
            legendaryVideoPlayer.loopPointReached += OnLegendaryVideoFinished;
        }

        if (legendaryAnimPanel != null) legendaryAnimPanel.SetActive(false);
        if (legendaryRevealGO != null) legendaryRevealGO.SetActive(false);
        if (tapToContinueGO != null) tapToContinueGO.SetActive(false);
    }

    private RenderTexture _videoRT;

    /// <summary>
    /// VideoPlayer를 RenderTexture 모드로 전환하여 RawImage에 출력.
    /// CameraFarPlane 모드는 Screen Space Overlay Canvas 뒤에서 렌더링되어 안 보임.
    /// </summary>
    private void EnsureVideoRendersToRawImage()
    {
        if (legendaryVideoPlayer == null) return;

        // legendaryAnimPanel 아래의 RawImage 찾기
        UnityEngine.UI.RawImage rawImg = legendaryAnimPanel != null
            ? legendaryAnimPanel.GetComponentInChildren<UnityEngine.UI.RawImage>(true)
            : null;

        if (rawImg == null)
        {
            Debug.LogWarning("[CompanionGacha] RawImage를 찾을 수 없음 — 비디오 렌더링 불가");
            return;
        }

        // RenderTexture 생성 (해상도는 화면 크기 기준)
        if (_videoRT == null || _videoRT.width != Screen.width || _videoRT.height != Screen.height)
        {
            if (_videoRT != null) _videoRT.Release();
            _videoRT = new RenderTexture(Screen.width, Screen.height, 0);
            _videoRT.Create();
        }

        // VideoPlayer → RenderTexture 모드로 전환
        legendaryVideoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
        legendaryVideoPlayer.targetTexture = _videoRT;

        // RawImage에 연결
        rawImg.texture = _videoRT;
        rawImg.gameObject.SetActive(true);

        Debug.Log($"[CompanionGacha] VideoPlayer → RenderTexture({_videoRT.width}x{_videoRT.height}) → RawImage 연결 완료");
    }

    private bool ShouldPlayLegendaryAnimation(CompanionData data)
    {
        if (data == null) return false;
        if (data.rarity != CompanionRarity.Legendary) return false;
        if (data.legendaryVideoClip == null) return false;
        if (legendaryAnimPanel == null) return false;
        if (legendaryVideoPlayer == null) return false;
        return true;
    }

    private void PlayLegendaryAnimation(List<CompanionData> results, CompanionData highlightTarget = null)
    {
        // ★ 이전 전설 연출 잔재 정리 (다시뽑기/연뽑 시 이전 연출이 남는 버그 방지)
        if (isLegendaryAnimPlaying || isWaitingForTap)
            CleanupLegendaryState();

        pendingLegendaryResults = results;
        isLegendaryAnimPlaying = true;
        isWaitingForTap = false;

        CompanionData target = highlightTarget;
        if (target == null)
            target = results.Find(c => ShouldPlayLegendaryAnimation(c));

        if (target == null)
        {
            ShowResult(results);
            return;
        }

        CloseGachaPanel();
        HideMainUI();  // 전설 연출 시에만 월드+UI 숨김

        // ★ 결과창이 열려있으면 (다시뽑기 케이스) 숨김 — 전설 연출 뒤에 비치는 것 방지
        if (resultPanel != null && resultPanel.activeSelf)
            resultPanel.SetActive(false);

        // 장비 가챠 애니메이션 패널이 켜져있으면 끄기 (검정 화면 가림 방지)
        if (GachaManager.Instance != null && GachaManager.Instance.gachaAnimationPanel != null)
            GachaManager.Instance.gachaAnimationPanel.SetActive(false);

        legendaryAnimPanel.SetActive(true);
        legendaryAnimPanel.transform.SetAsLastSibling(); // 최상단 표시

        if (legendaryRevealGO != null) legendaryRevealGO.SetActive(false);
        if (tapToContinueGO != null) tapToContinueGO.SetActive(false);

        if (legendaryRevealText != null)
            legendaryRevealText.text = $"{target.companionName}\n<size=80%>전설 등장!</size>";
        if (legendaryRevealPortrait != null && target.portrait != null)
        {
            legendaryRevealPortrait.sprite = target.portrait;
            legendaryRevealPortrait.color = Color.white;
        }

        // VideoPlayer → RenderTexture → RawImage 파이프라인 설정
        // CameraFarPlane 모드는 UI Canvas(Overlay) 뒤에 렌더링되어 안 보임
        EnsureVideoRendersToRawImage();

        legendaryVideoPlayer.clip = target.legendaryVideoClip;
        legendaryVideoPlayer.time = 0;
        legendaryVideoPlayer.Play();

        SoundManager.Instance?.PlaySFX("GachaLegendary");

        Debug.Log($"[CompanionGachaManager] ★ 전설 연출 시작: {target.companionName} " +
                  $"({target.legendaryVideoClip.name}, {target.legendaryVideoClip.length:F1}초)");
    }

    private void OnLegendaryVideoFinished(VideoPlayer vp)
    {
        if (vp != legendaryVideoPlayer) return;
        if (!isLegendaryAnimPlaying) return;

        Debug.Log("[CompanionGachaManager] ★ 전설 영상 재생 완료 → 비디오 끄고 등장 연출 표시");
        isLegendaryAnimPlaying = false;

        // 1단계: 비디오(RawImage) 끄기 + 텍스처 제거 (잔상 방지)
        if (legendaryVideoPlayer != null)
            legendaryVideoPlayer.Stop();
        if (legendaryAnimPanel != null)
        {
            var rawImg = legendaryAnimPanel.GetComponentInChildren<UnityEngine.UI.RawImage>(true);
            if (rawImg != null)
            {
                rawImg.texture = null;
                rawImg.gameObject.SetActive(false);
            }
        }

        // 2단계: 전설 등장 연출 표시
        if (legendaryRevealGO != null) legendaryRevealGO.SetActive(true);
        if (tapToContinueGO != null) tapToContinueGO.SetActive(true);

        SoundManager.Instance?.PlaySFX("GachaRare");

        isWaitingForTap = true;
    }

    private void OnLegendaryTapToContinue()
    {
        Debug.Log("[CompanionGachaManager] ★ 탭 감지 → 결과창으로 이동");

        if (legendaryVideoPlayer != null && legendaryVideoPlayer.isPlaying)
            legendaryVideoPlayer.Stop();

        // RawImage에 남은 마지막 프레임 제거
        if (legendaryAnimPanel != null)
        {
            var rawImg = legendaryAnimPanel.GetComponentInChildren<UnityEngine.UI.RawImage>(true);
            if (rawImg != null) rawImg.texture = null;
        }

        if (legendaryAnimPanel != null) legendaryAnimPanel.SetActive(false);
        if (legendaryRevealGO != null) legendaryRevealGO.SetActive(false);
        if (tapToContinueGO != null) tapToContinueGO.SetActive(false);

        isLegendaryAnimPlaying = false;
        isWaitingForTap = false;

        ShowMainUI();  // 월드 복원

        if (pendingLegendaryResults != null && pendingLegendaryResults.Count > 0)
        {
            ShowResult(pendingLegendaryResults);
            pendingLegendaryResults = null;
        }

        SaveLoadManager.Instance?.SaveGame();
    }

    public void CancelLegendaryAnimation()
    {
        if (!isLegendaryAnimPlaying && !isWaitingForTap) return;

        CleanupLegendaryState();
        ShowMainUI();
    }

    /// <summary>★ 전설 연출 상태 완전 정리 (잔재 방지)</summary>
    private void CleanupLegendaryState()
    {
        // 비디오 정지
        if (legendaryVideoPlayer != null && legendaryVideoPlayer.isPlaying)
            legendaryVideoPlayer.Stop();

        // RawImage 텍스처 제거 (마지막 프레임 잔상 방지)
        if (legendaryAnimPanel != null)
        {
            var rawImg = legendaryAnimPanel.GetComponentInChildren<UnityEngine.UI.RawImage>(true);
            if (rawImg != null)
            {
                rawImg.texture = null;
                rawImg.gameObject.SetActive(true); // 다음 재생을 위해 활성화 복원
            }
            legendaryAnimPanel.SetActive(false);
        }

        // 하위 연출 오브젝트 비활성화
        if (legendaryRevealGO != null) legendaryRevealGO.SetActive(false);
        if (tapToContinueGO != null) tapToContinueGO.SetActive(false);

        // 상태 플래그 초기화
        isLegendaryAnimPlaying = false;
        isWaitingForTap = false;
        pendingLegendaryResults = null;

        Debug.Log("[CompanionGachaManager] ★ 전설 연출 상태 정리 완료");
    }

    void OnDestroy()
    {
        if (legendaryVideoPlayer != null)
            legendaryVideoPlayer.loopPointReached -= OnLegendaryVideoFinished;

        // ★ 전설 연출 잔재 완전 정리
        CleanupLegendaryState();

        // RenderTexture 정리
        if (_videoRT != null) { _videoRT.Release(); _videoRT = null; }

        // 카메라 상태 복원 (씬 전환 시 메인 카메라가 꺼진 채로 남는 것 방지)
        ShowMainUI();
    }

}

// ═══════════════════════════════════════════════════════════════
//  소환 탭 데이터 구조
// ═══════════════════════════════════════════════════════════════

// SummonTabData      → SummonTabData.cs
// SwipeDragHandler   → SwipeDragHandler.cs
// CompanionResultItem → CompanionResultItem.cs
