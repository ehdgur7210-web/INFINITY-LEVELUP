using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 투사체 프리팹 버라이언트를 독립 프리팹으로 변환하는 에디터 도구
///
/// 사용법: 상단 메뉴 → Tools → 투사체 프리팹 버라이언트 해제
///
/// ★ 원본 프리팹의 내용을 복사하여 완전히 독립된 프리팹으로 만듭니다.
///    기존 SkillData의 attackEffectPrefab 참조는 그대로 유지됩니다.
/// </summary>
public class UnpackPrefabVariants : Editor
{
    [MenuItem("Tools/투사체 프리팹 버라이언트 해제")]
    static void Unpack()
    {
        string[] folders = new string[]
        {
            "Assets/Prefabs/Bullet/Common",
            "Assets/Prefabs/Bullet/Uncommon",
            "Assets/Prefabs/Bullet/Rare",
            "Assets/Prefabs/Bullet/Epic",
            "Assets/Prefabs/Bullet/Legend",
        };

        int count = 0;

        foreach (string folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[Unpack] 폴더 없음: {folder}");
                continue;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                // 프리팹 버라이언트인지 확인
                if (!PrefabUtility.IsPartOfVariantPrefab(prefab))
                {
                    Debug.Log($"[Unpack] 이미 독립: {path}");
                    continue;
                }

                // 1. 인스턴스 생성
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null) continue;

                // 2. 프리팹 연결 완전 해제
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                // 3. 기존 프리팹 덮어쓰기 (같은 경로 → 참조 유지)
                PrefabUtility.SaveAsPrefabAsset(instance, path);

                // 4. 인스턴스 정리
                DestroyImmediate(instance);

                count++;
                Debug.Log($"[Unpack] 독립 변환 완료: {path}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "프리팹 버라이언트 해제",
            $"{count}개 프리팹을 독립 프리팹으로 변환했습니다.\n\n이제 각 프리팹의 스프라이트/이펙트를 등급별로 변경하세요.",
            "확인"
        );
    }
}
