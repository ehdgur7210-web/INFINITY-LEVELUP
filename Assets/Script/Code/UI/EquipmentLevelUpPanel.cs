using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 장비 레벨업 패널 — 3탭 시스템 (강화 / 전용스킬 / 승급)
///
/// [탭1: 강화]
///   좌측: 현재 장비 아이콘 + 등급 배지 + 스탯 (빨간색 감소분)
///   화살표 →
///   우측: 레벨업 후 아이콘 + 등급 배지 + 스탯 (초록색 증가분)
///   중앙: [-] [수량] [+] 재료 수량 조절
///   하단: "레벨업" 버튼 (주황색)
///
/// [탭2: 전용스킬]
///   좌측: 스킬 아이콘 + 이름 + 현재 레벨 (N/maxLevel)
///   소모: "*같은 장비 N개가 소모됩니다"
///   우측: 다음 레벨 스킬 효과 미리보기
///   하단: 젬 아이콘 + 비용 + "스킬 배우기/레벨업" 버튼
///
/// [탭3: 승급]
///   현재 등급 → 다음 등급 미리보기
///   필요 재료 수량 표시
///   "승급하기" 버튼
///
/// [Inspector 연결]
///   기존 필드 그대로 사용 (탭1 = 강화)
///   탭 버튼/컨텐츠는 코드로 런타임 생성
/// </summary>
public class EquipmentLevelUpPanel : MonoBehaviour
{
    public static EquipmentLevelUpPanel Instance;

    [Header("패널")]
    public GameObject levelUpPanel;

    [Header("장비 정보 (좌측)")]
    public Image equipIcon;
    public TextMeshProUGUI equipNameText;
    public TextMeshProUGUI rarityText;
    public TextMeshProUGUI currentLevelText;
    public TextMeshProUGUI maxLevelText;
    public TextMeshProUGUI currentStatText;
    public TextMeshProUGUI descriptionText;

    [Header("레벨업 정보 (우측)")]
    public TextMeshProUGUI nextStatText;
    public Image arrowImage;
    public Image requiredMaterialIcon;
    public TextMeshProUGUI requiredMaterialCount;
    public TextMeshProUGUI requiredGoldText;

    [Header("버튼")]
    public Button levelUpButton;
    public TextMeshProUGUI levelUpButtonText;
    public Button closeButton;

    // ── 탭 색상 (프로젝트 공통) ──
    private static readonly Color TabActiveColor = new Color(1f, 0.85f, 0.2f);
    private static readonly Color TabInactiveColor = new Color(0.55f, 0.55f, 0.55f);

    // ── 등급 배지 색상 ──
    private static readonly Color BadgeCommon = new Color(0.6f, 0.6f, 0.6f);
    private static readonly Color BadgeRare = new Color(0.2f, 0.4f, 1f);
    private static readonly Color BadgeEpic = new Color(0.7f, 0.2f, 1f);
    private static readonly Color BadgeLegendary = new Color(1f, 0.6f, 0f);
    private static readonly Color BadgeMythic = new Color(1f, 0.15f, 0.15f);

    // ── 탭 시스템 ──
    private enum TabType { Enhance, Skill, Promote }
    private TabType currentTab = TabType.Enhance;

    [Header("탭 버튼 (Inspector 연결)")]
    public Button tabEnhanceBtn;
    public Button tabSkillBtn;
    public Button tabPromoteBtn;

    [Header("탭 컨텐츠 패널 (Inspector 연결)")]
    [Tooltip("강화탭 — 비워두면 기존 Inspector 필드(좌측/우측)를 그대로 사용")]
    public GameObject enhanceContent;
    [Tooltip("전용스킬탭 패널")]
    public GameObject skillContent;
    [Tooltip("승급탭 패널")]
    public GameObject promoteContent;

    [Header("스킬탭 UI (Inspector 연결)")]
    public Image skillIconImg;
    public TextMeshProUGUI skillNameText;
    public TextMeshProUGUI skillLevelText;
    public TextMeshProUGUI skillDescText;
    public TextMeshProUGUI skillNextDescText;
    public TextMeshProUGUI skillCostText;
    public TextMeshProUGUI skillConsumeText;
    public Button skillActionBtn;
    public TextMeshProUGUI skillActionBtnText;

    [Header("승급탭 UI (Inspector 연결)")]
    public Image promoteCurrentIcon;
    public Image promoteNextIcon;
    public TextMeshProUGUI promoteCurrentRarity;
    public TextMeshProUGUI promoteNextRarity;
    public TextMeshProUGUI promoteMatText;
    public TextMeshProUGUI promoteArrowText;
    public Button promoteActionBtn;
    public TextMeshProUGUI promoteActionBtnText;

    // ── 내부 상태 ──
    private EquipmentSlot currentSlot;
    private EquipmentData currentEquip;
    private int currentLevel;
    private int enhanceMaterialAmount = 1;

    // ── 슬라이드 애니메이션 ──
    private const float SlideDuration = 0.2f;
    private Coroutine slideCoroutine;

    // Open()이 Start()보다 먼저 호출될 수 있으므로 플래그로 관리
    private bool isOpen = false;
    private bool isInitialized = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        Debug.Log("[ManagerInit] EquipmentLevelUpPanel가 생성되었습니다.");

        // ★ 초기 숨김은 Awake에서 처리 (Start 타이밍 문제 방지)
        if (levelUpPanel != null) levelUpPanel.SetActive(false);
    }

    void Start()
    {
        InitializeListeners();

        // ★ Open()이 이미 호출된 상태면 패널을 다시 숨기지 않음
        if (isOpen)
        {
            Debug.Log("[EquipmentLevelUpPanel] Start: 이미 Open 상태 → 숨김 스킵");
        }
        else
        {
            // 탭 컨텐츠 초기 숨김
            if (enhanceContent != null) enhanceContent.SetActive(false);
            if (skillContent != null) skillContent.SetActive(false);
            if (promoteContent != null) promoteContent.SetActive(false);
        }

        Debug.Log($"[EquipmentLevelUpPanel] Start 완료 — isOpen={isOpen}, enhance={enhanceContent != null}, skill={skillContent != null}, promote={promoteContent != null}");
    }

    /// <summary>버튼 리스너 등록 (Start 또는 Open에서 최초 1회)</summary>
    private void InitializeListeners()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (levelUpButton != null) levelUpButton.onClick.AddListener(OnLevelUpClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        // 탭 버튼 리스너 (Inspector OnClick 중복 방지)
        if (tabEnhanceBtn != null)
        {
            tabEnhanceBtn.onClick.RemoveAllListeners();
            tabEnhanceBtn.onClick.AddListener(() => SelectTab(TabType.Enhance));
        }
        if (tabSkillBtn != null)
        {
            tabSkillBtn.onClick.RemoveAllListeners();
            tabSkillBtn.onClick.AddListener(() => SelectTab(TabType.Skill));
        }
        if (tabPromoteBtn != null)
        {
            tabPromoteBtn.onClick.RemoveAllListeners();
            tabPromoteBtn.onClick.AddListener(() => SelectTab(TabType.Promote));
        }

        // 스킬/승급 액션 버튼
        if (skillActionBtn != null)
        {
            skillActionBtn.onClick.RemoveAllListeners();
            skillActionBtn.onClick.AddListener(OnSkillActionClicked);
        }
        if (promoteActionBtn != null)
        {
            promoteActionBtn.onClick.RemoveAllListeners();
            promoteActionBtn.onClick.AddListener(OnPromoteClicked);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  열기 / 닫기
    // ═══════════════════════════════════════════════════════════════

    public void Open(EquipmentSlot slot, EquipmentData equipment, int itemLevel)
    {
        Debug.Log($"[EquipmentLevelUpPanel] Open 호출 — slot={slot?.name}, equip={equipment?.itemName}, lv={itemLevel}");

        if (slot == null || equipment == null)
        {
            Debug.LogWarning($"[EquipmentLevelUpPanel] Open 실패 — slot={slot != null}, equipment={equipment != null}");
            return;
        }

        // ★ Start()보다 먼저 호출될 수 있으므로 리스너 초기화 보장
        InitializeListeners();

        isOpen = true;
        currentSlot = slot;
        currentEquip = equipment;
        currentLevel = itemLevel;
        enhanceMaterialAmount = 1;

        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(true);

            // ★ 부모 활성화 시 자식 패널이 씬 기본 상태로 복원될 수 있으므로 강제 리셋
            ForceHideAllContent();
        }
        else
        {
            Debug.LogError("[EquipmentLevelUpPanel] levelUpPanel이 null! Inspector 연결 확인 필요");
        }

        SelectTab(TabType.Enhance);
        Debug.Log($"[EquipmentLevelUpPanel] 열기 완료 — {equipment.itemName} Lv.{itemLevel}, 탭=강화");
    }

    public void Close()
    {
        isOpen = false;
        if (levelUpPanel != null) levelUpPanel.SetActive(false);
        currentSlot = null;
        currentEquip = null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  (삭제됨: 탭 UI는 Inspector에서 직접 배치)
    // ═══════════════════════════════════════════════════════════════

    /* BuildTabUI, CreateTabButton, BuildSkillTabContent, BuildPromoteTabContent,
       CreatePromoteColumn — 모두 제거됨. Inspector에서 직접 UI를 배치하세요. */

    // ═══════════════════════════════════════════════════════════════
    //  탭 전환
    // ═══════════════════════════════════════════════════════════════

    private void SelectTab(TabType tab)
    {
        // ★ 튜토리얼 중 탭 전환 차단
        if (TutorialManager.Instance != null && TutorialManager.Instance.ShouldBlockNonFocusButtons)
        {
            Debug.Log($"[EquipLevelUp] 튜토리얼 중 탭 전환 차단: {tab}");
            return;
        }

        Debug.Log($"[SelectTab] 호출: {tab}, 호출자={new System.Diagnostics.StackTrace().GetFrame(1)?.GetMethod()?.Name}");
        currentTab = tab;
        UpdateTabVisuals();
        ShowTabContent(tab);

        // 현재 탭에 맞는 데이터 갱신
        switch (tab)
        {
            case TabType.Enhance:
                enhanceMaterialAmount = 1;
                RefreshUI();
                break;
            case TabType.Skill:
                RefreshSkillTab();
                break;
            case TabType.Promote:
                RefreshPromoteTab();
                break;
        }

        SoundManager.Instance?.PlayButtonClick();
    }

    /// <summary>탭 버튼 색상 갱신</summary>
    private void UpdateTabVisuals()
    {
        SetTabColor(tabEnhanceBtn, currentTab == TabType.Enhance);
        SetTabColor(tabSkillBtn, currentTab == TabType.Skill);
        SetTabColor(tabPromoteBtn, currentTab == TabType.Promote);
    }

    private void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null) img.color = active ? TabActiveColor : TabInactiveColor;
        TextMeshProUGUI txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.color = active ? Color.white : new Color(0.85f, 0.85f, 0.85f);
    }

    /// <summary>탭별 컨텐츠 표시/숨김</summary>
    private void ShowTabContent(TabType tab)
    {
        // ── 모든 컨텐츠 + Inspector 요소 비활성화 ──
        SetInspectorElementsActive(false);
        ForceHideAllContent();

        // ── 선택된 탭만 활성화 (ScrollView 부모까지 포함) ──
        switch (tab)
        {
            case TabType.Enhance:
                SetContentVisible(enhanceContent, true);
                SetInspectorElementsActive(true);
                break;
            case TabType.Skill:
                SetContentVisible(skillContent, true);
                break;
            case TabType.Promote:
                SetContentVisible(promoteContent, true);
                break;
        }

        // ★ 최종 상태 로그
        Debug.Log($"[ShowTabContent] 최종: e={enhanceContent?.activeSelf} s={skillContent?.activeSelf} p={promoteContent?.activeSelf}");

        // ★ 다음 프레임에서 다시 한번 강제 보장 (OnEnable/Animator 등 방지)
        StopCoroutine("EnforceTabStateNextFrame");
        StartCoroutine(EnforceTabStateNextFrame(tab));
    }

    /// <summary>패널 RectTransform 상태 로그</summary>
    private void LogPanelRect(string name, GameObject panel)
    {
        if (panel == null) return;
        RectTransform rt = panel.GetComponent<RectTransform>();
        if (rt == null) return;

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        string cgInfo = cg != null ? $"CanvasGroup(alpha={cg.alpha}, blocksRay={cg.blocksRaycasts})" : "CanvasGroup 없음";

        // 부모 체인 확인
        string parentChain = "";
        Transform p = panel.transform.parent;
        for (int i = 0; i < 3 && p != null; i++)
        {
            parentChain += $" → {p.name}(active={p.gameObject.activeSelf})";
            p = p.parent;
        }

        Debug.Log($"[PanelRect] {name}: active={panel.activeSelf}, " +
            $"pos={rt.anchoredPosition}, size={rt.rect.size}, " +
            $"siblingIndex={rt.GetSiblingIndex()}, " +
            $"activeInHierarchy={panel.activeInHierarchy}, " +
            $"{cgInfo}, " +
            $"부모체인: {panel.name}{parentChain}");
    }

    /// <summary>3개 컨텐츠 패널 강제 비활성화 — ScrollView 부모까지 포함</summary>
    private void ForceHideAllContent()
    {
        SetContentVisible(enhanceContent, false);
        SetContentVisible(skillContent, false);
        SetContentVisible(promoteContent, false);
    }

    /// <summary>
    /// 컨텐츠 패널의 ScrollView 부모를 찾아 SetActive.
    /// Inspector에 Content(자식)가 연결되어 있어도 ScrollView(부모)까지 토글.
    /// </summary>
    private void SetContentVisible(GameObject content, bool visible)
    {
        if (content == null) return;

        // ScrollView 부모 탐색 (Content → Viewport → ScrollView 구조)
        GameObject target = FindScrollViewParent(content);
        target.SetActive(visible);
    }

    /// <summary>Content → Viewport → ScrollView(ScrollRect) 구조에서 ScrollView GO 반환. 없으면 자기 자신.</summary>
    private GameObject FindScrollViewParent(GameObject content)
    {
        Transform current = content.transform.parent;
        for (int i = 0; i < 3 && current != null; i++)
        {
            if (current.GetComponent<ScrollRect>() != null)
                return current.gameObject;
            current = current.parent;
        }
        // ScrollRect 없으면 자기 자신을 토글
        return content;
    }

    /// <summary>다음 프레임에서 탭 상태를 강제 보장</summary>
    private IEnumerator EnforceTabStateNextFrame(TabType tab)
    {
        yield return null;

        if (tab != TabType.Enhance) SetContentVisible(enhanceContent, false);
        if (tab != TabType.Skill) SetContentVisible(skillContent, false);
        if (tab != TabType.Promote) SetContentVisible(promoteContent, false);

        switch (tab)
        {
            case TabType.Enhance: SetContentVisible(enhanceContent, true); break;
            case TabType.Skill: SetContentVisible(skillContent, true); break;
            case TabType.Promote: SetContentVisible(promoteContent, true); break;
        }
    }

    /// <summary>기존 Inspector 연결 요소 활성/비활성</summary>
    private void SetInspectorElementsActive(bool active)
    {
        if (equipIcon != null) equipIcon.gameObject.SetActive(active);
        if (equipNameText != null) equipNameText.gameObject.SetActive(active);
        if (rarityText != null) rarityText.gameObject.SetActive(active);
        if (currentLevelText != null) currentLevelText.gameObject.SetActive(active);
        if (maxLevelText != null) maxLevelText.gameObject.SetActive(active);
        if (currentStatText != null) currentStatText.gameObject.SetActive(active);
        if (descriptionText != null) descriptionText.gameObject.SetActive(active);
        if (nextStatText != null) nextStatText.gameObject.SetActive(active);
        if (arrowImage != null) arrowImage.gameObject.SetActive(active);
        if (requiredMaterialIcon != null) requiredMaterialIcon.gameObject.SetActive(active);
        if (requiredMaterialCount != null) requiredMaterialCount.gameObject.SetActive(active);
        if (requiredGoldText != null) requiredGoldText.gameObject.SetActive(active);
        if (levelUpButton != null) levelUpButton.gameObject.SetActive(active);
    }

    /// <summary>간단 슬라이드 인 애니메이션 (InventoryManager 방식 참고)</summary>
    private void AnimateSlideIn(GameObject target)
    {
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideInCoroutine(target));
    }

    private IEnumerator SlideInCoroutine(GameObject target)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null) yield break;

        Vector2 startPos = rect.anchoredPosition;
        Vector2 targetPos = startPos;
        startPos.x += 60f; // 오른쪽에서 슬라이드 인
        rect.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < SlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / SlideDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // cubic ease-out
            rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, eased);
            yield return null;
        }

        rect.anchoredPosition = targetPos;
        slideCoroutine = null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  탭1: 강화 — UI 갱신
    // ═══════════════════════════════════════════════════════════════

    private void RefreshUI()
    {
        if (currentEquip == null) return;

        // ★ 레벨 상한: 기본 20 + (강화 1당 +10)
        int enhLevel = currentSlot != null ? currentSlot.enhanceLevel : 0;
        int maxLv = 20 + (enhLevel * 10);
        bool isMaxLevel = currentLevel >= maxLv;

        // ── 좌측: 장비 정보 ──
        if (equipIcon != null)
        {
            equipIcon.sprite = currentEquip.itemIcon;
            equipIcon.color = Color.white;
        }

        if (equipNameText != null)
        {
            equipNameText.text = currentEquip.itemName;
            equipNameText.color = currentEquip.GetRarityColor();
        }

        if (rarityText != null)
        {
            rarityText.text = currentEquip.rarity.ToString();
            rarityText.color = currentEquip.GetRarityColor();
        }

        if (currentLevelText != null)
            currentLevelText.text = $"Lv.{currentLevel} / {maxLv}";

        if (maxLevelText != null)
        {
            maxLevelText.gameObject.SetActive(isMaxLevel);
            if (isMaxLevel) maxLevelText.text = "<color=#FFD700>MAX LEVEL</color>";
        }

        // 현재 스탯 (빨간색 감소분 표시)
        EquipmentStats curStats = currentEquip.GetLeveledStats(currentLevel);
        if (currentStatText != null)
            currentStatText.text = FormatStatsWithColor(curStats, "#FF6B6B");

        if (descriptionText != null)
            descriptionText.text = currentEquip.itemDescription ?? "";

        // ── 우측: 레벨업 정보 ──
        if (isMaxLevel)
        {
            if (nextStatText != null)
                nextStatText.text = "<color=#FFD700>최대 레벨 달성!</color>";
            if (arrowImage != null) arrowImage.gameObject.SetActive(false);
            if (requiredGoldText != null) requiredGoldText.text = "-";
            if (requiredMaterialCount != null) requiredMaterialCount.text = "-";
            if (levelUpButton != null) levelUpButton.interactable = false;
            if (levelUpButtonText != null) levelUpButtonText.text = "MAX";
        }
        else
        {
            // 다음 레벨 스탯 (초록색 증가분)
            EquipmentStats nextStats = currentEquip.GetLeveledStats(currentLevel + 1);
            if (nextStatText != null)
                nextStatText.text = FormatStatsWithDiff(curStats, nextStats);
            if (arrowImage != null)
                arrowImage.gameObject.SetActive(true);

            // 재료/골드 비용
            int goldNeeded = currentEquip.GetLevelUpGold(currentLevel);
            int matNeeded = currentEquip.GetRequiredMaterials(currentLevel);
            long goldHave = GameManager.Instance != null ? GameManager.Instance.PlayerGold : 0;
            int matHave = currentSlot != null ? Mathf.Max(0, currentSlot.itemCount - 1) : 0;

            // 수량 조절 반영
            int totalMatNeeded = matNeeded * enhanceMaterialAmount;
            int totalGoldNeeded = goldNeeded * enhanceMaterialAmount;
            bool canAfford = goldHave >= totalGoldNeeded && matHave >= totalMatNeeded;

            if (requiredGoldText != null)
            {
                requiredGoldText.text = $"{totalGoldNeeded:N0} G";
                requiredGoldText.color = goldHave >= totalGoldNeeded ? Color.white : Color.red;
            }

            if (requiredMaterialIcon != null)
                requiredMaterialIcon.sprite = currentEquip.itemIcon;

            if (requiredMaterialCount != null)
            {
                requiredMaterialCount.text = $"{matHave} / {totalMatNeeded}";
                requiredMaterialCount.color = matHave >= totalMatNeeded ? Color.white : Color.red;
            }

            if (levelUpButton != null) levelUpButton.interactable = canAfford;
            if (levelUpButtonText != null)
                levelUpButtonText.text = canAfford ? "레벨업" : "재료 부족";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  탭1: 레벨업 실행
    // ═══════════════════════════════════════════════════════════════

    private void OnLevelUpClicked()
    {
        // ★ 튜토리얼 중 레벨업 무조건 차단
        if (TutorialManager.Instance != null && TutorialManager.Instance.ShouldBlockNonFocusButtons)
        {
            Debug.Log("[EquipLevelUp] 튜토리얼 중 레벨업 차단");
            return;
        }

        if (currentSlot == null || currentEquip == null) return;

        // ★ 레벨 상한: 기본 20 + (강화 1당 +10)
        int enhLevel = currentSlot != null ? currentSlot.enhanceLevel : 0;
        int maxLv = 20 + (enhLevel * 10);
        if (currentLevel >= maxLv)
        {
            UIManager.Instance?.ShowMessage($"최대 레벨! (강화 +{enhLevel} → Lv.{maxLv})", Color.yellow);
            return;
        }

        int goldNeeded = currentEquip.GetLevelUpGold(currentLevel);
        int matNeeded = currentEquip.GetRequiredMaterials(currentLevel);
        int matHave = Mathf.Max(0, currentSlot.itemCount - 1);

        // 골드 확인 및 차감
        if (GameManager.Instance == null || !GameManager.Instance.SpendGold(goldNeeded))
        {
            UIManager.Instance?.ShowMessage("골드가 부족합니다!", Color.red);
            return;
        }

        // 재료 확인
        if (matHave < matNeeded)
        {
            GameManager.Instance?.AddGold(goldNeeded); // 환불
            UIManager.Instance?.ShowMessage("재료가 부족합니다!", Color.red);
            return;
        }

        // 재료 차감 (동일 아이템 n개)
        currentSlot.itemCount -= matNeeded;
        currentLevel++;
        currentSlot.itemLevel = currentLevel;
        currentSlot.UpdateItemLevel(currentLevel);

        // 해금 맵에 동기화
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.SyncEquipSlotToMap(
                currentEquip.itemID,
                currentSlot.itemCount,
                currentSlot.enhanceLevel,
                currentLevel);
        }

        // 전투력 재계산
        CombatPowerManager.Instance?.Recalculate();

        // 효과음 + 메시지
        SoundManager.Instance?.PlayEnhanceSuccess();
        UIManager.Instance?.ShowMessage($"{currentEquip.itemName} Lv.{currentLevel} 달성!", Color.green);

        Debug.Log($"[EquipmentLevelUpPanel] 레벨업: {currentEquip.itemName} Lv.{currentLevel - 1} → Lv.{currentLevel}");
        QuestManager.Instance?.UpdateQuestProgress(QuestType.EquipLevelUp, "", 1);
        AchievementSystem.Instance?.UpdateAchievementProgress(AchievementType.EquipLevelUp, "", currentLevel);

        SaveLoadManager.Instance?.SaveGame();

        // UI 갱신
        RefreshUI();
        InventoryManager.Instance?.RefreshEquipDisplay();
    }

    // ═══════════════════════════════════════════════════════════════
    //  탭2: 전용스킬 — UI 갱신
    // ═══════════════════════════════════════════════════════════════

    private void RefreshSkillTab()
    {
        if (currentEquip == null) return;

        // 이 장비의 등급에 해당하는 스킬 조회
        SkillData skill = GetEquipmentSkill();
        bool hasSkill = skill != null;

        if (!hasSkill)
        {
            // 스킬 없는 장비
            if (skillIconImg != null) skillIconImg.color = Color.gray;
            if (skillNameText != null) skillNameText.text = "전용 스킬 없음";
            if (skillLevelText != null) skillLevelText.text = "";
            if (skillDescText != null) skillDescText.text = "이 장비에는 전용 스킬이 없습니다.";
            if (skillConsumeText != null) skillConsumeText.text = "";
            if (skillNextDescText != null) skillNextDescText.text = "";
            if (skillCostText != null) skillCostText.text = "";
            if (skillActionBtn != null) skillActionBtn.interactable = false;
            if (skillActionBtnText != null) skillActionBtnText.text = "스킬 없음";
            return;
        }

        // 스킬 데이터 표시
        if (skillIconImg != null)
        {
            skillIconImg.sprite = skill.skillIcon;
            skillIconImg.color = Color.white;
        }
        if (skillNameText != null) skillNameText.text = skill.skillName;

        // 현재 스킬 레벨 조회
        int curSkillLv = 0;
        LearnedSkill learned = SkillManager.Instance?.GetLearnedSkill(skill.skillID);
        if (learned != null) curSkillLv = learned.currentLevel;

        bool isLearned = curSkillLv > 0;
        bool isMaxSkillLv = curSkillLv >= skill.maxLevel;

        if (skillLevelText != null)
            skillLevelText.text = $"Lv.{curSkillLv} / {skill.maxLevel}";

        if (skillDescText != null)
        {
            float curValue = skill.GetValueAtLevel(Mathf.Max(1, curSkillLv));
            skillDescText.text = isLearned
                ? $"{skill.skillDescription}\n효과: {curValue:F0}"
                : skill.skillDescription;
        }

        // 소모 안내
        int consumeCount = GetSkillLevelUpMaterialCount(curSkillLv);
        if (skillConsumeText != null)
        {
            if (isMaxSkillLv)
                skillConsumeText.text = "";
            else
                skillConsumeText.text = $"*같은 장비 {consumeCount}개가 소모됩니다";
        }

        // 다음 레벨 효과
        if (skillNextDescText != null)
        {
            if (isMaxSkillLv)
                skillNextDescText.text = "<color=#FFD700>최대 레벨!</color>";
            else
            {
                int nextLv = curSkillLv + 1;
                float nextValue = skill.GetValueAtLevel(nextLv);
                skillNextDescText.text = $"Lv.{nextLv} 효과: {nextValue:F0}";
            }
        }

        // 비용
        int gemCost = GetSkillGemCost(curSkillLv);
        int matHave = currentSlot != null ? Mathf.Max(0, currentSlot.itemCount - 1) : 0;
        long gemHave = GameManager.Instance != null ? GameManager.Instance.PlayerGem : 0;
        bool canAfford = gemHave >= gemCost && matHave >= consumeCount;

        if (skillCostText != null)
        {
            string gemColor = gemHave >= gemCost ? "#FFFFFF" : "#FF4444";
            skillCostText.text = $"<color={gemColor}>{gemCost}</color> 젬";
        }

        // 버튼 상태
        if (skillActionBtn != null) skillActionBtn.interactable = !isMaxSkillLv && canAfford;
        if (skillActionBtnText != null)
        {
            if (isMaxSkillLv) skillActionBtnText.text = "MAX";
            else if (!isLearned) skillActionBtnText.text = "스킬 배우기";
            else skillActionBtnText.text = "스킬 레벨업";
        }
    }

    /// <summary>이 장비의 등급에 해당하는 스킬 조회</summary>
    private SkillData GetEquipmentSkill()
    {
        if (currentEquip == null || EquipmentSkillSystem.Instance == null) return null;

        RaritySkillMapping mapping = EquipmentSkillSystem.Instance.GetSkillMappingForSlot(currentEquip.equipmentType);
        if (mapping == null) return null;

        return mapping.GetSkillByRarity(currentEquip.rarity);
    }

    /// <summary>스킬 레벨업에 필요한 같은 장비 수</summary>
    private int GetSkillLevelUpMaterialCount(int currentSkillLevel)
    {
        // 레벨 0→1: 1개, 1→2: 2개, 2→3: 3개, 3→4: 4개
        return currentSkillLevel + 1;
    }

    /// <summary>스킬 레벨업 젬 비용</summary>
    private int GetSkillGemCost(int currentSkillLevel)
    {
        // 레벨별 젬 비용: 50, 100, 200, 400
        int baseCost = 50;
        return baseCost * (1 << currentSkillLevel); // 50, 100, 200, 400
    }

    private void OnSkillActionClicked()
    {
        // ★ 튜토리얼 중 차단
        if (TutorialManager.Instance != null && TutorialManager.Instance.ShouldBlockNonFocusButtons)
            return;

        if (currentEquip == null || SkillManager.Instance == null) return;

        SkillData skill = GetEquipmentSkill();
        if (skill == null) return;

        LearnedSkill learned = SkillManager.Instance.GetLearnedSkill(skill.skillID);
        int curLv = learned?.currentLevel ?? 0;
        bool isLearned = curLv > 0;

        if (curLv >= skill.maxLevel)
        {
            UIManager.Instance?.ShowMessage("이미 최대 레벨입니다!", Color.yellow);
            return;
        }

        // 비용 확인
        int gemCost = GetSkillGemCost(curLv);
        int matNeeded = GetSkillLevelUpMaterialCount(curLv);
        int matHave = currentSlot != null ? Mathf.Max(0, currentSlot.itemCount - 1) : 0;

        // 젬 차감
        if (GameManager.Instance == null || !GameManager.Instance.SpendGem(gemCost))
        {
            UIManager.Instance?.ShowMessage("젬이 부족합니다!", Color.red);
            return;
        }

        // 재료 확인
        if (matHave < matNeeded)
        {
            GameManager.Instance?.AddGem(gemCost); // 환불
            UIManager.Instance?.ShowMessage("재료가 부족합니다!", Color.red);
            return;
        }

        // 재료 차감
        currentSlot.itemCount -= matNeeded;

        // 스킬 학습/레벨업
        if (!isLearned)
        {
            SkillManager.Instance.LearnSkillFromEquipment(skill);
            UIManager.Instance?.ShowMessage($"{skill.skillName} 습득!", Color.cyan);
        }
        else
        {
            learned.currentLevel++;
            UIManager.Instance?.ShowMessage($"{skill.skillName} Lv.{learned.currentLevel}!", Color.cyan);
        }

        // 해금 맵 동기화
        InventoryManager.Instance?.SyncEquipSlotToMap(
            currentEquip.itemID, currentSlot.itemCount,
            currentSlot.enhanceLevel, currentLevel);

        SoundManager.Instance?.PlayEnhanceSuccess();
        Debug.Log($"[EquipmentLevelUpPanel] 스킬 {(isLearned ? "레벨업" : "습득")}: {skill.skillName}");

        RefreshSkillTab();
        InventoryManager.Instance?.RefreshEquipDisplay();
    }

    // ═══════════════════════════════════════════════════════════════
    //  탭3: 승급 — UI 갱신
    // ═══════════════════════════════════════════════════════════════

    private void RefreshPromoteTab()
    {
        if (currentEquip == null) return;

        ItemRarity curRarity = currentEquip.rarity;
        bool canPromote = curRarity < ItemRarity.Legendary;
        ItemRarity nextRarity = canPromote ? curRarity + 1 : curRarity;

        // 현재 등급
        if (promoteCurrentIcon != null)
        {
            promoteCurrentIcon.sprite = currentEquip.itemIcon;
            promoteCurrentIcon.color = Color.white;
        }
        if (promoteCurrentRarity != null)
        {
            promoteCurrentRarity.text = GetRarityKorean(curRarity);
            promoteCurrentRarity.color = GetBadgeColor(curRarity);
        }

        // 다음 등급
        if (promoteNextIcon != null)
        {
            promoteNextIcon.sprite = currentEquip.itemIcon;
            promoteNextIcon.color = canPromote ? Color.white : new Color(0.5f, 0.5f, 0.5f);
        }
        if (promoteNextRarity != null)
        {
            promoteNextRarity.text = canPromote ? GetRarityKorean(nextRarity) : "최대 등급";
            promoteNextRarity.color = canPromote ? GetBadgeColor(nextRarity) : Color.gray;
        }

        // 화살표
        if (promoteArrowText != null)
            promoteArrowText.color = canPromote ? Color.white : Color.gray;

        // 필요 재료
        int promoteMatNeeded = GetPromoteMaterialCount(curRarity);
        int matHave = currentSlot != null ? Mathf.Max(0, currentSlot.itemCount - 1) : 0;

        if (promoteMatText != null)
        {
            if (!canPromote)
                promoteMatText.text = "최대 등급에 도달했습니다";
            else
            {
                string matColor = matHave >= promoteMatNeeded ? "#FFFFFF" : "#FF4444";
                promoteMatText.text = $"필요: 같은 장비 <color={matColor}>{matHave}/{promoteMatNeeded}</color>개";
            }
        }

        // 버튼
        if (promoteActionBtn != null)
            promoteActionBtn.interactable = canPromote && matHave >= promoteMatNeeded;
        if (promoteActionBtnText != null)
            promoteActionBtnText.text = canPromote ? "승급하기" : "MAX";
    }

    /// <summary>승급에 필요한 같은 장비 수</summary>
    private int GetPromoteMaterialCount(ItemRarity currentRarity)
    {
        switch (currentRarity)
        {
            case ItemRarity.Common: return 3;
            case ItemRarity.Uncommon: return 5;
            case ItemRarity.Rare: return 7;
            case ItemRarity.Epic: return 10;
            default: return 99;
        }
    }

    private void OnPromoteClicked()
    {
        if (currentEquip == null || currentSlot == null) return;

        ItemRarity curRarity = currentEquip.rarity;
        if (curRarity >= ItemRarity.Legendary)
        {
            UIManager.Instance?.ShowMessage("최대 등급입니다!", Color.yellow);
            return;
        }

        int matNeeded = GetPromoteMaterialCount(curRarity);
        int matHave = Mathf.Max(0, currentSlot.itemCount - 1);

        if (matHave < matNeeded)
        {
            UIManager.Instance?.ShowMessage("재료가 부족합니다!", Color.red);
            return;
        }

        // 재료 차감
        currentSlot.itemCount -= matNeeded;

        // 승급은 실제 ScriptableObject를 변경할 수 없으므로 메시지만 표시
        // (실제 승급 시스템은 다음 등급 장비의 ItemData를 별도로 찾아 교체하는 로직 필요)
        UIManager.Instance?.ShowMessage(
            $"{currentEquip.itemName} 승급 준비 완료! ({curRarity} → {curRarity + 1})", Color.green);

        SoundManager.Instance?.PlayEnhanceSuccess();
        Debug.Log($"[EquipmentLevelUpPanel] 승급: {currentEquip.itemName} {curRarity} → {curRarity + 1}");

        // 해금 맵 동기화
        InventoryManager.Instance?.SyncEquipSlotToMap(
            currentEquip.itemID, currentSlot.itemCount,
            currentSlot.enhanceLevel, currentLevel);

        RefreshPromoteTab();
        InventoryManager.Instance?.RefreshEquipDisplay();
    }

    // ═══════════════════════════════════════════════════════════════
    //  유틸리티
    // ═══════════════════════════════════════════════════════════════

    /// <summary>스탯을 지정 색상으로 표시</summary>
    private string FormatStatsWithColor(EquipmentStats stats, string hexColor)
    {
        string result = "";
        if (stats.attack > 0) result += $"공격력: <color={hexColor}>{stats.attack}</color>\n";
        if (stats.defense > 0) result += $"방어력: <color={hexColor}>{stats.defense}</color>\n";
        if (stats.health > 0) result += $"체력: <color={hexColor}>{stats.health}</color>\n";
        if (stats.mana > 0) result += $"마나: <color={hexColor}>{stats.mana}</color>\n";
        if (stats.criticalRate > 0) result += $"크리티컬: <color={hexColor}>{stats.criticalRate}%</color>\n";
        if (stats.criticalDamage > 0) result += $"크리 데미지: <color={hexColor}>{stats.criticalDamage:F1}x</color>\n";
        if (stats.attackSpeed > 0) result += $"공격속도: <color={hexColor}>{stats.attackSpeed:F1}</color>\n";
        if (stats.moveSpeed > 0) result += $"이동속도: <color={hexColor}>{stats.moveSpeed:F1}</color>\n";
        return result.TrimEnd('\n');
    }

    private string FormatStats(EquipmentStats stats)
    {
        string result = "";
        if (stats.attack > 0) result += $"공격력: {stats.attack}\n";
        if (stats.defense > 0) result += $"방어력: {stats.defense}\n";
        if (stats.health > 0) result += $"체력: {stats.health}\n";
        if (stats.mana > 0) result += $"마나: {stats.mana}\n";
        if (stats.criticalRate > 0) result += $"크리티컬: {stats.criticalRate}%\n";
        if (stats.criticalDamage > 0) result += $"크리 데미지: {stats.criticalDamage:F1}x\n";
        if (stats.attackSpeed > 0) result += $"공격속도: {stats.attackSpeed:F1}\n";
        if (stats.moveSpeed > 0) result += $"이동속도: {stats.moveSpeed:F1}\n";
        return result.TrimEnd('\n');
    }

    private string FormatStatsWithDiff(EquipmentStats cur, EquipmentStats next)
    {
        string result = "";
        if (next.attack > 0)
            result += $"공격력: {next.attack} <color=#4CAF50>(+{next.attack - cur.attack})</color>\n";
        if (next.defense > 0)
            result += $"방어력: {next.defense} <color=#4CAF50>(+{next.defense - cur.defense})</color>\n";
        if (next.health > 0)
            result += $"체력: {next.health} <color=#4CAF50>(+{next.health - cur.health})</color>\n";
        if (next.mana > 0)
            result += $"마나: {next.mana} <color=#4CAF50>(+{next.mana - cur.mana})</color>\n";
        if (next.criticalRate > 0)
            result += $"크리: {next.criticalRate}% <color=#4CAF50>(+{next.criticalRate - cur.criticalRate})</color>\n";
        if (next.criticalDamage > 0)
            result += $"크뎀: {next.criticalDamage:F1}x <color=#4CAF50>(+{next.criticalDamage - cur.criticalDamage:F2})</color>\n";
        if (next.attackSpeed > 0)
            result += $"공속: {next.attackSpeed:F1} <color=#4CAF50>(+{next.attackSpeed - cur.attackSpeed:F2})</color>\n";
        if (next.moveSpeed > 0)
            result += $"이속: {next.moveSpeed:F1} <color=#4CAF50>(+{next.moveSpeed - cur.moveSpeed:F2})</color>\n";
        return result.TrimEnd('\n');
    }

    /// <summary>등급 한국어 이름</summary>
    private string GetRarityKorean(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return "일반";
            case ItemRarity.Uncommon: return "고급";
            case ItemRarity.Rare: return "희귀";
            case ItemRarity.Epic: return "영웅";
            case ItemRarity.Legendary: return "전설";
            default: return rarity.ToString();
        }
    }

    /// <summary>등급 배지 색상</summary>
    private Color GetBadgeColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return BadgeCommon;
            case ItemRarity.Uncommon: return new Color(0.2f, 0.8f, 0.2f);
            case ItemRarity.Rare: return BadgeRare;
            case ItemRarity.Epic: return BadgeEpic;
            case ItemRarity.Legendary: return BadgeLegendary;
            default: return Color.white;
        }
    }

    // ── UI 오브젝트 생성 헬퍼 ──

}
