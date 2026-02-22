using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class VideoController : MonoBehaviour
{
    [Header("비디오")]
    public VideoPlayer videoPlayer;

    [Header("재생 후 이동할 씬")]
    public string nextSceneName = "MainMenu";

    [Header("스킵 허용")]
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
        // 아무 키나 터치로 스킵
        if (allowSkip && (Input.anyKeyDown || Input.touchCount > 0))
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