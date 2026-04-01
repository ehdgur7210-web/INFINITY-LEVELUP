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

        // ★ 스탯 표시 (한글 라벨 + 바)
        if (statAtkText != null) statAtkText.text = cs ? "공격력 : ??????????" : $"공격력 : {BuildBar(cd.statAttack)}";
        if (statDefText != null) statDefText.text = cs ? "방어력 : ??????????" : $"방어력 : {BuildBar(cd.statDefense)}";
        if (statSpdText != null) statSpdText.text = cs ? "스피드 : ??????????" : $"스피드 : {BuildBar(cd.statSpeed)}";
        if (statRangeText != null) statRangeText.text = cs ? "사거리 : ??????????" : $"사거리 : {BuildBar(cd.statRange)}";
        if (statCtrlText != null) statCtrlText.text = cs ? "조작성 : ??????????" : $"조작성 : {BuildBar(cd.statControl)}";

        // ★ 생성 버튼 비활성화
        if (confirmButton != null) confirmButton.interactable = !cs;
    }

    // TMP Sprite Asset: <sprite=0> = 채운 별, <sprite=1> = 빈 별
    // size 파라미터로 폰트 대비 별 크기 조절 (기본=폰트크기, 줄이면 작아짐)
    [Header("===== 별 크기 =====")]
    [SerializeField] private float starSize = 20f;

    private string BuildBar(int value)
    {
        var sb = new System.Text.StringBuilder();
        string filled = $"<sprite=0 tint=1 color=#FFD700>";
        string empty  = $"<sprite=1 tint=1 color=#555555>";

        // size 태그로 별 영역 축소
        sb.Append($"<size={starSize}>");
        for (int i = 0; i < 10; i++)
            sb.Append(i < value ? filled : empty);
        sb.Append("</size>");

        return sb.ToString();
    }

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

    [Header("===== 확인 다이얼로그 =====")]
    [Tooltip("기존 캐릭터 덮어쓰기 확인 패널 (없으면 코드로 간단 처리)")]
    [SerializeField] private GameObject confirmOverwritePanel;
    [SerializeField] private TextMeshProUGUI confirmOverwriteText;
    [SerializeField] private Button confirmOverwriteYes;
    [SerializeField] private Button confirmOverwriteNo;

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

        // ★ 이름 중복 체크 (다른 슬롯에 같은 이름이 있거나, 삭제된 세이브 파일이 남아있으면 차단)
        if (IsNameAlreadyUsed(charName))
        {
            ShowError($"'{charName}'은(는) 이미 사용된 이름입니다.\n다른 이름을 입력해주세요.");
            return;
        }

        int targetSlot = GameDataBridge.CharacterSlots.createTargetSlot;

        // ★ 기존 캐릭터가 있는 슬롯이면 확인 다이얼로그
        bool slotHasData = false;
        if (GameDataBridge.CharacterSlots.slots != null
            && targetSlot >= 0 && targetSlot < GameDataBridge.CharacterSlots.slots.Length)
        {
            var slot = GameDataBridge.CharacterSlots.slots[targetSlot];
            slotHasData = slot != null && (slot.exists || !string.IsNullOrEmpty(slot.charName));
        }

        if (slotHasData)
        {
            ShowOverwriteConfirm(targetSlot, charName);
            return;
        }

        // 빈 슬롯이면 바로 생성
        ExecuteCreateCharacter(targetSlot, charName);
    }

    private void ShowOverwriteConfirm(int slot, string newName)
    {
        string oldName = GameDataBridge.CharacterSlots.slots[slot].charName;
        int oldLevel = GameDataBridge.CharacterSlots.slots[slot].level;

        if (confirmOverwritePanel != null)
        {
            // Inspector에 확인 패널이 있으면 사용
            confirmOverwritePanel.SetActive(true);
            if (confirmOverwriteText != null)
                confirmOverwriteText.text = $"'{oldName}' (Lv.{oldLevel}) 캐릭터를 삭제하고\n새 캐릭터 '{newName}'을 생성하시겠습니까?\n\n<color=#FF4444>이 작업은 되돌릴 수 없습니다!</color>";

            if (confirmOverwriteYes != null)
            {
                confirmOverwriteYes.onClick.RemoveAllListeners();
                confirmOverwriteYes.onClick.AddListener(() =>
                {
                    confirmOverwritePanel.SetActive(false);
                    ExecuteCreateCharacter(slot, newName);
                });
            }
            if (confirmOverwriteNo != null)
            {
                confirmOverwriteNo.onClick.RemoveAllListeners();
                confirmOverwriteNo.onClick.AddListener(() => confirmOverwritePanel.SetActive(false));
            }
        }
        else
        {
            // 확인 패널이 없으면 에러 메시지로 경고
            ShowError($"슬롯에 '{oldName}' (Lv.{oldLevel})이 있습니다!\n먼저 캐릭터를 삭제한 후 생성해주세요.");
        }
    }

    private void ExecuteCreateCharacter(int targetSlot, string charName)
    {
        string accountID = GameDataBridge.CurrentUsername ?? "Guest";
        ClassData cd = classes[selectedClassIndex];

        // 기존 선택 슬롯 보존 (새 캐릭터 생성 후에도 기존 캐릭터 유지)
        int previousSlot = GameDataBridge.CharacterSlots.lastSelectedSlot;

        GameDataBridge.DeleteSaveSlot(targetSlot);
        Debug.Log($"[CharacterCreate] 슬롯 {targetSlot} 기존 세이브 삭제");

        GameDataBridge.InitNewCharacterData(targetSlot, charName, cd.classType, accountID);

        if (CharacterSelectManager.Instance != null)
            CharacterSelectManager.Instance.SaveSlot(targetSlot, charName, cd.classType, accountID);

        // 기존에 선택된 슬롯이 유효하면 유지, 없으면 새로 만든 슬롯 선택
        bool previousExists = previousSlot >= 0 && previousSlot < 4
            && GameDataBridge.CharacterSlots.slots != null
            && previousSlot < GameDataBridge.CharacterSlots.slots.Length
            && GameDataBridge.CharacterSlots.slots[previousSlot] != null
            && GameDataBridge.CharacterSlots.slots[previousSlot].exists;

        GameDataBridge.CharacterSlots.lastSelectedSlot = previousExists ? previousSlot : targetSlot;
        GameDataBridge.WriteCharacterSlots();

        GameDataBridge.WriteToFile(targetSlot);
        Debug.Log($"[CharacterCreate] 생성 완료: {charName} / {cd.className} / 슬롯{targetSlot} (선택 유지: 슬롯{GameDataBridge.CharacterSlots.lastSelectedSlot})");

        Hide();
        if (selectManager != null) selectManager.Show();
    }

    /// <summary>이름이 이미 사용 중인지 확인 (기존 슬롯 + 잔여 세이브 파일)</summary>
    private bool IsNameAlreadyUsed(string name)
    {
        int targetSlot = GameDataBridge.CharacterSlots.createTargetSlot;
        var slots = GameDataBridge.CharacterSlots.slots;

        // 1. 다른 슬롯에 같은 이름이 존재하는지
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (i == targetSlot) continue;
                if (slots[i] != null && slots[i].exists
                    && string.Equals(slots[i].charName, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // 2. 삭제됐지만 세이브 파일이 남아있는지 (파일 경로 직접 체크)
        string saveDir = Application.persistentDataPath + "/Saves/";
        string lowerName = name.ToLower();
        if (System.IO.Directory.Exists(saveDir))
        {
            foreach (string file in System.IO.Directory.GetFiles(saveDir))
            {
                if (System.IO.Path.GetFileName(file).ToLower().Contains(lowerName))
                {
                    Debug.LogWarning($"[CharacterCreate] '{name}' 이름의 잔여 세이브 파일 발견: {file}");
                    return true;
                }
            }
        }

        return false;
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
