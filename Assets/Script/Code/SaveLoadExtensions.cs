using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// InventoryManager 확장 메서드
/// - Extension Method 패턴 사용
/// </summary>
public static class InventoryManagerExtensions
{
    /// <summary>
    /// 인벤토리 데이터 가져오기 (저장용)
    /// </summary>
    public static InventoryItemData[] GetInventoryData(this InventoryManager manager)
    {
        List<InventoryItemData> itemDataList = new List<InventoryItemData>();

        // TODO: 실제 InventoryManager 구조에 맞게 수정
        // 예시:
        /*
        for (int i = 0; i < manager.inventorySlots.Length; i++)
        {
            InventorySlot slot = manager.inventorySlots[i];
            if (slot.HasItem)
            {
                InventoryItemData data = new InventoryItemData
                {
                    itemID = slot.item.itemID,
                    count = slot.count,
                    slotIndex = i
                };
                itemDataList.Add(data);
            }
        }
        */

        return itemDataList.ToArray();
    }

    /// <summary>
    /// 인벤토리 데이터 로드 (로드용)
    /// </summary>
    public static void LoadInventoryData(this InventoryManager manager, InventoryItemData[] itemData)
    {
        if (itemData == null) return;

        // TODO: 실제 InventoryManager 구조에 맞게 수정
        /*
        manager.ClearInventory();
        
        foreach (var data in itemData)
        {
            ItemData item = ItemDatabase.Instance.GetItemByID(data.itemID);
            if (item != null)
            {
                manager.AddItemToSlot(item, data.count, data.slotIndex);
            }
        }
        */
    }
}

/// <summary>
/// QuestManager 확장 메서드
/// ※ QuestManager에 GetQuestData/LoadQuestData가 직접 구현되어 있으므로
///    여기서는 별도 확장 필요 없음 (충돌 방지를 위해 비워둠)
/// </summary>
public static class QuestManagerExtensions
{
    // QuestManager.GetQuestData() / LoadQuestData() 가 직접 구현되어 있습니다.
}

/// <summary>
/// 간단한 퀵 세이브/로드 기능
/// </summary>
public static class QuickSave
{
    /// <summary>
    /// 빠른 저장 (F5)
    /// </summary>
    public static void Save()
    {
        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.SaveGame(0);
            Debug.Log("퀵 세이브 완료!");
        }
        else
        {
            Debug.LogWarning("SaveLoadManager를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 빠른 로드 (F9)
    /// </summary>
    public static void Load()
    {
        if (SaveLoadManager.Instance != null)
        {
            if (SaveLoadManager.Instance.LoadGame(0))
            {
                Debug.Log("퀵 로드 완료!");
            }
            else
            {
                Debug.LogWarning("저장 데이터가 없습니다!");
            }
        }
        else
        {
            Debug.LogWarning("SaveLoadManager를 찾을 수 없습니다!");
        }
    }
}

/// <summary>
/// 게임 내에서 F5/F9로 퀵 세이브/로드
/// GameManager나 플레이어에 추가하세요
/// </summary>
public class QuickSaveController : MonoBehaviour
{
    void Update()
    {
        // F5 - 퀵 세이브
        if (Input.GetKeyDown(KeyCode.F5))
        {
            QuickSave.Save();
        }

        // F9 - 퀵 로드
        if (Input.GetKeyDown(KeyCode.F9))
        {
            QuickSave.Load();
        }
    }
}

/// <summary>
/// ESC 일시정지 메뉴
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private GameObject pauseMenuPanel;

    [Header("버튼")]
    [SerializeField] private UnityEngine.UI.Button resumeButton;
    [SerializeField] private UnityEngine.UI.Button saveButton;
    [SerializeField] private UnityEngine.UI.Button settingsButton;
    [SerializeField] private UnityEngine.UI.Button mainMenuButton;

    private bool isPaused = false;

    void Start()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        SetupButtons();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    private void SetupButtons()
    {
        if (resumeButton != null)
            resumeButton.onClick.AddListener(Resume);

        if (saveButton != null)
            saveButton.onClick.AddListener(SaveGame);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettings);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(BackToMainMenu);
    }

    /// <summary>
    /// 게임 일시정지
    /// </summary>
    public void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Debug.Log("게임 일시정지");
    }

    /// <summary>
    /// 게임 재개
    /// </summary>
    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        Debug.Log("게임 재개");
    }

    /// <summary>
    /// 게임 저장
    /// </summary>
    private void SaveGame()
    {
        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.SaveGame(0);
            Debug.Log("게임 저장 완료!");
        }
    }

    /// <summary>
    /// 설정 열기
    /// </summary>
    private void OpenSettings()
    {
        Debug.Log("설정 열기");
        // TODO: 설정 패널 열기
    }

    /// <summary>
    /// 메인 메뉴로 돌아가기
    /// </summary>
    private void BackToMainMenu()
    {
        // 저장
        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.SaveGame(0);
        }

        // timeScale 복구
        Time.timeScale = 1f;

        // 메인 메뉴로 전환
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadMainMenu();
        }
        else
        {
            Debug.LogWarning("SceneTransitionManager를 찾을 수 없습니다!");
        }
    }
}

/// <summary>
/// 플레이 타임 추적기
/// GameManager에 추가하세요
/// </summary>
public class PlayTimeTracker : MonoBehaviour
{
    private float totalPlayTime = 0f;
    private bool isTracking = true;

    void Update()
    {
        if (isTracking)
        {
            totalPlayTime += Time.deltaTime;
        }
    }

    /// <summary>
    /// 현재 플레이 타임 가져오기 (초)
    /// </summary>
    public float GetPlayTime()
    {
        return totalPlayTime;
    }

    /// <summary>
    /// 플레이 타임 설정 (로드 시)
    /// </summary>
    public void SetPlayTime(float time)
    {
        totalPlayTime = time;
    }

    /// <summary>
    /// 플레이 타임 포맷팅 (HH:MM:SS)
    /// </summary>
    public string GetFormattedPlayTime()
    {
        int hours = Mathf.FloorToInt(totalPlayTime / 3600f);
        int minutes = Mathf.FloorToInt((totalPlayTime % 3600f) / 60f);
        int seconds = Mathf.FloorToInt(totalPlayTime % 60f);

        return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
    }

    /// <summary>
    /// 추적 시작/중지
    /// </summary>
    public void SetTracking(bool track)
    {
        isTracking = track;
    }

    /// <summary>
    /// 플레이 시간 리셋
    /// </summary>
    public void ResetPlayTime()
    {
        totalPlayTime = 0f;
    }
}