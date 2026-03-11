using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        SetupButtons();
        BuildSlots();
        int last = PlayerPrefs.GetInt(KEY_LAST_SEL, -1);
        SelectSlot(last >= 0 && last < MAX_SLOTS && LoadSlot(last).exists ? last : -1);
    }

    // ─────────────────────────────────────────────
    public void Show()
    {
        if (characterSelectPanel != null) characterSelectPanel.SetActive(true);
        RefreshAll();
        int last = PlayerPrefs.GetInt(KEY_LAST_SEL, -1);
        SelectSlot(last >= 0 && last < MAX_SLOTS && LoadSlot(last).exists ? last : -1);
    }

    public void Hide()
    {
        if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    private void BuildSlots()
    {
        slotUIs.Clear();
        foreach (Transform c in slotContainer) Destroy(c.gameObject);
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            int idx = i;
            GameObject go = Instantiate(slotPrefab, slotContainer);
            CharacterSlotUI ui = go.GetComponent<CharacterSlotUI>()
                              ?? go.AddComponent<CharacterSlotUI>();
            ui.Init(idx, OnSlotClicked);
            slotUIs.Add(ui);
        }
        RefreshAll();
    }

    private void RefreshAll()
    {
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            SlotData data = LoadSlot(i);
            Sprite icon = (data.exists && classIcons != null && data.classType < classIcons.Length)
                          ? classIcons[data.classType] : null;
            slotUIs[i].Refresh(data, icon, i == selectedSlot);
        }
    }

    // ─────────────────────────────────────────────
    private void OnSlotClicked(int idx)
    {
        // ★ 캐릭터 선택 효과음
        SoundManager.Instance?.PlayCharacterSelect();
        SelectSlot(idx);
    }

    public void SelectSlot(int idx)
    {
        selectedSlot = idx;
        for (int i = 0; i < slotUIs.Count; i++)
            slotUIs[i].SetSelected(i == idx);

        if (idx < 0) { SetLeftEmpty(); return; }
        SlotData data = LoadSlot(idx);
        if (!data.exists) { SetLeftEmpty(); return; }

        if (emptyHintGroup != null) emptyHintGroup.SetActive(false);
        if (classBigImage != null && classImages != null && data.classType < classImages.Length)
            classBigImage.sprite = classImages[data.classType];
        if (leftCharNameText != null) leftCharNameText.text = data.charName;
        if (leftClassNameText != null && classNames != null && data.classType < classNames.Length)
            leftClassNameText.text = classNames[data.classType];
        if (leftLevelText != null) leftLevelText.text = $"Lv. {data.level}";

        startButton.interactable = true;
        deleteButton.interactable = true;
        PlayerPrefs.SetInt(KEY_LAST_SEL, idx);
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
    }

    // ★ 게임 시작 - 스탯 저장 + 효과음 + SceneTransitionManager 연동
    private void OnStartClicked()
    {
        if (selectedSlot < 0) return;
        SlotData data = LoadSlot(selectedSlot);
        if (!data.exists) return;

        // ★ 입장 효과음
        SoundManager.Instance?.PlayGameStart();

        PlayerPrefs.SetString("SelectedCharacter", data.charName);
        PlayerPrefs.SetInt("CharacterType", data.classType);
        PlayerPrefs.SetInt("ActiveSlot", selectedSlot);

        // ★ 직업별 스탯 저장 (PlayerController.LoadCharacterData() 호환)
        if (classStats != null && data.classType < classStats.Length)
        {
            ClassStats s = classStats[data.classType];
            PlayerPrefs.SetFloat("CharacterHealth", s.baseHealth);
            PlayerPrefs.SetFloat("CharacterAttack", s.baseAttack);
            PlayerPrefs.SetFloat("CharacterDefense", s.baseDefense);
            PlayerPrefs.SetFloat("CharacterSpeed", s.baseSpeed);
            PlayerPrefs.SetFloat("CharacterAttackRange", s.attackRange);
            PlayerPrefs.SetFloat("CharacterAttackSpeed", s.attackSpeed);
        }
        PlayerPrefs.Save();

        // ★ SceneTransitionManager 우선, 없으면 직접 로드
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadGameplay();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
    }

    private void OnCreateClicked()
    {
        int emptySlot = -1;
        for (int i = 0; i < MAX_SLOTS; i++)
            if (!LoadSlot(i).exists) { emptySlot = i; break; }

        if (emptySlot < 0) { Debug.Log("[CharacterSelect] 슬롯이 가득 찼습니다."); return; }

        PlayerPrefs.SetInt("CreateTargetSlot", emptySlot);
        PlayerPrefs.Save();
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
        SelectSlot(-1);
        RefreshAll();
    }

    private void CancelDelete()
    {
        if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    public SlotData LoadSlot(int idx)
    {
        return new SlotData
        {
            exists = PlayerPrefs.GetInt(string.Format(KEY_EXISTS, idx), 0) == 1,
            charName = PlayerPrefs.GetString(string.Format(KEY_NAME, idx), ""),
            classType = PlayerPrefs.GetInt(string.Format(KEY_CLASS, idx), 0),
            level = PlayerPrefs.GetInt(string.Format(KEY_LEVEL, idx), 1),
            accountID = PlayerPrefs.GetString(string.Format(KEY_ID, idx), "")
        };
    }

    public void SaveSlot(int idx, string charName, int classType, string accountID)
    {
        PlayerPrefs.SetInt(string.Format(KEY_EXISTS, idx), 1);
        PlayerPrefs.SetString(string.Format(KEY_NAME, idx), charName);
        PlayerPrefs.SetInt(string.Format(KEY_CLASS, idx), classType);
        PlayerPrefs.SetInt(string.Format(KEY_LEVEL, idx), 1);
        PlayerPrefs.SetString(string.Format(KEY_ID, idx), accountID);
        PlayerPrefs.Save();
    }

    private void DeleteSlotData(int idx)
    {
        PlayerPrefs.DeleteKey(string.Format(KEY_EXISTS, idx));
        PlayerPrefs.DeleteKey(string.Format(KEY_NAME, idx));
        PlayerPrefs.DeleteKey(string.Format(KEY_CLASS, idx));
        PlayerPrefs.DeleteKey(string.Format(KEY_LEVEL, idx));
        PlayerPrefs.DeleteKey(string.Format(KEY_ID, idx));
        PlayerPrefs.Save();
    }
}

