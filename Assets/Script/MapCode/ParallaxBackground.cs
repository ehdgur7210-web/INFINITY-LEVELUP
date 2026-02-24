using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    [System.Serializable]
    public class ParallaxLayer
    {
        public Transform layerTransform;
        [Range(0f, 1f)]
        public float parallaxFactor = 0.5f;
        public float scrollSpeed = 0f; // 자동 스크롤 속도
    }

    [Header("References")]
    public Transform cameraTransform;

    [Header("Parallax Layers")]
    public ParallaxLayer[] layers;

    [Header("Settings")]
    public bool usePlayerMovement = true;
    public Transform player;

    private Vector3 lastCameraPosition;
    private Vector3 lastPlayerPosition;

    void Start()
    {
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        lastCameraPosition = cameraTransform.position;
        lastPlayerPosition = player != null ? player.position : Vector3.zero;
    }

    void LateUpdate()
    {
        // 카메라 이동량 계산
        Vector3 deltaMovement = cameraTransform.position - lastCameraPosition;

        // 플레이어 이동량 계산
        Vector3 playerDelta = Vector3.zero;
        if (usePlayerMovement && player != null)
        {
            playerDelta = player.position - lastPlayerPosition;
        }

        foreach (var layer in layers)
        {
            if (layer.layerTransform == null) continue;

            // 패럴랙스 효과
            Vector3 parallaxOffset = deltaMovement * layer.parallaxFactor;

            // 플레이어 기반 이동
            if (usePlayerMovement)
            {
                parallaxOffset += playerDelta * (1f - layer.parallaxFactor);
            }

            // 자동 스크롤
            if (layer.scrollSpeed != 0)
            {
                parallaxOffset += Vector3.left * layer.scrollSpeed * Time.deltaTime;
            }

            layer.layerTransform.position += parallaxOffset;

            // 무한 루프 체크
            CheckInfiniteLoop(layer);
        }

        lastCameraPosition = cameraTransform.position;
        lastPlayerPosition = player != null ? player.position : lastPlayerPosition;
    }

    void CheckInfiniteLoop(ParallaxLayer layer)
    {
        SpriteRenderer sprite = layer.layerTransform.GetComponent<SpriteRenderer>();
        if (sprite == null) return;

        float spriteWidth = sprite.bounds.size.x;
        float cameraX = cameraTransform.position.x;
        float layerX = layer.layerTransform.position.x;

        // 왼쪽으로 너무 많이 이동했을 때
        if (layerX + spriteWidth < cameraX - spriteWidth)
        {
            layer.layerTransform.position += Vector3.right * spriteWidth * 2;
        }
        // 오른쪽으로 너무 많이 이동했을 때
        else if (layerX > cameraX + spriteWidth)
        {
            layer.layerTransform.position += Vector3.left * spriteWidth * 2;
        }
    }
}