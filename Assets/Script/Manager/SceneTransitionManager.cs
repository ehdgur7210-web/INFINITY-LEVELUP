using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 씬 전환 관리 시스템
/// - 페이드 인/아웃 효과
/// - 로딩 화면
/// - 비동기 씬 로딩
/// - 플레이어 위치 설정
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("페이드 효과")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 1f;

    [Header("로딩 화면")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider loadingBar;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI tipText;

    [Header("로딩 팁")]
    [SerializeField] private string[] loadingTips = new string[]
    {
        "팁: 인벤토리를 정리하면 더 많은 아이템을 얻을 수 있습니다!",
        "팁: 몬스터를 처치하면 경험치와 골드를 획득합니다.",
        "팁: 상점에서 유용한 아이템을 구매할 수 있습니다.",
        "팁: 퀘스트를 완료하면 보상을 받을 수 있습니다.",
        "팁: 스킬을 적절히 사용하면 전투가 쉬워집니다."
    };

    // 씬 전환 시 플레이어 위치
    private Vector3? pendingPlayerPosition = null;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeTransitionManager();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 전환 매니저 초기화
    /// </summary>
    private void InitializeTransitionManager()
    {
        // 페이드 캔버스가 없으면 생성
        if (fadeCanvasGroup == null)
        {
            CreateFadeCanvas();
        }

        // 로딩 패널 초기화
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        // 처음에는 투명하게
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
        }
    }

    /// <summary>
    /// 페이드 캔버스 생성
    /// </summary>
    private void CreateFadeCanvas()
    {
        // Canvas 생성
        GameObject canvasObj = new GameObject("FadeCanvas");
        canvasObj.transform.SetParent(transform);
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 최상위
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // CanvasGroup
        fadeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        fadeCanvasGroup.blocksRaycasts = false;
        
        // 검은색 이미지
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform);
        
        Image image = imageObj.AddComponent<Image>();
        image.color = Color.black;
        
        RectTransform rt = image.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
    }

    #region 씬 전환 메서드

    /// <summary>
    /// 씬 전환 (기본)
    /// </summary>
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    /// <summary>
    /// 씬 전환 (인덱스)
    /// </summary>
    public void LoadScene(int sceneIndex)
    {
        StartCoroutine(LoadSceneCoroutine(sceneIndex));
    }

    /// <summary>
    /// 씬 전환 + 플레이어 위치 설정
    /// </summary>
    public void LoadSceneWithPosition(string sceneName, Vector3 playerPosition)
    {
        pendingPlayerPosition = playerPosition;
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    /// <summary>
    /// 메인 메뉴로 이동
    /// </summary>
    public void LoadMainMenu()
    {
        // 게임 데이터 저장
        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.SaveGame(0);
        }

        LoadScene("MainMenu");
    }

    /// <summary>
    /// 게임 플레이 씬으로 이동
    /// </summary>
    public void LoadGameplay()
    {
        LoadScene("Gameplay");
    }

    /// <summary>
    /// 현재 씬 재시작
    /// </summary>
    public void RestartScene()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        LoadScene(currentSceneName);
    }

    #endregion

    #region 씬 로딩 코루틴

    /// <summary>
    /// 씬 로딩 코루틴 (이름)
    /// </summary>
    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        // 1. 페이드 아웃
        yield return StartCoroutine(FadeOut());

        // 2. 로딩 화면 표시
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            ShowRandomTip();
        }

        // 3. 비동기 씬 로딩
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        // 4. 로딩 진행률 표시
        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            UpdateLoadingBar(progress);

            // 90% 이상이면 씬 활성화 준비
            if (asyncLoad.progress >= 0.9f)
            {
                UpdateLoadingText("준비 완료! 시작하려면 아무 키나 누르세요...");

                // 사용자 입력 대기 (선택사항)
                // yield return new WaitUntil(() => Input.anyKeyDown);

                // 또는 자동으로 진행
                yield return new WaitForSeconds(0.5f);

                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }

        // 5. 씬 로드 완료 후 처리
        yield return new WaitForSeconds(0.1f);

        // 플레이어 위치 설정
        if (pendingPlayerPosition.HasValue)
        {
            SetPlayerPosition(pendingPlayerPosition.Value);
            pendingPlayerPosition = null;
        }

        // 6. 로딩 화면 숨기기
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        // 7. 페이드 인
        yield return StartCoroutine(FadeIn());
    }

    /// <summary>
    /// 씬 로딩 코루틴 (인덱스)
    /// </summary>
    private IEnumerator LoadSceneCoroutine(int sceneIndex)
    {
        yield return StartCoroutine(FadeOut());

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            ShowRandomTip();
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);
        asyncLoad.allowSceneActivation = false;

        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            UpdateLoadingBar(progress);

            if (asyncLoad.progress >= 0.9f)
            {
                UpdateLoadingText("준비 완료!");
                yield return new WaitForSeconds(0.5f);
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        if (pendingPlayerPosition.HasValue)
        {
            SetPlayerPosition(pendingPlayerPosition.Value);
            pendingPlayerPosition = null;
        }

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        yield return StartCoroutine(FadeIn());
    }

    #endregion

    #region 페이드 효과

    /// <summary>
    /// 페이드 아웃 (어두워짐)
    /// </summary>
    private IEnumerator FadeOut()
    {
        if (fadeCanvasGroup == null) yield break;

        fadeCanvasGroup.blocksRaycasts = true;

        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime; // Time.timeScale 영향 안받음
            fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
    }

    /// <summary>
    /// 페이드 인 (밝아짐)
    /// </summary>
    private IEnumerator FadeIn()
    {
        if (fadeCanvasGroup == null) yield break;

        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    #endregion

    #region 로딩 UI 업데이트

    /// <summary>
    /// 로딩 바 업데이트
    /// </summary>
    private void UpdateLoadingBar(float progress)
    {
        if (loadingBar != null)
        {
            loadingBar.value = progress;
        }

        if (loadingText != null)
        {
            loadingText.text = $"로딩 중... {(progress * 100f):0}%";
        }
    }

    /// <summary>
    /// 로딩 텍스트 업데이트
    /// </summary>
    private void UpdateLoadingText(string text)
    {
        if (loadingText != null)
        {
            loadingText.text = text;
        }
    }

    /// <summary>
    /// 랜덤 팁 표시
    /// </summary>
    private void ShowRandomTip()
    {
        if (tipText != null && loadingTips.Length > 0)
        {
            string randomTip = loadingTips[Random.Range(0, loadingTips.Length)];
            tipText.text = randomTip;
        }
    }

    #endregion

    #region 플레이어 위치 설정

    /// <summary>
    /// 플레이어 위치 설정
    /// </summary>
    private void SetPlayerPosition(Vector3 position)
    {
        // 플레이어 찾기
        PlayerController player = FindObjectOfType<PlayerController>();
        
        if (player != null)
        {
            // CharacterController가 있으면 비활성화 후 이동
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                player.transform.position = position;
                cc.enabled = true;
            }
            else
            {
                player.transform.position = position;
            }

            Debug.Log($"플레이어 위치 설정: {position}");
        }
        else
        {
            Debug.LogWarning("플레이어를 찾을 수 없습니다!");
        }
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// 게임 종료
    /// </summary>
    public void QuitGame()
    {
        StartCoroutine(QuitGameCoroutine());
    }

    /// <summary>
    /// 게임 종료 코루틴
    /// </summary>
    private IEnumerator QuitGameCoroutine()
    {
        // 저장
        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.SaveGame(0);
        }

        // 페이드 아웃
        yield return StartCoroutine(FadeOut());

        // 종료
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    /// <summary>
    /// 페이드 지속시간 설정
    /// </summary>
    public void SetFadeDuration(float duration)
    {
        fadeDuration = duration;
    }

    /// <summary>
    /// 현재 씬 이름 가져오기
    /// </summary>
    public string GetCurrentSceneName()
    {
        return SceneManager.GetActiveScene().name;
    }

    /// <summary>
    /// 씬이 로드되었는지 확인
    /// </summary>
    public bool IsSceneLoaded(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == sceneName)
            {
                return true;
            }
        }
        return false;
    }

    #endregion
}

/// <summary>
/// 씬 포털 (특정 위치에서 씬 전환 트리거)
/// </summary>
public class ScenePortal : MonoBehaviour
{
    [Header("목표 씬")]
    [SerializeField] private string targetSceneName;
    [SerializeField] private Vector3 spawnPosition;

    [Header("UI")]
    [SerializeField] private GameObject interactionPrompt; // "E를 눌러 이동" 같은 UI
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    private bool playerInRange = false;

    void Start()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactionKey))
        {
            TeleportToScene();
        }
    }

    /// <summary>
    /// 씬 전환
    /// </summary>
    private void TeleportToScene()
    {
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadSceneWithPosition(
                targetSceneName,
                spawnPosition
            );
        }
        else
        {
            Debug.LogError("SceneTransitionManager를 찾을 수 없습니다!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(true);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
        }
    }

    // 2D 게임용
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(true);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
        }
    }
}
