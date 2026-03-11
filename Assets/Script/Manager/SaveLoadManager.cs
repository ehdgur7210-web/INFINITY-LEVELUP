using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ══════════════════════════════════════════════════════════
/// SaveLoadManager — 씬 로컬 세이브/로드 매니저 (리팩토링)
/// ══════════════════════════════════════════════════════════
///
/// ▶ 기존 방식 (DontDestroyOnLoad) 문제점
///   · 씬 전환 시 이전 씬의 SaveLoadManager가 살아남아 중복 발생
///   · 새 씬의 매니저들(Instance)과 충돌
///   · 데이터가 오염되거나 null 참조 발생
///
/// ▶ 새 방식 (JSON 싱글톤 + 씬 로컬)
///   · DontDestroyOnLoad 완전 제거
///   · 씬마다 SaveLoadManager가 새로 생성되고, 
///     GameDataBridge(정적)를 통해 데이터를 주고받음
///   · 씬 로드 시 Start()에서 자동으로 JSON을 읽어 모든 매니저에 적용
///   · 씬 이동 전에는 SceneTransitionManager가 SaveGame() 호출
///
/// ▶ 실행 순서 (DefaultExecutionOrder)
///   · ManagerRoot    : -999 (가장 먼저 Awake)
///   · SaveLoadManager: -150 (★ GameManager보다 먼저 Awake → JSON 선 읽기)
///   · GameManager    : -100
///   · 기타 매니저    :    0
///
/// ▶ 2단계 로드 전략
///   1단계 — Awake():  JSON 파일을 읽어 GameDataBridge.CurrentData에 올려놓음
///           OfflineRewardManager, GachaManager 등 Start()에서 CurrentData 직접 참조 가능
///   2단계 — Start():  모든 매니저 Start() 완료 후 ApplySaveData로 Instance에 주입
/// ══════════════════════════════════════════════════════════
/// </summary>
[DefaultExecutionOrder(-150)] // ★ GameManager(-100)보다 먼저 Awake 실행
public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    [Header("저장 설정")]
    [SerializeField] private bool useEncryption = false;
    [SerializeField] private bool autoSave = true;
    [SerializeField] private float autoSaveInterval = 300f; // 5분

    [Header("★ 데이터 초기화 키 (올리면 모든 데이터 리셋)")]
    public int dataResetVersion = 0;

    // ── 자동 저장 타이머 ──
    private float autoSaveTimer = 0f;

    // ── 플레이타임 누적 ──
    private float sessionStartTime = 0f;
    private float savedPlayTime = 0f;

    // ══════════════════════════════════════════════════════
    //  Unity 생명주기
    // ══════════════════════════════════════════════════════

    void Awake()
    {
        // ★ 씬 로컬 싱글톤 — DontDestroyOnLoad 없음
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // GameDataBridge에 설정값 주입
        GameDataBridge.DataResetVersion = dataResetVersion;
        GameDataBridge.UseEncryption = useEncryption;

        // 저장 시스템 초기화 (버전 체크 포함)
        InitializeSaveSystem();

        // ★★★ 핵심: Awake에서 즉시 JSON 읽기 → GameDataBridge.CurrentData에 선 로딩
        // 이유: OfflineRewardManager, GachaManager, SkillManager 등 Start()가
        //       실행되기 전에 이미 CurrentData에 데이터가 있어야 올바른 초기값으로 시작 가능
        // ApplySaveData(매니저 Instance에 실제 주입)는 Start()에서 수행
        if (GameDataBridge.FileExists(0))
        {
            GameDataBridge.ReadFromFile(0);
            Debug.Log("[SaveLoadManager] ★ Awake JSON 선 읽기 완료 → 각 매니저 Start() 전 데이터 준비됨");
        }
        else
        {
            Debug.Log("[SaveLoadManager] 저장 파일 없음 → 기본값 시작");
        }

        Debug.Log("[SaveLoadManager] Awake 완료");
    }

    void Start()
    {
        if (Instance != this) return;

        // ★ 2단계: 모든 매니저 Start() 완료 후 Instance에 데이터 주입
        // Awake에서 이미 CurrentData 로드됨 → ReadFromFile 재호출 불필요
        StartCoroutine(AutoLoadOnStart());
    }

    void Update()
    {
        if (!autoSave) return;

        autoSaveTimer += Time.deltaTime;
        if (autoSaveTimer >= autoSaveInterval)
        {
            autoSaveTimer = 0f;
            SaveGame(0);
            Debug.Log("[SaveLoadManager] 자동 저장 완료");
        }
    }

    // ── 저장 시스템 초기화 ──────────────────────────────
    private void InitializeSaveSystem()
    {
        // ★ GameDataBridge가 구버전 데이터를 자동 삭제
        GameDataBridge.CheckAndClearOldData();

        // 세션 시작 시각 기록 (플레이타임 누적용)
        sessionStartTime = Time.realtimeSinceStartup;

        Debug.Log($"[SaveLoadManager] 저장 경로: {GameDataBridge.GetFilePath(0)}");
    }

    // ── 씬 로드 후 자동 복원 코루틴 ──────────────────────
    private IEnumerator AutoLoadOnStart()
    {
        // 2프레임 대기: 모든 매니저 Start() 완료 보장
        yield return null;
        yield return null;

        // Awake에서 이미 GameDataBridge.ReadFromFile 완료
        // → 여기서는 CurrentData를 각 매니저 Instance에 주입(Apply)만 수행
        if (GameDataBridge.HasData)
        {
            ApplySaveData(GameDataBridge.CurrentData);
            Debug.Log("[SaveLoadManager] ★ 모든 매니저에 저장 데이터 적용 완료");
        }
        else
        {
            Debug.Log("[SaveLoadManager] 저장 데이터 없음 → 기본값으로 시작");
        }
    }

    // ══════════════════════════════════════════════════════
    //  저장 (Save)
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 현재 씬의 모든 매니저 데이터를 수집 → GameDataBridge → JSON 파일 저장
    /// SceneTransitionManager가 씬 이동 직전에 호출
    /// </summary>
    public void SaveGame(int slotIndex = 0)
    {
        try
        {
            SaveData data = CollectSaveData();
            GameDataBridge.SetData(data);    // 인메모리 갱신
            GameDataBridge.WriteToFile(slotIndex); // JSON 파일 기록

            Debug.Log($"[SaveLoadManager] ✅ 저장 완료 (슬롯:{slotIndex})");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoadManager] 저장 실패: {e.Message}");
        }
    }

    /// <summary>현재 씬의 모든 매니저 데이터를 SaveData로 수집</summary>
    private SaveData CollectSaveData()
    {
        SaveData data = new SaveData();
        data.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // ★ 플레이타임 누적 (세션 경과 + 이전 저장분)
        data.playTime = savedPlayTime + (Time.realtimeSinceStartup - sessionStartTime);

        // ── GameManager ──
        if (GameManager.Instance != null)
        {
            data.playerGold = GameManager.Instance.PlayerGold;
            data.playerGem = GameManager.Instance.PlayerGem;
            data.playerExp = GameManager.Instance.PlayerExp;
            data.playerLevel = GameManager.Instance.PlayerLevel;
        }

        // ── PlayerStats ──
        PlayerStats playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats != null)
        {
            data.playerHealth = playerStats.currentHealth;
            data.playerMaxHealth = playerStats.maxHealth;
            data.basePlayerMaxHealth = playerStats.baseMaxHealth; // 장비 제외 기본 HP
            data.playerMana = playerStats.currentMana;
            data.playerMaxMana = playerStats.maxMana;
        }

        // ── 인벤토리 ──
        if (InventoryManager.Instance != null)
            data.inventoryItems = InventoryManager.Instance.GetInventoryData();

        // ── 퀘스트 ──
        if (QuestManager.Instance != null)
            data.questData = QuestManager.Instance.GetQuestData();
        else if (GameDataBridge.CurrentData?.questData != null)
            data.questData = GameDataBridge.CurrentData.questData;
        // ── 장비 ──
        if (EquipmentManager.Instance != null)
            data.equipmentData = EquipmentManager.Instance.GetEquipmentSaveData();

        // ── 업적 ──
        if (AchievementSystem.Instance != null)
            data.achievementSaveData = AchievementSystem.Instance.GetAchievementSaveData();

        // ── 리소스 (티켓/광물) ──
        if (ResourceBarManager.Instance != null)
        {
            data.equipmentTickets = ResourceBarManager.Instance.equipmentTickets;
            data.companionTickets = ResourceBarManager.Instance.companionTickets;
            data.relicTickets = ResourceBarManager.Instance.relicTickets;
            data.crystals = ResourceBarManager.Instance.crystals;
            data.essences = ResourceBarManager.Instance.essences;
            data.fragments = ResourceBarManager.Instance.fragments;
            data.cropPoints = ResourceBarManager.Instance.cropPoints; // ★ 작물 포인트 저장
        }

        // ── 가챠 ──
        if (GachaManager.Instance != null)
        {
            data.gachaLevel = GachaManager.Instance.currentLevel;
            data.gachaCount = GachaManager.Instance.currentGachaCount;
        }

        // ── 농장 ──
        if (FarmManager.Instance != null)
            data.farmData = FarmManager.Instance.GetFarmSaveData();

        // ── 메일 ──
        if (MailManager.Instance != null)
            data.mailData = MailManager.Instance.GetMailSaveData();
        else if (GameDataBridge.CurrentData?.mailData != null)
            data.mailData = GameDataBridge.CurrentData.mailData;

        // ── 플레이어 위치 / 현재 씬 ──
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
            data.playerPosition = player.transform.position;

        data.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        return data;
    }

    // ══════════════════════════════════════════════════════
    //  로드 (Load)
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// JSON 파일 → GameDataBridge → 현재 씬 매니저들에 데이터 적용
    /// AutoLoadOnStart() 코루틴 또는 SceneTransitionManager에서 호출
    /// </summary>
    public bool LoadGame(int slotIndex = 0)
    {
        // 파일에서 최신 데이터 읽기 → CurrentData 갱신
        if (!GameDataBridge.ReadFromFile(slotIndex))
        {
            Debug.LogWarning($"[SaveLoadManager] 슬롯 {slotIndex} 로드 실패");
            return false;
        }

        // 각 매니저 Instance에 데이터 적용
        ApplySaveData(GameDataBridge.CurrentData);
        Debug.Log($"[SaveLoadManager] ✅ 로드 완료 (슬롯:{slotIndex})");
        return true;
    }

    /// <summary>SaveData를 각 매니저에 적용</summary>
    private void ApplySaveData(SaveData data)
    {
        // ── GameManager ──
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGold(data.playerGold);
            GameManager.Instance.SetGem(data.playerGem);
            GameManager.Instance.LoadPlayerData(data.playerExp, data.playerLevel);
        }

        // ★ 플레이타임 복원 + 세션 타이머 재시작
        savedPlayTime = data.playTime;
        sessionStartTime = Time.realtimeSinceStartup;

        // ── PlayerStats ──
        PlayerStats playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats != null)
        {
            if (data.basePlayerMaxHealth > 0)
            {
                playerStats.baseMaxHealth = data.basePlayerMaxHealth;
                playerStats.maxHealth = data.playerMaxHealth;
            }
            else
            {
                // 구버전 호환
                playerStats.baseMaxHealth = data.playerMaxHealth;
                playerStats.maxHealth = data.playerMaxHealth;
            }

            playerStats.maxMana = data.playerMaxMana;
            playerStats.currentHealth = playerStats.maxHealth;
            playerStats.currentMana = playerStats.maxMana;
            playerStats.UpdateStatsUI();
        }

        // ── 인벤토리 ──
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.LoadInventoryData(data.inventoryItems);

        // ── 퀘스트 ──
        if (QuestManager.Instance != null)
            QuestManager.Instance.LoadQuestData(data.questData);

        // ── 메일 ──
        if (MailManager.Instance != null && data.mailData != null)
        {
            if (ItemDatabase.Instance != null && ItemDatabase.Instance.IsReady)
                MailManager.Instance.LoadMailSaveData(data.mailData);
            else
                StartCoroutine(WaitAndLoadMail(data.mailData));
        }

        // ── 업적 ──
        if (AchievementSystem.Instance != null && data.achievementSaveData != null)
            AchievementSystem.Instance.LoadAchievementSaveData(data.achievementSaveData);

        // ── 리소스 ──
        if (ResourceBarManager.Instance != null)
        {
            ResourceBarManager.Instance.equipmentTickets = data.equipmentTickets;
            ResourceBarManager.Instance.companionTickets = data.companionTickets;
            ResourceBarManager.Instance.relicTickets = data.relicTickets;
            ResourceBarManager.Instance.crystals = data.crystals;
            ResourceBarManager.Instance.essences = data.essences;
            ResourceBarManager.Instance.fragments = data.fragments;
            ResourceBarManager.Instance.cropPoints = data.cropPoints; // ★ 작물 포인트 복원
            ResourceBarManager.Instance.UpdateAllResourceUI();
        }

        // ── 가챠 ──
        if (GachaManager.Instance != null)
        {
            GachaManager.Instance.currentLevel = Mathf.Max(1, data.gachaLevel);
            GachaManager.Instance.currentGachaCount = data.gachaCount;
            GachaManager.Instance.UpdateGachaPool();
        }

        // ── 농장 ──
        if (FarmManager.Instance != null && data.farmData != null)
            FarmManager.Instance.LoadFarmSaveData(data.farmData);

        // ── 장비 (ItemDatabase 준비 후 로드) ──
        if (EquipmentManager.Instance != null && data.equipmentData != null)
        {
            if (ItemDatabase.Instance != null && ItemDatabase.Instance.IsReady)
            {
                EquipmentManager.Instance.LoadEquipmentSaveData(data.equipmentData);
                RestoreFullHealth();
            }
            else
            {
                StartCoroutine(WaitAndLoadEquipment(data.equipmentData));
            }
        }

        // ── 플레이어 위치 ──
        PlayerController savedPlayer = FindObjectOfType<PlayerController>();
        if (savedPlayer != null && data.playerPosition != Vector3.zero)
        {
            savedPlayer.transform.position = data.playerPosition;
            Debug.Log($"[SaveLoadManager] 플레이어 위치 복원: {data.playerPosition}");
        }

        Debug.Log($"[SaveLoadManager] 데이터 적용 완료 (씬: {data.currentScene})");
    }

    // ── ItemDatabase 준비 후 장비 로드 ──────────────────
    private IEnumerator WaitAndLoadEquipment(EquipmentSaveData equipmentData)
    {
        float timeout = 5f;
        while ((ItemDatabase.Instance == null || !ItemDatabase.Instance.IsReady) && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        EquipmentManager.Instance?.LoadEquipmentSaveData(equipmentData);

        yield return null; // RecalculateStats 반영 1프레임 대기

        if (EquipmentSkillSystem.Instance != null)
            EquipmentSkillSystem.Instance.RefreshAllEquippedSkills();

        RestoreFullHealth();
        Debug.Log("[SaveLoadManager] 장비 지연 로드 완료");
    }

    // ── ItemDatabase 준비 후 메일 로드 ──────────────────
    private IEnumerator WaitAndLoadMail(MailSaveData mailData)
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

    // ── HP/마나 최대치 복원 ──────────────────────────────
    private void RestoreFullHealth()
    {
        PlayerStats ps = FindObjectOfType<PlayerStats>();
        if (ps != null)
        {
            ps.currentHealth = ps.maxHealth;
            ps.currentMana = ps.maxMana;
            ps.UpdateStatsUI();
        }
    }

    // ══════════════════════════════════════════════════════
    //  공개 유틸
    // ══════════════════════════════════════════════════════

    public bool DoesSaveExist(int slot = 0) => GameDataBridge.FileExists(slot);
    public string GetCurrentUser() => GameDataBridge.CurrentUsername;

    public void SetCurrentUser(string username)
    {
        GameDataBridge.SetCurrentUser(username);
    }

    public SaveSlotInfo GetSaveSlotInfo(int slotIndex)
    {
        if (!DoesSaveExist(slotIndex)) return null;

        try
        {
            // 파일을 임시로 읽어 슬롯 정보만 추출 (CurrentData 덮어쓰지 않도록 주의)
            string json = System.IO.File.ReadAllText(GameDataBridge.GetFilePath(slotIndex));
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
    /// 모든 저장 슬롯 정보 배열 반환 (MainMenuManager에서 슬롯 UI 생성 시 사용)
    /// </summary>
    public SaveSlotInfo[] GetAllSaveSlots(int maxSlots = 3)
    {
        SaveSlotInfo[] slots = new SaveSlotInfo[maxSlots];
        for (int i = 0; i < maxSlots; i++)
            slots[i] = GetSaveSlotInfo(i);
        return slots;
    }

    public void DeleteSave(int slotIndex)
    {
        string path = GameDataBridge.GetFilePath(slotIndex);
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
            Debug.Log($"[SaveLoadManager] 슬롯 {slotIndex} 삭제 완료");
        }
    }

    public void DeleteAllSaves()
    {
        GameDataBridge.DeleteAllFiles();
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[SaveLoadManager] 전체 데이터 초기화 완료");
    }

    // ══════════════════════════════════════════════════════
    //  앱 종료 / 백그라운드 자동저장
    // ══════════════════════════════════════════════════════

    void OnApplicationQuit()
    {
        if (autoSave) SaveGame(0);
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && autoSave)
        {
            SaveGame(0);
            Debug.Log("[SaveLoadManager] 백그라운드 전환 → 자동 저장 완료");
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

// ══════════════════════════════════════════════════════════
//  직렬화 데이터 클래스 (SaveData 등은 기존과 동일 유지)
// ══════════════════════════════════════════════════════════

[System.Serializable]
public class MailRewardSaveData
{
    public MailReward.RewardType rewardType;
    public int itemID;
    public int amount;
}

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

[System.Serializable]
public class MailSaveData
{
    public MailSaveEntry[] mails;
    public int nextMailID;
}

[System.Serializable]
public class SaveData
{
    public string saveTime;
    public string gameVersion = "1.0.0";
    public float playTime;

    public int playerLevel;
    public int playerExp;
    public int playerGold;
    public int playerGem;

    public float playerHealth;
    public float playerMaxHealth;
    public float basePlayerMaxHealth;
    public float playerMana;
    public float playerMaxMana;

    public string currentScene;
    public Vector3 playerPosition;

    public InventoryItemData[] inventoryItems;
    public EquipmentSaveData equipmentData;
    public QuestSaveData questData;
    public SkillSaveData[] unlockedSkills;
    public MailSaveData mailData;
    public AchievementSaveEntry[] achievementSaveData;

    public int equipmentTickets = 100;
    public int companionTickets = 50;
    public int relicTickets = 30;
    public int crystals = 0;
    public int essences = 0;
    public int fragments = 0;
    public int cropPoints = 0; // ★ 작물 포인트

    public int gachaLevel = 1;
    public int gachaCount = 0;

    public FarmSaveData farmData;
    public GameSettings settings;

    public int[] clearedStageIds;
    public bool[] unlockedAchievements;
    public int clearedStages;
}

[System.Serializable]
public class InventoryItemData
{
    public int itemID;
    public int count;
    public int slotIndex;
    public int enhanceLevel;
}

[System.Serializable]
public class EquippedSlotData
{
    public EquipmentType slotType;
    public int itemID = -1;
    public int enhanceLevel = 0;
}

[System.Serializable]
public class EquipmentSaveData
{
    public List<EquippedSlotData> slots = new List<EquippedSlotData>();
}

[System.Serializable]
public class QuestSaveData
{
    public int[] activeQuestIDs;
    public int[] completedQuestIDs;
    public QuestProgress[] questProgresses;
    public int[] currentObjectiveAmounts; // ★ 퀘스트 목표 진행도 저장
}

[System.Serializable]
public class SkillSaveData
{
    public int skillID;
    public int skillLevel;
    public bool isUnlocked;
}

[System.Serializable]
public class GameSettings
{
    public float masterVolume = 1f;
    public float bgmVolume = 0.7f;
    public float sfxVolume = 0.8f;
    public int graphicsQuality = 2;
    public bool fullscreen = true;
}

[System.Serializable]
public class AchievementSaveEntry
{
    public int achievementID;
    public bool isCompleted;
    public bool isRewarded;
    public int currentCount;
}

public class SaveSlotInfo
{
    public int slotIndex;
    public string saveTime;
    public int playerLevel;
    public string currentScene;
    public float playTime;

    public bool IsEmpty => string.IsNullOrEmpty(saveTime);
}