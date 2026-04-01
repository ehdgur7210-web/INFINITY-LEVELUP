using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ■ 캐릭터 선택 화면 (패널 방식) ■
///
/// [Hierarchy 구조]
/// Canvas
///  └── CharacterSelect (이 오브젝트에 컴포넌트 부착)
///        ├── Background (Image)
///        ├── LeftPanel
///        │     ├── ClassBigImage (Image)      ← 직업 대형 이미지
///        │     ├── CharNameText (TMP)
///        │     ├── ClassNameText (TMP)
///        │     ├── LevelText (TMP)
///        │     └── EmptyHintGroup (GO)
///        ├── SlotBar
///        │     └── SlotContainer (Horizontal Layout Group)
///        ├── BottomButtons
///        │     ├── StartButton
///        │     ├── CreateButton
///        │     └── DeleteButton
///        └── DeleteConfirmPanel (평소 비활성)
///              ├── ConfirmText (TMP)
///              ├── YesButton
///              └── NoButton
///
/// [CharacterSlot 프리팹 구조]
///   ├── SelectFrame (Image)
///   ├── FilledGroup (GO)
///   │     ├── ClassIconImage (Image)
///   │     ├── CharNameText (TMP)
///   │     ├── IDText (TMP)
///   │     └── LevelText (TMP)
///   └── EmptySlotGroup (GO)
/// </summary>
public class CharacterSelectManager : MonoBehaviour
{
    public static CharacterSelectManager Instance { get; private set; }

    // ── 저장 키 ──────────────────────────────────
    private const int MAX_SLOTS = 4;
    private const string KEY_EXISTS = "Slot_{0}_Exists";
    private const string KEY_NAME = "Slot_{0}_Name";
    private const string KEY_CLASS = "Slot_{0}_Class";
    private const string KEY_LEVEL = "Slot_{0}_Level";
    private const string KEY_ID = "Slot_{0}_ID";
    private const string KEY_LAST_SEL = "LastSelectedSlot";

    // ── 직업별 기본 스탯 ─────────────────────────
    [System.Serializable]
    public class ClassStats
    {
        public float baseHealth = 100f;
        public float baseAttack = 20f;
        public float baseDefense = 10f;
        public float baseSpeed = 5f;
        public float attackRange = 5f;
        public float attackSpeed = 0.5f;
    }

    // ── Inspector ─────────────────────────────────

    [Header("===== 패널 참조 =====")]
    [SerializeField] private GameObject characterSelectPanel;
    [SerializeField] private CharacterCreateManager createPanel;

    [Header("===== 왼쪽 패널 =====")]
    [SerializeField] private Image classBigImage;
    [SerializeField] private TextMeshProUGUI leftCharNameText;
    [SerializeField] private TextMeshProUGUI leftClassNameText;
    [SerializeField] private TextMeshProUGUI leftLevelText;
    [SerializeField] private GameObject emptyHintGroup;

    [Header("===== 슬롯 =====")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject slotPrefab;

    [Header("===== 버튼 =====")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button createButton;
    [SerializeField] private Button deleteButton;

    [Header("===== 직업 에셋 (Melee=0 / Ranged=1 / Magic=2) =====")]
    [SerializeField] private Sprite[] classImages;
    [SerializeField] private Sprite[] classIcons;
    [SerializeField] private string[] classNames = { "근거리 전사", "원거리 궁수", "마법사" };

    [Header("===== 직업별 기본 스탯 (Melee=0 / Ranged=1 / Magic=2) =====")]
    [SerializeField]
    private ClassStats[] classStats = new ClassStats[]
    {
        new ClassStats { baseHealth=150, baseAttack=25, baseDefense=15, baseSpeed=4f,   attackRange=1.5f,  attackSpeed=1.2f },
        new ClassStats { baseHealth=100, baseAttack=20, baseDefense=8,  baseSpeed=5.5f, attackRange=10f,   attackSpeed=0.5f },
        new ClassStats { baseHealth=80,  baseAttack=30, baseDefense=5,  baseSpeed=4.5f, attackRange=12f,   attackSpeed=0.8f },
    };

    [Header("===== 씬 이름 =====")]
    [SerializeField] private string gameSceneName = "MainScene";

    [Header("===== 로딩 화면 =====")]
    [Tooltip("로딩 패널 (LoginScene Canvas 안에 배치)")]
    [SerializeField] private GameObject 로딩패널;
    [Tooltip("로딩 프로그레스 바")]
    [SerializeField] private UnityEngine.UI.Slider 로딩바;
    [Tooltip("로딩 텍스트 (예: '로딩 중... 85%')")]
    [SerializeField] private TMPro.TextMeshProUGUI 로딩텍스트;

    [Header("===== 뒤로가기 =====")]
    [SerializeField] private Button backButton;

    [Header("===== 삭제 확인 =====")]
    [SerializeField] private GameObject deleteConfirmPanel;
    [SerializeField] private TextMeshProUGUI deleteConfirmText;
    [SerializeField] private Button deleteYesBtn;
    [SerializeField] private Button deleteNoBtn;

    // ── 슬롯 데이터 ───────────────────────────────
    public class SlotData
    {
        public bool exists;
        public string charName;
        public int classType;
        public int level;
        public string accountID;
    }

    private int selectedSlot = -1;
    private readonly List<CharacterSlotUI> slotUIs = new List<CharacterSlotUI>();

    // ─────────────────────────────────────────────
    private bool slotsCreated = false;
    private bool buttonsSetup = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Debug.Log("[ManagerInit] CharacterSelectManager가 생성되었습니다.");
    }

    void Start()
    {
        InitIfNeeded();
    }

    // ★ 핵심: 패널이 SetActive(true)될 때마다 호출됨
    void OnEnable()
    {
        InitIfNeeded();
        StartCoroutine(DelayedLayoutFix());
    }

    private IEnumerator DelayedLayoutFix()
    {
        yield return null; // 1프레임 대기 — Canvas 크기 확정 후
        if (slotContainer == null) yield break;

        // 모든 슬롯 강제 활성화
        for (int i = 0; i < slotUIs.Count; i++)
            slotUIs[i].gameObject.SetActive(true);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer as RectTransform);
        // 부모(Viewport)도 갱신
        if (slotContainer.parent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer.parent as RectTransform);
        // Slot(ScrollRect 루트)도 갱신
        if (slotContainer.parent?.parent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer.parent.parent as RectTransform);

        Debug.Log($"[CharacterSelect] 레이아웃 갱신 완료 — 슬롯 {slotUIs.Count}개, Content 너비={((RectTransform)slotContainer).rect.width}");
    }

    private void InitIfNeeded()
    {
        Debug.Log($"[CharacterSelect] InitIfNeeded 호출 — buttonsSetup={buttonsSetup}, slotsCreated={slotsCreated}, slotContainer={slotContainer}, slotPrefab={slotPrefab}");
        if (!buttonsSetup)
        {
            SetupButtons();
            buttonsSetup = true;
        }
        if (!slotsCreated) CreateSlots();
        GameDataBridge.ReadCharacterSlots();
        RefreshAll();

        // 레이아웃 강제 갱신 (Grid Layout 재계산)
        if (slotContainer != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer as RectTransform);
        }

        int last = GameDataBridge.CharacterSlots.lastSelectedSlot;
        SelectSlot(last >= 0 && last < MAX_SLOTS && LoadSlot(last).exists ? last : -1);
    }

    // ─────────────────────────────────────────────
    public void Show()
    {
        if (characterSelectPanel != null) characterSelectPanel.SetActive(true);
        if (!slotsCreated) CreateSlots();
        GameDataBridge.ReadCharacterSlots();

        // 모든 슬롯 강제 활성화 + 데이터 갱신
        for (int i = 0; i < slotUIs.Count; i++)
            slotUIs[i].gameObject.SetActive(true);
        RefreshAll();

        int last = GameDataBridge.CharacterSlots.lastSelectedSlot;
        SelectSlot(last >= 0 && last < MAX_SLOTS && LoadSlot(last).exists ? last : -1);
    }

    public void Hide()
    {
        if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    /// <summary>
    /// 프리팹으로 슬롯 4개 생성. 1회만 실행.
    /// </summary>
    private void CreateSlots()
    {
        Debug.Log($"[CharacterSelect] CreateSlots 시작 — slotContainer={slotContainer}, slotPrefab={slotPrefab}");

        if (slotContainer == null || slotPrefab == null)
        {
            Debug.LogError($"[CharacterSelect] ★ NULL! slotContainer={slotContainer}, slotPrefab={slotPrefab}");
            return;
        }

        slotsCreated = true;
        slotUIs.Clear();

        int existingCount = slotContainer.childCount;
        Debug.Log($"[CharacterSelect] 기존 자식 수: {existingCount}");
        for (int i = 0; i < existingCount && i < MAX_SLOTS; i++)
        {
            int idx = i;
            GameObject go = slotContainer.GetChild(i).gameObject;
            go.SetActive(true);
            CharacterSlotUI ui = go.GetComponent<CharacterSlotUI>()
                              ?? go.AddComponent<CharacterSlotUI>();
            ui.Init(idx, OnSlotClicked);
            slotUIs.Add(ui);
        }

        // 부족하면 프리팹으로 추가 생성
        for (int i = slotUIs.Count; i < MAX_SLOTS; i++)
        {
            int idx = i;
            GameObject go = Instantiate(slotPrefab, slotContainer);
            go.SetActive(true);
            go.name = $"CharacterSlot_{idx}";
            CharacterSlotUI ui = go.GetComponent<CharacterSlotUI>()
                              ?? go.AddComponent<CharacterSlotUI>();
            ui.Init(idx, OnSlotClicked);
            slotUIs.Add(ui);
        }

        // Grid Layout constraintCount를 슬롯 수에 맞추기
        GridLayoutGroup grid = slotContainer.GetComponent<GridLayoutGroup>();
        if (grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            grid.constraintCount = MAX_SLOTS;

        Debug.Log($"[CharacterSelect] CreateSlots 완료 — 총 슬롯: {slotUIs.Count}개");

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer as RectTransform);
    }

    private void RefreshAll()
    {
        for (int i = 0; i < slotUIs.Count; i++)
        {
            // 슬롯 항상 활성화
            slotUIs[i].gameObject.SetActive(true);

            SlotData data = LoadSlot(i);
            Sprite icon = (data.exists && classIcons != null && data.classType < classIcons.Length)
                          ? classIcons[data.classType] : null;
            slotUIs[i].Refresh(data, icon, i == selectedSlot);
        }
    }

    // ─────────────────────────────────────────────
    private void OnSlotClicked(int idx)
    {
        Debug.Log($"[CharacterSelect] 슬롯 클릭: {idx}");
        SoundManager.Instance?.PlayCharacterSelect();
        SelectSlot(idx);

        // 클릭 후 슬롯 상태 확인
        for (int i = 0; i < slotUIs.Count; i++)
            Debug.Log($"[CharacterSelect] 슬롯[{i}] activeSelf={slotUIs[i].gameObject.activeSelf}, activeInHierarchy={slotUIs[i].gameObject.activeInHierarchy}, parent={slotUIs[i].transform.parent?.name}");
    }

    public void SelectSlot(int idx)
    {
        Debug.Log($"[CharacterSelect] SelectSlot({idx}) — slotUIs.Count={slotUIs.Count}");
        selectedSlot = idx;
        for (int i = 0; i < slotUIs.Count; i++)
            slotUIs[i].SetSelected(i == idx);

        if (idx < 0) { SetLeftEmpty(); return; }
        SlotData data = LoadSlot(idx);
        if (!data.exists) { Debug.Log($"[CharacterSelect] 슬롯 {idx} 데이터 없음"); SetLeftEmpty(); return; }

        if (emptyHintGroup != null) emptyHintGroup.SetActive(false);
        if (classBigImage != null && classImages != null && data.classType < classImages.Length)
            classBigImage.sprite = classImages[data.classType];
        if (leftCharNameText != null) leftCharNameText.text = data.charName;
        if (leftClassNameText != null && classNames != null && data.classType < classNames.Length)
            leftClassNameText.text = classNames[data.classType];
        if (leftLevelText != null) leftLevelText.text = $"Lv. {data.level}";

        startButton.interactable = true;
        deleteButton.interactable = true;
        GameDataBridge.CharacterSlots.lastSelectedSlot = idx;
        GameDataBridge.WriteCharacterSlots();
    }

    private void SetLeftEmpty()
    {
        if (classBigImage != null) classBigImage.sprite = null;
        if (leftCharNameText != null) leftCharNameText.text = "";
        if (leftClassNameText != null) leftClassNameText.text = "";
        if (leftLevelText != null) leftLevelText.text = "";
        if (emptyHintGroup != null) emptyHintGroup.SetActive(true);
        startButton.interactable = false;
        deleteButton.interactable = false;
    }

    // ─────────────────────────────────────────────
    private void SetupButtons()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (createButton != null) createButton.onClick.AddListener(OnCreateClicked);
        if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteClicked);
        if (deleteYesBtn != null) deleteYesBtn.onClick.AddListener(ConfirmDelete);
        if (deleteNoBtn != null) deleteNoBtn.onClick.AddListener(CancelDelete);
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
        startButton.interactable = false;
        deleteButton.interactable = false;

        // ★ 뒤로가기 버튼 (Inspector 미연결 시 자동 탐색)
        if (backButton == null)
            backButton = FindButtonByName("BackButton", "backButton", "뒤로", "뒤로가기", "Back");
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    /// <summary>
    /// ★ 뒤로가기 → 서버 선택 화면으로 복귀
    /// </summary>
    private void OnBackClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        Hide();

        // ServerSelectionSystem의 패널을 다시 표시
        ServerSelectionSystem serverSys = FindObjectOfType<ServerSelectionSystem>(true);
        if (serverSys != null)
        {
            serverSys.ShowServerSelectionPanel();
            Debug.Log("[CharacterSelect] 서버 선택 화면으로 복귀");
        }
        else
        {
            // 서버 선택이 없으면 로그인 화면으로 복귀
            LoginSystem loginSys = FindObjectOfType<LoginSystem>(true);
            if (loginSys != null)
            {
                loginSys.ShowLoginPanel();
                Debug.Log("[CharacterSelect] 로그인 화면으로 복귀");
            }
        }
    }

    /// <summary>이름 목록으로 하위 Button 탐색</summary>
    private Button FindButtonByName(params string[] names)
    {
        foreach (string name in names)
        {
            // characterSelectPanel 하위에서 탐색
            Transform searchRoot = characterSelectPanel != null
                ? characterSelectPanel.transform : transform;
            Transform found = searchRoot.Find(name);
            if (found != null)
            {
                Button btn = found.GetComponent<Button>();
                if (btn != null) return btn;
            }
        }
        // 전체 하위에서 키워드 검색
        Button[] allButtons = (characterSelectPanel != null
            ? characterSelectPanel : gameObject).GetComponentsInChildren<Button>(true);
        foreach (var btn in allButtons)
        {
            string n = btn.gameObject.name.ToLower();
            if (n.Contains("back") || n.Contains("뒤로"))
                return btn;
        }
        return null;
    }

    // ★ 게임 시작 - 서버 데이터 로드 → 스탯 저장 → 씬 전환
    private void OnStartClicked()
    {
        if (selectedSlot < 0) return;
        SlotData data = LoadSlot(selectedSlot);
        if (!data.exists) return;

        // 중복 클릭 방지
        startButton.interactable = false;
        SoundManager.Instance?.PlayGameStart();

        StartCoroutine(StartGameWithServerData(data));
    }

    /// <summary>
    /// 서버 데이터 로드 시도 → 로컬 폴백 → 씬 전환
    /// </summary>
    private IEnumerator StartGameWithServerData(SlotData data)
    {
        bool serverLoadDone = false;
        bool serverLoadSuccess = false;

        // ★ 서버에서 해당 슬롯 데이터 로드 시도
        if (BackendGameDataManager.Instance != null && BackendManager.Instance != null && BackendManager.Instance.IsLoggedIn)
        {
            BackendGameDataManager.Instance.LoadFromServer(selectedSlot, success =>
            {
                serverLoadSuccess = success;
                serverLoadDone = true;
            });

            // 서버 응답 대기 (최대 5초)
            float timeout = 5f;
            while (!serverLoadDone && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (serverLoadSuccess)
            {
                // 서버 데이터의 캐릭터 이름이 현재 슬롯과 다르면 옛 데이터 → 로컬 사용
                string serverCharName = GameDataBridge.HasData
                    ? GameDataBridge.CurrentData.selectedCharacterName : "";
                if (!string.IsNullOrEmpty(data.charName) && serverCharName != data.charName)
                {
                    Debug.Log($"[CharacterSelect] 서버 데이터 불일치 (서버:'{serverCharName}' ≠ 슬롯:'{data.charName}') → 로컬 데이터 사용");
                    LoadLocalSlotData(data);
                }
                else
                {
                    Debug.Log($"[CharacterSelect] 서버 데이터 로드 성공 (슬롯:{selectedSlot})");

                    // ★ 서버 데이터 로드 후 로컬 오프라인 보상 데이터 병합
                    // 앱 종료 시 비동기 SaveToServer가 완료되기 전에 앱이 죽으면
                    // 서버에는 stale한 lastLogoutTime이 남아있을 수 있음
                    // → 로컬 파일의 lastLogoutTime이 더 최신이면 로컬 값 사용
                    MergeLocalOfflineData(selectedSlot);
                }
            }
            else
            {
                Debug.Log($"[CharacterSelect] 서버 데이터 없음/실패 → 로컬 데이터 사용");
                LoadLocalSlotData(data);
            }
        }
        else
        {
            // 오프라인 모드 → 로컬 데이터 사용
            LoadLocalSlotData(data);
        }

        // 캐릭터 정보 설정 (서버/로컬 데이터 위에 덮어쓰기)
        GameDataBridge.CurrentData.selectedCharacterName = data.charName;
        GameDataBridge.CurrentData.characterClassType    = data.classType;
        GameDataBridge.CurrentData.activeCharacterSlot   = selectedSlot;
        GameDataBridge.CurrentData.accountID             = data.accountID;

        // ★ 직업별 스탯 저장 (PlayerController.LoadCharacterData() 호환)
        if (classStats != null && data.classType < classStats.Length)
        {
            ClassStats s = classStats[data.classType];
            GameDataBridge.CurrentData.charBaseHealth  = s.baseHealth;
            GameDataBridge.CurrentData.charBaseAttack  = s.baseAttack;
            GameDataBridge.CurrentData.charBaseDefense = s.baseDefense;
            GameDataBridge.CurrentData.charBaseSpeed   = s.baseSpeed;
            GameDataBridge.CurrentData.charAttackRange = s.attackRange;
            GameDataBridge.CurrentData.charAttackSpeed = s.attackSpeed;
        }
        GameDataBridge.CharacterSlots.lastSelectedSlot = selectedSlot;
        GameDataBridge.WriteCharacterSlots();
        GameDataBridge.SetCurrentUser(data.accountID);

        // ★ SceneTransitionManager 우선, 없으면 로딩 화면 직접 표시
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadGameplay();
        }
        else
        {
            StartCoroutine(LoadMainSceneWithLoading());
        }
    }

    /// <summary>
    /// 서버 데이터 로드 후, 로컬 파일의 오프라인 보상 데이터가 더 최신이면 병합.
    /// 앱 종료 시 SaveToServer 비동기 호출이 완료되기 전에 앱이 종료되면
    /// 서버에는 stale한 lastLogoutTime이 남는다. 로컬 파일은 OnApplicationQuit에서
    /// 동기적으로 WriteToFile 하므로 항상 최신.
    /// </summary>
    private void MergeLocalOfflineData(int slot)
    {
        if (!GameDataBridge.FileExists(slot)) return;
        if (GameDataBridge.CurrentData == null) return;

        try
        {
            string path = GameDataBridge.GetFilePath(slot);
            string json = System.IO.File.ReadAllText(path);
            SaveData localData = JsonUtility.FromJson<SaveData>(json);
            if (localData == null) return;

            string serverLogout = GameDataBridge.CurrentData.lastLogoutTime ?? "";
            string localLogout  = localData.lastLogoutTime ?? "";

            DateTime serverTime = DateTime.MinValue;
            DateTime localTime  = DateTime.MinValue;

            bool hasServer = !string.IsNullOrEmpty(serverLogout) && DateTime.TryParse(serverLogout, out serverTime);
            bool hasLocal  = !string.IsNullOrEmpty(localLogout)  && DateTime.TryParse(localLogout, out localTime);

            // 로컬이 더 최신이면 오프라인 보상 필드만 로컬 값으로 교체
            if (hasLocal && (!hasServer || localTime > serverTime))
            {
                GameDataBridge.CurrentData.lastLogoutTime            = localData.lastLogoutTime;
                GameDataBridge.CurrentData.accumulatedOfflineMinutes = localData.accumulatedOfflineMinutes;
                GameDataBridge.CurrentData.offlineGoldRate           = localData.offlineGoldRate;
                GameDataBridge.CurrentData.offlineExpRate            = localData.offlineExpRate;
                GameDataBridge.CurrentData.offlineGemRate            = localData.offlineGemRate;
                GameDataBridge.CurrentData.offlineEquipTicketRate    = localData.offlineEquipTicketRate;
                GameDataBridge.CurrentData.offlineCurrentWave       = localData.offlineCurrentWave;

                Debug.Log($"[CharacterSelect] ★ 로컬 오프라인 데이터 병합 (서버:{serverLogout} → 로컬:{localLogout})");
            }
            else
            {
                Debug.Log($"[CharacterSelect] 서버 오프라인 데이터가 최신 — 병합 불필요");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CharacterSelect] 로컬 오프라인 데이터 병합 실패: {e.Message}");
        }
    }

    /// <summary>로컬 세이브 파일에서 슬롯 데이터 로드</summary>
    private void LoadLocalSlotData(SlotData data)
    {
        if (GameDataBridge.FileExists(selectedSlot))
        {
            GameDataBridge.ReadFromFile(selectedSlot);
            Debug.Log($"[CharacterSelect] 슬롯 {selectedSlot} 로컬 세이브 로드 완료");
        }
        else
        {
            GameDataBridge.InitNewCharacterData(selectedSlot, data.charName, data.classType, data.accountID);
            Debug.Log($"[CharacterSelect] 슬롯 {selectedSlot} 세이브 없음 → 새 캐릭터 데이터 생성");
        }
    }

    private void OnCreateClicked()
    {
        int emptySlot = -1;
        for (int i = 0; i < MAX_SLOTS; i++)
            if (!LoadSlot(i).exists) { emptySlot = i; break; }

        if (emptySlot < 0) { Debug.Log("[CharacterSelect] 슬롯이 가득 찼습니다."); return; }

        GameDataBridge.CharacterSlots.createTargetSlot = emptySlot;
        GameDataBridge.WriteCharacterSlots();
        Hide();
        if (createPanel != null) createPanel.Show();
    }

    private void OnDeleteClicked()
    {
        if (selectedSlot < 0) return;
        SlotData data = LoadSlot(selectedSlot);
        if (!data.exists) return;

        if (deleteConfirmPanel != null)
        {
            deleteConfirmPanel.SetActive(true);
            if (deleteConfirmText != null)
                deleteConfirmText.text = $"'{data.charName}' 캐릭터를 삭제하시겠습니까?\n삭제된 캐릭터는 복구할 수 없습니다.";
        }
        else ConfirmDelete();
    }

    private void ConfirmDelete()
    {
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
        DeleteSlotData(selectedSlot);

        // ★ 삭제 후 남은 슬롯을 왼쪽으로 땡기기
        ShiftSlotsLeft(selectedSlot);

        GameDataBridge.ReadCharacterSlots();
        for (int i = 0; i < slotUIs.Count; i++)
            slotUIs[i].gameObject.SetActive(true);
        RefreshAll();

        // 첫 번째 캐릭터 선택, 없으면 -1
        int firstExisting = -1;
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (LoadSlot(i).exists) { firstExisting = i; break; }
        }
        SelectSlot(firstExisting);
    }

    /// <summary>
    /// 삭제된 슬롯 뒤의 캐릭터들을 왼쪽으로 한 칸씩 이동.
    /// 예: 슬롯0 삭제 → [1]→[0], [2]→[1], [3]→[2], [3]→빈칸
    /// </summary>
    private void ShiftSlotsLeft(int deletedIdx)
    {
        CharacterSlotsData slotsData = GameDataBridge.CharacterSlots;
        if (slotsData.slots == null) return;

        for (int i = deletedIdx; i < MAX_SLOTS - 1; i++)
        {
            slotsData.slots[i] = slotsData.slots[i + 1];

            // 로컬 세이브 파일 이동 (i+1 → i)
            if (slotsData.slots[i] != null && slotsData.slots[i].exists
                && GameDataBridge.FileExists(i + 1))
            {
                GameDataBridge.ReadFromFile(i + 1);
                GameDataBridge.CurrentData.activeCharacterSlot = i;
                GameDataBridge.WriteToFile(i);
                GameDataBridge.DeleteSaveSlot(i + 1);
            }
        }

        // 마지막 슬롯은 빈 칸으로
        slotsData.slots[MAX_SLOTS - 1] = new CharacterSlotEntry { exists = false };
        GameDataBridge.DeleteSaveSlot(MAX_SLOTS - 1);

        // 빈 슬롯의 남은 로컬 파일 정리
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (slotsData.slots[i] == null || !slotsData.slots[i].exists)
                GameDataBridge.DeleteSaveSlot(i);
        }

        GameDataBridge.WriteCharacterSlots();

        // ★ 서버 데이터도 전체 정리 (슬롯 이동했으므로 옛 인덱스 데이터 삭제)
        if (BackendGameDataManager.Instance != null)
        {
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (slotsData.slots[i] == null || !slotsData.slots[i].exists)
                {
                    int slotIdx = i;
                    BackendGameDataManager.Instance.DeleteFromServer(slotIdx, success =>
                    {
                        Debug.Log($"[CharacterSelect] 서버 슬롯 {slotIdx} 삭제: {(success ? "성공" : "실패")}");
                    });
                }
            }
        }

        Debug.Log($"[CharacterSelect] ShiftSlotsLeft 완료 — 삭제슬롯:{deletedIdx}");
    }

    private void CancelDelete()
    {
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    public SlotData LoadSlot(int idx)
    {
        CharacterSlotEntry entry = (GameDataBridge.CharacterSlots.slots != null && idx < GameDataBridge.CharacterSlots.slots.Length)
            ? GameDataBridge.CharacterSlots.slots[idx] : null;
        if (entry == null) return new SlotData();
        return new SlotData
        {
            exists    = entry.exists,
            charName  = entry.charName  ?? "",
            classType = entry.classType,
            level     = entry.level,
            accountID = entry.accountID ?? ""
        };
    }

    public void SaveSlot(int idx, string charName, int classType, string accountID)
    {
        CharacterSlotsData slots = GameDataBridge.CharacterSlots;
        if (slots.slots == null || slots.slots.Length < 4)
        {
            CharacterSlotEntry[] newArr = new CharacterSlotEntry[4];
            for (int i = 0; i < 4; i++)
                newArr[i] = (slots.slots != null && i < slots.slots.Length) ? slots.slots[i] : new CharacterSlotEntry();
            slots.slots = newArr;
        }
        if (slots.slots[idx] == null) slots.slots[idx] = new CharacterSlotEntry();
        slots.slots[idx].exists    = true;
        slots.slots[idx].charName  = charName;
        slots.slots[idx].classType = classType;
        slots.slots[idx].level     = 1;
        slots.slots[idx].accountID = accountID;
        GameDataBridge.WriteCharacterSlots();
    }

    private void DeleteSlotData(int idx)
    {
        CharacterSlotsData slots = GameDataBridge.CharacterSlots;
        if (slots.slots != null && idx < slots.slots.Length)
        {
            slots.slots[idx] = new CharacterSlotEntry { exists = false };
            GameDataBridge.WriteCharacterSlots();
        }
        // 해당 슬롯의 게임 세이브 파일 삭제 (로컬)
        GameDataBridge.DeleteSaveSlot(idx);

        // ★ 서버 데이터도 삭제 (옛 데이터가 서버에 남아서 복원되는 버그 방지)
        if (BackendGameDataManager.Instance != null)
        {
            BackendGameDataManager.Instance.DeleteFromServer(idx, success =>
            {
                Debug.Log($"[CharacterSelect] 서버 슬롯 {idx} 삭제: {(success ? "성공" : "실패/미로그인")}");
            });
        }

        // ★ 인메모리 CurrentData도 초기화 (씬 전환 시 옛 데이터 잔존 방지)
        if (GameDataBridge.HasData && GameDataBridge.CurrentData.activeCharacterSlot == idx)
        {
            GameDataBridge.ResetCurrentData();
        }
    }

    // ══════════════════════════════════════════════════════
    //  로딩 화면 (SceneTransitionManager 없을 때 사용)
    // ══════════════════════════════════════════════════════

    private IEnumerator LoadMainSceneWithLoading()
    {
        // 로딩 패널 표시
        if (로딩패널 != null)
            로딩패널.SetActive(true);
        if (로딩바 != null)
            로딩바.value = 1f; // 1에서 시작
        if (로딩텍스트 != null)
            로딩텍스트.text = "로딩 중...";

        yield return null;

        // 비동기 씬 로딩
        AsyncOperation asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(gameSceneName);
        if (asyncLoad == null)
        {
            Debug.LogError($"[CharacterSelect] 씬 '{gameSceneName}' 없음!");
            if (로딩패널 != null) 로딩패널.SetActive(false);
            yield break;
        }
        asyncLoad.allowSceneActivation = false;

        // 로딩 진행률 표시 (1→0 방향)
        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            if (로딩바 != null)
                로딩바.value = 1f - progress; // 반대로: 1→0
            if (로딩텍스트 != null)
                로딩텍스트.text = $"로딩 중... {(int)(progress * 100)}%";

            if (asyncLoad.progress >= 0.9f)
            {
                if (로딩바 != null) 로딩바.value = 0f; // 0으로 완료
                if (로딩텍스트 != null) 로딩텍스트.text = "준비 완료!";
                yield return new WaitForSeconds(0.5f);
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}

