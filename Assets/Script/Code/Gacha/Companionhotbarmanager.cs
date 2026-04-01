using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// CompanionHotbarManager
/// ─────────────────────────────────────────────────────────
/// 동료 핫바 (4슬롯, 최대 동시 소환 3마리)
///
/// [기능]
///   - 동료 인벤 → 핫바에 등록
///   - 핫바 슬롯 클릭 → 동료 소환 (시간 제한 없이 계속 활동)
///   - 동료 HP가 0이 되면 사망 → 쿨타임 시작 → 재소환 가능
///   - 최대 동시 소환 3마리 (maxActiveSummons)
///   - 꾹 누르고 밖에 드래그 → 핫바에서 제거
///
/// [Inspector 연결]
///   hotbarSlotPrefab     : CompanionHotbarSlot 프리팹
///   hotbarParent         : HorizontalLayoutGroup
///   maxSlots             : 핫바 슬롯 수 (기본 4)
///   maxActiveSummons     : 동시 소환 최대 수 (기본 3)
///   companionSpawnAnchor : 플레이어 Transform
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

    [Tooltip("동시에 소환 가능한 최대 동료 수")]
    public int maxActiveSummons = 3;

    [Header("기본 쿨타임 (CompanionData에 값이 0이면 이 값 사용)")]
    public float defaultSummonCooldown = 60f;

    [Header("오토 소환")]
    [Tooltip("자동 소환 활성화 (오토 버튼과 연결)")]
    public bool autoSummonEnabled = false;
    [Tooltip("자동 소환 체크 간격 (초)")]
    public float autoSummonInterval = 1f;
    private float autoSummonTimer;

    [Header("오토 버튼 UI")]
    [Tooltip("AUTO 토글 버튼 (Inspector에서 연결하거나 자동 생성)")]
    public Button autoButton;
    [Tooltip("AUTO 버튼 텍스트")]
    public TextMeshProUGUI autoButtonText;
    [SerializeField] private Color autoOnColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    [SerializeField] private Color autoOffColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    // 슬롯 데이터
    private List<CompanionHotbarSlotData> slotDataList = new List<CompanionHotbarSlotData>();
    private List<CompanionHotbarSlot> slotUIs = new List<CompanionHotbarSlot>();

    // 소환된 동료 Agent 목록
    private Dictionary<int, CompanionAgent> spawnedAgents = new Dictionary<int, CompanionAgent>();

    // 슬롯별 쿨타임 타이머
    private float[] cooldownTimers;     // 남은 쿨타임 (0이면 소환 가능)
    private float[] cooldownMaxTimes;   // 총 쿨타임 시간

    [System.Serializable]
    public class CompanionHotbarSlotData
    {
        public CompanionData data;     // null이면 빈 슬롯
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ManagerInit] Companionhotbarmanager가 생성되었습니다.");
        }
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        InitializeSlots();
        FindPlayer();
        SetupAutoButton();
        RefreshAutoButtonUI();
        FixParentRaycast();

        // ★ AUTO ON 상태로 로드된 경우, 씬 초기화 완료 후 자동 소환 시작
        if (autoSummonEnabled)
            StartCoroutine(DelayedAutoSummon());
    }

    /// <summary>씬 로드 후 플레이어 탐색 완료를 기다린 뒤 자동 소환</summary>
    private System.Collections.IEnumerator DelayedAutoSummon()
    {
        // 플레이어가 준비될 때까지 최대 3초 대기
        float waited = 0f;
        while (companionSpawnAnchor == null && waited < 3f)
        {
            FindPlayer();
            yield return new WaitForSeconds(0.5f);
            waited += 0.5f;
        }

        if (companionSpawnAnchor == null) yield break;

        // 한 프레임 더 대기 (UI 초기화 완료)
        yield return null;

        // 소환 가능한 슬롯 순서대로 즉시 소환
        for (int i = 0; i < slotDataList.Count; i++)
        {
            if (GetActiveSummonCount() >= maxActiveSummons) break;
            if (slotDataList[i].data == null) continue;
            if (spawnedAgents.ContainsKey(i) && spawnedAgents[i] != null && !spawnedAgents[i].IsDead) continue;
            if (cooldownTimers[i] > 0f) continue;

            SummonCompanion(i);
        }
    }

    /// <summary>PlayerController 컴포넌트로 플레이어 찾기</summary>
    private void FindPlayer()
    {
        if (companionSpawnAnchor != null) return;

        // PlayerController 우선
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null)
        {
            companionSpawnAnchor = pc.transform;
            Debug.Log($"[CompanionHotbarManager] 플레이어 찾음: {pc.gameObject.name}");
            return;
        }

        // 태그 폴백
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            companionSpawnAnchor = p.transform;
            Debug.LogWarning($"[CompanionHotbarManager] 태그로 플레이어 찾음: {p.name} (PlayerController 없음)");
        }
    }

    void Update()
    {
        if (cooldownTimers == null) return;

        for (int i = 0; i < maxSlots; i++)
        {
            // ── 쿨타임 감소 ──
            if (cooldownTimers[i] > 0f)
            {
                cooldownTimers[i] -= Time.deltaTime;
                if (cooldownTimers[i] <= 0f)
                    cooldownTimers[i] = 0f;

                if (i < slotUIs.Count && slotUIs[i] != null)
                    slotUIs[i].UpdateCooldown(cooldownTimers[i], cooldownMaxTimes[i]);
            }

            // ── 소환된 동료가 외부에서 파괴된 경우 정리 ──
            if (spawnedAgents.ContainsKey(i) && spawnedAgents[i] == null)
            {
                spawnedAgents.Remove(i);
                slotUIs[i]?.SetSummoned(false);
            }
        }

        // ── 오토 소환 ──
        if (autoSummonEnabled)
            TryAutoSummon();
    }

    // ─────────────────────────────────────────────────────────
    //  현재 소환 수
    // ─────────────────────────────────────────────────────────

    /// <summary>현재 활동 중인 동료 수</summary>
    public int GetActiveSummonCount()
    {
        int count = 0;
        foreach (var kvp in spawnedAgents)
        {
            if (kvp.Value != null && !kvp.Value.IsDead)
                count++;
        }
        return count;
    }

    // ─────────────────────────────────────────────────────────
    //  슬롯 초기화
    // ─────────────────────────────────────────────────────────

    private void InitializeSlots()
    {
        slotDataList.Clear();
        slotUIs.Clear();

        cooldownTimers = new float[maxSlots];
        cooldownMaxTimes = new float[maxSlots];

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

        Debug.Log($"[CompanionHotbarManager] 동료 핫바 초기화 완료 ({maxSlots}슬롯, 최대 소환 {maxActiveSummons}마리)");
    }

    // ─────────────────────────────────────────────────────────
    //  동료 인벤 → 핫바 등록
    // ─────────────────────────────────────────────────────────

    public bool RegisterCompanion(CompanionData data)
    {
        // 이미 등록된 동료인지 확인
        for (int i = 0; i < slotDataList.Count; i++)
        {
            if (slotDataList[i].data != null && slotDataList[i].data.companionID == data.companionID)
            {
                UIManager.Instance?.ShowMessage($"{data.companionName}은(는) 이미 핫바에 있습니다!", Color.yellow);
                return false;
            }
        }

        for (int i = 0; i < slotDataList.Count; i++)
        {
            if (slotDataList[i].data == null)
            {
                SetSlot(i, data);
                SaveLoadManager.Instance?.SaveGame();

                // 튜토리얼 트리거
                TutorialManager.Instance?.OnActionCompleted("HotbarRegister");

                return true;
            }
        }

        UIManager.Instance?.ShowMessage("동료 핫바가 꽉 찼습니다!", Color.yellow);
        return false;
    }

    public void SetSlot(int index, CompanionData data)
    {
        if (index < 0 || index >= slotDataList.Count) return;

        DismissCompanion(index);

        slotDataList[index].data = data;
        cooldownTimers[index] = 0f;
        cooldownMaxTimes[index] = 0f;

        slotUIs[index]?.Refresh(data);
        slotUIs[index]?.ResetCooldownUI();

        Debug.Log($"[CompanionHotbarManager] 슬롯 {index}에 {data?.companionName} 등록");
    }

    // ─────────────────────────────────────────────────────────
    //  핫바 클릭 → 소환
    // ─────────────────────────────────────────────────────────

    public void OnSlotClicked(int index)
    {
        if (index < 0 || index >= slotDataList.Count) return;

        CompanionData cData = slotDataList[index].data;
        if (cData == null) return;

        // 이미 소환 중이면 무시
        if (spawnedAgents.ContainsKey(index) && spawnedAgents[index] != null && !spawnedAgents[index].IsDead)
        {
            UIManager.Instance?.ShowMessage($"{cData.companionName} 전투 중!", Color.cyan);
            return;
        }

        // 쿨타임 중
        if (cooldownTimers[index] > 0f)
        {
            UIManager.Instance?.ShowMessage(
                $"{cData.companionName} 쿨타임 {cooldownTimers[index]:F0}초", Color.yellow);
            return;
        }

        // 최대 소환 수 체크
        if (GetActiveSummonCount() >= maxActiveSummons)
        {
            UIManager.Instance?.ShowMessage(
                $"최대 {maxActiveSummons}마리까지 소환 가능!", Color.yellow);
            return;
        }

        SummonCompanion(index);
    }

    /// <summary>핫바 밖으로 드래그 → 슬롯 제거</summary>
    public void RemoveCompanionFromSlot(int index)
    {
        if (index < 0 || index >= slotDataList.Count) return;
        if (slotDataList[index].data == null) return;

        string name = slotDataList[index].data.companionName;

        DismissCompanion(index);
        slotDataList[index].data = null;
        cooldownTimers[index] = 0f;
        slotUIs[index]?.Refresh(null);
        slotUIs[index]?.ResetCooldownUI();

        UIManager.Instance?.ShowMessage($"{name} 핫바에서 제거됨", Color.yellow);
        SaveLoadManager.Instance?.SaveGame();
        Debug.Log($"[CompanionHotbarManager] 슬롯 {index} 드래그로 제거");
    }

    public RectTransform GetHotbarRect()
    {
        return hotbarParent as RectTransform;
    }

    // ─────────────────────────────────────────────────────────
    //  소환 / 사망 처리 / 해제
    // ─────────────────────────────────────────────────────────

    private void SummonCompanion(int index)
    {
        CompanionData cData = slotDataList[index].data;
        if (cData == null) return;

        if (cData.worldPrefab == null)
        {
            Debug.LogWarning($"[CompanionHotbarManager] {cData.companionName} worldPrefab이 없습니다!");
            UIManager.Instance?.ShowMessage($"{cData.companionName} 프리팹 미설정", Color.yellow);
            return;
        }

        // 플레이어 재탐색 (소환 시점에 반드시 최신 참조)
        if (companionSpawnAnchor == null)
            FindPlayer();

        Vector3 spawnPos = GetSpawnPosition(cData.spawnRadius);
        Debug.Log($"[CompanionHotbarManager] 소환 위치: {spawnPos}, 플레이어 위치: {(companionSpawnAnchor != null ? companionSpawnAnchor.position.ToString() : "NULL")}");

        GameObject go = Instantiate(cData.worldPrefab, spawnPos, Quaternion.identity);

        // ★ Instantiate 후 위치 강제 재설정 (프리팹 내부 좌표 오버라이드)
        go.transform.position = spawnPos;

        CompanionAgent agent = go.GetComponent<CompanionAgent>() ?? go.AddComponent<CompanionAgent>();
        agent.data = cData;

        // 동료 레벨 주입 (스킬 해금 판단용)
        if (CompanionInventoryManager.Instance != null)
        {
            var entry = CompanionInventoryManager.Instance.FindCompanionEntry(cData.companionID);
            if (entry != null)
                agent.SetCompanionLevel(entry.level);
        }

        // 사망 콜백 등록
        int slotIdx = index; // 클로저 캡처
        agent.OnDied += (deadAgent) => OnCompanionDied(slotIdx, deadAgent);

        spawnedAgents[index] = agent;
        slotUIs[index]?.SetSummoned(true);

        SoundManager.Instance?.PlayButtonClick();
        UIManager.Instance?.ShowMessage($"⚔ {cData.companionName} 소환!", Color.green);
        Debug.Log($"[CompanionHotbarManager] {cData.companionName} 소환 ({GetActiveSummonCount()}/{maxActiveSummons})");

        // 튜토리얼 트리거
        TutorialManager.Instance?.OnActionCompleted("CompanionSummon");
    }

    /// <summary>동료 사망 시 콜백 — 쿨타임 시작</summary>
    private void OnCompanionDied(int index, CompanionAgent agent)
    {
        if (index < 0 || index >= slotDataList.Count) return;

        // Agent 참조 정리
        if (spawnedAgents.ContainsKey(index))
            spawnedAgents.Remove(index);

        // 쿨타임 시작
        float cd = GetCooldown(index);
        cooldownTimers[index] = cd;
        cooldownMaxTimes[index] = cd;

        // UI 갱신
        slotUIs[index]?.SetSummoned(false);
        slotUIs[index]?.UpdateCooldown(cd, cd);

        CompanionData cData = slotDataList[index].data;
        string name = cData != null ? cData.companionName : "동료";
        Debug.Log($"[CompanionHotbarManager] {name} 사망 → 쿨타임 {cd}초");
    }

    /// <summary>즉시 해제 (슬롯 제거/씬 전환)</summary>
    private void DismissCompanion(int index)
    {
        if (!spawnedAgents.ContainsKey(index)) return;

        CompanionAgent agent = spawnedAgents[index];
        if (agent != null)
        {
            agent.OnDied = null; // 콜백 해제 (Dismiss는 사망이 아님)
            agent.Dismiss();
        }

        spawnedAgents.Remove(index);
        slotUIs[index]?.SetSummoned(false);
    }

    // ─────────────────────────────────────────────────────────
    //  헬퍼
    // ─────────────────────────────────────────────────────────

    private float GetCooldown(int index)
    {
        CompanionData cData = slotDataList[index].data;
        if (cData != null && cData.summonCooldown > 0f)
            return cData.summonCooldown;
        return defaultSummonCooldown;
    }

    private Vector3 GetSpawnPosition(float radius)
    {
        if (companionSpawnAnchor == null)
        {
            FindPlayer();
            if (companionSpawnAnchor == null) return Vector3.zero;
        }

        float angle = Random.Range(0f, 360f);
        float r = Random.Range(radius * 0.5f, radius);
        return companionSpawnAnchor.position + new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad) * r,
            Mathf.Sin(angle * Mathf.Deg2Rad) * r,
            0f
        );
    }

    // ─────────────────────────────────────────────────────────
    //  저장 / 로드
    // ─────────────────────────────────────────────────────────

    public string[] GetHotbarSaveData()
    {
        string[] ids = new string[slotDataList.Count];
        for (int i = 0; i < slotDataList.Count; i++)
            ids[i] = slotDataList[i].data != null ? slotDataList[i].data.companionID : "";
        return ids;
    }

    public void LoadHotbarSaveData(string[] savedIDs)
    {
        if (savedIDs == null) return;

        var allCompanions = new List<CompanionData>(Resources.FindObjectsOfTypeAll<CompanionData>());

        for (int i = 0; i < Mathf.Min(savedIDs.Length, slotDataList.Count); i++)
        {
            string id = savedIDs[i];
            if (string.IsNullOrEmpty(id))
            {
                SetSlot(i, null);
                continue;
            }

            CompanionData found = CompanionInventoryManager.Instance?.FindCompanionData(id);
            if (found == null)
                found = allCompanions.Find(c => c != null && c.companionID == id);

            if (found != null)
            {
                SetSlot(i, found);
                Debug.Log($"[CompanionHotbarManager] 슬롯 {i} 복원: {found.companionName}");
            }
            else
            {
                Debug.LogWarning($"[CompanionHotbarManager] 슬롯 {i} 복원 실패: ID '{id}'");
                SetSlot(i, null);
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    //  오토 소환
    // ─────────────────────────────────────────────────────────

    /// <summary>AUTO 버튼 초기 세팅 (Inspector 연결 또는 자동 생성)</summary>
    private void SetupAutoButton()
    {
        if (autoButton != null)
        {
            autoButton.onClick.RemoveAllListeners();
            autoButton.onClick.AddListener(ToggleAutoSummon);
            Debug.Log($"[CompanionHotbarManager] AUTO 버튼 연결 완료: {autoButton.gameObject.name}");
        }
    }

    /// <summary>
    /// ★ 부모 오브젝트의 Image raycastTarget을 꺼서 자식 버튼 클릭을 차단하지 않도록 함
    /// 동료핫바 배경 Image가 raycastTarget=true이면 AUTO/슬롯 버튼이 눌리지 않는 버그 방지
    /// </summary>
    private void FixParentRaycast()
    {
        // 자기 자신의 Image
        Image myImage = GetComponent<Image>();
        if (myImage != null && myImage.raycastTarget)
        {
            myImage.raycastTarget = false;
            Debug.Log("[CompanionHotbarManager] 자신의 Image raycastTarget OFF");
        }

        // hotbarParent의 Image
        if (hotbarParent != null)
        {
            Image parentImage = hotbarParent.GetComponent<Image>();
            if (parentImage != null && parentImage.raycastTarget)
            {
                parentImage.raycastTarget = false;
                Debug.Log("[CompanionHotbarManager] hotbarParent Image raycastTarget OFF");
            }
        }
    }

    /// <summary>AUTO 버튼 시각 갱신</summary>
    private void RefreshAutoButtonUI()
    {
        if (autoButtonText != null)
            autoButtonText.text = autoSummonEnabled ? "AUTO\nON" : "AUTO\nOFF";

        if (autoButton != null)
        {
            Image btnImg = autoButton.GetComponent<Image>();
            if (btnImg != null)
                btnImg.color = autoSummonEnabled ? autoOnColor : autoOffColor;
        }
    }

    /// <summary>오토 버튼에서 호출 — 자동 소환 ON/OFF 토글</summary>
    public void ToggleAutoSummon()
    {
        autoSummonEnabled = !autoSummonEnabled;

        RefreshAutoButtonUI();
        SoundManager.Instance?.PlayButtonClick();

        UIManager.Instance?.ShowMessage(
            autoSummonEnabled ? "동료 자동 소환 ON" : "동료 자동 소환 OFF",
            autoSummonEnabled ? Color.green : Color.gray);
        Debug.Log($"[CompanionHotbarManager] 오토 소환: {(autoSummonEnabled ? "ON" : "OFF")}");

        // ★ ON으로 전환 즉시 소환 시도
        if (autoSummonEnabled)
        {
            autoSummonTimer = 0f; // 타이머 초기화 → 즉시 TryAutoSummon 실행
        }

        // 저장
        SaveLoadManager.Instance?.SaveGame();
    }

    public void SetAutoSummon(bool enabled)
    {
        autoSummonEnabled = enabled;
        RefreshAutoButtonUI();
    }

    private void TryAutoSummon()
    {
        autoSummonTimer -= Time.deltaTime;
        if (autoSummonTimer > 0f) return;
        autoSummonTimer = autoSummonInterval;

        // 플레이어가 없으면 재탐색
        if (companionSpawnAnchor == null)
        {
            FindPlayer();
            if (companionSpawnAnchor == null) return;
        }

        // 소환 가능 여유가 없으면 스킵
        if (GetActiveSummonCount() >= maxActiveSummons) return;

        // 슬롯 순서대로 소환 가능한 동료 탐색
        for (int i = 0; i < slotDataList.Count; i++)
        {
            if (slotDataList[i].data == null) continue;

            // 이미 소환 중이면 스킵
            if (spawnedAgents.ContainsKey(i) && spawnedAgents[i] != null && !spawnedAgents[i].IsDead)
                continue;

            // 쿨타임 중이면 스킵
            if (cooldownTimers[i] > 0f) continue;

            // 소환 가능 → 소환
            SummonCompanion(i);

            // 소환 후 다시 여유 체크
            if (GetActiveSummonCount() >= maxActiveSummons) return;
        }
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
