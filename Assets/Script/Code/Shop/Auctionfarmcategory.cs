using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// AuctionFarmCategory
/// ════════════════════════════════════════════════════════════════════════════════════════════════
/// 경매장 드롭다운에 "채소" / "과일" / "기타 농산물" 카테고리 추가.
/// 기존 AuctionManager와 독립적으로 Farm 작물 경매 목록을 관리.
///
/// [Inspector 설정]
///   categoryDropdown     : 농산물 카테고리 TMP_Dropdown
///   farmRegisterPanel    : 농산물 경매 등록 팝업 패널
///   farmAuctionContent   : 농산물 경매 목록 스크롤뷰 콘텐츠
///   farmAuctionItemPrefab: 경매 목록 아이템 프리팹
/// </summary>
public class AuctionFarmCategory : MonoBehaviour
{
    public static AuctionFarmCategory Instance;

    // 농산물 경매 드롭다운 레퍼런스
    [Header("경매 카테고리 드롭다운")]
    public TMP_Dropdown categoryDropdown;

    // 드롭다운 추가할 옵션들
    private readonly List<string> farmCategories = new List<string>
    {
        "채소", "과일", "기타 농산물"
    };

    // 농산물 경매 등록 팝업 레퍼런스
    [Header("농산물 경매 등록 패널")]
    public GameObject farmRegisterPanel;
    public TextMeshProUGUI registerCropNameText;
    public TextMeshProUGUI registerCropCountText;
    public TMP_InputField startBidInput;
    public TMP_InputField buyoutInput;
    public TMP_Dropdown durationDropdown;
    public Button confirmRegisterBtn;
    public Button cancelRegisterBtn;

    // 농산물 경매 목록 레퍼런스
    [Header("농산물 경매 목록")]
    public Transform farmAuctionContent;
    public GameObject farmAuctionItemPrefab;

    // 농산물 경매 리스팅 데이터
    [System.Serializable]
    public class FarmAuctionListing
    {
        public string itemName;
        public Sprite icon;
        public int quantity;
        public int startingBid;
        public int buyoutPrice;
        public float remainingTime;
        public int currentBid;
        public bool isActive = true;
        public FarmInventoryConnector.CropItemType itemType;
        public int itemID = -1;
    }

    private List<FarmAuctionListing> farmAuctions = new List<FarmAuctionListing>();
    private FarmInventoryConnector.FarmItem pendingRegisterItem;

    // 현재 필터된 카테고리 (드롭다운)
    private string currentCategoryFilter = "전체";

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        SetupDropdown();
        SetupRegisterPanel();
        RefreshFarmAuctionList();
    }

    void Update()
    {
        // 경매 타이머 업데이트
        float delta = Time.deltaTime;
        bool anyEnded = false;

        foreach (var listing in farmAuctions)
        {
            if (!listing.isActive) continue;
            listing.remainingTime -= delta;
            if (listing.remainingTime <= 0f)
            {
                listing.isActive = false;
                OnFarmAuctionEnded(listing);
                anyEnded = true;
            }
        }

        if (anyEnded)
        {
            farmAuctions.RemoveAll(l => !l.isActive);
            RefreshFarmAuctionList();
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    //  드롭다운 설정
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    private void SetupDropdown()
    {
        if (categoryDropdown == null) return;

        // 기존 옵션 유지하고 뒤에 농산물 카테고리 추가
        foreach (string cat in farmCategories)
        {
            bool exists = false;
            foreach (var opt in categoryDropdown.options)
                if (opt.text == cat) { exists = true; break; }

            if (!exists)
                categoryDropdown.options.Add(new TMP_Dropdown.OptionData(cat));
        }

        categoryDropdown.onValueChanged.AddListener(OnCategoryChanged);
        categoryDropdown.RefreshShownValue();

        Debug.Log("[AuctionFarmCategory] 드롭다운 농산물 카테고리 추가 완료");
    }

    private void OnCategoryChanged(int index)
    {
        if (categoryDropdown == null || index >= categoryDropdown.options.Count) return;
        currentCategoryFilter = categoryDropdown.options[index].text;
        RefreshFarmAuctionList();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    //  등록 패널
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    private void SetupRegisterPanel()
    {
        if (farmRegisterPanel != null)
            farmRegisterPanel.SetActive(false);

        if (confirmRegisterBtn != null)
            confirmRegisterBtn.onClick.AddListener(OnConfirmRegister);

        if (cancelRegisterBtn != null)
            cancelRegisterBtn.onClick.AddListener(() =>
            {
                if (farmRegisterPanel != null) farmRegisterPanel.SetActive(false);
                pendingRegisterItem = null;
            });
    }

    /// <summary>
    /// Farm 작물 클릭 시 호출 — 경매 등록 팝업 열기
    /// </summary>
    public void OpenFarmRegisterPanel(FarmInventoryConnector.FarmItem item)
    {
        if (item == null) return;
        pendingRegisterItem = item;

        if (registerCropNameText != null) registerCropNameText.text = item.itemName;
        if (registerCropCountText != null) registerCropCountText.text = $"보유: {item.count}개";
        if (startBidInput != null) startBidInput.text = item.sellPrice.ToString();
        if (buyoutInput != null) buyoutInput.text = (item.sellPrice * 3).ToString();

        if (farmRegisterPanel != null) farmRegisterPanel.SetActive(true);
    }

    private void OnConfirmRegister()
    {
        if (pendingRegisterItem == null) return;

        int quantity = pendingRegisterItem.count; // 전체 수량 (원하면 InputField 추가)

        if (!int.TryParse(startBidInput?.text, out int startBid)) startBid = pendingRegisterItem.sellPrice;
        if (!int.TryParse(buyoutInput?.text, out int buyout)) buyout = startBid * 3;

        float duration = AuctionManager.Instance != null && AuctionManager.Instance.AvailableDurations.Length > 0
                         ? AuctionManager.Instance.AvailableDurations[0]
                         : 600f;

        if (durationDropdown != null && AuctionManager.Instance != null)
        {
            int di = durationDropdown.value;
            if (di < AuctionManager.Instance.AvailableDurations.Length)
                duration = AuctionManager.Instance.AvailableDurations[di];
        }

        FarmInventoryConnector.Instance?.RegisterFarmCropToAuction(
            pendingRegisterItem.itemName, quantity, startBid, buyout, duration, pendingRegisterItem.itemID);

        if (farmRegisterPanel != null) farmRegisterPanel.SetActive(false);
        pendingRegisterItem = null;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    //  경매 생성 / 경매 종료
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Farm 경매 생성 (FarmInventoryConnector에서 호출)
    /// </summary>
    public void CreateFarmAuction(FarmInventoryConnector.FarmItem item, int qty,
                                  int startBid, int buyout, float duration)
    {
        var listing = new FarmAuctionListing
        {
            itemName = item.itemName,
            icon = item.icon,
            quantity = qty,
            startingBid = startBid,
            buyoutPrice = buyout,
            remainingTime = duration,
            currentBid = 0,
            isActive = true,
            itemType = item.itemType,
            itemID = item.itemID
        };

        farmAuctions.Add(listing);
        RefreshFarmAuctionList();
        Debug.Log($"[AuctionFarmCategory] 농산물 경매 등록: {item.itemName} x{qty} 시작가 {startBid}G");
    }

    /// <summary>
    /// 경매 UI 갱신 (현재 카테고리 필터 적용)
    /// </summary>
    public void RefreshFarmAuctionList()
    {
        if (farmAuctionContent == null) return;

        // 기존 목록 초기화
        foreach (Transform child in farmAuctionContent)
            Destroy(child.gameObject);

        bool showAll = currentCategoryFilter == "전체" ||
                       !farmCategories.Contains(currentCategoryFilter);

        foreach (var listing in farmAuctions)
        {
            if (!listing.isActive) continue;

            // 카테고리 필터
            if (!showAll)
            {
                string required = listing.itemType switch
                {
                    FarmInventoryConnector.CropItemType.Vegetable => "채소",
                    FarmInventoryConnector.CropItemType.Fruit => "과일",
                    _ => "기타 농산물"
                };

                if (required != currentCategoryFilter) continue;
            }

            if (farmAuctionItemPrefab == null) continue;

            GameObject go = Instantiate(farmAuctionItemPrefab, farmAuctionContent);
            FarmAuctionItemUI ui = go.GetComponent<FarmAuctionItemUI>()
                                   ?? go.AddComponent<FarmAuctionItemUI>();
            ui.Setup(listing, this);
        }
    }

    // 경매 종료 처리
    private void OnFarmAuctionEnded(FarmAuctionListing listing)
    {
        if (listing.currentBid > 0)
        {
            // 낙찰 시 판매 수익 지급 (세금율 적용)
            float taxRate = AuctionManager.Instance != null ? AuctionManager.Instance.SaleTaxPercent / 100f : 0.1f;
            int revenue = Mathf.RoundToInt(listing.currentBid * (1f - taxRate));
            GameManager.Instance?.AddGold(revenue);
            UIManager.Instance?.ShowMessage($"{listing.itemName} 낙찰! +{revenue:N0}G", Color.green);
        }
        else
        {
            // 유찰 시 작물 반환
            FarmInventoryConnector.Instance?.AddFarmCrop(
                listing.itemName, listing.icon, listing.quantity,
                listing.itemType, listing.startingBid, listing.itemID);
            UIManager.Instance?.ShowMessage($"{listing.itemName} 유찰, 반환됨", Color.yellow);
        }

        Debug.Log($"[AuctionFarmCategory] 경매 종료: {listing.itemName}");
    }

    /// <summary>
    /// NPC 입찰 시뮬레이션 (외부 호출 용도)
    /// </summary>
    public void SimulateNPCBid(FarmAuctionListing listing)
    {
        if (listing == null || !listing.isActive) return;
        if (Random.value < 0.4f) // 40% 확률로 입찰
        {
            int bidIncrement = Mathf.Max(1, Mathf.RoundToInt(listing.startingBid * 0.05f));
            listing.currentBid = Mathf.Max(listing.startingBid,
                                           listing.currentBid + bidIncrement);
            RefreshFarmAuctionList();
        }
    }
}
