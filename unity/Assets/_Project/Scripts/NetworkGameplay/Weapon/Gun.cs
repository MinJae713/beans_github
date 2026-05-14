using UnityEngine;
using Fusion;
using Colosseum.Network;
using Colosseum.Card;
using Colosseum.Game;
using Colosseum.Contracts;
using Colosseum.NetworkGameplay.VFX;

namespace Colosseum.Weapon
{
    public partial class Gun : NetworkBehaviour
    {
        partial void OnDevModeSpawned();

        [Header("Gameplay Profile")]
        [Tooltip("Assets/_Project/Data/GameplayProfile.asset 드래그. 미할당 시 fallback 값 사용 + LogError.")]
        [SerializeField] private GameplayProfile _profile;

        [Header("Fire Settings")]
        [SerializeField] private NetworkPrefabRef _bulletPrefab;
        [SerializeField] private Transform _muzzle;

        // Profile 미할당 fallback. 마이그레이션 안전망.
        private const float FallbackFireInterval = 1f;
        private const int FallbackMagazineCapacity = 3;
        private const float FallbackReloadDuration = 1.5f;
        private const int ReloadStartedVfxSequenceSalt = 100;
        private const int ReloadCompletedVfxSequenceSalt = 101;
        private const int OverchargeReadyVfxSequenceSalt = 102;
        private const int OverchargeShotFiredVfxSequenceSalt = 103;
        private const int ProjectileModifierSpawnVfxSequenceSaltBase = 71;

        private float FireInterval => _profile != null ? _profile.combat.baseline.fireInterval : FallbackFireInterval;
        private int MagazineCapacity => _profile != null ? _profile.combat.baseline.ammoCapacity : FallbackMagazineCapacity;
        private float ReloadDurationBase => _profile != null ? _profile.combat.baseline.reloadTime : FallbackReloadDuration;

        [Networked] public float FireRateMultiplier { get; set; } = 1f;
        [Networked] public int ExtraBullets { get; set; } = 0;
        [Networked] private TickTimer _fireCooldown { get; set; }
        [Networked] private Vector2 _networkedAimDir { get; set; }

        // 탄창 상태
        [Networked] public int CurrentAmmo { get; set; }
        [Networked] private TickTimer _reloadTimer { get; set; }
        [Networked] public NetworkBool IsReloading { get; set; }
        [Networked] public float ReloadDuration { get; set; }

        // 과충전: 재장전 직후 첫 발 데미지 증폭
        [Networked] public NetworkBool IsOvercharged { get; set; }

        private CardEffect _cardEffect;

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
                    Debug.Log($"[Colosseum] Gun: 과충전 활성화! 다음 발 x{_cardEffect.OverchargeDamageMultiplier}");
                }

                Debug.Log($"[Colosseum] Gun: 재장전 완료! 탄약: {CurrentAmmo}/{MagazineCapacity}");
            }

            if (GetInput(out NetworkInputData input))
            {
                // 마우스 월드 좌표 → 총 위치 기준 방향 계산
                Vector2 origin = _muzzle != null ? (Vector2)_muzzle.position : (Vector2)transform.position;
                Vector2 aimDir = (Vector2)input.AimDirection - origin;
                if (aimDir == Vector2.zero) aimDir = Vector2.right;
                _networkedAimDir = aimDir.normalized;

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

        public override void Render()
        {
            if (_networkedAimDir != Vector2.zero)
            {
                float angle = Mathf.Atan2(_networkedAimDir.y, _networkedAimDir.x) * Mathf.Rad2Deg;
                transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// <summary>
        /// 산탄총 스프레드: 각 탄이 ±_spreadAngle 범위 내에서 랜덤 각도로 퍼진다.
        /// </summary>
        [Header("Spread Settings")]
        [SerializeField] private float _spreadAngle = 25f;

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
            NetworkObject bulletObj = Runner.Spawn(
                _bulletPrefab,
                muzzlePosition,
                Quaternion.identity,
                Object.InputAuthority
            );

            var bullet = bulletObj.GetComponent<BulletController>();
            if (bullet != null)
            {
                // CardEffect 강화 수치를 총알에 적용
                if (_cardEffect != null)
                {
                    // 기본 카드 강화 적용
                    float dmgMul = _cardEffect.DamageMultiplier;

                    // 과충전: 첫 발만 데미지 증폭
                    if (IsOvercharged)
                    {
                        dmgMul *= _cardEffect.OverchargeDamageMultiplier;
                        IsOvercharged = false;
                        PublishWeaponVfx(
                            VfxEventType.OverchargeShotFired,
                            VfxPayloadFactory.GetReloadEffectKey(VfxEventType.OverchargeShotFired),
                            1f,
                            _cardEffect.OverchargeDamageMultiplier,
                            OverchargeShotFiredVfxSequenceSalt);
                        Debug.Log($"[Colosseum] Overcharged shot! DMG x{_cardEffect.OverchargeDamageMultiplier}");
                    }

                    bullet.DamageMultiplier = dmgMul;
                    bullet.SpeedMultiplier = _cardEffect.BulletSpeedMultiplier;
                    bullet.SizeMultiplier = _cardEffect.BulletSizeMultiplier;
                    bullet.BounceCount = _cardEffect.BonusBounce;
                    bullet.PierceCount = _cardEffect.BonusPierce;
                    bullet.KnockbackForce = _cardEffect.KnockbackForce;
                    bullet.ExplosionRadius = _cardEffect.ExplosionRadius;
                    bullet.ExplosionDamageRatio = _cardEffect.ExplosionDamageRatio;

                    if (_cardEffect.CurrentStatusEffect != 0)
                    {
                        bullet.AppliedStatus = (BulletController.StatusEffect)_cardEffect.CurrentStatusEffect;
                    }
                }

                PublishProjectileSpawnedWithModifierVfxIfNeeded(bullet, direction, projectileOrdinal);
                bullet.Init(Object.InputAuthority, direction);
                Rpc_PublishShotSfx(muzzlePosition.x, muzzlePosition.y, direction.x, direction.y, shotSequenceSalt);
            }
            else
            {
                Debug.LogWarning("[Colosseum] Gun: BulletController component not found on spawned bullet!");
            }
        }

        private void PublishProjectileSpawnedWithModifierVfxIfNeeded(BulletController bullet, Vector2 direction, int projectileOrdinal)
        {
            if (bullet == null) return;
            if (!VfxPayloadFactory.HasProjectileModifier(
                bullet.SpeedMultiplier,
                bullet.SizeMultiplier,
                bullet.BounceCount,
                bullet.PierceCount,
                bullet.KnockbackForce,
                bullet.ExplosionRadius,
                bullet.AppliedStatus))
            {
                return;
            }

            string effectKey = VfxPayloadFactory.GetProjectileEffectKey(
                VfxEventType.ProjectileSpawnedWithModifier,
                bullet.SpeedMultiplier,
                bullet.SizeMultiplier,
                bullet.BounceCount,
                bullet.PierceCount,
                bullet.KnockbackForce,
                bullet.ExplosionRadius,
                bullet.AppliedStatus);

            Vector2 vfxPosition = GetMuzzlePosition();
            Rpc_PublishProjectileSpawnedWithModifierVfx(
                effectKey,
                vfxPosition.x,
                vfxPosition.y,
                direction.x,
                direction.y,
                Mathf.Max(bullet.SizeMultiplier, bullet.ExplosionRadius, 0.4f),
                ProjectileModifierSpawnVfxSequenceSaltBase + projectileOrdinal);
        }

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
            Debug.Log($"[Colosseum] Gun: 재장전 시작! ({reloadSpeed:F2}초)");
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
            int sequenceSalt)
        {
            VfxEventBus.Publish(VfxPayloadFactory.Create(
                VfxEventType.ProjectileSpawnedWithModifier,
                effectKey,
                effectKey,
                Runner,
                Object,
                Object.InputAuthority,
                Object.InputAuthority,
                new Vector2(x, y),
                new Vector2(dirX, dirY),
                radius,
                0.5f,
                radius,
                VfxAttachPoint.WeaponMuzzle,
                sequenceSalt));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishShotSfx(float x, float y, float directionX, float directionY, int sequenceSalt)
        {
            VfxEventBus.Publish(VfxPayloadFactory.Create(
                VfxEventType.ProjectileShot,
                "projectile_shot",
                "projectile_shot",
                Runner,
                Object,
                Object.InputAuthority,
                Object.InputAuthority,
                new Vector2(x, y),
                new Vector2(directionX, directionY),
                0.35f,
                0.15f,
                1f,
                VfxAttachPoint.WeaponMuzzle,
                sequenceSalt));
        }

        /// <summary>
        /// 외부에서 강제 재장전 (리스폰 시 탄약 리필 등)
        /// </summary>
        public void ForceReload()
        {
            IsReloading = false;
            CurrentAmmo = MagazineCapacity;
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
