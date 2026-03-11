using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 강화 시스템
///
/// ✅ 기존 기능 유지:
///   - 인벤토리 아이템 강화
///   - 장착 중인 아이템 강화 지원 (EquipmentSlot 클릭 시)
///   - 강화 성공 시 장착 중이면 EquipmentManager.RecalculateStats() 자동 호출
///   - 강화 패널에 "해제" 버튼 지원
///
/// ★ 신규 패치:
///   - TryEnhance / SafeEnhance 에서 Gold 외에 CropPoint도 함께 소모
///   - EnhancementCropCostPatch.PatchEnhancement() 로 통합 처리
///   - UI에 CropPoint 비용 표시 추가
/// </summary>
public class EnhancementSystem : MonoBehaviour
{
    public static EnhancementSystem Instance;

    [Header("강화 비용 설정")]
    public int baseEnhanceCost = 100;
    public float costMultiplier = 1.5f;

    [Header("강화 성공 확률")]
    public float[] successRates = new float[]
    {
        100f, 100f, 100f,
        95f, 90f, 85f,
        80f, 75f, 70f,
        65f, 60f, 55f,
        50f, 45f, 40f,
        30f, 30f, 30f,
        30f, 30f, 30f,
        20f, 20f, 20f,
        20f, 20f, 20f,
        10f, 10f, 10f
    };

    [Header("강화 실패 시")]
    public bool canBreak = true;
    public int breakStartLevel = 10;
    public bool levelDownOnFail = true;

    [Header("강화 UI")]
    public GameObject enhancementPanel;
    public Slider successRateBar;
    public TextMeshProUGUI successRateText;
    public TextMeshProUGUI costText;        // 골드 비용
    public TextMeshProUGUI cropCostText;    // ★ CropPoint 비용 표시 (신규)
    public TextMeshProUGUI statsText;

    [Header("아이템 슬롯")]
    public Image itemIconImage;
    public TextMeshProUGUI itemEnhanceLevelText;
    public TextMeshProUGUI itemNameText;

    [Header("반짝임 효과")]
    public ParticleSystem glowEffect;
    public float glowDuration = 2f;

    [Header("버튼")]
    public Button enhanceButton;
    public Button safeEnhanceButton;
    public Button unequipButton;

    // 현재 선택된 아이템
    private InventorySlot currentInventorySlot;
    private EquipmentType currentEquippedType;
    private bool isEquippedItem = false;
    private EquipmentData currentEquipment;
    private int currentEnhanceLevel = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (enhancementPanel != null) enhancementPanel.SetActive(false);

        if (enhanceButton != null) { enhanceButton.onClick.RemoveAllListeners(); enhanceButton.onClick.AddListener(TryEnhance); }
        if (safeEnhanceButton != null) { safeEnhanceButton.onClick.RemoveAllListeners(); safeEnhanceButton.onClick.AddListener(SafeEnhance); }
        if (unequipButton != null) { unequipButton.onClick.RemoveAllListeners(); unequipButton.onClick.AddListener(UnequipCurrent); }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H)) ToggleEnhancementUI();
    }

    public void ToggleEnhancementUI()
    {
        if (enhancementPanel == null) return;
        bool isActive = enhancementPanel.activeSelf;
        enhancementPanel.SetActive(!isActive);
        if (!isActive) ClearSelection();
    }

    // ─────────────────────────────────────────
    //  아이템 선택
    // ─────────────────────────────────────────

    public void SelectItemForEnhancement(InventorySlot slot)
    {
        if (slot == null || slot.itemData == null) return;

        if (!(slot.itemData is EquipmentData equipment))
        {
            UIManager.Instance?.ShowMessage("장비만 강화 가능!", Color.red);
            return;
        }

        currentInventorySlot = slot;
        currentEquipment = equipment;
        currentEnhanceLevel = slot.enhanceLevel;
        isEquippedItem = false;

        if (unequipButton != null) unequipButton.gameObject.SetActive(false);

        UpdateEnhancementUI();
        if (enhancementPanel != null && !enhancementPanel.activeSelf)
            enhancementPanel.SetActive(true);

        Debug.Log($"[EnhancementSystem] 인벤 아이템 선택: {equipment.itemName} +{currentEnhanceLevel}");
    }

    public void SelectEquippedItemForEnhancement(EquipmentType type)
    {
        if (EquipmentManager.Instance == null) return;

        EquipmentData equipment = EquipmentManager.Instance.GetEquippedItem(type);
        if (equipment == null) return;

        currentInventorySlot = null;
        currentEquipment = equipment;
        currentEnhanceLevel = EquipmentManager.Instance.GetEnhanceLevel(type);
        currentEquippedType = type;
        isEquippedItem = true;

        if (unequipButton != null) unequipButton.gameObject.SetActive(true);

        UpdateEnhancementUI();
        if (enhancementPanel != null && !enhancementPanel.activeSelf)
            enhancementPanel.SetActive(true);

        Debug.Log($"[EnhancementSystem] 장착 아이템 선택: {equipment.itemName} +{currentEnhanceLevel}");
    }

    // ─────────────────────────────────────────
    //  강화 시도
    //  ★ 패치: Gold + CropPoint 동시 소모
    // ─────────────────────────────────────────

    public void TryEnhance()
    {
        SoundManager.Instance?.PlayButtonClick();

        if (currentEquipment == null)
        {
            UIManager.Instance?.ShowMessage("강화할 장비를 선택하세요!", Color.red);
            return;
        }

        if (currentEnhanceLevel >= 30)
        {
            UIManager.Instance?.ShowMessage("최대 강화 레벨!", Color.yellow);
            return;
        }

        int goldCost = CalculateEnhanceCost(currentEnhanceLevel);

        // ★ Gold + CropPoint 통합 비용 처리
        // EnhancementCropCostPatch가 없으면 기존 방식(Gold만)으로 폴백
        if (EnhancementCropCostPatch.Instance != null)
        {
            if (!EnhancementCropCostPatch.Instance.PayEnhanceCost(currentEnhanceLevel, goldCost))
                return; // 비용 부족 (메시지는 내부에서 표시)
        }
        else
        {
            // 폴백: Gold만 차감
            if (GameManager.Instance == null || !GameManager.Instance.SpendGold(goldCost))
            {
                UIManager.Instance?.ShowMessage("골드 부족!", Color.red);
                return;
            }
        }

        bool success = RollEnhanceSuccess(currentEnhanceLevel);

        if (success) OnEnhanceSuccess();
        else OnEnhanceFail();
    }

    public void SafeEnhance()
    {
        if (currentEquipment == null) return;
        if (currentEnhanceLevel >= 30) return;

        int goldCost = CalculateEnhanceCost(currentEnhanceLevel) * 3; // 안전강화 3배

        // ★ Gold + CropPoint 통합 비용 처리 (안전강화도 동일하게 적용)
        if (EnhancementCropCostPatch.Instance != null)
        {
            if (!EnhancementCropCostPatch.Instance.PayEnhanceCost(currentEnhanceLevel, goldCost))
                return;
        }
        else
        {
            if (GameManager.Instance == null || !GameManager.Instance.SpendGold(goldCost))
            {
                UIManager.Instance?.ShowMessage("골드 부족! (안전 강화)", Color.red);
                return;
            }
        }

        // 안전 강화: 무조건 성공, 레벨 하락/파괴 없음
        currentEnhanceLevel++;
        OnEnhanceSuccess();
    }

    // ─────────────────────────────────────────
    //  강화 성공 / 실패
    // ─────────────────────────────────────────

    private void OnEnhanceSuccess()
    {
        SoundManager.Instance?.PlayEnhanceSuccess();

        if (isEquippedItem)
            EquipmentManager.Instance?.UpdateEnhanceLevel(currentEquippedType, currentEnhanceLevel);
        else
        {
            currentInventorySlot?.UpdateEnhanceLevel(currentEnhanceLevel);
            if (EquipmentManager.Instance != null &&
                EquipmentManager.Instance.IsEquipped(currentEquipment.equipmentType))
                EquipmentManager.Instance.RecalculateStats();
        }

        Debug.Log($"[EnhancementSystem] 강화 성공! +{currentEnhanceLevel}");
        UIManager.Instance?.ShowMessage($"⭐ 강화 성공! +{currentEnhanceLevel}", Color.green);

        PlayGlowEffect();
        UpdateEnhancementUI();

        AchievementSystem.Instance?.UpdateAchievementProgress(AchievementType.EnhanceEquipment, "", 1);
    }

    private void OnEnhanceFail()
    {
        SoundManager.Instance?.PlayEnhanceFail();
        Debug.Log("[EnhancementSystem] 강화 실패!");

        if (levelDownOnFail && currentEnhanceLevel > 0)
        {
            currentEnhanceLevel--;

            if (isEquippedItem)
                EquipmentManager.Instance?.UpdateEnhanceLevel(currentEquippedType, currentEnhanceLevel);
            else
                currentInventorySlot?.UpdateEnhanceLevel(currentEnhanceLevel);

            UIManager.Instance?.ShowMessage($"강화 실패... +{currentEnhanceLevel}으로 하락", Color.red);
        }
        else
        {
            UIManager.Instance?.ShowMessage("강화 실패!", Color.red);
        }

        // 파괴 체크
        if (canBreak && currentEnhanceLevel >= breakStartLevel)
        {
            float breakChance = (currentEnhanceLevel - breakStartLevel + 1) * 5f;
            if (Random.Range(0f, 100f) < breakChance)
            {
                UIManager.Instance?.ShowMessage($"{currentEquipment.itemName} 파괴됨!", Color.red);

                if (isEquippedItem)
                    EquipmentManager.Instance?.UnequipItem(currentEquippedType);
                else
                    InventoryManager.Instance?.RemoveItem(currentEquipment, 1);

                ClearSelection();
                return;
            }
        }

        UpdateEnhancementUI();
    }

    // ─────────────────────────────────────────
    //  해제 버튼
    // ─────────────────────────────────────────

    private void UnequipCurrent()
    {
        if (!isEquippedItem || EquipmentManager.Instance == null) return;

        EquipmentManager.Instance.UnequipItem(currentEquippedType);
        ClearSelection();
        if (enhancementPanel != null) enhancementPanel.SetActive(false);

        Debug.Log("[EnhancementSystem] 해제 버튼으로 장착 해제");
    }

    // ─────────────────────────────────────────
    //  UI 업데이트
    //  ★ cropCostText 추가
    // ─────────────────────────────────────────

    private void UpdateEnhancementUI()
    {
        if (currentEquipment == null) { ClearSelection(); return; }

        if (itemIconImage != null)
        {
            itemIconImage.sprite = currentEquipment.itemIcon;
            itemIconImage.enabled = true;
            itemIconImage.color = GetEnhanceColor(currentEnhanceLevel);
        }

        if (itemNameText != null)
        {
            string prefix = isEquippedItem ? "[장착 중] " : "";
            itemNameText.text = prefix + currentEquipment.itemName;
            itemNameText.color = isEquippedItem ? Color.yellow : Color.white;
        }

        if (itemEnhanceLevelText != null)
        {
            if (currentEnhanceLevel > 0)
            {
                itemEnhanceLevelText.text = $"+{currentEnhanceLevel}";
                itemEnhanceLevelText.enabled = true;
                itemEnhanceLevelText.color = GetEnhanceColor(currentEnhanceLevel);
            }
            else itemEnhanceLevelText.enabled = false;
        }

        float successRate = GetSuccessRate(currentEnhanceLevel);

        if (successRateBar != null) successRateBar.value = successRate / 100f;

        if (successRateText != null)
        {
            successRateText.text = $"{successRate:F0}%";
            successRateText.color = successRate >= 80f
                ? new Color(0.3f, 1f, 0.3f)
                : successRate >= 50f
                    ? new Color(1f, 1f, 0.3f)
                    : new Color(1f, 0.3f, 0.3f);
        }

        // 골드 비용
        int goldCost = CalculateEnhanceCost(currentEnhanceLevel);
        if (costText != null)
            costText.text = $"골드: {goldCost:N0} G";

        // ★ CropPoint 비용
        if (cropCostText != null)
        {
            if (EnhancementCropCostPatch.Instance != null)
            {
                int cpCost = EnhancementCropCostPatch.Instance.GetCropPointCost(currentEnhanceLevel);
                int curCp = FarmManager.Instance != null ? FarmManager.Instance.GetCropPoints() : 0;

                cropCostText.text = $"CP: {cpCost} (보유 {curCp})";
                cropCostText.color = curCp >= cpCost ? Color.white : Color.red;
            }
            else
            {
                cropCostText.text = "";
            }
        }

        // 능력치 표시
        if (statsText != null)
        {
            int baseAtk = currentEquipment.equipmentStats.attack;
            int baseDef = currentEquipment.equipmentStats.defense;
            int baseHp = currentEquipment.equipmentStats.health;

            float bonus = 1f + (currentEnhanceLevel * 0.1f);
            int curAtk = Mathf.RoundToInt(baseAtk * bonus);
            int curDef = Mathf.RoundToInt(baseDef * bonus);
            int curHp = Mathf.RoundToInt(baseHp * bonus);

            float nextBonus = 1f + ((currentEnhanceLevel + 1) * 0.1f);
            int nextAtk = Mathf.RoundToInt(baseAtk * nextBonus);
            int nextDef = Mathf.RoundToInt(baseDef * nextBonus);
            int nextHp = Mathf.RoundToInt(baseHp * nextBonus);

            statsText.text = "<b>【 현재 능력치 】</b>\n";
            if (currentEnhanceLevel > 0)
            {
                statsText.text += $" 공격력: <b>{curAtk}</b> <color=#888>({baseAtk}+{currentEnhanceLevel * 10}%)</color>\n";
                statsText.text += $" 방어력: <b>{curDef}</b> <color=#888>({baseDef}+{currentEnhanceLevel * 10}%)</color>\n";
                statsText.text += $" 체력: <b>{curHp}</b>  <color=#888>({baseHp}+{currentEnhanceLevel * 10}%)</color>\n";
            }
            else
            {
                statsText.text += $" 공격력: <b>{curAtk}</b>\n";
                statsText.text += $" 방어력: <b>{curDef}</b>\n";
                statsText.text += $" 체력: <b>{curHp}</b>\n";
            }

            if (currentEnhanceLevel < 30)
            {
                statsText.text += "<b>【 강화 성공 시 】</b>\n";
                statsText.text += $" 공격력: <b>{nextAtk}</b> <color=#4CAF50>(+{nextAtk - curAtk})</color>\n";
                statsText.text += $" 방어력: <b>{nextDef}</b> <color=#4CAF50>(+{nextDef - curDef})</color>\n";
                statsText.text += $" 체력: <b>{nextHp}</b>  <color=#4CAF50>(+{nextHp - curHp})</color>";
            }
            else
            {
                statsText.text += "<b><color=#FFD700>최대 강화 달성!</color></b>";
            }
        }
    }

    private void ClearSelection()
    {
        currentInventorySlot = null;
        currentEquipment = null;
        currentEnhanceLevel = 0;
        isEquippedItem = false;

        if (itemIconImage != null) { itemIconImage.sprite = null; itemIconImage.enabled = false; itemIconImage.color = Color.white; }
        if (itemEnhanceLevelText != null) itemEnhanceLevelText.enabled = false;
        if (itemNameText != null) { itemNameText.text = "강화할 장비를 선택하세요"; itemNameText.color = Color.gray; }
        if (successRateText != null) { successRateText.text = "--%"; successRateText.color = Color.white; }
        if (costText != null) costText.text = "골드: - G";
        if (cropCostText != null) cropCostText.text = "";
        if (successRateBar != null) successRateBar.value = 0f;
        if (statsText != null) statsText.text = "<color=#888>장비를 선택하면\n능력치가 표시됩니다</color>";
        if (unequipButton != null) unequipButton.gameObject.SetActive(false);
    }

    private void PlayGlowEffect()
    {
        if (glowEffect != null) StartCoroutine(PlayGlowCoroutine());
    }

    private IEnumerator PlayGlowCoroutine()
    {
        glowEffect.Play();
        yield return new WaitForSeconds(glowDuration);
        glowEffect.Stop();
    }

    int CalculateEnhanceCost(int level) => Mathf.RoundToInt(baseEnhanceCost * Mathf.Pow(costMultiplier, level));
    bool RollEnhanceSuccess(int level) => Random.Range(0f, 100f) < GetSuccessRate(level);
    float GetSuccessRate(int level) => level < successRates.Length ? successRates[level] : 30f;

    private Color GetEnhanceColor(int level)
    {
        if (level >= 30) return new Color(1f, 0.2f, 0.2f, 1f);
        if (level >= 25) return new Color(1f, 0.5f, 0f, 1f);
        if (level >= 20) return new Color(1f, 1f, 0f, 1f);
        if (level >= 15) return new Color(0.3f, 0.5f, 1f, 1f);
        if (level >= 10) return new Color(0.1f, 0.1f, 0.6f, 1f);
        if (level >= 5) return new Color(0.6f, 0.2f, 1f, 1f);
        return Color.white;
    }
}