using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// ══════════════════════════════════════════════════════════════
/// GachaResultSlotUI
/// 가챠 결과 1칸(슬롯) 담당 스크립트
///
/// ✅ 역할:
///   - 아이템 아이콘 / 이름 / 등급 표시
///   - 이펙트 기준 등급 이상이면 자동으로 화려한 연출 실행
///   - 슬롯이 왼쪽에서 나타나는 팝업 애니메이션
///
/// ✅ Inspector (ItemResultSlot 프리팹에 붙이기):
///   - iconImage    : 아이콘 Image 컴포넌트 (자식 "Image")
///   - itemNameText : 아이템 이름 TMP (자식 "ItemName")
///   - rarityText   : 등급 텍스트 TMP (자식 "등급")
///   - bgGlowImage  : 배경 글로우 Image (선택, 슬롯 배경)
/// ══════════════════════════════════════════════════════════════
/// </summary>
public class GachaResultSlotUI : MonoBehaviour
{
    // ── Inspector 연결 ────────────────────────────────────────────
    [Header("슬롯 UI 요소")]
    public Image iconImage;      // 아이콘 이미지 (자식 "Image")
    public TextMeshProUGUI itemNameText;   // 아이템 이름 (자식 "ItemName")
    public TextMeshProUGUI rarityText;     // 등급 텍스트 (자식 "등급")
    public Image bgGlowImage;   // 배경 글로우 (선택, 없으면 이펙트만)

    [Header("슬롯 팝업 애니메이션")]
    public float revealDelay = 0f;         // 이 슬롯이 나타나기까지 대기 시간 (외부에서 설정)
    public float revealDuration = 0.25f;      // 팝업 등장 시간 (초)

    // ── 내부 상태 ─────────────────────────────────────────────────
    private EquipmentData equip;         // 이 슬롯에 담긴 장비 데이터
    private bool hasEffect;     // 이펙트 재생 여부
    private List<GameObject> spawnedFX = new List<GameObject>(); // 생성된 이펙트 오브젝트 목록
    private bool isRevealed = false;     // 등장 애니메이션 완료/진행 여부

    /// <summary>이 슬롯이 이미 등장했는지 여부</summary>
    public bool IsRevealed => isRevealed;

    // ════════════════════════════════════════════════════════════
    //  초기화
    // ════════════════════════════════════════════════════════════

    public void Setup(EquipmentData data, ItemRarity effectThreshold, float delay)
    {
        Setup(data, effectThreshold, delay, 1);
    }

    public void Setup(EquipmentData data, ItemRarity effectThreshold, float delay, int count)
    {
        equip = data;
        revealDelay = delay;
        hasEffect = (data.rarity >= effectThreshold);
        isRevealed = false;

        transform.localScale = Vector3.zero;
        ApplyUI(data);
        StartCoroutine(RevealRoutine());
    }

    /// <summary>
    /// 슬롯을 세팅하되 등장 애니메이션은 시작하지 않음 (스크롤 기반 등장용)
    /// </summary>
    public void SetupDeferred(EquipmentData data, ItemRarity effectThreshold)
    {
        equip = data;
        hasEffect = (data.rarity >= effectThreshold);
        isRevealed = false;

        transform.localScale = Vector3.zero;
        ApplyUI(data);
    }

    /// <summary>
    /// 뷰포트에 진입 시 호출 — 등장 애니메이션 시작
    /// </summary>
    public void Reveal(float delay = 0f)
    {
        if (isRevealed) return;
        isRevealed = true;
        revealDelay = delay;
        StartCoroutine(RevealRoutine());
    }

    // ── UI 데이터 반영 ────────────────────────────────────────────

    private void ApplyUI(EquipmentData data)
    {
        // 아이콘
        if (iconImage != null)
        {
            iconImage.sprite = data.itemIcon;
            iconImage.color = Color.white;
        }

        // 아이템 이름
        if (itemNameText != null)
            itemNameText.text = data.itemName;

        // 등급 텍스트 + 색상
        if (rarityText != null)
        {
            rarityText.text = GetRarityLabel(data.rarity);
            rarityText.color = GetRarityColor(data.rarity);
        }

        // 배경 글로우 색상
        if (bgGlowImage != null)
        {
            Color c = GetRarityColor(data.rarity);
            bgGlowImage.color = new Color(c.r, c.g, c.b, 0.35f);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  등장 애니메이션 코루틴
    //  - 딜레이만큼 기다린 뒤 스케일 0 → 1 (Back Ease Out: 탄성 팝업)
    // ════════════════════════════════════════════════════════════

    private IEnumerator RevealRoutine()
    {
        // 딜레이 대기
        if (revealDelay > 0f)
            yield return new WaitForSeconds(revealDelay);

        // Back EaseOut 스케일 애니메이션
        float elapsed = 0f;
        while (elapsed < revealDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / revealDuration;
            float s = t - 1f;
            // Back EaseOut 공식: 탄성 있는 팝업 느낌
            float scale = 1f + 1.70158f * s * s * s + s * s;
            transform.localScale = Vector3.one * Mathf.Max(0f, scale);
            yield return null;
        }
        transform.localScale = Vector3.one;

        // 등장 완료 → 이펙트 등급이면 이펙트 실행
        if (hasEffect)
            StartCoroutine(PlayEffectRoutine(equip.rarity));
    }

    // ════════════════════════════════════════════════════════════
    //  이펙트 코루틴 (등급별 차등 연출)
    //
    //  Rare      : 배경 글로우 펄스 + 별 파티클 4개
    //  Epic      : 배경 글로우 펄스 + 별 파티클 8개 + 슬롯 흔들림
    //  Legendary : 배경 글로우 펄스 + 별 파티클 12개 + 슬롯 흔들림 + 광선 6개
    // ════════════════════════════════════════════════════════════

    private IEnumerator PlayEffectRoutine(ItemRarity rarity)
    {
        Color effectColor;
        int starCount;

        switch (rarity)
        {
            case ItemRarity.Legendary:
                effectColor = new Color(1f, 0.85f, 0.1f);   // 골드
                starCount = 12;
                break;
            case ItemRarity.Epic:
                effectColor = new Color(0.75f, 0.3f, 1f);   // 퍼플
                starCount = 8;
                break;
            default: // Rare
                effectColor = new Color(0.3f, 0.6f, 1f);    // 블루
                starCount = 4;
                break;
        }

        // ── 동시 실행 ──
        StartCoroutine(GlowPulse(effectColor));             // 배경 펄스
        SpawnStars(starCount, effectColor);                  // 별 파티클

        if (rarity >= ItemRarity.Epic)
            StartCoroutine(ShakeSlot());                     // 슬롯 흔들림 (에픽+)

        if (rarity == ItemRarity.Legendary)
            StartCoroutine(SpawnRays(effectColor, 6));       // 광선 (전설만)

        yield return null;
    }

    // ── 배경 글로우 펄스 ──────────────────────────────────────────
    private IEnumerator GlowPulse(Color color)
    {
        if (bgGlowImage == null) yield break;

        float duration = 1.0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // PingPong으로 알파 반복 (빛나는 느낌)
            float alpha = 0.2f + Mathf.PingPong(elapsed * 4f, 1f) * 0.6f;
            bgGlowImage.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        // 이펙트 끝난 후 부드럽게 원래 색으로
        bgGlowImage.color = new Color(color.r, color.g, color.b, 0.35f);
    }

    // ── 별 파티클 생성 ────────────────────────────────────────────
    private void SpawnStars(int count, Color color)
    {
        RectTransform myRT = GetComponent<RectTransform>();
        if (myRT == null) return;

        for (int i = 0; i < count; i++)
        {
            // 별 GameObject 생성 (슬롯과 같은 부모에 배치)
            GameObject star = new GameObject($"Star_{i}");
            star.transform.SetParent(transform, false);
            spawnedFX.Add(star);

            RectTransform rt = star.AddComponent<RectTransform>();
            rt.sizeDelta = Vector2.one * Random.Range(8f, 18f);
            rt.localPosition = Vector3.zero;
            rt.SetAsLastSibling(); // 슬롯 UI 위에 그려지도록

            Image img = star.AddComponent<Image>();
            img.color = color;

            // 원형 방향으로 균등 분산
            float angle = (360f / count) * i + Random.Range(-20f, 20f);
            float distance = Random.Range(40f, 90f);
            Vector3 dir = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad), 0f);

            StartCoroutine(FlyAndFade(rt, img, dir * distance, color));
        }
    }

    // ── 별 날아가기 + 페이드 ─────────────────────────────────────
    private IEnumerator FlyAndFade(RectTransform rt, Image img, Vector3 target, Color baseColor)
    {
        if (rt == null) yield break;

        float duration = Random.Range(0.45f, 0.75f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (rt == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // EaseOut 이동
            float easeT = 1f - Mathf.Pow(1f - t, 2f);
            rt.localPosition = Vector3.Lerp(Vector3.zero, target, easeT);

            // 점점 작아짐
            rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 0f, t * t);

            // 후반부 페이드
            float alpha = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) * 2f);
            img.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            // 자체 회전
            rt.localRotation = Quaternion.Euler(0f, 0f, elapsed * 220f);

            yield return null;
        }

        if (rt != null) Destroy(rt.gameObject);
    }

    // ── 슬롯 흔들림 (에픽+) ───────────────────────────────────────
    private IEnumerator ShakeSlot()
    {
        Vector3 origin = transform.localPosition;
        float duration = 0.35f;
        float elapsed = 0f;
        float mag = 5f; // 흔들림 크기(픽셀)

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 시간이 지날수록 흔들림 감소
            float strength = Mathf.Lerp(mag, 0f, elapsed / duration);
            transform.localPosition = origin + new Vector3(
                Random.Range(-strength, strength),
                Random.Range(-strength, strength), 0f);
            yield return null;
        }
        transform.localPosition = origin;
    }

    // ── 광선 이펙트 (전설) ────────────────────────────────────────
    private IEnumerator SpawnRays(Color color, int rayCount)
    {
        List<RectTransform> rays = new List<RectTransform>();

        for (int i = 0; i < rayCount; i++)
        {
            GameObject ray = new GameObject($"Ray_{i}");
            ray.transform.SetParent(transform, false);
            spawnedFX.Add(ray);

            RectTransform rt = ray.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(3f, 60f);
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.Euler(0f, 0f, (360f / rayCount) * i);
            rt.pivot = new Vector2(0.5f, 0f);

            Image img = ray.AddComponent<Image>();
            img.color = new Color(color.r, color.g, color.b, 0f);
            rays.Add(rt);
        }

        float duration = 0.9f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            foreach (var rt in rays)
            {
                if (rt == null) continue;
                // 중간에 가장 밝고 양 끝에서 페이드
                float alpha = Mathf.Sin(t * Mathf.PI) * 0.65f;
                rt.localRotation = Quaternion.Euler(0f, 0f,
                    rt.localRotation.eulerAngles.z + Time.deltaTime * 40f);
                var img = rt.GetComponent<Image>();
                if (img != null)
                    img.color = new Color(color.r, color.g, color.b, alpha);
            }
            yield return null;
        }

        foreach (var rt in rays)
            if (rt != null) Destroy(rt.gameObject);
    }

    // ════════════════════════════════════════════════════════════
    //  정리 (슬롯 재활용 or 제거 시)
    // ════════════════════════════════════════════════════════════

    public void ClearEffects()
    {
        foreach (var fx in spawnedFX)
            if (fx != null) Destroy(fx);
        spawnedFX.Clear();
    }

    void OnDestroy() { ClearEffects(); }

    // ════════════════════════════════════════════════════════════
    //  유틸
    // ════════════════════════════════════════════════════════════

    private string GetRarityLabel(ItemRarity r)
    {
        switch (r)
        {
            case ItemRarity.Common: return "일반";
            case ItemRarity.Uncommon: return "고급";
            case ItemRarity.Rare: return "✦ 희귀";
            case ItemRarity.Epic: return "✦✦ 영웅";
            case ItemRarity.Legendary: return "✦✦✦ 전설";
            default: return r.ToString();
        }
    }

    private Color GetRarityColor(ItemRarity r)
    {
        switch (r)
        {
            case ItemRarity.Common: return new Color(0.75f, 0.75f, 0.75f); // 회색
            case ItemRarity.Uncommon: return new Color(0.3f, 0.85f, 0.3f);  // 초록
            case ItemRarity.Rare: return new Color(0.3f, 0.6f, 1.0f);  // 파랑
            case ItemRarity.Epic: return new Color(0.75f, 0.3f, 1.0f);  // 보라
            case ItemRarity.Legendary: return new Color(1.0f, 0.8f, 0.1f);  // 골드
            default: return Color.white;
        }
    }
}