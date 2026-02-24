using UnityEngine;

/// <summary>
/// 데미지 팝업 매니저 (싱글톤)
/// 
/// ★ 역할:
///   - 어디서든 DamagePopupManager.Instance.ShowDamage() 호출하면
///     해당 위치에 데미지 숫자 팝업이 떠오름
///   - 크리티컬 등급에 따라 자동으로 색상/크기가 달라짐
/// 
/// ★ 사용법:
///   1. 빈 게임오브젝트에 이 스크립트를 붙임
///   2. (선택) damagePopupPrefab에 TextMeshPro 프리팹 연결
///      → 안 넣으면 자동 생성됨!
///   3. 플레이어 공격 코드에서 호출:
///      DamagePopupManager.Instance.ShowDamage(몬스터위치, 데미지, 크리티컬등급);
/// 
/// ★ 크리티컬 등급:
///   0 = 일반 (흰색)
///   1 = 크리티컬 (빨강)
///   2 = 슈퍼 크리티컬 (주황)
///   3 = 울트라 크리티컬 (노랑)
/// </summary>
public class DamagePopupManager : MonoBehaviour
{
    // ─── 싱글톤 ───
    public static DamagePopupManager Instance { get; private set; }

    [Header("팝업 프리팹 (선택사항)")]
    [Tooltip("TextMeshPro가 붙은 프리팹. 비워두면 자동 생성됩니다.")]
    [SerializeField] private GameObject damagePopupPrefab;

    void Awake()
    {
        // 싱글톤 설정 (하나만 존재하도록)
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ★ 메인 함수: 데미지 팝업을 화면에 표시
    /// </summary>
    /// <param name="position">팝업이 뜰 월드 좌표 (보통 몬스터 위치)</param>
    /// <param name="damageAmount">표시할 데미지 숫자</param>
    /// <param name="criticalTier">크리티컬 등급 (0=일반, 1=크리티컬, 2=슈퍼, 3=울트라)</param>
    public void ShowDamage(Vector3 position, float damageAmount, int criticalTier = 0)
    {
        // ─── 팝업 위치를 약간 위로 올림 (몬스터 머리 위) ───
        Vector3 spawnPos = position + new Vector3(
            Random.Range(-0.3f, 0.3f),   // X: 약간 좌우 랜덤 (겹침 방지)
            0.5f,                         // Y: 머리 위로
            0f
        );

        // ─── 팝업 오브젝트 생성 ───
        GameObject popupObj;

        if (damagePopupPrefab != null)
        {
            // 프리팹이 있으면 프리팹으로 생성
            popupObj = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // 프리팹이 없으면 빈 오브젝트 생성
            popupObj = new GameObject("DamagePopup");
            popupObj.transform.position = spawnPos;
        }

        // ─── DamagePopup 스크립트 추가 및 초기화 ───
        DamagePopup popup = popupObj.GetComponent<DamagePopup>();
        if (popup == null)
        {
            popup = popupObj.AddComponent<DamagePopup>();
        }

        // Setup 호출: 데미지 수치와 크리티컬 등급 전달
        popup.Setup(damageAmount, criticalTier);
    }

    /// <summary>
    /// 간편 함수: 일반 데미지 표시 (크리티컬 아님)
    /// </summary>
    public void ShowNormalDamage(Vector3 position, float damageAmount)
    {
        ShowDamage(position, damageAmount, 0);
    }

    /// <summary>
    /// 간편 함수: 회복량 표시 (초록색)
    /// </summary>
    public void ShowHeal(Vector3 position, float healAmount)
    {
        Vector3 spawnPos = position + new Vector3(0f, 0.5f, 0f);

        GameObject popupObj = new GameObject("HealPopup");
        popupObj.transform.position = spawnPos;

        // TextMeshPro 추가
        TMPro.TextMeshPro textMesh = popupObj.AddComponent<TMPro.TextMeshPro>();
        textMesh.text = $"+{healAmount:F0}";
        textMesh.fontSize = 5f;
        textMesh.color = Color.green;
        textMesh.alignment = TMPro.TextAlignmentOptions.Center;
        textMesh.sortingOrder = 200;

        // DamagePopup으로 떠오르는 효과 적용
        DamagePopup popup = popupObj.AddComponent<DamagePopup>();
        popup.Setup(healAmount, 0);
        // 색상을 초록으로 덮어쓰기
        textMesh.color = Color.green;

        Destroy(popupObj, 2f);
    }
}