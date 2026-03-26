using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ══════════════════════════════════════════════════════════════════
/// RankListSlot — 4위 이하 랭킹 리스트 슬롯
/// ══════════════════════════════════════════════════════════════════
///
/// ★ 프리팹 Hierarchy 구조:
///   RankListSlot (이 스크립트 + HorizontalLayoutGroup)
///     ├── Background          (Image)           ← backgroundImage (라운드 카드 배경)
///     ├── RankText            (TMP_Text)        ← rankText (순위 숫자)
///     ├── CharacterArea       (빈 오브젝트)
///     │   ├── CharacterFrame  (Image)           ← characterFrame (원형 프레임)
///     │   └── CharacterImage  (Image + Mask)    ← characterImage (원형 프로필)
///     ├── InfoArea            (빈 오브젝트, VerticalLayoutGroup)
///     │   ├── NicknameText    (TMP_Text)        ← nicknameText
///     │   └── ScoreText       (TMP_Text)        ← scoreText (전투력 K단위)
///     └── SubScoreText        (TMP_Text)        ← subScoreText (St.XXX, 우측 정렬)
///
/// ★ 동작:
///   - SetData():   일반 플레이어 행 (홀짝 행 색상 교대)
///   - SetMyData(): 내 순위 행 (빨간 배경 + 닉네임 강조)
///   - Setup():     RankingManager 내부 호출용 (RankEntry 기반)
///
/// ★ 프리팹 저장 위치: Assets/Prefabs/RankListSlot.prefab
/// ══════════════════════════════════════════════════════════════════
/// </summary>
public class RankListSlot : MonoBehaviour
{
    // ═══ Inspector 필드 ═════════════════════════════════════════

    [Header("순위")]
    [Tooltip("순위 숫자 텍스트 (4, 5, 6...)")]
    public TextMeshProUGUI rankText;

    [Header("캐릭터")]
    [Tooltip("원형 프로필 이미지")]
    public Image characterImage;
    [Tooltip("원형 프레임 이미지")]
    public Image characterFrame;

    [Header("정보")]
    [Tooltip("닉네임 텍스트")]
    public TextMeshProUGUI nicknameText;
    [Tooltip("전투력 텍스트 (K단위 표시)")]
    public TextMeshProUGUI scoreText;
    [Tooltip("점수 텍스트 (St.XXX 스타일, 우측)")]
    public TextMeshProUGUI subScoreText;

    [Header("배경")]
    [Tooltip("행 배경 이미지 (홀짝 색상 교대)")]
    public Image backgroundImage;

    // ═══ 행 색상 설정 ═══════════════════════════════════════════

    [Header("행 색상")]
    [Tooltip("홀수 행 배경색 (0, 2, 4...)")]
    [SerializeField] private Color oddRowColor = new Color(0.18f, 0.2f, 0.28f, 0.9f);
    [Tooltip("짝수 행 배경색 (1, 3, 5...)")]
    [SerializeField] private Color evenRowColor = new Color(0.14f, 0.16f, 0.22f, 0.9f);
    [Tooltip("내 순위 행 배경색 (빨간 강조)")]
    [SerializeField] private Color myRowColor = new Color(0.95f, 0.25f, 0.3f, 0.35f);

    [Header("텍스트 색상")]
    [Tooltip("일반 플레이어 닉네임 색상")]
    [SerializeField] private Color normalNameColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [Tooltip("내 닉네임 강조 색상 (밝은 노란색)")]
    [SerializeField] private Color myNameColor = new Color(1f, 0.92f, 0.5f, 1f);
    [Tooltip("상위 순위 (4~10위) 순위 텍스트 색상")]
    [SerializeField] private Color highRankColor = new Color(1f, 0.85f, 0.3f, 1f);
    [Tooltip("일반 순위 텍스트 색상")]
    [SerializeField] private Color normalRankColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    [Tooltip("프레임 기본 색상")]
    [SerializeField] private Color normalFrameColor = new Color(0.5f, 0.5f, 0.55f, 1f);
    [Tooltip("내 프레임 강조 색상")]
    [SerializeField] private Color myFrameColor = new Color(1f, 0.85f, 0.3f, 1f);

    // ═══ 내부 상태 ═════════════════════════════════════════════

    /// <summary>현재 이 행의 rowIndex (홀짝 색상 계산용)</summary>
    private int cachedRowIndex;

    // ═══════════════════════════════════════════════════════════════
    //  Unity 생명주기
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// RectTransform 크기 검증 — Width/Height가 0이면 NaN 발생하므로 기본값 보장.
    /// Instantiate 직후 부모 Layout이 아직 계산 안 됐을 때 방지.
    /// </summary>
    void Awake()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector2 size = rt.sizeDelta;
            // Stretch 앵커가 아닌데 크기가 0이면 기본값 강제
            if (size.x == 0f && Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x))
                size.x = 700f;
            if (size.y == 0f && Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y))
                size.y = 80f;
            rt.sizeDelta = size;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  공개 메서드 — 외부 호출용 (단순 데이터)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 일반 플레이어 데이터 세팅
    /// </summary>
    /// <param name="rank">순위 (4~)</param>
    /// <param name="nickname">닉네임</param>
    /// <param name="score">메인 점수 (전투력 등, K단위 변환 표시)</param>
    /// <param name="subScore">보조 점수 (St.XXX 표시)</param>
    /// <param name="characterSprite">캐릭터 프로필 이미지</param>
    public void SetData(int rank, string nickname, long score, long subScore, Sprite characterSprite)
    {
        gameObject.SetActive(true);

        // 순위
        if (rankText != null)
        {
            rankText.text = rank.ToString();
            rankText.color = (rank <= 10) ? highRankColor : normalRankColor;
        }

        // 캐릭터 이미지
        SetCharacterSprite(characterSprite);

        // 프레임 (일반)
        if (characterFrame != null)
            characterFrame.color = normalFrameColor;

        // 닉네임
        if (nicknameText != null)
        {
            nicknameText.text = nickname ?? "---";
            nicknameText.color = normalNameColor;
            nicknameText.fontStyle = FontStyles.Normal;
        }

        // 전투력 (K단위)
        if (scoreText != null)
            scoreText.text = RankingFormatUtil.FormatPowerShort((int)score);

        // 보조 점수 (St.XXX)
        if (subScoreText != null)
            subScoreText.text = $"St.{RankingFormatUtil.FormatK((int)subScore)}";

        // 배경 (홀짝)
        ApplyRowBackground(rank, false);
    }

    /// <summary>
    /// 내 순위 강조 세팅 (빨간 배경 + "나" 표시 + 닉네임 강조)
    /// </summary>
    /// <param name="rank">순위</param>
    /// <param name="nickname">닉네임</param>
    /// <param name="score">메인 점수</param>
    /// <param name="subScore">보조 점수</param>
    /// <param name="characterSprite">캐릭터 프로필 이미지</param>
    public void SetMyData(int rank, string nickname, long score, long subScore, Sprite characterSprite)
    {
        gameObject.SetActive(true);

        // 순위 (강조)
        if (rankText != null)
        {
            rankText.text = rank.ToString();
            rankText.color = myNameColor;
        }

        // 캐릭터 이미지
        SetCharacterSprite(characterSprite);

        // 프레임 (강조)
        if (characterFrame != null)
            characterFrame.color = myFrameColor;

        // 닉네임 (강조 + Bold)
        if (nicknameText != null)
        {
            nicknameText.text = nickname ?? "나";
            nicknameText.color = myNameColor;
            nicknameText.fontStyle = FontStyles.Bold;
        }

        // 전투력 (K단위)
        if (scoreText != null)
            scoreText.text = RankingFormatUtil.FormatPowerShort((int)score);

        // 보조 점수 (St.XXX)
        if (subScoreText != null)
            subScoreText.text = $"St.{RankingFormatUtil.FormatK((int)subScore)}";

        // 배경 (내 순위 강조색)
        ApplyRowBackground(rank, true);
    }

    // ═══════════════════════════════════════════════════════════════
    //  내부 호출용 — RankingManager.SpawnEntry()에서 호출
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// RankingManager 내부 호출용 (RankEntry 기반)
    /// </summary>
    /// <param name="rank">순위 (4~)</param>
    /// <param name="entry">랭킹 엔트리 데이터</param>
    /// <param name="classIcon">직업 아이콘 스프라이트</param>
    /// <param name="rankType">현재 랭킹 탭 타입</param>
    /// <param name="rowIndex">리스트 내 행 인덱스 (0-based, 홀짝 색상용)</param>
    /// <param name="combatPower">전투력 (K단위 표시용)</param>
    public void Setup(int rank, RankingManager.RankEntry entry,
                      Sprite classIcon, RankingManager.RankType rankType,
                      int rowIndex, int combatPower)
    {
        if (entry == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        cachedRowIndex = rowIndex;

        // ── 순위 텍스트 ──
        if (rankText != null)
        {
            rankText.text = rank.ToString();
            rankText.color = (rank <= 10) ? highRankColor : normalRankColor;
            if (entry.isMe) rankText.color = myNameColor;
        }

        // ── 캐릭터 아이콘 ──
        SetCharacterSprite(classIcon);

        // ── 프레임 색상 ──
        if (characterFrame != null)
            characterFrame.color = entry.isMe ? myFrameColor : normalFrameColor;

        // ── 닉네임 ──
        if (nicknameText != null)
        {
            nicknameText.text = entry.playerName ?? "---";
            nicknameText.fontStyle = entry.isMe ? FontStyles.Bold : FontStyles.Normal;
            nicknameText.color = entry.isMe ? myNameColor : normalNameColor;
        }

        // ── 전투력 (K 단위) ──
        if (scoreText != null)
            scoreText.text = RankingFormatUtil.FormatPowerShort(combatPower);

        // ── 점수 (St.XXX / Lv.XX) ──
        if (subScoreText != null)
            subScoreText.text = RankingFormatUtil.FormatScoreWithPrefix(entry.score, rankType);

        // ── 배경 (홀짝 / 내 순위) ──
        ApplyRowBackground(rowIndex, entry.isMe);
    }

    // ═══════════════════════════════════════════════════════════════
    //  내부 헬퍼
    // ═══════════════════════════════════════════════════════════════

    /// <summary>캐릭터 이미지 설정 (null이면 비활성)</summary>
    private void SetCharacterSprite(Sprite sprite)
    {
        if (characterImage == null) return;

        if (sprite != null)
        {
            characterImage.sprite = sprite;
            characterImage.color = Color.white;
            characterImage.enabled = true;
        }
        else
        {
            characterImage.enabled = false;
        }
    }

    /// <summary>행 배경 색상 적용 (홀짝 교대 / 내 순위 강조)</summary>
    private void ApplyRowBackground(int rowIndex, bool isMe)
    {
        if (backgroundImage == null) return;

        if (isMe)
        {
            backgroundImage.color = myRowColor;
        }
        else
        {
            backgroundImage.color = (rowIndex % 2 == 0) ? evenRowColor : oddRowColor;
        }
    }
}
