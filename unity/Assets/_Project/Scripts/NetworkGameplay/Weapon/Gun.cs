using UnityEngine;
using Fusion;
using Colosseum.Network;
using Colosseum.Card;
using Colosseum.Game;
using Colosseum.Contracts;
using Colosseum.NetworkGameplay.Diagnostics;
using Colosseum.NetworkGameplay.VFX;
using Colosseum.Utils;

namespace Colosseum.Weapon
{
    public partial class Gun : NetworkBehaviour
    {
        partial void OnDevModeSpawned();

        [Header("Gameplay Profile")]
        [Tooltip("Assets/_Project/Data/GameplayProfile.asset 드래그. 미할당 시 fallback 값 사용 + LogError.")]
        [SerializeField] private GameplayProfile _profile;

        [Header("Fire Settings")]
        // [A303-171] Visual-only projectile prefab (BulletVisual MonoBehaviour 부착).
        // NetworkObject 기반 _bulletPrefab 은 단계 7d 에서 완전 폐기되어 본 prefab 만 사용.
        [SerializeField] private GameObject _bulletVisualPrefab;
        [SerializeField] private Transform _muzzle;

        // Profile 미할당 fallback. 마이그레이션 안전망.
        private const float FallbackFireInterval = 0.5f;
        private const int FallbackMagazineCapacity = 6;
        private const float FallbackReloadDuration = 1.5f;
        private const int ReloadStartedVfxSequenceSalt = 100;
        private const int ReloadCompletedVfxSequenceSalt = 101;
        private const int OverchargeReadyVfxSequenceSalt = 102;
        private const int OverchargeShotFiredVfxSequenceSalt = 103;
        private const int ProjectileModifierSpawnVfxSequenceSaltBase = 71;

        private float FireInterval => _profile != null ? _profile.combat.baseline.fireInterval : FallbackFireInterval;
        private int BaseMagazineCapacity => _profile != null ? _profile.combat.baseline.ammoCapacity : FallbackMagazineCapacity;
        public int MagazineCapacity => CalculateMagazineCapacity(
            BaseMagazineCapacity,
            _cardEffect != null ? _cardEffect.BonusAmmoCapacity : 0);
        private float ReloadDurationBase => _profile != null ? _profile.combat.baseline.reloadTime : FallbackReloadDuration;

        [Networked] public float FireRateMultiplier { get; set; } = 1f;
        [Networked] public int ExtraBullets { get; set; } = 0;
        [Networked] private TickTimer _fireCooldown { get; set; }
        [Networked] private Vector2 _networkedAimDir { get; set; }

        // [A303-171] BulletVisual instance 식별자. server 가 매 발사 시 ++ 발급해 Rpc_FireBullet 으로 전파.
        // 후속 단계의 BulletVisualRegistry 가 이 id 로 Rpc_BulletHit 시 인스턴스 추적.
        [Networked] private int _nextBulletId { get; set; }

        /// <summary>
        /// A303-168: 현재 aim 방향(unit vector). PlayerAnimatorController가 팔/총 시각 계산용으로 읽음.
        /// FixedUpdateNetwork에서 InputAuthority가 제출한 world target을 StateAuthority가 확정한 최종 발사 방향.
        /// </summary>
        public Vector2 AimDirection => _networkedAimDir;
        public Vector2 MuzzleWorldPosition => GetMuzzlePosition();

        // 탄창 상태
        [Networked] public int CurrentAmmo { get; set; }
        [Networked] private TickTimer _reloadTimer { get; set; }
        [Networked] public NetworkBool IsReloading { get; set; }
        [Networked] public float ReloadDuration { get; set; }

        // 과충전: 재장전 직후 첫 발 데미지 증폭
        [Networked] public NetworkBool IsOvercharged { get; set; }

        private CardEffect _cardEffect;
        private CardDeck _cardDeck;

        /// <summary>
        /// 재장전 진행률 (0 → 1). UI에서 참조.
        /// </summary>
        public float ReloadProgress
        {
            get
            {
                if (!IsReloading || ReloadDuration <= 0f) return 0f;
                float? remaining = _reloadTimer.RemainingTime(Runner);
                if (!remaining.HasValue) return 1f;
                return 1f - (remaining.Value / ReloadDuration);
            }
        }

        public override void Spawned()
        {
            if (_profile == null)
            {
                Debug.LogError($"[Colosseum] Gun on {gameObject.name}: GameplayProfile 미할당. Fallback 값 사용 중. " +
                    $"Player.prefab → Gun → Profile 슬롯에 GameplayProfile.asset 드래그 필요.");
            }

            // 부모(Player)에서 CardEffect 컴포넌트 찾기
            _cardEffect = GetComponentInParent<CardEffect>();
            CurrentAmmo = MagazineCapacity;
            OnDevModeSpawned();
        }

        public override void FixedUpdateNetwork()
        {
            // 재장전 완료 체크
            if (IsReloading && _reloadTimer.Expired(Runner))
            {
                IsReloading = false;
                CurrentAmmo = MagazineCapacity;
                PublishWeaponVfx(
                    VfxEventType.ReloadCompleted,
                    VfxPayloadFactory.GetReloadEffectKey(VfxEventType.ReloadCompleted),
                    0.7f,
                    1f,
                    ReloadCompletedVfxSequenceSalt);

                // 과충전 활성화 (배율이 1 초과일 때만)
                if (_cardEffect != null && _cardEffect.OverchargeDamageMultiplier > 1f)
                {
                    IsOvercharged = true;
                    PublishWeaponVfx(
                        VfxEventType.OverchargeReady,
                        VfxPayloadFactory.GetReloadEffectKey(VfxEventType.OverchargeReady),
                        1f,
                        _cardEffect.OverchargeDamageMultiplier,
                        OverchargeReadyVfxSequenceSalt);
                    DiagLog.Log($"[Colosseum] Gun: 과충전 활성화! 다음 발 x{_cardEffect.OverchargeDamageMultiplier}");
                }

                DiagLog.Log($"[Colosseum] Gun: 재장전 완료! 탄약: {CurrentAmmo}/{MagazineCapacity}");
            }

            if (ShouldSuppressWeaponForCardSelection(IsCardSelectionPending()))
            {
                return;
            }

            if (GetInput(out NetworkInputData input))
            {
                Vector2 origin = _muzzle != null ? (Vector2)_muzzle.position : (Vector2)transform.position;
                _networkedAimDir = CalculateAimDirection(origin, input.AimDirection);

                // 재장전 중에는 발사 불가
                if (IsReloading) return;

                if (input.IsFirePressed && CurrentAmmo > 0 && _fireCooldown.ExpiredOrNotRunning(Runner))
                {
                    Fire();
                    CurrentAmmo--;

                    float cooldown = FireInterval / FireRateMultiplier;
                    _fireCooldown = TickTimer.CreateFromSeconds(Runner, cooldown);

                    // 탄창이 비면 자동 재장전 시작
                    if (CurrentAmmo <= 0)
                    {
                        StartReload();
                    }
                }
            }
        }

        // A303-168: Gun.Render()의 자체 회전 적용은 PlayerAnimatorController.LateUpdate가 덮어써서
        // dead path였음. 회전·위치 모두 PlayerAnimatorController가 단일 source. Render() 자체 제거.
        // aim 방향이 필요한 곳은 AimDirection public getter로 _networkedAimDir 직접 조회.

        /// <summary>
        /// 산탄총 스프레드: 각 탄이 ±_spreadAngle 범위 내에서 랜덤 각도로 퍼진다.
        /// </summary>
        [Header("Spread Settings")]
        [SerializeField] private float _spreadAngle = 25f;

        private static Vector2 CalculateAimDirection(Vector2 origin, Vector2 target)
        {
            Vector2 aimDir = target - origin;
            return aimDir == Vector2.zero ? Vector2.right : aimDir.normalized;
        }

        public static bool ShouldSuppressWeaponForCardSelection(bool isCardSelectionPending)
        {
            return isCardSelectionPending;
        }

        private bool IsCardSelectionPending()
        {
            if (_cardDeck == null)
            {
                _cardDeck = SceneSearch.FindInScene<CardDeck>(gameObject.scene);
            }

            try
            {
                return _cardDeck != null &&
                       _cardDeck.Object != null &&
                       _cardDeck.Object.IsValid &&
                       _cardDeck.IsDrawing;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
            catch (System.FieldAccessException)
            {
                return false;
            }
        }

        private void Fire()
        {
            if (!Object.HasStateAuthority) return;

            Vector2 fireDir = _networkedAimDir;

            // 기본 1발은 정확히 조준 방향으로
            SpawnBullet(fireDir, 0, 101);

            // 추가 탄환은 산탄총처럼 랜덤 스프레드
            for (int i = 0; i < ExtraBullets; i++)
            {
                float randomAngle = Random.Range(-_spreadAngle, _spreadAngle);
                Vector2 spreadDir = Rotate(fireDir, randomAngle);
                SpawnBullet(spreadDir, i + 1, 102 + i);
            }
        }

        private void SpawnBullet(Vector2 direction, int projectileOrdinal, int shotSequenceSalt)
        {
            Vector2 muzzlePosition = GetMuzzlePosition();
            int shotSourceTick = VfxPayloadFactory.GetSourceTick(Runner);
            int shotSourceEventSequence = VfxPayloadFactory.BuildSourceEventSequence(shotSourceTick, shotSequenceSalt);

            // [A303-171 단계 7b] Runner.Spawn (NetworkObject 기반) 경로 제거.
            // 발사 정보는 Rpc_FireBullet (visual) + BulletServerSimulation.OnFireBullet (server sim) 단일 경로.

            // Overcharge VFX 발행 + IsOvercharged false 처리 (기존 NetworkObject 경로에서 이전).
            float overchargeMul = 1f;
            if (IsOvercharged && _cardEffect != null)
            {
                overchargeMul = _cardEffect.OverchargeDamageMultiplier;
                PublishWeaponVfx(
                    VfxEventType.OverchargeShotFired,
                    VfxPayloadFactory.GetReloadEffectKey(VfxEventType.OverchargeShotFired),
                    1f,
                    overchargeMul,
                    OverchargeShotFiredVfxSequenceSalt);
                DiagLog.Log($"[Colosseum] Overcharged shot! DMG x{overchargeMul}");
                IsOvercharged = false;
            }

            Rpc_PublishShotSfx(muzzlePosition.x, muzzlePosition.y, direction.x, direction.y, shotSourceTick, shotSourceEventSequence);

            // [A303-171] Visual-only projectile 발사 — 모든 client 에서 BulletVisual instance 생성.
            int bulletId = ++_nextBulletId;
            float bulletSpeed = _profile != null ? _profile.combat.baseline.projectileSpeed : 32.2f;
            float bulletGravity = _profile != null ? _profile.combat.baseline.projectileGravityScale : 1f;
            float bulletLifetime = _profile != null ? _profile.combat.baseline.projectileLifetimeSeconds : 5f;

            // 카드 효과 — server-side simulation 인자로 전달.
            float baselineDamage = _profile != null ? _profile.combat.baseline.directHitDamage : 20f;
            float dmgMul = _cardEffect != null ? _cardEffect.DamageMultiplier : 1f;
            float speedMul = _cardEffect != null ? _cardEffect.BulletSpeedMultiplier : 1f;
            float sizeMul = _cardEffect != null ? _cardEffect.BulletSizeMultiplier : 1f;
            float gravityMod = _cardEffect != null ? _cardEffect.GravityModifier : 0f;
            int bounceCount = _cardEffect != null ? _cardEffect.BonusBounce : 0;
            int pierceCount = _cardEffect != null ? _cardEffect.BonusPierce : 0;
            float knockbackForce = _cardEffect != null ? _cardEffect.KnockbackForce : 0f;
            float explosionRadius = _cardEffect != null ? _cardEffect.ExplosionRadius : 0f;
            float explosionRatio = _cardEffect != null ? _cardEffect.ExplosionDamageRatio : 0.5f;
            int statusMask = _cardEffect != null ? _cardEffect.CurrentStatusEffectMask : 0;
            float statusMag = _cardEffect != null ? _cardEffect.StatusMagnitude : 1f;
            float freezeMag = _cardEffect != null ? _cardEffect.FreezeStatusMagnitude : statusMag;
            float burnMag = _cardEffect != null ? _cardEffect.BurnStatusMagnitude : statusMag;
            float poisonMag = _cardEffect != null ? _cardEffect.PoisonStatusMagnitude : statusMag;
            float electricMag = _cardEffect != null ? _cardEffect.ElectricStatusMagnitude : statusMag;
            float freezeDur = _cardEffect != null ? _cardEffect.FreezeStatusDuration : 0f;
            float burnDur = _cardEffect != null ? _cardEffect.BurnStatusDuration : 0f;
            float poisonDur = _cardEffect != null ? _cardEffect.PoisonStatusDuration : 0f;
            float electricDur = _cardEffect != null ? _cardEffect.ElectricStatusDuration : 0f;
            float finalDamage = baselineDamage * dmgMul * overchargeMul;

            Rpc_FireBullet(
                bulletId,
                Object.InputAuthority,
                muzzlePosition.x, muzzlePosition.y,
                direction.x, direction.y,
                bulletSpeed * speedMul,
                bulletGravity + gravityMod,
                bulletLifetime,
                sizeMul);

            BulletServerSimulation.Instance?.OnFireBullet(
                bulletId,
                Object.InputAuthority,
                muzzlePosition,
                direction,
                bulletSpeed * speedMul,
                bulletGravity + gravityMod,
                bulletLifetime,
                finalDamage,
                bounceCount,
                pierceCount,
                knockbackForce,
                explosionRadius,
                explosionRatio,
                statusMask,
                freezeMag,
                burnMag,
                poisonMag,
                electricMag,
                freezeDur,
                burnDur,
                poisonDur,
                electricDur,
                sizeMul);
        }

        // [A303-171 단계 7b] PublishProjectileSpawnedWithModifierVfxIfNeeded 제거 — BulletController 의존.
        // ProjectileSpawnedWithModifier VFX 발행은 단계 8 에서 visual-only 경로 hook 으로 재구현 검토.

        private void StartReload()
        {
            if (IsReloading) return;

            IsReloading = true;

            // CardEffect의 ReloadSpeedMultiplier 적용 (높을수록 빠르게 장전)
            float reloadSpeed = ReloadDurationBase;
            if (_cardEffect != null)
            {
                reloadSpeed /= _cardEffect.ReloadSpeedMultiplier;
            }

            ReloadDuration = reloadSpeed;
            _reloadTimer = TickTimer.CreateFromSeconds(Runner, reloadSpeed);
            PublishWeaponVfx(
                VfxEventType.ReloadStarted,
                VfxPayloadFactory.GetReloadEffectKey(VfxEventType.ReloadStarted),
                0.55f,
                reloadSpeed,
                ReloadStartedVfxSequenceSalt);
            DiagLog.Log($"[Colosseum] Gun: 재장전 시작! ({reloadSpeed:F2}초)");
        }

        private Vector2 GetMuzzlePosition()
        {
            return _muzzle != null ? (Vector2)_muzzle.position : (Vector2)transform.position;
        }

        private void PublishWeaponVfx(VfxEventType eventType, string effectKey, float radius, float magnitude, int sequenceSalt)
        {
            Vector2 vfxPosition = GetMuzzlePosition();
            Rpc_PublishWeaponVfx((int)eventType, effectKey, vfxPosition.x, vfxPosition.y, radius, magnitude, sequenceSalt);
        }

        public bool TryPublishProjectileRuntimeVfx(
            VfxEventType eventType,
            string effectKey,
            int projectileSourceObjectId,
            PlayerRef sourcePlayer,
            PlayerRef targetPlayer,
            Vector2 position,
            Vector2 direction,
            float radius,
            float magnitude,
            int sourceTick,
            int sourceEventSequence)
        {
            if (Object == null || !Object.HasStateAuthority)
            {
                return false;
            }

            Rpc_PublishProjectileRuntimeVfx(
                (int)eventType,
                effectKey,
                projectileSourceObjectId,
                sourcePlayer,
                targetPlayer,
                position.x,
                position.y,
                direction.x,
                direction.y,
                radius,
                magnitude,
                sourceTick,
                sourceEventSequence);
            return true;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishWeaponVfx(int eventType, string effectKey, float x, float y, float radius, float magnitude, int sequenceSalt)
        {
            VfxEventBus.Publish(VfxPayloadFactory.Create(
                (VfxEventType)eventType,
                effectKey,
                effectKey,
                Runner,
                Object,
                Object.InputAuthority,
                Object.InputAuthority,
                new Vector2(x, y),
                _networkedAimDir,
                radius,
                0.8f,
                magnitude,
                VfxAttachPoint.WeaponMuzzle,
                sequenceSalt));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishProjectileSpawnedWithModifierVfx(
            string effectKey,
            float x,
            float y,
            float dirX,
            float dirY,
            float radius,
            int sourceTick,
            int sourceEventSequence)
        {
            int sourceObjectId = Object != null ? unchecked((int)Object.Id.Raw) : 0;
            VfxEventBus.Publish(VfxPayloadFactory.CreateWithExplicitIdentity(
                VfxEventType.ProjectileSpawnedWithModifier,
                effectKey,
                effectKey,
                sourceObjectId,
                Object.InputAuthority,
                Object.InputAuthority,
                new Vector2(x, y),
                new Vector2(dirX, dirY),
                radius,
                0.5f,
                radius,
                VfxAttachPoint.WeaponMuzzle,
                sourceTick,
                sourceEventSequence));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishProjectileRuntimeVfx(
            int eventType,
            string effectKey,
            int projectileSourceObjectId,
            PlayerRef sourcePlayer,
            PlayerRef targetPlayer,
            float x,
            float y,
            float dirX,
            float dirY,
            float radius,
            float magnitude,
            int sourceTick,
            int sourceEventSequence)
        {
            VfxEventBus.Publish(VfxPayloadFactory.CreateWithExplicitIdentity(
                (VfxEventType)eventType,
                effectKey,
                effectKey,
                projectileSourceObjectId,
                sourcePlayer,
                targetPlayer,
                new Vector2(x, y),
                new Vector2(dirX, dirY),
                radius,
                0.65f,
                magnitude,
                VfxAttachPoint.World,
                sourceTick,
                sourceEventSequence));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishShotSfx(float x, float y, float directionX, float directionY, int sourceTick, int sourceEventSequence)
        {
            int sourceObjectId = Object != null ? unchecked((int)Object.Id.Raw) : 0;
            VfxEventBus.Publish(VfxPayloadFactory.CreateWithExplicitIdentity(
                VfxEventType.ProjectileShot,
                "projectile_shot",
                "projectile_shot",
                sourceObjectId,
                Object.InputAuthority,
                Object.InputAuthority,
                new Vector2(x, y),
                new Vector2(directionX, directionY),
                0.35f,
                0.15f,
                1f,
                VfxAttachPoint.WeaponMuzzle,
                sourceTick,
                sourceEventSequence));
        }

        // [A303-171] Visual-only projectile 발사 RPC.
        // server 가 매 발사 시 호출 → 모든 client (server 포함) 가 BulletVisual prefab 을 instantiate + Init.
        // 본 단계 (단계 2) 는 _bulletVisualPrefab 슬롯이 비어 있으면 dead path — 기존 NetworkObject 흐름과 병행.
        // 단계 7 에서 _bulletPrefab (NetworkObject 기반) 폐기 후 본 RPC 가 발사의 단일 진입점이 된다.
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_FireBullet(
            int bulletId,
            PlayerRef owner,
            float startX, float startY,
            float dirX, float dirY,
            float speed,
            float gravityScale,
            float lifetime,
            float sizeMul)
        {
            if (_bulletVisualPrefab == null) return; // 단계 2 dead path 보호

            GameObject visualObj = Instantiate(_bulletVisualPrefab, new Vector3(startX, startY, 0f), Quaternion.identity);
            var visual = visualObj.GetComponent<BulletVisual>();
            if (visual != null)
            {
                visual.Init(
                    bulletId,
                    owner,
                    new Vector2(startX, startY),
                    new Vector2(dirX, dirY),
                    speed,
                    gravityScale,
                    lifetime,
                    sizeMul);
            }
            else
            {
                Debug.LogWarning("[Colosseum] Rpc_FireBullet: BulletVisual component 미발견. prefab 슬롯 확인.");
                Destroy(visualObj);
            }
        }

        /// <summary>
        /// 외부에서 강제 재장전 (리스폰 시 탄약 리필 등)
        /// </summary>
        public void ForceReload()
        {
            IsReloading = false;
            CurrentAmmo = MagazineCapacity;
        }

        public void ApplyMagazineCapacityChange(int previousCapacity)
        {
            int capacity = MagazineCapacity;
            if (capacity <= previousCapacity)
            {
                return;
            }

            int previous = Mathf.Max(0, previousCapacity);
            int gainedAmmo = capacity - previous;
            CurrentAmmo = Mathf.Clamp(CurrentAmmo + gainedAmmo, 0, capacity);
            DiagLog.Log($"[Colosseum] Gun: 탄창 최대치 변경 {previous}->{capacity}, 탄약 {CurrentAmmo}/{capacity}");
        }

        public static int CalculateMagazineCapacity(int baselineCapacity, int bonusCapacity)
        {
            return Mathf.Max(1, baselineCapacity + bonusCapacity);
        }

        private Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(
                v.x * cos - v.y * sin,
                v.x * sin + v.y * cos
            );
        }
    }
}
