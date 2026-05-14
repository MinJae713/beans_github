---
last_updated: 2026-04-28
audience: art-director
---

# AssetPack 운영 매뉴얼

> A303-235 산출물. AD(우수연)가 코더 개입 없이 Inspector만으로 DevMode 에셋을 교체하기 위한 1장짜리 운영 참고서.

---

## 1. 대상 독자 / 목적

| 역할 | 이름 | 이 문서를 볼 때 |
|---|---|---|
| AD (아트 디렉터) | 우수연 | 새 Material·UI·Audio·카드·프리팹을 DevMode에 반영하고 싶을 때 |
| DevMode 모듈 R | 박병준 | NetworkPrefab 슬롯 등록 요청을 받았을 때 / 어댑터 동작 확인 시 |
| 그 외 팀원 | — | DevMode 자산 구조를 파악하고 싶을 때 (읽기 전용) |

**목적**: AD가 `DevModeAssetPack` ScriptableObject 슬롯에 자산을 드래그해 넣으면, 어댑터 5종이 DevMode 진입 시 자동으로 게임에 반영한다. 이 문서는 그 동선을 설명한다.

---

## 2. AssetPack 슬롯 6개

`Assets/_Project/Data/DevMode/` 아래 `DevModeAssetPack` ScriptableObject 인스턴스를 Inspector에서 열면 아래 6개 슬롯이 보인다.

| 슬롯명 | 타입 | 용도 | 명명 규칙 (씬 오브젝트) | 예시 자산명 |
|---|---|---|---|---|
| `playerPrefab` | `NetworkPrefabRef` | DevMode 스폰 플레이어 프리팹 | 해당 없음 (씬 오브젝트 불필요) | `PlayerDevMode` |
| `bulletPrefab` | `NetworkPrefabRef` | DevMode 기본 탄환 프리팹 | 해당 없음 | `BulletDevMode` |
| `cardDataList` | `List<CardData>` | DevMode 덱에 사용할 카드 목록 | 해당 없음 | `CardData_FireBolt` |
| `uiSprites` | `List<Sprite>` | HUD·UI 오버레이 스프라이트 | `devmode-ui-{index}` | `devmode-ui-0`, `devmode-ui-1` |
| `materials` | `List<Material>` | DevMode 전용 override 머터리얼 | `devmode-material-{index}` | `devmode-material-0` |
| `audioClips` | `List<AudioClip>` | DevMode 전용 사운드 | `devmode-audio-{index}` | `devmode-audio-0` |

> **playerPrefab / bulletPrefab**: `NetworkPrefabRef` 타입이므로 Fusion PrefabTable 사전 등록이 필수다. 직접 드래그 전에 박병준에게 등록 요청을 먼저 보낼 것 (→ 섹션 4 Step 2 참조).

> **uiSprites / materials / audioClips**: 일반 Unity 레퍼런스이므로 PrefabTable 등록 불필요. Inspector에서 직접 드래그 가능.

AssetPack 인스턴스가 없으면 Project 창에서 우클릭 → Create → Colosseum → DevMode → Asset Pack 으로 신규 생성한다.

---

## 3. 명명 규칙

`uiSprites`, `materials`, `audioClips` 슬롯은 **씬에 배치된 GameObject 이름**과 **슬롯 index**로 매칭된다. 어댑터가 GameObject 이름을 파싱해 해당 index의 자산을 적용한다.

### 규칙: `devmode-{category}-{index}`

| 어댑터 | prefix | 예시 GameObject 이름 | 매칭되는 슬롯 |
|---|---|---|---|
| `MaterialOverrideAdapter` | `devmode-material-` | `devmode-material-0` | `materials[0]` |
| `UIBindingAdapter` | `devmode-ui-` | `devmode-ui-1` | `uiSprites[1]` |
| `AudioBindingAdapter` | `devmode-audio-` | `devmode-audio-2` | `audioClips[2]` |

**파싱 로직 요약**: prefix로 시작 + 뒤에 0 이상 정수 → 매칭 성공. 그 외는 모두 실패.

```
devmode-material-0   → materials[0]  (O)
devmode-ui-1         → uiSprites[1]  (O)
devmode-audio-0      → audioClips[0] (O)

devmode-material-abc → 실패 (숫자 아님)
devmode-material-    → 실패 (index 없음)
material-0           → 실패 (prefix 불일치)
```

**규칙 위반 시 어댑터 동작**:
- 매칭 실패 → 해당 GameObject에 자산 적용 **skip** (크래시 없음)
- Unity 콘솔에 `[Colosseum] Dev Mode: XXXAdapter — 명명 규칙 불일치` 경고 출력
- `MaterialOverrideAdapter`는 컴포넌트가 활성화되거나 인스펙터를 다시 그릴 때 `OnValidate`가 명명 규칙 오타를 콘솔 경고로 출력한다. 즉시 표시되지 않으면 다른 오브젝트를 선택했다 다시 선택해 인스펙터를 갱신하거나 Play 모드 진입 직후 콘솔을 확인할 것. (UIBinding/AudioBinding 어댑터에는 OnValidate 없음 — Material만 해당)

**index는 0부터 시작**한다. 슬롯 리스트도 0번이 첫 번째 항목이다.

---

## 4. 어댑터 GameObject 사전 배치

DevMode 씬에는 5종 어댑터(`PlayerSpawnAdapter`, `BulletInjectionAdapter`, `MaterialOverrideAdapter`, `UIBindingAdapter`, `AudioBindingAdapter`)가 사전 배치되어 있다. AD는 어댑터 자체를 새로 만들 필요 없이 자산 슬롯과 GameObject 이름만 관리하면 된다.

### 어댑터별 필수 컴포넌트 (RequireComponent)

| 어댑터 | 필수 컴포넌트 | 부착 위치 |
|---|---|---|
| `MaterialOverrideAdapter` | `Renderer` | 머터리얼을 교체할 씬 오브젝트 |
| `UIBindingAdapter` | `Image` (UnityEngine.UI) | 스프라이트를 교체할 UI 오브젝트 |
| `AudioBindingAdapter` | `AudioSource` | 오디오 클립을 교체할 오브젝트 |
| `PlayerSpawnAdapter` | 없음 | DevMode 전용 관리 오브젝트 |
| `BulletInjectionAdapter` | 없음 | DevMode 전용 관리 오브젝트 |

> **컴포넌트 미부착 시**: 어댑터가 동작하지 않는다 (자산 교체 skip). Unity가 RequireComponent 경고를 출력하며, 해당 어댑터 GameObject에 Renderer·Image·AudioSource 중 해당 컴포넌트가 없는 경우 박병준에게 추가 요청한다.

### BulletInjectionAdapter Asset Pack 슬롯 wiring (필수)

`BulletInjectionAdapter` GameObject의 Inspector에는 `Asset Pack` 슬롯이 있다. 이 슬롯에 본 `DevModeAssetPack` ScriptableObject를 **반드시 직접 드래그**해야 한다.

- 이유: race mitigation — `Gun.Spawned()`가 `OnDevModeReady`보다 먼저 실행될 수 있어, `OnEnable` 시점에 Inspector 슬롯에서 즉시 cache를 설정한다.
- 누락 시: 첫 발사 1회에 한해 탄환 override가 적용되지 않을 수 있다.

---

## 5. 새 자산 추가 워크플로

### 경우 A — Material · UI Sprite · Audio Clip (PrefabTable 불필요)

1. **자산 생성**: Material / Sprite / AudioClip을 `Assets/_Project/` 적절한 경로에 저장한다.
2. **명명 규칙 적용**: 씬에서 적용 대상 GameObject를 선택하고 이름을 `devmode-{category}-{index}` 형식으로 변경한다. (예: `devmode-material-0`)
3. **어댑터 컴포넌트 확인**: 해당 GameObject에 어댑터 컴포넌트(MaterialOverrideAdapter / UIBindingAdapter / AudioBindingAdapter)와 필수 컴포넌트(Renderer / Image / AudioSource)가 부착되어 있는지 Inspector에서 확인한다. 없으면 박병준에게 추가 요청할 것 — `RequireComponent`가 해당 컴포넌트를 강제하므로 미부착 시 어댑터가 동작하지 않는다.
4. **AssetPack 슬롯에 드래그**: Project 창에서 `Assets/_Project/Data/DevMode/` 아래 `DevModeAssetPack` ScriptableObject 인스턴스를 선택 → Inspector에서 해당 슬롯 리스트에 자산을 드래그한다. (index 순서가 GameObject index와 일치해야 함)
5. **DevMode 진입 + 콘솔 검증**: Play 모드에서 DevMode를 진입한다. Unity 콘솔에서 아래 로그를 확인한다:
   - Material: `[Colosseum] Dev Mode: Material applied to devmode-material-0`
   - UI: `[Colosseum] Dev Mode: Sprite applied to devmode-ui-0`
   - Audio: `[Colosseum] Dev Mode: Clip applied to devmode-audio-0`
6. **이상 없음 확인 후 저장**: 콘솔 경고 없으면 완료. 씬을 저장한다.

### 경우 B — Player Prefab · Bullet Prefab (PrefabTable 등록 필요)

1. **프리팹 제작**: `NetworkObject` 컴포넌트를 부착한 프리팹을 `Assets/_Project/Prefabs/` 하위에 저장한다.
2. **박병준에게 등록 요청**: 슬랙 또는 Jira 코멘트로 요청 (프리팹 경로 + 용도 + 브랜치명 포함). 자세한 절차는 [01_PrefabTable_등록_절차.md](01_PrefabTable_등록_절차.md) 참조.
3. **PR 머지 대기**: 박병준 → 유민재 머지 완료 후 로컬에 pull.
4. **AssetPack 슬롯에 드래그**: Inspector에서 `playerPrefab` 또는 `bulletPrefab` 슬롯에 프리팹을 드래그. Unity가 자동으로 `NetworkPrefabRef` GUID를 할당한다. 단, 사전에 PrefabTable에 등록되지 않은 프리팹을 드래그하면 `PrefabTableValidator`가 즉시 콘솔 에러를 출력한다. Step 2(박병준에게 PrefabTable 등록 요청)가 선행되지 않은 채 드래그하지 말 것.
5. **BulletInjectionAdapter Asset Pack 슬롯 wiring**: `BulletInjectionAdapter` GameObject Inspector의 `Asset Pack` 슬롯에 본 AssetPack을 드래그한다 (race mitigation 필수 — 누락 시 첫 발사 1회 override 누락 가능).
6. **검증**: AssetPack Inspector에서 슬롯을 변경하면 곧바로 검증되며 콘솔에 에러 없으면 통과 (CustomEditor가 EndChangeCheck 경유로 검증 호출). DevMode 진입 후 콘솔에서 다음 로그를 확인한다:
   - `[Colosseum] Dev Mode: PlayerPrefab override applied` (playerPrefab을 갈아끼웠을 때)
   - `[Colosseum] Dev Mode: Bullet override cached - <prefabId>` (bulletPrefab을 갈아끼웠을 때)
   - 미출력 시: AssetPack 슬롯 채움 여부 / 어댑터 컴포넌트 부착 여부 / BulletInjectionAdapter의 `Asset Pack` 슬롯 wiring 순서로 확인한다.

### 경우 C — Card (cardDataList)

1. `CardData` ScriptableObject를 생성한다.
2. `DevModeAssetPack` Inspector에서 `cardDataList` 슬롯에 드래그해 추가한다.
3. 명명 규칙이나 PrefabTable 등록 불필요.

---

## 6. 자주 하는 실수 + 트러블슈팅

| 증상 | 원인 | 해결 |
|---|---|---|
| DevMode 진입 후 머터리얼/UI/오디오가 바뀌지 않는다 | `materials` / `uiSprites` / `audioClips` 슬롯이 비어 있다 | AssetPack Inspector에서 해당 슬롯 리스트에 자산 드래그 추가 |
| 콘솔에 `명명 규칙 불일치` 경고 | GameObject 이름이 `devmode-{category}-{index}` 형식이 아니다 | Hierarchy에서 오브젝트 이름 확인·수정. `devmode-material-abc` 같은 오타 주의 |
| `playerPrefab` 또는 `bulletPrefab` 슬롯 드래그 후 콘솔 에러: `NetworkPrefabRef가 유효하지 않습니다` | Fusion PrefabTable에 해당 프리팹이 미등록 | 박병준에게 PrefabTable 등록 요청 → [01_PrefabTable_등록_절차.md](01_PrefabTable_등록_절차.md) Step 1~4 수행 |
| 슬롯 index가 틀려서 엉뚱한 자산이 적용된다 | GameObject 이름의 index와 슬롯 리스트 순서가 불일치 | `devmode-material-0` → `materials[0]`, `devmode-material-1` → `materials[1]` 순서를 맞춤 |
| DevMode 진입 후 어댑터 로그 자체가 없다 | `#if UNITY_EDITOR` 가드로 인해 에디터 외 환경에서 어댑터 클래스가 컴파일 제외 | Unity Editor에서 Play 모드로 진입했는지 확인. 빌드된 .exe로 실행 중이라면 어댑터는 의도적으로 동작하지 않으며 missing script 경고가 정상이다 |
| DevMode 종료 후 다른 씬 오브젝트 머터리얼이 바뀌어 있다 | 매우 드문 케이스 | 박병준에게 보고. 어댑터는 이미 `new Material(...)` 인스턴스화 + OnDisable 원복 + DestroyImmediate를 보장한다 |

---

## 7. 관련 문서

| 문서 | 경로 | 참조 시점 |
|---|---|---|
| PrefabTable 등록 절차 | [01_PrefabTable_등록_절차.md](01_PrefabTable_등록_절차.md) | `playerPrefab` / `bulletPrefab` 추가 시 |
| DevMode AssetPack 어댑터 검증 시나리오 | [../70_검증_체크리스트/06_DevMode_AssetPack_어댑터.md](../70_검증_체크리스트/06_DevMode_AssetPack_어댑터.md) | 적용 결과 검증 기준 확인 시 |
