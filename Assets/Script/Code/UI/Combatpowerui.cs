using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ══════════════════════════════════════════════════════════════════
///  전투력 UI (CombatPowerUI)
/// ══════════════════════════════════════════════════════════════════
///
///  ▶ 기능:
///    - 총 전투력 숫자 표시 (롤업 애니메이션 포함)
///    - 전투력 등급 텍스트 표시
///    - 세부 전투력 분해 패널 (레벨/스탯/장비/스킬)
///    - 전투력 증가 시 +N 팝업 효과
///
///  ▶ Inspector 설정:
///    - mainPowerText    : 총 전투력 숫자 TMP
///    - gradeText        : 등급 텍스트 TMP
///    - detailPanel      : 세부 패널 (토글 가능)
///    - levelPowerText   : 레벨 기여 TMP
///    - statPowerText    : 스탯 기여 TMP
///    - equipmentPowerText: 장비 기여 TMP
///    - skillPowerText   : 스킬 기여 TMP
///    - gainPopupPrefab  : "+N" 팝업 프리팹 (없으면 자동 생성)
/// ══════════════════════════════════════════════════════════════════
/// </summary>
public class CombatPowerUI : MonoBehaviour
{
    [Header("메인 전투력 표시")]
    public TextMeshProUGUI mainPowerText;       // 총 전투력 숫자
    public TextMeshProUGUI gradeText;           // 등급 텍스트 (신화/전설/영웅 등)
    public Image gradeGlowImage;               // 등급 발광 이미지 (선택)

    [Header("세부 분해 패널")]
    public GameObject detailPanel;             // 세부 패널 (토글 가능)
    public TextMeshProUGUI levelPowerText;     // 레벨 기여 수치
    public TextMeshProUGUI statPowerText;      // 기본 스탯 기여 수치
    public TextMeshProUGUI equipmentPowerText; // 장비 기여 수치
    public TextMeshProUGUI skillPowerText;     // 스킬 기여 수치

    [Header("세부 패널 토글 버튼")]
    public Button detailToggleButton;

    [Header("증가 팝업 효과")]
    public GameObject gainPopupPrefab;         // "+N" 팝업 프리팹 (없으면 자동 생성)
    public Transform popupParent;              // 팝업 부모 (null이면 이 오브젝트)

    [Header("애니메이션 설정")]
    [Tooltip("전투력 롤업 애니메이션 시간 (초)")]
    public float rollupDuration = 0.8f;

    [Tooltip("팝업 유지 시간 (초)")]
    public float popupDuration = 1.5f;

    // ── 내부 상태 ─────────────────────────────────────────────────
    private int displayedPower = 0;
    private Coroutine rollupCoroutine;
    private bool isDetailOpen = false;

    // ═══════════════════════════════════════════════════════════════
    // Unity 생명주기
    // ═══════════════════════════════════════════════════════════════

    void Start()
    {
        // 이벤트 구독
        CombatPowerManager.OnCombatPowerChanged += OnCombatPowerChanged;

        // 세부 패널 초기 닫기
        if (detailPanel != null) detailPanel.SetActive(false);

        // 토글 버튼 설정
        if (detailToggleButton != null)
            detailToggleButton.onClick.AddListener(ToggleDetailPanel);

        // 초기값 표시
        if (CombatPowerManager.Instance != null)
        {
            displayedPower = CombatPowerManager.Instance.TotalCombatPower;
            RefreshUI(displayedPower, displayedPower);
        }
    }

    void OnDestroy()
    {
        CombatPowerManager.OnCombatPowerChanged -= OnCombatPowerChanged;
    }

    // ═══════════════════════════════════════════════════════════════
    // 이벤트 핸들러
    // ═══════════════════════════════════════════════════════════════

    private void OnCombatPowerChanged(int newPower, int oldPower)
    {
        // 롤업 애니메이션
        if (rollupCoroutine != null)
            StopCoroutine(rollupCoroutine);
        rollupCoroutine = StartCoroutine(RollupAnimation(displayedPower, newPower));

        // 증가량 팝업
        int diff = newPower - oldPower;
        if (diff > 0)
        {
            ShowGainPopup(diff);
        }

        // 세부 수치 갱신
        RefreshDetailPanel();
    }

    // ═══════════════════════════════════════════════════════════════
    // UI 갱신
    // ═══════════════════════════════════════════════════════════════

    private void RefreshUI(int current, int target)
    {
        // 메인 전투력 숫자
        if (mainPowerText != null)
            mainPowerText.text = current.ToString("N0");

        // 등급 텍스트
        if (gradeText != null && CombatPowerManager.Instance != null)
            gradeText.text = CombatPowerManager.Instance.GetPowerGrade();

        // 세부 수치
        RefreshDetailPanel();
    }

    private void RefreshDetailPanel()
    {
        if (CombatPowerManager.Instance == null) return;

        var mgr = CombatPowerManager.Instance;

        if (levelPowerText != null)
            levelPowerText.text = $"Lv  {mgr.LevelPower:N0}";

        if (statPowerText != null)
            statPowerText.text = $"StP  {mgr.StatPower:N0}";

        if (equipmentPowerText != null)
            equipmentPowerText.text = $"EP  {mgr.EquipmentPower:N0}";

        if (skillPowerText != null)
            skillPowerText.text = $"Sp  {mgr.SkillPower:N0}";
    }

    // ═══════════════════════════════════════════════════════════════
    // 세부 패널 토글
    // ═══════════════════════════════════════════════════════════════

    public void ToggleDetailPanel()
    {
        if (detailPanel == null) return;

        isDetailOpen = !isDetailOpen;
        detailPanel.SetActive(isDetailOpen);

        if (isDetailOpen)
            RefreshDetailPanel();
    }

    // ═══════════════════════════════════════════════════════════════
    // 롤업 애니메이션
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator RollupAnimation(int from, int to)
    {
        float elapsed = 0f;

        while (elapsed < rollupDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rollupDuration);
            // EaseOut 커브 적용
            t = 1f - Mathf.Pow(1f - t, 3f);

            displayedPower = Mathf.RoundToInt(Mathf.Lerp(from, to, t));

            if (mainPowerText != null)
                mainPowerText.text = displayedPower.ToString("N0");

            yield return null;
        }

        displayedPower = to;
        if (mainPowerText != null)
            mainPowerText.text = to.ToString("N0");

        // 등급 텍스트 최종 갱신
        if (gradeText != null && CombatPowerManager.Instance != null)
            gradeText.text = CombatPowerManager.Instance.GetPowerGrade();
    }

    // ═══════════════════════════════════════════════════════════════
    // 증가 팝업 (+N 효과)
    // ═══════════════════════════════════════════════════════════════

    private void ShowGainPopup(int amount)
    {
        GameObject popup = null;

        if (gainPopupPrefab != null)
        {
            Transform parent = popupParent != null ? popupParent : transform;
            popup = Instantiate(gainPopupPrefab, parent);
        }
        else
        {
            // 프리팹 없으면 자동 생성
            popup = CreateDefaultPopup();
        }

        if (popup == null) return;

        TextMeshProUGUI txt = popup.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
            txt.text = $"+{amount:N0}";

        StartCoroutine(AnimatePopup(popup));
    }

    private GameObject CreateDefaultPopup()
    {
        Transform parent = popupParent != null ? popupParent : transform;

        GameObject go = new GameObject("CombatPowerPopup");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(80f, 10f);
        rt.sizeDelta = new Vector2(120f, 40f);

        TextMeshProUGUI txt = go.AddComponent<TextMeshProUGUI>();
        txt.fontSize = 20;
        txt.fontStyle = FontStyles.Bold;
        txt.color = new Color(1f, 0.85f, 0.1f);
        txt.alignment = TextAlignmentOptions.Center;

        return go;
    }

    private IEnumerator AnimatePopup(GameObject popup)
    {
        if (popup == null) yield break;

        RectTransform rt = popup.GetComponent<RectTransform>();
        TextMeshProUGUI txt = popup.GetComponentInChildren<TextMeshProUGUI>();

        Vector2 startPos = rt != null ? rt.anchoredPosition : Vector2.zero;
        float elapsed = 0f;

        while (elapsed < popupDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popupDuration;

            // 위로 떠오르기
            if (rt != null)
                rt.anchoredPosition = startPos + new Vector2(0f, t * 40f);

            // 페이드 아웃 (후반 50%)
            if (txt != null && t > 0.5f)
            {
                float alpha = 1f - ((t - 0.5f) / 0.5f);
                Color c = txt.color;
                c.a = alpha;
                txt.color = c;
            }

            yield return null;
        }

        Destroy(popup);
    }
}