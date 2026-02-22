using UnityEngine;

/// <summary>
/// 폭발 이펙트 애니메이션
/// - 빠르게 확대
/// - 페이드 아웃
/// </summary>
public class ExplosionEffect : MonoBehaviour
{
    [SerializeField] private float expandSpeed = 4f;
    [SerializeField] private float maxScale = 3f;
    [SerializeField] private float duration = 0.5f;

    private SpriteRenderer sr;
    private float startTime;
    private Vector3 startScale;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        startTime = Time.time;
        startScale = transform.localScale;
    }

    void Update()
    {
        float elapsed = Time.time - startTime;
        float progress = elapsed / duration;

        if (progress >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // 확대
        float currentScale = Mathf.Lerp(startScale.x, startScale.x * maxScale, progress);
        transform.localScale = Vector3.one * currentScale;

        // 페이드 아웃
        if (sr != null)
        {
            Color c = sr.color;
            c.a = Mathf.Lerp(1f, 0f, progress);
            sr.color = c;
        }
    }
}