---
last_updated: 2026-04-20
audience: ai-first
---

# 파일 경로별 DRI

> 상태: candidate. 이 문서는 Unity 에셋/스크립트 경로별 주 책임자(DRI)를 매핑해 5인 동시 작업에서 씬/프리팹/스크립트 머지 충돌을 방지한다. 실제 DRI 배정은 팀 회의(W1, 2026-04-20 주간)에서 확정한다.

팀원 AI가 "이 파일을 지금 수정해도 되는가"를 판단할 때 이 문서를 우선 참조한다.

## 목적

| 목적 | 근거 |
|---|---|
| 씬·프리팹·스크립트 머지 충돌 방지 | Unity YAML merge의 한계, 5인 병렬 작업 |
| GitLab CODEOWNERS 연동으로 자동 리뷰어 지정 | MR 리뷰 누락 방지 |
| AI 세션이 "누가 이 파일을 수정 중인가" 판단 근거 | 동시 수정 예방 |

## DRI 매트릭스 (초안)

DRI 이름은 팀 회의에서 확정한다. 현재는 경로 패턴과 운영 규칙만 고정.

| 경로 패턴 | 유형 | DRI | 수정 권한 | 리뷰어 수 |
|---|---|---|---|---|
| `Assets/_Project/Scripts/Gameplay/**` | C# | TBD | DRI only | 1 |
| `Assets/_Project/Scripts/Network/**` | C# | TBD | DRI only | 2 |
| `Assets/_Project/Scripts/UI/**` | C# | TBD | DRI + 백업 1명 | 1 |
| `Assets/_Project/Scripts/Config/**` | C# | TBD | DRI only | 1 |
| `Assets/_Project/Scripts/Domain/**` | C# | TBD | DRI only | 1 |
| `Assets/_Project/Scenes/Main_Root.unity` | Scene | TBD | DRI only + 잠금 | 2 |
| `Assets/_Project/Scenes/Arena_Duel.unity` | Scene | TBD | DRI only + 잠금 | 2 |
| `Assets/_Project/Scenes/UI_HUD.unity` | Scene | TBD | DRI only + 잠금 | 1 |
| `Assets/_Project/Prefabs/Characters/**` | Prefab | TBD | Base는 DRI only, Variant는 소유자 자유 | 1 |
| `Assets/_Project/Prefabs/Projectiles/**` | Prefab | TBD | DRI only | 1 |
| `Assets/_Project/Prefabs/UI/**` | Prefab | TBD | DRI only | 1 |
| `Assets/_Project/Art/**` | Asset | TBD | DRI only | 1 |
| `Assets/_Project/Audio/**` | Asset | TBD | DRI only | 1 |
| `Assets/_Project/Config/BalanceProfile_main.asset` | SO | TBD | 실험은 포크, main은 DRI | 2 |
| `Assets/_Project/Config/NetworkProjectConfig.asset` | SO | TBD | DRI only | 2 |
| `docs/**` | Markdown | 작성자 또는 DRI | 작성자 자유 + PR 리뷰 1명 | 1 |
| `ProjectSettings/**` | Unity Settings | TBD | DRI only | 2 |
| `Packages/manifest.json` | 패키지 | TBD | DRI only | 2 |

## 운영 규칙

### 씬 잠금 (soft lock)

- 씬 수정 전에 Slack/Discord/Jira 상태 등에 "시작" 선언.
- 완료·푸시 후 "완료" 선언.
- 누군가 잠근 상태에서 수정 필요 시 Nested Prefab 또는 Additive Scene 분리를 검토.

### Prefab Variant 정책

- 공용 캐릭터 Base prefab은 DRI만 수정.
- 팀원별 Variant(예: `PlayerP1.prefab`, `PlayerP2.prefab`)는 소유자 자유 수정.
- Base의 콜라이더·물리·Animator 세팅을 바꾸면 모든 Variant에 영향 → DRI 승인 필수.
- Nested Prefab의 override 직렬화는 머지 충돌 주범. 가능하면 Variant로 분리.

### ScriptableObject 실험 분기

- `BalanceProfile_main.asset`을 직접 수정하지 않는다.
- 실험은 `BalanceProfile_experiment_{owner}_{YYYY-MM-DD}.asset` 포크.
- 채택 시 DRI가 main에 반영 + 변경로그 기록 ([85_밸런스_프리셋/30_변경로그/01_플레이테스트_변경로그.md](../85_밸런스_프리셋/30_변경로그/01_플레이테스트_변경로그.md)).

### CODEOWNERS 연동

DRI 확정 후 `CODEOWNERS` 파일에 경로 패턴 + DRI 매핑을 코드로 박는다. GitLab MR 자동 리뷰어 지정 활성화.

예시:
```
/Assets/_Project/Scripts/Gameplay/** @dri_gameplay
/Assets/_Project/Scripts/Network/** @dri_network @review_backup
/Assets/_Project/Scenes/Main_Root.unity @dri_scene
```

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

- 구체 DRI 이름 — 팀 회의(W1)
- CODEOWNERS 연동 형식 (GitLab MR 자동 지정 플러그인 필요 여부)
- 씬 잠금 선언 채널 (Slack vs Discord vs Jira status)
- `Assets/_Project/` 루트 하위 최종 폴더 구조 ([01_Unity_프로젝트_구조](01_Unity_프로젝트_구조.md) 확정 후)

## 관련 문서

- [01_Unity_프로젝트_구조](01_Unity_프로젝트_구조.md) — 폴더 루트 규칙
- [03_씬_프리팹_소유권](03_씬_프리팹_소유권.md) — 씬/프리팹 운영 원칙
- [04_Fusion_구현_가드레일](04_Fusion_구현_가드레일.md) — 네트워크 파일 수정 금지선
- [05_문서관리_규칙/05_팀_작업구조와_역할_소유권](../05_문서관리_규칙/05_팀_작업구조와_역할_소유권.md)

## 제안 (2026-04-20, 회의 전 초안 v3)

> **상태**: 팀 회의 전 Jun 초안. W1 Exit Criteria ③(DRI 배정 확정)의 대상.
> 특징: 유민재가 네트워크에서 빠지고 최진우가 전담한다. 최진우가 이미 `NetworkManager`/`NetworkInputData`/`InputProvider`/Player·Gun NetworkBehaviour 구현을 완료했기 때문. 김범승은 카드 구현 + 네트워크/성능 최적화를 공동 담당한다.

### 역할 정의 v3

| 이름 | 역할 | 주담당 |
|---|---|---|
| 박병준 | PM·Tech Lead + AD·GP 분담 | 카드 시스템 설계, 의사결정, 라운드 통합 |
| 우수연 | AD | VFX, UI, HUD, 연출 |
| 최진우 | Gameplay A + Network Lead | Player/Weapon/Network 전체, Fusion RPC·세션 |
| 김범승 | Gameplay B + 최적화 | HP/사망, 카드 구현, 네트워크·성능 최적화 공동 |
| 유민재 | Infra·Build·QA | 빌드/CI/배포, 도메인·호스팅, QA 주도, Scenes lock, 성능 모니터링 툴 |

### docs 폴더 DRI 매트릭스

| 경로 | R (Responsible) | A (Accountable) | C (Consulted) |
|---|---|---|---|
| 00_프로젝트_비전/ | 박병준 | 박병준 | 전원 |
| 05_문서관리_규칙/ | 박병준 | 박병준 | — |
| 10_게임플레이_기준/ | 최진우 | 박병준 | 김범승 |
| 20_전투_밸런스_기준/ | 김범승 | 박병준 | 최진우, 유민재 |
| 30_전장_레벨디자인/ | 우수연 | 박병준 | 최진우 |
| 40_카드_무기_설계/ | 박병준(설계)+김범승(구현가이드) | 박병준 | 최진우 |
| 45_피드백_UI_연출/ | 우수연 | 박병준 | 박병준, 김범승 |
| 50_기술_구조_기준/ | 최진우(Network/Fusion 가드레일), 유민재(빌드/CI) | 박병준 | 전원 |
| 60_AI_협업_운영_원칙/ | 박병준 | 박병준 | — |
| 70_검증_체크리스트/ | 유민재 | 박병준 | 전원 |
| 80_Jira_Git_운영_원칙/ | 박병준 | 박병준 | 유민재 |
| 85_밸런스_프리셋/ | 박병준+김범승(카드 가중치) | 박병준 | 최진우 |
| 90_백로그/·99_아카이브/ | 박병준 | 박병준 | — |

### Assets/_Project 코드·자산 DRI 매트릭스

| 경로 | R (primary) | Support | A |
|---|---|---|---|
| Characters/Player/ | 최진우 | 박병준 | 박병준 |
| Characters/Weapons/ (Gun, Bullet) | 최진우 | 박병준, 김범승 | 박병준 |
| Systems/HP_Death/ | 김범승 | 박병준 | 박병준 |
| Systems/Cards/ (구현) | 김범승 | 박병준 (SO 설계) | 박병준 |
| Systems/Rounds/ (RoomManager) | 최진우 + 김범승 | 박병준 | 박병준 |
| Network/ (NetworkManager/Input) | 최진우 | 박병준 | 박병준 |
| Scenes/ | 유민재 | — | 박병준 |
| UI/HUD/ | 우수연 | 박병준 | 박병준 |
| VFX/ | 우수연 | 박병준 | 박병준 |
| Audio_MMFeedbacks/ | 우수연 | 박병준 | 박병준 |
| Editor/ (Odin SO) | 박병준 | — | 박병준 |
| Presets/ (BalanceProfile) | 박병준 | 김범승 | 박병준 |
| Build_CI/ (빌드 스크립트) | 유민재 | — | 박병준 |

### 박병준 분담 지원 규칙

- **AD 지원**: MMFeedbacks 스펙 정의 + 카드 UI 연출 디렉션
- **GP 지원**: 라운드 상태머신 통합 + 카드 시스템 코어 설계
- **카드 "설계 vs 구현" 분리**: 박병준(SO 스키마·효과 분류·밸런스 프레임) / 김범승(효과 런타임·카드 선택→라운드 적용)

### 최적화 공동 담당 규칙 (W4/W5)

- **네트워크 최적화 (W4)**: 최진우 + 김범승 공동. Jira 스토리 2개(동일 제목, assignee 다름)로 split
- **성능 최적화 (W5)**: 최진우 + 김범승 공동. Jira 스토리 2개로 split

### 회의 조정 예상 항목

- Systems/Rounds primary 최종 결정 (최진우 단독 vs 공동)
- 20_전투_밸런스 C에 포함된 최진우·유민재 역할 구분
- Characters/Weapons의 반사탄 구현이 Systems/Cards와 중첩되는 부분
- 유민재의 50_기술_구조_기준 내 R 범위 (빌드 문서만인지, [50/02 네트워크 truth 표](02_네트워크_truth_표.md)도 포함인지)
