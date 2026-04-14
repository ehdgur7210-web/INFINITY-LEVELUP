using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 보스 체력바 프리팹을 자동 생성하는 에디터 도구
/// Unity 메뉴: Tools → 보스 체력바 프리팹 생성
/// </summary>
public class BossHealthBarPrefabBuilder
{
    [MenuItem("Tools/보스 체력바 프리팹 생성")]
    public static void CreateBossHealthBarPrefab()
    {
        // ═══════════════════════════════════════════
        // 1. 루트 Panel (BossBarUI)
        // ═══════════════════════════════════════════
        GameObject root = new GameObject("BossBarUI");
        RectTransform rootRect = root.AddComponent<RectTransform>();

        // 화면 상단 중앙에 고정
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -80f); // 골드 UI 아래에 위치
        rootRect.sizeDelta = new Vector2(600f, 60f);

        // 반투명 배경 패널
        Image panelBg = root.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.6f);

        // 수평 레이아웃
        HorizontalLayoutGroup hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.padding = new RectOffset(10, 10, 5, 5);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // ═══════════════════════════════════════════
        // 2. 보스 아이콘 (BossIcon)
        // ═══════════════════════════════════════════
        GameObject iconObj = new GameObject("BossIcon");
        iconObj.transform.SetParent(root.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(50f, 50f);

        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;

        // ═══════════════════════════════════════════
        // 3. 오른쪽 영역 (이름 + 바)
        // ═══════════════════════════════════════════
        GameObject rightArea = new GameObject("RightArea");
        rightArea.transform.SetParent(root.transform, false);
        RectTransform rightRect = rightArea.AddComponent<RectTransform>();
        rightRect.sizeDelta = new Vector2(520f, 50f);

        VerticalLayoutGroup vlg = rightArea.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2f;
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.childAlignment = TextAnchor.MiddleLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // ─── 3-1. 보스 이름 (BossName) ───
        GameObject nameObj = new GameObject("BossName");
        nameObj.transform.SetParent(rightArea.transform, false);
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(520f, 22f);

        TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = "보스 이름";
        nameText.fontSize = 16f;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = new Color(1f, 0.85f, 0.2f); // 금색
        nameText.alignment = TextAlignmentOptions.Left;
        nameText.enableAutoSizing = false;

        LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
        nameLayout.preferredHeight = 22f;

        // ─── 3-2. 바 영역 (BarBg) ───
        GameObject barBg = new GameObject("BarBg");
        barBg.transform.SetParent(rightArea.transform, false);
        RectTransform barBgRect = barBg.AddComponent<RectTransform>();
        barBgRect.sizeDelta = new Vector2(520f, 24f);

        Image barBgImage = barBg.AddComponent<Image>();
        barBgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        LayoutElement barLayout = barBg.AddComponent<LayoutElement>();
        barLayout.preferredHeight = 24f;

        // ─── 3-2-1. 뒷바 (BarBack) ───
        GameObject barBack = new GameObject("BarBack");
        barBack.transform.SetParent(barBg.transform, false);
        RectTransform barBackRect = barBack.AddComponent<RectTransform>();
        barBackRect.anchorMin = Vector2.zero;
        barBackRect.anchorMax = Vector2.one;
        barBackRect.offsetMin = new Vector2(2f, 2f);
        barBackRect.offsetMax = new Vector2(-2f, -2f);

        Image barBackImage = barBack.AddComponent<Image>();
        barBackImage.type = Image.Type.Filled;
        barBackImage.fillMethod = Image.FillMethod.Horizontal;
        barBackImage.fillAmount = 1f;
        barBackImage.color = new Color(0.2f, 0.5f, 1.0f); // 파랑 (기본)

        // ─── 3-2-2. 앞바 (BarFront) ───
        GameObject barFront = new GameObject("BarFront");
        barFront.transform.SetParent(barBg.transform, false);
        RectTransform barFrontRect = barFront.AddComponent<RectTransform>();
        barFrontRect.anchorMin = Vector2.zero;
        barFrontRect.anchorMax = Vector2.one;
        barFrontRect.offsetMin = new Vector2(2f, 2f);
        barFrontRect.offsetMax = new Vector2(-2f, -2f);

        Image barFrontImage = barFront.AddComponent<Image>();
        barFrontImage.type = Image.Type.Filled;
        barFrontImage.fillMethod = Image.FillMethod.Horizontal;
        barFrontImage.fillAmount = 1f;
        barFrontImage.color = new Color(1.0f, 0.65f, 0.0f); // 주황 (기본)

        // ─── 3-2-3. 바 카운트 텍스트 (BarCount) ───
        // BarCount는 BarBg 밖, RightArea 위에 겹쳐 표시
        GameObject barCountObj = new GameObject("BarCount");
        barCountObj.transform.SetParent(barBg.transform, false);
        RectTransform barCountRect = barCountObj.AddComponent<RectTransform>();
        barCountRect.anchorMin = new Vector2(1f, 0.5f);
        barCountRect.anchorMax = new Vector2(1f, 0.5f);
        barCountRect.pivot = new Vector2(1f, 0.5f);
        barCountRect.anchoredPosition = new Vector2(-8f, 0f);
        barCountRect.sizeDelta = new Vector2(80f, 24f);

        TextMeshProUGUI barCountText = barCountObj.AddComponent<TextMeshProUGUI>();
        barCountText.text = "x10";
        barCountText.fontSize = 14f;
        barCountText.fontStyle = FontStyles.Bold;
        barCountText.color = new Color(1f, 0.65f, 0f);
        barCountText.alignment = TextAlignmentOptions.Right;
        barCountText.enableAutoSizing = false;

        // ═══════════════════════════════════════════
        // 4. 프리팹으로 저장
        // ═══════════════════════════════════════════
        string folderPath = "Assets/Prefabs/UI";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }

        string prefabPath = $"{folderPath}/BossBarUI.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        // 생성 완료 메시지
        Debug.Log($"<color=green>[BossHP] 보스 체력바 프리팹 생성 완료: {prefabPath}</color>");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = prefab;

        EditorUtility.DisplayDialog(
            "보스 체력바 프리팹 생성 완료",
            $"프리팹이 생성되었습니다:\n{prefabPath}\n\n" +
            "이제 Boss 프리팹(Boss~Boss4)의\n" +
            "BossMonsterHealthBar → bossBarPrefab에\n" +
            "이 프리팹을 연결하세요!",
            "확인"
        );
    }
}
