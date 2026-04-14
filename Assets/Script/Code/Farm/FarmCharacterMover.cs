using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// FarmScene 전용 캐릭터 줍기 애니메이션 컨트롤러.
/// FarmScene 내 캐릭터 오브젝트에 부착.
/// plotTransforms가 Inspector에 비어 있으면 FarmPlotController를 자동 탐색.
/// </summary>
public class FarmCharacterMover : MonoBehaviour
{
    public static FarmCharacterMover Instance { get; private set; }

    [Header("===== 이동 설정 =====")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float arriveDistance = 0.3f;
    [SerializeField] private Vector3 plotOffset = new Vector3(0f, -0.5f, 0f);

    [Header("===== 밭 위치 (plotIndex 순서) =====")]
    [Tooltip("비워두면 Start에서 FarmPlotController를 자동 탐색")]
    [SerializeField] private Transform[] plotTransforms;

    [Header("===== Animator =====")]
    [SerializeField] private Animator characterAnimator;
    [Tooltip("Animator의 걷기 Bool 파라미터 이름")]
    [SerializeField] private string walkParam = "isWalking";
    [Tooltip("Animator의 줍기 Trigger 파라미터 이름")]
    [SerializeField] private string pickupParam = "Pickup";

    [Header("===== 줍기 애니메이션 =====")]
    [Tooltip("줍기 애니메이션 총 길이(초) — Animation Event 미사용 시 타이머로 대체")]
    [SerializeField] private float pickupAnimDuration = 0.8f;
    [Tooltip("수확 실행 타이밍 (0~1, 애니메이션 진행률) — Animation Event 있으면 무시됨")]
    [SerializeField][Range(0f, 1f)] private float pickupHarvestNormalizedTime = 0.5f;

    // ─── 애니메이터 파라미터 ID (캐싱) ───
    private int _animWalk;
    private int _animPickup;

    // ─── 진행 중인 작업 ───
    private Coroutine _moveCoroutine;
    private Coroutine _pickupCoroutine;

    // Animation Event 방식: 수확 콜백을 저장해두고 이벤트 시점에 호출
    private Action _pendingHarvestCallback;
    private Action _pendingCompleteCallback;

    public bool IsMoving { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Debug.Log("[FarmCharacterMover] Awake — FarmScene 전용 캐릭터 무버 생성.");

        if (characterAnimator == null)
            characterAnimator = GetComponentInChildren<Animator>();

        _animWalk   = Animator.StringToHash(walkParam);
        _animPickup = Animator.StringToHash(pickupParam);
    }

    void Start()
    {
        // plotTransforms가 Inspector에 비어 있으면 FarmScene의 FarmPlotController를 자동 탐색
        if (plotTransforms == null || plotTransforms.Length == 0)
            AutoFindPlotTransforms();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>FarmScene에 있는 FarmPlotController를 plotIndex 순으로 정렬해 plotTransforms에 채움</summary>
    private void AutoFindPlotTransforms()
    {
        var controllers = FindObjectsOfType<FarmPlotController>();
        if (controllers == null || controllers.Length == 0) return;

        System.Array.Sort(controllers, (a, b) => a.plotIndex.CompareTo(b.plotIndex));
        plotTransforms = new Transform[controllers.Length];
        for (int i = 0; i < controllers.Length; i++)
            plotTransforms[i] = controllers[i].transform;

        Debug.Log($"[FarmCharacterMover] plotTransforms 자동 탐색 완료 — {controllers.Length}개");
    }

    // ────────────────────────────────────────────
    //  이동
    // ────────────────────────────────────────────

    public void MoveToPlot(int plotIndex, Action onArrived = null)
    {
        if (plotTransforms == null || plotIndex >= plotTransforms.Length
            || plotTransforms[plotIndex] == null)
        { onArrived?.Invoke(); return; }

        Vector3 target = plotTransforms[plotIndex].position + plotOffset;
        if (Vector3.Distance(transform.position, target) <= arriveDistance)
        { onArrived?.Invoke(); return; }

        StopAllActions();
        _moveCoroutine = StartCoroutine(WalkTo(target, onArrived));
    }

    private IEnumerator WalkTo(Vector3 target, Action onArrived)
    {
        IsMoving = true;
        SetWalking(true);

        float dir = target.x - transform.position.x;
        if (Mathf.Abs(dir) > 0.01f)
        {
            var s = transform.localScale;
            s.x = dir > 0 ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
            transform.localScale = s;
        }

        while (Vector3.Distance(transform.position, target) > arriveDistance)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }

        SetWalking(false);
        IsMoving = false;
        _moveCoroutine = null;
        onArrived?.Invoke();
    }

    // ────────────────────────────────────────────
    //  이동 → 줍기 (수확 전용)
    // ────────────────────────────────────────────

    /// <summary>
    /// 밭까지 이동 후 줍기 애니메이션 재생.
    /// onHarvest  : 줍기 모션 중간(pickupHarvestNormalizedTime)에 실제 수확 실행
    /// onComplete : 줍기 애니메이션 완료 후 호출 (UI 갱신 등)
    /// </summary>
    public void MoveToPlotAndPickup(int plotIndex, Action onHarvest, Action onComplete = null)
    {
        MoveToPlot(plotIndex, () => PlayPickup(onHarvest, onComplete));
    }

    /// <summary>현재 위치에서 바로 줍기 애니메이션 재생</summary>
    public void PlayPickup(Action onHarvest, Action onComplete = null)
    {
        if (_pickupCoroutine != null) StopCoroutine(_pickupCoroutine);

        _pendingHarvestCallback  = onHarvest;
        _pendingCompleteCallback = onComplete;

        // Animator Trigger 발동
        if (characterAnimator != null)
            characterAnimator.SetTrigger(_animPickup);
        else
            Debug.LogWarning("[FarmCharacterMover] characterAnimator가 null — Inspector에서 연결 확인");

        _pickupCoroutine = StartCoroutine(PickupRoutine());
    }

    /// <summary>
    /// 줍기 애니메이션 코루틴.
    /// ★ Animation Event(OnPickupAnimEvent)가 설정되어 있으면 그 시점에 수확 실행.
    ///   설정 안 된 경우 pickupHarvestNormalizedTime 타이머로 수확 실행.
    /// </summary>
    private IEnumerator PickupRoutine()
    {
        float elapsed   = 0f;
        bool  harvested = false;

        while (elapsed < pickupAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pickupAnimDuration;

            // 타이머 기반 수확 (Animation Event 미사용 폴백)
            if (!harvested && _pendingHarvestCallback != null && t >= pickupHarvestNormalizedTime)
            {
                harvested = true;
                FireHarvestCallback();
            }
            yield return null;
        }

        _pickupCoroutine = null;

        // 수확 콜백이 아직 안 불렸으면 강제 실행 (누락 방지)
        if (!harvested && _pendingHarvestCallback != null)
            FireHarvestCallback();

        _pendingCompleteCallback?.Invoke();
        _pendingCompleteCallback = null;
    }

    private void FireHarvestCallback()
    {
        var cb = _pendingHarvestCallback;
        _pendingHarvestCallback = null;
        cb?.Invoke();
    }

    // ────────────────────────────────────────────
    //  Animation Event 연동
    //  Animator 클립의 "줍기" 애니메이션 원하는 프레임에
    //  이 함수명(OnPickupAnimEvent)을 Animation Event로 추가하면
    //  타이머 대신 정확한 프레임에 수확이 실행됩니다.
    // ────────────────────────────────────────────

    /// <summary>Animator Animation Event → 수확 실행 (줍는 손이 땅에 닿는 프레임에 연결)</summary>
    public void OnPickupAnimEvent()
    {
        if (_pendingHarvestCallback != null)
        {
            Debug.Log("[FarmCharacterMover] OnPickupAnimEvent → 수확 실행");
            FireHarvestCallback();
        }
    }

    // ────────────────────────────────────────────
    //  헬퍼
    // ────────────────────────────────────────────

    private void SetWalking(bool w)
    {
        if (characterAnimator != null) characterAnimator.SetBool(_animWalk, w);
    }

    private void StopAllActions()
    {
        if (_moveCoroutine   != null) { StopCoroutine(_moveCoroutine);   _moveCoroutine   = null; }
        if (_pickupCoroutine != null) { StopCoroutine(_pickupCoroutine); _pickupCoroutine = null; }
        SetWalking(false);
        IsMoving = false;
    }
}
