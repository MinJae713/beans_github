# 네트워크 truth 표

이 문서는 Photon Fusion 구조에서 어떤 정보가 네트워크 truth인지, 어떤 정보가 로컬 presentation인지 구분한다.

Photon App ID, 개인 환경 설정, secret, 로컬 절대경로는 이 문서에 기록하지 않는다.

## 기본 기준

- 게임 결과를 바꾸는 상태는 네트워크 truth다.
- 입력은 클라이언트가 제출하지만 결과는 authoritative tick에서 확정한다.
- 위치, 체력, 탄약, 카드 상태, 사망, 리스폰, 구간 돌파는 네트워크 truth다.
- 카메라 흔들림, 사운드, 파티클, UI 강조, 히트스톱 연출은 presentation이다.
- presentation은 truth를 읽어 표현할 수 있지만 truth를 소유하지 않는다.

## Truth 표

| state/event | source authority | Fusion 표현 | tick phase | presentation fallback |
|---|---|---|---|---|
| 플레이어 입력 | InputAuthority | INetworkInput | GetInput | 입력 UI 표시 |
| 이동/점프 결과 | StateAuthority | NetworkRigidbody2D | FixedUpdateNetwork | 보간, 애니메이션 |
| 발사 요청 | InputAuthority 제출, StateAuthority 검증 | input flag/event | FixedUpdateNetwork | 발사 예측 이펙트 |
| 투사체 생성 | StateAuthority | NetworkObject | FixedUpdateNetwork | muzzle flash |
| 투사체 위치 | StateAuthority | NetworkObject/Networked | FixedUpdateNetwork | trail, interpolation |
| 탄약/재장전 상태 | StateAuthority | Networked | FixedUpdateNetwork | HUD 표시 |
| 체력 | StateAuthority | Networked | FixedUpdateNetwork | HP bar, damage flash |
| 피격 결과 | StateAuthority | Networked state 또는 event | FixedUpdateNetwork | hit VFX/SFX |
| 넉백/경직 상태 | StateAuthority | Networked | FixedUpdateNetwork | hit stop, camera shake |
| 사망 상태 | StateAuthority | Networked | FixedUpdateNetwork | death animation |
| 구간 돌파 상태 | StateAuthority | Networked | FixedUpdateNetwork | 전선 이동 연출 |
| 카드/선택 결과 | StateAuthority | Networked | state transition | 선택 UI |
| 리스폰 상태 | StateAuthority | Networked | state transition | spawn VFX |
| 카메라 흔들림 | Local | local-only | presentation | 없음 |
| 사운드/파티클 | Local | local-only | presentation | 없음 |

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
- Photon App ID나 개인 환경값의 실제 값을 기준문서, MR 본문, tracked 설정 파일에 적는 방식.
- Host Mode에서만 성립하고 Server Mode 전환을 막는 구조.

## 미결정

- 투사체를 항상 NetworkObject로 둘지, 일부를 tick 기반 예측 객체로 둘지.
- 반사탄/폭발탄 같은 단발 이벤트의 최소 Networked 상태 범위.
- 관전 모드 도입 시 presentation-only 정보와 replay truth를 어떻게 나눌지.
