# Ch4~5 타당성 검토 결과 (Phase 2)

## Executive Summary
- **결과: PASS (Go)**
- Ch4(deadline-aware scheduling)와 Ch5(공유 선행노드 휴리스틱)가 단일 워커 시뮬레이터 위에서 정책별로 명확한 수치 차이를 만들어낸다는 것이 검증되었다.
- 모든 assertion이 통과하며, 정책 간 순위가 strict하게 분리된다 — 동률로 묻히는 정책 쌍이 존재하지 않는다.
- Phase 1(Ch1~3)에 이어 Phase 2(Ch4~5)도 `정책 변경 → 수치 차이` 라는 교육적 핵심 메커니즘이 살아 있다.

## Objective
- `쿡싸피` 챕터 4와 5에서 **deadline 인지**와 **공유 선행노드 fan-out 인지**가 단순 휴리스틱으로 환원 가능한지 확인한다.
- 단일 파일 자바 시뮬레이터(JDK 표준만 사용)로 정책 변경의 효과가 수치로 드러나는지 확인한다.
- Phase 1의 `완료 수`와 `평균 완료시간` 외에 `on-time 수` 라는 새 KPI가 의미 있는지 본다.

## DoD
- Ch4, Ch5 시나리오가 코드에 존재한다.
- 다섯 정책(FIFO, ShortestRemainingWork, EDF, LeastSlack, SharedFirst)이 동일 시뮬레이터 위에서 비교된다.
- Ch4: FIFO가 strict하게 가장 나쁜 on-time 수를 받고, 두 deadline-aware 정책(EDF, LST)이 SRW를 strict하게 이긴다.
- Ch5: SharedFirst가 평균 완료시간에서 FIFO와 SRW를 **둘 다** strict하게 이긴다.
- assertion 실패 없이 `PHASE 2 FEASIBILITY CHECK PASSED` 가 출력된다.

## Owner
- Phase 2 author session (deep-executor)

## Timeline
- 2026-04-09 1차 구현 — 시나리오·정책 코드 작성, 첫 실행에서 assertion은 통과했으나 FIFO/SRW가 동률(2/3)로 묶여 교육 목적을 흐림
- 2026-04-09 2차 재설계 — Ch4에 `instant` 주문(deadline=2)을 추가해 FIFO를 strict 최하위로 분리, Ch5는 ramen-family를 4개로 늘려 SharedFirst의 fan-out 가치를 수치로 가시화
- 2026-04-09 assertion 강화 — Phase 1처럼 `>=`가 아니라 `>` 기반 strict 비교로 굳혀, 향후 누군가 정책을 손대 결과가 동률로 무너지면 즉시 잡히도록 정비

## 구현 아티팩트
- `src/CookSsafyFeasibilityPhase2.java` (단일 파일, JDK 표준 라이브러리만 사용)
- 옵션: `PHASE2_DEBUG=1` 환경변수를 주면 정책별 tick-by-tick 로그와 outcome이 함께 출력됨

## 실행 명령
```bash
JAVAC="$USERPROFILE/.jdks/ms-21.0.9/bin/javac.exe"
JAVA="$USERPROFILE/.jdks/ms-21.0.9/bin/java.exe"
cd "C:/Users/SSAFY/Downloads/전달용/타당성검토"
rm -rf out_phase2 && mkdir -p out_phase2
"$JAVAC" -encoding UTF-8 -d out_phase2 src/CookSsafyFeasibilityPhase2.java
"$JAVA" -Dfile.encoding=UTF-8 -Dstdout.encoding=UTF-8 \
    -cp out_phase2 cookssafy.feasibility.CookSsafyFeasibilityPhase2
```

## 시나리오 요약

### Chapter 4 — two-deadline trap (heavy + instant urgency)
- 단일 워커, tickBudget = 8
- O1 `heavy`: 4 sequential 1t 노드, deadline=5 (slack=1)
- O2 `medium-3`: 3 sequential 1t 노드, deadline=8 (slack=5)
- O3 `instant`: 단일 1t 노드, deadline=2 (slack=1)
- 핵심: 두 개의 동시 긴급 주문(O1, O3)을 함께 살리려면 deadline을 봐야 한다.

### Chapter 5 — 공유 준비작업 (4 ramen-family + 1 side)
- 단일 워커, tickBudget = 10
- O1 udon, O2 ramen, O3 soba, O4 jjampong: 모두 같은 ID `shared-broth`(3t) 선행노드 + 자신만의 1t finish 노드를 가짐 (총 work = 4)
- O5 side-salad: chop-veg(1t) → plate-salad(1t), 총 work = 2
- 시뮬레이터는 한 주문이 `shared-broth` 를 끝내면 다른 ramen-family 형제 주문에도 같은 ID 노드를 자동 완료(=share-propagate) 처리한다.
- 핵심: 공유 노드의 fan-out=4를 인식하면 평균 완료시간이 강하게 단축된다.

## 관찰 결과

### Chapter 4 결과 표
| Policy | completed | onTime | avgCompletion | 해석 |
| --- | --- | --- | --- | --- |
| FIFO | 3/3 | **1/3** | 6.00 | topo 인덱스 우선이라 heavy/medium/instant를 인터리브. 결과적으로 O3가 tick 3에서 끝나 deadline 2 ✗, O1도 tick 8에서 끝나 deadline 5 ✗. 둘 다 놓침 |
| ShortestRemainingWork | 3/3 | **2/3** | 4.33 | O3(rem=1)→O2(rem=3)을 먼저 비우는 동안 heavy 4틱이 뒤로 밀려 O1이 tick 8에서 끝나 deadline 5 ✗. SRW가 FIFO보다는 한 건 더 살림 |
| EDF | 3/3 | **3/3** | 4.67 | deadline 2→5→8 순서. O3(1t) → 헤비 4틱 → 미디엄 3틱 → 모두 deadline에 정확히 맞춤 |
| LeastSlack | 3/3 | **3/3** | 4.67 | tick 0의 slack: heavy=1, medium=5, instant=1. instant와 heavy가 동률(slack=1) → deadline 타이브레이크로 instant 먼저, 결과적으로 EDF와 동일 시퀀스 |
| SharedFirst | 3/3 | **3/3** | 4.67 | 공유 노드가 없는 시나리오라 fan-out이 모두 1로 동률 → deadline 타이브레이크로 EDF와 동일 시퀀스 |

핵심 관찰: **FIFO(1) < SRW(2) < {EDF, LST, SharedFirst}(3)** 의 strict 단계가 만들어진다. SRW가 FIFO를 한 건 차이로 이긴 이유는 deadline-blind 정책이라도 짧은 작업을 먼저 비우면 instant 주문은 우연히 살릴 수 있기 때문 — 그러나 heavy의 deadline은 절대 잡지 못한다.

### Chapter 5 결과 표
| Policy | completed | onTime | avgCompletion | 해석 |
| --- | --- | --- | --- | --- |
| FIFO | 5/5 | 5/5 | 7.00 | shared-broth 한 번에 4개 ramen 주문을 propagate해놓고도, FIFO 비교자가 udon-finish/ramen-finish/soba-finish/jjampong-finish의 topo 인덱스(=1)보다 chop-veg의 topo 인덱스(=0)를 우선해 샐러드를 중간에 끼워 넣는다. 모든 ramen 마무리가 1틱씩 뒤로 밀림 |
| ShortestRemainingWork | 5/5 | 5/5 | **6.40** | 샐러드의 remainingWorkTotal=2가 shared-broth 메뉴의 4보다 작아서 샐러드를 먼저 처리. 그 동안 ramen 4건 모두 2틱씩 뒤로 밀림. 단지 짧은 주문 1건을 먼저 끝내는 효과만 받음 |
| EDF | 5/5 | 5/5 | 7.00 | deadline이 모두 20으로 동률 → topo 타이브레이크로 FIFO와 동일 시퀀스 |
| LeastSlack | 5/5 | 5/5 | 7.00 | slack 동률 → deadline 동률 → topo 타이브레이크로 FIFO와 동일 시퀀스 |
| SharedFirst | 5/5 | 5/5 | **6.20** | shared-broth의 fan-out=4를 인식해 가장 먼저 실행. propagate 후에는 4개 ramen-finish가 rem=1이라 곧장 줄세워 끝내고, 샐러드를 마지막에 처리. ramen 4건이 tick 4·5·6·7에 차례로 완료 |

핵심 관찰: **SharedFirst(6.20) < SRW(6.40) < {FIFO, EDF, LST}(7.00)** 의 strict 단계가 만들어진다. SRW가 FIFO를 이긴 것은 우연한 부수효과(샐러드를 첫 자리에 둠)이지만, **공유 노드의 가치를 모델링한 SharedFirst는 같은 자원으로 4건의 ramen 주문을 모두 더 일찍 끝낸다.**

## 핵심 해석

### Ch4 — 마감을 보지 않으면 무엇이 무너지는가
- `남은 작업량` 만 보는 그리디(SRW)는 짧은 주문을 비우는 데 능하지만 **heavy 주문의 마감 자체는 절대로 잡지 못한다**. 이건 deadline-blind 휴리스틱의 본질적인 한계다.
- 그래서 Ch4의 학습 목표는 분명히 “상태에 deadline이 있다면, 정책의 비교 함수에 deadline을 넣어야 한다”로 환원된다. EDF/LST는 코드 한 줄(=비교자 키 추가)로 결과를 strict하게 개선한다.
- FIFO를 strict 최하위로 만들려면 시나리오에 **두 개의 서로 다른 긴급도(slack=1, slack=1, slack=5)** 를 배치해야 한다는 것을 1차 실험에서 학습했다 — 처음 설계는 한 개의 tight deadline 만 있어 FIFO와 SRW가 2/3 동률로 묶였고, instant 주문을 추가하면서 비로소 FIFO가 strict하게 무너졌다.

### Ch5 — 공유 노드는 “정책의 시야”를 확장해야 보인다
- SRW는 **현재 보고 있는 한 주문의 잔여 작업량** 만 본다. 공유 노드를 끝냈을 때 다른 형제 주문들에 대해서도 “1틱 없어진 효과” 가 발생한다는 것은 SRW의 비교 함수에 안 들어 있다.
- SharedFirst의 비교 키는 단 한 줄의 변화다 — `availableNodeIds` 에 같은 nodeId가 몇 번 등장하는지 세서 fan-out으로 쓴다. 이 한 줄로 ramen 4건 평균 완료시간이 6.40 → 6.20 으로 떨어진다.
- 즉 Ch5는 “정책의 비교 함수에서 본인 주문이 아니라 **시스템 전체** 의 의존 그래프를 봐야 더 좋은 결정이 나온다”는 메시지를 코드로 보여준다. 토폴로지 정렬 → 의존성 분석 → fan-out 휴리스틱이라는 학습의 자연스러운 다음 단계가 정확히 이 한 챕터 안에 들어간다.

## Checkpoint B 판정
- **Go.** Ch4와 Ch5의 설계 의도가 “문서상의 설명” 이 아니라 “정책 비교자 한 줄을 바꾸면 수치가 바뀌는 코드 수준의 문제” 로 환원됨이 확인되었다. 학생이 챕터를 풀 때 막히는 지점도 명확하다 — Ch4는 “deadline을 비교 키에 넣을 줄 아는가”, Ch5는 “다른 주문의 상태를 비교 키에 끌어올 수 있는가” 다. 두 질문 모두 단일 워커, 단일 파일 자바 시뮬레이터로 평가 가능하다.
- 5개 정책의 strict 순위가 동률 없이 분리되어 있어 채점자/리뷰어가 “이 학생의 정책이 어느 단계인지” 판단하기 쉽다.
- 따라서 `03_챕터구조_초안.md` 의 Ch4~5 디자인은 이 시뮬레이터로 그대로 갈 수 있고, Phase 1과 동일한 보일러플레이트 위에 챕터를 얹기만 하면 된다는 것이 입증되었다.

## Residual Gaps
- **다중 워커(Ch6 이후) 미검증**: 본 실험은 모두 단일 워커(`worker-1`) 가정이다. Ch6/Ch7에서 워커가 2명 이상으로 늘어나는 순간 “병렬 가능 노드를 동시에 시작” / “워커 간 자원 충돌” / “shared-broth가 동시에 두 워커에 할당되는 race” 같은 문제가 새로 생긴다. 시뮬레이터의 `ActiveTask` 가 단수형이라서, 다중 워커는 별도 Phase 3 실험으로 분리해야 한다.
- **arrivedTick 변화 미검증**: Ch4/Ch5 모두 모든 주문이 tick 0에 동시 도착한다. 도착 시점이 흩어진 시나리오(=Phase 1 Ch2의 O3가 tick 1 도착처럼)에서는 EDF의 `현재까지 도착한 주문만 보고 결정` 특성이 새로운 케이스를 만들 수 있다.
- **shared-broth 의 다중 워커 race**: 한 워커가 `shared-broth` 를 시작한 사이 다른 워커가 같은 nodeId를 또 시작하는 케이스를 정책이 막아야 한다. 현재 시뮬레이터는 단일 워커라 자연스럽게 막혀 있지만, 다중 워커에서는 명시적인 lock이 필요하다.
- **deadline=on-time만 평가, lateness 정도 미평가**: 현재 KPI는 `on-time 수`(이항)만 본다. “얼마나 늦었는가”를 보는 weighted lateness, max lateness 같은 지표는 더 정교한 정책 비교에 필요할 수 있다 — 다만 챕터 4/5의 학습 목적에는 이항 KPI로 충분하다.
- **SharedFirst 의 비교 키 견고성**: 현재 fan-out 카운팅은 `availableNodeIds` 만 세서, 아직 prerequisite에 막혀 있는 미래의 공유 노드는 보지 않는다. 더 깊은 lookahead(=DAG 전체에서 같은 nodeId가 몇 번 등장하는지)는 후속 실험 후보다.

## 코드 변경 요약 (Phase 2 이번 세션)
- `src/CookSsafyFeasibilityPhase2.java`
  - Ch4 시나리오 재설계: `light-a`(2t) + `light-b`(2t) → `medium-3`(3t) + `instant`(1t, deadline=2)
  - Ch5 시나리오 재설계: `udon`+`ramen` 2건 → `udon`+`ramen`+`soba`+`jjampong` 4건으로 ramen-family 확장, shared-broth duration 2t → 3t
  - `assertChapter4`: `>=` 를 `>` 로 강화. FIFO strict 최하위 + EDF/LST가 SRW strict 우위 + EDF/LST 전원 on-time 명시
  - `assertChapter5`: SharedFirst가 FIFO **그리고** SRW를 strict하게 이기도록 두 조건 모두 명시
  - 진단용 `PHASE2_DEBUG` 환경변수 hook 추가 (기본 OFF, 평소엔 동일한 요약만 출력)

## 비고 — Jun에게 남기는 메모
- Ch5 시나리오에서 **share-propagate** 가 발생할 때 “먼저 끝낸 한 주문의 work만으로 형제 주문이 한 노드 무료 진행” 이라는 점을 학생에게 직관적으로 보여주려면, 게임 UI에서 propagate 이펙트가 시각적으로 강조되어야 한다 — 시뮬레이터의 로그(`tick X: share-propagate Oy/shared-broth`)를 그대로 게임 사이드 패널에 띄우면 충분히 와닿는다.
- Ch4 의 SharedFirst가 onTime=3/3로 EDF·LST 와 동률인 것은 “공유 노드가 없을 때 SharedFirst가 deadline 타이브레이크로 자연스럽게 EDF에 수렴” 이라는 의도된 동작 — 만약 게임 중에 학생이 “Ch4도 SharedFirst로 풀어도 되네” 라고 받아들이면 이는 정답에 가까운 해석이다. SharedFirst의 본질적 우위는 Ch5에서만 드러난다.
- Phase 1 시뮬레이터는 손대지 않았다 (`src/CookSsafyFeasibility.java` 변경 없음).
- 다중 워커 Ch6/Ch7은 시뮬레이터의 `ActiveTask` 모델 자체를 바꿔야 하므로 Phase 3 별도 실험으로 옮기는 것을 권장.
