# 06. MVP 스코프 및 하드넘버 스펙

작성일: 2026-04-12
작성 목적: `쿡싸피` MVP(Ch1~5) 의 모든 숫자를 **문서 밖에서 추측하지 않아도 될 만큼** 구체화한다. 기획 브리프·챕터 구조·타당성 실증 결과·샌드박스 결정이 모두 확정된 후 작성된 "실제 구현 투입용 단일 진실 소스".

선행 문서:
- `00_기획브리프_초안.md` — 프로젝트 정의
- `02_핵심시스템_초안.md` — 시뮬레이터 설계
- `03_챕터구조_초안.md` — Ch1~8 설계
- `05_API_및_상태모델_초안.md` — API 시그니처
- `07_샌드박스_옵션_검토.md` — Pyodide 결정
- `타당성검토/00_실험결과_요약.md` — Ch1~3 strict 분리 확인
- `타당성검토/01_Phase2_실험결과.md` — Ch4~5 Checkpoint B Go

---

## 1. MVP 범위 선언

### 1.1 포함 (In Scope)
- **Chapter 1~5** 전체 시나리오 (Ch1: 첫 DAG, Ch2: 다중 주문, Ch3: 긴/짧은 작업, Ch4: deadline, Ch5: 공유 준비작업)
- **단일 직원 (worker-1)** 기준의 `decide(state)` 학생 API
- **Pyodide 기반 브라우저 런타임** (학생 로컬 = 제출 검증 동일 계층)
- **리플레이 UI** (React + Dagre + Zustand) — Ch5 시나리오 플레이백까지 데모 가능
- **결정론 시뮬레이션** — 같은 시드 × 같은 코드 × 10회 실행 → 결과 bitwise 동일
- **5개 참조 정책** 내장: FIFO, ShortestCurrentDuration, ShortestRemainingWork, EDF, SharedFirst
- **챕터 통과 기준** 및 **리더보드 보조 지표** 계산 (주문 성공률, 평균 완료시간, on-time 수)

### 1.2 제외 (Out of Scope)
- **Chapter 6~8** (러시아워, 복수 직원, 캡스톤) — 확장 챕터로 격리, MVP 이후 Phase 3
- **복수 직원 assignment** — `decide()` 는 단일 Assignment 또는 None 반환만 허용
- **선점(preemption)** — 이미 작업 중인 직원에게 새 지시 무시
- **게임 내 레벨 에디터 / 커스텀 시나리오 제작 UI**
- **소셜 기능** (친구 대결, 댓글)
- **다국어** — 한국어 고정
- **모바일 레이아웃** — 1024~1440px 데스크탑 기준

### 1.3 "MVP 완료" 의 정의 (DoD)
다음 7개가 전부 참이면 MVP 완료로 본다.

1. 학생이 브라우저에서 `cook.py` 에디터에 코드를 붙여넣고 "실행" 을 누르면 리플레이가 재생된다.
2. Ch1~5 시나리오가 전부 로드 가능하며, 각 챕터에서 FIFO/SRW/EDF/SharedFirst 참조 정책이 `타당성검토/00_실험결과_요약.md` 및 `01_Phase2_실험결과.md` 의 수치와 일치한다 (completed, onTime, avgCompletion 정수/소수 bitwise 동일).
3. 학생 코드 3종 — 정상/무한루프/불허 import — 각각 정상 실행, 10s 타임아웃 자동 종료, 에러 메시지 표시로 처리된다.
4. 한 판 실행이 실벽시계 기준 ≤ 1.5s (Ch1~5 중 가장 무거운 Ch5 기준, Pyodide 런타임 1회 로드 후 재실행 기준).
5. 챕터별 통과 여부가 명확한 점수/메시지로 표시된다 (§4.2 기준 충족/미충족).
6. 리플레이에서 공유 노드 propagate 이벤트(`share-propagate O2/shared-broth tick=3`) 가 시각적으로 강조된다.
7. `cook.py` 학생 템플릿, `README.md` 학생 가이드, `decide-api.md` 상태모델 레퍼런스 3개 문서가 완성되어 있다.

---

## 2. 시뮬레이션 파라미터 (하드넘버)

### 2.1 공통 파라미터 (모든 챕터)
| 항목 | 값 | 근거 |
|------|---|------|
| tick 크기 | 1 (추상 단위) | 실시간 틱이 아니라 스텝 인덱스 |
| 직원 수 | 1 (worker-1) | MVP 정의 |
| `decide(state)` 호출 시점 | 매 틱, 직원이 idle일 때만 | Phase 1/2 시뮬레이터 동작 |
| 선점 | 불허 | §1.2 |
| 무효 Assignment 처리 | 무시하고 로그 | `02_핵심시스템_초안.md` §7 |
| 결정론 | 동일 시드·코드 → 동일 결과 | §1.3 DoD#5 |
| 시드 기본값 | `42` | 관례 + 학생이 변경 가능 |
| 학생 코드 실행 한도 | 한 판 총 10.0s CPU, 256MB 메모리 | Pyodide 런타임 한도 (§3) |
| 한 번의 `decide()` 호출 한도 | 50ms (soft), 200ms (hard kill) | 200틱 × 50ms = 10s 총 예산과 일관 |
| 틱 인덱스 시작 | 0 | Phase 1/2 타당성 실증 기준 |

### 2.2 챕터별 시나리오 (Phase 1/2 실증 수치를 그대로 확정)

#### Chapter 1 — 첫 번째 레시피
| 항목 | 값 |
|------|---|
| tickBudget | 5 |
| 주문 수 | 1 |
| 메뉴 | `basic-sandwich` (3 노드 선형 chain: chop-veg(1t) → assemble(1t) → plate(1t), total work = 3) |
| 주문 도착 | O1 arrivedTick=0, deadline=없음 (또는 tickBudget 끝) |
| 공유 노드 | 없음 |
| 정답 완료 tick | 3 |

#### Chapter 2 — 여러 주문, 여러 선택지
| 항목 | 값 |
|------|---|
| tickBudget | 8 |
| 주문 수 | 3 |
| 메뉴 | `long-chain` (5 노드 chain, work=5), `short-chain-a` (2 노드, work=2), `short-chain-b` (2 노드, work=2) |
| 주문 도착 | O1=long@tick0, O2=short-a@tick0, O3=short-b@tick1 |
| 공유 노드 | 없음 |
| Phase 1 관찰 | FIFO 2/3 (avg 5.00), SRW 2/3 (avg 3.00) — SRW가 FIFO 대비 평균 2틱 우위 |

#### Chapter 3 — 긴 작업과 짧은 작업
| 항목 | 값 |
|------|---|
| tickBudget | 10 |
| 주문 수 | 3 |
| 메뉴 | `noodle` (5 work), `side` (4 work), `drink` (2 work, tick 2 도착) |
| 공유 노드 | 없음 |
| Phase 1 관찰 (2026-04-09 재설계 후) | SCD avgCompletion=6.00, SRW avgCompletion=5.00 (strict) |
| 변별 포인트 | SCD는 dur=1 우선, SRW는 remaining-work 우선 |

#### Chapter 4 — 마감시간과 손님 인내심 (two-deadline trap)
| 항목 | 값 |
|------|---|
| tickBudget | 8 |
| 주문 수 | 3 |
| 메뉴 | `heavy` (4×1t 선형, work=4, deadline=5, slack=1), `medium-3` (3×1t, work=3, deadline=8, slack=5), `instant` (1×1t, work=1, deadline=2, slack=1) |
| 주문 도착 | 전부 tick 0 |
| 공유 노드 | 없음 |
| Phase 2 관찰 (strict 분리) | FIFO onTime=1/3, SRW onTime=2/3, EDF/LST/SharedFirst onTime=3/3 |
| 변별 포인트 | deadline 을 비교 키에 넣느냐 |

#### Chapter 5 — 공유 준비작업 (ramen-family + side)
| 항목 | 값 |
|------|---|
| tickBudget | 10 |
| 주문 수 | 5 |
| 메뉴 | `udon`/`ramen`/`soba`/`jjampong` (공통 `shared-broth` 3t + 고유 1t finish), `side-salad` (chop-veg 1t → plate-salad 1t) |
| 주문 도착 | 전부 tick 0 |
| 공유 노드 | `shared-broth` (fan-out=4) |
| 공유 동작 | 한 주문이 `shared-broth` 완료 시 형제 주문 3건에 share-propagate |
| Phase 2 관찰 (strict 분리) | SharedFirst avg=6.20 < SRW avg=6.40 < {FIFO, EDF, LST} avg=7.00 |
| 변별 포인트 | 정책이 본인 주문 바깥의 fan-out 을 비교 키에 넣느냐 |

### 2.3 작업 타입별 고정 시간표
`02_핵심시스템_초안.md` §6 과 Phase 1/2 실증 코드 기준으로 동결.

| 작업 타입 | duration (tick) |
|---------|----------------|
| 손질 (chop) | 1 |
| 섞기 (mix) | 1 |
| 플레이팅 (plate) | 1 |
| 조립 (assemble) | 1 |
| 조리 (cook-generic) | 2 |
| 끓이기 (boil) | 2 |
| 굽기 (grill) | 3 |
| 튀기기 (fry) | 3 |
| 공유 육수 (shared-broth) | 3 |

MVP에서 이 표를 벗어난 새 작업 타입은 추가하지 않는다. Ch6 이후 확장 챕터에서만 신규 타입 도입 허용.

---

## 3. 학생 API 및 상태모델 (동결)

### 3.1 시그니처
```python
# cook.py — 학생이 작성하는 단일 파일
def decide(state: "KitchenState") -> "Assignment | None":
    """매 틱 호출되어 idle 직원에게 할당할 작업을 반환한다.

    반환값이 None 이면 이 틱은 아무 작업도 시작하지 않는다.
    무효한 Assignment는 시뮬레이터가 무시하고 로그에 남긴다.
    """
    ...
```

**MVP는 단일 Assignment 만 지원.** `list[Assignment]` 반환은 Ch7 이후 확장.

### 3.2 KitchenState 필드 (MVP 최종)
```python
@dataclass(frozen=True)
class KitchenState:
    tick: int                                 # 현재 틱 (0-based)
    tick_budget: int                          # 영업 시간 상한
    employees: tuple[Employee, ...]           # 길이 1 (MVP)
    pending_orders: tuple[Order, ...]         # 아직 완료되지 않은 주문
    active_tasks: tuple[ActiveTask, ...]      # 길이 0 또는 1 (MVP)
    menu_catalog: dict[str, MenuDef]          # 메뉴 ID → 정의
    available_assignments: tuple[AssignmentCandidate, ...]
```

`frozen=True` + `tuple` 사용 → 학생 코드가 필드를 수정해도 TypeError로 튕김. 결정론 보장을 위해 상태 주입 측에서도 `copy.deepcopy` 전파.

### 3.3 Order 필드
```python
@dataclass(frozen=True)
class Order:
    id: str                                   # "O1", "O2", ...
    menu_id: str                              # menu_catalog key
    arrived_tick: int
    deadline_tick: int | None                 # Ch1~3은 None, Ch4+는 int
    node_status: Mapping[str, str]            # {node_id: "locked"|"available"|"in_progress"|"done"}
    available_node_ids: tuple[str, ...]       # status == "available" 인 노드만
```

### 3.4 MenuDef 필드
```python
@dataclass(frozen=True)
class MenuDef:
    id: str
    name: str
    nodes: tuple[RecipeNode, ...]             # (id, type, duration)
    edges: tuple[tuple[str, str], ...]        # (from, to) 선행조건
```

### 3.5 Assignment 필드
```python
@dataclass(frozen=True)
class Assignment:
    employee_id: str                          # MVP에선 항상 "worker-1"
    order_id: str
    node_id: str
```

### 3.6 AssignmentCandidate 필드 (편의 필드, 학생이 바로 쓸 수 있음)
```python
@dataclass(frozen=True)
class AssignmentCandidate:
    employee_id: str
    order_id: str
    node_id: str
    duration: int                             # 이 노드의 고정 시간
    deadline_tick: int | None
```

`available_assignments` 는 시뮬레이터가 매 틱 미리 계산해 state에 넣어준다. 학생이 직접 그래프 순회를 하지 않아도 **첫 정책**은 바로 쓸 수 있다. 고급 정책(예: fan-out 카운트)은 `pending_orders`, `menu_catalog`, `node_status` 를 직접 읽어 구현.

### 3.7 유효성 검증 규칙 (`05_API_및_상태모델_초안.md` 와 동일)
1. 존재하지 않는 `employee_id` → 무시
2. idle이 아닌 직원 → 무시
3. 존재하지 않는 `order_id` → 무시
4. `available_node_ids` 에 없는 `node_id` → 무시
5. 직원 역할과 노드 타입 불일치 → 무시 (MVP는 단일 역할이므로 실효 없음)
6. 무시된 Assignment는 `trace.log[].invalid_assignment` 로 기록되어 리플레이에서 노란색 경고로 표시

---

## 4. 평가 및 챕터 통과 기준

### 4.1 KPI 정의
| KPI | 계산 |
|----|-----|
| `completed` | 영업 시간 내에 모든 노드가 done 된 주문 수 |
| `onTime` | `completed` 중 `done_tick ≤ deadline_tick` 인 주문 수. deadline 없는 주문은 자동 on-time |
| `avgCompletion` | 완료된 주문들의 `done_tick - arrived_tick` 평균 |
| `invalidCount` | 무시된 Assignment 수 |
| `idleTicks` | 직원이 가능한 작업이 있음에도 None을 받은 틱 수 |

### 4.2 챕터 통과 기준
각 챕터에 "최소 통과 정책" 과 "고득점 기준" 을 명시.

| Chapter | 최소 통과 (별 1) | 고득점 (별 3) | 근거 |
|---------|-----------------|----------------|------|
| Ch1 | `completed == 1` AND `invalidCount == 0` | `avgCompletion == 3` | 단일 DAG는 어떤 유효 정책이든 3에 수렴 |
| Ch2 | `completed ≥ 2` | `completed == 3` AND `avgCompletion ≤ 4.0` | SRW급 정책 필요 |
| Ch3 | `completed ≥ 2` | `completed == 2` AND `avgCompletion ≤ 5.0` | SRW 레벨 (Phase 1 실측) |
| Ch4 | `onTime ≥ 2` | `onTime == 3` | EDF/LST 필요 (Phase 2 실측) |
| Ch5 | `completed == 5` AND `avgCompletion ≤ 6.8` | `completed == 5` AND `avgCompletion ≤ 6.3` | SRW ≤ 6.40 / SharedFirst ≤ 6.20 |

**별 1** 은 단순 FIFO나 SCD로 대부분 뚫리도록 설계. **별 3** 은 해당 챕터의 대표 개념(마감, fan-out) 없이는 도달 불가하도록 설계. 별 2 는 별 1과 별 3 사이의 선형 보간.

### 4.3 리더보드 스코어
```
score = onTimeRate * 1000
      - avgCompletion * 10
      - invalidCount * 50
      - idleTicks * 1
```
- `onTimeRate` = `onTime / totalOrders` ∈ [0, 1]
- 동점 시 `avgCompletion` 작은 쪽 우선
- 음수 가능 (오염된 정책은 별 1도 못 받음)
- MVP에서는 챕터별 개별 리더보드, 통합 리더보드는 Phase 3

---

## 5. 참조 정책 (시뮬레이터 내장, 학생 비교용)

`cook_reference.py` 모듈에 5개 정책을 내장한다. 학생은 "정책 전환" 버튼으로 자신의 `decide()` 와 비교 실행 가능.

| 정책 | 핵심 비교 키 | 구현 줄수 | 교육적 역할 |
|------|-------------|----------|-------------|
| FIFO | `(order.arrived_tick, order.id)` | ~3 | 최소 베이스라인 |
| ShortestCurrentDuration | `(candidate.duration, order.arrived_tick)` | ~3 | 현재 노드 우선 그리디 |
| ShortestRemainingWork | `(remaining_work(order), order.arrived_tick)` | ~5 | 주문 전체 잔여 작업 우선 |
| EDF | `(order.deadline_tick or INF, order.arrived_tick)` | ~3 | 마감 인지 |
| SharedFirst | `(-fanout(candidate.node_id), candidate.duration)` | ~6 | 공유 노드 가치 인지 |

- 5개 정책은 **Phase 1/2 Java 시뮬레이터의 동작과 bitwise 동일** 한 결과를 내야 함. 이것이 포팅 완료의 정의.
- 학생이 처음 시뮬레이터를 열었을 때 기본으로 제시되는 코드는 **FIFO** — 학생은 여기서 개선을 시작한다.

---

## 6. 샌드박스 및 런타임 구성 (§7 결정 반영)

### 6.1 학생 실행 (브라우저)
- 페이지 로드 → `loadPyodide()` (~10MB, 1회 캐시)
- 시뮬레이터 Python 코드 (내장, ~800줄 예상) 를 `pyodide.runPython(simulator_src)` 로 초기화
- 학생 `cook.py` 를 `pyodide.runPython(student_src)` 로 실행 → `decide` 함수 심볼 획득
- 한 판 실행: 시뮬레이터가 tick 루프 안에서 `decide(state)` 를 호출 (Python 내부 호출, IPC 없음)
- 결과: `TraceRecord` (Python 객체 → `to_js()` 로 JS 스토어 전달) → React 리플레이

### 6.2 제출 검증 (서버)
- Node.js + `pyodide` npm 패키지
- 동일한 시뮬레이터 Python 코드 로드
- 제출 `cook.py` 를 고정 시드 5개 × 5개 챕터 = 25회 실행
- 각 실행 결과를 DB에 저장 + 챕터별 통과 여부 판정
- 단일 인스턴스 기준 동시 10건 처리 (큐잉)

### 6.3 한도 및 타임아웃
- 한 판 총 wallclock: 10s (hard kill)
- 한 번의 `decide()` 호출: 50ms soft / 200ms hard. 200ms 초과 시 해당 호출을 invalid로 처리하고 로그 기록 후 판 종료.
- 메모리: Pyodide 런타임 프로세스 전체 256MB
- 허용 표준 라이브러리: `dataclasses`, `typing`, `collections`, `heapq`, `itertools`, `functools`, `math`, `bisect` (문서화)
- 차단: `os`, `sys`, `subprocess`, `socket`, `threading`, `multiprocessing`, `builtins.open`, `__import__` (Pyodide 브라우저 샌드박스에 OS syscall 부재로 자연스럽게 차단)

### 6.4 결정론 가드
- 시뮬레이터 내부 `random.Random(seed)` 만 사용, 학생 코드에서 `random.seed` 를 호출해도 시뮬레이터 인스턴스는 독립
- 주입되는 state 는 `copy.deepcopy` 후 `frozen=True` dataclass → 학생의 mutation 시도는 TypeError
- Python `dict` 는 insertion order 보장 (3.7+) 이므로 menu_catalog 순회도 결정론적

---

## 7. 구현 작업 분해 (WBS)

### 7.1 작업 묶음
| # | 작업 | 공수 | 선후관계 | 담당 |
|---|------|-----|---------|------|
| W1 | Java 시뮬레이터(Phase 1/2) → Python 포팅 | 2~3일 | 선행 없음 | A |
| W2 | Python 시뮬레이터 결정론/동치성 테스트 (Ch1~5 × 5정책) | 1일 | W1 | A |
| W3 | Pyodide 통합 POC (단일 HTML, 에디터 + 실행 + 로그) | 1일 | W1 (POC용, W2 대기 불필요) | B |
| W4 | React + Vite 프로젝트 셋업 + 기본 레이아웃 | 1일 | 선행 없음 | B |
| W5 | KPI 스트립, 주문 티켓, 플레이백 컨트롤 | 1일 | W4 | B |
| W6 | DAG 컴포넌트 (Dagre 자동 레이아웃) | 2~3일 | W4 | B |
| W7 | 트레이스 로더 + Pyodide 런타임 연결 | 1~2일 | W2, W3, W4 | A+B |
| W8 | 직원 패널 + 실패 태그 + 공유 propagate 강조 | 1일 | W6, W7 | B |
| W9 | 챕터 로더 + 통과 판정 + 리더보드 기본 UI | 1일 | W7 | A |
| W10 | 제출 검증 서버 (Node + Pyodide) | 2일 | W2 | A |
| W11 | 학생 가이드 문서 (`README.md`, `decide-api.md`, 학생 템플릿) | 1일 | W2 | A |
| W12 | 반응형 + 접근성 기본 | 1~2일 | W8 | B |
| W13 | E2E 테스트 (정상/무한루프/불허 import/결정론 10회) | 1일 | W7, W10 | A |
| W14 | 통합 디버그 + 버그 수정 | 2일 | 이후 | 둘 |

**병렬 처리 시 실 캘린더 일수: 12~15일 (2인, 주 5일 기준 약 3주).** FE 담당자 B는 W3/W4/W5/W6 를 병렬 진행, BE 담당자 A는 W1/W2/W10 를 병렬 진행.

### 7.2 단계별 마일스톤
- **M1 (1주차 종료)**: W1+W2+W3 완료 → "Python 시뮬레이터가 Java 결과를 1:1 재현 + Pyodide POC 동작"
- **M2 (2주차 종료)**: W4~W8 완료 → "브라우저에서 학생 코드 입력 → 리플레이 재생 가능"
- **M3 (3주차 종료)**: W9~W14 완료 → MVP DoD §1.3 의 7개 기준 전부 충족

---

## 8. 테스트 계획

### 8.1 단위 테스트 (Python 시뮬레이터)
- 각 챕터별 5개 정책 × 결과 수치가 Java 타당성 실증과 동일한지 assertion
  - Ch1: 전부 (1, 3.00)
  - Ch2: FIFO (2, 5.00), SRW (2, 3.00)
  - Ch3: SCD (2, 6.00), SRW (2, 5.00)
  - Ch4: FIFO onTime=1, SRW onTime=2, EDF/LST/SharedFirst onTime=3
  - Ch5: SharedFirst avg=6.20, SRW avg=6.40, FIFO/EDF/LST avg=7.00
- 결정론 테스트: 동일 시드 × 동일 정책 × 10회 실행 → 완전 동일 (KPI + 로그)

### 8.2 통합 테스트 (Pyodide 브라우저)
- 정상 코드: FIFO 정책 실행 → UI에 KPI + 리플레이 정상 표시
- 무한루프: `while True: pass` 포함 정책 → 10s 타임아웃 후 "실행 시간 초과" 메시지
- 불허 import: `import os` 포함 정책 → `ModuleNotFoundError` 를 학생이 읽을 수 있는 친절한 메시지로 감싸 표시
- 문법 오류: 들여쓰기 오류 → SyntaxError 를 에디터에서 line 정보와 함께 표시
- 잘못된 Assignment 반환: 존재하지 않는 order_id → invalidCount 증가 + 리플레이 경고 표시

### 8.3 제출 검증 서버 테스트
- Node + Pyodide 로 동일한 학생 코드 실행 → 브라우저 결과와 완전 동일
- 동시 10건 병렬 처리 시 모든 결과가 결정론적
- 한 건 처리 시간 ≤ 3s (브라우저 기준 1.5s의 2배 마진)

### 8.4 수동 학생 시나리오 테스트 (교육 관점)
- **"FIFO → SRW → EDF → SharedFirst"** 로 정책을 발전시키며 Ch1 부터 Ch5 까지 차례로 푸는 시나리오
- 각 단계에서 UI 가 "이전 정책으로는 왜 통과 못 했는지" 를 로그/태그로 명확히 보여주는지 확인

---

## 9. 리스크 및 완화책

| # | 리스크 | 영향 | 확률 | 완화책 |
|---|-------|------|-----|-------|
| R1 | Java → Python 포팅에서 결과 불일치 | 타당성 실증 근거가 무너짐 | 중 | W2 단위 테스트에서 bitwise 동치성 검증. 불일치 시 Java 시뮬레이터를 Python이 흉내내는 방향(테스트 기반 재작성)으로 진행 |
| R2 | Pyodide 로드 성능 저하 (SSAFY 네트워크) | 학생 첫 실행 대기 10초+ | 중 | 정적 CDN 캐싱 + 사전 로더 UI (로딩바 + "처음 한 번만 받으면 빨라집니다" 메시지) |
| R3 | Dagre DAG 레이아웃 복잡도 폭발 (W6 지연) | FE 일정 1주 지연 | 중 | `08_리플레이_UI_공수추정.md` §4 리스크 1 완화책 적용. 메뉴 DAG 수가 작으므로 (Ch5 기준 5개 DAG, 공유 노드 포함) 수동 fallback 가능 |
| R4 | 학생이 `decide()` 에서 `pending_orders` 를 mutation → TypeError 혼란 | 학습 경험 저해 | 낮음 | 가이드 문서에 "state 는 읽기 전용" 강조. 에러 메시지 한국어 래핑 |
| R5 | 결정론이 깨지는 미묘한 버그 (`dict` 순서, set 순회) | 리더보드 신뢰성 붕괴 | 낮음 | `dict` 대신 `collections.OrderedDict` 사용 검토, `set` 사용 금지 가이드 |
| R6 | 챕터 통과 기준이 너무 어려워 학생이 중간 이탈 | 교육 완료율 저하 | 중 | §4.2 의 별 1/3 구조로 진입장벽 낮춤. 시범 반(班) 파일럿 후 숫자 재조정 |
| R7 | Pyodide POC 가 근본 문제로 실패 (예: 브라우저 메모리 한계) | 관점 A → B 전환 필요 | 낮음 | `07_샌드박스_옵션_검토.md` §5 후퇴 경로: RestrictedPython + Java 시뮬레이터 유지로 회귀, Java 결과를 REST로 FE에 전달 |

---

## 10. 확정 결정 로그 (Decision Record)

### D1. 학생 API 는 `decide(state) -> Assignment | None` 로 고정
- 근거: `00_기획브리프` 2차 방향 수정 + Phase 1/2 타당성 실증이 이 시그니처로 통과
- 대안: `list[Assignment]` 반환형. **거부 이유**: 다중 직원이 MVP 범위 밖. Ch7 이후 확장 시점에 signature union 추가 예정

### D2. 샌드박스는 Pyodide
- 근거: `07_샌드박스_옵션_검토.md` §5
- 후퇴 경로: RestrictedPython (W3 POC 실패 시)

### D3. 시뮬레이터는 Python 으로 단일화 (Java 시뮬레이터는 검증 참고용)
- 근거: Pyodide 가 선택되면 시뮬레이터도 같은 런타임에서 돌아야 IPC 0
- 대안: Java 시뮬레이터 유지 + REST 브릿지. **거부 이유**: 2개 언어 유지 비용 > 포팅 1회 비용 (Java 코드 ~500줄, 포팅 2~3일)

### D4. MVP 는 Ch1~5, 단일 직원
- 근거: `03_챕터구조_초안.md` Checkpoint B Go + Checkpoint C (복수 직원은 확장으로 분리) 의견과 일치
- 대안: Ch1~8. **거부 이유**: 확장 챕터는 `ActiveTask` 단수형을 복수형으로 바꿔야 하며, 이는 시뮬레이터 구조 변경. MVP 범위 밖

### D5. 챕터 통과 기준은 Phase 1/2 실측 수치 기반
- 근거: `타당성검토/01_Phase2_실험결과.md` 의 정책별 strict ordering
- 조정 가능: 시범 반 테스트 후 별 1/3 threshold 미세 조정 (숫자는 §4.2 의 초기값)

### D6. 참조 정책 5개 내장 (FIFO/SCD/SRW/EDF/SharedFirst)
- 근거: Phase 1/2 에서 이 5개가 교육 포인트 전 구간을 커버함
- 대안: LeastSlack 추가. **보류 이유**: Ch4 에서 EDF 와 동일 순서로 수렴 — 교육적 차별화 낮음. 확장 챕터에서 재도입 검토

### D7. 리더보드 스코어 공식 § 4.3
- 근거: onTimeRate 1000점 기반 + 평균 완료시간 페널티 + 무효 assignment 페널티의 선형 결합
- 개정: 시범 반 결과에서 가중치 재조정 가능

---

## 11. 다음 세션 진입 시 첫 작업

1. `타당성검토/src/CookSsafyFeasibility.java` 와 `CookSsafyFeasibilityPhase2.java` 를 열어 **Python 포팅 설계** 시작 (W1)
   - 구조 먼저: `KitchenState`, `Order`, `MenuDef`, 정책 interface → Python dataclass 로 1:1 매핑
   - Java의 `Comparator` → Python 의 `key=lambda` + `sorted()`
   - Java 의 assertion → Python `assert` + `pytest`
2. W1 완료 후 W2 단위 테스트 케이스 작성 (§8.1 의 15개 assertion)
3. 병렬로 W3 Pyodide POC — 최소 HTML 1장에 pyodide.js + 에디터 + "실행" 버튼

이 3개가 끝나면 M1 마일스톤 달성. 그 다음은 React + Vite 셋업으로 진입.

---

*본 문서는 P2(MVP 하드넘버) 의 단일 진실 소스이다. 이후 기획 회의에서 숫자가 변경되면 본 문서를 먼저 수정한 뒤 후속 문서·코드가 따라간다.*
