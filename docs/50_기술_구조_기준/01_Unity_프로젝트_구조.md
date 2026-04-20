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
| 초기 모드 | Host Mode |
| 이후 목표 | 2차 릴리스에서 Server Mode |
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

## 서빙/릴리스 구조

기술 기준은 아래 2단계 릴리스 구조를 따른다.

| 단계 | 기준 |
|---|---|
| 1차 시연 | Windows Standalone + Photon Cloud Host Mode |
| 1차 매칭 | 방 코드 공유 또는 최소 세션 입장 |
| 1차 데이터 | 로컬 CSV 또는 로컬 로그 수준 |
| 1차 폴백 | Photon Cloud 온라인 -> LAN Host Mode -> 로컬 2P |
| 2차 공개 베타 | Server Mode + WebGL 빌드 + 웹 리더보드 후보 |
| 2차 조건 | Server Mode 안정화와 WebGL WebSocket 리허설 통과 |

WebGL은 1차 시연의 공식 목표가 아니다. 브라우저/WebSocket/탭 비활성화/Host 불안정성을 검증하기 전에는 2차 릴리스 범위로 둔다.

## WebGL/리더보드 범위

- WebGL 빌드는 빌드 파이프라인 확인 정도는 가능하지만, 1차 시연의 주 경로로 삼지 않는다.
- 공개 베타에서 WebGL을 지원하려면 Server Mode가 먼저 안정되어야 한다.
- 리더보드와 영속 데이터는 1차에서 제외한다.
- 1차에서 필요한 기록은 플레이테스트와 디버깅을 위한 로컬 로그로 제한한다.

## 레이어 구분

작업 브리핑에서는 수정 범위를 아래 네 레이어 중 하나 이상으로 명시한다.

| 레이어 | 역할 |
|---|---|
| Domain | 순수 게임 규칙, 카드 효과, 전투 수치, 승패 판정 |
| Presentation | Unity 씬, 프리팹, 렌더링, VFX, HUD |
| Input | 키 매핑, 입력 수집, 플레이어 조작 |
| Config | ScriptableObject, 밸런스 값, 에셋 참조 |

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
- 사망 연출, 카메라 셰이크, 파티클, UI 피드백은 네트워크 truth가 아니다.
- 시각 효과는 로컬 presentation에서 보정한다.

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

- 씬/프리팹 소유권을 팀원별로 어떻게 나눌지.
- Host Mode에서 Server Mode로 전환하는 정확한 게이트. 현재 임시 기준은 1차 시연까지 Host Mode, 2차 릴리스에서 Server Mode 전환이다.
- Corgi 레벨 도구를 실제 씬 제작에 어느 범위까지 사용할지.

## 변경 기록

- 2026-04-17: 인프라 담당 기술 결정 v1.1 기준을 반영했다. Unity 2022.3 LTS 계열 필수, Corgi 제한 활용, Fusion Host Mode 1차 시연, Server Mode/WebGL/리더보드 2차 릴리스 구조를 명시했다.
