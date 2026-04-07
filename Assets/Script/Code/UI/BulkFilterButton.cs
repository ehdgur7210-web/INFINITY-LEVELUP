using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 일괄 강화 탭의 강화수치 필터 버튼
/// 코드가 동적 생성: +0 ~ +20 (총 21개)
/// 클릭 시 BulkEnhancePanel에 알림 → 해당 강화수치 인스턴스만 표시
/// </summary>
public class BulkFilterButton : MonoBehaviour
{
    [Header("UI 참조 (자동 바인딩 가능)")]
    public TextMeshProUGUI enhanceText;     // "+5"
    public TextMeshProUGUI countText;       // "(50)"
    public Image background;                // 색상 변경용
    public Button button;

    [Header("색상")]
    public Color selectedColor = new Color(0.3f, 0.7f, 1f, 1f);  // 선택됨 (파란색)
    public Color hasItemColor = new Color(1f, 0.85f, 0.2f, 1f);  // 보유 (노란색)
    public Color emptyColor = new Color(0.4f, 0.4f, 0.4f, 1f);   // 빈 수치 (회색)

    private int _enhanceLevel;
    private int _count;
    private System.Action<int> _onClickCallback;

    void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (background == null) background = GetComponent<Image>();
        AutoBindTexts();
    }

    /// <summary>자식 TMP 자동 바인딩 (이름 우선, 없으면 순서대로)</summary>
    private void AutoBindTexts()
    {
        if (enhanceText != null && countText != null) return;

        var texts = GetComponentsInChildren<TextMeshProUGUI>(true);

        // 1) 이름 기반 매칭 (가장 신뢰도 높음)
        foreach (var t in texts)
        {
            string n = t.name.ToLower();
            if (enhanceText == null && (n.Contains("enhance") || n.Contains("강화") || n.Contains("level") || n.Contains("lv")))
                enhanceText = t;
            else if (countText == null && (n.Contains("count") || n.Contains("개수") || n.Contains("num")))
                countText = t;
        }

        // 2) 폴백: 자식 순서대로 (위→아래)
        if (enhanceText == null && texts.Length > 0) enhanceText = texts[0];
        if (countText == null && texts.Length > 1)
        {
            foreach (var t in texts)
                if (t != enhanceText) { countText = t; break; }
        }
    }

    /// <summary>
    /// 버튼 초기화
    /// </summary>
    /// <param name="enhanceLevel">표시할 강화 수치 (0~20)</param>
    /// <param name="count">해당 수치 보유 개수</param>
    /// <param name="onClick">클릭 콜백 (강화 수치 전달)</param>
    public void Setup(int enhanceLevel, int count, System.Action<int> onClick)
    {
        _enhanceLevel = enhanceLevel;
        _count = count;
        _onClickCallback = onClick;

        // 강화 표기: 둘 다 있으면 분리, 하나뿐이면 합쳐서 표기
        if (enhanceText != null && countText != null && enhanceText != countText)
        {
            enhanceText.text = $"강화 +{enhanceLevel}";
            countText.text = count > 0 ? $"({count})" : "(0)";
        }
        else if (enhanceText != null)
        {
            // 단일 텍스트 폴백 — "강화 +1\n(119)"
            enhanceText.text = count > 0
                ? $"강화 +{enhanceLevel}\n({count})"
                : $"강화 +{enhanceLevel}\n(0)";
        }

        if (background != null)
            background.color = count > 0 ? hasItemColor : emptyColor;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
            button.interactable = count > 0; // 빈 수치는 클릭 비활성
        }
    }

    /// <summary>외부에서 선택 상태 표시</summary>
    public void SetSelected(bool selected)
    {
        if (background == null) return;
        if (selected)
            background.color = selectedColor;
        else
            background.color = _count > 0 ? hasItemColor : emptyColor;
    }

    private void OnClick()
    {
        SoundManager.Instance?.PlayButtonClick();
        _onClickCallback?.Invoke(_enhanceLevel);
    }
}
