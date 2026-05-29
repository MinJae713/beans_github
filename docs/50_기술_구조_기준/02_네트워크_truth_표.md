---
last_updated: 2026-05-18
audience: ai-first
---

# 네트워크 truth 표

이 문서는 Photon Fusion 구조에서 어떤 정보가 네트워크 truth인지, 어떤 정보가 로컬 presentation인지 구분한다.

Photon App ID, 개인 환경 설정, secret, 로컬 절대경로는 이 문서에 기록하지 않는다.

## 기본 기준

- 게임 결과를 바꾸는 상태는 네트워크 truth다.
- 입력은 클라이언트가 제출하지만 결과는 authoritative tick에서 확정한다.
- 위치, 체력, 탄약, 카드 상태, 사망, 리스폰, 구간 돌파는 네트워크 truth다.
- 카메라 흔들림, 사운드, 파티클, UI 강조, 히트스톱 연출은 presentation이다.
- presentation은 truth를 읽어 표현할 수 있지만 truth를 소유하지 않는다.
- Room Registry, Worker Agent process table, Redis 방 목록은 Fusion gameplay truth가 아니라 control plane / service state다.

## Truth 표

| state/event | source authority | Fusion 표현 | tick phase | presentation fallback |
|---|---|---|---|---|
| 플레이어 입력 | InputAuthority | INetworkInput | GetInput | 입력 UI 표시 |
| 이동/점프 결과 | StateAuthority | NetworkRigidbody2D | FixedUpdateNetwork | 보간, 애니메이션 |
| 발사 요청 | InputAuthority 제출, StateAuthority 검증 | input flag/event | FixedUpdateNetwork | 발사 예측 이펙트 |
| 투사체 생성 | StateAuthority | NetworkObject | FixedUpdateNetwork | muzzle flash |
| 투사체 위치 | StateAuthority | NetworkObject/Networked | FixedUpdateNetwork | trail, interpolation |
| 투사체 단발 연출 사건 | StateAuthority | owner player/weapon RPC relay + projectile identity payload | FixedUpdateNetwork | impact/explosion VFX/SFX |
| 플레이어 최초 스폰 연출 사건 | StateAuthority | PlayerHealth Spawned -> RPC/event payload | Spawned 이후 presentation | `player_spawn` VFX |
| 반사 판정 결과 | StateAuthority | Networked event 또는 Networked state | FixedUpdateNetwork | 반사 트레일, 충돌 이펙트 |
| 반사 횟수(ReflectCount) | StateAuthority | `[Networked] byte ReflectCount` | FixedUpdateNetwork | 없음 (게임 결과에 영향) |
| 반사 후 투사체 방향 | StateAuthority | `NetworkObject` 위치/속도 갱신 | FixedUpdateNetwork | trail interpolation |
| 반사 피격 결과 | StateAuthority | Networked state 또는 event | FixedUpdateNetwork | hit VFX/SFX |
| 반사 경로 가독성 신호 | Local | local-only | presentation | 반사 예고선, 트레일 색상 |
| 탄약/재장전 상태 | StateAuthority | Networked | FixedUpdateNetwork | HUD 표시 |
| 체력 | StateAuthority | Networked | FixedUpdateNetwork | HP bar, damage flash |
| 피격 결과 | StateAuthority | Networked state 또는 event | FixedUpdateNetwork | hit VFX/SFX |
| 넉백/경직 상태 | StateAuthority | Networked | FixedUpdateNetwork | hit stop, camera shake |
| 상태이상 활성 인스턴스 (Burn/Poison/Frost/Electric 등) | StateAuthority | `[Networked, Capacity(8)] NetworkArray<StatusEffectInstance>` (PlayerStatusController, A303-261) | FixedUpdateNetwork (만료/DoT tick) | 색 tint, 파티클 (`IVisualTintEffect.GetVisualTint`) |
| 카드 효과 VFX 사건 payload 후보 | StateAuthority 결과를 관측한 emit point | `Contracts` DTO 또는 동등한 presentation event 후보. 게임 결과 state 아님 | FixedUpdateNetwork 결과 이후 또는 presentation queue 후보 | VFX manager, toast, marker, particle |
| VFX 중복 재생 방지 key 후보 | Local presentation cache | Networked state 아님. event id/tick/object id/sequence 조합 후보 | presentation | rollback/resimulation 중복 재생 방지 |
| 사망 상태 | StateAuthority | Networked | FixedUpdateNetwork | death animation |
| 카메라/경계 이탈 사망 | StateAuthority | `PlayerHealth` + `RoomManager` viewport 판정 | FixedUpdateNetwork | 좌우 카메라 이탈은 스폰/리스폰 후 3초 보호, 하단 낙사는 보호 없이 즉시 사망 |
| 구간 돌파 trigger | StateAuthority | `SegmentBoundary` collider `OnTriggerEnter` (Q40 D44) | FixedUpdateNetwork | 없음 (게임 결과) |
| 구간 돌파 상태 | StateAuthority | Networked | FixedUpdateNetwork | 전선 이동 연출 |
| 카드/선택 결과 | StateAuthority | Networked | state transition | 선택 UI |
| 리스폰 상태 | StateAuthority | Networked | state transition | spawn VFX |
| 게임오버 후 방 리셋 | Dedicated game server | `GameServerManager.ResetRoom()` + `RoomManager.ResetGame()` | gameover timer 만료 후 서버가 모든 player NetworkObject despawn 및 ready 상태 초기화 | GameOverUI, 재경기 대기실 |
| 카메라 흔들림 | Local | local-only | presentation | 없음 |
| 사운드/파티클 | Local | local-only | presentation | 없음 |
| 방 생성 요청 | Lobby / Worker Agent control plane | HTTP `POST /workers/spawn-room` | process orchestration | UI notice |
| Room Registry 단건 저장 | Worker Agent -> Spring Boot | HTTP `POST /api/v1/rooms` | control plane | 없음 |
| Room Registry 상태 변경 | Worker Agent stdout 파싱 -> Spring Boot | HTTP `PATCH /api/v1/rooms` | control plane | 방 목록 상태 라벨 |
| Room Registry 삭제 | Worker Agent process exit -> Spring Boot | HTTP `DELETE /api/v1/rooms/{sessionName}` | control plane | 방 목록에서 제거 |
| 방 목록 조회 | Lobby -> Spring Boot Room API | HTTP `GET /api/v1/rooms` | control plane | Find Room UI |

## Control Plane / Service State 기준

다음 상태는 Fusion authoritative gameplay state와 구분한다.

| 항목 | 기준 |
|---|---|
| `runningRooms` (Worker Agent) | 해당 Worker 인스턴스의 로컬 프로세스 관리 테이블 |
| Redis Room Registry | 전체 Worker를 통합한 중앙 방 메타데이터 정본 |
| Lobby의 방 목록 JSON | Spring Room API 결과를 클라이언트 표시용으로 변환한 projection |

이 값들은 gameplay victory, damage, card result를 직접 결정하지 않는다. 대신 "어떤 방 프로세스가 어디서 돌고 있는가", "현재 로비에 어떤 방이 보여야 하는가"를 위한 service/control plane 상태다.

## Fusion 처리 원칙

- 입력 수집은 `INetworkInput`과 `GetInput()` 패턴을 따른다.
- 이동, 공격, 재장전, 피격 결과는 `FixedUpdateNetwork()` 안에서 처리한다.
- 플레이어 물리는 `Rigidbody2D`, `Collider2D`, `NetworkRigidbody2D` 기준을 따른다.
- 카드 선택처럼 전투 흐름을 명시적으로 멈추는 상태는 양 플레이어 이동을 억제할 수 있다. 단, 한 플레이어가 사망해 리스폰을 기다리는 동안에는 사망자만 `IsDead` 경로로 멈추고 생존 플레이어의 이동 입력은 계속 처리한다.
- 원격 player proxy는 로컬 Fusion physics simulation 대상에서 제외해 `NetworkRigidbody2D`의 snapshot interpolation을 타게 한다. InputAuthority 또는 StateAuthority가 있는 player만 simulation에 넣고, `Player.prefab`의 `Rigidbody2D.interpolation`은 켜서 권한/예측 플레이어의 render frame도 부드럽게 보정한다.
- 이동 주도권 카메라는 StateAuthority가 `[Networked]` target X를 확정한다. 단, 로컬 클라이언트가 현재 movement camera authority인 경우에는 같은 networked target을 기준점으로 두고 자기 predicted transform X를 사용해 presentation camera target만 보강할 수 있다. 이 보강은 경계 판정 truth를 바꾸지 않는다.
- 일반 사망 리스폰은 StateAuthority가 리스폰 위치 계산 전에 생존 주도권자를 이동방향 기준 15% inset 지점에 맞추는 camera target X를 먼저 확정하고, 사망자는 그 camera target의 반대편 85% 지점에서 안전 발판 보정을 거쳐 부활시킨다. 화면 좌표 기준 오른쪽 진행은 주도권자 15%/사망자 85%, 왼쪽 진행은 주도권자 85%/사망자 15%다.
- Host/StateAuthority가 원격 플레이어 입력을 일시적으로 놓친 경우에는 이동 입력만 3 tick 이하로 짧게 유지할 수 있다. 점프, 발사, 이모지 같은 단발 입력은 재발행하지 않는다.
- 원격 projectile proxy도 로컬 physics simulation 대상에서 제외해 `NetworkRigidbody2D` snapshot interpolation을 타게 한다. InputAuthority 또는 StateAuthority가 있는 projectile만 simulation에 넣고, `Bullet.prefab`의 `Rigidbody2D.interpolation`은 켠다.
- 투사체 판정 root와 sprite/trail child는 authoritative projectile root 기준을 유지한다. 생성 직후 sprite/trail을 muzzle 위치로 되감아 catch-up시키지 않는다. 총구보다 조금 앞에서 등장하는 원격 시각 오차는 허용하고, collider, damage, hit timing, 반사 궤적은 root가 정본이다.
- Dedicated game server의 `GAME_OVER`, `ROOM_RESET`, `READY_UPDATE` 같은 reliable room control 메시지는 ready dictionary 하나에만 의존하지 않고 ready/nickname/spawned player registry의 합집합으로 보낸다. `OnPlayerJoined`는 이미 도착한 READY 상태를 false로 덮어쓰지 않는다.
- 신규 3D Rigidbody, 3D Collider, ConfigurableJoint는 금지한다.
- Host Mode MVP에서도 Server Authoritative 전환을 막는 구조를 만들지 않는다.

## RPC와 이벤트 사용 기준

| 구분 | 기준 |
|---|---|
| Networked state | 지속 상태, 재접속/보정에 필요한 값 |
| input flag | 매 tick 제출되는 조작 의도 |
| RPC/event | 단발성 연출 트리거 또는 상태 전이 알림 |
| local-only | 게임 결과와 무관한 화면/소리/카메라 효과 |

단발성 이벤트라도 게임 결과를 바꾸면 local-only가 아니다. 예를 들어 피격, 사망, 구간 돌파는 이벤트처럼 보여도 authoritative 결과 상태로 남아야 한다.

## Prediction/Reconciliation 기준

- 이동과 발사는 예측될 수 있다.
- 사망, 구간 돌파, 카드 선택 결과는 authoritative 결과로 보정한다.
- 예측 실패가 발생해도 UI와 VFX를 부드럽게 보정할 수는 있지만 결과를 바꾸면 안 된다.
- 클라이언트가 봤을 때 맞은 것처럼 보였지만 서버가 무효 처리할 수 있다. 이 경우 피드백은 서버 결과를 따라 정정한다.
- 카드 효과 VFX는 predicted/confirmed/corrected/rejected 상태를 구분할 수 있으나, 같은 전투 사건을 rollback/resimulation으로 반복 재생하면 안 된다.

## 카드 VFX event 후보 기준

카드 효과 VFX 계약 후보는 [45_피드백_UI_연출/03_카드_VFX_이벤트_계약.md](../45_피드백_UI_연출/03_카드_VFX_이벤트_계약.md)를 참고한다. 해당 문서의 payload sample은 pseudo-code이며, 실제 Contracts API 확정은 후속 구현 이슈에서 처리한다.

- `NetworkGameplay`은 전투 결과와 emit point를 제공하고, presentation prefab이나 particle asset을 직접 참조하지 않는다.
- `Contracts` payload 후보는 card/effect id, source tick, source object id, event sequence, 위치/방향/강도 hint를 담을 수 있다.
- VFX manager 후보는 payload를 소비해 local-only 효과를 재생하며, 전투 state를 변경하지 않는다.
- 일회성 burst VFX는 payload world position을 기준으로 재생하고, 상태형/지속형/card overhead VFX는 player slot hint로 source/target transform을 찾아 attach point와 preset offset을 더해 위치를 갱신한다. Transform 참조는 payload에 직접 넣지 않는다.
- `ProjectileReflected/projectile_reflect` VFX radius는 전투 판정이나 탄환 `SizeMultiplier`가 아니라 owner 캐릭터 collider 크기에서 파생한 presentation hint다. 반사 횟수와 반사 방향은 StateAuthority 결과지만, 반사 spark의 표시 크기는 로컬 연출 전용이다.
- 카메라 edge trigger는 일반탄 소멸 조건이 아니다. `BulletServerSimulation`은 반사 횟수가 남은 탄환만 카메라 상/좌/우 경계에서 StateAuthority 기준으로 velocity를 반사하고, 하단 경계·일반탄·반사 소진 탄환은 카메라 경계를 통과시킨다. 카메라 edge 반사 후에는 trigger collider의 화면 안쪽 표면 밖으로 탄환을 밀어내 같은 edge에서 bounce가 연속 소모되지 않게 한다. 탄환 소멸은 lifetime, 실제 월드 충돌, 피격/폭발 정책이 결정한다.
- `PlayerSpawn/player_spawn`은 기존 `VfxEventType` 값을 흔들지 않기 위해 enum tail에 추가하고, `PlayerHealth.Spawned()`의 StateAuthority 경로에서 발행한다.
- `PlayerJumpStarted/player_jump_started`와 `PlayerLanded/player_landed`는 기존 `VfxEventType` 값을 흔들지 않기 위해 enum tail에 추가하고, `PlayerController`/`PlayerMovement`의 StateAuthority 확정 경로에서 `CharacterFeet` 기준으로 발행한다. 점프는 아래 방향 `MagicMuzzleFire` VFX를 표시하고, 착지는 이벤트 계약만 유지하되 현재 profile에서는 표시하지 않는다.
- `VictoryZoneReached/victory_zone_confetti`는 기존 `VfxEventType` 값을 흔들지 않기 위해 enum tail에 추가하고, 마지막 구간 진입으로 `VictoryRun`이 시작된 뒤 `RoomManager`의 StateAuthority 경로에서 한 번 발행한다. payload radius는 room width, magnitude는 camera height이며 presentation은 prefab을 camera height 기준으로 균일 scale한 뒤 상단 끝을 카메라 상단 끝에 맞추고 X축으로만 타일링한다. 빅토리런 중에는 로비/리셋 전까지 유지되는 local-only 연출이다.
- `OverchargeReady/reload_overcharge`처럼 `WeaponMuzzle`에 붙는 상태형 VFX는 player slot으로 source player를 찾은 뒤 해당 player의 `Gun` muzzle 위치를 따라간다.
- `StatusTick` 같은 반복 사건은 VFX를 매 tick 새로 생성하지 않고 상태 시작/종료 또는 낮은 빈도 UI 갱신으로 제한한다.
- dedupe cache는 라운드 종료, player despawn, scene transition 시 clear한다.

## 구현 브리핑에 반드시 적을 항목

네트워크 관련 작업 브리핑에는 아래 항목을 명시한다.

| 항목 | 예시 |
|---|---|
| source authority | InputAuthority / StateAuthority / Local |
| Fusion 표현 | Networked / INetworkInput / RPC/event / local-only |
| tick phase | GetInput / FixedUpdateNetwork / presentation |
| 보정 기대값 | prediction 가능 / 서버 결과 우선 / 로컬 연출 전용 |
| 테스트 관찰점 | 위치, 체력, 탄약, 사망, 돌파, 카드 결과 |

## 금지

- 게임 결과 상태를 UI나 VFX 오브젝트에 저장하는 방식.
- 피격, 사망, 돌파를 local-only 이벤트로만 처리하는 방식.
- VFX event나 toast가 카드 효과, 데미지, 상태이상 지속 시간, 라운드 결과를 계산하거나 확정하는 방식.
- Photon App ID나 개인 환경값의 실제 값을 기준문서, MR 본문, tracked 설정 파일에 적는 방식.
- Host Mode에서만 성립하고 Server Mode 전환을 막는 구조.

## 미결정

- 투사체를 항상 NetworkObject로 둘지, 일부를 tick 기반 예측 객체로 둘지.
- 폭발탄 같은 단발 이벤트의 최소 Networked 상태 범위 (반사탄은 확정).
- 관전 모드 도입 시 presentation-only 정보와 replay truth를 어떻게 나눌지.

> Q5/Q38 반사탄 drift는 Option A 채택으로 종결 (2026-04-20, [90/06 아카이브](../99_아카이브/2026-04-20_반사탄_drift_Option_A_채택.md))
