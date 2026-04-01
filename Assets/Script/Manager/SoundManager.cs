using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [System.Serializable]
    public class SoundClip
    {
        public string clipName;     // 클립을 찾을 때 사용할 이름 (예: "ButtonClick")
        public AudioClip clip;      // 실제 오디오 클립 에셋
        [Range(0f, 1f)]
        public float volume = 1f;   // 이 클립 고유의 볼륨 (0~1)
    }

    [Header("===== BGM(배경음악) 클립 목록 =====")]
    [Tooltip("배경음악으로 사용할 오디오 클립들을 여기에 등록하세요")]
    [SerializeField] private SoundClip[] bgmClips;

    [Header("===== SFX(효과음) 클립 목록 =====")]
    [Tooltip("효과음으로 사용할 오디오 클립들을 여기에 등록하세요")]
    [SerializeField] private SoundClip[] sfxClips;

    [Header("===== 오디오 소스 (자동 생성됨) =====")]
    [Tooltip("BGM 재생용 AudioSource (없으면 자동 생성)")]
    [SerializeField] private AudioSource bgmSource;

    [Tooltip("SFX 재생용 AudioSource (없으면 자동 생성)")]
    [SerializeField] private AudioSource sfxSource;

    [Header("===== 기본 설정 =====")]
    [Tooltip("BGM 교체 시 페이드 시간 (초)")]
    [SerializeField] private float bgmFadeDuration = 1.0f;

    [Tooltip("SFX 동시 재생 최대 수")]
    [SerializeField] private int maxSFXSources = 5;

    private float bgmVolume = 1f;
    private float sfxVolume = 1f;
    private bool isBGMMuted = false;
    private bool isSFXMuted = false;
    private string currentBGMName = "";
    private Coroutine bgmFadeCoroutine;

    private Dictionary<string, SoundClip> bgmDict = new Dictionary<string, SoundClip>();
    private Dictionary<string, SoundClip> sfxDict = new Dictionary<string, SoundClip>();
    private List<AudioSource> sfxSourcePool = new List<AudioSource>();


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] SoundManager가 생성되었습니다.");
            DontDestroyOnLoad(gameObject);

            InitializeSoundManager();
        }
        else
        {
            // 새 씬의 SoundManager에 오디오 클립 데이터가 있으면 기존 것에 덮어쓰기
            bool hasData = (bgmClips != null && bgmClips.Length > 0) ||
                           (sfxClips != null && sfxClips.Length > 0);

            if (hasData)
            {
                if (bgmClips != null && bgmClips.Length > 0)
                    Instance.bgmClips = bgmClips;
                if (sfxClips != null && sfxClips.Length > 0)
                    Instance.sfxClips = sfxClips;

                Instance.BuildClipDictionaries();
                Debug.Log("[SoundManager] 씬 전환 감지 → 오디오 클립 갱신 완료!");
            }

            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // ★ Inspector에 클립이 없으면 Resources 폴더에서 자동 로드 시도
        if ((bgmClips == null || bgmClips.Length == 0) && (sfxClips == null || sfxClips.Length == 0))
        {
            AutoLoadClipsFromResources();
            BuildClipDictionaries();
        }

        bgmVolume = 1f;
        isBGMMuted = false;

        // ★ BGM 재생 시도 (우선순위: 딕셔너리 → AudioSource 기존 클립)
        if (bgmDict.ContainsKey("시작음"))
        {
            PlayBGM("시작음");
        }
        else if (bgmDict.Count > 0)
        {
            // "시작음"이 없으면 첫 번째 등록된 BGM 재생
            foreach (var kvp in bgmDict)
            {
                PlayBGM(kvp.Key);
                break;
            }
        }
        else if (bgmSource != null && bgmSource.clip != null)
        {
            // ★ Inspector에 클립이 비어있어도, AudioSource에 직접 할당된 클립이 있으면 재생
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume;
            bgmSource.Play();
            currentBGMName = bgmSource.clip.name;
            Debug.Log($"[SoundManager] AudioSource 기존 클립 재생: {bgmSource.clip.name}");
        }
        else
        {
            Debug.LogWarning("[SoundManager] BGM 클립이 없습니다! Inspector에서 bgmClips 배열을 설정하거나 AudioSource에 클립을 할당하세요.");
        }

        Debug.Log($"[SoundManager] BGM 재생 시도 - volume: {bgmSource.volume}, isPlaying: {bgmSource.isPlaying}, clip: {bgmSource.clip}");
    }

    private void InitializeSoundManager()
    {
        SetupAudioSources();
        BuildClipDictionaries();
        CreateSFXPool();
        LoadSettings();
    }

    /// <summary>
    /// Inspector에 클립이 비어있을 때 Resources/Sound 폴더에서 자동 로드.
    /// 파일명을 기반으로 BGM/SFX를 자동 분류하고, SFX 퀵 메서드 이름에 매핑.
    /// </summary>
    private void AutoLoadClipsFromResources()
    {
        // Resources/Sound 폴더에서 모든 AudioClip 로드
        AudioClip[] allClips = Resources.LoadAll<AudioClip>("Sound");
        if (allClips == null || allClips.Length == 0)
        {
            Debug.LogWarning("[SoundManager] Resources/Sound 폴더에 오디오 클립이 없습니다.");
            return;
        }

        var bgmList = new List<SoundClip>();
        var sfxList = new List<SoundClip>();

        // ★ SFX 파일명 → 퀵 메서드 이름 매핑 (파일명 키워드 기반)
        var sfxMapping = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "SFX_UI_Button", "ButtonClick" },
            { "013_Confirm", "Purchase" },
            { "071_Unequip", "Equip" },
            { "079_Buy_sell", "SellItem" },
            { "033_Denied", "EnhanceFail" },
            { "02_Heal", "EnhanceSuccess" },
            { "22_Slash", "MeleeAttack" },
            { "Hand Gun", "BulletFire" },
            { "Magic Spell", "MagicCast" },
            { "Bullet Impact", "MonsterHit" },
            { "77_flesh", "MonsterDeath" },
            { "52_Dive", "SkillUse" },
            { "45_Landing", "LevelUp" },
            { "35_Miss", "GachaRoll" },
        };

        foreach (var clip in allClips)
        {
            // BGM 판별: 파일명에 "BGM" 또는 "Background Music" 포함
            if (clip.name.Contains("BGM") || clip.name.Contains("Background Music"))
            {
                string bgmName = clip.name;
                // "Background Music" → "시작음"으로 매핑
                if (clip.name.Contains("Background Music"))
                    bgmName = "시작음";

                bgmList.Add(new SoundClip { clipName = bgmName, clip = clip, volume = 1f });
            }
            else
            {
                // SFX 매핑 시도
                string sfxName = clip.name; // 기본: 파일명 그대로
                foreach (var mapping in sfxMapping)
                {
                    if (clip.name.Contains(mapping.Key))
                    {
                        sfxName = mapping.Value;
                        break;
                    }
                }
                sfxList.Add(new SoundClip { clipName = sfxName, clip = clip, volume = 1f });
            }
        }

        if (bgmList.Count > 0) bgmClips = bgmList.ToArray();
        if (sfxList.Count > 0) sfxClips = sfxList.ToArray();

        Debug.Log($"[SoundManager] ★ Resources/Sound에서 자동 로드 — BGM {bgmList.Count}개, SFX {sfxList.Count}개");
    }

    private void SetupAudioSources()
    {
        // ★ 기존 AudioSource가 있으면 재사용 (Inspector에서 클립 설정된 경우 보존)
        if (bgmSource == null)
        {
            AudioSource[] existing = GetComponents<AudioSource>();
            if (existing.Length > 0)
            {
                bgmSource = existing[0];
                Debug.Log($"[SoundManager] 기존 AudioSource 재사용 (clip: {bgmSource.clip?.name ?? "없음"})");
            }
            else
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
            }
        }
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.priority = 0;

        if (sfxSource == null)
        {
            AudioSource[] existing = GetComponents<AudioSource>();
            // bgmSource와 다른 AudioSource를 찾거나, 없으면 새로 추가
            sfxSource = null;
            foreach (var src in existing)
                if (src != bgmSource) { sfxSource = src; break; }
            if (sfxSource == null)
                sfxSource = gameObject.AddComponent<AudioSource>();
        }
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
    }

    public void BuildClipDictionaries()
    {
        if (bgmClips != null)
            foreach (SoundClip sc in bgmClips)
                if (sc.clip != null && !string.IsNullOrEmpty(sc.clipName))
                    bgmDict[sc.clipName] = sc;

        if (sfxClips != null)
            foreach (SoundClip sc in sfxClips)
                if (sc.clip != null && !string.IsNullOrEmpty(sc.clipName))
                    sfxDict[sc.clipName] = sc;

        Debug.Log($"[SoundManager] BGM {bgmDict.Count}개, SFX {sfxDict.Count}개 등록됨");
    }

    private void CreateSFXPool()
    {
        for (int i = 0; i < maxSFXSources; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.loop = false;
            source.playOnAwake = false;
            sfxSourcePool.Add(source);
        }
    }

    // ==========================================================
    //  BGM 재생 메서드
    // ==========================================================

    /// <summary>BGM 재생</summary>
    public void PlayBGM(string clipName, bool fadeIn = true)
    {
        if (currentBGMName == clipName && bgmSource.isPlaying) return;

        if (!bgmDict.ContainsKey(clipName))
        {
            Debug.LogWarning($"[SoundManager] BGM '{clipName}'을(를) 찾을 수 없습니다!");
            return;
        }

        SoundClip soundClip = bgmDict[clipName];

        if (bgmFadeCoroutine != null)
            StopCoroutine(bgmFadeCoroutine);

        if (fadeIn && bgmSource.isPlaying)
        {
            bgmFadeCoroutine = StartCoroutine(CrossFadeBGM(soundClip));
        }
        else
        {
            bgmSource.clip = soundClip.clip;
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume * soundClip.volume;
            bgmSource.Play();
        }

        currentBGMName = clipName;
        Debug.Log($"[SoundManager] BGM 재생: {clipName}");
    }

    /// <summary>AudioClip을 직접 받아 BGM 크로스페이드 재생 (StageDataSO 연동용)</summary>
    public void CrossFadeBGM(AudioClip clip)
    {
        if (clip == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        if (bgmFadeCoroutine != null)
            StopCoroutine(bgmFadeCoroutine);

        var wrapper = new SoundClip { clip = clip, volume = 1f };

        if (bgmSource.isPlaying)
        {
            bgmFadeCoroutine = StartCoroutine(CrossFadeBGM(wrapper));
        }
        else
        {
            bgmSource.clip = clip;
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume;
            bgmSource.Play();
        }

        currentBGMName = clip.name;
    }

    /// <summary>BGM 정지</summary>
    public void StopBGM(bool fadeOut = true)
    {
        if (!bgmSource.isPlaying) return;

        if (fadeOut)
        {
            if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = StartCoroutine(FadeOutBGM());
        }
        else
        {
            bgmSource.Stop();
        }

        currentBGMName = "";
    }

    /// <summary>BGM 일시정지</summary>
    public void PauseBGM()
    {
        if (bgmSource.isPlaying) bgmSource.Pause();
    }

    /// <summary>BGM 일시정지 해제</summary>
    public void ResumeBGM()
    {
        if (!bgmSource.isPlaying && bgmSource.clip != null)
            bgmSource.UnPause();
    }

    private IEnumerator CrossFadeBGM(SoundClip newClip)
    {
        float timer = 0f;
        float startVolume = bgmSource.volume;
        float halfDuration = bgmFadeDuration / 2f;

        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, timer / halfDuration);
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.clip = newClip.clip;
        bgmSource.Play();

        timer = 0f;
        float targetVolume = isBGMMuted ? 0f : bgmVolume * newClip.volume;

        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0f, targetVolume, timer / halfDuration);
            yield return null;
        }

        bgmSource.volume = targetVolume;
        bgmFadeCoroutine = null;
    }

    private IEnumerator FadeOutBGM()
    {
        float timer = 0f;
        float startVolume = bgmSource.volume;

        while (timer < bgmFadeDuration)
        {
            timer += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, timer / bgmFadeDuration);
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.volume = 0f;
        bgmFadeCoroutine = null;
    }

    // ==========================================================
    //  SFX (효과음) 재생 메서드
    // ==========================================================

    /// <summary>효과음 재생 (이름으로 찾기)</summary>
    public void PlaySFX(string clipName)
    {
        if (isSFXMuted) return;

        if (!sfxDict.ContainsKey(clipName))
        {
            Debug.LogWarning($"[SoundManager] SFX '{clipName}'을(를) 찾을 수 없습니다!");
            return;
        }

        SoundClip soundClip = sfxDict[clipName];
        AudioSource availableSource = GetAvailableSFXSource();

        if (availableSource != null)
        {
            availableSource.clip = soundClip.clip;
            availableSource.volume = sfxVolume * soundClip.volume;
            availableSource.Play();
        }
        else
        {
            sfxSource.PlayOneShot(soundClip.clip, sfxVolume * soundClip.volume);
        }
    }

    /// <summary>효과음 재생 (AudioClip 직접 전달)</summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (isSFXMuted || clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    private AudioSource GetAvailableSFXSource()
    {
        foreach (AudioSource source in sfxSourcePool)
            if (!source.isPlaying) return source;
        return null;
    }

    // ==========================================================
    //  자주 사용하는 효과음 단축 메서드
    // ==========================================================

    /// <summary>버튼 클릭</summary>
    public void PlayButtonClick() { PlaySFX("ButtonClick"); }
    /// <summary>장비 착용</summary>
    public void PlayEquipSound() { PlaySFX("Equip"); }
    /// <summary>강화 성공</summary>
    public void PlayEnhanceSuccess() { PlaySFX("EnhanceSuccess"); }
    /// <summary>강화 실패</summary>
    public void PlayEnhanceFail() { PlaySFX("EnhanceFail"); }
    /// <summary>상품 구매</summary>
    public void PlayPurchaseSound() { PlaySFX("Purchase"); }
    /// <summary>가챠 뽑기</summary>
    public void PlayGachaSound() { PlaySFX("Gacha"); }
    /// <summary>레벨업</summary>
    public void PlayLevelUpSound() { PlaySFX("LevelUp"); }
    /// <summary>스킬 사용</summary>
    public void PlaySkillSound() { PlaySFX("SkillUse"); }
    /// <summary>몬스터 처치</summary>
    public void PlayMonsterDeathSound() { PlaySFX("MonsterDeath"); }
    /// <summary>패널 열기</summary>
    public void PlayPanelOpen() { PlaySFX("PanelOpen"); }
    /// <summary>패널 닫기</summary>
    public void PlayPanelClose() { PlaySFX("PanelClose"); }
    /// <summary>총알 발사</summary>
    public void PlayBulletFire() { PlaySFX("BulletFire"); }
    /// <summary>근거리 공격</summary>
    public void PlayMeleeAttack() { PlaySFX("MeleeAttack"); }
    /// <summary>마법 시전</summary>
    public void PlayMagicCast() { PlaySFX("MagicCast"); }
    /// <summary>몬스터 피격</summary>
    public void PlayMonsterHit() { PlaySFX("MonsterHit"); }
    /// <summary>로그인 성공</summary>
    public void PlayLoginSuccess() { PlaySFX("LoginSuccess"); }
    /// <summary>회원가입</summary>
    public void PlayRegister() { PlaySFX("Register"); }
    /// <summary>서버 진입</summary>
    public void PlayServerEnter() { PlaySFX("ServerEnter"); }
    /// <summary>캐릭터 선택</summary>
    public void PlayCharacterSelect() { PlaySFX("CharacterSelect"); }
    /// <summary>게임 시작</summary>
    public void PlayGameStart() { PlaySFX("GameStart"); }
    /// <summary>가챠 롤</summary>
    public void PlayGachaRoll() { PlaySFX("GachaRoll"); }
    /// <summary>제작 성공</summary>
    public void PlayCraftSuccess() { PlaySFX("CraftSuccess"); }
    /// <summary>제작 실패</summary>
    public void PlayCraftFail() { PlaySFX("CraftFail"); }
    /// <summary>업적 보상</summary>
    public void PlayAchievementReward() { PlaySFX("AchievementReward"); }
    /// <summary>퀘스트 보상</summary>
    public void PlayQuestReward() { PlaySFX("QuestReward"); }
    /// <summary>메일 보상</summary>
    public void PlayMailReward() { PlaySFX("MailReward"); }
    /// <summary>쿠폰 보상</summary>
    public void PlayCouponReward() { PlaySFX("CouponReward"); }
    /// <summary>아이템 판매</summary>
    public void PlaySellItem() { PlaySFX("SellItem"); }
    /// <summary>경매장 입찰</summary>
    public void PlayAuctionBid() { PlaySFX("AuctionBid"); }
    /// <summary>경매장 즉구입</summary>
    public void PlayAuctionBuyout() { PlaySFX("AuctionBuyout"); }
    /// <summary>경매장 등록</summary>
    public void PlayAuctionRegister() { PlaySFX("AuctionRegister"); }
    /// <summary>장비 장착</summary>
    public void PlayEquip() { PlaySFX("Equip"); }
    /// <summary>스킬 습득</summary>
    public void PlaySkillLearn() { PlaySFX("SkillLearn"); }
    /// <summary>오프라인 보상</summary>
    public void PlayOfflineReward() { PlaySFX("OfflineReward"); }

    // ==========================================================
    //  볼륨 설정
    // ==========================================================

    /// <summary>BGM 볼륨 설정 (0.0 ~ 1.0)</summary>
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);

        if (!isBGMMuted && bgmSource != null)
        {
            float clipVolume = 1f;
            if (!string.IsNullOrEmpty(currentBGMName) && bgmDict.ContainsKey(currentBGMName))
                clipVolume = bgmDict[currentBGMName].volume;
            bgmSource.volume = bgmVolume * clipVolume;
        }

        SaveSettings();
    }

    /// <summary>SFX 볼륨 설정 (0.0 ~ 1.0)</summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        SaveSettings();
    }

    /// <summary>현재 BGM 볼륨 가져오기</summary>
    public float GetBGMVolume() => bgmVolume;

    /// <summary>현재 SFX 볼륨 가져오기</summary>
    public float GetSFXVolume() => sfxVolume;

    // ==========================================================
    //  음소거 (Mute)
    // ==========================================================

    /// <summary>BGM 음소거 토글</summary>
    public void ToggleBGMMute()
    {
        isBGMMuted = !isBGMMuted;
        if (bgmSource != null)
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume;
        SaveSettings();
        Debug.Log($"[SoundManager] BGM 음소거: {isBGMMuted}");
    }

    /// <summary>SFX 음소거 토글</summary>
    public void ToggleSFXMute()
    {
        isSFXMuted = !isSFXMuted;
        SaveSettings();
        Debug.Log($"[SoundManager] SFX 음소거: {isSFXMuted}");
    }

    public bool IsBGMMuted() => isBGMMuted;
    public bool IsSFXMuted() => isSFXMuted;

    /// <summary>BGM 음소거 직접 설정</summary>
    public void SetBGMMute(bool mute)
    {
        isBGMMuted = mute;
        if (bgmSource != null)
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume;
        SaveSettings();
    }

    /// <summary>SFX 음소거 직접 설정</summary>
    public void SetSFXMute(bool mute)
    {
        isSFXMuted = mute;
        SaveSettings();
    }

    // ==========================================================
    //  설정 저장/로드 (GameDataBridge)
    // ==========================================================

    /// <summary>볼륨 및 음소거 설정을 GameDataBridge.CurrentData에 저장</summary>
    public void SaveSettings()
    {
        SaveData data = GameDataBridge.CurrentData;
        if (data == null || data.settings == null) return;
        data.settings.bgmVolume = bgmVolume;
        data.settings.sfxVolume = sfxVolume;
        data.settings.bgmMuted  = isBGMMuted;
        data.settings.sfxMuted  = isSFXMuted;
    }

    /// <summary>GameDataBridge.CurrentData에서 설정 불러오기</summary>
    public void LoadSettings()
    {
        SaveData data = GameDataBridge.CurrentData;
        if (data != null && data.settings != null)
        {
            bgmVolume  = data.settings.bgmVolume > 0f ? data.settings.bgmVolume : 1f;
            sfxVolume  = data.settings.sfxVolume > 0f ? data.settings.sfxVolume : 1f;
            isBGMMuted = data.settings.bgmMuted;
            isSFXMuted = data.settings.sfxMuted;
        }

        if (bgmSource != null)
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume;

        Debug.Log($"[SoundManager] 설정 로드 - BGM볼륨:{bgmVolume:F2}, SFX볼륨:{sfxVolume:F2}");
    }
}