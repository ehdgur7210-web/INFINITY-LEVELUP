using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CompanionInventoryManager
/// ════════════════════════════════════════════════════════════════════════════════════════════════
/// 인벤토리 "동료" 탭에서 보유 동료 표시 및 핫바 등록 관리.
///
/// [기능]
///   - 동료 가챠 결과 시 동료 목록에 추가
///   - 동료 슬롯 클릭 시 CompanionHotbarManager에 등록
///   - 동료 목록 저장/로드
///
/// [Inspector 설정]
///   companionSlotPrefab : CompanionInventorySlot 프리팹
///   companionSlotParent : companionContent 아래 GridLayoutGroup
///   maxCompanionSlots   : 최대 동료 슬롯 수
/// </summary>
public class CompanionInventoryManager : MonoBehaviour
{
    public static CompanionInventoryManager Instance;

    // ★★ 데이터 손실 방지: 로드 완료 플래그 (InventoryManager.IsInventoryLoaded와 동일 패턴)
    public bool IsCompanionLoaded { get; private set; } = false;

    [Header("슬롯 설정")]
    public int maxCompanionSlots = 30;

    // 동료 보유 목록 (중복 동료 허용 - count로 관리)
    [System.Serializable]
    public class CompanionEntry
    {
        public CompanionData data;
        public int count = 1;
        public int level = 1;
        public int exp = 0;
        public int stars = -1;  // -1 = baseStars
        public CompanionSkillLevelEntry[] skillLevels;
    }

    private List<CompanionEntry> companionList = new List<CompanionEntry>();
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        Debug.Log("[ManagerInit] CompanionInventoryManager가 생성되었습니다.");
        Debug.Log($"[CompanionInventory] ★ Awake — Instance 등록 완료 (GO: {gameObject.name})");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    //  동료 추가 (가챠 매니저에서 호출)
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 동료 추가 (중복이면 count 증가)
    /// </summary>
    public void AddCompanion(CompanionData data)
    {
        if (data == null) return;

        // 기존 동료 탐색
        foreach (var entry in companionList)
        {
            if (entry.data == data)
            {
                entry.count++;
                RefreshUI();
                Debug.Log($"[CompanionInventoryManager] {data.companionName} 추가 (count={entry.count})");
                return;
            }
        }

        // 새 슬롯
        if (companionList.Count >= maxCompanionSlots)
        {
            UIManager.Instance?.ShowMessage("동료 인벤토리가 꽉 찼습니다!", Color.yellow);
            return;
        }

        companionList.Add(new CompanionEntry { data = data, count = 1 });
        RefreshUI();
        Debug.Log($"[CompanionInventoryManager] {data.companionName} 신규 추가 완료");
    }

    /// <summary>
    /// 특정 동료 제거 (핫바 해제 후)
    /// </summary>
    public bool RemoveCompanion(CompanionData data, int count = 1)
    {
        for (int i = 0; i < companionList.Count; i++)
        {
            if (companionList[i].data == data)
            {
                companionList[i].count -= count;
                if (companionList[i].count <= 0)
                    companionList.RemoveAt(i);

                RefreshUI();
                return true;
            }
        }
        return false;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    //  슬롯 클릭 시 핫바 등록
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 동료 슬롯 클릭 시 핫바에 등록
    /// </summary>
    public void OnCompanionSlotClicked(int index)
    {
        if (index < 0 || index >= companionList.Count) return;

        CompanionData data = companionList[index].data;
        bool ok = CompanionHotbarManager.Instance?.RegisterCompanion(data) ?? false;

        if (ok)
        {
            UIManager.Instance?.ShowMessage(
                $"{data.companionName} 을 핫바에 등록!\n(핫바 클릭으로 소환)", Color.green);
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    //  UI 갱신
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    private void RefreshUI()
    {
        // UI 슬롯은 InventoryManager 동료탭이 관리
        // CompanionInventoryManager는 데이터만 관리
        Debug.Log($"[CompanionInventory] ★ 데이터 갱신 — companionList: {companionList.Count}명");
    }

    /// <summary>보유 동료 목록 반환 (InventoryManager 동료탭에서 사용)</summary>
    public List<CompanionEntry> GetCompanionList() => companionList;

    // ════════════════════════════════════════════════════════════════════════════════════════════════
    //  저장/로드
    // ════════════════════════════════════════════════════════════════════════════════════════════════

    public CompanionSaveData[] GetSaveData()
    {
        List<CompanionSaveData> result = new List<CompanionSaveData>();
        foreach (var entry in companionList)
        {
            if (entry.data == null) continue;
            result.Add(new CompanionSaveData
            {
                companionID = entry.data.companionID,
                count = entry.count,
                level = entry.level,
                exp = entry.exp,
                stars = entry.stars,
                skillLevels = entry.skillLevels
            });
        }
        return result.ToArray();
    }

    public void LoadSaveData(CompanionSaveData[] saved, List<CompanionData> allCompanions)
    {
        // ★ 빈 데이터로 호출돼도 로드된 것으로 표시 — 기존 상태는 유지
        if (saved == null || allCompanions == null)
        {
            Debug.Log("[CompanionInventoryManager] LoadSaveData: 빈 데이터 → 기존 상태 유지 (IsCompanionLoaded=true)");
            IsCompanionLoaded = true;
            return;
        }

        companionList.Clear();

        foreach (var s in saved)
        {
            CompanionData found = allCompanions.Find(c => c != null && c.companionID == s.companionID);
            if (found == null)
            {
                Debug.LogWarning($"[CompanionInventoryManager] ID '{s.companionID}' 동료를 찾을 수 없음!");
                continue;
            }

            companionList.Add(new CompanionEntry
            {
                data = found,
                count = s.count,
                level = Mathf.Max(1, s.level),
                exp = s.exp,
                stars = s.stars,
                skillLevels = s.skillLevels
            });
        }

        RefreshUI();
        IsCompanionLoaded = true;
        Debug.Log($"[CompanionInventoryManager] 동료 로드 완료 ({companionList.Count}명) (IsCompanionLoaded=true)");
    }

    /// <summary>companionID로 CompanionData SO를 검색합니다.</summary>
    public CompanionData FindCompanionData(string companionID)
    {
        if (string.IsNullOrEmpty(companionID)) return null;
        foreach (var entry in companionList)
        {
            if (entry.data != null && entry.data.companionID == companionID)
                return entry.data;
        }
        return null;
    }

    /// <summary>companionID로 CompanionEntry(레벨/스킬 포함) 검색</summary>
    public CompanionEntry FindCompanionEntry(string companionID)
    {
        if (string.IsNullOrEmpty(companionID)) return null;
        foreach (var entry in companionList)
        {
            if (entry.data != null && entry.data.companionID == companionID)
                return entry;
        }
        return null;
    }
}

// CompanionSaveData → CompanionSaveData.cs