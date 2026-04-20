# Colloseum — AI Agent Entry Point

이 레포는 Colloseum(SSAFY 자율 A303) 프로젝트다. 팀원 5명이 각자의 AI(Claude/Codex/Gemini 등)를 활용해 개발하며, 모든 AI 세션은 `docs/` 기준문서를 근거로 작업한다.

이 파일은 Codex CLI 등 AGENTS.md를 우선 로드하는 에이전트의 엔트리 포인트다. Claude Code 세션은 `CLAUDE.md`를 본다. 두 파일은 같은 내용을 공유한다.

## 먼저 읽어라 (AI 세션 시작 시 필수)

| 순서 | 문서 | 목적 |
|---|---|---|
| 1 | [docs/README.md](docs/README.md) | 현재 SOT 선언, 문서 상태 체계 |
| 2 | [docs/05_문서관리_규칙/08_문서_상태_인덱스.md](docs/05_문서관리_규칙/08_문서_상태_인덱스.md) | 현재 binding 문서 목록 |
| 3 | [docs/00_프로젝트_비전/01_프로젝트_비전.md](docs/00_프로젝트_비전/01_프로젝트_비전.md) | 게임 정체성 |
| 4 | [docs/90_백로그/04_미결정_질문_레지스터.md](docs/90_백로그/04_미결정_질문_레지스터.md) | 멈춰야 할 질문 |

작업 유형별 추가 문서는 [docs/60_AI_협업_운영_원칙/04_작업유형별_참조_매트릭스.md](docs/60_AI_협업_운영_원칙/04_작업유형별_참조_매트릭스.md)에서 확인한다.

## 프로젝트 컨텍스트 요약

| 항목 | 내용 |
|---|---|
| 장르 | 2D 대전 (Nidhogg × ROUNDS Fusion) |
| 엔진 | Unity 2022 LTS |
| 네트워크 | Photon Fusion 2 (네이티브 자체 구현) |
| 팀 | 5인 |
| 데드라인 | 2026-05-20 |
| 현재 SOT 루트 | `docs/` |
| GitLab | https://lab.ssafy.com/s14-final/S14P31A303 |

상세는 [docs/00_프로젝트_비전/01_프로젝트_비전.md](docs/00_프로젝트_비전/01_프로젝트_비전.md).

## 작업 시작 전 체크리스트

1. 대상 문서의 상태(`accepted` / `accepted_candidate` / `candidate`)를 확인했는가.
2. 미결정 질문 레지스터에 해당 주제가 `blocked`로 걸려 있지 않은가.
3. 작업 브리핑(참조 문서, 임시 가정, 중단 조건, 검증 기준)을 준비했는가.
4. 수정 대상 파일의 DRI를 확인했는가 ([docs/50_기술_구조_기준/05_파일경로별_DRI.md](docs/50_기술_구조_기준/05_파일경로별_DRI.md)).

체크리스트 통과 전에 코드·에셋·공용 프리팹·네트워크 설정 수정을 시작하지 않는다.

## 절대 하지 마라

| 금지 항목 | 이유 |
|---|---|
| secret · App ID · 개인 절대경로를 기준문서/MR/커밋에 실제 값으로 적기 | 자격증명 유출 |
| 3D Rigidbody · 3D Collider · ConfigurableJoint 기반 신규 전투 확장 | 프로젝트 2D 합의 및 래그돌 포기 결정 위반 |
| `FindObjectOfType` 을 `FixedUpdateNetwork()` 내부 hot path에서 사용 | 네트워크 상태 소유권 흐려짐 |
| `Time.deltaTime`, `Time.time` 을 `FixedUpdateNetwork()` 내부에서 사용 | Fusion 결정론 깨짐, desync |
| `accepted` 문서와 충돌하는 구현을 `candidate` 문서만 근거로 진행 | 상태 우선순위 위반 |
| Host 전용 임시동작을 기준 구현으로 인정 | Server Mode 전환 불가 |

상세 구현 금지선은 [docs/50_기술_구조_기준/04_Fusion_구현_가드레일.md](docs/50_기술_구조_기준/04_Fusion_구현_가드레일.md) 를 읽는다.

## 작업 브리핑 양식

작업 브리핑 없이 코드 수정에 진입하지 않는다. 양식은 [docs/60_AI_협업_운영_원칙/02_작업_브리핑_양식.md](docs/60_AI_협업_운영_원칙/02_작업_브리핑_양식.md) 를 따른다.

## 결과 보고 기준

- 수정한 파일 경로(절대 또는 repo-relative)를 모두 나열한다.
- 스모크 테스트 결과는 [docs/70_검증_체크리스트/01_스모크테스트.md](docs/70_검증_체크리스트/01_스모크테스트.md) 항목별로 Pass/Fail로 기록한다.
- Fusion 관련 작업은 [docs/70_검증_체크리스트/05_Fusion_로컬_2인_리허설.md](docs/70_검증_체크리스트/05_Fusion_로컬_2인_리허설.md) 리허설 여부를 명시한다.

## AI 세션에서 멈춰야 할 신호

- 필요한 정보가 `accepted` 문서에 없고 `candidate`끼리 상충한다.
- 미결정 질문 레지스터에서 해당 주제가 `blocked` 상태다.
- 작업이 기준문서의 금지선(위 표)에 닿는다.

이 경우 사용자에게 질문으로 반환하고 자의적으로 진행하지 않는다.
