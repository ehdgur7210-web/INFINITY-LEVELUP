using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ═══════════════════════════════════════════════════════════════════
// CompanionDetailTab.cs
//
// 동료 상세 패널 — [상세] 탭
//
// ★ 레이아웃 (동료 목록 숨김, 전체 너비 사용):
//   ┌───────────────┬─────────────────────────┐
//   │               │ 이름   Lv.5  ★★★★★   │
//   │               │ [에픽] 불의 정령          │
//   │   동료 대형   │ ─────────────────────── │
//   │   초상화      │ 공격력      6,067만      │
//   │   (portrait)  │ 체력        3억          │
//   │               │ 공격속도    1.49초        │
//   │               │ 치명타확률  25.95%        │
//   │               │ 치명타데미지 232%         │
//   │               │ 스킬데미지   225%         │
//   │               │ ─────────────────────── │
//   │               │ >> 스킬 쿨타임 감소 2.99% │
//   │               │ >> 물리 방어력     8.25%  │
//   │               │ >> 골드 획득량 증가  2.45  │
//   │               │ (스크롤 가능)              │
//   └───────────────┴─────────────────────────┘
// ═══════════════════════════════════════════════════════════════════

public class CompanionDetailTab : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  Inspector 필드
    // ─────────────────────────────────────────────────────────────

    [Header("===== 좌측: 대형 초상화 =====")]
    [Tooltip("동료 대형 이미지 (좌측 절반 차지)")]
    [SerializeField] private Image bigPortraitImage;

    [Header("===== 우측: 프로필 =====")]
    [SerializeField] private TextMeshProUGUI nameText;           // "불의 정령"
    [SerializeField] private TextMeshProUGUI levelText;          // "Lv.5"
    [SerializeField] private TextMeshProUGUI starsText;          // "★★★★★"
    [SerializeField] private TextMeshProUGUI rarityText;         // "[에픽]"
    [SerializeField] private TextMeshProUGUI descriptionText;    // 설명 텍스트

    [Header("===== 우측: 기본 스탯 =====")]
    [SerializeField] private TextMeshProUGUI atkValueText;       // 공격력
    [SerializeField] private TextMeshProUGUI hpValueText;        // 체력
    [SerializeField] private TextMeshProUGUI atkSpdValueText;    // 공격속도
    [SerializeField] private TextMeshProUGUI critRateValueText;  // 치명타확률
    [SerializeField] private TextMeshProUGUI critDmgValueText;   // 치명타데미지
    [SerializeField] private TextMeshProUGUI skillDmgValueText;  // 스킬데미지

    [Header("===== 우측: 추가 효과 (스크롤) =====")]
    [SerializeField] private Transform bonusListContent;
    [SerializeField] private GameObject bonusRowPrefab;

    [Header("===== 스탯 계산 설정 =====")]
    [SerializeField] private float statGrowthRate = 0.1f;        // 레벨당 10% 성장
    [SerializeField] private float starStatBonus = 0.1f;         // 별 1개당 10% 추가

    // ─────────────────────────────────────────────────────────────
    //  내부 변수
    // ─────────────────────────────────────────────────────────────

    private CompanionData _companion;
    private int _level = 1;
    private int _stars = 1;

    // ─────────────────────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────────────────────

    /// <summary>탭 활성화 시 호출</summary>
    public void Refresh(CompanionData companion, int level)
    {
        _companion = companion;
        _level = Mathf.Max(1, level);
        _stars = GetCompanionStars(companion);

        RefreshProfile();
        RefreshBaseStats();
        RefreshBonusList();
    }

    // ─────────────────────────────────────────────────────────────
    //  좌측: 대형 초상화
    //  우측: 프로필 영역
    // ─────────────────────────────────────────────────────────────

    private void RefreshProfile()
    {
        if (_companion == null) return;

        // 좌측 대형 초상화
        if (bigPortraitImage != null)
        {
            bigPortraitImage.sprite = _companion.portrait;
            bigPortraitImage.color = _companion.portrait != null ? Color.white : new Color(1, 1, 1, 0.1f);
        }

        // 우측 프로필
        if (nameText != null)
            nameText.text = _companion.companionName;

        if (levelText != null)
            levelText.text = $"Lv.{_level}";

        if (starsText != null)
        {
            starsText.text = MakeStarString(_stars);
            starsText.color = StarSpriteUtil.GetStarColor(_stars);
        }

        if (rarityText != null)
        {
            rarityText.text = $"[{GetRarityName(_companion.rarity)}]";
            rarityText.color = GetRarityColor(_companion.rarity);
        }

        if (descriptionText != null)
            descriptionText.text = _companion.description ?? "";
    }

    // ─────────────────────────────────────────────────────────────
    //  우측: 기본 스탯
    // ─────────────────────────────────────────────────────────────

    private void RefreshBaseStats()
    {
        if (_companion == null) return;

        // 레벨 성장 × 별 보너스
        float levelMult = 1f + statGrowthRate * (_level - 1);
        float starMult = 1f + starStatBonus * (_stars - 1);
        float totalMult = levelMult * starMult;

        float atk = _companion.attackPower * totalMult;
        float hp = atk * 10f;              // TODO: CompanionData에 hp 필드 추가 시 교체
        float atkSpd = _companion.attackSpeed;
        float critRate = 5f + _level * 0.5f + _stars * 1f;
        float critDmg = 150f + _level * 2f + _stars * 5f;
        float skillDmg = 100f + _level * 3f + _stars * 5f;

        if (atkValueText != null) atkValueText.text = FormatStat(atk);
        if (hpValueText != null) hpValueText.text = FormatStat(hp);
        if (atkSpdValueText != null) atkSpdValueText.text = $"{1f / atkSpd:F2}초";
        if (critRateValueText != null) critRateValueText.text = $"{critRate:F2}%";
        if (critDmgValueText != null) critDmgValueText.text = $"{critDmg:F0}%";
        if (skillDmgValueText != null) skillDmgValueText.text = $"{skillDmg:F0}%";
    }

    // ─────────────────────────────────────────────────────────────
    //  우측: 추가 효과 리스트
    // ─────────────────────────────────────────────────────────────

    private void RefreshBonusList()
    {
        if (bonusListContent == null) return;

        // 기존 행 제거
        foreach (Transform child in bonusListContent)
            Destroy(child.gameObject);

        if (_companion == null || bonusRowPrefab == null) return;

        // 별 등급 보너스
        if (_stars >= 3)
            AddBonusRow("골드 획득량 증가", $"+{(_stars - 2) * 1.5f:F2}%");
        if (_stars >= 5)
            AddBonusRow("스킬 쿨타임 감소", $"-{(_stars - 4) * 1f:F2}%");
        if (_stars >= 7)
            AddBonusRow("물리 방어력", $"+{(_stars - 6) * 1.5f:F2}%");
        if (_stars >= 9)
            AddBonusRow("경험치 획득 보너스", $"+{(_stars - 8) * 2f:F2}%");
        if (_stars >= 11)
            AddBonusRow("전 스탯 추가 보너스", $"+{(_stars - 10) * 5f:F0}%");

        // 레어리티 기본 보너스
        switch (_companion.rarity)
        {
            case CompanionRarity.Rare:
                AddBonusRow("기본 골드 보너스", "+1.00%");
                break;
            case CompanionRarity.Epic:
                AddBonusRow("기본 골드 보너스", "+2.00%");
                AddBonusRow("기본 쿨타임 감소", "-1.00%");
                break;
            case CompanionRarity.Legendary:
                AddBonusRow("기본 골드 보너스", "+3.00%");
                AddBonusRow("기본 쿨타임 감소", "-2.00%");
                AddBonusRow("기본 방어력 보너스", "+1.50%");
                break;
        }

        // 스킬 보너스
        if (_companion.skills != null)
        {
            foreach (var skill in _companion.skills)
            {
                if (skill == null) continue;
                AddBonusRow($">> {skill.skillName}", skill.description ?? "");
            }
        }
    }

    private void AddBonusRow(string label, string value)
    {
        if (bonusRowPrefab == null || bonusListContent == null) return;

        GameObject row = Instantiate(bonusRowPrefab, bonusListContent);
        var texts = row.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length > 0) texts[0].text = label;
        if (texts.Length > 1) texts[1].text = value;
    }

    // ─────────────────────────────────────────────────────────────
    //  데이터 연동
    // ─────────────────────────────────────────────────────────────

    private int GetCompanionStars(CompanionData data)
    {
        if (data == null) return 1;
        var invMgr = CompanionInventoryManager.Instance;
        if (invMgr == null) return data.baseStars;

        var saveList = invMgr.GetSaveData();
        if (saveList == null) return data.baseStars;

        foreach (var s in saveList)
        {
            if (s != null && s.companionID == data.companionID)
                return s.stars > 0 ? s.stars : data.baseStars;
        }
        return data.baseStars;
    }

    // ─────────────────────────────────────────────────────────────
    //  유틸리티
    // ─────────────────────────────────────────────────────────────

    private string FormatStat(float value)
    {
        if (value >= 100000000f) return $"{value / 100000000f:F1}억";
        if (value >= 10000f) return $"{value / 10000f:F0}만";
        return $"{value:N0}";
    }

    private string MakeStarString(int stars)
    {
        if (stars <= 0) return StarSpriteUtil.GetStars(1);
        return StarSpriteUtil.GetColoredStars(stars);
    }


    private string GetRarityName(CompanionRarity rarity)
    {
        switch (rarity)
        {
            case CompanionRarity.Common:    return "일반";
            case CompanionRarity.Rare:      return "희귀";
            case CompanionRarity.Epic:      return "에픽";
            case CompanionRarity.Legendary: return "전설";
            default: return "일반";
        }
    }

    private Color GetRarityColor(CompanionRarity rarity)
    {
        switch (rarity)
        {
            case CompanionRarity.Common:    return Color.gray;
            case CompanionRarity.Rare:      return new Color(0.3f, 0.5f, 1f);
            case CompanionRarity.Epic:      return new Color(0.7f, 0.2f, 0.9f);
            case CompanionRarity.Legendary: return new Color(1f, 0.8f, 0.2f);
            default: return Color.white;
        }
    }
}
