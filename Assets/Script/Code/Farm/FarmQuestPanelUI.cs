using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FarmQuestPanelUI : MonoBehaviour
{
    [Header("── 패널 ─────────────────────────────")]
    public Button closeButton;

    [Header("── 스크롤뷰 ──────────────────────────")]
    public GameObject questSlotPrefab;
    public Transform questListContent;

    [Header("── 헤더 UI ──────────────────────────")]
    public TextMeshProUGUI cropPointsText;
    public TextMeshProUGUI refreshTimerText;

    [Header("── 난이도 색상 ─────────────────────")]
    public Color colorEasy = new Color(0.5f, 1f, 0.5f);
    public Color colorNormal = Color.white;
    public Color colorHard = new Color(1f, 0.6f, 0.3f);
    public Color colorElite = new Color(1f, 0.3f, 1f);

    private readonly List<GameObject> spawnedSlots = new List<GameObject>();

    void Awake()
    {
        closeButton?.onClick.AddListener(() => { gameObject.SetActive(false); FarmSceneController.Instance?.ResetBanner(); });
    }

    void OnEnable()
    {
        FarmQuestManager.OnQuestsRefreshed += OnQuestsRefreshed;
        FarmQuestManager.OnQuestProgress += OnQuestProgress;
        FarmQuestManager.OnQuestCompleted += OnQuestCompleted;
        FarmManagerExtension.OnCropPointsChanged += OnCropPointsChanged;
        RefreshQuestList();
        StartCoroutine(RefreshTimerCoroutine());
    }

    void OnDisable()
    {
        FarmQuestManager.OnQuestsRefreshed -= OnQuestsRefreshed;
        FarmQuestManager.OnQuestProgress -= OnQuestProgress;
        FarmQuestManager.OnQuestCompleted -= OnQuestCompleted;
        FarmManagerExtension.OnCropPointsChanged -= OnCropPointsChanged;
        StopAllCoroutines();
    }

    public void RefreshQuestList()
    {
        if (FarmQuestManager.Instance == null || questListContent == null) return;

        var quests = FarmQuestManager.Instance.GetActiveQuests();
        int count = quests.Count;

        while (spawnedSlots.Count < count)
        {
            if (questSlotPrefab == null) break;
            spawnedSlots.Add(Instantiate(questSlotPrefab, questListContent));
        }

        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            var go = spawnedSlots[i];
            if (go == null) continue;
            if (i < count) { go.SetActive(true); SetupSlot(go, quests[i], i); }
            else go.SetActive(false);
        }

        long pts = CropPointService.Value;
        if (cropPointsText) cropPointsText.text = $"작물 포인트: {UIManager.FormatKoreanUnit(pts)}";
        LayoutRebuilder.ForceRebuildLayoutImmediate(questListContent as RectTransform);
    }

    // ════════════════════════════════════════════════
    //  CropQuestSlot 프리팹 구조
    //    CropQuestSlot
    //      ├─ Icon              (Image)
    //      ├─ QuestName         (TMP)
    //      ├─ QuestDec          (TMP)
    //      ├─ 슬라이드진행도    (TMP)
    //      ├─ Slider            (Slider)
    //      ├─ 보상
    //      │    ├─ Cp
    //      │    │    └─ Text (TMP) ← 작물포인트 보상
    //      │    └─ Gold
    //      │         └─ Text (TMP) ← 골드 보상
    //      ├─ 진행중 (GO)
    //      │    └─ Text (TMP)
    //      ├─ 보상수령 (GO + Button)
    //      │    └─ Text (TMP)
    //      └─ 완료   (GO)
    //
    //  상태 흐름:
    //    진행중(O) 보상수령(X) 완료(X)  ← 퀘스트 진행 중
    //    진행중(X) 보상수령(O) 완료(X)  ← 목표 달성, 수령 전
    //    진행중(X) 보상수령(X) 완료(O)  ← 수령 완료
    // ════════════════════════════════════════════════
    private void SetupSlot(GameObject go, FarmQuestState quest, int index)
    {
        bool inProgress = !quest.isCompleted;
        bool canClaim = quest.isCompleted && !quest.isSubmitted;
        bool isDone = quest.isSubmitted;

        // ── 아이콘
        var icon = go.transform.Find("Icon")?.GetComponent<Image>();
        if (icon != null && quest.targetCropIcon != null)
            icon.sprite = quest.targetCropIcon;

        // ── 제목
        var titleTmp = go.transform.Find("QuestName")?.GetComponent<TextMeshProUGUI>();
        if (titleTmp) { titleTmp.text = quest.questTitle; titleTmp.color = GetDifficultyColor(quest.difficulty); }

        // ── 설명
        SetTmp(go.transform, "QuestDec", quest.questDescription);

        // ── 진행도 텍스트
        SetTmp(go.transform, "슬라이드진행도", $"{quest.currentAmount} / {quest.requiredAmount}");

        // ── 슬라이더
        var slider = go.transform.Find("Slider")?.GetComponent<Slider>()
                  ?? go.GetComponentInChildren<Slider>();
        if (slider) slider.value = quest.GetProgressRate();

        // ── 보상 텍스트 연동
        //    보상 > Cp > Text(TMP)  → 작물포인트
        //    보상 > Gold > Text(TMP) → 골드
        var rewardRoot = go.transform.Find("보상");
        if (rewardRoot != null)
        {
            SetChildTmp(rewardRoot, "Cp", $"{quest.cropPointReward}");
            SetChildTmp(rewardRoot, "Gold", $"{UIManager.FormatKoreanUnit(quest.goldReward)}");
        }

        // ── 상태별 GO 표시/숨김
        SetActive(go, "진행중", inProgress);
        SetActive(go, "보상수령", canClaim);
        SetActive(go, "완료", isDone);

        // ── 진행중 텍스트
        if (inProgress)
        {
            var t = go.transform.Find("진행중")?.GetComponentInChildren<TextMeshProUGUI>();
            if (t) t.text = $"{quest.currentAmount} / {quest.requiredAmount}";
        }

        // ── 보상수령 버튼
        if (canClaim)
        {
            var claimGo = go.transform.Find("보상수령")?.gameObject;
            if (claimGo != null)
            {
                var btn = claimGo.GetComponent<Button>();
                if (btn == null) btn = claimGo.AddComponent<Button>();
                btn.onClick.RemoveAllListeners();
                int captured = index;
                btn.onClick.AddListener(() =>
                {
                    FarmQuestManager.Instance?.SubmitQuest(captured);
                    RefreshQuestList();
                });
                var btnTmp = claimGo.GetComponentInChildren<TextMeshProUGUI>();
                if (btnTmp) btnTmp.text = "✅ 보상 수령";
            }
        }
    }

    // ── 유틸: 직접 자식 TMP 세팅
    private void SetTmp(Transform parent, string childName, string text)
    {
        if (parent == null) return;
        var tmp = parent.Find(childName)?.GetComponent<TextMeshProUGUI>();
        if (tmp) tmp.text = text;
    }

    // ── 유틸: 자식 GO 안의 첫 번째 TMP 세팅 (Text 이름 무관)
    private void SetChildTmp(Transform parent, string childName, string text)
    {
        if (parent == null) return;
        var child = parent.Find(childName);
        if (child == null) return;
        var tmp = child.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp) tmp.text = text;
    }

    private void SetActive(GameObject root, string childName, bool active)
    {
        root.transform.Find(childName)?.gameObject.SetActive(active);
    }

    private Color GetDifficultyColor(QuestDifficulty diff) => diff switch
    {
        QuestDifficulty.Easy => colorEasy,
        QuestDifficulty.Normal => colorNormal,
        QuestDifficulty.Hard => colorHard,
        QuestDifficulty.Elite => colorElite,
        _ => colorNormal
    };

    private IEnumerator RefreshTimerCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (refreshTimerText == null || FarmQuestManager.Instance == null) continue;
            float secs = FarmQuestManager.Instance.GetSecondsUntilRefresh();
            int h = (int)(secs / 3600), m = (int)((secs % 3600) / 60), s = (int)(secs % 60);
            refreshTimerText.text = h > 0
                ? $"다음 퀘스트 갱신: {h}시간 {m:D2}분"
                : $"다음 퀘스트 갱신: {m}분 {s:D2}초";
        }
    }

    private void OnQuestsRefreshed() => RefreshQuestList();
    private void OnQuestProgress(int i, int n) => RefreshQuestList();
    private void OnQuestCompleted(FarmQuestState q)
    {
        RefreshQuestList();
        UIManager.Instance?.ShowMessage($"퀘스트 완료! {q.questTitle}", Color.yellow);
    }
    private void OnCropPointsChanged(long pts)
    {
        if (cropPointsText) cropPointsText.text = $"작물 포인트: {UIManager.FormatKoreanUnit(pts)}";
    }
}