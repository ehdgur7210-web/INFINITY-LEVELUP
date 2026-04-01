using System;
using UnityEngine;
using BackEnd;
using LitJson;

/// <summary>
/// ══════════════════════════════════════════════════════════
/// BackendGameDataManager — 게임 데이터 서버 동기화
/// ══════════════════════════════════════════════════════════
///
/// [수정 내역]
///   Bug6: Insert 성공 콜백에서 GetReturnValuetoJSON()으로 inDate를 얻으려 했음
///         → 뒤끝 Insert 응답 JSON에 inDate 미포함
///         → Insert 성공 후 GetMyData 재조회로 RowInDate를 안전하게 취득하도록 수정
///
///   Bug7: "table not found" 에러
///         → 뒤끝 SDK는 테이블명 대소문자를 구분함
///         → Inspector SerializeField 값이 콘솔 테이블명과 불일치 가능
///         → Start()에서 테이블명 검증 로그 + 모든 API 호출에 상세 에러 로그 추가
///
///   Bug8: Insert 404 에러 — 중복 Insert 방지
///         → 뒤끝 일대일 테이블은 유저당 1행만 허용, 이미 행이 있으면 Insert 실패
///         → _rowInDate가 null이어도 서버에 행이 있을 수 있음 (DDOL 재생성, 씬 전환 등)
///         → Insert 전 GetMyData로 기존 행 확인 → 있으면 Update, 없으면 Insert
///
/// 뒤끝 콘솔 테이블 설정:
///   테이블명 : gamedata  ← 콘솔에서 정확한 이름 확인 필수 (대소문자 구분!)
///   컬럼     : slot_index    (int32)   기본값 0
///              player_level  (int32)   기본값 1
///              player_gold   (int64)   기본값 0   ← long
///              character_name(string)  기본값 ""
///              save_json     (string)  기본값 ""
///              combat_power  (int32)   기본값 0   ← 랭킹용
///              farm_score    (int64)   기본값 0   ← long, 랭킹용
/// ══════════════════════════════════════════════════════════
/// </summary>
public class BackendGameDataManager : MonoBehaviour
{
    public static BackendGameDataManager Instance { get; private set; }

    [Header("뒤끝 테이블 이름 (콘솔과 대소문자 정확히 일치해야 함)")]
    [SerializeField] private string tableName = "gamedata";

    /// <summary>서버 데이터 로드 완료 여부</summary>
    public bool IsDataLoaded { get; private set; }

    /// <summary>서버 저장/로드 진행 중 여부</summary>
    public bool IsBusy { get; private set; }

    /// <summary>서버 데이터 로드 완료 시 발생</summary>
    public static event Action OnServerDataLoaded;

    /// <summary>서버 저장 완료 시 발생</summary>
    public static event Action OnServerDataSaved;

    // 서버 행의 inDate (Update/Delete 시 필요)
    private string _rowInDate;

    /// <summary>서버 행의 inDate (BackendRankingManager 등 외부에서 참조)</summary>
    public string RowInDate => _rowInDate;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Debug.Log("[ManagerInit] BackendGameDataManager가 생성되었습니다.");
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // ★ 테이블명 검증 — 공백 제거 + 로그
        tableName = tableName.Trim();
        Debug.Log($"[BackendGameData] ★ 초기화 — 테이블명: \"{tableName}\" (길이:{tableName.Length})");

        if (string.IsNullOrEmpty(tableName))
            Debug.LogError("[BackendGameData] ❌ tableName이 비어있음! Inspector에서 뒤끝 콘솔 테이블명을 입력하세요.");

        // ★ 테이블 존재 여부 사전 확인
        ValidateTable();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ══════════════════════════════════════════════════════
    //  테이블 사전 검증
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Start 시 테이블 접근 가능 여부를 미리 확인합니다.
    /// table not found 에러를 게임 시작 시 즉시 감지할 수 있습니다.
    /// </summary>
    private void ValidateTable()
    {
        if (BackendManager.Instance == null || !BackendManager.Instance.IsLoggedIn)
        {
            Debug.Log("[BackendGameData] 로그인 전 → 테이블 검증 스킵 (로그인 후 자동 검증)");
            return;
        }

        Debug.Log($"[BackendGameData] ▶ 테이블 검증 시작 — GetMyData(\"{tableName}\")");

        Where where = new Where();
        where.Equal("slot_index", 0);

        Backend.GameData.GetMyData(tableName, where, callback =>
        {
            if (callback.IsSuccess())
            {
                Debug.Log($"[BackendGameData] ✅ 테이블 \"{tableName}\" 접근 성공");
            }
            else
            {
                string statusCode = callback.GetStatusCode();
                string errorCode = callback.GetErrorCode();
                string message = callback.GetMessage();

                Debug.LogError($"[BackendGameData] ❌ 테이블 검증 실패!\n" +
                    $"  테이블명: \"{tableName}\"\n" +
                    $"  statusCode: {statusCode}\n" +
                    $"  errorCode: {errorCode}\n" +
                    $"  message: {message}\n" +
                    $"  → 뒤끝 콘솔에서 테이블명을 확인하세요 (대소문자 구분!)");
            }
        });
    }

    // ══════════════════════════════════════════════════════
    //  서버에서 로드
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 현재 슬롯의 게임 데이터를 서버에서 로드합니다.
    /// CharacterSelectManager.StartGameWithServerData()에서 호출됩니다.
    /// </summary>
    public void LoadFromServer(int slot, Action<bool> onComplete = null)
    {
        if (BackendManager.Instance == null || !BackendManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[BackendGameData] 로그인되지 않음 → 로드 스킵");
            onComplete?.Invoke(false);
            return;
        }

        IsBusy = true;

        Where where = new Where();
        where.Equal("slot_index", slot);

        Debug.Log($"[BackendGameData] ▶ LoadFromServer — GetMyData(\"{tableName}\", slot:{slot})");

        Backend.GameData.GetMyData(tableName, where, callback =>
        {
            IsBusy = false;

            if (!callback.IsSuccess())
            {
                string statusCode = callback.GetStatusCode();
                string errorCode = callback.GetErrorCode();
                string message = callback.GetMessage();

                // ★ 404 = 해당 슬롯 데이터 없음 (에러가 아닌 정상 케이스)
                // → _rowInDate null → 로컬 데이터 사용 → 자동 Insert로 서버에 첫 행 생성
                if (statusCode == "404")
                {
                    Debug.Log($"[BackendGameData] LoadFromServer 404 — 서버에 데이터 없음 → 로컬 데이터 사용 + 자동 Insert 예약");
                    _rowInDate = null;
                    IsDataLoaded = true;
                    onComplete?.Invoke(false);

                    // ★ 첫 데이터 자동 Insert (CurrentData가 준비되면 실행)
                    AutoInsertFirstData(slot);
                    return;
                }

                Debug.LogError($"[BackendGameData] ❌ LoadFromServer 실패!\n" +
                    $"  호출: GetMyData(\"{tableName}\")\n" +
                    $"  statusCode: {statusCode}\n" +
                    $"  errorCode: {errorCode}\n" +
                    $"  message: {message}");
                onComplete?.Invoke(false);
                return;
            }

            Debug.Log($"[BackendGameData] ◀ LoadFromServer — GetMyData 성공");

            JsonData rows = callback.FlattenRows();

            if (rows.Count == 0)
            {
                Debug.Log($"[BackendGameData] 서버에 슬롯 {slot} 데이터 없음 → 로컬 유지 + 자동 Insert 예약");
                _rowInDate = null;
                IsDataLoaded = true;
                onComplete?.Invoke(false);

                // ★ 첫 데이터 자동 Insert
                AutoInsertFirstData(slot);
                return;
            }

            JsonData row = rows[0];

            // inDate 저장 (Update 시 필요)
            _rowInDate = row.ContainsKey("inDate") ? row["inDate"].ToString() : null;
            Debug.Log($"[BackendGameData] RowInDate 취득: {(_rowInDate ?? "null")}");

            // save_json → SaveData 복원
            if (!row.ContainsKey("save_json"))
            {
                Debug.LogWarning("[BackendGameData] save_json 컬럼 없음");
                onComplete?.Invoke(false);
                return;
            }

            string saveJson = row["save_json"].ToString();
            if (!string.IsNullOrEmpty(saveJson))
            {
                try
                {
                    SaveData serverData = JsonUtility.FromJson<SaveData>(saveJson);
                    GameDataBridge.SetData(serverData);
                    Debug.Log($"[BackendGameData] ✅ 서버 로드 완료 (슬롯:{slot}, Lv:{serverData.playerLevel})");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BackendGameData] JSON 파싱 실패: {e.Message}");
                    onComplete?.Invoke(false);
                    return;
                }
            }

            IsDataLoaded = true;
            OnServerDataLoaded?.Invoke();
            onComplete?.Invoke(true);
        });
    }

    // ══════════════════════════════════════════════════════
    //  서버에 저장
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 현재 GameDataBridge.CurrentData를 서버에 저장합니다.
    /// SaveLoadManager.SaveGame() 내부에서 자동 호출됩니다.
    /// </summary>
    public void SaveToServer(Action<bool> onComplete = null)
    {
        // ★ 로그인 상태 검증 (커스텀 플래그 + SDK UserInDate)
        bool customLoggedIn = BackendManager.Instance != null && BackendManager.Instance.IsLoggedIn;
        string userInDate = null;
        try { userInDate = Backend.UserInDate; } catch { }

        Debug.Log($"[BackendGameData] 로그인 상태 확인 — customLoggedIn:{customLoggedIn}, Backend.UserInDate:{(userInDate ?? "null")}");

        if (!customLoggedIn)
        {
            Debug.LogWarning("[BackendGameData] 로그인되지 않음 → 저장 스킵");
            onComplete?.Invoke(false);
            return;
        }

        if (string.IsNullOrEmpty(userInDate))
        {
            Debug.LogError("[BackendGameData] ❌ BackendManager.IsLoggedIn=true인데 Backend.UserInDate가 null! SDK 세션 만료 가능성");
            onComplete?.Invoke(false);
            return;
        }

        SaveData data = GameDataBridge.CurrentData;
        if (data == null)
        {
            Debug.LogWarning("[BackendGameData] 저장할 데이터 없음");
            onComplete?.Invoke(false);
            return;
        }

        IsBusy = true;

        int cp = CombatPowerManager.Instance?.TotalCombatPower ?? 0;
        long farmScore = FarmManager.Instance?.GetCropPoints() ?? 0;

        Debug.Log($"[BackendGameData] ▶ SaveToServer 시작 — 테이블:\"{tableName}\", slot:{data.activeCharacterSlot}, Lv:{data.playerLevel}, CP:{cp}, Farm:{farmScore}, RowInDate:{(_rowInDate ?? "null")}");

        string saveJson = JsonUtility.ToJson(data);

        // ★ save_json 크기 진단 로그
        int jsonBytes = System.Text.Encoding.UTF8.GetByteCount(saveJson);
        Debug.Log($"[BackendGameData] save_json 크기: {saveJson.Length}자 / {jsonBytes} bytes ({jsonBytes / 1024}KB)");
        if (jsonBytes > 500_000)
            Debug.LogWarning($"[BackendGameData] ⚠ save_json이 500KB 초과! ({jsonBytes} bytes) — 뒤끝 제한 초과 가능성 있음");

        Param param = new Param();
        param.Add("slot_index", data.activeCharacterSlot);
        param.Add("player_level", data.playerLevel);
        param.Add("player_gold", data.playerGold);
        param.Add("character_name", data.selectedCharacterName ?? "");
        param.Add("save_json", saveJson);
        // 랭킹용 컬럼도 함께 저장
        param.Add("combat_power", cp);
        param.Add("farm_score", farmScore);

        if (string.IsNullOrEmpty(_rowInDate))
        {
            Debug.Log("[BackendGameData] RowInDate 없음 → Insert 경로");
            InsertToServer(param, onComplete);
        }
        else
        {
            Debug.Log($"[BackendGameData] RowInDate 있음 → Update 경로 ({_rowInDate})");
            UpdateToServer(param, onComplete);
        }
    }

    /// <summary>
    /// ★ Bug8 수정: Insert 전에 서버에 기존 행이 있는지 확인.
    /// _rowInDate가 null이어도 서버에 행이 있을 수 있음
    /// (DDOL 재생성, 씬 전환, AutoInsert 경쟁 등)
    /// → 기존 행 있으면 Update로 전환, 없으면 Insert 실행
    /// </summary>
    private void InsertToServer(Param param, Action<bool> onComplete)
    {
        SaveData data = GameDataBridge.CurrentData;
        int slot = data?.activeCharacterSlot ?? 0;

        Debug.Log($"[BackendGameData] ▶ Insert 전 기존 행 확인 — GetMyData(\"{tableName}\", slot:{slot})");

        Where where = new Where();
        where.Equal("slot_index", slot);

        Backend.GameData.GetMyData(tableName, where, checkCallback =>
        {
            if (checkCallback.IsSuccess())
            {
                JsonData rows = checkCallback.FlattenRows();
                if (rows != null && rows.Count > 0 && rows[0].ContainsKey("inDate"))
                {
                    // ★ 이미 행이 존재 → Insert가 아니라 Update 필요
                    _rowInDate = rows[0]["inDate"].ToString();
                    Debug.LogWarning($"[BackendGameData] ⚠ 서버에 이미 행 존재! (inDate:{_rowInDate}) → Insert 대신 Update로 전환");
                    UpdateToServer(param, onComplete);
                    return;
                }
            }

            // 행이 없음 → Insert 실행
            DoInsert(param, onComplete);
        });
    }

    /// <summary>실제 Insert 실행</summary>
    private void DoInsert(Param param, Action<bool> onComplete)
    {
        Debug.Log($"[BackendGameData] ▶ Insert 호출 — Backend.GameData.Insert(\"{tableName}\", param)");

        Backend.GameData.Insert(tableName, param, callback =>
        {
            IsBusy = false;

            if (!callback.IsSuccess())
            {
                string statusCode = callback.GetStatusCode();
                string errorCode = callback.GetErrorCode();
                string message = callback.GetMessage();

                Debug.LogError($"[BackendGameData] ❌ Insert 실패!\n" +
                    $"  호출: Backend.GameData.Insert(\"{tableName}\")\n" +
                    $"  statusCode: {statusCode}\n" +
                    $"  errorCode: {errorCode}\n" +
                    $"  message: {message}\n" +
                    $"  가능한 원인:\n" +
                    $"  1. 테이블명 \"{tableName}\"이 뒤끝 콘솔과 불일치 (대소문자)\n" +
                    $"  2. 컬럼명이 콘솔과 불일치\n" +
                    $"  3. 뒤끝 콘솔에서 '클라이언트 Insert 허용'이 꺼져있음\n" +
                    $"  4. 일대일 테이블에서 이미 행 존재 (중복 Insert)");
                onComplete?.Invoke(false);
                return;
            }

            Debug.Log("[BackendGameData] ✅ Insert 성공 → RowInDate 재조회 시작");

            FetchRowInDate(() =>
            {
                Debug.Log($"[BackendGameData] ✅ 서버 저장 완료 (Insert, inDate:{_rowInDate})");
                OnServerDataSaved?.Invoke();
                onComplete?.Invoke(true);
            });
        });
    }

    private void UpdateToServer(Param param, Action<bool> onComplete)
    {
        Debug.Log($"[BackendGameData] ▶ Update 호출 — Backend.GameData.UpdateV2(\"{tableName}\", \"{_rowInDate}\")");

        Backend.GameData.UpdateV2(tableName, _rowInDate, Backend.UserInDate, param, callback =>
        {
            IsBusy = false;

            if (!callback.IsSuccess())
            {
                string statusCode = callback.GetStatusCode();
                string errorCode = callback.GetErrorCode();
                string message = callback.GetMessage();

                Debug.LogError($"[BackendGameData] ❌ Update 실패!\n" +
                    $"  호출: Backend.GameData.UpdateV2(\"{tableName}\", \"{_rowInDate}\")\n" +
                    $"  statusCode: {statusCode}\n" +
                    $"  errorCode: {errorCode}\n" +
                    $"  message: {message}");

                // 행이 삭제된 경우 Insert로 재시도
                _rowInDate = null;
                IsBusy = true;
                Debug.Log("[BackendGameData] Update 실패 → Insert로 재시도");
                InsertToServer(param, onComplete);
                return;
            }

            Debug.Log("[BackendGameData] ✅ 서버 저장 완료 (Update)");
            OnServerDataSaved?.Invoke();
            onComplete?.Invoke(true);
        });
    }

    /// <summary>
    /// Insert 후 GetMyData로 재조회하여 _rowInDate를 취득합니다.
    /// (뒤끝 Insert 응답에 inDate가 포함되지 않으므로 재조회 필요)
    /// </summary>
    private void FetchRowInDate(Action onComplete)
    {
        SaveData data = GameDataBridge.CurrentData;
        int slot = data?.activeCharacterSlot ?? 0;

        Where where = new Where();
        where.Equal("slot_index", slot);

        Debug.Log($"[BackendGameData] ▶ FetchRowInDate — GetMyData(\"{tableName}\", slot:{slot})");

        Backend.GameData.GetMyData(tableName, where, callback =>
        {
            if (callback.IsSuccess())
            {
                JsonData rows = callback.FlattenRows();
                if (rows != null && rows.Count > 0 && rows[0].ContainsKey("inDate"))
                {
                    _rowInDate = rows[0]["inDate"].ToString();
                    Debug.Log($"[BackendGameData] ✅ RowInDate 취득: {_rowInDate}");
                }
                else
                {
                    Debug.LogWarning("[BackendGameData] ⚠ RowInDate 취득 실패: 행 없음");
                }
            }
            else
            {
                string statusCode = callback.GetStatusCode();
                string errorCode = callback.GetErrorCode();
                string message = callback.GetMessage();

                Debug.LogError($"[BackendGameData] ❌ FetchRowInDate 실패!\n" +
                    $"  호출: GetMyData(\"{tableName}\")\n" +
                    $"  statusCode: {statusCode}\n" +
                    $"  errorCode: {errorCode}\n" +
                    $"  message: {message}");
            }

            onComplete?.Invoke();
        });
    }

    // ══════════════════════════════════════════════════════
    //  첫 데이터 자동 Insert
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// LoadFromServer에서 데이터가 없을 때 (404 또는 빈 rows)
    /// 지연 후 CurrentData를 서버에 자동 Insert합니다.
    /// CharacterSelectManager가 로컬 데이터를 로드한 후 실행되도록
    /// 코루틴으로 2초 대기 후 SaveToServer를 호출합니다.
    /// </summary>
    private void AutoInsertFirstData(int slot)
    {
        StartCoroutine(AutoInsertCoroutine(slot));
    }

    private System.Collections.IEnumerator AutoInsertCoroutine(int slot)
    {
        // CharacterSelectManager가 로컬 데이터를 로드할 시간 확보
        yield return new WaitForSeconds(2f);

        // 이미 다른 경로에서 RowInDate가 설정됐으면 스킵
        if (!string.IsNullOrEmpty(_rowInDate))
        {
            Debug.Log("[BackendGameData] AutoInsert 스킵 — RowInDate 이미 있음");
            yield break;
        }

        SaveData data = GameDataBridge.CurrentData;
        if (data == null)
        {
            Debug.LogWarning("[BackendGameData] AutoInsert 스킵 — CurrentData null");
            yield break;
        }

        Debug.Log($"[BackendGameData] ▶ AutoInsert 실행 — 슬롯:{slot}, Lv:{data.playerLevel}");
        SaveToServer(success =>
        {
            if (success)
                Debug.Log($"[BackendGameData] ✅ AutoInsert 완료 — RowInDate:{_rowInDate}");
            else
                Debug.LogWarning("[BackendGameData] ⚠ AutoInsert 실패 — 다음 SaveGame()에서 재시도됨");
        });
    }

    // ══════════════════════════════════════════════════════
    //  슬롯 삭제
    // ══════════════════════════════════════════════════════

    /// <summary>서버에서 해당 슬롯의 데이터를 삭제합니다.</summary>
    public void DeleteFromServer(int slot, Action<bool> onComplete = null)
    {
        if (BackendManager.Instance == null || !BackendManager.Instance.IsLoggedIn || string.IsNullOrEmpty(_rowInDate))
        {
            onComplete?.Invoke(false);
            return;
        }

        Backend.GameData.DeleteV2(tableName, _rowInDate, Backend.UserInDate, callback =>
        {
            if (callback.IsSuccess())
            {
                _rowInDate = null;
                Debug.Log($"[BackendGameData] ✅ 서버 슬롯 {slot} 삭제 완료");
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogError($"[BackendGameData] ❌ 삭제 실패: statusCode={callback.GetStatusCode()}, errorCode={callback.GetErrorCode()}, msg={callback.GetMessage()}");
                onComplete?.Invoke(false);
            }
        });
    }
}
