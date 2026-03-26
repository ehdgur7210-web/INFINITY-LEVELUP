using UnityEngine;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("Cinemachine Virtual Camera")]
    public CinemachineVirtualCamera virtualCamera;

    [Header("Camera Settings")]
    public float normalSize = 5f;
    public float zoomInSize = 3f;
    public float zoomOutSize = 8f;
    public float zoomSpeed = 2f;

    [Header("Camera Shake")]
    public float shakeIntensity = 1f;
    public float shakeTime = 0.3f;

    private CinemachineBasicMultiChannelPerlin noise;
    private float currentSize;
    private float shakeTimer;

    void Start()
    {
        if (virtualCamera == null)
        {
            virtualCamera = GetComponent<CinemachineVirtualCamera>();
        }

        if (virtualCamera != null)
        {
            noise = virtualCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
            currentSize = normalSize;
        }
    }

    void Update()
    {
        HandleZoom();
        HandleShake();
    }

    void HandleZoom()
    {
        if (virtualCamera == null) return;

        // 마우스 스크롤 휠 입력
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0)
        {
            currentSize -= scrollInput * zoomSpeed;
            currentSize = Mathf.Clamp(currentSize, zoomInSize, zoomOutSize);
        }

        // 부드럽게 줌 적용 (Orthographic Size)
        virtualCamera.m_Lens.OrthographicSize = Mathf.Lerp(
            virtualCamera.m_Lens.OrthographicSize,
            currentSize,
            Time.deltaTime * 5f
        );
    }

    void HandleShake()
    {
        if (noise == null) return;

        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0)
            {
                noise.m_AmplitudeGain = 0f;
            }
        }
    }

    // 카메라 흔들기 효과
    public void ShakeCamera(float intensity = -1f, float duration = -1f)
    {
        if (noise == null) return;

        float actualIntensity = intensity > 0 ? intensity : shakeIntensity;
        float actualDuration = duration > 0 ? duration : shakeTime;

        noise.m_AmplitudeGain = actualIntensity;
        shakeTimer = actualDuration;
    }

    // 특정 타깃 따라가기
    public void SetFollowTarget(Transform target)
    {
        if (virtualCamera != null)
        {
            virtualCamera.Follow = target;
            virtualCamera.LookAt = target;
        }
    }

    // 카메라 크기를 설정
    public void SetCameraSize(float size)
    {
        currentSize = size;
    }

    // 즉시 카메라 크기를 설정
    public void SetCameraSizeImmediate(float size)
    {
        currentSize = size;
        if (virtualCamera != null)
        {
            virtualCamera.m_Lens.OrthographicSize = size;
        }
    }
}