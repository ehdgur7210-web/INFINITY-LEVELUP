using UnityEngine;
using TMPro;

/// <summary>
/// 플로팅 데미지 텍스트 (피격 시 숫자가 떠오름)
///
/// 크리티컬 단계 시스템:
///   - 일반 공격:      흰색, 작은 크기
///   - 크리티컬:       빨간색, 중간 크기   (Critical!)
///   - 슈퍼 크리티컬:  주황색, 큰 크기     (Super Critical!!)
///   - 울트라 크리티컬: 노란색, 매우 큰 크기 (Ultra Critical!!!)
///
/// 사용법:
///   1. TextMeshPro가 있는 오브젝트에 추가하거나, 이 스크립트가 자동 추가됨
///   2. DamagePopupManager.Instance.ShowDamage() 호출하면 팝업이 생성됨
///   3. 일정 시간 후 자동으로 사라짐 (위로 떠오르면서 페이드아웃)
/// </summary>
public class DamagePopup : MonoBehaviour
{
    // 플로팅 팝업 동작 변수
    private TextMeshPro textMesh;       // 3D 월드 텍스트 (TextMeshPro)
    private float disappearTimer;       // 사라지기까지 남은 시간
    private Color textColor;            // 현재 텍스트 색상 (페이드아웃용)
    private Vector3 moveVector;         // 위로 떠오르는 방향/속도
    private float disappearSpeed = 3f;  // 페이드아웃 속도
    private Vector3 scaleVector;        // 크기 변화 (처음엔 크다가 작아짐)

    /// <summary>
    /// 팝업 초기화 (생성 직후 호출)
    /// </summary>
    /// <param name="damageAmount">표시할 데미지 수치</param>
    /// <param name="criticalTier">크리티컬 단계 (0=일반, 1=크리티컬, 2=슈퍼, 3=울트라)</param>
    public void Setup(float damageAmount, int criticalTier = 0)
    {
        // 기존 TextMeshPro 컴포넌트 가져오거나 없으면 추가
        textMesh = GetComponent<TextMeshPro>();

        // TextMeshPro가 없으면 자동으로 추가
        if (textMesh == null)
        {
            textMesh = gameObject.AddComponent<TextMeshPro>();
        }

        // 기본 텍스트 설정 처리
        textMesh.alignment = TextAlignmentOptions.Center;   // 중앙 정렬
        textMesh.sortingOrder = 200;                         // 다른 것보다 위에 그림

        // 크리티컬 단계에 따른 색상, 크기, 텍스트 설정 처리
        switch (criticalTier)
        {
            case 0: // 일반 공격 설정
                textMesh.text = $"{damageAmount:F0}";                        // 숫자만 표시
                textMesh.fontSize = 5f;                                      // 작은 크기
                textColor = Color.white;                                      // 흰색
                moveVector = new Vector3(                                     // 살짝 랜덤하게 떠오르도록 설정
                    Random.Range(-0.5f, 0.5f),  // X: 좌우 랜덤
                    1.5f,                        // Y: 위로
                    0f
                );
                disappearTimer = 0.8f;                                       // 0.8초 후 사라짐
                break;

            case 1: // 일반 크리티컬 (빨간색) 설정
                textMesh.text = $"{damageAmount:F0}!";                       // 느낌표 1개
                textMesh.fontSize = 7f;                                      // 중간 크기
                textColor = new Color(1f, 0.2f, 0.2f);                      // 빨간색
                moveVector = new Vector3(
                    Random.Range(-0.3f, 0.3f),
                    2f,                          // 더 높이 위로 떠오름
                    0f
                );
                disappearTimer = 1.0f;                                       // 1초
                break;

            case 2: // 슈퍼 크리티컬 (주황색) 설정
                textMesh.text = $"{damageAmount:F0}!!";                      // 느낌표 2개
                textMesh.fontSize = 9f;                                      // 큰 크기
                textColor = new Color(1f, 0.6f, 0f);                        // 주황색
                moveVector = new Vector3(
                    Random.Range(-0.2f, 0.2f),
                    2.5f,                        // 더 높이
                    0f
                );
                disappearTimer = 1.2f;                                       // 1.2초
                break;

            case 3: // 울트라 크리티컬 (노란색) 설정
                textMesh.text = $"{StarSpriteUtil.GetStars(1)}{damageAmount:F0}{StarSpriteUtil.GetStars(1)}"; // 별 + 숫자
                textMesh.fontSize = 12f;                                     // 매우 큰 크기
                textColor = new Color(1f, 1f, 0f);                          // 노란색
                moveVector = new Vector3(
                    0f,                          // 정중앙에서 떠오름
                    3f,                          // 매우 높이
                    0f
                );
                disappearTimer = 1.5f;                                       // 1.5초
                break;
        }

        // 초기 색상 적용 처리
        textMesh.color = textColor;

        // 울트라 크리티컬의 경우 처음에 크게 등장하는 "쾅!" 느낌 처리
        if (criticalTier >= 2)
        {
            transform.localScale = Vector3.one * 1.5f;   // 1.5배 크기로 시작
            scaleVector = Vector3.one * 1.5f;
        }
        else
        {
            transform.localScale = Vector3.one;
            scaleVector = Vector3.one;
        }
    }

    void Update()
    {
        if (textMesh == null) return;
        // 단계 1. 위로 떠오르는 처리
        // moveVector 방향으로 이동 (시간이 지날수록 느려짐)
        transform.position += moveVector * Time.deltaTime;
        moveVector -= moveVector * 3f * Time.deltaTime;    // 감속 효과

        // 단계 2. 타이머 감소 처리
        disappearTimer -= Time.deltaTime;

        // 단계 3. 사라지기 시작 (타이머가 0 이하) 처리
        if (disappearTimer < 0)
        {
            // 알파값을 낮춰 사라짐 (페이드아웃)
            textColor.a -= disappearSpeed * Time.deltaTime;
            textMesh.color = textColor;

            // 크기도 같이 줄어듦
            scaleVector -= Vector3.one * 2f * Time.deltaTime;
            scaleVector = Vector3.Max(scaleVector, Vector3.zero);   // 0 아래로는 안 줄어들게
            transform.localScale = scaleVector;

            // 완전히 사라지면 오브젝트 삭제
            if (textColor.a <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
}
