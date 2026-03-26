using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ══════════════════════════════════════════════════════════
/// GameManager — 게임 전반 관리 매니저 (리팩토링)
/// ══════════════════════════════════════════════════════════
///
/// ▶ 변경사항
///   · DontDestroyOnLoad 제거 (씬 로컬)
///   · SceneManager.sceneLoaded 이벤트 구독 제거 (중복 로드 방지)
///   · 데이터 초기화는 Start()에서 기본값으로만 설정
///   · 실제 저장 데이터 복원은 SaveLoadManager.Start()가 담당
///     (DefaultExecutionOrder +100으로 GameManager보다 나중에 실행)
///
/// ▶ 실행 순서
///   1. GameManager.Awake()  [Order: -100] → Instance 등록
///   2. GameManager.Start()  [Order: -100] → 기본값 초기화
///   3. SaveLoadManager.Start() [Order: +100] → JSON 읽기 → 데이터 덮어쓰기
/// ══════════════════════════════════════════════════════════
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("플레이어 자원")]
    public int playerGold = 1000;
    public int playerGem = 100;
    [SerializeField] private int playerExp = 0;
    [SerializeField] private int playerLevel = 1;

    [Header("게임 설정")]
    public GameConfig gameConfig;

    // 자원 변경 이벤트
    public static event Action<int> OnGoldChanged;
    public static event Action<int> OnGemChanged;
    public static event Action<int, int> OnExpChanged;  // exp, level
    public static event Action<string, int> OnItemAcquired;

    // 읽기 전용 프로퍼티
    public int PlayerGold => playerGold;
    public int PlayerGem => playerGem;
    public int PlayerExp => playerExp;
    public int PlayerLevel => playerLevel;

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
        Debug.Log("[GameManager] Awake 완료 (씬 로컬, DontDestroyOnLoad 없음)");
    }

    void Start()
    {
        if (Instance != this) return;

        // ★ PlayerPrefs 기본값만 로드 (빠른 초기화용)
        //   실제 JSON 저장 데이터는 SaveLoadManager.Start()가 덮어씀
        InitializeGame();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Delete))
        {
            Debug.Log("=== 데이터 초기화 (Delete 키) ===");
            GameDataBridge.DeleteAllFiles();
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 초기화 ───────────────────────────────────────────
    private void InitializeGame()
    {
        // PlayerPrefs에서 기본값 로드 (저장 데이터가 없을 때의 초기값)
        LoadGameData();
        Debug.Log("[GameManager] 기본값 초기화 완료 (SaveLoadManager가 JSON 데이터로 덮어씁니다)");
    }

    // ══════════════════════════════════════════════════════
    //  골드 관리
    // ══════════════════════════════════════════════════════

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        playerGold += amount;
        OnGoldChanged?.Invoke(playerGold);
        Debug.Log($"골드 획득: +{amount} (총 골드: {playerGold})");
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0) { Debug.LogWarning("지출 금액이 0 이하입니다!"); return false; }

        if (playerGold >= amount)
        {
            playerGold -= amount;
            OnGoldChanged?.Invoke(playerGold);
            return true;
        }

        Debug.LogWarning($"골드 부족! 필요: {amount}, 보유: {playerGold}");
        return false;
    }

    public void SetGold(int amount)
    {
        playerGold = Mathf.Max(0, amount);
        OnGoldChanged?.Invoke(playerGold);
    }

    // ══════════════════════════════════════════════════════
    //  보석 관리
    // ══════════════════════════════════════════════════════

    public void AddGem(int amount)
    {
        if (amount <= 0) return;
        playerGem += amount;
        OnGemChanged?.Invoke(playerGem);
    }

    public bool SpendGem(int amount)
    {
        if (amount <= 0) { Debug.LogWarning("지출 금액이 0 이하입니다!"); return false; }

        if (playerGem >= amount)
        {
            playerGem -= amount;
            OnGemChanged?.Invoke(playerGem);
            return true;
        }

        Debug.LogWarning($"보석 부족! 필요: {amount}, 보유: {playerGem}");
        return false;
    }

    public void SetGem(int amount)
    {
        playerGem = Mathf.Max(0, amount);
        OnGemChanged?.Invoke(playerGem);
    }

    // ══════════════════════════════════════════════════════
    //  경험치 / 레벨 관리
    // ══════════════════════════════════════════════════════

    public void AddExp(int amount)
    {
        if (amount <= 0) return;
        playerExp += amount;
        CheckLevelUp();
    }

    public void LoadPlayerData(int exp, int level)
    {
        playerExp = exp;
        playerLevel = level;
        Debug.Log($"플레이어 데이터 로드: Lv.{level}, Exp.{exp}");
        CheckLevelUp();
    }

    private void CheckLevelUp()
    {
        int requiredExp = GetRequiredExpForLevel(playerLevel);
        int levelUpCount = 0;

        while (playerExp >= requiredExp && requiredExp > 0)
        {
            playerExp -= requiredExp;
            playerLevel++;
            levelUpCount++;
            requiredExp = GetRequiredExpForLevel(playerLevel);
        }

        if (levelUpCount > 0)
        {
            PlayerStats.Instance?.OnLevelUp(playerLevel);

            if (AchievementSystem.Instance != null)
            {
                AchievementSystem.Instance.UpdateAchievementProgress(
                    AchievementType.ReachLevel, playerLevel.ToString(), 1);
            }

            GiveLevelUpReward();
            TutorialManager.Instance?.OnPlayerLevelUp(playerLevel);
            OnExpChanged?.Invoke(playerExp, playerLevel);

            UIManager.Instance?.OnLevelUp(playerLevel);
        }

        UIManager.Instance?.UpdateExpUI(playerExp, playerLevel);
    }

    public int GetRequiredExpForLevel(int level)
        => 100 + (level * 50) + (level * level * 10);

    private void GiveLevelUpReward()
    {
        AddGold(100 * playerLevel);
        AddGem(10);
    }

    // ══════════════════════════════════════════════════════
    //  몬스터 처치 보상
    // ══════════════════════════════════════════════════════

    public void OnMonsterDeath(Vector3 position, int monsterLevel = 1)
    {
        int goldAmount = Mathf.Max(1, 10 + (monsterLevel * 5) + UnityEngine.Random.Range(-5, 10));
        AddGold(goldAmount);
        AddExp(20 + (monsterLevel * 10));
        RollItemDrop(position, monsterLevel);
    }

    private void RollItemDrop(Vector3 position, int monsterLevel)
    {
        float dropChance = UnityEngine.Random.Range(0f, 100f);
        if (dropChance <= 20f)
            Debug.Log($"아이템 드롭! (위치: {position})");
    }

    // ══════════════════════════════════════════════════════
    //  데이터 저장 / 로드 (GameDataBridge)
    // ══════════════════════════════════════════════════════

    public void SaveGameData()
    {
        SaveData data = GameDataBridge.CurrentData;
        if (data == null) return;
        data.playerGold  = playerGold;
        data.playerGem   = playerGem;
        data.playerExp   = playerExp;
        data.playerLevel = playerLevel;
    }

    public void LoadGameData()
    {
        SaveData data = GameDataBridge.CurrentData;
        if (data != null)
        {
            playerGold  = data.playerGold  > 0 ? data.playerGold  : 1000;
            playerGem   = data.playerGem   > 0 ? data.playerGem   : 100;
            playerExp   = data.playerExp;
            playerLevel = data.playerLevel > 0 ? data.playerLevel : 1;
        }

        PlayerStats.Instance?.ApplyLevelStats(playerLevel);

        OnGoldChanged?.Invoke(playerGold);
        OnGemChanged?.Invoke(playerGem);
        OnExpChanged?.Invoke(playerExp, playerLevel);
    }

    public void ResetGameData()
    {
        playerGold  = 1000;
        playerGem   = 100;
        playerExp   = 0;
        playerLevel = 1;
        SaveGameData();
        Debug.Log("게임 데이터 초기화 완료");
    }

    // ══════════════════════════════════════════════════════
    //  유틸리티
    // ══════════════════════════════════════════════════════

    public void PauseGame() { Time.timeScale = 0f; }
    public void ResumeGame() { Time.timeScale = 1f; }

    public void QuitGame()
    {
        SaveLoadManager.Instance?.SaveGame();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnApplicationQuit()
    {
        SaveGameData();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveGameData();
    }
}

/// <summary>게임 설정 ScriptableObject</summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("기본 설정")]
    public int startingGold = 10000;
    public int startingGem = 100;

    [Header("드롭 확률")]
    [Range(0f, 100f)] public float commonItemDropRate = 50f;
    [Range(0f, 100f)] public float uncommonItemDropRate = 30f;
    [Range(0f, 100f)] public float rareItemDropRate = 15f;
    [Range(0f, 100f)] public float epicItemDropRate = 4f;
    [Range(0f, 100f)] public float legendaryItemDropRate = 1f;

    [Header("경제 설정")]
    public float sellPriceMultiplier = 0.5f;

    [Header("레벨링 설정")]
    public AnimationCurve expRequiredCurve;
}