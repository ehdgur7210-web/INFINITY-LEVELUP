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
    // 에디터 PlayerPrefs 초기화
    // ─────────────────────────────────────────
    [MenuItem("Game/Data/[에디터] PlayerPrefs 초기화")]
    static void ResetEditorPlayerPrefs()
    {
        PlayerPrefs.DeleteKey("EDITOR_PlayerGold");
        PlayerPrefs.DeleteKey("EDITOR_PlayerGem");
        PlayerPrefs.DeleteKey("EDITOR_PlayerExp");
        PlayerPrefs.DeleteKey("EDITOR_PlayerLevel");
        PlayerPrefs.DeleteKey("EDITOR_BuildVersion");
        PlayerPrefs.Save();

        Debug.Log("[GameDataResetTool]  에디터 PlayerPrefs 초기화 완료");
        EditorUtility.DisplayDialog("초기화 완료",
            "에디터 PlayerPrefs가 초기화되었습니다.\n(빌드 데이터는 영향 없음)", "확인");
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

        PlayerPrefs.DeleteKey("EDITOR_PlayerGold");
        PlayerPrefs.DeleteKey("EDITOR_PlayerGem");
        PlayerPrefs.DeleteKey("EDITOR_PlayerExp");
        PlayerPrefs.DeleteKey("EDITOR_PlayerLevel");
        PlayerPrefs.DeleteKey("EDITOR_BuildVersion");
        PlayerPrefs.Save();

        string saveDir = Application.persistentDataPath + "/Saves/";
        int deletedCount = 0;
        if (Directory.Exists(saveDir))
        {
            string[] files = Directory.GetFiles(saveDir, "SaveData_EDITOR_*");
            foreach (string file in files)
            {
                File.Delete(file);
                deletedCount++;
            }
        }

        Debug.Log($"[GameDataResetTool] 에디터 전체 초기화 완료 (JSON {deletedCount}개 삭제)");
        EditorUtility.DisplayDialog("초기화 완료",
            $"에디터 데이터 전체 초기화!\nJSON {deletedCount}개 삭제\n(빌드 데이터는 영향 없음)", "확인");
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
        Debug.Log($"[에디터] 골드: {PlayerPrefs.GetInt("EDITOR_PlayerGold", -1)}");
        Debug.Log($"[에디터] 젬: {PlayerPrefs.GetInt("EDITOR_PlayerGem", -1)}");
        Debug.Log($"[에디터] 레벨: {PlayerPrefs.GetInt("EDITOR_PlayerLevel", -1)}");
        Debug.Log($"[에디터] 버전: {PlayerPrefs.GetString("EDITOR_BuildVersion", "없음")}");
        Debug.Log($"[빌드] 골드: {PlayerPrefs.GetInt("PlayerGold", -1)}");
        Debug.Log($"[빌드] 젬: {PlayerPrefs.GetInt("PlayerGem", -1)}");
        Debug.Log($"[빌드] 레벨: {PlayerPrefs.GetInt("PlayerLevel", -1)}");
        Debug.Log($"[빌드] 버전: {PlayerPrefs.GetString("BUILD_BuildVersion", "없음")}");

        string saveDir = Application.persistentDataPath + "/Saves/";
        if (Directory.Exists(saveDir))
        {
            string[] allFiles = Directory.GetFiles(saveDir, "SaveData_*");
            Debug.Log($"JSON 저장 파일 ({allFiles.Length}개):");
            foreach (string f in allFiles)
                Debug.Log($"  - {Path.GetFileName(f)}");
        }
        Debug.Log("======================================");
    }
}
#endif