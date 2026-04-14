using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// TMP 폰트 아틀라스 텍스처에 Read/Write 권한 강제 부여
/// Tools > Fix TMP Atlas Read/Write
/// </summary>
public static class TMPAtlasReadWriteFixer
{
    [MenuItem("Tools/Fix TMP Atlas Read/Write")]
    static void Fix()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string fontPath = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font == null) continue;

            Texture2D atlas = font.atlasTexture;
            if (atlas == null) continue;

            string texPath = AssetDatabase.GetAssetPath(atlas);

            // 서브에셋(같은 .asset 파일 안)이면 TextureImporter가 없으므로 별도 처리
            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    Debug.Log($"[TMPFixer] Read/Write 활성화: {texPath}");
                    fixedCount++;
                }
                else
                {
                    Debug.Log($"[TMPFixer] 이미 Read/Write 활성: {texPath}");
                }
            }
            else
            {
                // 서브에셋 — SerializedObject로 직접 isReadable 설정
                SerializedObject so = new SerializedObject(atlas);
                SerializedProperty isReadableProp = so.FindProperty("m_IsReadable");
                if (isReadableProp != null)
                {
                    if (!isReadableProp.boolValue)
                    {
                        isReadableProp.boolValue = true;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(atlas);
                        Debug.Log($"[TMPFixer] 서브에셋 Read/Write 활성화: {font.name} Atlas");
                        fixedCount++;
                    }
                    else
                    {
                        Debug.Log($"[TMPFixer] 서브에셋 이미 Read/Write 활성: {font.name} Atlas");
                    }
                }
                else
                {
                    Debug.LogWarning($"[TMPFixer] m_IsReadable 프로퍼티 없음: {font.name} ({texPath})");
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("TMP Atlas Fixer",
            fixedCount > 0 ? $"{fixedCount}개 폰트 아틀라스 Read/Write 활성화 완료!" : "이미 모두 활성화되어 있습니다.",
            "확인");
    }
}
