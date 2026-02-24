using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class VideoController : MonoBehaviour
{
    [Header("����")]
    public VideoPlayer videoPlayer;

    [Header("��� �� �̵��� ��")]
    public string nextSceneName = "MainMenu";

    [Header("��ŵ ���")]
    public bool allowSkip = true;

    void Start()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoEnd;
            videoPlayer.Play();
        }
    }

    void Update()
    {
        if (!allowSkip) return;

        // ✅ 마우스 클릭 (에디터/PC)
        if (Input.GetMouseButtonDown(0))
        {
            GoToNextScene();
            return;
        }

        // ✅ 터치 시작 시 1회만 (TouchPhase.Began) - 모바일
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            GoToNextScene();
        }
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        GoToNextScene();
    }

    void GoToNextScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}