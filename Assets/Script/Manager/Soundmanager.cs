using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class SoundManager : MonoBehaviour
{

    public static SoundManager Instance { get; private set; }


    [System.Serializable]
    public class SoundClip
    {
        public string clipName;     // Ŭ���� ã�� �� ����� �̸� (��: "ButtonClick")
        public AudioClip clip;      // ���� ����� Ŭ�� ����
        [Range(0f, 1f)]
        public float volume = 1f;   // �� Ŭ�� ������ ���� (0~1)
    }

    [Header("===== BGM(�������) Ŭ�� ��� =====")]
    [Tooltip("����������� ����� ����� Ŭ������ ���⿡ ����ϼ���")]
    [SerializeField] private SoundClip[] bgmClips;

    [Header("===== SFX(ȿ����) Ŭ�� ��� =====")]
    [Tooltip("ȿ�������� ����� ����� Ŭ������ ���⿡ ����ϼ���")]
    [SerializeField] private SoundClip[] sfxClips;

    [Header("===== ����� �ҽ� (�ڵ� ������) =====")]
    [Tooltip("BGM ����� AudioSource (����θ� �ڵ� ����)")]
    [SerializeField] private AudioSource bgmSource;

    [Tooltip("SFX ����� AudioSource (����θ� �ڵ� ����)")]
    [SerializeField] private AudioSource sfxSource;

    [Header("===== �⺻ ���� =====")]
    [Tooltip("BGM ��ȯ �� ���̵� �ð� (��)")]
    [SerializeField] private float bgmFadeDuration = 1.0f;

    [Tooltip("SFX ���� ��� �ִ� ��")]
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

    // ★ Fix: 에디터와 빌드의 사운드 설정 분리
    // 에디터에서 SFX를 껐어도 빌드에는 영향 없음
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
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            InitializeSoundManager();
        }
        else
        {
            // ✅ 새 씬의 SoundManager에 오디오 클립 데이터가 있으면 기존 것에 덮어쓰기
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
        // ����� ���� �ʱ�ȭ (����׿�)
        PlayerPrefs.DeleteKey("SoundManager_BGMVolume");
        PlayerPrefs.DeleteKey("SoundManager_BGMMute");
        PlayerPrefs.Save();

        bgmVolume = 1f;
        isBGMMuted = false;

        PlayBGM("시작음");

        Debug.Log($"[SoundManager] BGM ��� �õ� - volume: {bgmSource.volume}, isPlaying: {bgmSource.isPlaying}, clip: {bgmSource.clip}");
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
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.priority = 0;


        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
    }


    public void BuildClipDictionaries()
    {
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

        Debug.Log($"[SoundManager] BGM {bgmDict.Count}��, SFX {sfxDict.Count}�� ��ϵ�");
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


    public void PlayBGM(string clipName, bool fadeIn = true)
    {
        // ���� BGM�� �̹� ��� ���̸� ����
        if (currentBGMName == clipName && bgmSource.isPlaying)
        {
            return;
        }

        // ��ųʸ����� Ŭ�� �˻�
        if (!bgmDict.ContainsKey(clipName))
        {
            Debug.LogWarning($"[SoundManager] BGM '{clipName}'��(��) ã�� �� �����ϴ�!");
            return;
        }

        SoundClip soundClip = bgmDict[clipName];

        // ���� ���̵� �ڷ�ƾ�� ������ ����
        if (bgmFadeCoroutine != null)
        {
            StopCoroutine(bgmFadeCoroutine);
        }

        if (fadeIn && bgmSource.isPlaying)
        {
            // ���� BGM �� �� BGM���� ũ�ν����̵�
            bgmFadeCoroutine = StartCoroutine(CrossFadeBGM(soundClip));
        }
        else
        {
            // ��� ���
            bgmSource.clip = soundClip.clip;
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume * soundClip.volume;
            bgmSource.Play();
        }

        currentBGMName = clipName;
        Debug.Log($"[SoundManager] BGM ���: {clipName}");
    }

    /// <summary>
    /// BGM ����
    /// </summary>
    /// <param name="fadeOut">���̵�ƿ� ��� ����</param>
    public void StopBGM(bool fadeOut = true)
    {
        if (!bgmSource.isPlaying) return;

        if (fadeOut)
        {
            // ���� ���̵� �ڷ�ƾ ���� �� ���̵�ƿ� ����
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
    /// BGM �Ͻ�����
    /// </summary>
    public void PauseBGM()
    {
        if (bgmSource.isPlaying)
        {
            bgmSource.Pause();
        }
    }

    /// <summary>
    /// BGM �Ͻ����� ���� (�̾ ���)
    /// </summary>
    public void ResumeBGM()
    {
        if (!bgmSource.isPlaying && bgmSource.clip != null)
        {
            bgmSource.UnPause();
        }
    }

    /// <summary>
    /// BGM ũ�ν����̵� (���� BGM ������ ���̰� �� �� BGM ������ Ű��)
    /// </summary>
    private IEnumerator CrossFadeBGM(SoundClip newClip)
    {
        float timer = 0f;
        float startVolume = bgmSource.volume;
        float halfDuration = bgmFadeDuration / 2f;

        // �� ���̵�ƿ� (���� BGM ���� ���̱�)
        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, timer / halfDuration);
            yield return null; // ���� �����ӱ��� ���
        }

        // �� Ŭ�� ��ü �� ���
        bgmSource.Stop();
        bgmSource.clip = newClip.clip;
        bgmSource.Play();

        // �� ���̵��� (�� BGM ���� Ű���)
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
    /// BGM ���̵�ƿ� (������ ���� �ٿ��� ����)
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
    //  SFX (ȿ����) ���� �޼���
    // ==========================================================

    /// <summary>
    /// ȿ���� ��� (�̸����� ã��)
    /// </summary>
    /// <param name="clipName">����� ȿ���� �̸� (sfxClips���� ����� �̸�)</param>
    public void PlaySFX(string clipName)
    {
        // ���Ұ� ���¸� ������� ����
        if (isSFXMuted) return;

        // ��ųʸ����� Ŭ�� �˻�
        if (!sfxDict.ContainsKey(clipName))
        {
            Debug.LogWarning($"[SoundManager] SFX '{clipName}'��(��) ã�� �� �����ϴ�!");
            return;
        }

        SoundClip soundClip = sfxDict[clipName];

        // ��� ������(��� ���� �ƴ�) AudioSource ã��
        AudioSource availableSource = GetAvailableSFXSource();
        if (availableSource != null)
        {
            availableSource.clip = soundClip.clip;
            availableSource.volume = sfxVolume * soundClip.volume;
            availableSource.Play();
        }
        else
        {
            // Ǯ�� ������ ������ �⺻ sfxSource�� OneShot ���
            sfxSource.PlayOneShot(soundClip.clip, sfxVolume * soundClip.volume);
        }
    }

    /// <summary>
    /// ȿ���� ��� (AudioClip ���� ����)
    /// �̹� Ŭ�� ������ ���� �� ���
    /// </summary>
    /// <param name="clip">����� ����� Ŭ��</param>
    /// <param name="volumeScale">���� ���� (0~1)</param>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (isSFXMuted || clip == null) return;

        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    /// <summary>
    /// SFX �ҽ� Ǯ���� ��� ������ AudioSource ã��
    /// </summary>
    /// <returns>��� ���� �ƴ� AudioSource (������ null)</returns>
    private AudioSource GetAvailableSFXSource()
    {
        foreach (AudioSource source in sfxSourcePool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }
        return null; // ��� �ҽ��� ��� ��
    }

    // ==========================================================
    //  ���� ����ϴ� ȿ���� ���� �޼���
    // ==========================================================

    /// <summary>
    /// ��ư Ŭ�� ȿ���� ���
    /// TopMenuManager, UIManager ��� ȣ��
    /// </summary>
    public void PlayButtonClick()
    {
        PlaySFX("ButtonClick");
    }

    /// <summary>
    /// ��� ���� ȿ����
    /// </summary>
    public void PlayEquipSound()
    {
        PlaySFX("Equip");
    }

    /// <summary>
    /// ��ȭ ���� ȿ����
    /// </summary>
    public void PlayEnhanceSuccess()
    {
        PlaySFX("EnhanceSuccess");
    }

    /// <summary>
    /// ��ȭ ���� ȿ����
    /// </summary>
    public void PlayEnhanceFail()
    {
        PlaySFX("EnhanceFail");
    }

    /// <summary>
    /// ������ ���� ȿ����
    /// </summary>
    public void PlayPurchaseSound()
    {
        PlaySFX("Purchase");
    }

    /// <summary>
    /// ��í �̱� ȿ����
    /// </summary>
    public void PlayGachaSound()
    {
        PlaySFX("Gacha");
    }

    /// <summary>
    /// ������ ȿ����
    /// </summary>
    public void PlayLevelUpSound()
    {
        PlaySFX("LevelUp");
    }

    /// <summary>
    /// ��ų ��� ȿ����
    /// </summary>
    public void PlaySkillSound()
    {
        PlaySFX("SkillUse");
    }

    /// <summary>
    /// ���� óġ ȿ����
    /// </summary>
    public void PlayMonsterDeathSound()
    {
        PlaySFX("MonsterDeath");
    }

    /// <summary>
    /// �г� ���� ȿ����
    /// </summary>
    public void PlayPanelOpen()
    {
        PlaySFX("PanelOpen");
    }

    /// <summary>
    /// �г� �ݱ� ȿ����
    /// </summary>
    public void PlayPanelClose()
    {
        PlaySFX("PanelClose");
    }

    /// <summary>
    /// �Ѿ� �߻� ȿ����
    /// </summary>
    public void PlayBulletFire()
    {
        PlaySFX("BulletFire");
    }

    /// <summary>
    /// �ٰŸ� ���� ȿ����
    /// </summary>
    public void PlayMeleeAttack()
    {
        PlaySFX("MeleeAttack");
    }

    /// <summary>
    /// ���� ���� ȿ����
    /// </summary>
    public void PlayMagicCast()
    {
        PlaySFX("MagicCast");
    }

    /// <summary>
    /// ���� �ǰ� ȿ����
    /// </summary>
    public void PlayMonsterHit()
    {
        PlaySFX("MonsterHit");
    }

    /// <summary>
    /// �α��� ���� ȿ����
    /// </summary>
    public void PlayLoginSuccess()
    {
        PlaySFX("LoginSuccess");
    }

    /// <summary>
    /// ȸ������ ȿ����
    /// </summary>
    public void PlayRegister()
    {
        PlaySFX("Register");
    }

    /// <summary>
    /// ���� ���� ȿ����
    /// </summary>
    public void PlayServerEnter()
    {
        PlaySFX("ServerEnter");
    }

    /// <summary>
    /// ĳ���� ���� ȿ����
    /// </summary>
    public void PlayCharacterSelect()
    {
        PlaySFX("CharacterSelect");
    }

    /// <summary>
    /// �ΰ��� ����(���� ����) ȿ����
    /// </summary>
    public void PlayGameStart()
    {
        PlaySFX("GameStart");
    }



    // ==========================================================
    //  ���� ����
    // ==========================================================

    /// <summary>
    /// BGM ������ ���� ���� (0.0 ~ 1.0)
    /// �ɼ� UI�� �����̴����� ȣ��
    /// </summary>
    /// <param name="volume">���� �� (0=����, 1=�ִ�)</param>
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume); // 0~1 ���̷� ����

        // ���� ��� ���� BGM�� ��� �ݿ�
        if (!isBGMMuted && bgmSource != null)
        {
            // ���� ������� Ŭ���� ���� ������ �ݿ�
            float clipVolume = 1f;
            if (!string.IsNullOrEmpty(currentBGMName) && bgmDict.ContainsKey(currentBGMName))
            {
                clipVolume = bgmDict[currentBGMName].volume;
            }
            bgmSource.volume = bgmVolume * clipVolume;
        }

        SaveSettings(); // ���� ����
    }

    /// <summary>
    /// SFX ������ ���� ���� (0.0 ~ 1.0)
    /// </summary>
    /// <param name="volume">���� �� (0=����, 1=�ִ�)</param>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        SaveSettings(); // ���� ����
    }

    /// <summary>
    /// ���� BGM ���� �������� (�ɼ� UI �����̴� �ʱⰪ ������)
    /// </summary>
    public float GetBGMVolume()
    {
        return bgmVolume;
    }

    /// <summary>
    /// ���� SFX ���� ��������
    /// </summary>
    public float GetSFXVolume()
    {
        return sfxVolume;
    }

    // ==========================================================
    //  ���Ұ� (Mute)
    // ==========================================================

    /// <summary>
    /// BGM ���Ұ� ���
    /// </summary>
    public void ToggleBGMMute()
    {
        isBGMMuted = !isBGMMuted;

        if (bgmSource != null)
        {
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume;
        }

        SaveSettings();
        Debug.Log($"[SoundManager] BGM ���Ұ�: {isBGMMuted}");
    }

    /// <summary>
    /// SFX ���Ұ� ���
    /// </summary>
    public void ToggleSFXMute()
    {
        isSFXMuted = !isSFXMuted;
        SaveSettings();
        Debug.Log($"[SoundManager] SFX ���Ұ�: {isSFXMuted}");
    }

    /// <summary>
    /// BGM ���Ұ� ���� ��������
    /// </summary>
    public bool IsBGMMuted()
    {
        return isBGMMuted;
    }

    /// <summary>
    /// SFX ���Ұ� ���� ��������
    /// </summary>
    public bool IsSFXMuted()
    {
        return isSFXMuted;
    }

    /// <summary>
    /// BGM ���Ұ� ���� ����
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
    /// SFX ���Ұ� ���� ����
    /// </summary>
    public void SetSFXMute(bool mute)
    {
        isSFXMuted = mute;
        SaveSettings();
    }

    // ==========================================================
    //  ���� ����/�ε� (PlayerPrefs)
    // ==========================================================

    /// <summary>
    /// ���� ���� ������ PlayerPrefs�� ����
    /// ���� ���� ���Ұ� ���� ����
    /// </summary>
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(KEY_BGM_VOLUME, bgmVolume);
        PlayerPrefs.SetFloat(KEY_SFX_VOLUME, sfxVolume);
        PlayerPrefs.SetInt(KEY_BGM_MUTE, isBGMMuted ? 1 : 0);   // bool �� int ��ȯ
        PlayerPrefs.SetInt(KEY_SFX_MUTE, isSFXMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// PlayerPrefs���� ����� ���� ���� �ҷ�����
    /// ����� ���� ������ �⺻��(���� 1.0, ���Ұ� ����) ���
    /// </summary>
    public void LoadSettings()
    {
        bgmVolume = PlayerPrefs.GetFloat(KEY_BGM_VOLUME, 1f);    // �⺻��: �ִ� ����
        sfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, 1f);
        isBGMMuted = PlayerPrefs.GetInt(KEY_BGM_MUTE, 0) == 1;  // �⺻��: ���Ұ� ����
        isSFXMuted = PlayerPrefs.GetInt(KEY_SFX_MUTE, 0) == 1;

        // BGM AudioSource�� ���� �ݿ�
        if (bgmSource != null)
        {
            bgmSource.volume = isBGMMuted ? 0f : bgmVolume;
        }

        Debug.Log($"[SoundManager] ���� �ε� - BGM����:{bgmVolume:F2}, SFX����:{sfxVolume:F2}");
    }
    /// <summary>
    /// ��í �̱� ����/�ٿ� ���� ȿ����
    /// </summary>
    public void PlayGachaRoll()
    {
        PlaySFX("GachaRoll");
    }

    /// <summary>
    /// ����(ũ������) ���� ȿ����
    /// </summary>
    public void PlayCraftSuccess()
    {
        PlaySFX("CraftSuccess");
    }

    /// <summary>
    /// ���� ���� ȿ����
    /// </summary>
    public void PlayCraftFail()
    {
        PlaySFX("CraftFail");
    }

    /// <summary>
    /// ���� ���� ���� ȿ����
    /// </summary>
    public void PlayAchievementReward()
    {
        PlaySFX("AchievementReward");
    }

    /// <summary>
    /// ����Ʈ ���� ���� ȿ����
    /// </summary>
    public void PlayQuestReward()
    {
        PlaySFX("QuestReward");
    }

    /// <summary>
    /// ���� ���� ���� ȿ����
    /// </summary>
    public void PlayMailReward()
    {
        PlaySFX("MailReward");
    }

    /// <summary>
    /// ���� ���� ���� ȿ����
    /// </summary>
    public void PlayCouponReward()
    {
        PlaySFX("CouponReward");
    }

    /// <summary>
    /// ������ �Ǹ� ȿ����
    /// </summary>
    public void PlaySellItem()
    {
        PlaySFX("SellItem");
    }

    /// <summary>
    /// ����� ���� ȿ����
    /// </summary>
    public void PlayAuctionBid()
    {
        PlaySFX("AuctionBid");
    }

    /// <summary>
    /// ����� ��ñ��� ȿ����
    /// </summary>
    public void PlayAuctionBuyout()
    {
        PlaySFX("AuctionBuyout");
    }

    /// <summary>
    /// ����� ��ǰ ��� ȿ����
    /// </summary>
    public void PlayAuctionRegister()
    {
        PlaySFX("AuctionRegister");
    }


    /// <summary>
    /// ��� ���� ȿ����
    /// </summary>
    public void PlayEquip()
    {
        PlaySFX("Equip");
    }

    /// <summary>
    /// ��ų ����/������ ȿ����
    /// </summary>
    public void PlaySkillLearn()
    {
        PlaySFX("SkillLearn");
    }


    public void PlayOfflineReward()
    {
        PlaySFX("OfflineReward");
    }


}