---
last_updated: 2026-04-27
audience: ai-first
---

# PrefabTable 등록 절차

> 상태: accepted_candidate. A303-232 산출물. DevMode AssetPack 어댑터 critical path 차단 해제용.

이 문서는 AD(우수연)가 신규 프리팹을 DevMode AssetPack 슬롯에 등록할 때 필요한 Photon Fusion PrefabTable 사전 등록 절차를 안내한다.

## 대상 독자

| 역할 | 이름 | 책임 |
|---|---|---|
| AD (아트 디렉터) | 우수연 | 신규 프리팹 제작 + Inspector 슬롯 등록 |
| DevMode 모듈 R | 박병준 | PrefabTable 등록 PR 생성 + Validator 운영 |
| NetworkProjectConfig 게이트키퍼 | 유민재 | PrefabTable PR 머지 최종 승인 |

## 대상 슬롯

PrefabTable 등록이 **필수**인 슬롯은 `NetworkPrefabRef` 타입이다.

| 슬롯명 | 타입 | PrefabTable 필수 여부 |
|---|---|---|
| `playerPrefab` | `NetworkPrefabRef` | **필수** |
| `bulletPrefab` | `NetworkPrefabRef` | **필수** |
| `materials` | `List<Material>` | 해당 없음 |
| `uiSprites` | `List<Sprite>` | 해당 없음 |
| `audioClips` | `List<AudioClip>` | 해당 없음 |

`materials` / `uiSprites` / `audioClips` 슬롯은 일반 Unity 레퍼런스이므로 PrefabTable 등록 없이 Inspector에서 직접 할당 가능하다. 별도 명명규칙(`devmode-{cat}-{n}`)은 어댑터 매칭 문서를 참조한다.

## 등록 절차

### Step 1 — AD: 신규 프리팹 제작

1. 프리팹에 `NetworkObject` 컴포넌트를 부착한다.
2. Fusion이 관리하는 Networked 컴포넌트(예: PlayerController, Gun 등)가 있으면 함께 부착한다.
3. 프리팹을 `Assets/_Project/Prefabs/` 하위에 저장한다.
4. 박병준에게 슬랙 또는 Jira 코멘트로 등록 요청을 보낸다.
   - 요청 항목: 프리팹 경로, 용도(playerPrefab / bulletPrefab), 브랜치명.

### Step 2 — 박병준: PrefabTable 등록 PR 생성

1. `unity/ProjectSettings/PhotonAppSettings.asset` 또는 `NetworkProjectConfig`를 직접 수정하지 않는다.
2. Unity Editor에서 **Window → Fusion → Fusion Hub → PrefabTable** 탭을 열거나, `NetworkProjectConfig` 인스펙터에서 프리팹을 PrefabTable에 추가한다.
3. 변경된 `NetworkProjectConfig.asset`을 포함한 PR을 생성한다.
4. PR 리뷰어로 유민재를 지정한다.

> **주의**: `NetworkProjectConfig.asset` 파일은 바이너리/YAML 혼합이므로 직접 텍스트 편집 금지. 반드시 Unity Editor UI를 통해 등록한다.

### Step 3 — 유민재: PR 리뷰 및 머지

1. PrefabTable에 추가된 프리팹이 올바른 `NetworkObject`를 가졌는지 확인한다.
2. App ID / secret 노출 여부를 확인한다 ([50/04 Fusion 가드레일](../50_기술_구조_기준/04_Fusion_구현_가드레일.md) §금지 참조).
3. 이상 없으면 머지한다.

### Step 4 — AD: Inspector 슬롯 등록

1. 머지된 브랜치를 로컬에 pull한다.
2. Unity Editor에서 `DevModeAssetPack` ScriptableObject 인스턴스를 선택한다.
   - 기본 경로: `Assets/_Project/Data/DevMode/`
3. Inspector에서 `playerPrefab` 또는 `bulletPrefab` 슬롯에 해당 프리팹을 드래그한다.
   - `NetworkPrefabRef` 슬롯에 프리팹을 드래그하면 Unity가 자동으로 GUID를 할당한다.
4. 저장 후 PrefabTableValidator가 자동으로 검증한다 (Step 5 참조).

### Step 5 — PrefabTableValidator 자동 검증

`Assets/_Project/Editor/DevMode/PrefabTableValidator.cs`가 다음 시점에 자동 실행된다:

- DevModeAssetPack ScriptableObject의 `OnValidate()` 콜백 시 (Inspector 값 변경 직후)
- 수동 실행: Unity 메뉴 **Colosseum → DevMode → Validate PrefabTable**

검증 통과 시 콘솔에 출력 없음 (조용히 통과).

검증 실패 시 콘솔에 에러 출력:
```
[Colosseum] PrefabTable Validator: {assetName}.playerPrefab - NetworkPrefabRef가 유효하지 않습니다. Fusion PrefabTable에 프리팹이 등록되어 있는지 확인하세요. (docs/72_DevMode_가이드/01_PrefabTable_등록_절차.md Step 2 참조)
```

## 트러블슈팅

### 증상: DevMode 진입 후 플레이어 / 탄환이 스폰되지 않는다

**원인**: `NetworkPrefabRef` 슬롯에 등록된 프리팹이 Fusion PrefabTable에 없는 경우 `runner.Spawn()`이 조용히 실패한다.

**진단 순서**:
1. Unity 콘솔에서 `[Colosseum] PrefabTable Validator` 에러를 확인한다.
2. 에러가 있으면 위 절차 Step 2~3을 다시 수행한다.
3. 에러가 없는데 스폰되지 않으면 NetworkManager 콘솔 로그(`[DevModeController] PlayerCount 폴링 타임아웃`)를 확인한다.

### 증상: Validator 에러 메시지 해석

| 에러 메시지 | 의미 | 조치 |
|---|---|---|
| `NetworkPrefabRef가 유효하지 않습니다` | 슬롯이 비어 있거나 PrefabTable 미등록 | Step 1~4 재수행 |
| `playerPrefab` 슬롯에 대한 에러 | 플레이어 프리팹 미등록 | Step 2에서 playerPrefab 경로 확인 |
| `bulletPrefab` 슬롯에 대한 에러 | 탄환 프리팹 미등록 | Step 2에서 bulletPrefab 경로 확인 |

## 한계 및 범위

- 이 절차는 `NetworkPrefabRef` 슬롯(`playerPrefab`, `bulletPrefab`)에만 적용된다.
- `materials` / `uiSprites` / `audioClips` 슬롯은 PrefabTable 무관. 직접 할당 가능.
- `cardDataList` 슬롯은 ScriptableObject 레퍼런스. PrefabTable 무관.
- PrefabTableValidator는 `IsValid` 사전 체크만 수행한다. 5슬롯 모두 채움 여부는 강제하지 않는다 (어댑터 미사용 AssetPack도 유효).

## 참조

- [50/04 Fusion 구현 가드레일](../50_기술_구조_기준/04_Fusion_구현_가드레일.md)
- [50/05 파일경로별 DRI](../50_기술_구조_기준/05_파일경로별_DRI.md)
- DevMode AssetPack 어댑터 검증 시나리오: `docs/70_검증_체크리스트/06_DevMode_AssetPack_어댑터.md` (A303-235 산출물)
