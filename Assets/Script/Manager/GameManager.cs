using System;
using System.Collections.Generic;
using UnityEngine;
using static Cinemachine.DocumentationSortingAttribute;

/// <summary>
/// 게임 전반을 관리하는 매니저
/// - 싱글톤 패턴
/// - 게임 데이터 중앙 관리
/// - 이벤트 시스템으로 느슨한 결합 구현
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("플레이어 자원")]
    public int playerGold = 1000;
    public int playerGem = 100;
    [SerializeField] private int playerExp = 0;
    [SerializeField] private int playerLevel = 1;

    [Header("게임 설정")]
    public GameConfig gameConfig; // ScriptableObject로 설정 관리

    // 자원 변경 이벤트 (UI 업데이트 등에 활용)
    public static event Action<int> OnGoldChanged;
    public static event Action<int> OnGemChanged;
    public static event Action<int, int> OnExpChanged; // exp, level
    public static event Action<string, int> OnItemAcquired;

    // 프로퍼티로 외부에서 읽기 전용 접근
    public int PlayerGold => playerGold;
    public int PlayerGem => playerGem;
    public int PlayerExp => playerExp;
    public int PlayerLevel => playerLevel;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeGame();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeGame()
    {
        LoadGameData();
        Debug.Log("GameManager 초기화 완료");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Delete))
        {
            Debug.Log("=== 데이터 초기화 ===");
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
    }

    #region 골드 관리

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
            Debug.Log($"골드 사용: -{amount} (남은 골드: {playerGold})");
            return true;
        }
        else
        {
            Debug.LogWarning($"골드가 부족합니다! 필요: {amount}, 보유: {playerGold}");
            return false;
        }
    }

    public void SetGold(int amount)
    {
        playerGold = Mathf.Max(0, amount);
        OnGoldChanged?.Invoke(playerGold);
    }

    #endregion

    #region 보석 관리

    public void AddGem(int amount)
    {
        if (amount <= 0) return;
        playerGem += amount;
        OnGemChanged?.Invoke(playerGem);
        Debug.Log($"보석 획득: +{amount} (총 보석: {playerGem})");
    }

    public bool SpendGem(int amount)
    {
        if (amount <= 0) { Debug.LogWarning("지출 금액이 0 이하입니다!"); return false; }

        if (playerGem >= amount)
        {
            playerGem -= amount;
            OnGemChanged?.Invoke(playerGem);
            Debug.Log($"보석 사용: -{amount} (남은 보석: {playerGem})");
            return true;
        }
        else
        {
            Debug.LogWarning($"보석이 부족합니다! 필요: {amount}, 보유: {playerGem}");
            return false;
        }
    }

    public void SetGem(int amount)
    {
        playerGem = Mathf.Max(0, amount);
        OnGemChanged?.Invoke(playerGem);
    }

    #endregion

    #region 경험치 및 레벨 관리

    public void AddExp(int amount)
    {
        if (amount <= 0) return;

        playerExp += amount;
        Debug.Log($"경험치 획득: +{amount} (현재 Exp: {playerExp})");

        // ✅ CheckLevelUp() 안에서 레벨업 판정 후 OnLevelUp 호출
        // ❌ 제거: PlayerStats.Instance?.OnLevelUp(level); ← 여기서 호출하면 안 됨
        //    이유 1) 'level' 변수가 GameManager에 없음 (playerLevel 이어야 함)
        //    이유 2) 레벨업이 없어도 AddExp 호출마다 OnLevelUp이 실행됨
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
            Debug.Log($"레벨 업! {levelUpCount}레벨 상승 -> 현재 레벨: {playerLevel}");

            // ✅ 레벨업이 실제로 일어났을 때만 OnLevelUp 호출 (playerLevel로 전달)
            //    OnLevelUp 내부에서 PlayerStats.level 동기화 + 스탯 성장 자동 처리
            PlayerStats.Instance?.OnLevelUp(playerLevel);

            // ★ 업적 업데이트
            if (AchievementSystem.Instance != null)
            {
                AchievementSystem.Instance.UpdateAchievementProgress(
                    AchievementType.ReachLevel,
                    playerLevel.ToString(),
                    1
                );
                Debug.Log($"[GameManager] 업적 업데이트: 레벨 {playerLevel} 달성");
            }

            // 레벨업 보상
            GiveLevelUpReward();

            // 이벤트 발생
            OnExpChanged?.Invoke(playerExp, playerLevel);

            // UIManager 알림
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OnLevelUp(playerLevel);
            }
        }

        // UI 업데이트 (레벨업 여부 관계없이)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateExpUI(playerExp, playerLevel);
        }
    }

    public int GetRequiredExpForLevel(int level)
    {
        return 100 + (level * 50) + (level * level * 10);
    }

    private void GiveLevelUpReward()
    {
        AddGold(100 * playerLevel);
        AddGem(10);
        Debug.Log($"레벨업 보상: 골드 {100 * playerLevel}, 보석 10");
    }

    #endregion

    #region 몬스터 처치 보상

    public void OnMonsterDeath(Vector2 position, int monsterLevel = 1)
    {
        int baseGold = 10;
        int goldAmount = baseGold + (monsterLevel * 5) + UnityEngine.Random.Range(-5, 10);
        goldAmount = Mathf.Max(1, goldAmount);
        AddGold(goldAmount);

        int expAmount = 20 + (monsterLevel * 10);
        AddExp(expAmount);

        RollItemDrop(position, monsterLevel);
    }

    private void RollItemDrop(Vector2 position, int monsterLevel)
    {
        float dropChance = UnityEngine.Random.Range(0f, 100f);
        if (dropChance <= 20f)
        {
            Debug.Log($"아이템 드롭! (위치: {position})");
        }
    }

    #endregion

    #region 데이터 저장/로드

    public void SaveGameData()
    {
        PlayerPrefs.SetInt("PlayerGold", playerGold);
        PlayerPrefs.SetInt("PlayerGem", playerGem);
        PlayerPrefs.SetInt("PlayerExp", playerExp);
        PlayerPrefs.SetInt("PlayerLevel", playerLevel);
        PlayerPrefs.Save();
        Debug.Log("게임 데이터 저장 완료");
    }

    public void LoadGameData()
    {
        playerGold = PlayerPrefs.GetInt("PlayerGold", 1000);
        playerGem = PlayerPrefs.GetInt("PlayerGem", 100);
        playerExp = PlayerPrefs.GetInt("PlayerExp", 0);
        playerLevel = PlayerPrefs.GetInt("PlayerLevel", 1);

        // ✅ 로드 시에는 OnLevelUp 대신 ApplyLevelStats 직접 호출
        //    (로드는 레벨업 이벤트가 아니라 복원이므로 체력 회복 등 부작용 없이 스탯만 반영)
        PlayerStats.Instance?.ApplyLevelStats(playerLevel);

        OnGoldChanged?.Invoke(playerGold);
        OnGemChanged?.Invoke(playerGem);
        OnExpChanged?.Invoke(playerExp, playerLevel);

        Debug.Log("게임 데이터 로드 완료");
    }

    public void ResetGameData()
    {
        playerGold = 1000;
        playerGem = 100;
        playerExp = 0;
        playerLevel = 1;
        SaveGameData();
        Debug.Log("게임 데이터 초기화 완료");
    }

    #endregion

    #region 유틸리티

    public void PauseGame() { Time.timeScale = 0f; Debug.Log("게임 일시정지"); }
    public void ResumeGame() { Time.timeScale = 1f; Debug.Log("게임 재개"); }

    public void QuitGame()
    {
        SaveGameData();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    void OnApplicationQuit()
    {
        SaveGameData();
    }
}

/// <summary>
/// 게임 설정 ScriptableObject
/// </summary>
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