using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
// CompanionAscensionTab.cs
//
// 동료 상세 패널 — [승성] 탭 (별 승급)
//
// ★ 레이아웃:
//   ┌──────────────────┬──────────────────┐
//   │  [동료 아이콘]   │ 같은 동료 목록   │
//   │  이름 ★★★★★    │ (재료용 중복분)  │
//   │       ↓          │ ┌──┐┌──┐┌──┐    │
//   │  이름 ★★★★★★  │ │  ││  ││  │    │
//   │                  │ └──┘└──┘└──┘    │
//   │  필요 재료: 2체  │                  │
//   │  성공확률: 80%   │                  │
//   │  골드: 50,000    │                  │
//   │                  │                  │
//   │  [승성]          │                  │
//   └──────────────────┴──────────────────┘
//
// ★ 승성 규칙:
//   - 같은 동료의 중복분을 재료로 사용하여 별 +1
//   - ★1→2: 재료 1체, 100%
//   - ★2→3: 재료 1체, 100%
//   - ★3→4: 재료 1체, 90%
//   - ★4→5: 재료 2체, 80%
//   - ★5→6: 재료 2체, 70%
//   - ★6→7: 재료 3체, 60%
//   - ★7+:  재료 3체, 50%
//   - 실패 시 재료만 소멸, 별은 유지
// ═══════════════════════════════════════════════════════════════════

public class CompanionAscensionTab : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  Inspector 필드
    // ─────────────────────────────────────────────────────────────

    [Header("===== 현재 동료 =====")]
    [SerializeField] private Image companionIcon;
    [SerializeField] private TextMeshProUGUI companionNameText;
    [SerializeField] private TextMeshProUGUI currentStarsText;    // "★★★★★"
    [SerializeField] private Image arrowImage;                     // ↓ 화살표 이미지
    [SerializeField] private TextMeshProUGUI nextStarsText;       // "★★★★★★"

    [Header("===== 승성 조건 =====")]
    [SerializeField] private TextMeshProUGUI requiredCountText;   // "필요 재료: 같은 동료 2체"
    [SerializeField] private TextMeshProUGUI ownedCountText;      // "보유: 3체"
    [SerializeField] private TextMeshProUGUI successRateText;     // "성공확률: 80%"
    [SerializeField] private TextMeshProUGUI goldCostText;        // "비용: 50,000 골드"

    [Header("===== 승성 버튼 =====")]
    [SerializeField] private Button ascensionButton;
    [SerializeField] private TextMeshProUGUI ascensionButtonText;

    [Header("===== 스탯 보너스 미리보기 =====")]
    [SerializeField] private TextMeshProUGUI statBonusText;       // "전 스탯 +10%"
    [SerializeField] private Image statBonusArrowImage;            // → 화살표 이미지
    [SerializeField] private TextMeshProUGUI statBonusNextText;    // "+20%" (다음 값)

    [Header("===== 승성 설정 (Inspector 조정 가능) =====")]
    [SerializeField] private int baseGoldCost = 10000;            // 기본 골드 비용
    [SerializeField] private float starStatBonus = 0.1f;          // 별 1개당 스탯 보너스 (10%)

    // ─────────────────────────────────────────────────────────────
    //  내부 변수
    // ─────────────────────────────────────────────────────────────

    private CompanionData _companion;
    private int _currentStars;

    // ─────────────────────────────────────────────────────────────
    //  승성 테이블 (별 수 → 필요 재료 수, 성공 확률)
    // ─────────────────────────────────────────────────────────────

    private struct AscensionRequirement
    {
        public int materialCount;  // 필요한 같은 동료 수
        public float successRate;  // 성공 확률 (0~1)

        public AscensionRequirement(int count, float rate) { materialCount = count; successRate = rate; }
    }

    /// <summary>현재 별 수에 따른 승성 요구 사항</summary>
    private AscensionRequirement GetRequirement(int currentStars)
    {
        switch (currentStars)
        {
            case 1:  return new AscensionRequirement(1, 1.00f);
            case 2:  return new AscensionRequirement(1, 1.00f);
            case 3:  return new AscensionRequirement(1, 0.90f);
            case 4:  return new AscensionRequirement(2, 0.80f);
            case 5:  return new AscensionRequirement(2, 0.70f);
            case 6:  return new AscensionRequirement(3, 0.60f);
            default: return new AscensionRequirement(3, 0.50f); // 7★+
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (ascensionButton != null)
            ascensionButton.onClick.AddListener(OnAscensionClicked);
    }

    // ─────────────────────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────────────────────

    /// <summary>탭 활성화 시 호출</summary>
    public void Refresh(CompanionData companion, int level)
    {
        _companion = companion;
        _currentStars = GetCompanionStars(companion);

        RefreshUI();
    }

    // ─────────────────────────────────────────────────────────────
    //  UI 갱신
    // ─────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        if (_companion == null) return;

        int maxStars = _companion.maxStars;
        bool isMaxed = _currentStars >= maxStars;
        var req = GetRequirement(_currentStars);
        int owned = GetOwnedDuplicateCount(_companion);
        int goldCost = GetGoldCost(_currentStars);

        // 동료 정보
        if (companionIcon != null && _companion.portrait != null)
        {
            companionIcon.sprite = _companion.portrait;
            companionIcon.color = Color.white;
        }
        if (companionNameText != null)
            companionNameText.text = _companion.companionName;

        // 별 표시: 현재 → 다음
        if (currentStarsText != null)
            currentStarsText.text = MakeStarString(_currentStars);
        if (arrowImage != null)
            arrowImage.gameObject.SetActive(!isMaxed);
        if (nextStarsText != null)
            nextStarsText.text = isMaxed ? "<color=yellow>MAX</color>" : $"<color=#00FF00>{MakeStarString(_currentStars + 1)}</color>";

        // 조건
        if (requiredCountText != null)
            requiredCountText.text = isMaxed ? "최대 별 등급 도달" : $"필요 재료: 같은 동료 {req.materialCount}체";
        if (ownedCountText != null)
            ownedCountText.text = $"보유 중복분: {owned}체";
        if (successRateText != null)
            successRateText.text = isMaxed ? "" : $"성공확률: <color=#00FF00>{req.successRate * 100f:F0}%</color>";
        if (goldCostText != null)
            goldCostText.text = isMaxed ? "" : $"비용: {goldCost:N0} 골드";

        // 스탯 보너스 미리보기
        if (statBonusText != null)
        {
            if (isMaxed)
            {
                statBonusText.text = $"현재 별 보너스: 전 스탯 +{_currentStars * starStatBonus * 100f:F0}%";
                if (statBonusArrowImage != null) statBonusArrowImage.gameObject.SetActive(false);
                if (statBonusNextText != null) statBonusNextText.gameObject.SetActive(false);
            }
            else
            {
                statBonusText.text = $"승성 시: 전 스탯 +{_currentStars * starStatBonus * 100f:F0}%";
                if (statBonusArrowImage != null) statBonusArrowImage.gameObject.SetActive(true);
                if (statBonusNextText != null)
                {
                    statBonusNextText.gameObject.SetActive(true);
                    statBonusNextText.text = $"<color=#00FF00>+{(_currentStars + 1) * starStatBonus * 100f:F0}%</color>";
                }
            }
        }

        // 버튼
        bool canAscend = !isMaxed && owned >= req.materialCount;
        if (ascensionButton != null)
            ascensionButton.interactable = canAscend;
        if (ascensionButtonText != null)
        {
            if (isMaxed) ascensionButtonText.text = "최대 등급";
            else if (owned < req.materialCount) ascensionButtonText.text = $"재료 부족 ({owned}/{req.materialCount})";
            else ascensionButtonText.text = $"승성 ({_currentStars}★ ▶ {_currentStars + 1}★)";
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  승성 실행
    // ─────────────────────────────────────────────────────────────

    private void OnAscensionClicked()
    {
        if (_companion == null) return;
        if (_currentStars >= _companion.maxStars) return;

        var req = GetRequirement(_currentStars);
        int owned = GetOwnedDuplicateCount(_companion);
        if (owned < req.materialCount) return;

        int goldCost = GetGoldCost(_currentStars);
        if (!TrySpendGold(goldCost)) return;

        SoundManager.Instance?.PlayButtonClick();

        // 재료 소모 (중복분 count 차감)
        ConsumeDuplicates(_companion, req.materialCount);

        // 확률 판정
        bool success = Random.value <= req.successRate;

        if (success)
        {
            _currentStars++;
            SetCompanionStars(_companion, _currentStars);

            UIManager.Instance?.ShowMessage(
                $"승성 성공! {_companion.companionName} → {MakeStarString(_currentStars)}",
                Color.green);
            SoundManager.Instance?.PlayQuestReward();
            Debug.Log($"[CompanionAscension] 성공: {_companion.companionName} → {_currentStars}★");
        }
        else
        {
            UIManager.Instance?.ShowMessage(
                $"승성 실패... 재료 {req.materialCount}체 소멸 (별 유지: {MakeStarString(_currentStars)})",
                Color.red);
            Debug.Log($"[CompanionAscension] 실패: {_companion.companionName} {_currentStars}★ 유지, 재료 소멸");
        }

        SaveLoadManager.Instance?.SaveGame();
        RefreshUI();
    }

    // ─────────────────────────────────────────────────────────────
    //  데이터 연동
    // ─────────────────────────────────────────────────────────────

    /// <summary>골드 차감 — GameManager → GameDataBridge 폴백</summary>
    private bool TrySpendGold(long amount)
    {
        if (GameManager.Instance != null)
        {
            if (!GameManager.Instance.SpendGold(amount))
            {
                UIManager.Instance?.ShowMessage($"골드가 부족합니다! ({amount:N0} 필요)", Color.red);
                return false;
            }
            return true;
        }
        if (GameDataBridge.CurrentData != null)
        {
            if (GameDataBridge.CurrentData.playerGold < amount)
            {
                UIManager.Instance?.ShowMessage($"골드가 부족합니다! ({amount:N0} 필요)", Color.red);
                return false;
            }
            GameDataBridge.CurrentData.playerGold -= amount;
            return true;
        }
        return false;
    }

    /// <summary>동료의 현재 별 수 조회</summary>
    private int GetCompanionStars(CompanionData data)
    {
        if (data == null) return 1;
        var invMgr = CompanionInventoryManager.Instance;
        if (invMgr == null) return data.baseStars;

        var entryList = invMgr.GetCompanionList();
        if (entryList == null) return data.baseStars;

        foreach (var entry in entryList)
        {
            if (entry != null && entry.data != null && entry.data.companionID == data.companionID)
                return entry.stars > 0 ? entry.stars : data.baseStars;
        }
        return data.baseStars;
    }

    /// <summary>동료의 별 수 저장 (CompanionEntry에 직접 기록)</summary>
    private void SetCompanionStars(CompanionData data, int stars)
    {
        if (data == null) return;
        var invMgr = CompanionInventoryManager.Instance;
        if (invMgr == null) return;

        var entryList = invMgr.GetCompanionList();
        if (entryList == null) return;

        foreach (var entry in entryList)
        {
            if (entry != null && entry.data != null && entry.data.companionID == data.companionID)
            {
                entry.stars = stars;
                return;
            }
        }
    }

    /// <summary>같은 동료 중복 보유 수 (count - 1, 본체 제외)</summary>
    private int GetOwnedDuplicateCount(CompanionData data)
    {
        if (data == null) return 0;
        var invMgr = CompanionInventoryManager.Instance;
        if (invMgr == null) return 0;

        var entryList = invMgr.GetCompanionList();
        if (entryList == null) return 0;

        foreach (var entry in entryList)
        {
            if (entry != null && entry.data != null && entry.data.companionID == data.companionID)
                return Mathf.Max(0, entry.count - 1);
        }
        return 0;
    }

    /// <summary>중복분에서 재료 소모 (count 차감, CompanionEntry에 직접 기록)</summary>
    private void ConsumeDuplicates(CompanionData data, int amount)
    {
        if (data == null) return;
        var invMgr = CompanionInventoryManager.Instance;
        if (invMgr == null) return;

        var entryList = invMgr.GetCompanionList();
        if (entryList == null) return;

        foreach (var entry in entryList)
        {
            if (entry != null && entry.data != null && entry.data.companionID == data.companionID)
            {
                entry.count = Mathf.Max(1, entry.count - amount);
                return;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  유틸리티
    // ─────────────────────────────────────────────────────────────

    private int GetGoldCost(int currentStars)
    {
        return baseGoldCost * currentStars;
    }

    /// <summary>별 수 → 스프라이트 별 문자열</summary>
    private string MakeStarString(int stars)
    {
        if (stars <= 0) return StarSpriteUtil.GetStars(1);
        return StarSpriteUtil.GetColoredStars(stars);
    }
}
