---
last_updated: 2026-05-08
audience: ai-first
---

# Fusion 동일 PC 2인 리허설

이 문서는 동일 PC 2인 전투가 단순 오프라인 조작이 아니라 Photon Fusion 구조 위에서 성립하는지 확인하는 기준을 정의한다.

구체적인 실행 명령, Unity 설치 경로, 로그 저장 절대경로는 이 문서에 고정하지 않는다. 각 작업자는 구현 레포와 로컬 환경에 맞는 실행 경로를 작업 브리핑에 적는다.

현재 프로젝트는 Onboarding / Lobby / Worker / API의 원격 EC2 분리 배포 프로토타입도 병행하고 있다. 다만 이 문서는 제목 그대로 "동일 PC 2인" 기준을 다루며, 원격 분리 배포 smoke는 `01_스모크테스트.md`의 인프라 항목으로 별도 기록한다.

## 목적

MVP가 나중에 네트워크 구조로 갈아엎히지 않도록, 초기 playable slice부터 입력, 이동, 발사, 피격, 사망, 구간 돌파가 Fusion 권위 구조를 통과하는지 확인한다.

| 검증 목적 | 의미 |
|---|---|
| 입력 경계 | 두 플레이어 입력이 INetworkInput 경로를 타는가 |
| 권위 경계 | 결과 상태가 StateAuthority에서 확정되는가 |
| 동기화 대상 | 위치, 체력, 탄약, 사망, 돌파가 truth로 관리되는가 |
| presentation 분리 | VFX/SFX/UI가 결과 상태를 소유하지 않는가 |
| 회귀 방지 | 오프라인 임시 구현이 네트워크 전환을 막지 않는가 |

## 리허설 전제

- Unity 프로젝트는 팀 확정 기술스택을 따른다.
- Host Mode 기준으로 시작하되 Server Authoritative 전환을 막지 않는 구조여야 한다.
- 플레이어 물리는 `Rigidbody2D`, `Collider2D`, `NetworkRigidbody2D` 기준을 따른다.
- 이동/점프/공격/피격은 `FixedUpdateNetwork()` 흐름에서 결과가 정리되어야 한다.
- raw 로그, 스크린샷, 영상은 레포 밖 artifact 위치에 둔다.

## 로비 경로 실행 전제

방 목록/방 생성/방 참가를 포함한 동일 PC 2인 리허설은 Unity exe만으로 성립하지 않는다. MR !65 / A303-254 이후 로비 경로는 Spring Room API와 Redis를 포함한 control plane을 전제로 한다.

`50/06_매칭_로비_기준.md`는 아직 candidate이므로 로비 아키텍처의 accepted 승격을 의미하지 않는다. 다만 이 문서에서 "Room Registry 기반 동일 PC 2인 리허설"을 실행한다고 선언한 경우, 아래 전제와 pass/fail 규칙은 mandatory gate로 적용한다.

| 구성요소 | 기본값 / 확인 기준 |
|---|---|
| Redis | `localhost:6379` 기동. Spring Room API가 이 Redis에 연결되어야 함 |
| Spring Boot Room API | `http://127.0.0.1:8081/api/v1` 기준으로 `GET/POST/PATCH/DELETE /rooms`가 가능해야 함 |
| Worker Agent | `http://127.0.0.1:8080`에서 lobby의 spawn 요청을 받아야 함 |
| Worker Agent -> Room API | `--room-api-base-url http://127.0.0.1:8081/api/v1` 또는 `COLOSSEUM_ROOM_API_BASE_URL`로 같은 Room API를 바라봐야 함 |
| Unity Lobby/GameServer | `COLOSSEUM_WORKER_BASE_URL=http://127.0.0.1:8080`, `COLOSSEUM_ROOM_API_BASE_URL=http://127.0.0.1:8081/api/v1` 기준을 공유해야 함 |

이 전제가 빠지면 `ROOM_CREATE_FAILED`, 방 목록 비어 있음, stale room, spawn 실패가 발생할 수 있다. 이 경우 Fusion 전투/입력 회귀로 분류하지 말고 Room Registry 또는 worker-agent 실행 전제 실패로 먼저 분류한다.

### 실행 순서 체크리스트

1. Redis가 `localhost:6379` 또는 작업 브리핑의 지정 포트에서 기동 중인지 확인한다.
2. Spring Room API를 띄우고 `http://127.0.0.1:8081/api/v1` 기준 Room Registry `GET/POST/PATCH/DELETE /rooms` CRUD smoke를 확인한다. health-only 확인은 Room Registry 기반 리허설 통과 증빙으로 충분하지 않다.
3. Worker Agent를 `http://127.0.0.1:8080` 기준으로 띄우고 `--room-api-base-url` 또는 `COLOSSEUM_ROOM_API_BASE_URL`이 같은 Room API를 가리키는지 확인한다.
4. Lobby/GameServer 실행 환경에 `COLOSSEUM_WORKER_BASE_URL`과 `COLOSSEUM_ROOM_API_BASE_URL`이 같은 기준으로 들어갔는지 확인한다.
5. 그 다음 동일 PC 2인 리허설을 실행하고, 방 생성/방 목록/방 참가 실패를 전투 회귀와 분리해 기록한다.

각 단계는 리허설 리포트에 PASS/FAIL/BLOCKED와 증빙 위치를 남긴다. Redis, Spring Room API, Worker Agent, Room API base URL, Unity env 정렬 중 하나라도 확인되지 않으면 로비 경로 리허설은 prerequisite-fail 또는 rehearsal-stop으로 기록하고 통과로 보고하지 않는다.

Room Registry가 사용할 수 없고 fallback/no-go 결정이 없으면 방 생성/방 목록 기반 리허설은 BLOCKED다. 이 경우 DevMode, LAN Host Mode, 수동 room 재시작 같은 다른 경로를 별도로 확인할 수는 있지만, Room Registry 기반 동일 PC 2인 리허설 통과 증빙으로 대체하지 않는다.

원격 EC2 환경에서 동일한 로비 흐름을 검증했더라도, 이 문서의 pass/fail은 동일 PC 기준 prerequisite와 실행 결과를 별도로 남긴다. 즉 원격 분리 배포 성공은 이 문서의 로컬 baseline evidence를 자동 대체하지 않는다.

## 최소 시나리오

| no | 시나리오 | 통과 기준 |
|---|---|---|
| 1 | 방 생성/참가 | Host와 두 플레이어 객체가 생성됨 |
| 2 | 양쪽 이동/점프 | 각 플레이어 입력이 분리되어 반영됨 |
| 3 | 기본 공격 | StateAuthority가 투사체 또는 발사 결과를 생성 |
| 4 | 피격/넉백 | 체력/넉백/경직 상태가 truth로 반영됨 |
| 5 | 일반 사망 처리 | `DeathResolving -> RespawnResolving`으로 전이하고 `ChoicePending`은 열리지 않음 |
| 6 | 구간 돌파 | `BreakthroughResolving`에서 돌파자/방어자 결과가 상태머신에 반영 |
| 7 | 구간 돌파 패배 카드 선택 | 방어자만 `ChoicePending`에 진입하고 선택 결과가 다음 라운드 상태에 반영 |
| 8 | 리스폰 | 위치, 자원, 카메라가 재정렬됨 |

## 자동화 smoke 기준

동일 PC 2인 검증은 수동 리허설만으로 끝내지 않고, 변경 영향도에 따라 자동화 smoke를 병행한다. 실행 명령과 작업자별 절대경로는 이 문서에 고정하지 않고 작업 브리핑이나 운영 표준문서에 둔다.

| 범위 | 통과 기준 |
|---|---|
| game screen 진입 | worker-agent, lobby server, client 2개가 기동되고 두 클라이언트가 같은 room의 game screen에 진입 |
| spawn | 양쪽 플레이어 NetworkObject가 생성되고 조작 가능한 상태가 됨 |
| 이동/점프 | 각 클라이언트 입력이 서로 독립적으로 반영됨 |
| 발사/피격 | StateAuthority 기준 발사, 충돌, 체력 변화가 로그와 화면 상태로 확인됨 |
| 구간 전환 | 상대 진영 진입 또는 동등한 라운드 전환 조건으로 `ChoicePending`이 열림 |
| 카드 선택 | 카드 선택 UI 진입, 선택, 확정 후 다음 전투 복귀가 확인됨 |
| game-over | 최종 승리 조건에서 양쪽 클라이언트 결과 UI가 표시됨 |

Fusion/asmdef/DevMode/scene wiring/카드 선택/room transition/game-over 경계를 건드린 브랜치는 `Build Both`만으로 push-ready로 보지 않는다. Editor import/compile, Editor DevMode smoke, 2클라 gameplay smoke, game-over 회귀를 분리해서 본다.

## 관찰 체크리스트

| 항목 | 관찰 질문 |
|---|---|
| 입력 | 두 플레이어 입력이 서로 덮어쓰지 않는가 |
| 이동 | 클라이언트 예측과 서버 결과 보정이 깨지지 않는가 |
| 발사 | 발사 가능 조건과 탄 생성이 StateAuthority 기준인가 |
| 피격 | 맞은 것처럼 보이는 결과와 실제 서버 결과가 정리되는가 |
| UI | 카드 선택 UI가 조준/공격 입력과 충돌하지 않는가 |
| 로그 | 사망 원인, 돌파 원인, 사용 프리셋을 추적할 수 있는가 |
| 회귀 | 3D 물리 컴포넌트나 오프라인 임시 우회가 생기지 않았는가 |

## 증빙 기준

- 실행한 Unity 테스트 종류를 남긴다.
- 수동 리허설 여부와 관찰 결과를 남긴다.
- 사용한 프리셋 이름을 남긴다.
- 실패 시 재현 절차와 화면/로그 증빙 위치를 남긴다.
- raw 파일은 레포 밖 artifact 위치에 두고, docs에는 요약만 남긴다.

## BLOCKED 기준

아래 항목은 단순 실패가 아니라 구현 방향 재검토가 필요한 BLOCKED로 본다.

- 이동/공격 결과가 Fusion 입력 경로를 거치지 않는다.
- 피격, 사망, 구간 돌파가 local-only로만 처리된다.
- 신규 3D Rigidbody, 3D Collider, ConfigurableJoint가 전투 구현에 추가된다.
- 카드 선택 UI가 전투 입력과 구조적으로 분리되지 않는다.
- Host Mode에서는 되지만 Server Mode 전환을 막는 전제가 생긴다.
- `Fusion.NetworkInputUtils.GetTypeKey` 또는 `NetworkInputData` type key 오류가 발생한다.
- Fusion Weaver 대상 assembly 누락이나 `FieldAccessException: Fusion.NetworkBehaviour:Ptr is inaccessible`로 non-DevMode gameplay path가 실패한다.

## 리포트 양식

```text
리허설 이름:
실행 날짜:
Unity/Fusion 환경:
사용 프리셋:
실행 범위:
통과 시나리오:
실패 시나리오:
관찰한 리스크:
raw artifact 위치:
다음 조치:
```

## 미결정

- automated PlayMode 테스트로 어느 시나리오까지 고정할지.
- 동일 PC 2인 입력을 키보드 분할로 할지, 패드/멀티 인스턴스로 할지.
- 네트워크 지연 시뮬레이션을 언제 리허설에 포함할지.
