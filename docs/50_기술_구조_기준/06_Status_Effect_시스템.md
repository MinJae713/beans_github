---
last_updated: 2026-05-18
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
  ↓ TakeDamage(damage, attacker, AppliedStatusMask, status별 duration/magnitude)
PlayerHealth
  ↓ ApplyByEnum(status, attacker, status별 magnitude, durationOverride) 반복 ← enum→SO 어댑터
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
- `OnTick` — `tickInterval` 마다 (DoT 데미지 발행 등). 전체 만료와 같은 simulation tick이면 `OnTick`을 먼저 처리한 뒤 `OnExpire`한다.
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
- `FixedUpdateNetwork` — 매 tick DoT tick/만료 + modifier 누적 재계산. DoT는 첫 적용 즉시 피해를 주지 않고 첫 `tickInterval` 경과 후 시작하며, 마지막 경계 tick은 포함한다.
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

MVP16 현재 wiring:
- `Assets/_Project/Data/StatusEffects/{FrostEffect,BurnEffect,PoisonEffect,ElectricEffect}.asset`를 생성해 `Player.prefab`의 `_freeze/_burn/_poison/_electric` 슬롯에 연결한다.
- 런타임 `effectKey`는 `NetworkString<_16>` 제약 때문에 `frost/burn/poison/electric` 같은 짧은 키를 사용하고, presentation 이벤트 발행 시 `hit_apply_frost` 등 카드/VFX/SFX key로 변환한다.
- 상태탄 카드를 여러 종류 획득하면 `CardEffect.CurrentStatusEffectMask`에 누적되고, `BulletController.AppliedStatusMask`와 상태별 duration 슬롯(`Freeze/Burn/Poison/ElectricStatusDuration`)을 통해 한 발에 여러 상태를 함께 싣는다. `CurrentStatusEffect` / `AppliedStatus` 단일 enum은 최신 상태 key를 유지하는 호환/분류 경로이며 실제 적용 정본은 mask다.
- 카드가 양수 `statusDuration`을 제공하면 탄환이 해당 상태별 지속시간을 상태 적용까지 전달한다. 0 이하이면 SO의 `baseDuration`을 사용한다.
- 카드 런타임은 `CardData.statusMagnitude`를 두지 않고, `CardEffect`의 상태별 magnitude 슬롯(`Freeze/Burn/Poison/ElectricStatusMagnitude`)에 해당 상태탄 카드 장수(1/2/3)를 저장한다. 이 값은 Burn/Poison DoT tick 피해 계산의 카드 장수 축으로 사용한다. 구형 단일 `StatusMagnitude`는 호환용 내부 경로로만 남긴다.
- 같은 상태이상이 이미 활성인 대상에게 다시 적중하면 `PlayerStatusController`가 Timer를 갱신하고 `Source`를 최신 시전자로 바꾼다. 스택형 효과는 카드 장수와 무관하게 적중 1회당 runtime stack 1개를 추가한다. VFX의 stack scale은 카드 획득 횟수가 아니라 재적중 후 `StatusEffectInstance.Stacks`를 `MagnitudeHint`로 발행한 값을 기준으로 한다. DoT gameplay magnitude는 상태별 카드 장수 magnitude와 runtime stack을 함께 사용한다.
- Burn/Poison DoT tick 수는 `floor(duration / tickInterval)`로 본다. 현재 SO 값 기준 Burn은 3초/0.5초 = 6회, Poison은 3초/1초 = 3회다. tick 피해는 상태탄 카드 장수와 피격 대상의 runtime 상태 스택을 함께 반영한다.

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
| 같은 키 재적용 (스택형) | `Stacks + 1` (maxStack 클램프) + Timer 갱신 + 더 큰 Magnitude 보존 + `Source`를 최신 시전자로 갱신. Magnitude는 카드 장수 같은 gameplay 강도이며 runtime stack 증가량으로 쓰지 않는다. 감전처럼 stun threshold를 가진 효과는 임계 도달 frame에서 stack을 0으로 소비하고 별도 stun timer를 시작해 재적중이 무한 경직으로 이어지지 않게 한다. | 본인(R), A303-349 |
| 같은 키 재적용 (비스택형) | Stacks 유지 + Timer 갱신 + 더 큰 Magnitude 보존 + `Source`를 최신 시전자로 갱신. Magnitude는 필요 시 효과 강도만 표현한다. | 본인(R), A303-349 |
| DoT 데미지 attacker 표시 | `instance.Source` (마지막 시전자) | 김범승 합의 (HP/사망/리스폰 R) |
| DoT tick 순서 | 첫 적용 즉시 피해 없음. `tickInterval`마다 피해, 만료와 같은 tick이면 tick 후 만료 | A303-264/265 후보 |
| 상태이상 + 사망 race | 사망 시점에 active 효과 즉시 cleanup (NetworkArray 슬롯 default 채움) — 후속 결정 | 김범승 합의 |
| 시각 표현 (색·파티클) | Definition.visualTint + IVisualTintEffect → 비주얼팀 wiring | 우수연(R) |

## 6. 본 MVP 단계 효과 4종

| Effect | 분류 | 동작 |
|---|---|---|
| `BurnEffect` | DoT (짧고 강함) | 3초 동안 0.5초마다 _damagePerTick 데미지, 총 6회 |
| `PoisonEffect` | DoT (길고 약함) | 3초 동안 1초마다 _damagePerTick 데미지, 총 3회 |
| `FrostEffect` | Modifier | _moveSpeedMultiplier 곱연산 (A303-317에서 공중 조작 효과 폐기) |
| `ElectricEffect` | Stack + Stun | Stacks ≥ _stunThreshold 도달 시 stack을 0으로 소비하고 별도 stun timer가 baseDuration 동안 경직을 유지 |

MVP 4종 SO는 모두 `isStackable=true`, `maxStack=3`으로 둔다. Burn/Poison은 `Magnitude`를 상태탄 카드 장수, `Stacks`를 피격 대상에게 누적된 runtime 상태 스택으로 해석해 DoT tick 피해를 계산한다. Frost는 현재 `Stacks`를 감속 수치에 곱하지 않으며 재적중 stack은 Timer refresh와 VFX 크기 표현에만 직접 영향을 준다. Electric은 stun threshold 판정에 `Stacks`를 사용하되, 임계 도달 후에는 stack을 0으로 소비하고 `_stackTriggeredStunTimer`가 실제 경직 시간을 관리한다.

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

- 2026-05-20: Burn/Poison DoT 피해는 상태탄 카드 장수 magnitude와 피격 대상 runtime stack을 함께 사용한다. runtime stack 증가는 magnitude와 무관하게 재적중 1회당 +1로 고정한다.
- 2026-05-18: 상태 VFX stack scale 기준을 카드 획득 횟수에서 피격 대상 `StatusEffectInstance.Stacks`로 정정. presentation은 실제 runtime stack을 소비한다.
- 2026-05-18: 상태탄 카드 여러 종류를 한 발에 함께 적용하도록 `CardEffect`/`BulletController` 상태 전달 경로를 단일 enum에서 mask + 상태별 duration으로 확장. 대상의 `PlayerStatusController`는 mask에 포함된 상태별로 `ApplyByEnum`을 반복 호출한다.
- 2026-05-17: A303-260/266/319 후보 재정렬 후 PM 리뷰 기준으로 카드 런타임 공개 경로를 최신 상태 key + `StatusDuration` override + `Source` refresh 범위로 정리. `StatusMagnitude`는 현재 카드 공개 필드가 아니라 직접 API 호출자의 내부 확장 계약으로 축소.
- 2026-05-02: A303-261 도입. 인프라 3 부품 + Common 4종 효과 SO + DevMode 디버그 트리거.
