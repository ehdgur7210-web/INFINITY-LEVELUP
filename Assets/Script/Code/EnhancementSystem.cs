using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 강화 시스템
///
/// ✅ 수정:
///   - 인벤토리 아이템 강화 (기존)
///   - 장착 중인 아이템 강화 지원 추가 (EquipmentSlot 클릭 시)
///   - 강화 성공 시 장착 중이면 EquipmentManager.RecalculateStats() 자동 호출
///   - 강화 패널에 "해제" 버튼 지원
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
    public TextMeshProUGUI costText;
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
    public Button unequipButton; // 장착 해제 버튼 (장착 중인 아이템 선택 시 표시)

    // 현재 선택된 아이템
    private InventorySlot currentInventorySlot;   // 인벤 슬롯 아이템
    private EquipmentType currentEquippedType;    // 장착 중인 아이템의 슬롯 타입
    private bool isEquippedItem = false;          // 장착 중인 아이템 여부
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
        if (enhanceButton != null) enhanceButton.onClick.AddListener(TryEnhance);
        if (safeEnhanceButton != null) safeEnhanceButton.onClick.AddListener(SafeEnhance);
        if (unequipButton != null) unequipButton.onClick.AddListener(UnequipCurrent);
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
    // 인벤 슬롯 아이템 강화 선택
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

        // 해제 버튼 숨기기 (인벤 아이템은 장착 해제 불필요)
        if (unequipButton != null) unequipButton.gameObject.SetActive(false);

        UpdateEnhancementUI();
        if (enhancementPanel != null && !enhancementPanel.activeSelf)
            enhancementPanel.SetActive(true);

        Debug.Log($"[EnhancementSystem] 인벤 아이템 선택: {equipment.itemName} +{currentEnhanceLevel}");
    }

    // ─────────────────────────────────────────
    // 장착 중인 아이템 강화 선택 (EquipmentSlot 클릭)
    // ─────────────────────────────────────────
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

        // 해제 버튼 표시 (장착 중이므로)
        if (unequipButton != null) unequipButton.gameObject.SetActive(true);

        UpdateEnhancementUI();
        if (enhancementPanel != null && !enhancementPanel.activeSelf)
            enhancementPanel.SetActive(true);

        Debug.Log($"[EnhancementSystem] 장착 아이템 선택: {equipment.itemName} +{currentEnhanceLevel}");
    }

    // ─────────────────────────────────────────
    // 강화 시도
    // ─────────────────────────────────────────
    public void TryEnhance()
    {
        // ★ 강화 버튼 클릭 효과음
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

        int cost = CalculateEnhanceCost(currentEnhanceLevel);
        if (GameManager.Instance == null || !GameManager.Instance.SpendGold(cost))
        {
            UIManager.Instance?.ShowMessage("골드 부족!", Color.red);
            return;
        }

        bool success = RollEnhanceSuccess(currentEnhanceLevel);

        if (success)
        {
            currentEnhanceLevel++;
            OnEnhanceSuccess();
        }
        else
        {
            OnEnhanceFail();
        }
    }

    public void SafeEnhance()
    {
        if (currentEquipment == null) return;
        if (currentEnhanceLevel >= 30) return;

        int cost = CalculateEnhanceCost(currentEnhanceLevel) * 3; // 안전 강화는 3배 비용
        if (GameManager.Instance == null || !GameManager.Instance.SpendGold(cost))
        {
            UIManager.Instance?.ShowMessage("골드 부족! (안전 강화)", Color.red);
            return;
        }

        // 안전 강화: 무조건 성공, 레벨 하락/파괴 없음
        currentEnhanceLevel++;
        OnEnhanceSuccess();
    }

    // ─────────────────────────────────────────
    // 강화 성공/실패 처리
    // ─────────────────────────────────────────
    private void OnEnhanceSuccess()
    {
        // ★ 강화 성공 효과음
        SoundManager.Instance?.PlayEnhanceSuccess();
        // 슬롯 UI 갱신
        if (isEquippedItem)
        {
            // 장착 중인 아이템 → EquipmentManager에 레벨 갱신 (스탯 자동 재계산)
            EquipmentManager.Instance?.UpdateEnhanceLevel(currentEquippedType, currentEnhanceLevel);
        }
        else
        {
            // 인벤 슬롯 아이템 → 슬롯 UI 갱신
            currentInventorySlot?.UpdateEnhanceLevel(currentEnhanceLevel);

            // 장착 중이면 스탯도 재계산
            if (EquipmentManager.Instance != null &&
                EquipmentManager.Instance.IsEquipped(currentEquipment.equipmentType))
            {
                EquipmentManager.Instance.RecalculateStats();
            }
        }

        Debug.Log($"[EnhancementSystem] 강화 성공! +{currentEnhanceLevel}");
        UIManager.Instance?.ShowMessage($"⭐ 강화 성공! +{currentEnhanceLevel}", Color.green);

        PlayGlowEffect();
        UpdateEnhancementUI();

        // 업적
        AchievementSystem.Instance?.UpdateAchievementProgress(
            AchievementType.EnhanceEquipment, "", 1);
    }

    private void OnEnhanceFail()
    {
        // ★ 강화 실패 효과음
        SoundManager.Instance?.PlayEnhanceFail();
        Debug.Log("[EnhancementSystem] 강화 실패!");

        if (levelDownOnFail && currentEnhanceLevel > 0)
        {
            currentEnhanceLevel--;

            if (isEquippedItem)
                EquipmentManager.Instance?.UpdateEnhanceLevel(currentEquippedType, currentEnhanceLevel);
            else
                currentInventorySlot?.UpdateEnhanceLevel(currentEnhanceLevel);

            Debug.Log($"[EnhancementSystem] 레벨 하락... +{currentEnhanceLevel}");
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
    // 해제 버튼 (장착 중인 아이템)
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
    // UI 업데이트
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

        if (successRateBar != null)
            successRateBar.value = successRate / 100f;

        if (successRateText != null)
        {
            successRateText.text = $"{successRate:F0}%";
            successRateText.color = successRate >= 80f
                ? new Color(0.3f, 1f, 0.3f)
                : successRate >= 50f
                    ? new Color(1f, 1f, 0.3f)
                    : new Color(1f, 0.3f, 0.3f);
        }

        int cost = CalculateEnhanceCost(currentEnhanceLevel);
        if (costText != null)
            costText.text = $"비용: {cost:N0} G";

        if (statsText != null)
        {
            int baseAtk = currentEquipment.attackBonus;
            int baseDef = currentEquipment.defenseBonus;
            int curAtk = baseAtk + (currentEnhanceLevel * 2);
            int curDef = baseDef + (currentEnhanceLevel * 1);

            statsText.text = "<b>【 현재 능력치 】</b>\n";
            if (currentEnhanceLevel > 0)
            {
                statsText.text += $" 공격력: <b>{curAtk}</b> <color=#888>({baseAtk}+{currentEnhanceLevel * 2})</color>\n";
                statsText.text += $" 방어력: <b>{curDef}</b> <color=#888>({baseDef}+{currentEnhanceLevel})</color>\n";
            }
            else
            {
                statsText.text += $" 공격력: <b>{curAtk}</b>\n";
                statsText.text += $" 방어력: <b>{curDef}</b>\n";
            }

            if (currentEnhanceLevel < 30)
            {
                statsText.text += "<b>【 강화 성공 시 】</b>\n";
                statsText.text += $" 공격력: <b>{curAtk + 2}</b> <color=#4CAF50>(+2)</color>\n";
                statsText.text += $" 방어력: <b>{curDef + 1}</b> <color=#4CAF50>(+1)</color>";
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
        if (costText != null) costText.text = "비용: - G";
        if (successRateBar != null) successRateBar.value = 0f;
        if (statsText != null) statsText.text = "<color=#888>장비를 선택하면\n능력치가 표시됩니다</color>";
        if (unequipButton != null) unequipButton.gameObject.SetActive(false);
    }

    private void PlayGlowEffect()
    {
        if (glowEffect != null)
            StartCoroutine(PlayGlowCoroutine());
    }

    private IEnumerator PlayGlowCoroutine()
    {
        glowEffect.Play();
        yield return new WaitForSeconds(glowDuration);
        glowEffect.Stop();
    }

    private void ApplyEnhancementToPlayer()
    {
        if (currentEquipment == null || EquipmentManager.Instance == null) return;
        if (EquipmentManager.Instance.IsEquipped(currentEquipment.equipmentType))
        {
            EquipmentManager.Instance.RecalculateStats();
        }
    }

    int CalculateEnhanceCost(int level)
        => Mathf.RoundToInt(baseEnhanceCost * Mathf.Pow(costMultiplier, level));

    bool RollEnhanceSuccess(int level)
        => Random.Range(0f, 100f) < GetSuccessRate(level);

    float GetSuccessRate(int level)
        => level < successRates.Length ? successRates[level] : 30f;

    private Color GetLevelColor(int level)
    {
        if (level >= 30) return new Color(1f, 0.2f, 0.2f, 1f);   // 🔴 빨강  (30강)
        if (level >= 25) return new Color(1f, 0.5f, 0f, 1f);     // 🟠 주황  (25강)
        if (level >= 20) return new Color(1f, 1f, 0f, 1f);       // 🟡 노랑  (20강)
        if (level >= 15) return new Color(0.3f, 0.5f, 1f, 1f);   // 🔵 파랑  (15강)
        if (level >= 10) return new Color(0.1f, 0.1f, 0.6f, 1f); // 🌑 남색  (10강)
        if (level >= 5) return new Color(0.6f, 0.2f, 1f, 1f);   // 🟣 보라  (5강)
        return Color.white;                                        // ⚪ 흰색  (0~4강)
    }

    private Color GetEnhanceColor(int level)
    {
        if (level >= 30) return new Color(1f, 0.2f, 0.2f, 1f);   // 🔴 빨강  (30강)
        if (level >= 25) return new Color(1f, 0.5f, 0f, 1f);     // 🟠 주황  (25강)
        if (level >= 20) return new Color(1f, 1f, 0f, 1f);       // 🟡 노랑  (20강)
        if (level >= 15) return new Color(0.3f, 0.5f, 1f, 1f);   // 🔵 파랑  (15강)
        if (level >= 10) return new Color(0.1f, 0.1f, 0.6f, 1f); // 🌑 남색  (10강)
        if (level >= 5) return new Color(0.6f, 0.2f, 1f, 1f);   // 🟣 보라  (5강)
        return Color.white;                                        // ⚪ 흰색  (0~4강)
    }
}