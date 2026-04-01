using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 경매장 시스템 (Auction House)
/// - 플레이어가 아이템을 경매 등록
/// - NPC 입찰자가 자동으로 입찰 (시세 기반)
/// - 실시간 타이머, 입찰 경쟁
/// - 즉시 구매(Buy-It-Now) 옵션
/// - 경매 종료 시 메일로 아이템/골드 전달
/// - 경매 내역 기록
/// </summary>
public class AuctionManager : MonoBehaviour
{
    public static AuctionManager Instance { get; private set; }

    // ───────────── 설정 ─────────────
    [Header("경매장 설정")]
    [Tooltip("플레이어 최대 동시 등록 수")]
    [SerializeField] private int maxPlayerListings = 5;

    [Tooltip("등록 수수료율 (%)")]
    [Range(0f, 30f)]
    [SerializeField] private float listingFeePercent = 5f;

    [Tooltip("낙찰 수수료율 (%, 판매 금액에서 차감)")]
    [Range(0f, 30f)]
    [SerializeField] private float saleTaxPercent = 10f;

    [Tooltip("최소 입찰 증가율 (%)")]
    [Range(1f, 50f)]
    [SerializeField] private float minBidIncrementPercent = 5f;

    [Header("경매 시간")]
    [Tooltip("기본 경매 지속 시간 (초)")]
    [SerializeField] private float defaultDurationSeconds = 600f; // 10분

    [Tooltip("선택 가능한 경매 시간 (초)")]
    [SerializeField] private float[] availableDurations = { 300f, 600f, 1800f, 3600f };

    [Header("NPC 입찰자")]
    [SerializeField] private AuctionNPCProfile[] npcProfiles;

    [Tooltip("NPC 입찰 판정 간격 (초)")]
    [SerializeField] private float npcBidCheckInterval = 10f;
    public float NpcBidCheckInterval => npcBidCheckInterval;

    [Tooltip("NPC가 시세 대비 최대 지불할 배율")]
    [SerializeField] private float npcMaxPayRatio = 1.3f;

    [Tooltip("경매 마감 임박 시 NPC 입찰 확률 증가 (마지막 N초)")]
    [SerializeField] private float npcUrgentBidWindow = 60f;

    // ───────────── 내부 데이터 ─────────────
    private List<AuctionListing> allAuctions = new List<AuctionListing>();
    private List<AuctionHistory> histories = new List<AuctionHistory>();
    private float npcBidTimer = 0f;
    private int nextAuctionID = 1;

    // ───────────── 이벤트 ─────────────
    public static event Action OnAuctionsUpdated;
    public static event Action<AuctionListing> OnAuctionCreated;
    public static event Action<AuctionListing, AuctionBid> OnNewBid;
    public static event Action<AuctionListing> OnAuctionEnded;
    public static event Action<AuctionListing> OnPlayerOutbid; // 플레이어가 입찰에서 밀렸을 때

    // ───────────── 프로퍼티 ─────────────
    public List<AuctionListing> AllAuctions => allAuctions;
    public List<AuctionHistory> Histories => histories;
    public int MaxPlayerListings => maxPlayerListings;
    public float[] AvailableDurations => availableDurations;
    public float MinBidIncrementPercent => minBidIncrementPercent;
    public float ListingFeePercent => listingFeePercent;
    public float SaleTaxPercent => saleTaxPercent;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] AuctionManager가 생성되었습니다.");

        }
        else
        {
            enabled = false;
            Destroy(gameObject);
        }
    }

    // ══════════════════════════════════════════
    //  경매 등록
    // ══════════════════════════════════════════

    /// <summary>
    /// 플레이어가 아이템을 경매에 등록
    /// </summary>
    public bool CreateAuction(ItemData item, int quantity, int startingBid, int buyoutPrice, float durationSeconds)
    {
        // ── 검증 ──
        if (item == null || quantity <= 0 || startingBid <= 0)
        {
            ShowMessage("잘못된 등록 정보입니다.", Color.red);
            return false;
        }

        // 인벤토리 확인
        if (InventoryManager.Instance == null || !InventoryManager.Instance.HasItem(item, quantity))
        {
            ShowMessage("아이템이 부족합니다!", Color.red);
            return false;
        }

        // 등록 수 확인
        int playerCount = allAuctions.Count(a => a.isActive && a.sellerIsPlayer);
        if (playerCount >= maxPlayerListings)
        {
            ShowMessage($"최대 등록 수({maxPlayerListings}개)를 초과했습니다!", Color.red);
            return false;
        }

        // 즉시 구매가 검증
        if (buyoutPrice > 0 && buyoutPrice <= startingBid)
        {
            ShowMessage("즉시 구매가는 시작가보다 높아야 합니다!", Color.red);
            return false;
        }

        // 등록 수수료 계산 & 차감
        int fee = CalculateListingFee(startingBid, quantity);
        if (GameManager.Instance == null || !GameManager.Instance.SpendGold(fee))
        {
            ShowMessage($"등록 수수료가 부족합니다! (수수료: {fee:N0}G)", Color.red);
            return false;
        }

        // 인벤토리에서 아이템 제거 (에스크로)
        InventoryManager.Instance.RemoveItem(item, quantity);

        // 경매 생성
        AuctionListing auction = new AuctionListing
        {
            auctionID = nextAuctionID++,
            item = item,
            quantity = quantity,
            startingBid = startingBid,
            currentBid = 0,
            buyoutPrice = buyoutPrice,
            sellerName = "나",
            sellerIsPlayer = true,
            startTime = DateTime.Now,
            endTime = DateTime.Now.AddSeconds(durationSeconds),
            isActive = true,
            bids = new List<AuctionBid>()
        };

        allAuctions.Add(auction);

        OnAuctionCreated?.Invoke(auction);
        OnAuctionsUpdated?.Invoke();

        SaveLoadManager.Instance?.SaveGame();
        ShowMessage($"'{item.itemName}' x{quantity} 경매 등록!\n시작가: {startingBid:N0}G | 수수료: {fee:N0}G", Color.cyan);
        Debug.Log($"[Auction] 등록: {item.itemName} x{quantity}, 시작가 {startingBid}G, 기간 {durationSeconds}초");

        return true;
    }

    /// <summary>
    /// 간편 등록 (기본 즉시구매가 = 시작가*3, 기본 기간 = availableDurations[0])
    /// ItemDetailPanel 등 외부에서 호출용
    /// </summary>
    public bool RegisterItem(ItemData item, int quantity, int price)
    {
        int buyout = price * 3;
        float duration = availableDurations.Length > 0 ? availableDurations[0] : defaultDurationSeconds;
        return CreateAuction(item, quantity, price, buyout, duration);
    }

    // ══════════════════════════════════════════
    //  입찰
    // ══════════════════════════════════════════

    /// <summary>
    /// 플레이어 입찰
    /// </summary>
    public bool PlaceBid(int auctionID, int bidAmount)
    {
        AuctionListing auction = FindAuction(auctionID);
        if (auction == null || !auction.isActive)
        {
            ShowMessage("경매를 찾을 수 없습니다.", Color.red);
            return false;
        }

        // 만료 체크
        if (DateTime.Now >= auction.endTime)
        {
            ShowMessage("이미 종료된 경매입니다.", Color.red);
            return false;
        }

        // 자기 경매에 입찰 불가
        if (auction.sellerIsPlayer)
        {
            ShowMessage("자신의 경매에는 입찰할 수 없습니다.", Color.red);
            return false;
        }

        // 최소 입찰가 확인
        int minBid = GetMinimumBid(auction);
        if (bidAmount < minBid)
        {
            ShowMessage($"최소 입찰가: {minBid:N0}G", Color.red);
            return false;
        }

        // 골드 확인
        if (GameManager.Instance == null || GameManager.Instance.PlayerGold < bidAmount)
        {
            ShowMessage("골드가 부족합니다!", Color.red);
            return false;
        }

        // 이전 최고 입찰자가 플레이어였으면 환불
        AuctionBid previousTopBid = auction.GetHighestBid();
        if (previousTopBid != null && previousTopBid.isPlayer)
        {
            // 같은 플레이어가 연속 입찰 → 차액만 추가 차감
            int additionalCost = bidAmount - previousTopBid.bidAmount;
            if (!GameManager.Instance.SpendGold(additionalCost))
            {
                ShowMessage("골드가 부족합니다!", Color.red);
                return false;
            }
        }
        else
        {
            // 새 입찰 → 전액 차감
            if (!GameManager.Instance.SpendGold(bidAmount))
            {
                ShowMessage("골드가 부족합니다!", Color.red);
                return false;
            }
        }

        // 입찰 등록
        AuctionBid bid = new AuctionBid
        {
            bidderName = "나",
            isPlayer = true,
            bidAmount = bidAmount,
            bidTime = DateTime.Now
        };

        auction.currentBid = bidAmount;
        auction.bids.Add(bid);

        // 마감 임박 시 시간 연장 (스나이핑 방지)
        TimeSpan remaining = auction.endTime - DateTime.Now;
        if (remaining.TotalSeconds < 30)
        {
            auction.endTime = DateTime.Now.AddSeconds(30);
            Debug.Log("[Auction] 마감 임박 입찰 → 30초 연장");
        }

        OnNewBid?.Invoke(auction, bid);
        OnAuctionsUpdated?.Invoke();

        SaveLoadManager.Instance?.SaveGame();
        ShowMessage($"'{auction.item.itemName}' 에 {bidAmount:N0}G 입찰!", Color.green);
        Debug.Log($"[Auction] 플레이어 입찰: #{auctionID} = {bidAmount}G");

        return true;
    }

    /// <summary>
    /// 즉시 구매 (Buy-It-Now)
    /// </summary>
    public bool BuyoutAuction(int auctionID)
    {
        AuctionListing auction = FindAuction(auctionID);
        if (auction == null || !auction.isActive)
        {
            ShowMessage("경매를 찾을 수 없습니다.", Color.red);
            return false;
        }

        if (auction.buyoutPrice <= 0)
        {
            ShowMessage("즉시 구매가 설정되지 않은 경매입니다.", Color.red);
            return false;
        }

        if (auction.sellerIsPlayer)
        {
            ShowMessage("자신의 경매는 구매할 수 없습니다.", Color.red);
            return false;
        }

        if (DateTime.Now >= auction.endTime)
        {
            ShowMessage("이미 종료된 경매입니다.", Color.red);
            return false;
        }

        // 이전에 플레이어가 입찰했으면 차액만 필요
        int cost = auction.buyoutPrice;
        AuctionBid myPreviousBid = auction.bids.FindLast(b => b.isPlayer);
        if (myPreviousBid != null)
        {
            cost = auction.buyoutPrice - myPreviousBid.bidAmount;
        }

        if (GameManager.Instance == null || !GameManager.Instance.SpendGold(cost))
        {
            ShowMessage("골드가 부족합니다!", Color.red);
            return false;
        }

        // 즉시 구매 입찰 등록
        AuctionBid buyoutBid = new AuctionBid
        {
            bidderName = "나",
            isPlayer = true,
            bidAmount = auction.buyoutPrice,
            bidTime = DateTime.Now
        };

        auction.currentBid = auction.buyoutPrice;
        auction.bids.Add(buyoutBid);

        // 즉시 종료
        EndAuction(auction);

        SaveLoadManager.Instance?.SaveGame();
        ShowMessage($"'{auction.item.itemName}' 즉시 구매 완료!\n-{auction.buyoutPrice:N0}G", Color.green);
        return true;
    }

    // ══════════════════════════════════════════
    //  경매 취소
    // ══════════════════════════════════════════

    /// <summary>
    /// 플레이어 경매 취소 (입찰자가 없을 때만)
    /// </summary>
    public bool CancelAuction(int auctionID)
    {
        AuctionListing auction = FindAuction(auctionID);
        if (auction == null || !auction.isActive || !auction.sellerIsPlayer)
        {
            ShowMessage("취소할 수 없는 경매입니다.", Color.red);
            return false;
        }

        if (auction.bids.Count > 0)
        {
            ShowMessage("입찰자가 있는 경매는 취소할 수 없습니다!", Color.red);
            return false;
        }

        // 아이템 반환
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(auction.item, auction.quantity);
        }

        auction.isActive = false;
        auction.result = AuctionResult.Cancelled;

        OnAuctionsUpdated?.Invoke();

        ShowMessage($"'{auction.item.itemName}' 경매 취소됨\n(등록 수수료는 반환되지 않습니다)", Color.yellow);
        return true;
    }

    // ══════════════════════════════════════════
    //  경매 진행 & 종료
    // ══════════════════════════════════════════

    /// <summary>
    /// 매 프레임 경매 타이머 갱신 & 만료 체크
    /// </summary>
    void ProcessActiveAuctions()
    {
        List<AuctionListing> toEnd = new List<AuctionListing>();

        foreach (var auction in allAuctions)
        {
            if (!auction.isActive) continue;

            if (DateTime.Now >= auction.endTime)
            {
                toEnd.Add(auction);
            }
        }

        foreach (var auction in toEnd)
        {
            EndAuction(auction);
        }
    }

    /// <summary>
    /// 경매 종료 처리
    /// </summary>
    private void EndAuction(AuctionListing auction)
    {
        if (!auction.isActive) return;
        auction.isActive = false;

        AuctionBid winningBid = auction.GetHighestBid();

        if (winningBid != null)
        {
            // ── 낙찰 ──
            auction.result = AuctionResult.Sold;
            auction.winnerName = winningBid.bidderName;
            auction.finalPrice = winningBid.bidAmount;

            // 판매자: 골드 지급 (세금 차감)
            int tax = Mathf.RoundToInt(winningBid.bidAmount * saleTaxPercent / 100f);
            int sellerGold = winningBid.bidAmount - tax;

            if (auction.sellerIsPlayer)
            {
                // 플레이어가 판매자 → 골드 메일 발송
                if (MailManager.Instance != null)
                {
                    List<MailReward> rewards = new List<MailReward>
                    {
                        new MailReward(MailReward.RewardType.Gold, sellerGold)
                    };
                    MailManager.Instance.SendRewardMail(
                        "경매 낙찰!",
                        $"'{auction.item.itemName}' x{auction.quantity}이(가) {winningBid.bidAmount:N0}G에 낙찰되었습니다!\n수수료: {tax:N0}G\n수령 골드: {sellerGold:N0}G",
                        rewards
                    );
                }
            }

            // 낙찰자: 아이템 수령
            if (winningBid.isPlayer)
            {
                // 플레이어가 낙찰 → 인벤토리 또는 메일
                if (InventoryManager.Instance != null && InventoryManager.Instance.HasSpace())
                {
                    InventoryManager.Instance.AddItem(auction.item, auction.quantity);
                    ShowMessage($"낙찰! '{auction.item.itemName}' x{auction.quantity} 획득!", Color.green);
                }
                else if (MailManager.Instance != null)
                {
                    MailManager.Instance.SendItemToMail(auction.item, auction.quantity, "경매 낙찰");
                }
            }
            else
            {
                // NPC가 낙찰 → 판매자에게만 골드 (위에서 처리됨)
            }

            // 이전 입찰자 중 플레이어에게 환불 (최고 입찰자 제외)
            RefundOutbidPlayers(auction, winningBid);

            Debug.Log($"[Auction] 낙찰: #{auction.auctionID} → {winningBid.bidderName} @ {winningBid.bidAmount}G");
        }
        else
        {
            // ── 유찰 ──
            auction.result = AuctionResult.Expired;

            // 판매자에게 아이템 반환
            if (auction.sellerIsPlayer)
            {
                if (MailManager.Instance != null)
                {
                    MailManager.Instance.SendItemToMail(auction.item, auction.quantity, "경매 유찰");
                    MailManager.Instance.SendNoticeMail(
                        "📭 경매 유찰",
                        $"'{auction.item.itemName}' x{auction.quantity} 경매가 입찰 없이 종료되었습니다.\n아이템이 반환되었습니다."
                    );
                }
                else if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.AddItem(auction.item, auction.quantity);
                }

                ShowMessage($"'{auction.item.itemName}' 경매 유찰 - 아이템 반환", Color.yellow);
            }

            Debug.Log($"[Auction] 유찰: #{auction.auctionID}");
        }

        // 히스토리 저장
        histories.Add(new AuctionHistory
        {
            auctionID = auction.auctionID,
            item = auction.item,
            quantity = auction.quantity,
            finalPrice = auction.finalPrice,
            winnerName = auction.winnerName ?? "없음",
            sellerName = auction.sellerName,
            result = auction.result,
            endTime = DateTime.Now,
            totalBids = auction.bids.Count
        });

        OnAuctionEnded?.Invoke(auction);
        OnAuctionsUpdated?.Invoke();
    }

    /// <summary>
    /// 밀린 플레이어 입찰자에게 골드 환불
    /// </summary>
    private void RefundOutbidPlayers(AuctionListing auction, AuctionBid winningBid)
    {
        if (auction.bids == null || winningBid == null) return;

        // 플레이어 입찰 중 최고 입찰만 찾기
        AuctionBid lastPlayerBid = auction.bids.FindLast(b => b.isPlayer);

        // 낙찰자가 플레이어가 아니고, 플레이어가 입찰했었다면 → 환불
        if (lastPlayerBid != null && !winningBid.isPlayer)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddGold(lastPlayerBid.bidAmount);

                if (MailManager.Instance != null)
                {
                    MailManager.Instance.SendNoticeMail(
                        "💸 입찰 환불",
                        $"'{auction.item.itemName}' 경매에서 다른 입찰자에게 밀렸습니다.\n{lastPlayerBid.bidAmount:N0}G가 환불되었습니다."
                    );
                }
            }
        }
    }

    // ══════════════════════════════════════════
    //  NPC 자동 입찰
    // ══════════════════════════════════════════

    /// <summary>
    /// NPC 입찰 처리 (주기적 호출)
    /// </summary>
    void ProcessNPCBids()
    {
        if (npcProfiles == null || npcProfiles.Length == 0) return;

        foreach (var auction in allAuctions)
        {
            if (!auction.isActive) continue;
            if (!auction.sellerIsPlayer) continue; // NPC 경매에는 NPC가 입찰 안 함

            // 현재 최고 입찰자가 NPC면 스킵 (같은 NPC 연속 입찰 방지)
            AuctionBid topBid = auction.GetHighestBid();
            if (topBid != null && !topBid.isPlayer) continue;

            // NPC 입찰 판정
            TryNPCBid(auction);
        }
    }

    /// <summary>
    /// 개별 경매에 NPC 입찰 시도
    /// </summary>
    private void TryNPCBid(AuctionListing auction)
    {
        // 아이템 적정 가치 계산
        float fairValue = auction.item.GetItemValue();
        if (fairValue <= 0) fairValue = auction.item.buyPrice;
        float maxNPCPay = fairValue * npcMaxPayRatio * auction.quantity;

        int minBid = GetMinimumBid(auction);

        // NPC가 지불할 의향이 없으면 스킵
        if (minBid > maxNPCPay) return;

        // 남은 시간에 따른 입찰 확률
        TimeSpan remaining = auction.endTime - DateTime.Now;
        float bidChance = 0.1f; // 기본 10%

        // 아이템 가치 대비 현재가가 싸면 확률 증가
        float currentPrice = auction.currentBid > 0 ? auction.currentBid : auction.startingBid;
        float valueRatio = maxNPCPay / Mathf.Max(1, currentPrice);
        if (valueRatio > 2f) bidChance += 0.3f;
        else if (valueRatio > 1.5f) bidChance += 0.2f;
        else if (valueRatio > 1.1f) bidChance += 0.1f;

        // 마감 임박 시 확률 급증
        if (remaining.TotalSeconds <= npcUrgentBidWindow)
        {
            bidChance += 0.3f;
        }

        // 등급 높을수록 관심도 증가
        bidChance += (int)auction.item.rarity * 0.05f;

        // 판정
        if (UnityEngine.Random.Range(0f, 1f) > bidChance) return;

        // NPC 입찰 금액 결정
        int npcBidAmount = minBid;

        // 가끔 최소보다 조금 더 높게 입찰 (경쟁 시뮬레이션)
        if (UnityEngine.Random.Range(0f, 1f) < 0.3f)
        {
            int extra = Mathf.RoundToInt(minBid * UnityEngine.Random.Range(0.05f, 0.2f));
            npcBidAmount += extra;
        }

        // 최대 지불 금액 초과 방지
        npcBidAmount = Mathf.Min(npcBidAmount, Mathf.RoundToInt(maxNPCPay));

        if (npcBidAmount < minBid) return;

        // NPC 프로필 선택
        AuctionNPCProfile npc = npcProfiles[UnityEngine.Random.Range(0, npcProfiles.Length)];

        // 입찰 등록
        AuctionBid bid = new AuctionBid
        {
            bidderName = npc.npcName,
            isPlayer = false,
            bidAmount = npcBidAmount,
            bidTime = DateTime.Now
        };

        auction.currentBid = npcBidAmount;
        auction.bids.Add(bid);

        // 마감 임박 입찰 시 시간 연장
        TimeSpan timeLeft = auction.endTime - DateTime.Now;
        if (timeLeft.TotalSeconds < 30)
        {
            auction.endTime = DateTime.Now.AddSeconds(30);
        }

        // 플레이어가 이전 최고 입찰자였으면 알림
        AuctionBid previousBid = auction.bids.Count >= 2 ? auction.bids[auction.bids.Count - 2] : null;
        if (previousBid != null && previousBid.isPlayer)
        {
            OnPlayerOutbid?.Invoke(auction);

            if (MailManager.Instance != null)
            {
                MailManager.Instance.SendNoticeMail(
                    "⚠️ 입찰 경쟁!",
                    $"'{auction.item.itemName}' 경매에서 {npc.npcName}님이 {npcBidAmount:N0}G로 더 높은 입찰을 했습니다!\n재입찰하려면 경매장을 확인하세요."
                );
            }

            ShowMessage($"'{auction.item.itemName}' 경매에서 밀렸습니다!\n{npc.npcName}이(가) {npcBidAmount:N0}G 입찰!", Color.red);
        }

        OnNewBid?.Invoke(auction, bid);
        OnAuctionsUpdated?.Invoke();

        Debug.Log($"[Auction] NPC 입찰: {npc.npcName} → #{auction.auctionID} @ {npcBidAmount}G");
    }

    // ══════════════════════════════════════════
    //  NPC 경매 생성 (자동 매물)
    // ══════════════════════════════════════════

    /// <summary>
    /// NPC가 경매에 아이템 등록 (주기적으로 호출하거나 Start에서 호출)
    /// </summary>
    public void GenerateNPCAuctions(int count = 5)
    {
        if (npcProfiles == null || npcProfiles.Length == 0) return;

        int created = 0;
        foreach (var npc in npcProfiles)
        {
            if (npc.auctionItems == null) continue;

            foreach (var npcItem in npc.auctionItems)
            {
                if (created >= count) return;
                if (npcItem.item == null) continue;

                // 등록 확률
                if (UnityEngine.Random.Range(0f, 100f) > npcItem.listChance) continue;

                // 이미 같은 아이템 경매가 있으면 스킵
                bool alreadyListed = allAuctions.Exists(a =>
                    a.isActive && !a.sellerIsPlayer && a.item.itemID == npcItem.item.itemID);
                if (alreadyListed) continue;

                int quantity = UnityEngine.Random.Range(npcItem.minQuantity, npcItem.maxQuantity + 1);
                float priceVariation = UnityEngine.Random.Range(0.7f, 1.2f);
                int startPrice = Mathf.Max(1, Mathf.RoundToInt(npcItem.item.buyPrice * priceVariation));
                int buyoutPrice = Mathf.RoundToInt(startPrice * UnityEngine.Random.Range(1.5f, 2.5f));
                float duration = availableDurations[UnityEngine.Random.Range(0, availableDurations.Length)];

                AuctionListing auction = new AuctionListing
                {
                    auctionID = nextAuctionID++,
                    item = npcItem.item,
                    quantity = quantity,
                    startingBid = startPrice,
                    currentBid = 0,
                    buyoutPrice = buyoutPrice,
                    sellerName = npc.npcName,
                    sellerIsPlayer = false,
                    startTime = DateTime.Now,
                    endTime = DateTime.Now.AddSeconds(duration),
                    isActive = true,
                    bids = new List<AuctionBid>()
                };

                allAuctions.Add(auction);
                created++;
            }
        }

        if (created > 0)
        {
            OnAuctionsUpdated?.Invoke();
            Debug.Log($"[Auction] NPC 경매 {created}개 생성");
        }
    }

    void Start()
    {
        if (Instance != this) return;
        CancelInvoke();
        GenerateNPCAuctions(8);

        // ✅ Update() 대신 InvokeRepeating으로 성능 최적화
        // 경매 타이머: 1초마다 체크 (매 프레임 불필요)
        InvokeRepeating(nameof(ProcessActiveAuctions), 1f, 1f);
        // NPC 입찰: 기존 npcBidCheckInterval 주기 유지
        InvokeRepeating(nameof(ProcessNPCBids), npcBidCheckInterval, npcBidCheckInterval);
    }

    // ✅ Update 완전 제거 - InvokeRepeating으로 대체
    // void Update() 삭제

    // ══════════════════════════════════════════
    //  검색 & 필터
    // ══════════════════════════════════════════

    /// <summary>
    /// 활성 경매 목록 (필터 + 정렬)
    /// </summary>
    public List<AuctionListing> GetActiveAuctions(
        string searchText = "",
        ItemType? typeFilter = null,
        ItemRarity? rarityFilter = null,
        AuctionSortType sortType = AuctionSortType.EndingSoon)
    {
        List<AuctionListing> results = allAuctions.FindAll(a => a.isActive);

        // 검색
        if (!string.IsNullOrEmpty(searchText))
        {
            string lower = searchText.ToLower();
            results = results.FindAll(a => a.item.itemName.ToLower().Contains(lower));
        }

        // 타입 필터
        if (typeFilter.HasValue)
            results = results.FindAll(a => a.item.itemType == typeFilter.Value);

        // 등급 필터
        if (rarityFilter.HasValue)
            results = results.FindAll(a => a.item.rarity == rarityFilter.Value);

        // 정렬
        switch (sortType)
        {
            case AuctionSortType.EndingSoon:
                results.Sort((a, b) => a.endTime.CompareTo(b.endTime));
                break;
            case AuctionSortType.PriceLow:
                results.Sort((a, b) => a.GetCurrentPrice().CompareTo(b.GetCurrentPrice()));
                break;
            case AuctionSortType.PriceHigh:
                results.Sort((a, b) => b.GetCurrentPrice().CompareTo(a.GetCurrentPrice()));
                break;
            case AuctionSortType.RarityHigh:
                results.Sort((a, b) => b.item.rarity.CompareTo(a.item.rarity));
                break;
            case AuctionSortType.MostBids:
                results.Sort((a, b) => b.bids.Count.CompareTo(a.bids.Count));
                break;
            case AuctionSortType.Newest:
                results.Sort((a, b) => b.startTime.CompareTo(a.startTime));
                break;
        }

        return results;
    }

    /// <summary>
    /// 플레이어가 등록한 경매 목록
    /// </summary>
    public List<AuctionListing> GetMyAuctions()
    {
        return allAuctions.FindAll(a => a.sellerIsPlayer);
    }

    /// <summary>
    /// 플레이어가 입찰 중인 경매 목록
    /// </summary>
    public List<AuctionListing> GetMyBids()
    {
        return allAuctions.FindAll(a => a.isActive && a.bids.Exists(b => b.isPlayer));
    }

    // ══════════════════════════════════════════
    //  유틸리티
    // ══════════════════════════════════════════

    public AuctionListing FindAuction(int auctionID)
    {
        return allAuctions.Find(a => a.auctionID == auctionID);
    }

    /// <summary>
    /// 최소 입찰가 계산
    /// </summary>
    public int GetMinimumBid(AuctionListing auction)
    {
        if (auction.currentBid <= 0)
            return auction.startingBid;

        int increment = Mathf.Max(1, Mathf.CeilToInt(auction.currentBid * minBidIncrementPercent / 100f));
        return auction.currentBid + increment;
    }

    /// <summary>
    /// 등록 수수료 계산
    /// </summary>
    public int CalculateListingFee(int startingBid, int quantity)
    {
        return Mathf.Max(1, Mathf.RoundToInt(startingBid * quantity * listingFeePercent / 100f));
    }

    /// <summary>
    /// 낙찰 수수료 계산 (판매 금액 기준)
    /// </summary>
    public int CalculateSaleTax(int salePrice)
    {
        return Mathf.RoundToInt(salePrice * saleTaxPercent / 100f);
    }

    private void ShowMessage(string msg, Color color)
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage(msg, color);
    }

    void OnApplicationQuit()
    {
        // 활성 경매 중 플레이어 등록 → 아이템 보존 (저장 시스템에서 처리)
        // 플레이어 입찰 골드 → 보존
    }
}

// ══════════════════════════════════════════
//  데이터 클래스
// ══════════════════════════════════════════

/// <summary>
/// 경매 등록 데이터
/// </summary>
[System.Serializable]
public class AuctionListing
{
    public int auctionID;
    public ItemData item;
    public int quantity;
    public int startingBid;           // 시작가
    public int currentBid;            // 현재 최고 입찰가
    public int buyoutPrice;           // 즉시 구매가 (0이면 비활성)
    public string sellerName;
    public bool sellerIsPlayer;
    public DateTime startTime;
    public DateTime endTime;
    public bool isActive;
    public List<AuctionBid> bids;

    // 결과
    public AuctionResult result = AuctionResult.Active;
    public string winnerName;
    public int finalPrice;

    /// <summary>현재 표시 가격</summary>
    public int GetCurrentPrice() => currentBid > 0 ? currentBid : startingBid;

    /// <summary>최고 입찰</summary>
    public AuctionBid GetHighestBid() => bids != null && bids.Count > 0 ? bids[bids.Count - 1] : null;

    /// <summary>남은 시간</summary>
    public TimeSpan GetRemainingTime() => isActive ? (endTime - DateTime.Now) : TimeSpan.Zero;

    /// <summary>남은 시간 문자열</summary>
    public string GetRemainingTimeString()
    {
        TimeSpan t = GetRemainingTime();
        if (t.TotalSeconds <= 0) return "종료됨";
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}시간 {t.Minutes}분";
        if (t.TotalMinutes >= 1) return $"{t.Minutes}분 {t.Seconds}초";
        return $"{t.Seconds}초";
    }

    /// <summary>플레이어가 현재 최고 입찰자인지</summary>
    public bool IsPlayerTopBidder()
    {
        var top = GetHighestBid();
        return top != null && top.isPlayer;
    }
}

/// <summary>
/// 입찰 데이터
/// </summary>
[System.Serializable]
public class AuctionBid
{
    public string bidderName;
    public bool isPlayer;
    public int bidAmount;
    public DateTime bidTime;
}

/// <summary>
/// 경매 결과
/// </summary>
public enum AuctionResult
{
    Active,     // 진행 중
    Sold,       // 낙찰
    Expired,    // 유찰
    Cancelled   // 취소
}

/// <summary>
/// 정렬 방식
/// </summary>
public enum AuctionSortType
{
    EndingSoon,   // 마감 임박순
    Newest,       // 최신순
    PriceLow,     // 가격 낮은순
    PriceHigh,    // 가격 높은순
    RarityHigh,   // 등급 높은순
    MostBids      // 입찰 많은순
}

/// <summary>
/// 경매 히스토리
/// </summary>
[System.Serializable]
public class AuctionHistory
{
    public int auctionID;
    public ItemData item;
    public int quantity;
    public int finalPrice;
    public string winnerName;
    public string sellerName;
    public AuctionResult result;
    public DateTime endTime;
    public int totalBids;

    public string GetResultString()
    {
        switch (result)
        {
            case AuctionResult.Sold: return $"낙찰 ({finalPrice:N0}G)";
            case AuctionResult.Expired: return "유찰";
            case AuctionResult.Cancelled: return "취소";
            default: return "진행 중";
        }
    }
}

/// <summary>
/// NPC 프로필 (Inspector 설정)
/// </summary>
[System.Serializable]
public class AuctionNPCProfile
{
    [Header("NPC 정보")]
    public string npcName = "떠돌이 모험가";

    [Tooltip("NPC 초상화")]
    public Sprite npcIcon;

    [Header("경매 등록 아이템 (NPC가 매물로 올리는 것)")]
    public NPCAuctionItem[] auctionItems;
}

/// <summary>
/// NPC 경매 아이템 설정
/// </summary>
[System.Serializable]
public class NPCAuctionItem
{
    public ItemData item;

    [Range(0f, 100f)]
    [Tooltip("등록 확률 (%)")]
    public float listChance = 30f;

    [Min(1)] public int minQuantity = 1;
    [Min(1)] public int maxQuantity = 3;
}