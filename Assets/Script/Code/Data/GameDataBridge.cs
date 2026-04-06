using System;
using System.IO;
using UnityEngine;

/// <summary>
/// ══════════════════════════════════════════════════════════
/// GameDataBridge — JSON 싱글톤 데이터 브리지 (정적 클래스)
/// ══════════════════════════════════════════════════════════
///
/// ▶ 핵심 개념
///   · C# static 변수는 씬이 바뀌어도 메모리에서 사라지지 않음
///   · DontDestroyOnLoad 없이 씬 간 데이터를 유지하는 유일한 클래스
///   · MonoBehaviour가 아니므로 씬에 종속되지 않음
///
/// ▶ 씬 전환 흐름
///   1) 씬 이동 직전  → SaveLoadManager.SaveGame()
///                     → GameDataBridge.WriteToFile()  (JSON 디스크 저장)
///   2) 새 씬 로드 후 → SaveLoadManager.LoadGame()
///                     → GameDataBridge.ReadFromFile() (JSON 디스크 읽기)
///                     → 각 매니저에 데이터 적용
///
/// ▶ 충돌 해결 원리
///   · 모든 매니저(GameManager, InventoryManager 등)에서 DontDestroyOnLoad 제거
///   · 씬마다 매니저 인스턴스가 새로 생성 → 중복/충돌 원천 차단
///   · 데이터는 JSON 파일 + 이 클래스의 정적 변수가 보존
/// ══════════════════════════════════════════════════════════
/// </summary>
public static class GameDataBridge
{
    // ──────────────────────────────────────────────────────
    //  인메모리 데이터 (정적 필드 → 씬 파괴 후에도 유지)
    // ──────────────────────────────────────────────────────

    /// <summary>현재 메모리에 올라와 있는 SaveData (씬 간 공유)</summary>
    public static SaveData CurrentData { get; private set; } = new SaveData();

    /// <summary>최소 1회 이상 파일/SaveLoadManager로부터 데이터를 수신했는지</summary>
    public static bool HasData { get; private set; } = false;

    // ──────────────────────────────────────────────────────
    //  설정값 (SaveLoadManager.Awake()에서 주입)
    // ──────────────────────────────────────────────────────

    /// <summary>현재 로그인 유저명 (유저별 별도 파일 생성)</summary>
    public static string CurrentUsername { get; private set; } = "guest";

    /// <summary>캐릭터 슬롯 목록 (LoginScene 전용 파일)</summary>
    public static CharacterSlotsData CharacterSlots { get; private set; } = new CharacterSlotsData();

    /// <summary>로그인/계정 데이터 (LoginScene 전용 파일)</summary>
    public static LoginData Login { get; private set; } = new LoginData();

    /// <summary>현재 활성 슬롯 번호 (SaveCurrentTime 등에서 WriteToFile 호출 시 사용)</summary>
    public static int ActiveSlot => CurrentData?.activeCharacterSlot ?? 0;

    /// <summary>데이터 초기화 버전 (값을 올리면 전체 데이터 리셋)</summary>
    public static int DataResetVersion { get; set; } = 0;

    /// <summary>암호화 사용 여부 (SaveLoadManager Inspector에서 설정)</summary>
    public static bool UseEncryption { get; set; } = false;

    // ──────────────────────────────────────────────────────
    //  경로 설정
    // ──────────────────────────────────────────────────────

#if UNITY_EDITOR
    private const string PLATFORM_PREFIX = "EDITOR_";
#else
    private const string PLATFORM_PREFIX = "BUILD_";
#endif

    private const string SAVE_FILE_NAME = "SaveData";
    private const string CHAR_SLOTS_FILE = "CharacterSlots";
    private const string LOGIN_FILE = "LoginData";
    private const string SAVE_FILE_EXT = ".json";
    private const string ENCRYPTION_KEY = "MySecretKey123!@#";
    private const string RESET_VERSION_KEY = "DataResetVersion";

    private static string SaveDirectory => Application.persistentDataPath + "/Saves/";

    // ══════════════════════════════════════════════════════
    //  외부 설정 메서드
    // ══════════════════════════════════════════════════════

    /// <summary>로그인 성공 후 호출 — 유저별 저장 경로 분기</summary>
    public static void SetCurrentUser(string username)
    {
        string newUser = string.IsNullOrEmpty(username) ? "guest" : username;

        // ★ 계정이 바뀌면 이전 계정의 슬롯 데이터 클리어
        if (CurrentUsername != newUser)
        {
            CharacterSlots = new CharacterSlotsData();
            Debug.Log($"[GameDataBridge] 계정 전환 ({CurrentUsername} → {newUser}) → CharacterSlots 초기화");
        }

        CurrentUsername = newUser;
        Debug.Log($"[GameDataBridge] 유저 설정: {CurrentUsername}");
    }

    /// <summary>SaveLoadManager가 수집한 데이터를 인메모리에 등록</summary>
    public static void SetData(SaveData data)
    {
        int prevEquip = CurrentData?.equipmentData?.slots?.Count ?? -1;
        CurrentData = data ?? new SaveData();
        HasData = true;
        int newEquip = CurrentData?.equipmentData?.slots?.Count ?? -1;
        if (prevEquip != newEquip)
            Debug.Log($"[EQUIP-TRACE] GameDataBridge.SetData: 장비 {prevEquip}개 → {newEquip}개");
    }

    /// <summary>
    /// ★ 인메모리 CurrentData를 기본값으로 초기화
    /// 계정 전환 시 이전 유저 데이터가 남는 것을 방지
    /// </summary>
    public static void ResetCurrentData()
    {
        CurrentData = new SaveData();
        HasData = false;
        Debug.Log("[GameDataBridge] ★ CurrentData 초기화 완료 (계정 전환)");
    }

    /// <summary>
    /// ★ 새 캐릭터 생성 시 CurrentData를 기본값으로 완전 초기화
    /// 이전 캐릭터의 골드, 장비, 레벨 등이 남는 것을 방지
    /// </summary>
    public static void InitNewCharacterData(int slot, string charName, int classType, string accountID)
    {
        CurrentData = new SaveData
        {
            // 기본 플레이어 데이터
            playerLevel = 1,
            playerExp = 0,
            playerGold = 0,
            playerGem = 0,
            playerHealth = 100f,
            playerMaxHealth = 100f,
            basePlayerMaxHealth = 100f,
            playerMana = 50f,
            playerMaxMana = 50f,

            // 캐릭터 정보
            selectedCharacterName = charName,
            characterClassType = classType,
            activeCharacterSlot = slot,
            accountID = accountID,

            // 리소스 기본값
            equipmentTickets = 100,
            companionTickets = 50,
            relicTickets = 30,
            crystals = 0,
            essences = 0,
            fragments = 0,
            cropPoints = 0,

            // 가챠 기본값
            gachaLevel = 1,
            gachaCount = 0,

            // ★ 오프라인 보상 — 생성 시점을 기준으로 초기화
            lastLogoutTime = DateTime.Now.ToString("o"),
            accumulatedOfflineMinutes = 0f,

            // 나머지 필드는 SaveData 기본값 (null / 0 / false)
        };
        HasData = true;
        Debug.Log($"[GameDataBridge] ★ 새 캐릭터 데이터 초기화: {charName} (슬롯{slot})");
    }

    // ══════════════════════════════════════════════════════
    //  파일 I/O
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 현재 인메모리 데이터를 JSON 파일로 저장
    /// SaveLoadManager.SaveGame()에서 호출
    /// </summary>
    public static bool WriteToFile(int slot = 0)
    {
        try
        {
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);

            string json = JsonUtility.ToJson(CurrentData, true);

            if (UseEncryption)
                json = EncryptString(json);

            File.WriteAllText(GetFilePath(slot), json);
            Debug.Log($"[GameDataBridge] ✅ JSON 저장 완료 → {GetFilePath(slot)}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataBridge] 저장 실패: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// JSON 파일을 읽어 인메모리 데이터 갱신
    /// SaveLoadManager.LoadGame()에서 호출
    /// </summary>
    public static bool ReadFromFile(int slot = 0)
    {
        string path = GetFilePath(slot);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[GameDataBridge] 저장 파일 없음: {path}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);

            if (UseEncryption)
                json = DecryptString(json);

            SaveData data = JsonUtility.FromJson<SaveData>(json);
            CurrentData = data ?? new SaveData();
            HasData = true;

            Debug.Log($"[GameDataBridge] ✅ JSON 로드 완료 ← {path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataBridge] 로드 실패: {e.Message}");
            return false;
        }
    }

    /// <summary>해당 슬롯에 저장 파일이 있는지 확인</summary>
    public static bool FileExists(int slot = 0) => File.Exists(GetFilePath(slot));

    /// <summary>저장 파일 경로 반환</summary>
    public static string GetFilePath(int slot = 0)
        => $"{SaveDirectory}{SAVE_FILE_NAME}_{PLATFORM_PREFIX}{CurrentUsername}_{slot}{SAVE_FILE_EXT}";

    // ★ 캐릭터 슬롯을 유저별로 분리 (이전: 전체 공용 파일 1개)
    private static string CharSlotsFilePath
        => $"{SaveDirectory}{CHAR_SLOTS_FILE}_{CurrentUsername}{SAVE_FILE_EXT}";

    private static string LoginFilePath
        => $"{SaveDirectory}{LOGIN_FILE}{SAVE_FILE_EXT}";

    // ══════════════════════════════════════════════════════
    //  캐릭터 슬롯 파일 I/O
    // ══════════════════════════════════════════════════════

    public static bool WriteCharacterSlots()
    {
        try
        {
            if (!Directory.Exists(SaveDirectory)) Directory.CreateDirectory(SaveDirectory);
            File.WriteAllText(CharSlotsFilePath, JsonUtility.ToJson(CharacterSlots, true));
            return true;
        }
        catch (Exception e) { Debug.LogError($"[GameDataBridge] CharacterSlots 저장 실패: {e.Message}"); return false; }
    }

    public static bool ReadCharacterSlots()
    {
        if (!File.Exists(CharSlotsFilePath))
        {
            // ★ 파일 없음 → 이전 계정 데이터가 남아있지 않도록 초기화
            CharacterSlots = new CharacterSlotsData();
            Debug.Log($"[GameDataBridge] CharacterSlots 파일 없음 (유저:{CurrentUsername}) → 빈 슬롯 초기화");
            return false;
        }
        try
        {
            CharacterSlotsData data = JsonUtility.FromJson<CharacterSlotsData>(File.ReadAllText(CharSlotsFilePath));
            CharacterSlots = data ?? new CharacterSlotsData();
            // 슬롯 배열 크기 보장
            if (CharacterSlots.slots == null || CharacterSlots.slots.Length < 4)
            {
                CharacterSlotEntry[] newSlots = new CharacterSlotEntry[4];
                for (int i = 0; i < 4; i++)
                    newSlots[i] = (CharacterSlots.slots != null && i < CharacterSlots.slots.Length)
                        ? CharacterSlots.slots[i] : new CharacterSlotEntry();
                CharacterSlots.slots = newSlots;
            }
            return true;
        }
        catch (Exception e) { Debug.LogError($"[GameDataBridge] CharacterSlots 로드 실패: {e.Message}"); return false; }
    }

    /// <summary>특정 슬롯의 게임 세이브 파일 삭제</summary>
    public static void DeleteSaveSlot(int slot)
    {
        string path = GetFilePath(slot);
        if (File.Exists(path)) File.Delete(path);
    }

    // ══════════════════════════════════════════════════════
    //  로그인 데이터 파일 I/O
    // ══════════════════════════════════════════════════════

    public static bool WriteLoginData()
    {
        try
        {
            if (!Directory.Exists(SaveDirectory)) Directory.CreateDirectory(SaveDirectory);
            File.WriteAllText(LoginFilePath, JsonUtility.ToJson(Login, true));
            return true;
        }
        catch (Exception e) { Debug.LogError($"[GameDataBridge] LoginData 저장 실패: {e.Message}"); return false; }
    }

    public static bool ReadLoginData()
    {
        if (!File.Exists(LoginFilePath)) return false;
        try
        {
            LoginData data = JsonUtility.FromJson<LoginData>(File.ReadAllText(LoginFilePath));
            Login = data ?? new LoginData();
            if (Login.accounts == null) Login.accounts = new System.Collections.Generic.List<LoginAccountEntry>();
            return true;
        }
        catch (Exception e) { Debug.LogError($"[GameDataBridge] LoginData 로드 실패: {e.Message}"); return false; }
    }

    // ══════════════════════════════════════════════════════
    //  데이터 버전 관리 (구버전 자동 삭제)
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 게임 시작 시 호출 — dataResetVersion이 바뀌면 전체 초기화
    /// Inspector에서 DataResetVersion 숫자를 올리고 빌드하면 모든 유저 데이터 삭제
    /// </summary>
    public static void CheckAndClearOldData()
    {
        int savedVer = PlayerPrefs.GetInt(RESET_VERSION_KEY, -1);

        if (savedVer != DataResetVersion)
        {
            Debug.Log($"[GameDataBridge] ★ 데이터 버전 변경 ({savedVer} → {DataResetVersion}) → 전체 초기화");
            DeleteAllFiles();

            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetInt(RESET_VERSION_KEY, DataResetVersion);
            PlayerPrefs.Save();

            CurrentData = new SaveData();
            HasData = false;

            Debug.Log("[GameDataBridge] ✅ 초기화 완료 → 기본값으로 시작");
        }
        else
        {
            Debug.Log("[GameDataBridge] 데이터 버전 동일 → 저장 데이터 유지");
        }
    }

    /// <summary>Saves 폴더의 모든 JSON 파일 삭제</summary>
    public static void DeleteAllFiles()
    {
        if (!Directory.Exists(SaveDirectory)) return;

        foreach (string file in Directory.GetFiles(SaveDirectory, "*.json"))
        {
            File.Delete(file);
            Debug.Log($"[GameDataBridge] 삭제: {file}");
        }

        CharacterSlots = new CharacterSlotsData();
        Login          = new LoginData();
        CurrentData    = new SaveData();
        HasData        = false;
    }

    // ══════════════════════════════════════════════════════
    //  에디터 전용 유틸
    // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Game/데이터 초기화 (테스트용)")]
    static void ClearAllSaveDataEditor()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        string path = Application.persistentDataPath + "/Saves/";
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
            Debug.Log($"[GameDataBridge] 폴더 전체 삭제: {path}");
        }

        CurrentData = new SaveData();
        HasData = false;

        UnityEditor.EditorApplication.Beep();
        Debug.Log("✅ [GameDataBridge] 에디터 + 빌드 저장 데이터 전부 삭제 완료!");
    }

    [UnityEditor.MenuItem("Game/저장 폴더 열기")]
    static void OpenSaveFolderEditor()
    {
        string path = Application.persistentDataPath + "/Saves/";
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        UnityEditor.EditorUtility.RevealInFinder(path);
        Debug.Log($"[GameDataBridge] 저장 폴더: {path}");
    }
#endif

    // ══════════════════════════════════════════════════════
    //  암호화 (XOR + Base64)
    // ══════════════════════════════════════════════════════

    private static string EncryptString(string text)
    {
        char[] chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            chars[i] = (char)(chars[i] ^ ENCRYPTION_KEY[i % ENCRYPTION_KEY.Length]);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(chars));
    }

    private static string DecryptString(string encryptedText)
    {
        byte[] bytes = Convert.FromBase64String(encryptedText);
        char[] chars = System.Text.Encoding.UTF8.GetString(bytes).ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            chars[i] = (char)(chars[i] ^ ENCRYPTION_KEY[i % ENCRYPTION_KEY.Length]);
        return new string(chars);
    }
}

// ══════════════════════════════════════════════════════════
//  캐릭터 슬롯 데이터
// ══════════════════════════════════════════════════════════

[System.Serializable]
public class CharacterSlotEntry
{
    public bool   exists    = false;
    public string charName  = "";
    public int    classType = 0;
    public int    level     = 1;
    public string accountID = "";
}

[System.Serializable]
public class CharacterSlotsData
{
    public CharacterSlotEntry[] slots           = new CharacterSlotEntry[4];
    public int                  lastSelectedSlot = -1;
    public int                  createTargetSlot = 0;

    public CharacterSlotsData()
    {
        slots = new CharacterSlotEntry[4];
        for (int i = 0; i < 4; i++) slots[i] = new CharacterSlotEntry();
    }
}

// ══════════════════════════════════════════════════════════
//  로그인 / 계정 데이터
// ══════════════════════════════════════════════════════════

[System.Serializable]
public class LoginAccountEntry
{
    public string username = "";
    public string password = "";
}

[System.Serializable]
public class LoginData
{
    public System.Collections.Generic.List<LoginAccountEntry> accounts = new System.Collections.Generic.List<LoginAccountEntry>();
    public string rememberedUsername = "";
    public string rememberedPassword = "";
    public bool   rememberLogin      = false;

    public bool HasUser(string username) =>
        accounts.Exists(a => a.username == username);

    public string GetPassword(string username)
    {
        LoginAccountEntry acc = accounts.Find(a => a.username == username);
        return acc != null ? acc.password : "";
    }

    public void SetPassword(string username, string password)
    {
        LoginAccountEntry acc = accounts.Find(a => a.username == username);
        if (acc != null) acc.password = password;
        else accounts.Add(new LoginAccountEntry { username = username, password = password });
    }
}