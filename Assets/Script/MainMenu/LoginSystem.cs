using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class LoginSystem : MonoBehaviour
{
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private Toggle rememberToggle;

    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float messageDuration = 2f;

    [SerializeField] private GameObject serverSelectionPanel;

    [SerializeField] private int minUsernameLength = 4;
    [SerializeField] private int minPasswordLength = 4;

    private Coroutine messageCoroutine;

    /// <summary>서버 통신 중 여부 (중복 클릭 방지)</summary>
    private bool isProcessing;

    void Start()
    {
        GameDataBridge.ReadLoginData();
        InitializeLogin();
        SetupButtons();
        LoadRememberedCredentials();
    }

    private void InitializeLogin()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (serverSelectionPanel != null) serverSelectionPanel.SetActive(false);
        if (messageText != null) messageText.gameObject.SetActive(false);
        if (usernameInput != null) usernameInput.text = "";
        if (passwordInput != null) passwordInput.text = "";

        // ★ 기억하기 토글: 처음엔 비활성 + 꺼짐 (아이디/비번 둘 다 입력 후에만 활성)
        if (rememberToggle != null)
        {
            rememberToggle.isOn = false;
            rememberToggle.interactable = false;
        }
    }

    private void SetupButtons()
    {
        if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
        if (registerButton != null) registerButton.onClick.AddListener(OnRegisterClicked);
        if (usernameInput != null) usernameInput.onSubmit.AddListener((t) => FocusPassword());
        if (passwordInput != null) passwordInput.onSubmit.AddListener((t) => OnLoginClicked());

        // ★ 입력 변경 시 기억하기 토글 활성/비활성 갱신
        if (usernameInput != null) usernameInput.onValueChanged.AddListener(_ => RefreshRememberToggleState());
        if (passwordInput != null) passwordInput.onValueChanged.AddListener(_ => RefreshRememberToggleState());
    }

    /// <summary>
    /// 기억하기 토글의 활성 여부 갱신.
    /// 아이디/비번 둘 다 minLength 이상 입력되어야 토글 클릭 가능.
    /// </summary>
    private void RefreshRememberToggleState()
    {
        if (rememberToggle == null) return;

        string u = usernameInput != null ? usernameInput.text : "";
        string p = passwordInput != null ? passwordInput.text : "";
        bool ready = !string.IsNullOrEmpty(u) && !string.IsNullOrEmpty(p)
                     && u.Length >= minUsernameLength && p.Length >= minPasswordLength;

        rememberToggle.interactable = ready;

        // 입력이 다시 비워지면 토글도 강제 OFF (의도하지 않은 저장 방지)
        if (!ready && rememberToggle.isOn)
            rememberToggle.isOn = false;
    }

    private void FocusPassword()
    {
        if (passwordInput != null) passwordInput.Select();
    }

    // ══════════════════════════════════════════════════════
    //  로그인 (뒤끝 서버)
    // ══════════════════════════════════════════════════════

    private void OnLoginClicked()
    {
        if (isProcessing) return;

        SoundManager.Instance?.PlayButtonClick();
        string username = usernameInput != null ? usernameInput.text : "";
        string password = passwordInput != null ? passwordInput.text : "";
        if (!ValidateInput(username, password)) return;

        SetProcessing(true);
        ShowMessage("로그인 중...", Color.white);

        BackendManager.Instance.Login(username, password,
            onSuccess: () =>
            {
                SetProcessing(false);
                SoundManager.Instance?.PlayLoginSuccess();
                ShowMessage("로그인 성공!", Color.green);

                // 자격 증명 저장
                if (rememberToggle != null && rememberToggle.isOn)
                    SaveCredentials(username, password);
                else
                    ClearSavedCredentials();

                // 유저명 설정
                GameDataBridge.SetCurrentUser(username);
                SaveLoadManager.Instance?.SetCurrentUser(username);

                // 인메모리 데이터 초기화
                GameDataBridge.ResetCurrentData();

                StartCoroutine(TransitionToServerSelection());
            },
            onFail: (errorMsg) =>
            {
                SetProcessing(false);
                ShowMessage(errorMsg, Color.red);
            }
        );
    }

    // ══════════════════════════════════════════════════════
    //  회원가입 (뒤끝 서버)
    // ══════════════════════════════════════════════════════

    private void OnRegisterClicked()
    {
        if (isProcessing) return;

        SoundManager.Instance?.PlayButtonClick();
        string username = usernameInput != null ? usernameInput.text : "";
        string password = passwordInput != null ? passwordInput.text : "";
        if (!ValidateInput(username, password)) return;

        SetProcessing(true);
        ShowMessage("회원가입 중...", Color.white);

        BackendManager.Instance.SignUp(username, password,
            onSuccess: () =>
            {
                SetProcessing(false);
                SoundManager.Instance?.PlayRegister();
                ShowMessage("회원가입 완료! 로그인해주세요.", Color.green);
            },
            onFail: (errorMsg) =>
            {
                SetProcessing(false);
                ShowMessage(errorMsg, Color.red);
            }
        );
    }

    // ══════════════════════════════════════════════════════
    //  입력 검증
    // ══════════════════════════════════════════════════════

    private bool ValidateInput(string username, string password)
    {
        if (string.IsNullOrEmpty(username)) { ShowMessage("아이디를 입력해주세요.", Color.yellow); return false; }
        if (username.Length < minUsernameLength) { ShowMessage($"아이디는 {minUsernameLength}자 이상이어야 합니다.", Color.yellow); return false; }
        if (string.IsNullOrEmpty(password)) { ShowMessage("비밀번호를 입력해주세요.", Color.yellow); return false; }
        if (password.Length < minPasswordLength) { ShowMessage($"비밀번호는 {minPasswordLength}자 이상이어야 합니다.", Color.yellow); return false; }
        return true;
    }

    // ══════════════════════════════════════════════════════
    //  처리 상태 (중복 클릭 방지)
    // ══════════════════════════════════════════════════════

    private void SetProcessing(bool processing)
    {
        isProcessing = processing;
        if (loginButton != null) loginButton.interactable = !processing;
        if (registerButton != null) registerButton.interactable = !processing;
    }

    // ══════════════════════════════════════════════════════
    //  자격 증명 저장 (로컬 — 자동 입력용)
    // ══════════════════════════════════════════════════════

    private void SaveCredentials(string username, string password)
    {
        GameDataBridge.Login.rememberedUsername = username;
        GameDataBridge.Login.rememberedPassword = ObfuscatePassword(password);
        GameDataBridge.Login.rememberLogin      = true;
        GameDataBridge.WriteLoginData();
    }

    private void ClearSavedCredentials()
    {
        GameDataBridge.Login.rememberedUsername = "";
        GameDataBridge.Login.rememberedPassword = "";
        GameDataBridge.Login.rememberLogin      = false;
        GameDataBridge.WriteLoginData();
    }

    private void LoadRememberedCredentials()
    {
        LoginData data = GameDataBridge.Login;
        if (data.rememberLogin && !string.IsNullOrEmpty(data.rememberedUsername))
        {
            if (usernameInput != null)  usernameInput.text  = data.rememberedUsername;
            if (passwordInput != null)  passwordInput.text  = DeobfuscatePassword(data.rememberedPassword);

            // ★ 자동 채운 입력값에 맞춰 토글 활성화 + ON
            RefreshRememberToggleState();
            if (rememberToggle != null && rememberToggle.interactable)
                rememberToggle.isOn = true;

            // ★ 자동 로그인은 하지 않음 — 사용자가 로그인 버튼을 직접 눌러야 함
        }
    }

    // ══════════════════════════════════════════════════════
    //  UI 메시지
    // ══════════════════════════════════════════════════════

    private void ShowMessage(string message, Color color)
    {
        if (messageText == null) return;
        messageText.text = message;
        messageText.color = color;
        messageText.gameObject.SetActive(true);
        if (messageCoroutine != null) StopCoroutine(messageCoroutine);
        messageCoroutine = StartCoroutine(HideMessageAfterDelay());
    }

    private IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSeconds(messageDuration);
        if (messageText != null) messageText.gameObject.SetActive(false);
    }

    // ══════════════════════════════════════════════════════
    //  화면 전환
    // ══════════════════════════════════════════════════════

    private IEnumerator TransitionToServerSelection()
    {
        yield return new WaitForSeconds(0.5f);
        if (loginPanel != null) loginPanel.SetActive(false);
        if (serverSelectionPanel != null) serverSelectionPanel.SetActive(true);

        GameDataBridge.ReadCharacterSlots();
    }

    public void ShowLoginPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (serverSelectionPanel != null) serverSelectionPanel.SetActive(false);
    }

    // ══════════════════════════════════════════════════════
    //  비밀번호 난독화 (XOR 기반)
    // ══════════════════════════════════════════════════════

    private string ObfuscatePassword(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(raw);
        byte key = 0xA5;
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] ^= key;
        return System.Convert.ToBase64String(bytes);
    }

    private string DeobfuscatePassword(string obfuscated)
    {
        if (string.IsNullOrEmpty(obfuscated)) return obfuscated;
        try
        {
            byte[] bytes = System.Convert.FromBase64String(obfuscated);
            byte key = 0xA5;
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] ^= key;
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch (System.FormatException)
        {
            // 기존 평문 비밀번호 마이그레이션: Base64 디코딩 실패 시 원본 반환
            return obfuscated;
        }
    }

    public void Logout()
    {
        // 뒤끝 서버 로그아웃
        BackendManager.Instance?.Logout();

        // ★ 아이디 기억하기가 켜져있으면 아이디는 유지, 비밀번호만 초기화
        //   (이전: ClearSavedCredentials로 아이디까지 전부 삭제 → 기억하기 무효화)
        if (GameDataBridge.Login.rememberLogin)
        {
            // 비밀번호만 지우고 아이디 + rememberLogin 유지
            GameDataBridge.Login.rememberedPassword = "";
            GameDataBridge.WriteLoginData();
            if (passwordInput != null) passwordInput.text = "";
        }
        else
        {
            ClearSavedCredentials();
            if (usernameInput != null) usernameInput.text = "";
            if (passwordInput != null) passwordInput.text = "";
        }

        if (rememberToggle != null) rememberToggle.isOn = GameDataBridge.Login.rememberLogin;
        InitializeLogin();
    }
}
