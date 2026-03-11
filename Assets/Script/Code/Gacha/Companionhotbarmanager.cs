using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CompanionHotbarManager
/// ─────────────────────────────────────────────────────────
/// 스킬 핫바 위에 위치한 동료 핫바.
///
/// [기능]
///   - 동료 인벤 슬롯 클릭 → 핫바에 등록
///   - 핫바 슬롯 클릭 → 동료 소환 (월드에 CompanionAgent 스폰)
///   - 소환된 동료는 자동으로 몬스터를 추적/공격
///   - 핫바 슬롯 우클릭 → 소환 해제 및 슬롯 제거
///
/// [Inspector 연결]
///   hotbarSlotPrefab  : CompanionHotbarSlot 프리팹
///   hotbarParent      : 핫바 슬롯 배치할 HorizontalLayoutGroup
///   maxSlots          : 핫바 슬롯 수 (기본 4)
///   companionSpawnAnchor : 소환 기준점 (플레이어 Transform)
/// </summary>
public class CompanionHotbarManager : MonoBehaviour
{
    public static CompanionHotbarManager Instance;

    [Header("핫바 슬롯 설정")]
    public GameObject hotbarSlotPrefab;
    public Transform hotbarParent;
    public int maxSlots = 4;

    [Header("소환 설정")]
    [Tooltip("동료가 소환될 기준 Transform (Player)")]
    public Transform companionSpawnAnchor;

    // 슬롯 데이터
    private List<CompanionHotbarSlotData> slotDataList = new List<CompanionHotbarSlotData>();
    private List<CompanionHotbarSlot> slotUIs = new List<CompanionHotbarSlot>();

    // 소환된 동료 Agent 목록
    private Dictionary<int, CompanionAgent> spawnedAgents = new Dictionary<int, CompanionAgent>();

    [System.Serializable]
    public class CompanionHotbarSlotData
    {
        public CompanionData data;     // null이면 빈 슬롯
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        InitializeSlots();

        // 플레이어 찾기
        if (companionSpawnAnchor == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) companionSpawnAnchor = p.transform;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  슬롯 초기화
    // ─────────────────────────────────────────────────────────

    private void InitializeSlots()
    {
        slotDataList.Clear();
        slotUIs.Clear();

        if (hotbarSlotPrefab == null || hotbarParent == null)
        {
            Debug.LogWarning("[CompanionHotbarManager] 핫바 프리팹 또는 부모가 없습니다!");
            return;
        }

        for (int i = 0; i < maxSlots; i++)
        {
            slotDataList.Add(new CompanionHotbarSlotData());

            GameObject go = Instantiate(hotbarSlotPrefab, hotbarParent);
            var slot = go.GetComponent<CompanionHotbarSlot>() ?? go.AddComponent<CompanionHotbarSlot>();
            slot.Init(this, i);
            slotUIs.Add(slot);
        }

        Debug.Log($"[CompanionHotbarManager] 동료 핫바 초기화 완료 ({maxSlots}슬롯)");
    }

    // ─────────────────────────────────────────────────────────
    //  동료 인벤 → 핫바 등록 (CompanionInventoryManager에서 호출)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 동료를 핫바의 첫 번째 빈 슬롯에 등록
    /// </summary>
    public bool RegisterCompanion(CompanionData data)
    {
        for (int i = 0; i < slotDataList.Count; i++)
        {
            if (slotDataList[i].data == null)
            {
                SetSlot(i, data);
                return true;
            }
        }

        UIManager.Instance?.ShowMessage("동료 핫바가 꽉 찼습니다!", Color.yellow);
        return false;
    }

    /// <summary>
    /// 특정 슬롯에 동료 설정
    /// </summary>
    public void SetSlot(int index, CompanionData data)
    {
        if (index < 0 || index >= slotDataList.Count) return;

        // 기존 소환 해제
        DismissCompanion(index);

        slotDataList[index].data = data;
        slotUIs[index]?.Refresh(data);

        Debug.Log($"[CompanionHotbarManager] 슬롯 {index}에 {data?.companionName} 등록");
    }

    // ─────────────────────────────────────────────────────────
    //  핫바 클릭 → 소환 / 해제
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 핫바 슬롯 클릭 처리
    /// - 동료 없음 → 무시
    /// - 소환 안 됨 → 소환
    /// - 소환 됨 → 해제
    /// </summary>
    public void OnSlotClicked(int index)
    {
        if (index < 0 || index >= slotDataList.Count) return;

        CompanionData cData = slotDataList[index].data;
        if (cData == null) return;

        if (spawnedAgents.ContainsKey(index) && spawnedAgents[index] != null)
        {
            // 이미 소환 중 → 해제
            DismissCompanion(index);
            UIManager.Instance?.ShowMessage($"{cData.companionName} 복귀!", Color.cyan);
        }
        else
        {
            // 소환
            SummonCompanion(index);
        }
    }

    /// <summary>
    /// 우클릭 → 슬롯에서 제거 (소환 해제 + 슬롯 비우기)
    /// </summary>
    public void OnSlotRightClicked(int index)
    {
        if (index < 0 || index >= slotDataList.Count) return;

        DismissCompanion(index);
        slotDataList[index].data = null;
        slotUIs[index]?.Refresh(null);

        Debug.Log($"[CompanionHotbarManager] 슬롯 {index} 제거");
    }

    // ─────────────────────────────────────────────────────────
    //  소환 / 해제
    // ─────────────────────────────────────────────────────────

    private void SummonCompanion(int index)
    {
        CompanionData cData = slotDataList[index].data;
        if (cData == null) return;

        if (cData.worldPrefab == null)
        {
            Debug.LogWarning($"[CompanionHotbarManager] {cData.companionName} worldPrefab이 없습니다!");
            UIManager.Instance?.ShowMessage($"{cData.companionName} 소환 (프리팹 미설정)", Color.yellow);
            return;
        }

        // 소환 위치: 플레이어 주변 랜덤
        Vector3 spawnPos = GetSpawnPosition(cData.spawnRadius);

        GameObject go = Instantiate(cData.worldPrefab, spawnPos, Quaternion.identity);
        CompanionAgent agent = go.GetComponent<CompanionAgent>() ?? go.AddComponent<CompanionAgent>();
        agent.data = cData;

        spawnedAgents[index] = agent;
        slotUIs[index]?.SetActive(true);

        UIManager.Instance?.ShowMessage($"⚔ {cData.companionName} 소환!", Color.green);
        Debug.Log($"[CompanionHotbarManager] {cData.companionName} 소환 완료 @ {spawnPos}");
    }

    private void DismissCompanion(int index)
    {
        if (!spawnedAgents.ContainsKey(index)) return;

        CompanionAgent agent = spawnedAgents[index];
        if (agent != null) agent.Dismiss();

        spawnedAgents.Remove(index);
        slotUIs[index]?.SetActive(false);
    }

    private Vector3 GetSpawnPosition(float radius)
    {
        if (companionSpawnAnchor == null)
        {
            // 플레이어 재탐색
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) companionSpawnAnchor = p.transform;
            else return Vector3.zero;
        }

        float angle = Random.Range(0f, 360f);
        float r = Random.Range(radius * 0.5f, radius);
        return companionSpawnAnchor.position + new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad) * r,
            0f,
            Mathf.Sin(angle * Mathf.Deg2Rad) * r
        );
    }

    // ─────────────────────────────────────────────────────────
    //  씬 전환 시 정리
    // ─────────────────────────────────────────────────────────

    public void DismissAll()
    {
        for (int i = 0; i < maxSlots; i++)
            DismissCompanion(i);
    }
}

// ─────────────────────────────────────────────────────────
//  핫바 슬롯 UI 컴포넌트
// ─────────────────────────────────────────────────────────

/// <summary>
/// 동료 핫바 슬롯 (CompanionHotbarSlotPrefab에 붙이는 컴포넌트)
/// Prefab 구조:
///   - Image (아이콘) → iconImage
///   - Image (테두리) → borderImage
///   - TextMeshProUGUI (이름) → nameText (optional)
///   - Image (소환중 표시) → activeIndicator
/// </summary>
public class CompanionHotbarSlot : MonoBehaviour
{
    [Header("UI 참조")]
    public Image iconImage;
    public Image borderImage;
    public Image activeIndicator;   // 소환 중일 때 강조 표시
    public TextMeshProUGUI nameText;

    [Header("색상")]
    public Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    public Color activeColor = new Color(0f, 1f, 0.5f, 0.8f);
    public Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private CompanionHotbarManager manager;
    private int slotIndex;
    private bool isActive;

    // 등급 색상
    private readonly Color[] rarityColors = new Color[]
    {
        Color.white,                            // Common
        new Color(0.3f, 0.5f, 1f),             // Rare
        new Color(0.7f, 0.2f, 1f),             // Epic
        new Color(1f, 0.8f, 0.1f)              // Legendary
    };

    public void Init(CompanionHotbarManager mgr, int index)
    {
        manager = mgr;
        slotIndex = index;

        // 클릭 이벤트
        Button btn = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        btn.onClick.AddListener(OnClicked);

        // 우클릭 (별도 컴포넌트나 EventTrigger로 구현 가능)
        // 여기서는 간단히 별도 버튼으로 처리 예시만
        Refresh(null);
    }

    public void Refresh(CompanionData data)
    {
        if (data == null)
        {
            if (iconImage != null) { iconImage.sprite = null; iconImage.color = emptyColor; }
            if (borderImage != null) borderImage.color = emptyColor;
            if (nameText != null) nameText.text = "";
            SetActive(false);
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = data.portrait;
            iconImage.color = Color.white;
        }

        if (borderImage != null && (int)data.rarity < rarityColors.Length)
            borderImage.color = rarityColors[(int)data.rarity];

        if (nameText != null) nameText.text = data.companionName;
    }

    public void SetActive(bool active)
    {
        isActive = active;
        if (activeIndicator != null)
            activeIndicator.gameObject.SetActive(active);
        if (borderImage != null)
            borderImage.color = active ? activeColor : inactiveColor;
    }

    private void OnClicked()
    {
        manager?.OnSlotClicked(slotIndex);
    }

    // 우클릭 처리: EventSystem IPointerClickHandler도 가능하지만
    // 간단하게 별도 메서드 제공
    public void OnRightClick()
    {
        manager?.OnSlotRightClicked(slotIndex);
    }
}