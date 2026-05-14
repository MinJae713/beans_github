---
last_updated: 2026-05-02
audience: ai-first
---

# Status Effect 시스템

> 문서 상태: candidate (2026-05-02 A303-261 도입)
> binding 범위: 상태이상 카드 효과의 런타임 적용 흐름. 카드 데이터·수치 결정은 `40_카드_무기_설계` 와 `85_밸런스_프리셋` 참조.

## 0. 한 줄 요약

상태이상 효과(중독·화염·냉기·감전 등)를 **abstract ScriptableObject + INetworkStruct + NetworkBehaviour 3 부품**으로 분리한 확장 가능한 시스템. 새 상태이상 카드 추가 시 SO 1개만 만들면 되고, `PlayerHealth` / `PlayerController` / `BulletController` 코드 수정 0건이 목표(OCP).

## 1. 도입 배경

기존 `PlayerHealth.TakeDamage` 가 `BulletController.StatusEffect` enum을 받아 `Debug.Log` 한 줄만 남기는 stub 상태였다. 새 상태이상 카드가 추가될 때마다 PlayerHealth / PlayerController / BulletController 분기를 더하는 패턴은 카드 누적 시 비용이 폭발하므로, 인프라 차원에서 OCP를 보장하는 시스템을 깐다.

## 2. 3 부품 구조

```
[Card 명중 흐름]

BulletController.OnHit
  ↓ TakeDamage(damage, attacker, AppliedStatus enum)
PlayerHealth
  ↓ ApplyByEnum(status, attacker)            ← enum→SO 어댑터
PlayerStatusController                        ← (NetworkBehaviour) 한 플레이어의 활성 효과 컨테이너
  ↓ Apply(StatusEffectDefinition def, ...)
StatusEffectDefinition (SO)                   ← (abstract) 효과 정의 + OnApply/OnTick/OnExpire
  ↓ NetworkArray<StatusEffectInstance>       ← (INetworkStruct) 활성 인스턴스 (지속/스택/Source)
PlayerStatusController.FixedUpdateNetwork
  ↓ 매 tick 만료/DoT tick 갱신 + modifier 누적

[외부 조회 흐름]

PlayerMovement.ApplyMovement
  ↓ self.StatusController.GetMoveSpeedMultiplier()
  ↓ self.StatusController.GetAirControlMultiplier()
  ↓ self.StatusController.IsStunned()        ← 입력 잠금
PlayerAnimatorController (후속)
  ↓ self.StatusController.GetVisualTint()    ← 비주얼팀 hook
```

### 2.1 `StatusEffectDefinition` (abstract ScriptableObject)

상태이상 한 종류를 데이터로 정의한다. 효과별 동작은 abstract hook 3개로 분리:

- `OnApply` — 적용 직후 1회 (modifier 적용·시각 변경 등 즉시 반영)
- `OnTick` — `tickInterval` 마다 (DoT 데미지 발행 등)
- `OnExpire` — 전체 `Timer` 만료 시 1회 (modifier 복원·시각 원복)

선택적 인터페이스로 modifier·stun·시각 노출:

- `IMovementModifierEffect` — `GetMoveSpeedMultiplier` / `GetAirControlMultiplier` (Frost 등)
- `IStunEffect` — `IsStunningNow` (Electric 등)
- `IVisualTintEffect` — `GetVisualTint` (모든 효과 선택적)

### 2.2 `StatusEffectInstance` (INetworkStruct)

한 플레이어에게 적용 중인 상태이상의 런타임 인스턴스. Fusion이 자동 동기화한다.

- `EffectKey` (NetworkString_16) — Definition.effectKey와 매칭. 빈 문자열이면 슬롯 비어있음.
- `Timer` — 전체 만료 타이머
- `NextTickTimer` — DoT용 다음 tick 타이머
- `Stacks` — 스택형 효과의 누적 카운트
- `Source` (PlayerRef) — 효과 적용자 (kill credit용)
- `Magnitude` — 효과 강도 (DoT 배율 등)

### 2.3 `PlayerStatusController` (NetworkBehaviour)

한 플레이어의 활성 효과 컨테이너 + 매니저. Capacity 8.

- `Apply(def, source, magnitude)` — 직접 SO를 받아 효과 적용. State Authority 한정.
- `ApplyByEnum(status, source)` — enum 기반 진입점. Inspector에 매핑된 SO 조회 후 Apply 위임.
- `FixedUpdateNetwork` — 매 tick 만료/DoT tick + modifier 누적 재계산.
- 외부 조회: `GetMoveSpeedMultiplier` / `GetAirControlMultiplier` / `IsStunned` / `GetVisualTint`.

## 3. 새 상태이상 카드 추가 절차 (김범승 R)

기존 코드 수정 0건. SO 1개 + 카드.asset wiring만 필요.

```
1. Scripts/Status/Effects/{NewEffect}.cs 작성
   - StatusEffectDefinition 상속
   - OnApply / OnTick / OnExpire 구현
   - 필요 시 IMovementModifierEffect / IStunEffect / IVisualTintEffect 구현
   - [CreateAssetMenu] 어트리뷰트 추가

2. Assets/_Project/Data/StatusEffects/{NewEffect}.asset 인스턴스 생성
   - effectKey / baseDuration / tickInterval / 스택 정책 / visualTint 입력

3. Assets/_Project/Data/Cards/{NewCard}.asset 의 statusEffectDef 슬롯에 위 SO 드래그
   (또는 enum 매핑 방식이라면 Player.prefab의 PlayerStatusController 4 슬롯에 드래그)

4. PlayerStatusController NetworkArray Capacity 8 안에서 동작 — 추가 코드 변경 없음
```

상세는 [40_카드_무기_설계/30_구현가이드/02_상태이상_시스템_구현가이드.md](../40_카드_무기_설계/30_구현가이드/02_상태이상_시스템_구현가이드.md).

## 4. 핵심 제약

근거: [04_Fusion_구현_가드레일.md](04_Fusion_구현_가드레일.md)

- **Fusion 결정론** — 모든 시간 측정은 `TickTimer` + `Runner`. `Time.deltaTime` 금지.
- **State Authority 한정** — `Apply` / 만료 / DoT tick은 SA에서만 실행. 결과는 NetworkArray로 자동 동기화.
- **NetworkArray Capacity 8** — 한 플레이어 동시 보유 최대 8종. 본 MVP 카드 풀(4종)에 충분 + 향후 확장 여유.
- **OCP 보장** — 새 상태이상 추가 시 PlayerHealth / PlayerController / BulletController 코드 수정 0건.
- **이벤트 누수 방지** — Definition가 PlayerHealth 등 외부 컴포넌트에 직접 += 구독을 걸지 않음. 필요한 hook은 컨트롤러 메서드 호출로만.

## 5. 정책 결정 사항

| 항목 | 정책 | 결정자 |
|---|---|---|
| 같은 키 재적용 (스택형) | `Stacks +1` (maxStack 클램프) + Timer 갱신 | 본인(R) |
| 같은 키 재적용 (비스택형) | Stacks 유지 + Timer 갱신 (refresh) | 본인(R) |
| DoT 데미지 attacker 표시 | `instance.Source` (마지막 시전자) | 김범승 합의 (HP/사망/리스폰 R) |
| 상태이상 + 사망 race | 사망 시점에 active 효과 즉시 cleanup (NetworkArray 슬롯 default 채움) — 후속 결정 | 김범승 합의 |
| 시각 표현 (색·파티클) | Definition.visualTint + IVisualTintEffect → 비주얼팀 wiring | 우수연(R) |

## 6. 본 MVP 단계 효과 4종

| Effect | 분류 | 동작 |
|---|---|---|
| `BurnEffect` | DoT (짧고 강함) | tickInterval 마다 _damagePerTick 데미지 |
| `PoisonEffect` | DoT (길고 약함) | tickInterval 마다 _damagePerTick 데미지 |
| `FrostEffect` | Modifier | _moveSpeedMultiplier 곱연산 (A303-317에서 공중 조작 효과 폐기) |
| `ElectricEffect` | Stack + Stun | Stacks ≥ _stunThreshold 일 때 IsStunningNow=true |

수치(damagePerTick / moveSpeedMultiplier / stunThreshold 등)는 `85_밸런스_프리셋` 또는 SO 인스턴스 Inspector에서 운영 결정.

## 7. 검증 방법 — DevMode 디버그 트리거

`Scripts/Status/StatusEffectDevTrigger.cs` (`#if UNITY_EDITOR` 가드, production 빌드 무관).

- F1 = Burn / F2 = Poison / F3 = Frost / F4 = Electric +1 스택
- DevMode 진입 후 InputAuthority 측에서 키 입력 → 자기 자신에게 효과 적용
- Console에 `[Colosseum] StatusEffect applied: ...` 로그 확인
- HP 감소 / 이동 둔화 / 입력 잠금 등 직접 체감

카드 wiring 없이도 인프라 동작 검증 가능.

## 8. 관련 기준문서

- [40_카드_무기_설계/02_카드_효과_분류_기준.md](../40_카드_무기_설계/02_카드_효과_분류_기준.md) — Status 카테고리 정의
- [40_카드_무기_설계/10_세부설계/04_40장_카드_카탈로그.md](../40_카드_무기_설계/10_세부설계/04_40장_카드_카탈로그.md) — Common 7~10 4종 정의
- [40_카드_무기_설계/30_구현가이드/02_상태이상_시스템_구현가이드.md](../40_카드_무기_설계/30_구현가이드/02_상태이상_시스템_구현가이드.md) — 새 효과 추가 가이드 (김범승용)
- [50_기술_구조_기준/01_Unity_프로젝트_구조.md](01_Unity_프로젝트_구조.md) — 폴더 구조
- [50_기술_구조_기준/04_Fusion_구현_가드레일.md](04_Fusion_구현_가드레일.md) — 결정론·금지선
- [50_기술_구조_기준/05_파일경로별_DRI.md](05_파일경로별_DRI.md) — Scripts/Status/** CODEOWNERS

## 9. 변경 기록

- 2026-05-02: A303-261 도입. 인프라 3 부품 + Common 4종 효과 SO + DevMode 디버그 트리거.
