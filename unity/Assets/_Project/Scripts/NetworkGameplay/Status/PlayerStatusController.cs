using UnityEngine;
using Fusion;
using Colosseum.Contracts;
using Colosseum.NetworkGameplay.Diagnostics;
using Colosseum.NetworkGameplay.VFX;

namespace Colosseum.Status
{
    /// <summary>
    /// 한 플레이어가 보유한 활성 상태이상의 컨테이너 + 매니저.
    /// PlayerHealth / PlayerController / BulletController는 본 컨트롤러를 통해서만 상태이상에 접근한다.
    ///
    /// 핵심 책임:
    ///   - 활성 상태이상 인스턴스 8개까지 보관 (Fusion NetworkArray)
    ///   - FixedUpdateNetwork에서 만료·DoT tick 갱신
    ///   - 외부 hook 노출 (PlayerController가 매 stat 계산 시 호출):
    ///       GetMoveSpeedMultiplier / IsStunned / GetVisualTint
    ///
    /// enum→SO 매핑 (StatusEffect와 SO 사이의 어댑터):
    ///   - Inspector에 4개 SO 슬롯 노출 (Freeze/Burn/Poison/Electric)
    ///   - ApplyByEnum이 enum을 받아 해당 SO를 조회 후 Apply
    ///   - 김범승 후속 PR이 카드 wiring 완료하면 enum→SO 매핑은 단계적으로 폐기 가능
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerStatusController : NetworkBehaviour
    {
        public const int Capacity = 8;
        private const int StatusAppliedVfxSequenceSalt = 61;
        private const int StatusRefreshVfxSequenceSalt = 62;

        [Header("Status Effect 매핑 (enum → SO)")]
        [Tooltip("StatusEffect.Freeze 적용 시 사용할 SO. 미할당 시 효과 발동 안 함.")]
        [SerializeField] private StatusEffectDefinition _freeze;

        [Tooltip("StatusEffect.Burn 적용 시 사용할 SO.")]
        [SerializeField] private StatusEffectDefinition _burn;

        [Tooltip("StatusEffect.Poison 적용 시 사용할 SO.")]
        [SerializeField] private StatusEffectDefinition _poison;

        [Tooltip("StatusEffect.Electric 적용 시 사용할 SO.")]
        [SerializeField] private StatusEffectDefinition _electric;

        [Networked, Capacity(Capacity)]
        private NetworkArray<StatusEffectInstance> _active { get; }

        [Networked] private TickTimer _stackTriggeredStunTimer { get; set; }

        // 주기 modifier 누적 캐시. 매 tick 갱신.
        private float _cachedMoveSpeedMul = 1f;
        private bool _cachedStunned = false;
        private Color _cachedVisualTint = Color.white;
        private int _statusVfxEventOrdinal;

        // ──────────────────────────────────────────────
        //  외부 진입점 (Apply / Remove)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 직접 SO를 받아 효과를 적용. 카드 wiring 완료 후의 정공법.
        /// State Authority에서만 호출 (Fusion 권위 결정).
        /// </summary>
        public void Apply(StatusEffectDefinition def, PlayerRef source, float magnitude = 1f, float durationOverride = 0f)
        {
            if (!Object.HasStateAuthority) return;
            if (def == null) return;
            if (string.IsNullOrEmpty(def.effectKey)) return;

            float duration = ResolveDuration(def, durationOverride);

            // 같은 키 이미 활성: 정책 적용
            int existingIdx = FindByKey(def.effectKey);
            if (existingIdx >= 0)
            {
                var existing = _active[existingIdx];
                float resolvedMagnitude = Mathf.Max(1f, magnitude);
                existing = RefreshExistingStatusInstance(existing, def.isStackable, def.maxStack, source, resolvedMagnitude);
                float vfxStackHint = existing.Stacks;
                if (def.isStackable)
                {
                    // 스택형: 같은 상태 재적중 기준으로 Stacks 증가 + Timer 갱신.
                    // Electric 같은 trigger형 stun은 임계 도달 순간 stack을 소비하고 별도 stun 타이머로 경직을 유지한다.
                    StartStackTriggeredStunIfNeeded(def, ref existing, duration);
                    existing.Timer = TickTimer.CreateFromSeconds(Runner, duration);
                    _active.Set(existingIdx, existing);
                    Rpc_PublishStatusAppliedVfx(def.effectKey, source, transform.position.x, transform.position.y, duration, vfxStackHint, NextStatusVfxSequenceSalt(StatusAppliedVfxSequenceSalt));
                }
                else
                {
                    // 비스택형: Timer 갱신 + 더 큰 magnitude 보존
                    existing.Timer = TickTimer.CreateFromSeconds(Runner, duration);
                    _active.Set(existingIdx, existing);
                    Rpc_PublishStatusAppliedVfx(def.effectKey, source, transform.position.x, transform.position.y, duration, vfxStackHint, NextStatusVfxSequenceSalt(StatusRefreshVfxSequenceSalt));
                }
                DiagLog.Log($"[Colosseum] StatusEffect refresh: {def.effectKey} (stacks={existing.Stacks})");
                return;
            }

            // 새 슬롯 할당
            int slot = FindEmptySlot();
            if (slot < 0)
            {
                Debug.LogWarning($"[Colosseum] StatusEffect Apply 실패: 슬롯 부족 (Capacity={Capacity})");
                return;
            }

            float initialMagnitude = Mathf.Max(1f, magnitude);
            var instance = new StatusEffectInstance
            {
                EffectKey = def.effectKey,
                Timer = TickTimer.CreateFromSeconds(Runner, duration),
                NextTickTimer = def.tickInterval > 0f
                    ? TickTimer.CreateFromSeconds(Runner, def.tickInterval)
                    : TickTimer.None,
                Stacks = 1,
                Source = source,
                Magnitude = initialMagnitude
            };
            _active.Set(slot, instance);

            def.OnApply(this, ref instance);
            float initialVfxStackHint = instance.Stacks;
            StartStackTriggeredStunIfNeeded(def, ref instance, duration);
            _active.Set(slot, instance); // OnApply가 instance를 mutate했을 수 있음

            DiagLog.Log($"[Colosseum] StatusEffect applied: {def.effectKey} from {source} (duration={duration}s)");
            Rpc_PublishStatusAppliedVfx(def.effectKey, source, transform.position.x, transform.position.y, duration, initialVfxStackHint, NextStatusVfxSequenceSalt(StatusAppliedVfxSequenceSalt));
        }

        /// <summary>
        /// enum 기반 진입점 (BulletController / PlayerHealth가 호출).
        /// Inspector에 매핑된 SO를 조회한 뒤 Apply로 위임.
        /// </summary>
        public void ApplyByEnum(Weapon.StatusEffect status, PlayerRef source, float magnitude = 1f, float durationOverride = 0f)
        {
            if (!Object.HasStateAuthority) return;
            var def = GetDefinitionByEnum(status);
            if (def == null) return;
            Apply(def, source, magnitude, durationOverride);
        }

        public static float ResolveDuration(StatusEffectDefinition def, float durationOverride)
        {
            if (durationOverride > 0f)
            {
                return durationOverride;
            }

            return def != null ? Mathf.Max(0f, def.baseDuration) : 0f;
        }

        public static int CalculatePeriodicTickCount(float duration, float tickInterval)
        {
            if (duration <= 0f || tickInterval <= 0f)
            {
                return 0;
            }

            return Mathf.FloorToInt((duration / tickInterval) + 0.0001f);
        }

        public static int CalculateStatusStackDelta(float magnitude)
        {
            return 1;
        }

        private static StatusEffectInstance RefreshExistingStatusInstance(
            StatusEffectInstance existing,
            bool isStackable,
            int maxStack,
            PlayerRef source,
            float resolvedMagnitude)
        {
            existing.Source = source;
            if (isStackable)
            {
                existing.Stacks = Mathf.Min(
                    existing.Stacks + CalculateStatusStackDelta(resolvedMagnitude),
                    maxStack);
            }

            existing.Magnitude = Mathf.Max(existing.Magnitude, resolvedMagnitude);
            return existing;
        }

        public static bool ShouldTriggerStackStun(StatusEffectDefinition def, StatusEffectInstance instance)
        {
            return def is IStunEffect stunner && stunner.IsStunningNow(instance);
        }

        public static StatusEffectInstance ConsumeStacksAfterTriggeredStun(
            StatusEffectDefinition def,
            StatusEffectInstance instance)
        {
            if (ShouldTriggerStackStun(def, instance))
            {
                instance.Stacks = 0;
            }

            return instance;
        }

        public static string GetPresentationEffectKey(string runtimeEffectKey)
        {
            switch (runtimeEffectKey)
            {
                case "freeze":
                case "frost":
                    return "hit_apply_frost";
                case "burn":
                    return "hit_apply_burn";
                case "poison":
                    return "hit_apply_poison";
                case "electric":
                    return "hit_apply_electric_stack";
                default:
                    return runtimeEffectKey;
            }
        }

        private StatusEffectDefinition GetDefinitionByEnum(Weapon.StatusEffect status)
        {
            switch (status)
            {
                case Weapon.StatusEffect.Freeze:   return _freeze;
                case Weapon.StatusEffect.Burn:     return _burn;
                case Weapon.StatusEffect.Poison:   return _poison;
                case Weapon.StatusEffect.Electric: return _electric;
                default: return null;
            }
        }

        // ──────────────────────────────────────────────
        //  매 tick 갱신
        // ──────────────────────────────────────────────

        public override void FixedUpdateNetwork()
        {
            // modifier 누적 재계산 (매 tick 1회)
            float moveMul = 1f;
            bool stunned = IsStackTriggeredStunActive();
            Color tint = Color.white;

            for (int i = 0; i < _active.Length; i++)
            {
                var inst = _active[i];
                if (inst.IsEmpty) continue;

                var def = ResolveDefinition(inst.EffectKey.ToString());
                if (def == null)
                {
                    // SO 매핑 깨짐 — 슬롯 정리
                    _active.Set(i, default);
                    continue;
                }

                // State Authority만 만료/tick 처리 (결정론)
                if (Object.HasStateAuthority)
                {
                    if (def.tickInterval > 0f && inst.NextTickTimer.Expired(Runner))
                    {
                        def.OnTick(this, ref inst);
                        inst.NextTickTimer = TickTimer.CreateFromSeconds(Runner, def.tickInterval);
                        _active.Set(i, inst);
                    }

                    if (inst.Timer.Expired(Runner))
                    {
                        def.OnExpire(this, ref inst);
                        _active.Set(i, default);
                        continue;
                    }
                }

                // modifier 누적 — 효과별 SO가 멤버 메서드로 노출 가능하지만,
                // 여기선 서브클래스가 직접 instance.Magnitude / Stacks를 해석해 modifier 반환하는 패턴.
                if (def is IMovementModifierEffect mover)
                {
                    moveMul *= mover.GetMoveSpeedMultiplier(inst);
                }
                if (def is IStunEffect stunner)
                {
                    if (stunner.IsStunningNow(inst)) stunned = true;
                }
                if (def is IVisualTintEffect tinter)
                {
                    var c = tinter.GetVisualTint(inst);
                    tint = MultiplyColor(tint, c);
                }
            }

            _cachedMoveSpeedMul = moveMul;
            _cachedStunned = stunned;
            _cachedVisualTint = tint;
        }

        // ──────────────────────────────────────────────
        //  외부 modifier 조회 (PlayerController / Animator가 호출)
        // ──────────────────────────────────────────────

        public float GetMoveSpeedMultiplier() => _cachedMoveSpeedMul;
        public bool IsStunned() => _cachedStunned || IsStackTriggeredStunActive();
        public Color GetVisualTint() => _cachedVisualTint;

        // ──────────────────────────────────────────────
        //  내부 helper
        // ──────────────────────────────────────────────

        private int FindByKey(string key)
        {
            for (int i = 0; i < _active.Length; i++)
            {
                if (_active[i].EffectKey == key) return i;
            }
            return -1;
        }

        private int FindEmptySlot()
        {
            for (int i = 0; i < _active.Length; i++)
            {
                if (_active[i].IsEmpty) return i;
            }
            return -1;
        }

        private StatusEffectDefinition ResolveDefinition(string key)
        {
            // enum 슬롯 4개 우선 검색. 새 SO가 enum과 무관해도 effectKey 매칭이면 작동.
            if (_freeze != null && _freeze.effectKey == key) return _freeze;
            if (_burn != null && _burn.effectKey == key) return _burn;
            if (_poison != null && _poison.effectKey == key) return _poison;
            if (_electric != null && _electric.effectKey == key) return _electric;
            return null;
        }

        private void StartStackTriggeredStunIfNeeded(
            StatusEffectDefinition def,
            ref StatusEffectInstance instance,
            float duration)
        {
            if (!ShouldTriggerStackStun(def, instance))
            {
                return;
            }

            _stackTriggeredStunTimer = TickTimer.CreateFromSeconds(Runner, duration);
            instance = ConsumeStacksAfterTriggeredStun(def, instance);
        }

        private bool IsStackTriggeredStunActive()
        {
            return Runner != null && !_stackTriggeredStunTimer.ExpiredOrNotRunning(Runner);
        }

        private static Color MultiplyColor(Color a, Color b)
        {
            return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        }

        private int NextStatusVfxSequenceSalt(int baseSalt)
        {
            unchecked
            {
                _statusVfxEventOrdinal++;
                return baseSalt + (_statusVfxEventOrdinal * 1000);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishStatusAppliedVfx(string effectKey, PlayerRef source, float x, float y, float duration, float stackHint, int sequenceSalt)
        {
            string presentationEffectKey = GetPresentationEffectKey(effectKey);
            VfxEventBus.Publish(VfxPayloadFactory.Create(
                VfxEventType.StatusApplied,
                presentationEffectKey,
                presentationEffectKey,
                Runner,
                Object,
                source,
                Object.InputAuthority,
                new Vector2(x, y),
                Vector2.up,
                1f,
                duration,
                stackHint,
                VfxAttachPoint.CharacterBody,
                sequenceSalt));
        }
    }

    /// <summary>이동 modifier 노출 인터페이스. Frost 같은 효과가 구현.</summary>
    public interface IMovementModifierEffect
    {
        float GetMoveSpeedMultiplier(StatusEffectInstance instance);
    }

    /// <summary>경직 효과 노출 인터페이스. Electric 같은 효과가 구현.</summary>
    public interface IStunEffect
    {
        bool IsStunningNow(StatusEffectInstance instance);
    }

    /// <summary>시각 tint 노출 인터페이스. 모든 효과가 선택적으로 구현 가능.</summary>
    public interface IVisualTintEffect
    {
        Color GetVisualTint(StatusEffectInstance instance);
    }
}
