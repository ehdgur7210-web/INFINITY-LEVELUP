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
        // ★ 씬 로컬 싱글톤 — 이전 씬의 Instance가 아직 파괴 안 됐을 수 있으므로
        //   scene.isLoaded로 유효성 검사 (언로딩 중인 씬의 Instance는 교체)
        if (Instance != null && Instance != this && Instance.gameObject.scene.isLoaded)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Debug.Log("[ManagerInit] SaveLoadManager가 생성되었습니다.");

        // GameDataBridge에 설정값 주입
        GameDataBridge.DataResetVersion = dataResetVersion;
        GameDataBridge.UseEncryption = useEncryption;

        // 저장 시스템 초기화 (버전 체크 포함)
        InitializeSaveSystem();

        // ★★★ 핵심: Awake에서 데이터 선 로딩
        // 캐릭터 선택 시 이미 CurrentData가 로드되었으면 파일 재읽기 불필요
        // 아직 데이터가 없으면 활성 슬롯 파일에서 읽기
        int slot = GameDataBridge.ActiveSlot;
        if (GameDataBridge.HasData)
        {
            // ★ 캐릭터 선택에서 이미 로드된 데이터 사용 (파일 재읽기 생략)
            Debug.Log($"[SaveLoadManager] ★ 인메모리 데이터 사용 (유저:{GameDataBridge.CurrentUsername}, 슬롯:{slot})");
        }
        else if (GameDataBridge.FileExists(slot))
        {
            GameDataBridge.ReadFromFile(slot);
            Debug.Log($"[SaveLoadManager] ★ Awake JSON 읽기 완료 (유저:{GameDataBridge.CurrentUsername}, 슬롯:{slot})");
        }
        else
        {
            Debug.Log($"[SaveLoadManager] 저장 파일 없음 → 기본값 시작 (유저:{GameDataBridge.CurrentUsername}, 슬롯:{slot})");
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
            SaveGame();
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

        Debug.Log($"[SaveLoadManager] 저장 경로: {GameDataBridge.GetFilePath(GameDataBridge.ActiveSlot)}");
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var cd = GameDataBridge.CurrentData;
            string sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Debug.Log($"[LOAD-DEBUG] ★ ApplySaveData 시작 씬={sc}" +
                $" | 장비={cd.equipmentData?.slots?.Count ?? -1}개" +
                $" | 인벤={cd.inventoryItems?.Length ?? -1}개" +
                $" | 동료={cd.companions?.Length ?? -1}개" +
                $" | 골드={cd.playerGold} | 젬={cd.playerGem} | Lv={cd.playerLevel}" +
                $" | 웨이브={cd.offlineCurrentWave}");
#endif
            ApplySaveData(GameDataBridge.CurrentData);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[SaveLoadManager] ★ 모든 매니저에 저장 데이터 적용 완료");
#endif
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
    /// ★ 항상 현재 로그인 유저 + 활성 캐릭터 슬롯에 저장
    /// 외부에서 SaveGame(0) 호출해도 ActiveSlot 기준으로 저장됨
    /// </summary>
    public void SaveGame(int slotIndex = -1)
    {
        try
        {
            // ★ 항상 활성 캐릭터 슬롯에 저장 (인자 무시)
            int slot = GameDataBridge.ActiveSlot;
            SaveData data = CollectSaveData();
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Debug.Log($"[SAVE-DEBUG] ★ SaveGame 호출 씬={scene}" +
                $" | 장비={data.equipmentData?.slots?.Count ?? -1}개" +
                $" | 인벤={data.inventoryItems?.Length ?? -1}개" +
                $" | 동료={data.companions?.Length ?? -1}개" +
                $" | 골드={data.playerGold} | 젬={data.playerGem} | Lv={data.playerLevel}" +
                $" | 웨이브={data.offlineCurrentWave}");
            GameDataBridge.SetData(data);       // 인메모리 갱신
            GameDataBridge.WriteToFile(slot);    // JSON 파일 기록

            var slots = GameDataBridge.CharacterSlots;
            if (slots.slots != null && slot < slots.slots.Length && slots.slots[slot] != null)
            {
                slots.slots[slot].level = data.playerLevel;
                GameDataBridge.WriteCharacterSlots();
            }

            // ★ 뒤끝 서버에도 저장 (로그인 상태일 때만)
            // 서버 저장 완료 후 랭킹 점수 갱신 (RowInDate 확보 보장)
            if (BackendGameDataManager.Instance != null)
            {
                Debug.Log("[SaveLoadManager] ▶ 서버 저장 시작 (SaveToServer)");
                BackendGameDataManager.Instance.SaveToServer(success =>
                {
                    Debug.Log($"[SaveLoadManager] ◀ 서버 저장 결과: {(success ? "성공" : "실패")}");
                    if (success)
                    {
                        if (BackendRankingManager.Instance != null)
                        {
                            Debug.Log("[SaveLoadManager] ▶ 랭킹 점수 갱신 시작 (UpdateAllScores)");
                            BackendRankingManager.Instance.UpdateAllScores();
                        }
                        else
                        {
                            Debug.LogWarning("[SaveLoadManager] ⚠ BackendRankingManager.Instance == null → 랭킹 갱신 스킵");
                        }
                    }
                });
            }
            else
            {
                Debug.LogWarning("[SaveLoadManager] ⚠ BackendGameDataManager.Instance == null → 서버 저장 스킵");
            }

            Debug.Log($"[SaveLoadManager] ✅ 저장 완료 (유저:{GameDataBridge.CurrentUsername}, 슬롯:{slot})");
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

        // ★ FarmScene에서는 MainScene 전용 매니저를 읽지 않음 (빈 데이터로 덮어쓰기 방지)
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool isMainScene = currentScene == "MainScene";

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
        else if (GameDataBridge.CurrentData != null)
        {
            data.playerGold = GameDataBridge.CurrentData.playerGold;
            data.playerGem = GameDataBridge.CurrentData.playerGem;
            data.playerExp = GameDataBridge.CurrentData.playerExp;
            data.playerLevel = GameDataBridge.CurrentData.playerLevel;
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
        else if (GameDataBridge.CurrentData != null)
        {
            data.playerHealth = GameDataBridge.CurrentData.playerHealth;
            data.playerMaxHealth = GameDataBridge.CurrentData.playerMaxHealth;
            data.basePlayerMaxHealth = GameDataBridge.CurrentData.basePlayerMaxHealth;
            data.playerMana = GameDataBridge.CurrentData.playerMana;
            data.playerMaxMana = GameDataBridge.CurrentData.playerMaxMana;
        }

        // ── 인벤토리 (MainScene 전용) ──
        if (isMainScene && InventoryManager.Instance != null)
            data.inventoryItems = InventoryManager.Instance.GetInventoryData();
        else if (GameDataBridge.CurrentData?.inventoryItems != null)
            data.inventoryItems = GameDataBridge.CurrentData.inventoryItems;

        // ── 퀘스트 ──
        if (QuestManager.Instance != null)
            data.questData = QuestManager.Instance.GetQuestData();
        else if (GameDataBridge.CurrentData?.questData != null)
            data.questData = GameDataBridge.CurrentData.questData;
        // ── 장비 (MainScene 전용) ──
        // ★ IsEquipmentLoaded=false이면 아직 로드 안 됨 → GameDataBridge 폴백 (빈 데이터로 덮어쓰기 방지)
        if (isMainScene && EquipmentManager.Instance != null && EquipmentManager.Instance.IsEquipmentLoaded)
        {
            data.equipmentData = EquipmentManager.Instance.GetEquipmentSaveData();
            Debug.Log($"[EQUIP-TRACE] CollectSaveData: EquipmentManager에서 수집 → {data.equipmentData?.slots?.Count ?? 0}개 (IsLoaded=true)");
        }
        else if (GameDataBridge.CurrentData?.equipmentData != null)
        {
            data.equipmentData = GameDataBridge.CurrentData.equipmentData;
            Debug.Log($"[EQUIP-TRACE] CollectSaveData: GameDataBridge 폴백 → {data.equipmentData?.slots?.Count ?? 0}개 (isMain={isMainScene}, EquipMgr={EquipmentManager.Instance != null}, IsLoaded={EquipmentManager.Instance?.IsEquipmentLoaded})");
        }
        else
        {
            Debug.LogWarning($"[EQUIP-TRACE] CollectSaveData: 장비 데이터 없음! (isMain={isMainScene}, EquipMgr={EquipmentManager.Instance != null}, Bridge={GameDataBridge.CurrentData?.equipmentData != null})");
        }

        // ── 업적 ──
        if (AchievementSystem.Instance != null)
            data.achievementSaveData = AchievementSystem.Instance.GetAchievementSaveData();
        else if (GameDataBridge.CurrentData?.achievementSaveData != null)
            data.achievementSaveData = GameDataBridge.CurrentData.achievementSaveData;

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
        else if (GameDataBridge.CurrentData != null)
        {
            data.equipmentTickets = GameDataBridge.CurrentData.equipmentTickets;
            data.companionTickets = GameDataBridge.CurrentData.companionTickets;
            data.relicTickets = GameDataBridge.CurrentData.relicTickets;
            data.crystals = GameDataBridge.CurrentData.crystals;
            data.essences = GameDataBridge.CurrentData.essences;
            data.fragments = GameDataBridge.CurrentData.fragments;
            data.cropPoints = GameDataBridge.CurrentData.cropPoints;
        }

        // ── 가챠 (MainScene 전용) ──
        if (isMainScene && GachaManager.Instance != null)
        {
            data.gachaLevel = GachaManager.Instance.currentLevel;
            data.gachaCount = GachaManager.Instance.currentGachaCount;
        }
        else if (GameDataBridge.CurrentData != null)
        {
            data.gachaLevel = GameDataBridge.CurrentData.gachaLevel;
            data.gachaCount = GameDataBridge.CurrentData.gachaCount;
        }

        // ── 동료 (MainScene 전용) ──
        if (isMainScene && CompanionInventoryManager.Instance != null)
            data.companions = CompanionInventoryManager.Instance.GetSaveData();
        else if (GameDataBridge.CurrentData?.companions != null)
            data.companions = GameDataBridge.CurrentData.companions;

        // ── 동료 핫바 (MainScene 전용) ──
        if (isMainScene && CompanionHotbarManager.Instance != null)
        {
            data.companionHotbarIDs = CompanionHotbarManager.Instance.GetHotbarSaveData();
            data.autoSummonEnabled = CompanionHotbarManager.Instance.autoSummonEnabled;
        }
        else if (GameDataBridge.CurrentData?.companionHotbarIDs != null)
        {
            data.companionHotbarIDs = GameDataBridge.CurrentData.companionHotbarIDs;
            data.autoSummonEnabled = GameDataBridge.CurrentData.autoSummonEnabled;
        }

        // ── VIP ──
        if (VipManager.Instance != null)
        {
            data.vipLevel = VipManager.Instance.CurrentVipLevel;
            data.vipExp = VipManager.Instance.CurrentVipExp;
            data.vipExpireDate = VipManager.Instance.ExpireDate;
            data.vipFreeGiftClaimed = VipManager.Instance.IsFreeGiftClaimed;
            data.vipPaidGiftPurchased = VipManager.Instance.IsPaidGiftPurchased;
            data.vipClaimedFreeLevels = VipManager.Instance.ClaimedFreeLevels;
            data.vipClaimedPaidLevels = VipManager.Instance.ClaimedPaidLevels;
        }
        else if (GameDataBridge.CurrentData != null)
        {
            data.vipLevel = GameDataBridge.CurrentData.vipLevel;
            data.vipExp = GameDataBridge.CurrentData.vipExp;
            data.vipExpireDate = GameDataBridge.CurrentData.vipExpireDate;
            data.vipFreeGiftClaimed = GameDataBridge.CurrentData.vipFreeGiftClaimed;
            data.vipPaidGiftPurchased = GameDataBridge.CurrentData.vipPaidGiftPurchased;
        }

        // ── 농장 ──
        // ★ FarmInventoryUI가 살아있으면 harvests를 GameDataBridge에 강제 동기화
        FarmInventoryUI.Instance?.SyncHarvestsToGameDataBridge();

        if (FarmManager.Instance != null)
            data.farmData = FarmManager.Instance.GetFarmSaveData();
        else if (GameDataBridge.CurrentData?.farmData != null)
            data.farmData = GameDataBridge.CurrentData.farmData;

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

        // ── 스킬 ──
        if (SkillManager.Instance != null)
        {
            data.learnedSkillIDs = SkillManager.Instance.GetLearnedSkillIDs();
            data.hotbarSkillIDs  = SkillManager.Instance.GetHotbarSkillIDs();
            data.autoSkillEnabled = SkillManager.Instance.autoSkillEnabled;
        }
        else if (GameDataBridge.CurrentData != null)
        {
            data.learnedSkillIDs = GameDataBridge.CurrentData.learnedSkillIDs;
            data.hotbarSkillIDs  = GameDataBridge.CurrentData.hotbarSkillIDs;
            data.autoSkillEnabled = GameDataBridge.CurrentData.autoSkillEnabled;
        }

        // ── 오프라인 보상 ──
        if (OfflineRewardManager.Instance != null)
        {
            data.lastLogoutTime            = DateTime.Now.ToString("o");
            data.accumulatedOfflineMinutes = OfflineRewardManager.Instance.AccumulatedMinutes;
            data.offlineGoldRate           = OfflineRewardManager.Instance.goldPerMinute;
            data.offlineExpRate            = OfflineRewardManager.Instance.expPerMinute;
            data.offlineGemRate            = OfflineRewardManager.Instance.gemPerMinute;
            data.offlineEquipTicketRate    = 0f; // ★ 장비티켓 보상 제거됨 (호환성 유지)
        }
        else if (GameDataBridge.CurrentData != null)
        {
            // 폴백: 기존 로그아웃 시간 유지 (현재 시간으로 덮어쓰면 오프라인 시간 0이 됨)
            data.lastLogoutTime            = GameDataBridge.CurrentData.lastLogoutTime ?? DateTime.Now.ToString("o");
            data.accumulatedOfflineMinutes = GameDataBridge.CurrentData.accumulatedOfflineMinutes;
            data.offlineGoldRate           = GameDataBridge.CurrentData.offlineGoldRate;
            data.offlineExpRate            = GameDataBridge.CurrentData.offlineExpRate;
            data.offlineGemRate            = GameDataBridge.CurrentData.offlineGemRate;
            data.offlineEquipTicketRate    = GameDataBridge.CurrentData.offlineEquipTicketRate;
        }
        // ★ 2배 보상 횟수 저장 (GameDataBridge.CurrentData에서 항상 수집)
        if (GameDataBridge.CurrentData != null)
        {
            data.adClaimCount = GameDataBridge.CurrentData.adClaimCount;
            data.adClaimDate  = GameDataBridge.CurrentData.adClaimDate;
        }

        // ── 스테이지/웨이브 (오프라인 보상과 독립적으로 저장) ──
        // ★ PendingWaveIndex가 있으면 최우선 (아직 WaveSpawner에 적용 전인 복원 대기 값)
        if (WaveSpawner.PendingWaveIndex >= 0)
        {
            data.offlineCurrentWave = WaveSpawner.PendingWaveIndex;
            Debug.Log($"[SaveLoadManager] ★ 웨이브 저장 (PendingWaveIndex): {data.offlineCurrentWave}");
        }
        else if (WaveSpawner.Instance != null && WaveSpawner.Instance.CurrentWaveIndex > 0)
        {
            data.offlineCurrentWave = WaveSpawner.Instance.CurrentWaveIndex;
            Debug.Log($"[SaveLoadManager] ★ 웨이브 저장 (WaveSpawner): {data.offlineCurrentWave}");
        }
        else if (GameDataBridge.HasData && GameDataBridge.CurrentData.offlineCurrentWave > 0)
        {
            data.offlineCurrentWave = GameDataBridge.CurrentData.offlineCurrentWave;
            Debug.Log($"[SaveLoadManager] ★ 웨이브 보존 (GameDataBridge 폴백): {data.offlineCurrentWave}");
        }
        else
        {
            // WaveSpawner가 있고 index가 0이면 실제 1-1 스테이지
            if (WaveSpawner.Instance != null)
                data.offlineCurrentWave = WaveSpawner.Instance.CurrentWaveIndex;
            Debug.Log($"[SaveLoadManager] 웨이브 저장: {data.offlineCurrentWave} (기본)");
        }

        // ── 튜토리얼 ──
        data.tutorialCompleted = TutorialManager.Instance != null
            ? TutorialManager.Instance.IsTutorialCompleted()
            : (GameDataBridge.CurrentData?.tutorialCompleted ?? false);
        data.tutorialPhase = GameDataBridge.CurrentData?.tutorialPhase ?? 0;
        data.tutorialStep = GameDataBridge.CurrentData?.tutorialStep ?? -1;

        // ── 사운드 설정 ──
        if (SoundManager.Instance != null)
        {
            if (data.settings == null) data.settings = new GameSettings();
            data.settings.bgmVolume = SoundManager.Instance.GetBGMVolume();
            data.settings.sfxVolume = SoundManager.Instance.GetSFXVolume();
            data.settings.bgmMuted  = SoundManager.Instance.IsBGMMuted();
            data.settings.sfxMuted  = SoundManager.Instance.IsSFXMuted();
        }
        else if (GameDataBridge.CurrentData?.settings != null)
        {
            data.settings = GameDataBridge.CurrentData.settings;
        }

        // ── 캐릭터 정보 (씬 간 유지) ──
        if (GameDataBridge.CurrentData != null)
        {
            data.selectedCharacterName = GameDataBridge.CurrentData.selectedCharacterName;
            data.characterClassType    = GameDataBridge.CurrentData.characterClassType;
            data.activeCharacterSlot   = GameDataBridge.CurrentData.activeCharacterSlot;
            data.accountID             = GameDataBridge.CurrentData.accountID;
            data.selectedServer        = GameDataBridge.CurrentData.selectedServer;
            data.charBaseHealth        = GameDataBridge.CurrentData.charBaseHealth;
            data.charBaseAttack        = GameDataBridge.CurrentData.charBaseAttack;
            data.charBaseDefense       = GameDataBridge.CurrentData.charBaseDefense;
            data.charBaseSpeed         = GameDataBridge.CurrentData.charBaseSpeed;
            data.charAttackRange       = GameDataBridge.CurrentData.charAttackRange;
            data.charAttackSpeed       = GameDataBridge.CurrentData.charAttackSpeed;
            data.selectedPortraitIndex = GameDataBridge.CurrentData.selectedPortraitIndex; // ★ 프로필 이미지
        }

        return data;
    }

    // ══════════════════════════════════════════════════════
    //  로드 (Load)
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// JSON 파일 → GameDataBridge → 현재 씬 매니저들에 데이터 적용
    /// ★ 항상 현재 로그인 유저 + 활성 캐릭터 슬롯에서 로드
    /// </summary>
    public bool LoadGame(int slotIndex = -1)
    {
        // ★ 항상 활성 캐릭터 슬롯에서 로드
        int slot = GameDataBridge.ActiveSlot;

        // 파일에서 최신 데이터 읽기 → CurrentData 갱신
        if (!GameDataBridge.ReadFromFile(slot))
        {
            Debug.LogWarning($"[SaveLoadManager] 로드 실패 (유저:{GameDataBridge.CurrentUsername}, 슬롯:{slot})");
            return false;
        }

        // 각 매니저 Instance에 데이터 적용
        ApplySaveData(GameDataBridge.CurrentData);
        Debug.Log($"[SaveLoadManager] ✅ 로드 완료 (유저:{GameDataBridge.CurrentUsername}, 슬롯:{slot})");
        return true;
    }

    /// <summary>SaveData를 각 매니저에 적용</summary>
    private void ApplySaveData(SaveData data)
    {
        Debug.Log($"[LOAD-CHECK] 로드 시점 harvests 수: {GameDataBridge.CurrentData?.farmData?.inventoryData?.harvests?.Count}");

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

        // ── ★ 농장 수확 아이템 ItemDatabase 사전 등록 (LoadInventoryData보다 먼저!) ──
        // FarmManager.RegisterHarvestItemsInDatabase()가 코루틴 재시도 중일 수 있으므로
        // 여기서 동기적으로 한 번 더 등록하여 LoadInventoryData에서 아이템 드롭 방지
        EnsureFarmHarvestItemsRegistered(data);

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

            // ★ 가챠 UI 티켓 연동 (로드 완료 후)
            GachaUI.Instance?.UpdateTicketDisplay();
        }

        // ── VIP ──
        if (VipManager.Instance != null)
            VipManager.Instance.ApplyLocalData(data.vipLevel, data.vipExp, data.vipExpireDate,
                data.vipFreeGiftClaimed, data.vipPaidGiftPurchased,
                data.vipClaimedFreeLevels, data.vipClaimedPaidLevels);

        // ── 가챠 ──
        if (GachaManager.Instance != null)
        {
            GachaManager.Instance.currentLevel = Mathf.Max(1, data.gachaLevel);
            GachaManager.Instance.currentGachaCount = data.gachaCount;
            GachaManager.Instance.UpdateGachaPool();
        }

        // ── 동료 ──
        if (CompanionInventoryManager.Instance != null && data.companions != null)
        {
            var allCompanions = new List<CompanionData>(Resources.FindObjectsOfTypeAll<CompanionData>());
            CompanionInventoryManager.Instance.LoadSaveData(data.companions, allCompanions);
            Debug.Log($"[LOAD-CHECK] 동료 {data.companions.Length}명 로드 완료");
        }

        // ── 동료 핫바 ──
        if (CompanionHotbarManager.Instance != null && data.companionHotbarIDs != null)
        {
            CompanionHotbarManager.Instance.LoadHotbarSaveData(data.companionHotbarIDs);
            CompanionHotbarManager.Instance.SetAutoSummon(data.autoSummonEnabled);
            Debug.Log($"[LOAD-CHECK] 동료 핫바 {data.companionHotbarIDs.Length}슬롯 로드 완료 (오토: {data.autoSummonEnabled})");
        }

        // ── 농장 ──
        if (FarmManager.Instance != null && data.farmData != null)
            FarmManager.Instance.LoadFarmSaveData(data.farmData);

        // ── ★ 농장 수확물 → 메인 인벤토리 기타탭 전달 ──
        if (InventoryManager.Instance != null)
            TransferFarmHarvestToInventory(data);

        // ── 장비 (ItemDatabase 준비 후 로드) ──
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[EQUIP-TRACE] ApplySaveData: EquipMgr={EquipmentManager.Instance != null}, equipData={data.equipmentData?.slots?.Count ?? -1}개, ItemDB={ItemDatabase.Instance != null}, IsReady={ItemDatabase.Instance?.IsReady}");
#endif
        if (EquipmentManager.Instance != null && data.equipmentData != null)
        {
            if (ItemDatabase.Instance != null && ItemDatabase.Instance.IsReady)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[EQUIP-TRACE] ApplySaveData: LoadEquipmentSaveData 호출 ({data.equipmentData.slots.Count}개)");
#endif
                EquipmentManager.Instance.LoadEquipmentSaveData(data.equipmentData);
                // ★ 장비 스킬 동기화 — 빈 슬롯의 잔류 스킬 클리어 포함
                EquipmentSkillSystem.Instance?.RefreshAllEquippedSkills();
                RestoreFullHealth();
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[EQUIP-TRACE] ApplySaveData: ItemDatabase 미준비 → WaitAndLoadEquipment 코루틴 ({data.equipmentData.slots.Count}개)");
#endif
                StartCoroutine(WaitAndLoadEquipment(data.equipmentData));
            }
        }
        else if (data.equipmentData != null && data.equipmentData.slots != null && data.equipmentData.slots.Count > 0)
        {
            // ★ MainScene에서만 대기 (FarmScene에는 EquipmentManager가 없음)
            string curScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (curScene == "MainScene")
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[EQUIP-TRACE] ApplySaveData: EquipMgr 미준비 → WaitAndLoadEquipment 코루틴 ({data.equipmentData.slots.Count}개)");
#endif
                StartCoroutine(WaitAndLoadEquipment(data.equipmentData));
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[EQUIP-TRACE] ApplySaveData: {curScene}에서는 장비 로드 스킵 (EquipmentManager 없는 씬), Bridge 보존={data.equipmentData.slots.Count}개");
#endif
            }
        }

        // ── 플레이어 위치 ──
        PlayerController savedPlayer = FindObjectOfType<PlayerController>();
        if (savedPlayer != null && data.playerPosition != Vector3.zero)
        {
            savedPlayer.transform.position = data.playerPosition;
            Debug.Log($"[SaveLoadManager] 플레이어 위치 복원: {data.playerPosition}");
        }

        // ── 오프라인 보상 ──
        if (OfflineRewardManager.Instance != null)
            OfflineRewardManager.Instance.LoadOfflineData(data);

        // ── 스테이지/웨이브 복원 ──
        Debug.Log($"[SaveLoadManager] ★ 웨이브 복원 시도: data.offlineCurrentWave={data.offlineCurrentWave}, WaveSpawner={WaveSpawner.Instance != null}");
        if (WaveSpawner.Instance != null)
        {
            WaveSpawner.Instance.ApplyWaveIndex(data.offlineCurrentWave);
            Debug.Log($"[SaveLoadManager] ★ 웨이브 복원 완료: {WaveSpawner.Instance.StageLabel}");
        }
        else
        {
            // ★ WaveSpawner가 아직 없으면 PendingWaveIndex에 보관
            // SaveGame이 중간에 GameDataBridge를 덮어써도 이 값은 안전
            WaveSpawner.PendingWaveIndex = data.offlineCurrentWave;
            Debug.LogWarning($"[SaveLoadManager] ⚠ WaveSpawner 없음 → PendingWaveIndex={data.offlineCurrentWave} 보관");
        }

        // ── 튜토리얼 ──
        // GameDataBridge에 tutorialPhase/Step 복원 (TutorialManager.Start()에서 읽음)
        if (GameDataBridge.CurrentData != null)
        {
            GameDataBridge.CurrentData.tutorialPhase = data.tutorialPhase;
            GameDataBridge.CurrentData.tutorialStep = data.tutorialStep;
            GameDataBridge.CurrentData.tutorialCompleted = data.tutorialCompleted;
        }
        if (TutorialManager.Instance != null && data.tutorialCompleted)
            TutorialManager.Instance.SetTutorialCompleted(true);

        // ── 사운드 설정 ──
        if (SoundManager.Instance != null && data.settings != null)
        {
            SoundManager.Instance.SetBGMVolume(data.settings.bgmVolume);
            SoundManager.Instance.SetSFXVolume(data.settings.sfxVolume);
            SoundManager.Instance.SetBGMMute(data.settings.bgmMuted);
            SoundManager.Instance.SetSFXMute(data.settings.sfxMuted);
        }

        // ── ★ 캐릭터 프로필 이미지 복원 ──
        if (RankingManager.Instance != null)
            RankingManager.Instance.RestorePortraitFromSave();

        Debug.Log($"[SaveLoadManager] 데이터 적용 완료 (씬: {data.currentScene})");
    }

    // ── ★ 농장 수확 아이템 ItemDatabase 사전 등록 ──────────────────
    /// <summary>
    /// LoadInventoryData() 전에 팜 수확 아이템을 ItemDatabase에 등록.
    /// FarmManager.RegisterHarvestItemsInDatabase()가 코루틴 재시도 중일 수 있어
    /// LoadInventoryData 시점에 아이템이 미등록 → "아이템 ID 찾을 수 없음" 드롭 방지.
    /// </summary>
    private void EnsureFarmHarvestItemsRegistered(SaveData data)
    {
        if (ItemDatabase.Instance == null || !ItemDatabase.Instance.IsReady)
        {
            Debug.LogWarning("[SaveLoadManager] ★ EnsureFarmHarvest: ItemDatabase 미준비 → 등록 스킵!");
            return;
        }

        int totalRegistered = 0;

        // 경로1: FarmManager.allCrops에서 등록
        if (FarmManager.Instance != null && FarmManager.Instance.allCrops != null)
        {
            foreach (var crop in FarmManager.Instance.allCrops)
            {
                if (crop?.harvestRewards == null) continue;
                foreach (var reward in crop.harvestRewards)
                {
                    if (reward?.item == null) continue;
                    ItemDatabase.Instance.RegisterItem(reward.item);
                    totalRegistered++;
                }
            }
        }

        // 경로2: Resources에서 모든 CropData SO를 탐색하여 등록
        CropData[] allCropAssets = Resources.FindObjectsOfTypeAll<CropData>();
        if (allCropAssets != null)
        {
            foreach (var crop in allCropAssets)
            {
                if (crop?.harvestRewards == null) continue;
                foreach (var reward in crop.harvestRewards)
                {
                    if (reward?.item == null) continue;
                    ItemDatabase.Instance.RegisterItem(reward.item);
                    totalRegistered++;
                }
            }
        }

        // 경로3: inventoryItems[]에 있는 모든 아이템을 직접 Resources에서 로드 시도
        // CropData를 경유하지 않고 ItemData SO를 직접 찾아 등록
        if (data?.inventoryItems != null)
        {
            foreach (var inv in data.inventoryItems)
            {
                if (inv.itemID <= 0) continue;
                // 이미 등록되어 있으면 스킵
                if (ItemDatabase.Instance.GetItemByID(inv.itemID) != null) continue;

                // 메모리에 로드된 모든 ItemData SO에서 직접 탐색
                ItemData[] allItems = Resources.FindObjectsOfTypeAll<ItemData>();
                foreach (var item in allItems)
                {
                    if (item != null && item.itemID == inv.itemID)
                    {
                        ItemDatabase.Instance.RegisterItem(item);
                        totalRegistered++;
                        Debug.Log($"[SaveLoadManager] ★ ItemData 직접 등록: {item.itemName} (itemID={inv.itemID})");
                        break;
                    }
                }
            }
        }

        Debug.Log($"[SaveLoadManager] ★ EnsureFarmHarvest 완료: {totalRegistered}개 등록, " +
                  $"FarmManager.allCrops={FarmManager.Instance?.allCrops?.Count ?? -1}, " +
                  $"CropData SO={allCropAssets?.Length ?? 0}, " +
                  $"밀Item 조회={ItemDatabase.Instance.GetItemByID(100)?.itemName ?? "NOT FOUND"}");
    }

    // ── ★ cropID → ItemData 조회 (FarmManager.allCrops 의존 제거) ──────
    /// <summary>
    /// cropID로 CropData를 찾습니다.
    /// FarmManager.allCrops → Resources.FindObjectsOfTypeAll 순으로 폴백.
    /// MainScene의 FarmManager.allCrops가 비어있어도 CropData SO를 찾을 수 있습니다.
    /// </summary>
    private CropData FindCropByID(int cropID)
    {
        // 1차: FarmManager.allCrops (가장 빠름)
        if (FarmManager.Instance != null)
        {
            CropData crop = FarmManager.Instance.GetCropByID(cropID);
            if (crop != null) return crop;
        }

        // 2차: 메모리에 로드된 모든 CropData SO에서 탐색 (비활성 포함)
        CropData[] allCrops = Resources.FindObjectsOfTypeAll<CropData>();
        if (allCrops != null)
        {
            foreach (var crop in allCrops)
            {
                if (crop != null && crop.cropID == cropID)
                {
                    Debug.Log($"[SaveLoadManager] ★ CropData 폴백 탐색 성공: cropID={cropID} → {crop.cropName} (Resources.FindAll)");
                    return crop;
                }
            }
        }

        return null;
    }

    // ── ★ 농장 수확물 → 메인 인벤토리 전달 ──────────────────
    /// <summary>
    /// farmData.inventoryData.harvests (cropID 기반)에 남아있는 수확물을
    /// InventoryManager의 기타탭 (ItemData 기반)으로 전달.
    ///
    /// ★ 수정1: FarmManager.allCrops 의존 제거 — FindCropByID()로 폴백 탐색
    /// ★ 수정2: inventoryItems[] 기반 중복 차감 → InventoryManager 실제 보유량 기반
    /// ★ 수정3: CropData 조회 실패 시 inventoryItems[]에서 itemID 직접 매칭 (최종 폴백)
    /// </summary>
    private void TransferFarmHarvestToInventory(SaveData data)
    {
        var harvests = data?.farmData?.inventoryData?.harvests;
        if (harvests == null || harvests.Count == 0) return;

        // ★ FarmManager.Instance 없어도 전달 시도 (FindCropByID가 폴백 처리)

        int transferred = 0;

        for (int i = harvests.Count - 1; i >= 0; i--)
        {
            var h = harvests[i];
            if (h.count <= 0) { harvests.RemoveAt(i); continue; }

            // ★ CropData 조회: FarmManager.allCrops → Resources.FindAll 폴백
            CropData crop = FindCropByID(h.cropID);
            ItemData harvestItem = null;

            if (crop != null && crop.harvestRewards != null)
            {
                foreach (var reward in crop.harvestRewards)
                {
                    if (reward?.item != null)
                    {
                        harvestItem = reward.item;
                        break;
                    }
                }
            }

            // ★ 최종 폴백: CropData를 찾을 수 없으면 inventoryItems[]에서 itemID 직접 매칭
            // AddItemToSaveData()가 수확 시 inventoryItems[]에 기록했으므로 거기서 복구
            if (harvestItem == null && data.inventoryItems != null)
            {
                foreach (var inv in data.inventoryItems)
                {
                    if (inv.count > 0)
                    {
                        ItemData item = ItemDatabase.Instance?.GetItemByID(inv.itemID);
                        // itemType == 9 (FarmVegetable) 인 아이템 중 첫 번째를 매칭
                        // 정확한 cropID→itemID 매핑이 불가하므로 경고와 함께 시도
                        if (item != null && (int)item.itemType == 9)
                        {
                            harvestItem = item;
                            Debug.LogWarning($"[SaveLoadManager] CropData 없는 cropID={h.cropID} → inventoryItems에서 팜 아이템 추정: {item.itemName} (itemID={inv.itemID})");
                            break;
                        }
                    }
                }
            }

            if (harvestItem == null)
            {
                Debug.LogWarning($"[SaveLoadManager] 수확물 전달 실패: cropID={h.cropID} — CropData/ItemData 모두 찾을 수 없음 → 데이터 유지");
                continue;
            }

            // ★ ItemDatabase 등록 보장
            ItemDatabase.Instance?.RegisterItem(harvestItem);

            // ★ 수확물은 무조건 AddItem — 중복 체크 없음
            // harvests[]는 팜에서 수확한 수량 그대로이므로 전량 추가
            if (InventoryManager.Instance != null)
            {
                bool ok = InventoryManager.Instance.AddItem(harvestItem, h.count);
                if (ok)
                {
                    Debug.Log($"[SaveLoadManager] 수확물 전달: {harvestItem.itemName} x{h.count} → 기타탭");
                    transferred++;
                }
            }

            // 전달 완료 → harvests에서 제거 (중복 전달 방지)
            harvests.RemoveAt(i);
        }

        // 전달 완료 후 harvests 리스트 초기화 (재진입 시 중복 전달 방지)
        data.farmData?.inventoryData?.harvests?.Clear();

        if (transferred > 0)
        {
            Debug.Log($"[TransferFarm] {transferred}개 전달 완료 → SaveGame 호출");
            SaveGame();
        }
    }

    // ── ItemDatabase 준비 후 장비 로드 ──────────────────
    private IEnumerator WaitAndLoadEquipment(EquipmentSaveData equipmentData)
    {
        float timeout = 10f;
        // ★ EquipmentManager + ItemDatabase 모두 준비될 때까지 대기
        while (timeout > 0f)
        {
            if (EquipmentManager.Instance != null
                && ItemDatabase.Instance != null && ItemDatabase.Instance.IsReady)
                break;
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (EquipmentManager.Instance == null)
        {
            Debug.LogError($"[EQUIP-TRACE] WaitAndLoadEquipment: 10초 타임아웃! EquipmentManager가 생성되지 않음");
            yield break;
        }

        Debug.Log($"[EQUIP-TRACE] WaitAndLoadEquipment: 로드 시작 ({equipmentData?.slots?.Count ?? 0}개)");
        EquipmentManager.Instance.LoadEquipmentSaveData(equipmentData);

        yield return null; // RecalculateStats 반영 1프레임 대기

        if (EquipmentSkillSystem.Instance != null)
            EquipmentSkillSystem.Instance.RefreshAllEquippedSkills();

        RestoreFullHealth();
        Debug.Log($"[EQUIP-TRACE] WaitAndLoadEquipment: 장비 지연 로드 완료 ({EquipmentManager.Instance.IsEquipmentLoaded}, {equipmentData?.slots?.Count ?? 0}개)");
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
        if (autoSave) SaveGame();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && autoSave)
        {
            SaveGame();
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
    public long playerGold;
    public long playerGem;

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

    public int equipmentTickets = 0;
    public int companionTickets = 0;
    public int relicTickets = 0;
    public int crystals = 0;
    public int essences = 0;
    public int fragments = 0;
    public long cropPoints = 0; // ★ 작물 포인트

    public int gachaLevel = 1;
    public int gachaCount = 0;

    // ── 동료 인벤토리 ──
    public CompanionSaveData[] companions;

    // ── 동료 핫바 ──
    public string[] companionHotbarIDs;  // 핫바 슬롯별 companionID (null/빈문자열 = 빈 슬롯)
    public bool autoSummonEnabled;

    public FarmSaveData farmData;
    public GameSettings settings;

    public int[] clearedStageIds;
    public bool[] unlockedAchievements;
    public int clearedStages;

    // ── 스킬 ──
    public int[] learnedSkillIDs;
    public int[] hotbarSkillIDs;
    public bool autoSkillEnabled;

    // ── VIP ──
    public int vipLevel = 0;
    public int vipExp = 0;
    public string vipExpireDate = "";
    public bool vipFreeGiftClaimed = false;
    public bool vipPaidGiftPurchased = false;
    public string vipClaimedFreeLevels = "";
    public string vipClaimedPaidLevels = "";

    // ── 오프라인 보상 ──
    public string lastLogoutTime;
    public float accumulatedOfflineMinutes;
    public float offlineGoldRate;
    public float offlineExpRate;
    public float offlineGemRate;
    public float offlineEquipTicketRate;
    public int offlineCurrentWave;

    // ── 2배 보상 횟수 ──
    public int adClaimCount;
    public string adClaimDate;

    // ── 튜토리얼 ──
    public bool tutorialCompleted;
    public int tutorialPhase = 0;  // 0=미시작, 1=Phase1완료, 2=Phase2완료, 3=Phase3완료, 99=전체완료
    public int tutorialStep = -1;  // 현재 튜토리얼 스텝 인덱스 (-1=비활성, 0+=진행 중)

    // ── 캐릭터 프로필 이미지 ──
    public int selectedPortraitIndex = 0; // ★ 랭킹/HUD 캐릭터 이미지 인덱스

    // ── 캐릭터 정보 (로그인씬 → 게임씬 전달) ──
    public string selectedCharacterName;
    public int characterClassType;
    public int activeCharacterSlot;
    public string accountID;
    public string selectedServer;
    public float charBaseHealth;
    public float charBaseAttack;
    public float charBaseDefense;
    public float charBaseSpeed;
    public float charAttackRange;
    public float charAttackSpeed;
}

[System.Serializable]
public class InventoryItemData
{
    public int itemID;
    public int count;
    public int slotIndex;
    public int enhanceLevel;
    public bool isUnlocked;
    public int itemLevel;

    // ★ 인스턴스 리스트 — 신규 형식 (Step 1)
    //   기존 세이브에는 없음 → 로드 시 count로부터 자동 생성 (마이그레이션)
    public List<EquipInstanceData> instances;
}

[System.Serializable]
public class EquipInstanceData
{
    public string instanceId;
    public int enhanceLevel;
    public int itemLevel;
    public bool isEquipped;
}

[System.Serializable]
public class EquippedSlotData
{
    public EquipmentType slotType;
    public int itemID = -1;
    public int enhanceLevel = 0;
    public int itemLevel = 0;
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
    public int currentQuestStatus = -1;   // ★ 현재 퀘스트 상태 (-1=없음, QuestStatus 캐스팅)
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
    public bool bgmMuted = false;
    public bool sfxMuted = false;
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