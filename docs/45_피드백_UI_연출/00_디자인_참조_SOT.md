---
last_updated: 2026-04-20
audience: ai-first
---

# 디자인 참조 SOT

> 문서 상태: accepted_candidate
> binding 범위: 화면 흐름, 와이어프레임, HUD/카드 UI/메뉴의 배치 방향을 확인할 때 우선 참조한다.
> 실행 중단 조건: Figma 와이어프레임이 gameplay truth, 네트워크 truth, 확정 기술스택, accepted 문서와 충돌하면 기준문서 소유자에게 확인한다.
> 상태 기준: 문서 상태와 binding 범위는 `05_문서관리_규칙/08_문서_상태_인덱스.md`를 우선한다.

이 문서는 Colloseum의 디자인 참조 SOT를 등록한다. Figma는 화면 구조와 와이어프레임 판단의 우선 참조지만, 게임 규칙과 네트워크 판정을 직접 확정하지 않는다.

## 현재 디자인 참조 SOT

| 구분 | 값 |
|---|---|
| 도구 | Figma / FigJam Board |
| 이름 | Colosseum SSAFY |
| 링크 | <https://www.figma.com/board/BGVI5ohn0mzXxjiGXy5RFA/Colosseum-SSAFY?node-id=0-1&t=FQMo2unuhiw7Kfsd-0> |
| 담당 도메인 | `45_피드백_UI_연출`, `30_전장_레벨디자인`, `40_카드_무기_설계`의 화면 표현 |
| 우선 적용 범위 | 화면 흐름, HUD 위치, 카드 선택 화면, 메뉴/로비 흐름, 전장 정보 표시 방식 |

## 적용 원칙

- Figma 와이어프레임은 화면 구조와 정보 배치의 SOT다.
- Figma 와이어프레임은 `10_게임플레이_기준`, `50_기술_구조_기준`, `40_카드_무기_설계`의 확정 규칙을 덮어쓰지 않는다.
- Figma에 있는 화면이 현재 상태머신과 맞지 않으면 Figma를 그대로 구현하지 말고 작업 브리핑에서 충돌로 표시한다.
- Figma의 색상, 간격, 폰트 크기, 애니메이션 시간은 아직 최종 수치가 아니다. 실제 수치는 Unity UI 프리셋, 디자인 시스템, 플레이테스트 기록에서 관리한다.
- Figma 노드 기반 구현 작업은 반드시 작업 브리핑에 대상 URL, 노드, 구현 범위, 검증 기준을 적고 시작한다.

## 구현자가 확인해야 할 것

| 작업 | 확인 기준 |
|---|---|
| HUD 구현 | Figma의 화면 내 정보 위치와 `02_UI_HUD_정보구조.md`의 정보 우선순위가 충돌하지 않는지 확인 |
| 카드 선택 UI | Figma의 카드 선택 화면이 `ChoicePending` 입력 잠금과 StateAuthority 확정 흐름을 흐리지 않는지 확인 |
| 전장 진행 UI | 구간 진행도, viewport, 이동 주도권 `GO` 표시가 Figma에서 읽히는지 확인 |
| 메뉴/로비 UI | Figma 흐름을 따르되, 실제 네트워크 세션 생성/참가 흐름은 기술 문서와 구현 브리핑을 우선 |
| 발표/프로토타입 UI | Figma를 시각 기준으로 삼되, 현재 구현 상태를 확정 규칙처럼 문서화하지 않음 |

## 충돌 처리

| 충돌 | 처리 |
|---|---|
| Figma와 accepted gameplay 문서가 다름 | accepted gameplay 문서 우선. Figma 수정 또는 디자인 이슈로 분리 |
| Figma와 accepted 기술스택 문서가 다름 | accepted 기술스택 문서 우선. 구현 금지 API를 쓰지 않음 |
| Figma와 candidate UI 문서가 다름 | 작업 브리핑에 가정과 검증 기준을 적고 담당자 확인 |
| Figma 링크 접근 불가 | BLOCKED로 보고하고 스크린샷이나 export를 요청 |
| Figma 노드가 바뀜 | 변경 이유를 작업 브리핑 또는 의사결정 로그에 남김 |
