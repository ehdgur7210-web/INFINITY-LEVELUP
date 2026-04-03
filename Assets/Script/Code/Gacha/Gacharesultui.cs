using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ══════════════════════════════════════════════════════════════
/// GachaResultUI (가로 스크롤 연출 버전)
///
/// ✅ 연출 방식:
///   - 결과 아이템들이 왼쪽부터 하나씩 순서대로 팝업
///   - ScrollRect의 Content에 슬롯을 생성해 가로로 쭉 나열
///   - 슬롯별 이펙트는 가챠 레벨에 따라 기준 등급이 자동으로 바뀜
///
/// ✅ 레벨별 이펙트 기준 등급 (Inspector에서 설정 가능):
///   - Lv 1~2 : Rare  이상 → 이펙트
///   - Lv 3~4 : Epic  이상 → 이펙트
///   - Lv 5   : Legendary 이상 → 이펙트
///
/// ✅ Inspector 연결 목록 (GachaResultBack에 붙이기):
///   resultPanel      ← GachaResultBack  (자기 자신 or 배경 패널)
///   scrollContent    ← Content  (Viewport > Content)
///   slotPrefab       ← ItemResultSlot 프리팹
///   closeButton      ← X 버튼
///   resultTitleText  ← 장비뽑기결과 TMP (선택)
///
/// ✅ GachaResultSlotUI 설정 (ItemResultSlot 프리팹에 붙이기):
///   iconImage    ← Image
///   itemNameText ← ItemName
///   rarityText   ← 등급
/// ══════════════════════════════════════════════════════════════
/// </summary>
public class GachaResultUI : MonoBehaviour
{
    // ── 싱글톤 ─────────────────────────────────────────────────────
    public static GachaResultUI Instance;

    // ── Inspector 연결 ───────────────────────────────────────────
    [Header("★ 패널 & 스크롤 (반드시 연결!)")]
    public GameObject resultPanel;         // GachaResultBack 전체 패널
    public Transform scrollContent;       // ScrollRect > Viewport > Content
    public GameObject slotPrefab;          // ItemResultSlot 프리팹

    [Header("버튼 & 텍스트")]
    public Button closeButton;         // X 닫기 버튼
    public TextMeshProUGUI resultTitleText;     // 상단 타이틀 TMP (선택)

    [Header("슬롯 등장 타이밍")]
    [Tooltip("슬롯 하나가 등장하는 간격 (초). 작을수록 빠르게 나열됨")]
    public float slotRevealInterval = 0.03f;        // 각 슬롯 사이 딜레이

    [Header("레벨별 이펙트 기준 등급")]
    [Tooltip("가챠 레벨 1~2일 때 이펙트 기준 등급")]
    public ItemRarity effectThresholdLow = ItemRarity.Rare;       // 저레벨: 희귀부터
    [Tooltip("가챠 레벨 3~4일 때 이펙트 기준 등급")]
    public ItemRarity effectThresholdMid = ItemRarity.Epic;       // 중레벨: 영웅부터
    [Tooltip("가챠 레벨 5 이상일 때 이펙트 기준 등급")]
    public ItemRarity effectThresholdHigh = ItemRarity.Legendary;  // 고레벨: 전설만

    [Header("디버그")]
    public bool debugMode = true;

    // ── 로컬 사운드 fallback ─────────────────────────────────────
    private AudioSource audioSource;
    public AudioClip revealSound;

    // ── 슬롯 풀링 ────────────────────────────────────────────────
    [Header("슬롯 풀링")]
    [Tooltip("미리 생성할 슬롯 개수 (100~200 권장)")]
    public int slotPoolSize = 150;

    private List<GameObject> slotPool = new List<GameObject>();
    private int slotPoolUsedCount = 0;
    private bool isPoolInitialized = false;

    // ── 내부 상태 ────────────────────────────────────────────────
    private List<GachaResultSlotUI> activeSlots = new List<GachaResultSlotUI>();
    private ScrollRect cachedScrollRect;
    private int lastRevealedIndex = -1;  // 스크롤 기반 등장에서 마지막으로 등장시킨 인덱스

    // ════════════════════════════════════════════════════════════
    //  초기화
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] GachaResultUI가 생성되었습니다.");
            Debug.Log("[GachaResultUI] Instance 등록");
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);

        // ★ 시작 시 결과 패널 숨기기
        if (resultPanel != null)
        {
            if (resultPanel != gameObject)
            {
                resultPanel.SetActive(false);
            }
            else
            {
                // resultPanel이 자기 자신이면 CanvasGroup으로 숨김 (Instance 유지)
                CanvasGroup cg = resultPanel.GetComponent<CanvasGroup>();
                if (cg == null) cg = resultPanel.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }
        }

        if (scrollContent == null)
            Debug.LogError("[GachaResultUI] scrollContent(Content)가 연결되지 않았습니다!");
        if (slotPrefab == null)
            Debug.LogError("[GachaResultUI] slotPrefab이 연결되지 않았습니다!");
    }

    void OnEnable()
    {
        if (Instance == null) Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ════════════════════════════════════════════════════════════
    //  ★ 슬롯 풀 초기화 (GachaManager.Init에서 호출)
    // ════════════════════════════════════════════════════════════

    public void InitSlotPool()
    {
        if (isPoolInitialized) return;
        if (slotPrefab == null || scrollContent == null)
        {
            Debug.LogError("[GachaResultUI] InitSlotPool 실패: slotPrefab 또는 scrollContent가 null!");
            return;
        }

        for (int i = 0; i < slotPoolSize; i++)
        {
            GameObject go = Instantiate(slotPrefab, scrollContent);
            go.SetActive(false);
            go.name = $"PooledSlot_{i}";

            GachaResultSlotUI slotUI = go.GetComponent<GachaResultSlotUI>();
            if (slotUI == null)
            {
                slotUI = go.AddComponent<GachaResultSlotUI>();
                AutoBindSlotComponents(slotUI, go);
            }

            slotPool.Add(go);
        }

        isPoolInitialized = true;
        Debug.Log($"[GachaResultUI] 슬롯 풀 초기화 완료: {slotPoolSize}개");
    }

    private GameObject GetSlotFromPool()
    {
        if (slotPoolUsedCount < slotPool.Count)
        {
            GameObject go = slotPool[slotPoolUsedCount];
            slotPoolUsedCount++;
            go.SetActive(true);
            return go;
        }

        // 풀 부족 시 동적 생성
        if (slotPrefab != null && scrollContent != null)
        {
            GameObject go = Instantiate(slotPrefab, scrollContent);
            go.name = $"PooledSlot_{slotPool.Count}";

            GachaResultSlotUI slotUI = go.GetComponent<GachaResultSlotUI>();
            if (slotUI == null)
            {
                slotUI = go.AddComponent<GachaResultSlotUI>();
                AutoBindSlotComponents(slotUI, go);
            }

            slotPool.Add(go);
            slotPoolUsedCount++;
            Debug.Log($"[GachaResultUI] 풀 확장: {slotPool.Count}개");
            return go;
        }

        return null;
    }

    private void ReturnAllSlotsToPool()
    {
        for (int i = 0; i < slotPoolUsedCount; i++)
        {
            if (i < slotPool.Count && slotPool[i] != null)
                slotPool[i].SetActive(false);
        }
        slotPoolUsedCount = 0;
    }

    // ════════════════════════════════════════════════════════════
    //  ★ 외부 진입점: GachaManager에서 호출
    // ════════════════════════════════════════════════════════════

    public void ShowResults(List<EquipmentData> results)
    {
        if (results == null || results.Count == 0)
        {
            Debug.LogWarning("[GachaResultUI] ShowResults: 결과 리스트가 비어있습니다.");
            return;
        }

        if (debugMode)
            Debug.Log($"[GachaResultUI] ShowResults 호출 - 총 {results.Count}개 아이템");

        ClearSlots();

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            // CanvasGroup이 있으면 보이게
            CanvasGroup cg = resultPanel.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true; }
        }
        else
            Debug.LogError("[GachaResultUI] resultPanel이 null! Inspector 연결 확인!");

        UpdateTitle(results.Count);

        // 현재 가챠 레벨에 맞는 이펙트 기준 등급 결정
        ItemRarity threshold = GetCurrentEffectThreshold();

        if (debugMode)
            Debug.Log($"[GachaResultUI] 가챠 Lv.{GetCurrentGachaLevel()} | " +
                      $"이펙트 기준: {threshold} 이상");

        StartCoroutine(SpawnSlotsRoutine(results, threshold));
    }

    // ════════════════════════════════════════════════════════════
    //  슬롯 생성 코루틴
    //  왼쪽부터 slotRevealInterval 간격으로 하나씩 나타남
    // ════════════════════════════════════════════════════════════

    private IEnumerator SpawnSlotsRoutine(List<EquipmentData> results, ItemRarity threshold)
    {
        // 풀이 초기화되지 않았으면 지금 초기화
        if (!isPoolInitialized)
            InitSlotPool();

        // 풀 슬롯 전부 반환 (이전 결과 정리)
        ReturnAllSlotsToPool();

        // 스크롤 리셋
        cachedScrollRect = GetComponentInChildren<ScrollRect>();
        if (cachedScrollRect != null)
        {
            cachedScrollRect.horizontalNormalizedPosition = 0f;
            cachedScrollRect.verticalNormalizedPosition = 1f;
            cachedScrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        }

        lastRevealedIndex = -1;

        // ★ 그룹핑 없이 개별 슬롯 생성 (Deferred — 아직 안 보임)
        for (int i = 0; i < results.Count; i++)
        {
            EquipmentData data = results[i];
            if (data == null) continue;

            GameObject slotGO = GetSlotFromPool();
            if (slotGO == null) break;

            slotGO.name = $"Slot_{i}_{data.itemName}";
            slotGO.transform.SetAsLastSibling();

            GachaResultSlotUI slotUI = slotGO.GetComponent<GachaResultSlotUI>();
            slotUI.SetupDeferred(data, threshold);
            activeSlots.Add(slotUI);
        }

        // 레이아웃 갱신 대기 (위치 계산 필요)
        yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent as RectTransform);
        yield return null;

        // ★ 자동 스크롤 + 순차 등장 연출
        yield return StartCoroutine(AutoRevealRoutine());

        // ★ 자동 등장 끝나면 수동 스크롤 시 미등장 슬롯도 등장
        if (cachedScrollRect != null)
            cachedScrollRect.onValueChanged.AddListener(OnScrollValueChanged);

        // 대량 뽑기 시 효과음 1회
        if (results.Count > 10)
            PlaySlotSound(ItemRarity.Rare, threshold);

        if (debugMode)
            Debug.Log($"[GachaResultUI] 슬롯 생성 완료: {activeSlots.Count}개");
    }

    // ════════════════════════════════════════════════════════════
    //  ★ 자동 스크롤 + 순차 등장 연출
    //  슬롯을 하나씩 등장시키며 자동으로 아래로 스크롤
    // ════════════════════════════════════════════════════════════

    [Header("자동 스크롤 연출")]
    [Tooltip("자동 스크롤 속도 (높을수록 빠르게 따라감)")]
    public float autoScrollSpeed = 8f;

    private Coroutine autoRevealCoroutine;

    private IEnumerator AutoRevealRoutine()
    {
        if (activeSlots.Count == 0) yield break;

        RectTransform contentRT = scrollContent as RectTransform;
        if (contentRT == null || cachedScrollRect == null) yield break;

        // ★ 전체 슬롯을 인덱스 순서(위→아래, 왼→오)로 등장
        //    뷰포트 판정 없이 순번대로 강제 등장 + 자동 스크롤
        for (int i = 0; i < activeSlots.Count; i++)
        {
            GachaResultSlotUI slot = activeSlots[i];
            if (slot == null) continue;
            if (slot.IsRevealed) continue;

            // 슬롯 등장 (딜레이 없이 즉시)
            slot.Reveal(0f);

            // 이 슬롯이 보이도록 자동 스크롤
            yield return StartCoroutine(ScrollToSlot(slot.GetComponent<RectTransform>()));

            // 등장 간격
            yield return new WaitForSeconds(slotRevealInterval);
        }
    }

    /// <summary>
    /// 특정 슬롯이 뷰포트 안에 보이도록 부드럽게 스크롤
    /// </summary>
    private IEnumerator ScrollToSlot(RectTransform slotRT)
    {
        if (cachedScrollRect == null || slotRT == null) yield break;

        RectTransform viewportRT = cachedScrollRect.viewport != null
            ? cachedScrollRect.viewport
            : cachedScrollRect.GetComponent<RectTransform>();
        RectTransform contentRT = scrollContent as RectTransform;

        // 슬롯의 Content 기준 로컬 위치
        float slotLocalY = -slotRT.localPosition.y; // Content 아래로 갈수록 양수
        float viewportHeight = viewportRT.rect.height;
        float contentHeight = contentRT.rect.height;
        float scrollableHeight = contentHeight - viewportHeight;

        if (scrollableHeight <= 0f) yield break;

        // 슬롯이 뷰포트 하단에서 약간 여유를 두고 보이게
        float targetScrollY = (slotLocalY - viewportHeight + slotRT.rect.height + 20f) / scrollableHeight;
        targetScrollY = Mathf.Clamp01(targetScrollY);

        // 현재 스크롤 위치 (verticalNormalizedPosition: 1=맨위, 0=맨아래)
        float targetNormalized = 1f - targetScrollY;

        // 이미 보이는 영역이면 스크롤 불필요
        if (targetNormalized >= cachedScrollRect.verticalNormalizedPosition) yield break;

        // 부드러운 스크롤
        float elapsed = 0f;
        float maxTime = 0.15f;
        float startVal = cachedScrollRect.verticalNormalizedPosition;

        while (elapsed < maxTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / maxTime;
            cachedScrollRect.verticalNormalizedPosition = Mathf.Lerp(startVal, targetNormalized, t);
            yield return null;
        }
        cachedScrollRect.verticalNormalizedPosition = targetNormalized;
    }

    // ════════════════════════════════════════════════════════════
    //  스크롤 기반 슬롯 등장 (수동 스크롤 시)
    // ════════════════════════════════════════════════════════════

    private void OnScrollValueChanged(Vector2 _)
    {
        RevealVisibleSlots();
    }

    /// <summary>
    /// 뷰포트 안(+여유 영역)에 있는 미등장 슬롯들을 순차 등장시킴
    /// </summary>
    private void RevealVisibleSlots()
    {
        if (cachedScrollRect == null || activeSlots.Count == 0) return;

        RectTransform viewportRT = cachedScrollRect.viewport != null
            ? cachedScrollRect.viewport
            : cachedScrollRect.GetComponent<RectTransform>();

        // 뷰포트 월드 좌표 경계
        Vector3[] vpCorners = new Vector3[4];
        viewportRT.GetWorldCorners(vpCorners);
        float vpLeft = vpCorners[0].x;
        float vpRight = vpCorners[2].x;
        float vpBottom = vpCorners[0].y;
        float vpTop = vpCorners[2].y;

        // 여유 영역 (뷰포트 크기의 50% 만큼 미리 등장)
        float marginX = (vpRight - vpLeft) * 0.5f;
        float marginY = (vpTop - vpBottom) * 0.5f;

        int revealCount = 0;
        for (int i = 0; i < activeSlots.Count; i++)
        {
            GachaResultSlotUI slot = activeSlots[i];
            if (slot == null || slot.IsRevealed) continue;

            RectTransform slotRT = slot.GetComponent<RectTransform>();
            Vector3[] slotCorners = new Vector3[4];
            slotRT.GetWorldCorners(slotCorners);

            float slotLeft = slotCorners[0].x;
            float slotRight = slotCorners[2].x;
            float slotBottom = slotCorners[0].y;
            float slotTop = slotCorners[2].y;

            // 뷰포트 + 여유 영역과 겹치는지 체크
            bool visible = slotRight >= (vpLeft - marginX) && slotLeft <= (vpRight + marginX)
                        && slotTop >= (vpBottom - marginY) && slotBottom <= (vpTop + marginY);

            if (visible)
            {
                slot.Reveal(revealCount * 0.06f);  // 순차 딜레이
                revealCount++;
            }
        }
    }

    // ════════════════════════════════════════════════════════════
    //  자동 컴포넌트 바인딩
    //  프리팹에 GachaResultSlotUI 컴포넌트가 없을 때
    //  자식 이름(Image, ItemName, 등급)으로 자동 연결
    // ════════════════════════════════════════════════════════════

    private void AutoBindSlotComponents(GachaResultSlotUI slotUI, GameObject slotGO)
    {
        Transform imgT = slotGO.transform.Find("Image");
        Transform nameT = slotGO.transform.Find("ItemName");
        Transform rarT = slotGO.transform.Find("등급");

        if (imgT != null) slotUI.iconImage = imgT.GetComponent<Image>();
        if (nameT != null) slotUI.itemNameText = nameT.GetComponent<TextMeshProUGUI>();
        if (rarT != null) slotUI.rarityText = rarT.GetComponent<TextMeshProUGUI>();

        if (debugMode)
            Debug.Log($"[GachaResultUI] AutoBind → Image:{imgT != null} " +
                      $"ItemName:{nameT != null} 등급:{rarT != null}");
    }

    // ════════════════════════════════════════════════════════════
    //  레벨별 이펙트 기준 등급 결정
    // ════════════════════════════════════════════════════════════

    private ItemRarity GetCurrentEffectThreshold()
    {
        int lv = GetCurrentGachaLevel();
        if (lv >= 5) return effectThresholdHigh; // Lv 5+ : Legendary
        if (lv >= 3) return effectThresholdMid;  // Lv 3~4: Epic
        return effectThresholdLow;                // Lv 1~2: Rare
    }

    private int GetCurrentGachaLevel()
    {
        return GachaManager.Instance != null ? GachaManager.Instance.currentLevel : 1;
    }

    // ════════════════════════════════════════════════════════════
    //  타이틀 업데이트
    // ════════════════════════════════════════════════════════════

    private void UpdateTitle(int count)
    {
        if (resultTitleText == null) return;

        int lv = GetCurrentGachaLevel();
        string tierName = lv >= 5 ? "🧬 DNA 장비" : lv >= 3 ? "🔬 분자 장비" : "⚛️ 원자 장비";

        resultTitleText.text = $"{tierName} 뽑기 결과 ({count}개)";
    }

    // ════════════════════════════════════════════════════════════
    //  효과음
    // ════════════════════════════════════════════════════════════

    private void PlaySlotSound(ItemRarity rarity, ItemRarity threshold)
    {
        string sfxKey = (rarity >= threshold) ? "GachaRare" : "GachaReveal";

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(sfxKey);
            return;
        }

        if (audioSource != null && revealSound != null)
            audioSource.PlayOneShot(revealSound);
    }

    // ════════════════════════════════════════════════════════════
    //  패널 닫기 / 슬롯 정리
    // ════════════════════════════════════════════════════════════

    public void ClosePanel()
    {
        SoundManager.Instance?.PlayButtonClick();
        StopAllCoroutines();

        // 스크롤 리스너 해제
        if (cachedScrollRect != null)
            cachedScrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);

        ClearSlots();

        if (resultPanel != null)
        {
            if (resultPanel != gameObject)
            {
                resultPanel.SetActive(false);
            }
            else
            {
                // CanvasGroup으로 숨김 (Instance 유지)
                CanvasGroup cg = resultPanel.GetComponent<CanvasGroup>();
                if (cg == null) cg = resultPanel.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }
        }

        // 결과 확인 후 저장
        SaveLoadManager.Instance?.SaveGame();

        // 인벤토리 강제 갱신 + 자동 열기
        bool tutorialActive = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;
        if (InventoryManager.Instance != null)
        {
            if (!tutorialActive)
            {
                InventoryManager.Instance.ForceRefreshAll();
                InventoryManager.Instance.OpenInventory();
                Debug.Log("[GachaResultUI] ClosePanel — 인벤토리 갱신+열기 (일반 모드)");
            }
            else
            {
                // ★ 튜토리얼 중: ForceRefreshAll만 (열기/닫기는 건드리지 않음)
                InventoryManager.Instance.ForceRefreshAll();
                Debug.Log("[GachaResultUI] ClosePanel — 튜토리얼 중 인벤토리 갱신만");
            }
        }

        // 튜토리얼 트리거
        TutorialManager.Instance?.OnActionCompleted("GachaResultClosed");

        Debug.Log("[GachaResultUI] 패널 닫힘 → SaveGame 완료");
    }

    /// <summary>N프레임 후 EventSystem 복원 (필요시 사용)</summary>
    private IEnumerator RestoreEventSystemDelayed()
    {
        yield return null;
        yield return null;
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && !es.enabled)
        {
            es.enabled = true;
            Debug.Log("[GachaResultUI] EventSystem 복원");
        }
    }

    private void ClearSlots()
    {
        foreach (var slot in activeSlots)
        {
            if (slot != null)
                slot.ClearEffects();
        }
        activeSlots.Clear();

        // ★ Destroy 대신 풀로 반환
        ReturnAllSlotsToPool();
    }
}

// ════════════════════════════════════════════════════════════════
//  GachaResultSlot - 하위 호환 더미
// ════════════════════════════════════════════════════════════════
public class GachaResultSlot : MonoBehaviour
{
    public Image itemIconImage;
    public Image backgroundImage;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI rarityText;
    public GameObject newBadge;

    public void SetupSlot(EquipmentData equipment)
    {
        if (equipment == null) return;
        if (itemIconImage != null) { itemIconImage.sprite = equipment.itemIcon; itemIconImage.color = Color.white; }
        if (itemNameText != null) itemNameText.text = equipment.itemName;
    }
}