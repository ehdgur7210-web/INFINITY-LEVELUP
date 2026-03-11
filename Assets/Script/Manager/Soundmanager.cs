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

    // ★ 에디터와 빌드의 사운드 설정 분리
#if UNITY_EDITOR
    private const string SOUND_PREFIX = "EDITOR_";
#else
    private const string SOUND_PREFIX = "";
#endif

    private const string KEY_BGM_VOLUME = SOUND_PREFIX + "SoundManager_BGMVolume";
    private const string KEY_SFX_VOLUME = SOUND_PREFIX + "SoundManager_SFXVolume";
    private const string KEY_BGM_MUTE = SOUND_PREFIX + "SoundManager_BGMMute";
    private const string KEY_SFX_MUTE = SOUND_PREFIX + "SoundManager_SFXMute";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

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
        // 볼륨 설정 초기화
        PlayerPrefs.DeleteKey("SoundManager_BGMVolume");
        PlayerPrefs.DeleteKey("SoundManager_BGMMute");
        PlayerPrefs.Save();

        bgmVolume = 1f;
        isBGMMuted = false;

        PlayBGM("시작음");

        Debug.Log($"[SoundManager] BGM 재생 시도 - volume: {bgmSource.volume}, isPlaying: {bgmSource.isPlaying}, clip: {bgmSource.clip}");
    }

    private void InitializeSoundManager()
    {
        SetupAudioSources();
        BuildClipDictionaries();
        CreateSFXPool();
        LoadSettings();
    }

    private void SetupAudioSources()
    {
        if (bgmSource == null)
            bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.priority = 0;

        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();
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
    //  설정 저장/로드 (PlayerPrefs)
    // ==========================================================

    /// <summary>볼륨 및 음소거 설정 저장</summary>
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(KEY_BGM_VOLUME, bgmVolume);
        PlayerPrefs.SetFloat(KEY_SFX_VOLUME, sfxVolume);
        PlayerPrefs.SetInt(KEY_BGM_MUTE, isBGMMuted ? 1 : 0);
        PlayerPrefs.SetInt(KEY_SFX_MUTE, isSFXMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>저장된 설정 불러오기</summary>
    public void LoadSettings()
    {
        bgmVolume = PlayerPrefs.GetFloat(KEY_BGM_VOLUME, 1f);
        sfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, 1f);
        isBGMMuted = PlayerPrefs.GetInt(KEY_BGM_MUTE, 0) == 1;
        isSFXMuted = PlayerPrefs.GetInt(KEY_SFX_MUTE, 0) == 1;

        if (bgmSource != null)
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume;

        Debug.Log($"[SoundManager] 설정 로드 - BGM볼륨:{bgmVolume:F2}, SFX볼륨:{sfxVolume:F2}");
    }
}