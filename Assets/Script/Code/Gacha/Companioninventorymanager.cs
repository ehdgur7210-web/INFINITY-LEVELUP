using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CompanionInventoryManager
/// ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
/// ÀÎº¥Åä¸® "µ¿·á" ÅÇ¿¡¼­ µ¿·á ¸ñ·Ï Ç¥½Ã ¹× ÇÖ¹Ù µî·Ï °ü¸®.
///
/// [±â´É]
///   - µ¿·á »Ì±â °á°ú ¡æ µ¿·á ¸ñ·Ï¿¡ Ãß°¡
///   - µ¿·á ½½·Ô Å¬¸¯ ¡æ CompanionHotbarManager¿¡ µî·Ï
///   - µ¿·á ¸ñ·Ï ÀúÀå/·Îµå
///
/// [Inspector ¿¬°á]
///   companionSlotPrefab : CompanionInvSlot ÇÁ¸®ÆÕ
///   companionSlotParent : companionContent ÇÏÀ§ GridLayoutGroup
///   maxCompanionSlots   : ÃÖ´ë º¸À¯ µ¿·á ¼ö
/// </summary>
public class CompanionInventoryManager : MonoBehaviour
{
    public static CompanionInventoryManager Instance;

    [Header("½½·Ô ¼³Á¤")]
    public GameObject companionSlotPrefab;
    public Transform companionSlotParent;
    public int maxCompanionSlots = 30;

    // º¸À¯ µ¿·á ¸ñ·Ï (Áßº¹ º¸À¯ °¡´É - count·Î °ü¸®)
    [System.Serializable]
    public class CompanionEntry
    {
        public CompanionData data;
        public int count = 1;
    }

    private List<CompanionEntry> companionList = new List<CompanionEntry>();
    private List<CompanionInvSlot> slotUIs = new List<CompanionInvSlot>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        InitializeSlots();
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  ½½·Ô ÃÊ±âÈ­
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    private void InitializeSlots()
    {
        if (companionSlotPrefab == null || companionSlotParent == null)
        {
            Debug.LogWarning("[CompanionInventoryManager] ½½·Ô ÇÁ¸®ÆÕ ¶Ç´Â ºÎ¸ð°¡ ¾ø½À´Ï´Ù!");
            return;
        }

        slotUIs.Clear();

        for (int i = 0; i < maxCompanionSlots; i++)
        {
            GameObject go = Instantiate(companionSlotPrefab, companionSlotParent);
            CompanionInvSlot slot = go.GetComponent<CompanionInvSlot>()
                                    ?? go.AddComponent<CompanionInvSlot>();
            slot.Init(this, i);
            slotUIs.Add(slot);
        }

        RefreshUI();
        Debug.Log($"[CompanionInventoryManager] µ¿·á ÀÎº¥ ÃÊ±âÈ­ ¿Ï·á ({maxCompanionSlots}½½·Ô)");
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  µ¿·á Ãß°¡ (»Ì±â °á°ú¿¡¼­ È£Ãâ)
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    /// <summary>
    /// µ¿·á Ãß°¡ (Áßº¹ÀÌ¸é count Áõ°¡)
    /// </summary>
    public void AddCompanion(CompanionData data)
    {
        if (data == null) return;

        // ±âÁ¸ µ¿·á Å½»ö
        foreach (var entry in companionList)
        {
            if (entry.data == data)
            {
                entry.count++;
                RefreshUI();
                Debug.Log($"[CompanionInventoryManager] {data.companionName} Ãß°¡ (count={entry.count})");
                return;
            }
        }

        // »õ µ¿·á
        if (companionList.Count >= maxCompanionSlots)
        {
            UIManager.Instance?.ShowMessage("µ¿·á ÀÎº¥Åä¸®°¡ ²Ë Ã¡½À´Ï´Ù!", Color.yellow);
            return;
        }

        companionList.Add(new CompanionEntry { data = data, count = 1 });
        RefreshUI();
        Debug.Log($"[CompanionInventoryManager] {data.companionName} »õ·Î Ãß°¡ ¿Ï·á");
    }

    /// <summary>
    /// Æ¯Á¤ µ¿·á Á¦°Å (ÇÖ¹Ù ¹èÄ¡ µî)
    /// </summary>
    public bool RemoveCompanion(CompanionData data, int count = 1)
    {
        for (int i = 0; i < companionList.Count; i++)
        {
            if (companionList[i].data == data)
            {
                companionList[i].count -= count;
                if (companionList[i].count <= 0)
                    companionList.RemoveAt(i);

                RefreshUI();
                return true;
            }
        }
        return false;
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  ½½·Ô Å¬¸¯ ¡æ ÇÖ¹Ù µî·Ï
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    /// <summary>
    /// µ¿·á ½½·Ô Å¬¸¯ ½Ã ÇÖ¹Ù¿¡ µî·Ï
    /// </summary>
    public void OnCompanionSlotClicked(int index)
    {
        if (index < 0 || index >= companionList.Count) return;

        CompanionData data = companionList[index].data;
        bool ok = CompanionHotbarManager.Instance?.RegisterCompanion(data) ?? false;

        if (ok)
        {
            UIManager.Instance?.ShowMessage(
                $"{data.companionName} ¡æ ÇÖ¹Ù¿¡ µî·Ï!\n(ÇÖ¹Ù Å¬¸¯À¸·Î ¼ÒÈ¯)", Color.green);
        }
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  UI °»½Å
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    private void RefreshUI()
    {
        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (i < companionList.Count)
                slotUIs[i].SetEntry(companionList[i]);
            else
                slotUIs[i].ClearSlot();
        }
    }

    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
    //  ÀúÀå/·Îµå
    // ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

    public CompanionSaveData[] GetSaveData()
    {
        List<CompanionSaveData> result = new List<CompanionSaveData>();
        foreach (var entry in companionList)
        {
            if (entry.data == null) continue;
            result.Add(new CompanionSaveData
            {
                companionID = entry.data.companionID,
                count = entry.count
            });
        }
        return result.ToArray();
    }

    public void LoadSaveData(CompanionSaveData[] saved, List<CompanionData> allCompanions)
    {
        companionList.Clear();

        if (saved == null || allCompanions == null) return;

        foreach (var s in saved)
        {
            CompanionData found = allCompanions.Find(c => c != null && c.companionID == s.companionID);
            if (found == null)
            {
                Debug.LogWarning($"[CompanionInventoryManager] ID '{s.companionID}' µ¿·á¸¦ Ã£À» ¼ö ¾øÀ½!");
                continue;
            }

            companionList.Add(new CompanionEntry { data = found, count = s.count });
        }

        RefreshUI();
        Debug.Log($"[CompanionInventoryManager] µ¿·á ·Îµå ¿Ï·á ({companionList.Count}Á¾)");
    }
}

// ¦¡¦¡¦¡ ÀúÀå µ¥ÀÌÅÍ ±¸Á¶ ¦¡¦¡¦¡
[System.Serializable]
public class CompanionSaveData
{
    public string companionID;
    public int count;
}

// ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡
//  µ¿·á ÀÎº¥ ½½·Ô UI
// ¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡¦¡

/// <summary>
/// µ¿·á ÀÎº¥Åä¸® °³º° ½½·Ô
/// Prefab ±¸Á¶:
///   - Image (ÃÊ»óÈ­) ¡æ portraitImage
///   - Image (µî±Þ Å×µÎ¸®) ¡æ rarityBorder
///   - TextMeshProUGUI (ÀÌ¸§) ¡æ nameText
///   - TextMeshProUGUI (º¸À¯ ¼ö) ¡æ countText
///   - TextMeshProUGUI (µî±Þ) ¡æ rarityText
///   - Button (Å¬¸¯ ¡æ ÇÖ¹Ù µî·Ï)
/// </summary>
public class CompanionInvSlot : MonoBehaviour
{
    [Header("UI ÂüÁ¶")]
    public Image portraitImage;
    public Image rarityBorder;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI countText;
    public TextMeshProUGUI rarityText;

    private CompanionInventoryManager.CompanionEntry currentEntry;
    private CompanionInventoryManager manager;
    private int slotIndex;

    private readonly Color[] rarityColors = new Color[]
    {
        Color.gray,
        new Color(0.3f, 0.5f, 1f),
        new Color(0.7f, 0.2f, 1f),
        new Color(1f, 0.8f, 0.1f)
    };
    private readonly string[] rarityNames = { "ÀÏ¹Ý", "Èñ±Í", "¿µ¿õ", "Àü¼³" };

    public void Init(CompanionInventoryManager mgr, int index)
    {
        manager = mgr;
        slotIndex = index;

        Button btn = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        btn.onClick.AddListener(OnClicked);

        ClearSlot();
    }

    public void SetEntry(CompanionInventoryManager.CompanionEntry entry)
    {
        currentEntry = entry;
        if (entry?.data == null) { ClearSlot(); return; }

        CompanionData d = entry.data;
        int ri = (int)d.rarity;
        Color rc = ri < rarityColors.Length ? rarityColors[ri] : Color.white;

        if (portraitImage != null) { portraitImage.sprite = d.portrait; portraitImage.color = Color.white; }
        if (rarityBorder != null) rarityBorder.color = rc;
        if (nameText != null) nameText.text = d.companionName;
        if (countText != null) countText.text = entry.count > 1 ? $"x{entry.count}" : "";
        if (rarityText != null) { rarityText.text = ri < rarityNames.Length ? rarityNames[ri] : ""; rarityText.color = rc; }
    }

    public void ClearSlot()
    {
        currentEntry = null;
        if (portraitImage != null) { portraitImage.sprite = null; portraitImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f); }
        if (rarityBorder != null) rarityBorder.color = Color.clear;
        if (nameText != null) nameText.text = "";
        if (countText != null) countText.text = "";
        if (rarityText != null) rarityText.text = "";
    }

    private void OnClicked()
    {
        if (currentEntry == null) return;
        manager?.OnCompanionSlotClicked(slotIndex);
    }
}