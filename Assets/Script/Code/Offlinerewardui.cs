using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ══════════════════════════════════════════════════════════
///  방치 보상 UI - 항상 열려있는 토글 패널
/// ══════════════════════════════════════════════════════════
///
/// ✅ 동작:
///   - popupPanel은 항상 켜져 있음 (토글 버튼으로 열고 닫기)
///   - 온라인/오프라인 구분 없이 OfflineRewardManager가 시간 누적
///   - OnRewardUpdated 이벤트 받아서 수치 실시간 갱신
///   - 수령 버튼 → ClaimReward() 호출 후 UI 리셋
///
/// ✅ Inspector 연결:
///   - popupPanel        : 토글할 패널 (시작 시 끄거나 켤 수 있음)
///   - toggleButton      : 패널 열고 닫는 버튼 (HUD에 배치)
///   - characterImage    : 캐릭터 이미지
///   - durationText      : "13:10:43" 누적 시간
///   - durationSlider    : 누적 게이지
///   - goldAmountText    : 골드 수치
///   - gemAmountText     : 젬 수치
///   - expAmountText     : 경험치 수치
///   - itemGridParent    : 아이템 슬롯 부모 (Grid Layout Group)
///   - itemSlotPrefab    : 아이템 슬롯 프리팹
///   - quickMoveButton   : "빠른이동" 버튼 (패널 닫기)
///   - claimButton       : "수령" 버튼
/// </summary>
public class OfflineRewardUI : MonoBehaviour
{
    [Header("패널 토글")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private Button toggleButton;       // HUD의 토글 버튼
    [SerializeField] private bool startOpen = true;     // ★ 항상 켜진 상태로 시작

    [Header("캐릭터 & 시간")]
    [SerializeField] private Image characterImage;
    [SerializeField] private Sprite defaultCharacterSprite;
    [SerializeField] private TextMeshProUGUI durationText;
    [SerializeField] private Slider durationSlider;

    [Header("수익 미리보기")]
    [SerializeField] private TextMeshProUGUI goldAmountText;
    [SerializeField] private TextMeshProUGUI gemAmountText;
    [SerializeField] private TextMeshProUGUI expAmountText;
    [SerializeField] private TextMeshProUGUI equiAmountText;

    [Header("아이템 그리드")]
    [SerializeField] private Transform itemGridParent;
    [SerializeField] private GameObject itemSlotPrefab;

    [Header("버튼")]
    [SerializeField] private Button quickMoveButton;    // 닫기
    [SerializeField] private Button claimButton;        // 수령
    [SerializeField] private Button adClaimButton;      // 2배 (선택)

    [Header("수령 버튼 색상")]
    [SerializeField] private Color claimReadyColor = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private Color claimNotReadyColor = new Color(0.5f, 0.5f, 0.5f);

    // ── 내부 ──────────────────────────────────────
    private List<GameObject> spawnedSlots = new List<GameObject>();
    private bool isPanelOpen = true;


    void Start()
    {
        // ★ 버튼 이벤트 연결
        if (toggleButton != null) toggleButton.onClick.AddListener(TogglePanel);
        if (quickMoveButton != null) quickMoveButton.onClick.AddListener(ClosePanel);
        if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked);
        if (adClaimButton != null) adClaimButton.onClick.AddListener(OnAdClaimClicked);

        // ★ 매니저 이벤트 구독
        OfflineRewardManager.OnRewardUpdated += RefreshUI;

        // ★ 튜토리얼 미완료 시 팝업 자동 열기 안 함 (tutorialPhase 0 = 아직 안 한 상태)
        bool tutorialNotDone = (GameDataBridge.CurrentData?.tutorialPhase ?? 0) < 99;
        if (tutorialNotDone)
        {
            isPanelOpen = false;
            ForceSetPanel(false);
        }
        else
        {
            isPanelOpen = startOpen;
            ForceSetPanel(isPanelOpen);
        }

        // 캐릭터 이미지 설정
        SetCharacterImage();

        // ★ 1프레임 뒤 초기 데이터 반영 (매니저보다 늦게 시작할 수 있으므로)
        StartCoroutine(InitialRefreshDelayed());

        // ★ 매초 시간 표시 갱신 (이벤트와 별개로 실시간 카운트)
        StartCoroutine(RealtimeTimerLoop());
    }

    // ═══════════════════════════════════════════════
    // ★ 매초 실시간 시간/수익 갱신 루프
    // ═══════════════════════════════════════════════
    private System.Collections.IEnumerator RealtimeTimerLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(1f);  // ★ timeScale 영향 안 받음

            if (OfflineRewardManager.Instance == null) continue;

            float minutes = OfflineRewardManager.Instance.AccumulatedMinutes;

            // ── 시간 텍스트 실시간 갱신 ──
            if (durationText != null)
            {
                int totalSec = Mathf.RoundToInt(minutes * 60f);
                int h = totalSec / 3600;
                int m = (totalSec % 3600) / 60;
                int s = totalSec % 60;
                durationText.text = $"{h:D2}:{m:D2}:{s:D2}";
            }

            // ── 게이지 갱신 ──
            if (durationSlider != null)
            {
                float max = OfflineRewardManager.Instance.MaxAccumulateMinutes;
                durationSlider.value = max > 0 ? Mathf.Clamp01(minutes / max) : 0f;
            }

            // ── 수익 미리보기 실시간 갱신 ──
            var reward = OfflineRewardManager.Instance.CalculateCurrentReward();
            if (goldAmountText != null) goldAmountText.text = FormatNumber(reward.goldReward);
            if (gemAmountText != null) gemAmountText.text = FormatNumber(reward.gemReward);
            if (expAmountText != null) expAmountText.text = FormatNumber(reward.expReward);

            // ── 수령 버튼 상태 ──
            bool canClaim = OfflineRewardManager.Instance.IsClaimable;
            if (claimButton != null)
            {
                claimButton.interactable = canClaim;
                var img = claimButton.GetComponent<Image>();
                if (img != null) img.color = canClaim ? claimReadyColor : claimNotReadyColor;
            }
        }
    }

    private System.Collections.IEnumerator InitialRefreshDelayed()
    {
        yield return null;
        yield return null;

        // 튜토리얼 미완료 시 패널 열지 않음
        bool tutorialNotDone = (GameDataBridge.CurrentData?.tutorialPhase ?? 0) < 99;
        if (tutorialNotDone)
        {
            isPanelOpen = false;
            ForceSetPanel(false);
        }
        else
        {
            ForceSetPanel(isPanelOpen);
        }

        if (OfflineRewardManager.Instance != null)
        {
            var reward = OfflineRewardManager.Instance.CalculateCurrentReward();
            RefreshUI(reward);
        }
    }

    // ★ SetActive를 직접 안전하게 호출
    private void ForceSetPanel(bool active)
    {
        if (popupPanel == null) return;
        popupPanel.SetActive(active);
    }

    void OnDestroy()
    {
        OfflineRewardManager.OnRewardUpdated -= RefreshUI;
    }

    // ═══════════════════════════════════════════════
    // 토글
    // ═══════════════════════════════════════════════
    public void TogglePanel()
    {
        SoundManager.Instance?.PlayButtonClick();
        isPanelOpen = !isPanelOpen;
        ForceSetPanel(isPanelOpen);
    }

    public void OpenPanel()
    {
        isPanelOpen = true;
        ForceSetPanel(true);
    }

    public void ClosePanel()
    {
        SoundManager.Instance?.PlayButtonClick();
        isPanelOpen = false;
        ForceSetPanel(false);
    }

    // ═══════════════════════════════════════════════
    // ★ ShowReward - 오프라인 복귀 시 자동으로 패널 열기
    //   OfflineRewardManager에서 호출
    // ═══════════════════════════════════════════════
    public void ShowReward(OfflineRewardData reward)
    {
        // 튜토리얼 진행 중이면 자동 열기 안 함
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
        {
            RefreshUI(reward);
            return;
        }

        OpenPanel();
        RefreshUI(reward);
    }

    // ═══════════════════════════════════════════════
    // UI 갱신 (이벤트 콜백)
    // ═══════════════════════════════════════════════
    private void RefreshUI(OfflineRewardData reward)
    {
        if (reward == null) return;

        // ── 누적 시간 텍스트 ──
        if (durationText != null)
        {
            TimeSpan span = reward.offlineDuration;
            int h = (int)span.TotalHours;
            int m = span.Minutes;
            int s = span.Seconds;
            durationText.text = $"{h:D2}:{m:D2}:{s:D2}";
        }

        // ── 누적 게이지 ──
        if (durationSlider != null && OfflineRewardManager.Instance != null)
        {
            float max = OfflineRewardManager.Instance.MaxAccumulateMinutes;
            float cur = (float)reward.effectiveMinutes;
            durationSlider.value = max > 0 ? Mathf.Clamp01(cur / max) : 0f;
        }

        // ── 수익 미리보기 ──
        if (goldAmountText != null) goldAmountText.text = FormatNumber(reward.goldReward);
        if (gemAmountText != null) gemAmountText.text = FormatNumber(reward.gemReward);
        if (expAmountText != null) expAmountText.text = FormatNumber(reward.expReward);
        if (equiAmountText != null) equiAmountText.text = FormatNumber(reward.equipmentTicketReward);

        // ── 아이템 그리드 ──
        RefreshItemGrid(reward.itemRewards);

        // ── 수령 버튼 상태 ──
        bool canClaim = OfflineRewardManager.Instance?.IsClaimable ?? false;
        if (claimButton != null)
        {
            claimButton.interactable = canClaim;
            var img = claimButton.GetComponent<Image>();
            if (img != null) img.color = canClaim ? claimReadyColor : claimNotReadyColor;
        }
        if (adClaimButton != null)
            adClaimButton.interactable = canClaim;
    }

    // ── 아이템 슬롯 갱신 ──────────────────────────
    private void RefreshItemGrid(List<OfflineItemRewardResult> items)
    {
        foreach (var s in spawnedSlots)
            if (s != null) Destroy(s);
        spawnedSlots.Clear();

        if (itemGridParent == null || itemSlotPrefab == null) return;
        if (items == null || items.Count == 0) return;

        foreach (var item in items)
        {
            if (item.item == null) continue;
            GameObject slot = Instantiate(itemSlotPrefab, itemGridParent);
            spawnedSlots.Add(slot);

            Image icon = slot.GetComponentInChildren<Image>();
            if (icon != null && item.item.itemIcon != null)
                icon.sprite = item.item.itemIcon;

            TextMeshProUGUI txt = slot.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
                txt.text = item.amount > 1 ? $"x{item.amount}" : "";
        }
    }

    // ── 캐릭터 이미지 ─────────────────────────────
    private void SetCharacterImage()
    {
        if (characterImage == null) return;
        Sprite avatar = UIManager.Instance?.Character?.sprite;
        characterImage.sprite = avatar != null ? avatar : defaultCharacterSprite;
    }

    // ═══════════════════════════════════════════════
    // 버튼 콜백
    // ═══════════════════════════════════════════════
    private void OnClaimClicked()
    {
        if (OfflineRewardManager.Instance == null) return;
        SoundManager.Instance?.PlayOfflineReward();
        OfflineRewardManager.Instance.ClaimReward();
        // 수령 후 패널은 닫지 않음 - 계속 열려있게
    }

    private void OnAdClaimClicked()
    {
        if (OfflineRewardManager.Instance == null) return;
        SoundManager.Instance?.PlayOfflineReward();
        OfflineRewardManager.Instance.ClaimRewardWithAd();
    }

    // ═══════════════════════════════════════════════
    // 유틸
    // ═══════════════════════════════════════════════
    private string FormatNumber(int n)
    {
        if (n >= 1_000_000) return $"{n / 1_000_000f:F1}M";
        if (n >= 1_000) return $"{n / 1_000f:F1}K";
        return n.ToString();
    }
}