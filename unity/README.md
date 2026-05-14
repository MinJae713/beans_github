# COLOSSEUM

<p align="center">
  2D online versus action prototype built with Unity and Photon Fusion
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Unity-2022.3%20LTS-000000?style=flat-square&logo=unity&logoColor=white" alt="Unity 2022.3 LTS" />
  <img src="https://img.shields.io/badge/C%23-Game%20Logic-512BD4?style=flat-square&logo=csharp&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/Photon%20Fusion-2.0.12-004480?style=flat-square&logo=photon&logoColor=white" alt="Photon Fusion 2" />
  <img src="https://img.shields.io/badge/URP-2D%20Pipeline-FF6F00?style=flat-square&logo=unity&logoColor=white" alt="URP 2D" />
  <img src="https://img.shields.io/badge/TextMeshPro-UI-3C91E6?style=flat-square&logo=unity&logoColor=white" alt="TextMeshPro" />
  <img src="https://img.shields.io/badge/Git%20LFS-Binary%20Assets-F64935?style=flat-square&logo=gitlfs&logoColor=white" alt="Git LFS" />
</p>

---

## Project Snapshot

**COLOSSEUM**은 2인 온라인 대전을 전제로 만든 2D 횡스크롤 액션 게임 프로토타입입니다.  
중앙 방에서 시작해 상대를 처치하며 진영 방향으로 전진하고, 끝 방에 먼저 도달하면 승리하는 구조를 갖고 있습니다.

이 저장소는 단순 기능 나열보다, 프로젝트를 어떻게 설계했고 어디까지 구현했는지 한눈에 보여주는 포트폴리오형 README를 목표로 정리했습니다.

### Role

- Solo developer
- Gameplay programming
- Network gameplay architecture
- UI prototyping
- System design for card-based combat progression

### Build Context

| Item | Details |
|------|---------|
| Project Type | 2D online PvP action prototype |
| Engine | Unity 2022.3 LTS |
| Rendering | Universal Render Pipeline (2D) |
| Language | C# |
| Networking | Photon Fusion 2, Host Mode |
| UI | TextMeshPro + runtime-generated UI |
| Version Control | Git + Git LFS |
| Remote Repository | [GitHub Private Repository](https://github.com/worldclasscitizen/colosseum-private) |

---

## Tech Stack

### Core

<p>
  <img src="https://img.shields.io/badge/Unity-2022.3%20LTS-000000?style=flat-square&logo=unity&logoColor=white" alt="Unity" />
  <img src="https://img.shields.io/badge/C%23-.NET%20Runtime-512BD4?style=flat-square&logo=csharp&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/Visual%20Studio-IDE-5C2D91?style=flat-square&logo=visualstudio&logoColor=white" alt="Visual Studio" />
  <img src="https://img.shields.io/badge/Rider-Compatible-000000?style=flat-square&logo=rider&logoColor=white" alt="Rider" />
</p>

### Gameplay and Networking

<p>
  <img src="https://img.shields.io/badge/Photon%20Fusion-Host%20Mode-004480?style=flat-square&logo=photon&logoColor=white" alt="Photon Fusion" />
  <img src="https://img.shields.io/badge/Physics-2D%20Collision-8E44AD?style=flat-square" alt="2D Physics" />
  <img src="https://img.shields.io/badge/ScriptableObject-Card%20Data-2E8B57?style=flat-square&logo=unity&logoColor=white" alt="ScriptableObject" />
  <img src="https://img.shields.io/badge/Networked%20State-Synced%20Gameplay-1565C0?style=flat-square" alt="Networked State" />
</p>

### UI and Pipeline

<p>
  <img src="https://img.shields.io/badge/TextMeshPro-UI%20System-3C91E6?style=flat-square&logo=unity&logoColor=white" alt="TextMeshPro" />
  <img src="https://img.shields.io/badge/Runtime%20UI-Code%20Driven-6D4C41?style=flat-square" alt="Runtime UI" />
  <img src="https://img.shields.io/badge/URP-2D%20Renderer-FF6F00?style=flat-square&logo=unity&logoColor=white" alt="URP" />
  <img src="https://img.shields.io/badge/Git-Version%20Control-F05032?style=flat-square&logo=git&logoColor=white" alt="Git" />
  <img src="https://img.shields.io/badge/Git%20LFS-Asset%20Management-F64935?style=flat-square&logo=gitlfs&logoColor=white" alt="Git LFS" />
</p>

### Why This Stack

- `Unity + URP 2D`로 빠른 프로토타이핑과 2D 액션 구현에 집중했습니다.
- `Photon Fusion`의 Host Mode를 사용해 권한 구조를 단순화하고, 핵심 전투 로직을 서버 권한 기반으로 정리했습니다.
- 카드 효과는 `ScriptableObject + 누적형 컴포넌트` 구조로 설계해 밸런싱과 확장을 쉽게 만들었습니다.
- UI는 코드 기반 생성 방식을 택해 빠른 실험과 반복에 유리하도록 구성했습니다.

---

## Key Features

### 1. Room Push PvP Structure

- 총 9개의 방으로 구성된 전장
- 중앙 방에서 시작하고, 적을 처치한 플레이어가 상대 진영 방향으로 전진
- 끝 방에 도달하면 승리
- `RoomManager`가 방 인덱스와 마지막 킬러 정보를 네트워크 상태로 동기화

### 2. Networked Combat Loop

- 좌우 이동, 점프, 마우스 에임 기반 전투
- 1발 탄창과 자동 재장전 구조
- 벽 반사, 관통, 넉백, 폭발 등 총알 특수 효과 지원
- 사망 후 리스폰과 진영 기반 재배치 로직 구현

### 3. Card-Based Progression

- 사망 시 3장 중 1장을 선택하는 드로우 시스템
- 카드 효과를 배율형, 누적형 스탯으로 분리
- ScriptableObject 기반 데이터 관리
- 전투 흐름을 매 판 다르게 만드는 성장 요소 설계

### 4. Code-Driven UI

- 메인 메뉴, 방 인디케이터, 재장전 게이지, HP UI, 게임 오버 UI 구현
- 빠른 반복 개발을 위해 런타임 생성 중심으로 구성
- 외부 UI 에셋 의존도를 낮추는 방향으로 설계

---

## Implemented Systems

### Gameplay

- 방 기반 진행 시스템
- 플레이어 이동, 점프, 에임
- HP, 사망, 리스폰
- 총기 발사와 자동 재장전
- 총알 충돌 분리 처리
- 총알 특수 효과 누적

### Card System

- 카드 드로우 및 선택 UI
- 카드 덱 관리
- 카드 효과 누적 계산

### Networking

- Photon Fusion Host/Client 구조
- 입력 패킹과 전송
- 핵심 상태 `[Networked]` 동기화

### UI

- `MainMenu`
- `RoomIndicatorUI`
- `ReloadIndicator`
- `HPBarManager`
- `GameOverUI`

---

## Card Pool

| Card | Summary | Rarity | Values |
|------|---------|--------|--------|
| Big Bullet | 탄 크기 증가 + 피해량 증가 | Common | `size x1.3`, `damage x1.2` |
| Speed Shot | 탄속 증가 | Common | `speed x1.2` |
| Bouncy | 벽 반사 1회 추가 | Common | `bounce +1` |
| Quick Reload | 재장전 속도 향상 | Common | `reload x1.25` |
| Buckshot | 추가 탄 발사 | Rare | `extra bullets +2` |
| Lifesteal | 피해량 일부 체력 흡수 | Rare | `lifesteal 10%` |
| Piercing Round | 적 관통 | Rare | `pierce +1` |
| Shove | 넉백 부여 | Rare | `knockback 8` |
| Blast Shot | 폭발 반경 피해 | Legendary | `radius 2`, `50% splash` |
| Overcharge | 재장전 페널티 + 첫 발 고배율 피해 | Legendary | `reload x0.67`, `first shot x3` |

---

## Project Structure

```text
Assets/_Project/
├── Data/Cards/
├── Prefabs/
└── Scripts/
    ├── Camera/
    ├── Card/
    ├── Game/
    ├── Network/
    ├── Player/
    ├── UI/
    └── Weapon/
```

### Main Responsibilities by Folder

- `Card`: 카드 데이터, 덱 관리, 효과 누적 처리
- `Game`: 방 진행 규칙과 게임 흐름
- `Network`: 입력 전달과 세션/러너 관리
- `Player`: 이동, 체력, 리스폰
- `UI`: 메뉴와 전투 HUD
- `Weapon`: 총기, 투사체, 피격 처리

---

## Current Status

### Completed

- Host 기준 로컬 플레이 흐름 구현
- 카드 기반 전투 강화 구조 구현
- 방 전진형 승리 규칙 구현
- 핵심 HUD와 메뉴 UI 구현

### In Progress or Missing

- Client 카드 선택 RPC
- Create Room / Find Room 실동작 연결
- 상태 이상 효과 구현
- HP 변경 콜백 기반 UI 갱신 보강
- 설정 화면

### Next Steps

- 2인 온라인 멀티플레이 실기기 테스트
- 로비 및 매칭 UX 구현
- 상태 이상 카드 추가
- 리스폰 직후 무적 시간 추가
- 피격, 폭발, 사망 VFX/SFX 보강
- 플레이어 아트, 애니메이션, 맵 비주얼 제작

---

## Getting Started

### Requirements

- Unity Hub
- `Unity 2022.3 LTS`
- Git LFS

### Setup

1. 저장소를 클론합니다.
2. `git lfs install`을 실행합니다.
3. Unity Hub에서 프로젝트를 엽니다.
4. 필요 시 `Fusion Hub`에서 Photon App ID를 설정합니다.

```bash
git clone https://github.com/worldclasscitizen/colosseum-private.git
git lfs install
```

### Scenes

| Scene | Build Index | Purpose |
|-------|-------------|---------|
| `MainMenu` | 0 | 메인 메뉴 |
| `SampleScene` | 1 | 플레이 테스트 씬 |

---

## Summary

이 프로젝트는 작은 범위의 프로토타입이지만, 단순한 2D 액션보다 한 단계 더 나아가  
`네트워크 전투`, `진영 압박형 룰`, `카드 기반 성장`, `코드 중심 UI 제작`을 한 저장소 안에서 보여주는 작업물입니다.

포트폴리오 관점에서는 다음 역량을 강조합니다.

- 실시간 멀티플레이 구조 이해
- Unity 기반 전투 시스템 설계
- 데이터 중심 카드 시스템 설계
- 기능 우선 프로토타이핑과 반복 개발
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    