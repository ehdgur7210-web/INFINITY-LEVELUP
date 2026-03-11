using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CompanionGachaManager
/// ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
/// µ¿·á »Ì±â ½Ã½ºÅÛ
///
/// [±â´É]
///   - µ¿·á ÀÌº¥Æ® ¹öÆ° ¡æ µ¿·á »Ì±â ÆÐ³Î ¿ÀÇÂ
///   - 1È¸ »Ì±â / 10È¸ »Ì±â ¹öÆ°
///   - °á°ú È­¸é: »ÌÀº µ¿·á ÀÌ¹ÌÁö ³ª¿­
///   - °á°ú È­¸é ¿ÜºÎ Å¬¸¯ ¡æ ÆÐ³Î ´Ý±â
///   - ÀÌ¹ÌÁö Å¬¸¯ ¡æ µ¿·á ¼³¸í ÆË¾÷
///   - »ÌÀº µ¿·á ¡æ CompanionInventoryManager¿¡ ÀúÀå
///
/// [ºñ¿ë]
///   - ResourceBarManager.SpendEquipmentTickets() »ç¿ë
///   - 1È¸: singlePullCost, 10È¸: tenPullCost
///
/// [Inspector ¿¬°á]
///   companionGachaPanel  : µ¿·á »Ì±â ¼±ÅÃ ÆÐ³Î
///   resultPanel          : °á°ú Ç¥½Ã ÆÐ³Î
///   resultGrid           : °á°ú ÀÌ¹ÌÁö GridLayoutGroup
///   resultItemPrefab     : °á°ú °³º° ¾ÆÀÌÅÛ ÇÁ¸®ÆÕ
///   detailPopup          : µ¿·á »ó¼¼ ÆË¾÷
///   companionPool        : »Ì±â °¡´ÉÇÑ µ¿·á ¸ñ·Ï (CompanionData)
/// </summary>
public class CompanionGachaManager : MonoBehaviour
{
    public static CompanionGachaManager Instance;

    // ¦¡¦¡¦¡ µ¿·á Ç® ¦¡¦¡¦¡
    [Header("µ¿·á »Ì±â Ç®")]
    public List<CompanionData> companionPool = new List<CompanionData>();

    // ¦¡¦¡¦¡ ºñ¿ë ¦¡¦¡¦¡
    [Header("»Ì±â ºñ¿ë (Àåºñ Æ¼ÄÏ °ø¿ë)")]
    public int singlePullCost = 1;
    public int tenPullCost = 10;

    // ¦¡¦¡¦¡ »Ì±â UI ¦¡¦¡¦¡
    [Header("µ¿·á »Ì±â ÆÐ³Î")]
    public GameObject companionGachaPanel;
    public Button singlePullBtn;
    public Button tenPullBtn;
    public Button closePanelBtn;
    public TextMeshProUGUI ticketCountText;   // º¸À¯ Æ¼ÄÏ ¼ö

    // ¦¡¦¡¦¡ °á°ú È­¸é ¦¡¦¡¦¡
    [Header("°á°ú È­¸é")]
    public GameObject resultPanel;
    public Transform resultGrid;
    public GameObject resultItemPrefab;
    public Button resultCloseBtn;
    public GameObject resultBackground;      // ¿ÜºÎ Å¬¸¯ °¨Áö¿ë ÀüÃ¼ ¹è°æ ¹öÆ°

    // ¦¡¦¡¦¡ µ¿·á »ó¼¼ ÆË¾÷ ¦¡¦¡¦¡
    [Header("µ¿·á »ó¼¼ ÆË¾÷")]
    public GameObject detailPopup;
    public Image detailPortrait;
    public TextMeshProUGUI detailNameText;
    public TextMeshProUGUI detailRarityText;
    public TextMeshProUGUI detailDescText;
    public TextMeshProUGUI detailStatsText;
    public Button detailCloseBtn;
    public Button detailAddToHotbarBtn;

    // ¦¡¦¡¦¡ ³»ºÎ ¦¡¦¡¦¡
    private CompanionData selectedCompanion;

    private readonly Color[] rarityColors =
    {
        Color.gray,
        new Color(0.3f, 0.5f, 1f),
        new Color(0.7f, 0.2f, 1f),
        new Color(1f,   0.8f, 0.1f)
    };

    private readonly string[] rarityNames = { "ÀÏ¹Ý", "Èñ±Í", "¿µ¿õ", "Àü¼³" };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        SetupUI();
        CloseAll();
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  UI ÃÊ±âÈ­
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    private void SetupUI()
    {
        if (singlePullBtn != null) singlePullBtn.onClick.AddListener(PerformSinglePull);
        if (tenPullBtn != null) tenPullBtn.onClick.AddListener(PerformTenPull);
        if (closePanelBtn != null) closePanelBtn.onClick.AddListener(CloseGachaPanel);
        if (resultCloseBtn != null) resultCloseBtn.onClick.AddListener(CloseResultPanel);

        if (resultBackground != null)
        {
            Button bgBtn = resultBackground.GetComponent<Button>()
                           ?? resultBackground.AddComponent<Button>();
            bgBtn.onClick.AddListener(CloseResultPanel);
        }

        if (detailCloseBtn != null) detailCloseBtn.onClick.AddListener(CloseDetailPopup);
        if (detailAddToHotbarBtn != null) detailAddToHotbarBtn.onClick.AddListener(AddSelectedToHotbar);
    }

    private void CloseAll()
    {
        if (companionGachaPanel != null) companionGachaPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (detailPopup != null) detailPopup.SetActive(false);
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  ÆÐ³Î ¿­±â / ´Ý±â
    //  µ¿·á ÀÌº¥Æ® ¹öÆ°¿¡ onClick ¿¬°á: CompanionGachaManager.Instance.OpenGachaPanel()
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    public void OpenGachaPanel()
    {
        if (companionGachaPanel == null) return;
        companionGachaPanel.SetActive(true);
        RefreshTicketUI();
        Debug.Log("[CompanionGachaManager] µ¿·á »Ì±â ÆÐ³Î ¿ÀÇÂ");
    }

    public void CloseGachaPanel()
    {
        if (companionGachaPanel != null) companionGachaPanel.SetActive(false);
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  Æ¼ÄÏ UI °»½Å
    //  ¡Ú ResourceBarManager¿¡ GetEquipmentTickets() °¡ ¾øÀ» °æ¿ì
    //    HasEquipmentTickets()·Î ÃÖ¼Ò/ÃÖ´ë Ç¥½Ã
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    private void RefreshTicketUI()
    {
        if (ticketCountText == null) return;

        if (ResourceBarManager.Instance == null)
        {
            ticketCountText.text = "Æ¼ÄÏ: -";
            return;
        }

        // GetEquipmentTickets() °¡ ÀÖÀ¸¸é Á÷Á¢ »ç¿ë, ¾øÀ¸¸é ¼ÒÀ¯ ¿©ºÎ¸¸ Ç¥½Ã
        // (ResourceBarManager¿¡ ¸Þ¼­µå°¡ ÀÖ´Ù°í °¡Á¤. ¾øÀ¸¸é ¾Æ·¡ ÁÖ¼® ÂüÁ¶)
        int tickets = GetTicketCount();
        ticketCountText.text = $"Æ¼ÄÏ: {tickets}";
    }

    /// <summary>
    /// ResourceBarManager¿¡¼­ Æ¼ÄÏ ¼ö·® °¡Á®¿À±â.
    /// GetEquipmentTickets() °¡ ¾øÀ¸¸é Inspector¿¡¼­ Á÷Á¢ °ü¸®ÇÏ´Â º¯¼ö·Î Æú¹é.
    /// </summary>
    private int GetTicketCount()
    {
        if (ResourceBarManager.Instance == null) return 0;

        // ResourceBarManager¿¡ GetEquipmentTickets() ÀÌ ÀÖÀ¸¸é »ç¿ë
        // (ÄÄÆÄÀÏ ¿¡·¯ ½Ã ¾Æ·¡ µÎ ÁÙÀ» ÁÖ¼®Ã³¸®ÇÏ°í fallback¸¸ »ç¿ë)
        try
        {
            return ResourceBarManager.Instance.GetEquipmentTickets();
        }
        catch
        {
            // GetEquipmentTickets()°¡ ¾ø´Â °æ¿ì Æú¹é:
            // º¸À¯ ¿©ºÎ¸¸ Ã¼Å©ÇØ¼­ "ÃæºÐÇÔ" / "ºÎÁ·" Ç¥½Ã
            return ResourceBarManager.Instance.HasEquipmentTickets(singlePullCost) ? singlePullCost : 0;
        }
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  »Ì±â ·ÎÁ÷
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    public void PerformSinglePull()
    {
        if (!SpendTickets(singlePullCost)) return;

        CompanionData result = DrawCompanion();
        if (result == null) return;

        CompanionInventoryManager.Instance?.AddCompanion(result);
        ShowResult(new List<CompanionData> { result });

        Debug.Log($"[CompanionGachaManager] 1È¸ °á°ú: {result.companionName} ({result.rarity})");
    }

    public void PerformTenPull()
    {
        if (!SpendTickets(tenPullCost)) return;

        List<CompanionData> results = new List<CompanionData>();
        for (int i = 0; i < 10; i++)
        {
            CompanionData r = DrawCompanion();
            if (r == null) continue;
            results.Add(r);
            CompanionInventoryManager.Instance?.AddCompanion(r);
        }

        ShowResult(results);
        Debug.Log($"[CompanionGachaManager] 10È¸ ¿Ï·á ({results.Count}°³)");
    }

    // ¦¡¦¡¦¡ È®·ü °¡Áß »Ì±â ¦¡¦¡¦¡
    private CompanionData DrawCompanion()
    {
        if (companionPool == null || companionPool.Count == 0)
        {
            UIManager.Instance?.ShowMessage("µ¿·á Ç®ÀÌ ºñ¾îÀÖ½À´Ï´Ù!", Color.red);
            return null;
        }

        float total = 0f;
        foreach (var c in companionPool)
            if (c != null) total += c.probability;

        if (total <= 0f)
            return companionPool[Random.Range(0, companionPool.Count)];

        float roll = Random.Range(0f, total);
        float cumul = 0f;

        foreach (var c in companionPool)
        {
            if (c == null) continue;
            cumul += c.probability;
            if (roll <= cumul) return c;
        }

        return companionPool[companionPool.Count - 1];
    }

    // ¦¡¦¡¦¡ Æ¼ÄÏ ¼Ò¸ð ¦¡¦¡¦¡
    private bool SpendTickets(int cost)
    {
        if (ResourceBarManager.Instance == null)
        {
            Debug.LogError("[CompanionGachaManager] ResourceBarManager ¾øÀ½!");
            return false;
        }

        if (!ResourceBarManager.Instance.SpendEquipmentTickets(cost))
        {
            UIManager.Instance?.ShowMessage($"Æ¼ÄÏ {cost}°³ ÇÊ¿ä!", Color.red);
            return false;
        }

        RefreshTicketUI();
        return true;
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  °á°ú È­¸é
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    private void ShowResult(List<CompanionData> results)
    {
        if (resultPanel == null || resultGrid == null) return;

        foreach (Transform child in resultGrid)
            Destroy(child.gameObject);

        foreach (var companion in results)
        {
            if (companion == null || resultItemPrefab == null) continue;

            GameObject go = Instantiate(resultItemPrefab, resultGrid);
            CompanionResultItem item = go.GetComponent<CompanionResultItem>()
                                      ?? go.AddComponent<CompanionResultItem>();
            item.Setup(companion, this);
        }

        CloseGachaPanel();
        resultPanel.SetActive(true);
    }

    public void CloseResultPanel()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  »ó¼¼ ÆË¾÷
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    public void ShowDetailPopup(CompanionData data)
    {
        if (data == null || detailPopup == null) return;

        selectedCompanion = data;

        if (detailPortrait != null) detailPortrait.sprite = data.portrait;
        if (detailNameText != null) detailNameText.text = data.companionName;

        int ri = (int)data.rarity;

        if (detailRarityText != null)
        {
            detailRarityText.text = ri < rarityNames.Length ? rarityNames[ri] : data.rarity.ToString();
            detailRarityText.color = ri < rarityColors.Length ? rarityColors[ri] : Color.white;
        }

        if (detailDescText != null) detailDescText.text = data.description;

        if (detailStatsText != null)
        {
            detailStatsText.text =
                $"°ø°Ý·Â  : {data.attackPower}\n" +
                $"°ø°Ý¼Óµµ: {data.attackSpeed}/s\n" +
                $"»ç°Å¸®  : {data.attackRange}m\n" +
                $"ÀÌµ¿¼Óµµ: {data.moveSpeed}";
        }

        detailPopup.SetActive(true);
    }

    public void CloseDetailPopup()
    {
        if (detailPopup != null) detailPopup.SetActive(false);
        selectedCompanion = null;
    }

    private void AddSelectedToHotbar()
    {
        if (selectedCompanion == null) return;

        bool ok = CompanionHotbarManager.Instance?.RegisterCompanion(selectedCompanion) ?? false;
        if (ok)
            UIManager.Instance?.ShowMessage($"{selectedCompanion.companionName} ÇÖ¹Ù µî·Ï!", Color.green);

        CloseDetailPopup();
    }
}

// ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
//  °á°ú °³º° ¾ÆÀÌÅÛ UI
// ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

/// <summary>
/// µ¿·á »Ì±â °á°ú È­¸éÀÇ °³º° ¾ÆÀÌÅÛ
/// Prefab ±¸Á¶:
///   - Image (ÃÊ»óÈ­)  ¡æ portraitImage
///   - Image (Å×µÎ¸®)  ¡æ borderImage
///   - TMP (ÀÌ¸§)      ¡æ nameText
///   - TMP (µî±Þ)      ¡æ rarityText
///   - Button (Å¬¸¯ ¡æ »ó¼¼ ÆË¾÷)
/// </summary>
public class CompanionResultItem : MonoBehaviour
{
    public Image portaitImage;
    public Image borderImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI rarityText;

    private CompanionData data;
    private CompanionGachaManager manager;

    private readonly Color[] rarityColors =
    {
        Color.gray,
        new Color(0.3f, 0.5f, 1f),
        new Color(0.7f, 0.2f, 1f),
        new Color(1f,   0.8f, 0.1f)
    };
    private readonly string[] rarityNames = { "ÀÏ¹Ý", "Èñ±Í", "¿µ¿õ", "Àü¼³" };

    public void Setup(CompanionData cd, CompanionGachaManager mgr)
    {
        data = cd;
        manager = mgr;

        if (portaitImage != null) portaitImage.sprite = cd.portrait;

        int ri = (int)cd.rarity;
        Color rc = ri < rarityColors.Length ? rarityColors[ri] : Color.white;

        if (borderImage != null) borderImage.color = rc;
        if (nameText != null) nameText.text = cd.companionName;
        if (rarityText != null) { rarityText.text = ri < rarityNames.Length ? rarityNames[ri] : ""; rarityText.color = rc; }

        Button btn = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        btn.onClick.AddListener(OnClicked);
    }

    private void OnClicked() => manager?.ShowDetailPopup(data);
}