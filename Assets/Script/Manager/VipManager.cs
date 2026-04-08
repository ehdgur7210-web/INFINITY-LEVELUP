using UnityEngine;
using System;
using System.Collections.Generic;
using BackEnd;
using LitJson;

// =====================================================
// VipManager.cs
// ⭐ Param/JsonData 방식 (모델 클래스 불필요)
//
// 뒤끝 콘솔 테이블 설정:
//   테이블명 : vipinfo
//   컬럼     : vip_level(int32)  기본값 0   Primary ✅
//              vip_exp(int32)    기본값 0
//              expire_date(string)           Nullable ✅
//              free_gift_claimed(bool)       기본값 false
//
// 실행 순서:
//   BackendManager.Start() → 뒤끝 초기화 + 로그인
//   → 로그인 성공 콜백 → VipManager.LoadVipDataFromServer()
// =====================================================
public class VipManager : MonoBehaviour
{
    // ─────────────────────────────────────────
    // 싱글턴
    // ─────────────────────────────────────────
    public static VipManager Instance { get; private set; }

    // ─────────────────────────────────────────
    // 인스펙터 설정
    // ─────────────────────────────────────────
    [Header("VIP 등급 데이터 (ScriptableObject)")]
    [Tooltip("VIP1 ~ VIPN까지 순서대로 넣어주세요")]
    [SerializeField] private List<VipData> vipDataList = new List<VipData>();

    [Header("뒤끝 테이블 이름")]
    [Tooltip("뒤끝 콘솔 테이블명과 동일하게 (소문자)")]
    [SerializeField] private string tableName = "vipinfo";

    // ─────────────────────────────────────────
    // 플레이어 VIP 상태 (서버에서 받아온 값)
    // ─────────────────────────────────────────

    /// <summary>현재 VIP 등급 (0 = 비VIP)</summary>
    public int CurrentVipLevel { get; private set; } = 0;

    /// <summary>현재 VIP 경험치</summary>
    public int CurrentVipExp { get; private set; } = 0;

    /// <summary>VIP 만료일 문자열 (예: "2025-12-31T23:59:59")</summary>
    public string ExpireDate { get; private set; } = "";

    /// <summary>무료 선물 수령 여부 (하위 호환용)</summary>
    public bool IsFreeGiftClaimed { get; private set; } = false;

    /// <summary>유료 선물 구매 여부 (하위 호환용)</summary>
    public bool IsPaidGiftPurchased { get; private set; } = false;

    /// <summary>등급별 무료 선물 수령 기록 (예: "1,2" = VIP1,2 수령완료)</summary>
    public string ClaimedFreeLevels { get; private set; } = "";

    /// <summary>등급별 유료 선물 구매 기록</summary>
    public string ClaimedPaidLevels { get; private set; } = "";

    /// <summary>해당 등급의 무료 선물을 수령했는지</summary>
    public bool IsFreeLevelClaimed(int level)
    {
        if (string.IsNullOrEmpty(ClaimedFreeLevels)) return false;
        foreach (var s in ClaimedFreeLevels.Split(','))
            if (int.TryParse(s, out int v) && v == level) return true;
        return false;
    }

    /// <summary>해당 등급의 유료 선물을 구매했는지</summary>
    public bool IsPaidLevelClaimed(int level)
    {
        if (string.IsNullOrEmpty(ClaimedPaidLevels)) return false;
        foreach (var s in ClaimedPaidLevels.Split(','))
            if (int.TryParse(s, out int v) && v == level) return true;
        return false;
    }

    private void MarkFreeClaimed(int level)
    {
        if (IsFreeLevelClaimed(level)) return;
        ClaimedFreeLevels = string.IsNullOrEmpty(ClaimedFreeLevels) ? level.ToString() : $"{ClaimedFreeLevels},{level}";
    }

    private void MarkPaidClaimed(int level)
    {
        if (IsPaidLevelClaimed(level)) return;
        ClaimedPaidLevels = string.IsNullOrEmpty(ClaimedPaidLevels) ? level.ToString() : $"{ClaimedPaidLevels},{level}";
    }

    /// <summary>서버 데이터 로드 완료 여부</summary>
    public bool IsDataLoaded { get; private set; } = false;

    /// <summary>서버 행의 inDate (UPDATE 시 사용)</summary>
    private string _serverInDate = null;

    /// <summary>★ 뒤끝 콘솔에 vipinfo 테이블이 없을 때 true → 이후 서버 호출 전부 스킵</summary>
    private bool _tableMissing = false;

    // ─────────────────────────────────────────
    // 이벤트 (VipUI가 구독해서 UI 갱신)
    // ─────────────────────────────────────────

    /// <summary>데이터 변경 시 발생 → VipUI가 받아서 화면 갱신</summary>
    public static event Action OnVipDataChanged;

    /// <summary>무료 선물 수령 완료 시 발생</summary>
    public static event Action OnFreeGiftClaimed;

    /// <summary>유료 선물 구매 완료 시 발생 (int = 구매한 등급)</summary>
    public static event Action<int> OnVipPurchased;

    // ─────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] VipManager가 생성되었습니다.");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // 로컬 데이터 먼저 로드 (서버 응답 전까지 폴백)
        LoadFromLocal();

        // VipData SO가 없으면 기본 데이터 생성
        if (vipDataList == null || vipDataList.Count == 0)
            GenerateDefaultVipData();
    }

    // ─────────────────────────────────────────
    // 서버 데이터 로드
    // ─────────────────────────────────────────

    /// <summary>
    /// 뒤끝 서버에서 VIP 데이터를 불러옵니다.
    /// BackendManager 로그인 성공 후 호출됩니다.
    /// </summary>
    public void LoadVipDataFromServer()
    {
        if (_tableMissing) return; // 콘솔에 테이블 없음 → 호출 자체 스킵

        // GetMyData: 내 유저의 vipinfo 테이블 전체 조회
        Backend.GameData.GetMyData(tableName, new Where(), callback =>
        {
            if (!callback.IsSuccess())
            {
                // ★ 테이블 없음 감지 → 플래그 설정 (이후 호출 전부 스킵)
                if (IsTableMissingError(callback.GetMessage()))
                {
                    _tableMissing = true;
                    Debug.LogWarning($"[VipManager] 뒤끝 콘솔에 '{tableName}' 테이블이 없습니다. 콘솔에서 Private 테이블을 생성하세요. (이후 VIP 서버 호출은 모두 스킵됩니다)");
                    return;
                }
                Debug.LogError($"[VipManager] 로드 실패: {callback.GetMessage()}");
                return;
            }

            // FlattenRows: 결과 JSON을 배열로 평탄화
            JsonData rows = callback.FlattenRows();

            if (rows.Count == 0)
            {
                // 데이터 없음 = 처음 접속한 유저 → 초기 데이터 생성
                Debug.Log("[VipManager] 데이터 없음 → 초기 데이터 생성");
                CreateInitialData();
            }
            else
            {
                // 첫 번째 행 파싱 후 저장 (유저당 1행)
                ParseData(rows[0]);

                // ★ inDate 저장 (UPDATE 시 행 특정에 사용)
                JsonData rawRows = callback.Rows();
                if (rawRows != null && rawRows.Count > 0 && rawRows[0].ContainsKey("inDate"))
                    _serverInDate = rawRows[0]["inDate"].ToString();

                IsDataLoaded = true;
                OnVipDataChanged?.Invoke();
                Debug.Log($"[VipManager] 로드 완료 - VIP{CurrentVipLevel}, 경험치: {CurrentVipExp}, inDate={_serverInDate}");
            }
        });
    }

    /// <summary>
    /// 서버에서 받은 JSON 한 행을 파싱해서 필드에 저장합니다.
    /// </summary>
    private void ParseData(JsonData row)
    {
        // ⭐ 컬럼명은 뒤끝 콘솔 테이블의 컬럼명과 정확히 일치해야 합니다
        CurrentVipLevel = int.Parse(row["vip_level"].ToString());
        CurrentVipExp = int.Parse(row["vip_exp"].ToString());
        ExpireDate = row["expire_date"].ToString();
        IsFreeGiftClaimed = bool.Parse(row["free_gift_claimed"].ToString());
    }

    /// <summary>
    /// 처음 접속한 유저의 VIP 초기 데이터를 서버에 생성합니다.
    /// </summary>
    private void CreateInitialData()
    {
        // Param: 뒤끝에 저장할 데이터 컨테이너
        Param param = new Param();
        param.Add("vip_level", 0);
        param.Add("vip_exp", 0);
        param.Add("expire_date", "");
        param.Add("free_gift_claimed", false);

        Backend.GameData.Insert(tableName, param, callback =>
        {
            if (!callback.IsSuccess())
            {
                if (IsTableMissingError(callback.GetMessage()))
                {
                    _tableMissing = true;
                    Debug.LogWarning($"[VipManager] '{tableName}' 테이블 없음 → 초기 데이터 생성 스킵. 뒤끝 콘솔에서 Private 테이블 생성 필요.");
                    return;
                }
                Debug.LogError($"[VipManager] 초기 데이터 생성 실패: {callback.GetMessage()}");
                return;
            }

            // 생성 성공 → 다시 로드해서 inDate 등 값 채우기
            Debug.Log("[VipManager] 초기 데이터 생성 완료 → 재로드");
            LoadVipDataFromServer();
        });
    }

    /// <summary>뒤끝 에러 메시지에 "table not found" 또는 "찾을 수 없" 포함 여부</summary>
    private static bool IsTableMissingError(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return false;
        return msg.IndexOf("table not found", StringComparison.OrdinalIgnoreCase) >= 0
            || msg.IndexOf("찾을 수 없", StringComparison.Ordinal) >= 0;
    }

    // ─────────────────────────────────────────
    // 서버 데이터 저장
    // ─────────────────────────────────────────

    /// <summary>
    /// 현재 VIP 데이터를 서버에 저장합니다.
    /// ★ inDate로 행을 특정해서 업데이트 (vip_level WHERE 사용 시 등급 변경 후 불일치 버그)
    /// </summary>
    private void SaveToServer(Action onSuccess = null, Action onFail = null)
    {
        // ★ 테이블 없음이면 서버 호출 스킵
        if (_tableMissing) return;

        // ★ 로그인 상태 확인 (미로그인 시 서버 저장 스킵)
        string userInDate = null;
        try { userInDate = Backend.UserInDate; } catch { }
        if (string.IsNullOrEmpty(userInDate))
        {
            Debug.Log("[VipManager] 서버 미로그인 → 서버 저장 스킵 (로컬은 이미 저장됨)");
            return;
        }

        Param param = new Param();
        param.Add("vip_level", CurrentVipLevel);
        param.Add("vip_exp", CurrentVipExp);
        param.Add("expire_date", ExpireDate);
        param.Add("free_gift_claimed", IsFreeGiftClaimed);

        if (!string.IsNullOrEmpty(_serverInDate))
        {
            // ★ inDate로 정확한 행 특정 → 등급이 바뀌어도 안전
            Backend.GameData.UpdateV2(tableName, _serverInDate, Backend.UserInDate, param, callback =>
            {
                if (!callback.IsSuccess())
                {
                    if (IsTableMissingError(callback.GetMessage()))
                    {
                        _tableMissing = true;
                        Debug.LogWarning($"[VipManager] '{tableName}' 테이블 없음 → 이후 서버 호출 스킵");
                        onFail?.Invoke();
                        return;
                    }
                    Debug.LogError($"[VipManager] 저장 실패: {callback.GetMessage()}");
                    onFail?.Invoke();
                    return;
                }
                Debug.Log("[VipManager] 저장 완료 (inDate)");
                onSuccess?.Invoke();
            });
        }
        else
        {
            // inDate 미확보 → Insert 폴백
            Backend.GameData.Insert(tableName, param, callback =>
            {
                if (!callback.IsSuccess())
                {
                    if (IsTableMissingError(callback.GetMessage()))
                    {
                        _tableMissing = true;
                        Debug.LogWarning($"[VipManager] '{tableName}' 테이블 없음 → 이후 서버 호출 스킵. 뒤끝 콘솔에서 Private 테이블 생성 필요.");
                        onFail?.Invoke();
                        return;
                    }
                    Debug.LogError($"[VipManager] 저장 실패 (Insert): {callback.GetMessage()}");
                    onFail?.Invoke();
                    return;
                }
                // 새 행의 inDate 저장
                JsonData result = callback.GetReturnValuetoJSON();
                if (result != null && result.ContainsKey("inDate"))
                    _serverInDate = result["inDate"].ToString();
                Debug.Log("[VipManager] 저장 완료 (Insert)");
                onSuccess?.Invoke();
            });
        }
    }

    // ─────────────────────────────────────────
    // VIP 경험치 추가
    // ─────────────────────────────────────────

    /// <summary>
    /// VIP 경험치를 추가하고 등급업 체크 후 서버에 저장합니다.
    /// 상점 구매, 접속 보상 등에서 호출하세요.
    /// </summary>
    public void AddVipExp(int amount)
    {
        CurrentVipExp += amount;
        CheckLevelUp();

        SaveToServer(onSuccess: () =>
        {
            OnVipDataChanged?.Invoke();
        });
    }

    /// <summary>
    /// 경험치가 다음 등급 기준치 이상이면 자동으로 등급을 올립니다.
    /// 여러 등급을 한번에 뛸 수 있도록 재귀 호출합니다.
    /// </summary>
    private void CheckLevelUp()
    {
        VipData next = GetVipData(CurrentVipLevel + 1);
        if (next == null) return; // 최고 등급이면 종료

        if (CurrentVipExp >= next.requiredVipExp)
        {
            CurrentVipLevel++;
            // ★ 새 등급 선물 초기화 (등급마다 1회씩 수령 가능)
            IsFreeGiftClaimed = false;
            IsPaidGiftPurchased = false;
            Debug.Log($"[VipManager] VIP 등급 상승! → VIP{CurrentVipLevel} (선물 초기화)");
            CheckLevelUp(); // 연속 등급업 체크
        }
    }

    // ─────────────────────────────────────────
    // 무료 선물 수령
    // ─────────────────────────────────────────

    /// <summary>VIP 기간이 유효한지 확인합니다.</summary>
    public bool IsVipActive()
    {
        if (CurrentVipLevel <= 0) return false;
        return GetRemainingTime() > TimeSpan.Zero;
    }

    /// <summary>
    /// 무료 선물 수령을 처리합니다.
    /// 이미 수령했거나 VIP 기간이 만료되면 실패합니다.
    /// </summary>
    /// <summary>특정 등급의 무료 선물 수령 (VipUI에서 탭 등급 전달)</summary>
    public void ClaimFreeGift(int level = -1)
    {
        if (level <= 0) level = CurrentVipLevel;

        if (level > CurrentVipLevel)
        {
            UIManager.Instance?.ShowMessage("등급이 부족합니다.", Color.red);
            return;
        }

        if (IsFreeLevelClaimed(level))
        {
            UIManager.Instance?.ShowMessage("이미 수령한 선물입니다.", Color.yellow);
            return;
        }

        if (!IsVipActive())
        {
            UIManager.Instance?.ShowMessage("VIP 기간이 만료되었습니다.", Color.red);
            return;
        }

        // ★ 등급별 수령 기록
        MarkFreeClaimed(level);
        IsFreeGiftClaimed = (level == CurrentVipLevel); // 하위 호환

        VipData data = GetVipData(level);
        if (data != null)
        {
            if (data.giftInfo.freeRewards != null && data.giftInfo.freeRewards.Length > 0)
                GiveRewards(data.giftInfo.freeRewards);
            else
                AddGoldSafe(data.giftInfo.paidGiftPrice * 10);

            Debug.Log($"[VipManager] VIP{level} 무료 선물 수령 완료");
        }

        OnFreeGiftClaimed?.Invoke();
        OnVipDataChanged?.Invoke();
        SaveLoadManager.Instance?.SaveGame();
        UIManager.Instance?.ShowMessage($"VIP{level} 선물을 수령했습니다!", Color.green);
        SaveToServer();
    }

    // ─────────────────────────────────────────
    // 유료 선물 구매
    // ─────────────────────────────────────────

    /// <summary>
    /// 유료 VIP 선물을 구매합니다.
    /// 다이아 차감 후 VIP 경험치를 지급합니다.
    /// </summary>
    public void PurchaseVipGift(int vipLevel)
    {
        if (!IsVipActive())
        {
            UIManager.Instance?.ShowMessage("VIP 기간을 먼저 연장해주세요!", Color.red);
            return;
        }

        if (IsPaidLevelClaimed(vipLevel))
        {
            UIManager.Instance?.ShowMessage("이미 구매한 선물입니다.", Color.yellow);
            return;
        }

        VipData data = GetVipData(vipLevel);
        if (data == null)
        {
            Debug.LogError($"[VipManager] VIP{vipLevel} 데이터 없음");
            return;
        }

        int price = data.giftInfo.paidGiftPrice;

        if (!TrySpendGem(price, dryRun: true))
        {
            UIManager.Instance?.ShowMessage("다이아가 부족합니다.", Color.red);
            return;
        }

        UIManager.Instance?.ShowConfirmDialog(
            $"VIP{vipLevel} 선물을 {price} 다이아로 구매하시겠습니까?",
            onConfirm: () =>
            {
                if (IsPaidLevelClaimed(vipLevel)) return;
                if (!TrySpendGem(price, dryRun: false)) return;

                MarkPaidClaimed(vipLevel);

                if (data.giftInfo.paidRewards != null && data.giftInfo.paidRewards.Length > 0)
                {
                    GiveRewards(data.giftInfo.paidRewards);
                }
                else
                {
                    AddGoldSafe(data.giftInfo.paidGiftOriginalValue);
                }

                AddVipExp(data.vipLevel * 50);
                OnVipPurchased?.Invoke(vipLevel);
                OnVipDataChanged?.Invoke();
                SaveLoadManager.Instance?.SaveGame();
                UIManager.Instance?.ShowMessage($"VIP{vipLevel} 선물 구매 완료!", Color.green);
            }
        );
    }

    /// <summary>VIP 기간을 연장합니다 (다이아로 30일 추가).</summary>
    public void ExtendVipDuration(int days = 30, int gemCost = 500)
    {
        if (!TrySpendGem(gemCost, dryRun: true))
        {
            UIManager.Instance?.ShowMessage("다이아가 부족합니다.", Color.red);
            return;
        }

        UIManager.Instance?.ShowConfirmDialog(
            $"VIP 기간을 {days}일 연장하시겠습니까?\n({gemCost} 다이아)",
            onConfirm: () =>
            {
                Debug.Log($"[VipManager] ★ 연장 콜백 진입! 현재 젬={GameManager.Instance?.PlayerGem}, gemCost={gemCost}");

                if (!TrySpendGem(gemCost, dryRun: false))
                {
                    Debug.LogError($"[VipManager] ❌ 젬 부족으로 연장 실패! 필요={gemCost}, 보유={GameManager.Instance?.PlayerGem}");
                    UIManager.Instance?.ShowMessage("다이아가 부족합니다.", Color.red);
                    return;
                }

                Debug.Log($"[VipManager] ✅ 젬 차감 성공! 남은 젬={GameManager.Instance?.PlayerGem}");

                // 기간 연장: 현재 만료일이 미래면 그 위에 추가, 과거/빈값이면 지금부터
                DateTime baseDate = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(ExpireDate) && DateTime.TryParse(ExpireDate, out DateTime existing))
                {
                    if (existing > DateTime.UtcNow)
                        baseDate = existing;
                }

                string newExpire = baseDate.AddDays(days).ToString("o"); // ★ ISO 8601 라운드트립 포맷
                ExpireDate = newExpire;

                Debug.Log($"[VipManager] ★ 기간 연장: baseDate={baseDate}, 새 만료일={newExpire}");

                // VIP 레벨이 0이면 1로 설정
                if (CurrentVipLevel <= 0)
                    CurrentVipLevel = 1;

                // 선물 초기화 (새 기간)
                IsFreeGiftClaimed = false;
                IsPaidGiftPurchased = false;

                // ★ SaveData에도 즉시 반영 (GameDataBridge 경유)
                if (GameDataBridge.CurrentData != null)
                {
                    GameDataBridge.CurrentData.vipLevel = CurrentVipLevel;
                    GameDataBridge.CurrentData.vipExp = CurrentVipExp;
                    GameDataBridge.CurrentData.vipExpireDate = ExpireDate;
                    GameDataBridge.CurrentData.vipFreeGiftClaimed = IsFreeGiftClaimed;
                    GameDataBridge.CurrentData.vipPaidGiftPurchased = IsPaidGiftPurchased;
                }

                Debug.Log($"[VipManager] ✅ VipLevel={CurrentVipLevel}, ExpireDate={ExpireDate}, IsVipActive={IsVipActive()}");

                // UI 즉시 갱신
                OnVipDataChanged?.Invoke();
                SaveLoadManager.Instance?.SaveGame();

                string remaining = GetRemainingTimeString();
                UIManager.Instance?.ShowMessage($"VIP {days}일 연장 완료! ({remaining})", Color.green);
                Debug.Log($"[VipManager] ✅ 연장 완료! 남은 기간: {remaining}, VipLevel={CurrentVipLevel}");

                // 서버에도 저장 (비동기, 실패해도 로컬은 이미 저장됨)
                SaveToServer();
            }
        );
    }

    // ─────────────────────────────────────────
    // 보상 지급 시스템
    // ─────────────────────────────────────────

    /// <summary>보상 목록을 순서대로 지급</summary>
    private void GiveRewards(VipRewardEntry[] rewards)
    {
        if (rewards == null) return;

        foreach (var r in rewards)
        {
            if (r.amount <= 0) continue;

            switch (r.rewardType)
            {
                case VipRewardType.Gold:
                    AddGoldSafe(r.amount);
                    UIManager.Instance?.ShowMessage($"+{r.amount:N0} 골드", Color.yellow);
                    break;

                case VipRewardType.Gem:
                    if (GameManager.Instance != null)
                        GameManager.Instance.AddGem(r.amount);
                    else if (GameDataBridge.CurrentData != null)
                        GameDataBridge.CurrentData.playerGem += r.amount;
                    UIManager.Instance?.ShowMessage($"+{r.amount:N0} 다이아", Color.cyan);
                    break;

                case VipRewardType.EquipmentTicket:
                    if (ResourceBarManager.Instance != null)
                        ResourceBarManager.Instance.AddEquipmentTickets(r.amount);
                    else if (GameDataBridge.CurrentData != null)
                        GameDataBridge.CurrentData.equipmentTickets += r.amount;
                    UIManager.Instance?.ShowMessage($"+{r.amount} 장비 티켓", Color.green);
                    break;

                case VipRewardType.CompanionTicket:
                    if (ResourceBarManager.Instance != null)
                        ResourceBarManager.Instance.AddCompanionTickets(r.amount);
                    else if (GameDataBridge.CurrentData != null)
                        GameDataBridge.CurrentData.companionTickets += r.amount;
                    UIManager.Instance?.ShowMessage($"+{r.amount} 동료 티켓", Color.green);
                    break;

                case VipRewardType.CropPoint:
                    CropPointService.Add(r.amount);
                    UIManager.Instance?.ShowMessage($"+{r.amount} 작물 포인트", Color.green);
                    break;

                case VipRewardType.Item:
                    if (r.item != null && InventoryManager.Instance != null)
                    {
                        InventoryManager.Instance.AddItem(r.item, r.amount);
                        UIManager.Instance?.ShowMessage($"+{r.amount} {r.item.itemName}", Color.green);
                    }
                    break;
            }

            Debug.Log($"[VipManager] 보상 지급: {r.rewardType} x{r.amount}");
        }
    }

    /// <summary>골드 안전 지급 — GameManager → GameDataBridge 폴백</summary>
    private void AddGoldSafe(long amount)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.AddGold(amount);
        else if (GameDataBridge.CurrentData != null)
            GameDataBridge.CurrentData.playerGold += amount;
    }

    /// <summary>다이아 차감 헬퍼 — GameManager → GameDataBridge 폴백</summary>
    private bool TrySpendGem(int amount, bool dryRun)
    {
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.PlayerGem < amount) return false;
            if (!dryRun) GameManager.Instance.SpendGem(amount);
            return true;
        }
        if (GameDataBridge.CurrentData != null)
        {
            if (GameDataBridge.CurrentData.playerGem < amount) return false;
            if (!dryRun) GameDataBridge.CurrentData.playerGem -= amount;
            return true;
        }
        return false;
    }

    // ─────────────────────────────────────────
    // VIP 만료 시간
    // ─────────────────────────────────────────

    /// <summary>
    /// 남은 VIP 유효기간을 반환합니다.
    /// 만료됐거나 비VIP면 TimeSpan.Zero 반환
    /// </summary>
    public TimeSpan GetRemainingTime()
    {
        if (string.IsNullOrEmpty(ExpireDate)) return TimeSpan.Zero;

        // ★ RoundtripKind만 사용 (AssumeUniversal과 동시 사용 불가)
        if (DateTime.TryParse(ExpireDate, null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime expireDateTime))
        {
            // UTC로 통일하여 비교
            DateTime expireUtc = expireDateTime.Kind == DateTimeKind.Utc
                ? expireDateTime
                : expireDateTime.ToUniversalTime();

            TimeSpan remaining = expireUtc - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        Debug.LogWarning($"[VipManager] ExpireDate 파싱 실패: '{ExpireDate}'");
        return TimeSpan.Zero;
    }

    /// <summary>
    /// 남은 기간을 "37일 15시간 52분" 형태 문자열로 반환합니다.
    /// UI 텍스트에 바로 사용하세요.
    /// </summary>
    public string GetRemainingTimeString()
    {
        if (CurrentVipLevel <= 0) return "VIP 미가입";
        if (string.IsNullOrEmpty(ExpireDate)) return "기간 미설정";

        TimeSpan t = GetRemainingTime();
        if (t <= TimeSpan.Zero) return "만료됨";

        int days = (int)t.TotalDays;
        int hours = t.Hours;
        int minutes = t.Minutes;

        if (days > 0) return $"{days}일 {hours}시간 {minutes}분";
        if (hours > 0) return $"{hours}시간 {minutes}분";
        return $"{minutes}분";
    }

    // ─────────────────────────────────────────
    // VipData ScriptableObject 조회
    // ─────────────────────────────────────────

    /// <summary>
    /// 특정 등급의 VipData를 반환합니다.
    /// 범위 밖이면 null 반환
    /// </summary>
    public VipData GetVipData(int vipLevel)
    {
        int index = vipLevel - 1; // vipLevel 1 → index 0
        if (index < 0 || index >= vipDataList.Count) return null;
        return vipDataList[index];
    }

    /// <summary>현재 플레이어 등급의 VipData 반환</summary>
    public VipData GetCurrentVipData() => GetVipData(CurrentVipLevel);

    /// <summary>다음 등급의 VipData 반환 (최고등급이면 null)</summary>
    public VipData GetNextVipData() => GetVipData(CurrentVipLevel + 1);

    /// <summary>전체 VIP 등급 수 반환 (탭 버튼 동적 생성에 사용)</summary>
    public int GetMaxVipLevel() => vipDataList.Count;

    /// <summary>
    /// 현재 등급 내 경험치 진행률 0.0 ~ 1.0 반환.
    /// VipUI 경험치 슬라이더에 사용합니다.
    /// </summary>
    public float GetExpProgress()
    {
        VipData next = GetNextVipData();
        if (next == null) return 1f; // 최고 등급이면 꽉 참

        VipData current = GetCurrentVipData();
        int baseExp = current != null ? current.requiredVipExp : 0;
        int range = next.requiredVipExp - baseExp;

        if (range <= 0) return 1f;
        return Mathf.Clamp01((float)(CurrentVipExp - baseExp) / range);
    }

    // ─────────────────────────────────────────
    // 에디터 테스트 (인스펙터 우클릭 메뉴)
    // ─────────────────────────────────────────

    // ─────────────────────────────────────────
    // 로컬 저장/로드 (서버 없이 동작)
    // ─────────────────────────────────────────

    /// <summary>SaveLoadManager에서 호출 — 로컬 세이브 데이터를 VipManager에 적용</summary>
    public void ApplyLocalData(int level, int exp, string expireDate, bool giftClaimed, bool paidPurchased = false,
        string claimedFree = "", string claimedPaid = "")
    {
        CurrentVipLevel = level;
        CurrentVipExp = exp;
        ExpireDate = expireDate;
        IsFreeGiftClaimed = giftClaimed;
        IsPaidGiftPurchased = paidPurchased;
        ClaimedFreeLevels = claimedFree ?? "";
        ClaimedPaidLevels = claimedPaid ?? "";
        IsDataLoaded = true;
        OnVipDataChanged?.Invoke();
        Debug.Log($"[VipManager] 로컬 데이터 적용: VIP{level}, EXP={exp}, FreeClaimed={ClaimedFreeLevels}, PaidClaimed={ClaimedPaidLevels}");
    }

    /// <summary>GameDataBridge에서 VIP 데이터 로드 (Start 시 폴백)</summary>
    private void LoadFromLocal()
    {
        if (GameDataBridge.CurrentData == null) return;

        var data = GameDataBridge.CurrentData;
        CurrentVipLevel = data.vipLevel;
        CurrentVipExp = data.vipExp;
        ExpireDate = data.vipExpireDate ?? "";
        IsFreeGiftClaimed = data.vipFreeGiftClaimed;
        IsDataLoaded = true;
        Debug.Log($"[VipManager] 로컬 폴백 로드: VIP{CurrentVipLevel}, EXP={CurrentVipExp}");
    }

    /// <summary>VipData SO가 없을 때 기본 10등급 데이터 생성 (런타임 전용)</summary>
    private void GenerateDefaultVipData()
    {
        vipDataList = new List<VipData>();
        int[] requiredExp = { 100, 300, 600, 1000, 2000, 3500, 5500, 8000, 12000, 20000 };
        Color[] gradeColors =
        {
            new Color(0.8f, 0.8f, 0.8f),   // VIP1 회색
            new Color(0.5f, 0.9f, 0.5f),   // VIP2 연녹
            new Color(0.3f, 0.7f, 1f),     // VIP3 파랑
            new Color(0.6f, 0.4f, 1f),     // VIP4 보라
            new Color(1f, 0.7f, 0.2f),     // VIP5 주황
            new Color(1f, 0.5f, 0.5f),     // VIP6 빨강
            new Color(1f, 0.85f, 0.2f),    // VIP7 금색
            new Color(0.2f, 1f, 0.9f),     // VIP8 청록
            new Color(1f, 0.4f, 0.8f),     // VIP9 핑크
            new Color(1f, 1f, 0.4f),       // VIP10 밝은금
        };

        for (int i = 0; i < 10; i++)
        {
            VipData data = ScriptableObject.CreateInstance<VipData>();
            data.vipLevel = i + 1;
            data.displayName = $"VIP{i + 1}";
            data.requiredVipExp = requiredExp[i];
            data.gradeColor = gradeColors[i];
            data.benefits = new List<VipBenefitData>
            {
                new VipBenefitData
                {
                    description = $"보스 도전 횟수 +{i + 1}",
                    value = i + 1,
                    benefitType = VipBenefitType.BossCountBonus
                },
                new VipBenefitData
                {
                    description = $"던전 입장 횟수 +{Mathf.CeilToInt((i + 1) * 0.5f)}",
                    value = Mathf.CeilToInt((i + 1) * 0.5f),
                    benefitType = VipBenefitType.DungeonEntryBonus
                },
                new VipBenefitData
                {
                    description = $"경험치 보너스 +{(i + 1) * 5}%",
                    value = (i + 1) * 5,
                    benefitType = VipBenefitType.ExpItemPurchaseBonus
                },
                new VipBenefitData
                {
                    description = $"공격력 보너스 +{(i + 1) * 2}%",
                    value = (i + 1) * 2,
                    benefitType = VipBenefitType.AttackBonus
                }
            };

            // VIP6+ 추가 혜택
            if (i >= 5)
            {
                data.benefits.Add(new VipBenefitData
                {
                    description = "무료보스 무제한입장",
                    value = 1,
                    benefitType = VipBenefitType.FreeBossEntry
                });
            }
            if (i >= 7)
            {
                data.benefits.Add(new VipBenefitData
                {
                    description = "비밀거래 개방",
                    value = 1,
                    benefitType = VipBenefitType.SecretTrade
                });
            }

            data.giftInfo = new VipGiftInfo
            {
                freeGiftDescription = $"VIP{i + 1} 무료 선물",
                paidGiftPrice = (i + 1) * 200,
                paidGiftOriginalValue = (i + 1) * 2000,
                discountPercent = $"{90 - i}%"
            };
            data.defaultDurationDays = 30;
            vipDataList.Add(data);
        }
        Debug.Log($"[VipManager] 기본 VipData {vipDataList.Count}등급 생성 완료");
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 경험치 +100")]
    private void TestAddExp()
    {
        CurrentVipExp += 100;
        OnVipDataChanged?.Invoke();
        Debug.Log($"[VipManager] 테스트 경험치 +100 → 현재: {CurrentVipExp}");
    }

    [ContextMenu("테스트: 무료선물 수령 초기화")]
    private void TestResetGift()
    {
        IsFreeGiftClaimed = false;
        OnVipDataChanged?.Invoke();
        Debug.Log("[VipManager] 무료 선물 수령 상태 초기화");
    }
#endif
}