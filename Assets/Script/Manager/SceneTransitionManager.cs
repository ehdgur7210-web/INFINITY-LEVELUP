using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ══════════════════════════════════════════════════════════
/// SceneTransitionManager — 씬 전환 관리 (리팩토링)
/// ══════════════════════════════════════════════════════════
///
/// ▶ 변경사항
///   · DontDestroyOnLoad 유지 (페이드/로딩 UI가 씬을 넘어야 함)
///     단, ManagerRoot의 자식이 아닌 독립 오브젝트로 씬에 배치할 것
///   
///   · LoadFarmScene() : 씬 이동 전 SaveGame(0) 추가 (기존 누락)
///   
///   · OnSceneLoaded()에서 LoadGame() 호출 제거
///     → SaveLoadManager.Start()의 AutoLoadOnStart()가 담당
///     → 중복 로드 원천 차단
///
///   · 플레이어 DontDestroyOnLoad 등록/위치 이동 로직 유지
///     (플레이어는 씬 간 이동이 필요한 캐릭터이므로 예외)
///
/// ▶ SceneTransitionManager를 씬에 배치할 때 주의사항
///   · ManagerRoot의 자식으로 넣지 말 것 (ManagerRoot는 DDOL 제거됨)
///   · 별도 오브젝트로 씬에 배치, 첫 씬(MainMenu 또는 MainScene)에만 배치
/// ══════════════════════════════════════════════════════════
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    // ─── 씬 이름 상수 ────────────────────────────────────
    public const string SCENE_MAIN = "MainScene";
    public const string SCENE_FARM = "FarmScene";
    public const string SCENE_MENU = "MainMenu";

    [Header("페이드 효과")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.8f;

    [Header("로딩 화면")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider loadingBar;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI tipText;

    [Header("로딩 팁")]
    [SerializeField]
    private string[] loadingTips = new string[]
    {
        "팁: 농장에서 작물을 키워 퀘스트를 클리어하세요!",
        "팁: 비료를 사용하면 작물 성장이 더 빨라집니다.",
        "팁: 작물 포인트로 강화 재료를 획득하세요!",
        "팁: 퀘스트는 시간마다 갱신됩니다.",
        "팁: 다양한 작물을 조합해 더 많은 퀘스트를 클리어하세요."
    };

    // ─── 씬 전환 시 플레이어 스폰 위치 ─────────────────────
    private Vector3? pendingPlayerPosition = null;

    // ─── 씬을 넘나드는 플레이어 오브젝트 ───────────────────
    private static GameObject persistentPlayer = null;

    // ══════════════════════════════════════════════════════
    //  Unity 생명주기
    // ══════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // ★ SceneTransitionManager는 독립 오브젝트로 DontDestroyOnLoad 유지
            // 페이드/로딩 UI가 씬 전환 중에도 살아있어야 하기 때문
            DontDestroyOnLoad(gameObject);

            InitializeTransitionManager();
            SceneManager.sceneLoaded += OnSceneLoaded;

            Debug.Log("[SceneTransitionManager] 등록 완료 (독립 DontDestroyOnLoad)");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    // ══════════════════════════════════════════════════════
    //  씬 로드 콜백
    // ══════════════════════════════════════════════════════

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // AudioListener 중복 제거
        RemoveDuplicateAudioListeners();

        // ★ 채팅 채널 재입장 (씬 전환 후)
        BackendChatManager.Instance?.RejoinDefaultChannels();

        // 플레이어 씬 배치 + 모드 전환
        // ★ LoadGame은 여기서 하지 않음!
        //   SaveLoadManager.Start()의 AutoLoadOnStart() 코루틴이 담당
        //   → 중복 로드 방지
        StartCoroutine(SetupPlayerForScene(scene.name));
    }

    private IEnumerator SetupPlayerForScene(string sceneName)
    {
        yield return null;
        yield return null; // 씬 오브젝트 초기화 완료 대기

        // ── 플레이어 위치 적용 ──
        if (pendingPlayerPosition.HasValue)
        {
            MovePlayerToPosition(pendingPlayerPosition.Value);
            pendingPlayerPosition = null;
        }

        // ── 씬별 플레이어 모드 전환 ──
        if (persistentPlayer != null)
        {
            PlayerController pc = persistentPlayer.GetComponent<PlayerController>();
            if (pc != null)
            {
                // ★ 씬 전환 완료 → 전투 다시 활성화
                pc.SetCombatEnabled(true);

                if (sceneName == SCENE_FARM)
                {
                    pc.SetMovementMode(true);
                    Debug.Log("[SceneTransitionManager] 농장씬 → 플레이어 이동 활성화");
                }
                else if (sceneName == SCENE_MAIN)
                {
                    pc.SetMovementMode(false);
                    Debug.Log("[SceneTransitionManager] 메인씬 → 플레이어 방치형 모드");
                }
            }
        }

        // ★ 농장씬 Canvas 활성화 보장
        //   FarmSceneController.EnsureFarmCanvasActive()가 주 담당이지만
        //   DontDestroyOnLoad 객체(이 매니저)에서도 한 번 더 확인
        if (sceneName == SCENE_FARM)
        {
            EnsureFarmCanvasActive();
        }
    }

    /// <summary>
    /// 농장씬 전환 후 FarmCanvas가 비활성 상태이면 강제 활성화.
    /// FarmPlantModePanel 등이 OverlayGO를 잘못 참조하여
    /// FarmCanvas 전체를 SetActive(false)하는 버그 방어.
    /// </summary>
    private void EnsureFarmCanvasActive()
    {
        // FarmSceneController가 이미 처리했을 수 있으므로 Canvas가 꺼진 경우만 개입
        Canvas[] canvases = FindObjectsOfType<Canvas>(true); // includeInactive=true
        foreach (Canvas c in canvases)
        {
            // SceneTransitionManager 소유 Canvas(페이드/로딩)는 건드리지 않음
            if (c.transform.IsChildOf(transform)) continue;

            if (!c.gameObject.activeSelf)
            {
                c.gameObject.SetActive(true);
                Debug.LogWarning($"[SceneTransitionManager] ★ 비활성 Canvas '{c.name}' 강제 활성화!");
            }
        }
    }

    private void RemoveDuplicateAudioListeners()
    {
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        if (listeners.Length > 1)
        {
            for (int i = 0; i < listeners.Length - 1; i++)
                listeners[i].enabled = false;
            listeners[listeners.Length - 1].enabled = true;
        }
    }

    // ══════════════════════════════════════════════════════
    //  플레이어 DontDestroyOnLoad 등록
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 플레이어를 씬 이동 가능한 오브젝트로 등록
    /// PlayerController.Awake()에서 호출
    /// </summary>
    public void RegisterPlayer(GameObject player)
    {
        if (persistentPlayer != null && persistentPlayer == player) return;

        // ★ 이미 등록된 플레이어가 있으면 씬 소속 새 Player를 파괴 (중복 방지)
        if (persistentPlayer != null && persistentPlayer != player)
        {
            Debug.Log("[SceneTransitionManager] 씬 소속 중복 Player 감지 → 파괴 (원본 유지)");
            Destroy(player);
            return;
        }

        persistentPlayer = player;

        // 부모 계층에서 분리 후 DontDestroyOnLoad
        player.transform.SetParent(null);
        DontDestroyOnLoad(player);

        Debug.Log("[SceneTransitionManager] 플레이어 등록 완료 (DontDestroyOnLoad)");
    }

    // ══════════════════════════════════════════════════════
    //  씬 전환 공개 메서드
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 농장 씬으로 이동
    /// ★ 수정: 이동 전 SaveGame(0) 추가 (기존에 누락되어 있던 버그 수정)
    /// </summary>
    public void LoadFarmScene(Vector3 spawnPosition = default)
    {
        // ★ 씬 이동 전 현재 씬 데이터 저장
        SaveLoadManager.Instance?.SaveGame();

        pendingPlayerPosition = spawnPosition == default ? Vector3.zero : spawnPosition;
        StartCoroutine(LoadSceneCoroutine(SCENE_FARM));
    }

    /// <summary>메인 게임씬으로 복귀 (저장 후 이동)</summary>
    public void LoadMainScene(Vector3 returnPosition = default)
    {
        // 저장 먼저
        SaveLoadManager.Instance?.SaveGame();

        pendingPlayerPosition = returnPosition == default ? Vector3.zero : returnPosition;
        StartCoroutine(LoadSceneCoroutine(SCENE_MAIN));
    }

    /// <summary>씬 이름으로 이동 (저장 후)</summary>
    public void LoadScene(string sceneName)
    {
        SaveLoadManager.Instance?.SaveGame();
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    /// <summary>씬 이름 + 스폰 위치 지정 이동 (저장 후)</summary>
    public void LoadSceneWithPosition(string sceneName, Vector3 playerPosition)
    {
        // ★ 씬 이동 전 저장
        SaveLoadManager.Instance?.SaveGame();

        pendingPlayerPosition = playerPosition;
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    /// <summary>메인 메뉴로 이동 (저장 후)</summary>
    public void LoadMainMenu()
    {
        SaveLoadManager.Instance?.SaveGame();
        StartCoroutine(LoadSceneCoroutine(SCENE_MENU));
    }

    /// <summary>현재 씬 재시작 (저장 후)</summary>
    public void RestartScene()
    {
        SaveLoadManager.Instance?.SaveGame();
        StartCoroutine(LoadSceneCoroutine(SceneManager.GetActiveScene().name));
    }

    /// <summary>Gameplay 씬 로드 (기존 코드 호환용)</summary>
    public void LoadGameplay()
    {
        StartCoroutine(LoadSceneCoroutine(SCENE_MAIN)); // SCENE_MAIN = "MainScene"
    }

    // ══════════════════════════════════════════════════════
    //  씬 이동 전 정리
    // ══════════════════════════════════════════════════════

    private void CleanupBeforeSceneLoad()
    {
        // WaveSpawner 정지
        if (WaveSpawner.Instance != null)
        {
            WaveSpawner.Instance.KillAllAliveMonsters();
            WaveSpawner.Instance.StopAllCoroutines();
        }

        // PoolManager 전체 비활성화 (활성 몬스터/총알/이펙트 회수)
        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.DisableAllPools();
            Debug.Log("[SceneTransitionManager] PoolManager 전체 풀 비활성화");
        }

        // Monster 컴포넌트 전체 비활성화
        foreach (var m in GameObject.FindGameObjectsWithTag("Monster"))
            m.SetActive(false);
        foreach (var b in GameObject.FindGameObjectsWithTag("Bullet"))
            b.SetActive(false);
    }

    // ══════════════════════════════════════════════════════
    //  씬 로딩 코루틴
    // ══════════════════════════════════════════════════════

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        // ★ 씬 이동 전 채팅 채널 퇴장
        BackendChatManager.Instance?.LeaveAllChannels();

        // ★ 씬 전환 시 플레이어 전투 비활성화
        if (persistentPlayer != null)
        {
            PlayerController pc = persistentPlayer.GetComponent<PlayerController>();
            if (pc != null) pc.SetCombatEnabled(false);
        }

        // 씬 이동 전 정리
        CleanupBeforeSceneLoad();

        // 페이드 아웃
        yield return StartCoroutine(FadeOut());

        // 로딩 화면 표시
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            ShowRandomTip();
        }

        // 비동기 씬 로딩
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        if (asyncLoad == null)
        {
            Debug.LogError($"[SceneTransitionManager] 씬 '{sceneName}' 없음 → Build Settings 확인!");
            if (loadingPanel != null) loadingPanel.SetActive(false);
            yield break;
        }
        asyncLoad.allowSceneActivation = false;

        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            UpdateLoadingBar(progress);

            if (asyncLoad.progress >= 0.9f)
            {
                UpdateLoadingText("준비 완료!");
                yield return new WaitForSeconds(0.3f);
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        // 페이드 인
        yield return StartCoroutine(FadeIn());
    }

    // ══════════════════════════════════════════════════════
    //  플레이어 위치 설정
    // ══════════════════════════════════════════════════════

    private void MovePlayerToPosition(Vector3 position)
    {
        GameObject player = persistentPlayer ?? GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("[SceneTransitionManager] 플레이어를 찾을 수 없습니다!");
            return;
        }

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        player.transform.position = position;
        Debug.Log($"[SceneTransitionManager] 플레이어 위치 → {position}");
    }

    // ══════════════════════════════════════════════════════
    //  페이드 / 로딩 UI
    // ══════════════════════════════════════════════════════

    private void InitializeTransitionManager()
    {
        if (fadeCanvasGroup == null) CreateFadeCanvas();
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 0f;
    }

    private void CreateFadeCanvas()
    {
        GameObject canvasObj = new GameObject("FadeCanvas");
        canvasObj.transform.SetParent(transform);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasObj.AddComponent<GraphicRaycaster>();

        fadeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        fadeCanvasGroup.blocksRaycasts = false;

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform);

        Image image = imageObj.AddComponent<Image>();
        image.color = Color.black;

        RectTransform rt = image.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
    }

    private IEnumerator FadeOut()
    {
        if (fadeCanvasGroup == null) yield break;
        fadeCanvasGroup.blocksRaycasts = true;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 1f;
    }

    private IEnumerator FadeIn()
    {
        if (fadeCanvasGroup == null) yield break;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    private void UpdateLoadingBar(float progress)
    {
        if (loadingBar != null) loadingBar.value = progress;
        if (loadingText != null) loadingText.text = $"로딩 중... {(progress * 100f):0}%";
    }

    private void UpdateLoadingText(string text)
    {
        if (loadingText != null) loadingText.text = text;
    }

    private void ShowRandomTip()
    {
        if (tipText != null && loadingTips.Length > 0)
            tipText.text = loadingTips[Random.Range(0, loadingTips.Length)];
    }

    // ══════════════════════════════════════════════════════
    //  유틸
    // ══════════════════════════════════════════════════════

    public void QuitGame()
    {
        StartCoroutine(QuitGameCoroutine());
    }

    private IEnumerator QuitGameCoroutine()
    {
        SaveLoadManager.Instance?.SaveGame();
        yield return StartCoroutine(FadeOut());
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public string GetCurrentSceneName() => SceneManager.GetActiveScene().name;
    public bool IsInFarmScene() => GetCurrentSceneName() == SCENE_FARM;
    public bool IsInMainScene() => GetCurrentSceneName() == SCENE_MAIN;
}

// ══════════════════════════════════════════════════════════
//  씬 포털 (트리거로 씬 전환)
// ══════════════════════════════════════════════════════════

/// <summary>농장 씬 진입 포털</summary>
public class FarmScenePortal : MonoBehaviour
{
    [Header("목표 씬")]
    [SerializeField] private string targetSceneName = SceneTransitionManager.SCENE_FARM;
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;

    [Header("상호작용")]
    [SerializeField] private KeyCode interactionKey = KeyCode.F;
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private string promptText = "F - 농장으로 이동";

    private bool playerInRange = false;

    void Start()
    {
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactionKey))
            SceneTransitionManager.Instance?.LoadSceneWithPosition(targetSceneName, spawnPosition);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        if (interactionPrompt != null) interactionPrompt.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }
}

/// <summary>메인씬 복귀 포털 (농장씬에서 사용)</summary>
public class ReturnToMainPortal : MonoBehaviour
{
    [SerializeField] private Vector3 returnPosition = Vector3.zero;
    [SerializeField] private KeyCode interactionKey = KeyCode.F;
    [SerializeField] private GameObject interactionPrompt;

    private bool playerInRange = false;

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactionKey))
            SceneTransitionManager.Instance?.LoadMainScene(returnPosition);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        if (interactionPrompt != null) interactionPrompt.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }
}