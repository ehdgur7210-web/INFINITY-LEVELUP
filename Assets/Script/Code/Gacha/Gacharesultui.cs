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
    public float slotRevealInterval = 0.08f;        // 각 슬롯 사이 딜레이

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

    // ── 내부 상태 ────────────────────────────────────────────────
    private List<GachaResultSlotUI> activeSlots = new List<GachaResultSlotUI>();

    // ════════════════════════════════════════════════════════════
    //  초기화
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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

        // ★ resultPanel이 자기 자신(GachaResultBack)이면 끄지 않음
        //   (GO가 꺼지면 Instance가 null이 되어 다음 호출 시 동작 불가)
        //   resultPanel이 별도 자식이면 정상적으로 숨김
        if (resultPanel != null && resultPanel != gameObject)
            resultPanel.SetActive(false);

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
            resultPanel.SetActive(true);
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
        // Content의 기존 자식 제거
        if (scrollContent != null)
        {
            foreach (Transform child in scrollContent)
                Destroy(child.gameObject);
        }

        // 스크롤을 맨 왼쪽으로 리셋
        ScrollRect sr = GetComponentInChildren<ScrollRect>();
        if (sr != null)
            sr.horizontalNormalizedPosition = 0f;

        // ★ 중복 아이템 그룹핑 (이름+등급 기준)
        var grouped = new List<System.Tuple<EquipmentData, int>>();
        var countMap = new Dictionary<string, int>();
        var dataMap = new Dictionary<string, EquipmentData>();
        var orderList = new List<string>();

        foreach (var data in results)
        {
            if (data == null) continue;
            string key = $"{data.itemName}_{data.rarity}";
            if (countMap.ContainsKey(key))
            {
                countMap[key]++;
            }
            else
            {
                countMap[key] = 1;
                dataMap[key] = data;
                orderList.Add(key);
            }
        }

        foreach (var key in orderList)
            grouped.Add(new System.Tuple<EquipmentData, int>(dataMap[key], countMap[key]));

        if (debugMode)
            Debug.Log($"[GachaResultUI] 그룹핑: {results.Count}개 → {grouped.Count}종류");

        // ★ 대량 뽑기 최적화
        float interval = slotRevealInterval;
        int batchSize = 1;
        if (grouped.Count >= 50) { interval = 0f; batchSize = 10; }
        else if (grouped.Count >= 20) { interval = 0.02f; batchSize = 5; }

        for (int i = 0; i < grouped.Count; i++)
        {
            EquipmentData data = grouped[i].Item1;
            int count = grouped[i].Item2;

            if (slotPrefab == null || scrollContent == null) break;

            GameObject slotGO = Instantiate(slotPrefab, scrollContent);
            slotGO.name = $"Slot_{i}_{data.itemName}";

            GachaResultSlotUI slotUI = slotGO.GetComponent<GachaResultSlotUI>();
            if (slotUI == null)
            {
                slotUI = slotGO.AddComponent<GachaResultSlotUI>();
                AutoBindSlotComponents(slotUI, slotGO);
            }

            slotUI.Setup(data, threshold, 0f, count);
            activeSlots.Add(slotUI);

            // ★ 배치 단위로 대기
            if ((i + 1) % batchSize == 0)
            {
                if (interval > 0f)
                    yield return new WaitForSeconds(interval);
                else
                    yield return null;
            }
        }

        // ★ 대량 뽑기 시 효과음 1회만
        if (results.Count > 10)
            PlaySlotSound(ItemRarity.Rare, threshold);

        if (debugMode)
            Debug.Log($"[GachaResultUI] 슬롯 생성 완료: {activeSlots.Count}개 (원본 {results.Count}개)");
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
        ClearSlots();

        if (resultPanel != null)
            resultPanel.SetActive(false);

        // 결과 확인 후 저장
        SaveLoadManager.Instance?.SaveGame();

        // 인벤토리 강제 갱신 + 자동 열기
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ForceRefreshAll();
            InventoryManager.Instance.OpenInventory();
        }

        // 튜토리얼 트리거
        TutorialManager.Instance?.OnActionCompleted("GachaResultClosed");

        Debug.Log("[GachaResultUI] 패널 닫힘 → SaveGame 완료");
    }

    private void ClearSlots()
    {
        foreach (var slot in activeSlots)
        {
            if (slot != null)
            {
                slot.ClearEffects();
                Destroy(slot.gameObject);
            }
        }
        activeSlots.Clear();

        if (scrollContent != null)
        {
            foreach (Transform child in scrollContent)
                Destroy(child.gameObject);
        }
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