using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 동료 인벤토리 슬롯 (아이콘 클릭 → 액션 버튼 토글)
///
/// Setup(CompanionSaveData, int) 으로 외부 주입.
/// 아이콘 클릭 시 하단 두 버튼(레벨업 / 핫바등록) 토글.
///
/// [프리팹 Hierarchy]
///   CompanionInventorySlot (CompanionInventorySlot.cs)
///   ├── Background (Image) — 등급별 배경색
///   ├── CompanionIcon (Image + Button) — 클릭 시 ActionButtons 토글
///   ├── GradeStars (Image) — 성급 스프라이트 (별 1~4)
///   ├── CompanionName (TextMeshProUGUI) — 동료 이름
///   ├── LevelText (TextMeshProUGUI) — "Lv.N"
///   ├── CountText (TextMeshProUGUI) — "x수량" (2+ 시)
///   └── ActionButtons (GameObject) — 아이콘 클릭 시 토글
///       ├── LevelUpButton (Button) — CompanionLevelUpPanel 오픈
///       └── HotbarRegisterButton (Button) — 핫바 등록
///
/// [Inspector 연결]
///   companionIcon       → CompanionIcon (Image)
///   gradeStarsImage     → GradeStars (Image) — 성급 스프라이트
///   gradeText           → GradeText (TextMeshProUGUI, 텍스트 폴백용)
///   companionNameText   → CompanionName
///   levelText           → LevelText
///   countText           → CountText
///   backgroundImage     → Background
///   actionButtons       → ActionButtons (GameObject)
///   levelUpButton       → LevelUpButton
///   hotbarRegisterButton → HotbarRegisterButton
///   gradeSprites        → 성급별 스프라이트 배열 [0]=1성, [1]=2성, [2]=3성, [3]=4성
/// </summary>
public class CompanionInventorySlot : MonoBehaviour, IPointerClickHandler
{
    [Header("슬롯 UI")]
    public Image companionIcon;
    public TextMeshProUGUI companionNameText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI countText;
    public Image backgroundImage;

    [Header("성급 표시")]
    [Tooltip("성급 스프라이트 이미지 (등급에 따라 교체)")]
    public Image gradeStarsImage;
    [Tooltip("성급 텍스트 폴백 (스프라이트 미설정 시 ★ 텍스트 표시)")]
    public TextMeshProUGUI gradeText;
    [Tooltip("성급별 스프라이트 [0]=Common ★, [1]=Rare ★★, [2]=Epic ★★★, [3]=Legendary ★★★★")]
    public Sprite[] gradeSprites;

    [Header("액션 버튼 (아이콘 클릭 시 토글)")]
    public GameObject actionButtons;
    public Button levelUpButton;
    public Button hotbarRegisterButton;

    // ── 내부 데이터 ──
    private CompanionData companionData;
    private CompanionSaveData saveData;
    private int slotIndex = -1;
    private bool actionButtonsVisible = false;

    // ═══ 등급별 색상 ═══
    private static readonly Color[] RarityBgColors =
    {
        new Color(0.25f, 0.25f, 0.25f, 0.9f),  // Common
        new Color(0.10f, 0.20f, 0.45f, 0.9f),  // Rare
        new Color(0.35f, 0.10f, 0.45f, 0.9f),  // Epic
        new Color(0.50f, 0.35f, 0.05f, 0.9f),  // Legendary
    };

    private static readonly Color[] RarityTextColors =
    {
        new Color(0.8f, 0.8f, 0.8f),   // Common
        new Color(0.3f, 0.5f, 1f),      // Rare
        new Color(0.8f, 0.3f, 1f),      // Epic
        new Color(1f, 0.7f, 0f),        // Legendary
    };

    private static string[] _gradeStarTextsCache;
    private static string[] GradeStarTexts
    {
        get
        {
            if (_gradeStarTextsCache == null)
            {
                _gradeStarTextsCache = new string[]
                {
                    StarSpriteUtil.GetStars(1), // Common
                    StarSpriteUtil.GetStars(2), // Rare
                    StarSpriteUtil.GetStars(3), // Epic
                    StarSpriteUtil.GetStars(4), // Legendary
                };
            }
            return _gradeStarTextsCache;
        }
    }

    void Start()
    {
        // 액션 버튼 초기 숨김
        if (actionButtons != null)
            actionButtons.SetActive(false);

        // 레벨업 버튼 바인딩 → 디테일 패널 열기
        if (levelUpButton != null)
            levelUpButton.onClick.AddListener(OnLevelUpClicked);

        // 핫바 등록 버튼 바인딩
        if (hotbarRegisterButton != null)
            hotbarRegisterButton.onClick.AddListener(OnHotbarRegisterClicked);
    }

    private void OnLevelUpClicked()
    {
        if (companionData == null) return;
        SoundManager.Instance?.PlayButtonClick();
        HideActionButtons();
        OpenDetailPanel();
    }

    // ═══════════════════════════════════════════════════════════════
    //  슬롯 설정
    // ═══════════════════════════════════════════════════════════════

    /// <summary>외부에서 CompanionSaveData + 인덱스로 주입</summary>
    public void Setup(CompanionSaveData data, int index)
    {
        saveData = data;
        slotIndex = index;

        // CompanionData SO 조회
        companionData = ResolveCompanionData(data);

        HideActionButtons();
        RefreshUI();
    }

    /// <summary>CompanionData SO로 직접 설정 (CompanionInventoryManager 호환)</summary>
    public void Setup(CompanionData data, int count, int index)
    {
        companionData = data;
        slotIndex = index;

        // CompanionSaveData 생성
        saveData = new CompanionSaveData
        {
            companionID = data != null ? data.companionID : "",
            count = count
        };

        HideActionButtons();
        RefreshUI();
    }

    /// <summary>CompanionData + 레벨 포함 설정 (BuildCompanionSlots용)</summary>
    public void SetupFromCompanionData(CompanionData data, int count, int level, int index)
    {
        companionData = data;
        slotIndex = index;

        saveData = new CompanionSaveData
        {
            companionID = data != null ? data.companionID : "",
            count = count,
            level = level
        };

        HideActionButtons();
        RefreshUI();
    }

    /// <summary>ItemData로 설정 (InventoryManager 동료탭 호환)</summary>
    public void Setup(ItemData item, int count, int index)
    {
        slotIndex = index;
        companionData = null;

        // ItemData 이름으로 CompanionData SO 검색
        if (item != null)
        {
            CompanionData[] allCompanions = Resources.FindObjectsOfTypeAll<CompanionData>();
            foreach (var c in allCompanions)
            {
                if (c != null && c.companionName == item.itemName)
                {
                    companionData = c;
                    break;
                }
            }
        }

        saveData = new CompanionSaveData
        {
            companionID = companionData != null ? companionData.companionID : (item != null ? item.itemName : ""),
            count = count
        };

        HideActionButtons();
        RefreshUI();

        // CompanionData를 못 찾았으면 ItemData 기반으로 기본 표시
        if (companionData == null && item != null)
        {
            if (companionIcon != null) { companionIcon.sprite = item.itemIcon; companionIcon.color = Color.white; companionIcon.gameObject.SetActive(true); }
            if (companionNameText != null) { companionNameText.text = item.itemName; companionNameText.gameObject.SetActive(true); }
            if (countText != null) { countText.text = count > 1 ? $"x{count}" : ""; countText.gameObject.SetActive(count > 1); }
        }
    }

    /// <summary>슬롯 초기화 (빈 슬롯)</summary>
    public void ClearSlot()
    {
        companionData = null;
        saveData = null;
        slotIndex = -1;
        HideActionButtons();
        RefreshUI();
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI 갱신
    // ═══════════════════════════════════════════════════════════════

    public void RefreshUI()
    {
        if (companionData != null)
        {
            int ri = (int)companionData.rarity;

            // ── 아이콘 ──
            if (companionIcon != null)
            {
                companionIcon.sprite = companionData.portrait;
                companionIcon.color = Color.white;
                companionIcon.gameObject.SetActive(true);
            }

            // ── 이름 ──
            if (companionNameText != null)
            {
                companionNameText.text = companionData.companionName;
                companionNameText.color = GetRarityTextColor(ri);
                companionNameText.gameObject.SetActive(true);
            }

            // ── 성급 (스프라이트 우선, 텍스트 폴백) ──
            UpdateGradeDisplay(ri);

            // ── 레벨 ──
            if (levelText != null)
            {
                int level = GetCompanionLevel();
                if (level > 0)
                {
                    levelText.text = $"Lv.{level}";
                    levelText.gameObject.SetActive(true);
                }
                else
                {
                    levelText.text = "Lv.1";
                    levelText.gameObject.SetActive(true);
                }
            }

            // ── 수량 ──
            if (countText != null)
            {
                int count = saveData != null ? saveData.count : 1;
                if (count > 1)
                {
                    countText.text = $"x{count}";
                    countText.gameObject.SetActive(true);
                }
                else
                {
                    countText.gameObject.SetActive(false);
                }
            }

            // ── 배경 ──
            if (backgroundImage != null)
            {
                backgroundImage.color = GetRarityBgColor(ri);
                backgroundImage.gameObject.SetActive(true);
            }
        }
        else
        {
            // ── 빈 슬롯 ──
            if (companionIcon != null)
            {
                companionIcon.sprite = null;
                companionIcon.color = new Color(1f, 1f, 1f, 0f);
                companionIcon.gameObject.SetActive(false);
            }
            if (companionNameText != null) companionNameText.gameObject.SetActive(false);
            if (levelText != null) levelText.gameObject.SetActive(false);
            if (countText != null) countText.gameObject.SetActive(false);

            if (gradeStarsImage != null) gradeStarsImage.gameObject.SetActive(false);
            if (gradeText != null) gradeText.gameObject.SetActive(false);

            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                backgroundImage.gameObject.SetActive(true);
            }
        }
    }

    /// <summary>성급 표시 갱신 — 스프라이트 우선, 없으면 텍스트 폴백</summary>
    private void UpdateGradeDisplay(int rarityIndex)
    {
        // 스프라이트 방식
        if (gradeStarsImage != null)
        {
            if (gradeSprites != null && rarityIndex < gradeSprites.Length
                && gradeSprites[rarityIndex] != null)
            {
                // ★ 부모 오브젝트도 같이 활성화
                Transform parent = gradeStarsImage.transform.parent;
                if (parent != null && !parent.gameObject.activeSelf)
                    parent.gameObject.SetActive(true);

                gradeStarsImage.sprite = gradeSprites[rarityIndex];
                gradeStarsImage.color = Color.white;
                gradeStarsImage.gameObject.SetActive(true);

                // 스프라이트가 있으면 텍스트 숨김
                if (gradeText != null) gradeText.gameObject.SetActive(false);
                return;
            }
            else
            {
                gradeStarsImage.gameObject.SetActive(false);
            }
        }

        // 텍스트 폴백
        if (gradeText != null)
        {
            Transform parent = gradeText.transform.parent;
            if (parent != null && !parent.gameObject.activeSelf)
                parent.gameObject.SetActive(true);

            gradeText.text = GetGradeStarText(rarityIndex);
            gradeText.color = GetRarityTextColor(rarityIndex);
            gradeText.gameObject.SetActive(true);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  클릭 → 액션 버튼 토글
    // ═══════════════════════════════════════════════════════════════

    public void OnPointerClick(PointerEventData eventData)
    {
        if (companionData == null) return;

        SoundManager.Instance?.PlayButtonClick();
        ToggleActionButtons();
    }

    private void ToggleActionButtons()
    {
        actionButtonsVisible = !actionButtonsVisible;
        if (actionButtons != null)
            actionButtons.SetActive(actionButtonsVisible);
    }

    public void HideActionButtons()
    {
        actionButtonsVisible = false;
        if (actionButtons != null)
            actionButtons.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════
    //  액션 버튼 핸들러
    // ═══════════════════════════════════════════════════════════════

    /// <summary>동료 슬롯 클릭 → CompanionDetailPanel 바로 열기</summary>
    private void OpenDetailPanel()
    {
        if (companionData == null) return;

        if (CompanionDetailPanel.Instance != null)
        {
            CompanionDetailPanel.Instance.Open(companionData);
        }
        else
        {
            var found = FindObjectOfType<CompanionDetailPanel>(true);
            if (found != null)
            {
                found.gameObject.SetActive(true);
                found.Open(companionData);
            }
            else
            {
                UIManager.Instance?.ShowMessage(
                    $"{companionData.companionName} Lv.{GetCompanionLevel()} — 패널 준비 중",
                    Color.yellow);
            }
        }
    }

    /// <summary>핫바 등록 버튼 → CompanionHotbarManager.RegisterCompanion</summary>
    private void OnHotbarRegisterClicked()
    {
        if (companionData == null) return;
        SoundManager.Instance?.PlayButtonClick();

        if (CompanionHotbarManager.Instance != null)
        {
            bool registered = CompanionHotbarManager.Instance.RegisterCompanion(companionData);
            if (registered)
            {
                UIManager.Instance?.ShowMessage(
                    $"{companionData.companionName}을(를) 핫바에 등록했습니다!\n(핫바 클릭으로 소환)",
                    Color.green);
            }
        }
        else
        {
            UIManager.Instance?.ShowMessage("핫바 시스템을 찾을 수 없습니다", Color.red);
            Debug.LogWarning("[CompanionInventorySlot] CompanionHotbarManager.Instance가 null!");
        }

        HideActionButtons();
    }

    // ═══════════════════════════════════════════════════════════════
    //  데이터 헬퍼
    // ═══════════════════════════════════════════════════════════════

    /// <summary>CompanionSaveData에서 CompanionData SO 조회</summary>
    private CompanionData ResolveCompanionData(CompanionSaveData data)
    {
        if (data == null || string.IsNullOrEmpty(data.companionID))
            return null;

        // CompanionInventoryManager에서 조회
        if (CompanionInventoryManager.Instance != null)
        {
            // 리플렉션 회피: Resources에서 직접 로드
            CompanionData[] allCompanions = Resources.FindObjectsOfTypeAll<CompanionData>();
            foreach (var c in allCompanions)
            {
                if (c != null && c.companionID == data.companionID)
                    return c;
            }
        }

        // Resources 폴더 폴백
        CompanionData[] loaded = Resources.LoadAll<CompanionData>("");
        foreach (var c in loaded)
        {
            if (c != null && c.companionID == data.companionID)
                return c;
        }

        Debug.LogWarning($"[CompanionInventorySlot] companionID '{data.companionID}'를 찾을 수 없습니다");
        return null;
    }

    /// <summary>동료 레벨 반환</summary>
    private int GetCompanionLevel()
    {
        if (saveData == null) return 1;
        return Mathf.Max(1, saveData.level);
    }


    // ═══════════════════════════════════════════════════════════════
    //  색상/텍스트 유틸
    // ═══════════════════════════════════════════════════════════════

    private Color GetRarityBgColor(int rarityIndex)
    {
        if (rarityIndex >= 0 && rarityIndex < RarityBgColors.Length)
            return RarityBgColors[rarityIndex];
        return RarityBgColors[0];
    }

    private Color GetRarityTextColor(int rarityIndex)
    {
        if (rarityIndex >= 0 && rarityIndex < RarityTextColors.Length)
            return RarityTextColors[rarityIndex];
        return Color.white;
    }

    private string GetGradeStarText(int rarityIndex)
    {
        if (rarityIndex >= 0 && rarityIndex < GradeStarTexts.Length)
            return GradeStarTexts[rarityIndex];
        return StarSpriteUtil.GetStars(1);
    }

    /// <summary>현재 설정된 CompanionData 반환 (외부 참조용)</summary>
    public CompanionData GetCompanionData() => companionData;

    /// <summary>현재 슬롯 인덱스 반환</summary>
    public int GetSlotIndex() => slotIndex;
}
