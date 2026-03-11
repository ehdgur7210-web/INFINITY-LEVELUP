using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
// FarmSupplySlotUI.cs
// 물 슬롯 / 비료 슬롯 공용 컴포넌트
//
// ★ 물스크롤 Content, 비료스크롤 Content 안에 넣을 프리팹에 사용
//
// 프리팹 Hierarchy:
//   FarmSupplySlot (Button + FarmSupplySlotUI)
//     ├ IconImage      (Image)          ← iconImage
//     ├ NameText       (TMP)            ← nameText
//     ├ CostText       (TMP)            ← costText   "💎10" or "💰500"
//     ├ EffectText     (TMP)            ← effectText "+20% 단축" / "수확+30%"
//     ├ OwnedText      (TMP)            ← ownedText  "보유 3개"
//     └ LockOverlay    (GameObject)     ← lockOverlay
//         └ LockText   (TMP)            "Lv.5 필요"
// ═══════════════════════════════════════════════════════════════════

public class FarmSupplySlotUI : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI effectText;
    public TextMeshProUGUI ownedText;
    public GameObject lockOverlay;
    public Button selectButton;

    // ─── 물 슬롯 셋업 ─────────────────────────────────────────────
    public void SetupWater(WaterData water, int owned, bool isUnlocked, System.Action onClick)
    {
        if (iconImage && water.icon) iconImage.sprite = water.icon;
        if (nameText) nameText.text = water.waterName;
        if (costText) costText.text = water.costGem > 0 ? $"💎 {water.costGem}" : $"💰 {water.costGold}";
        if (effectText) effectText.text = water.extraSpeedBonus > 0
                                            ? $"+{water.extraSpeedBonus * 100f:F0}% 단축"
                                            : "기본 물주기";
        if (ownedText) ownedText.text = $"보유 {owned}개";

        SetLock(!isUnlocked, water.requiredPlayerLevel);

        // onClick null이면 외부에서 직접 Button에 등록하는 방식 사용
        if (onClick != null)
        {
            selectButton?.onClick.RemoveAllListeners();
            selectButton?.onClick.AddListener(() => onClick.Invoke());
        }
    }

    // ─── 비료 슬롯 셋업 ───────────────────────────────────────────
    public void SetupFertilizer(FertilizerData fert, int owned, bool isUnlocked, System.Action onClick)
    {
        if (iconImage && fert.icon) iconImage.sprite = fert.icon;
        if (nameText) nameText.text = fert.fertilizerName;
        if (costText) costText.text = fert.costGem > 0 ? $"💎 {fert.costGem}" : $"💰 {fert.costGold}";
        if (effectText) effectText.text = $"수확 +{fert.yieldBonus * 100f:F0}%  속도 +{fert.speedBonus * 100f:F0}%";
        if (ownedText) ownedText.text = $"보유 {owned}개";

        SetLock(!isUnlocked, fert.requiredPlayerLevel);

        if (onClick != null)
        {
            selectButton?.onClick.RemoveAllListeners();
            selectButton?.onClick.AddListener(() => onClick.Invoke());
        }
    }

    /// <summary>구매 후 보유 수량만 갱신 (슬롯 재생성 없이)</summary>
    public void UpdateOwned(int owned)
    {
        if (ownedText) ownedText.text = $"보유 {owned}개";
    }

    private void SetLock(bool locked, int reqLv)
    {
        if (lockOverlay == null) return;
        lockOverlay.SetActive(locked);
        if (locked)
        {
            var lt = lockOverlay.GetComponentInChildren<TextMeshProUGUI>();
            if (lt) lt.text = $"Lv.{reqLv} 필요";
        }
    }
}