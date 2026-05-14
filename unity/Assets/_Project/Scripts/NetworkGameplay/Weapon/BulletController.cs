using UnityEngine;
using Fusion;
using Colosseum.Player;
using Colosseum.Utils;
using Colosseum.Game;
using Colosseum.Contracts;
using Colosseum.NetworkGameplay.VFX;

namespace Colosseum.Weapon
{
    public class BulletController : NetworkBehaviour
    {
        [Header("Gameplay Profile")]
        [Tooltip("Assets/_Project/Data/GameplayProfile.asset 드래그. 미할당 시 fallback 값 사용 + LogError.")]
        [SerializeField] private GameplayProfile _profile;

        [Header("Per-Bullet Stats (탄환 인스턴스 고유 값, Profile 필드와는 별개)")]
        [SerializeField] private float _linearDrag = 0.2f;
        [SerializeField] private float _ownerIgnoreTime = 0.3f;

        // Profile 미할당 시 fallback. 마이그레이션 이전만
        private const float FallbackDirectHitDamage = 10f;
        private const float FallbackProjectileSpeed = 15f;
        private const float FallbackProjectileGravityScale = 1f;
        private const float FallbackProjectileLifetimeSeconds = 5f;
        private const float FallbackProjectileBaseScale = 1f;
        private const float FallbackDirectHitKnockbackImpulse = 0f;
        private const int ProjectileImpactVfxSequenceSalt = 11;
        private const int CameraEdgeReflectVfxSequenceSalt = 21;
        private const int WallReflectVfxSequenceSalt = 31;
        private const int WallPierceVfxSequenceSalt = 41;
        private const int ExplosionTriggeredVfxSequenceSalt = 51;

        private float DirectHitDamage => _profile != null ? _profile.combat.baseline.directHitDamage : FallbackDirectHitDamage;
        private float ProjectileSpeed => _profile != null ? _profile.combat.baseline.projectileSpeed : FallbackProjectileSpeed;
        private float ProjectileGravityScale => _profile != null ? _profile.combat.baseline.projectileGravityScale : FallbackProjectileGravityScale;
        private float ProjectileLifetimeSeconds => _profile != null ? _profile.combat.baseline.projectileLifetimeSeconds : FallbackProjectileLifetimeSeconds;
        private float ProjectileBaseScale => _profile != null ? _profile.combat.baseline.projectileBaseScale : FallbackProjectileBaseScale;
        private float DirectHitKnockbackImpulse => _profile != null ? _profile.combat.baseline.directHitKnockbackImpulse : FallbackDirectHitKnockbackImpulse;

        [Networked] public float DamageMultiplier { get; set; } = 1f;
        [Networked] public float SpeedMultiplier { get; set; } = 1f;
        [Networked] public float SizeMultiplier { get; set; } = 1f;
        [Networked] public int BounceCount { get; set; } = 0;
        [Networked] public int PierceCount { get; set; } = 0;
        [Networked] public float KnockbackForce { get; set; } = 0f;
        [Networked] public float ExplosionRadius { get; set; } = 0f;
        [Networked] public float ExplosionDamageRatio { get; set; } = 0.5f;

        // CardData.StatusEffectType와 같은 정렬 유지
        public enum StatusEffect { None = 0, Freeze = 1, Burn = 2, Poison = 3, Electric = 4 }
        [Networked] public StatusEffect AppliedStatus { get; set; } = StatusEffect.None;

        [Networked] public byte ReflectCount { get; set; } = 0;

        [Networked] private TickTimer _lifetimeTimer { get; set; }
        [Networked] private TickTimer _ownerIgnoreTimer { get; set; }
        [Networked] private int _remainingBounces { get; set; }
        [Networked] private int _remainingPierces { get; set; }
        [Networked] private PlayerRef _owner { get; set; }

        private Rigidbody2D _rb;
        private Collider2D _physicsCollider;
        private bool _ownerIgnoreExpired = false;
        private bool _despawnRequested;

        public override void Spawned()
        {
            if (_profile == null)
            {
                Debug.LogError($"[Colosseum] BulletController on {gameObject.name}: GameplayProfile 미할당. Fallback 값 사용 중. " +
                    $"Bullet.prefab -> BulletController -> Profile 슬롯에 GameplayProfile.asset 드래그 필요.");
            }

            Runner.SetIsSimulated(Object, true);
        }

        public void Init(PlayerRef owner, Vector2 direction)
        {
            _owner = owner;
            _remainingBounces = BounceCount;
            _remainingPierces = PierceCount;

            _physicsCollider = GetComponent<Collider2D>();
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = ProjectileGravityScale;
            _rb.drag = _linearDrag;
            _rb.angularVelocity = 0f;

            var allBullets = SceneSearch.FindAllInScene<BulletController>(gameObject.scene);
            foreach (var other in allBullets)
            {
                if (other == this) continue;
                var otherCollider = other.GetComponent<Collider2D>();
                if (otherCollider != null && _physicsCollider != null)
                    Physics2D.IgnoreCollision(_physicsCollider, otherCollider);
            }

            IgnoreAllPlayerPhysics();
            CreateHitTrigger();

            if (BounceCount > 0 && _physicsCollider != null)
            {
                var bounceMat = new PhysicsMaterial2D("BulletBounce");
                bounceMat.bounciness = 1f;
                bounceMat.friction = 0f;
                _physicsCollider.sharedMaterial = bounceMat;
            }

            transform.localScale = Vector3.one * 0.08f * ProjectileBaseScale * SizeMultiplier;

            float speed = ProjectileSpeed * SpeedMultiplier;
            _rb.velocity = Vector2.zero;
            _rb.velocity = direction.normalized * speed;

            _lifetimeTimer = TickTimer.CreateFromSeconds(Runner, ProjectileLifetimeSeconds);
            _ownerIgnoreTimer = TickTimer.CreateFromSeconds(Runner, _ownerIgnoreTime);

            Debug.Log($"[Colosseum] Bullet Init - Speed:{speed}, Size:{SizeMultiplier}, " +
                $"Bounce:{BounceCount}, Pierce:{PierceCount}, Damage:{DamageMultiplier}");
        }

        public override void FixedUpdateNetwork()
        {
            if (_despawnRequested)
            {
                Runner.Despawn(Object);
                return;
            }

            if (_lifetimeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
                return;
            }

            if (!_ownerIgnoreExpired && _ownerIgnoreTimer.Expired(Runner))
            {
                _ownerIgnoreExpired = true;
            }
        }

        private void IgnoreAllPlayerPhysics()
        {
            if (_physicsCollider == null) return;
            var players = SceneSearch.FindAllInScene<PlayerHealth>(gameObject.scene);
            foreach (var p in players)
            {
                var playerColliders = p.GetComponentsInChildren<Collider2D>();
                foreach (var pc in playerColliders)
                {
                    Physics2D.IgnoreCollision(_physicsCollider, pc, true);
                }
            }
        }

        private void CreateHitTrigger()
        {
            var triggerObj = new GameObject("HitTrigger");
            triggerObj.transform.SetParent(transform, false);
            triggerObj.transform.localPosition = Vector3.zero;
            triggerObj.layer = gameObject.layer;

            var triggerCollider = triggerObj.AddComponent<CircleCollider2D>();
            var originalCircle = _physicsCollider as CircleCollider2D;
            triggerCollider.radius = originalCircle != null ? originalCircle.radius : 0.5f;
            triggerCollider.isTrigger = true;

            var relay = triggerObj.AddComponent<TriggerRelay>();
            relay.Init(this);
        }

        public void OnHitTrigger(Collider2D other)
        {
            if (Object == null || !Object.HasStateAuthority) return;

            // A303-263: 카메라 경계 충돌 분기. 총알 자식 trigger collider가 카메라 자식 trigger와
            // OnTriggerEnter2D로 만나는 경로. 일반탄은 즉시 despawn, 반사탄은 velocity 반사 + BounceCount 차감.
            var edgeWall = other.GetComponent<Colosseum.CameraSystem.CameraEdgeWall>();
            if (edgeWall != null)
            {
                HandleCameraEdgeImpact(edgeWall);
                return;
            }

            var health = other.GetComponent<PlayerHealth>();
            if (health == null) return;

            var netObj = other.GetComponent<NetworkObject>();
            if (netObj != null && netObj.InputAuthority == _owner && !_ownerIgnoreExpired)
                return;

            float finalDamage = DirectHitDamage * DamageMultiplier;
            if (ReflectCount > 0)
                Debug.Log($"[Colosseum] Reflected bullet hit (ReflectCount={ReflectCount})");
            PlayerRef targetPlayer = netObj != null ? netObj.InputAuthority : default;
            PublishProjectileVfx(VfxEventType.ProjectileImpact, other.transform.position, _rb != null ? _rb.velocity : Vector2.zero, finalDamage, ProjectileImpactVfxSequenceSalt, targetPlayer);
            health.TakeDamage(finalDamage, _owner, AppliedStatus);

            float knockbackImpulse = DirectHitKnockbackImpulse + KnockbackForce;
            if (knockbackImpulse > 0f)
            {
                var targetRb = other.GetComponent<Rigidbody2D>();
                if (targetRb != null)
                {
                    Vector2 knockDir = _rb.velocity.normalized;
                    targetRb.AddForce(knockDir * knockbackImpulse, ForceMode2D.Impulse);
                    Debug.Log($"[Colosseum] Knockback applied: {knockbackImpulse} force");
                }
            }

            Explode(other);
            DespawnIfAlive();
        }

        /// <summary>
        /// A303-263: 카메라 경계 trigger 충돌 처리. 일반 벽 충돌과 동일한 정책:
        ///   반사탄 (BonusBounce > 0) → velocity 수동 반사 + _remainingBounces 차감 + ReflectCount 증가
        ///   일반탄 → 즉시 Despawn (Explode는 호출 안 함 — 화면 밖이라 splash 의미 없음)
        /// Trigger collider라 PhysicsMaterial2D bounciness 의존 안 함, velocity를 코드로 직접 반사.
        /// State Authority 한정 호출 (호출부에서 가드).
        /// </summary>
        private void HandleCameraEdgeImpact(Colosseum.CameraSystem.CameraEdgeWall edgeWall)
        {
            if (_remainingBounces > 0)
            {
                if (_rb != null)
                {
                    Vector2 v = _rb.velocity;
                    switch (edgeWall.Side)
                    {
                        case Colosseum.CameraSystem.CameraEdgeWall.EdgeSide.Left:
                        case Colosseum.CameraSystem.CameraEdgeWall.EdgeSide.Right:
                            v.x = -v.x;
                            break;
                        case Colosseum.CameraSystem.CameraEdgeWall.EdgeSide.Top:
                        case Colosseum.CameraSystem.CameraEdgeWall.EdgeSide.Bottom:
                            v.y = -v.y;
                            break;
                    }
                    _rb.velocity = v;
                    transform.right = v.normalized;
                }
                _remainingBounces--;
                if (ReflectCount < byte.MaxValue) ReflectCount++;
                PublishProjectileVfx(VfxEventType.ProjectileReflected, transform.position, _rb != null ? _rb.velocity : Vector2.zero, ReflectCount, CameraEdgeReflectVfxSequenceSalt);
                Debug.Log($"[Colosseum] Bullet bounced off camera edge ({edgeWall.Side})! Remaining: {_remainingBounces}, Total: {ReflectCount}");
                return;
            }

            // 일반탄 또는 반사 소진: 화면 밖에서 사라짐. Explode 호출 안 함 (splash 의미 없음).
            Debug.Log($"[Colosseum] Bullet despawned at camera edge ({edgeWall.Side})");
            DespawnIfAlive();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (Object == null || !Object.HasStateAuthority) return;

            if (collision.gameObject.GetComponent<BulletController>() != null)
                return;

            if (_remainingBounces > 0)
            {
                _remainingBounces--;
                if (ReflectCount < byte.MaxValue) ReflectCount++;
                PublishProjectileVfx(VfxEventType.ProjectileReflected, collision.GetContact(0).point, _rb != null ? _rb.velocity : Vector2.zero, ReflectCount, WallReflectVfxSequenceSalt);
                Debug.Log($"[Colosseum] Bullet bounced! Remaining: {_remainingBounces}, Total: {ReflectCount}");
                return;
            }

            if (_remainingPierces > 0)
            {
                _remainingPierces--;
                if (_physicsCollider != null && collision.collider != null)
                {
                    Physics2D.IgnoreCollision(_physicsCollider, collision.collider, true);
                }
                PublishProjectileVfx(VfxEventType.ProjectilePierced, collision.GetContact(0).point, _rb != null ? _rb.velocity : Vector2.zero, _remainingPierces, WallPierceVfxSequenceSalt);
                Debug.Log($"[Colosseum] Bullet pierced wall! Remaining: {_remainingPierces}");
                return;
            }

            Explode(null);
            DespawnIfAlive();
        }

        private void DespawnIfAlive()
        {
            if (Runner == null || Object == null) return;
            if (_despawnRequested) return;

            _despawnRequested = true;
            if (_rb != null)
            {
                _rb.velocity = Vector2.zero;
                _rb.angularVelocity = 0f;
                _rb.simulated = false;
            }

            if (_physicsCollider != null)
            {
                _physicsCollider.enabled = false;
            }

            var childColliders = GetComponentsInChildren<Collider2D>();
            foreach (var childCollider in childColliders)
            {
                childCollider.enabled = false;
            }
        }

        private void Explode(Collider2D directHitCollider)
        {
            if (ExplosionRadius <= 0f) return;

            float splashDamage = DirectHitDamage * DamageMultiplier * ExplosionDamageRatio;
            PublishProjectileVfx(VfxEventType.ExplosionTriggered, transform.position, Vector2.zero, splashDamage, ExplosionTriggeredVfxSequenceSalt);
            var hits = Physics2D.OverlapCircleAll(transform.position, ExplosionRadius);

            foreach (var hit in hits)
            {
                if (hit == directHitCollider) continue;

                var hitHealth = hit.GetComponent<PlayerHealth>();
                if (hitHealth != null)
                {
                    hitHealth.TakeDamage(splashDamage, _owner, StatusEffect.None);

                    float knockbackImpulse = DirectHitKnockbackImpulse + KnockbackForce;
                    if (knockbackImpulse > 0f)
                    {
                        var hitRb = hit.GetComponent<Rigidbody2D>();
                        if (hitRb != null)
                        {
                            Vector2 blastDir = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
                            hitRb.AddForce(blastDir * knockbackImpulse * 0.5f, ForceMode2D.Impulse);
                        }
                    }

                    Debug.Log($"[Colosseum] Explosion hit {hit.name} for {splashDamage} splash damage");
                }
            }
        }

        public float GetDamage()
        {
            return DirectHitDamage * DamageMultiplier;
        }

        private void PublishProjectileVfx(VfxEventType eventType, Vector2 position, Vector2 direction, float magnitude, int sequenceSalt, PlayerRef targetPlayer = default)
        {
            string effectKey = VfxPayloadFactory.GetProjectileEffectKey(
                eventType,
                SpeedMultiplier,
                SizeMultiplier,
                BounceCount,
                PierceCount,
                KnockbackForce,
                ExplosionRadius,
                AppliedStatus);

            float radius = eventType == VfxEventType.ExplosionTriggered
                ? Mathf.Max(ExplosionRadius, 0.4f)
                : Mathf.Max(SizeMultiplier, 0.4f);

            Rpc_PublishProjectileVfx(
                (int)eventType,
                effectKey,
                position.x,
                position.y,
                direction.x,
                direction.y,
                radius,
                magnitude,
                targetPlayer,
                sequenceSalt);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishProjectileVfx(
            int eventType,
            string effectKey,
            float x,
            float y,
            float dirX,
            float dirY,
            float radius,
            float magnitude,
            PlayerRef targetPlayer,
            int sequenceSalt)
        {
            VfxEventBus.Publish(VfxPayloadFactory.Create(
                (VfxEventType)eventType,
                effectKey,
                effectKey,
                Runner,
                Object,
                _owner,
                targetPlayer,
                new Vector2(x, y),
                new Vector2(dirX, dirY),
                radius,
                0.65f,
                magnitude,
                VfxAttachPoint.World,
                sequenceSalt));
        }
    }

    public class TriggerRelay : MonoBehaviour
    {
        private BulletController _bullet;

        public void Init(BulletController bullet)
        {
            _bullet = bullet;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_bullet != null)
                _bullet.OnHitTrigger(other);
        }
    }
}
