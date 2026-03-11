using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClassButtonUI : MonoBehaviour
{
    [SerializeField] private GameObject selectHighlight;
    [SerializeField] private Image classIcon;
    [SerializeField] private TextMeshProUGUI classBtnNameText;
    [SerializeField] private GameObject comingSoonBadge;   // ★ "업데이트 예정" 뱃지

    private int index;
    private System.Action<int> onClickCallback;
    private bool isComingSoon;

    public void Init(int idx, string name, Sprite icon, bool comingSoon, System.Action<int> callback)
    {
        index = idx;
        isComingSoon = comingSoon;
        onClickCallback = callback;

        if (classBtnNameText != null) classBtnNameText.text = name;

        // ★ 아이콘 어둡게
        if (classIcon != null)
        {
            if (icon != null) classIcon.sprite = icon;
            classIcon.color = comingSoon ? new Color(0.2f, 0.2f, 0.2f, 1f) : Color.white;
        }

        // ★ 뱃지
        if (comingSoonBadge != null) comingSoonBadge.SetActive(comingSoon);

        Button btn = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        btn.onClick.AddListener(() => onClickCallback?.Invoke(index));
        SetSelected(false);
    }

    public void SetSelected(bool on)
    {
        // ★ comingSoon은 선택 하이라이트 표시 안 함
        if (selectHighlight != null) selectHighlight.SetActive(on && !isComingSoon);
    }
}