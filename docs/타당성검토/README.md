# 타당성 검토: Ch1~3 백엔드 실험

## 목적
- UI 없이도 `쿡싸피`의 초기 챕터 설계가 실제 알고리즘 문제처럼 성립하는지 확인한다.
- `decide(state)` 정책만 바꿔가며 결과가 달라지는지 자바 시뮬레이터로 검증한다.
- 현재 범위는 `직원 1명`, `공유 작업 없음`, `Chapter 1~3`이다.

## 실험 질문
1. 단일 주문 DAG는 최소 정책만으로 정상 완주되는가
2. 여러 주문이 동시에 열리면 FIFO와 다른 정책이 실제로 다른 결과를 만드는가
3. 긴 작업과 짧은 작업이 섞이면 시간 고려 정책이 FIFO보다 유의미하게 낫다고 말할 수 있는가

## 구현 범위
- 틱 기반 시뮬레이터
- 메뉴별 레시피 DAG
- 주문 도착 시점
- 단일 직원
- `availableAssignments` 기반 정책 인터페이스
- 정책 비교
  - `FirstAvailable`
  - `FIFO`
  - `ShortestCurrentDuration`
  - `ShortestRemainingWork`

## 파일 구성
- `src/CookSsafyFeasibility.java`
  - 시뮬레이터, 정책, 시나리오, assertion을 한 파일에 모은 실험 코드
- `run.ps1`
  - 로컬 JDK를 찾아 컴파일/실행하는 스크립트

## 시나리오 요약
- Chapter 1
  - 단일 주문 1개, 선형 DAG 3노드
  - 목표: 최소 정책으로도 완주됨을 확인
- Chapter 2
  - 여러 주문 동시 도착/순차 도착, 모든 노드 duration=1
  - 목표: 단순 FIFO보다 `남은 총 작업량` 기준 정책이 평균 완료시간에서 이득을 보이는지 확인
- Chapter 3
  - 긴 작업(3틱)과 짧은 작업(1틱) 혼합
  - 목표: 시간 고려 정책이 FIFO보다 더 많은 주문을 영업 시간 안에 완료하는지 확인

## 실행 방법
PowerShell에서 아래를 실행한다.

```powershell
.\run.ps1
```

직접 컴파일/실행할 경우:

```powershell
& "$env:USERPROFILE\.jdks\ms-21.0.9\bin\javac.exe" -encoding UTF-8 -d .\out .\src\CookSsafyFeasibility.java
& "$env:USERPROFILE\.jdks\ms-21.0.9\bin\java.exe" -cp .\out cookssafy.feasibility.CookSsafyFeasibility
```

## 기대 해석
- Chapter 1: 어떤 정책이든 완주
- Chapter 2: `ShortestRemainingWork`가 `FIFO`보다 평균 완료시간이 더 낮음
- Chapter 3: `ShortestCurrentDuration` 또는 `ShortestRemainingWork`가 `FIFO`보다 더 많은 주문을 완료

## 현재 의미
- 이 실험은 `초기 챕터가 단순 문서 아이디어에 머물지 않고 실제 정책 문제로 환원 가능하다`는 타당성 증거를 만드는 용도다.
- 이후 Ch4부터는 deadline과 마감 여유 시간을 추가해 `EDF (Earliest Deadline First)` 및 우선순위큐 계열 정책으로 확장하면 된다.
