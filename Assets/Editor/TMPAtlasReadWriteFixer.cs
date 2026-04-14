using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// TMP 폰트 아틀라스 텍스처에 Read/Write 권한 강제 부여
/// Tools > Fix TMP Atlas Read/Write
/// Tools > Fix TMP Font → Dynamic Mode (한글 □□□□ 깨짐 수정)
/// </summary>
public static class TMPAtlasReadWriteFixer
{
    /// <summary>
    /// Static 모드로 전환된 폰트를 Dynamic 모드로 되돌린다.
    /// 한글 글리프가 □□□□로 깨질 때 실행.
    /// </summary>
    [MenuItem("Tools/Fix TMP Font → Dynamic Mode")]
    static void SetFontsDynamic()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null) continue;

            SerializedObject so = new SerializedObject(font);

            // m_AtlasPopulationMode: 0 = Static, 1 = Dynamic
            SerializedProperty modeProp = so.FindProperty("m_AtlasPopulationMode");
            if (modeProp != null && modeProp.intValue == 0)
            {
                modeProp.intValue = 1; // Dynamic
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(font);
                Debug.Log($"[TMPFixer] Dynamic 모드 복원: {font.name} ({path})");
                fixedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("TMP Font Mode Fixer",
            fixedCount > 0
                ? $"{fixedCount}개 폰트를 Dynamic 모드로 복원했습니다.\n'Fix TMP Atlas Read/Write'도 함께 실행하세요."
                : "이미 모두 Dynamic 모드입니다.",
            "확인");
    }


    /// <summary>
    /// 씬 내 achievementPanel 하위 TMP 텍스트들이 어떤 폰트를 쓰는지 콘솔에 출력
    /// Tools > Log Achievement Panel Fonts
    /// </summary>
    [MenuItem("Tools/Log Achievement Panel Fonts")]
    static void LogAchievementFonts()
    {
        TextMeshProUGUI[] allTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        int count = 0;
        foreach (var tmp in allTexts)
        {
            if (tmp.gameObject.scene.name == null) continue;
            // achievementPanel 하위인지 확인 (부모 체인에서 검색)
            Transform t = tmp.transform;
            bool inAchieve = false;
            while (t != null)
            {
                if (t.name.ToLower().Contains("achievement"))
                {
                    inAchieve = true;
                    break;
                }
                t = t.parent;
            }
            if (!inAchieve) continue;

            string fontName = tmp.font != null ? tmp.font.name : "null";
            Debug.Log($"[AchieveFontLog] {tmp.gameObject.name} → 폰트: {fontName}");
            count++;
        }
        Debug.Log($"[AchieveFontLog] 총 {count}개 텍스트 로그 완료");
        EditorUtility.DisplayDialog("Font Log", $"{count}개 텍스트의 폰트를 콘솔에 출력했습니다.", "확인");
    }

    /// <summary>
    /// achievementPanel 하위 모든 TMP 텍스트를 NEXON 한글 폰트로 교체
    /// Tools > Fix Achievement Panel Fonts (→ NEXON Korean)
    /// </summary>
    [MenuItem("Tools/Fix Achievement Panel Fonts (→ NEXON Korean)")]
    static void ReplaceAchievementPanelFonts()
    {
        string nexonPath = "Assets/NEXON_Lv1_Gothic/NEXON Lv1 Gothic_OTF_TTF/TTF/NEXONLv1GothicBold SDF.asset";
        TMP_FontAsset koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(nexonPath);
        if (koreanFont == null)
        {
            koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/NotoSansKR-VariableFont_wght SDF.asset");
        }
        if (koreanFont == null)
        {
            EditorUtility.DisplayDialog("오류", "한글 폰트를 찾을 수 없습니다.", "확인");
            return;
        }

        int fixedCount = 0;
        TextMeshProUGUI[] allTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        foreach (var tmp in allTexts)
        {
            if (tmp.gameObject.scene.name == null) continue;
            Transform t = tmp.transform;
            bool inAchieve = false;
            while (t != null)
            {
                if (t.name.ToLower().Contains("achievement"))
                {
                    inAchieve = true;
                    break;
                }
                t = t.parent;
            }
            if (!inAchieve) continue;
            if (tmp.font == koreanFont) continue;

            Undo.RecordObject(tmp, "Replace Achievement Font");
            Debug.Log($"[AchieveFontFix] {tmp.gameObject.name}: {tmp.font?.name} → {koreanFont.name}");
            tmp.font = koreanFont;
            EditorUtility.SetDirty(tmp);
            fixedCount++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        EditorUtility.DisplayDialog("Font Replacer",
            fixedCount > 0
                ? $"{fixedCount}개 텍스트를 [{koreanFont.name}]으로 교체했습니다.\nCtrl+S로 씬 저장하세요."
                : "이미 모두 한글 폰트를 사용 중입니다.",
            "확인");
    }

    /// <summary>
    /// 씬 내 모든 TextMeshProUGUI 중 Kreon RTL 폰트를 사용하는 것을
    /// NEXONLv1GothicBold SDF 로 교체한다.
    /// Tools > Fix Achievement Font (Kreon → NEXON Korean)
    /// </summary>
    [MenuItem("Tools/Fix Achievement Font (Kreon → NEXON Korean)")]
    static void ReplaceKreonWithKoreanFont()
    {
        // ── 교체 대상 폰트 로드 ──
        string nexonPath = "Assets/NEXON_Lv1_Gothic/NEXON Lv1 Gothic_OTF_TTF/TTF/NEXONLv1GothicBold SDF.asset";
        TMP_FontAsset koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(nexonPath);
        if (koreanFont == null)
        {
            // fallback: NotoSansKR
            string notoPath = "Assets/NotoSansKR-VariableFont_wght SDF.asset";
            koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(notoPath);
        }
        if (koreanFont == null)
        {
            EditorUtility.DisplayDialog("오류",
                "한글 폰트를 찾을 수 없습니다.\nNEXONLv1GothicBold SDF 또는 NotoSansKR SDF 파일을 확인하세요.",
                "확인");
            return;
        }

        int fixedCount = 0;
        // 씬 내 모든 TextMeshProUGUI 검색 (비활성 포함)
        TextMeshProUGUI[] allTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        foreach (TextMeshProUGUI tmp in allTexts)
        {
            // 에디터 전용 오브젝트 제외
            if (tmp.gameObject.scene.name == null) continue;

            if (tmp.font != null && tmp.font.name.Contains("Kreon"))
            {
                Undo.RecordObject(tmp, "Replace Kreon Font");
                tmp.font = koreanFont;
                EditorUtility.SetDirty(tmp);
                Debug.Log($"[TMPFixer] 폰트 교체: {tmp.gameObject.name} ({tmp.gameObject.scene.name})");
                fixedCount++;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        EditorUtility.DisplayDialog("Font Replacer",
            fixedCount > 0
                ? $"{fixedCount}개 텍스트의 폰트를 [{koreanFont.name}]으로 교체했습니다.\n씬을 저장(Ctrl+S)하세요."
                : "Kreon RTL 폰트를 사용하는 텍스트가 없습니다.",
            "확인");
    }

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
