using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 조합 상세 정보 패널
/// - 선택한 레시피의 자세한 정보 표시
/// - 결과 아이템 이미지 표시 ⭐
/// </summary>
public class CraftingDetailView : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private GameObject detailPanel;

    [Header("결과 아이템")]
    [SerializeField] private Image resultItemIcon;           // ⭐ 결과 아이템 아이콘
    [SerializeField] private Image resultItemBackground;     // ⭐ 배경 (등급별 색상)
    [SerializeField] private TextMeshProUGUI resultItemName;
    [SerializeField] private TextMeshProUGUI resultItemDescription;

    [Header("재료 목록")]
    [SerializeField] private Transform ingredientListParent;
    [SerializeField] private GameObject ingredientSlotPrefab;

    [Header("비용 및 조건")]
    [SerializeField] private TextMeshProUGUI goldCostText;
    [SerializeField] private TextMeshProUGUI levelRequirementText;
    [SerializeField] private TextMeshProUGUI craftTimeText;
    [SerializeField] private TextMeshProUGUI successRateText;

    [Header("제작 버튼")]
    [SerializeField] private Button craftButton;
    [SerializeField] private TextMeshProUGUI craftButtonText;

    [Header("진행 상태")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TextMeshProUGUI progressText;

    private CraftRecipe currentRecipe;
    private List<GameObject> currentIngredientSlots = new List<GameObject>();

    void Start()
    {
        if (craftButton != null)
        {
            craftButton.onClick.AddListener(OnCraftButtonClicked);
        }

        HideDetail();
    }

    /// <summary>
    /// ⭐ 레시피 상세 정보 표시
    /// </summary>
    public void ShowRecipeDetail(CraftRecipe recipe)
    {
        if (recipe == null) return;

        currentRecipe = recipe;

        if (detailPanel != null)
        {
            detailPanel.SetActive(true);
        }

        // ⭐⭐⭐ 결과 아이템 이미지 표시
        UpdateResultItemDisplay(recipe);

        // 재료 목록 표시
        UpdateIngredientsList(recipe);

        // 비용 및 조건 표시
        UpdateRequirements(recipe);

        // 제작 가능 여부 확인
        UpdateCraftButtonState();
    }

    /// <summary>
    /// ⭐⭐⭐ 결과 아이템 이미지 및 정보 표시
    /// </summary>
    private void UpdateResultItemDisplay(CraftRecipe recipe)
    {
        if (recipe.resultItem == null) return;

        // 아이템 아이콘
        if (resultItemIcon != null)
        {
            resultItemIcon.sprite = recipe.resultItem.itemIcon;
            resultItemIcon.gameObject.SetActive(true);
            resultItemIcon.color = Color.white;

            Debug.Log($"[CraftingDetailView] 결과 아이템 이미지 설정: {recipe.resultItem.itemName}");
        }

        // ⭐ 등급별 배경색
        if (resultItemBackground != null)
        {
            Color bgColor = GetRarityColor(recipe.resultItem.rarity);
            resultItemBackground.color = bgColor;
            resultItemBackground.gameObject.SetActive(true);
        }

        // 아이템 이름
        if (resultItemName != null)
        {
            resultItemName.text = recipe.resultItem.itemName;
        }

        // 아이템 설명
        if (resultItemDescription != null)
        {
            resultItemDescription.text = recipe.resultItem.itemDescription;
        }
    }

    /// <summary>
    /// 재료 목록 업데이트
    /// </summary>
    private void UpdateIngredientsList(CraftRecipe recipe)
    {
        // 기존 슬롯 제거
        ClearIngredientSlots();

        if (ingredientListParent == null || ingredientSlotPrefab == null)
        {
            Debug.LogWarning("[CraftingDetailView] 재료 슬롯 설정이 없습니다!");
            return;
        }

        foreach (CraftIngredient ingredient in recipe.ingredients)
        {
            GameObject slotObj = Instantiate(ingredientSlotPrefab, ingredientListParent);
            CraftingIngredientSlot slot = slotObj.GetComponent<CraftingIngredientSlot>();

            if (slot != null)
            {
                int currentAmount = 0;
                if (InventoryManager.Instance != null)
                {
                    currentAmount = InventoryManager.Instance.GetItemCount(ingredient.item);
                }

                slot.SetupSlot(ingredient.item, ingredient.requiredAmount, currentAmount);
            }

            currentIngredientSlots.Add(slotObj);
        }
    }

    /// <summary>
    /// 비용 및 조건 업데이트
    /// </summary>
    private void UpdateRequirements(CraftRecipe recipe)
    {
        // 골드 비용
        if (goldCostText != null && recipe.requiredGold > 0)
        {
            long playerGold = GameManager.Instance != null ? GameManager.Instance.playerGold : 0;
            bool hasEnough = playerGold >= recipe.requiredGold;

            string colorTag = hasEnough ? "green" : "red";
            goldCostText.text = $"<color={colorTag}>골드: {recipe.requiredGold}</color>";
            goldCostText.gameObject.SetActive(true);
        }
        else if (goldCostText != null)
        {
            goldCostText.gameObject.SetActive(false);
        }

        // 레벨 요구사항
        if (levelRequirementText != null && recipe.requiredLevel > 1)
        {
            int playerLevel = 1;
            if (GameManager.Instance != null && GameManager.Instance.PlayerLevel > 0)
            {
                playerLevel = GameManager.Instance.PlayerLevel;
            }

            bool hasEnough = playerLevel >= recipe.requiredLevel;
            string colorTag = hasEnough ? "green" : "red";
            levelRequirementText.text = $"<color={colorTag}>요구 레벨: {recipe.requiredLevel}</color>";
            levelRequirementText.gameObject.SetActive(true);
        }
        else if (levelRequirementText != null)
        {
            levelRequirementText.gameObject.SetActive(false);
        }

        // 제작 시간
        if (craftTimeText != null && recipe.craftTime > 0)
        {
            craftTimeText.text = $"제작 시간: {recipe.craftTime}초";
            craftTimeText.gameObject.SetActive(true);
        }
        else if (craftTimeText != null)
        {
            craftTimeText.gameObject.SetActive(false);
        }

        // 성공 확률
        if (successRateText != null && recipe.canFail)
        {
            successRateText.text = $"성공률: {recipe.successRate}%";
            successRateText.gameObject.SetActive(true);
        }
        else if (successRateText != null)
        {
            successRateText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 제작 버튼 상태 업데이트
    /// </summary>
    private void UpdateCraftButtonState()
    {
        if (currentRecipe == null || craftButton == null) return;

        int playerLevel = GameManager.Instance != null ? GameManager.Instance.PlayerLevel : 1;
        long playerGold = GameManager.Instance != null ? GameManager.Instance.playerGold : 0;

        bool canCraft = currentRecipe.CanCraft(playerLevel, playerGold);

        craftButton.interactable = canCraft;

        if (craftButtonText != null)
        {
            craftButtonText.text = canCraft ? "제작" : "재료 부족";
        }
    }

    /// <summary>
    /// 제작 버튼 클릭
    /// </summary>
    private void OnCraftButtonClicked()
    {
        if (currentRecipe == null || CraftingManager.Instance == null) return;

        CraftingManager.Instance.CraftItem(currentRecipe);
    }

    /// <summary>
    /// 상세 정보 숨기기
    /// </summary>
    public void HideDetail()
    {
        if (detailPanel != null)
        {
            detailPanel.SetActive(false);
        }

        ClearIngredientSlots();
    }

    /// <summary>
    /// 재료 슬롯 제거
    /// </summary>
    private void ClearIngredientSlots()
    {
        foreach (GameObject slot in currentIngredientSlots)
        {
            if (slot != null)
            {
                Destroy(slot);
            }
        }

        currentIngredientSlots.Clear();
    }

    /// <summary>
    /// ⭐ 등급별 색상 반환
    /// </summary>
    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common:
                return new Color(0.5f, 0.5f, 0.5f, 0.4f);     // 회색
            case ItemRarity.Uncommon:
                return new Color(0f, 1f, 0f, 0.5f);           // 초록
            case ItemRarity.Rare:
                return new Color(0f, 0.5f, 1f, 0.6f);         // 파랑
            case ItemRarity.Epic:
                return new Color(0.6f, 0f, 1f, 0.7f);         // 보라
            case ItemRarity.Legendary:
                return new Color(1f, 0.5f, 0f, 0.8f);         // 주황
            default:
                return new Color(1f, 1f, 1f, 0.3f);           // 투명
        }
    }

    /// <summary>
    /// 진행 상태 업데이트
    /// </summary>
    public void UpdateProgress(float progress, string status)
    {
        if (progressSlider != null)
        {
            progressSlider.value = progress;
            progressSlider.gameObject.SetActive(progress > 0f && progress < 1f);
        }

        if (progressText != null)
        {
            progressText.text = status;
            progressText.gameObject.SetActive(!string.IsNullOrEmpty(status));
        }
    }
}


