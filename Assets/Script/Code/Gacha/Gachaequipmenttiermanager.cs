using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// GachaEquipmentTierManager
/// ─────────────────────────────────────────────────────────
/// 장비 뽑기 티어 시스템
///
/// [티어 구조]
///   원자(Atom) 장비 뽑기    : GachaManager 레벨 1 ~ (moleculeUnlockLevel - 1)
///   분자(Molecule) 장비 뽑기: GachaManager 레벨 moleculeUnlockLevel ~ (dnaUnlockLevel - 1)
///   DNA 장비 뽑기           : GachaManager 레벨 dnaUnlockLevel 이상
///
/// [레벨 1→2 비용]
///   lv1to2GoldCost + lv1to2CropPointCost
///   → GachaManager.TryPayLevelUpCost() 에서 이 값을 참조
///
/// [연결]
///   GachaManager.Instance 의 가챠 풀을 현재 티어에 맞게 교체
/// </summary>
public class GachaEquipmentTierManager : MonoBehaviour
{
    public static GachaEquipmentTierManager Instance;

    // ─── 티어 정의 ───
    public enum EquipmentTier
    {
        Atom = 0,   // 원자 장비
        Molecule = 1,   // 분자 장비
        DNA = 2    // DNA 장비
    }

    [Header("티어 경계 레벨")]
    public int moleculeUnlockLevel = 10;
    public int dnaUnlockLevel = 20;

    // ★ 레벨별 업그레이드 비용 (Inspector에서 자유롭게 설정)
    [Header("레벨별 업그레이드 비용")]
    [Tooltip("인덱스 0 = 레벨1→2, 인덱스 1 = 레벨2→3, ...")]
    public LevelUpCost[] levelUpCosts = new LevelUpCost[]
    {
        new LevelUpCost { goldCost = 500, cropPointCost = 100 },   // 1→2
        new LevelUpCost { goldCost = 2000, cropPointCost = 300 },  // 2→3
        new LevelUpCost { goldCost = 5000, cropPointCost = 500 },  // 3→4
        new LevelUpCost { goldCost = 10000, cropPointCost = 1000 },// 4→5
    };

    // 하위 호환용
    public int lv1to2GoldCost => levelUpCosts.Length > 0 ? levelUpCosts[0].goldCost : 500;
    public int lv1to2CropPointCost => levelUpCosts.Length > 0 ? levelUpCosts[0].cropPointCost : 100;

    [System.Serializable]
    public class LevelUpCost
    {
        public int goldCost;
        public int cropPointCost;
    }

    // ─── 티어별 가챠 풀 ───
    [Header("원자 장비 가챠 풀")]
    public List<GachaManager.GachaItem> atomGachaPool = new List<GachaManager.GachaItem>();

    [Header("분자 장비 가챠 풀")]
    public List<GachaManager.GachaItem> moleculeGachaPool = new List<GachaManager.GachaItem>();

    [Header("DNA 장비 가챠 풀")]
    public List<GachaManager.GachaItem> dnaGachaPool = new List<GachaManager.GachaItem>();

    // ─── UI ───
    [Header("티어 표시 UI")]
    public TextMeshProUGUI tierNameText;
    public TextMeshProUGUI tierDescriptionText;
    public Image tierBadgeImage;

    [Header("티어 색상")]
    public Color atomColor = new Color(0.6f, 0.8f, 1f);
    public Color moleculeColor = new Color(0.8f, 0.4f, 1f);
    public Color dnaColor = new Color(1f, 0.6f, 0.2f);

    [Header("레벨업 비용 표시 UI")]
    public TextMeshProUGUI levelUpCostText;
    public GameObject levelUpPanel;

    private EquipmentTier currentTier = EquipmentTier.Atom;
    private int cachedGachaLevel = -1;

    // 현재 티어 공개 (GachaManager에서 조회)
    public EquipmentTier CurrentTier => currentTier;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        Debug.Log("[ManagerInit] GachaEquipmentTierManager가 생성되었습니다.");
    }

    void Start()
    {
        RefreshTierFromGachaLevel();
        UpdateTierUI();
    }

    // ─────────────────────────────────────────────────────────
    //  매 프레임 GachaManager 레벨 변화 감지
    // ─────────────────────────────────────────────────────────

    void Update()
    {
        if (GachaManager.Instance == null) return;

        int lvl = GachaManager.Instance.currentLevel;
        if (lvl == cachedGachaLevel) return;

        cachedGachaLevel = lvl;
        RefreshTierFromGachaLevel();
        UpdateTierUI();
    }

    // ─────────────────────────────────────────────────────────
    //  티어 결정 및 풀 교체
    // ─────────────────────────────────────────────────────────

    public void RefreshTierFromGachaLevel()
    {
        if (GachaManager.Instance == null) return;

        int level = GachaManager.Instance.currentLevel;

        EquipmentTier newTier;
        if (level >= dnaUnlockLevel) newTier = EquipmentTier.DNA;
        else if (level >= moleculeUnlockLevel) newTier = EquipmentTier.Molecule;
        else newTier = EquipmentTier.Atom;

        if (newTier == currentTier) return;

        currentTier = newTier;
        ApplyTierPool();

        Debug.Log($"[GachaEquipmentTierManager] 티어 변경 → {currentTier} (레벨 {level})");
        UIManager.Instance?.ShowMessage($"{GetTierName(currentTier)} 해금!", Color.yellow);
    }

    private void ApplyTierPool()
    {
        if (GachaManager.Instance == null) return;

        List<GachaManager.GachaItem> pool = currentTier switch
        {
            EquipmentTier.Atom => atomGachaPool,
            EquipmentTier.Molecule => moleculeGachaPool,
            EquipmentTier.DNA => dnaGachaPool,
            _ => atomGachaPool
        };

        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"[GachaEquipmentTierManager] {currentTier} 풀이 비어있음!");
            return;
        }

        // GachaManager의 gachaPoolLv1을 현재 티어 풀로 교체 후 갱신
        GachaManager.Instance.gachaPoolLv1.Clear();
        GachaManager.Instance.gachaPoolLv1.AddRange(pool);
        GachaManager.Instance.UpdateGachaPool();
        GachaManager.Instance.ValidateGachaPool();

        Debug.Log($"[GachaEquipmentTierManager] {currentTier} 풀 적용 완료 ({pool.Count}개)");
    }

    // ─────────────────────────────────────────────────────────
    //  레벨업 비용 처리 (GachaManager에서 호출)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 레벨업 비용 지불 시도.
    /// fromLevel == 1 일 때만 Gold + CropPoint 추가 비용 부과.
    /// </summary>
    public bool TryPayLevelUpCost(int fromLevel)
    {
        int costIndex = fromLevel - 1; // fromLevel 1 → index 0

        // 배열 범위 밖이면 무료
        if (levelUpCosts == null || costIndex < 0 || costIndex >= levelUpCosts.Length)
            return true;

        LevelUpCost cost = levelUpCosts[costIndex];

        // 비용이 둘 다 0이면 무료
        if (cost.goldCost <= 0 && cost.cropPointCost <= 0)
            return true;

        // 골드 확인
        if (cost.goldCost > 0)
        {
            long currentGold = GameManager.Instance != null ? GameManager.Instance.PlayerGold : 0;
            if (currentGold < cost.goldCost)
            {
                UIManager.Instance?.ShowConfirmDialog(
                    $"가챠레벨업에골드가부족합니다.\n필요:{cost.goldCost:N0}G\n보유:{UIManager.FormatKoreanUnit(currentGold)}G",
                    onConfirm: null);
                return false;
            }
        }

        // CropPoint 확인
        if (cost.cropPointCost > 0)
        {
            long curCp = FarmManager.Instance != null ? FarmManager.Instance.GetCropPoints() : 0;
            if (curCp < cost.cropPointCost)
            {
                UIManager.Instance?.ShowConfirmDialog(
                    $"가챠레벨업을하기위해선\n작물포인트가필요합니다.\n필요:{cost.cropPointCost}CP\n보유:{curCp}CP",
                    onConfirm: null);
                return false;
            }
        }

        // 차감
        if (cost.goldCost > 0)
            GameManager.Instance.SpendGold(cost.goldCost);
        if (cost.cropPointCost > 0)
            FarmManager.Instance.SpendCropPoints(cost.cropPointCost);

        UIManager.Instance?.ShowMessage(
            $"레벨 {fromLevel}→{fromLevel + 1} 달성!\n-{cost.goldCost:N0}G / -{cost.cropPointCost}CP", Color.cyan);

        return true;
    }

    /// <summary>레벨업 비용 팝업 표시</summary>
    public void ShowLevelUpPanel()
    {
        if (GachaManager.Instance == null) return;

        if (levelUpCostText != null)
        {
            int curLv = GachaManager.Instance.currentLevel;
            int costIdx = curLv - 1;

            if (levelUpCosts != null && costIdx >= 0 && costIdx < levelUpCosts.Length
                && (levelUpCosts[costIdx].goldCost > 0 || levelUpCosts[costIdx].cropPointCost > 0))
            {
                var c = levelUpCosts[costIdx];
                levelUpCostText.text = $"레벨업: {c.goldCost:N0}G + {c.cropPointCost}CP\n(뽑기 {GachaManager.Instance.gachaCountForLevelUp}회 달성 시 자동 레벨업)";
            }
            else
            {
                levelUpCostText.text = $"자동 레벨업 (뽑기 {GachaManager.Instance.gachaCountForLevelUp}회 달성 시)";
            }
        }

        if (levelUpPanel != null) levelUpPanel.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────
    //  UI 갱신
    // ─────────────────────────────────────────────────────────

    private void UpdateTierUI()
    {
        string tName = GetTierName(currentTier);
        Color tColor = GetTierColor(currentTier);

        if (tierNameText != null)
        {
            tierNameText.text = tName;
            tierNameText.color = tColor;
        }

        if (tierDescriptionText != null)
            tierDescriptionText.text = GetTierDescription(currentTier);

        if (tierBadgeImage != null)
            tierBadgeImage.color = tColor;
    }

    // ─────────────────────────────────────────────────────────
    //  헬퍼 (외부에서도 호출 가능)
    // ─────────────────────────────────────────────────────────

    public string GetTierName(EquipmentTier tier) => tier switch
    {
        EquipmentTier.Atom => "원자 장비 뽑기",
        EquipmentTier.Molecule => "분자 장비 뽑기",
        EquipmentTier.DNA => "DNA 장비 뽑기",
        _ => "장비 뽑기"
    };

    public string GetTierDescription(EquipmentTier tier) => tier switch
    {
        EquipmentTier.Atom =>
            $"초급 장비 뽑기\nLv.{moleculeUnlockLevel} 달성 시 분자 장비 뽑기 해금",
        EquipmentTier.Molecule =>
            $"중급 장비 뽑기\nLv.{dnaUnlockLevel} 달성 시 DNA 장비 뽑기 해금",
        EquipmentTier.DNA =>
            "최고급 장비 뽑기\n최상위 희귀도 장비 획득 가능",
        _ => ""
    };

    public Color GetTierColor(EquipmentTier tier) => tier switch
    {
        EquipmentTier.Atom => atomColor,
        EquipmentTier.Molecule => moleculeColor,
        EquipmentTier.DNA => dnaColor,
        _ => Color.white
    };
}