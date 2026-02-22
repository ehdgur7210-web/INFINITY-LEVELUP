using System;
using System.IO;
using UnityEngine;

/// <summary>
/// JSON 기반 세이브/로드 매니저
/// - JSON 파일로 데이터 저장
/// - 암호화 옵션 지원
/// - 자동 저장 기능
/// - 다중 슬롯 지원
/// </summary>
public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    [Header("저장 설정")]
    [SerializeField] private bool useEncryption = false;        // 암호화 사용 여부
    [SerializeField] private bool autoSave = true;              // 자동 저장
    [SerializeField] private float autoSaveInterval = 300f;     // 자동 저장 간격 (5분)
    [SerializeField] private int maxSaveSlots = 3;              // 최대 저장 슬롯 수

    // 저장 경로
    private string saveDirectory;
    private const string SAVE_FILE_NAME = "SaveData";
    private const string SAVE_FILE_EXTENSION = ".json";
    
    // 암호화 키 (실제 프로젝트에서는 더 복잡하게 관리)
    private const string ENCRYPTION_KEY = "MySecretKey123!@#";

    // 자동 저장 타이머
    private float autoSaveTimer = 0f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSaveSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // 자동 저장 타이머
        if (autoSave)
        {
            autoSaveTimer += Time.deltaTime;
            if (autoSaveTimer >= autoSaveInterval)
            {
                autoSaveTimer = 0f;
                SaveGame(0); // 슬롯 0에 자동 저장
                Debug.Log("자동 저장 완료!");
            }
        }
    }

    /// <summary>
    /// 저장 시스템 초기화
    /// </summary>
    private void InitializeSaveSystem()
    {
        // 저장 경로 설정 (Application.persistentDataPath는 플랫폼별로 자동 설정됨)
        saveDirectory = Application.persistentDataPath + "/Saves/";
        
        // 저장 폴더가 없으면 생성
        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
        }
        
        Debug.Log($"저장 경로: {saveDirectory}");
    }

    #region 저장 (Save)

    /// <summary>
    /// 게임 저장 (특정 슬롯에)
    /// </summary>
    public void SaveGame(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxSaveSlots)
        {
            Debug.LogError($"잘못된 슬롯 인덱스: {slotIndex}");
            return;
        }

        try
        {
            // 저장할 데이터 수집
            SaveData saveData = CollectSaveData();
            
            // JSON으로 변환
            string json = JsonUtility.ToJson(saveData, true); // true = 보기 좋게 포맷팅
            
            // 암호화 (옵션)
            if (useEncryption)
            {
                json = EncryptString(json);
            }
            
            // 파일에 저장
            string filePath = GetSaveFilePath(slotIndex);
            File.WriteAllText(filePath, json);
            
            Debug.Log($"게임 저장 완료! 슬롯: {slotIndex}, 경로: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"저장 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 현재 게임 상태를 SaveData로 수집
    /// </summary>
    private SaveData CollectSaveData()
    {
        SaveData data = new SaveData();
        
        // 저장 시간
        data.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        // GameManager 데이터
        if (GameManager.Instance != null)
        {
            data.playerGold = GameManager.Instance.PlayerGold;
            data.playerGem = GameManager.Instance.PlayerGem;
            data.playerExp = GameManager.Instance.PlayerExp;
            data.playerLevel = GameManager.Instance.PlayerLevel;
        }
        
        // PlayerStats 데이터
        PlayerStats playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats != null)
        {
            data.playerHealth = playerStats.currentHealth;
            data.playerMaxHealth = playerStats.maxHealth;
            // 추가 스탯...
        }
        
        // 인벤토리 데이터
        if (InventoryManager.Instance != null)
        {
            data.inventoryItems = InventoryManager.Instance.GetInventoryData();
        }
        
        // 퀘스트 데이터
        if (QuestManager.Instance != null)
        {
            data.questData = QuestManager.Instance.GetQuestData();
        }
        
        // 플레이어 위치 (현재 씬)
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            data.playerPosition = player.transform.position;
            data.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }
        
        return data;
    }

    #endregion

    #region 로드 (Load)

    /// <summary>
    /// 게임 로드 (특정 슬롯에서)
    /// </summary>
    public bool LoadGame(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxSaveSlots)
        {
            Debug.LogError($"잘못된 슬롯 인덱스: {slotIndex}");
            return false;
        }

        string filePath = GetSaveFilePath(slotIndex);
        
        // 저장 파일이 없으면
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"저장 파일이 없습니다: {filePath}");
            return false;
        }

        try
        {
            // 파일 읽기
            string json = File.ReadAllText(filePath);
            
            // 복호화 (옵션)
            if (useEncryption)
            {
                json = DecryptString(json);
            }
            
            // JSON을 SaveData로 변환
            SaveData saveData = JsonUtility.FromJson<SaveData>(json);
            
            // 데이터 적용
            ApplySaveData(saveData);
            
            Debug.Log($"게임 로드 완료! 슬롯: {slotIndex}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"로드 실패: {e.Message}");
            return false;
        }
        
    }

    /// <summary>
    /// SaveData를 게임에 적용
    /// </summary>
    private void ApplySaveData(SaveData data)
    {
        // GameManager 데이터 적용
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGold(data.playerGold);
            GameManager.Instance.SetGem(data.playerGem);
            // Exp와 Level은 직접 설정하는 메서드 추가 필요
            GameManager.Instance.LoadPlayerData(data.playerExp, data.playerLevel);
        }
        
        // PlayerStats 데이터 적용
        PlayerStats playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.currentHealth = data.playerHealth;
            playerStats.maxHealth = data.playerMaxHealth;
        }
        
        // 인벤토리 데이터 적용
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.LoadInventoryData(data.inventoryItems);
        }
        
        // 퀘스트 데이터 적용
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.LoadQuestData(data.questData);
        }
        
        // 플레이어 위치 적용 (씬 전환 후)
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadSceneWithPosition(
                data.currentScene, 
                data.playerPosition
            );
        }
    }

    #endregion

    #region 저장 슬롯 관리

    /// <summary>
    /// 저장 파일 경로 생성
    /// </summary>
    private string GetSaveFilePath(int slotIndex)
    {
        return saveDirectory + SAVE_FILE_NAME + "_" + slotIndex + SAVE_FILE_EXTENSION;
    }

    /// <summary>
    /// 저장 슬롯이 존재하는지 확인
    /// </summary>
    public bool DoesSaveExist(int slotIndex)
    {
        string filePath = GetSaveFilePath(slotIndex);
        return File.Exists(filePath);
    }

    /// <summary>
    /// 저장 슬롯 정보 가져오기
    /// </summary>
    public SaveSlotInfo GetSaveSlotInfo(int slotIndex)
    {
        if (!DoesSaveExist(slotIndex))
        {
            return null;
        }

        try
        {
            string filePath = GetSaveFilePath(slotIndex);
            string json = File.ReadAllText(filePath);
            
            if (useEncryption)
            {
                json = DecryptString(json);
            }
            
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
            Debug.LogError($"슬롯 정보 읽기 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 저장 슬롯 삭제
    /// </summary>
    public void DeleteSave(int slotIndex)
    {
        string filePath = GetSaveFilePath(slotIndex);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log($"저장 슬롯 {slotIndex} 삭제 완료");
        }
    }

    /// <summary>
    /// 모든 저장 슬롯 정보 가져오기
    /// </summary>
    public SaveSlotInfo[] GetAllSaveSlots()
    {
        SaveSlotInfo[] slots = new SaveSlotInfo[maxSaveSlots];
        
        for (int i = 0; i < maxSaveSlots; i++)
        {
            slots[i] = GetSaveSlotInfo(i);
        }
        
        return slots;
    }

    #endregion

    #region 암호화/복호화 (간단한 XOR)

    /// <summary>
    /// 문자열 암호화 (XOR)
    /// </summary>
    private string EncryptString(string text)
    {
        char[] chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)(chars[i] ^ ENCRYPTION_KEY[i % ENCRYPTION_KEY.Length]);
        }
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(chars));
    }

    /// <summary>
    /// 문자열 복호화 (XOR)
    /// </summary>
    private string DecryptString(string encryptedText)
    {
        byte[] bytes = Convert.FromBase64String(encryptedText);
        char[] chars = System.Text.Encoding.UTF8.GetString(bytes).ToCharArray();
        
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)(chars[i] ^ ENCRYPTION_KEY[i % ENCRYPTION_KEY.Length]);
        }
        
        return new string(chars);
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// 저장 폴더 열기 (에디터에서만)
    /// </summary>
    public void OpenSaveFolder()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.RevealInFinder(saveDirectory);
        #else
        Debug.Log($"저장 경로: {saveDirectory}");
        #endif
    }

    #endregion

    void OnApplicationQuit()
    {
        // 게임 종료 시 자동 저장
        if (autoSave)
        {
            SaveGame(0);
        }
    }
}

/// <summary>
/// 저장 데이터 클래스
/// - JSON으로 직렬화될 모든 데이터
/// - [System.Serializable] 필수!
/// </summary>
[System.Serializable]
public class SaveData
{
    // 메타 정보
    public string saveTime;                 // 저장 시간
    public string gameVersion = "1.0.0";    // 게임 버전
    public float playTime;                  // 플레이 타임 (초)
    
    // 플레이어 기본 정보
    public int playerLevel;
    public int playerExp;
    public int playerGold;
    public int playerGem;
    
    // 플레이어 스탯
    public float playerHealth;
    public float playerMaxHealth;
    public float playerMana;
    public float playerMaxMana;
    
    // 플레이어 위치 및 씬
    public string currentScene;
    public Vector3 playerPosition;
    
    // 인벤토리 데이터
    public InventoryItemData[] inventoryItems;
    
    // 장비 데이터
    public EquipmentSaveData equipmentData;
    
    // 퀘스트 데이터
    public QuestSaveData questData;
    
    // 스킬 데이터
    public SkillSaveData[] unlockedSkills;
    
    // 게임 진행도
    public int clearedStages;
    public bool[] unlockedAchievements;
    
    // 설정
    public GameSettings settings;
}

/// <summary>
/// 인벤토리 아이템 데이터
/// </summary>
[System.Serializable]
public class InventoryItemData
{
    public int itemID;              // 아이템 ID
    public int count;               // 개수
    public int slotIndex;           // 슬롯 인덱스
}

/// <summary>
/// 장비 저장 데이터
/// </summary>
[System.Serializable]
public class EquipmentSaveData
{
    public int weaponID = -1;       // -1은 장비 안함
    public int armorID = -1;
    public int accessory1ID = -1;
    public int accessory2ID = -1;
    
    // 강화 레벨
    public int weaponEnhanceLevel;
    public int armorEnhanceLevel;
}

/// <summary>
/// 퀘스트 저장 데이터
/// </summary>
[System.Serializable]
public class QuestSaveData
{
    public int[] activeQuestIDs;    // 진행중인 퀘스트
    public int[] completedQuestIDs; // 완료한 퀘스트
    public QuestProgress[] questProgresses; // 퀘스트 진행도
}

/// <summary>
/// 퀘스트 진행도
/// </summary>


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
/// 게임 설정
/// </summary>
[System.Serializable]
public class GameSettings
{
    public float masterVolume = 1f;
    public float bgmVolume = 0.7f;
    public float sfxVolume = 0.8f;
    public int graphicsQuality = 2;     // 0=Low, 1=Medium, 2=High
    public bool fullscreen = true;
}

/// <summary>
/// 저장 슬롯 정보 (UI 표시용)
/// </summary>
public class SaveSlotInfo
{
    public int slotIndex;
    public string saveTime;
    public int playerLevel;
    public string currentScene;
    public float playTime;
    
    public bool IsEmpty => string.IsNullOrEmpty(saveTime);
}
