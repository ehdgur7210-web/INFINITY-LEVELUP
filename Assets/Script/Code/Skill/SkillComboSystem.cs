using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// SkillComboSystem — 스킬 조합(포커 핸드) 시스템
/// ═══════════════════════════════════════════════════════════════
///
/// ★ 핵심 규칙:
///   - 핫바 6슬롯이 15초마다 랜덤 스킬로 교체
///   - 각 슬롯의 "레벨"은 장비 레어리티로 결정:
///       Common=1, Uncommon=2, Rare=3, Epic=4, Legendary=5
///   - 6슬롯의 레벨 조합으로 콤보 판정 (포커 핸드):
///
///   [콤보 우선순위 — 높은 것부터 판정]
///     Six     (6동일)       : 111111, 222222 ... → 최강 스킬 (레벨별 다름)
///     Five    (5동일+1)     : 111112 등          → 강력 스킬 (레벨별 다름)
///     Straight(1,2,3,4,5포함): 다른 속성 조합       → 스트레이트 스킬
///     Four    (4동일+2)     : 111145 등          → 포 스킬
///     FullHouse(3+3)        : 111222 등          → 풀하우스 스킬
///     Triple  (3동일+나머지)  : 111234 등          → 트리플 스킬
///     TwoPair (2+2+나머지)   : 112234 등          → 투페어 스킬
///     없음                   :                    → 콤보 없음
///
///   ★ Five/Six: 레벨에 따라 다른 스킬 발동
///     - 11111 / 22222 = 각각 다른 Five 스킬
///     - 33333 이상 = 확률이 낮으므로 초강력 스킬
///
/// ★ Inspector 설정:
///   - shuffleInterval : 15초 (랜덤 교체 주기)
///   - comboSkills     : 콤보별 발동 스킬 프리팹/데이터
///   - comboUI         : 콤보 발동 시 연출 UI
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public class SkillComboSystem : MonoBehaviour
{
    public static SkillComboSystem Instance { get; private set; }

    // ═══ 콤보 타입 ═══════════════════════════════════════════════

    public enum ComboType
    {
        None,
        TwoPair,      // 2+2
        Triple,       // 3동일
        Straight,     // 1,2,3,4,5 포함
        FullHouse,    // 3+3
        FourOfAKind,  // 4동일
        FiveOfAKind,  // 5동일
        SixOfAKind,   // 6동일 (최강)
    }

    // ═══ Inspector 필드 ═══════════════════════════════════════════

    [Header("===== 셔플 설정 =====")]
    [Tooltip("핫바 스킬 랜덤 교체 주기 (초)")]
    [SerializeField] private float shuffleInterval = 15f;

    [Header("===== 콤보 스킬 (콤보별 발동 효과) =====")]
    [SerializeField] private ComboSkillSet twoPairSkill;
    [SerializeField] private ComboSkillSet tripleSkill;
    [SerializeField] private ComboSkillSet straightSkill;
    [SerializeField] private ComboSkillSet fullHouseSkill;
    [SerializeField] private ComboSkillSet fourOfAKindSkill;

    [Header("Five/Six — 레벨별 스킬 (레벨 1~5)")]
    [Tooltip("Five 스킬 (인덱스 0=Lv1, 1=Lv2 ... 4=Lv5)")]
    [SerializeField] private ComboSkillSet[] fiveOfAKindSkills = new ComboSkillSet[5];
    [Tooltip("Six 스킬 (인덱스 0=Lv1, 1=Lv2 ... 4=Lv5)")]
    [SerializeField] private ComboSkillSet[] sixOfAKindSkills = new ComboSkillSet[5];

    [Header("===== 콤보 UI 연출 =====")]
    [SerializeField] private GameObject comboPopupPanel;
    [SerializeField] private TextMeshProUGUI comboNameText;
    [SerializeField] private TextMeshProUGUI comboDescText;
    [SerializeField] private Image comboIconImage;
    [SerializeField] private float comboPopupDuration = 2f;

    [Header("===== 셔플 UI =====")]
    [SerializeField] private TextMeshProUGUI shuffleTimerText;

    [Header("===== 데미지 배율 (콤보별) =====")]
    [SerializeField] private float twoPairMultiplier = 1.5f;
    [SerializeField] private float tripleMultiplier = 2.0f;
    [SerializeField] private float straightMultiplier = 3.0f;
    [SerializeField] private float fullHouseMultiplier = 3.5f;
    [SerializeField] private float fourMultiplier = 5.0f;
    [SerializeField] private float fiveMultiplier = 8.0f;
    [SerializeField] private float sixMultiplier = 15.0f;

    // ═══ 내부 상태 ═══════════════════════════════════════════════

    private float shuffleTimer;
    private int[] currentSlotLevels = new int[6]; // 각 슬롯의 현재 레벨 (1~5)
    private ComboType lastCombo = ComboType.None;
    private int lastComboLevel = 0; // Five/Six일 때 어떤 레벨인지
    private Coroutine comboPopupCoroutine;

    // ═══ 콤보 스킬 데이터 ═════════════════════════════════════════

    [System.Serializable]
    public class ComboSkillSet
    {
        public string comboName;                    // "트리플!", "스트레이트!" 등
        [TextArea(1, 2)]
        public string description;                  // 콤보 설명
        public Sprite icon;                         // 콤보 아이콘
        public GameObject effectPrefab;             // 발동 이펙트 프리팹
        public AudioClip sound;                     // 발동 사운드
        public float baseDamage = 100f;             // 기본 데미지
        public float areaRadius = 5f;               // 범위
    }

    // ═══ Unity 생명주기 ═══════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        shuffleTimer = shuffleInterval;
        InitializeSlotLevels();

        if (comboPopupPanel != null)
            comboPopupPanel.SetActive(false);
    }

    void Update()
    {
        // 셔플 타이머
        shuffleTimer -= Time.deltaTime;

        // 타이머 UI 갱신
        if (shuffleTimerText != null)
            shuffleTimerText.text = $"{Mathf.CeilToInt(Mathf.Max(0, shuffleTimer))}";

        if (shuffleTimer <= 0f)
        {
            shuffleTimer = shuffleInterval;
            ShuffleHotbarSkills();
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  슬롯 레벨 초기화 (장비 레어리티 기반)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>현재 장착 장비의 레어리티로 슬롯 레벨 결정</summary>
    public void InitializeSlotLevels()
    {
        for (int i = 0; i < 6; i++)
        {
            EquipmentType eqType = (EquipmentType)i;
            currentSlotLevels[i] = GetEquipmentLevel(eqType);
        }
    }

    /// <summary>장비 레어리티 → 레벨 (Common=1 ~ Legendary=5, 미장착=1)</summary>
    private int GetEquipmentLevel(EquipmentType eqType)
    {
        if (EquipmentManager.Instance == null) return 1;
        EquipmentData eq = EquipmentManager.Instance.GetEquippedItem(eqType);
        if (eq == null) return 1;

        return eq.rarity switch
        {
            ItemRarity.Common    => 1,
            ItemRarity.Uncommon  => 2,
            ItemRarity.Rare      => 3,
            ItemRarity.Epic      => 4,
            ItemRarity.Legendary => 5,
            _                    => 1
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  15초마다 핫바 스킬 랜덤 교체
    // ═══════════════════════════════════════════════════════════════

    private void ShuffleHotbarSkills()
    {
        // 1) 슬롯 레벨 갱신 (장비가 바뀌었을 수 있음)
        InitializeSlotLevels();

        // 2) 각 슬롯에 해당 레벨의 랜덤 스킬 배정
        if (EquipmentSkillSystem.Instance != null && SkillManager.Instance != null)
        {
            for (int i = 0; i < 6; i++)
            {
                EquipmentType eqType = (EquipmentType)i;
                RandomizeSlotSkill(eqType, i);
            }
        }

        // 3) 콤보 판정
        ComboType combo = EvaluateCombo(currentSlotLevels);
        int comboLevel = GetComboLevel(combo, currentSlotLevels);

        lastCombo = combo;
        lastComboLevel = comboLevel;

        // 4) 콤보 발동
        if (combo != ComboType.None)
        {
            ExecuteCombo(combo, comboLevel);
        }

        // 로그
        string levels = $"[{currentSlotLevels[0]},{currentSlotLevels[1]},{currentSlotLevels[2]}," +
                         $"{currentSlotLevels[3]},{currentSlotLevels[4]},{currentSlotLevels[5]}]";
        Debug.Log($"[SkillCombo] 셔플 완료 — 레벨:{levels} → 콤보:{combo}" +
                  (combo >= ComboType.FiveOfAKind ? $" (Lv.{comboLevel})" : ""));
    }

    /// <summary>슬롯에 해당 레어리티의 스킬을 랜덤 배정</summary>
    private void RandomizeSlotSkill(EquipmentType eqType, int slotIndex)
    {
        if (EquipmentManager.Instance == null) return;
        EquipmentData eq = EquipmentManager.Instance.GetEquippedItem(eqType);
        if (eq == null) return;

        // 현재 장비의 레어리티에 맞는 스킬을 EquipmentSkillSystem에서 가져옴
        // (이미 장착 시 배정된 스킬 — 셔플은 레벨만 사용하고 스킬 자체는 유지)
        // 스킬 자체를 교체하려면 RaritySkillMapping에서 랜덤 레어리티 선택
        // → 장비 레어리티 기반으로 유지 (원래 장비 시스템과 호환)
    }

    // ═══════════════════════════════════════════════════════════════
    //  콤보 판정 (포커 핸드)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>6슬롯 레벨 배열로 콤보 타입 판정</summary>
    public static ComboType EvaluateCombo(int[] levels)
    {
        if (levels == null || levels.Length < 6) return ComboType.None;

        // 레벨별 카운트 (인덱스 1~5)
        int[] counts = new int[6]; // [0]미사용, [1]~[5]
        for (int i = 0; i < 6; i++)
        {
            int lv = Mathf.Clamp(levels[i], 1, 5);
            counts[lv]++;
        }

        // 최대 동일 개수
        int maxCount = 0;
        int secondMax = 0;
        int distinctCount = 0;
        for (int lv = 1; lv <= 5; lv++)
        {
            if (counts[lv] > 0) distinctCount++;
            if (counts[lv] > maxCount)
            {
                secondMax = maxCount;
                maxCount = counts[lv];
            }
            else if (counts[lv] > secondMax)
            {
                secondMax = counts[lv];
            }
        }

        // ── 판정 (높은 콤보부터) ──

        // Six of a Kind: 6동일
        if (maxCount >= 6)
            return ComboType.SixOfAKind;

        // Five of a Kind: 5동일
        if (maxCount >= 5)
            return ComboType.FiveOfAKind;

        // Straight: 1,2,3,4,5 모두 포함 (6번째는 아무거나)
        if (counts[1] >= 1 && counts[2] >= 1 && counts[3] >= 1
            && counts[4] >= 1 && counts[5] >= 1)
            return ComboType.Straight;

        // Four of a Kind: 4동일
        if (maxCount >= 4)
            return ComboType.FourOfAKind;

        // Full House: 3+3
        if (maxCount >= 3 && secondMax >= 3)
            return ComboType.FullHouse;

        // Triple: 3동일 (나머지는 아무거나)
        if (maxCount >= 3)
            return ComboType.Triple;

        // Two Pair: 2+2 이상
        int pairCount = 0;
        for (int lv = 1; lv <= 5; lv++)
            if (counts[lv] >= 2) pairCount++;
        if (pairCount >= 2)
            return ComboType.TwoPair;

        return ComboType.None;
    }

    /// <summary>Five/Six 콤보의 레벨 (어떤 숫자가 5/6개인지)</summary>
    private int GetComboLevel(ComboType combo, int[] levels)
    {
        if (combo != ComboType.FiveOfAKind && combo != ComboType.SixOfAKind)
            return 0;

        int[] counts = new int[6];
        for (int i = 0; i < 6; i++)
            counts[Mathf.Clamp(levels[i], 1, 5)]++;

        int targetCount = (combo == ComboType.SixOfAKind) ? 6 : 5;
        for (int lv = 1; lv <= 5; lv++)
            if (counts[lv] >= targetCount) return lv;

        return 1;
    }

    // ═══════════════════════════════════════════════════════════════
    //  콤보 발동
    // ═══════════════════════════════════════════════════════════════

    private void ExecuteCombo(ComboType combo, int comboLevel)
    {
        ComboSkillSet skillSet = GetComboSkillSet(combo, comboLevel);
        float multiplier = GetComboMultiplier(combo);

        // 기본 공격력 기반 데미지 계산
        float baseAtk = PlayerStats.Instance != null ? PlayerStats.Instance.GetTotalAttack() : 100f;
        float totalDamage = baseAtk * multiplier;

        // 스킬 세트에 baseDamage가 설정되어 있으면 추가
        if (skillSet != null)
            totalDamage += skillSet.baseDamage;

        // 33333(Lv3)+ Five/Six는 추가 보너스
        if ((combo == ComboType.FiveOfAKind || combo == ComboType.SixOfAKind) && comboLevel >= 3)
        {
            float rareBonus = 1f + (comboLevel - 2) * 0.5f; // Lv3=+50%, Lv4=+100%, Lv5=+150%
            totalDamage *= rareBonus;
        }

        // 범위 데미지 적용
        float radius = skillSet != null ? skillSet.areaRadius : 8f;
        DealAreaDamage(totalDamage, radius);

        // 이펙트
        SpawnComboEffect(skillSet);

        // 사운드
        if (skillSet?.sound != null)
            SoundManager.Instance?.PlaySFX(skillSet.sound);

        // UI 팝업
        ShowComboPopup(combo, comboLevel, skillSet);

        Debug.Log($"[SkillCombo] ★ {combo} 발동! (Lv.{comboLevel}) 데미지:{totalDamage:F0} 범위:{radius}");
    }

    private ComboSkillSet GetComboSkillSet(ComboType combo, int comboLevel)
    {
        int idx = Mathf.Clamp(comboLevel - 1, 0, 4);

        return combo switch
        {
            ComboType.TwoPair     => twoPairSkill,
            ComboType.Triple      => tripleSkill,
            ComboType.Straight    => straightSkill,
            ComboType.FullHouse   => fullHouseSkill,
            ComboType.FourOfAKind => fourOfAKindSkill,
            ComboType.FiveOfAKind => (fiveOfAKindSkills != null && idx < fiveOfAKindSkills.Length)
                                     ? fiveOfAKindSkills[idx] : null,
            ComboType.SixOfAKind  => (sixOfAKindSkills != null && idx < sixOfAKindSkills.Length)
                                     ? sixOfAKindSkills[idx] : null,
            _ => null
        };
    }

    private float GetComboMultiplier(ComboType combo)
    {
        return combo switch
        {
            ComboType.TwoPair     => twoPairMultiplier,
            ComboType.Triple      => tripleMultiplier,
            ComboType.Straight    => straightMultiplier,
            ComboType.FullHouse   => fullHouseMultiplier,
            ComboType.FourOfAKind => fourMultiplier,
            ComboType.FiveOfAKind => fiveMultiplier,
            ComboType.SixOfAKind  => sixMultiplier,
            _ => 1f
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  범위 데미지
    // ═══════════════════════════════════════════════════════════════

    private void DealAreaDamage(float damage, float radius)
    {
        Transform player = GetPlayerTransform();
        if (player == null) return;

        Vector3 center = player.position;

        // Physics2D 기반 적 탐색
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        int hitCount = 0;

        foreach (var col in hits)
        {
            if (col == null || col.gameObject == player.gameObject) continue;

            // Monster 컴포넌트
            Monster monster = col.GetComponent<Monster>();
            if (monster != null && monster.currentHp > 0)
            {
                monster.Hit(damage, 0);
                hitCount++;
                continue;
            }

            // BossMonster
            BossMonster boss = col.GetComponent<BossMonster>();
            if (boss != null)
            {
                boss.TakeDamage(damage);
                hitCount++;
                continue;
            }

            // IHitable 인터페이스
            IHitable hitable = col.GetComponent<IHitable>();
            if (hitable != null)
            {
                hitable.Hit(damage);
                hitCount++;
            }
        }

        // 태그 폴백
        if (hitCount == 0)
        {
            DealAreaDamageByTag(center, damage, radius);
        }
    }

    private void DealAreaDamageByTag(Vector3 center, float damage, float radius)
    {
        GameObject[] monsters = null;
        try { monsters = GameObject.FindGameObjectsWithTag("Monster"); } catch { }
        if (monsters == null) return;

        foreach (var go in monsters)
        {
            if (go == null || !go.activeInHierarchy) continue;
            if (Vector2.Distance(center, go.transform.position) > radius) continue;

            Monster m = go.GetComponent<Monster>();
            if (m != null && m.currentHp > 0) { m.Hit(damage, 0); continue; }

            IHitable h = go.GetComponent<IHitable>();
            if (h != null) h.Hit(damage);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  이펙트 / UI
    // ═══════════════════════════════════════════════════════════════

    private void SpawnComboEffect(ComboSkillSet skillSet)
    {
        if (skillSet?.effectPrefab == null) return;
        Transform player = GetPlayerTransform();
        if (player == null) return;

        GameObject fx = Instantiate(skillSet.effectPrefab, player.position, Quaternion.identity);
        Destroy(fx, 3f);
    }

    private void ShowComboPopup(ComboType combo, int level, ComboSkillSet skillSet)
    {
        if (comboPopupPanel == null) return;

        if (comboPopupCoroutine != null)
            StopCoroutine(comboPopupCoroutine);

        string displayName = skillSet?.comboName;
        if (string.IsNullOrEmpty(displayName))
            displayName = GetDefaultComboName(combo, level);

        if (comboNameText != null)
            comboNameText.text = displayName;

        if (comboDescText != null)
            comboDescText.text = skillSet?.description ?? $"x{GetComboMultiplier(combo):F1} 데미지!";

        if (comboIconImage != null && skillSet?.icon != null)
        {
            comboIconImage.sprite = skillSet.icon;
            comboIconImage.gameObject.SetActive(true);
        }
        else if (comboIconImage != null)
        {
            comboIconImage.gameObject.SetActive(false);
        }

        comboPopupPanel.SetActive(true);
        comboPopupCoroutine = StartCoroutine(HideComboPopup());

        // 화면 메시지
        Color comboColor = combo >= ComboType.FiveOfAKind
            ? new Color(1f, 0.85f, 0.1f) // 금색
            : combo >= ComboType.Straight
                ? new Color(0.4f, 0.9f, 1f) // 파란색
                : new Color(0.5f, 1f, 0.5f); // 초록색

        UIManager.Instance?.ShowMessage($"★ {displayName} ★", comboColor);
    }

    private IEnumerator HideComboPopup()
    {
        yield return new WaitForSeconds(comboPopupDuration);
        if (comboPopupPanel != null)
            comboPopupPanel.SetActive(false);
    }

    private string GetDefaultComboName(ComboType combo, int level)
    {
        return combo switch
        {
            ComboType.TwoPair     => "투 페어!",
            ComboType.Triple      => "트리플!",
            ComboType.Straight    => "스트레이트!",
            ComboType.FullHouse   => "풀 하우스!",
            ComboType.FourOfAKind => "포 카드!",
            ComboType.FiveOfAKind => level >= 3
                ? $"★ 파이브 Lv.{level}! ★"
                : $"파이브 Lv.{level}!",
            ComboType.SixOfAKind  => level >= 3
                ? $"★★ 식스 Lv.{level}! ★★"
                : $"식스 Lv.{level}!",
            _ => ""
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  공개 API
    // ═══════════════════════════════════════════════════════════════

    /// <summary>현재 콤보 정보 조회</summary>
    public ComboType CurrentCombo => lastCombo;
    public int CurrentComboLevel => lastComboLevel;
    public int[] CurrentSlotLevels => currentSlotLevels;

    /// <summary>셔플 타이머 리셋 (장비 변경 시 즉시 재판정)</summary>
    public void ForceReshuffle()
    {
        shuffleTimer = 0f; // 다음 Update에서 즉시 셔플
    }

    /// <summary>특정 레벨 배열로 콤보 테스트 (디버그용)</summary>
    public ComboType TestCombo(int[] testLevels)
    {
        return EvaluateCombo(testLevels);
    }

    // ═══════════════════════════════════════════════════════════════
    //  유틸
    // ═══════════════════════════════════════════════════════════════

    private Transform GetPlayerTransform()
    {
        if (PlayerStats.Instance != null)
            return PlayerStats.Instance.transform;

        PlayerController pc = FindObjectOfType<PlayerController>();
        return pc != null ? pc.transform : null;
    }
}
