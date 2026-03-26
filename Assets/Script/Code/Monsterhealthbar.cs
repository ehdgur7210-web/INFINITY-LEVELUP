using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ���� �Ӹ� ���� ǥ�õǴ� ü�¹�
/// 
/// �� ����:
/// 1. Canvas(World Space) �������� ���� healthBarPrefab�� ����
/// 2. Monster �����տ� �� ��ũ��Ʈ�� ����
/// 3. �ڵ����� �Ӹ� ���� ü�¹ٰ� ������
/// 
/// �� ������ ���� (���� ������ ��):
///   MonsterHealthBar (Canvas - World Space)
///     ���� Background (Image - ���� �Ǵ� ��ο��)
///         ���� Fill (Image - ������/�ʷϻ�, Image Type = Filled)
/// 
/// �� ���� ���ʹ� BossMonsterHealthBar�� ��� ���̼���!
/// </summary>
public class MonsterHealthBar : MonoBehaviour
{
    [Header("ü�¹� ������")]
    [Tooltip("���� �����̽� Canvas�� ���� ü�¹� ������ (������ �ڵ� ������)")]
    [SerializeField] private GameObject healthBarPrefab;

    [Header("��ġ ����")]
    [Tooltip("���� �Ӹ� ���� �󸶳� �ø��� (Y�� ������)")]
    [SerializeField] protected Vector3 offset = new Vector3(0f, 1.5f, 0f);
    // �� protected: �ڽ� Ŭ����(BossMonsterHealthBar)������ ���� ����

    [Header("ü�¹� ũ��")]
    [Tooltip("ü�¹� ��ü ������ (���� �����̽� ����)")]
    [SerializeField] private float barScale = 0.01f;

    [Header("���� ����")]
    [Tooltip("ü���� ���� �� ���� (�ʷ�)")]
    [SerializeField] protected Color highHpColor = Color.green;
    [Tooltip("ü���� �߰��� �� ���� (���)")]
    [SerializeField] protected Color midHpColor = Color.yellow;
    [Tooltip("ü���� ���� �� ���� (����)")]
    [SerializeField] protected Color lowHpColor = Color.red;
    // �� protected: �ڽ� Ŭ�������� ���� ���� ����

    [Header("ǥ�� ����")]
    [Tooltip("true�� �׻� ǥ��, false�� �ǰ� �ÿ��� ��� ǥ��")]
    [SerializeField] private bool alwaysVisible = true;
    [Tooltip("�ǰ� �� ü�¹ٰ� ���̴� �ð� (alwaysVisible=false�� ���� ���)")]
    [SerializeField] private float visibleDuration = 3f;

    // ������ ���� ���� (protected = �ڽ� Ŭ�������� ���� ����) ������
    protected GameObject healthBarInstance; // ������ ü�¹� ������Ʈ
    protected Slider hpSlider;              // �����̴� ����� �� ���
    protected Image fillImage;              // �̹��� ����� �� ���
    protected Monster monster;              // �� ��ũ��Ʈ�� ���� ����

    private float visibleTimer = 0f;
    private Canvas healthBarCanvas;


    // ��������������������������������������������������������������������������������������
    void Start()
    {
        // ���� ������Ʈ�� Monster ������Ʈ ��������
        monster = GetComponent<Monster>();

        // �� virtual �Լ� ȣ�� �� �ڽ� Ŭ������ ������ �ڽ� ������ �����
        CreateHealthBar();
    }

    // ��������������������������������������������������������������������������������������
    void Update()
    {
        if (healthBarInstance == null) return;


        // ������ 1. ��ġ ������Ʈ (�� virtual �� ������ �������̵� ����) ������
        UpdateHealthBarPosition();

        // ������ 2. ü�� ���� ��� �� ü�¹� ���� ������
        if (monster != null)
        {
            float hpRatio = Mathf.Clamp01((float)monster.currentHp / monster.maxHp);
            UpdateBar(hpRatio);
        }

        // ������ 3. ���� �ð� �� ü�¹� ����� (alwaysVisible=false�� ��) ������
        if (!alwaysVisible)
        {
            if (visibleTimer > 0f)
            {
                visibleTimer -= Time.deltaTime;
                healthBarInstance.SetActive(true);
            }
            else
            {
                healthBarInstance.SetActive(false);
            }
        }
    }

    // ��������������������������������������������������������������������������������������
    /// <summary>
    /// ü�¹ٸ� �����ϴ� �Լ�
    /// �� virtual: BossMonsterHealthBar���� �������̵��ؼ� �ٸ� ������� ���� ����
    /// </summary>
    protected virtual void CreateHealthBar()
    {
        if (healthBarPrefab != null)
        {
            // ������ �������� ������ ���������� ���� ������
            healthBarInstance = Instantiate(healthBarPrefab, transform.position + offset, Quaternion.identity);
            healthBarInstance.transform.SetParent(transform); // ���� �ڽ����� �� ���� �ı���

            // �����̴� �Ǵ� Fill �̹��� ã��
            hpSlider = healthBarInstance.GetComponentInChildren<Slider>();
            if (hpSlider == null)
            {
                Image[] images = healthBarInstance.GetComponentsInChildren<Image>();
                foreach (Image img in images)
                {
                    if (img.gameObject.name.Contains("Fill") || img.type == Image.Type.Filled)
                    {
                        fillImage = img;
                        break;
                    }
                }
            }
        }
        else
        {
            // ������ ������ ������ �ڵ�� �ڵ� ���� ������
            CreateDefaultHealthBar();
        }

        if (!alwaysVisible && healthBarInstance != null)
            healthBarInstance.SetActive(false);
    }

    // ��������������������������������������������������������������������������������������
    /// <summary>
    /// ü�¹� ��ġ�� �� ������ ������Ʈ�ϴ� �Լ�
    /// �� virtual: ������ ��ũ�� UI ������� �������̵� ����
    /// </summary>
    protected virtual void UpdateHealthBarPosition()
    {
        // �Ϲ� ����: ���� ��ǥ���� �����¸�ŭ ���� ����
        healthBarInstance.transform.position = transform.position + offset;

        // ī�޶� �׻� �ٶ󺸰� (������ ȿ��) - 3D ���ӿ��� �ʿ�
        if (Camera.main != null)
            healthBarInstance.transform.forward = Camera.main.transform.forward;
    }

    // ��������������������������������������������������������������������������������������
    /// <summary>
    /// ������ ���� �ڵ�� �⺻ ü�¹ٸ� �ڵ� �����ϴ� �Լ�
    /// (�������� �� ���� ü�¹ٰ� ����)
    /// </summary>
    private void CreateDefaultHealthBar()
    {
        // ������ 1. ���� �����̽� ĵ���� ���� ������
        healthBarInstance = new GameObject($"{gameObject.name}_HealthBar");
        healthBarInstance.transform.SetParent(transform);
        healthBarInstance.transform.localPosition = offset;

        Canvas canvas = healthBarInstance.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; // ���� ��ǥ�� ǥ��
        canvas.sortingOrder = 100;                  // �ٸ� ������Ʈ���� ���� �׸�
        healthBarCanvas = canvas;

        RectTransform canvasRect = healthBarInstance.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(100f, 15f);  // ���� 100, ���� 15 (�ȼ� ����)
        canvasRect.localScale = Vector3.one * barScale; // ���� �����̽����� ũ�� ����

        // ������ 2. ��� (��ο� ��) ������
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(healthBarInstance.transform);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // ������ ��ο� ȸ��

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bgRect.localScale = Vector3.one;
        bgRect.localPosition = Vector3.zero;

        // ������ 3. ü�� ä��� �� ������
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(healthBarInstance.transform);
        fillImage = fillObj.AddComponent<Image>();
        fillImage.color = highHpColor; // ó���� �ʷϻ�

        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f); // ü�� ������ ���� anchorMax.x ����
        fillRect.offsetMin = new Vector2(2f, 2f);  // �׵θ� ������ �е�
        fillRect.offsetMax = new Vector2(-2f, -2f);
        fillRect.localScale = Vector3.one;
        fillRect.localPosition = Vector3.zero;
    }

    // ��������������������������������������������������������������������������������������
    /// <summary>
    /// ü�� ������ ���� �������� ������ ������Ʈ�ϴ� �Լ�
    /// �� virtual: �ڽ� Ŭ�������� �ٸ� UI ������� �������̵� ����
    /// </summary>
    /// <param name="hpRatio">ü�� ���� (0.0 = �� ����, 1.0 = ���� �� ����)</param>
    public virtual void UpdateBar(float hpRatio)
    {
        // ������ �����̴� ��� ������
        if (hpSlider != null)
        {
            hpSlider.value = hpRatio;
        }
        // ������ ��Ŀ ���� ��� ������
        else if (fillImage != null)
        {
            RectTransform fillRect = fillImage.GetComponent<RectTransform>();
            if (fillRect != null)
            {
                // ������ ��Ŀ�� ü�� ������ŭ ���̸� �������� �پ��� ȿ��
                fillRect.anchorMax = new Vector2(hpRatio, 1f);
            }
        }

        // ������ ü�� ������ ���� ���� ���� ������
        Color barColor;
        if (hpRatio > 0.5f)
            barColor = Color.Lerp(midHpColor, highHpColor, (hpRatio - 0.5f) * 2f); // ��� �� �ʷ�
        else
            barColor = Color.Lerp(lowHpColor, midHpColor, hpRatio * 2f);            // ���� �� ���

        if (fillImage != null)
            fillImage.color = barColor;
    }

    // ��������������������������������������������������������������������������������������
    /// <summary>
    /// �ǰ� �� �ܺο��� ȣ���ؼ� ü�¹ٸ� ��� �����ִ� �Լ�
    /// (alwaysVisible = false�� �� ���)
    /// ��) monster.GetComponent<MonsterHealthBar>().ShowTemporarily();
    /// </summary>
    public void ShowTemporarily()
    {
        visibleTimer = visibleDuration;
    }

    // ��������������������������������������������������������������������������������������
    /// <summary>
    /// ������Ʈ�� �ı��� �� ü�¹ٵ� ���� ����
    /// (���� �����̽� ü�¹ٰ� ������ �ڽ��� �ƴ� ��츦 ���� ������ġ)
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (healthBarInstance != null)
            Destroy(healthBarInstance);
    }
}