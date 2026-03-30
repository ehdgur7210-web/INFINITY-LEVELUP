using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 경매장 UI
/// 
/// 탭 구성:
///   [경매 목록] - 전체 경매 검색/필터/입찰/즉시구매
///   [출품하기]  - 인벤토리에서 아이템 선택 → 경매 등록
///   [내 경매]   - 내가 올린 경매 + 내 입찰 현황
///   [기록]      - 낙찰/유찰 히스토리
///
/// ── SellBidPopUp 히어라키 매핑 ──
/// SellBidPopUp (registerPopup)
///  ├─ Image
///  │   ├─ Image (아이콘 영역)
///  │   │   ├─ Image (1)       → regItemIcon
///  │   │   └─ Name            → regItemName
///  │   ├─ Image (1) (정보 영역)
///  │   │   ├─ Text (TMP)      → regMaxPriceText   (최대값)
///  │   │   ├─ Text (TMP) (1)  → regMinPriceText   (최소값)
///  │   │   └─ Text (TMP) (2)  → regQuantityInfo   (보유 수량)
///  │   ├─ 등록                → regConfirmBtn
///  │   ├─ 취소                → regCancelBtn
///  │   ├─ InputField (TMP)    → regStartPriceInput  (시작가)
///  │   ├─ InputField (TMP)(1) → regBuyoutInput      (즉시구매가)
///  │   ├─ InputField (TMP)(2) → regQuantityInput    (수량)
///  │   ├─ Text (TMP)          → regDurationLabel    (경매시간 라벨)
///  │   └─ Dropdown            → regDurationDropdown (경매시간)
/// </summary>
public class AuctionUI : MonoBehaviour
{
    public static AuctionUI Instance;

    // ───────── 메인 패널 ─────────
    [Header("메인")]
    [SerializeField] private GameObject auctionPanel;
    [SerializeField] private TextMeshProUGUI playerGoldText;
    [SerializeField] private Button closeButton;

    // ───────── 탭 버튼 ─────────
    [Header("탭")]
    [SerializeField] private Button browseTabBtn;
    [SerializeField] private Button sellTabBtn;
    [SerializeField] private Button myTabBtn;
    [SerializeField] private Button historyTabBtn;

    [SerializeField] private GameObject browseContent;
    [SerializeField] private GameObject sellContent;
    [SerializeField] private GameObject myContent;
    [SerializeField] private GameObject historyContent;

    // ───────── 경매 목록 탭 ─────────
    [Header("경매 목록 (Browse)")]
    [SerializeField] private Transform auctionListParent;
    [SerializeField] private GameObject auctionSlotPrefab;
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private TMP_Dropdown typeDropdown;
    [SerializeField] private TMP_Dropdown sortDropdown;
    [SerializeField] private Button searchBtn;
    [SerializeField] private TextMeshProUGUI listingCountText;

    // ───────── 입찰 팝업 ─────────
    [Header("입찰 팝업")]
    [SerializeField] private GameObject bidPopup;
    [SerializeField] private Image bidItemIcon;
    [SerializeField] private TextMeshProUGUI bidItemName;
    [SerializeField] private TextMeshProUGUI bidItemDesc;
    [SerializeField] private TextMeshProUGUI bidCurrentPrice;
    [SerializeField] private TextMeshProUGUI bidMinAmount;
    [SerializeField] private TextMeshProUGUI bidBuyoutPrice;
    [SerializeField] private TextMeshProUGUI bidSellerName;
    [SerializeField] private TextMeshProUGUI bidTimeRemaining;
    [SerializeField] private TextMeshProUGUI bidBidCount;
    [SerializeField] private TMP_InputField bidAmountInput;
    [SerializeField] private Button bidConfirmBtn;
    [SerializeField] private Button buyoutBtn;
    [SerializeField] private Button bidCloseBtn;

    // ───────── 출품 탭 ─────────
    [Header("출품하기 (Sell)")]
    [SerializeField] private Transform sellItemListParent;
    [SerializeField] private GameObject sellItemSlotPrefab;

    // ───────── 출품 등록 팝업 (SellBidPopUp) ─────────
    [Header("출품 등록 팝업 (SellBidPopUp)")]
    [SerializeField] private GameObject registerPopup;

    [Header("  아이템 정보 (Image 하위)")]
    [Tooltip("아이콘 - Image > Image > Image(1)")]
    [SerializeField] private Image regItemIcon;
    [Tooltip("이름 - Image > Image > Name")]
    [SerializeField] private TextMeshProUGUI regItemName;

    [Header("  아이템 상세 (Image(1) 하위)")]
    [Tooltip("최대값 - Text (TMP)")]
    [SerializeField] private TextMeshProUGUI regMaxPriceText;
    [Tooltip("최소값 - Text (TMP) (1)")]
    [SerializeField] private TextMeshProUGUI regMinPriceText;
    [Tooltip("보유 수량 - Text (TMP) (2)")]
    [SerializeField] private TextMeshProUGUI regQuantityInfo;

    [Header("  입력 필드")]
    [Tooltip("시작가 - InputField (TMP)")]
    [SerializeField] private TMP_InputField regStartPriceInput;
    [Tooltip("즉시구매가 - InputField (TMP) (1)")]
    [SerializeField] private TMP_InputField regBuyoutInput;
    [Tooltip("수량 - InputField (TMP) (2)")]
    [SerializeField] private TMP_InputField regQuantityInput;

    [Header("  경매시간")]
    [Tooltip("경매시간 라벨 (Dropdown 왼쪽) - Text (TMP)")]
    [SerializeField] private TextMeshProUGUI regDurationLabel;
    [Tooltip("경매시간 드롭다운 - Dropdown")]
    [SerializeField] private TMP_Dropdown regDurationDropdown;

    [Header("  수수료 & 버튼")]
    [SerializeField] private TextMeshProUGUI regFeeText;
    [Tooltip("등록 버튼")]
    [SerializeField] private Button regConfirmBtn;
    [Tooltip("취소 버튼")]
    [SerializeField] private Button regCancelBtn;

    // ───────── 내 경매 탭 ─────────
    [Header("내 경매 (My)")]
    [SerializeField] private Transform myAuctionParent;
    [SerializeField] private GameObject myAuctionSlotPrefab;
    [SerializeField] private Transform myBidParent;
    [SerializeField] private GameObject myBidSlotPrefab;

    // ───────── 기록 탭 ─────────
    [Header("기록 (History)")]
    [SerializeField] private Transform historyParent;
    [SerializeField] private GameObject historySlotPrefab;

    // ───────── 내부 상태 ─────────
    private int currentTab = 0;
    private int selectedAuctionID = -1;
    private ItemData selectedSellItem = null;
    private int selectedSellItemOwnedQty = 0;
    private float uiRefreshTimer = 0f;
    private const float UI_REFRESH_INTERVAL = 1f;

    // ✅ 스크롤 위치 유지용
    [Header("스크롤뷰 참조")]
    [SerializeField] private ScrollRect auctionScrollRect; // auctionListParent의 부모 ScrollRect

    // ══════════════════════════════════════
    //  초기화
    // ══════════════════════════════════════

    void Awake()
    {
        // ★ 씬 소속 컴포넌트 = 씬 재로드마다 새로 생성됨
        // 이전 Instance는 파괴된 오브젝트 → 나(새 오브젝트)로 교체
        Instance = this;
    }

    void Start()
    {
        SetupButtons();
        SetupDropdowns();

        AuctionManager.OnAuctionsUpdated += OnAuctionsUpdated;
        AuctionManager.OnPlayerOutbid += OnPlayerOutbid;
        GameManager.OnGoldChanged += UpdateGold;

        if (auctionPanel != null) auctionPanel.SetActive(false);
        if (bidPopup != null) bidPopup.SetActive(false);
        if (registerPopup != null) registerPopup.SetActive(false);
    }

    void OnDestroy()
    {
        AuctionManager.OnAuctionsUpdated -= OnAuctionsUpdated;
        AuctionManager.OnPlayerOutbid -= OnPlayerOutbid;
        GameManager.OnGoldChanged -= UpdateGold;
    }

    void Update()
    {
        if (auctionPanel != null && auctionPanel.activeSelf && currentTab == 0)
        {
            uiRefreshTimer += Time.deltaTime;
            if (uiRefreshTimer >= UI_REFRESH_INTERVAL)
            {
                uiRefreshTimer = 0f;
                // ✅ 전체 재생성(스크롤 초기화) 대신 시간 텍스트만 업데이트
                RefreshAuctionTimers();
            }
        }

        if (bidPopup != null && bidPopup.activeSelf && selectedAuctionID >= 0)
        {
            UpdateBidPopupTimer();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleAuction();
        }
    }

    /// <summary>
    /// ✅ 스크롤 위치 유지 - 기존 슬롯의 시간/가격 텍스트만 갱신
    /// 전체 재생성 없이 남은 시간 표시만 업데이트
    /// </summary>
    private void RefreshAuctionTimers()
    {
        if (auctionListParent == null || AuctionManager.Instance == null) return;

        List<AuctionListing> auctions = AuctionManager.Instance.GetActiveAuctions("", null, null, AuctionSortType.EndingSoon);

        int i = 0;
        foreach (Transform child in auctionListParent)
        {
            if (i >= auctions.Count) break;
            AuctionListing auction = auctions[i];

            // 남은 시간 텍스트만 갱신
            Transform timeTr = child.Find("RemainingTime");
            if (timeTr != null)
            {
                TextMeshProUGUI timeText = timeTr.GetComponent<TextMeshProUGUI>();
                if (timeText != null)
                {
                    TimeSpan remaining = auction.GetRemainingTime();
                    timeText.text = auction.GetRemainingTimeString();
                    timeText.color = remaining.TotalMinutes < 5 ? Color.red : Color.white;
                }
            }

            // 현재 입찰가 갱신
            Transform priceTr = child.Find("CurrentBid");
            if (priceTr != null)
            {
                TextMeshProUGUI priceText = priceTr.GetComponent<TextMeshProUGUI>();
                if (priceText != null)
                {
                    priceText.text = auction.currentBid > 0
                        ? $"현재가: {auction.currentBid:N0}G"
                        : $"시작가: {auction.startingBid:N0}G";
                }
            }

            i++;
        }
    }

    private void SetupButtons()
    {
        if (closeButton != null) closeButton.onClick.AddListener(CloseAuction);

        if (browseTabBtn != null) browseTabBtn.onClick.AddListener(() => SwitchTab(0));
        if (sellTabBtn != null) sellTabBtn.onClick.AddListener(() => SwitchTab(1));
        if (myTabBtn != null) myTabBtn.onClick.AddListener(() => SwitchTab(2));
        if (historyTabBtn != null) historyTabBtn.onClick.AddListener(() => SwitchTab(3));

        if (searchBtn != null) searchBtn.onClick.AddListener(() => RefreshBrowseTab());

        if (bidConfirmBtn != null) bidConfirmBtn.onClick.AddListener(OnBidConfirm);
        if (buyoutBtn != null) buyoutBtn.onClick.AddListener(OnBuyout);
        if (bidCloseBtn != null) bidCloseBtn.onClick.AddListener(() => bidPopup?.SetActive(false));

        if (regConfirmBtn != null) regConfirmBtn.onClick.AddListener(OnRegisterConfirm);
        if (regCancelBtn != null) regCancelBtn.onClick.AddListener(() => registerPopup?.SetActive(false));

        if (regStartPriceInput != null) regStartPriceInput.onValueChanged.AddListener(_ => UpdateRegFee());
        if (regQuantityInput != null) regQuantityInput.onValueChanged.AddListener(_ => OnRegQuantityChanged());
    }

    private void SetupDropdowns()
    {
        if (typeDropdown != null)
        {
            typeDropdown.ClearOptions();
            typeDropdown.AddOptions(new List<string>
                { "전체", "무기", "방어구", "악세서리", "소비", "재료", "퀘스트", "화폐", "기타", "장비" });
            typeDropdown.onValueChanged.AddListener(_ => RefreshBrowseTab());
        }

        if (sortDropdown != null)
        {
            sortDropdown.ClearOptions();
            sortDropdown.AddOptions(new List<string>
                { "마감 임박순", "최신순", "가격 낮은순", "가격 높은순", "등급 높은순", "입찰 많은순" });
            sortDropdown.onValueChanged.AddListener(_ => RefreshBrowseTab());
        }

        if (regDurationDropdown != null && AuctionManager.Instance != null)
        {
            regDurationDropdown.ClearOptions();
            List<string> options = new List<string>();
            foreach (float sec in AuctionManager.Instance.AvailableDurations)
            {
                if (sec >= 3600) options.Add($"{sec / 3600f}시간");
                else options.Add($"{sec / 60f}분");
            }
            regDurationDropdown.AddOptions(options);
        }
    }

    // ══════════════════════════════════════
    //  탭 전환
    // ══════════════════════════════════════

    private void SwitchTab(int tab)
    {
        currentTab = tab;

        if (browseContent != null) browseContent.SetActive(tab == 0);
        if (sellContent != null) sellContent.SetActive(tab == 1);
        if (myContent != null) myContent.SetActive(tab == 2);
        if (historyContent != null) historyContent.SetActive(tab == 3);

        switch (tab)
        {
            case 0: RefreshBrowseTab(); break;
            case 1: RefreshSellTab(); break;
            case 2: RefreshMyTab(); break;
            case 3: RefreshHistoryTab(); break;
        }

        UpdateGold(GameManager.Instance != null ? GameManager.Instance.PlayerGold : 0);
    }

    // ══════════════════════════════════════
    //  경매 목록 탭
    // ══════════════════════════════════════

    private void RefreshBrowseTab()
    {
        // ✅ 스크롤 위치 저장
        float savedScrollY = auctionScrollRect != null ? auctionScrollRect.verticalNormalizedPosition : 1f;

        ClearChildren(auctionListParent);
        if (AuctionManager.Instance == null) return;

        string search = searchInput != null ? searchInput.text : "";
        ItemType? typeFilter = null;
        if (typeDropdown != null && typeDropdown.value > 0)
            typeFilter = (ItemType)(typeDropdown.value - 1);

        AuctionSortType sort = AuctionSortType.EndingSoon;
        if (sortDropdown != null)
            sort = (AuctionSortType)sortDropdown.value;

        List<AuctionListing> auctions = AuctionManager.Instance.GetActiveAuctions(search, typeFilter, null, sort);

        if (listingCountText != null)
            listingCountText.text = $"경매 {auctions.Count}건";

        foreach (var auction in auctions)
        {
            CreateAuctionSlot(auction);
        }

        // ✅ 탭 전환 시에만 맨 위로, 아닌 경우 스크롤 위치 복원
        if (auctionScrollRect != null)
        {
            // 한 프레임 뒤에 복원 (레이아웃 계산 완료 후)
            StartCoroutine(RestoreScrollPosition(savedScrollY));
        }
    }

    private System.Collections.IEnumerator RestoreScrollPosition(float pos)
    {
        yield return null; // 한 프레임 대기 (ContentSizeFitter 계산 완료)
        if (auctionScrollRect != null)
            auctionScrollRect.verticalNormalizedPosition = pos;
    }

    private void CreateAuctionSlot(AuctionListing auction)
    {
        if (auctionSlotPrefab == null || auctionListParent == null) return;

        GameObject slot = Instantiate(auctionSlotPrefab, auctionListParent);

        SetChildImage(slot, "Icon", auction.item.itemIcon);
        SetChildColor(slot, "RarityBorder", auction.item.GetRarityColor());
        SetChildText(slot, "ItemName", auction.item.itemName, auction.item.GetRarityColor());
        SetChildText(slot, "Quantity", auction.quantity > 1 ? $"x{auction.quantity}" : "");

        string priceStr = auction.currentBid > 0
            ? $"현재가: {auction.currentBid:N0}G"
            : $"시작가: {auction.startingBid:N0}G";
        SetChildText(slot, "CurrentBid", priceStr);

        if (auction.buyoutPrice > 0)
            SetChildText(slot, "BuyoutPrice", $"즉구: {auction.buyoutPrice:N0}G");
        else
            SetChildText(slot, "BuyoutPrice", "");

        string timeStr = auction.GetRemainingTimeString();
        TimeSpan remaining = auction.GetRemainingTime();
        Color timeColor = remaining.TotalMinutes < 5 ? Color.red : Color.white;
        SetChildText(slot, "RemainingTime", timeStr, timeColor);

        SetChildText(slot, "BidCount", $"입찰 {auction.bids.Count}");
        SetChildText(slot, "Seller", auction.sellerName);

        if (auction.IsPlayerTopBidder())
            SetChildText(slot, "MyBidStatus", "★ 최고 입찰 중", Color.green);
        else if (auction.bids.Exists(b => b.isPlayer))
            SetChildText(slot, "MyBidStatus", "⚠ 밀림!", Color.red);

        Button btn = slot.GetComponent<Button>();
        if (btn != null)
        {
            int id = auction.auctionID;
            btn.onClick.AddListener(() => OpenBidPopup(id));
        }
    }

    // ══════════════════════════════════════
    //  입찰 팝업
    // ══════════════════════════════════════

    private void OpenBidPopup(int auctionID)
    {
        if (AuctionManager.Instance == null) return;

        AuctionListing auction = AuctionManager.Instance.FindAuction(auctionID);
        if (auction == null || !auction.isActive) return;

        selectedAuctionID = auctionID;

        if (bidPopup != null) bidPopup.SetActive(true);

        if (bidItemIcon != null && auction.item.itemIcon != null)
            bidItemIcon.sprite = auction.item.itemIcon;
        if (bidItemName != null)
        {
            bidItemName.text = $"{auction.item.itemName}" + (auction.quantity > 1 ? $" x{auction.quantity}" : "");
            bidItemName.color = auction.item.GetRarityColor();
        }
        if (bidItemDesc != null)
            bidItemDesc.text = auction.item.itemDescription;

        if (bidCurrentPrice != null)
        {
            string p = auction.currentBid > 0
                ? $"현재 최고가: {auction.currentBid:N0}G"
                : $"시작가: {auction.startingBid:N0}G";
            bidCurrentPrice.text = p;
        }

        int minBid = AuctionManager.Instance.GetMinimumBid(auction);
        if (bidMinAmount != null)
            bidMinAmount.text = $"최소 입찰: {minBid:N0}G";

        if (bidBuyoutPrice != null)
        {
            bidBuyoutPrice.text = auction.buyoutPrice > 0
                ? $"즉시 구매: {auction.buyoutPrice:N0}G"
                : "즉시 구매 없음";
        }

        if (buyoutBtn != null)
            buyoutBtn.interactable = auction.buyoutPrice > 0 && !auction.sellerIsPlayer;

        if (bidSellerName != null)
            bidSellerName.text = $"판매자: {auction.sellerName}";

        UpdateBidPopupTimer();

        if (bidBidCount != null)
            bidBidCount.text = $"입찰 {auction.bids.Count}회";

        if (bidAmountInput != null)
            bidAmountInput.text = minBid.ToString();

        if (bidConfirmBtn != null)
            bidConfirmBtn.interactable = !auction.sellerIsPlayer;
    }

    private void UpdateBidPopupTimer()
    {
        if (AuctionManager.Instance == null || selectedAuctionID < 0) return;

        AuctionListing auction = AuctionManager.Instance.FindAuction(selectedAuctionID);
        if (auction == null) return;

        if (bidTimeRemaining != null)
        {
            TimeSpan t = auction.GetRemainingTime();
            bidTimeRemaining.text = $"남은 시간: {auction.GetRemainingTimeString()}";
            bidTimeRemaining.color = t.TotalMinutes < 5 ? Color.red : Color.white;
        }
    }

    private void OnBidConfirm()
    {
        if (AuctionManager.Instance == null || selectedAuctionID < 0) return;

        int amount = 0;
        if (bidAmountInput != null)
            int.TryParse(bidAmountInput.text, out amount);

        bool success = AuctionManager.Instance.PlaceBid(selectedAuctionID, amount);
        if (success)
        {
            // ★ 경매장 입찰 효과음
            SoundManager.Instance?.PlayAuctionBid();
            OpenBidPopup(selectedAuctionID);
        }
    }

    private void OnBuyout()
    {
        if (AuctionManager.Instance == null || selectedAuctionID < 0) return;

        // ★ 경매장 즉시구매 효과음
        SoundManager.Instance?.PlayAuctionBuyout();
        AuctionManager.Instance.BuyoutAuction(selectedAuctionID);
        bidPopup?.SetActive(false);
    }

    // ══════════════════════════════════════
    //  출품하기 탭
    // ══════════════════════════════════════

    private void RefreshSellTab()
    {
        ClearChildren(sellItemListParent);
        if (InventoryManager.Instance == null) return;

        List<ItemData> allItems = InventoryManager.Instance.GetAllItems();

        Debug.Log($"======= 판매탭 아이템 목록 =======");
        Debug.Log($"총 슬롯 아이템 수: {allItems.Count}");
        for (int i = 0; i < allItems.Count; i++)
        {
            Debug.Log($"  [{i}] {allItems[i].itemName} (ID: {allItems[i].itemID})");
        }
        Debug.Log($"==================================");

        if (allItems == null || allItems.Count == 0)
        {
            Debug.Log("인벤토리 비어있음");
            return;
        }

        foreach (ItemData item in allItems)
        {
            if (item == null) continue;
            int count = InventoryManager.Instance.GetItemCount(item);
            Debug.Log($"슬롯 생성 시도: {item.itemName} x{count}");
            CreateSellItemSlot(item, count);
        }
    }

    private void CreateSellItemSlot(ItemData item, int count)
    {
        if (sellItemSlotPrefab == null || sellItemListParent == null) return;

        GameObject slot = Instantiate(sellItemSlotPrefab, sellItemListParent);

        Transform iconTr = slot.transform.Find("Icon");
        if (iconTr != null && item.itemIcon != null)
        {
            Image icon = iconTr.GetComponent<Image>();
            if (icon != null) icon.sprite = item.itemIcon;
        }

        Transform textTr = slot.transform.Find("Text (TMP)");
        if (textTr != null)
        {
            TextMeshProUGUI text = textTr.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = $"{item.itemName}  <color=#aaa>x{count}</color>\n<size=80%>참고가: {item.buyPrice:N0}G</size>";
                text.color = item.GetRarityColor();
            }
        }

        Button btn = slot.GetComponent<Button>();
        if (btn != null)
        {
            ItemData captured = item;
            int capturedCount = count;
            btn.onClick.AddListener(() => OpenRegisterPopup(captured, capturedCount));
        }
    }

    // ══════════════════════════════════════
    //  출품 등록 팝업 (SellBidPopUp)
    // ══════════════════════════════════════

    /// <summary>
    /// 등록 팝업 열기
    /// </summary>
    private void OpenRegisterPopup(ItemData item, int ownedQuantity)
    {
        selectedSellItem = item;
        selectedSellItemOwnedQty = ownedQuantity;

        if (registerPopup != null)
            registerPopup.SetActive(true);

        // ── 아이콘 & 이름 ──
        if (regItemIcon != null && item.itemIcon != null)
            regItemIcon.sprite = item.itemIcon;

        if (regItemName != null)
        {
            regItemName.text = item.itemName;
            regItemName.color = item.GetRarityColor();
        }

        // ── ★ 최대값 / 최소값 / 보유수량 표시 ──
        int baseValue = item.buyPrice;
        int minValue = Mathf.Max(1, Mathf.RoundToInt(baseValue * 0.5f));
        int maxValue = Mathf.RoundToInt(baseValue * 2.5f);

        if (regMaxPriceText != null)
            regMaxPriceText.text = $"최대값: {maxValue:N0}G";

        if (regMinPriceText != null)
            regMinPriceText.text = $"최소값: {minValue:N0}G";

        if (regQuantityInfo != null)
            regQuantityInfo.text = $"보유: {ownedQuantity}개";

        // ── 입력 필드 기본값 ──
        if (regStartPriceInput != null)
            regStartPriceInput.text = baseValue.ToString();

        if (regBuyoutInput != null)
            regBuyoutInput.text = (baseValue * 2).ToString();

        if (regQuantityInput != null)
            regQuantityInput.text = "1";

        // ── ★ 경매시간 라벨 ──
        if (regDurationLabel != null)
            regDurationLabel.text = "경매시간";

        // ── 경매시간 드롭다운 ──
        if (regDurationDropdown != null) regDurationDropdown.value = 1;

        // ── 수수료 갱신 ──
        UpdateRegFee();
    }

    /// <summary>
    /// ★ 수량 입력 변경 → 보유량 초과 방지 + 보유수량 텍스트 갱신
    /// </summary>
    private void OnRegQuantityChanged()
    {
        if (regQuantityInput == null) return;

        int qty = 1;
        int.TryParse(regQuantityInput.text, out qty);

        // 보유 수량 초과 클램프
        if (qty > selectedSellItemOwnedQty)
        {
            qty = selectedSellItemOwnedQty;
            regQuantityInput.text = qty.ToString();
        }
        if (qty < 1)
        {
            qty = 1;
            regQuantityInput.text = "1";
        }

        // ★ 보유수량 텍스트 갱신
        if (regQuantityInfo != null)
            regQuantityInfo.text = $"보유: {selectedSellItemOwnedQty}개 (등록: {qty}개)";

        UpdateRegFee();
    }

    private void UpdateRegFee()
    {
        if (regFeeText == null || AuctionManager.Instance == null) return;

        int price = 0, qty = 1;
        if (regStartPriceInput != null) int.TryParse(regStartPriceInput.text, out price);
        if (regQuantityInput != null) int.TryParse(regQuantityInput.text, out qty);

        int fee = AuctionManager.Instance.CalculateListingFee(price, qty);
        regFeeText.text = $"등록 수수료: {fee:N0}G  |  낙찰 수수료: {AuctionManager.Instance.SaleTaxPercent}%";
    }

    private void OnRegisterConfirm()
    {
        Debug.Log($"[등록버튼] selectedSellItem: {selectedSellItem?.itemName ?? "NULL"}");
        if (selectedSellItem == null || AuctionManager.Instance == null) return;

        int startPrice = 0, buyout = 0, qty = 1;
        if (regStartPriceInput != null) int.TryParse(regStartPriceInput.text, out startPrice);
        if (regBuyoutInput != null) int.TryParse(regBuyoutInput.text, out buyout);
        if (regQuantityInput != null) int.TryParse(regQuantityInput.text, out qty);

        // ★ 수량 검증
        if (qty <= 0 || qty > selectedSellItemOwnedQty)
        {
            Debug.LogWarning($"[등록] 수량 오류: {qty} (보유: {selectedSellItemOwnedQty})");
            return;
        }

        // ★ 즉구가 검증
        if (buyout > 0 && buyout <= startPrice)
        {
            Debug.LogWarning("[등록] 즉시구매가는 시작가보다 높아야 합니다!");
            return;
        }

        float duration = 600f;
        if (regDurationDropdown != null && AuctionManager.Instance.AvailableDurations.Length > 0)
        {
            int idx = Mathf.Clamp(regDurationDropdown.value, 0, AuctionManager.Instance.AvailableDurations.Length - 1);
            duration = AuctionManager.Instance.AvailableDurations[idx];
        }

        startPrice = Mathf.Max(1, startPrice);
        qty = Mathf.Max(1, qty);

        Debug.Log($"[등록] {selectedSellItem.itemName} | 시작가:{startPrice} | 즉구:{buyout} | 수량:{qty} | 기간:{duration}");

        bool success = AuctionManager.Instance.CreateAuction(selectedSellItem, qty, startPrice, buyout, duration);
        Debug.Log($"[등록결과] {success}");

        if (success)
        {
            // ★ 경매장 출품 등록 효과음
            SoundManager.Instance?.PlayAuctionRegister();
            registerPopup?.SetActive(false);
            RefreshSellTab();
        }
    }

    // ══════════════════════════════════════
    //  내 경매 탭
    // ══════════════════════════════════════

    private void RefreshMyTab()
    {
        if (AuctionManager.Instance == null) return;

        // ── 내 출품 목록 ──
        ClearChildren(myAuctionParent);
        List<AuctionListing> myAuctions = AuctionManager.Instance.GetMyAuctions();

        foreach (var auction in myAuctions)
        {
            if (myAuctionSlotPrefab == null || myAuctionParent == null) break;

            GameObject slot = Instantiate(myAuctionSlotPrefab, myAuctionParent);
            TextMeshProUGUI text = slot.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                string status = auction.isActive
                    ? $"현재가: {auction.GetCurrentPrice():N0}G | {auction.GetRemainingTimeString()} | 입찰 {auction.bids.Count}"
                    : auction.result == AuctionResult.Sold
                        ? $"<color=green>낙찰: {auction.finalPrice:N0}G → {auction.winnerName}</color>"
                        : $"<color=#888>{auction.result}</color>";

                text.text = $"{auction.item.itemName} x{auction.quantity}\n{status}";
                text.color = auction.item.GetRarityColor();
            }

            Button cancelBtn = slot.transform.Find("CancelButton")?.GetComponent<Button>();
            if (cancelBtn != null)
            {
                int id = auction.auctionID;
                bool canCancel = auction.isActive && auction.bids.Count == 0;
                cancelBtn.interactable = canCancel;
                cancelBtn.onClick.AddListener(() =>
                {
                    AuctionManager.Instance.CancelAuction(id);
                    RefreshMyTab();
                });
            }
        }

        // ── 내 입찰 목록 ──
        ClearChildren(myBidParent);
        List<AuctionListing> myBids = AuctionManager.Instance.GetMyBids();

        foreach (var auction in myBids)
        {
            if (myBidSlotPrefab == null || myBidParent == null) break;

            GameObject slot = Instantiate(myBidSlotPrefab, myBidParent);
            TextMeshProUGUI text = slot.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                bool isTop = auction.IsPlayerTopBidder();
                AuctionBid myBid = auction.bids.FindLast(b => b.isPlayer);
                int myAmount = myBid != null ? myBid.bidAmount : 0;

                string statusStr = isTop
                    ? $"<color=green>★ 최고 입찰 중</color> | 내 입찰: {myAmount:N0}G"
                    : $"<color=red>⚠ 밀림!</color> | 내 입찰: {myAmount:N0}G → 현재가: {auction.currentBid:N0}G";

                text.text = $"{auction.item.itemName} | {auction.GetRemainingTimeString()}\n{statusStr}";
            }

            Button btn = slot.GetComponent<Button>();
            if (btn != null)
            {
                int id = auction.auctionID;
                btn.onClick.AddListener(() => { SwitchTab(0); OpenBidPopup(id); });
            }
        }
    }

    // ══════════════════════════════════════
    //  기록 탭
    // ══════════════════════════════════════

    private void RefreshHistoryTab()
    {
        ClearChildren(historyParent);
        if (AuctionManager.Instance == null) return;

        List<AuctionHistory> list = AuctionManager.Instance.Histories;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (historySlotPrefab == null || historyParent == null) break;

            AuctionHistory h = list[i];
            GameObject slot = Instantiate(historySlotPrefab, historyParent);

            TextMeshProUGUI text = slot.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                string resultColor = h.result == AuctionResult.Sold ? "green" : "#888";
                text.text = $"{h.endTime:MM/dd HH:mm} | {h.item.itemName} x{h.quantity}\n" +
                            $"<color={resultColor}>{h.GetResultString()}</color> | " +
                            $"판매자: {h.sellerName} | 낙찰자: {h.winnerName} | 입찰 {h.totalBids}회";
            }
        }
    }

    // ══════════════════════════════════════
    //  공용
    // ══════════════════════════════════════

    public void OpenAuction()
    {
        if (auctionPanel != null) auctionPanel.SetActive(true);
        SwitchTab(0);
    }

    public void CloseAuction()
    {
        if (auctionPanel != null) auctionPanel.SetActive(false);
        if (bidPopup != null) bidPopup.SetActive(false);
        if (registerPopup != null) registerPopup.SetActive(false);
        TopMenuManager.Instance?.ClearBanner();
    }

    public void ToggleAuction()
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.ShouldBlockNonFocusButtons) return;
        if (auctionPanel != null)
        {
            if (auctionPanel.activeSelf) CloseAuction();
            else OpenAuction();
        }
    }

    // ══════════════════════════════════════
    //  이벤트 핸들러
    // ══════════════════════════════════════

    private void OnAuctionsUpdated() => SwitchTab(currentTab);

    private void OnPlayerOutbid(AuctionListing auction)
    {
        if (bidPopup != null && bidPopup.activeSelf && selectedAuctionID == auction.auctionID)
        {
            OpenBidPopup(auction.auctionID);
        }
    }

    private void UpdateGold(long gold)
    {
        if (playerGoldText != null)
            playerGoldText.text = $"{UIManager.FormatKoreanUnit(gold)}G";
    }

    // ══════════════════════════════════════
    //  UI 헬퍼
    // ══════════════════════════════════════

    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        foreach (Transform child in parent) Destroy(child.gameObject);
    }

    private void SetChildText(GameObject parent, string childName, string value, Color? color = null)
    {
        Transform t = parent.transform.Find(childName);
        if (t == null) return;
        TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = value;
            if (color.HasValue) tmp.color = color.Value;
        }
    }

    private void SetChildImage(GameObject parent, string childName, Sprite sprite)
    {
        if (sprite == null) return;
        Transform t = parent.transform.Find(childName);
        if (t == null) return;
        Image img = t.GetComponent<Image>();
        if (img != null) img.sprite = sprite;
    }

    private void SetChildColor(GameObject parent, string childName, Color color)
    {
        Transform t = parent.transform.Find(childName);
        if (t == null) return;
        Image img = t.GetComponent<Image>();
        if (img != null) img.color = color;
    }
}