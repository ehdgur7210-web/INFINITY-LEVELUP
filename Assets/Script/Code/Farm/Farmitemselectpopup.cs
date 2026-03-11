using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
// FarmItemSelectPopup.cs  (수정 버전)
//
// ★ 씨앗:    인벤토리(FarmInventoryUI) 보유분만 표시
//             레벨 제한 없음 (상점에서 이미 처리됨)
//             보유 0개면 회색 + "상점에서 구매" 안내
//
// ★ 물/비료: 전체 목록 표시 + 클릭 시 선택만 (결제는 호출부에서)
//             레벨 제한 없음
//
// ★ FarmItemSlot 프리팹:
//   FarmItemSlot (Button)
//     ├ IconImage    (Image)
//     ├ NameText     (TMP)   [0]
//     ├ CostText     (TMP)   [1]
//     ├ SubText      (TMP)   [2]  ← 보유수량 or 효과
//     └ LockOverlay  (GO)
//         └ LockText (TMP)        ← "상점에서 구매"
// ═══════════════════════════════════════════════════════════════════

public class FarmItemSelectPopup : MonoBehaviour
{
    [Header("===== UI =====")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Transform itemContainer;
    [SerializeField] private GameObject itemSlotPrefab;
    [SerializeField] private Button closeButton;

    public Action<CropData> OnSeedSelected;
    public Action<WaterData> OnWaterSelected;
    public Action<FertilizerData> OnFertSelected;

    private readonly List<GameObject> spawnedSlots = new List<GameObject>();

    void Awake()
    {
        closeButton?.onClick.AddListener(Hide);
        gameObject.SetActive(false);
    }

    // ═══ 씨앗 — 인벤토리 보유분만 ═══════════════════════════════
    public void ShowSeeds(List<CropData> crops)
    {
        if (titleText != null) titleText.text = "씨앗 선택";
        ClearSlots();
        gameObject.SetActive(true);

        foreach (var crop in crops)
        {
            int owned = FarmInventoryUI.Instance?.GetSeedCount(crop.cropID) ?? 0;
            bool canUse = owned > 0;

            var go = SpawnSlot(crop.seedIcon, crop.cropName,
                                canUse ? $"보유 {owned}개" : "보유 없음",
                                !canUse, "작물상점에서 구매");
            var cap = crop;
            go.GetComponent<Button>()?.onClick.AddListener(() =>
            {
                if (!canUse)
                {
                    UIManager.Instance?.ShowMessage("씨앗 없음! 상점에서 구매하세요.", Color.yellow);
                    return;
                }
                OnSeedSelected?.Invoke(cap);
                Hide();
            });
        }
    }

    // ═══ 물 — 전체 표시, 선택만 (결제는 호출부) ══════════════════
    public void ShowWaters(List<WaterData> waters)
    {
        if (titleText != null) titleText.text = "물 선택";
        ClearSlots();
        gameObject.SetActive(true);

        foreach (var water in waters)
        {
            string cost = water.costGem > 0 ? $"💎 {water.costGem}" : $"💰 {water.costGold}";
            string sub = water.extraSpeedBonus > 0
                ? $"+{water.extraSpeedBonus * 100f:F0}% 단축"
                : "기본";

            var go = SpawnSlot(water.icon, water.waterName, cost + "  " + sub, false, "");
            var cap = water;
            go.GetComponent<Button>()?.onClick.AddListener(() =>
            {
                OnWaterSelected?.Invoke(cap);
                Hide();
            });
        }
    }

    // ═══ 비료 — 전체 표시, 선택만 ═══════════════════════════════
    public void ShowFertilizers(List<FertilizerData> fertilizers)
    {
        if (titleText != null) titleText.text = "비료 선택";
        ClearSlots();
        gameObject.SetActive(true);

        foreach (var fert in fertilizers)
        {
            string cost = fert.costGem > 0 ? $"💎 {fert.costGem}" : $"💰 {fert.costGold}";
            string sub = $"수확+{fert.yieldBonus * 100f:F0}%";

            var go = SpawnSlot(fert.icon, fert.fertilizerName, cost + "  " + sub, false, "");
            var cap = fert;
            go.GetComponent<Button>()?.onClick.AddListener(() =>
            {
                OnFertSelected?.Invoke(cap);
                Hide();
            });
        }
    }

    // ═══ 숨기기 ══════════════════════════════════════════════════
    public void Hide()
    {
        gameObject.SetActive(false);
        ClearSlots();
        SoundManager.Instance?.PlayPanelClose();
    }

    // ═══ 슬롯 생성 ═══════════════════════════════════════════════
    private GameObject SpawnSlot(Sprite icon, string name, string sub,
                                 bool locked, string lockMsg)
    {
        var go = Instantiate(itemSlotPrefab, itemContainer);
        spawnedSlots.Add(go);

        var iconImg = go.GetComponentInChildren<Image>();
        if (iconImg && icon) iconImg.sprite = icon;

        var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length > 0) texts[0].text = name;
        if (texts.Length > 1) texts[1].text = sub;

        var lockOverlay = go.transform.Find("LockOverlay")?.gameObject;
        lockOverlay?.SetActive(locked);
        if (locked && lockOverlay != null)
        {
            var lt = lockOverlay.GetComponentInChildren<TextMeshProUGUI>();
            if (lt) lt.text = lockMsg;
        }

        // 클릭음은 각 리스너에서 처리
        return go;
    }

    private void ClearSlots()
    {
        foreach (var go in spawnedSlots) if (go != null) Destroy(go);
        spawnedSlots.Clear();
    }
}