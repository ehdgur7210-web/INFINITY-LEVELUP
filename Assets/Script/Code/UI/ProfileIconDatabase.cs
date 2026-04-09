using UnityEngine;

/// <summary>
/// 캐릭터 클래스별 프로필 아이콘 DB
/// 친구창/채팅창에서 클래스 인덱스로 스프라이트 룩업.
///
/// [사용법]
///   1. Project 창에서 우클릭 → Create → Game → ProfileIconDatabase
///   2. 만든 .asset에 sprites 배열 채우기 (0=전사, 1=원거리, 2=마법사 ...)
///   3. ProfileIconDatabase.Instance 정적 접근으로 사용
/// </summary>
[CreateAssetMenu(fileName = "ProfileIconDatabase", menuName = "Game/Profile Icon Database")]
public class ProfileIconDatabase : ScriptableObject
{
    [Tooltip("클래스 인덱스 → 프로필 스프라이트 (0=전사, 1=원거리, 2=마법사, 그 외 fallback)")]
    public Sprite[] classSprites;

    [Tooltip("매칭 안 될 때 사용할 기본 아이콘")]
    public Sprite defaultSprite;

    private static ProfileIconDatabase _instance;

    /// <summary>Resources/ProfileIconDatabase.asset 자동 로드</summary>
    public static ProfileIconDatabase Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<ProfileIconDatabase>("ProfileIconDatabase");
            return _instance;
        }
    }

    /// <summary>클래스 인덱스로 아이콘 가져오기 (DB가 없거나 인덱스 범위 밖이면 default)</summary>
    public static Sprite GetIcon(int classIndex)
    {
        var db = Instance;
        if (db == null) return null;

        if (db.classSprites != null && classIndex >= 0 && classIndex < db.classSprites.Length)
        {
            var sp = db.classSprites[classIndex];
            if (sp != null) return sp;
        }
        return db.defaultSprite;
    }
}
