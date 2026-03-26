using System;
using UnityEngine;
using BackEnd;
using LitJson;

/// <summary>
/// 뒤끝 SDK 매니저 (싱글톤)
/// - SDK 초기화
/// - 커스텀 로그인 / 회원가입
/// - 로그인 상태 관리
/// </summary>
public class BackendManager : MonoBehaviour
{
    public static BackendManager Instance { get; private set; }

    /// <summary>SDK 초기화 완료 여부</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>현재 로그인 상태</summary>
    public bool IsLoggedIn { get; private set; }

    /// <summary>로그인된 유저의 닉네임 (뒤끝 서버 기준)</summary>
    public string LoggedInUsername { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        InitializeBackend();
    }

    private void InitializeBackend()
    {
        var bro = Backend.Initialize();

        if (bro.IsSuccess())
        {
            Debug.Log("[BackendManager] 초기화 성공 : " + bro);
            IsInitialized = true;
        }
        else
        {
            Debug.LogError("[BackendManager] 초기화 실패 : " + bro);
            IsInitialized = false;
        }
    }

    // ══════════════════════════════════════════════════════
    //  커스텀 로그인
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 뒤끝 커스텀 로그인
    /// </summary>
    /// <param name="username">아이디</param>
    /// <param name="password">비밀번호</param>
    /// <param name="onSuccess">성공 콜백</param>
    /// <param name="onFail">실패 콜백 (에러 메시지)</param>
    public void Login(string username, string password, Action onSuccess, Action<string> onFail)
    {
        if (!IsInitialized)
        {
            onFail?.Invoke("서버 연결에 실패했습니다. 잠시 후 다시 시도해주세요.");
            return;
        }

        Backend.BMember.CustomLogin(username, password, callback =>
        {
            if (callback.IsSuccess())
            {
                IsLoggedIn = true;
                LoggedInUsername = username;
                Debug.Log($"[BackendManager] 로그인 성공: {username}");

                // ★ 닉네임 등록 (미등록 시에만)
                EnsureNickname(username);

                // VIP 데이터 로드
                VipManager.Instance?.LoadVipDataFromServer();

                // ★ 서버 우편 로드
                BackendPostManager.Instance?.LoadServerPosts();

                // ★ 채팅 서버 연결
                BackendChatManager.Instance?.ConnectChat();

                onSuccess?.Invoke();
            }
            else
            {
                string errorMsg = ParseLoginError(callback);
                Debug.LogWarning($"[BackendManager] 로그인 실패: {errorMsg}");
                onFail?.Invoke(errorMsg);
            }
        });
    }

    // ══════════════════════════════════════════════════════
    //  커스텀 회원가입
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 뒤끝 커스텀 회원가입
    /// </summary>
    /// <param name="username">아이디</param>
    /// <param name="password">비밀번호</param>
    /// <param name="onSuccess">성공 콜백</param>
    /// <param name="onFail">실패 콜백 (에러 메시지)</param>
    public void SignUp(string username, string password, Action onSuccess, Action<string> onFail)
    {
        if (!IsInitialized)
        {
            onFail?.Invoke("서버 연결에 실패했습니다. 잠시 후 다시 시도해주세요.");
            return;
        }

        Backend.BMember.CustomSignUp(username, password, callback =>
        {
            if (callback.IsSuccess())
            {
                Debug.Log($"[BackendManager] 회원가입 성공: {username}");
                onSuccess?.Invoke();
            }
            else
            {
                string errorMsg = ParseSignUpError(callback);
                Debug.LogWarning($"[BackendManager] 회원가입 실패: {errorMsg}");
                onFail?.Invoke(errorMsg);
            }
        });
    }

    // ══════════════════════════════════════════════════════
    //  로그아웃
    // ══════════════════════════════════════════════════════

    /// <summary>뒤끝 로그아웃</summary>
    public void Logout()
    {
        // ★ 채팅 서버 연결 해제
        BackendChatManager.Instance?.DisconnectChat();

        Backend.BMember.Logout();
        IsLoggedIn = false;
        LoggedInUsername = "";
        Debug.Log("[BackendManager] 로그아웃 완료");
    }

    // ══════════════════════════════════════════════════════
    //  닉네임 등록
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 뒤끝 서버에 닉네임이 없으면 username으로 등록합니다.
    /// 이미 닉네임이 있으면 아무것도 하지 않습니다.
    /// </summary>
    private void EnsureNickname(string username)
    {
        Debug.Log($"[BackendManager] ▶ EnsureNickname 호출 — username: \"{username}\"");

        Backend.BMember.GetUserInfo(callback =>
        {
            if (!callback.IsSuccess())
            {
                Debug.LogWarning($"[BackendManager] 유저 정보 조회 실패: {callback.GetMessage()}");
                return;
            }

            JsonData json = callback.GetReturnValuetoJSON();

            // 닉네임 필드 확인
            string currentNickname = null;
            if (json != null && json.ContainsKey("row") && json["row"].ContainsKey("nickname"))
            {
                var nicknameData = json["row"]["nickname"];
                if (nicknameData != null)
                    currentNickname = nicknameData.ToString();
            }

            Debug.Log($"[BackendManager] 현재 서버 닉네임: \"{currentNickname ?? "null"}\" / 목표: \"{username}\"");

            // ★ 닉네임이 이미 username과 동일하면 스킵
            if (!string.IsNullOrEmpty(currentNickname) && currentNickname == username)
            {
                Debug.Log($"[BackendManager] 닉네임 이미 정확히 등록됨: {currentNickname}");
                return;
            }

            // ★ 닉네임이 없으면 Create, 있지만 다르면 Update
            if (string.IsNullOrEmpty(currentNickname))
            {
                // 닉네임 미등록 → 신규 등록
                Debug.Log($"[BackendManager] ▶ 닉네임 신규 등록 시도: {username}");
                Backend.BMember.CreateNickname(username, nickCallback =>
                {
                    if (nickCallback.IsSuccess())
                    {
                        Debug.Log($"[BackendManager] ✅ 닉네임 신규 등록 완료: {username}");
                    }
                    else if (nickCallback.GetStatusCode() == "409")
                    {
                        string fallback = username + "_" + UnityEngine.Random.Range(1000, 9999);
                        Debug.LogWarning($"[BackendManager] 닉네임 \"{username}\" 중복 → \"{fallback}\"으로 재시도");
                        Backend.BMember.CreateNickname(fallback, retryCallback =>
                        {
                            if (retryCallback.IsSuccess())
                                Debug.Log($"[BackendManager] ✅ 닉네임 등록 완료 (대체): {fallback}");
                            else
                                Debug.LogWarning($"[BackendManager] 닉네임 등록 최종 실패: {retryCallback.GetMessage()}");
                        });
                    }
                    else
                    {
                        Debug.LogWarning($"[BackendManager] 닉네임 등록 실패: {nickCallback.GetMessage()}");
                    }
                });
            }
            else
            {
                // ★ 닉네임이 있지만 username과 다름 (내부 번호 등) → 갱신
                Debug.Log($"[BackendManager] ▶ 닉네임 갱신 시도: \"{currentNickname}\" → \"{username}\"");
                Backend.BMember.UpdateNickname(username, nickCallback =>
                {
                    if (nickCallback.IsSuccess())
                    {
                        Debug.Log($"[BackendManager] ✅ 닉네임 갱신 완료: {username}");
                    }
                    else if (nickCallback.GetStatusCode() == "409")
                    {
                        string fallback = username + "_" + UnityEngine.Random.Range(1000, 9999);
                        Debug.LogWarning($"[BackendManager] 닉네임 \"{username}\" 중복 → \"{fallback}\"으로 갱신 재시도");
                        Backend.BMember.UpdateNickname(fallback, retryCallback =>
                        {
                            if (retryCallback.IsSuccess())
                                Debug.Log($"[BackendManager] ✅ 닉네임 갱신 완료 (대체): {fallback}");
                            else
                                Debug.LogWarning($"[BackendManager] 닉네임 갱신 최종 실패: {retryCallback.GetMessage()}");
                        });
                    }
                    else
                    {
                        Debug.LogWarning($"[BackendManager] 닉네임 갱신 실패: {nickCallback.GetMessage()}");
                    }
                });
            }
        });
    }

    // ══════════════════════════════════════════════════════
    //  에러 메시지 파싱
    // ══════════════════════════════════════════════════════

    private string ParseLoginError(BackendReturnObject callback)
    {
        string statusCode = callback.GetStatusCode();
        string errorCode = callback.GetErrorCode();

        // 뒤끝 에러 코드 기반 한글 메시지
        if (statusCode == "401" || errorCode == "BadUnauthorizedException")
            return "아이디 또는 비밀번호가 올바르지 않습니다.";

        if (statusCode == "404" || errorCode == "NotFoundException")
            return "존재하지 않는 계정입니다.";

        if (statusCode == "429" || errorCode == "TooManyRequestException")
            return "너무 많은 요청입니다. 잠시 후 다시 시도해주세요.";

        return $"로그인 실패: {callback.GetMessage()}";
    }

    private string ParseSignUpError(BackendReturnObject callback)
    {
        string statusCode = callback.GetStatusCode();
        string errorCode = callback.GetErrorCode();

        if (statusCode == "409" || errorCode == "DuplicatedParameterException")
            return "이미 존재하는 아이디입니다.";

        if (statusCode == "429" || errorCode == "TooManyRequestException")
            return "너무 많은 요청입니다. 잠시 후 다시 시도해주세요.";

        return $"회원가입 실패: {callback.GetMessage()}";
    }
}
