using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// AchievementBulkGenerator — 업적 데이터 대량 자동 생성
///
/// Unity 메뉴 > Tools > 업적 대량 생성
///
/// [생성 항목]
///   1. 레벨 달성: 10, 20, 30 ... 200 (20개)
///   2. 장비 강화: 부위별 × 등급별 × 5/10/15/20강 (5부위 × 4등급 × 4단계 = 80개)
///   3. 장비 레벨업: 10, 20, 30 ... 100 (10개)
///   4. 동료 승성: 직업별 × 1~12성 (4직업 × 12성 = 48개)
///   5. 동료 레벨업: 10, 20, 30 ... 100 (10개)
///
/// 총 약 168개 업적 자동 생성
/// </summary>
public class AchievementBulkGenerator : EditorWindow
{
    private bool genLevel = true;
    private bool genEnhance = true;
    private bool genEquipLevel = true;
    private bool genCompanionAscend = true;
    private bool genCompanionLevel = true;

    private int levelMax = 200;
    private int levelStep = 10;
    private int enhanceMax = 20;
    private int equipLevelMax = 100;
    private int companionLevelMax = 100;
    private int companionStarMax = 12;

    private bool deleteExisting = false;

    [MenuItem("Tools/업적 대량 생성")]
    public static void ShowWindow()
    {
        GetWindow<AchievementBulkGenerator>("업적 대량 생성").minSize = new Vector2(400, 500);
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("업적 대량 생성 도구", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("체크된 항목의 업적 ScriptableObject를 Assets/Data/Achievement/ 에 자동 생성합니다.", MessageType.Info);

        EditorGUILayout.Space(10);
        genLevel = EditorGUILayout.ToggleLeft($"레벨 달성 ({levelStep}레벨마다, 최대 {levelMax})", genLevel);
        genEnhance = EditorGUILayout.ToggleLeft($"장비 강화 (부위별 × 등급별 × 5/10/15/{enhanceMax}강)", genEnhance);
        genEquipLevel = EditorGUILayout.ToggleLeft($"장비 레벨업 (10레벨마다, 최대 {equipLevelMax})", genEquipLevel);
        genCompanionAscend = EditorGUILayout.ToggleLeft($"동료 승성 (직업별 × 1~{companionStarMax}성)", genCompanionAscend);
        genCompanionLevel = EditorGUILayout.ToggleLeft($"동료 레벨업 (10레벨마다, 최대 {companionLevelMax})", genCompanionLevel);

        EditorGUILayout.Space(10);
        deleteExisting = EditorGUILayout.ToggleLeft("기존 자동생성 업적 삭제 후 재생성", deleteExisting);

        EditorGUILayout.Space(10);
        int estimate = EstimateCount();
        EditorGUILayout.LabelField($"예상 생성 수: {estimate}개", EditorStyles.boldLabel);

        EditorGUILayout.Space(15);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button("업적 생성 시작", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("업적 대량 생성", $"{estimate}개의 업적을 생성합니다.\n계속하시겠습니까?", "생성", "취소"))
                GenerateAll();
        }
        GUI.backgroundColor = Color.white;
    }

    private int EstimateCount()
    {
        int count = 0;
        if (genLevel) count += levelMax / levelStep;
        if (genEnhance) count += 5 * 4 * 4; // 5부위 × 4등급 × 4단계
        if (genEquipLevel) count += equipLevelMax / 10;
        if (genCompanionAscend) count += 4 * companionStarMax; // 4직업 × 성
        if (genCompanionLevel) count += companionLevelMax / 10;
        return count;
    }

    private void GenerateAll()
    {
        string folder = "Assets/Data/Achievement";
        EnsureFolder(folder);

        if (deleteExisting)
        {
            var guids = AssetDatabase.FindAssets("t:AchievementData", new[] { folder });
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.Contains("_Auto_"))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        int id = 10000;
        int created = 0;

        // ═══════════════════════════════════════
        //  1. 레벨 달성
        // ═══════════════════════════════════════
        if (genLevel)
        {
            for (int lvl = levelStep; lvl <= levelMax; lvl += levelStep)
            {
                var grade = GetGradeByTier(lvl, levelMax);
                int goldReward = 1000 * (lvl / 10);
                int gemReward = 5 * (lvl / 10);

                CreateAchievement(folder, id++,
                    $"레벨 {lvl} 달성",
                    $"플레이어 레벨 {lvl}에 도달하세요.",
                    AchievementType.ReachLevel, grade,
                    lvl, "",
                    goldReward, gemReward, 0, "");
                created++;
            }
        }

        // ═══════════════════════════════════════
        //  2. 장비 강화 (부위별 × 등급별 × 단계별)
        // ═══════════════════════════════════════
        if (genEnhance)
        {
            var slots = new (string name, string nameKR, AchievementType type)[]
            {
                ("Helmet",  "투구",     AchievementType.EnhanceHelmet),
                ("Armor",   "갑옷",     AchievementType.EnhanceArmor),
                ("Weapon",  "무기",     AchievementType.EnhanceWeapon),
                ("Gloves",  "장갑",     AchievementType.EnhanceGloves),
                ("Boots",   "신발",     AchievementType.EnhanceBoots),
            };

            var rarities = new (string name, string nameKR)[]
            {
                ("Uncommon", "언커먼"),
                ("Rare",     "레어"),
                ("Epic",     "에픽"),
                ("Legendary","레전드"),
            };

            int[] enhanceLevels = { 5, 10, 15, 20 };

            foreach (var slot in slots)
            {
                foreach (var rarity in rarities)
                {
                    foreach (int enhLvl in enhanceLevels)
                    {
                        int rarityIdx = System.Array.IndexOf(rarities, rarity);
                        var grade = GetEnhanceGrade(rarityIdx, enhLvl);
                        string targetID = $"{rarity.name}_{slot.name}_{enhLvl}";

                        int goldReward = 2000 * (rarityIdx + 1) * (enhLvl / 5);
                        int gemReward = 10 * (rarityIdx + 1) * (enhLvl / 5);

                        CreateAchievement(folder, id++,
                            $"{rarity.nameKR} {slot.nameKR} +{enhLvl} 달성",
                            $"{rarity.nameKR} 등급 {slot.nameKR}을(를) +{enhLvl}까지 강화하세요.",
                            slot.type, grade,
                            enhLvl, targetID,
                            goldReward, gemReward, 0, "");
                        created++;
                    }
                }
            }
        }

        // ═══════════════════════════════════════
        //  3. 장비 레벨업
        // ═══════════════════════════════════════
        if (genEquipLevel)
        {
            for (int lvl = 10; lvl <= equipLevelMax; lvl += 10)
            {
                var grade = GetGradeByTier(lvl, equipLevelMax);
                int goldReward = 1500 * (lvl / 10);
                int gemReward = 5 * (lvl / 10);

                CreateAchievement(folder, id++,
                    $"장비 Lv.{lvl} 달성",
                    $"장비를 레벨 {lvl}까지 올리세요.",
                    AchievementType.EquipLevelUp, grade,
                    lvl, "",
                    goldReward, gemReward, 0, "");
                created++;
            }
        }

        // ═══════════════════════════════════════
        //  4. 동료 승성 (직업별)
        // ═══════════════════════════════════════
        if (genCompanionAscend)
        {
            var classes = new (string id, string nameKR)[]
            {
                ("warrior",  "전사"),
                ("ranger",   "궁수"),
                ("mage",     "마법사"),
                ("healer",   "힐러"),
            };

            for (int star = 1; star <= companionStarMax; star++)
            {
                foreach (var cls in classes)
                {
                    var grade = GetGradeByTier(star, companionStarMax);
                    int goldReward = 3000 * star;
                    int gemReward = 10 * star;

                    CreateAchievement(folder, id++,
                        $"{cls.nameKR} 동료 {star}성 달성",
                        $"{cls.nameKR} 동료를 {star}성까지 승성하세요.",
                        AchievementType.CompanionAscend, grade,
                        star, cls.id,
                        goldReward, gemReward, 0, "");
                    created++;
                }
            }
        }

        // ═══════════════════════════════════════
        //  5. 동료 레벨업
        // ═══════════════════════════════════════
        if (genCompanionLevel)
        {
            for (int lvl = 10; lvl <= companionLevelMax; lvl += 10)
            {
                var grade = GetGradeByTier(lvl, companionLevelMax);
                int goldReward = 2000 * (lvl / 10);
                int gemReward = 8 * (lvl / 10);

                CreateAchievement(folder, id++,
                    $"동료 Lv.{lvl} 달성",
                    $"동료의 레벨을 {lvl}까지 올리세요.",
                    AchievementType.CompanionLevelUp, grade,
                    lvl, "",
                    goldReward, gemReward, 0, "");
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("완료", $"{created}개 업적이 생성되었습니다!\n경로: {folder}", "확인");
        Debug.Log($"[AchievementGenerator] {created}개 업적 생성 완료");
    }

    // ═══════════════════════════════════════
    //  헬퍼
    // ═══════════════════════════════════════

    private void CreateAchievement(string folder, int id, string name, string desc,
        AchievementType type, AchievementGrade grade, int targetAmount, string targetID,
        int gold, int gem, int exp, string title)
    {
        var achieve = ScriptableObject.CreateInstance<AchievementData>();
        achieve.achievementID = id;
        achieve.achievementName = name;
        achieve.description = desc;
        achieve.type = type;
        achieve.grade = grade;
        achieve.targetAmount = targetAmount;
        achieve.targetID = targetID;
        achieve.isHidden = false;
        achieve.reward = new AchievementReward
        {
            gold = gold,
            gem = gem,
            exp = exp,
            title = title,
            items = new ItemData[0]
        };

        string safeName = name.Replace(" ", "_").Replace("+", "p");
        foreach (char c in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(c, '_');
        if (safeName.Length > 40) safeName = safeName.Substring(0, 40);

        string path = $"{folder}/Auto_{id}_{safeName}.asset";
        AssetDatabase.CreateAsset(achieve, path);
    }

    private AchievementGrade GetGradeByTier(int current, int max)
    {
        float ratio = (float)current / max;
        if (ratio >= 0.75f) return AchievementGrade.Platinum;
        if (ratio >= 0.50f) return AchievementGrade.Gold;
        if (ratio >= 0.25f) return AchievementGrade.Silver;
        return AchievementGrade.Bronze;
    }

    private AchievementGrade GetEnhanceGrade(int rarityIdx, int enhLevel)
    {
        // 등급 + 강화레벨 조합으로 결정
        int score = rarityIdx * 4 + (enhLevel / 5);
        if (score >= 12) return AchievementGrade.Platinum;
        if (score >= 8) return AchievementGrade.Gold;
        if (score >= 4) return AchievementGrade.Silver;
        return AchievementGrade.Bronze;
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
