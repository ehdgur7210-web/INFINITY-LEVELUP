using UnityEngine;

// 제작 카테고리
public enum CraftCategory
{
    Weapon,     // 무기
    Armor,      // 방어구
    Consumable, // 소비 아이템
    Material,   // 재료
    Misc        // 기타
}

// 제작 재료
[System.Serializable]
public class CraftIngredient
{
    public ItemData item;       // 재료 아이템
    public int requiredAmount;  // 필요 개수
}

// 제작 레시피 데이터
[CreateAssetMenu(fileName = "New Recipe", menuName = "Game/Craft Recipe")]
public class CraftRecipe : ScriptableObject
{
    [Header("기본 정보")]
    public int recipeID;                        // 레시피 ID
    public string recipeName;                   // 레시피 이름
    
    [TextArea(2, 4)]
    public string recipeDescription;            // 레시피 설명
    
    [Header("카테고리")]
    public CraftCategory category;              // 제작 카테고리
    
    [Header("결과물")]
    public ItemData resultItem;                 // 제작 결과 아이템
    public int resultAmount = 1;                // 제작 개수
    
    [Header("재료")]
    public CraftIngredient[] ingredients;       // 필요 재료들
    
    [Header("제작 조건")]
    public int requiredLevel = 1;               // 요구 레벨
    public int requiredGold = 0;                // 제작 비용 (골드)
    public float craftTime = 1f;                // 제작 시간 (초)
    
    [Header("선행 레시피")]
    public CraftRecipe[] prerequisiteRecipes;   // 선행 레시피
    
    [Header("성공 확률")]
    public float successRate = 100f;            // 제작 성공 확률 (%)
    public bool canFail = false;                // 실패 가능 여부
    
    [Header("UI")]
    public Sprite recipeIcon;                   // 레시피 아이콘
    
    /// <summary>
    /// 제작 가능 여부 확인
    /// </summary>
    public bool CanCraft(int playerLevel, long playerGold)
    {
        // 레벨 확인
        if (playerLevel < requiredLevel)
        {
            return false;
        }

        // 골드 확인
        if (playerGold < requiredGold)
        {
            return false;
        }

        // 재료 확인
        if (InventoryManager.Instance != null)
        {
            foreach (CraftIngredient ingredient in ingredients)
            {
                if (!InventoryManager.Instance.HasItem(ingredient.item, ingredient.requiredAmount))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
