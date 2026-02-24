using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 가챠 결과 UI - 상위 등급만 큐로 표시
/// 
/// ✅ 동작:
///   - Rare 이상 아이템만 결과 패널에 표시
///   - 여러 개면 큐로 처리 → 이미지 클릭 시 다음 아이템
///   - 마지막 아이템 클릭 시 패널 닫힘
///   - Common/Uncommon은 결과 패널 없이 인벤에만 들어감
///
/// ✅ Inspector 설정:
///   - resultPanel        : 결과 팝업 패널 GameObject
///   - itemIconImage      : 아이템 아이콘 Image (자동으로 Button 추가됨)
///   - itemNameText       : 아이템 이름 TMP
///   - rarityText         : 희귀도 텍스트 TMP
///   - backgroundGlow     : 등급별 배경 이미지 (선택)
///   - counterText        : "1 / 3" 카운터 TMP (선택)
///   - clickHintText      : "탭하여 다음" 힌트 TMP (선택)
///   - closeButton        : 닫기 버튼 (선택)
///   - showRarityThreshold: 이 등급 이상만 표시 (기본: Rare)
/// </summary>
public class GachaResultUI : MonoBehaviour
{
    public static GachaResultUI Instance;

    [Header("결과 패널")]
    public GameObject resultPanel;

    [Header("아이템 표시 UI")]
    public Image itemIconImage;             // 아이템 아이콘 (클릭 시 다음으로)
    public Image backgroundGlow;            // 등급별 배경 글로우
    public TextMeshProUGUI itemNameText;    // 아이템 이름
    public TextMeshProUGUI rarityText;      // 희귀도 텍스트
    public TextMeshProUGUI counterText;     // "1 / 3" 카운터
    public TextMeshProUGUI clickHintText;   // "탭하여 다음" 힌트
    public Button closeButton;

    [Header("등급 필터")]
    [Tooltip("이 등급 이상만 결과 패널에 표시 (기본: Rare=희귀)")]
    public ItemRarity showRarityThreshold = ItemRarity.Rare;

    [Header("애니메이션")]
    public float punchScaleDuration = 0.3f;
    public float glowPulseDuration = 0.8f;

    // ── 큐 ──────────────────────────────
    private Queue<EquipmentData> resultQueue = new Queue<EquipmentData>();
    private int totalCount = 0;
    private int shownCount = 0;
    private bool isAnimating = false;

    // ── 로컬 사운드 fallback ─────────────
    private AudioSource audioSource;
    public AudioClip revealSound;
    public AudioClip rareSound;

    // ─────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(OnClickNext);

        // 아이콘 이미지에 클릭 이벤트 자동 추가
        if (itemIconImage != null)
        {
            Button iconBtn = itemIconImage.GetComponent<Button>();
            if (iconBtn == null)
                iconBtn = itemIconImage.gameObject.AddComponent<Button>();
            iconBtn.onClick.AddListener(OnClickNext);
            iconBtn.transition = Selectable.Transition.None;
        }

        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    // ─────────────────────────────────────
    // ★ 외부에서 호출: 가챠 결과 전달
    // ─────────────────────────────────────
    public void ShowResults(List<EquipmentData> results)
    {
        if (results == null || results.Count == 0) return;

        // ── 상위 등급 필터링 후 큐에 넣기 ──
        resultQueue.Clear();
        foreach (var item in results)
        {
            if (item != null && (int)item.rarity >= (int)showRarityThreshold)
                resultQueue.Enqueue(item);
        }

        // 상위 등급이 하나도 없으면 패널 안 열림
        if (resultQueue.Count == 0)
        {
            Debug.Log($"[GachaResultUI] {showRarityThreshold} 이상 없음 → 패널 생략");
            return;
        }

        totalCount = resultQueue.Count;
        shownCount = 0;

        if (resultPanel != null)
            resultPanel.SetActive(true);

        ShowNextItem();
    }

    // ─────────────────────────────────────
    // 다음 아이템 표시
    // ─────────────────────────────────────
    private void ShowNextItem()
    {
        if (resultQueue.Count == 0)
        {
            ClosePanel();
            return;
        }

        if (isAnimating) return;

        EquipmentData item = resultQueue.Dequeue();
        shownCount++;

        // ── UI 업데이트 ──
        if (itemIconImage != null)
        {
            itemIconImage.sprite = item.itemIcon;
            itemIconImage.color = Color.white;
        }

        if (itemNameText != null)
            itemNameText.text = item.itemName;

        if (rarityText != null)
        {
            rarityText.text = GetRarityText(item.rarity);
            rarityText.color = GetRarityColor(item.rarity);
        }

        if (backgroundGlow != null)
            backgroundGlow.color = GetRarityColor(item.rarity);

        // 카운터: "1 / 3" (1개면 숨김)
        if (counterText != null)
        {
            counterText.gameObject.SetActive(totalCount > 1);
            counterText.text = $"{shownCount} / {totalCount}";
        }

        // 힌트 텍스트
        if (clickHintText != null)
            clickHintText.text = resultQueue.Count > 0 ? "탭하여 다음 아이템" : "탭하여 닫기";

        // ── 효과음 ──
        PlayRevealSound(item.rarity);

        // ── 팝업 애니메이션 ──
        StopAllCoroutines();
        StartCoroutine(PunchScaleAnim());
        if (item.rarity >= ItemRarity.Epic)
            StartCoroutine(GlowPulseAnim());
    }

    // ─────────────────────────────────────
    // 클릭 → 다음 아이템 or 닫기
    // ─────────────────────────────────────
    public void OnClickNext()
    {
        SoundManager.Instance?.PlayButtonClick();

        if (isAnimating) return;

        if (resultQueue.Count > 0)
            ShowNextItem();
        else
            ClosePanel();
    }

    private void ClosePanel()
    {
        if (resultPanel != null)
            resultPanel.SetActive(false);

        resultQueue.Clear();
        totalCount = 0;
        shownCount = 0;
    }

    // ─────────────────────────────────────
    // 애니메이션
    // ─────────────────────────────────────
    IEnumerator PunchScaleAnim()
    {
        isAnimating = true;
        if (itemIconImage == null) { isAnimating = false; yield break; }

        Transform t = itemIconImage.transform;
        float elapsed = 0f;

        while (elapsed < punchScaleDuration)
        {
            elapsed += Time.deltaTime;
            float ratio = elapsed / punchScaleDuration;
            float s = ratio - 1f;
            float scale = 1f + 1.70158f * s * s * s + s * s;
            t.localScale = Vector3.one * Mathf.Max(0f, scale);
            yield return null;
        }

        t.localScale = Vector3.one;
        isAnimating = false;
    }

    IEnumerator GlowPulseAnim()
    {
        if (backgroundGlow == null) yield break;
        Color baseColor = backgroundGlow.color;
        float elapsed = 0f;

        while (elapsed < glowPulseDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.PingPong(elapsed * 3f, 1f);
            backgroundGlow.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.3f + alpha * 0.5f);
            yield return null;
        }

        backgroundGlow.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.6f);
    }

    // ─────────────────────────────────────
    // 사운드
    // ─────────────────────────────────────
    void PlayRevealSound(ItemRarity rarity)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(rarity >= ItemRarity.Epic ? "GachaRare" : "GachaReveal");
            return;
        }
        if (audioSource == null) return;
        if (rarity >= ItemRarity.Rare && rareSound != null) audioSource.PlayOneShot(rareSound);
        else if (revealSound != null) audioSource.PlayOneShot(revealSound);
    }

    // ─────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────
    string GetRarityText(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Rare: return "✦ 희귀";
            case ItemRarity.Epic: return "✦✦ 영웅";
            case ItemRarity.Legendary: return "✦✦✦ 전설";
            default: return rarity.ToString();
        }
    }

    Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Rare: return new Color(0.3f, 0.6f, 1f);
            case ItemRarity.Epic: return new Color(0.7f, 0.3f, 1f);
            case ItemRarity.Legendary: return new Color(1f, 0.7f, 0.1f);
            default: return Color.white;
        }
    }
}

/// <summary>
/// GachaResultSlot - 하위 호환성 유지용 (실제로는 GachaResultUI에서 직접 표시)
/// </summary>
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