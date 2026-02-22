using UnityEngine;

public class InfiniteBackground : MonoBehaviour
{
    [Header("Scroll Settings")]
    [Tooltip("스크롤 속도")]
    public float scrollSpeed = 0.5f;

    [Tooltip("자동 스크롤 활성화")]
    public bool autoScroll = true;

    [Tooltip("스크롤 방향 (-1, 0)은 왼쪽으로")]
    public Vector2 scrollDirection = new Vector2(-1, 0);

    [Header("References")]
    [Tooltip("추적할 카메라 (비워두면 Main Camera 자동 찾기)")]
    public Transform cameraTransform;

    [Tooltip("배경 스프라이트 (비워두면 자동 찾기)")]
    public SpriteRenderer backgroundSprite;

    [Header("Parallax Effect")]
    [Tooltip("패럴랙스 효과 사용")]
    public bool useParallax = true;

    [Range(0f, 1f)]
    [Tooltip("0 = 고정, 1 = 카메라와 같은 속도")]
    public float parallaxFactor = 0.2f;

    [Header("Advanced Settings")]
    [Tooltip("Material을 사용한 스크롤 (더 부드러움)")]
    public bool useMaterialOffset = true;

    private Vector3 startPosition;
    private Material materialInstance;
    private Vector2 materialOffset;

    void Start()
    {
        // 카메라 자동 찾기
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        // 스프라이트 자동 찾기
        if (backgroundSprite == null)
        {
            backgroundSprite = GetComponent<SpriteRenderer>();
        }

        startPosition = transform.position;

        // Material 인스턴스 생성 (원본 보호)
        if (useMaterialOffset && backgroundSprite != null)
        {
            materialInstance = new Material(backgroundSprite.material);
            backgroundSprite.material = materialInstance;
            materialOffset = Vector2.zero;
        }
    }

    void Update()
    {
        if (useMaterialOffset)
        {
            UpdateMaterialOffset();
        }
        else
        {
            UpdateTransformPosition();
        }
    }

    // Material Offset 방식 (권장)
    void UpdateMaterialOffset()
    {
        if (materialInstance == null) return;

        // 자동 스크롤
        if (autoScroll)
        {
            materialOffset += scrollDirection.normalized * scrollSpeed * Time.deltaTime;
        }

        // 패럴랙스 효과
        if (useParallax && cameraTransform != null)
        {
            Vector2 cameraOffset = new Vector2(
                cameraTransform.position.x * parallaxFactor,
                cameraTransform.position.y * parallaxFactor
            );
            materialInstance.mainTextureOffset = materialOffset + cameraOffset;
        }
        else
        {
            materialInstance.mainTextureOffset = materialOffset;
        }
    }

    // Transform Position 방식
    void UpdateTransformPosition()
    {
        if (autoScroll)
        {
            transform.Translate(scrollDirection.normalized * scrollSpeed * Time.deltaTime);
        }

        if (useParallax && cameraTransform != null)
        {
            float distance = cameraTransform.position.x * parallaxFactor;
            transform.position = new Vector3(
                startPosition.x + distance,
                transform.position.y,
                transform.position.z
            );
        }

        CheckWrapAround();
    }

    void CheckWrapAround()
    {
        if (backgroundSprite == null) return;

        float spriteWidth = backgroundSprite.bounds.size.x;
        float distanceX = transform.position.x - startPosition.x;

        // 왼쪽으로 한 바퀴 돌았을 때
        if (distanceX <= -spriteWidth)
        {
            transform.position = new Vector3(
                startPosition.x,
                transform.position.y,
                transform.position.z
            );
        }
        // 오른쪽으로 한 바퀴 돌았을 때
        else if (distanceX >= spriteWidth)
        {
            transform.position = new Vector3(
                startPosition.x,
                transform.position.y,
                transform.position.z
            );
        }
    }

    void OnDestroy()
    {
        // Material 인스턴스 정리
        if (materialInstance != null)
        {
            Destroy(materialInstance);
        }
    }
}