using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FarmManagerExtension — FarmManager UnityEvent → 정적 C# 이벤트 브리지
///
/// 사용법:
///   1. ManagerRoot 또는 FarmManager와 같은 GameObject에 추가
///   2. FarmPlotUI, FarmInventoryUI 등에서 아래 정적 이벤트를 구독
///
///   FarmManagerExtension.OnPlotStateChangedStatic += (index) => ...;
///   FarmManagerExtension.OnHarvestCompleteStatic += (index, rewards) => ...;
///   FarmManagerExtension.OnCropPointsChanged += (pts) => ...;
/// </summary>
[DefaultExecutionOrder(-30)]
public class FarmManagerExtension : MonoBehaviour
{
   
    public static FarmManagerExtension Instance { get; private set; }

    // ════════════════════════════════════════════════
    //  정적 이벤트 (구독 가능)
    // ════════════════════════════════════════════════

    /// <summary>FarmManager.OnPlotStateChanged (UnityEvent) → 정적 이벤트</summary>
    public static event Action<int> OnPlotStateChangedStatic;

    /// <summary>FarmManager.OnHarvestComplete (UnityEvent) → 정적 이벤트</summary>
    public static event Action<int, List<CropHarvestReward>> OnHarvestCompleteStatic;

    /// <summary>FarmManager.OnCropPointsChanged (static event) → 재노출</summary>
    public static event Action<long> OnCropPointsChanged;

    public static void InvokePlotChanged(int plotIndex)
   => OnPlotStateChangedStatic?.Invoke(plotIndex);

    // ════════════════════════════════════════════════
    //  Unity 생명주기
    // ════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        ConnectEvents();
    }

    // FarmManager가 씬 이동 후 재연결 필요할 때
    void OnEnable()
    {
        if (Instance == this)
            StartCoroutine(ReconnectAfterFrame());
    }

    private System.Collections.IEnumerator ReconnectAfterFrame()
    {
        yield return null; // FarmManager.Awake 완료 대기
        ConnectEvents();
    }

    // ════════════════════════════════════════════════
    //  이벤트 연결
    // ════════════════════════════════════════════════

    private void ConnectEvents()
    {
        if (FarmManager.Instance == null)
        {
            Debug.LogWarning("[FarmManagerExtension] FarmManager가 없습니다. 나중에 재시도합니다.");
            return;
        }

        // OnPlotStateChanged (UnityEvent<int>) 연결
        // 중복 추가 방지: 먼저 제거 후 추가
        FarmManager.Instance.OnPlotStateChanged.RemoveListener(BridgePlotStateChanged);
        FarmManager.Instance.OnPlotStateChanged.AddListener(BridgePlotStateChanged);

        // OnHarvestComplete (UnityEvent<int, List<CropHarvestReward>>) 연결
        FarmManager.Instance.OnHarvestComplete.RemoveListener(BridgeHarvestComplete);
        FarmManager.Instance.OnHarvestComplete.AddListener(BridgeHarvestComplete);

        // FarmManager.OnCropPointsChanged (static event) 연결
        FarmManager.OnCropPointsChanged -= BridgeCropPoints;
        FarmManager.OnCropPointsChanged += BridgeCropPoints;

        Debug.Log("[FarmManagerExtension] FarmManager 이벤트 연결 완료");
    }

    void OnDestroy()
    {
        // 정리
        if (FarmManager.Instance != null)
        {
            FarmManager.Instance.OnPlotStateChanged.RemoveListener(BridgePlotStateChanged);
            FarmManager.Instance.OnHarvestComplete.RemoveListener(BridgeHarvestComplete);
        }
        FarmManager.OnCropPointsChanged -= BridgeCropPoints;
    }

    // ════════════════════════════════════════════════
    //  브리지 메서드
    // ════════════════════════════════════════════════

    private void BridgePlotStateChanged(int index)
        => OnPlotStateChangedStatic?.Invoke(index);

    private void BridgeHarvestComplete(int index, List<CropHarvestReward> rewards)
        => OnHarvestCompleteStatic?.Invoke(index, rewards);

    private void BridgeCropPoints(long pts)
        => OnCropPointsChanged?.Invoke(pts);
}
