using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 동료 가챠 결과 슬롯
///
/// [프리팹 Hierarchy]
///   CompanionResultItem (Button + CompanionResultItem.cs)
///   ├── Portrait (Image) → portaitImage
///   ├── Border (Image) → borderImage
///   ├── BgGlow (Image) → bgGlowImage (선택, 전설 전용 배경 글로우)
///   ├── Name (TextMeshProUGUI) → nameText
///   └── Rarity (TextMeshProUGUI) → rarityText
///
/// [연출]
///   Common/Rare/Epic : Back EaseOut 팝업만
///   Legendary        : 팝업 + 별 파티클 12개 + 배경 글로우 펄스 + 쉐이크 + 광선 6개
/// </summary>
public class CompanionResultItem : MonoBehaviour
{
    [Header("슬롯 UI")]
    public Image portaitImage;
    public Image borderImage;
    public Image bgGlowImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI rarityText;

    [Header("등장 애니메이션")]
    [HideInInspector] public float revealDelay = 0f;
    public float revealDuration = 0.25f;

    private CompanionData data;
    private CompanionGachaManager manager;
    private readonly List<GameObject> spawnedFX = new List<GameObject>();

    private static readonly Color[] rarityColors =
    {
        Color.gray,                         // Common
        new Color(0.3f, 0.5f, 1f),         // Rare
        new Color(0.7f, 0.2f, 1f),         // Epic
        new Color(1f, 0.8f, 0.1f)          // Legendary
    };
    private static readonly string[] rarityNames = { "일반", "희귀", "에픽", "전설" };

    // ═══════════════════════════════════════════════════════════════
    //  초기화
    // ═══════════════════════════════════════════════════════════════

    private int displayCount = 1;

    public void Setup(CompanionData cd, CompanionGachaManager mgr)
    {
        Setup(cd, mgr, 0f, 1);
    }

    public void Setup(CompanionData cd, CompanionGachaManager mgr, float delay)
    {
        Setup(cd, mgr, delay, 1);
    }

    public void Setup(CompanionData cd, CompanionGachaManager mgr, float delay, int count)
    {
        data = cd;
        manager = mgr;
        revealDelay = delay;
        displayCount = count;

        if (portaitImage != null) portaitImage.sprite = cd.portrait;

        int ri = (int)cd.rarity;
        Color rc = ri < rarityColors.Length ? rarityColors[ri] : Color.white;

        if (borderImage != null) borderImage.color = rc;
        if (nameText != null) nameText.text = displayCount > 1 ? $"{cd.companionName} x{displayCount}" : cd.companionName;
        if (rarityText != null)
        {
            rarityText.text = ri < rarityNames.Length ? rarityNames[ri] : "";
            rarityText.color = rc;
        }

        if (bgGlowImage != null)
            bgGlowImage.color = new Color(rc.r, rc.g, rc.b, 0.15f);

        Button btn = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        btn.onClick.AddListener(OnClicked);

        // 반드시 활성화 후 코루틴 시작
        gameObject.SetActive(true);
        transform.localScale = Vector3.zero;
        StartCoroutine(RevealRoutine());
    }

    // ═══════════════════════════════════════════════════════════════
    //  등장 애니메이션 (모든 등급 공통 — Back EaseOut 팝업)
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator RevealRoutine()
    {
        if (revealDelay > 0f)
            yield return new WaitForSeconds(revealDelay);

        // Back EaseOut 스케일 애니메이션
        float elapsed = 0f;
        while (elapsed < revealDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / revealDuration;
            float s = t - 1f;
            float scale = 1f + 1.70158f * s * s * s + s * s;
            transform.localScale = Vector3.one * Mathf.Max(0f, scale);
            yield return null;
        }
        transform.localScale = Vector3.one;

        // 전설 등급만 특수 이펙트
        if (data != null && data.rarity == CompanionRarity.Legendary)
            StartCoroutine(PlayLegendaryEffects());
    }

    // ═══════════════════════════════════════════════════════════════
    //  전설 전용 이펙트 (별 12개 + 글로우 펄스 + 쉐이크 + 광선 6개)
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator PlayLegendaryEffects()
    {
        Color gold = new Color(1f, 0.85f, 0.1f);

        StartCoroutine(GlowPulse(gold));
        SpawnStars(12, gold);
        StartCoroutine(ShakeSlot());
        StartCoroutine(SpawnRays(gold, 6));

        yield return null;
    }

    // ── 배경 글로우 펄스 ──
    private IEnumerator GlowPulse(Color color)
    {
        if (bgGlowImage == null) yield break;

        float duration = 1.2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 0.2f + Mathf.PingPong(elapsed * 3.5f, 1f) * 0.6f;
            bgGlowImage.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }
        bgGlowImage.color = new Color(color.r, color.g, color.b, 0.35f);
    }

    // ── 별 파티클 ──
    private void SpawnStars(int count, Color color)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject star = new GameObject($"Star_{i}");
            star.transform.SetParent(transform, false);
            spawnedFX.Add(star);

            RectTransform rt = star.AddComponent<RectTransform>();
            rt.sizeDelta = Vector2.one * Random.Range(10f, 20f);
            rt.localPosition = Vector3.zero;
            rt.SetAsLastSibling();

            Image img = star.AddComponent<Image>();
            img.color = color;

            float angle = (360f / count) * i + Random.Range(-20f, 20f);
            float distance = Random.Range(45f, 100f);
            Vector3 dir = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad), 0f);

            StartCoroutine(FlyAndFade(rt, img, dir * distance, color));
        }
    }

    private IEnumerator FlyAndFade(RectTransform rt, Image img, Vector3 target, Color baseColor)
    {
        if (rt == null) yield break;

        float duration = Random.Range(0.5f, 0.8f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (rt == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float easeT = 1f - Mathf.Pow(1f - t, 2f);
            rt.localPosition = Vector3.Lerp(Vector3.zero, target, easeT);
            rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 0f, t * t);

            float alpha = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) * 2f);
            img.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            rt.localRotation = Quaternion.Euler(0f, 0f, elapsed * 220f);
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    // ── 슬롯 쉐이크 ──
    private IEnumerator ShakeSlot()
    {
        Vector3 origin = transform.localPosition;
        float duration = 0.4f;
        float elapsed = 0f;
        float mag = 6f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float strength = Mathf.Lerp(mag, 0f, elapsed / duration);
            transform.localPosition = origin + new Vector3(
                Random.Range(-strength, strength),
                Random.Range(-strength, strength), 0f);
            yield return null;
        }
        transform.localPosition = origin;
    }

    // ── 광선 이펙트 ──
    private IEnumerator SpawnRays(Color color, int rayCount)
    {
        List<RectTransform> rays = new List<RectTransform>();

        for (int i = 0; i < rayCount; i++)
        {
            GameObject ray = new GameObject($"Ray_{i}");
            ray.transform.SetParent(transform, false);
            spawnedFX.Add(ray);

            RectTransform rt = ray.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(3f, 70f);
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.Euler(0f, 0f, (360f / rayCount) * i);
            rt.pivot = new Vector2(0.5f, 0f);

            Image img = ray.AddComponent<Image>();
            img.color = new Color(color.r, color.g, color.b, 0f);
            img.raycastTarget = false;
            rays.Add(rt);
        }

        float duration = 1.0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            foreach (var rt in rays)
            {
                if (rt == null) continue;
                float alpha = Mathf.Sin(t * Mathf.PI) * 0.6f;
                rt.localRotation = Quaternion.Euler(0f, 0f,
                    rt.localRotation.eulerAngles.z + Time.deltaTime * 45f);
                var img = rt.GetComponent<Image>();
                if (img != null)
                    img.color = new Color(color.r, color.g, color.b, alpha);
            }
            yield return null;
        }

        foreach (var rt in rays)
            if (rt != null) Destroy(rt.gameObject);
    }

    // ═══════════════════════════════════════════════════════════════
    //  클릭 / 정리
    // ═══════════════════════════════════════════════════════════════

    private void OnClicked() => manager?.ShowDetailPopup(data);

    /// <summary>이 슬롯의 동료 데이터 반환</summary>
    public CompanionData GetData() => data;

    public void ClearEffects()
    {
        foreach (var fx in spawnedFX)
            if (fx != null) Destroy(fx);
        spawnedFX.Clear();
    }

    void OnDestroy() { ClearEffects(); }
}
