using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 동료 핫바 슬롯 (CompanionHotbarSlotPrefab에 붙이는 컴포넌트)
///
/// [동작]
///   - 짧은 클릭 → 소환 (쿨타임 체크는 Manager에서)
///   - 꾹 누르기(longPressThreshold초) + 밖으로 드래그 → 핫바에서 제거
///   - 쿨타임 중 → 어두운 오버레이 + 남은 시간 텍스트
///   - 소환 중 → 초록 테두리
///
/// Prefab 구조:
///   CompanionHotbarSlot (70x70)
///   ├── Icon (Image) → iconImage
///   ├── Border (Image) → borderImage
///   ├── ActiveIndicator (Image, 초록) → activeIndicator
///   ├── NameText (TMP) → nameText (optional)
///   ├── CooldownOverlay (Image, 반투명 검정) → cooldownOverlay
///   └── CooldownText (TMP, 흰색 굵게) → cooldownText
/// </summary>
public class CompanionHotbarSlot : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI 참조")]
    public Image iconImage;
    public Image borderImage;
    public Image activeIndicator;
    public TextMeshProUGUI nameText;

    [Header("쿨타임 UI")]
    [Tooltip("쿨타임 중 어둡게 덮는 오버레이 Image (반투명 검정)")]
    public Image cooldownOverlay;
    [Tooltip("쿨타임 남은 시간 텍스트")]
    public TextMeshProUGUI cooldownText;

    [Header("색상")]
    public Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    public Color activeColor = new Color(0f, 1f, 0.5f, 0.8f);
    public Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Color cooldownOverlayColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("롱프레스/드래그 설정")]
    [Tooltip("꾹 누르기 인식 시간 (초)")]
    public float longPressThreshold = 0.5f;
    [Tooltip("드래그 중 고스트 아이콘의 투명도")]
    public float dragGhostAlpha = 0.7f;
    [Tooltip("드래그 중 원본 슬롯의 투명도")]
    public float dragOriginAlpha = 0.3f;

    private CompanionHotbarManager manager;
    private int slotIndex;
    private bool isSummoned;
    private CompanionData currentData;

    // 롱프레스/드래그 상태
    private bool isPointerDown;
    private float pointerDownTime;
    private bool isLongPressed;
    private bool isDragging;

    // 드래그 고스트
    private GameObject dragGhost;
    private Canvas rootCanvas;
    private CanvasGroup slotCanvasGroup;

    // 등급 색상
    private readonly Color[] rarityColors = new Color[]
    {
        Color.white,                            // Common
        new Color(0.3f, 0.5f, 1f),             // Rare
        new Color(0.7f, 0.2f, 1f),             // Epic
        new Color(1f, 0.8f, 0.1f)              // Legendary
    };

    // ─────────────────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────────────────

    public void Init(CompanionHotbarManager mgr, int index)
    {
        manager = mgr;
        slotIndex = index;

        // CanvasGroup (드래그 중 투명도 제어)
        slotCanvasGroup = GetComponent<CanvasGroup>();
        if (slotCanvasGroup == null)
            slotCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        ResetCooldownUI();
        Refresh(null);
    }

    // ─────────────────────────────────────────────────────────
    //  UI 갱신
    // ─────────────────────────────────────────────────────────

    public void Refresh(CompanionData data)
    {
        currentData = data;

        if (data == null)
        {
            if (iconImage != null) { iconImage.sprite = null; iconImage.color = emptyColor; }
            if (borderImage != null) borderImage.color = emptyColor;
            if (nameText != null) nameText.text = "";
            SetSummoned(false);
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

    public void SetSummoned(bool summoned)
    {
        isSummoned = summoned;
        if (activeIndicator != null)
            activeIndicator.gameObject.SetActive(summoned);

        // 소환 중일 때 테두리 색상
        if (borderImage != null && currentData != null)
        {
            if (summoned)
                borderImage.color = activeColor;
            else if ((int)currentData.rarity < rarityColors.Length)
                borderImage.color = rarityColors[(int)currentData.rarity];
        }

    }

    // ─────────────────────────────────────────────────────────
    //  쿨타임/지속시간 UI 업데이트 (Manager.Update에서 매 프레임 호출)
    // ─────────────────────────────────────────────────────────

    /// <summary>쿨타임 오버레이 갱신</summary>
    public void UpdateCooldown(float remaining, float total)
    {
        bool onCooldown = remaining > 0f;

        if (cooldownOverlay != null)
        {
            cooldownOverlay.gameObject.SetActive(onCooldown);
            if (onCooldown)
                cooldownOverlay.color = cooldownOverlayColor;
        }

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(onCooldown);
            if (onCooldown)
                cooldownText.text = $"{Mathf.CeilToInt(remaining)}";
        }
    }

    /// <summary>쿨타임 UI 초기 상태로 리셋</summary>
    public void ResetCooldownUI()
    {
        if (cooldownOverlay != null) cooldownOverlay.gameObject.SetActive(false);
        if (cooldownText != null) cooldownText.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────
    //  입력 처리: 짧은 클릭 = 소환, 롱프레스 + 드래그 = 제거
    // ─────────────────────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        if (currentData == null) return;
        isPointerDown = true;
        pointerDownTime = Time.unscaledTime;
        isLongPressed = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 드래그 중이었으면 클릭 무시
        if (isDragging || isLongPressed) return;
        if (currentData == null) return;

        // 짧은 클릭 → 소환
        manager?.OnSlotClicked(slotIndex);
    }

    // ─────────────────────────────────────────────────────────
    //  드래그 — 롱프레스 후에만 드래그 시작
    // ─────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentData == null) return;

        // 롱프레스 인식 확인
        float holdTime = Time.unscaledTime - pointerDownTime;
        if (holdTime < longPressThreshold)
        {
            // 짧게 눌렀다 드래그 → 드래그 시작 안 함
            isDragging = false;
            return;
        }

        isLongPressed = true;
        isDragging = true;

        // 루트 Canvas 캐싱
        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

        CreateDragGhost(eventData);

        if (slotCanvasGroup != null)
            slotCanvasGroup.alpha = dragOriginAlpha;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || dragGhost == null) return;
        dragGhost.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            // 드래그가 시작되지 않았으면 정리만
            isLongPressed = false;
            return;
        }

        isDragging = false;
        isLongPressed = false;

        if (slotCanvasGroup != null)
            slotCanvasGroup.alpha = 1f;

        if (dragGhost != null)
            Destroy(dragGhost);

        // 핫바 영역 밖인지 판정
        RectTransform hotbarRect = manager?.GetHotbarRect();
        if (hotbarRect != null && currentData != null)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    hotbarRect, eventData.position, eventData.pressEventCamera))
            {
                manager.RemoveCompanionFromSlot(slotIndex);
            }
        }
    }

    private void CreateDragGhost(PointerEventData eventData)
    {
        if (rootCanvas == null) return;

        dragGhost = new GameObject("DragGhost");
        dragGhost.transform.SetParent(rootCanvas.transform, false);
        dragGhost.transform.SetAsLastSibling();

        RectTransform ghostRect = dragGhost.AddComponent<RectTransform>();
        RectTransform slotRect = GetComponent<RectTransform>();
        if (slotRect != null)
            ghostRect.sizeDelta = slotRect.sizeDelta;
        else
            ghostRect.sizeDelta = new Vector2(70f, 70f);

        Image ghostImage = dragGhost.AddComponent<Image>();
        if (iconImage != null && iconImage.sprite != null)
        {
            ghostImage.sprite = iconImage.sprite;
            ghostImage.color = new Color(1f, 1f, 1f, dragGhostAlpha);
        }

        ghostImage.raycastTarget = false;

        CanvasGroup ghostCG = dragGhost.AddComponent<CanvasGroup>();
        ghostCG.blocksRaycasts = false;
        ghostCG.interactable = false;

        dragGhost.transform.position = eventData.position;
    }
}
