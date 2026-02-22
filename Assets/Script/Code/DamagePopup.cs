using UnityEngine;
using TMPro;

/// <summary>
/// 데미지 팝업 텍스트 (몬스터 피격 시 숫자가 떠오름)
/// 
/// ★ 크리티컬 등급 시스템:
///   - 일반 공격:      흰색, 작은 글씨
///   - 크리티컬:       빨간색, 중간 글씨   (Critical!)
///   - 슈퍼 크리티컬:  주황색, 큰 글씨     (Super Critical!!)
///   - 울트라 크리티컬: 노란색, 매우 큰 글씨 (Ultra Critical!!!)
/// 
/// ★ 사용법:
///   1. TextMeshPro가 있는 프리팹을 만들거나, 이 스크립트가 자동 생성함
///   2. DamagePopupManager.Instance.ShowDamage() 호출하면 팝업이 뜸
///   3. 일정 시간 후 자동으로 사라짐 (위로 떠오르면서 페이드아웃)
/// </summary>
public class DamagePopup : MonoBehaviour
{
    // ─── 팝업 설정값 ───
    private TextMeshPro textMesh;       // 3D 월드 텍스트 (TextMeshPro)
    private float disappearTimer;       // 사라지기까지 남은 시간
    private Color textColor;            // 현재 텍스트 색상 (페이드아웃용)
    private Vector3 moveVector;         // 위로 떠오르는 방향/속도
    private float disappearSpeed = 3f;  // 페이드아웃 속도
    private Vector3 scaleVector;        // 크기 변화 (처음에 커졌다가 줄어듦)

    /// <summary>
    /// 팝업 초기화 (생성 직후 호출됨)
    /// </summary>
    /// <param name="damageAmount">표시할 데미지 수치</param>
    /// <param name="criticalTier">크리티컬 등급 (0=일반, 1=크리티컬, 2=슈퍼, 3=울트라)</param>
    public void Setup(float damageAmount, int criticalTier = 0)
    {
        // ─── TextMeshPro 컴포넌트 가져오기 ───
        textMesh = GetComponent<TextMeshPro>();

        // TextMeshPro가 없으면 자동으로 추가
        if (textMesh == null)
        {
            textMesh = gameObject.AddComponent<TextMeshPro>();
        }

        // ─── 기본 텍스트 설정 ───
        textMesh.alignment = TextAlignmentOptions.Center;   // 가운데 정렬
        textMesh.sortingOrder = 200;                         // 다른 것보다 위에 그림

        // ─── 크리티컬 등급에 따라 색상, 크기, 텍스트 결정 ───
        switch (criticalTier)
        {
            case 0: // ── 일반 공격 ──
                textMesh.text = $"{damageAmount:F0}";                        // 숫자만 표시
                textMesh.fontSize = 5f;                                      // 작은 글씨
                textColor = Color.white;                                      // 흰색
                moveVector = new Vector3(                                     // 약간 랜덤한 방향으로 떠오름
                    Random.Range(-0.5f, 0.5f),  // X: 좌우 랜덤
                    1.5f,                        // Y: 위로
                    0f
                );
                disappearTimer = 0.8f;                                       // 0.8초 후 사라짐
                break;

            case 1: // ── 크리티컬 (빨간색) ──
                textMesh.text = $"{damageAmount:F0}!";                       // 느낌표 1개
                textMesh.fontSize = 7f;                                      // 중간 글씨
                textColor = new Color(1f, 0.2f, 0.2f);                      // 빨간색
                moveVector = new Vector3(
                    Random.Range(-0.3f, 0.3f),
                    2f,                          // 좀 더 높이 떠오름
                    0f
                );
                disappearTimer = 1.0f;                                       // 1초
                break;

            case 2: // ── 슈퍼 크리티컬 (주황색) ──
                textMesh.text = $"{damageAmount:F0}!!";                      // 느낌표 2개
                textMesh.fontSize = 9f;                                      // 큰 글씨
                textColor = new Color(1f, 0.6f, 0f);                        // 주황색
                moveVector = new Vector3(
                    Random.Range(-0.2f, 0.2f),
                    2.5f,                        // 더 높이
                    0f
                );
                disappearTimer = 1.2f;                                       // 1.2초
                break;

            case 3: // ── 울트라 크리티컬 (노란색) ──
                textMesh.text = $"★{damageAmount:F0}★";                     // 별 + 숫자
                textMesh.fontSize = 12f;                                     // 매우 큰 글씨
                textColor = new Color(1f, 1f, 0f);                          // 노란색
                moveVector = new Vector3(
                    0f,                          // 정중앙에서 떠오름
                    3f,                          // 매우 높이
                    0f
                );
                disappearTimer = 1.5f;                                       // 1.5초
                break;
        }

        // ─── 색상 적용 ───
        textMesh.color = textColor;

        // ─── 울트라 크리티컬은 시작 스케일을 크게 해서 "뻥!" 느낌 ───
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
        // ─── 1. 위로 떠오르기 ───
        // moveVector 방향으로 이동 (시간이 지날수록 느려짐)
        transform.position += moveVector * Time.deltaTime;
        moveVector -= moveVector * 3f * Time.deltaTime;    // 감속 효과

        // ─── 2. 타이머 감소 ───
        disappearTimer -= Time.deltaTime;

        // ─── 3. 사라지기 시작 (타이머가 0 이하) ───
        if (disappearTimer < 0)
        {
            // 투명도를 점점 줄임 (페이드아웃)
            textColor.a -= disappearSpeed * Time.deltaTime;
            textMesh.color = textColor;

            // 크기도 점점 줄임
            scaleVector -= Vector3.one * 2f * Time.deltaTime;
            scaleVector = Vector3.Max(scaleVector, Vector3.zero);   // 0 이하로 안 내려가게
            transform.localScale = scaleVector;

            // 완전히 투명해지면 오브젝트 파괴
            if (textColor.a <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
}