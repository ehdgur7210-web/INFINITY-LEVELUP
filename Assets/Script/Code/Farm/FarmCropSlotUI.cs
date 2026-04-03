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

    public CropData LinkedCrop => linkedCrop;
    private CropData linkedCrop;

    // ─── 초기 셋업 ────────────────────────────────────────────────
    public void Setup(CropData crop, bool isUnlocked, int owned, System.Action onSelect)
    {
        linkedCrop = crop;

        // ★ selectButton이 없으면 루트 Button 사용
        if (selectButton == null)
            selectButton = GetComponent<Button>();

        // ★ Inspector 미연결 대비: 자식에서 TMP 자동 탐색 (New Text 잔류 방지)
        AutoFindTextFields();

        // ★ 아이콘 설정: seedIcon → growthSprites[0] → harvestIcon 순서로 폴백
        Sprite icon = crop.seedIcon;
        if (icon == null && crop.growthSprites != null && crop.growthSprites.Length > 0)
            icon = crop.growthSprites[0];
        if (icon == null)
            icon = crop.harvestIcon;

        if (cropImage != null)
        {
            cropImage.sprite = icon;
            cropImage.color = Color.white;
            cropImage.enabled = true;
            cropImage.gameObject.SetActive(true);
            // ★ SetNativeSize 제거 — 슬롯 레이아웃을 망칠 수 있음
        }

        if (cropNameText) cropNameText.text = crop.cropName;

        bool useGem = crop.seedCostGem > 0;
        if (costText) costText.text = useGem ? $"{crop.seedCostGem}" : $"{crop.seedCostGold}";

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

        // onSelect 리스너 등록
        if (onSelect != null)
        {
            selectButton?.onClick.RemoveAllListeners();
            selectButton?.onClick.AddListener(() => onSelect.Invoke());
        }
    }

    /// <summary>Inspector 미연결 시 자식에서 이름 기반 자동 탐색</summary>
    private void AutoFindTextFields()
    {
        if (cropNameText != null && costText != null && ownedText != null) return;

        var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in tmps)
        {
            string n = t.gameObject.name.ToLower();
            if (cropNameText == null && (n.Contains("name") || n.Contains("cropname")))
                cropNameText = t;
            else if (costText == null && n.Contains("cost"))
                costText = t;
            else if (ownedText == null && n.Contains("owned"))
                ownedText = t;
        }

        // 아직 못 찾은 필드가 있으면 순서 기반 폴백 (Name → Cost → Owned)
        if (tmps.Length > 0)
        {
            var unassigned = new System.Collections.Generic.List<TextMeshProUGUI>();
            foreach (var t in tmps)
            {
                if (t != cropNameText && t != costText && t != ownedText)
                    unassigned.Add(t);
            }
            if (cropNameText == null && unassigned.Count > 0) { cropNameText = unassigned[0]; unassigned.RemoveAt(0); }
            if (costText == null && unassigned.Count > 0) { costText = unassigned[0]; unassigned.RemoveAt(0); }
            if (ownedText == null && unassigned.Count > 0) { ownedText = unassigned[0]; unassigned.RemoveAt(0); }
        }
    }

    /// <summary>구매 후 보유 수량만 갱신 (슬롯 재생성 없이)</summary>
    public void UpdateOwned(int owned)
    {
        if (ownedText) ownedText.text = owned > 0 ? $"보유 {owned}개" : "";
    }
}
