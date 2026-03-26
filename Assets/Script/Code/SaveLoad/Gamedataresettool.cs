// ⚠️ 이 파일은 Editor 폴더 안에 있어야 합니다
// 경로: Assets/Editor/GameDataResetTool.cs

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// ★ 에디터 전용 데이터 초기화 도구
/// Unity 상단 메뉴 → "Game/Data" 에서 사용
/// </summary>
public class GameDataResetTool
{
    // ─────────────────────────────────────────
    // 에디터 PlayerPrefs 초기화 (버전 키만 남김)
    // ─────────────────────────────────────────
    [MenuItem("Game/Data/[에디터] PlayerPrefs 초기화")]
    static void ResetEditorPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("[GameDataResetTool]  에디터 PlayerPrefs 초기화 완료");
        EditorUtility.DisplayDialog("초기화 완료",
            "PlayerPrefs가 초기화되었습니다.\n(JSON 저장 파일은 영향 없음)", "확인");
    }

    // ─────────────────────────────────────────
    // 에디터 JSON 저장 파일 삭제
    // ─────────────────────────────────────────
    [MenuItem("Game/Data/[에디터] JSON 저장 파일 삭제")]
    static void DeleteEditorSaveFiles()
    {
        string saveDir = Application.persistentDataPath + "/Saves/";

        if (!Directory.Exists(saveDir))
        {
            EditorUtility.DisplayDialog("파일 없음", "저장 폴더가 없습니다.", "확인");
            return;
        }

        string[] files = Directory.GetFiles(saveDir, "SaveData_EDITOR_*");
        foreach (string file in files)
        {
            File.Delete(file);
            Debug.Log($"[GameDataResetTool] 삭제: {file}");
        }

        string msg = files.Length > 0
            ? $"{files.Length}개 에디터 저장 파일 삭제 완료"
            : "삭제할 에디터 저장 파일이 없습니다.";

        EditorUtility.DisplayDialog("삭제 완료", msg + "\n(빌드 데이터는 영향 없음)", "확인");
    }

    // ─────────────────────────────────────────
    // 에디터 전체 초기화
    // ─────────────────────────────────────────
    [MenuItem("Game/Data/[에디터] 전체 초기화 (PlayerPrefs + JSON)")]
    static void ResetAllEditorData()
    {
        if (!EditorUtility.DisplayDialog("전체 초기화 확인",
            "에디터 PlayerPrefs와 JSON 저장 파일을 모두 삭제합니다.\n정말 초기화하시겠습니까?",
            "초기화", "취소"))
            return;

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        string saveDir = Application.persistentDataPath + "/Saves/";
        int deletedCount = 0;
        if (Directory.Exists(saveDir))
        {
            string[] files = Directory.GetFiles(saveDir, "*.json");
            foreach (string file in files)
            {
                File.Delete(file);
                deletedCount++;
            }
        }

        Debug.Log($"[GameDataResetTool] 에디터 전체 초기화 완료 (JSON {deletedCount}개 삭제)");
        EditorUtility.DisplayDialog("초기화 완료",
            $"전체 초기화 완료!\nJSON {deletedCount}개 삭제", "확인");
    }

    // ─────────────────────────────────────────
    // 튜토리얼 진행 초기화
    // ─────────────────────────────────────────
    [MenuItem("Game/Data/[에디터] 튜토리얼 초기화")]
    static void ResetTutorial()
    {
        if (GameDataBridge.CurrentData != null)
        {
            GameDataBridge.CurrentData.tutorialPhase = 0;
            GameDataBridge.CurrentData.tutorialStep = -1;
            GameDataBridge.CurrentData.tutorialCompleted = false;
            Debug.Log("[GameDataResetTool] 인메모리 튜토리얼 상태 초기화 완료");
        }

        // JSON 파일에서도 초기화 (에디터 저장 파일만)
        string saveDir = Application.persistentDataPath + "/Saves/";
        if (Directory.Exists(saveDir))
        {
            string[] files = Directory.GetFiles(saveDir, "SaveData_EDITOR_*.json");
            int count = 0;
            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    // tutorialPhase, tutorialStep, tutorialCompleted 값 교체
                    json = System.Text.RegularExpressions.Regex.Replace(
                        json, "\"tutorialPhase\":\\s*\\d+", "\"tutorialPhase\":0");
                    json = System.Text.RegularExpressions.Regex.Replace(
                        json, "\"tutorialStep\":\\s*-?\\d+", "\"tutorialStep\":-1");
                    json = json.Replace("\"tutorialCompleted\":true", "\"tutorialCompleted\":false");
                    File.WriteAllText(file, json);
                    count++;
                    Debug.Log($"[GameDataResetTool] 튜토리얼 초기화: {Path.GetFileName(file)}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[GameDataResetTool] 파일 수정 실패: {file}\n{e.Message}");
                }
            }
            EditorUtility.DisplayDialog("튜토리얼 초기화",
                $"튜토리얼 상태가 초기화되었습니다.\n(인메모리 + JSON {count}개 수정)\n\n다음 접속 시 튜토리얼이 다시 시작됩니다.", "확인");
        }
        else
        {
            EditorUtility.DisplayDialog("튜토리얼 초기화",
                "인메모리 튜토리얼 상태 초기화 완료.\n(JSON 파일 없음)", "확인");
        }
    }

    // ─────────────────────────────────────────
    // 저장 폴더 열기
    // ─────────────────────────────────────────
    [MenuItem("Game/Data/저장 폴더 열기")]
    static void OpenSaveFolder()
    {
        string saveDir = Application.persistentDataPath + "/Saves/";
        if (!Directory.Exists(saveDir))
            Directory.CreateDirectory(saveDir);

        EditorUtility.RevealInFinder(saveDir);
    }

    // ─────────────────────────────────────────
    // 현재 저장 데이터 정보 출력
    // ─────────────────────────────────────────
    [MenuItem("Game/Data/현재 저장 데이터 정보 출력 (Console)")]
    static void PrintSaveInfo()
    {
        Debug.Log("========== 저장 데이터 정보 ==========");

        SaveData data = GameDataBridge.CurrentData;
        if (data != null)
        {
            Debug.Log($"[인메모리] 골드: {data.playerGold}");
            Debug.Log($"[인메모리] 젬: {data.playerGem}");
            Debug.Log($"[인메모리] 레벨: {data.playerLevel}");
            Debug.Log($"[인메모리] 캐릭터: {data.selectedCharacterName}");
            Debug.Log($"[인메모리] 계정: {data.accountID}");
        }
        else
        {
            Debug.Log("[인메모리] 데이터 없음");
        }

        string saveDir = Application.persistentDataPath + "/Saves/";
        if (Directory.Exists(saveDir))
        {
            string[] allFiles = Directory.GetFiles(saveDir, "*.json");
            Debug.Log($"JSON 저장 파일 ({allFiles.Length}개):");
            foreach (string f in allFiles)
                Debug.Log($"  - {Path.GetFileName(f)}");
        }
        Debug.Log("======================================");
    }
}
#endif