---
last_updated: 2026-04-27
audience: ai-first
---

# DevMode AssetPack 어댑터 검증 시나리오

이 문서는 DevMode AssetPack 어댑터 5종의 검증 기준을 정의한다.
각 시나리오 공통 acceptance: "AD가 Inspector 슬롯 교체 → DevMode 진입 → 5초 내 결과 자동 확인"

자동화 실행은 Unity MCP `script-execute` 를 통해 콘솔 정규식 + gameobject-find + screenshot 픽셀 비교 훅으로 수행한다.
실제 자동화 스크립트는 향후 별도 task에서 관리한다. 현재는 Manual smoke 체크리스트 + 시나리오별 자동 검증 훅(콘솔 정규식 + component-get + screenshot)을 수동 실행한다.

raw 결과물(로그, 스크린샷)은 레포에 직접 넣지 않는다. 공유가 필요한 경우만 `docs/` 에 요약을 남긴다.

---

## 시나리오 1 — PlayerSpawn (A303-233 산출물 의존)

| 항목 | 내용 |
|---|---|
| 의존 | A303-233 (PlayerSpawnAdapter + NetworkManager.SetDevModePlayerPrefab hook) |
| 전제 | AssetPack.playerPrefab 슬롯에 PrefabTable 등록된 NetworkPrefabRef 할당 |

### 검증 절차

1. DevMode 씬 진입
2. PlayerSpawnAdapter가 OnDevModeReady를 수신했는지 콘솔 확인
3. NetworkManager가 DevMode 플레이어 프리팹으로 spawn했는지 확인

### 자동 검증 훅

- 콘솔 정규식: `\[Colosseum\] Dev Mode: Player .* spawned`
- gameobject-find: NetworkObject 이름이 DevMode 프리팹 명칭과 일치하는지 확인
- screenshot: P1 spawn 위치 픽셀 색상 검증 (기준 색상은 DevMode 프리팹 외형으로 결정)

### Acceptance 기준

- 5초 이내 콘솔 정규식 PASS
- spawn된 NetworkObject 이름이 AssetPack.playerPrefab과 일치
- sharedMaterial 변형 없음 (다른 씬 오염 0)

---

## 시나리오 2 — BulletInjection (A303-234 산출물 의존)

| 항목 | 내용 |
|---|---|
| 의존 | A303-234 (BulletInjectionAdapter + Gun.DevMode.cs partial hook) |
| 전제 | AssetPack.bulletPrefab 슬롯에 PrefabTable 등록된 NetworkPrefabRef 할당 |

### 검증 절차

1. DevMode 씬 진입
2. P1이 발사
3. 발사된 탄환이 DevMode 프리팹인지 확인

### 자동 검증 훅

- 콘솔 정규식: `\[Colosseum\] Dev Mode: Bullet .* spawned by .*`
- gameobject-find: 신규 bullet GameObject 외형 확인
- component-get: HasStateAuthority 검증 (StateAuthority 기준 spawn)

### Acceptance 기준

- 발사 후 1초 이내 콘솔 정규식 PASS
- Resimulation 중 중복 spawn 0회
- DevMode 종료 후 원본 bulletPrefab으로 복원

---

## 시나리오 3 — MaterialOverride (A303-235 산출물 1)

| 항목 | 내용 |
|---|---|
| 의존 | MaterialOverrideAdapter.cs (본 task) |
| 전제 | GameObject 이름 `devmode-material-0`, Renderer 컴포넌트, AssetPack.materials[0] 할당 |

### 검증 절차

1. DevMode 씬 진입
2. MaterialOverrideAdapter가 OnDevModeReady를 수신했는지 콘솔 확인
3. `devmode-material-0` Renderer.material.name에 `(Instance)` 접미사 확인 (인스턴스화 검증)
4. 다른 씬 동일 material을 참조하는 객체에 시각 변형 없음 확인 (R10 mitigation)

### 자동 검증 훅

- 콘솔 정규식: `\[Colosseum\] Dev Mode: Material applied to devmode-material-0`
- component-get: `Renderer.material.name`이 `(Instance)` 접미사를 포함하는지 확인
- screenshot: 픽셀 색상이 AssetPack.materials[0] 기준 색상과 일치하는지 확인

### Acceptance 기준

- 5초 이내 콘솔 정규식 PASS
- `Renderer.material.name`에 `(Instance)` 포함 (sharedMaterial 직접 변경 아님 증명)
- DevMode 종료(OnDisable) 후 `Renderer.sharedMaterial`이 원본으로 복원
- 동일 material을 참조하는 다른 오브젝트 변형 0 (cross-scene leak 없음)

---

## 시나리오 4 — UIBinding (A303-235 산출물 2)

| 항목 | 내용 |
|---|---|
| 의존 | UIBindingAdapter.cs (본 task) |
| 전제 | GameObject 이름 `devmode-ui-0`, Image 컴포넌트, AssetPack.uiSprites[0] 할당 |

### 검증 절차

1. DevMode 씬 진입
2. UIBindingAdapter가 OnDevModeReady를 수신했는지 콘솔 확인
3. `devmode-ui-0` Image.sprite.name이 AssetPack.uiSprites[0].name과 일치하는지 확인
4. preserveAspect 등 기존 Image 설정이 변경되지 않았는지 확인

### 자동 검증 훅

- 콘솔 정규식: `\[Colosseum\] Dev Mode: Sprite applied to devmode-ui-0`
- component-get: `Image.sprite.name`이 AssetPack.uiSprites[0].name과 일치 + `preserveAspect` 보존 확인
- screenshot: UI 영역 픽셀 색상이 적용된 스프라이트 기준과 일치하는지 확인

### Acceptance 기준

- 5초 이내 콘솔 정규식 PASS
- `Image.sprite.name` 일치
- `preserveAspect`, `color`, `raycastTarget` 등 기존 설정 변경 없음

---

## 시나리오 5 — AudioBinding (A303-235 산출물 3)

| 항목 | 내용 |
|---|---|
| 의존 | AudioBindingAdapter.cs (본 task) |
| 전제 | GameObject 이름 `devmode-audio-0`, AudioSource 컴포넌트, AssetPack.audioClips[0] 할당 |

### 검증 절차

1. DevMode 씬 진입
2. AudioBindingAdapter가 Start에서 OnDevModeReady를 구독하고 IsReady late-join 분기를 통해 Apply가 호출됐는지 콘솔 확인
3. `devmode-audio-0` AudioSource.clip.name이 AssetPack.audioClips[0].name과 일치하는지 확인
4. spatialBlend, volume 등 기존 AudioSource 설정이 변경되지 않았는지 확인

### 자동 검증 훅

- 콘솔 정규식: `\[Colosseum\] Dev Mode: Clip applied to devmode-audio-0`
- component-get: `AudioSource.clip.name`이 AssetPack.audioClips[0].name과 일치 + `spatialBlend` 보존 확인

### Acceptance 기준

- 5초 이내 콘솔 정규식 PASS
- `AudioSource.clip.name` 일치
- `spatialBlend`, `volume`, `pitch` 등 기존 설정 변경 없음

---

## 시나리오 6 — Scene Reload 복원력 (R9)

| 항목 | 내용 |
|---|---|
| 의존 | 어댑터 5종 모두 활성 상태 |
| 전제 | DevMode 씬에 어댑터 5종 GameObject 배치 완료 |

### 검증 절차

1. DevMode 씬 진입 → 어댑터 5종 모두 `applied` 로그 출력 확인
2. NetworkScene reload 강제 (`NetworkSceneManager.LoadScene()` 재호출 또는 Host 재연결)
3. 씬 배치 어댑터 GameObject 파괴 → 재생성 확인
4. 재생성된 어댑터가 IsReady late-join 분기를 통해 재구독했는지 확인

### 자동 검증 훅

- 콘솔 정규식 5종 모두:
  - `\[Colosseum\] Dev Mode: Material applied to devmode-material-.*`
  - `\[Colosseum\] Dev Mode: Sprite applied to devmode-ui-.*`
  - `\[Colosseum\] Dev Mode: Clip applied to devmode-audio-.*`
  - (PlayerSpawn, BulletInjection 시나리오 로그 포함, 233/234 의존)
- gameobject-find: reload 후 어댑터 GameObject가 재생성됐는지 확인

### Acceptance 기준

- reload 후 5초 이내 어댑터 재구독 + IsReady late-join 분기 발동
- 재구독 후 5종 슬롯 전부 재적용 확인
- NullReferenceException 0회
- static event 누수(stale subscriber) 0회

---

## Manual Smoke 체크리스트

| ID | 확인 항목 |
|---|---|
| DEV-01 | DevMode 진입 → 어댑터 3종(Material/UI/Audio) 모두 `applied` 로그 출력 |
| DEV-02 | DevMode 진입/종료 50회 — NullReferenceException 0회 |
| DEV-03 | DevMode 종료 → MaterialOverrideAdapter OnDisable → sharedMaterial 원복 확인 |
| DEV-04 | Inspector에서 `devmode-material-abc` 명명 시 OnValidate 경고 출력 |
| DEV-05 | AssetPack.materials 슬롯 비어있을 때 적절한 경고 로그 + 크래시 없음 |
| DEV-06 | NetworkScene reload 후 어댑터 5종 재구독 + 슬롯 재적용 |

## BLOCKED 기준

아래 항목은 단순 실패가 아니라 구현 방향 재검토가 필요한 BLOCKED로 본다.

- `Renderer.sharedMaterial`을 직접 변경하여 cross-scene material leak이 발생한다.
- OnDisable에서 `OnDevModeReady -= Apply`가 누락되어 stale subscriber가 쌓인다.
- NetworkScene reload 후 어댑터가 재구독하지 않아 슬롯이 빈 상태로 남는다.
- `#if UNITY_EDITOR` 가드 없이 production 빌드에 어댑터 코드가 포함된다.
