using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ============================================================
/// SoundManager - 게임 전체 사운드를 관리하는 싱글톤 매니저
/// ============================================================
/// 
/// 【역할】
/// - BGM(배경음악) 재생/정지/전환
/// - SFX(효과음) 재생
/// - 볼륨 조절 및 PlayerPrefs에 설정 저장/로드
/// - 씬 전환 시에도 유지 (DontDestroyOnLoad)
/// 
/// 【사용법 - 다른 스크립트에서 호출하기】
/// SoundManager.Instance.PlayBGM("MainBGM");       // BGM 재생
/// SoundManager.Instance.PlaySFX("ButtonClick");    // 효과음 재생
/// SoundManager.Instance.SetBGMVolume(0.5f);        // BGM 볼륨 설정
/// SoundManager.Instance.SetSFXVolume(0.8f);        // SFX 볼륨 설정
/// 
/// 【Unity 설정 방법】
/// 1. 빈 게임오브젝트 "SoundManager" 생성
/// 2. 이 스크립트를 붙이기
/// 3. Inspector에서 bgmClips, sfxClips 배열에 오디오 클립 등록
///    - 각 클립의 clipName을 지정 (예: "MainBGM", "ButtonClick")
/// 4. bgmSource, sfxSource는 자동 생성됨 (수동 할당도 가능)
/// ============================================================
/// </summary>
public class SoundManager : MonoBehaviour
{
    // ===== 싱글톤 인스턴스 =====
    // 어디서든 SoundManager.Instance로 접근 가능
    public static SoundManager Instance { get; private set; }

    // ───────────────────────────────────────────
    // [직렬화된 오디오 클립 구조체]
    // Inspector에서 이름과 클립을 짝지어 등록할 수 있음
    // ───────────────────────────────────────────
    [System.Serializable]
    public class SoundClip
    {
        public string clipName;     // 클립을 찾을 때 사용할 이름 (예: "ButtonClick")
        public AudioClip clip;      // 실제 오디오 클립 파일
        [Range(0f, 1f)]
        public float volume = 1f;   // 이 클립 고유의 볼륨 (0~1)
    }

    // ───────────────────────────────────────────
    // [Inspector에서 설정할 필드들]
    // ───────────────────────────────────────────
    [Header("===== BGM(배경음악) 클립 목록 =====")]
    [Tooltip("배경음악으로 사용할 오디오 클립들을 여기에 등록하세요")]
    [SerializeField] private SoundClip[] bgmClips;

    [Header("===== SFX(효과음) 클립 목록 =====")]
    [Tooltip("효과음으로 사용할 오디오 클립들을 여기에 등록하세요")]
    [SerializeField] private SoundClip[] sfxClips;

    [Header("===== 오디오 소스 (자동 생성됨) =====")]
    [Tooltip("BGM 재생용 AudioSource (비워두면 자동 생성)")]
    [SerializeField] private AudioSource bgmSource;

    [Tooltip("SFX 재생용 AudioSource (비워두면 자동 생성)")]
    [SerializeField] private AudioSource sfxSource;

    [Header("===== 기본 설정 =====")]
    [Tooltip("BGM 전환 시 페이드 시간 (초)")]
    [SerializeField] private float bgmFadeDuration = 1.0f;

    [Tooltip("SFX 동시 재생 최대 수")]
    [SerializeField] private int maxSFXSources = 5;

    // ───────────────────────────────────────────
    // [내부 변수들]
    // ───────────────────────────────────────────
    private float bgmVolume = 1f;               // 현재 BGM 마스터 볼륨 (0~1)
    private float sfxVolume = 1f;               // 현재 SFX 마스터 볼륨 (0~1)
    private bool isBGMMuted = false;            // BGM 음소거 여부
    private bool isSFXMuted = false;            // SFX 음소거 여부
    private string currentBGMName = "";         // 현재 재생 중인 BGM 이름
    private Coroutine bgmFadeCoroutine;         // BGM 페이드 코루틴 참조

    // 빠른 검색을 위한 딕셔너리 (이름 → 클립)
    private Dictionary<string, SoundClip> bgmDict = new Dictionary<string, SoundClip>();
    private Dictionary<string, SoundClip> sfxDict = new Dictionary<string, SoundClip>();

    // 추가 SFX 소스 풀 (동시에 여러 효과음 재생용)
    private List<AudioSource> sfxSourcePool = new List<AudioSource>();

    // ───────────────────────────────────────────
    // PlayerPrefs 저장 키 (설정 저장용)
    // ───────────────────────────────────────────
    private const string KEY_BGM_VOLUME = "SoundManager_BGMVolume";
    private const string KEY_SFX_VOLUME = "SoundManager_SFXVolume";
    private const string KEY_BGM_MUTE = "SoundManager_BGMMute";
    private const string KEY_SFX_MUTE = "SoundManager_SFXMute";

    // ==========================================================
    //  Unity 라이프사이클 메서드
    // ==========================================================

    /// <summary>
    /// 싱글톤 초기화 - 가장 먼저 실행됨
    /// </summary>
    void Awake()
    {
        // 싱글톤 패턴: 이미 인스턴스가 있으면 자신을 파괴
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시에도 파괴되지 않음
            InitializeSoundManager();       // 사운드 시스템 초기화
        }
        else
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // 저장된 설정 초기화 (디버그용)
        PlayerPrefs.DeleteKey("SoundManager_BGMVolume");
        PlayerPrefs.DeleteKey("SoundManager_BGMMute");
        PlayerPrefs.Save();

        bgmVolume = 1f;
        isBGMMuted = false;

        PlayBGM("시작음");

        Debug.Log($"[SoundManager] BGM 재생 시도 - volume: {bgmSource.volume}, isPlaying: {bgmSource.isPlaying}, clip: {bgmSource.clip}");
    }

    // ==========================================================
    //  초기화
    // ==========================================================

    /// <summary>
    /// 사운드 매니저 전체 초기화
    /// - AudioSource 생성
    /// - 딕셔너리 구축
    /// - 저장된 설정 불러오기
    /// </summary>
    private void InitializeSoundManager()
    {
        // ① AudioSource 컴포넌트 자동 생성 (Inspector에서 할당 안 했을 경우)
        SetupAudioSources();

        // ② 클립 배열을 딕셔너리로 변환 (빠른 이름 검색용)
        BuildClipDictionaries();

        // ③ SFX 소스 풀 생성 (동시 재생용)
        CreateSFXPool();

        // ④ PlayerPrefs에서 저장된 볼륨/음소거 설정 불러오기
        LoadSettings();

        Debug.Log("[SoundManager] 초기화 완료!");
    }

    /// <summary>
    /// AudioSource 컴포넌트 설정
    /// BGM용: 루프 켜기 / SFX용: 루프 끄기
    /// </summary>
    private void SetupAudioSources()
    {
        // BGM용 AudioSource 생성
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }
        bgmSource.loop = true;             // BGM은 반복 재생
        bgmSource.playOnAwake = false;      // 자동 재생 끄기
        bgmSource.priority = 0;            // 최우선 순위 (BGM은 항상 재생)

        // SFX용 AudioSource 생성
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        sfxSource.loop = false;             // 효과음은 한 번만 재생
        sfxSource.playOnAwake = false;      // 자동 재생 끄기
    }

    /// <summary>
    /// 클립 배열 → 딕셔너리 변환
    /// 이름으로 O(1) 검색 가능하게 함
    /// </summary>
    private void BuildClipDictionaries()
    {
        // BGM 딕셔너리 구축
        if (bgmClips != null)
        {
            foreach (SoundClip sc in bgmClips)
            {
                if (sc.clip != null && !string.IsNullOrEmpty(sc.clipName))
                {
                    bgmDict[sc.clipName] = sc;
                }
            }
        }

        // SFX 딕셔너리 구축
        if (sfxClips != null)
        {
            foreach (SoundClip sc in sfxClips)
            {
                if (sc.clip != null && !string.IsNullOrEmpty(sc.clipName))
                {
                    sfxDict[sc.clipName] = sc;
                }
            }
        }

        Debug.Log($"[SoundManager] BGM {bgmDict.Count}개, SFX {sfxDict.Count}개 등록됨");
    }

    /// <summary>
    /// SFX 동시 재생을 위한 AudioSource 풀 생성
    /// (예: 칼 휘두르는 소리 + 몬스터 피격 소리 동시 재생)
    /// </summary>
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
    //  BGM (배경음악) 관련 메서드
    // ==========================================================

    /// <summary>
    /// BGM 재생
    /// 같은 BGM이 이미 재생 중이면 무시함
    /// </summary>
    /// <param name="clipName">재생할 BGM 이름 (bgmClips에서 등록한 이름)</param>
    /// <param name="fadeIn">페이드인 사용 여부 (true면 서서히 커짐)</param>
    public void PlayBGM(string clipName, bool fadeIn = true)
    {
        // 같은 BGM이 이미 재생 중이면 무시
        if (currentBGMName == clipName && bgmSource.isPlaying)
        {
            return;
        }

        // 딕셔너리에서 클립 검색
        if (!bgmDict.ContainsKey(clipName))
        {
            Debug.LogWarning($"[SoundManager] BGM '{clipName}'을(를) 찾을 수 없습니다!");
            return;
        }

        SoundClip soundClip = bgmDict[clipName];

        // 기존 페이드 코루틴이 있으면 중지
        if (bgmFadeCoroutine != null)
        {
            StopCoroutine(bgmFadeCoroutine);
        }

        if (fadeIn && bgmSource.isPlaying)
        {
            // 현재 BGM → 새 BGM으로 크로스페이드
            bgmFadeCoroutine = StartCoroutine(CrossFadeBGM(soundClip));
        }
        else
        {
            // 즉시 재생
            bgmSource.clip = soundClip.clip;
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume * soundClip.volume;
            bgmSource.Play();
        }

        currentBGMName = clipName;
        Debug.Log($"[SoundManager] BGM 재생: {clipName}");
    }

    /// <summary>
    /// BGM 정지
    /// </summary>
    /// <param name="fadeOut">페이드아웃 사용 여부</param>
    public void StopBGM(bool fadeOut = true)
    {
        if (!bgmSource.isPlaying) return;

        if (fadeOut)
        {
            // 기존 페이드 코루틴 중지 후 페이드아웃 시작
            if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = StartCoroutine(FadeOutBGM());
        }
        else
        {
            bgmSource.Stop();
        }

        currentBGMName = "";
    }

    /// <summary>
    /// BGM 일시정지
    /// </summary>
    public void PauseBGM()
    {
        if (bgmSource.isPlaying)
        {
            bgmSource.Pause();
        }
    }

    /// <summary>
    /// BGM 일시정지 해제 (이어서 재생)
    /// </summary>
    public void ResumeBGM()
    {
        if (!bgmSource.isPlaying && bgmSource.clip != null)
        {
            bgmSource.UnPause();
        }
    }

    /// <summary>
    /// BGM 크로스페이드 (기존 BGM 서서히 줄이고 → 새 BGM 서서히 키움)
    /// </summary>
    private IEnumerator CrossFadeBGM(SoundClip newClip)
    {
        float timer = 0f;
        float startVolume = bgmSource.volume;
        float halfDuration = bgmFadeDuration / 2f;

        // ① 페이드아웃 (현재 BGM 볼륨 줄이기)
        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, timer / halfDuration);
            yield return null; // 다음 프레임까지 대기
        }

        // ② 클립 교체 후 재생
        bgmSource.Stop();
        bgmSource.clip = newClip.clip;
        bgmSource.Play();

        // ③ 페이드인 (새 BGM 볼륨 키우기)
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

    /// <summary>
    /// BGM 페이드아웃 (서서히 볼륨 줄여서 정지)
    /// </summary>
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
    //  SFX (효과음) 관련 메서드
    // ==========================================================

    /// <summary>
    /// 효과음 재생 (이름으로 찾기)
    /// </summary>
    /// <param name="clipName">재생할 효과음 이름 (sfxClips에서 등록한 이름)</param>
    public void PlaySFX(string clipName)
    {
        // 음소거 상태면 재생하지 않음
        if (isSFXMuted) return;

        // 딕셔너리에서 클립 검색
        if (!sfxDict.ContainsKey(clipName))
        {
            Debug.LogWarning($"[SoundManager] SFX '{clipName}'을(를) 찾을 수 없습니다!");
            return;
        }

        SoundClip soundClip = sfxDict[clipName];

        // 사용 가능한(재생 중이 아닌) AudioSource 찾기
        AudioSource availableSource = GetAvailableSFXSource();
        if (availableSource != null)
        {
            availableSource.clip = soundClip.clip;
            availableSource.volume = sfxVolume * soundClip.volume;
            availableSource.Play();
        }
        else
        {
            // 풀에 여유가 없으면 기본 sfxSource로 OneShot 재생
            sfxSource.PlayOneShot(soundClip.clip, sfxVolume * soundClip.volume);
        }
    }

    /// <summary>
    /// 효과음 재생 (AudioClip 직접 전달)
    /// 이미 클립 참조가 있을 때 사용
    /// </summary>
    /// <param name="clip">재생할 오디오 클립</param>
    /// <param name="volumeScale">볼륨 배율 (0~1)</param>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (isSFXMuted || clip == null) return;

        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    /// <summary>
    /// SFX 소스 풀에서 사용 가능한 AudioSource 찾기
    /// </summary>
    /// <returns>재생 중이 아닌 AudioSource (없으면 null)</returns>
    private AudioSource GetAvailableSFXSource()
    {
        foreach (AudioSource source in sfxSourcePool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }
        return null; // 모든 소스가 사용 중
    }

    // ==========================================================
    //  자주 사용하는 효과음 단축 메서드
    // ==========================================================

    /// <summary>
    /// 버튼 클릭 효과음 재생
    /// TopMenuManager, UIManager 등에서 호출
    /// </summary>
    public void PlayButtonClick()
    {
        PlaySFX("ButtonClick");
    }

    /// <summary>
    /// 장비 장착 효과음
    /// </summary>
    public void PlayEquipSound()
    {
        PlaySFX("Equip");
    }

    /// <summary>
    /// 강화 성공 효과음
    /// </summary>
    public void PlayEnhanceSuccess()
    {
        PlaySFX("EnhanceSuccess");
    }

    /// <summary>
    /// 강화 실패 효과음
    /// </summary>
    public void PlayEnhanceFail()
    {
        PlaySFX("EnhanceFail");
    }

    /// <summary>
    /// 아이템 구매 효과음
    /// </summary>
    public void PlayPurchaseSound()
    {
        PlaySFX("Purchase");
    }

    /// <summary>
    /// 가챠 뽑기 효과음
    /// </summary>
    public void PlayGachaSound()
    {
        PlaySFX("Gacha");
    }

    /// <summary>
    /// 레벨업 효과음
    /// </summary>
    public void PlayLevelUpSound()
    {
        PlaySFX("LevelUp");
    }

    /// <summary>
    /// 스킬 사용 효과음
    /// </summary>
    public void PlaySkillSound()
    {
        PlaySFX("SkillUse");
    }

    /// <summary>
    /// 몬스터 처치 효과음
    /// </summary>
    public void PlayMonsterDeathSound()
    {
        PlaySFX("MonsterDeath");
    }

    /// <summary>
    /// 패널 열기 효과음
    /// </summary>
    public void PlayPanelOpen()
    {
        PlaySFX("PanelOpen");
    }

    /// <summary>
    /// 패널 닫기 효과음
    /// </summary>
    public void PlayPanelClose()
    {
        PlaySFX("PanelClose");
    }

    /// <summary>
    /// 총알 발사 효과음
    /// </summary>
    public void PlayBulletFire()
    {
        PlaySFX("BulletFire");
    }

    /// <summary>
    /// 근거리 공격 효과음
    /// </summary>
    public void PlayMeleeAttack()
    {
        PlaySFX("MeleeAttack");
    }

    /// <summary>
    /// 마법 시전 효과음
    /// </summary>
    public void PlayMagicCast()
    {
        PlaySFX("MagicCast");
    }

    /// <summary>
    /// 몬스터 피격 효과음
    /// </summary>
    public void PlayMonsterHit()
    {
        PlaySFX("MonsterHit");
    }

    /// <summary>
    /// 로그인 성공 효과음
    /// </summary>
    public void PlayLoginSuccess()
    {
        PlaySFX("LoginSuccess");
    }

    /// <summary>
    /// 회원가입 효과음
    /// </summary>
    public void PlayRegister()
    {
        PlaySFX("Register");
    }

    /// <summary>
    /// 서버 입장 효과음
    /// </summary>
    public void PlayServerEnter()
    {
        PlaySFX("ServerEnter");
    }

    /// <summary>
    /// 캐릭터 선택 효과음
    /// </summary>
    public void PlayCharacterSelect()
    {
        PlaySFX("CharacterSelect");
    }

    /// <summary>
    /// 인게임 시작(게임 입장) 효과음
    /// </summary>
    public void PlayGameStart()
    {
        PlaySFX("GameStart");
    }



    // ==========================================================
    //  볼륨 조절
    // ==========================================================

    /// <summary>
    /// BGM 마스터 볼륨 설정 (0.0 ~ 1.0)
    /// 옵션 UI의 슬라이더에서 호출
    /// </summary>
    /// <param name="volume">볼륨 값 (0=무음, 1=최대)</param>
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume); // 0~1 사이로 제한

        // 현재 재생 중인 BGM에 즉시 반영
        if (!isBGMMuted && bgmSource != null)
        {
            // 현재 재생중인 클립의 고유 볼륨도 반영
            float clipVolume = 1f;
            if (!string.IsNullOrEmpty(currentBGMName) && bgmDict.ContainsKey(currentBGMName))
            {
                clipVolume = bgmDict[currentBGMName].volume;
            }
            bgmSource.volume = bgmVolume * clipVolume;
        }

        SaveSettings(); // 설정 저장
    }

    /// <summary>
    /// SFX 마스터 볼륨 설정 (0.0 ~ 1.0)
    /// </summary>
    /// <param name="volume">볼륨 값 (0=무음, 1=최대)</param>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        SaveSettings(); // 설정 저장
    }

    /// <summary>
    /// 현재 BGM 볼륨 가져오기 (옵션 UI 슬라이더 초기값 설정용)
    /// </summary>
    public float GetBGMVolume()
    {
        return bgmVolume;
    }

    /// <summary>
    /// 현재 SFX 볼륨 가져오기
    /// </summary>
    public float GetSFXVolume()
    {
        return sfxVolume;
    }

    // ==========================================================
    //  음소거 (Mute)
    // ==========================================================

    /// <summary>
    /// BGM 음소거 토글
    /// </summary>
    public void ToggleBGMMute()
    {
        isBGMMuted = !isBGMMuted;

        if (bgmSource != null)
        {
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume;
        }

        SaveSettings();
        Debug.Log($"[SoundManager] BGM 음소거: {isBGMMuted}");
    }

    /// <summary>
    /// SFX 음소거 토글
    /// </summary>
    public void ToggleSFXMute()
    {
        isSFXMuted = !isSFXMuted;
        SaveSettings();
        Debug.Log($"[SoundManager] SFX 음소거: {isSFXMuted}");
    }

    /// <summary>
    /// BGM 음소거 상태 가져오기
    /// </summary>
    public bool IsBGMMuted()
    {
        return isBGMMuted;
    }

    /// <summary>
    /// SFX 음소거 상태 가져오기
    /// </summary>
    public bool IsSFXMuted()
    {
        return isSFXMuted;
    }

    /// <summary>
    /// BGM 음소거 직접 설정
    /// </summary>
    public void SetBGMMute(bool mute)
    {
        isBGMMuted = mute;
        if (bgmSource != null)
        {
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume;
        }
        SaveSettings();
    }

    /// <summary>
    /// SFX 음소거 직접 설정
    /// </summary>
    public void SetSFXMute(bool mute)
    {
        isSFXMuted = mute;
        SaveSettings();
    }

    // ==========================================================
    //  설정 저장/로드 (PlayerPrefs)
    // ==========================================================

    /// <summary>
    /// 현재 사운드 설정을 PlayerPrefs에 저장
    /// 볼륨 값과 음소거 상태 저장
    /// </summary>
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(KEY_BGM_VOLUME, bgmVolume);
        PlayerPrefs.SetFloat(KEY_SFX_VOLUME, sfxVolume);
        PlayerPrefs.SetInt(KEY_BGM_MUTE, isBGMMuted ? 1 : 0);   // bool → int 변환
        PlayerPrefs.SetInt(KEY_SFX_MUTE, isSFXMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// PlayerPrefs에서 저장된 사운드 설정 불러오기
    /// 저장된 값이 없으면 기본값(볼륨 1.0, 음소거 꺼짐) 사용
    /// </summary>
    public void LoadSettings()
    {
        bgmVolume = PlayerPrefs.GetFloat(KEY_BGM_VOLUME, 1f);    // 기본값: 최대 볼륨
        sfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, 1f);
        isBGMMuted = PlayerPrefs.GetInt(KEY_BGM_MUTE, 0) == 1;  // 기본값: 음소거 꺼짐
        isSFXMuted = PlayerPrefs.GetInt(KEY_SFX_MUTE, 0) == 1;

        // BGM AudioSource에 볼륨 반영
        if (bgmSource != null)
        {
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume;
        }

        Debug.Log($"[SoundManager] 설정 로드 - BGM볼륨:{bgmVolume:F2}, SFX볼륨:{sfxVolume:F2}");
    }
    /// <summary>
    /// 가챠 뽑기 단일/다연 공통 효과음
    /// </summary>
    public void PlayGachaRoll()
    {
        PlaySFX("GachaRoll");
    }

    /// <summary>
    /// 조합(크래프팅) 성공 효과음
    /// </summary>
    public void PlayCraftSuccess()
    {
        PlaySFX("CraftSuccess");
    }

    /// <summary>
    /// 조합 실패 효과음
    /// </summary>
    public void PlayCraftFail()
    {
        PlaySFX("CraftFail");
    }

    /// <summary>
    /// 업적 보상 수령 효과음
    /// </summary>
    public void PlayAchievementReward()
    {
        PlaySFX("AchievementReward");
    }

    /// <summary>
    /// 퀘스트 보상 수령 효과음
    /// </summary>
    public void PlayQuestReward()
    {
        PlaySFX("QuestReward");
    }

    /// <summary>
    /// 우편 보상 수령 효과음
    /// </summary>
    public void PlayMailReward()
    {
        PlaySFX("MailReward");
    }

    /// <summary>
    /// 쿠폰 보상 수령 효과음
    /// </summary>
    public void PlayCouponReward()
    {
        PlaySFX("CouponReward");
    }

    /// <summary>
    /// 아이템 판매 효과음
    /// </summary>
    public void PlaySellItem()
    {
        PlaySFX("SellItem");
    }

    /// <summary>
    /// 경매장 입찰 효과음
    /// </summary>
    public void PlayAuctionBid()
    {
        PlaySFX("AuctionBid");
    }

    /// <summary>
    /// 경매장 즉시구매 효과음
    /// </summary>
    public void PlayAuctionBuyout()
    {
        PlaySFX("AuctionBuyout");
    }

    /// <summary>
    /// 경매장 출품 등록 효과음
    /// </summary>
    public void PlayAuctionRegister()
    {
        PlaySFX("AuctionRegister");
    }


    /// <summary>
    /// 장비 장착 효과음
    /// </summary>
    public void PlayEquip()
    {
        PlaySFX("Equip");
    }

    /// <summary>
    /// 스킬 습득/레벨업 효과음
    /// </summary>
    public void PlaySkillLearn()
    {
        PlaySFX("SkillLearn");
    }

    /// <summary>
    /// 오프라인 보상 수령 효과음
    /// </summary>
    public void PlayOfflineReward()
    {
        PlaySFX("OfflineReward");
    }


}