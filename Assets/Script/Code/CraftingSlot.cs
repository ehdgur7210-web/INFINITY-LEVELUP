using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CraftingSlot : MonoBehaviour
{
    [Header("레시피 정보")]
    public CraftRecipe recipe;

    [Header("UI 참조")]
    public Image recipeIconImage;
    public TextMeshProUGUI recipeNameText;
    public Button selectButton;  // ⭐ 제작 버튼 → 선택 버튼으로 변경
    public Image statusIcon;

    [Header("상태 색상")]
    public Color craftableColor = Color.green;
    public Color notCraftableColor = Color.red;

    void Start()
    {
        if (selectButton != null)
        {
            selectButton.onClick.AddListener(OnSlotClicked);
        }
    }

    void Update()
    {
        UpdateCraftableStatus();
    }

    public void SetupSlot(CraftRecipe craftRecipe)
    {
        recipe = craftRecipe;
        UpdateSlotUI();
    }

    void UpdateSlotUI()
    {
        if (recipe == null) return;

        // 아이콘
        if (recipeIconImage != null)
        {
            if (recipe.recipeIcon != null)
            {
                recipeIconImage.sprite = recipe.recipeIcon;
            }
            else if (recipe.resultItem != null && recipe.resultItem.itemIcon != null)
            {
                recipeIconImage.sprite = recipe.resultItem.itemIcon;
            }
            recipeIconImage.color = Color.white;
        }

        // 이름
        if (recipeNameText != null)
        {
            recipeNameText.text = recipe.recipeName;
        }
    }

    void UpdateCraftableStatus()
    {
        if (recipe == null) return;

        int playerLevel = 1;
        long playerGold = 0;

        if (CraftingManager.Instance != null && CraftingManager.Instance.playerStats != null)
        {
            playerLevel = CraftingManager.Instance.playerStats.level;
        }

        if (GameManager.Instance != null)
        {
            playerGold = GameManager.Instance.playerGold;
        }

        bool canCraft = recipe.CanCraft(playerLevel, playerGold);

        if (statusIcon != null)
        {
            statusIcon.color = canCraft ? craftableColor : notCraftableColor;
        }
    }

    /// <summary>
    /// ⭐ 슬롯 클릭 → DetailView에 정보 표시
    /// </summary>
    void OnSlotClicked()
    {
        // ★ 레시피 슬롯 클릭 효과음
        SoundManager.Instance?.PlayButtonClick();
        if (recipe != null && CraftingManager.Instance != null)
        {
            if (CraftingManager.Instance.detailView != null)
            {
                CraftingManager.Instance.detailView.ShowRecipeDetail(recipe);
            }
        }
    }
}