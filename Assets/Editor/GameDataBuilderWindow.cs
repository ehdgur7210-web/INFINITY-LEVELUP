using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// GameDataBuilderWindow — 게임 데이터 에디터 툴
///
/// Unity 메뉴 > Tools > 게임 데이터 빌더
///
/// [탭]
///   1. 퀘스트 — QuestData ScriptableObject 생성
///   2. 업적   — AchievementData ScriptableObject 생성
///   3. 조합   — CraftRecipe ScriptableObject 생성
///   4. 농장   — FarmQuestTemplateSO ScriptableObject 생성
///
/// 입력 후 [생성] 버튼 → Assets/Data/{카테고리}/ 폴더에 에셋 자동 생성
/// </summary>
public class GameDataBuilderWindow : EditorWindow
{
    private int selectedTab = 0;
    private readonly string[] tabNames = { "퀘스트", "업적", "조합", "농장퀘스트" };
    private Vector2 scrollPos;
    private Vector2 listScrollPos;
    private bool showList = false;

    // ── 퀘스트 입력 필드 ──
    private int q_id;
    private string q_name = "";
    private string q_description = "";
    private string q_objectiveText = "";
    private int q_requiredLevel = 1;
    private string q_giverNPC = "";
    private string q_completeNPC = "";
    // 목표
    private QuestType q_objectiveType = QuestType.Kill;
    private string q_targetID = "";
    private int q_requiredAmount = 10;
    // 보상
    private int q_rewardGold = 1000;
    private int q_rewardExp = 500;

    // ── 업적 입력 필드 ──
    private int a_id;
    private string a_name = "";
    private string a_description = "";
    private AchievementType a_type = AchievementType.KillMonsters;
    private AchievementGrade a_grade = AchievementGrade.Bronze;
    private int a_targetAmount = 100;
    private string a_targetID = "";
    private bool a_isHidden = false;
    private int a_rewardGold = 5000;
    private int a_rewardGem = 10;
    private int a_rewardExp = 0;
    private string a_rewardTitle = "";

    // ── 조합 입력 필드 ──
    private int c_id;
    private string c_name = "";
    private string c_description = "";
    private CraftCategory c_category = CraftCategory.Consumable;
    private ItemData c_resultItem;
    private int c_resultAmount = 1;
    private int c_requiredLevel = 1;
    private int c_requiredGold = 100;
    private float c_craftTime = 1f;
    private float c_successRate = 100f;
    private bool c_canFail = false;
    private Sprite c_icon;
    // 재료 (최대 5개)
    private ItemData[] c_ingredientItems = new ItemData[5];
    private int[] c_ingredientAmounts = new int[] { 1, 1, 1, 1, 1 };
    private int c_ingredientCount = 1;

    // ── 농장퀘스트 입력 필드 ──
    private string f_title = "작물 납품 퀘스트";
    private string f_description = "작물을 수확해서 납품하세요!";
    private int f_targetCropID = 0;
    private QuestDifficulty f_difficulty = QuestDifficulty.Normal;
    private int f_baseRequired = 3;
    private int f_requiredPerTenLvl = 1;
    private int f_baseCropPoint = 10;
    private int f_cropPointPerTenLvl = 5;
    private int f_goldReward = 200;
    private Sprite f_icon;

    [MenuItem("Tools/게임 데이터 빌더")]
    public static void ShowWindow()
    {
        var window = GetWindow<GameDataBuilderWindow>("게임 데이터 빌더");
        window.minSize = new Vector2(450, 600);
    }

    void OnGUI()
    {
        EditorGUILayout.Space(5);
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(30));
        EditorGUILayout.Space(10);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        switch (selectedTab)
        {
            case 0: DrawQuestTab(); break;
            case 1: DrawAchievementTab(); break;
            case 2: DrawCraftTab(); break;
            case 3: DrawFarmQuestTab(); break;
        }

        EditorGUILayout.Space(20);

        // ── 기존 에셋 목록 토글 ──
        showList = EditorGUILayout.Foldout(showList, "기존 에셋 목록 보기", true);
        if (showList) DrawExistingAssetList();

        EditorGUILayout.EndScrollView();
    }

    // ═══════════════════════════════════════
    //  퀘스트 탭
    // ═══════════════════════════════════════

    private void DrawQuestTab()
    {
        EditorGUILayout.LabelField("퀘스트 생성", EditorStyles.boldLabel);
        DrawSeparator();

        q_id = EditorGUILayout.IntField("퀘스트 ID", q_id);
        q_name = EditorGUILayout.TextField("퀘스트 이름", q_name);
        q_description = EditorGUILayout.TextField("설명", q_description);
        q_requiredLevel = EditorGUILayout.IntField("필요 레벨", q_requiredLevel);
        q_giverNPC = EditorGUILayout.TextField("퀘스트 NPC", q_giverNPC);
        q_completeNPC = EditorGUILayout.TextField("완료 NPC", q_completeNPC);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── 목표 ──", EditorStyles.boldLabel);
        q_objectiveType = (QuestType)EditorGUILayout.EnumPopup("목표 타입", q_objectiveType);
        q_objectiveText = EditorGUILayout.TextField("목표 설명", q_objectiveText);
        q_targetID = EditorGUILayout.TextField("대상 ID", q_targetID);
        q_requiredAmount = EditorGUILayout.IntField("필요 수량", q_requiredAmount);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── 보상 ──", EditorStyles.boldLabel);
        q_rewardGold = EditorGUILayout.IntField("골드", q_rewardGold);
        q_rewardExp = EditorGUILayout.IntField("경험치", q_rewardExp);

        EditorGUILayout.Space(15);
        if (GUILayout.Button("퀘스트 생성", GUILayout.Height(35)))
            CreateQuestData();
    }

    private void CreateQuestData()
    {
        if (string.IsNullOrEmpty(q_name)) { ShowNotification(new GUIContent("이름을 입력하세요!")); return; }

        EnsureFolder("Assets/Data/Quest");

        var quest = CreateInstance<QuestData>();
        quest.questID = q_id;
        quest.questName = q_name;
        quest.questDescription = q_description;
        quest.questObjectiveText = q_objectiveText;
        quest.requiredLevel = q_requiredLevel;
        quest.questGiverNPC = q_giverNPC;
        quest.questCompleteNPC = q_completeNPC;

        quest.objectives = new QuestObjective[]
        {
            new QuestObjective
            {
                objectiveName = q_objectiveText,
                objectiveType = q_objectiveType,
                targetID = q_targetID,
                requiredAmount = q_requiredAmount,
                currentAmount = 0
            }
        };

        quest.reward = new QuestReward
        {
            gold = q_rewardGold,
            exp = q_rewardExp,
            rewardItems = new ItemData[0],
            itemCounts = new int[0]
        };

        string path = $"Assets/Data/Quest/Quest_{q_id}_{SanitizeFileName(q_name)}.asset";
        AssetDatabase.CreateAsset(quest, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(quest);
        ShowNotification(new GUIContent($"퀘스트 생성 완료: {q_name}"));
        q_id++;
    }

    // ═══════════════════════════════════════
    //  업적 탭
    // ═══════════════════════════════════════

    private void DrawAchievementTab()
    {
        EditorGUILayout.LabelField("업적 생성", EditorStyles.boldLabel);
        DrawSeparator();

        a_id = EditorGUILayout.IntField("업적 ID", a_id);
        a_name = EditorGUILayout.TextField("업적 이름", a_name);
        a_description = EditorGUILayout.TextField("설명", a_description);
        a_type = (AchievementType)EditorGUILayout.EnumPopup("업적 타입", a_type);
        a_grade = (AchievementGrade)EditorGUILayout.EnumPopup("등급", a_grade);
        a_targetAmount = EditorGUILayout.IntField("목표 수량", a_targetAmount);
        a_targetID = EditorGUILayout.TextField("대상 ID (선택)", a_targetID);
        a_isHidden = EditorGUILayout.Toggle("숨김 업적", a_isHidden);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── 보상 ──", EditorStyles.boldLabel);
        a_rewardGold = EditorGUILayout.IntField("골드", a_rewardGold);
        a_rewardGem = EditorGUILayout.IntField("젬", a_rewardGem);
        a_rewardExp = EditorGUILayout.IntField("경험치", a_rewardExp);
        a_rewardTitle = EditorGUILayout.TextField("칭호 (선택)", a_rewardTitle);

        EditorGUILayout.Space(15);
        if (GUILayout.Button("업적 생성", GUILayout.Height(35)))
            CreateAchievementData();
    }

    private void CreateAchievementData()
    {
        if (string.IsNullOrEmpty(a_name)) { ShowNotification(new GUIContent("이름을 입력하세요!")); return; }

        EnsureFolder("Assets/Data/Achievement");

        var achieve = CreateInstance<AchievementData>();
        achieve.achievementID = a_id;
        achieve.achievementName = a_name;
        achieve.description = a_description;
        achieve.type = a_type;
        achieve.grade = a_grade;
        achieve.targetAmount = a_targetAmount;
        achieve.targetID = a_targetID;
        achieve.isHidden = a_isHidden;

        achieve.reward = new AchievementReward
        {
            gold = a_rewardGold,
            gem = a_rewardGem,
            exp = a_rewardExp,
            title = a_rewardTitle,
            items = new ItemData[0]
        };

        string path = $"Assets/Data/Achievement/Achieve_{a_id}_{SanitizeFileName(a_name)}.asset";
        AssetDatabase.CreateAsset(achieve, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(achieve);
        ShowNotification(new GUIContent($"업적 생성 완료: {a_name}"));
        a_id++;
    }

    // ═══════════════════════════════════════
    //  조합 탭
    // ═══════════════════════════════════════

    private void DrawCraftTab()
    {
        EditorGUILayout.LabelField("조합 레시피 생성", EditorStyles.boldLabel);
        DrawSeparator();

        c_id = EditorGUILayout.IntField("레시피 ID", c_id);
        c_name = EditorGUILayout.TextField("레시피 이름", c_name);
        c_description = EditorGUILayout.TextField("설명", c_description);
        c_category = (CraftCategory)EditorGUILayout.EnumPopup("카테고리", c_category);
        c_icon = (Sprite)EditorGUILayout.ObjectField("아이콘", c_icon, typeof(Sprite), false);
        c_resultItem = (ItemData)EditorGUILayout.ObjectField("결과 아이템", c_resultItem, typeof(ItemData), false);
        c_resultAmount = EditorGUILayout.IntField("결과 수량", c_resultAmount);
        c_requiredLevel = EditorGUILayout.IntField("필요 레벨", c_requiredLevel);
        c_requiredGold = EditorGUILayout.IntField("필요 골드", c_requiredGold);
        c_craftTime = EditorGUILayout.FloatField("제작 시간 (초)", c_craftTime);
        c_successRate = EditorGUILayout.Slider("성공 확률 (%)", c_successRate, 0f, 100f);
        c_canFail = EditorGUILayout.Toggle("실패 가능", c_canFail);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── 재료 ──", EditorStyles.boldLabel);
        c_ingredientCount = EditorGUILayout.IntSlider("재료 개수", c_ingredientCount, 1, 5);

        for (int i = 0; i < c_ingredientCount; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"재료 {i + 1}", GUILayout.Width(50));
            c_ingredientItems[i] = (ItemData)EditorGUILayout.ObjectField(c_ingredientItems[i], typeof(ItemData), false);
            c_ingredientAmounts[i] = EditorGUILayout.IntField(c_ingredientAmounts[i], GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(15);
        if (GUILayout.Button("레시피 생성", GUILayout.Height(35)))
            CreateCraftRecipe();
    }

    private void CreateCraftRecipe()
    {
        if (string.IsNullOrEmpty(c_name)) { ShowNotification(new GUIContent("이름을 입력하세요!")); return; }

        EnsureFolder("Assets/Data/Craft");

        var recipe = CreateInstance<CraftRecipe>();
        recipe.recipeID = c_id;
        recipe.recipeName = c_name;
        recipe.recipeDescription = c_description;
        recipe.category = c_category;
        recipe.recipeIcon = c_icon;
        recipe.resultItem = c_resultItem;
        recipe.resultAmount = c_resultAmount;
        recipe.requiredLevel = c_requiredLevel;
        recipe.requiredGold = c_requiredGold;
        recipe.craftTime = c_craftTime;
        recipe.successRate = c_successRate;
        recipe.canFail = c_canFail;

        var ingredients = new List<CraftIngredient>();
        for (int i = 0; i < c_ingredientCount; i++)
        {
            if (c_ingredientItems[i] != null)
            {
                ingredients.Add(new CraftIngredient
                {
                    item = c_ingredientItems[i],
                    requiredAmount = c_ingredientAmounts[i]
                });
            }
        }
        recipe.ingredients = ingredients.ToArray();

        string path = $"Assets/Data/Craft/Recipe_{c_id}_{SanitizeFileName(c_name)}.asset";
        AssetDatabase.CreateAsset(recipe, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(recipe);
        ShowNotification(new GUIContent($"레시피 생성 완료: {c_name}"));
        c_id++;
    }

    // ═══════════════════════════════════════
    //  농장퀘스트 탭
    // ═══════════════════════════════════════

    private void DrawFarmQuestTab()
    {
        EditorGUILayout.LabelField("농장 퀘스트 생성", EditorStyles.boldLabel);
        DrawSeparator();

        f_title = EditorGUILayout.TextField("퀘스트 제목", f_title);
        f_description = EditorGUILayout.TextField("설명", f_description);
        f_icon = (Sprite)EditorGUILayout.ObjectField("아이콘", f_icon, typeof(Sprite), false);
        f_targetCropID = EditorGUILayout.IntField("대상 작물 ID", f_targetCropID);
        f_difficulty = (QuestDifficulty)EditorGUILayout.EnumPopup("난이도", f_difficulty);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── 수량 (레벨 스케일링) ──", EditorStyles.boldLabel);
        f_baseRequired = EditorGUILayout.IntField("기본 필요량", f_baseRequired);
        f_requiredPerTenLvl = EditorGUILayout.IntField("10레벨당 추가", f_requiredPerTenLvl);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── 보상 ──", EditorStyles.boldLabel);
        f_baseCropPoint = EditorGUILayout.IntField("기본 크롭포인트", f_baseCropPoint);
        f_cropPointPerTenLvl = EditorGUILayout.IntField("10레벨당 추가 포인트", f_cropPointPerTenLvl);
        f_goldReward = EditorGUILayout.IntField("골드", f_goldReward);

        EditorGUILayout.Space(15);
        if (GUILayout.Button("농장 퀘스트 생성", GUILayout.Height(35)))
            CreateFarmQuest();
    }

    private void CreateFarmQuest()
    {
        if (string.IsNullOrEmpty(f_title)) { ShowNotification(new GUIContent("제목을 입력하세요!")); return; }

        EnsureFolder("Assets/Data/FarmQuest");

        var quest = CreateInstance<FarmQuestTemplateSO>();
        quest.questTitle = f_title;
        quest.questDescription = f_description;
        quest.questIcon = f_icon;
        quest.targetCropID = f_targetCropID;
        quest.difficulty = f_difficulty;
        quest.baseRequiredAmount = f_baseRequired;
        quest.requiredAmountPerTenLevels = f_requiredPerTenLvl;
        quest.baseCropPointReward = f_baseCropPoint;
        quest.cropPointRewardPerTenLevels = f_cropPointPerTenLvl;
        quest.goldReward = f_goldReward;

        string diffStr = f_difficulty.ToString();
        string path = $"Assets/Data/FarmQuest/FarmQ_{f_targetCropID}_{diffStr}_{SanitizeFileName(f_title)}.asset";
        AssetDatabase.CreateAsset(quest, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(quest);
        ShowNotification(new GUIContent($"농장 퀘스트 생성 완료: {f_title}"));
    }

    // ═══════════════════════════════════════
    //  기존 에셋 목록
    // ═══════════════════════════════════════

    private void DrawExistingAssetList()
    {
        listScrollPos = EditorGUILayout.BeginScrollView(listScrollPos, GUILayout.MaxHeight(300));

        switch (selectedTab)
        {
            case 0: DrawAssetList<QuestData>("퀘스트", a => $"[{a.questID}] {a.questName}"); break;
            case 1: DrawAssetList<AchievementData>("업적", a => $"[{a.achievementID}] {a.achievementName} ({a.grade})"); break;
            case 2: DrawAssetList<CraftRecipe>("조합", a => $"[{a.recipeID}] {a.recipeName} ({a.category})"); break;
            case 3: DrawAssetList<FarmQuestTemplateSO>("농장", a => $"{a.questTitle} (작물:{a.targetCropID}, {a.difficulty})"); break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawAssetList<T>(string label, System.Func<T, string> format) where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        EditorGUILayout.LabelField($"{label} 에셋: {guids.Length}개", EditorStyles.miniLabel);

        if (guids.Length == 0)
        {
            EditorGUILayout.HelpBox("에셋이 없습니다.", MessageType.Info);
            return;
        }

        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null) continue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(format(asset));
            if (GUILayout.Button("선택", GUILayout.Width(50)))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            if (GUILayout.Button("삭제", GUILayout.Width(50)))
            {
                if (EditorUtility.DisplayDialog("삭제 확인", $"{format(asset)}\n정말 삭제하시겠습니까?", "삭제", "취소"))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                    AssetDatabase.SaveAssets();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    // ═══════════════════════════════════════
    //  유틸
    // ═══════════════════════════════════════

    private void DrawSeparator()
    {
        EditorGUILayout.Space(3);
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorGUILayout.Space(3);
    }

    private void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Replace(' ', '_');
        if (name.Length > 30) name = name.Substring(0, 30);
        return name;
    }
}
