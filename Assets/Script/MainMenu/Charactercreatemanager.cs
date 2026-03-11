using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// ■ 캐릭터 생성 화면 (패널 방식) ■
///
/// [Hierarchy 구조]
/// Canvas
///  └── CharacterCreate (평소 비활성)
///        ├── LeftClassList
///        │     └── ClassListContainer (Vertical Layout Group)
///        ├── CenterPreview
///        │     ├── ClassPreviewImage (Image)
///        │     ├── BlindOverlay (Image)        ← ★ 검정 반투명 오버레이
///        │     ├── ComingSoonText (TMP)        ← ★ "업데이트 예정"
///        │     ├── ClassNameText (TMP)
///        │     └── ClassDescText (TMP)
///        ├── RightStatPanel
///        ├── NameInputPanel
///        ├── BottomButtons
///        └── ErrorPanel
///
/// [ClassBtn 프리팹]
///   ├── SelectHighlight (Image)
///   ├── ClassIcon (Image)
///   ├── ClassBtnNameText (TMP)
///   └── ComingSoonBadge (GO)   ← ★ "업데이트 예정" 뱃지
/// </summary>
public class CharacterCreateManager : MonoBehaviour
{
    [System.Serializable]
    public class ClassData
    {
        public string className;
        [TextArea(2, 4)]
        public string description;
        public Sprite previewImage;
        public Sprite buttonIcon;
        public int classType;      // Melee=0, Ranged=1, Magic=2

        [Range(0, 10)] public int statAttack;
        [Range(0, 10)] public int statDefense;
        [Range(0, 10)] public int statSpeed;
        [Range(0, 10)] public int statRange;
        [Range(0, 10)] public int statControl;
        public string classTag;

        [Tooltip("true = 업데이트 예정 (선택 불가 + 블라인드 처리)")]
        public bool comingSoon = false;   // ★
    }

    [Header("===== 패널 참조 =====")]
    [SerializeField] private GameObject createPanel;
    [SerializeField] private CharacterSelectManager selectManager;

    [Header("===== 직업 데이터 =====")]
    [SerializeField] private List<ClassData> classes = new List<ClassData>();

    [Header("===== 왼쪽 직업 버튼 =====")]
    [SerializeField] private Transform classListContainer;
    [SerializeField] private GameObject classBtnPrefab;

    [Header("===== 가운데 미리보기 =====")]
    [SerializeField] private Image classPreviewImage;
    [SerializeField] private TextMeshProUGUI classNameText;
    [SerializeField] private TextMeshProUGUI classDescText;

    [Header("===== 블라인드 (업데이트 예정) =====")]
    [Tooltip("ClassPreviewImage 위에 올려놓는 검정 반투명 Image 오브젝트")]
    [SerializeField] private GameObject blindOverlay;
    [Tooltip("가운데에 표시할 '업데이트 예정' TMP")]
    [SerializeField] private TextMeshProUGUI comingSoonText;
    [SerializeField] private string comingSoonMessage = "업데이트 예정";

    [Header("===== 오른쪽 스탯 =====")]
    [SerializeField] private TextMeshProUGUI statAtkText;
    [SerializeField] private TextMeshProUGUI statDefText;
    [SerializeField] private TextMeshProUGUI statSpdText;
    [SerializeField] private TextMeshProUGUI statRangeText;
    [SerializeField] private TextMeshProUGUI statCtrlText;
    [SerializeField] private TextMeshProUGUI classTagText;

    [Header("===== 이름 입력 =====")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TextMeshProUGUI nameLimitText;
    [SerializeField] private int maxNameLength = 12;

    [Header("===== 버튼 =====")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button backButton;

    [Header("===== 에러 패널 =====")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorText;

    private int selectedClassIndex = 0;
    private readonly List<ClassButtonUI> classBtnUIs = new List<ClassButtonUI>();
    private bool initialized = false; // ★ lazy init 플래그

    void Start()
    {
        // ★ 패널이 처음부터 활성화된 경우에도 중복 초기화 방지
        if (!initialized) Initialize();
        if (createPanel != null) createPanel.SetActive(false);
    }

    // ★ 실제 초기화 로직 분리 - Start() 또는 Show()에서 최초 1회만 실행
    private void Initialize()
    {
        initialized = true;
        BuildClassButtons();

        int firstAvailable = classes.FindIndex(c => !c.comingSoon);
        SelectClass(firstAvailable >= 0 ? firstAvailable : 0);

        SetupButtons();
        if (errorPanel != null) errorPanel.SetActive(false);
        if (blindOverlay != null) blindOverlay.SetActive(false);
        if (comingSoonText != null) comingSoonText.gameObject.SetActive(false);
        if (nameInputField != null)
        {
            nameInputField.characterLimit = maxNameLength;
            nameInputField.onValueChanged.AddListener(OnNameChanged);
        }
    }

    public void Show()
    {
        // ★ 패널이 비활성 상태로 시작해서 Start()가 안 불렸을 경우 여기서 초기화
        if (!initialized) Initialize();

        if (createPanel != null) createPanel.SetActive(true);
        if (nameInputField != null) nameInputField.text = "";
        if (nameLimitText != null) nameLimitText.text = $"0/{maxNameLength}";
        int firstAvailable = classes.FindIndex(c => !c.comingSoon);
        SelectClass(firstAvailable >= 0 ? firstAvailable : 0);
    }

    public void Hide()
    {
        if (createPanel != null) createPanel.SetActive(false);
    }

    private void BuildClassButtons()
    {
        classBtnUIs.Clear();
        foreach (Transform c in classListContainer) Destroy(c.gameObject);

        for (int i = 0; i < classes.Count; i++)
        {
            int idx = i;
            GameObject go = Instantiate(classBtnPrefab, classListContainer);
            ClassButtonUI ui = go.GetComponent<ClassButtonUI>()
                            ?? go.AddComponent<ClassButtonUI>();
            ui.Init(idx, classes[i].className, classes[i].buttonIcon,
                    classes[i].comingSoon, OnClassButtonClicked);   // ★ comingSoon 전달
            classBtnUIs.Add(ui);
        }
    }

    private void OnClassButtonClicked(int idx)
    {
        // ★ 업데이트 예정 직업 클릭 차단
        if (idx >= 0 && idx < classes.Count && classes[idx].comingSoon)
        {
            ShowError($"'{classes[idx].className}'은 업데이트 예정입니다.");
            return;
        }
        SelectClass(idx);
    }

    private void SelectClass(int idx)
    {
        if (idx < 0 || idx >= classes.Count) return;
        selectedClassIndex = idx;

        for (int i = 0; i < classBtnUIs.Count; i++)
            classBtnUIs[i].SetSelected(i == idx);

        ClassData cd = classes[idx];
        bool cs = cd.comingSoon;

        // ★ 이미지 블라인드 처리
        if (classPreviewImage != null)
        {
            classPreviewImage.sprite = cd.previewImage;
            classPreviewImage.color = cs ? new Color(0.15f, 0.15f, 0.15f, 1f) : Color.white;
        }

        // ★ 오버레이 / 업데이트 예정 텍스트
        if (blindOverlay != null) blindOverlay.SetActive(cs);
        if (comingSoonText != null)
        {
            comingSoonText.gameObject.SetActive(cs);
            if (cs) comingSoonText.text = comingSoonMessage;
        }

        // 텍스트 정보
        if (classNameText != null) classNameText.text = cd.className;
        if (classDescText != null) classDescText.text = cs ? "업데이트 예정인 직업입니다." : cd.description;
        if (classTagText != null) classTagText.text = cs ? "-" : cd.classTag;

        // ★ 스탯 블라인드
        if (statAtkText != null) statAtkText.text = cs ? "??????????" : BuildBar(cd.statAttack);
        if (statDefText != null) statDefText.text = cs ? "??????????" : BuildBar(cd.statDefense);
        if (statSpdText != null) statSpdText.text = cs ? "??????????" : BuildBar(cd.statSpeed);
        if (statRangeText != null) statRangeText.text = cs ? "??????????" : BuildBar(cd.statRange);
        if (statCtrlText != null) statCtrlText.text = cs ? "??????????" : BuildBar(cd.statControl);

        // ★ 생성 버튼 비활성화
        if (confirmButton != null) confirmButton.interactable = !cs;
    }

    private string BuildBar(int value)
        => new string('■', value) + new string('□', 10 - value);

    private void OnNameChanged(string value)
    {
        if (nameLimitText != null)
            nameLimitText.text = $"{value.Length}/{maxNameLength}";
    }

    private void SetupButtons()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnConfirmClicked()
    {
        if (selectedClassIndex >= 0 && selectedClassIndex < classes.Count
            && classes[selectedClassIndex].comingSoon)
        {
            ShowError("업데이트 예정인 직업은 선택할 수 없습니다.");
            return;
        }

        string charName = nameInputField != null ? nameInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(charName)) { ShowError("캐릭터 이름을 입력해주세요."); return; }
        if (charName.Length < 2) { ShowError("이름은 2자 이상이어야 합니다."); return; }

        int targetSlot = PlayerPrefs.GetInt("CreateTargetSlot", 0);
        string accountID = PlayerPrefs.GetString("AccountID", "Guest");
        ClassData cd = classes[selectedClassIndex];

        if (CharacterSelectManager.Instance != null)
            CharacterSelectManager.Instance.SaveSlot(targetSlot, charName, cd.classType, accountID);

        PlayerPrefs.SetString("SelectedCharacter", charName);
        PlayerPrefs.SetInt("CharacterType", cd.classType);
        PlayerPrefs.SetInt("LastSelectedSlot", targetSlot);
        PlayerPrefs.Save();

        Debug.Log($"[CharacterCreate] 생성 완료: {charName} / {cd.className} / 슬롯{targetSlot}");
        Hide();
        if (selectManager != null) selectManager.Show();
    }

    private void OnBackClicked()
    {
        Hide();
        if (selectManager != null) selectManager.Show();
    }

    private void ShowError(string msg)
    {
        if (errorPanel != null) errorPanel.SetActive(true);
        if (errorText != null) errorText.text = msg;
        CancelInvoke(nameof(HideError));
        Invoke(nameof(HideError), 2.5f);
    }

    private void HideError()
    {
        if (errorPanel != null) errorPanel.SetActive(false);
    }
}
