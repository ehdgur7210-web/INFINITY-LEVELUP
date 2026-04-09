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
    [SerializeField] private float comboPopupDuration = 1.5f;
    [Tooltip("comboPopupPanel이 비어있으면 런타임에 자동 생성")]
    [SerializeField] private bool autoCreatePopupIfMissing = true;

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
        if (Instance != null && Instance != this && Instance.gameObject.scene.isLoaded) { Destroy(gameObject); return; }
        Instance = this;
        Debug.Log("[ManagerInit] SkillComboSystem가 생성되었습니다.");
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

    /// <summary>장비 레어리티 → 레벨 (Common=1 ~ Legendary=5, 미장착=0)</summary>
    private int GetEquipmentLevel(EquipmentType eqType)
    {
        if (EquipmentManager.Instance == null) return 0;
        EquipmentData eq = EquipmentManager.Instance.GetEquippedItem(eqType);
        if (eq == null) return 0; // ★ 미장착 = 0 (콤보 카운트에서 제외)

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

    [Header("===== 강화/레벨 → 스킬 등급 확률 =====")]
    [Tooltip("강화 1당 진행도 (%) — 기본 3이면 강화 20강 = +60% 진행도")]
    [SerializeField] private float enhanceBoostPerLevel = 3f;

    [Tooltip("캐릭터 레벨 N당 +1% 진행도 — 기본 5면 레벨 100 = +20% 진행도")]
    [SerializeField] private float playerLevelPer1Percent = 5f;

    [Tooltip("기본 가중치 (Common, Uncommon, Rare, Epic, Legendary) — 상위 등급일수록 가파르게 낮춤")]
    [SerializeField] private float[] baseRarityWeights = { 1000f, 250f, 60f, 12f, 1.5f };

    [Tooltip("진행도가 가중치에 영향을 주는 강도 (1.0 = 표준)")]
    [SerializeField] private float progressInfluence = 1.0f;

    /// <summary>슬롯에 랜덤 등급의 스킬을 배정 (강화+레벨 → 상위 확률 증가, 하위 확률 감소)</summary>
    private void RandomizeSlotSkill(EquipmentType eqType, int slotIndex)
    {
        if (EquipmentManager.Instance == null || EquipmentSkillSystem.Instance == null) return;
        EquipmentData eq = EquipmentManager.Instance.GetEquippedItem(eqType);
        if (eq == null) return;

        RaritySkillMapping mapping = EquipmentSkillSystem.Instance.GetSkillMappingForSlot(eqType);
        if (mapping == null) return;

        int maxRarity = (int)eq.rarity;
        int enhLevel = EquipmentManager.Instance.GetEnhanceLevel(eqType);
        int playerLv = GameManager.Instance != null ? GameManager.Instance.PlayerLevel : 1;

        // ★ 진행도 = 강화 보너스 + 레벨 보너스
        // 예) 강화 20강 + 레벨 110 = 60 + 22 = 82% 진행도
        float enhBoost = enhLevel * enhanceBoostPerLevel;
        float lvBoost = playerLevelPer1Percent > 0 ? (playerLv / playerLevelPer1Percent) : 0f;
        float totalBoost = enhBoost + lvBoost;

        int selectedRarity = GetWeightedRarity(maxRarity, totalBoost);

        SkillData skill = mapping.GetSkillByRarity((ItemRarity)selectedRarity);
        if (skill == null)
            skill = mapping.GetSkillByRarity(eq.rarity);
        if (skill == null) return;

        currentSlotLevels[slotIndex] = selectedRarity + 1;

        if (SkillManager.Instance != null)
            SkillManager.Instance.SwapEquipmentSkillOnHotbarAtIndex(null, skill, slotIndex);
    }

    /// <summary>
    /// 가중치 기반 등급 선택 (피벗 방식).
    /// - 상위 등급일수록 기본 가중치가 가파르게 낮음 (Legendary 매우 희귀)
    /// - progressBoost가 높을수록: 상위는 ×증가, 하위는 ÷감소 (양방향 보정)
    /// - pivot = maxRarity / 2 — 중심 등급을 기준으로 위/아래로 갈수록 보정 강해짐
    /// </summary>
    private int GetWeightedRarity(int maxRarity, float progressBoost)
    {
        if (maxRarity <= 0) return 0;

        float pivot = maxRarity / 2f;
        float boostFraction = (progressBoost / 100f) * progressInfluence;

        float totalWeight = 0f;
        float[] weights = new float[maxRarity + 1];

        for (int i = 0; i <= maxRarity && i < baseRarityWeights.Length; i++)
        {
            // 피벗 기준 거리: 상위(+) / 하위(-)
            float distance = i - pivot;
            float modifier = 1f + boostFraction * distance;

            // 하위 등급은 0.05 이하로 떨어지지 않도록 클램프 (완전 차단 방지)
            modifier = Mathf.Max(0.05f, modifier);

            weights[i] = baseRarityWeights[i] * modifier;
            totalWeight += weights[i];
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = maxRarity; i >= 0; i--) // 상위부터 체크
        {
            cumulative += weights[i];
            if (roll < cumulative) return i;
        }

        return 0;
    }

    // ═══════════════════════════════════════════════════════════════
    //  콤보 판정 (포커 핸드)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>6슬롯 레벨 배열로 콤보 타입 판정</summary>
    public static ComboType EvaluateCombo(int[] levels)
    {
        if (levels == null || levels.Length < 6) return ComboType.None;

        // 레벨별 카운트 (인덱스 0~5, 0=미장착)
        int[] counts = new int[6]; // [0]=미장착, [1]~[5]=레어리티
        for (int i = 0; i < 6; i++)
        {
            int lv = Mathf.Clamp(levels[i], 0, 5); // ★ 미장착=0 허용
            counts[lv]++;
        }

        // ★ 6슬롯 모두 언커먼(2+) 이상이어야 콤보 발동
        //   미장착(0)이나 커먼(1) 슬롯이 하나라도 있으면 콤보 없음
        for (int i = 0; i < 6; i++)
        {
            if (levels[i] < 2) return ComboType.None;
        }

        // 최대 동일 개수 (미장착=0은 counts에서 제외됨 — lv=1부터 시작)
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

            // ★ 동료(CompanionAgent)는 범위 공격 대상에서 제외
            if (col.GetComponent<CompanionAgent>() != null) continue;

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
        // ★ 패널이 없으면 런타임에 자동 생성
        if (comboPopupPanel == null && autoCreatePopupIfMissing)
            EnsureRuntimePopup();

        if (comboPopupPanel == null) return;

        if (comboPopupCoroutine != null)
            StopCoroutine(comboPopupCoroutine);

        string displayName = skillSet?.comboName;
        if (string.IsNullOrEmpty(displayName))
            displayName = GetDefaultComboName(combo, level);

        if (comboNameText != null)
        {
            comboNameText.text = displayName;
            // 콤보 등급별 색상
            comboNameText.color = combo >= ComboType.FiveOfAKind
                ? new Color(1f, 0.85f, 0.1f)
                : combo >= ComboType.Straight
                    ? new Color(0.4f, 0.9f, 1f)
                    : new Color(0.6f, 1f, 0.6f);
        }

        // ★ 이름만 표시 — desc/icon은 비활성화
        if (comboDescText != null) comboDescText.gameObject.SetActive(false);
        if (comboIconImage != null) comboIconImage.gameObject.SetActive(false);

        comboPopupPanel.SetActive(true);
        comboPopupCoroutine = StartCoroutine(HideComboPopup());
    }

    /// <summary>
    /// comboPopupPanel이 인스펙터에 연결 안 돼 있으면 런타임에 작은 다이얼로그 생성.
    /// 화면 중앙 상단에 콤보 이름만 표시.
    /// </summary>
    private void EnsureRuntimePopup()
    {
        if (comboPopupPanel != null) return;

        // 1) Canvas 찾기 (없으면 생성)
        Canvas targetCanvas = null;
        var canvases = FindObjectsOfType<Canvas>();
        foreach (var c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
            {
                targetCanvas = c;
                break;
            }
        }

        if (targetCanvas == null)
        {
            GameObject canvasGO = new GameObject("ComboPopupCanvas");
            targetCanvas = canvasGO.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 100;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        // 2) Panel (배경) 생성
        GameObject panelGO = new GameObject("ComboPopupPanel");
        panelGO.transform.SetParent(targetCanvas.transform, false);

        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.75f);
        panelRT.anchorMax = new Vector2(0.5f, 0.75f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta = new Vector2(400f, 100f);

        var panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.7f);
        panelImage.raycastTarget = false;

        // 3) Text 생성
        GameObject textGO = new GameObject("ComboNameText");
        textGO.transform.SetParent(panelGO.transform, false);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = 50f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        comboPopupPanel = panelGO;
        comboNameText = tmp;
        panelGO.SetActive(false);

        Debug.Log("[SkillComboSystem] 콤보 팝업 런타임 자동 생성 완료");
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
