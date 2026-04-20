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

## Host Mode 임시동작 금지선

1차 시연은 Host Mode를 사용할 수 있지만, Host Mode에서만 우연히 맞는 구조를 규범으로 만들지 않는다.

| 항목 | 기준 |
|---|---|
| 허용 | Host Mode로 로컬 2인/온라인 1v1을 먼저 검증 |
| 금지 | Host 클라이언트 UI가 곧바로 authoritative 상태를 바꾸는 구조 |
| 금지 | StateAuthority 검증 없이 카드/피격/리스폰 결과를 확정 |
| 기록 | Server Mode 전환 시 바꿔야 하는 임시 브리지는 리스크로 남김 |

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

## 미결정

- 프로토타입에 남은 전역 탐색을 어느 마일스톤에서 제거할지.
- 카드 선택 요청을 RPC로 둘지, 별도 Networked request state로 둘지.
- Server Mode 전환 전까지 허용할 Host Mode bridge의 최대 범위.
