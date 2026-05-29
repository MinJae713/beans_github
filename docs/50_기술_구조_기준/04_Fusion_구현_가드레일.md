---
last_updated: 2026-05-16
audience: ai-first
---

# Fusion 구현 가드레일

> 상태: accepted_candidate. 이 문서는 Host-only shortcut 금지, App ID/secret 비노출, StateAuthority 검증, 3D 물리 확장 금지에 한해 임시 구현 기준으로 사용한다. 문서 상태와 binding 범위는 `05_문서관리_규칙/08_문서_상태_인덱스.md`를 우선한다.

이 문서는 팀원 프로토타입을 기준으로 구현자가 반복해서 헷갈릴 수 있는 Unity/Fusion 경계선을 정리한다.

이 문서는 특정 개인 로컬 경로, Photon App ID, secret, 수치 고정값을 기록하지 않는다.

## 적용 범위

| 항목 | 기준 |
|---|---|
| 기준 구현 루트 | 팀이 합의한 Unity 구현 레포의 루트 |
| Unity 프로젝트 루트 | `Assets/`, `Packages/`, `ProjectSettings/`가 있는 경로 |
| 기준문서 루트 | `docs/` |
| 프로토타입 코드 | 현재 동작 관찰과 리스크 발견의 reference |
| 기준 우선순위 | `accepted` 기준문서가 프로토타입 임시 구현보다 우선 |

프로토타입에서 동작하는 방식이 있더라도, 그 방식이 기준문서의 금지선과 충돌하면 규범으로 승격하지 않는다. Host Mode에서만 동작하는 임시 코드도 Server Mode 전환을 막으면 기술 부채로 기록한다.

## `FindObjectOfType` 의존 제한

`FindObjectOfType`, `FindObjectsOfType` 같은 전역 탐색은 프로토타입 부트스트랩에서는 허용될 수 있지만, 전투 루프의 기본 구조로 삼지 않는다.

| 위치 | 기준 |
|---|---|
| 허용 후보 | 임시 bootstrap, editor helper, 테스트 probe |
| 제한 | `FixedUpdateNetwork()` 내부, 발사/피격/사망/카드 선택 hot path |
| 대체 | serialized reference, scene context, spawn 시 명시 주입, NetworkObject/NetworkId 참조 |
| 남길 경우 | 이유, 제거 시점, 영향 컴포넌트를 작업 브리핑 또는 리스크 로그에 기록 |

전역 탐색이 많아지면 네트워크 상태 소유권과 씬/프리팹 소유권이 흐려진다. 특히 카드 선택 UI, 플레이어 상태, 룸/구간 관리자는 서로를 전역 탐색으로 찾기보다 명시적으로 연결되어야 한다.

## `INetworkInput` 확장 슬롯 규칙

입력 구조체는 플레이어의 의도를 담는 통로이지, 모든 UI 상태를 밀어 넣는 저장소가 아니다.

| 슬롯 유형 | 허용 기준 |
|---|---|
| 이동축 | 매 tick 필요한 조작 의도 |
| 버튼 | 점프, 발사, 재장전처럼 CombatActive에서 반복 처리되는 의도 |
| 조준 방향 | 발사/방향 전환에 필요한 최소 벡터 |
| 짧은 enum | 무기 모드나 액션 모드처럼 tick 처리에 필요한 작은 상태 |
| 금지 | 카드 상세 데이터, UI 선택 결과 전체, 프리셋 객체 참조, secret/환경값 |

`INetworkInput`에 새 필드를 추가할 때는 아래를 작업 브리핑에 적는다.

| 항목 | 작성 기준 |
|---|---|
| 목적 | 어떤 gameplay intent를 전달하는가 |
| 허용 상태 | CombatActive, RoundIntro 등 어느 상태에서만 읽는가 |
| 소비자 | 어떤 컴포넌트가 `FixedUpdateNetwork()`에서 읽는가 |
| 충돌 입력 | 기존 입력과 동시에 들어올 때 우선순위 |
| 네트워크 비용 | 매 tick 필요한 값인지, RPC/event가 더 맞는지 |

카드 선택은 일반적으로 매 tick 입력이 아니라 `ChoicePending` 상태의 요청/검증/확정 흐름이다. 따라서 카드 선택 결과를 `INetworkInput`에 계속 싣는 방식은 기본안으로 삼지 않는다.

## 기준 레포/폴더 표기

문서와 작업 브리핑는 개인 절대경로가 아니라 repo-relative path를 사용한다.

| 잘못된 표기 | 올바른 표기 |
|---|---|
| 개인 PC의 전체 경로 | `Assets/...`, `docs/...` 같은 상대 경로 |
| 특정 실험 폴더를 영구 루트처럼 표기 | 현재 작업 대상 레포의 Unity 프로젝트 루트 |
| App ID가 들어간 설정 파일 내용 복사 | 값은 생략하고 placeholder, 등록 절차, local/secure 주입 방식만 설명 |

팀 공유용 문서에는 개인 경로, 개인 다운로드 폴더, 로컬 실행 산출물 경로를 적지 않는다.

## Fusion Weaver / asmdef 기준

Fusion `NetworkBehaviour`를 포함하는 assembly는 Fusion IL Weaver 대상에 포함되어야 한다. Assembly definition을 새로 나누거나 `NetworkBehaviour`, `INetworkInput`, `[Networked]` 타입의 assembly를 옮길 때는 아래 항목을 함께 확인한다.

| 항목 | 기준 |
|---|---|
| Weaver 대상 | `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`의 `AssembliesToWeave`에 해당 assembly가 포함되어 있는가 |
| 입력 타입 분리 | `INetworkInput` 구현체는 필요한 assembly에서 명시 참조할 수 있는 별도 asmdef로 분리되어 있는가 |
| 권한 범위 | `allowUnsafeCode`는 Fusion weave 결과가 필요한 최소 assembly에만 허용하는가 |
| 검증 | Build Both 이후 non-DevMode 2클라에서 game screen, player spawn, 입력/발사, 카드 선택 후 복귀가 통과하는가 |

현재 `NetworkBehaviour`와 `[Networked]` 속성을 포함하는 런타임 gameplay 코드는 `Colosseum.NetworkGameplay` assembly에 둔다. Fusion이 생성한 접근 코드가 `Fusion.NetworkBehaviour.Ptr`를 사용하므로 이 assembly만 `allowUnsafeCode=true`가 필요하다. 이 설정을 끄면 빌드는 통과해도 game server에서 `FieldAccessException: Fusion.NetworkBehaviour:Ptr is inaccessible`가 발생해 player spawn 이후 gameplay 검증이 실패한다.

이 설정은 일반 C# unsafe 블록 작성을 허용하려는 목적이 아니라 Fusion IL Weaver가 생성한 네트워크 접근 코드의 런타임 호환성을 맞추기 위한 예외다. 새 asmdef를 추가할 때는 이 예외를 넓히기보다 `NetworkBehaviour`가 있는 assembly와 `INetworkInput` assembly를 분리하고, 필요한 assembly만 Weaver 대상에 등록한다. `Colosseum.Game`은 gameplay 네트워크 코드의 하위 호환 경계 이름으로 유지하되 unsafe 권한을 갖지 않는다.

### A303-278 최소 경계 근거

`Colosseum.NetworkGameplay`는 "모든 게임 코드"가 아니라 현재 non-DevMode 2클라 gameplay loop를 구성하는 런타임 묶음이다. 이 묶음 안에는 서로 직접 참조하는 `NetworkBehaviour` 타입과 그 즉시 의존 타입이 함께 들어 있다.

Asmdef 바로 옆의 `Assets/_Project/Scripts/NetworkGameplay/Colosseum.NetworkGameplay.asmdef.rationale.md`도 같은 근거를 MR/review용으로 짧게 반복한다.

| 검토한 더 좁은 분리 후보 | 현재 바로 분리하지 않는 이유 |
|---|---|
| `UI` 하위 폴더 별도 asmdef | `HPBarManager`, `GameOverUI`, `RoomIndicatorUI`, `ReloadIndicator`가 `PlayerHealth`, `RoomManager`, `Gun`을 참조하고, runtime gameplay 쪽도 HP bar/game over UI를 직접 호출한다. 단순 분리는 assembly 순환 참조가 된다. |
| `DevMode` 하위 폴더 별도 asmdef | `DevModeController`/adapter가 `NetworkManager`, `CardData`, UI를 참조하고, `Gun.DevMode` partial이 `BulletInjectionAdapter`를 직접 참조한다. 단순 분리는 assembly 순환 참조가 된다. |
| `Card`, `Weapon`, `Player`, `Game` 폴더별 asmdef | `CardDeck -> PlayerHealth/Gun`, `Gun -> CardEffect/Game`, `BulletController -> Player/Game`, `RoomManager -> Player/Camera/UI` 의존이 교차한다. 폴더 단위 분리는 순환 참조 또는 대규모 인터페이스 추출이 필요하다. |

따라서 A303-278의 최소 실용 경계는 다음과 같이 정의한다.

1. `Colosseum.Game`에는 unsafe 권한을 주지 않는다.
2. `INetworkInput` 구현체는 `Colosseum.NetworkInput`으로 분리한다.
3. Fusion Weaver 대상과 `allowUnsafeCode=true`는 non-DevMode 2클라 gameplay loop가 실제로 필요한 `Colosseum.NetworkGameplay`에만 둔다.
4. `Colosseum.NetworkGameplay` 안에 수동 `unsafe` 블록, 포인터 조작, 임의 unmanaged memory 접근을 추가하지 않는다. 이 예외는 Fusion Weaver 생성 코드 호환성에만 사용한다.
5. 더 좁은 asmdef 분리는 `UI`, `DevMode`, `Card`, `Weapon`, `Player`, `Game` 사이의 직접 참조를 인터페이스/event boundary로 먼저 끊은 뒤 별도 리팩터 이슈로 수행한다.

## Host Mode 임시동작 금지선

1차 시연은 Host Mode를 사용할 수 있지만, Host Mode에서만 우연히 맞는 구조를 규범으로 만들지 않는다.

| 항목 | 기준 |
|---|---|
| 허용 | Host Mode로 로컬 2인/온라인 1v1을 먼저 검증 |
| 허용 예외 | Q39 "후진 금지" Host-only clamp — 2026-04-21 D43 (Option A1). W3 RTT 실측에서 rubber-banding 체감 시 A2 승격. 상세 [90/04#Q39](../90_백로그/04_미결정_질문_레지스터.md) |
| 금지 | Host 클라이언트 UI가 곧바로 authoritative 상태를 바꾸는 구조 |
| 금지 | StateAuthority 검증 없이 카드/피격/리스폰 결과를 확정 |
| 기록 | Server Mode 전환 시 바꿔야 하는 임시 브리지는 리스크로 남김 |

Dedicated Server 프로토타입에서 `Lobby -> Worker Agent -> GameServer` 계층을 둘 수 있다. 이때 `Lobby <-> Worker Agent` HTTP 통신은 Fusion gameplay state가 아니라 **서버 프로세스 실행 제어용 control plane**으로 취급한다. 카드/피격/리스폰/승패 같은 gameplay authoritative 결과는 여전히 Fusion `StateAuthority` 규칙을 따른다.

카메라 transform 자체는 presentation으로 각 클라이언트가 보간할 수 있지만, 이동 주도권 카메라의 기준 X와 적용 구간은 gameplay 경계 판정과 연결되므로 local-only 계산으로 두지 않는다. `RoomManager` 같은 `StateAuthority` 소유자가 `[Networked]` camera target state를 확정하고, `CameraController`와 이동 경계 판정은 같은 target을 읽어 host/join divergence를 막는다. 로컬 클라이언트가 현재 movement camera authority인 경우에는 이 networked target을 기준점으로 유지한 채 자기 predicted transform X로 presentation target을 보강할 수 있다. 이 보강은 카메라 표시 부드러움 전용이며 경계 판정과 승패 truth를 변경하지 않는다.

현재 구현에서는 Room Registry도 같은 control plane에 속한다.

- Worker Agent는 `POST/PATCH/DELETE /api/v1/rooms`로 Spring Boot Room API를 호출한다.
- Lobby는 Find Room 화면에서 Spring Boot `GET /api/v1/rooms` 결과를 사용해 방 목록을 구성한다.
- Redis에 저장된 방 메타데이터는 gameplay truth가 아니라 service state다.

즉 "Fusion authoritative state"와 "방 프로세스/방 목록 service state"를 문서와 구현 모두에서 섞지 않는 것이 현재 가드레일이다.

## 금지

- Photon App ID, secret, 계정 정보, 개인 절대경로를 기준문서, MR 본문, tracked 설정 파일에 실제 값으로 적는 것.
- 신규 3D Rigidbody, 3D Collider, ConfigurableJoint 기반 전투 확장을 허용하는 문구.
- `FindObjectOfType` 기반 런타임 탐색을 안정 구조처럼 문서화하는 것.
- Host 전용 임시동작을 기준 구현으로 인정하는 것.
- 실제 키 매핑이나 수치값을 기준문서의 최종값처럼 고정하는 것.

## 구현 브리핑 체크

구현자가 Fusion 관련 작업을 시작할 때는 아래를 확인한다.

| no | 확인 항목 |
|---|---|
| 1 | Unity 프로젝트 루트가 repo-relative path로 적혀 있는가 |
| 2 | 수정 파일과 읽기 전용 파일이 분리되어 있는가 |
| 3 | `FindObjectOfType` 추가 또는 유지 이유가 있는가 |
| 4 | `INetworkInput` 필드 추가가 정말 매 tick 입력인지 확인했는가 |
| 5 | App ID/secret/개인 경로가 산출물에 없는가 |
| 6 | Host Mode 임시동작과 기준 구현이 구분되어 있는가 |
| 7 | Worker Agent를 도입했다면 gameplay authority와 process orchestration 책임이 분리되어 있는가 |

## 실전 Fusion 가드레일 5항목 (PR 리뷰 체크리스트)

Fusion 2 네이티브 구현에서 반복 재현되는 실전 함정을 정리한다. 각 항목은 PR 리뷰 시 기계적으로 확인 가능한 형태로 작성한다.

### 1. `[Networked]` 컬렉션은 반드시 `[Capacity(N)]`

| 구분 | 내용 |
|---|---|
| 규칙 | `NetworkArray<T>`, `NetworkDictionary<K,V>`, `NetworkLinkedList<T>` 에는 반드시 `[Capacity(N)]` 을 붙인다. N은 프리팹 스폰 최대치 기준 |
| 반례 | `[Networked] public NetworkArray<int> Foo { get; }` — 빌드 에러 또는 런타임 실패 |
| 정례 | `[Networked, Capacity(16)] public NetworkArray<int> Foo { get; }` |
| 탐지 | `rg '\[Networked\][^\[]*Network(Array\|Dictionary\|LinkedList)' Assets/_Project/Scripts` 후 `[Capacity(` 누락 검출 |

### 2. 변경 감지는 `ChangeDetector` 또는 `OnChanged` 콜백

| 구분 | 내용 |
|---|---|
| 규칙 | HP bar, damage flash, UI 갱신은 `Update()` polling 금지. `Render()` 내부 `ChangeDetector` 또는 `[Networked, OnChangedRender(nameof(Handler))]` 콜백 사용 |
| 반례 | `void Update() { hpBar.value = health.Current / health.Max; }` — 매 프레임 동작, resim 부정확 |
| 정례 (`ChangeDetector`) | `Render()` 안에서 `GetChangeDetector(ChangeDetector.Source.SimulationState)` + `DetectChanges(this, out prev, out cur)` |
| 정례 (콜백) | `[Networked, OnChangedRender(nameof(OnHpChanged))] public int Health { get; set; }` |
| 탐지 | `Update`/`LateUpdate` 내부에서 `[Networked]` 속성 읽기 + UI에 쓰는 패턴 찾기 |

### 3. `Runner.IsForward` 없이 side effect 금지 (Resimulation 가드)

| 구분 | 내용 |
|---|---|
| 규칙 | Fusion client prediction에서 `FixedUpdateNetwork()` 는 한 tick에 여러 번 호출된다. `AudioSource.Play`, `Instantiate`, `SetActive(true)`, Particle trigger 는 반드시 `if (Runner.IsForward) { ... }` 내부에서만 |
| 반례 | `public override void FixedUpdateNetwork() { AudioSource.PlayOneShot(hitSfx); }` — resim 중 여러 번 재생 |
| 정례 | `if (Runner.IsForward) AudioSource.PlayOneShot(hitSfx);` |
| 탐지 | `rg -n 'AudioSource\.(Play\|PlayOneShot)\|Instantiate\|SetActive' Assets/_Project/Scripts` 후 각 라인이 `IsForward` 분기 내부인지 확인 |

### 4. Tick 기반 시간만 사용, `Time.deltaTime` · `Time.time` 금지 (FixedUpdateNetwork 내부)

| 구분 | 내용 |
|---|---|
| 규칙 | 투사체 수명, 쿨타임, DoT, 버프 지속시간 등 시간 기반 카운터는 `Runner.DeltaTime` 또는 `Runner.Tick` 기반 |
| 반례 | `lifetime -= Time.deltaTime;` in `FixedUpdateNetwork()` — 결정론 깨짐, Host/Client desync |
| 정례 | `lifetime -= Runner.DeltaTime;` 또는 `if (Runner.Tick >= expireTick) Runner.Despawn(Object);` |
| 탐지 | `rg -n 'Time\.(deltaTime\|time\|unscaledDeltaTime)' Assets/_Project/Scripts/Gameplay Assets/_Project/Scripts/Network` |

### 5. RPC 방향성 명시 (source · target · channel)

| 구분 | 내용 |
|---|---|
| 규칙 | `[Rpc(RpcSources.X, RpcTargets.Y)]` source/target 조합은 의도 반영. Channel은 게임플레이 영향 유무로 구분 |
| 정례 | `[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]` — 카드 선택 요청, 스킬 사용 요청 |
| 정례 | `[Rpc(RpcSources.StateAuthority, RpcTargets.All)]` — 킬 피드, 라운드 종료 알림 |
| 정례 | `[Rpc(Channel = RpcChannel.Unreliable)]` — 손실 허용 연출 전용 (타격 VFX 등) |
| 반례 | `[Rpc(RpcSources.All, RpcTargets.All)]` — 권한 탈취 경로, 기본 금지 |
| 반례 | 게임플레이 결과 RPC를 `Unreliable`로 표기 — 손실 시 상태 불일치 |
| 탐지 | `rg -n '\[Rpc\(' Assets/_Project/Scripts` 후 source/target 명시 여부 수동 확인 |

### 실전 가드레일 요약 체크 (PR 시 5초 확인)

| no | 확인 항목 | 탐지 grep |
|---|---|---|
| 1 | `[Networked]` 컬렉션에 `[Capacity]` 붙어있는가 | `rg '\[Networked\][^\[]*Network(Array\|Dictionary\|LinkedList)'` |
| 2 | `Update()` 에서 `[Networked]` 속성을 UI에 직접 쓰지 않는가 | 수동 |
| 3 | `FixedUpdateNetwork` 내부 side effect가 `IsForward` 분기 안인가 | `rg -n 'AudioSource\.Play\|Instantiate\|SetActive'` |
| 4 | `FixedUpdateNetwork` 내부에 `Time.deltaTime` 없는가 | `rg -n 'Time\.(deltaTime\|time)'` |
| 5 | `[Rpc(...)]` 에 source/target 명시되고 `All/All` 이 없는가 | `rg -n '\[Rpc\('` |

## 미결정

- 프로토타입에 남은 전역 탐색을 어느 마일스톤에서 제거할지.
- 카드 선택 요청을 RPC로 둘지, 별도 Networked request state로 둘지.
- Server Mode 전환 전까지 허용할 Host Mode bridge의 최대 범위.
- 실전 가드레일 자동화: Editor 스크립트 또는 CI grep 체크를 어느 단계에서 활성화할지.
