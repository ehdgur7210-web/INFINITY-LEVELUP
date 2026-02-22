using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 로그인 시스템
/// - 아이디/비밀번호 입력
/// - 로그인 검증 (현재는 로컬, 추후 서버 연동 가능)
/// - 서버 선택 화면으로 이동
/// </summary>
public class LoginSystem : MonoBehaviour
{
    [Header("로그인 패널")]
    [SerializeField] private GameObject loginPanel;

    [Header("로그인 입력")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private Toggle rememberToggle;

    [Header("메시지")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float messageDuration = 2f;

    [Header("다음 화면")]
    [SerializeField] private GameObject serverSelectionPanel;

    [Header("설정")]
    [SerializeField] private int minUsernameLength = 4;
    [SerializeField] private int minPasswordLength = 4;

    private Coroutine messageCoroutine;

    void Start()
    {
        InitializeLogin();
        SetupButtons();
        //LoadRememberedCredentials();
    }

    /// <summary>
    /// 로그인 초기화
    /// </summary>
    private void InitializeLogin()
    {
        // 로그인 패널만 활성화
        if (loginPanel != null)
            loginPanel.SetActive(true);

        if (serverSelectionPanel != null)
            serverSelectionPanel.SetActive(false);

        // 메시지 숨기기
        if (messageText != null)
            messageText.gameObject.SetActive(false);


        // ★★★ 추가: 입력 필드 강제 초기화 ★★★
        if (usernameInput != null)
            usernameInput.text = "";

        if (passwordInput != null)
            passwordInput.text = "";
    }

    /// <summary>
    /// 버튼 이벤트 설정
    /// </summary>
    private void SetupButtons()
    {
        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginClicked);

        if (registerButton != null)
            registerButton.onClick.AddListener(OnRegisterClicked);

        // Enter 키로 로그인
        if (usernameInput != null)
            usernameInput.onSubmit.AddListener((text) => FocusPassword());

        if (passwordInput != null)
            passwordInput.onSubmit.AddListener((text) => OnLoginClicked());
    }

    /// <summary>
    /// 비밀번호 필드로 포커스 이동
    /// </summary>
    private void FocusPassword()
    {
        if (passwordInput != null)
            passwordInput.Select();
    }

    /// <summary>
    /// 로그인 버튼 클릭
    /// </summary>
    private void OnLoginClicked()
    {
        // ★ 버튼 클릭 효과음
        SoundManager.Instance?.PlayButtonClick();

        string username = usernameInput != null ? usernameInput.text : "";
        string password = passwordInput != null ? passwordInput.text : "";

        // 입력 검증
        if (!ValidateInput(username, password))
            return;

        // 로그인 시도 (현재는 로컬, 추후 서버 연동)
        if (AttemptLogin(username, password))
        {
            // ★ 로그인 성공 효과음
            SoundManager.Instance?.PlayLoginSuccess();
            // 로그인 성공
            ShowMessage("로그인 성공!", Color.green);

            // 자동 로그인 저장
            if (rememberToggle != null && rememberToggle.isOn)
            {
                SaveCredentials(username, password);
            }

            // 서버 선택 화면으로 이동
            StartCoroutine(TransitionToServerSelection());
        }
        else
        {
            // 로그인 실패
            ShowMessage("아이디 또는 비밀번호가 올바르지 않습니다.", Color.red);
        }
    }

    /// <summary>
    /// 회원가입 버튼 클릭
    /// </summary>
    private void OnRegisterClicked()
    {
        // ★ 버튼 클릭 효과음
        SoundManager.Instance?.PlayButtonClick();

        string username = usernameInput != null ? usernameInput.text : "";
        string password = passwordInput != null ? passwordInput.text : "";

        // 입력 검증
        if (!ValidateInput(username, password))
            return;

        // 이미 존재하는 계정인지 확인
        if (PlayerPrefs.HasKey($"User_{username}"))
        {
            ShowMessage("이미 존재하는 아이디입니다.", Color.red);
            return;
        }

        // 계정 생성 (간단한 로컬 저장)
        RegisterUser(username, password);
        // ★ 회원가입 성공 효과음
        SoundManager.Instance?.PlayRegister();
        ShowMessage("회원가입 완료! 로그인해주세요.", Color.green);
    }

    /// <summary>
    /// 입력 검증
    /// </summary>
    private bool ValidateInput(string username, string password)
    {
        if (string.IsNullOrEmpty(username))
        {
            ShowMessage("아이디를 입력해주세요.", Color.yellow);
            return false;
        }

        if (username.Length < minUsernameLength)
        {
            ShowMessage($"아이디는 {minUsernameLength}자 이상이어야 합니다.", Color.yellow);
            return false;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowMessage("비밀번호를 입력해주세요.", Color.yellow);
            return false;
        }

        if (password.Length < minPasswordLength)
        {
            ShowMessage($"비밀번호는 {minPasswordLength}자 이상이어야 합니다.", Color.yellow);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 로그인 시도 (로컬 검증)
    /// </summary>
    private bool AttemptLogin(string username, string password)
    {
        // PlayerPrefs에서 저장된 계정 정보 확인
        string savedPassword = PlayerPrefs.GetString($"User_{username}", "");

        //if (string.IsNullOrEmpty(savedPassword))
        //{
        //    // 계정이 없으면 자동으로 생성 (개발 편의)
        //    RegisterUser(username, password);
        //    return true;
        //}

        // 비밀번호 확인
        return savedPassword == password;
    }

    /// <summary>
    /// 사용자 등록
    /// </summary>
    private void RegisterUser(string username, string password)
    {
        PlayerPrefs.SetString($"User_{username}", password);
        PlayerPrefs.Save();
        Debug.Log($"계정 생성: {username}");
    }

    /// <summary>
    /// 자동 로그인 정보 저장
    /// </summary>
    private void SaveCredentials(string username, string password)
    {
        PlayerPrefs.SetString("RememberedUsername", username);
        PlayerPrefs.SetString("RememberedPassword", password);
        PlayerPrefs.SetInt("RememberLogin", 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 저장된 로그인 정보 불러오기
    /// </summary>
    private void LoadRememberedCredentials()
    {
        if (PlayerPrefs.GetInt("RememberLogin", 0) == 1)
        {
            string username = PlayerPrefs.GetString("RememberedUsername", "");
            string password = PlayerPrefs.GetString("RememberedPassword", "");

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                if (usernameInput != null)
                    usernameInput.text = username;

                if (passwordInput != null)
                    passwordInput.text = password;

                if (rememberToggle != null)
                    rememberToggle.isOn = true;
            }
        }
    }

    /// <summary>
    /// 메시지 표시
    /// </summary>
    private void ShowMessage(string message, Color color)
    {
        if (messageText == null)
            return;

        messageText.text = message;
        messageText.color = color;
        messageText.gameObject.SetActive(true);

        // 기존 코루틴 중지
        if (messageCoroutine != null)
            StopCoroutine(messageCoroutine);

        // 메시지 자동 숨김
        messageCoroutine = StartCoroutine(HideMessageAfterDelay());
    }

    /// <summary>
    /// 메시지 자동 숨김 코루틴
    /// </summary>
    private IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSeconds(messageDuration);

        if (messageText != null)
            messageText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 서버 선택 화면으로 전환
    /// </summary>
    private IEnumerator TransitionToServerSelection()
    {
        yield return new WaitForSeconds(0.5f);

        if (loginPanel != null)
            loginPanel.SetActive(false);

        if (serverSelectionPanel != null)
            serverSelectionPanel.SetActive(true);
    }

    /// <summary>
    /// 로그인 패널 표시 (외부에서 호출)
    /// </summary>
    public void ShowLoginPanel()
    {
        if (loginPanel != null)
            loginPanel.SetActive(true);

        if (serverSelectionPanel != null)
            serverSelectionPanel.SetActive(false);
    }

    /// <summary>
    /// 로그아웃
    /// </summary>
    public void Logout()
    {
        // 자동 로그인 정보 삭제
        PlayerPrefs.DeleteKey("RememberedUsername");
        PlayerPrefs.DeleteKey("RememberedPassword");
        PlayerPrefs.DeleteKey("RememberLogin");
        PlayerPrefs.Save();

        // 입력 필드 초기화
        if (usernameInput != null)
            usernameInput.text = "";

        if (passwordInput != null)
            passwordInput.text = "";

        if (rememberToggle != null)
            rememberToggle.isOn = false;

        InitializeLogin();
    }
}