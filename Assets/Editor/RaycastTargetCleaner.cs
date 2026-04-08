using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 씬/프리팹의 모든 Image/Text/RawImage 중에서
/// 클릭이 필요 없는 것들의 raycastTarget을 false로 끄는 에디터 도구.
///
/// EventSystem 비용을 크게 줄임 — GraphicRaycaster가 매 프레임 검사하는
/// 대상 수를 최소화하기 위함.
///
/// 사용법: Tools → Optimize → Cleanup Raycast Targets
///
/// 동작:
/// - Image, RawImage, TextMeshProUGUI, Text 컴포넌트를 모두 찾음
/// - 부모/자기 자신/자식에 Selectable(Button, Toggle, InputField, Slider 등)이 있으면 그대로 둠
/// - 없으면 raycastTarget = false 로 끔
/// - 결과를 콘솔에 출력
/// </summary>
public static class RaycastTargetCleaner
{
    [MenuItem("Tools/Optimize/Cleanup Raycast Targets (Active Scene)")]
    public static void CleanupActiveScene()
    {
        int changed = 0;
        int total = 0;

        // Image
        foreach (var img in Object.FindObjectsOfType<Image>(true))
        {
            total++;
            if (!img.raycastTarget) continue;
            if (NeedsRaycast(img.gameObject)) continue;
            Undo.RecordObject(img, "Disable RaycastTarget");
            img.raycastTarget = false;
            EditorUtility.SetDirty(img);
            changed++;
        }

        // RawImage
        foreach (var img in Object.FindObjectsOfType<RawImage>(true))
        {
            total++;
            if (!img.raycastTarget) continue;
            if (NeedsRaycast(img.gameObject)) continue;
            Undo.RecordObject(img, "Disable RaycastTarget");
            img.raycastTarget = false;
            EditorUtility.SetDirty(img);
            changed++;
        }

        // TextMeshProUGUI
        foreach (var txt in Object.FindObjectsOfType<TextMeshProUGUI>(true))
        {
            total++;
            if (!txt.raycastTarget) continue;
            if (NeedsRaycast(txt.gameObject)) continue;
            Undo.RecordObject(txt, "Disable RaycastTarget");
            txt.raycastTarget = false;
            EditorUtility.SetDirty(txt);
            changed++;
        }

        // Legacy Text
        foreach (var txt in Object.FindObjectsOfType<Text>(true))
        {
            total++;
            if (!txt.raycastTarget) continue;
            if (NeedsRaycast(txt.gameObject)) continue;
            Undo.RecordObject(txt, "Disable RaycastTarget");
            txt.raycastTarget = false;
            EditorUtility.SetDirty(txt);
            changed++;
        }

        Debug.Log($"[RaycastTargetCleaner] 완료 — 검사 {total}개 / 변경 {changed}개\n" +
                  $"씬을 저장하세요 (Ctrl+S).");
        EditorUtility.DisplayDialog("RaycastTarget 정리 완료",
            $"검사: {total}개\n변경: {changed}개\n\n씬을 저장하세요 (Ctrl+S).", "OK");
    }

    /// <summary>
    /// 이 GameObject 또는 자식/부모에 raycast가 필요한 컴포넌트가 있는지.
    /// Selectable (Button, Toggle, InputField, Slider, Scrollbar, Dropdown 등),
    /// IPointerXxxHandler, ScrollRect 등이 있으면 raycast 필요.
    /// </summary>
    private static bool NeedsRaycast(GameObject go)
    {
        // 자기 자신
        if (HasInteractiveComponent(go)) return true;

        // 부모 (Button의 자식 Image/Text는 부모 Button 클릭에 필요)
        Transform t = go.transform.parent;
        while (t != null)
        {
            if (HasInteractiveComponent(t.gameObject)) return true;
            t = t.parent;
        }

        return false;
    }

    private static bool HasInteractiveComponent(GameObject go)
    {
        if (go.GetComponent<Selectable>() != null) return true;
        if (go.GetComponent<ScrollRect>() != null) return true;
        // EventTrigger 또는 IPointerXxxHandler 구현체
        var handlers = go.GetComponents<MonoBehaviour>();
        foreach (var h in handlers)
        {
            if (h == null) continue;
            if (h is UnityEngine.EventSystems.IPointerClickHandler) return true;
            if (h is UnityEngine.EventSystems.IPointerDownHandler) return true;
            if (h is UnityEngine.EventSystems.IPointerEnterHandler) return true;
            if (h is UnityEngine.EventSystems.IDragHandler) return true;
            if (h is UnityEngine.EventSystems.IBeginDragHandler) return true;
            if (h is UnityEngine.EventSystems.IEndDragHandler) return true;
        }
        return false;
    }
}
