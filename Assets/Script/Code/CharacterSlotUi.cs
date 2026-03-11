using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSlotUI : MonoBehaviour
{
    [SerializeField] private GameObject selectFrame;
    [SerializeField] private GameObject filledGroup;
    [SerializeField] private GameObject emptySlotGroup;
    [SerializeField] private Image classIconImage;
    [SerializeField] private TextMeshProUGUI charNameText;
    [SerializeField] private TextMeshProUGUI idText;
    [SerializeField] private TextMeshProUGUI levelText;

    private int slotIndex;
    private System.Action<int> onClickCallback;

    public void Init(int idx, System.Action<int> callback)
    {
        slotIndex = idx;
        onClickCallback = callback;
        Button btn = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        btn.onClick.AddListener(() => onClickCallback?.Invoke(slotIndex));
    }

    public void Refresh(CharacterSelectManager.SlotData data, Sprite icon, bool selected)
    {
        if (emptySlotGroup != null) emptySlotGroup.SetActive(!data.exists);
        if (filledGroup != null) filledGroup.SetActive(data.exists);
        if (data.exists)
        {
            if (classIconImage != null) classIconImage.sprite = icon;
            if (charNameText != null) charNameText.text = data.charName;
            if (idText != null) idText.text = data.accountID;
            if (levelText != null) levelText.text = $"Lv.{data.level}";
        }
        SetSelected(selected);
    }

    public void SetSelected(bool on)
    {
        if (selectFrame != null) selectFrame.SetActive(on);
    }
}