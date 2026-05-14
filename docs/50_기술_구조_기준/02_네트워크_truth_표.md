---
last_updated: 2026-05-08
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
| 구간 돌파 trigger | StateAuthority | `SegmentBoundary` collider `OnTriggerEnter` (Q40 D44) | FixedUpdateNetwork | 없음 (게임 결과) |
| 구간 돌파 상태 | StateAuthority | Networked | FixedUpdateNetwork | 전선 이동 연출 |
| 카드/선택 결과 | StateAuthority | Networked | state transition | 선택 UI |
| 리스폰 상태 | StateAuthority | Networked | state transition | spawn VFX |
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
