using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ══════════════════════════════════════════════════════════
///  오프라인(방치) 보상 UI - 행동수익 스타일
/// ══════════════════════════════════════════════════════════
///
/// ✅ UI 레이아웃 (Inspector 연결 목록):
///
///  [팝업 루트]
///    ├─ popupPanel              ← 전체 팝업 패널
///    │
///    ├─ [상단]
///    │    ├─ characterImage     ← 캐릭터 이미지 (Image)
///    │    ├─ durationText       ← "행동 시간  12:00:00"
///    │    └─ durationSlider     ← 누적시간 게이지 (Slider)
///    │
///    ├─ [수익 미리보기 행]
///    │    ├─ goldAmountText     ← 골드 수량 TMP
///    │    ├─ gemAmountText      ← 젬 수량 TMP
///    │    └─ expAmountText      ← 경험치 수량 TMP
///    │
///    ├─ [아이템 그리드]
///    │    ├─ itemGridParent     ← Grid Layout Group 붙은 Transform
///    │    └─ itemSlotPrefab     ← 아이템 슬롯 프리팹 (Image + TMP)
///    │
///    └─ [하단 버튼]
///         ├─ quickMoveButton   ← "빠른 이동" (보상 없이 닫기)
///         ├─ claimButton       ← "수령" (기본 수령)
///         └─ adClaimButton     ← "2배 수령" 광고 버튼 (선택)
///
/// </summary>
public class OfflineRewardUI : MonoBehaviour
{
    // ─── 패널 ───────────────────────────────────
    [Header("팝업 패널")]
    [SerializeField] private GameObject popupPanel;

    // ─── 캐릭터 / 시간 ──────────────────────────
    [Header("캐릭터 & 시간")]
    [SerializeField] private Image characterImage;          // 캐릭터 이미지
    [SerializeField] private Sprite defaultCharacterSprite; // 기본 캐릭터 스프라이트
    [SerializeField] private TextMeshProUGUI durationText;  // "행동 시간  12:00:00"
    [SerializeField] private Slider durationSlider;         // 누적 시간 게이지 (0 ~ maxOfflineHours)

    // ─── 수익 미리보기 ──────────────────────────
    [Header("수익 미리보기 텍스트")]
    [SerializeField] private TextMeshProUGUI goldAmountText;
    [SerializeField] private TextMeshProUGUI gemAmountText;
    [SerializeField] private TextMeshProUGUI expAmountText;

    // ─── 아이템 그리드 ──────────────────────────
    [Header("아이템 그리드")]
    [SerializeField] private Transform itemGridParent;      // Grid Layout Group 붙이기
    [SerializeField] private GameObject itemSlotPrefab;     // 슬롯 프리팹

    // ─── 버튼 ────────────────────────────────────
    [Header("버튼")]
    [SerializeField] private Button quickMoveButton;        // 빠른 이동 (수령 안하고 닫기)
    [SerializeField] private Button claimButton;            // 수령
    [SerializeField] private Button adClaimButton;          // 2배 수령 (광고)

    // ─── 설정 ────────────────────────────────────
    [Header("설정")]
    [SerializeField] private float maxOfflineHours = 24f;   // 최대 방치 시간 (게이지 Max)
    [SerializeField] private bool pauseGameWhileOpen = true;

    // ─── 내부 ────────────────────────────────────
    private OfflineRewardData currentReward;
    private List<GameObject> spawnedSlots = new List<GameObject>();

    // ═══════════════════════════════════════════════
    void Start()
    {
        if (quickMoveButton != null)
            quickMoveButton.onClick.AddListener(OnQuickMoveClicked);

        if (claimButton != null)
            claimButton.onClick.AddListener(OnClaimButtonClicked);

        if (adClaimButton != null)
            adClaimButton.onClick.AddListener(OnAdClaimButtonClicked);

        if (popupPanel != null)
            popupPanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════
    // ★ 외부 호출: 보상 팝업 표시
    // ═══════════════════════════════════════════════
    public void ShowReward(OfflineRewardData reward)
    {
        if (reward == null) return;
        currentReward = reward;

        if (popupPanel != null) popupPanel.SetActive(true);
        if (pauseGameWhileOpen) Time.timeScale = 0f;

        RefreshUI(reward);
    }

    // ─────────────────────────────────────────────
    private void RefreshUI(OfflineRewardData reward)
    {
        // ── 캐릭터 이미지 ──
        if (characterImage != null)
        {
            // 플레이어 아바타 스프라이트가 있으면 사용, 없으면 기본값
            Sprite avatar = GetPlayerAvatarSprite();
            characterImage.sprite = avatar != null ? avatar : defaultCharacterSprite;
        }

        // ── 행동 시간 텍스트 ──
        if (durationText != null)
            durationText.text = $"행동 시간   <color=#FF9900>{FormatDuration(reward.offlineDuration)}</color>";

        // ── 시간 게이지 ──
        if (durationSlider != null)
        {
            float ratio = (float)(reward.offlineDuration.TotalHours / maxOfflineHours);
            durationSlider.value = Mathf.Clamp01(ratio);
        }

        // ── 수익 미리보기 ──
        if (goldAmountText != null)
            goldAmountText.text = $"{reward.goldReward:N0}";

        if (gemAmountText != null)
            gemAmountText.text = $"{reward.gemReward:N0}";

        if (expAmountText != null)
            expAmountText.text = $"{reward.expReward:N0}";

        // ── 아이템 그리드 ──
        RefreshItemGrid(reward.itemRewards);
    }

    // ─────────────────────────────────────────────
    private void RefreshItemGrid(List<OfflineItemRewardResult> itemRewards)
    {
        // 기존 슬롯 제거
        foreach (var s in spawnedSlots)
            if (s != null) Destroy(s);
        spawnedSlots.Clear();

        if (itemGridParent == null || itemSlotPrefab == null) return;
        if (itemRewards == null || itemRewards.Count == 0) return;

        foreach (var reward in itemRewards)
        {
            if (reward.item == null) continue;

            GameObject slot = Instantiate(itemSlotPrefab, itemGridParent);
            spawnedSlots.Add(slot);

            // 아이콘
            Image icon = slot.GetComponentInChildren<Image>();
            if (icon != null && reward.item.itemIcon != null)
                icon.sprite = reward.item.itemIcon;

            // 수량 텍스트
            TextMeshProUGUI txt = slot.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
                txt.text = reward.amount > 1 ? $"x{reward.amount}" : "";
        }
    }

    // ─────────────────────────────────────────────
    private Sprite GetPlayerAvatarSprite()
    {
        // UIManager에 캐릭터 이미지가 있으면 가져옴
        if (UIManager.Instance != null && UIManager.Instance.Character != null)
            return UIManager.Instance.Character.sprite;
        return null;
    }

    // ═══════════════════════════════════════════════
    // 버튼 이벤트
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 빠른 이동 - 보상 수령 없이 닫기 (또는 기본 수령 후 닫기 - 원하는 대로)
    /// </summary>
    private void OnQuickMoveClicked()
    {
        SoundManager.Instance?.PlayButtonClick();
        // 빠른이동 = 기본 보상만 수령하고 즉시 닫기
        if (OfflineRewardManager.Instance != null)
            OfflineRewardManager.Instance.ClaimReward();
        ClosePopup();
    }

    /// <summary>
    /// 수령 버튼
    /// </summary>
    private void OnClaimButtonClicked()
    {
        SoundManager.Instance?.PlayOfflineReward();
        if (OfflineRewardManager.Instance != null)
            OfflineRewardManager.Instance.ClaimReward();
        ClosePopup();
    }

    /// <summary>
    /// 광고 2배 수령 버튼
    /// </summary>
    private void OnAdClaimButtonClicked()
    {
        SoundManager.Instance?.PlayOfflineReward();
        if (OfflineRewardManager.Instance != null)
            OfflineRewardManager.Instance.ClaimRewardWithAd();
        ClosePopup();
    }

    // ─────────────────────────────────────────────
    private void ClosePopup()
    {
        if (pauseGameWhileOpen) Time.timeScale = 1f;

        foreach (var s in spawnedSlots)
            if (s != null) Destroy(s);
        spawnedSlots.Clear();

        if (popupPanel != null)
            popupPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    // 시간 포맷: "12:00:00" 또는 "3시간 30분"
    // ─────────────────────────────────────────────
    private string FormatDuration(System.TimeSpan span)
    {
        // HH:MM:SS 형태 (참고 이미지 스타일)
        int h = (int)span.TotalHours;
        int m = span.Minutes;
        int s = span.Seconds;
        return $"{h:D2}:{m:D2}:{s:D2}";
    }
}