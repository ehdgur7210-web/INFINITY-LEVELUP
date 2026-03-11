using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 주요 광물/티켓을 표시하는 상단 리소스 바
/// 가챠 티켓, 동료 티켓, 유물 티켓 등을 항상 표시
/// </summary>
public class ResourceBarManager : MonoBehaviour
{
    public static ResourceBarManager Instance;

    [Header("패널 UI")]
    [SerializeField] private GameObject penal;
    [SerializeField] private Button Togl;


    [Header("티켓 UI")]
    [SerializeField] private TextMeshProUGUI equipmentTicketText;   // 장비 뽑기 티켓
    [SerializeField] private TextMeshProUGUI companionTicketText;   // 동료 뽑기 티켓
    [SerializeField] private TextMeshProUGUI relicTicketText;       // 유물 뽑기 티켓

    [Header("티켓 아이콘 (선택)")]
    [SerializeField] private Image equipmentTicketIcon;
    [SerializeField] private Image companionTicketIcon;
    [SerializeField] private Image relicTicketIcon;

    [Header("광물 UI (추가 광물)")]
    [SerializeField] private TextMeshProUGUI crystalText;           // 크리스탈
    [SerializeField] private TextMeshProUGUI essenceText;           // 에센스
    [SerializeField] private TextMeshProUGUI fragmentText;          // 파편

    [Header("작물 포인트 UI")]
    [SerializeField] private TextMeshProUGUI cropPointText;         // ★ 작물 포인트

    [Header("광물 아이콘 (선택)")]
    [SerializeField] private Image crystalIcon;
    [SerializeField] private Image essenceIcon;
    [SerializeField] private Image fragmentIcon;

    [Header("현재 보유량")]
    public int equipmentTickets = 100;      // 장비 뽑기 티켓
    public int companionTickets = 50;       // 동료 뽑기 티켓
    public int relicTickets = 30;           // 유물 뽑기 티켓
    public int crystals = 0;                // 크리스탈
    public int essences = 0;                // 에센스
    public int fragments = 0;               // 파편
    public int cropPoints = 0;              // ★ 작물 포인트

    [Header("애니메이션 설정")]
    [SerializeField] private bool enablePunchAnimation = true;
    [SerializeField] private float punchScale = 1.1f;
    [SerializeField] private float animationDuration = 0.2f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // ★ FarmScene → MainScene 전환 시 저장된 cropPoints 복원
        cropPoints = GameDataBridge.CurrentData?.farmData?.cropPoints ?? 0;

        UpdateAllResourceUI();

        // ★ FarmScene에서 실시간 변경 시 즉시 반영
        FarmManagerExtension.OnCropPointsChanged += OnCropPointsChanged;

        // 토글 버튼 이벤트 연결 추가
        if (Togl != null)
        {
            Togl.onClick.AddListener(ToggleResourceBar);
            Debug.Log("[ResourceBar] 토글 버튼 이벤트 연결됨!");
        }
        else
        {
            Debug.LogError("[ResourceBar] Togl 버튼이 null입니다! Inspector에서 연결하세요!");
        }

        // 시작 시 패널 표시
        HidePenal();
    }

    public void HidePenal()
    {
        if (penal != null)
        {
            penal.SetActive(false);
        }


    }

    public void ShowPenal()
    {
        penal.SetActive(true);
    }

    public void ToggleResourceBar()
    {
        // ★ 리소스 바 토글 버튼 효과음
        SoundManager.Instance?.PlayButtonClick();
        if (penal != null)
        {
            bool isActive = penal.activeSelf;
            if (isActive) HidePenal();
            else ShowPenal();
        }
    }
    public bool IsBarVisible()
    {
        return penal != null && penal.activeSelf;
    }
    #region 티켓 관리

    /// <summary>
    /// 장비 티켓 추가
    /// </summary>
    public void AddEquipmentTickets(int amount)
    {
        equipmentTickets += amount;
        UpdateEquipmentTicketUI();

        if (enablePunchAnimation && equipmentTicketText != null)
        {
            AnimateText(equipmentTicketText.transform);
        }

        Debug.Log($"[ResourceBar] 장비 티켓 +{amount} (현재: {equipmentTickets})");
    }

    /// <summary>
    /// 장비 티켓 사용
    /// </summary>
    public bool SpendEquipmentTickets(int amount)
    {
        if (equipmentTickets < amount)
        {
            Debug.LogWarning($"[ResourceBar] 장비 티켓 부족! (필요: {amount}, 보유: {equipmentTickets})");
            return false;
        }

        equipmentTickets -= amount;
        UpdateEquipmentTicketUI();

        Debug.Log($"[ResourceBar] 장비 티켓 -{amount} (현재: {equipmentTickets})");
        return true;
    }

    /// <summary>
    /// 동료 티켓 추가
    /// </summary>
    public void AddCompanionTickets(int amount)
    {
        companionTickets += amount;
        UpdateCompanionTicketUI();

        if (enablePunchAnimation && companionTicketText != null)
        {
            AnimateText(companionTicketText.transform);
        }

        Debug.Log($"[ResourceBar] 동료 티켓 +{amount} (현재: {companionTickets})");
    }

    /// <summary>
    /// 동료 티켓 사용
    /// </summary>
    public bool SpendCompanionTickets(int amount)
    {
        if (companionTickets < amount)
        {
            Debug.LogWarning($"[ResourceBar] 동료 티켓 부족! (필요: {amount}, 보유: {companionTickets})");
            return false;
        }

        companionTickets -= amount;
        UpdateCompanionTicketUI();

        Debug.Log($"[ResourceBar] 동료 티켓 -{amount} (현재: {companionTickets})");
        return true;
    }

    /// <summary>
    /// 유물 티켓 추가
    /// </summary>
    public void AddRelicTickets(int amount)
    {
        relicTickets += amount;
        UpdateRelicTicketUI();

        if (enablePunchAnimation && relicTicketText != null)
        {
            AnimateText(relicTicketText.transform);
        }

        Debug.Log($"[ResourceBar] 유물 티켓 +{amount} (현재: {relicTickets})");
    }

    /// <summary>
    /// 유물 티켓 사용
    /// </summary>
    public bool SpendRelicTickets(int amount)
    {
        if (relicTickets < amount)
        {
            Debug.LogWarning($"[ResourceBar] 유물 티켓 부족! (필요: {amount}, 보유: {relicTickets})");
            return false;
        }

        relicTickets -= amount;
        UpdateRelicTicketUI();

        Debug.Log($"[ResourceBar] 유물 티켓 -{amount} (현재: {relicTickets})");
        return true;
    }

    #endregion

    #region 광물 관리

    /// <summary>
    /// 크리스탈 추가
    /// </summary>
    public void AddCrystals(int amount)
    {
        crystals += amount;
        UpdateCrystalUI();

        if (enablePunchAnimation && crystalText != null)
        {
            AnimateText(crystalText.transform);
        }
    }

    /// <summary>
    /// 크리스탈 사용
    /// </summary>
    public bool SpendCrystals(int amount)
    {
        if (crystals < amount) return false;

        crystals -= amount;
        UpdateCrystalUI();
        return true;
    }

    /// <summary>
    /// 에센스 추가
    /// </summary>
    public void AddEssences(int amount)
    {
        essences += amount;
        UpdateEssenceUI();

        if (enablePunchAnimation && essenceText != null)
        {
            AnimateText(essenceText.transform);
        }
    }

    /// <summary>
    /// 에센스 사용
    /// </summary>
    public bool SpendEssences(int amount)
    {
        if (essences < amount) return false;

        essences -= amount;
        UpdateEssenceUI();
        return true;
    }

    /// <summary>
    /// 파편 추가
    /// </summary>
    public void AddFragments(int amount)
    {
        fragments += amount;
        UpdateFragmentUI();

        if (enablePunchAnimation && fragmentText != null)
        {
            AnimateText(fragmentText.transform);
        }
    }

    /// <summary>
    /// 파편 사용
    /// </summary>
    public bool SpendFragments(int amount)
    {
        if (fragments < amount) return false;

        fragments -= amount;
        UpdateFragmentUI();
        return true;
    }

    #endregion

    #region UI 업데이트

    public void UpdateEquipmentTicketUI()
    {
        if (equipmentTicketText != null)
        {
            equipmentTicketText.text = FormatNumber(equipmentTickets);
        }
    }

    private void UpdateCompanionTicketUI()
    {
        if (companionTicketText != null)
        {
            companionTicketText.text = FormatNumber(companionTickets);
        }
    }

    private void UpdateRelicTicketUI()
    {
        if (relicTicketText != null)
        {
            relicTicketText.text = FormatNumber(relicTickets);
        }
    }

    private void UpdateCrystalUI()
    {
        if (crystalText != null)
        {
            crystalText.text = FormatNumber(crystals);
        }
    }

    private void UpdateEssenceUI()
    {
        if (essenceText != null)
        {
            essenceText.text = FormatNumber(essences);
        }
    }

    private void UpdateFragmentUI()
    {
        if (fragmentText != null)
        {
            fragmentText.text = FormatNumber(fragments);
        }
    }

    /// <summary>
    /// 모든 리소스 UI 업데이트
    /// </summary>
    public void UpdateAllResourceUI()
    {
        UpdateEquipmentTicketUI();
        UpdateCompanionTicketUI();
        UpdateRelicTicketUI();
        UpdateCrystalUI();
        UpdateEssenceUI();
        UpdateFragmentUI();
        UpdateCropPointUI(); // ★
    }

    // ★ 작물 포인트 UI 갱신
    private void UpdateCropPointUI()
    {
        if (cropPointText != null)
            cropPointText.text = $"🌱 {FormatNumber(cropPoints)}";
    }

    // ★ FarmManagerExtension 이벤트 수신
    private void OnCropPointsChanged(int amount)
    {
        cropPoints = amount;
        UpdateCropPointUI();
        if (enablePunchAnimation && cropPointText != null)
            AnimateText(cropPointText.transform);
    }

    // ★ 외부에서 직접 추가할 때 (퀘스트 보상 등)
    public void AddCropPoints(int amount)
    {
        cropPoints += amount;
        UpdateCropPointUI();
        if (enablePunchAnimation && cropPointText != null)
            AnimateText(cropPointText.transform);
    }

    public void SetCropPoints(int amount)
    {
        cropPoints = amount;
        UpdateCropPointUI();
    }

    private void OnDestroy()
    {
        FarmManagerExtension.OnCropPointsChanged -= OnCropPointsChanged;
    }

    public int GetEquipmentTickets()
    {
        return equipmentTickets; // 내부 필드명에 맞게 수정
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// 숫자 포맷팅 (천 단위 콤마)
    /// </summary>
    private string FormatNumber(int number)
    {
        if (number >= 1000000)
        {
            return $"{number / 1000000f:F1}M";
        }
        else if (number >= 1000)
        {
            return $"{number / 1000f:F1}K";
        }
        else
        {
            return number.ToString();
        }
    }

    /// <summary>
    /// 텍스트 펀치 애니메이션
    /// </summary>
    private void AnimateText(Transform textTransform)
    {
        if (textTransform == null) return;

        StopAllCoroutines();
        StartCoroutine(PunchAnimation(textTransform));
    }

    private System.Collections.IEnumerator PunchAnimation(Transform target)
    {
        Vector3 originalScale = target.localScale;
        Vector3 targetScale = originalScale * punchScale;

        float elapsed = 0f;

        // 확대
        while (elapsed < animationDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (animationDuration / 2f);
            target.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        elapsed = 0f;

        // 축소
        while (elapsed < animationDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (animationDuration / 2f);
            target.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }

        target.localScale = originalScale;
    }

    #endregion

    #region 체크 메서드

    public bool HasEquipmentTickets(int amount) => equipmentTickets >= amount;
    public bool HasCompanionTickets(int amount) => companionTickets >= amount;
    public bool HasRelicTickets(int amount) => relicTickets >= amount;
    public bool HasCrystals(int amount) => crystals >= amount;
    public bool HasEssences(int amount) => essences >= amount;
    public bool HasFragments(int amount) => fragments >= amount;

    #endregion

    #region 디버그

    [ContextMenu("Add 100 Equipment Tickets")]
    private void DebugAddEquipmentTickets()
    {
        AddEquipmentTickets(100);
    }

    [ContextMenu("Add 100 Companion Tickets")]
    private void DebugAddCompanionTickets()
    {
        AddCompanionTickets(100);
    }

    [ContextMenu("Add 100 Relic Tickets")]
    private void DebugAddRelicTickets()
    {
        AddRelicTickets(100);
    }

    #endregion
}