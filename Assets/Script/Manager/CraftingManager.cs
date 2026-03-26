using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance;

    [Header("레시피")]
    public List<CraftRecipe> knownRecipes = new List<CraftRecipe>();
    public List<CraftRecipe> allRecipes = new List<CraftRecipe>();

    [Header("제작 UI 패널")]
    public GameObject craftingPanel;
    public Transform recipeListParent;
    public GameObject recipeSlotPrefab;

    [Header("상세 정보 패널")]
    public CraftingDetailView detailView;  // ⭐ 추가

    [Header("제작 진행 UI")]
    public GameObject craftingProgressPanel;
    public UnityEngine.UI.Slider progressBar;
    public TMPro.TextMeshProUGUI progressText;

    [Header("플레이어 참조")]
    public PlayerStats playerStats;

    private bool isCrafting = false;
    private List<CraftingSlot> recipeSlots = new List<CraftingSlot>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            enabled = false;
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (Instance != this) return;
        if (craftingPanel != null)
        {
            craftingPanel.SetActive(false);
        }

        if (craftingProgressPanel != null)
        {
            craftingProgressPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            ToggleCraftingUI();
        }
    }

    public void ToggleCraftingUI()
    {
        if (craftingPanel != null)
        {
            bool isActive = craftingPanel.activeSelf;
            craftingPanel.SetActive(!isActive);

            if (!isActive)
            {
                UpdateCraftingUI();
            }
        }
    }

    public bool LearnRecipe(CraftRecipe recipe)
    {
        if (recipe == null || knownRecipes.Contains(recipe)) return false;

        knownRecipes.Add(recipe);
        Debug.Log($"레시피 습득: {recipe.recipeName}");

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessage($"레시피 습득: {recipe.recipeName}");
        }

        UpdateCraftingUI();
        return true;
    }

    public void CraftItem(CraftRecipe recipe)
    {
        // ★ 조합 버튼 클릭 효과음
        SoundManager.Instance?.PlayButtonClick();
        if (recipe == null || isCrafting)
        {
            Debug.Log("제작 불가능!");
            return;
        }

        int playerLevel = playerStats != null ? Mathf.Max(1, playerStats.level) : 1;
        int playerGold = GameManager.Instance != null ? GameManager.Instance.playerGold : 0;

        if (!recipe.CanCraft(playerLevel, playerGold))
        {
            CheckMissingIngredients(recipe);
            return;
        }

        StartCoroutine(CraftItemCoroutine(recipe));
    }

    IEnumerator CraftItemCoroutine(CraftRecipe recipe)
    {
        isCrafting = true;

        if (craftingProgressPanel != null)
        {
            craftingProgressPanel.SetActive(true);
        }

        // ⭐ DetailView 진행 상태 업데이트
        if (detailView != null)
        {
            detailView.UpdateProgress(0f, "제작 준비 중...");
        }

        ConsumeIngredients(recipe);

        if (GameManager.Instance != null && recipe.requiredGold > 0)
        {
            GameManager.Instance.SpendGold(recipe.requiredGold);
        }

        float elapsed = 0f;
        while (elapsed < recipe.craftTime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / recipe.craftTime;

            if (progressBar != null)
            {
                progressBar.value = progress;
            }

            if (progressText != null)
            {
                progressText.text = $"제작 중... {(progress * 100f):F0}%";
            }

            // ⭐ DetailView 진행 상태 업데이트
            if (detailView != null)
            {
                detailView.UpdateProgress(progress, $"제작 중... {(progress * 100f):F0}%");
            }

            yield return null;
        }

        bool success = CheckCraftSuccess(recipe);

        if (success)
        {
            // ★ 조합 성공 효과음
            SoundManager.Instance?.PlayCraftSuccess();
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddItem(recipe.resultItem, recipe.resultAmount);
            }

            Debug.Log($"제작 성공: {recipe.resultItem.itemName}");

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMessage($"제작 성공! {recipe.resultItem.itemName}");
            }
        }
        else
        {
            // ★ 조합 실패 효과음
            SoundManager.Instance?.PlayCraftFail();
            Debug.Log($"제작 실패: {recipe.recipeName}");

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMessage("제작 실패...");
            }
        }

        if (craftingProgressPanel != null)
        {
            craftingProgressPanel.SetActive(false);
        }

        // ⭐ DetailView 진행 상태 초기화
        if (detailView != null)
        {
            detailView.UpdateProgress(0f, "");
        }

        isCrafting = false;
        SaveLoadManager.Instance?.SaveGame();
        UpdateCraftingUI(); // 재료 소모 후 UI 갱신
    }

    void ConsumeIngredients(CraftRecipe recipe)
    {
        if (InventoryManager.Instance == null) return;

        foreach (CraftIngredient ingredient in recipe.ingredients)
        {
            InventoryManager.Instance.RemoveItem(ingredient.item, ingredient.requiredAmount);
        }
    }

    bool CheckCraftSuccess(CraftRecipe recipe)
    {
        if (!recipe.canFail) return true;

        float roll = Random.Range(0f, 100f);
        return roll < recipe.successRate;
    }

    void CheckMissingIngredients(CraftRecipe recipe)
    {
        if (InventoryManager.Instance == null) return;

        List<string> missingItems = new List<string>();

        if (playerStats != null && playerStats.level < recipe.requiredLevel)
        {
            missingItems.Add($"Lv.{recipe.requiredLevel} 필요");
        }

        if (GameManager.Instance != null && GameManager.Instance.playerGold < recipe.requiredGold)
        {
            int needed = recipe.requiredGold - GameManager.Instance.playerGold;
            missingItems.Add($"{needed}G 부족");
        }

        foreach (CraftIngredient ingredient in recipe.ingredients)
        {
            int currentAmount = InventoryManager.Instance.GetItemCount(ingredient.item);
            if (currentAmount < ingredient.requiredAmount)
            {
                int needed = ingredient.requiredAmount - currentAmount;
                missingItems.Add($"{ingredient.item.itemName} x{needed}");
            }
        }

        if (missingItems.Count > 0)
        {
            string message = "부족: " + string.Join(", ", missingItems);
            Debug.Log(message);

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMessage(message);
            }
        }
    }

    void UpdateCraftingUI()
    {
        foreach (CraftingSlot slot in recipeSlots)
        {
            if (slot != null) Destroy(slot.gameObject);
        }
        recipeSlots.Clear();

        foreach (CraftRecipe recipe in knownRecipes)
        {
            if (recipeSlotPrefab != null && recipeListParent != null)
            {
                GameObject slotObj = Instantiate(recipeSlotPrefab, recipeListParent);
                CraftingSlot slot = slotObj.GetComponent<CraftingSlot>();

                if (slot != null)
                {
                    slot.SetupSlot(recipe);
                    recipeSlots.Add(slot);
                }
            }
        }
    }

    public List<CraftRecipe> GetRecipesByCategory(CraftCategory category)
    {
        return knownRecipes.Where(r => r.category == category).ToList();
    }

    public List<CraftRecipe> GetCraftableRecipes()
    {
        int playerLevel = playerStats != null ? Mathf.Max(1, playerStats.level) : 1;
        int playerGold = GameManager.Instance != null ? GameManager.Instance.playerGold : 0;

        return knownRecipes.Where(r => r.CanCraft(playerLevel, playerGold)).ToList();
    }
}