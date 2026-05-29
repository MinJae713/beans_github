---
last_updated: 2026-05-08
audience: ai-first
---

# Unity 프로젝트 구조

> 상태: accepted. 이 문서는 Unity 2022.3 LTS, Photon Fusion 2, 2D 물리, 3D 물리 금지선의 현재 구현 기준이다. 문서 상태와 binding 범위는 `05_문서관리_규칙/08_문서_상태_인덱스.md`를 우선한다.

## 기본 환경

| 항목 | 기준 |
|---|---|
| Unity 버전 | Unity 2022.3 LTS 계열 필수. 패치 버전은 팀 호환 기준 |
| 구현 레포 루트 | 팀이 합의한 Unity 구현 레포의 clone 루트 |
| Unity 프로젝트 루트 | `Assets/`, `Packages/`, `ProjectSettings/`가 있는 경로. 작업 브리핑에는 repo-relative path로 명시 |
| 기준문서 루트 | `docs/` |
| raw artifact 위치 | 팀이 합의한 레포 밖 또는 ignore된 로컬 artifact 경로 |

Unity 2022.3 LTS 계열을 필수 기준으로 둔다. 세부 패치 버전은 팀 개발환경과 에셋 호환성을 기준으로 맞춘다. Unity 6은 Corgi/Odin/Fusion 에셋 호환 리스크 때문에 현재 기준에서 제외한다.

특정 실험용 프로젝트 루트를 기준문서의 고정값으로 보지 않는다. 현재 작업 대상 레포에서 `Assets/`, `Packages/`, `ProjectSettings/`가 함께 있는 위치를 Unity 프로젝트 루트로 보고, 작업 브리핑에 상대 경로로 명시한다.

## 목표 아키텍처

기술스택은 팀원이 확정한 기준을 따른다.

게임의 목표 구조는 `Unity 2022.3 LTS + Photon Fusion 2 + Rigidbody2D/NetworkRigidbody2D` 기반의 2D Fusion 네이티브 구조다.

| 영역 | 기준 |
|---|---|
| 카메라 | Orthographic, 횡스크롤 2D 화면 |
| 게임 평면 | X-Y 2D 평면 |
| 물리 | Rigidbody2D + Collider2D + NetworkRigidbody2D |
| 캐릭터 | Razor Madness 구조를 참고한 Fusion 네이티브 자체 구현 |
| 래그돌 | 사용하지 않음. 사망 연출은 애니메이션/VFX/피드백으로 처리 |
| Corgi | 캐릭터 시스템이 아니라 MMFeedbacks, 레벨 도구, 참고 코드 활용 |
| Odin | 카드/프리셋 ScriptableObject 편집 생산성 용도 |
| 네트워크 | Photon Fusion 2 Server Authoritative 지향 |

기존의 `겉보기 2D + 내부 3D 물리`, `3D Rigidbody`, `Configurable Joint`, `액티브 래그돌` 전제는 폐기한다.

이 결정은 게임 규칙 결정이 아니라 기술스택 결정이다. 전장/구간/카드 보상/TTK 철학은 기존 기획 기준을 계속 따른다.

## 기술스택 준수 게이트

구현 작업은 아래 기준을 먼저 통과해야 한다.

| 구분 | 기준 |
|---|---|
| 허용 API | Rigidbody2D, Collider2D, NetworkRigidbody2D |
| 허용 API | NetworkBehaviour, INetworkInput, FixedUpdateNetwork |
| 금지 API | 신규 3D Rigidbody, 3D Collider, ConfigurableJoint |
| 금지 전제 | 액티브 래그돌 기반 캐릭터, 3D 물리 기반 전투 확장 |
| 관찰 처리 | 기존 잔존 코드는 파일/라인을 기록하고 기술 부채화 |
| BLOCKED 처리 | 목표 구현이 금지 API 확장을 요구하면 진행 중단 |

기존 코드에 3D 물리 기반 구현이 남아 있어도, 작업자가 임의로 대규모 마이그레이션을 시작하지 않는다. 기능 구현 범위 안에서 확장해야 하는 순간 `BLOCKED`로 보고하고, 별도 마이그레이션 작업으로 분리한다.

## 확정 기술스택

| 항목 | 기준 |
|---|---|
| 엔진 | Unity 2022.3 LTS |
| 렌더 파이프라인 | URP |
| 넷코드 | Photon Fusion 2 |
| 초기 모드 | Host Mode 시작, Dedicated Server 프로토타입 병행 가능 |
| 이후 목표 | 2차 릴리스에서 Server Mode 안정화 및 원격 Worker 배포 |
| 권위 모델 | Server Authoritative 지향 |
| 캐릭터 구조 | NetworkBehaviour + NetworkRigidbody2D |
| 레퍼런스 | Photon Fusion Razor Madness |
| 물리 | Rigidbody2D / Collider2D |
| Corgi Engine | MMFeedbacks, 레벨 도구, 소스 패턴 참고 |
| Odin Inspector | 카드/프리셋 SO 편집 |

## Corgi Engine 활용 범위

| 영역 | 사용 | 기준 |
|---|---|---|
| 캐릭터 컨트롤러 | 아니오 | Fusion tick/rollback 구조와 충돌 위험 |
| 무기/어빌리티 | 아니오 | NetworkBehaviour 기반으로 자체 구현 |
| Health/Damage | 아니오 | 서버 권위와 [Networked] 상태로 자체 구현 |
| MMFeedbacks | 예 | 히트스톱, 셰이크, 슬로모, 파티클 트리거 |
| 레벨/카메라 도구 | 예 | 레벨 제작과 카메라 보조 도구로 활용 |
| 소스코드 패턴 참고 | 예 | 직접 복붙이 아니라 구조 참고 |

## 네트워크 구조 원칙

- M1부터 Fusion 위에서 구현한다.
- 로컬 1v1도 네트워크 입력 구조를 전제로 만든다.
- 입력은 `INetworkInput` 구조체와 `GetInput()` 패턴으로 수집한다.
- 이동/점프/공격은 `FixedUpdateNetwork()` 안에서 처리한다.
- 위치 동기화는 `NetworkRigidbody2D` 또는 Fusion 네이티브 2D 패턴을 우선한다.
- HP, 카드 상태, 탄약, 쿨타임, 사망/리스폰 상태는 `[Networked]` 상태 또는 서버 권위 이벤트로 관리한다.
- VFX, 카메라 셰이크, 히트스톱, 사망 연출은 로컬 presentation 효과로 분리한다.
- 서버 판정 로직은 표현 오브젝트를 직접 제어하지 않고, 필요한 경우 `Contracts`에 정의된 사건 데이터나 읽기 전용 상태를 공개한다.
- presentation 코드는 VFX/SFX/UI/카메라 피드백을 재생할 수 있지만, 체력/탄약/피격/승패 같은 게임 결과를 변경하지 않는다.
- Dedicated Server 프로토타입에서는 `Lobby -> Worker Agent -> GameServer` 계층을 둘 수 있다.
- 이때 `Lobby <-> Worker Agent` 통신은 게임 판정 권한이 아니라 process orchestration/control plane으로 취급한다.
- 방 목록과 Room Registry는 Fusion gameplay truth가 아니라 별도 service/control plane 상태로 취급할 수 있다.
- 현재 구현 기준으로 로비의 방 목록 조회는 Worker Agent 로컬 메모리 polling이 아니라 Spring Boot Room Registry API 조회를 사용한다.

## 현재 구현 경계 (2026-05-07 기준)

현재 Unity 런타임 경계는 폴더뿐 아니라 asmdef 기준으로도 나뉜다.

| asmdef | 역할 | 비고 |
|---|---|---|
| `Colosseum.Contracts` | 공용 enum, snapshot, scene 선택 계약 | 로직/표현이 공유하는 최소 타입 |
| `Colosseum.NetworkInput` | `INetworkInput` 구현체 | Fusion 입력 전용 |
| `Colosseum.NetworkGameplay` | Fusion 런타임 gameplay, Lobby/Game server, Worker 연동 | Fusion Weaver 대상. 현재 유일한 `allowUnsafeCode=true` runtime assembly |
| `Colosseum.Presentation` | 메뉴/UI/HUD 등 표현 계층 | `Contracts`, `NetworkGameplay`를 소비 |
| `Colosseum.Game` | 하위 호환 또는 비-network 경계 이름 | `allowUnsafeCode=false`, Fusion Weaver 대상 아님 |
| `Colosseum.DevMode` | Editor Play Mode용 DevMode 컨트롤러와 어댑터 | `defineConstraints: UNITY_EDITOR`, `allowUnsafeCode=false`, scene 로드를 위해 `includePlatforms: Editor`는 사용하지 않음 |
| `Colosseum.StatusEffects` | 상태이상 ScriptableObject 구현 | `NetworkGameplay`를 참조하되 `allowUnsafeCode=false` |

현재 구현에서는 `NetworkGameplay`가 방 생성, 게임 서버 프로세스 기동 요청, Room Registry 조회 브리지를 포함한다. `Presentation`은 Room 목록 UI, 로비 버튼, 화면 전환 같은 표현을 맡고, `Contracts`는 `SelectableScene` 같은 공통 타입을 제공한다.

현재 원격 배포 레퍼런스는 gameplay runtime 외부에 4개 역할을 둔다: Onboarding EC2(Nginx 정적 다운로드), Lobby EC2(Unity Linux Dedicated 프로세스), Worker EC2(Docker 기반 Worker Agent + Linux Dedicated mount), API EC2(Docker 기반 Spring Room API). 이 중 Onboarding은 client distribution 전용이며 Fusion gameplay truth나 Room Registry authoritative source에 포함되지 않는다.

A303-282 이후 `Colosseum.Game`은 `CardData`, `GameplayProfile`, `RoomData`, `SegmentProfile`, `SceneSearch`, 카메라 edge helper처럼 Fusion weaver가 필요 없는 타입을 받는 방향으로 축소한다. `Colosseum.DevMode`는 Editor 전용 구현을 분리하지만 scene에 붙은 MonoBehaviour가 Editor Play Mode에서 로드되어야 하므로 `includePlatforms: Editor`가 아니라 `UNITY_EDITOR` define constraint와 소스 가드를 사용한다. `NetworkGameplay`에서 DevMode 타입을 직접 참조하는 구조는 금지하고, 불가피한 Editor 전용 bridge는 명시적으로 좁게 둔다.

## 서빙/릴리스 구조

기술 기준은 아래 2단계 릴리스 구조를 따른다.

| 단계 | 기준 |
|---|---|
| 1차 시연 | Windows Standalone + Photon Cloud Host Mode |
| 1차 매칭 | 최소 세션 입장을 기본선으로 두고, Lobby Browser / Room Registry 경로는 `06_매칭_로비_기준.md`의 candidate gate를 따른다 |
| 1차 데이터 | 로컬 CSV 또는 로컬 로그 수준 |
| 1차 폴백 | Photon Cloud 온라인 -> LAN Host Mode -> 로컬 2P |
| 2차 공개 베타 | Server Mode + WebGL 빌드 + 웹 리더보드 후보 |
| 2차 조건 | Server Mode 안정화와 WebGL WebSocket 리허설 통과 |

WebGL은 1차 시연의 공식 목표가 아니다. 브라우저/WebSocket/탭 비활성화/Host 불안정성을 검증하기 전에는 2차 릴리스 범위로 둔다.

현재 프로토타입에서 Dedicated Server 구조를 실험하더라도, 1차 시연의 binding baseline은 Windows Standalone + Photon Cloud Host Mode + 최소 세션 입장이다. Worker Agent 도입은 운영 구조 실험이며, 기준 릴리스 경로와 별도로 관리한다.

`06_매칭_로비_기준.md`의 게이트는 Room Registry / Lobby Browser 경로를 사용했다고 주장하는 MR, 리허설, 시연-ready 보고에만 mandatory로 적용한다. 해당 문서가 `accepted_candidate`로 승격되고 DRI 증빙 게이트를 통과하기 전에는 "Room Registry 시연-ready", "Lobby Browser push-ready", "방 목록 로비 완료" 같은 표현을 release baseline처럼 사용하지 않는다.

## WebGL/리더보드 범위

- WebGL 빌드는 빌드 파이프라인 확인 정도는 가능하지만, 1차 시연의 주 경로로 삼지 않는다.
- 공개 베타에서 WebGL을 지원하려면 Server Mode가 먼저 안정되어야 한다.
- 리더보드와 영속 데이터는 1차에서 제외한다.
- 1차에서 필요한 기록은 플레이테스트와 디버깅을 위한 로컬 로그로 제한한다.

## 레이어 구분

작업 브리핑에서는 수정 범위를 아래 레이어 중 하나 이상으로 명시한다. 한 GameObject나 Prefab 안에 여러 레이어 컴포넌트가 함께 붙을 수 있지만, 코드 책임은 섞지 않는다.

| 레이어 | 역할 |
|---|---|
| Domain | 서버 판정 대상 게임 규칙, 카드 효과 적용, 전투 수치, 승패 판정 |
| Contracts | Domain과 Presentation이 공유하는 타입, 이벤트 payload, 읽기 전용 상태 스냅샷 |
| Presentation | Unity 씬, 프리팹, 렌더링, VFX, SFX, HUD, 카메라 피드백 |
| Input | 키 매핑, 입력 수집, 플레이어 조작 |
| Config | ScriptableObject, 밸런스 값, 에셋 참조 |

레이어별 기본 방향은 아래와 같다.

- `Domain`은 `FixedUpdateNetwork()` 안의 판정과 `[Networked]` 상태를 중심으로 한다.
- `Contracts`는 서로 다른 레이어가 공유해야 하는 C# 타입만 둔다. 실행 흐름, 계산 로직, Unity 오브젝트 조작은 넣지 않는다.
- `Presentation`은 `Contracts` 또는 Domain의 읽기 전용 상태를 소비해 화면과 소리를 갱신한다.
- `Input`은 사용자의 조작 의도를 수집해 Fusion 입력 구조로 전달한다.
- `Config`는 런타임 정책과 에셋 참조를 데이터로 제공한다.

## 권장 디렉터리 기준

현재 구현 구조가 아래 경로를 모두 갖고 있지 않아도, 새 코드와 에셋을 추가할 때는 이 기준을 우선한다. 기존 파일 이동은 기능 작업 중 임의로 하지 않고 별도 정리 작업으로 분리한다.

### `unity/Assets/_Project/Scripts/Contracts/`

서버 판정 로직과 클라이언트 표현이 공유해야 하는 C# 계약을 둔다. 이 폴더는 일반 `util` 폴더가 아니다. "여러 곳에서 쓰니까 넣는 곳"이 아니라, 레이어 사이에서 합의해야 하는 의미와 데이터 모양을 고정하는 곳이다.

포함 가능 항목은 아래와 같다.

- 전투 사건 payload: 발사, 피격, 반사, 사망, 리스폰, 구간 돌파 같은 사건의 데이터 구조.
- 분류 enum: 표면 타입, 카드 효과 태그, 충돌 결과 타입, feedback 종류.
- 읽기 전용 스냅샷: 플레이어, 투사체, 카드 적용 상태를 외부에서 표시하거나 검증하는 데 필요한 최소 상태.
- 계약 인터페이스: presentation이나 QA가 구독할 수 있는 사건 소스의 읽기 전용 인터페이스.

넣지 않는 항목은 아래와 같다.

- 데미지 계산, 탄도 이동, 카드 효과 적용 같은 게임 판정 로직.
- ParticleSystem, AudioSource, Animator, Camera, UI Text 같은 표현 실행 코드.
- 특정 프리팹, 씬 오브젝트, 머티리얼, 사운드 클립에 대한 직접 참조.
- 임시 편의 메서드 모음, 무관한 문자열 상수 모음, 테스트 전용 헬퍼.

예시 구조는 아래와 같다.

```text
unity/Assets/_Project/Scripts/Contracts/
- Combat/
  - CombatEventType.cs
  - FireEventPayload.cs
  - HitEventPayload.cs
  - RicochetEventPayload.cs
  - DeathEventPayload.cs
  - SurfaceType.cs
- Card/
  - CardEffectTag.cs
  - CardRuntimeSnapshot.cs
- Weapon/
  - BulletRuntimeSnapshot.cs
  - BulletImpactPayload.cs
- Player/
  - PlayerRuntimeSnapshot.cs
```

계약 타입은 직렬화와 네트워크 전달 가능성을 고려해 단순한 값 중심으로 설계한다. Unity 오브젝트 참조가 필요해 보이면 먼저 `network id`, `player id`, `bullet id`, `world position`, `normal`, `effect tag`처럼 안정적인 식별자와 값으로 표현할 수 있는지 확인한다.

### `unity/Assets/_Project/Scripts/Presentation/`

시각/청각/카메라/UI 피드백을 실행하는 C# 컴포넌트를 둔다. 이 폴더의 코드는 `Contracts`의 사건 데이터나 Domain의 읽기 전용 상태를 소비해 표현을 갱신한다.

포함 가능 항목은 아래와 같다.

- `BulletVisualBinder`: 투사체 id나 상태를 받아 trail, 색, 크기, 머티리얼을 갱신.
- `ImpactFeedbackPlayer`: 피격 위치, 표면 타입, normal을 받아 파티클과 사운드를 재생.
- `PlayerFeedbackBinder`: 피격 깜박임, 사망 애니메이션 트리거, 리스폰 표현을 연결.
- `SegmentAdvanceSfxPresentationManager`: `VfxEventBus`의 `SegmentBoundaryAdvanced`/`segment_advance` payload를 구독해 구간 돌파 local SFX one-shot을 재생하고 resimulation 중복 재생을 방지.
- `CameraFeedbackController`: 카메라 셰이크, 줌, 구간 이동 피드백을 재생.
- `HudFeedbackBinder`: 탄약, 체력, 카드 선택 상태 같은 표시를 갱신.

넣지 않는 항목은 아래와 같다.

- 체력 감소, 탄약 차감, 카드 효과 적용, 사망 판정, 승패 판정.
- `FixedUpdateNetwork()` 안에서 게임 결과를 바꾸는 코드.
- Network truth를 presentation 오브젝트의 상태로 대체하는 코드.

Presentation 컴포넌트는 같은 Prefab 안에서 Domain 컴포넌트와 공존할 수 있다. 예를 들어 `Bullet` 루트에 판정/동기화 컴포넌트가 있고, 하위 `Visual` 오브젝트에 trail, particle, renderer, binder가 붙는 구조는 허용한다. 이때 Domain 컴포넌트는 Visual 하위 구조를 몰라도 동작해야 하며, Visual 교체가 판정 결과를 바꾸면 안 된다.

현재 구현 예시:

- `Scripts/Presentation/UI/MainMenuUI.cs`는 닉네임 입력 완료 후 Lobby 선접속을 트리거하고, Room 목록 Refresh 버튼, Create Room 버튼의 Lobby 연결 상태 기반 활성/비활성, 로비 화면 전환을 관리한다.
- `Scripts/Presentation/SFX/SegmentAdvanceSfxPresentationManager.cs`는 구간 돌파 확정 사건을 소리로 표현하되 게임 결과를 변경하지 않는다.
- `Scripts/NetworkGameplay/Network/NetworkManager.cs`는 `ConnectToLobby`, `CREATE_ROOM`, `LIST_ROOMS`, `JoinGameRoom` 흐름과 Client timing 로그를 관리한다.
- `Scripts/NetworkGameplay/Network/LobbyServerManager.cs`는 Worker Agent spawn 요청과 Spring Room API 조회를 브리지하고, Lobby timing 로그(`LT1`, `LT2`)를 기록한다.
- `worker-agent/Program.cs`는 GameServer spawn / cleanup과 Room API `POST/PATCH/DELETE`를 수행하며 Worker timing 로그(`WT1` ~ `WT4`)를 기록한다.
- `Scripts/NetworkGameplay/Network/GameServerManager.cs`는 Fusion `StartGame(Server)` 준비/완료 구간을 `GT1` ~ `GT5` 로그로 분해해 GameServer cold start 병목을 관찰한다.

이 경계는 "UI가 직접 HTTP를 호출하지 않고, Presentation -> NetworkGameplay -> control plane" 순서로 흐르도록 유지한다.

### `unity/Assets/_Project/Data/Presentation/`

표현 레이어가 참조하는 ScriptableObject와 에셋 매핑 데이터를 둔다. 이 경로는 밸런스 수치의 출처가 아니라, 동일한 사건을 어떤 표현으로 보여줄지 정하는 presentation 설정의 출처다.

포함 가능 항목은 아래와 같다.

- 표면 타입별 impact VFX/SFX 매핑.
- 카드 효과 태그별 투사체 색, trail, 발광, HUD 아이콘 매핑.
- 피격/사망/리스폰 feedback profile.
- 구간 돌파 `segment_advance` sweep VFX 연결 profile. SFX는 같은 event/effect key를 `SegmentAdvanceSfxPresentationManager`가 구독해 별도 one-shot으로 처리한다.
- 카메라 shake/zoom/transition profile.
- HUD 표시 스타일과 warning threshold 같은 표현 전용 설정.

넣지 않는 항목은 아래와 같다.

- 실제 데미지, 탄속, 장전 시간, 탄약 수 같은 전투 판정 수치.
- 카드 효과의 최종 판정값.
- NetworkProjectConfig, Photon AppID, 빌드/배포 환경 값.

`Data/Cards/`와 `GameplayProfile.asset`은 게임 규칙과 수치 정책을 제공하고, `Data/Presentation/`은 그 결과를 보여주는 방식을 제공한다. 예를 들어 `Electric Shot` 카드가 어떤 effect tag를 갖는지는 카드/계약 기준이고, 그 tag가 파란 trail과 스파크 particle로 보이는지는 `Data/Presentation/` 기준이다.

현재 런타임에서 Resources 로드가 필요한 VFX presentation profile은 `unity/Assets/_Project/Resources/Presentation/VfxFeedbackProfile.asset`처럼 `Resources/Presentation/` 아래에 둘 수 있다. 이 경로의 asset도 전투 수치의 정본이 아니라 `VfxEventType`, `EffectKey`, duration/radius, source/target follow 여부, attach offset 값을 VFX 표현으로 연결하는 설정이다. 구간 돌파 sweep은 payload의 world position과 이 asset의 duration/radius 값을 조합해 표시하고, 구간 돌파 SFX는 이 asset의 duration/radius가 아니라 `SegmentAdvanceSfxPresentationManager`가 수신한 event/effect key, dedupe key, world position을 기준으로 별도 재생한다.

외부 VFX 후보 asset을 제품 branch로 옮길 때는 `_CandidateImports` 전체를 반입하지 않고, 선택된 root prefab과 Unity가 요구하는 transitive dependency 및 `.meta`만 포함한다.

## Room Registry / 외부 서비스 연동 기준

현재 구현 기준으로 Room Registry는 Unity 내부 정본이 아니다.

| 단계 | 책임 |
|---|---|
| Unity Lobby | `LIST_ROOMS` 요청 수신, Spring Room API 결과를 클라이언트용 로비 JSON으로 전달 |
| Worker Agent | GameServer 프로세스 spawn / 종료 / stdout 상태 수집 / Room API `POST/PATCH/DELETE` 호출 |
| Spring Boot | 중앙 Room Registry API (`/api/v1/rooms`) |
| Redis | 중앙 방 메타데이터 저장소 (`room:{sessionName}`, `rooms:all`) |

현재 UI는 `Update()` polling으로 방 목록을 계속 읽지 않는다. Find Room 진입 시 1회 조회, Refresh 버튼 수동 조회, 방 생성 직후 이벤트성 broadcast 갱신을 기본으로 한다. 이 선택은 Room Registry 정본이 외부 REST API로 이동한 현재 구조에 맞춘 것이다.

원격 분리 배포 reference에서는 Lobby가 `COLOSSEUM_WORKER_BASE_URL=http://10.x.x.x:8080`, `COLOSSEUM_ROOM_API_BASE_URL=http://10.x.x.x:8081/api/v1` 형식의 private VPC 주소를 사용해 Worker/API에 접근한다. Worker와 API는 `infra/remotebuild/` 자산으로 컨테이너화할 수 있지만, Lobby는 현재 기준으로 Linux Dedicated 프로세스를 직접 실행하는 경계를 유지한다.

## 이벤트 계약 원칙

서버 판정 로직은 "무슨 일이 일어났는가"를 안정적인 데이터로 공개하고, presentation은 "그 일을 어떻게 보여줄 것인가"를 결정한다.

사건 데이터에 포함할 수 있는 값은 아래와 같다.

- 사건 종류: fired, hit, ricochet, death, respawn, segment breached 등.
- 주체 식별자: owner player id, target player id, bullet id, segment id.
- 위치와 방향: world position, normal, incoming direction, outgoing direction.
- 결과 값: damage, knockback impulse, remaining health, is lethal.
- 분류 값: surface type, card effect tags, weapon type.
- 네트워크 기준: simulation tick, prediction/confirmed 여부가 필요한 경우의 상태.

사건 데이터에 넣지 않는 값은 아래와 같다.

- 특정 particle prefab, material, sound clip, animator trigger 이름.
- 카메라 shake 강도처럼 presentation profile에서 정해야 하는 값.
- UI 문구나 색상 같은 화면 표현 정책.
- 클라이언트 로컬에서만 의미 있는 임시 GameObject 참조.

이 원칙을 따르면 서버 판정 로직은 표현 방식 변경의 영향을 덜 받고, 표현 담당자는 같은 사건을 VFX/SFX/UI/카메라에 독립적으로 연결할 수 있다.

## 파일 수정 권한 기준

| 범주 | 의미 |
|---|---|
| 수정 가능 | 이번 작업자가 실제로 변경해도 되는 파일/디렉토리 |
| 읽기 전용 | 참고만 가능하고 수정하면 안 되는 파일/디렉토리 |
| 동시 수정 금지 | 씬, 프리팹, 공용 ScriptableObject처럼 충돌 위험이 큰 대상 |
| 생성 가능 | 새 테스트, 새 임시 씬, 새 산출물을 만들 수 있는 위치 |

Unity 작업에서는 씬과 프리팹 충돌이 크므로, 여러 세션이 같은 씬이나 공용 프리팹을 동시에 수정하지 않는다.

## 네트워크 대비 원칙

- 로컬 MVP라도 Input, Simulation/Domain, Presentation을 섞지 않는다.
- 네트워크 truth는 입력, 핵심 위치, 체력, 탄약, 카드 상태, 발사 이벤트, 피격/사망/구간 돌파 판정이다.
- `Contracts`는 네트워크 truth와 presentation 사이에서 공유하는 데이터 모양을 고정하지만, truth 자체를 소유하지 않는다.
- 사망 연출, 카메라 셰이크, 파티클, UI 피드백은 네트워크 truth가 아니다.
- 시각 효과는 로컬 presentation에서 보정한다.
- 예측 이펙트는 가능하지만, 서버 확정 결과와 충돌하면 서버 확정 결과를 기준으로 보정한다.

## 산출물 정책

raw 로그, 스크린샷, 영상, crash dump, 임시 probe는 레포 밖에 둔다.

```text
<local-artifacts-root>/<YYYYMMDD-HHMM>-<short-task>/
- logs/
- screenshots/
- traces/
- temp/
- notes.md
```

공유가 필요한 결과만 `docs/`에 요약한다.

## 미결정

- 씬/프리팹 잠금 단위와 통합 리듬을 실제 작업 흐름에 맞춰 어디까지 세분화할지.
- Host Mode에서 Server Mode로 전환하는 정확한 게이트. 현재 임시 기준은 1차 시연까지 Host Mode, 2차 릴리스에서 Server Mode 전환이다.
- Corgi 레벨 도구를 실제 씬 제작에 어느 범위까지 사용할지.

## 변경 기록

- 2026-05-07: A303-282 기준으로 `Colosseum.DevMode`, `Colosseum.StatusEffects`, `Colosseum.Game`의 현재 asmdef 경계를 보강했다. `NetworkGameplay`만 Fusion Weaver/unsafe 예외를 유지하고, DevMode와 상태이상 SO 구현은 `allowUnsafeCode=false` 경계로 둔다.
- 2026-05-02: A303-261 기준으로 `Scripts/Status/` 폴더를 신설했다. 상태이상 시스템 인프라(`StatusEffectDefinition` SO + `StatusEffectInstance` INetworkStruct + `PlayerStatusController` NetworkBehaviour)와 효과별 SO 클래스(`Effects/`)를 둔다. Domain 레이어에 속하며, 새 상태이상 카드 추가 시 SO 1개만 만들면 되는 OCP 구조다. 상세는 [06_Status_Effect_시스템.md](06_Status_Effect_시스템.md).
- 2026-04-30: A303-256 기준으로 `Contracts`, `Presentation`, `Data/Presentation`의 책임 경계와 이벤트 계약 원칙을 추가했다.
- 2026-04-17: 인프라 담당 기술 결정 v1.1 기준을 반영했다. Unity 2022.3 LTS 계열 필수, Corgi 제한 활용, Fusion Host Mode 1차 시연, Server Mode/WebGL/리더보드 2차 릴리스 구조를 명시했다.
