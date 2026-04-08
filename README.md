# INFINITY-LEVELUP

2D 프로젝트 ! 방치형 2D 하이퍼 캐쥬얼 게임

---

## 데이터 아키텍처 규칙

### 단일 source of truth 원칙
재화·상태 데이터는 **`GameDataBridge.CurrentData`** 한 곳만 소유합니다.
매니저(FarmManager, ResourceBarManager 등)에 같은 값을 복사한 인스턴스 필드를
두지 마세요. 4군데 sync 시도가 race를 만들고 데이터 손실을 일으킵니다.

새 재화를 추가할 때는 다음 패턴을 따르세요:

```csharp
// Assets/Script/Code/Data/XxxService.cs
public static class XxxService
{
    public static long Value => GameDataBridge.CurrentData?.xxx ?? 0;
    public static event Action<long> OnChanged;
    public static void Add(long amount) { /* bridge += + 이벤트 발화 */ }
    public static bool Spend(long amount) { /* bridge -= + 이벤트 발화 */ }
    public static void Set(long value) { /* bridge = + 이벤트 발화 */ }
}
```

UI는 `OnChanged` 이벤트를 구독해서만 갱신하고, 자체 캐시 필드를 두지 마세요.

### 현재 단일 소스로 관리되는 데이터
- **`cropPoints`** (작물포인트): `CropPointService` →
  `GameDataBridge.CurrentData.cropPoints`
  - `Assets/Script/Code/Data/CropPointService.cs`
  - 호환성: `FarmManager.cropPoints`, `ResourceBarManager.cropPoints`,
    `FarmManager.AddCropPoints/SpendCropPoints/GetCropPoints`,
    `ResourceBarManager.AddCropPoints/SetCropPoints`은 모두 wrapper로 유지되어
    내부적으로 서비스에 위임. 기존 호출 사이트는 변경 불필요.
  - `FarmManager.OnCropPointsChanged`,
    `FarmManagerExtension.OnCropPointsChanged`도 `CropPointService.OnChanged`
    프록시로 동작.

---

## 데이터 손실 방지 가드 (IsLoaded 패턴)

매니저가 초기화 전에 SaveGame이 호출되면 빈 메모리로 JSON을 덮어쓸 수 있습니다.
이를 막기 위해 데이터 매니저는 **로드 완료 플래그**를 가지고, 그 플래그가 false면
`SaveLoadManager.CollectSaveData`가 `GameDataBridge` 폴백을 사용합니다.

- `InventoryManager.IsInventoryLoaded`
- `EquipmentManager.IsEquipmentLoaded`
- `CompanionInventoryManager.IsCompanionLoaded`

각 매니저는 `Start()`에서 `GameDataBridge.CurrentData`로 자체 선로드해서
`SaveLoadManager.AutoLoadOnStart`의 2프레임 race window를 닫습니다.

---

## 씬 전환 규칙

- `SceneTransitionManager`만 `LoadFarmScene`/`LoadMainScene` 등을 호출하세요.
- 더블클릭/이중 호출은 `_isTransitioning` 가드가 차단합니다 (cropPoints/인벤
  유실 방지).
- `MainEnterButton`처럼 Inspector OnClick + 코드 AddListener 둘 다 등록하지
  마세요 — 이중 호출됩니다. 둘 중 하나만 사용.

---

## 최근 주요 변경

| 커밋 | 내용 |
|---|---|
| `50bb73bb` | **cropPoints 단일 source of truth refactor** — `CropPointService` 도입, sync/watch/JSON 폴백/4단 분기 모두 삭제 (-204줄) |
| `d35e3fb0` | FarmScene quit 시 저장 차단 버그 fix (`playerLevel > 0` 가드 완화) |
| `a75650ac` | 재진입 race window 제거 — 매니저들이 `GameDataBridge`에서 자체 선로드 |
| `4f2b4101` | 인벤토리 Main→Farm→Main 재진입 빈 UI 버그 fix |
| `32ed2703` | 데이터 손실 방지 가드 (IsLoaded 플래그, pendingUnknownItems, 더블클릭 가드) |
