using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// Assets/Editor/ 폴더에 넣고
/// Tools → Fix Equipment Scripts 클릭
/// </summary>
public class FixEquipmentScripts
{
    [MenuItem("Tools/Fix Equipment Scripts (None 수정)")]
    public static void Fix()
    {
        // 1. EquipmentVisualData 스크립트의 GUID 찾기
        string targetGuid = FindScriptGuid("EquipmentVisualData");
        if (string.IsNullOrEmpty(targetGuid))
        {
            targetGuid = FindScriptGuid("EquipmentData");
            if (string.IsNullOrEmpty(targetGuid))
            {
                EditorUtility.DisplayDialog("오류",
                    "EquipmentVisualData.cs 또는 EquipmentData.cs 스크립트를 찾을 수 없습니다.", "확인");
                return;
            }
            Debug.Log("[Fix] EquipmentVisualData 없음 → EquipmentData 사용");
        }

        Debug.Log($"[Fix] 타겟 Script GUID: {targetGuid}");

        // 2. Equipments 폴더의 .asset 파일 전부 직접 열어서 수정
        string folderPath = "Assets/Resources/Equipments";
        string[] files = Directory.GetFiles(
            Application.dataPath + "/Resources/Equipments", "*.asset");

        int fixedCount = 0;
        int skipCount = 0;

        foreach (string fullPath in files)
        {
            string text = File.ReadAllText(fullPath);

            // m_Script 라인 찾기
            // 예: m_Script: {fileID: 11500000, guid: abc123, type: 3}
            //  또는 m_Script: {fileID: 0}   ← None인 경우
            Match m = Regex.Match(text,
                @"m_Script: \{fileID: (\d+)(?:, guid: (\w+), type: \d+)?\}");

            if (!m.Success)
            {
                skipCount++;
                continue;
            }

            string existingGuid = m.Groups[2].Value;

            // 이미 올바른 GUID면 스킵
            if (existingGuid == targetGuid)
            {
                skipCount++;
                continue;
            }

            // GUID가 없거나 다르면 교체
            string newScriptRef =
                $"m_Script: {{fileID: 11500000, guid: {targetGuid}, type: 3}}";

            string newText = Regex.Replace(text,
                @"m_Script: \{fileID: \d+(?:, guid: \w+, type: \d+)?\}",
                newScriptRef);

            File.WriteAllText(fullPath, newText);
            fixedCount++;

            string fileName = Path.GetFileNameWithoutExtension(fullPath);
            Debug.Log($"[Fix] 수정: {fileName}");
        }

        AssetDatabase.Refresh();

        string result = $"수정: {fixedCount}개\n스킵(이미 정상): {skipCount}개";
        Debug.Log($"[Fix] 완료! {result}");
        EditorUtility.DisplayDialog("완료", result, "확인");
    }

    /// <summary>
    /// 스크립트 이름으로 GUID 찾기
    /// </summary>
    private static string FindScriptGuid(string scriptName)
    {
        string[] guids = AssetDatabase.FindAssets($"{scriptName} t:MonoScript");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null && script.name == scriptName)
            {
                Debug.Log($"[Fix] {scriptName} 발견: {path} (GUID: {guid})");
                return guid;
            }
        }
        return null;
    }
}