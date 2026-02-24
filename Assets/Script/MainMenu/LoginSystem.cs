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

#if UNITY_EDITOR
    private const string PLATFORM_PREFIX = "EDITOR_";
#else
    private const string PLATFORM_PREFIX = "";
#endif

    private string UserKey(string username) => $"{PLATFORM_PREFIX}User_{username}";
    private string KEY_REMEMBER_USER => $"{PLATFORM_PREFIX}RememberedUsername";
    private string KEY_REMEMBER_PASS => $"{PLATFORM_PREFIX}RememberedPassword";
    private string KEY_REMEMBER_LOGIN => $"{PLATFORM_PREFIX}RememberLogin";

    void Start()
    {
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
    }

    private void SetupButtons()
    {
        if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
        if (registerButton != null) registerButton.onClick.AddListener(OnRegisterClicked);
        if (usernameInput != null) usernameInput.onSubmit.AddListener((t) => FocusPassword());
        if (passwordInput != null) passwordInput.onSubmit.AddListener((t) => OnLoginClicked());
    }

    private void FocusPassword()
    {
        if (passwordInput != null) passwordInput.Select();
    }

    private void OnLoginClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        string username = usernameInput != null ? usernameInput.text : "";
        string password = passwordInput != null ? passwordInput.text : "";
        if (!ValidateInput(username, password)) return;

        if (AttemptLogin(username, password))
        {
            SoundManager.Instance?.PlayLoginSuccess();
            ShowMessage("로그인 성공!", Color.green);
            if (rememberToggle != null && rememberToggle.isOn)
                SaveCredentials(username, password);
            else
                ClearSavedCredentials();
            SaveLoadManager.Instance?.SetCurrentUser(username);
            StartCoroutine(TransitionToServerSelection());
        }
        else
        {
            ShowMessage("아이디 또는 비밀번호가 올바르지 않습니다.", Color.red);
        }
    }

    private void OnRegisterClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        string username = usernameInput != null ? usernameInput.text : "";
        string password = passwordInput != null ? passwordInput.text : "";
        if (!ValidateInput(username, password)) return;

        if (PlayerPrefs.HasKey(UserKey(username)))
        {
            ShowMessage("이미 존재하는 아이디입니다.", Color.red);
            return;
        }
        RegisterUser(username, password);
        SoundManager.Instance?.PlayRegister();
        ShowMessage("회원가입이 완료되었습니다.", Color.green);
    }

    private bool ValidateInput(string username, string password)
    {
        if (string.IsNullOrEmpty(username)) { ShowMessage("아이디를 입력해주세요.", Color.yellow); return false; }
        if (username.Length < minUsernameLength) { ShowMessage($"아이디는 {minUsernameLength}자 이상이어야 합니다.", Color.yellow); return false; }
        if (string.IsNullOrEmpty(password)) { ShowMessage("비밀번호를 입력해주세요.", Color.yellow); return false; }
        if (password.Length < minPasswordLength) { ShowMessage($"비밀번호는 {minPasswordLength}자 이상이어야 합니다.", Color.yellow); return false; }
        return true;
    }

    private bool AttemptLogin(string username, string password)
    {
        return PlayerPrefs.GetString(UserKey(username), "") == password;
    }

    private void RegisterUser(string username, string password)
    {
        PlayerPrefs.SetString(UserKey(username), password);
        PlayerPrefs.Save();
    }

    private void SaveCredentials(string username, string password)
    {
        PlayerPrefs.SetString(KEY_REMEMBER_USER, username);
        PlayerPrefs.SetString(KEY_REMEMBER_PASS, password);
        PlayerPrefs.SetInt(KEY_REMEMBER_LOGIN, 1);
        PlayerPrefs.Save();
    }

    private void ClearSavedCredentials()
    {
        PlayerPrefs.DeleteKey(KEY_REMEMBER_USER);
        PlayerPrefs.DeleteKey(KEY_REMEMBER_PASS);
        PlayerPrefs.DeleteKey(KEY_REMEMBER_LOGIN);
        PlayerPrefs.Save();
    }

    private void LoadRememberedCredentials()
    {
        if (PlayerPrefs.GetInt(KEY_REMEMBER_LOGIN, 0) == 1)
        {
            string username = PlayerPrefs.GetString(KEY_REMEMBER_USER, "");
            string password = PlayerPrefs.GetString(KEY_REMEMBER_PASS, "");
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                if (usernameInput != null) usernameInput.text = username;
                if (passwordInput != null) passwordInput.text = password;
                if (rememberToggle != null) rememberToggle.isOn = true;
            }
        }
    }

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

    private IEnumerator TransitionToServerSelection()
    {
        yield return new WaitForSeconds(0.5f);
        if (loginPanel != null) loginPanel.SetActive(false);
        if (serverSelectionPanel != null) serverSelectionPanel.SetActive(true);
    }

    public void ShowLoginPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (serverSelectionPanel != null) serverSelectionPanel.SetActive(false);
    }

    public void Logout()
    {
        ClearSavedCredentials();
        if (usernameInput != null) usernameInput.text = "";
        if (passwordInput != null) passwordInput.text = "";
        if (rememberToggle != null) rememberToggle.isOn = false;
        InitializeLogin();
    }
}