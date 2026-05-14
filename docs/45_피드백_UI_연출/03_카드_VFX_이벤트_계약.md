---
last_updated: 2026-05-08
audience: ai-first
---

# 카드 VFX 이벤트 계약

> 상태: candidate
> 범위: MVP16 카드 효과를 presentation이 읽을 수 있는 VFX 사건 계약 후보, VFX 한정 UI 후보, placeholder 구현 기준 후보.
> 중단 조건: 이 문서가 network truth, 카드 선택 StateAuthority 확정, 기존 HUD 정보구조, 캐릭터 최종 아트 구조를 덮어써야 하면 중단하고 각 DRI 확인을 받는다.

이 문서는 카드 효과가 전투에서 발동했을 때 어떤 사건을 presentation 레이어로 내보낼지 정리하는 후보 문서다. 목적은 VFX prefab을 먼저 확정하는 것이 아니라, 전투 truth와 시각 표현 사이의 계약 후보를 먼저 좁히는 것이다.

이 문서의 상태는 plain `candidate`다. 구현 이슈에서 사용하려면 작업 브리핑에 임시 가정, 검증 기준, 영향 DRI 확인을 남긴다.

## 핵심 결론

- 전투 결과는 `NetworkGameplay`과 StateAuthority가 결정한다.
- VFX는 전투 결과를 읽어 재생하는 presentation이며 게임 결과를 바꾸지 않는다.
- 카드 효과 VFX는 `Contracts`의 사건 payload 또는 동등한 읽기 전용 DTO를 통해 presentation이 소비한다.
- 실제 고품질 캐릭터 부착형 VFX는 캐릭터 sprite/material/animation/attach point가 확정된 뒤 후속 이슈로 분리한다.
- MVP 1차 구현은 placeholder 품질이어도 되며, 중복 재생 방지와 전투 복귀 안정성을 우선 검증한다.

## 역할 경계 후보

아래 표는 A303-298 문서 작업에서 정리한 임시 라우팅 후보이며, `50_기술_구조_기준/05_파일경로별_DRI.md`의 accepted DRI 매트릭스를 대체하지 않는다. 실제 Jira assignee, MR reviewer, CODEOWNERS 변경은 별도 결정 또는 MR에서 확정한다.

| 영역 | 주 책임 | 기준 |
|---|---|---|
| 광범위 UI, HUD, 캐릭터, 공통 비주얼 톤 | 우수연 | 화면 정보구조, 캐릭터 최종 sprite/material/animation, 공통 아트 톤, 카드 UI의 시각 완성도 |
| 카드 효과 VFX, VFX 한정 UI, VFX 이벤트 계약 | 김범승 | 카드 효과 사건을 어떤 preset/VFX/toast/indicator로 표시할지, payload와 dedupe 기준, placeholder VFX manager 범위 |
| map 디자인, 전장 구조, 카메라 기준 | 박병준 | 전장 구조, map/greybox/카메라 기준, 씬 락과 전장 문서 정합성 |
| 전투/네트워크 판정 사건 생성 | 최진우 | 발사, 피격, 반사, 폭발, 상태이상, 라운드 전환의 authoritative 결과 생성 |

경계 문장 후보: 기본 HUD와 캐릭터 최종 룩은 우수연 범위, 카드 효과가 발동했다는 사실을 짧게 보여주는 VFX 전용 toast/marker/indicator는 김범승 범위로 라우팅한다. map 디자인과 전장 구조는 박병준 범위로 라우팅한다.

## 계층 원칙

```
NetworkGameplay
  - 전투 truth 생성
  - 카드 적용, 탄환 충돌, 상태이상 적용
  - presentation event emit point 제공

Contracts
  - VFX 사건 payload 또는 읽기 전용 event DTO
  - Unity prefab, ParticleSystem, MMFeedbacks, ScriptableObject 참조 금지

Presentation
  - VFX event listener
  - preset lookup
  - pooled placeholder prefab, particle, MMFeedbacks, toast 재생
```

## 금지선

- VFX manager가 데미지, 넉백, 상태이상 지속 시간, 카드 선택 결과를 계산하지 않는다.
- `NetworkGameplay`이 presentation prefab, particle prefab, toast UI prefab을 직접 참조하지 않는다.
- `GameplayProfile`에 particle prefab, VFX 색상, toast duration, camera shake profile 같은 presentation 값을 섞지 않는다.
- 상태이상 tick마다 과도한 toast나 particle을 새로 생성하지 않는다.
- 캐릭터 최종 prefab 내부 구조를 VFX 작업이 선점하지 않는다.

## 이벤트 타입 후보

MVP 1차 구현에서 모든 타입을 동시에 구현할 필요는 없다. 아래 목록은 계약 후보이며, 실제 구현 이슈에서는 우선순위를 다시 고른다.

| 이벤트 | 의미 | 1차 우선순위 |
|---|---|---|
| `CardEffectApplied` | 카드 효과가 player runtime state에 적용됨 | 필수 |
| `CardEffectToastRequested` | 카드 적용 결과를 짧게 설명하는 toast 요청 | 필수 |
| `ProjectileSpawnedWithModifier` | 변형된 투사체가 생성됨 | 후보 |
| `ProjectileImpact` | 투사체가 피격/충돌함 | 필수 후보 |
| `SegmentBoundaryAdvanced` | StateAuthority가 구간 돌파/전선 이동을 확정했으며, `segment_advance` world-space sweep VFX와 SFX가 소비하는 presentation 사건 | 필수 후보 |
| `ProjectileReflected` | 반사탄이 반사됨 | 후보 |
| `ProjectilePierced` | 관통탄이 관통함 | 후보 |
| `ExplosionTriggered` | 폭발탄이 폭발함 | 필수 후보 |
| `PitFallDeath` | pit 낙하/사망 순간이 확정됨 | 필수 후보 |
| `StatusApplied` | 상태이상이 적용됨 | 필수 후보 |
| `StatusTick` | 상태이상 주기 효과가 발생함 | 1차 제외 |
| `StatusExpired` | 상태이상이 종료됨 | 후보 |
| `ReloadStarted` | 재장전 시작 | 후보 |
| `ReloadCompleted` | 재장전 완료 | 후보 |
| `OverchargeReady` | 오버차지 발사 준비 완료 | 필수 후보 |
| `OverchargeShotFired` | 오버차지 탄 발사 | 후보 |

## payload pseudo-code sample

아래 코드는 compile-ready API가 아니라 필드 책임을 설명하기 위한 pseudo-code sample이다. 실제 타입명, namespace, 필드명은 후속 구현 이슈에서 `Contracts` 구조와 asmdef 경계에 맞춰 확정한다.

`Contracts`에 둘 payload 후보는 Unity/Fusion object를 직접 들고 있지 않는 plain data여야 한다. `UnityEngine.Vector2` 의존을 피해야 하는 경우 `VfxVector2` 같은 plain struct를 사용할 수 있다.

```csharp
public readonly struct VfxEventPayload
{
    public readonly VfxEventType EventType;
    public readonly string CardKey;
    public readonly string EffectKey;
    public readonly int SourceTick;
    public readonly int SourceObjectId;
    public readonly int SourceEventSequence;
    public readonly VfxPredictionState PredictionState;
    public readonly int SourcePlayerSlot;
    public readonly int TargetPlayerSlot;
    public readonly VfxVector2 WorldPosition;
    public readonly VfxVector2 Direction;
    public readonly float RadiusHint;
    public readonly float DurationHint;
    public readonly float MagnitudeHint;
    public readonly VfxAttachPoint AttachPoint;
}
```

| 필드 | 기준 |
|---|---|
| `CardKey` | 카드 catalog/data와 연결되는 식별자. 카드 수치 자체를 복사하지 않음 |
| `EffectKey` | VFX preset lookup용 효과 식별자 |
| `SourceTick` | Fusion tick 또는 동등한 사건 발생 순서 hint |
| `SourceObjectId` | NetworkObject 또는 gameplay object의 안정 식별자. Contracts에는 engine object 참조 대신 id만 둠 |
| `SourceEventSequence` | 같은 object에서 발생한 단발 사건 순번 |
| `PredictionState` | predicted/confirmed/corrected/rejected 구분 |
| `RadiusHint`, `DurationHint`, `MagnitudeHint` | 표현 hint. 판정 수치의 정본이 아님 |
| `AttachPoint` | `World`, `CharacterHead`, `CharacterBody`, `CharacterFeet`, `WeaponMuzzle`, `HudOverlay`, `CardSlot` 같은 부착 위치 계약 |

## rollback/resimulation 중복 재생 방지

Fusion rollback/resimulation 환경에서는 같은 전투 사건이 presentation에 여러 번 관측될 수 있다. VFX는 게임 결과가 아니므로 같은 사건을 반복 재생하면 안 된다.

- 기본 dedupe key는 `(EventType, SourceObjectId, SourceEventSequence)`로 둔다.
- object id가 없으면 `(EventType, SourcePlayerSlot, TargetPlayerSlot, SourceTick, EffectKey)`를 fallback key로 사용한다.
- predicted 이벤트는 즉시 재생할 수 있지만, confirmed 이벤트가 같은 key로 오면 재생하지 않고 위치/강도만 보정한다.
- rejected/corrected 이벤트가 오면 같은 key의 pending VFX를 중단하거나 fade-out한다.
- dedupe cache는 라운드 종료, player despawn, scene transition 시 clear한다.
- `PitFallDeath` VFX/SFX는 실제 낙하/사망 순간의 pit world position만 소비한다. 낙하 전 사전경고 VFX/SFX를 만들지 않는다.
- `StatusTick`은 tick마다 새 VFX를 만들지 않고 상태 시작/종료 또는 낮은 빈도의 UI 갱신으로 제한한다.
- `SegmentBoundaryAdvanced`의 `segment_sweep` VFX와 SFX one-shot은 같은 `(EventType, SourceObjectId, SourceEventSequence)` 기준으로 중복 재생을 막는다. VFX는 payload의 world position과 `VfxFeedbackProfile.asset`의 sweep duration/radius 값을 표현값으로 쓰고, SFX는 `SegmentAdvanceSfxPresentationManager`가 event/effect key와 world position을 받아 별도 one-shot으로 처리한다. fallback key는 `EffectKey=segment_advance`를 포함해 같은 구간 돌파 사건을 클라이언트 resimulation마다 새 소리나 sweep으로 만들지 않게 한다.

## VFX 한정 UI 범위

| 포함 | 제외 |
|---|---|
| 카드 적용 직후 짧은 toast | 체력, 탄약, 라운드, 구간 진행도 같은 기본 HUD |
| 상태이상 적용/종료 아이콘 또는 짧은 marker | 상시 버프 목록처럼 HUD 정보구조에 편입되는 UI |
| 오버차지 준비/소비 indicator | 메뉴, 로비, 설정 UI |
| 폭발 범위 또는 착탄 순간의 짧은 world-space indicator | 캐릭터 최종 sprite/animation/material |
| 반사/관통/분열 등 projectile modifier trail/marker | 전투 결과를 결정하는 상태 저장 |
| `segment_advance` 구간 돌파 순간의 `segment_sweep` world-space marker와 local SFX one-shot | 구간 진행도 HUD 또는 라운드 결과 truth |

VFX 한정 UI는 짧게 나타났다 사라지는 feedback이다. 플레이어가 계속 읽어야 하는 정보는 `02_UI_HUD_정보구조.md`의 HUD 범위로 넘긴다.

## MVP16 카드별 VFX 분류

| cardKey | 1차 VFX 타입 | UI 필요 | 캐릭터 디자인 의존 |
|---|---|---|---|
| `projectile_size_up` | projectile scale/trail 강조 | 낮음 | 낮음 |
| `projectile_speed_up` | 빠른 trail/속도선 | 낮음 | 낮음 |
| `hit_knockback_up` | hit impact 방향/밀림 강조 | 낮음 | 중간 |
| `reload_time_down` | reload completion flash | 중간 | 낮음 |
| `ammo_capacity_up` | 카드 적용 toast 중심 | 중간 | 낮음 |
| `projectile_heavy` | 무거운 trail/착탄 dust | 낮음 | 낮음 |
| `hit_apply_poison` | poison hit + 상태 icon | 높음 | 높음 |
| `hit_apply_burn` | burn hit + 상태 icon | 높음 | 높음 |
| `hit_apply_frost` | frost hit + 상태 icon | 높음 | 높음 |
| `hit_apply_electric_stack` | electric stack + stun flash | 높음 | 높음 |
| `reload_overcharge` | ready glow + fired burst | 높음 | 중간 |
| `projectile_reflect` | reflect spark/trail color change | 낮음 | 낮음 |
| `projectile_explosion` | explosion impact + radius hint | 중간 | 낮음 |
| `projectile_pierce` | pierce streak/afterimage | 낮음 | 낮음 |
| `projectile_multishot` | multi-shot muzzle burst | 낮음 | 낮음 |
| `projectile_gravity_zero` | straight beam-like trail | 낮음 | 낮음 |

## placeholder 구현 순서

### 1차: 계약과 수신 경로

- `CardEffectApplied`
- `CardEffectToastRequested`
- `ProjectileImpact`
- `ExplosionTriggered`
- `StatusApplied`
- `OverchargeReady`

성공 기준은 시각 품질이 아니라 전투 코드가 presentation prefab을 모르고도 event가 VFX manager까지 도착하는 것이다.

### 2차: 데모 가독성 placeholder

- 폭발탄: 착탄 폭발 + 반경 hint.
- 상태이상 4종: 상태 icon + 색상/particle placeholder.
- 오버차지: 준비/소비 표시.

### 3차: 캐릭터 디자인 이후 polish

- `CharacterHead`, `CharacterBody`, `CharacterFeet`, `WeaponMuzzle` attach point에 붙는 상태이상 VFX.
- palette swap, outline, hit flash, overlay.
- 캐릭터 animation과 sync되는 burn/frost/electric/poison 표현.

## preset 배치 기준

후보 경로 예시:

```text
Assets/_Project/Data/Presentation/VfxFeedbackProfile.asset
Assets/_Project/Data/Presentation/CardVfxPreset.asset
Assets/_Project/Data/Presentation/StatusVfxPreset.asset
```

위 경로는 실제 asset 생성 또는 파일 이동을 의미하지 않는 예시다. preset은 presentation 설정이며 전투 수치의 정본이 아니다. lookup key 후보는 `CardKey`, `EffectKey`, `EventType` 조합을 기본으로 검토한다.
profile lookup이 실패해 fallback preset을 쓰더라도 `segment_advance`처럼 시연 핵심 피드백은 profile의 대표 색상 계열과 어긋나지 않게 맞춘다.

## 후속 이슈 분리 기준

A303-298은 계약/docs 정리 범위다. 실제 구현은 후속 이슈로 나눈다.

| 후속 범위 | 권장 DoD |
|---|---|
| VFX manager + preset lookup + dedupe cache | event payload 수신, dedupe key 적용, placeholder 1~2개 smoke |
| MVP 주요 placeholder asset 연결 | 폭발탄, 상태이상, 오버차지 중 demo 가독성 높은 효과 연결 |
| 상태이상 UI/indicator 확장 | `StatusApplied`/`StatusExpired` 중심 표현, `StatusTick` 반복 toast 금지 |
| 캐릭터 부착형 polish | 캐릭터 attach point 확정 후 body/head/feet 기반 VFX 적용 |

## 검증 질문

| no | 질문 |
|---|---|
| 1 | VFX가 전투 결과를 바꾸지 않는가? |
| 2 | event payload가 card/effect id, 위치, 강도 hint만 담고 판정 수치를 소유하지 않는가? |
| 3 | rollback/resimulation 상황에서 같은 사건이 반복 재생되지 않는가? |
| 4 | VFX 한정 UI가 기본 HUD 정보구조를 침범하지 않는가? |
| 5 | 캐릭터 최종 prefab 구조를 가정하지 않고 attach point 계약과 fallback으로 버티는가? |
| 6 | placeholder VFX 실패가 spawn, 발사, 피격, 라운드 전환, 카드 선택 후 전투 복귀를 막지 않는가? |

## 관련 문서

- [01_타격감_피드백_기준.md](01_타격감_피드백_기준.md)
- [02_UI_HUD_정보구조.md](02_UI_HUD_정보구조.md)
- [40_카드_무기_설계/02_카드_효과_분류_기준.md](../40_카드_무기_설계/02_카드_효과_분류_기준.md)
- [50_기술_구조_기준/02_네트워크_truth_표.md](../50_기술_구조_기준/02_네트워크_truth_표.md)
- [50_기술_구조_기준/05_파일경로별_DRI.md](../50_기술_구조_기준/05_파일경로별_DRI.md)
