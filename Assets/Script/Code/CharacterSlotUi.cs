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

        // Inspector 미연결 시 자식에서 자동 탐색
        AutoBindIfNeeded();

        Button btn = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClickCallback?.Invoke(slotIndex));
    }

    private void AutoBindIfNeeded()
    {
        if (filledGroup == null)
        {
            Transform t = transform.Find("FilledGroup");
            if (t != null) filledGroup = t.gameObject;
        }
        if (emptySlotGroup == null)
        {
            Transform t = transform.Find("EmptySlotGroup");
            if (t == null) t = transform.Find("EmptyGroup");
            if (t != null) emptySlotGroup = t.gameObject;
        }
        if (selectFrame == null)
        {
            Transform t = transform.Find("SelectFrame");
            if (t != null) selectFrame = t.gameObject;
        }
        if (classIconImage == null && filledGroup != null)
        {
            Transform t = filledGroup.transform.Find("ClassIconImage");
            if (t == null) t = filledGroup.transform.Find("ClassIcon");
            if (t != null) classIconImage = t.GetComponent<Image>();
        }
        if (charNameText == null && filledGroup != null)
        {
            Transform t = filledGroup.transform.Find("CharNameText");
            if (t != null) charNameText = t.GetComponent<TextMeshProUGUI>();
        }
        if (levelText == null && filledGroup != null)
        {
            Transform t = filledGroup.transform.Find("LevelText");
            if (t != null) levelText = t.GetComponent<TextMeshProUGUI>();
        }
        if (idText == null && filledGroup != null)
        {
            Transform t = filledGroup.transform.Find("IDText");
            if (t == null) t = filledGroup.transform.Find("IdText");
            if (t != null) idText = t.GetComponent<TextMeshProUGUI>();
        }
    }

    public void Refresh(CharacterSelectManager.SlotData data, Sprite icon, bool selected)
    {
        gameObject.SetActive(true);

        if (data.exists)
        {
            if (filledGroup != null) filledGroup.SetActive(true);
            if (emptySlotGroup != null && emptySlotGroup != gameObject) emptySlotGroup.SetActive(false);

            if (classIconImage != null) { classIconImage.sprite = icon; classIconImage.enabled = true; }
            if (charNameText != null) { charNameText.text = data.charName; charNameText.enabled = true; }
            if (idText != null) { idText.text = data.accountID; idText.enabled = true; }
            if (levelText != null) { levelText.text = $"Lv.{data.level}"; levelText.enabled = true; }
        }
        else
        {
            // filledGroup이 자기 자신이면 내용물만 숨기기
            if (filledGroup != null && filledGroup != gameObject)
                filledGroup.SetActive(false);

            if (emptySlotGroup != null && emptySlotGroup != gameObject) emptySlotGroup.SetActive(true);

            if (classIconImage != null) { classIconImage.sprite = null; classIconImage.enabled = false; }
            if (charNameText != null) { charNameText.text = ""; charNameText.enabled = false; }
            if (levelText != null) { levelText.text = ""; levelText.enabled = false; }
            if (idText != null) { idText.text = ""; idText.enabled = false; }
        }

        SetSelected(selected);
        // ★ 슬롯 자체는 절대 꺼지면 안 됨
        gameObject.SetActive(true);
    }

    public void SetSelected(bool on)
    {
        if (selectFrame != null && selectFrame != gameObject)
            selectFrame.SetActive(on);
    }
}
