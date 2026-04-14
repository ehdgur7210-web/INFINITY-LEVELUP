using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 장비 상세 팝업 — 레벨업 / 판매 / 장착
///
/// [구성]
///   - 아이템 아이콘 + 이름 + 레어도
///   - 현재 레벨 / 최대 레벨
///   - 현재 스탯 → 다음 레벨 스탯 (화살표)
///   - 필요 재료 아이콘 + 수량 (보유/필요)
///   - 필요 골드
///   - 레벨업 버튼 (재료/골드 부족 시 비활성화)
///   - 판매 버튼 + 수량 조절 (최대 보유-1)
///   - 장착 버튼
///
/// [Inspector 연결]
///   MainScene Canvas 하위에 EquipmentDetailPopup 오브젝트 배치
/// </summary>
public class EquipmentDetailPopup : MonoBehaviour
{
    public static EquipmentDetailPopup Instance;

    [Header("팝업 패널")]
    public GameObject popupPanel;

    [Header("아이템 정보")]
    public Image itemIcon;
    public TextMeshProUGUI itemNameText;
    public Image rarityBorder;
    public TextMeshProUGUI rarityText;

    [Header("레벨 표시")]
    public TextMeshProUGUI levelText;
    public Slider levelProgressBar;

    [Header("스탯 비교")]
    public TextMeshProUGUI currentStatsText;
    public TextMeshProUGUI nextStatsText;
    public Image arrowImage;

    [Header("최종 강화 스탯 (선택)")]
    [Tooltip("MaxLv + Max강화 적용 시 최종 스탯을 표시할 텍스트. 미연결 시 currentStatsText 하단에 자동 추가")]
    public TextMeshProUGUI maxEnhanceStatsText;

    [Header("레벨업 비용")]
    public TextMeshProUGUI goldCostText;
    public Image materialIcon;
    public TextMeshProUGUI materialCountText;

    [Header("버튼")]
    public Button levelUpButton;
    public TextMeshProUGUI levelUpButtonText;
    public Button sellButton;
    public Button equipButton;
    public Button closeButton;

    [Header("판매 영역")]
    public GameObject sellArea;
    public Slider sellQuantitySlider;
    public TextMeshProUGUI sellQuantityText;
    public TextMeshProUGUI sellPriceText;
    public Button sellConfirmButton;

    // ── 내부 상태 ──
    private InventorySlot currentSlot;
    private EquipmentData currentEquip;
    private bool isSellMode = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        Debug.Log("[ManagerInit] EquipmentDetailPopup가 생성되었습니다.");
    }

    void Start()
    {
        if (popupPanel != null) popupPanel.SetActive(false);
        if (sellArea != null) sellArea.SetActive(false);

        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (levelUpButton != null) levelUpButton.onClick.AddListener(OnLevelUpClicked);
        if (equipButton != null) equipButton.onClick.AddListener(OnEquipClicked);
        if (sellButton != null) sellButton.onClick.AddListener(ToggleSellMode);
        if (sellConfirmButton != null) sellConfirmButton.onClick.AddListener(OnSellConfirmed);
        if (sellQuantitySlider != null) sellQuantitySlider.onValueChanged.AddListener(OnSellSliderChanged);
    }

    // ═══════════════════════════════════════════════════════════════
    //  열기 / 닫기
    // ═══════════════════════════════════════════════════════════════

    public void Open(InventorySlot slot)
    {
        if (slot == null || slot.itemData == null) return;
        if (!(slot.itemData is EquipmentData eq)) return;

        currentSlot = slot;
        currentEquip = eq;
        isSellMode = false;

        if (sellArea != null) sellArea.SetActive(false);
        if (popupPanel != null) popupPanel.SetActive(true);

        RefreshUI();
    }

    public void Close()
    {
        if (popupPanel != null) popupPanel.SetActive(false);
        currentSlot = null;
        currentEquip = null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI 갱신
    // ═══════════════════════════════════════════════════════════════

    private void RefreshUI()
    {
        if (currentEquip == null || currentSlot == null) return;

        // ── 기본 정보 ──
        if (itemIcon != null)
        {
            itemIcon.sprite = currentEquip.itemIcon;
            itemIcon.color = Color.white;
        }

        if (itemNameText != null)
            itemNameText.text = currentEquip.itemName;

        if (rarityBorder != null)
            rarityBorder.color = currentEquip.GetRarityColor();

        if (rarityText != null)
        {
            rarityText.text = currentEquip.rarity.ToString();
            rarityText.color = currentEquip.GetRarityColor();
        }

        // ── 레벨 ──
        int curLv = currentSlot.itemLevel;
        int maxLv = currentEquip.maxItemLevel;
        bool isMaxLevel = curLv >= maxLv;

        if (levelText != null)
            levelText.text = $"Lv.{curLv} / {maxLv}";

        if (levelProgressBar != null)
            levelProgressBar.value = maxLv > 0 ? (float)curLv / maxLv : 1f;

        // ── 스탯 비교 ──
        RefreshStatComparison(curLv, isMaxLevel);

        // ── 레벨업 비용 ──
        RefreshLevelUpCost(curLv, isMaxLevel);

        // ── 판매 버튼 상태 ──
        if (sellButton != null)
            sellButton.interactable = currentSlot.itemCount > 1;

        // ── 장착 버튼 ──
        if (equipButton != null)
            equipButton.interactable = currentSlot.isUnlocked && currentSlot.itemCount > 0;
    }

    private void RefreshStatComparison(int curLv, bool isMaxLevel)
    {
        EquipmentStats curStats = currentEquip.GetLeveledStats(curLv);

        // ★ 현재 강화 단계 반영 (장착 전이라도 슬롯의 enhanceLevel 사용)
        float curEnhMult = EnhancementSystem.GetEnhanceMultiplier(currentSlot.enhanceLevel);
        EquipmentStats curStatsEnh = ApplyEnhanceMult(curStats, curEnhMult);

        string cur = FormatStats(curStatsEnh);
        if (currentSlot.enhanceLevel > 0)
            cur = $"<color=#FFD700>+{currentSlot.enhanceLevel}</color>\n" + cur;
        if (currentStatsText != null) currentStatsText.text = cur;

        if (isMaxLevel)
        {
            if (nextStatsText != null) nextStatsText.text = "<color=#FFD700>MAX</color>";
            if (arrowImage != null) arrowImage.gameObject.SetActive(false);
        }
        else
        {
            EquipmentStats nextStats = currentEquip.GetLeveledStats(curLv + 1);
            EquipmentStats nextStatsEnh = ApplyEnhanceMult(nextStats, curEnhMult);
            string next = FormatStatsWithDiff(curStatsEnh, nextStatsEnh);
            if (nextStatsText != null) nextStatsText.text = next;
            if (arrowImage != null)
                arrowImage.gameObject.SetActive(true);
        }

        // ── ★ 최종 강화 스탯 미리보기 (MaxLv + Max강화 적용) ──
        int maxLv = currentEquip.maxItemLevel;
        int maxEnh = EnhancementSystem.Instance != null ? EnhancementSystem.Instance.maxEnhanceLevel : 20;
        float maxEnhMult = 1f + (maxEnh * 0.1f);
        EquipmentStats finalStats = ApplyEnhanceMult(currentEquip.GetLeveledStats(maxLv), maxEnhMult);

        // ★ 현재 단계와 무관하게 항상 표시 (+12든 +13이든 풀강 시 최종 수치 미리보기)
        string finalLine = $"<color=#FFD700>풀강 시 최종 (Lv.{maxLv} / +{maxEnh} 기준)</color>\n" + FormatStats(finalStats);

        if (maxEnhanceStatsText != null)
        {
            maxEnhanceStatsText.text = finalLine;
        }
        else if (currentStatsText != null)
        {
            // 인스펙터 미연결 시 currentStatsText 하단에 합쳐서 표시
            currentStatsText.text += "\n\n" + finalLine;
        }
    }

    /// <summary>강화 배율을 스탯에 곱해 새 EquipmentStats 반환</summary>
    private EquipmentStats ApplyEnhanceMult(EquipmentStats src, float mult)
    {
        return new EquipmentStats
        {
            attack         = Mathf.RoundToInt(src.attack * mult),
            defense        = Mathf.RoundToInt(src.defense * mult),
            health         = Mathf.RoundToInt(src.health * mult),
            mana           = Mathf.RoundToInt(src.mana * mult),
            criticalRate   = Mathf.RoundToInt(src.criticalRate * mult),
            criticalDamage = src.criticalDamage * mult,
            attackSpeed    = src.attackSpeed * mult,
            moveSpeed      = src.moveSpeed * mult,
        };
    }

    private void RefreshLevelUpCost(int curLv, bool isMaxLevel)
    {
        if (isMaxLevel)
        {
            if (goldCostText != null) goldCostText.text = "-";
            if (materialCountText != null) materialCountText.text = "-";
            if (levelUpButton != null) levelUpButton.interactable = false;
            if (levelUpButtonText != null) levelUpButtonText.text = "MAX";
            return;
        }

        int goldNeeded = currentEquip.GetLevelUpGold(curLv);
        int matNeeded = currentEquip.GetRequiredMaterials(curLv);
        long goldHave = GameManager.Instance != null ? GameManager.Instance.PlayerGold : 0;
        int matHave = currentSlot.itemCount - 1; // 재료 = 동일 아이템 (보유 - 1, 1개는 남겨야 함)

        bool canAfford = goldHave >= goldNeeded && matHave >= matNeeded;

        if (goldCostText != null)
        {
            goldCostText.text = $"{goldNeeded:N0} G";
            goldCostText.color = goldHave >= goldNeeded ? Color.white : Color.red;
        }

        if (materialIcon != null && currentEquip.itemIcon != null)
            materialIcon.sprite = currentEquip.itemIcon;

        if (materialCountText != null)
        {
            materialCountText.text = $"{matHave} / {matNeeded}";
            materialCountText.color = matHave >= matNeeded ? Color.white : Color.red;
        }

        if (levelUpButton != null) levelUpButton.interactable = canAfford;
        if (levelUpButtonText != null) levelUpButtonText.text = "레벨업";
    }

    // ═══════════════════════════════════════════════════════════════
    //  레벨업
    // ═══════════════════════════════════════════════════════════════

    private void OnLevelUpClicked()
    {
        if (currentSlot == null || currentEquip == null) return;

        int curLv = currentSlot.itemLevel;
        if (curLv >= currentEquip.maxItemLevel)
        {
            UIManager.Instance?.ShowMessage("이미 최대 레벨입니다!", Color.yellow);
            return;
        }

        int goldNeeded = currentEquip.GetLevelUpGold(curLv);
        int matNeeded = currentEquip.GetRequiredMaterials(curLv);
        int matHave = currentSlot.itemCount - 1;

        // ★ 재료 먼저 검증 — 골드 차감 전에 확인하여 환불 race condition 방지
        if (matHave < matNeeded)
        {
            UIManager.Instance?.ShowMessage("재료가 부족합니다!", Color.red);
            return;
        }

        // 골드 확인 및 차감
        if (GameManager.Instance == null || !GameManager.Instance.SpendGold(goldNeeded))
        {
            UIManager.Instance?.ShowMessage("골드가 부족합니다!", Color.red);
            return;
        }

        // 재료 차감: count에서 matNeeded 만큼 감소
        currentSlot.itemCount -= matNeeded;
        currentSlot.itemLevel = curLv + 1;
        currentSlot.UpdateItemLevel(curLv + 1);

        // 해금 맵에 동기화
        InventoryManager.Instance?.SyncEquipSlotToMap(
            currentEquip.itemID, currentSlot.itemCount, currentSlot.enhanceLevel, currentSlot.itemLevel);

        SoundManager.Instance?.PlayEnhanceSuccess();
        UIManager.Instance?.ShowMessage($"{currentEquip.itemName} Lv.{curLv + 1} 달성!", Color.green);

        Debug.Log($"[EquipmentDetailPopup] 레벨업: {currentEquip.itemName} Lv.{curLv} -> Lv.{curLv + 1}");

        SaveLoadManager.Instance?.SaveGame();
        RefreshUI();
    }

    // ═══════════════════════════════════════════════════════════════
    //  장착
    // ═══════════════════════════════════════════════════════════════

    private void OnEquipClicked()
    {
        if (currentSlot == null || currentEquip == null) return;
        if (EquipmentManager.Instance == null) return;

        EquipmentManager.Instance.EquipItem(currentEquip, currentSlot.enhanceLevel);
        InventoryManager.Instance?.RemoveItem(currentEquip, 1);

        UIManager.Instance?.ShowMessage($"{currentEquip.itemName} 장착!", Color.green);
        Close();
    }

    // ═══════════════════════════════════════════════════════════════
    //  판매 시스템
    // ═══════════════════════════════════════════════════════════════

    private void ToggleSellMode()
    {
        if (currentSlot == null) return;

        // 1개만 있으면 판매 불가
        if (currentSlot.itemCount <= 1)
        {
            UIManager.Instance?.ShowMessage("마지막 1개는 판매할 수 없습니다!", Color.red);
            return;
        }

        isSellMode = !isSellMode;
        if (sellArea != null) sellArea.SetActive(isSellMode);

        if (isSellMode)
        {
            int maxSell = currentSlot.itemCount - 1; // 1개 남김
            if (sellQuantitySlider != null)
            {
                sellQuantitySlider.minValue = 1;
                sellQuantitySlider.maxValue = maxSell;
                sellQuantitySlider.value = 1;
                sellQuantitySlider.wholeNumbers = true;
            }
            UpdateSellUI(1);
        }
    }

    private void OnSellSliderChanged(float value)
    {
        UpdateSellUI(Mathf.RoundToInt(value));
    }

    private void UpdateSellUI(int quantity)
    {
        if (currentEquip == null) return;
        int priceEach = Mathf.RoundToInt(currentEquip.buyPrice * 0.5f);
        int totalPrice = priceEach * quantity;

        if (sellQuantityText != null) sellQuantityText.text = $"{quantity}개";
        if (sellPriceText != null) sellPriceText.text = $"+{totalPrice:N0} G";
    }

    private void OnSellConfirmed()
    {
        if (currentSlot == null || currentEquip == null) return;

        int quantity = sellQuantitySlider != null ? Mathf.RoundToInt(sellQuantitySlider.value) : 1;

        // 최종 검증: 판매 후 최소 1개 남아야 함
        if (currentSlot.itemCount - quantity < 1)
        {
            quantity = currentSlot.itemCount - 1;
            if (quantity <= 0)
            {
                UIManager.Instance?.ShowMessage("마지막 1개는 판매할 수 없습니다!", Color.red);
                return;
            }
        }

        int priceEach = Mathf.RoundToInt(currentEquip.buyPrice * 0.5f);
        int totalPrice = priceEach * quantity;

        GameManager.Instance?.AddGold(totalPrice);
        InventoryManager.Instance?.RemoveItem(currentEquip, quantity);

        UIManager.Instance?.ShowMessage($"{currentEquip.itemName} {quantity}개 판매! +{totalPrice:N0}G", Color.green);

        Debug.Log($"[EquipmentDetailPopup] 판매: {currentEquip.itemName} x{quantity} = {totalPrice}G");

        SaveLoadManager.Instance?.SaveGame();
        isSellMode = false;
        if (sellArea != null) sellArea.SetActive(false);

        // 슬롯 데이터가 갱신되었으므로 UI 새로고침
        if (currentSlot.itemData != null)
            RefreshUI();
        else
            Close(); // 아이템이 완전히 제거되었으면 닫기
    }

    // ═══════════════════════════════════════════════════════════════
    //  포맷 유틸
    // ═══════════════════════════════════════════════════════════════

    private string FormatStats(EquipmentStats stats)
    {
        string result = "";
        if (stats.attack > 0) result += $"공격력: {stats.attack}\n";
        if (stats.defense > 0) result += $"방어력: {stats.defense}\n";
        if (stats.health > 0) result += $"체력: {stats.health}\n";
        if (stats.mana > 0) result += $"마나: {stats.mana}\n";
        if (stats.criticalRate > 0) result += $"크리: {stats.criticalRate}%\n";
        if (stats.criticalDamage > 0) result += $"크뎀: {stats.criticalDamage:F1}x\n";
        if (stats.attackSpeed > 0) result += $"공속: {stats.attackSpeed:F1}\n";
        if (stats.moveSpeed > 0) result += $"이속: {stats.moveSpeed:F1}\n";
        return result.TrimEnd('\n');
    }

    private string FormatStatsWithDiff(EquipmentStats cur, EquipmentStats next)
    {
        string result = "";
        if (next.attack > 0) result += $"공격력: {next.attack} <color=#4CAF50>(+{next.attack - cur.attack})</color>\n";
        if (next.defense > 0) result += $"방어력: {next.defense} <color=#4CAF50>(+{next.defense - cur.defense})</color>\n";
        if (next.health > 0) result += $"체력: {next.health} <color=#4CAF50>(+{next.health - cur.health})</color>\n";
        if (next.mana > 0) result += $"마나: {next.mana} <color=#4CAF50>(+{next.mana - cur.mana})</color>\n";
        if (next.criticalRate > 0) result += $"크리: {next.criticalRate}% <color=#4CAF50>(+{next.criticalRate - cur.criticalRate})</color>\n";
        if (next.criticalDamage > 0) result += $"크뎀: {next.criticalDamage:F1}x <color=#4CAF50>(+{next.criticalDamage - cur.criticalDamage:F2})</color>\n";
        if (next.attackSpeed > 0) result += $"공속: {next.attackSpeed:F1} <color=#4CAF50>(+{next.attackSpeed - cur.attackSpeed:F2})</color>\n";
        if (next.moveSpeed > 0) result += $"이속: {next.moveSpeed:F1} <color=#4CAF50>(+{next.moveSpeed - cur.moveSpeed:F2})</color>\n";
        return result.TrimEnd('\n');
    }
}
