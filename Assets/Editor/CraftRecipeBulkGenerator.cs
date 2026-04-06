using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// CraftRecipeBulkGenerator — 장비 등급 조합 레시피 자동 생성
///
/// Unity 메뉴 > Tools > 조합 레시피 생성
///
/// [규칙]
///   같은 부위 + 같은 등급 장비를 조합 → 다음 등급 장비
///
///   2개 조합 (확률):
///     커먼 2개 → 언커먼 (50%)
///     언커먼 2개 → 레어 (50%)
///     레어 2개 → 에픽 (40%)
///     에픽 2개 → 레전드 (30%)
///
///   10개 합성 (100%):
///     커먼 10개 → 언커먼 (100%)
///     언커먼 10개 → 레어 (100%)
///     레어 10개 → 에픽 (100%)
///     에픽 10개 → 레전드 (100%)
///
///   6부위 × 4등급 × 2방식 = 48개 레시피
/// </summary>
public class CraftRecipeBulkGenerator : EditorWindow
{
    private bool deleteExisting = false;

    [MenuItem("Tools/조합 레시피 생성")]
    public static void ShowWindow()
    {
        GetWindow<CraftRecipeBulkGenerator>("조합 레시피 생성").minSize = new Vector2(400, 300);
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("장비 등급 조합 레시피 자동 생성", EditorStyles.boldLabel);

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "같은 부위 + 같은 등급 장비 조합 → 다음 등급\n\n" +
            "2개 조합: 커먼(50%) → 언커먼(50%) → 레어(40%) → 에픽(30%) → 레전드\n" +
            "10개 합성: 모든 등급 100% 성공\n\n" +
            "6부위 × 4등급 × 2방식 = 48개 레시피",
            MessageType.Info);

        EditorGUILayout.Space(10);
        deleteExisting = EditorGUILayout.ToggleLeft("기존 자동생성 레시피 삭제 후 재생성", deleteExisting);

        EditorGUILayout.Space(15);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button("레시피 생성 시작", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("조합 레시피 생성", "48개 레시피를 생성합니다.\n계속하시겠습니까?", "생성", "취소"))
                GenerateAll();
        }
        GUI.backgroundColor = Color.white;
    }

    private void GenerateAll()
    {
        string folder = "Assets/Data/Craft";
        EnsureFolder(folder);

        if (deleteExisting)
        {
            var guids = AssetDatabase.FindAssets("t:CraftRecipe", new[] { folder });
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.Contains("Auto_"))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        // 모든 EquipmentData 로드
        var allEquips = Resources.FindObjectsOfTypeAll<EquipmentData>()
            .Where(e => e != null)
            .ToList();

        Debug.Log($"[CraftRecipeGen] 발견된 장비 데이터: {allEquips.Count}개");

        // 부위별 + 등급별 분류
        var equipMap = new Dictionary<(EquipmentType, ItemRarity), List<EquipmentData>>();
        foreach (var eq in allEquips)
        {
            var key = (eq.equipmentType, eq.rarity);
            if (!equipMap.ContainsKey(key))
                equipMap[key] = new List<EquipmentData>();
            equipMap[key].Add(eq);
        }

        var slotTypes = new[] { EquipmentType.Helmet, EquipmentType.Armor, EquipmentType.WeaponLeft,
                                EquipmentType.WeaponRight, EquipmentType.Gloves, EquipmentType.Boots };
        var slotNames = new Dictionary<EquipmentType, string>
        {
            { EquipmentType.Helmet, "투구" },
            { EquipmentType.Armor, "갑옷" },
            { EquipmentType.WeaponLeft, "왼손무기" },
            { EquipmentType.WeaponRight, "오른손무기" },
            { EquipmentType.Gloves, "장갑" },
            { EquipmentType.Boots, "신발" }
        };

        var rarities = new[] { ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Epic };
        var rarityNames = new Dictionary<ItemRarity, string>
        {
            { ItemRarity.Common, "커먼" },
            { ItemRarity.Uncommon, "언커먼" },
            { ItemRarity.Rare, "레어" },
            { ItemRarity.Epic, "에픽" }
        };
        var nextRarity = new Dictionary<ItemRarity, ItemRarity>
        {
            { ItemRarity.Common, ItemRarity.Uncommon },
            { ItemRarity.Uncommon, ItemRarity.Rare },
            { ItemRarity.Rare, ItemRarity.Epic },
            { ItemRarity.Epic, ItemRarity.Legendary }
        };
        var nextRarityNames = new Dictionary<ItemRarity, string>
        {
            { ItemRarity.Common, "언커먼" },
            { ItemRarity.Uncommon, "레어" },
            { ItemRarity.Rare, "에픽" },
            { ItemRarity.Epic, "레전드" }
        };

        // 2개 조합 확률
        var twoChance = new Dictionary<ItemRarity, float>
        {
            { ItemRarity.Common, 50f },
            { ItemRarity.Uncommon, 50f },
            { ItemRarity.Rare, 40f },
            { ItemRarity.Epic, 30f }
        };

        int id = 20000;
        int created = 0;

        foreach (var slot in slotTypes)
        {
            string slotName = slotNames[slot];

            foreach (var rarity in rarities)
            {
                string rarityName = rarityNames[rarity];
                string nextName = nextRarityNames[rarity];
                ItemRarity next = nextRarity[rarity];

                // 재료 장비 찾기
                var sourceKey = (slot, rarity);
                EquipmentData sourceEquip = equipMap.ContainsKey(sourceKey) && equipMap[sourceKey].Count > 0
                    ? equipMap[sourceKey][0] : null;

                // 결과 장비 찾기
                var resultKey = (slot, next);
                EquipmentData resultEquip = equipMap.ContainsKey(resultKey) && equipMap[resultKey].Count > 0
                    ? equipMap[resultKey][0] : null;

                if (sourceEquip == null || resultEquip == null)
                {
                    Debug.LogWarning($"[CraftRecipeGen] 장비 없음: {slotName} {rarityName} → {nextName}");
                    continue;
                }

                // ── 2개 조합 (확률) ──
                var recipe2 = CreateInstance<CraftRecipe>();
                recipe2.recipeID = id++;
                recipe2.recipeName = $"{rarityName} {slotName} → {nextName} (2개)";
                recipe2.recipeDescription = $"{rarityName} {slotName} 2개를 조합하여 {nextName} 등급으로 승급합니다. (성공률 {twoChance[rarity]}%)";
                recipe2.category = GetCategory(slot);
                recipe2.resultItem = resultEquip;
                recipe2.resultAmount = 1;
                recipe2.ingredients = new CraftIngredient[]
                {
                    new CraftIngredient { item = sourceEquip, requiredAmount = 2 }
                };
                recipe2.requiredLevel = 1;
                recipe2.requiredGold = GetGoldCost(rarity, 2);
                recipe2.craftTime = 1f;
                recipe2.successRate = twoChance[rarity];
                recipe2.canFail = true;
                recipe2.recipeIcon = resultEquip.itemIcon;

                string path2 = $"{folder}/Auto_{recipe2.recipeID}_{SanitizeName($"{rarityName}_{slotName}_2개")}.asset";
                AssetDatabase.CreateAsset(recipe2, path2);
                created++;

                // ── 10개 합성 (100%) ──
                var recipe10 = CreateInstance<CraftRecipe>();
                recipe10.recipeID = id++;
                recipe10.recipeName = $"{rarityName} {slotName} → {nextName} (10개)";
                recipe10.recipeDescription = $"{rarityName} {slotName} 10개를 합성하여 {nextName} 등급으로 확정 승급합니다.";
                recipe10.category = GetCategory(slot);
                recipe10.resultItem = resultEquip;
                recipe10.resultAmount = 1;
                recipe10.ingredients = new CraftIngredient[]
                {
                    new CraftIngredient { item = sourceEquip, requiredAmount = 10 }
                };
                recipe10.requiredLevel = 1;
                recipe10.requiredGold = GetGoldCost(rarity, 10);
                recipe10.craftTime = 1f;
                recipe10.successRate = 100f;
                recipe10.canFail = false;
                recipe10.recipeIcon = resultEquip.itemIcon;

                string path10 = $"{folder}/Auto_{recipe10.recipeID}_{SanitizeName($"{rarityName}_{slotName}_10개")}.asset";
                AssetDatabase.CreateAsset(recipe10, path10);
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("완료", $"{created}개 조합 레시피가 생성되었습니다!\n경로: {folder}", "확인");
        Debug.Log($"[CraftRecipeGen] {created}개 레시피 생성 완료");
    }

    private CraftCategory GetCategory(EquipmentType type) => type switch
    {
        EquipmentType.WeaponLeft  => CraftCategory.Weapon,
        EquipmentType.WeaponRight => CraftCategory.Weapon,
        _ => CraftCategory.Armor
    };

    private int GetGoldCost(ItemRarity rarity, int count) => rarity switch
    {
        ItemRarity.Common   => 500 * count,
        ItemRarity.Uncommon => 1000 * count,
        ItemRarity.Rare     => 2000 * count,
        ItemRarity.Epic     => 5000 * count,
        _ => 1000 * count
    };

    private string SanitizeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Replace(' ', '_');
        if (name.Length > 40) name = name.Substring(0, 40);
        return name;
    }

    private void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
