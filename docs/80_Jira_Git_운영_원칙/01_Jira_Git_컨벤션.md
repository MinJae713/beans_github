---
last_updated: 2026-05-08
audience: ai-first
---

# Colosseum 프로젝트 구조 가이드 (Jira/Git 컨벤션 v3.0)

> 문서 상태: accepted_candidate (2026-04-21 팀회의 T6 A안 승인, D55 등록)
> 버전: v3.0 — 팀원 박병준 작성 `PROJECT_STRUCTURE_GUIDE.md`를 정본으로 채택 + Jun v2.1 보완 6개 + 권장 1개 병합
> 변경 이력: 2026-04-22 §17.5 이슈 타입 정책 + §17.6 Fix Version 정책 추가 (Jun 보완, 11번 백로그 인벤토리 결정 이관). 2026-04-27 §16.5 인벤토리를 별도 문서 `03_Photon_AppID_Runbook.md` 로 분리 (`S14P31A303-153` 산출물). 2026-04-27 §12.5 LFS quota 실측 + 자체 임계 정책으로 갱신 (`S14P31A303-152` 산출물, `90_백로그/03_Git_LFS_실측.md` 신설).
> binding 범위: Unity 프로젝트 구조, 폴더/네이밍, Git/Jira/MR 운영, Unity 협업 규칙. 실제 GitLab protected branch, 담당자 지시, 작업 브리핑이 우선한다.
> 실행 중단 조건: GitLab 설정 확인 없이 브랜치 생성, MR 대상 확정, protected branch push를 시도해야 하는 경우 BLOCKED로 보고한다.
> 저작: §1~§11 박병준(PROJECT_STRUCTURE_GUIDE.md 원저) · §12~§18 Jun(v2.1 보완 병합). 2026-04-21 T6 승인 후 팀 공동 관리.
> 리뷰 이력: v2.0 DRAFT 및 v2.1 후보안에 대한 3자 AI 딥리뷰 결과는 `99_아카이브/2026-04-20_Jira_Git_컨벤션_v2_딥리뷰.md`.

## 1. 아키텍처 전략

본 프로젝트는 **도메인 기반 분류 + 컴포넌트 분리** 전략을 채택합니다.

- **도메인 기반**: 폴더를 기능 영역(Player, Weapon, Card, Game, Network, UI)으로 분류
- **컴포넌트 분리**: 하나의 큰 스크립트를 역할별로 쪼개서 담당자 간 파일 충돌 방지

### 왜 이 전략인가?

Photon Fusion 2의 구조적 특성 때문입니다.

1. **Client/Server 폴더 분리 불가**: Fusion의 Client-Side Prediction 때문에 서버 코드와 클라이언트 코드가 물리적으로 동일해야 합니다. 서버에서는 "판정"으로, 클라이언트에서는 "예측"으로 같은 코드가 양쪽에서 실행됩니다.
2. **네트워크/게임플레이 분리 제한**: `[Networked]` 프로퍼티가 반드시 로직과 같은 `NetworkBehaviour` 클래스에 있어야 하므로, 네트워크 동기화 코드와 게임 로직을 완전히 분리할 수 없습니다.
3. **비주얼 분리는 가능**: 렌더링/이펙트 코드는 `[Networked]` 프로퍼티를 읽기만 하므로 별도 컴포넌트로 분리 가능합니다.

---

## 2. 폴더 구조

```
Assets/_Project/
├── Scripts/
│   ├── Player/            ← 플레이어 관련 로직
│   │   ├── PlayerController.cs    (NetworkBehaviour 라이프사이클·오케스트레이션, [Networked] 필드)
│   │   ├── PlayerMovement.cs       (이동, 점프 물리)
│   │   ├── PlayerGroundProbe.cs   (지면 판정, 플레이어 겹침 해소)
│   │   ├── PlayerInput.cs          (GetInput, 입력 처리)
│   │   ├── PlayerBoundary.cs       (방 경계 감지, 방 전환 트리거)
│   │   ├── PlayerHealth.cs         (HP, 데미지, 사망, 리스폰)
│   │   ├── PlayerDeathVisual.cs    (사망 시 스프라이트 숨김/표시)
│   │   └── PlayerVisual.cs         (스프라이트 색상, flip)
│   │
│   ├── Weapon/            ← 무기 관련 로직
│   │   ├── Gun.cs                  (발사, 탄창, 재장전, 네트워크 동기화)
│   │   └── BulletController.cs     (총알 물리, 충돌, 상태이상)
│   │
│   ├── Card/              ← 카드 시스템
│   │   ├── CardData.cs             (ScriptableObject 데이터 정의)
│   │   ├── CardDeck.cs             (드로우, 선택, RPC, 타임아웃)
│   │   └── CardEffect.cs           (카드 효과 누적 수치)
│   │
│   ├── Game/              ← 게임 규칙/상태
│   │   ├── RoomData.cs             (방 데이터 구조체)
│   │   └── RoomManager.cs          (방 진행, 승리 판정)
│   │
│   ├── Network/           ← 네트워크 인프라
│   │   ├── ServerManager.cs        (Dedicated Server 전용)
│   │   ├── NetworkManager.cs       (클라이언트 접속/세션 관리)
│   │   ├── InputProvider.cs        (입력 캡처 → Fusion 전송)
│   │   └── NetworkInputData.cs     (입력 데이터 구조체)
│   │
│   ├── Camera/            ← 카메라
│   │   └── CameraController.cs     (카메라 팔로우, 방 전환)
│   │
│   ├── UI/                ← 사용자 인터페이스
│   │   ├── MainMenuUI.cs           (메인 메뉴, 방 생성/참가)
│   │   ├── GameOverUI.cs           (대기실, 게임오버, 재경기)
│   │   ├── CardSelectionUI.cs      (카드 선택 화면)
│   │   ├── HPBarManager.cs         (HP바 관리)
│   │   ├── PlayerHPBar.cs          (개별 HP바)
│   │   ├── ReloadIndicator.cs      (재장전 표시)
│   │   └── RoomIndicatorUI.cs      (방 진행도 표시)
│   │
│   └── Editor/            ← Unity 에디터 전용
│       └── BuildHelper.cs          (빌드 자동화)
│
├── Prefabs/               ← GameObject 설계도
│   ├── Player.prefab
│   └── Bullet.prefab
│
├── Data/                  ← ScriptableObject 데이터
│   └── Cards/             (카드별 수치 설정)
│
├── Animations/            ← 애니메이션 클립/컨트롤러
├── Audio/                 ← 사운드 파일
├── Materials/             ← 머티리얼/셰이더
└── UI/                    ← UI 스프라이트/아틀라스
```

---

## 3. 역할별 담당 범위

| 담당자 | Scripts 수정 범위 | 에셋 수정 범위 |
|--------|------------------|---------------|
| 네트워크 | `Network/` 전체, `PlayerInput.cs` | 거의 없음 |
| 게임플레이 | `PlayerMovement`, `PlayerBoundary`, `PlayerGroundProbe`, `PlayerHealth`, `Gun`, `BulletController`, `CardDeck`, `CardEffect`, `RoomManager` | `Data/Cards/`, `Prefabs/` |
| 비주얼 | `PlayerVisual`, `PlayerDeathVisual`, `CameraController`, `UI/` 전체 | `Animations/`, `Audio/`, `Materials/`, `UI/` |
| 인프라 | `Editor/BuildHelper.cs` | 빌드 스크립트, `ProjectSettings/` |

### 충돌 가능 영역 (주의)

네트워크 담당자와 게임플레이 담당자는 아래 파일에서 충돌할 수 있습니다:

- `PlayerHealth.cs`: `[Networked]` 프로퍼티(네트워크) + 데미지 로직(게임플레이)
- `Gun.cs`: `[Networked]` 프로퍼티(네트워크) + 발사 로직(게임플레이)
- `CardDeck.cs`: RPC(네트워크) + 드로우 로직(게임플레이)

이 파일들은 Fusion의 구조적 제약으로 분리가 불가하므로, **Git 브랜치 전략과 코드 리뷰**로 관리합니다.

---

## 4. 파일/폴더 생성 기준

### 새 스크립트를 만들 때

**질문 1: 어느 도메인에 속하는가?**

- 플레이어가 하는 행동 → `Player/`
- 총/총알 관련 → `Weapon/`
- 카드 시스템 → `Card/`
- 게임 규칙(승리 조건, 방 진행) → `Game/`
- 서버/클라이언트 접속 → `Network/`
- 화면에 표시되는 UI → `UI/`
- 카메라 → `Camera/`
- 에디터에서만 쓰이는 도구 → `Editor/`

**질문 2: 기존 파일에 추가할 것인가, 새 파일을 만들 것인가?**

새 파일을 만드는 기준:

- 기존 파일과 **담당자가 다른** 로직을 추가할 때
- 기존 파일이 **300줄을 초과**할 때
- 기존 파일과 **독립적으로 테스트** 가능한 기능일 때

기존 파일에 추가하는 기준:

- 같은 담당자가 수정하는 로직일 때
- 기존 `[Networked]` 프로퍼티를 직접 읽고 써야 할 때
- 기존 메서드와 실행 순서가 밀접할 때 (예: Die() 직후 반드시 실행)

**질문 3: NetworkBehaviour인가, MonoBehaviour인가?**

- `[Networked]` 프로퍼티가 필요하면 → `NetworkBehaviour`
- `FixedUpdateNetwork()`에서 실행되어야 하면 → `NetworkBehaviour`
- 위 둘 다 불필요하면 → `MonoBehaviour` (GetComponent로 NetworkBehaviour 참조)

### 새 에셋 폴더를 만들 때

- 한 폴더에 파일이 **15개 이상** 쌓이면 하위 폴더로 분류
- 분류 기준은 **담당자** (누가 이 파일을 수정하는가?)
- 예: `Prefabs/Characters/`, `Prefabs/Weapons/`, `Prefabs/Environment/`

### 새 ScriptableObject를 만들 때

- 게임 밸런스 수치(데미지, 속도, 쿨타임) → `Data/` 폴더
- Inspector에서 기획자가 직접 조정할 값은 ScriptableObject로 분리
- 코드에 하드코딩하지 않음

---

## 5. 파일 수정 기준

### 수정 전 확인사항

1. **내 담당 영역의 파일인가?** → 아니면 담당자와 협의
2. **`[Networked]` 프로퍼티를 추가/변경하는가?** → 네트워크 담당자와 반드시 협의
3. **다른 컴포넌트의 public 인터페이스를 변경하는가?** → 해당 파일 담당자와 협의

### 수정 시 규칙

- `HasStateAuthority` 체크 없이 게임 상태를 변경하지 않음
- `FindObjectOfType<>()`은 `Spawned()`에서 한 번만 호출하고 캐싱
- UI 코드에서 `[Networked]` 프로퍼티를 직접 수정하지 않음 (RPC 사용)
- 서버 전용 로직에는 반드시 `if (!Object.HasStateAuthority) return;` 가드 추가

---

## 6. 파일 삭제/이동 기준

### 삭제할 때

- 해당 파일을 참조하는 모든 곳을 먼저 확인
- Prefab Inspector에서 해당 컴포넌트가 사용 중인지 확인
- 삭제 후 빌드 에러 없는지 확인
- `.meta` 파일도 함께 삭제 (Unity가 GUID로 참조하므로 .meta 누락 시 참조 끊김)

### 이동할 때

- Unity에서 이동 (Explorer/Finder에서 직접 이동 금지)
- Unity가 `.meta`의 GUID를 유지해주므로 참조가 안 끊김
- 혹시 끊기면 `.meta` 파일의 guid를 이전 값으로 복원

---

## 7. 네이밍 컨벤션

### 스크립트 파일명

```
[도메인명][역할].cs
```

- `PlayerMovement.cs` (O)
- `Movement.cs` (X — 어떤 도메인인지 불명확)
- `PlayerMovementController.cs` (X — 불필요하게 긴 접미사)

### 역할 접미사 규칙

| 접미사 | 용도 | 예시 |
|--------|------|------|
| (없음) | 핵심 로직 | `PlayerHealth`, `Gun`, `CardDeck` |
| `Visual` | 렌더링/이펙트 | `PlayerVisual`, `PlayerDeathVisual` |
| `UI` | 화면 표시 | `CardSelectionUI`, `GameOverUI` |
| `Data` | 데이터 정의 | `CardData`, `RoomData` |
| `Manager` | 전체 관리/오케스트레이션 | `RoomManager`, `HPBarManager` |
| `Controller` | 카메라/입력 제어 | `CameraController`, `BulletController` |

### namespace

```csharp
namespace Colosseum.[도메인]
// 예: Colosseum.Player, Colosseum.Weapon, Colosseum.Card
```

---

## 8. 모노레포 구조

본 프로젝트는 **모노레포**를 채택합니다. Unity, Spring Boot, 인프라 코드가 하나의 저장소(`lab.ssafy.com/s14-final/S14P31A303`)에 공존합니다. SSAFY 환경상 별도 레포 분리 불가.

### 루트 디렉토리 구조 (2026-04-21 T6 결정: 지금부터 즉시 적용)

> 2026-04-21 팀회의 T6에서 "MVP 이후 확장 시"가 아닌 **지금부터 이 구조를 적용**하기로 결정. Spring Boot 채택 전이라도 `unity/`, `worker-agent/`, `server/`, `infra/`, `docs/` 루트 폴더를 사용해 모노레포 경계를 선제적으로 박는다.

```
Colosseum/                         ← 모노레포 루트
├── unity/                         ← Unity 프로젝트 전체
│   ├── Assets/
│   │   └── _Project/
│   ├── Packages/
│   └── ProjectSettings/
│
├── worker-agent/                  ← ASP.NET Core Worker Agent (현재 활성)
│   ├── Program.cs
│   ├── Colosseum.WorkerAgent.csproj
│   └── Properties/
│
├── server/                        ← Spring Boot 서버 (현재 활성: Room Registry API)
│   ├── src/
│   │   ├── main/java/...
│   │   └── main/resources/
│   ├── build.gradle
│   └── Dockerfile
│
├── infra/                         ← 인프라/배포 설정
│   ├── localbuild/                ← 로컬 실행 보조 (Spring JAR + Docker Compose)
│   │   ├── Dockerfile
│   │   └── docker-compose.yml
│   ├── remotebuild/               ← 원격 EC2 배포 자산 (현재 활성)
│   │   ├── APIServer/             ← Spring Boot API Docker/Compose
│   │   ├── WorkerServer/          ← Worker Agent Docker/Compose + Linux Dedicated mount
│   │   └── OnboardingPage/        ← Nginx 정적 페이지 + Client.zip 다운로드
│   └── aws/                       ← 키페어/네트워크/운영 메모 등 인프라 보조 자산
│
├── docs/                          ← 공통 문서
│
├── .gitlab-ci.yml                 ← CI/CD (GitLab CI)
├── .gitlab/                       ← CODEOWNERS 등 GitLab 운영 자산
│
├── .gitattributes
└── .gitignore
```

### 모노레포 원칙

1. **하나의 저장소, 하나의 develop 브랜치**: 모든 코드가 동일한 Git history를 공유합니다.
2. **폴더로 경계 구분**: `unity/`, `worker-agent/`, `server/`, `infra/`가 물리적 경계입니다.
3. **CI/CD는 GitLab CI 기준**: 현재 파이프라인 정본은 루트 `.gitlab-ci.yml` 이다. workflow 생성은 `master` 브랜치와 `web` 수동 실행을 기준으로 두고, 잡 실행은 `TARGET` 변수(`api`, `worker`, `lobby`, `onboarding`, `all`)로 분기한다. 자동 파이프라인에서는 `changes` 규칙으로 관련 경로가 바뀐 job만 생성하고, `web` 수동 실행은 `changes`를 우회해 필요한 job을 강제 실행할 수 있다.
4. **공유 문서는 `docs/`에**: 양쪽 프로젝트에 공통되는 문서는 루트 `docs/`에 보관합니다.

### 현재 원격 배포 레퍼런스 (2026-05-08)

- Onboarding EC2: `infra/remotebuild/OnboardingPage/` 자산을 Nginx 컨테이너로 서빙하고 `Client.zip` 다운로드를 제공한다.
- Lobby EC2: Unity Linux Dedicated build를 `--mode lobby`로 직접 실행한다. 현재 레퍼런스에서는 Docker 래핑 대상이 아니다.
- Worker EC2: `infra/remotebuild/WorkerServer/` 자산으로 Worker Agent 컨테이너를 띄우고, Linux Dedicated build 폴더를 mount해 GameServer 프로세스를 실행한다.
- API EC2: `infra/remotebuild/APIServer/` 자산으로 Spring Boot Room Registry API를 컨테이너로 실행한다.
- 원격 control plane 주소는 작업 브리핑에 명시하되, 현재 구현 예시는 `COLOSSEUM_WORKER_BASE_URL=http://10.x.x.x:8080`, `COLOSSEUM_ROOM_API_BASE_URL=http://10.x.x.x:8081/api/v1` 형식을 따른다.

---

## 9. Git 브랜치 전략

### 브랜치 구조

```
master
├── develop                                                ← 개발 통합 브랜치
│   ├── feature/gameplay/player/A303-{n}-...               ← Unity 게임플레이/Player 도메인
│   ├── feature/gameplay/weapon/A303-{n}-...               ← Unity 게임플레이/Weapon 도메인
│   ├── feature/gameplay/card/A303-{n}-...                 ← Unity 게임플레이/Card 도메인
│   ├── feature/gameplay/game/A303-{n}-...                 ← Unity 게임플레이/Game 규칙
│   ├── feature/network/network/A303-{n}-...               ← Unity 네트워크 인프라
│   ├── feature/visual/ui/A303-{n}-...                     ← Unity UI
│   ├── feature/visual/camera/A303-{n}-...                 ← Unity 카메라
│   ├── feature/infra/worker-agent/A303-{n}-...            ← ASP.NET Worker Agent
│   ├── feature/server/{서브}/A303-{n}-...                  ← Spring Boot / Redis / Room Registry API
│   └── feature/infra/{서브}/A303-{n}-...                   ← Docker, CI/CD, 배포
├── docs                                                   ← 문서 전용
└── hotfix/A303-{n}-...                                    ← 긴급 수정 (master에서 분기)
```

### 브랜치 네이밍 (v3.0: Jira 이슈 키 A303-{번호} 강제 + 서브디렉토리 필수)

```
feature/[담당영역]/[서브디렉토리명]/A303-{이슈번호}-[작업내용]
fix/[담당영역]/[서브디렉토리명]/A303-{이슈번호}-[버그내용]
hotfix/A303-{이슈번호}-[내용]
docs/A303-{이슈번호}-[문서내용]
```

**서브디렉토리 규칙 (2026-04-21 T6 결정)**

- `[서브디렉토리명]`은 §2 폴더 구조의 **1레벨 폴더명**을 그대로 사용한다(예: `player`, `weapon`, `card`, `game`, `network`, `ui`, `camera`, `editor`).
- 수정 대상이 2레벨 이상 깊이의 폴더에 있어도 **1레벨만 브랜치명에 표기**한다. 예: `Assets/_Project/Scripts/Player/Movement/` → `player`만 사용.
- 서브디렉토리명은 **필수**. 복수 서브에 걸치는 작업은 주 변경 영역의 서브디렉토리 선택.
- `hotfix/`와 `docs/`는 영역·서브디렉토리 모두 생략 가능.

예시:
- `feature/gameplay/player/A303-45-player-movement-refactor` (PlayerMovement.cs 수정)
- `feature/gameplay/weapon/A303-78-bullet-bounce-effect` (BulletController.cs 수정)
- `feature/gameplay/card/A303-52-poison-card-effect` (CardEffect.cs 수정)
- `feature/gameplay/game/A303-33-room-progression` (RoomManager.cs 수정)
- `feature/network/network/A303-24-reconnect-logic` (NetworkManager.cs 수정)
- `feature/visual/ui/A303-92-card-selection-ui` (CardSelectionUI.cs 수정)
- `feature/visual/camera/A303-88-follow-smooth` (CameraController.cs 수정)
- `feature/infra/editor/A303-120-build-pipeline` (BuildHelper.cs 수정)
- `fix/gameplay/player/A303-85-respawn-bug`
- `hotfix/A303-90-crash-on-disconnect`
- `docs/A303-99-git-convention-v3`

**Jira 이슈 키 필수.** 이슈가 없는 임시 문서 정리는 예외적으로 `chore/docs-{slug}` 허용(Jira 이슈 생성 시 정식 브랜치로 이전).

### 담당영역 prefix 가이드

| Prefix | 대상 폴더 | 담당 | Jira Component |
|--------|-----------|------|---------------|
| `gameplay` | `unity/Assets/_Project/Scripts/Player/`, `Weapon/`, `Card/`, `Game/` | 게임플레이 | Gameplay / Physics |
| `network` | `unity/Assets/_Project/Scripts/Network/` | 네트워크 | Network |
| `visual` | `unity/Assets/_Project/Scripts/UI/`, `Camera/`, Animations, Audio | 비주얼 | UI / Art / Audio |
| `server` | `server/` 전체 | 백엔드 | API |
| `infra` | `infra/`, `.gitlab-ci.yml`, `.gitlab/`, Docker/Nginx 설정, `worker-agent/`, `infra/localbuild/`, `infra/remotebuild/` | DevOps / Worker Agent / 로컬·원격 실행 보조 | Infra |

**여러 영역에 걸치는 작업**: 비중이 더 큰 쪽의 prefix를 사용합니다.
예) 로그인 기능 = Spring Boot API + Unity UI → `feature/server/auth/A303-110-login` 형식(서브디렉토리명 필수, `server/` 하위 1레벨 사용. 백엔드가 메인이므로 영역 prefix는 `server`)

예) Room Registry API + Redis 연동 = `feature/server/room/A303-{n}-room-registry-api`

예) 로컬 Docker 실행 보조 (`infra/localbuild/`) = `feature/infra/localbuild/A303-{n}-spring-local-compose`

예) 원격 Worker 배포 자산 (`infra/remotebuild/WorkerServer/`) = `feature/infra/remotebuild/A303-{n}-worker-ec2-deploy`

### 워크플로우

```
1. develop에서 feature 브랜치 생성
   git checkout develop
   git pull origin develop
   git checkout -b feature/gameplay/card/A303-45-new-card-mechanic

2. 작업 후 커밋 (작은 단위로 자주)
   git add [수정한 파일]
   git commit -m "[A303-45] feat: add poison card effect"

3. develop에 MR 생성 (제목에 이슈 키 포함)
   제목: [A303-45] feat: add poison card effect

4. 코드 리뷰 후 머지 (기본 Squash Merge, §13 참조)

5. 릴리즈 시 develop → master 머지 (Gate 시점)
```

### hotfix 워크플로우

```
1. master에서 hotfix 브랜치 생성
   git checkout master
   git checkout -b hotfix/A303-90-critical-crash-fix

2. 수정 후 커밋
   git commit -m "[A303-90] fix: resolve crash on room transition"

3. master에 MR → 머지
4. develop에도 cherry-pick 또는 머지 (양쪽 모두 반영 필수)
```

### 충돌 방지 규칙

**규칙 1: 담당 영역 외 파일 수정 시 반드시 MR에 명시**

MR 본문에 "이 MR은 `PlayerHealth.cs`를 수정합니다 (네트워크 담당자 확인 필요)" 작성.

**규칙 2: 공유 파일 수정은 짧은 주기로 머지**

`PlayerHealth.cs`, `Gun.cs`, `CardDeck.cs` 등 충돌 가능 파일을 수정하는 브랜치는 오래 유지하지 않습니다. 하루 이내에 MR을 올리고 머지합니다.

**규칙 3: develop을 자주 pull**

feature 브랜치에서 작업 중에도 매일 develop을 rebase합니다:
```bash
git fetch origin
git rebase origin/develop
```

**규칙 4: Prefab/Scene 수정은 한 명만 (상세 §15 씬 락 프로토콜)**

**규칙 5: 크로스 프로젝트 변경 시 하나의 MR로**

Unity와 Spring Boot를 동시에 수정하는 기능은 하나의 MR에 포함시켜 양쪽의 정합성을 보장합니다.

현재 구현 기준으로 `worker-agent`와 `server`의 역할은 아래처럼 나눈다.

- `worker-agent/`: Unity 게임 서버 프로세스 spawn / 종료 / stdout 상태 수집 / Spring Room API 호출
- `server/`: Redis 기반 Room Registry API (`GET/POST/PATCH/DELETE /api/v1/rooms`)
- `infra/localbuild/`: 팀원 로컬 환경에서 Spring JAR + Redis를 Docker Compose로 기동하는 보조 실행 경로
- `infra/remotebuild/`: 원격 EC2 배포 자산. `APIServer/`, `WorkerServer/`, `OnboardingPage/`의 Docker/Compose/Nginx 설정과 정적 배포 파일을 포함한다.

### 커밋 메시지 규칙 (v3.0: Jira 이슈 키 강제)

```
[A303-{이슈번호}] [타입]: [한 줄 설명]

(필요 시 본문에 "왜 바꿨는가")
```

예: `[A303-45] feat: add poison card effect`

타입:

| 타입 | 의미 |
|------|------|
| feat | 새 기능 |
| fix | 버그 수정 |
| refactor | 기능 변경 없는 코드 개선 |
| chore | 빌드, 설정 등 비코드 변경 |
| docs | 문서 수정 |
| art | 아트 에셋 추가/변경 |
| audio | 사운드/FMOD 관련 |
| hotfix | 긴급 수정 |
| test | 테스트 추가/수정 |

한 커밋은 하나의 논리적 변경만 담습니다.

---

## 10. 컴포넌트 분리 시 코드 패턴

### 분리된 컴포넌트 간 참조 방법

```csharp
// PlayerDeathVisual.cs — IsDead를 읽어서 비주얼 처리
public class PlayerDeathVisual : MonoBehaviour
{
    private PlayerHealth _health;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();  // 같은 GameObject에서 캐싱
    }

    public void OnDeathStateChanged(bool isDead)
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in renderers)
            r.enabled = !isDead;
    }
}
```

### 이벤트 기반 연결 (권장)

```csharp
// PlayerHealth.cs
public event System.Action<bool> OnDeathChanged;

private void Die(...)
{
    IsDead = true;
    OnDeathChanged?.Invoke(true);
}

// PlayerDeathVisual.cs
private void OnEnable()
{
    GetComponent<PlayerHealth>().OnDeathChanged += HandleDeathChanged;
}
```

이렇게 하면 `PlayerHealth`는 `PlayerDeathVisual`의 존재를 모르고, 비주얼 담당자가 `PlayerDeathVisual`을 자유롭게 수정해도 `PlayerHealth`에 영향이 없습니다.

---

## 11. 체크리스트

### 새 기능 추가 시

- [ ] 어느 도메인 폴더에 넣을지 결정했는가?
- [ ] 기존 파일에 추가할지, 새 파일을 만들지 판단했는가?
- [ ] NetworkBehaviour vs MonoBehaviour 결정했는가?
- [ ] HasStateAuthority 가드가 필요한 로직인가?
- [ ] 담당 영역 외 파일을 수정하는가? → MR에 명시
- [ ] Jira 이슈(A303-{n})가 있는가? 없으면 먼저 생성

### MR 생성 시

- [ ] 브랜치명이 `{feature|fix}/[영역]/[서브디렉토리]/A303-{n}-{내용}` 형식인가? (서브디렉토리는 §2 1레벨만)
- [ ] 커밋 메시지가 `[A303-{n}] [타입]: {제목}` 형식인가?
- [ ] MR 제목에 `[A303-{n}]` 포함했는가?
- [ ] MR 본문 필수 필드(§13)를 모두 채웠는가?
- [ ] 공유 파일(`PlayerHealth`/`Gun`/`CardDeck`) 수정 시 관련 담당자를 리뷰어로 지정했는가?
- [ ] 게이트 영역(NetworkProjectConfig/art/카드 구조) 변경 시 해당 게이트키퍼 승인 받았는가?
- [ ] Prefab/Scene 수정 시 `[SCENE-LOCK]` 공지 후 작업했는가? (§15)
- [ ] CI가 green인가?

---

## 12. Unity Git 운영 설정

Unity 팀 협업의 1위 사고 원인은 씬/프리팹/meta 충돌과 LFS 누락입니다. 다음 설정은 Unity 프로젝트 생성 **이전** 또는 **즉시** 레포에 적용합니다.

### 12.1 `.gitignore`

- Unity 공식 템플릿 기준(`Library/`, `Temp/`, `Logs/`, `Build/`, `UserSettings/`, `*.csproj`, `*.sln`, IDE 설정).
- 현재 레포에 적용 완료(2026-04-20 커밋).
- Photon AppID·secret은 `.env.local`, `*.local.asset` 등 `*.local.*` 패턴으로 ignore.

### 12.2 `.gitattributes`

- LFS 추적 확장자 명시: `*.psd`, `*.fbx`, `*.wav`, `*.ogg`, `*.png`, `*.jpg`, `*.mp3`, `*.blend`, `*.ttf` 등.
- UnityYAMLMerge: `*.unity`, `*.prefab`, `*.asset`, `*.meta`, `*.mat`, `*.anim`, `*.controller`에 `merge=unityyamlmerge eol=lf` 지정.
- 현재 레포에 적용 완료(2026-04-20 커밋).

### 12.3 `UnityYAMLMerge` 로컬 등록

각 팀원 로컬에서 `.git/config`에 Unity Merge Tool 등록 필수. Unity 프로젝트 clone 직후 1회.

```text
[merge "unityyamlmerge"]
  name = Unity SmartMerge
  driver = '<Unity installation path>/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A
  recursive = binary
```

### 12.4 `ProjectSettings/EditorSettings.asset`

Unity 프로젝트 생성 직후 다음 값 고정(유민재 또는 박병준이 첫 커밋에 포함).

- Asset Serialization Mode: **Force Text** (씬/프리팹 YAML 머지 가능)
- Version Control Mode: **Visible Meta Files**
- Line Endings: **Unix (LF)**

### 12.5 LFS quota

**SSAFY GitLab 실측 (2026-04-27, S14P31A303-152)**: self-hosted CE/EE에 admin-level quota 강제 없음. 프로젝트 레벨 사용량만 표시되고 cap 미설정. 그룹 레벨 `usage_quotas` 페이지는 404. **강제 한도 없음**.

현재 사용량(2026-04-27, origin/develop @ 61f8248): Repository 8.26 MiB / LFS 978.34 KiB (전부 Photon Fusion SDK + TextMeshPro 내장 리소스). 자체 게임 에셋 LFS 진입 0개.

**운영 정책 (자체 임계, S14P31A303-152 결정)**:

| 임계 | 트리거 | 대응 |
|---|---|---|
| 임계 1 | 단일 에셋 50 MB 초과 | LFS 미진입, 외부 스토리지(SSAFY Drive / Google Drive)로 분기 + 레포에 다운로드 링크 + SHA256 |
| 임계 2 | LFS 누적 5 GB 도달 | SSAFY 인프라 팀 문의 + 압축 강화 + 임계 1을 25 MB로 하향 |
| 임계 3 | LFS 누적 10 GB 도달 | 외부 스토리지 전면 이전 + 마스터 브랜치 LFS purge 검토 |

**상시 압축**: PSD 작업본 gitignore + PNG 커밋 / 오디오 OGG 96 kbps / Sprite Atlas / 텍스처 2K 이하.

**모니터링**: 주간(매주 월요일) 박병준이 `git lfs ls-files --size` 합산 + GitLab Usage 페이지 확인 → Mattermost `#dev-infra` 공유. MR 단계 리뷰어가 신규 LFS 진입 파일 50 MB 체크. Gate 시점(G1~G4)에 재측정.

상세 측정·정책·루틴은 [`90_백로그/03_Git_LFS_실측.md`](../90_백로그/03_Git_LFS_실측.md) 참조.

---

## 13. MR 운영 체계

### 13.1 MR 제목 형식

```
[A303-{이슈번호}] [타입]: [간단한 설명]
```

### 13.2 MR 본문 필수 필드

코드 MR:
```text
source_branch:
target_branch:
Jira 이슈: A303-{번호}
도메인 구분자: [GAMEPLAY|NETWORK|PHYSICS|UI|ART|AUDIO|INFRA|PLAN]
변경 요약:
테스트 방법: (Unity Editor Play / 씬 확인 / 자동 테스트 여부)
씬/프리팹 변경: (있으면 파일명과 소유자 확인)
NetworkObject 변경: (있으면 Y/N + 내용)
관련 기준문서:
게이트키퍼 리뷰 필요: (해당 영역 게이트키퍼 이름)
```

기준문서 MR (간소화):
```text
source_branch: docs/A303-{n}-...
target_branch: develop
문서 변경 요약:
영향 도메인:
확정/임시/보류 상태:
관련 Jira:
관련 기준문서:
검토 요청자:
AI 검토 사용 여부:
남은 미결정:
```

### 13.3 승인 정책

| 방향 | 최소 승인 | 추가 요건 |
|---|---|---|
| `{feature|fix}/*` → `develop` | 1명 | 게이트 영역(§13.4) 변경 시 해당 게이트키퍼 DRI 추가 승인 |
| `develop` → `master` | 1명 + CI green | Gate 태그 시점(G1~G4, §14)은 **2명 + Smoke 통과** |
| `hotfix/*` → `master` | 1명 + 작성자 Mattermost 공지 | 머지 후 24h 내 사후 리뷰 티켓 생성. develop 역머지 필수 |

### 13.4 게이트키퍼 영역 (`05/05` DRI 매트릭스 참조)

| 변경 영역 | 게이트키퍼 DRI | 추가 승인 필수 |
|---|---|---|
| `NetworkProjectConfig`, Fusion Runner, 서버 모드 | 유민재 | Y |
| `Assets/Art/*`, 캐릭터/맵/VFX/UI 룩, 발표용 비주얼 | 우수연 | Y |
| 폴더/네임스페이스, 카드 시스템 구조, 기준문서, 카드 데이터 모델 | 박병준 | Y |

### 13.5 CODEOWNERS 자동 리뷰어

`.gitlab/CODEOWNERS` 파일로 경로별 자동 리뷰어 지정을 권고. T2 DRI 이름 확정 후 Jun이 작성. 예시:

```
/docs/                           @jun
/unity/Assets/_Project/Scripts/Network/  @minjae
/unity/Assets/_Project/Scripts/Card/     @byungjun
/unity/Assets/Art/                        @suyeon
```

### 13.6 머지 방식

**기본 Squash Merge** (팀원 가이드 §9 결정 존중). 기능 단위로 커밋 히스토리를 깔끔하게 유지.

예외: Gate 태그 직전 `develop → master` 머지는 **Merge commit `--no-ff`** 사용 — 전체 Gate의 rollback 단위 보존(`git revert -m 1 <gate-merge>`).

### 13.7 셀프 머지

기본 금지. hotfix는 **작성자 Mattermost 공지 + 머지 후 24h 내 사후 리뷰 티켓** 조건으로 예외 허용.

### 13.8 AI 리뷰

AI 도구(Claude / Codex / Gemini 등)의 리뷰는 **리뷰 보조 자료**이며, **승인 카운트에 포함되지 않는다**. 최종 승인자는 사람 담당자.

### 13.9 리뷰 SLA

리뷰 요청자는 리뷰어 지정 후 24h 내 피드백. 지연 시 Mattermost 재공지.

---

## 14. Gate 태그와 SCHEDULE 매핑

### 14.1 Gate 태그

| 시점 | 태그 | Push DRI | 조건 |
|---|---|---|---|
| G1 Pass | `gate-1` | 유민재 | `70/04 G1` 통과 확인 + master 머지 완료 |
| G2 Pass | `gate-2` | 유민재 | `70/04 G2` 통과 |
| G3 Pass | `gate-3` | 유민재 | `70/04 G3` 통과 |
| G4 Pass | `gate-4` | 유민재 + 박병준 | `70/04 G4` 통과 + 발표 빌드 확인 |
| 최종 발표 빌드 | `v1.0-demo` | 박병준(PM) | G4 Pass 이후 |

**Protected Tags 설정**: `gate-*`, `v*` 패턴을 Maintainer only. T2 DRI 확정 후 Jun이 GitLab UI에서 적용.

### 14.2 SCHEDULE W1~W5 ↔ Gate 매핑

| 주차 | Exit Criteria 요약 | Gate | 태그 후보 |
|---|---|---|---|
| W1 (2026-04-14~20) | 7컴포넌트 smoke + Fusion 2P 리허설 | — | — |
| W2 (2026-04-21~29) | 카드 10종 확정 + Host Mode 3라운드 | G1 | `gate-1` (W2 말) |
| W3 (2026-04-30~05-06) | 첫 Playable Slice + 원격 2P | G2 | `gate-2` (W3 말) |
| W4 (2026-05-07~13) | 네트워크 최적화 + 밸런스 | G3 | `gate-3` (W4 말) |
| W5 (2026-05-14~20) | 리허설 2회 + 발표 빌드 | G4 | `gate-4` → `v1.0-demo` |

태그 push 조건: 해당 주차 Exit Criteria 전부 충족 + 플레이테스트 리포트 작성 + `05/06 의사결정 로그` D-번호 등록.

### 14.3 Gate 플레이테스트

- Gate 통과 시 전원 플레이테스트 기본.
- Jira에 `[PLAN] Gate N 플레이테스트` Task로 등록.
- 리포트 양식: `70_검증_체크리스트/02_플레이테스트_리포트_양식.md`.

---

## 15. 씬/프리팹 락 프로토콜

씬 파일(`.unity`) 또는 공용 프리팹 편집 전 다음 절차 필수:

1. **Jira 서브태스크 `[SCENE-LOCK] <씬명>` 생성** — 상위 이슈에 attach
2. **팀 Mattermost `#dev-scene-lock` 채널에 공지** — "lock: <파일명>, 예상 duration"
3. **해당 MR 머지 또는 락 해제 공지 전까지** 같은 파일을 다른 사람이 수정 금지
4. **충돌 발생 시** `UnityYAMLMerge`로 해결(§12.3). 해결 불가 시 담당 DRI 1명이 최종 판단

### 공용 프리팹 목록 (락 대상)

- `Player.prefab`
- `Bullet.prefab`
- 추가되는 Prefab은 박병준(폴더/구조 DRI) 확인 후 목록 갱신.

---

## 16. Photon AppID / Secret 운영

### 16.1 금지

- Photon App ID, 라이선스 키, 계정 정보, 개인 PC 경로를 **기준문서·MR 본문·tracked 설정 파일에 실제 값으로 직접 기록 금지**.
- tracked 파일(`Assets/Photon/PhotonAppSettings.asset` 등)은 placeholder 또는 값 제거 상태 유지.

### 16.2 주입 경로 (2026-04-21 T4-3 결정)

- **보관 방식**: 박병준 PC 로컬 (gitignore 패턴 `*.local.*`로 차단). tracked 파일에 실제 값 기록 금지.
- 권장 구체 경로: `.env.local` 또는 `unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.local.asset` (placeholder 덮어쓰기 — Fusion 2 `Resources.Load` 동작 경로).
  - **구 경로 `ProjectSettings/PhotonAppSettings.local.asset` 는 deprecated** (2026-04-27 v0.2 정정). Fusion 2 의 `Resources.Load<PhotonAppSettings>("PhotonAppSettings")` 가 `Resources/` 하위만 인식.

### 16.3 팀원 전달 (2026-04-21 T4-3 결정)

**Mattermost DM**으로 1:1 전달. 공개 채널, 메모, README, MR 본문 금지.

### 16.4 담당 (2026-04-21 T4-3 결정, D53)

- **AppID 발급 담당**: 박병준
- **보관 경로**: 박병준 PC 로컬 (위 §16.2)
- **팀원 전달 방식**: Mattermost DM
- **신규 팀원 합류 시**: 박병준이 동일 방식으로 DM 전달

### 16.5 AppID local override runbook (accepted_candidate)

> 2026-04-27 박병준 작성. 본 인벤토리 5항목은 별도 문서 [`03_Photon_AppID_Runbook.md`](03_Photon_AppID_Runbook.md) 로 정리되었다 (`S14P31A303-153` 산출물). 향후 갱신은 03 문서에서 진행하고, 본 §16.5 는 항목 인덱스만 유지한다.

- §1: Photon/Fusion placeholder asset 실제 경로 (`unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`) 및 placeholder 검증.
- §2: local override 파일명·배치 위치. `.gitignore` 패턴 `**/PhotonAppSettings.local.*` / `**/*.local.asset` 일치 확인.
- §3: Unity Editor 에서 placeholder 에 AppID 주입 단계별 절차 (sidecar 자동 주입 권장 / 수기 비권장).
- §4: CI/MR 에서 tracked 파일에 secret 유입 여부 검증 (현재 self-check + 리뷰어 grep, `S14P31A303-216` 완료 후 CI 자동화).
- §5: AppID 미보유 시 로컬 2P / LAN Host Mode fallback (`70_검증_체크리스트/05_Fusion_로컬_2인_리허설.md` 연계).

---

## 17. Jira 도메인 구분자 / Priority / DoD

### 17.1 도메인 구분자

웹 프로젝트식 `[FE]`, `[BE]`, `[AI]` 구분자는 사용하지 않습니다. Unity 게임 도메인 기준:

| 구분자 | 역할 | Jira Component | 브랜치 prefix(§9) |
|---|---|---|---|
| [GAMEPLAY] | 게임 로직, 카드, 승리조건, 전투, 밸런스 | Gameplay | gameplay |
| [NETWORK] | Photon Fusion, 동기화, 세션, 서버 권위 | Network | network |
| [PHYSICS] | 캐릭터 이동, 충돌, 점프, 물리 | Physics | gameplay |
| [UI] | HUD, 메뉴, 카드 선택, 로비, 인게임 UI | UI | visual |
| [ART] | 스프라이트, 애니메이션, VFX, 맵 비주얼 | Art | visual |
| [AUDIO] | FMOD, SFX, BGM, 사운드 이벤트 | Audio | visual |
| [INFRA] | CI/CD, 빌드, 프로젝트 설정, Git | Infra | infra |
| [PLAN] | 기획, 설계, 문서, 플레이테스트, 발표 | Planning | (docs/ 또는 별도) |
| [API] | Spring Boot 백엔드 (현재 활성) | API | server |

Epic은 구분자, Label, Component 없이 기능명만 사용.

### 17.2 Priority 기준

| 등급 | 기준 |
|---|---|
| High | 이번 스프린트에 반드시 완료해야 하거나, 없으면 다음 작업이 막힘 |
| Medium | 이번 스프린트에 완료하면 좋지만 데모 가능성은 유지됨 |
| Low | 다음 스프린트로 미뤄도 되며 여유가 있을 때 처리 |

의존성이 있으면 사용자 중요도와 관계없이 `High`.

### 17.3 DoD

개발 작업 완료 기준:
- 코드 또는 에셋이 정상 동작
- MR을 올리고 최소 1명의 리뷰를 받음
- 리뷰 코멘트를 반영하고 MR이 머지됨
- 빌드 깨지지 않음 (CI green)
- Story라면 인수 조건 모두 충족
- 아트 에셋이라면 AD(우수연) 승인

기획/설계 작업은 `docs/` 트리에 작성 + 팀원 공유·피드백 반영 완료.

### 17.4 Sprint 용량

5인 × 10점 × 70% = **35점/주**.

### 17.5 이슈 타입 정책 (2026-04-22 추가)

**개발 백로그 표준**: `Epic(마일스톤) → Story → Sub-task` 3단계 고정.

| 타입 | 적용 범위 | 예시 |
|---|---|---|
| **Epic** | 마일스톤(M1~M4) 컨테이너. 기능 분류 아님 | M1 2인 로컬 플레이어블 슬라이스 |
| **Story** | 코드 산출물이 있는 모든 항목 (기본값) | [GAMEPLAY] 카드 드래프트 구현 |
| **Sub-task** | ① 담당자 핸드오프, ② QA/빌드 분리, ③ 1일 이상 독립 작업 중 하나일 때 | [NETWORK] 세션 생성 RPC |
| **Task** (예외 허용) | 코드 산출물이 없는 운영성 항목 | 회의·의사결정·리허설·문서·Gate 태그 발행 |

**Task 허용 범위 (예외)**:
- 회의·의사결정 로그·플레이테스트 운영·발표 리허설
- 문서 정리·용어 교체·인프라 문서 작업 (CODEOWNERS·Git LFS 실측 등)
- Gate 태그 발행과 master 머지 (DevOps)

**Story 강제**: [GAMEPLAY], [NETWORK], [UI], [ART], [AUDIO] 구분자 전부. [INFRA]는 기본 Story, 문서·DevOps 성격만 Task. [PLAN]은 전부 Task.

**근거**: 기존 실패 구조(Epic 12 + Task 108 + Story/Sub-task 0)가 Task 일원화 방식이었고 컨벤션 불일치로 D63에서 전량 삭제됨. 회귀 방지가 핵심.

**참조**: 실제 M1~M4 적용 인벤토리는 `90_백로그/11_2026-04-21_M1-M4_Jira_생성_대기.md` §0-0/§0-2.

### 17.6 Fix Version 정책 (2026-04-22 추가)

**원칙**: `Fix Version`은 "이번 마일스톤에서 내보낼 약속", `gate-*`/`v1.0-demo` Git 태그는 "실제 증적". 서로 대체하지 않음.

**Jira Versions (4개)**:

| Version | 마일스톤 | Git 태그 | 릴리즈 기준 |
|---|---|---|---|
| `v0.1-M1` | M1 (W1~W2) | `gate-1` | G1 Pass + W2 Exit |
| `v0.2-M2` | M2 (W3) | `gate-2` | G2 Pass + 70/06 6질문 |
| `v0.3-M3` | M3 (W4) | `gate-3` | G3 Pass + RTT 기준 |
| `v1.0-demo` | M4 (W5) | `v1.0-demo` | G4 Pass + 시연 완주 |

**운영 규칙**:

- Story 생성 시 **Epic Link + Fix Version 동시 필수 입력**
- Sub-task는 부모 Story의 Fix Version 승계
- Sprint 보드 = M1~M4 주차 Sprint / 릴리즈 추적 = `Fix Version` (분리)

**Gate Pass 당일 루틴**:

1. 미완료 Story의 `Fix Version`을 차기 버전으로 이동 (carry-over)
2. 해당 Version을 `release` 상태로 전환 (Jira Versions 화면)
3. `gate-N` (또는 `v1.0-demo`) Git 태그 푸시
4. Epic close 여부 판정 — "해당 마일스톤 Story가 전부 Done **또는** 차기 Version으로 이동 완료"

**캐리오버 원칙**: 하나의 Story가 마일스톤을 넘어가면 이슈를 새로 쪼개지 말고 `Fix Version`만 다음 버전으로 이동.

**참조**: 실제 적용 루틴과 JQL 예시는 `90_백로그/11_2026-04-21_M1-M4_Jira_생성_대기.md` §0-1/§6-1.

---

## 18. 기준문서와의 연결

| 기준문서 | 연결 |
|---|---|
| `05_문서관리_규칙/01_문서_상태와_변경_규칙.md` | 문서 상태 7단계 정의. 본 문서의 상태 판단 근거 |
| `05_문서관리_규칙/05_팀_작업구조와_역할_소유권.md` | 5인 DRI 매트릭스. §13.4 게이트키퍼 근거 |
| `50_기술_구조_기준/01_Unity_프로젝트_구조.md` (accepted) | Unity 2022.3 LTS + Fusion 2 네이티브. §1·§9의 상위 전제 |
| `50_기술_구조_기준/03_씬_프리팹_소유권.md` | §15 씬 락 프로토콜의 상위 원칙 |
| `60_AI_협업_운영_원칙/02_작업_브리핑_양식.md` | §13.2 MR 본문 필드와 1:1 매칭 |
| `70_검증_체크리스트/04_마일스톤_게이트.md` | §14.1 Gate 태그의 상위 기준 |
| `SCHEDULE.md` | §14.2 W1~W5 매핑 출처 |

충돌 시 `accepted` 기술 구조 기준(특히 `50/01`)이 본 문서보다 우선.

---

## 승격 트리거

본 문서가 `candidate` → `accepted_candidate` 승격 조건(v3.0):

| 조건 | 확인 방법 |
|---|---|
| (1) 팀회의 T6에서 본 문서 채택 승인 | **✅ 2026-04-21 완료 (D55 등록, A안 채택)** |
| (2) GitLab protected branch 설정과 §9 일치 확인 | **✅ 2026-04-21 완료** (D59). `master`·`develop` 두 브랜치 protected 적용 확인(박병준) |
| (3) `develop` 브랜치 최초 생성 + 최초 `feature/*` MR 1건 완주 | MR URL 기록 |
| (4) `.gitignore` / `.gitattributes` 레포 적용 완료 | ✅ 2026-04-20 완료 |
| (5a) T2 DRI 확정 (§13.5 CODEOWNERS 전제) | **✅ 2026-04-21 D49** |
| (5b) `.gitlab/CODEOWNERS` 파일 작성 (§13.5) | ✅ 2026-04-21 완료 (커밋 `444c5c7`) |

`accepted` 승격은 팀 전 스프린트에서 본 컨벤션대로 MR 10건 이상 완주 + 씬/프리팹 merge conflict 0건 관찰 이후.

---

## 미결정

- ~~팀 GitLab 실제 protected branch 설정 확인~~ — ✅ 2026-04-21 완료 (D59. `master`·`develop` 두 브랜치 protected).
- ~~CODEOWNERS 파일 적용 시점~~ — ✅ 2026-04-21 작성 완료 (`.gitlab/CODEOWNERS` origin/develop 반영, 커밋 `444c5c7`).
- ~~LFS quota 실측~~ — ✅ 2026-04-27 완료 (S14P31A303-152). 강제 쿼터 없음 + 자체 임계 정책 §12.5 / [`90_백로그/03_Git_LFS_실측.md`](../90_백로그/03_Git_LFS_실측.md)에 정착.
- Spring Boot API는 현재 Room Registry 용도로 활성 상태다. `server/` 폴더는 후보가 아니라 실제 구현 경계로 취급한다.
- Worker Agent는 `worker-agent/` 루트에 유지한다. 현재 역할은 GameServer 프로세스 orchestration/control plane이며, `server/` 하위로 재배치하지 않는다.
- 로컬 실행 보조는 `infra/localbuild/`를 사용한다. `Dockerfile`, `docker-compose.yml`, 실행 가이드를 이 폴더에 유지하고, Spring JAR는 git에 커밋하지 않고 로컬에서 `./gradlew bootJar`로 생성해 사용한다.
- 원격 배포 자산은 `infra/remotebuild/`를 사용한다. 현재 레퍼런스는 Onboarding, Lobby, Worker, API의 4개 EC2 역할 분리이며, 이 중 Lobby를 제외한 Onboarding/API/Worker는 Docker 기반 실행 자산을 해당 폴더에 둔다.

### 2026-04-21 T6 회의에서 결정됨 (본 문서 v3.0 accepted_candidate 승격)

- ✅ **T6-1 A안**: 영역축 브랜치 + Jira 이슈 키 강제 + 서브디렉토리 1레벨 필수
- ✅ **T6-2 A안**: `PROJECT_STRUCTURE_GUIDE.md` 정본 + v2.1 보완 6개 병합 = v3.0
- ✅ **§8 루트 디렉토리**: "MVP 이후 확장 시" → 지금부터 즉시 적용
- ✅ **§9 브랜치 네이밍**: `feature/[영역]/[서브디렉토리]/A303-{n}-{slug}`
- ✅ **T4-3 Photon AppID**: 박병준 / 로컬 보관 / Mattermost DM 전달
- ✅ **T3 Unity `Assets/_Project` 루트 폴더명**: `_Project` 확정 (Unity project root = `unity/`, Assets 내부 프로젝트 폴더 = `_Project`)

---

## 관련 문서

- [`05_문서관리_규칙/01_문서_상태와_변경_규칙.md`](../05_문서관리_규칙/01_문서_상태와_변경_규칙.md)
- [`05_문서관리_규칙/05_팀_작업구조와_역할_소유권.md`](../05_문서관리_규칙/05_팀_작업구조와_역할_소유권.md)
- [`50_기술_구조_기준/01_Unity_프로젝트_구조.md`](../50_기술_구조_기준/01_Unity_프로젝트_구조.md)
- [`50_기술_구조_기준/03_씬_프리팹_소유권.md`](../50_기술_구조_기준/03_씬_프리팹_소유권.md)
- [`60_AI_협업_운영_원칙/02_작업_브리핑_양식.md`](../60_AI_협업_운영_원칙/02_작업_브리핑_양식.md)
- [`70_검증_체크리스트/04_마일스톤_게이트.md`](../70_검증_체크리스트/04_마일스톤_게이트.md)
- [`SCHEDULE.md`](../SCHEDULE.md)
- [`99_아카이브/2026-04-20_Jira_Git_컨벤션_v2_딥리뷰.md`](../99_아카이브/2026-04-20_Jira_Git_컨벤션_v2_딥리뷰.md) — v2.0 DRAFT 3자 딥리뷰 원문
