using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
// FarmCropSlotUI.cs
// 씨앗 슬롯 컴포넌트 (채소씨앗 / 과일씨앗 스크롤 Content 안 프리팹)
//
// 프리팹 Hierarchy:
//   FarmCropSlot (Button + FarmCropSlotUI)
//     ├ CropImage      (Image)          ← cropImage   (seedIcon)
//     ├ CropNameText   (TMP)            ← cropNameText
//     ├ CostText       (TMP)            ← costText    "💎5" or "💰200"
//     ├ OwnedText      (TMP)            ← ownedText   "보유 3개"
//     └ LockOverlay    (GameObject)     ← lockOverlay
//         └ LockText   (TMP)            "Lv.5 필요"
// ═══════════════════════════════════════════════════════════════════

public class FarmCropSlotUI : MonoBehaviour
{
    public Image cropImage;
    public TextMeshProUGUI cropNameText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI ownedText;
    public GameObject lockOverlay;
    public Button selectButton;

    private CropData linkedCrop;

    // ─── 초기 셋업 ────────────────────────────────────────────────
    public void Setup(CropData crop, bool isUnlocked, int owned, System.Action onSelect)
    {
        linkedCrop = crop;

        if (cropImage && crop.seedIcon) cropImage.sprite = crop.seedIcon;
        if (cropNameText) cropNameText.text = crop.cropName;

        bool useGem = crop.seedCostGem > 0;
        if (costText) costText.text = useGem ? $"💎 {crop.seedCostGem}" : $"💰 {crop.seedCostGold}";

        UpdateOwned(owned);

        if (lockOverlay)
        {
            lockOverlay.SetActive(!isUnlocked);
            if (!isUnlocked)
            {
                var lt = lockOverlay.GetComponentInChildren<TextMeshProUGUI>();
                if (lt)
                {
                    string reason = "";
                    if (crop.requiredPlayerLevel > 1)
                        reason = $"Lv.{crop.requiredPlayerLevel} 필요";
                    else if (crop.requiredGreenhouseLevel > 1)
                        reason = $"비닐하우스 Lv.{crop.requiredGreenhouseLevel}";
                    lt.text = reason;
                }
            }
        }

        // onSelect가 null이면 리스너 등록 안 함 (외부에서 직접 Button에 등록)
        if (onSelect != null)
        {
            selectButton?.onClick.RemoveAllListeners();
            selectButton?.onClick.AddListener(() => onSelect.Invoke());
        }
    }

    /// <summary>구매 후 보유 수량만 갱신 (슬롯 재생성 없이)</summary>
    public void UpdateOwned(int owned)
    {
        if (ownedText) ownedText.text = owned > 0 ? $"보유 {owned}개" : "";
    }
}