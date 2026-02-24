using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// JSON 기반 세이브/로드 매니저
///
/// ✅ 수정사항 전체:
///   1. ApplySaveData - baseMaxHealth 복원 (HP바 처음에 낮게 보이는 버그 수정)
///   2. ApplySaveData - 로드 시 HP/마나 풀로 채워서 시작
///   3. CheckAndClearOldSaveData - dataResetVersion 수동 방식으로 변경 (buildGUID 제거)
///   4. 에디터 메뉴 "Game/데이터 초기화" - EDITOR + BUILD 파일 모두 삭제
///   5. 에디터 메뉴 "Game/저장 폴더 열기" 추가
/// </summary>
// ★ Fix: Script Execution Order를 코드로 강제 지정
// SaveLoadManager가 GameManager보다 먼저 Awake() 실행 보장
[DefaultExecutionOrder(-200)]
public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    // ★ GameManager가 이 플래그를 확인하고 LoadGameData 실행 시점 결정
    public static bool IsInitialized { get; private set; } = false;

    [Header("저장 설정")]
    [SerializeField] private bool useEncryption = false;        // 암호화 사용 여부
    [SerializeField] private bool autoSave = true;              // 자동 저장
    [SerializeField] private float autoSaveInterval = 300f;     // 자동 저장 간격 (5분 = 300초)
    [SerializeField] private int maxSaveSlots = 3;              // 최대 저장 슬롯 수

    // ── 저장 경로 ──────────────────────────────
    private string saveDirectory;
    private const string SAVE_FILE_NAME = "SaveData";
    private const string SAVE_FILE_EXTENSION = ".json";
    private const string BUILD_VERSION_KEY = "BuildVersion";

    // ── 암호화 키 ──────────────────────────────
    private const string ENCRYPTION_KEY = "MySecretKey123!@#";

    // ── 자동 저장 타이머 ────────────────────────
    private float autoSaveTimer = 0f;

    // ── 현재 로그인 유저 ────────────────────────
    // 아이디마다 다른 저장 파일을 사용할 수 있음
    private string currentUsername = "guest";

    // ── 에디터/빌드 구분 접두사 ─────────────────
    // 에디터에서 플레이한 데이터가 실제 빌드에 섞이는 것을 방지
#if UNITY_EDITOR
    private const string PLATFORM_PREFIX = "EDITOR_";
#else
    private const string PLATFORM_PREFIX = "BUILD_";
#endif

    // ══════════════════════════════════════════════════════════════
    // ★ 에디터 전용 메뉴 (Unity 상단 Game 메뉴에 표시됨)
    // ══════════════════════════════════════════════════════════════
#if UNITY_EDITOR
    /// <summary>
    /// [Game → 데이터 초기화 (테스트용)]
    /// EDITOR_ + BUILD_ 파일 전부 삭제 + PlayerPrefs 초기화
    /// </summary>
    [UnityEditor.MenuItem("Game/데이터 초기화 (테스트용)")]
    static void ClearAllSaveDataEditor()
    {
        // PlayerPrefs 전체 삭제 (버전 기록 포함)
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        // ✅ 핵심: Saves 폴더 자체를 통째로 삭제
        // → SaveData_EDITOR_*.json 과 SaveData_BUILD_*.json 모두 삭제됨
        string path = Application.persistentDataPath + "/Saves/";
        if (System.IO.Directory.Exists(path))
        {
            System.IO.Directory.Delete(path, true);
            Debug.Log($"[SaveLoadManager] 폴더 전체 삭제: {path}");
        }

        UnityEditor.EditorApplication.Beep(); // 완료 알림음
        Debug.Log("✅ [SaveLoadManager] 에디터 + 빌드 저장 데이터 전부 삭제 완료! 다시 플레이하면 새 게임입니다.");
    }

    /// <summary>
    /// [Game → 저장 폴더 열기]
    /// 탐색기로 저장 폴더를 직접 열어서 파일 확인/삭제 가능
    /// </summary>
    [UnityEditor.MenuItem("Game/저장 폴더 열기")]
    static void OpenSaveFolderEditor()
    {
        string path = Application.persistentDataPath + "/Saves/";
        if (!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);

        UnityEditor.EditorUtility.RevealInFinder(path);
        Debug.Log($"[SaveLoadManager] 저장 폴더: {path}");
    }
#endif

    // ══════════════════════════════════════════════════════════════
    // 로그인 / 유저 설정
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 로그인 성공 후 호출 - 유저별 저장 경로 설정
    /// </summary>
    public void SetCurrentUser(string username)
    {
        currentUsername = string.IsNullOrEmpty(username) ? "guest" : username;
        Debug.Log($"[SaveLoadManager] 현재 유저: {currentUsername}");
    }

    public string GetCurrentUser() => currentUsername;

    // ══════════════════════════════════════════════════════════════
    // Unity 생명주기
    // ══════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            IsInitialized = false; // 초기화 시작
            InitializeSaveSystem();
            IsInitialized = true;  // ★ 초기화 완료 - GameManager가 이제 LoadGameData 가능
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // 자동 저장 타이머 - autoSaveInterval마다 슬롯 0에 자동 저장
        if (autoSave)
        {
            autoSaveTimer += Time.deltaTime;
            if (autoSaveTimer >= autoSaveInterval)
            {
                autoSaveTimer = 0f;
                SaveGame(0);
                Debug.Log("[SaveLoadManager] 자동 저장 완료!");
            }
        }
    }

    // ── 저장 시스템 초기화 ──────────────────────
    private void InitializeSaveSystem()
    {
        // 저장 경로 설정 (플랫폼별로 자동 설정됨)
        // Android: /data/data/<패키지명>/files/Saves/
        // iOS:     <앱>/Documents/Saves/
        // Windows: AppData/LocalLow/<회사>/<앱>/Saves/
        saveDirectory = Application.persistentDataPath + "/Saves/";

        // ✅ 빌드가 바뀌었으면 이전 저장 데이터 자동 삭제
        CheckAndClearOldSaveData();

        // 저장 폴더가 없으면 생성
        if (!Directory.Exists(saveDirectory))
            Directory.CreateDirectory(saveDirectory);

        Debug.Log($"[SaveLoadManager] 저장 경로: {saveDirectory}");
    }

    // ══════════════════════════════════════════════════════════════
    // 저장 (Save)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 게임 저장 (특정 슬롯에)
    /// 예: SaveGame(0) → 슬롯 0에 저장
    /// </summary>
    public void SaveGame(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxSaveSlots)
        {
            Debug.LogError($"[SaveLoadManager] 잘못된 슬롯 인덱스: {slotIndex}");
            return;
        }

        try
        {
            // 현재 게임 상태 수집
            SaveData saveData = CollectSaveData();

            // JSON 변환 (pretty print = 보기 좋게 들여쓰기)
            string json = JsonUtility.ToJson(saveData, true);

            // 암호화 (Inspector에서 useEncryption 체크 시)
            if (useEncryption)
                json = EncryptString(json);

            // 파일에 저장
            string filePath = GetSaveFilePath(slotIndex);
            File.WriteAllText(filePath, json);

            Debug.Log($"[SaveLoadManager] 저장 완료! 슬롯: {slotIndex} | 경로: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoadManager] 저장 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 현재 게임 상태를 SaveData 객체로 수집
    /// </summary>
    private SaveData CollectSaveData()
    {
        SaveData data = new SaveData();

        // 저장 시간 기록
        data.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // ── GameManager 데이터 ──────────────────
        if (GameManager.Instance != null)
        {
            data.playerGold = GameManager.Instance.PlayerGold;
            data.playerGem = GameManager.Instance.PlayerGem;
            data.playerExp = GameManager.Instance.PlayerExp;
            data.playerLevel = GameManager.Instance.PlayerLevel;
        }

        // ── PlayerStats 데이터 ──────────────────
        PlayerStats playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats != null)
        {
            data.playerHealth = playerStats.currentHealth;
            data.playerMaxHealth = playerStats.maxHealth;
            // ✅ [유령 스탯 수정] 장비 제외한 순수 기본 HP 별도 저장
            data.basePlayerMaxHealth = playerStats.baseMaxHealth;
            data.playerMana = playerStats.currentMana;
            data.playerMaxMana = playerStats.maxMana;
        }

        // ── 인벤토리 데이터 ──────────────────
        if (InventoryManager.Instance != null)
            data.inventoryItems = InventoryManager.Instance.GetInventoryData();

        // ── 퀘스트 데이터 ──────────────────
        if (QuestManager.Instance != null)
            data.questData = QuestManager.Instance.GetQuestData();

        // ── 장비 데이터 ──────────────────
        if (EquipmentManager.Instance != null)
            data.equipmentData = EquipmentManager.Instance.GetEquipmentSaveData();

        // ── 업적 데이터 ──────────────────
        if (AchievementSystem.Instance != null)
            data.achievementSaveData = AchievementSystem.Instance.GetAchievementSaveData();

        // ── 티켓 / 광물 데이터 ──────────────────
        if (ResourceBarManager.Instance != null)
        {
            data.equipmentTickets = ResourceBarManager.Instance.equipmentTickets;
            data.companionTickets = ResourceBarManager.Instance.companionTickets;
            data.relicTickets = ResourceBarManager.Instance.relicTickets;
            data.crystals = ResourceBarManager.Instance.crystals;
            data.essences = ResourceBarManager.Instance.essences;
            data.fragments = ResourceBarManager.Instance.fragments;
        }

        // ── 가챠 진행도 ──────────────────
        if (GachaManager.Instance != null)
        {
            data.gachaLevel = GachaManager.Instance.currentLevel;
            data.gachaCount = GachaManager.Instance.currentGachaCount;
        }

        // ✅ [아이템 소실 수정] 메일 데이터 저장
        if (MailManager.Instance != null)
            data.mailData = MailManager.Instance.GetMailSaveData();

        // ── 플레이어 위치 / 현재 씬 ──────────────────
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            data.playerPosition = player.transform.position;
        }
        // 씬은 항상 현재 활성 씬으로 저장 (플레이어가 없어도 저장)
        data.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        return data;
    }

    // ══════════════════════════════════════════════════════════════
    // 로드 (Load)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 게임 로드 (특정 슬롯에서)
    /// 성공 시 true, 실패 시 false 반환
    /// </summary>
    public bool LoadGame(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxSaveSlots)
        {
            Debug.LogError($"[SaveLoadManager] 잘못된 슬롯 인덱스: {slotIndex}");
            return false;
        }

        string filePath = GetSaveFilePath(slotIndex);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"[SaveLoadManager] 저장 파일 없음: {filePath}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(filePath);

            // 복호화
            if (useEncryption)
                json = DecryptString(json);

            // JSON → SaveData 변환
            SaveData saveData = JsonUtility.FromJson<SaveData>(json);

            // 게임에 적용
            ApplySaveData(saveData);

            Debug.Log($"[SaveLoadManager] 로드 완료! 슬롯: {slotIndex}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoadManager] 로드 실패: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// SaveData를 실제 게임에 적용
    /// </summary>
    private void ApplySaveData(SaveData data)
    {
        // ── GameManager 데이터 적용 ──────────────────
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGold(data.playerGold);
            GameManager.Instance.SetGem(data.playerGem);
            GameManager.Instance.LoadPlayerData(data.playerExp, data.playerLevel);
        }

        // ── PlayerStats 데이터 적용 ──────────────────
        PlayerStats playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats != null)
        {
            // ✅ [장비 유령 스탯 수정] baseMaxHealth는 장비 보너스를 제외한 순수 기본 HP
            // 이전 코드에서 playerMaxHealth(장비 포함 합산)를 baseMaxHealth에 저장해
            // 장비 해제 후에도 높은 HP가 유지되는 버그 수정
            // → basePlayerMaxHealth를 별도로 저장하고 복원
            if (data.basePlayerMaxHealth > 0)
            {
                // 새 세이브 데이터 (basePlayerMaxHealth 필드 있음)
                playerStats.baseMaxHealth = data.basePlayerMaxHealth;
                playerStats.maxHealth = data.playerMaxHealth;
            }
            else
            {
                // 구 세이브 데이터 호환: 장비 스탯 없다고 가정하고 playerMaxHealth를 기본값으로 사용
                playerStats.baseMaxHealth = data.playerMaxHealth;
                playerStats.maxHealth = data.playerMaxHealth;
            }

            playerStats.maxMana = data.playerMaxMana;
            playerStats.currentHealth = playerStats.maxHealth;
            playerStats.currentMana = playerStats.maxMana;

            // UI 즉시 갱신
            playerStats.UpdateStatsUI();
        }

        // ── 인벤토리 데이터 적용 ──────────────────
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.LoadInventoryData(data.inventoryItems);

        // ── 퀘스트 데이터 적용 ──────────────────
        if (QuestManager.Instance != null)
            QuestManager.Instance.LoadQuestData(data.questData);

        // ✅ [아이템 소실 수정] 메일 데이터 복원
        if (MailManager.Instance != null && data.mailData != null)
        {
            if (ItemDatabase.Instance != null && ItemDatabase.Instance.IsReady)
                MailManager.Instance.LoadMailSaveData(data.mailData);
            else
                StartCoroutine(WaitAndLoadMail(data.mailData));
        }

        // ── 업적 데이터 적용 ──────────────────
        // ✅ [Bug6 수정] 장비 데이터 적용은 아래 블록(ItemDatabase 준비 확인 후)에서 한 번만 처리
        if (AchievementSystem.Instance != null && data.achievementSaveData != null)
            AchievementSystem.Instance.LoadAchievementSaveData(data.achievementSaveData);

        // ── 티켓 / 광물 데이터 복원 ──────────────────
        if (ResourceBarManager.Instance != null)
        {
            ResourceBarManager.Instance.equipmentTickets = data.equipmentTickets;
            ResourceBarManager.Instance.companionTickets = data.companionTickets;
            ResourceBarManager.Instance.relicTickets = data.relicTickets;
            ResourceBarManager.Instance.crystals = data.crystals;
            ResourceBarManager.Instance.essences = data.essences;
            ResourceBarManager.Instance.fragments = data.fragments;
            ResourceBarManager.Instance.UpdateAllResourceUI();
            Debug.Log($"[SaveLoadManager] 티켓 복원 완료 - 장비:{data.equipmentTickets} 동료:{data.companionTickets}");
        }

        // ── 가챠 진행도 복원 ──────────────────
        if (GachaManager.Instance != null)
        {
            GachaManager.Instance.currentLevel = Mathf.Max(1, data.gachaLevel);
            GachaManager.Instance.currentGachaCount = data.gachaCount;
            GachaManager.Instance.UpdateGachaPool(); // 레벨에 맞는 풀 재설정
            Debug.Log($"[SaveLoadManager] 가챠 복원 완료 - Lv.{data.gachaLevel} ({data.gachaCount}회)");
        }

        // ── 장비 데이터 적용 (ItemDatabase 준비 후, 단 한 번만 실행) ──────────────────
        // ✅ [Bug6 수정] 이 블록 하나에서만 LoadEquipmentSaveData 호출
        // ✅ [Bug5 수정] 장비 로드 완료 후 SkillManager.RoutineRefreshEquipmentSkills가
        //    자동으로 핫바를 재구성하므로 별도 처리 불필요
        if (EquipmentManager.Instance != null && data.equipmentData != null)
        {
            // ItemDatabase가 준비된 경우 즉시 적용, 아니면 코루틴으로 대기
            if (ItemDatabase.Instance != null && ItemDatabase.Instance.IsReady)
            {
                EquipmentManager.Instance.LoadEquipmentSaveData(data.equipmentData);
                Debug.Log("[SaveLoadManager] 장비 즉시 로드 완료");
            }
            else
            {
                StartCoroutine(WaitAndLoadEquipment(data.equipmentData));
            }
        }
        // ✅ [장비 미저장 수정] SceneTransitionManager.LoadSceneWithPosition 제거
        // 씬 리로드 없이 현재 씬에서 플레이어 위치만 복원
        PlayerController savedPlayer = FindObjectOfType<PlayerController>();
        if (savedPlayer != null && data.playerPosition != Vector3.zero)
        {
            savedPlayer.transform.position = data.playerPosition;
            Debug.Log($"[SaveLoadManager] 플레이어 위치 복원: {data.playerPosition}");
        }

        Debug.Log($"[SaveLoadManager] 데이터 복원 완료 (씬: {data.currentScene})");
    }

    /// <summary>
    /// ItemDatabase 준비될 때까지 대기 후 장비 로드
    /// </summary>
    private System.Collections.IEnumerator WaitAndLoadEquipment(EquipmentSaveData equipmentData)
    {
        float timeout = 5f;
        while ((ItemDatabase.Instance == null || !ItemDatabase.Instance.IsReady) && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (ItemDatabase.Instance != null && ItemDatabase.Instance.IsReady)
        {
            EquipmentManager.Instance?.LoadEquipmentSaveData(equipmentData);
            Debug.Log("[SaveLoadManager] ★ ItemDatabase 준비 완료 → 장비 로드 성공");
        }
        else
        {
            EquipmentManager.Instance?.LoadEquipmentSaveData(equipmentData);
            Debug.LogWarning("[SaveLoadManager] ItemDatabase IsReady 확인 불가 → 강제 장비 로드");
        }

        // ✅ [Bug5 수정] 코루틴으로 장비가 늦게 로드된 경우
        // SkillManager.RoutineRefreshEquipmentSkills보다 늦게 실행될 수 있으므로
        // 장비 로드 직후 스킬 핫바를 강제로 재구성
        yield return null;
        if (EquipmentSkillSystem.Instance != null)
        {
            EquipmentSkillSystem.Instance.RefreshAllEquippedSkills();
            Debug.Log("[SaveLoadManager] 장비 지연 로드 후 스킬 핫바 재구성 완료");
        }
    }
    // ✅ [아이템 소실 수정] ItemDatabase 준비 후 메일 로드 (장비와 동일한 패턴)
    private System.Collections.IEnumerator WaitAndLoadMail(MailSaveData mailData)
    {
        float timeout = 5f;
        while ((ItemDatabase.Instance == null || !ItemDatabase.Instance.IsReady) && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        MailManager.Instance?.LoadMailSaveData(mailData);
        Debug.Log("[SaveLoadManager] 메일 지연 로드 완료");
    }

    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 저장 파일 경로 생성
    /// 예: .../Saves/SaveData_EDITOR_guest_0.json
    ///     .../Saves/SaveData_BUILD_guest_0.json
    /// </summary>
    private string GetSaveFilePath(int slotIndex)
    {
        return saveDirectory
               + SAVE_FILE_NAME + "_"
               + PLATFORM_PREFIX
               + currentUsername + "_"
               + slotIndex
               + SAVE_FILE_EXTENSION;
    }

    /// <summary>
    /// 해당 슬롯에 저장 파일이 존재하는지 확인
    /// </summary>
    public bool DoesSaveExist(int slotIndex)
        => File.Exists(GetSaveFilePath(slotIndex));

    /// <summary>
    /// 저장 슬롯 정보 가져오기 (UI 표시용)
    /// 슬롯이 없으면 null 반환
    /// </summary>
    public SaveSlotInfo GetSaveSlotInfo(int slotIndex)
    {
        if (!DoesSaveExist(slotIndex)) return null;

        try
        {
            string json = File.ReadAllText(GetSaveFilePath(slotIndex));
            if (useEncryption) json = DecryptString(json);

            SaveData data = JsonUtility.FromJson<SaveData>(json);
            return new SaveSlotInfo
            {
                slotIndex = slotIndex,
                saveTime = data.saveTime,
                playerLevel = data.playerLevel,
                currentScene = data.currentScene,
                playTime = data.playTime
            };
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoadManager] 슬롯 정보 읽기 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 모든 저장 슬롯 정보 배열로 가져오기
    /// </summary>
    public SaveSlotInfo[] GetAllSaveSlots()
    {
        SaveSlotInfo[] slots = new SaveSlotInfo[maxSaveSlots];
        for (int i = 0; i < maxSaveSlots; i++)
            slots[i] = GetSaveSlotInfo(i);
        return slots;
    }

    /// <summary>
    /// 특정 슬롯 저장 파일 삭제
    /// </summary>
    public void DeleteSave(int slotIndex)
    {
        string filePath = GetSaveFilePath(slotIndex);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log($"[SaveLoadManager] 슬롯 {slotIndex} 삭제 완료");
        }
    }

    /// <summary>
    /// 모든 슬롯 + PlayerPrefs 전부 삭제 (완전 초기화)
    /// </summary>
    public void DeleteAllSaves()
    {
        for (int i = 0; i < maxSaveSlots; i++)
        {
            string filePath = GetSaveFilePath(i);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log($"[SaveLoadManager] 슬롯 {i} 삭제");
            }
        }

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[SaveLoadManager] 전체 데이터 초기화 완료");
    }

    // ══════════════════════════════════════════════════════════════
    // ✅ 버전 체크 - 빌드할 때마다 이전 데이터 자동 삭제
    // ══════════════════════════════════════════════════════════════
    // ★★★ 데이터를 초기화하고 싶을 때 이 숫자를 올리고 빌드하세요 ★★★
    // 예: 0 → 1 로 바꾸고 빌드하면 모든 유저 데이터 완전 초기화
    [Header("★ 데이터 초기화 키 (올리면 모든 데이터 리셋)")]
    public int dataResetVersion = 0;
    private const string RESET_VERSION_KEY = "DataResetVersion";

    private void CheckAndClearOldSaveData()
    {
        // ✅ [Bug 수정] buildGUID는 매 빌드마다 바뀌어 자동으로 모든 데이터를 초기화함
        // → buildGUID 체크 완전 제거, 수동 dataResetVersion만 사용
        // ★ 데이터를 초기화하고 싶을 때 Inspector에서 dataResetVersion을 1 올리세요

        int savedResetVersion = PlayerPrefs.GetInt(RESET_VERSION_KEY, -1);
        bool forceReset = (savedResetVersion != dataResetVersion);

        if (forceReset)
        {
            Debug.Log($"[SaveLoadManager] ★ 수동 데이터 초기화 (v{dataResetVersion})");

            // JSON 저장 파일 전부 삭제
            if (Directory.Exists(saveDirectory))
            {
                string[] files = Directory.GetFiles(saveDirectory, "*.json");
                foreach (string file in files)
                {
                    File.Delete(file);
                    Debug.Log($"[SaveLoadManager] 삭제: {file}");
                }
            }

            // PlayerPrefs 전체 삭제
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            // 새 버전 기록 (다음 실행 시 초기화 안 함)
            PlayerPrefs.SetInt(RESET_VERSION_KEY, dataResetVersion);
            PlayerPrefs.Save();

            Debug.Log($"[SaveLoadManager] ✅ 수동 초기화 완료 → 기본값으로 시작");
        }
        else
        {
            Debug.Log($"[SaveLoadManager] 데이터 버전 동일 → 저장 데이터 유지");
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 암호화 / 복호화 (XOR 방식)
    // ══════════════════════════════════════════════════════════════

    private string EncryptString(string text)
    {
        // XOR 암호화 후 Base64 인코딩
        char[] chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            chars[i] = (char)(chars[i] ^ ENCRYPTION_KEY[i % ENCRYPTION_KEY.Length]);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(chars));
    }

    private string DecryptString(string encryptedText)
    {
        // Base64 디코딩 후 XOR 복호화
        byte[] bytes = Convert.FromBase64String(encryptedText);
        char[] chars = System.Text.Encoding.UTF8.GetString(bytes).ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            chars[i] = (char)(chars[i] ^ ENCRYPTION_KEY[i % ENCRYPTION_KEY.Length]);
        return new string(chars);
    }

    // ══════════════════════════════════════════════════════════════
    // 앱 종료 / 백그라운드 자동저장
    // ══════════════════════════════════════════════════════════════

    void OnApplicationQuit()
    {
        // PC/에디터 종료 시 자동 저장
        if (autoSave) SaveGame(0);
    }

    void OnApplicationPause(bool pauseStatus)
    {
        // 안드로이드는 OnApplicationQuit이 항상 호출되지 않음
        // 백그라운드로 전환될 때(pauseStatus=true) 저장
        if (pauseStatus && autoSave)
        {
            SaveGame(0);
            Debug.Log("[SaveLoadManager] 백그라운드 전환 → 자동 저장 완료");
        }
    }
}

// ══════════════════════════════════════════════════════════════════
// 데이터 클래스 (직렬화)
// [System.Serializable] 이 있어야 JSON으로 저장/로드 가능
// ══════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════
// ✅ 메일 저장용 직렬화 클래스 (SaveLoadManager에 함께 정의 - MailManager 의존성 제거)
// ═══════════════════════════════════════════════════════════════════

/// <summary>메일 보상 저장 데이터 (itemID로 저장)</summary>
[System.Serializable]
public class MailRewardSaveData
{
    public MailReward.RewardType rewardType;
    public int itemID;   // -1 = 아이템 없음 (골드/젬 등)
    public int amount;
}

/// <summary>메일 1개 저장 데이터</summary>
[System.Serializable]
public class MailSaveEntry
{
    public int mailID;
    public string title;
    public string content;
    public string sendDateStr;
    public bool isRead;
    public bool hasReward;
    public bool isRewardClaimed;
    public MailRewardSaveData[] rewards;
}

/// <summary>전체 메일 저장 데이터</summary>
[System.Serializable]
public class MailSaveData
{
    public MailSaveEntry[] mails;
    public int nextMailID;
}

/// <summary>
/// 전체 저장 데이터 - JSON으로 직렬화될 모든 게임 데이터
/// </summary>
[System.Serializable]
public class SaveData
{
    // ── 메타 정보 ──
    public string saveTime;               // 저장 시각 (yyyy-MM-dd HH:mm:ss)
    public string gameVersion = "1.0.0"; // 게임 버전
    public float playTime;              // 누적 플레이 타임 (초)

    // ── 플레이어 기본 정보 ──
    public int playerLevel;
    public int playerExp;
    public int playerGold;
    public int playerGem;

    // ── 플레이어 스탯 ──
    public float playerHealth;          // 저장 당시 현재 HP
    public float playerMaxHealth;       // 최대 HP (장비 포함)
    public float basePlayerMaxHealth;   // ✅ 순수 기본 HP (장비 제외) - 유령 스탯 버그 방지
    public float playerMana;            // 저장 당시 현재 마나
    public float playerMaxMana;         // 최대 마나

    // ── 위치 및 씬 ──
    public string currentScene;
    public Vector3 playerPosition;

    // ── 인벤토리 ──
    public InventoryItemData[] inventoryItems;

    // ── 장비 ──
    public EquipmentSaveData equipmentData;

    // ── 퀘스트 ──
    public QuestSaveData questData;

    // ── 스킬 ──
    public SkillSaveData[] unlockedSkills;

    // ✅ [아이템 소실 수정] 메일 데이터 추가 (메일함 보관 아이템 영구 유지)
    public MailSaveData mailData;

    // ── 게임 진행도 ──
    public int clearedStages;
    public bool[] unlockedAchievements;

    // ── 업적 ──
    public AchievementSaveEntry[] achievementSaveData;

    // ── 티켓 / 광물 (ResourceBarManager) ──
    public int equipmentTickets = 100;
    public int companionTickets = 50;
    public int relicTickets = 30;
    public int crystals = 0;
    public int essences = 0;
    public int fragments = 0;

    // ── 가챠 진행도 (GachaManager) ──
    public int gachaLevel = 1;
    public int gachaCount = 0;

    // ── 설정 ──
    public GameSettings settings;
}

/// <summary>
/// 인벤토리 아이템 저장 데이터
/// </summary>
[System.Serializable]
public class InventoryItemData
{
    public int itemID;       // 아이템 고유 ID
    public int count;        // 보유 개수
    public int slotIndex;    // 인벤 슬롯 위치
    public int enhanceLevel; // 강화 레벨
}

/// <summary>
/// 장비 슬롯 한 개 저장 데이터
/// </summary>
[System.Serializable]
public class EquippedSlotData
{
    public EquipmentType slotType;
    public int itemID = -1; // -1 = 장착 없음
    public int enhanceLevel = 0;
}

/// <summary>
/// 전체 장비 저장 데이터 (최대 6슬롯: 왼손/오른손/투구/갑옷/장갑/신발)
/// </summary>
[System.Serializable]
public class EquipmentSaveData
{
    public List<EquippedSlotData> slots = new List<EquippedSlotData>();
}

/// <summary>
/// 퀘스트 저장 데이터
/// </summary>
[System.Serializable]
public class QuestSaveData
{
    public int[] activeQuestIDs;    // 진행 중인 퀘스트 ID 목록
    public int[] completedQuestIDs; // 완료한 퀘스트 ID 목록
    public QuestProgress[] questProgresses;   // 각 퀘스트 진행도
}

/// <summary>
/// 스킬 저장 데이터
/// </summary>
[System.Serializable]
public class SkillSaveData
{
    public int skillID;
    public int skillLevel;
    public bool isUnlocked;
}

/// <summary>
/// 게임 설정 저장 데이터
/// </summary>
[System.Serializable]
public class GameSettings
{
    public float masterVolume = 1f;
    public float bgmVolume = 0.7f;
    public float sfxVolume = 0.8f;
    public int graphicsQuality = 2;    // 0=Low, 1=Medium, 2=High
    public bool fullscreen = true;
}

/// <summary>
/// 업적 저장 항목 (완료/보상수령 여부 포함)
/// </summary>
[System.Serializable]
public class AchievementSaveEntry
{
    public int achievementID;
    public bool isCompleted;
    public bool isRewarded;
    public int currentCount;
}

/// <summary>
/// 저장 슬롯 UI 표시용 정보 (로그인 화면 슬롯 선택 등에 사용)
/// </summary>
public class SaveSlotInfo
{
    public int slotIndex;
    public string saveTime;
    public int playerLevel;
    public string currentScene;
    public float playTime;

    // 슬롯이 비어있는지 여부 (saveTime이 없으면 빈 슬롯)
    public bool IsEmpty => string.IsNullOrEmpty(saveTime);
}