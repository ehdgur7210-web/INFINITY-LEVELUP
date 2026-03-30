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

    // ═══ 씨앗 — 보유 시 우선, 미보유도 선택 가능 (심기 시 재화 즉시 차감) ═══
    public void ShowSeeds(List<CropData> crops)
    {
        if (titleText != null) titleText.text = "씨앗 선택";
        ClearSlots();
        gameObject.SetActive(true);

        // ★ FarmInventoryUI.Instance null 대비 FindObjectOfType 폴백
        FarmInventoryUI farmInv = FarmInventoryUI.Instance;
        if (farmInv == null)
            farmInv = FindObjectOfType<FarmInventoryUI>(true);

        foreach (var crop in crops)
        {
            int owned = farmInv?.GetSeedCount(crop.cropID) ?? 0;

            // 보유 여부에 따라 표시 텍스트만 변경 (선택은 항상 가능)
            string subText = owned > 0 ? $"보유 {owned}개" : "재화로 구매";
            var go = SpawnSlot(crop.seedIcon, crop.cropName, subText, false, "");
            var cap = crop;
            go.GetComponent<Button>()?.onClick.AddListener(() =>
            {
                OnSeedSelected?.Invoke(cap);
                Hide();
            });
        }
    }

    // ═══ 물 — 전체 표시, 보유수 표시, 선택만 (결제는 호출부) ═══
    public void ShowWaters(List<WaterData> waters)
    {
        if (titleText != null) titleText.text = "물 선택";
        ClearSlots();
        gameObject.SetActive(true);

        FarmInventoryUI farmInv = FarmInventoryUI.Instance;
        if (farmInv == null) farmInv = FindObjectOfType<FarmInventoryUI>(true);

        foreach (var water in waters)
        {
            int owned = farmInv?.GetWaterCount(water.waterID) ?? 0;
            string effect = water.extraSpeedBonus > 0
                ? $"+{water.extraSpeedBonus * 100f:F0}% 단축"
                : "기본";
            string sub = owned > 0
                ? $"보유 {owned}개  {effect}"
                : $"{(water.costGem > 0 ? $"{water.costGem}" : $"{water.costGold}")}  {effect}";

            var go = SpawnSlot(water.icon, water.waterName, sub, false, "");
            var cap = water;
            go.GetComponent<Button>()?.onClick.AddListener(() =>
            {
                OnWaterSelected?.Invoke(cap);
                Hide();
            });
        }
    }

    // ═══ 비료 — 전체 표시, 보유수 표시, 선택만 ══════════════════
    public void ShowFertilizers(List<FertilizerData> fertilizers)
    {
        if (titleText != null) titleText.text = "비료 선택";
        ClearSlots();
        gameObject.SetActive(true);

        FarmInventoryUI farmInv = FarmInventoryUI.Instance;
        if (farmInv == null) farmInv = FindObjectOfType<FarmInventoryUI>(true);

        foreach (var fert in fertilizers)
        {
            int owned = farmInv?.GetFertCount(fert.fertilizerID) ?? 0;
            string effect = $"수확+{fert.yieldBonus * 100f:F0}%";
            string sub = owned > 0
                ? $"보유 {owned}개  {effect}"
                : $"{(fert.costGem > 0 ? $"{fert.costGem}" : $"{fert.costGold}")}  {effect}";

            var go = SpawnSlot(fert.icon, fert.fertilizerName, sub, false, "");
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