using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
// FarmProgressSlot.cs  ★ 수정 버전
//
// ★ 수정 내용:
//   1. btnPlant (심기) 추가 — 빈밭일 때 FarmPlantModePanel 오픈
//   2. btnInstant (빠른수확) 추가 — 성장중일 때 젬 소모 즉시완료
//   3. contentRoot 없어도 모든 UI 정상 동작 (contentRoot는 선택사항)
//   4. lockInfoText / btnUnlock 추가 — 잠금 정보 표시
//   5. remainText 추가 — 남은 시간 표시
//
// ★ 프리팹 Hierarchy:
//   FarmProgressSlot  (root)
//     ├ BgImage              ← bgImage
//     ├ SlotNumText          ← slotNumText
//     ├ CropIcon             ← cropIcon
//     ├ StatusText           ← statusText
//     ├ ProgressFill         ← progressFill
//     ├ RemainText           ← remainText       ★ NEW
//     ├ WaterBadge/FertBadge/ReadyBadge
//     ├ LockOverlay          ← lockOverlay
//     │   ├ LockInfoText     ← lockInfoText      ★ NEW
//     │   └ BtnUnlock        ← btnUnlock         ★ NEW
//     └ ActionButtons
//         ├ BtnPlant         ← btnPlant          ★ NEW  [🌱 심기]
//         ├ BtnWater/BtnChgWater
//         ├ BtnFert/BtnChgFert
//         ├ BtnInstant       ← btnInstant        ★ NEW  [⚡ 빠른수확]
//         │   └ InstantCost  ← instantCostText   ★ NEW
//         └ BtnHarvest       ← btnHarvest
// ═══════════════════════════════════════════════════════════════════

public class FarmProgressSlot : MonoBehaviour
{
    [Header("===== 배경 / 번호 =====")]
    [SerializeField] private Image bgImage;
    [SerializeField] private TextMeshProUGUI slotNumText;

    [Header("===== 작물 상태 표시 =====")]
    [SerializeField] private Image cropIcon;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Image progressFill;
    [SerializeField] private TextMeshProUGUI remainText;      // ★ 남은시간
    [SerializeField] private GameObject waterBadge;
    [SerializeField] private GameObject fertBadge;
    [SerializeField] private GameObject readyBadge;
    [SerializeField] private GameObject lockBadge;            // 기존 호환용

    [Header("===== 잠금 처리 (contentRoot는 선택사항) =====")]
    [Tooltip("없어도 됩니다. 있으면 잠금시 통째로 숨김")]
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private TextMeshProUGUI lockInfoText;    // ★ "🔒 Lv.5 / 💰1,000"
    [SerializeField] private Button btnUnlock;                // ★ 해금 버튼

    [Header("===== 액션 버튼 =====")]
    [Tooltip("빈밭일 때 표시 — 클릭하면 FarmPlantModePanel 오픈")]
    [SerializeField] private Button btnPlant;                 // ★ 심기

    [SerializeField] private Button btnWater;
    [SerializeField] private Image waterBtnIcon;
    [SerializeField] private Button btnChgWater;

    [SerializeField] private Button btnFert;
    [SerializeField] private Image fertBtnIcon;
    [SerializeField] private Button btnChgFert;

    [Tooltip("성장중일 때 표시 — 젬 소모 즉시완료")]
    [SerializeField] private Button btnInstant;               // ★ 빠른수확
    [SerializeField] private TextMeshProUGUI instantCostText; // ★ "💎 3"

    [SerializeField] private Button btnHarvest;

    [Header("===== 색상 =====")]
    [SerializeField] private Color colorEmpty = new Color(0.7f, 0.5f, 0.3f, 1f);
    [SerializeField] private Color colorGrowing = new Color(0.3f, 0.7f, 0.3f, 1f);
    [SerializeField] private Color colorReady = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color colorLocked = new Color(0.4f, 0.4f, 0.4f, 1f);

    private int plotIndex;
    private FarmItemSelectPopup sharedPopup;

    // ═══ 초기화 ══════════════════════════════════════════════════

    public void Setup(int index, FarmItemSelectPopup popup)
    {
        plotIndex = index;
        sharedPopup = popup;

        if (slotNumText != null) slotNumText.text = (index + 1).ToString();

        if (btnPlant != null) btnPlant.onClick.AddListener(OnPlantClicked);
        if (btnInstant != null) btnInstant.onClick.AddListener(OnInstantClicked);
        if (btnUnlock != null) btnUnlock.onClick.AddListener(OnUnlockClicked);
        if (btnWater != null) btnWater.onClick.AddListener(OnWaterClicked);
        if (btnChgWater != null) btnChgWater.onClick.AddListener(OnChangeWaterClicked);
        if (btnFert != null) btnFert.onClick.AddListener(OnFertClicked);
        if (btnChgFert != null) btnChgFert.onClick.AddListener(OnChangeFertClicked);
        if (btnHarvest != null) btnHarvest.onClick.AddListener(OnHarvestClicked);

        FarmSelectionMemory.OnWaterChanged += UpdateWaterIcon;
        FarmSelectionMemory.OnFertChanged += UpdateFertIcon;
        UpdateWaterIcon(FarmSelectionMemory.SelectedWater);
        UpdateFertIcon(FarmSelectionMemory.SelectedFertilizer);

        Refresh();
    }

    void OnDestroy()
    {
        FarmSelectionMemory.OnWaterChanged -= UpdateWaterIcon;
        FarmSelectionMemory.OnFertChanged -= UpdateFertIcon;
    }

    // ═══ UI 갱신 ═════════════════════════════════════════════════

    public void Refresh()
    {
        FarmPlotState plot = FarmManager.Instance?.GetPlot(plotIndex);
        if (plot == null) return;

        // ── 🔒 잠긴 밭 ────────────────────────────────────────────
        if (!plot.isUnlocked)
        {
            if (bgImage != null) bgImage.color = colorLocked;

            // contentRoot가 있으면 통째로 숨김, 없으면 요소 개별 숨김
            if (contentRoot != null)
                contentRoot.SetActive(false);
            else
                HideAllContent();

            if (lockOverlay != null) lockOverlay.SetActive(true);
            if (lockBadge != null) lockBadge.SetActive(false);

            if (lockInfoText != null)
            {
                int reqLv = FarmManager.Instance?.GetUnlockRequiredLevel(plotIndex) ?? 1;
                int cost = FarmManager.Instance?.GetUnlockCost(plotIndex) ?? 0;
                lockInfoText.text = $"🔒 Lv.{reqLv}\n💰 {cost:N0}";
            }
            if (btnUnlock != null)
            {
                btnUnlock.gameObject.SetActive(true);
                int reqLv = FarmManager.Instance?.GetUnlockRequiredLevel(plotIndex) ?? 1;
                int plyrLv = PlayerStats.Instance?.level ?? 1;
                btnUnlock.interactable = plyrLv >= reqLv;
            }
            return;
        }

        // 잠금 해제
        if (contentRoot != null) contentRoot.SetActive(true);
        else ShowAllContent();   // contentRoot 없으면 개별 요소 복원
        if (lockOverlay != null) lockOverlay.SetActive(false);
        if (lockBadge != null) lockBadge.SetActive(false);
        if (btnUnlock != null) btnUnlock.gameObject.SetActive(false);

        // ── 🌱 빈 밭 ──────────────────────────────────────────────
        if (plot.currentCrop == null)
        {
            if (bgImage != null) bgImage.color = colorEmpty;
            if (cropIcon != null) cropIcon.gameObject.SetActive(false);
            if (statusText != null) statusText.text = "🌱 빈 밭";
            if (remainText != null) remainText.text = "";
            if (progressFill != null) progressFill.fillAmount = 0f;
            if (waterBadge != null) waterBadge.SetActive(false);
            if (fertBadge != null) fertBadge.SetActive(false);
            if (readyBadge != null) readyBadge.SetActive(false);

            SetBtn(btnPlant, true);   // ★ 심기 버튼만 표시
            SetBtn(btnWater, false); SetBtn(btnChgWater, false);
            SetBtn(btnFert, false); SetBtn(btnChgFert, false);
            SetBtn(btnInstant, false);
            SetBtn(btnHarvest, false);
            return;
        }

        // ── 🌿 성장 중 / ✅ 수확 가능 ─────────────────────────────
        bool isReady = plot.IsReadyToHarvest();
        float progress = FarmCropExtension.CalcGrowthProgress(plot);
        float remain = FarmCropExtension.CalcRemainingSeconds(plot);

        if (bgImage != null) bgImage.color = isReady ? colorReady : colorGrowing;

        if (cropIcon != null)
        {
            cropIcon.gameObject.SetActive(true);
            cropIcon.sprite = isReady
                ? (plot.currentCrop.harvestIcon ?? plot.currentCrop.seedIcon)
                : plot.currentCrop.GetSpriteForStage(plot.GetStage());
        }

        if (statusText != null)
            statusText.text = isReady
                ? $"✅ {plot.currentCrop.cropName}"
                : $"{plot.currentCrop.cropName} {progress * 100f:F0}%";

        if (progressFill != null) progressFill.fillAmount = progress;
        if (remainText != null) remainText.text = isReady ? "수확 가능!" : FormatTime(remain);

        if (waterBadge != null) waterBadge.SetActive(plot.isWatered);
        if (fertBadge != null) fertBadge.SetActive(plot.isFertilized);
        if (readyBadge != null) readyBadge.SetActive(isReady);

        if (instantCostText != null)
        {
            int gem = Mathf.Max(1, Mathf.CeilToInt(remain / 60f));
            instantCostText.text = $"💎{gem}";
        }

        SetBtn(btnPlant, false);
        SetBtn(btnWater, !isReady && !plot.isWatered);
        SetBtn(btnChgWater, !isReady && !plot.isWatered);
        SetBtn(btnFert, !isReady && !plot.isFertilized);
        SetBtn(btnChgFert, !isReady && !plot.isFertilized);
        SetBtn(btnInstant, !isReady);   // ★ 성장중이면 항상 표시
        SetBtn(btnHarvest, isReady);
    }

    // ─── contentRoot 없을 때 요소 직접 숨김/표시 ──────────────────

    private void HideAllContent()
    {
        if (cropIcon != null) cropIcon.gameObject.SetActive(false);
        if (statusText != null) statusText.gameObject.SetActive(false);
        if (remainText != null) remainText.gameObject.SetActive(false);
        if (progressFill != null) progressFill.gameObject.SetActive(false);
        if (waterBadge != null) waterBadge.SetActive(false);
        if (fertBadge != null) fertBadge.SetActive(false);
        if (readyBadge != null) readyBadge.SetActive(false);
        SetBtn(btnPlant, false); SetBtn(btnWater, false);
        SetBtn(btnChgWater, false); SetBtn(btnFert, false);
        SetBtn(btnChgFert, false); SetBtn(btnInstant, false);
        SetBtn(btnHarvest, false);
    }

    private void ShowAllContent()
    {
        if (statusText != null) statusText.gameObject.SetActive(true);
        if (remainText != null) remainText.gameObject.SetActive(true);
        if (progressFill != null) progressFill.gameObject.SetActive(true);
    }

    // ─── 아이콘 갱신 ─────────────────────────────────────────────

    private void UpdateWaterIcon(WaterData w)
    {
        if (waterBtnIcon == null) return;
        waterBtnIcon.sprite = w?.icon;
        waterBtnIcon.enabled = w?.icon != null;
    }

    private void UpdateFertIcon(FertilizerData f)
    {
        if (fertBtnIcon == null) return;
        fertBtnIcon.sprite = f?.icon;
        fertBtnIcon.enabled = f?.icon != null;
    }

    // ═══ 버튼 클릭 핸들러 ════════════════════════════════════════

    // ★ 심기 버튼
    private void OnPlantClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        if (FarmPlantModePanel.Instance != null)
            FarmPlantModePanel.Instance.OpenForPlot(plotIndex);
        else
            Debug.LogWarning("[FarmProgressSlot] FarmPlantModePanel이 씬에 없습니다!");
    }

    private void OnWaterClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        WaterData water = FarmSelectionMemory.SelectedWater;
        if (water != null) ApplyWater(water);
        else FarmManager.Instance?.WaterCrop(plotIndex);
    }

    private void OnChangeWaterClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        if (sharedPopup == null) return;
        sharedPopup.OnWaterSelected = (w) => FarmSelectionMemory.SetWater(w);
        var waters = FarmProgressPanel.Instance?.AvailableWaters;
        if (waters != null && waters.Count > 0) sharedPopup.ShowWaters(waters);
    }

    private void OnFertClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        FertilizerData fert = FarmSelectionMemory.SelectedFertilizer;
        if (fert == null) { UIManager.Instance?.ShowMessage("비료를 먼저 선택해주세요!", Color.yellow); return; }

        bool paid = FarmInventoryUI.Instance?.ConsumeFertilizer(fert.fertilizerID) ?? false;
        if (!paid)
        {
            if (fert.costGem > 0) paid = GameManager.Instance?.SpendGem(fert.costGem) ?? false;
            else if (fert.costGold > 0) paid = GameManager.Instance?.SpendGold(fert.costGold) ?? false;
            else paid = true;
        }
        if (!paid) { UIManager.Instance?.ShowMessage("재화 부족!", Color.red); return; }
        FarmManager.Instance?.FertilizeCrop(plotIndex, fert);
    }

    private void OnChangeFertClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        if (sharedPopup == null) return;
        sharedPopup.OnFertSelected = (f) => FarmSelectionMemory.SetFertilizer(f);
        var ferts = FarmManager.Instance?.allFertilizers;
        if (ferts != null && ferts.Count > 0) sharedPopup.ShowFertilizers(ferts);
    }

    // ★ 빠른수확
    private void OnInstantClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        FarmPlotState plot = FarmManager.Instance?.GetPlot(plotIndex);
        if (plot == null) return;

        float remain = FarmCropExtension.CalcRemainingSeconds(plot);
        int gemCost = Mathf.Max(1, Mathf.CeilToInt(remain / 60f));

        bool paid = GameManager.Instance?.SpendGem(gemCost) ?? false;
        if (!paid) { UIManager.Instance?.ShowMessage($"💎 {gemCost} 부족!", Color.red); return; }

        FarmManager.Instance?.InstantFinish(plotIndex);
        UIManager.Instance?.ShowMessage($"⚡ 즉시 완료! (💎 -{gemCost})", Color.yellow);
    }

    private void OnHarvestClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        FarmManager.Instance?.HarvestCrop(plotIndex);
        SoundManager.Instance?.PlayQuestReward();
    }

    // ★ 해금
    private void OnUnlockClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        FarmManager.Instance?.UnlockPlot(plotIndex);
    }

    private void ApplyWater(WaterData water)
    {
        bool paid = FarmInventoryUI.Instance?.ConsumeWater(water.waterID) ?? false;
        if (!paid)
        {
            if (water.costGem > 0) paid = GameManager.Instance?.SpendGem(water.costGem) ?? false;
            else if (water.costGold > 0) paid = GameManager.Instance?.SpendGold(water.costGold) ?? false;
            else paid = true;
        }
        if (!paid) { UIManager.Instance?.ShowMessage("재화 부족!", Color.red); return; }

        FarmPlotState plot = FarmManager.Instance?.GetPlot(plotIndex);
        if (plot == null) return;
        plot.isWatered = true;
        FarmManagerExtension.InvokePlotChanged(plotIndex);

        string bonus = water.extraSpeedBonus > 0 ? $" +{water.extraSpeedBonus * 100f:F0}%" : "";
        UIManager.Instance?.ShowMessage($"💧 {water.waterName}{bonus}", Color.cyan);
    }

    private void SetBtn(Button btn, bool active)
    {
        if (btn != null) btn.gameObject.SetActive(active);
    }

    private string FormatTime(float s)
    {
        int h = (int)(s / 3600);
        int m = (int)((s % 3600) / 60);
        int ss = (int)(s % 60);
        if (h > 0) return $"{h}h {m:D2}m";
        if (m > 0) return $"{m}분 {ss:D2}초";
        return $"{ss}초";
    }
}