using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 몬스터 머리 위에 표시되는 체력바
/// 
/// ★ 사용법:
/// 1. Canvas(World Space) 프리팹을 만들어서 healthBarPrefab에 연결
/// 2. Monster 프리팹에 이 스크립트를 붙임
/// 3. 자동으로 머리 위에 체력바가 생성됨
/// 
/// ★ 프리팹 구조 (직접 만들어야 함):
///   MonsterHealthBar (Canvas - World Space)
///     └─ Background (Image - 검정 또는 어두운색)
///         └─ Fill (Image - 빨간색/초록색, Image Type = Filled)
/// 
/// ★ 보스 몬스터는 BossMonsterHealthBar를 대신 붙이세요!
/// </summary>
public class MonsterHealthBar : MonoBehaviour
{
    [Header("체력바 프리팹")]
    [Tooltip("월드 스페이스 Canvas로 만든 체력바 프리팹 (없으면 자동 생성됨)")]
    [SerializeField] private GameObject healthBarPrefab;

    [Header("위치 설정")]
    [Tooltip("몬스터 머리 위로 얼마나 올릴지 (Y축 오프셋)")]
    [SerializeField] protected Vector3 offset = new Vector3(0f, 1.5f, 0f);
    // ★ protected: 자식 클래스(BossMonsterHealthBar)에서도 접근 가능

    [Header("체력바 크기")]
    [Tooltip("체력바 전체 스케일 (월드 스페이스 기준)")]
    [SerializeField] private float barScale = 0.01f;

    [Header("색상 설정")]
    [Tooltip("체력이 높을 때 색상 (초록)")]
    [SerializeField] protected Color highHpColor = Color.green;
    [Tooltip("체력이 중간일 때 색상 (노랑)")]
    [SerializeField] protected Color midHpColor = Color.yellow;
    [Tooltip("체력이 낮을 때 색상 (빨강)")]
    [SerializeField] protected Color lowHpColor = Color.red;
    // ★ protected: 자식 클래스에서 색상 접근 가능

    [Header("표시 설정")]
    [Tooltip("true면 항상 표시, false면 피격 시에만 잠깐 표시")]
    [SerializeField] private bool alwaysVisible = true;
    [Tooltip("피격 후 체력바가 보이는 시간 (alwaysVisible=false일 때만 사용)")]
    [SerializeField] private float visibleDuration = 3f;

    // ─── 내부 변수 (protected = 자식 클래스에서 접근 가능) ───
    protected GameObject healthBarInstance; // 생성된 체력바 오브젝트
    protected Slider hpSlider;              // 슬라이더 방식일 때 사용
    protected Image fillImage;              // 이미지 방식일 때 사용
    protected Monster monster;              // 이 스크립트가 붙은 몬스터

    private float visibleTimer = 0f;        // 체력바 표시 남은 시간 (자식 클래스 불필요)
    private Canvas healthBarCanvas;         // 코드 자동생성용 캔버스

    // ───────────────────────────────────────────
    void Start()
    {
        // 같은 오브젝트의 Monster 컴포넌트 가져오기
        monster = GetComponent<Monster>();

        // ★ virtual 함수 호출 → 자식 클래스가 있으면 자식 버전이 실행됨
        CreateHealthBar();
    }

    // ───────────────────────────────────────────
    void Update()
    {
        if (healthBarInstance == null) return;

        // ─── 1. 위치 업데이트 (★ virtual → 보스는 오버라이드 가능) ───
        UpdateHealthBarPosition();

        // ─── 2. 체력 비율 계산 후 체력바 갱신 ───
        if (monster != null)
        {
            float hpRatio = Mathf.Clamp01((float)monster.currentHp / monster.maxHp);
            UpdateBar(hpRatio);
        }

        // ─── 3. 일정 시간 후 체력바 숨기기 (alwaysVisible=false일 때) ───
        if (!alwaysVisible)
        {
            if (visibleTimer > 0f)
            {
                visibleTimer -= Time.deltaTime;
                healthBarInstance.SetActive(true);
            }
            else
            {
                healthBarInstance.SetActive(false);
            }
        }
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// 체력바를 생성하는 함수
    /// ★ virtual: BossMonsterHealthBar에서 오버라이드해서 다른 방식으로 생성 가능
    /// </summary>
    protected virtual void CreateHealthBar()
    {
        if (healthBarPrefab != null)
        {
            // ─── 프리팹이 있으면 프리팹으로 생성 ───
            healthBarInstance = Instantiate(healthBarPrefab, transform.position + offset, Quaternion.identity);
            healthBarInstance.transform.SetParent(transform); // 몬스터 자식으로 → 같이 파괴됨

            // 슬라이더 또는 Fill 이미지 찾기
            hpSlider = healthBarInstance.GetComponentInChildren<Slider>();
            if (hpSlider == null)
            {
                Image[] images = healthBarInstance.GetComponentsInChildren<Image>();
                foreach (Image img in images)
                {
                    if (img.gameObject.name.Contains("Fill") || img.type == Image.Type.Filled)
                    {
                        fillImage = img;
                        break;
                    }
                }
            }
        }
        else
        {
            // ─── 프리팹 없으면 코드로 자동 생성 ───
            CreateDefaultHealthBar();
        }

        if (!alwaysVisible && healthBarInstance != null)
            healthBarInstance.SetActive(false);
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// 체력바 위치를 매 프레임 업데이트하는 함수
    /// ★ virtual: 보스는 스크린 UI 방식으로 오버라이드 가능
    /// </summary>
    protected virtual void UpdateHealthBarPosition()
    {
        // 일반 몬스터: 월드 좌표에서 오프셋만큼 위에 고정
        healthBarInstance.transform.position = transform.position + offset;

        // 카메라를 항상 바라보게 (빌보드 효과) - 3D 게임에서 필요
        if (Camera.main != null)
            healthBarInstance.transform.forward = Camera.main.transform.forward;
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// 프리팹 없이 코드로 기본 체력바를 자동 생성하는 함수
    /// (프리팹을 안 만들어도 체력바가 나옴)
    /// </summary>
    private void CreateDefaultHealthBar()
    {
        // ─── 1. 월드 스페이스 캔버스 생성 ───
        healthBarInstance = new GameObject($"{gameObject.name}_HealthBar");
        healthBarInstance.transform.SetParent(transform);
        healthBarInstance.transform.localPosition = offset;

        Canvas canvas = healthBarInstance.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; // 월드 좌표에 표시
        canvas.sortingOrder = 100;                  // 다른 오브젝트보다 위에 그림
        healthBarCanvas = canvas;

        RectTransform canvasRect = healthBarInstance.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(100f, 15f);  // 가로 100, 세로 15 (픽셀 기준)
        canvasRect.localScale = Vector3.one * barScale; // 월드 스페이스에서 크기 조절

        // ─── 2. 배경 (어두운 바) ───
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(healthBarInstance.transform);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // 반투명 어두운 회색

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bgRect.localScale = Vector3.one;
        bgRect.localPosition = Vector3.zero;

        // ─── 3. 체력 채우기 바 ───
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(healthBarInstance.transform);
        fillImage = fillObj.AddComponent<Image>();
        fillImage.color = highHpColor; // 처음엔 초록색

        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f); // 체력 비율에 따라 anchorMax.x 조절
        fillRect.offsetMin = new Vector2(2f, 2f);  // 테두리 느낌의 패딩
        fillRect.offsetMax = new Vector2(-2f, -2f);
        fillRect.localScale = Vector3.one;
        fillRect.localPosition = Vector3.zero;
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// 체력 비율에 따라 게이지와 색상을 업데이트하는 함수
    /// ★ virtual: 자식 클래스에서 다른 UI 방식으로 오버라이드 가능
    /// </summary>
    /// <param name="hpRatio">체력 비율 (0.0 = 빈 상태, 1.0 = 가득 찬 상태)</param>
    public virtual void UpdateBar(float hpRatio)
    {
        // ─── 슬라이더 방식 ───
        if (hpSlider != null)
        {
            hpSlider.value = hpRatio;
        }
        // ─── 앵커 조절 방식 ───
        else if (fillImage != null)
        {
            RectTransform fillRect = fillImage.GetComponent<RectTransform>();
            if (fillRect != null)
            {
                // 오른쪽 앵커를 체력 비율만큼 줄이면 게이지가 줄어드는 효과
                fillRect.anchorMax = new Vector2(hpRatio, 1f);
            }
        }

        // ─── 체력 비율에 따라 색상 변경 ───
        Color barColor;
        if (hpRatio > 0.5f)
            barColor = Color.Lerp(midHpColor, highHpColor, (hpRatio - 0.5f) * 2f); // 노랑 → 초록
        else
            barColor = Color.Lerp(lowHpColor, midHpColor, hpRatio * 2f);            // 빨강 → 노랑

        if (fillImage != null)
            fillImage.color = barColor;
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// 피격 시 외부에서 호출해서 체력바를 잠깐 보여주는 함수
    /// (alwaysVisible = false일 때 사용)
    /// 예) monster.GetComponent<MonsterHealthBar>().ShowTemporarily();
    /// </summary>
    public void ShowTemporarily()
    {
        visibleTimer = visibleDuration;
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// 오브젝트가 파괴될 때 체력바도 같이 제거
    /// (월드 스페이스 체력바가 몬스터의 자식이 아닐 경우를 위한 안전장치)
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (healthBarInstance != null)
            Destroy(healthBarInstance);
    }
}