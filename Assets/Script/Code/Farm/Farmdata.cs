using System;
using System.Collections.Generic;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// Farmdata.cs
//
// ★ CropData, FertilizerData, CropStage, CropType, CropHarvestReward
//   → 별도 CropData.cs / FertilizerData.cs 로 분리됨
//
// ★ 이 파일에 남아있는 것:
//   - FarmGatherResult    : 채집 포인트 결과
//   - FarmGatherPoint     : 씬에 배치하는 채집 오브젝트
//   - FarmPlotUnlockData  : 밭 해금 비용 데이터
//   - FarmPlotState       : 밭 한 칸의 런타임 상태 (partial)
//   - FarmPlotSaveData    : 밭 저장 데이터
//   - FarmSaveData        : 전체 농장 저장 데이터 (partial)
// ═══════════════════════════════════════════════════════════════════

// ─── 채집 결과 ───────────────────────────────────────────────────
[Serializable]
public class FarmGatherResult
{
    public ItemData item;
    public int amount;
    public long cropPoints;
    public int cropIDForQuest = -1; // 퀘스트용 cropID (-1 = 해당없음)
}

// ─── 씬에 배치하는 채집 오브젝트 ────────────────────────────────
/// <summary>
/// FarmScene에 배치. 플레이어가 범위 안에서 E키 누르면 채집
/// FarmManager.OnGatherComplete() 호출
/// </summary>
public class FarmGatherPoint : MonoBehaviour
{
    [Header("===== 채집 정보 =====")]
    public string gatherName = "채집 포인트";
    public ItemData gatherItem;
    [Range(1, 20)] public int minAmount = 1;
    [Range(1, 20)] public int maxAmount = 3;
    public long cropPoints = 2;
    public int cropIDForQuest = -1;

    [Header("===== 쿨다운 =====")]
    [Tooltip("채집 후 재사용 대기시간 (초)")]
    public float cooldownSeconds = 60f;

    [Header("===== UI =====")]
    public KeyCode interactionKey = KeyCode.E;
    public GameObject interactionPrompt;
    public SpriteRenderer spriteRenderer;

    private bool playerInRange = false;
    private bool onCooldown = false;
    private float cooldownTimer = 0f;

    void Start()
    {
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }

    void Update()
    {
        if (onCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                onCooldown = false;
                if (spriteRenderer != null) spriteRenderer.color = Color.white;
                Debug.Log($"[FarmGatherPoint] {gatherName} 재사용 가능");
            }
            return;
        }

        if (playerInRange && Input.GetKeyDown(interactionKey))
            Gather();
    }

    private void Gather()
    {
        if (onCooldown) return;

        int amount = UnityEngine.Random.Range(minAmount, maxAmount + 1);
        FarmGatherResult result = new FarmGatherResult
        {
            item = gatherItem,
            amount = amount,
            cropPoints = cropPoints,
            cropIDForQuest = cropIDForQuest
        };
        FarmManager.Instance?.OnGatherComplete(result);

        onCooldown = true;
        cooldownTimer = cooldownSeconds;
        if (spriteRenderer != null) spriteRenderer.color = Color.gray;
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !onCooldown)
        {
            playerInRange = true;
            if (interactionPrompt != null) interactionPrompt.SetActive(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
        }
    }
}

// ─── 밭 해금 비용 데이터 ─────────────────────────────────────────
[Serializable]
public class FarmPlotUnlockData
{
    public int plotIndex;
    public int unlockCostGold;
    public int requiredPlayerLevel;
}

// ─── 밭 한 칸 런타임 상태 ────────────────────────────────────────
[Serializable]
public partial class FarmPlotState
{
    public int plotIndex;
    public bool isUnlocked;
    public CropData currentCrop;
    public DateTime plantTime;
    public bool isWatered;
    public bool isFertilized;
    public int fertilizerID = -1;
    public FertilizerData currentFertilizer;
    public bool isHarvested;

    // ── 성장 속도 배율 (물/비료 적용) ──────────────────────────
    public float GetSpeedMultiplier()
    {
        float reduction = 0f;
        if (isWatered && currentCrop != null)
            reduction += currentCrop.waterSpeedBonus;
        if (isFertilized && currentFertilizer != null)
            reduction += currentFertilizer.speedBonus;
        return Mathf.Max(0.1f, 1f - reduction); // 최소 10% 시간 유지
    }

    // ── 성장 진행도 (0~1) ───────────────────────────────────────
    public float GetProgress()
    {
        if (currentCrop == null || isHarvested) return 0f;
        float totalSeconds = currentCrop.growthTimeSeconds * GetSpeedMultiplier();
        float elapsed = (float)(DateTime.Now - plantTime).TotalSeconds;
        return Mathf.Clamp01(elapsed / totalSeconds);
    }

    public bool IsReadyToHarvest()
    {
        if (currentCrop == null || isHarvested) return false;
        // ★ 튜토리얼 중 즉시 성장: 물+비료 완료된 밭만 즉시 수확 가능
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
        {
            if (isWatered && isFertilized)
                return true;
            // 물/비료 안 준 밭은 일반 성장 로직 따름
        }
        return GetProgress() >= 1f;
    }

    // ── 성장 단계 ───────────────────────────────────────────────
    public CropStage GetStage()
    {
        if (currentCrop == null || isHarvested) return CropStage.Empty;
        float p = GetProgress();
        if (p >= 1.00f) return CropStage.ReadyToHarvest;
        if (p >= 0.75f) return CropStage.NearReady;
        if (p >= 0.50f) return CropStage.Growing;
        if (p >= 0.25f) return CropStage.Sprout;
        return CropStage.Seeded;
    }

    // ── 남은 시간 (초) ──────────────────────────────────────────
    public float GetRemainingSeconds()
    {
        if (currentCrop == null) return 0f;
        float totalSeconds = currentCrop.growthTimeSeconds * GetSpeedMultiplier();
        float elapsed = (float)(DateTime.Now - plantTime).TotalSeconds;
        return Mathf.Max(0f, totalSeconds - elapsed);
    }
}

// ─── 밭 저장 데이터 ──────────────────────────────────────────────
[Serializable]
public class FarmPlotSaveData
{
    public int plotIndex;
    public bool isUnlocked;
    public int cropID;
    public string plantTimeISO;
    public bool isWatered;
    public bool isFertilized;
    public int fertilizerID;
    public bool isHarvested;
}

// ─── 전체 농장 저장 데이터 ────────────────────────────────────────
[Serializable]
public partial class FarmSaveData
{
    public FarmPlotSaveData[] plots;
    public int totalHarvestCount;
    public long cropPoints;
    public FarmBuildingSaveData buildingData;
    public FarmInventorySaveData inventoryData; // ★ 씨앗/수확물 수량 저장
    // ★ 메인 인벤으로 이미 옮긴 수확물 누적 (중복 전달 방지용)
    //   harvests[] 자체는 팜 인벤 표시용으로 유지하고, transferredHarvests로 차분 계산
    public System.Collections.Generic.List<FarmItemCount> transferredHarvests;
}