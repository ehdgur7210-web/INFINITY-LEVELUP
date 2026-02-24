using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// SPUM 캐릭터 장비 비주얼 시스템
/// </summary>
public class SPUMEquipmentVisualSystem : MonoBehaviour
{
    [Header("SPUM 참조")]
    [SerializeField] private SPUM_Prefabs spumPrefabs;

    [Header("파츠 매칭 (자동 검색)")]
    private Dictionary<string, MatchingElement> allMatchingElements = new Dictionary<string, MatchingElement>();

    // 무기는 Left/Right로 완전 분리
    private MatchingElement weaponLeftElement;
    private MatchingElement weaponRightElement;

    // 방어구
    private MatchingElement helmetElement;
    private MatchingElement armorElement;
    private MatchingElement glovesElement;
    private MatchingElement bootsElement;

    [Header("디버그")]
    [SerializeField] private bool debugMode = true;

    void Awake()
    {
        if (spumPrefabs == null)
        {
            spumPrefabs = GetComponentInChildren<SPUM_Prefabs>();
        }

        if (spumPrefabs != null)
        {
            FindSPUMMatchingElements();
        }
        else
        {
            Debug.LogError("[SPUMEquipmentVisualSystem] SPUM_Prefabs를 찾을 수 없습니다!");
        }
    }

    void Start()
    {
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.OnEquipmentChanged += OnEquipmentChanged;
            UpdateAllEquipmentVisuals();
        }
    }

    void OnDestroy()
    {
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.OnEquipmentChanged -= OnEquipmentChanged;
        }
    }

    private void FindSPUMMatchingElements()
    {
        if (spumPrefabs == null) return;

        Debug.Log("[SPUMEquipmentVisualSystem] ========== SPUM 파츠 검색 시작 ==========");

        SPUM_MatchingList[] matchingLists = spumPrefabs.GetComponentsInChildren<SPUM_MatchingList>(true);

        Debug.Log($"[SPUMEquipmentVisualSystem] 발견된 MatchingList 개수: {matchingLists.Length}");

        foreach (var matchingList in matchingLists)
        {
            Debug.Log($"[SPUMEquipmentVisualSystem] MatchingList: {matchingList.gameObject.name}");

            foreach (var element in matchingList.matchingTables)
            {
                if (element.renderer != null)
                {
                    string key = GetMatchingKey(element);
                    allMatchingElements[key] = element;

                    if (debugMode)
                    {
                        Debug.Log($"[SPUMEquipmentVisualSystem] MatchingElement 발견:");
                        Debug.Log($"  - GameObject: {element.renderer.gameObject.name}");
                        Debug.Log($"  - PartType: {element.PartType}");
                        Debug.Log($"  - PartSubType: {element.PartSubType}");
                        Debug.Log($"  - Dir: {element.Dir}");
                        Debug.Log($"  - Structure: {element.Structure}");
                        Debug.Log($"  - Key: {key}");
                    }
                }
            }
        }

        MapEquipmentParts();

        Debug.Log($"[SPUMEquipmentVisualSystem] ========== 총 {allMatchingElements.Count}개 MatchingElement 매핑 완료 ==========");
    }

    private string GetMatchingKey(MatchingElement element)
    {
        return $"{element.UnitType}_{element.PartType}_{element.PartSubType}_{element.Dir}_{element.Structure}";
    }

    private void MapEquipmentParts()
    {
        Debug.Log("[SPUMEquipmentVisualSystem] ========== 장비 파츠 매핑 시작 ==========");

        foreach (var kvp in allMatchingElements)
        {
            var element = kvp.Value;
            string gameObjectName = element.renderer.gameObject.name.ToLower();
            string partType = element.PartType?.ToLower() ?? "";
            string structure = element.Structure?.ToLower() ?? "";
            string dir = element.Dir?.ToLower() ?? "";

            // GameObject 이름으로 직접 매칭
            if (gameObjectName.Contains("l_weapon"))
            {
                weaponLeftElement = element;
                Debug.Log($"[SPUMEquipmentVisualSystem] ✓ 왼손 무기 매핑 (GameObject 이름): {element.renderer.gameObject.name}");
                continue;
            }
            else if (gameObjectName.Contains("r_weapon"))
            {
                weaponRightElement = element;
                Debug.Log($"[SPUMEquipmentVisualSystem] ✓ 오른손 무기 매핑 (GameObject 이름): {element.renderer.gameObject.name}");
                continue;
            }

            // PartType, Structure, Dir 기반 매칭
            if (partType.Contains("weapon") || structure.Contains("weapon") ||
                structure.Contains("sword") || structure.Contains("gun") || structure.Contains("blade"))
            {
                if (dir.Contains("left") || dir.Contains("l") || dir == "0")
                {
                    if (weaponLeftElement == null)
                    {
                        weaponLeftElement = element;
                        Debug.Log($"[SPUMEquipmentVisualSystem] ✓ 왼손 무기 매핑 (Dir 기반): {element.renderer.gameObject.name}");
                    }
                }
                else if (dir.Contains("right") || dir.Contains("r") || dir == "1")
                {
                    if (weaponRightElement == null)
                    {
                        weaponRightElement = element;
                        Debug.Log($"[SPUMEquipmentVisualSystem] ✓ 오른손 무기 매핑 (Dir 기반): {element.renderer.gameObject.name}");
                    }
                }
            }
            else if (gameObjectName.Contains("helmet") || gameObjectName.Contains("11_helmet"))
            {
                helmetElement = element;
                Debug.Log($"[SPUMEquipmentVisualSystem] ✓ 투구 매핑: {element.renderer.gameObject.name}");
            }
            else if (partType.Contains("helmet") || partType.Contains("head") ||
                     structure.Contains("helmet") || structure.Contains("head") || structure.Contains("hat"))
            {
                if (helmetElement == null)
                {
                    helmetElement = element;
                    Debug.Log($"[SPUMEquipmentVisualSystem] ✓ 투구 매핑 (PartType 기반): {element.renderer.gameObject.name}");
                }
            }
            else if (gameObjectName.Contains("armor") || gameObjectName.Contains("body"))
            {
                armorElement = element;
                Debug.Log($"[SPUMEquipmentVisualSystem] ✓ 갑옷 매핑: {element.renderer.gameObject.name}");
            }
            else if (partType.Contains("armor") || partType.Contains("body") ||
                     structure.Contains("armor") || structure.Contains("body") || structure.Contains("chest"))
            {
                if (armorElement == null)
                {
                    armorElement = element;
                    Debug.Log($"[SPUMEquipmentVisualSystem] ✓ 갑옷 매핑 (PartType 기반): {element.renderer.gameObject.name}");
                }
            }
            else if (gameObjectName.Contains("glove") || gameObjectName.Contains("hand"))
            {
                glovesElement = element;
                Debug.Log($"[SPUMEquipmentVisualSystem] ✓ 장갑 매핑: {element.renderer.gameObject.name}");
            }
            else if (partType.Contains("glove") || partType.Contains("hand") ||
                     structure.Contains("glove") || structure.Contains("hand") || structure.Contains("gauntlet"))
            {
                if (glovesElement == null)
                {
                    glovesElement = element;
                    Debug.Log($"[SPUMEquipmentVisualSystem] ✓ 장갑 매핑 (PartType 기반): {element.renderer.gameObject.name}");
                }
            }
            else if (gameObjectName.Contains("boot") || gameObjectName.Contains("foot"))
            {
                bootsElement = element;
                Debug.Log($"[SPUMEquipmentVisualSystem] ✓ 신발 매핑: {element.renderer.gameObject.name}");
            }
            else if (partType.Contains("boot") || partType.Contains("foot") ||
                     structure.Contains("boot") || structure.Contains("foot") || structure.Contains("shoe"))
            {
                if (bootsElement == null)
                {
                    bootsElement = element;
                    Debug.Log($"[SPUMEquipmentVisualSystem] ✓ 신발 매핑 (PartType 기반): {element.renderer.gameObject.name}");
                }
            }
        }

        Debug.Log("[SPUMEquipmentVisualSystem] ========== 장비 파츠 매핑 완료 ==========");
        DebugPrintMappedParts();
    }

    /// <summary>
    /// ✅ 수정: EquipmentManager.OnEquipmentChanged 이벤트 시그니처와 일치
    /// Action&lt;EquipmentType, EquipmentData, int&gt;
    /// </summary>
    private void OnEquipmentChanged(EquipmentType type, EquipmentData equipment, int enhanceLevel)
    {
        Debug.Log($"[SPUMEquipmentVisualSystem] OnEquipmentChanged: {type} - {equipment?.itemName ?? "NULL"} +{enhanceLevel}");
        UpdateEquipmentVisual(type, equipment);
    }

    public void UpdateEquipmentVisual(EquipmentType type, EquipmentData equipment)
    {
        Debug.Log($"[SPUMEquipmentVisualSystem] UpdateEquipmentVisual 호출: {type}");

        switch (type)
        {
            case EquipmentType.WeaponLeft:
                UpdateWeaponVisual(weaponLeftElement, equipment, "왼손 무기");
                break;
            case EquipmentType.WeaponRight:
                UpdateWeaponVisual(weaponRightElement, equipment, "오른손 무기");
                break;
            case EquipmentType.Helmet:
                UpdateSinglePartVisual(helmetElement, equipment, "투구");
                break;
            case EquipmentType.Armor:
                UpdateSinglePartVisual(armorElement, equipment, "갑옷");
                break;
            case EquipmentType.Gloves:
                UpdateSinglePartVisual(glovesElement, equipment, "장갑");
                break;
            case EquipmentType.Boots:
                UpdateSinglePartVisual(bootsElement, equipment, "신발");
                break;
        }
    }

    private void UpdateWeaponVisual(MatchingElement element, EquipmentData equipment, string handName)
    {
        Debug.Log($"[SPUMEquipmentVisualSystem] UpdateWeaponVisual: {handName}");

        if (element == null || element.renderer == null)
        {
            Debug.LogWarning($"[SPUMEquipmentVisualSystem] {handName} MatchingElement가 없습니다!");
            return;
        }

        Debug.Log($"[SPUMEquipmentVisualSystem] {handName} GameObject: {element.renderer.gameObject.name}");

        if (equipment != null && equipment is EquipmentVisualData visualData && visualData.equipmentSprite != null)
        {
            Debug.Log($"[SPUMEquipmentVisualSystem] {handName} 스프라이트 적용 시작:");
            Debug.Log($"  - 스프라이트: {visualData.equipmentSprite.name}");
            Debug.Log($"  - 색상: {visualData.spriteColor}");
            Debug.Log($"  - 렌더러 GameObject: {element.renderer.gameObject.name}");
            Debug.Log($"  - 렌더러 활성화 상태: {element.renderer.gameObject.activeSelf}");

            element.renderer.sprite = visualData.equipmentSprite;
            element.renderer.color = visualData.spriteColor;
            element.renderer.enabled = true;
            element.renderer.gameObject.SetActive(true);

            element.renderer.transform.localPosition = visualData.localPosition;
            element.renderer.transform.localRotation = Quaternion.Euler(visualData.localRotation);
            element.renderer.transform.localScale = visualData.localScale;

            Debug.Log($"[SPUMEquipmentVisualSystem] ✓ {handName} 비주얼 적용 완료!");
            Debug.Log($"  - Sprite: {element.renderer.sprite?.name}");
            Debug.Log($"  - Enabled: {element.renderer.enabled}");
            Debug.Log($"  - GameObject Active: {element.renderer.gameObject.activeSelf}");
        }
        else
        {
            Debug.Log($"[SPUMEquipmentVisualSystem] {handName} 비주얼 제거");

            element.renderer.sprite = null;
            element.renderer.enabled = false;
        }
    }

    private void UpdateSinglePartVisual(MatchingElement element, EquipmentData equipment, string partName)
    {
        Debug.Log($"[SPUMEquipmentVisualSystem] UpdateSinglePartVisual: {partName}");

        if (element == null || element.renderer == null)
        {
            Debug.LogWarning($"[SPUMEquipmentVisualSystem] {partName} MatchingElement가 없습니다!");
            return;
        }

        if (equipment != null && equipment is EquipmentVisualData visualData && visualData.equipmentSprite != null)
        {
            element.renderer.sprite = visualData.equipmentSprite;
            element.renderer.color = visualData.spriteColor;
            element.renderer.enabled = true;
            element.renderer.gameObject.SetActive(true);

            element.renderer.transform.localPosition = visualData.localPosition;
            element.renderer.transform.localRotation = Quaternion.Euler(visualData.localRotation);
            element.renderer.transform.localScale = visualData.localScale;

            Debug.Log($"[SPUMEquipmentVisualSystem] ✓ {partName} 비주얼 적용: {visualData.equipmentSprite.name}");
        }
        else
        {
            element.renderer.sprite = null;
            element.renderer.enabled = false;

            Debug.Log($"[SPUMEquipmentVisualSystem] {partName} 비주얼 제거");
        }
    }

    public void UpdateAllEquipmentVisuals()
    {
        if (EquipmentManager.Instance == null)
        {
            Debug.LogWarning("[SPUMEquipmentVisualSystem] EquipmentManager.Instance가 null입니다.");
            return;
        }

        Debug.Log("[SPUMEquipmentVisualSystem] UpdateAllEquipmentVisuals 호출");

        foreach (EquipmentType type in System.Enum.GetValues(typeof(EquipmentType)))
        {
            EquipmentData equipment = EquipmentManager.Instance.GetEquippedItem(type);
            UpdateEquipmentVisual(type, equipment);
        }
    }

    [ContextMenu("Debug: Print Mapped Parts")]
    public void DebugPrintMappedParts()
    {
        Debug.Log("========== SPUM 파츠 매핑 현황 ==========");
        Debug.Log($"왼손 무기: {(weaponLeftElement?.renderer?.gameObject.name ?? "NULL")}");
        Debug.Log($"오른손 무기: {(weaponRightElement?.renderer?.gameObject.name ?? "NULL")}");
        Debug.Log($"투구: {(helmetElement?.renderer?.gameObject.name ?? "NULL")}");
        Debug.Log($"갑옷: {(armorElement?.renderer?.gameObject.name ?? "NULL")}");
        Debug.Log($"장갑: {(glovesElement?.renderer?.gameObject.name ?? "NULL")}");
        Debug.Log($"신발: {(bootsElement?.renderer?.gameObject.name ?? "NULL")}");
        Debug.Log($"총 MatchingElement 수: {allMatchingElements.Count}");
        Debug.Log("=========================================");
    }

    [ContextMenu("Debug: Print All Matching Elements")]
    public void DebugPrintAllMatchingElements()
    {
        Debug.Log("========== 모든 MatchingElement ==========");
        foreach (var kvp in allMatchingElements)
        {
            var element = kvp.Value;
            Debug.Log($"GameObject: {element.renderer?.gameObject.name}");
            Debug.Log($"  - PartType: {element.PartType}");
            Debug.Log($"  - SubType: {element.PartSubType}");
            Debug.Log($"  - Dir: {element.Dir}");
            Debug.Log($"  - Structure: {element.Structure}");
            Debug.Log("---");
        }
        Debug.Log("=========================================");
    }

    [ContextMenu("Debug: Re-find SPUM Parts")]
    public void DebugRefindParts()
    {
        allMatchingElements.Clear();
        weaponLeftElement = null;
        weaponRightElement = null;
        helmetElement = null;
        armorElement = null;
        glovesElement = null;
        bootsElement = null;

        FindSPUMMatchingElements();
        Debug.Log("[SPUMEquipmentVisualSystem] 파츠 재검색 완료");
    }

    [ContextMenu("Debug: Test Weapon Left")]
    public void DebugTestWeaponLeft()
    {
        if (weaponLeftElement != null && weaponLeftElement.renderer != null)
        {
            Debug.Log($"[TEST] 왼손 무기 테스트: {weaponLeftElement.renderer.gameObject.name}");
            Debug.Log($"  - 현재 Sprite: {weaponLeftElement.renderer.sprite?.name ?? "NULL"}");
            Debug.Log($"  - Enabled: {weaponLeftElement.renderer.enabled}");
            Debug.Log($"  - GameObject Active: {weaponLeftElement.renderer.gameObject.activeSelf}");

            weaponLeftElement.renderer.color = Color.red;
            weaponLeftElement.renderer.enabled = true;
            weaponLeftElement.renderer.gameObject.SetActive(true);

            Debug.Log("[TEST] 빨간색으로 변경 완료. Scene 뷰에서 확인하세요!");
        }
        else
        {
            Debug.LogError("[TEST] 왼손 무기 element가 NULL입니다!");
        }
    }

    [ContextMenu("Debug: Test Weapon Right")]
    public void DebugTestWeaponRight()
    {
        if (weaponRightElement != null && weaponRightElement.renderer != null)
        {
            Debug.Log($"[TEST] 오른손 무기 테스트: {weaponRightElement.renderer.gameObject.name}");
            Debug.Log($"  - 현재 Sprite: {weaponRightElement.renderer.sprite?.name ?? "NULL"}");
            Debug.Log($"  - Enabled: {weaponRightElement.renderer.enabled}");
            Debug.Log($"  - GameObject Active: {weaponRightElement.renderer.gameObject.activeSelf}");

            weaponRightElement.renderer.color = Color.blue;
            weaponRightElement.renderer.enabled = true;
            weaponRightElement.renderer.gameObject.SetActive(true);

            Debug.Log("[TEST] 파란색으로 변경 완료. Scene 뷰에서 확인하세요!");
        }
        else
        {
            Debug.LogError("[TEST] 오른손 무기 element가 NULL입니다!");
        }
    }
}