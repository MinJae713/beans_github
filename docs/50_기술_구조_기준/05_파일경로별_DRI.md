---
last_updated: 2026-05-08
audience: ai-first
---

# 파일 경로별 DRI

> 상태: accepted. 2026-04-21 팀회의 T2에서 "제안 v3" 구조 승인 + 이름 확정. Unity 에셋/스크립트 경로별 주 책임자(DRI)를 매핑해 5인 동시 작업에서 씬/프리팹/스크립트 머지 충돌을 방지한다.
>
> **승격 기록**: 2026-04-21 T2 회의 결정 → `05/06` D49 등록 → accepted 승격.
>
> **A303-256 보강**: 2026-04-30 M2 전투 플레이어블 협업 구조를 반영해 전투/네트워크 판정 로직, 클라이언트 표현/연출, 공통 계약, 맵/카메라, 인프라/빌드 책임 경계를 보강한다.

팀원 AI가 "이 파일을 지금 수정해도 되는가"를 판단할 때 이 문서를 우선 참조한다.

## 목적

| 목적 | 근거 |
|---|---|
| 씬·프리팹·스크립트 머지 충돌 방지 | Unity YAML merge의 한계, 5인 병렬 작업 |
| GitLab CODEOWNERS 연동으로 자동 리뷰어 지정 | MR 리뷰 누락 방지 |
| AI 세션이 "누가 이 파일을 수정 중인가" 판단 근거 | 동시 수정 예방 |

## DRI 매트릭스

> DRI 이름은 아래 "확정 DRI 매트릭스" 섹션 참조 (2026-04-21 T2 승인). 본 섹션은 **경로 패턴·수정 권한·리뷰어 수** 운영 규칙을 정의.
> 더 구체적인 경로 패턴은 포괄 경로보다 우선한다. 예를 들어 `Scripts/Presentation/**`은 `Scripts/**` 일반 규칙이 아니라 Presentation행을 따른다.
> A303-298에서 추가한 VFX 전용 경로 행은 카드 효과 VFX 후속 구현을 위한 candidate routing supplement다. accepted DRI 변경 또는 실제 CODEOWNERS 변경은 별도 MR에서 확정한다.

| 경로 패턴 | 유형 | DRI 참조 | 수정 권한 | 리뷰어 수 |
|---|---|---|---|---|
| `unity/Assets/_Project/Scripts/NetworkGameplay/Player/**` | C# | Unity 코드 매트릭스 Player행 | DRI only | 1 |
| `unity/Assets/_Project/Scripts/NetworkGameplay/Weapon/**` | C# | Weapon행 | DRI only | 1 |
| `unity/Assets/_Project/Scripts/NetworkGameplay/Card/**` | C# | Card System행 | DRI only | 2 |
| `unity/Assets/_Project/Scripts/NetworkGameplay/Game/**` | C# | Game 규칙행 | DRI only | 1 |
| `unity/Assets/_Project/Scripts/NetworkGameplay/Network/**` | C# | Network/Infra행 | DRI only | 2 |
| `unity/Assets/_Project/Scripts/NetworkGameplay/Status/**` | C# | Player행(R) + Card System행(S, 효과 정책) | DRI only | 1 |
| `unity/Assets/_Project/Scripts/Contracts/**` | C# | Contracts행 | DRI only + 관련 로직/표현 DRI 리뷰 | 2 |
| `unity/Assets/_Project/Scripts/Presentation/**` | C# | Presentation행 | DRI only + 이벤트 계약 변경 시 Contracts 리뷰 | 1 |
| `unity/Assets/_Project/Scripts/Presentation/Vfx/**` | C# | VFX event consumer행 후보(A303-298) | DRI only + Presentation/Contracts 리뷰 | 2 |
| `unity/Assets/_Project/Scripts/NetworkGameplay/Camera/**` | C# | Camera행 | DRI only | 1 |
| `unity/Assets/_Project/Scripts/Presentation/UI/**` | C# | UI행 | DRI + 백업 1명 | 1 |
| `unity/Assets/_Project/Scripts/Presentation/UI/Vfx/**` | C# | VFX 한정 UI행 후보(A303-298) | DRI only + UI 리뷰 | 2 |
| `unity/Assets/_Project/Scripts/Editor/**` | C# | Editor행 | DRI only | 1 |
| `unity/Assets/_Project/Scripts/NetworkGameplay/Player/**` | C# | Player행 (별칭) | DRI only | 1 |
| `unity/Assets/_Project/Scripts/NetworkGameplay/Weapon/**` | C# | Weapon행 (별칭) | DRI only | 1 |
| `unity/Assets/_Project/Scripts/NetworkGameplay/Game/**` | C# | Game 규칙행 (별칭) | DRI only | 1 |
| `unity/Assets/_Project/Scripts/NetworkGameplay/Network/**` | C# | Network/Infra행 (별칭) | DRI only | 2 |
| `unity/Assets/_Project/Scripts/NetworkGameplay/Camera/**` | C# | Camera행 (별칭) | DRI only | 1 |
| `unity/Assets/_Project/Art/Greybox/**` | Asset | 30_전장 R(박병준) + Art R(우수연) co-DRI | D-15 시한부 한정 박병준 자유, post-demo Art R 정합화 | 1 |
| `worker-agent/**` | ASP.NET | Network/Infra행 | DRI only | 2 |
| `server/**` | Spring Boot | Infra/Backend행 | DRI only | 2 |
| `infra/localbuild/**` | Docker/Compose | Infra/Backend행 | DRI only | 1 |
| `infra/remotebuild/**` | Docker/Compose/Nginx/Remote deploy | Infra/Backend행 | DRI only | 1 |
| `infra/remotebuild/OnboardingPage/**` | Static web / Client distribution | Infra/Backend행 | DRI only | 1 |
| `unity/Assets/_Project/Scenes/**.unity` | Scene | 유민재 (Scenes 락 소유) | DRI only + 씬 락(§씬잠금) | 2 |
| `unity/Assets/_Project/Prefabs/**` | Prefab | 서브디렉토리 상속 | Base DRI only, Variant 자유 | 1 |
| `unity/Assets/_Project/Data/**` | SO | 서브디렉토리 상속 | 실험은 포크, main은 DRI | 2 |
| `unity/Assets/_Project/Data/Presentation/**` | SO/Asset | Presentation행 | DRI only + Contracts 연동 시 리뷰 | 1 |
| `unity/Assets/_Project/Data/Presentation/Vfx/**` | SO/Asset | VFX preset행 후보(A303-298) | DRI only + Presentation 리뷰 | 2 |
| `unity/Assets/_Project/Animations/**` · `Audio/**` · `Materials/**` · `UI/**` | Asset | 우수연 (S=박병준) | DRI only | 1 |
| 위 4개 경로 (D-15 시한부, D71 cascade) | Asset | v1.0-demo까지 박병준 R(이관, AD 위임) + 우수연 S co-DRI | 박병준 자유, post-demo 우수연 R 복귀 정합화 | 1 |
| `unity/ProjectSettings/NetworkProjectConfig.asset` | SO | 유민재 (게이트키퍼) | DRI only | 2 |
| `unity/ProjectSettings/AudioManager.asset` | Unity Settings | 유민재 (빌드/플랫폼 오디오 출력) | DRI only + 인게임 표현 영향 시 우수연 리뷰 | 2 |
| `docs/**` | Markdown | 작성자 또는 해당 섹션 R | 작성자 자유 + PR 리뷰 1명 | 1 |
| `unity/ProjectSettings/**` | Unity Settings | 유민재 / 박병준 | DRI only | 2 |
| `unity/Packages/manifest.json` | 패키지 | 유민재 | DRI only | 2 |

> **NetworkGameplay/** 별칭 보강 (2026-05-07, A303-280): 본 레포는 `Scripts/Player/`·`Scripts/Game/`·`Scripts/Camera/`·`Scripts/Network/` 외에 `Scripts/NetworkGameplay/Player/` 등 동치 경로가 병존한다. R/S 책임은 동일하되 경로 패턴만 추가. 향후 단일 경로로 정리 시 별도 D-번호 등록.

## 운영 규칙

### 씬/프리팹 잠금 (soft lock) — 2026-04-21 T2 결정

- **락 할당 규칙**: 본 문서의 DRI 매트릭스를 그대로 따른다. 씬/프리팹의 R(Responsible)이 락 소유권을 가지며, 수정 전 `[SCENE-LOCK]` Jira 서브태스크 생성 + Mattermost `#dev-scene-lock` 공지.
- 씬 수정 전에 Mattermost `#dev-scene-lock` 채널에 "시작" 선언.
- 완료·푸시 후 "완료" 선언.
- 누군가 잠근 상태에서 수정 필요 시 Nested Prefab 또는 Additive Scene 분리를 검토.
- 상세 프로토콜: `80_Jira_Git_운영_원칙/01_Jira_Git_컨벤션.md` §15.

### Prefab Variant 정책

- 공용 캐릭터 Base prefab은 DRI만 수정.
- 팀원별 Variant(예: `PlayerP1.prefab`, `PlayerP2.prefab`)는 소유자 자유 수정.
- Base의 콜라이더·물리·Animator 세팅을 바꾸면 모든 Variant에 영향 → DRI 승인 필수.
- Nested Prefab의 override 직렬화는 머지 충돌 주범. 가능하면 Variant로 분리.

### Data / Prefabs 서브디렉토리 DRI 상속 — 2026-04-21 T2 결정

`Assets/_Project/Data/` 와 `Assets/_Project/Prefabs/` 아래의 서브디렉토리는 **해당 서브 섹션의 DRI 담당자를 그대로 상속**한다.

- 현재 주요 경로: `Data/Cards/`는 Card System행 R(김범승), `Data/DevMode/`는 DevMode 작업 DRI, `GameplayProfile.asset`은 85_밸런스_프리셋 R(김범승)을 DRI로 본다.
- 향후: `Data/Weapons/`, `Data/Presentation/`, `Prefabs/UI/`, `Prefabs/Environment/` 등 신규 서브가 생기면 그 섹션의 DRI 담당자(R)를 자동 적용한다.
- 신규 서브디렉토리 생성 시 본 문서에 한 줄 추가 (작성자: 박병준).

### ScriptableObject 실험 분기

- `GameplayProfile_main.asset`을 직접 수정하지 않는다.
- 실험은 `GameplayProfile_experiment_{owner}_{YYYY-MM-DD}.asset` 포크.
- 채택 시 DRI가 main에 반영 + 변경로그 기록 ([85_밸런스_프리셋/30_변경로그/01_플레이테스트_변경로그.md](../85_밸런스_프리셋/30_변경로그/01_플레이테스트_변경로그.md)).

### CODEOWNERS 연동 — 2026-04-21 T2 결정

**자동 리뷰어 지정 규칙**: DRI 매트릭스의 **R(Responsible) ↔ S(Support) 상호 보완** 관계로 MR 리뷰어 자동 지정.

- **S가 MR을 올리면 R이 리뷰어로 자동 지정**된다.
- **R이 MR을 올리면 S(또는 A)가 리뷰어로 자동 지정**된다.
- 게이트 영역(§13.4 기준) 변경 시 A(Accountable) 추가 승인 필수.

DRI 이름 확정(본 승격)에 따라 박병준이 `.gitlab/CODEOWNERS` 파일에 경로 패턴 + DRI 매핑을 코드로 박는다. GitLab MR 자동 리뷰어 지정 활성화.

A303-298 문서 변경은 `.gitlab/CODEOWNERS` 파일을 수정하지 않는다. 아래 예시는 CODEOWNERS 정책을 설명하는 예시이며, VFX 전용 경로 예시는 후속 CODEOWNERS MR 전까지 실제 자동 리뷰어 지정 규칙으로 주장하지 않는다.

예시 (R↔S 상호 보완):
```
/unity/Assets/_Project/Scripts/NetworkGameplay/Player/**  @choi_jinwoo @kim_beomseung
/unity/Assets/_Project/Scripts/NetworkGameplay/Network/** @choi_jinwoo @yoo_minjae
/unity/Assets/_Project/Scripts/NetworkGameplay/Status/**  @choi_jinwoo @kim_beomseung
/worker-agent/**                       @choi_jinwoo @yoo_minjae
/server/**                             @yoo_minjae @choi_jinwoo
/infra/localbuild/**                   @yoo_minjae
/infra/remotebuild/**                  @yoo_minjae
/unity/Assets/_Project/Scripts/Contracts/** @kim_beomseung @choi_jinwoo @woo_suyeon
/unity/Assets/_Project/Scripts/Presentation/** @woo_suyeon @kim_beomseung
/unity/Assets/_Project/Scripts/Presentation/Vfx/** @kim_beomseung @woo_suyeon
/unity/Assets/_Project/Scripts/Presentation/UI/Vfx/** @kim_beomseung @woo_suyeon
/unity/Assets/_Project/Scripts/NetworkGameplay/Card/**    @kim_beomseung @park_byungjun
/unity/Assets/_Project/Scripts/Presentation/UI/**         @woo_suyeon @park_byungjun
/unity/Assets/_Project/Scenes/*.unity     @yoo_minjae @park_byungjun
/docs/                                     @park_byungjun
```

> 실제 GitLab 핸들은 박병준이 CODEOWNERS 작성 시 확정. A303-298에서 추가된 VFX 예시는 실제 CODEOWNERS 변경이 아니라 후보 문맥이다.

### Git LFS

다음 확장자는 LFS 트래킹 대상:
- `*.psd`, `*.fbx`, `*.blend`, `*.wav`, `*.mp3`, `*.mp4`, `*.png`(고해상도), `*.tga`

`.gitattributes` 에 Unity Smart Merge 활성화:
```
*.unity merge=unityyamlmerge
*.prefab merge=unityyamlmerge
*.asset merge=unityyamlmerge
```

## 충돌 발생 시 처리

1. 머지 충돌이 발생하면 커밋 전 양쪽 DRI에게 알린다.
2. 씬·프리팹은 자동 머지 시도하지 말고 수동 재작업.
3. 충돌 원인과 해소 방법을 작업 브리핑에 기록.
4. 반복되면 [05_문서관리_규칙/06_의사결정_로그](../05_문서관리_규칙/06_의사결정_로그.md) 에 소유권 재배정 검토.

## 미결정

- `Assets/_Project/` 루트 하위 최종 폴더 구조 — `80/01` v3.0 §2에 확정됨. 서브 신설 시 본 문서 매트릭스에 직접 추가 + `05/06` D-번호 등록.

### 2026-04-21 T2 회의에서 결정됨 (본 문서 accepted 승격)

- ✅ 구체 DRI 이름 확정 (아래 매트릭스)
- ✅ CODEOWNERS 연동 형식: R↔S 상호 보완 (작성 담당 박병준)
- ✅ 씬 잠금 선언 채널: Mattermost `#dev-scene-lock`
- ✅ Data/Prefabs 서브디렉토리 DRI 상속 규칙
- ✅ PR 리뷰어 자동 지정 (R↔S)

## DRI 변경 관리

DRI 변경은 **본 문서를 직접 수정 + `05/06 의사결정 로그`에 D-번호 등록**으로 추적한다. 별도 변경로그 폴더를 두지 않는다 (git log + D-번호로 충분).

변경 유형:
- 신규 폴더/에셋 추가 시 DRI 상속 기록 → 본 문서 매트릭스 1행 추가
- 담당자 변경 (이관·교체) → 본 문서 매트릭스 업데이트 + `05/06` D-번호
- R↔S 역할 스왑 → 동일

## 관련 문서

- [01_Unity_프로젝트_구조](01_Unity_프로젝트_구조.md) — 폴더 루트 규칙
- [03_씬_프리팹_소유권](03_씬_프리팹_소유권.md) — 씬/프리팹 운영 원칙
- [04_Fusion_구현_가드레일](04_Fusion_구현_가드레일.md) — 네트워크 파일 수정 금지선
- [80_Jira_Git_운영_원칙/01_Jira_Git_컨벤션](../80_Jira_Git_운영_원칙/01_Jira_Git_컨벤션.md) — 모노레포 루트 구조와 브랜치 규칙
- [05_문서관리_규칙/05_팀_작업구조와_역할_소유권](../05_문서관리_규칙/05_팀_작업구조와_역할_소유권.md)

## 확정 DRI 매트릭스 (2026-04-21 D49 승인, 2026-04-30 A303-256 보강)

> **상태**: accepted. 2026-04-21 팀회의 T2에서 기본 구조를 승인했고, 2026-04-30 A303-256에서 M2 전투 플레이어블 협업 구조에 맞춰 책임 경계를 보강했다.
> 특징: 최진우는 전투/네트워크 판정 로직, 우수연은 광범위 UI·캐릭터·공통 비주얼 표현, 김범승은 공통 계약·카드/전투 정책·QA, 박병준은 문서/운영 정합성·map 디자인·카메라, 유민재는 인프라·빌드·QA(A)를 맡는다. 카드 효과 VFX 세부 라우팅은 A303-298 candidate supplement로 검토한다. 본 문서의 R(Responsible) / S(Support) / A(Accountable) 매핑이 정본이다.

### 역할 정의 (확정)

| 이름 | 역할 | 주담당 |
|---|---|---|
| 박병준 | 문서/운영 정합성 + 맵/카메라 | 기준문서 운영, 의사결정 정리, 전장/맵 구조, map 디자인, 카메라 기준, 라운드 통합 검토 |
| 우수연 | 클라이언트 표현/연출 | 광범위 UI, HUD, SFX/오디오 표현, 애니메이션, 캐릭터 최종 sprite/material/animation, 공통 비주얼 톤, 피격/사망/발사 공통 표현 |
| 최진우 | 전투/네트워크 판정 로직 | Player/Weapon/Network 런타임, 이동·점프·발사·피격·반사·사망 판정, Fusion RPC·세션 |
| 김범승 | 공통 계약 + 카드/전투 정책 + QA 주도 | Contracts, 카드/GameplayProfile 수치 정책, 이벤트 payload 기준, 스모크·플레이테스트·게이트 판정 실행 주담당 (D58). 카드 효과 VFX 계약·preset 기준·VFX 한정 UI는 A303-298 candidate supplement로 검토 |
| 유민재 | Infra·Build·QA(A) | 빌드/CI/배포, 도메인·호스팅, worker-agent 실행 환경, Spring Boot/Redis 실행 환경, `infra/localbuild` Docker Compose, `infra/remotebuild` 원격 EC2 배포 자산, Scenes lock, 성능 모니터링 툴, QA 최종 책임·승인·외부 보고, 빌드/플랫폼 오디오 출력 문제 (D58) |

### docs 폴더 DRI 매트릭스 (RACI, 2026-04-30 A303-256 보강)

| 경로 | R (Responsible) | A (Accountable) | C (Consulted) |
|---|---|---|---|
| 00_프로젝트_비전/ | 박병준 | 박병준 | 전원 |
| 05_문서관리_규칙/ | 박병준 | 박병준 | 김범승(정합성 크로스체크) |
| **10_게임플레이_기준/** | 김범승 | 최진우 | **유민재(네트워크 요소 포함)** |
| **20_전투_밸런스_기준/** | **김범승(설계)** | **최진우(구현)** | 유민재 |
| 30_전장_레벨디자인/ | 박병준 | 박병준 | 최진우(판정), 김범승(QA), 유민재(씬 락) |
| **40_카드_무기_설계/** | 김범승 | 박병준 | 최진우(런타임), 우수연(표현 연동) |
| 45_피드백_UI_연출/ | 우수연(광범위 UI/캐릭터/공통 비주얼), 김범승(카드 효과 VFX/한정 UI 계약 후보) | 박병준 | 최진우(판정 이벤트), 유민재(플랫폼 오디오 출력) |
| **50_기술_구조_기준/** | 최진우(Network/Fusion), 유민재(빌드/CI), 김범승(Contracts/경계), 우수연(Presentation 구조) | 박병준 | 전원 |
| 60_AI_협업_운영_원칙/ | 박병준 | 박병준 | — |
| 70_검증_체크리스트/ | 김범승 | 유민재 | 전원 (QA 주도 R=김범승, A=유민재, D58 스왑) |
| 80_Jira_Git_운영_원칙/ | 박병준 | 박병준 | 유민재 |
| 85_밸런스_프리셋/ | 김범승 | 박병준 | 최진우 |
| 90_백로그/·99_아카이브/ | 박병준 | 박병준 | — |

### Unity 코드 DRI 매트릭스 (RSA 순서, 2026-04-30 A303-256 보강)

> RSA 순서는 회의 결정 표기 = **개발 담당** 우선순위. R(Responsible 주담당) / S(Support 부담당) / A(Assist 보조).

| 영역 | R (주담당) | S (부담당) | A (보조) |
|---|---|---|---|
| Player | 최진우(전투/네트워크 판정) | 우수연(표현), 김범승(계약/QA) | 박병준 |
| Weapon | 최진우(발사/탄도/피격 판정) | 우수연(표현), 김범승(계약/QA) | 박병준 |
| Card System 정책/데이터 | 김범승(정책/데이터 계약) | 최진우(런타임 적용), 우수연(표현 연동) | 박병준 |
| Card Runtime/RPC/RNG | 최진우(런타임·RNG 권한) | 김범승(정책/검증), 우수연(표현 연동) | 박병준 |
| Game 규칙 | 최진우(런타임 판정) / 김범승(정책·QA) | 박병준 | — |
| Network/Infra | 최진우(런타임) / 유민재(실행 환경) | 박병준 | — |
| Contracts | 김범승 | 최진우(판정 로직), 우수연(표현 소비), 유민재(네트워크/빌드 영향) | 박병준 |
| Presentation | 우수연 | 김범승(이벤트 계약), 최진우(판정 이벤트) | 박병준 |
| VFX event consumer / VFX 한정 UI 후보(A303-298) | 김범승 | 우수연(공통 비주얼 톤), 최진우(판정 이벤트) | 박병준 |
| Camera | 박병준 | 최진우 | — |
| UI | 우수연 | 박병준 | — |
| Editor (Build Tool) | 유민재 | 박병준 | — |
| Build/Platform Audio Output | 유민재 | 우수연(인게임 표현 영향 확인), 박병준 | — |
| Animations / Audio / Materials / UI 에셋 | 우수연 | 박병준 | — |
| Data / | 서브디렉토리 DRI 상속 (§Data 섹션 참조) | — | — |
| Prefabs / | 서브디렉토리 DRI 상속 (§Prefabs 섹션 참조) | — | — |

### 박병준 분담 지원 규칙

- **문서/운영 정합성**: 기준문서 구조, 의사결정 로그, CODEOWNERS/DRI 운영 기준을 관리한다.
- **맵/카메라**: 전장 구조, map 디자인, 카메라 스케일·이동 기준, 라운드 통합 관점을 관리한다.
- **카드 정책 분리**: 카드/전투 수치 정책과 SO 스키마는 김범승이 관리하고, 런타임 적용은 최진우, 표현 연동은 우수연과 함께 검토한다.

### 최적화 공동 담당 규칙 (W4/W5)

- **네트워크 최적화 (W4)**: 최진우 + 김범승 공동. Jira 스토리 2개(동일 제목, assignee 다름)로 split
- **성능 최적화 (W5)**: 최진우 + 김범승 공동. Jira 스토리 2개로 split
