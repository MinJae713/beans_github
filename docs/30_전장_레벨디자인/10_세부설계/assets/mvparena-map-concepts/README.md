# MVPArena Map Concepts — 시각 mockup (v0.1)

[../../03_MVPArena_비주얼_컨셉.md](../../03_MVPArena_비주얼_컨셉.md) v0.1.1 §7 자산 라이브러리의 1차 시각 후보. Claude Design (claude.ai/design) 출처, 2026-05-10 박병준 의뢰 → Variant A · ROUNDS-LEAN 1차 선택 + 지형 톤 진중하게 조정 완료.

> 본 mockup은 시각 reference (candidate)다. T1 자산 작업 진입 시 우수연 검수 + 박병준 Unity 자산화. 게임플레이 fact 충돌 시 [../../../../10_게임플레이_기준/02_라운드_상태머신.md](../../../../10_게임플레이_기준/02_라운드_상태머신.md), [../../02_MVP_첫_전장_명세.md](../../02_MVP_첫_전장_명세.md) 우선.

## 보기

브라우저에서 `index.html` 열기. React 18 + Babel standalone CDN 의존 (오프라인 첫 실행 시 CDN fetch 필요).

브라우저 `file://` 스킴 CORS 제약 회피용 로컬 정적 서버 권장:

```
cd docs/30_전장_레벨디자인/10_세부설계/assets/mvparena-map-concepts
python -m http.server 8000
# → http://localhost:8000
```

## 4 variants

| 변형 | 컨셉 | 톤 |
|---|---|---|
| **A · ROUNDS-LEAN** ★ | flat 가장 강하게, outline 명확, grain 최소. 지형은 desaturated rust + 코너 반경 작게 + 액센트 차분 | 1차 선택 |
| B · ASTRONEER | 파스텔 그라데이션 + 둥근 코너 + 따뜻한 헐 | 비교용 |
| C · SAYONARA | 보라/마젠타 네온 + 강한 글로우 | 비교용 |
| D · HYBRID | 3-way 균형 | 비교용 |

각 variant = ① **1280×720 게임 화면** (S2·S3·S4 3구간 + 3F safe band 가이드 + jump max 1.5F + 낙사존 chevron + 7구간 HUD 인디케이터 + P1/P2 spawn dot) ② **1080×600 tile library** (8 primitives, 4×2 grid).

## 8 tile primitives ↔ §7 자산 라이브러리 매핑

| Code | 라벨 | [§7](../../03_MVPArena_비주얼_컨셉.md) 매핑 | 비고 |
|---|---|---|---|
| T_FLR | Floor Base | (신규 분리) 발판 base 1F | 정적 |
| T_WAL | Wall Material | (신규 분리) 벽 base | 정적 |
| T_STP | Step Block | 단차 블록 (1.5F) | 정적 |
| T_RMP | Ramp 45° | (신규) 비탈길 — 1F↔1.5F 경사 연결 | 정적, dir=left/right |
| T_RFL | Reflective Surface | 반사 표면 (S4 패턴) | 정적 |
| T_VOID | Fall Signal | 낙사 신호 (S6 패턴) | 시각 cue only |
| P_BOX | Movable Crate | (신규) 이동 가능 상자 — 인터랙티브 | **메커닉 추후 검증 — 게임플레이 SOT 갱신 의무** |
| P_PIP | Pipe Debris | 장식 prop (잔해/파이프) | 정적 |

> §7은 자산 라이브러리 5종(단차/반사/시야/낙사/장식)만 명시. 본 mockup은 발판 base + 벽 base 2종을 신규 분리하고 비탈길/상자 2종 신규 추가 → 8개. **V7 신규**: 기존 §7 "시야 차단물(T_BLK)" + "장식 prop 잔존 메탈(P_ANT)" 2종은 mockup 4차 iter에서 사용 미정 사유로 제거 — S5 시야 메커닉 자산은 v0.2 잠금 시 [30/01 전장 구간 기준](../../../01_전장_구간_기준.md) 합의 후 재검토. v0.2 잠금 시 §7 표 7~8종으로 확장 검토.
>
> **P_BOX 인터랙티브 자산 주의**: 이동 가능 상자는 정적 비주얼이 아닌 메커닉 자산이다. mockup은 "추후 사용" 표기만 박제하고, 실제 메커닉(총격 충격력, 캐릭터 충돌 푸시) 도입 시 게임플레이 SOT([10/02](../../../../10_게임플레이_기준/02_라운드_상태머신.md), [30/01](../../../01_전장_구간_기준.md)) 갱신 + 미결정 추가 의무.

## 파일 구성

| 파일 | 역할 |
|---|---|
| `index.html` | shell. React 18 + ReactDOM + Babel standalone CDN 로드 + 3 jsx import. 원본명: `MVPArena Map Concepts.html` |
| `design-canvas.jsx` | Figma 같은 캔버스 래퍼 (DesignCanvas / DCSection / DCArtboard). state persist는 host bridge 의존 — 본 환경에선 비활성, 렌더는 정상 |
| `map-art.jsx` | `MV` 4 variants config + 8 tile primitive 컴포넌트 + 우주/별/행성 |
| `map-variants.jsx` | `MapScreen` (게임 화면) + `MapTiles` (tile library 그리드) |

## 출처 박제

- **출처**: Claude Design 핸드오프 번들 (`api.anthropic.com/v1/design/h/0k76sfuLWdBbZBLn4o1Zow`)
- **의뢰 일자**: 2026-05-10
- **의뢰자**: 박병준 (v1.0-demo 한정 우수연 → 박병준 R 일시 이관 + AD 위임 하, [D71](../../../../05_문서관리_규칙/06_의사결정_로그.md) 근거)
- **iteration**:
  1. 캐릭터 포함 4 variants → `MVPArena Visual Concepts.html` (본 폴더에 박제 안 함; 본 폴더는 map-only)
  2. 캐릭터 제외 map-only 4 variants → 본 mockup
  3. Variant A 지형 톤 진중 조정 (사용자 피드백 → 채도 낮춤 + 코너 반경 축소 + 액센트 차분)
  4. **Tile Library 구성 갱신** (사용자 피드백, 2026-05-11):
     - Tile Library 정렬 → 진짜 중앙 기준으로 보정 (TileCard padTop + center 헬퍼)
     - **제거**: Sight Blocker (T_BLK) + Antenna/Console (P_ANT) — 사용 미정 사유 (V7 신규로 박제)
     - **추가**: Ramp 45° (T_RMP) 비탈길 + Movable Crate (P_BOX) 인터랙티브 상자
     - S3 게임화면도 reflect + crate stack + ramp 조합으로 갱신
- **후속**:
  - T1 자산 작업 (5/11~5/15) — 박병준 Unity 자산화 entry point
  - [§9](../../03_MVPArena_비주얼_컨셉.md) V6 미결정 1차 후보, 우수연 검수 후 v0.2 잠금
  - [§9](../../03_MVPArena_비주얼_컨셉.md) V7 신규 (T_BLK 제거 사유 박제, S5 시야 자산 v0.2 재검토)
  - P_BOX 메커닉 도입 시 게임플레이 SOT 갱신 의무
