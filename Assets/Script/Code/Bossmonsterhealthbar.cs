using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 보스 몬스터 전용 체력바
/// MonsterHealthBar를 상속받아서 보스에 맞게 오버라이드
/// 
/// ★ 사용법:
/// 1. BossMonster 프리팹에 MonsterHealthBar 대신 이 스크립트를 붙임
/// 2. bossUIPrefab에 스크린 UI용 체력바 프리팹 연결 (없으면 머리 위 월드 스페이스로 대체)
/// 
/// ★ bossUIPrefab 구조 (스크린 하단에 표시할 때):
///   BossHealthBarUI (Panel)
///     └─ BossNameText (TextMeshProUGUI)
///     └─ HpSlider (Slider)
///         └─ Fill Area
///             └─ Fill (Image)
/// 
/// ★ 체력바가 안 뜰 때 체크리스트:
///   □ 씬에 Canvas가 하나라도 있는가?
///   □ Canvas의 Render Mode가 Screen Space - Overlay인가?
///   □ bossUIPrefab이 Inspector에 연결되어 있는가?
///   □ Console에 에러 메시지가 없는가?
/// </summary>
public class BossMonsterHealthBar : MonsterHealthBar
{
    [Header("보스 전용 UI 프리팹")]
    [Tooltip("스크린에 표시할 보스 체력바 프리팹 (없으면 머리 위 방식으로 자동 대체)")]
    [SerializeField] private GameObject bossUIPrefab;

    [Header("보스 정보")]
    [Tooltip("보스 이름 앞에 붙는 칭호 (예: '강력한', '고대의')")]
    [SerializeField] private string bossTitle = "강력한";

    // ─── 보스 전용 내부 변수 ───
    private TextMeshProUGUI bossNameText;   // 보스 이름 텍스트
    private Canvas targetCanvas;            // 스크린 UI를 붙일 Canvas
    private RectTransform bossUIRect;       // 스크린 좌표 이동용 RectTransform
    private bool isScreenSpaceUI = false;   // 스크린 UI 방식인지 판별

    // ───────────────────────────────────────────
    /// <summary>
    /// ★ 부모(MonsterHealthBar)의 CreateHealthBar()를 오버라이드
    /// 보스는 스크린 UI로 먼저 시도하고, 안 되면 머리 위 방식으로 대체
    /// </summary>
    protected override void CreateHealthBar()
    {
        // ─── 스크린 UI 방식 시도 ───
        if (bossUIPrefab != null)
        {
            // 씬에서 Canvas 자동 탐색 (FindObjectOfType은 느리지만 Start에서만 호출되므로 OK)
            targetCanvas = FindObjectOfType<Canvas>();

            if (targetCanvas != null)
            {
                // Canvas의 자식으로 보스 체력바 생성 (스크린에 고정됨)
                healthBarInstance = Instantiate(bossUIPrefab, targetCanvas.transform);
                bossUIRect = healthBarInstance.GetComponent<RectTransform>();
                isScreenSpaceUI = true;

                // 슬라이더 연결
                hpSlider = healthBarInstance.GetComponentInChildren<Slider>();

                // 이름 텍스트 연결 및 보스 이름 설정
                bossNameText = healthBarInstance.GetComponentInChildren<TextMeshProUGUI>();
                if (bossNameText != null)
                {
                    // monster는 부모 클래스에서 Start()에 GetComponent로 이미 채워짐
                    string name = monster != null ? monster.monsterName : gameObject.name;
                    bossNameText.text = $"{bossTitle} {name}";
                }

                Debug.Log($"[BossHealthBar] {gameObject.name} - 스크린 UI 방식으로 체력바 생성 완료");
                return; // 스크린 UI 생성 성공 → 여기서 끝
            }
            else
            {
                // Canvas를 못 찾으면 경고 후 머리 위 방식으로 대체
                Debug.LogWarning("[BossHealthBar] Canvas를 찾지 못했습니다. 머리 위(월드 스페이스) 방식으로 대체합니다.\n씬에 Canvas가 있는지 확인해주세요!");
            }
        }
        else
        {
            Debug.Log("[BossHealthBar] bossUIPrefab이 없습니다. 머리 위(월드 스페이스) 방식으로 체력바를 생성합니다.");
        }

        // ─── 폴백: 부모 클래스의 머리 위 방식 사용 ───
        // bossUIPrefab이 없거나 Canvas를 못 찾았을 때 자동으로 머리 위 체력바로 대체
        isScreenSpaceUI = false;
        base.CreateHealthBar(); // 부모(MonsterHealthBar)의 CreateHealthBar 실행
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// ★ 부모(MonsterHealthBar)의 UpdateHealthBarPosition()을 오버라이드
    /// 스크린 UI 방식: 보스 머리 위 월드 좌표 → 스크린 좌표로 변환해서 UI 이동
    /// 월드 스페이스 방식: 부모 함수 그대로 사용
    /// </summary>
    protected override void UpdateHealthBarPosition()
    {
        // 스크린 UI가 아니면 부모 방식(월드 스페이스) 그대로 사용
        if (!isScreenSpaceUI || bossUIRect == null || Camera.main == null)
        {
            base.UpdateHealthBarPosition();
            return;
        }

        // ─── 보스 월드 좌표 → 스크린 픽셀 좌표 변환 ───
        // offset은 부모 클래스의 protected 변수 (머리 위 높이)
        Vector3 worldPos = transform.position + offset;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        // 카메라 뒤에 있으면 (z < 0) 체력바 숨기기
        if (screenPos.z < 0)
        {
            healthBarInstance.SetActive(false);
            return;
        }
        healthBarInstance.SetActive(true);

        // ─── Canvas Render Mode에 따라 위치 설정 방법이 다름 ───
        if (targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Screen Space - Overlay: 스크린 픽셀 좌표를 그대로 사용
            bossUIRect.position = screenPos;
        }
        else
        {
            // Screen Space - Camera 또는 World Space:
            // 스크린 좌표를 캔버스 로컬 좌표로 변환해야 함
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetCanvas.GetComponent<RectTransform>(), // 캔버스 RectTransform 기준
                screenPos,                                   // 변환할 스크린 좌표
                targetCanvas.worldCamera,                    // 캔버스가 사용하는 카메라
                out localPoint                               // 변환된 로컬 좌표 결과
            );
            bossUIRect.localPosition = localPoint;
        }
    }

    // ───────────────────────────────────────────
    /// <summary>
    /// ★ 오브젝트 파괴 시 체력바 정리 (부모 OnDestroy도 같이 호출)
    /// </summary>
    protected override void OnDestroy()
    {
        // 부모의 OnDestroy 호출 (healthBarInstance 제거)
        base.OnDestroy();
    }
}