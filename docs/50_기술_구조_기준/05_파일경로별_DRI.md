---
last_updated: 2026-04-20
audience: ai-first
---

# 파일 경로별 DRI

> 상태: accepted. 2026-04-21 팀회의 T2에서 "제안 v3" 구조 승인 + 이름 확정. Unity 에셋/스크립트 경로별 주 책임자(DRI)를 매핑해 5인 동시 작업에서 씬/프리팹/스크립트 머지 충돌을 방지한다.
>
> **승격 기록**: 2026-04-21 T2 회의 결정 → `05/06` D49 등록 → accepted 승격.

팀원 AI가 "이 파일을 지금 수정해도 되는가"를 판단할 때 이 문서를 우선 참조한다.

## 목적

| 목적 | 근거 |
|---|---|
| 씬·프리팹·스크립트 머지 충돌 방지 | Unity YAML merge의 한계, 5인 병렬 작업 |
| GitLab CODEOWNERS 연동으로 자동 리뷰어 지정 | MR 리뷰 누락 방지 |
| AI 세션이 "누가 이 파일을 수정 중인가" 판단 근거 | 동시 수정 예방 |

## DRI 매트릭스

> DRI 이름은 아래 "확정 DRI 매트릭스" 섹션 참조 (2026-04-21 T2 승인). 본 섹션은 **경로 패턴·수정 권한·리뷰어 수** 운영 규칙을 정의.

| 경로 패턴 | 유형 | DRI 참조 | 수정 권한 | 리뷰어 수 |
|---|---|---|---|---|
| `unity/Assets/_Project/Scripts/Player/**` | C# | Unity 코드 매트릭스 Player행 | DRI only | 1 |
| `unity/Assets/_Project/Scripts/Weapon/**` | C# | Weapon행 | DRI only | 1 |
| `unity/Assets/_Project/Scripts/Card/**` | C# | Card System행 | DRI only | 2 |
| `unity/Assets/_Project/Scripts/Game/**` | C# | Game 규칙행 | DRI only | 1 |
| `unity/Assets/_Project/Scripts/Network/**` | C# | Network 인프라행 | DRI only | 2 |
| `unity/Assets/_Project/Scripts/Camera/**` | C# | Camera행 | DRI only | 1 |
| `unity/Assets/_Project/Scripts/UI/**` | C# | UI행 | DRI + 백업 1명 | 1 |
| `unity/Assets/_Project/Scripts/Editor/**` | C# | Editor행 | DRI only | 1 |
| `unity/Assets/_Project/Scenes/**.unity` | Scene | 유민재 (Scenes 락 소유) | DRI only + 씬 락(§씬잠금) | 2 |
| `unity/Assets/_Project/Prefabs/**` | Prefab | 서브디렉토리 상속 | Base DRI only, Variant 자유 | 1 |
| `unity/Assets/_Project/Data/**` | SO | 서브디렉토리 상속 | 실험은 포크, main은 DRI | 2 |
| `unity/Assets/_Project/Animations/**` · `Audio/**` · `Materials/**` · `UI/**` | Asset | 우수연 (S=박병준) | DRI only | 1 |
| `unity/ProjectSettings/NetworkProjectConfig.asset` | SO | 유민재 (게이트키퍼) | DRI only | 2 |
| `docs/**` | Markdown | 작성자 또는 해당 섹션 R | 작성자 자유 + PR 리뷰 1명 | 1 |
| `unity/ProjectSettings/**` | Unity Settings | 유민재 / 박병준 | DRI only | 2 |
| `unity/Packages/manifest.json` | 패키지 | 유민재 | DRI only | 2 |

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

- 현재: `Data/Cards/` 만 존재 → Card 시스템의 R(김범승)를 DRI로 상속.
- 향후: `Data/Weapons/`, `Prefabs/UI/`, `Prefabs/Environment/` 등 신규 서브가 생기면 그 섹션의 DRI 담당자(R)를 자동 적용.
- 신규 서브디렉토리 생성 시 본 문서에 한 줄 추가 (작성자: 박병준).

### ScriptableObject 실험 분기

- `BalanceProfile_main.asset`을 직접 수정하지 않는다.
- 실험은 `BalanceProfile_experiment_{owner}_{YYYY-MM-DD}.asset` 포크.
- 채택 시 DRI가 main에 반영 + 변경로그 기록 ([85_밸런스_프리셋/30_변경로그/01_플레이테스트_변경로그.md](../85_밸런스_프리셋/30_변경로그/01_플레이테스트_변경로그.md)).

### CODEOWNERS 연동 — 2026-04-21 T2 결정

**자동 리뷰어 지정 규칙**: DRI 매트릭스의 **R(Responsible) ↔ S(Support) 상호 보완** 관계로 MR 리뷰어 자동 지정.

- **S가 MR을 올리면 R이 리뷰어로 자동 지정**된다.
- **R이 MR을 올리면 S(또는 A)가 리뷰어로 자동 지정**된다.
- 게이트 영역(§13.4 기준) 변경 시 A(Accountable) 추가 승인 필수.

DRI 이름 확정(본 승격)에 따라 박병준이 `.gitlab/CODEOWNERS` 파일에 경로 패턴 + DRI 매핑을 코드로 박는다. GitLab MR 자동 리뷰어 지정 활성화.

예시 (R↔S 상호 보완):
```
/unity/Assets/_Project/Scripts/Player/**  @choi_jinwoo @kim_beomseung
/unity/Assets/_Project/Scripts/Network/** @choi_jinwoo @yoo_minjae
/unity/Assets/_Project/Scripts/Card/**    @kim_beomseung @park_byungjun
/unity/Assets/_Project/Scripts/UI/**      @woo_suyeon @park_byungjun
/unity/Assets/_Project/Scenes/*.unity     @yoo_minjae @park_byungjun
/docs/                                     @park_byungjun
```

> 실제 GitLab 핸들은 박병준이 CODEOWNERS 작성 시 확정.

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
- [05_문서관리_규칙/05_팀_작업구조와_역할_소유권](../05_문서관리_규칙/05_팀_작업구조와_역할_소유권.md)

## 확정 DRI 매트릭스 (2026-04-21 T2 회의 승인, D49 등록)

> **상태**: accepted. 2026-04-21 팀회의 T2에서 아래 구조 전체 승인.
> 특징: 유민재가 네트워크에서 빠지고 최진우가 전담한다(최진우가 이미 `NetworkManager`/`NetworkInputData`/`InputProvider`/Player·Gun NetworkBehaviour 구현 완료). 김범승은 카드 구현 + 네트워크/성능 최적화를 공동 담당한다. 회의 결정 RSA 순서는 "개발 담당" 표시이며, 본 문서의 R(Responsible) / S(Support) / A(Accountable) 매핑이 정본.

### 역할 정의 (확정)

| 이름 | 역할 | 주담당 |
|---|---|---|
| 박병준 | PM·Tech Lead + AD·GP 분담 | 카드 시스템 설계, 의사결정, 라운드 통합 |
| 우수연 | AD | VFX, UI, HUD, 연출 |
| 최진우 | Gameplay A + Network Lead | Player/Weapon/Network 전체, Fusion RPC·세션 |
| 김범승 | Gameplay B + 최적화 + QA 주도 | HP/사망, 카드 구현, 네트워크·성능 최적화 공동, **QA 주도(R) — 스모크·플레이테스트·게이트 판정 실행 주담당 (D58)** |
| 유민재 | Infra·Build·QA(A) | 빌드/CI/배포, 도메인·호스팅, Scenes lock, 성능 모니터링 툴, **QA Accountable — 최종 책임·승인·외부 보고 (D58)** |

### docs 폴더 DRI 매트릭스 (RACI, 2026-04-21 T2 확정)

| 경로 | R (Responsible) | A (Accountable) | C (Consulted) |
|---|---|---|---|
| 00_프로젝트_비전/ | 박병준 | 박병준 | 전원 |
| 05_문서관리_규칙/ | 박병준 | 박병준 | — |
| **10_게임플레이_기준/** | 김범승 | 최진우 | **유민재(네트워크 요소 포함)** |
| **20_전투_밸런스_기준/** | **김범승(설계)** | **최진우(구현)** | 유민재 |
| 30_전장_레벨디자인/ | 김범승 | 최진우 | 유민재 |
| **40_카드_무기_설계/** | 김범승 | **박병준(구현)** | 최진우 |
| 45_피드백_UI_연출/ | 우수연 | 박병준 | — |
| **50_기술_구조_기준/** | 최진우(Network/Fusion 가드레일), 유민재(빌드/CI) | 박병준 | 전원 |
| 60_AI_협업_운영_원칙/ | 박병준 | 박병준 | — |
| 70_검증_체크리스트/ | 김범승 | 유민재 | 전원 (QA 주도 R=김범승, A=유민재, D58 스왑) |
| 80_Jira_Git_운영_원칙/ | 박병준 | 박병준 | 유민재 |
| 85_밸런스_프리셋/ | 김범승 | 박병준 | 최진우 |
| 90_백로그/·99_아카이브/ | 박병준 | 박병준 | — |

### Unity 코드 DRI 매트릭스 (RSA 순서, 2026-04-21 T2 확정)

> RSA 순서는 회의 결정 표기 = **개발 담당** 우선순위. R(Responsible 주담당) / S(Support 부담당) / A(Assist 보조).

| 영역 | R (주담당) | S (부담당) | A (보조) |
|---|---|---|---|
| Player | 최진우 | 김범승 | 박병준 |
| Weapon | 최진우 | 김범승 | 박병준 |
| Card System | 김범승 | 박병준 | 최진우 |
| Game 규칙 | 최진우 | 김범승 | 박병준 |
| Network 인프라 | 최진우 / 유민재 | 박병준 | — |
| Camera | 박병준 | 최진우 | — |
| UI | 우수연 | 박병준 | — |
| Editor (Build Tool) | 유민재 | 박병준 | — |
| Animations / Audio / Materials / UI 에셋 | 우수연 | 박병준 | — |
| Data / | 서브디렉토리 DRI 상속 (§Data 섹션 참조) | — | — |
| Prefabs / | 서브디렉토리 DRI 상속 (§Prefabs 섹션 참조) | — | — |

### 박병준 분담 지원 규칙

- **AD 지원**: MMFeedbacks 스펙 정의 + 카드 UI 연출 디렉션
- **GP 지원**: 라운드 상태머신 통합 + 카드 시스템 코어 설계
- **카드 "설계 vs 구현" 분리**: 박병준(구현 — 회의 결정) / 김범승(설계·SO 스키마·효과 분류·밸런스 프레임)

### 최적화 공동 담당 규칙 (W4/W5)

- **네트워크 최적화 (W4)**: 최진우 + 김범승 공동. Jira 스토리 2개(동일 제목, assignee 다름)로 split
- **성능 최적화 (W5)**: 최진우 + 김범승 공동. Jira 스토리 2개로 split
