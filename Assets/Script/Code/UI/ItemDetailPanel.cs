using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 범용 아이템 상세 패널
///
/// 아이콘/이름/레어리티 색상/설명 표시
/// Consumable 아이템이면 "사용" 버튼 활성화
/// FarmVegetable/FarmFruit이면 "경매 등록" 버튼 활성화
/// ConsumableEffect 처리: HealthRestore / ManaRestore / AttackBuff / DefenseBuff / SpeedBuff
///
/// [프리팹 Hierarchy]
///   ItemDetailPanel (Panel + ItemDetailPanel.cs)
///   ├── Dimmer (Image, 반투명 배경, 클릭 시 닫기)
///   └── ContentPanel
///       ├── ItemIcon (Image) — 아이템 아이콘 (큰 사이즈)
///       ├── ItemName (TextMeshProUGUI) — 레어리티 색상
///       ├── RarityText (TextMeshProUGUI) — 레어리티 라벨
///       ├── DescriptionText (TextMeshProUGUI) — 아이템 설명
///       ├── EffectText (TextMeshProUGUI) — 소비 효과 미리보기
///       ├── CountText (TextMeshProUGUI) — "보유: x수량"
///       ├── UseButton (Button) — 소비 아이템일 때만 활성화
///       ├── SellButton (Button) — FarmVegetable/FarmFruit → 작물포인트 판매
///       ├── SellQuantityInput (TMP_InputField) — 판매 수량
///       ├── SellPreviewText (TextMeshProUGUI) — "획득: N 작물포인트"
///       ├── AuctionButton (Button) — FarmVegetable/FarmFruit → 경매 등록
///       ├── AuctionRegisterPanel (GameObject) — 경매 등록 서브패널
///       │   ├── StartBidInput (TMP_InputField) — 시작가
///       │   ├── BuyoutInput (TMP_InputField) — 즉시 구매가
///       │   ├── QuantityInput (TMP_InputField) — 수량
///       │   ├── FeeText (TextMeshProUGUI) — "수수료: NG (5%)"
///       │   ├── ConfirmAuctionButton (Button) — 등록 확정
///       │   └── CancelAuctionButton (Button) — 취소
///       └── CloseButton (Button)
/// </summary>
public class ItemDetailPanel : MonoBehaviour
{
    public static ItemDetailPanel Instance;

    [Header("패널")]
    public GameObject detailPanel;

    [Header("아이템 정보 표시")]
    public Image itemIcon;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI rarityText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI effectText;
    public TextMeshProUGUI countText;

    [Header("버튼")]
    public Button useButton;
    public TextMeshProUGUI useButtonText;
    public Button closeButton;

    [Header("판매 (FarmVegetable/FarmFruit → CropPoint)")]
    public Button sellButton;
    public TextMeshProUGUI sellButtonText;
    public TMP_InputField sellQuantityInput;
    public TextMeshProUGUI sellPreviewText;

    [Header("경매 등록")]
    public Button auctionButton;
    public TextMeshProUGUI auctionButtonText;
    public GameObject auctionRegisterPanel;
    public TMP_InputField startBidInput;
    public TMP_InputField buyoutInput;
    public TMP_InputField quantityInput;
    public TextMeshProUGUI feeText;
    public Button confirmAuctionButton;
    public Button cancelAuctionButton;

    // ── 레어리티별 작물 판매 단가 (CropPoint) ──
    private static readonly int[] CropSellPrices = { 10, 10, 25, 60, 150 };
    // Common=10, Uncommon=10, Rare=25, Epic=60, Legendary=150

    // ── 내부 상태 ──
    private ItemData currentItem;
    private int currentCount;
    private int currentSlotIndex;
    private bool currentIsConsumable;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] ItemDetailPanel가 생성되었습니다.");
        }
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (detailPanel != null) detailPanel.SetActive(false);
        if (auctionRegisterPanel != null) auctionRegisterPanel.SetActive(false);

        if (useButton != null) useButton.onClick.AddListener(OnUseClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        // 판매 버튼
        if (sellButton != null) sellButton.onClick.AddListener(OnSellClicked);
        if (sellQuantityInput != null) sellQuantityInput.onValueChanged.AddListener(_ => UpdateSellPreview());

        // 경매 버튼
        if (auctionButton != null) auctionButton.onClick.AddListener(OnAuctionClicked);
        if (confirmAuctionButton != null) confirmAuctionButton.onClick.AddListener(OnConfirmAuction);
        if (cancelAuctionButton != null) cancelAuctionButton.onClick.AddListener(HideAuctionRegisterPanel);

        // 수량/가격 변경 시 수수료 실시간 갱신
        if (startBidInput != null) startBidInput.onValueChanged.AddListener(_ => UpdateFeePreview());
        if (quantityInput != null) quantityInput.onValueChanged.AddListener(_ => UpdateFeePreview());
    }

    // ═══════════════════════════════════════════════════════════════
    //  열기 / 닫기
    // ═══════════════════════════════════════════════════════════════

    /// <summary>아이템 상세 패널 열기</summary>
    public void Open(ItemData item, int count, int slotIndex, bool isConsumable)
    {
        if (item == null) return;

        currentItem = item;
        currentCount = count;
        currentSlotIndex = slotIndex;
        currentIsConsumable = isConsumable;

        if (detailPanel != null) detailPanel.SetActive(true);

        RefreshUI();
    }

    public void Close()
    {
        HideAuctionRegisterPanel();
        if (detailPanel != null) detailPanel.SetActive(false);
        currentItem = null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI 갱신
    // ═══════════════════════════════════════════════════════════════

    private void RefreshUI()
    {
        if (currentItem == null) return;

        // ── 아이콘 ──
        if (itemIcon != null)
        {
            itemIcon.sprite = currentItem.itemIcon;
            itemIcon.color = Color.white;
        }

        // ── 이름 (레어리티 색상) ──
        if (itemNameText != null)
        {
            itemNameText.text = currentItem.itemName;
            itemNameText.color = currentItem.GetRarityColor();
        }

        // ── 레어리티 라벨 ──
        if (rarityText != null)
        {
            rarityText.text = GetRarityLabel(currentItem.rarity);
            rarityText.color = currentItem.GetRarityColor();
        }

        // ── 설명 ──
        if (descriptionText != null)
        {
            descriptionText.text = !string.IsNullOrEmpty(currentItem.itemDescription)
                ? currentItem.itemDescription
                : "설명 없음";
        }

        // ── 소비 효과 / 재료 정보 미리보기 ──
        if (effectText != null)
        {
            if (currentIsConsumable && currentItem.consumableEffect != null)
            {
                effectText.text = GetEffectDescription(currentItem.consumableEffect);
                effectText.gameObject.SetActive(true);
            }
            else if (currentItem.itemType == ItemType.Material)
            {
                // ★ Material 아이템 → 동료 경험치 정보 표시
                int expValue = GetMaterialExpValue(currentItem);
                effectText.text = $"동료 경험치: <color=#00FF00>+{expValue}</color>";
                effectText.gameObject.SetActive(true);
            }
            else
            {
                effectText.gameObject.SetActive(false);
            }
        }

        // ── 보유 수량 ──
        if (countText != null)
            countText.text = $"보유: x{currentCount}";

        // ── 사용 버튼 (소비 아이템만) ──
        if (useButton != null)
        {
            useButton.gameObject.SetActive(currentIsConsumable);
            useButton.interactable = currentCount > 0;
        }
        if (useButtonText != null)
            useButtonText.text = "사용";

        // ── 판매 버튼 (FarmVegetable / FarmFruit만) ──
        bool isFarmItem = currentItem != null && IsFarmAuctionable(currentItem);
        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(isFarmItem);
            sellButton.interactable = currentCount > 0;
        }
        if (isFarmItem)
        {
            int unitPrice = GetCropSellPrice(currentItem.rarity);
            if (sellButtonText != null)
                sellButtonText.text = $"판매 ({unitPrice} CP)";
            if (sellQuantityInput != null)
                sellQuantityInput.text = "1";
            UpdateSellPreview();
        }
        else
        {
            if (sellQuantityInput != null)
                sellQuantityInput.gameObject.SetActive(false);
            if (sellPreviewText != null)
                sellPreviewText.gameObject.SetActive(false);
        }

        // ── 경매 등록 버튼 (FarmVegetable / FarmFruit만) ──
        if (auctionButton != null)
        {
            auctionButton.gameObject.SetActive(isFarmItem);
            auctionButton.interactable = currentCount > 0;
        }
        if (auctionButtonText != null)
            auctionButtonText.text = "경매 등록";

        // 경매 서브패널 숨기기
        HideAuctionRegisterPanel();
    }

    // ═══════════════════════════════════════════════════════════════
    //  사용 버튼
    // ═══════════════════════════════════════════════════════════════

    private void OnUseClicked()
    {
        if (currentItem == null || !currentIsConsumable) return;
        if (currentCount <= 0) return;

        SoundManager.Instance?.PlayButtonClick();

        // ConsumableEffect 적용
        bool used = ApplyConsumableEffect(currentItem);

        if (used)
        {
            // 인벤토리에서 1개 소비
            InventoryManager.Instance?.RemoveItem(currentItem, 1);

            currentCount--;
            UIManager.Instance?.ShowMessage($"{currentItem.itemName} 사용!", Color.green);
            Debug.Log($"[ItemDetailPanel] 소비 아이템 사용: {currentItem.itemName}");
            SaveLoadManager.Instance?.SaveGame();

            if (currentCount <= 0)
            {
                Close();
                return;
            }

            // UI 갱신 (수량 변경 반영)
            RefreshUI();
        }
    }

    /// <summary>ConsumableEffect 타입별 처리</summary>
    private bool ApplyConsumableEffect(ItemData item)
    {
        if (item.consumableEffect == null) return false;
        if (PlayerStats.Instance == null)
        {
            UIManager.Instance?.ShowMessage("플레이어를 찾을 수 없습니다!", Color.red);
            return false;
        }

        ConsumableEffect effect = item.consumableEffect;
        PlayerStats ps = PlayerStats.Instance;

        switch (effect.type)
        {
            case ConsumableType.HealthRestore:
                ps.Heal(Mathf.RoundToInt(effect.value));
                UIManager.Instance?.ShowMessage($"체력 +{effect.value} 회복!", Color.green);
                return true;

            case ConsumableType.ManaRestore:
                ps.RestoreMana(effect.value);
                UIManager.Instance?.ShowMessage($"마나 +{effect.value} 회복!", Color.cyan);
                return true;

            case ConsumableType.AttackBuff:
                ps.bonusAttack += effect.value;
                ps.UpdateStatsUI();
                UIManager.Instance?.ShowMessage(
                    $"공격력 +{effect.value} ({effect.duration}초)", Color.yellow);
                if (effect.duration > 0)
                    StartCoroutine(RemoveBuffAfterDelay(
                        () => { ps.bonusAttack -= effect.value; ps.UpdateStatsUI(); },
                        effect.duration));
                return true;

            case ConsumableType.DefenseBuff:
                ps.bonusDefense += effect.value;
                ps.UpdateStatsUI();
                UIManager.Instance?.ShowMessage(
                    $"방어력 +{effect.value} ({effect.duration}초)", Color.yellow);
                if (effect.duration > 0)
                    StartCoroutine(RemoveBuffAfterDelay(
                        () => { ps.bonusDefense -= effect.value; ps.UpdateStatsUI(); },
                        effect.duration));
                return true;

            case ConsumableType.SpeedBuff:
                ps.bonusSpeed += effect.value;
                ps.UpdateStatsUI();
                UIManager.Instance?.ShowMessage(
                    $"이동속도 +{effect.value} ({effect.duration}초)", Color.yellow);
                if (effect.duration > 0)
                    StartCoroutine(RemoveBuffAfterDelay(
                        () => { ps.bonusSpeed -= effect.value; ps.UpdateStatsUI(); },
                        effect.duration));
                return true;

            default:
                Debug.LogWarning($"[ItemDetailPanel] 미처리 ConsumableType: {effect.type}");
                return false;
        }
    }

    /// <summary>지속 시간 후 버프 제거 코루틴</summary>
    private System.Collections.IEnumerator RemoveBuffAfterDelay(
        System.Action onExpire, float duration)
    {
        yield return new WaitForSeconds(duration);
        onExpire?.Invoke();
        UIManager.Instance?.ShowMessage("버프 효과가 만료되었습니다", Color.gray);
    }

    // ═══════════════════════════════════════════════════════════════
    //  판매 (FarmVegetable/FarmFruit → CropPoint)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>레어리티별 작물 판매 단가</summary>
    private int GetCropSellPrice(ItemRarity rarity)
    {
        int ri = (int)rarity;
        return ri >= 0 && ri < CropSellPrices.Length ? CropSellPrices[ri] : CropSellPrices[0];
    }

    /// <summary>판매 미리보기 갱신</summary>
    private void UpdateSellPreview()
    {
        if (currentItem == null || !IsFarmAuctionable(currentItem))
        {
            if (sellPreviewText != null) sellPreviewText.gameObject.SetActive(false);
            return;
        }

        int qty = 1;
        if (sellQuantityInput != null) int.TryParse(sellQuantityInput.text, out qty);
        qty = Mathf.Clamp(qty, 1, currentCount);

        int unitPrice = GetCropSellPrice(currentItem.rarity);
        int total = unitPrice * qty;

        if (sellPreviewText != null)
        {
            sellPreviewText.text = $"획득: {total:N0} 작물포인트 (단가 {unitPrice})";
            sellPreviewText.gameObject.SetActive(true);
        }
    }

    /// <summary>판매 버튼 클릭</summary>
    private void OnSellClicked()
    {
        if (currentItem == null || currentCount <= 0) return;
        if (!IsFarmAuctionable(currentItem)) return;

        SoundManager.Instance?.PlayButtonClick();

        // 수량 파싱
        int qty = 1;
        if (sellQuantityInput != null) int.TryParse(sellQuantityInput.text, out qty);
        qty = Mathf.Clamp(qty, 1, currentCount);

        int unitPrice = GetCropSellPrice(currentItem.rarity);
        int totalPoints = unitPrice * qty;

        // ── CropPoint 지급 (단일 source of truth) ──
        CropPointService.Add(totalPoints);
        // ★ 즉시 저장 — 씬 전환 없이 사용해도 데이터 보존
        SaveLoadManager.Instance?.SaveGame();

        // ── 인벤토리에서 차감 ──
        InventoryManager.Instance?.RemoveItem(currentItem, qty);

        currentCount -= qty;

        UIManager.Instance?.ShowMessage(
            $"{currentItem.itemName} x{qty} 판매!\n+{totalPoints:N0} 작물포인트",
            new Color(0.4f, 0.8f, 0.2f));

        Debug.Log($"[ItemDetailPanel] 작물 판매: {currentItem.itemName} x{qty} → +{totalPoints} CropPoint");

        // ── 인벤토리 UI 갱신 ──
        InventoryManager.Instance?.RefreshInventoryUI();

        if (currentCount <= 0)
        {
            Close();
            return;
        }

        RefreshUI();
    }

    // ═══════════════════════════════════════════════════════════════
    //  경매 등록
    // ═══════════════════════════════════════════════════════════════

    /// <summary>FarmVegetable/FarmFruit인지 판별</summary>
    private bool IsFarmAuctionable(ItemData item)
    {
        return item.itemType == ItemType.FarmVegetable ||
               item.itemType == ItemType.FarmFruit;
    }

    /// <summary>"경매 등록" 버튼 클릭 → 등록 서브패널 열기</summary>
    private void OnAuctionClicked()
    {
        if (currentItem == null || currentCount <= 0) return;

        SoundManager.Instance?.PlayButtonClick();

        if (AuctionManager.Instance == null)
        {
            UIManager.Instance?.ShowMessage("경매장을 찾을 수 없습니다!", Color.red);
            return;
        }

        ShowAuctionRegisterPanel();
    }

    /// <summary>경매 등록 서브패널 표시 (기본값 세팅)</summary>
    private void ShowAuctionRegisterPanel()
    {
        if (auctionRegisterPanel != null)
            auctionRegisterPanel.SetActive(true);

        // 기본값: 시작가 = buyPrice, 즉시구매 = buyPrice*3, 수량 = 1
        int defaultPrice = currentItem.buyPrice > 0 ? currentItem.buyPrice : 100;

        if (startBidInput != null) startBidInput.text = defaultPrice.ToString();
        if (buyoutInput != null) buyoutInput.text = (defaultPrice * 3).ToString();
        if (quantityInput != null) quantityInput.text = "1";

        UpdateFeePreview();
    }

    /// <summary>경매 등록 서브패널 숨기기</summary>
    private void HideAuctionRegisterPanel()
    {
        if (auctionRegisterPanel != null)
            auctionRegisterPanel.SetActive(false);
    }

    /// <summary>수수료 실시간 미리보기 갱신</summary>
    private void UpdateFeePreview()
    {
        if (feeText == null) return;

        int startBid = 0;
        int qty = 1;
        if (startBidInput != null) int.TryParse(startBidInput.text, out startBid);
        if (quantityInput != null) int.TryParse(quantityInput.text, out qty);

        qty = Mathf.Clamp(qty, 1, currentCount);

        float feePercent = AuctionManager.Instance != null
            ? AuctionManager.Instance.ListingFeePercent : 5f;
        int fee = Mathf.Max(1, Mathf.RoundToInt(startBid * qty * feePercent / 100f));

        feeText.text = $"수수료: {fee:N0}G ({feePercent:0}%)";
    }

    /// <summary>경매 등록 확정</summary>
    private void OnConfirmAuction()
    {
        if (currentItem == null || currentCount <= 0) return;

        SoundManager.Instance?.PlayButtonClick();

        // 입력값 파싱
        int startBid = 100;
        int buyout = 300;
        int qty = 1;

        if (startBidInput != null) int.TryParse(startBidInput.text, out startBid);
        if (buyoutInput != null) int.TryParse(buyoutInput.text, out buyout);
        if (quantityInput != null) int.TryParse(quantityInput.text, out qty);

        // 수량 범위 제한
        qty = Mathf.Clamp(qty, 1, currentCount);

        // 가격 검증
        if (startBid <= 0)
        {
            UIManager.Instance?.ShowMessage("시작가를 입력하세요!", Color.red);
            return;
        }
        if (buyout > 0 && buyout <= startBid)
        {
            UIManager.Instance?.ShowMessage("즉시 구매가는 시작가보다 높아야 합니다!", Color.red);
            return;
        }

        // AuctionManager.CreateAuction 호출
        // → 내부에서 5% 수수료 차감 + 인벤토리 아이템 제거 처리
        float duration = AuctionManager.Instance.AvailableDurations.Length > 0
            ? AuctionManager.Instance.AvailableDurations[0] : 600f;

        bool success = AuctionManager.Instance.CreateAuction(
            currentItem, qty, startBid, buyout, duration);

        if (success)
        {
            currentCount -= qty;

            // 인벤토리 UI 갱신
            InventoryManager.Instance?.RefreshInventoryUI();

            if (currentCount <= 0)
            {
                Close();
                return;
            }

            HideAuctionRegisterPanel();
            RefreshUI();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  유틸
    // ═══════════════════════════════════════════════════════════════

    /// <summary>소비 효과 설명 문자열 생성</summary>
    private string GetEffectDescription(ConsumableEffect effect)
    {
        if (effect == null) return "";

        string desc = "";
        switch (effect.type)
        {
            case ConsumableType.HealthRestore:
                desc = $"<color=#4CAF50>체력 {effect.value} 회복</color>";
                break;
            case ConsumableType.ManaRestore:
                desc = $"<color=#2196F3>마나 {effect.value} 회복</color>";
                break;
            case ConsumableType.AttackBuff:
                desc = $"<color=#FF9800>공격력 +{effect.value}</color>";
                if (effect.duration > 0) desc += $" ({effect.duration}초)";
                break;
            case ConsumableType.DefenseBuff:
                desc = $"<color=#FF9800>방어력 +{effect.value}</color>";
                if (effect.duration > 0) desc += $" ({effect.duration}초)";
                break;
            case ConsumableType.SpeedBuff:
                desc = $"<color=#FF9800>이동속도 +{effect.value}</color>";
                if (effect.duration > 0) desc += $" ({effect.duration}초)";
                break;
        }
        return desc;
    }

    /// <summary>레어리티 한국어 라벨</summary>
    private string GetRarityLabel(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common:    return "일반";
            case ItemRarity.Uncommon:  return "고급";
            case ItemRarity.Rare:      return "희귀";
            case ItemRarity.Epic:      return "영웅";
            case ItemRarity.Legendary: return "전설";
            default:                   return rarity.ToString();
        }
    }

    /// <summary>★ 동료 경험치 재료 아이템의 경험치 값</summary>
    private int GetMaterialExpValue(ItemData item)
    {
        if (item == null) return 0;
        return item.rarity switch
        {
            ItemRarity.Common    => 50,
            ItemRarity.Uncommon  => 150,
            ItemRarity.Rare      => 500,
            ItemRarity.Epic      => 1500,
            ItemRarity.Legendary => 5000,
            _                    => 50
        };
    }
}
