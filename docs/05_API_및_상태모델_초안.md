# API 및 상태모델 초안

## 목적
- 학생이 실제로 어떤 인터페이스를 구현하게 될지 빠르게 이해할 수 있도록 정리한다.
- 시뮬레이터, 리플레이 UI, 채점기 모두가 공유할 공통 데이터 모델의 방향을 잠근다.

## 학생 파일 구조

```python
# cook.py

def decide(state):
    ...
```

## MVP 함수 시그니처

```python
def decide(state: KitchenState) -> Assignment | None:
    """현재 틱의 상태를 읽고, 직원 1명에게 할당할 다음 작업을 반환한다."""
```

## 확장 시그니처

```python
def decide(state: KitchenState) -> list[Assignment]:
    """idle 상태인 여러 직원에게 할당할 작업 목록을 반환한다."""
```

## KitchenState 초안

```python
@dataclass
class KitchenState:
    tick: int
    tick_budget: int
    employees: list[Employee]
    pending_orders: list[Order]
    active_tasks: list[ActiveTask]
    menu_catalog: dict[str, MenuDef]
    available_assignments: list[AssignmentCandidate]
```

## Employee 초안

```python
@dataclass
class Employee:
    id: str
    role: str
    idle: bool
```

## Order 초안

```python
@dataclass
class Order:
    id: str
    menu_id: str
    arrived_tick: int
    deadline_tick: int
    node_status: dict[str, str]
    available_node_ids: list[str]
```

## MenuDef 초안

```python
@dataclass
class MenuDef:
    id: str
    name: str
    nodes: list[RecipeNode]
    edges: list[tuple[str, str]]
```

## Assignment 초안

```python
@dataclass
class Assignment:
    employee_id: str
    order_id: str
    node_id: str
```

## AssignmentCandidate 초안

```python
@dataclass
class AssignmentCandidate:
    employee_id: str
    order_id: str
    node_id: str
    duration: int
    deadline_tick: int
```

## 유효성 검증 규칙
- 직원이 존재하지 않으면 무효
- 직원이 idle이 아니면 무효
- 주문이 존재하지 않으면 무효
- 노드가 현재 가능한 상태가 아니면 무효
- 직원 역할과 노드 타입이 맞지 않으면 무효
- 무효 지시는 실행하지 않고 로그에 남긴다

## MVP 기준 설계 판단
- `available_assignments`를 state에 직접 넣으면 학생이 처음부터 그래프 계산 세부 구현에 막히지 않는다.
- 대신 상위권 학생은 `pending_orders`, `menu_catalog`, `node_status`를 직접 읽어 자기만의 우선순위 로직을 만들 수 있다.
- 즉 학습 난이도와 진입장벽을 모두 조절할 수 있다.

## 현재 결론
- MVP는 `직원 1명, 단일 Assignment 반환`이 가장 안전하다.
- 장기 확장을 고려해 상태 구조는 처음부터 복수 직원과 assignment 후보를 수용할 수 있게 설계하는 편이 좋다.
