# Colosseum — Claude Code Entry Point

이 레포는 Colosseum(SSAFY 자율 A303) 프로젝트다. 팀원 5명이 각자의 AI(Claude/Codex/Gemini 등)를 활용해 개발하며, 모든 AI 세션은 `docs/` 기준문서를 근거로 작업한다.

**전체 엔트리 지침은 [AGENTS.md](AGENTS.md) 와 동일하다.** 이 파일은 Claude Code가 자동 로드하는 위치 때문에 중복 유지한다.

## 먼저 읽어라

| 순서 | 문서 |
|---|---|
| 1 | [docs/README.md](docs/README.md) |
| 2 | [docs/05_문서관리_규칙/08_문서_상태_인덱스.md](docs/05_문서관리_규칙/08_문서_상태_인덱스.md) |
| 3 | [docs/00_프로젝트_비전/01_프로젝트_비전.md](docs/00_프로젝트_비전/01_프로젝트_비전.md) |
| 4 | [docs/90_백로그/04_미결정_질문_레지스터.md](docs/90_백로그/04_미결정_질문_레지스터.md) |

## 프로젝트 컨텍스트

- 장르: 2D 대전 (Nidhogg × ROUNDS Fusion)
- 엔진: Unity 2022 LTS + Photon Fusion 2 네이티브
- 팀: 5인, 데드라인 2026-05-20
- SOT 루트: `docs/`
- GitLab: https://lab.ssafy.com/s14-final/S14P31A303

## Claude Code 특화 운영

- 사용자 글로벌 지침은 `~/.claude/CLAUDE.md` 를 따른다.
- MCP 서버 설정은 각 작업자의 로컬 Claude Code 설정(사용자 home 하위)에 둔다. 이 레포 특화 MCP는 현재 없음. (개인 경로 금지선: `05/01` 기준문서 작성 금지선 §기준문서 참조)
- OMC/triflux 레이어 사용 시에도 이 문서의 금지선(아래)은 동일하게 적용.
- 작업 브리핑 양식: [docs/60_AI_협업_운영_원칙/02_작업_브리핑_양식.md](docs/60_AI_협업_운영_원칙/02_작업_브리핑_양식.md)

## 절대 하지 마라 (요약)

| 금지 | 상세 근거 |
|---|---|
| secret · App ID · 개인 절대경로 기록 | [50/04 Fusion 가드레일](docs/50_기술_구조_기준/04_Fusion_구현_가드레일.md) |
| 3D 물리 신규 확장 | [50/04 Fusion 가드레일](docs/50_기술_구조_기준/04_Fusion_구현_가드레일.md) |
| `FindObjectOfType` hot path 사용 | 동일 |
| `Time.deltaTime` in `FixedUpdateNetwork()` | 동일, 실전 5대장 섹션 |
| `accepted` 대신 `candidate` 우선 | [docs/README.md](docs/README.md) SOT 선언 |

전체 항목은 [AGENTS.md](AGENTS.md) 참조.

## 작업 시작 전 체크리스트

1. 대상 문서 상태 확인
2. 미결정 질문 레지스터 `blocked` 없음 확인
3. 작업 브리핑 준비
4. DRI 확인: [docs/50_기술_구조_기준/05_파일경로별_DRI.md](docs/50_기술_구조_기준/05_파일경로별_DRI.md)

## 결과 보고 · 스모크 테스트

- 수정 파일 경로 전부 나열
- 스모크 테스트: [docs/70_검증_체크리스트/01_스모크테스트.md](docs/70_검증_체크리스트/01_스모크테스트.md)
- Fusion 작업: [docs/70_검증_체크리스트/05_Fusion_로컬_2인_리허설.md](docs/70_검증_체크리스트/05_Fusion_로컬_2인_리허설.md)

## 세션에서 멈춰야 할 때

- 필요한 정보가 `accepted`에 없고 `candidate`끼리 상충
- 미결정 질문 레지스터에서 해당 주제 `blocked`
- 작업이 금지선에 닿음

→ 사용자에게 반환. 자의적으로 진행하지 않는다.
