using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Fusion;
using Colosseum.Contracts;
using Colosseum.CameraSystem;
using Colosseum.Game;
using Colosseum.Player;
using Colosseum.NetworkGameplay.Diagnostics;
using Colosseum.NetworkGameplay.VFX;

namespace Colosseum.Weapon
{
    /// <summary>
    /// [A303-171] server-side bullet simulation singleton.
    ///
    /// Gun.cs 의 SpawnBullet 이 발사 시 OnFireBullet 호출 → entry 추가.
    /// FixedUpdateNetwork 매 tick 시뮬레이션 + 충돌 판정 + 카드 효과 적용 + 충돌 시 RPC broadcast.
    /// 본 클래스는 server-authoritative simulation 단독 책임 — client 측은 BulletVisual 이 자체 시뮬.
    ///
    /// scene 의 NetworkObject 로 한 instance 만 배치 (Unity Editor 작업 § 단계 4a).
    /// </summary>
    public class BulletServerSimulation : NetworkBehaviour
    {
        public static BulletServerSimulation Instance { get; private set; }

        // [단계 6] Entry 를 class 로 변경 — PiercedColliders HashSet 보관 + 매 tick rewrite 비용 절감.
        private class Entry
        {
            public int BulletId;
            public PlayerRef Owner;
            public Vector2 Position;
            public Vector2 Velocity;
            public float GravityScale;
            public float LifetimeRemaining;

            // Damage / Ignore
            public float Damage;
            public bool OwnerIgnoreExpired;
            public float OwnerIgnoreTimer;

            // 카드 효과 (단계 6 추가)
            public int BounceRemaining;
            public int PierceRemaining;
            public float KnockbackForce;
            public float ExplosionRadius;
            public float ExplosionDamageRatio;
            public float SizeMultiplier;
            public int StatusMask;
            public float FreezeMagnitude;
            public float BurnMagnitude;
            public float PoisonMagnitude;
            public float ElectricMagnitude;
            public float FreezeDuration;
            public float BurnDuration;
            public float PoisonDuration;
            public float ElectricDuration;

            // 관통한 wall collider 목록 — 같은 collider 재충돌 무시 (develop Physics2D.IgnoreCollision 패리티).
            public readonly HashSet<Collider2D> PiercedColliders = new HashSet<Collider2D>();
        }

        private readonly List<Entry> _entries = new List<Entry>();

        [Header("Hit Detection")]
        [Tooltip("Linecast 충돌 검사 layer mask. Player + Ground 권장.")]
        [SerializeField] private LayerMask _hitLayerMask = ~0;

        [Header("Simulation")]
        [Tooltip("선형 drag — develop BulletController 의 Rigidbody2D.drag (0.2) 대비 체감 발사력 매칭용 (현재 0.5). inspector 에서 양쪽(server/visual) 동일하게 튜닝.")]
        [SerializeField] private float _linearDrag = 0.5f;

        private const float OwnerIgnoreSeconds = 0.3f;

        // [A303-171] VFX sequence salt constants — develop BulletController 패리티.
        private const int ProjectileImpactVfxSequenceSalt = 11;
        private const int TerrainImpactVfxSequenceSalt = 12;
        private const int WallReflectVfxSequenceSalt = 31;
        private const int WallPierceVfxSequenceSalt = 41;
        private const int ExplosionTriggeredVfxSequenceSalt = 51;
        private const float DefaultVfxDuration = 0.65f;
        private const float MinVfxRadius = 0.4f;

        public override void Spawned()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Colosseum] BulletServerSimulation: 중복 instance. 이전 instance 유지.");
                return;
            }
            Instance = this;
            DiagLog.Log($"[Colosseum/DIAG] BulletServerSimulation.Spawned: HasStateAuthority={Object.HasStateAuthority}, layerMask={_hitLayerMask.value}");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Gun.cs SpawnBullet 에서 server-only 호출. 카드 효과 모든 인자 함께 전달.
        /// </summary>
        public void OnFireBullet(
            int bulletId,
            PlayerRef owner,
            Vector2 startPosition,
            Vector2 direction,
            float speed,
            float gravityScale,
            float lifetime,
            float damage,
            int bounceCount,
            int pierceCount,
            float knockbackForce,
            float explosionRadius,
            float explosionDamageRatio,
            int statusMask,
            float freezeMagnitude,
            float burnMagnitude,
            float poisonMagnitude,
            float electricMagnitude,
            float freezeDuration,
            float burnDuration,
            float poisonDuration,
            float electricDuration,
            float sizeMultiplier)
        {
            if (Object == null || !Object.HasStateAuthority) return;

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _entries.Add(new Entry
            {
                BulletId = bulletId,
                Owner = owner,
                Position = startPosition,
                Velocity = dir * speed,
                GravityScale = gravityScale,
                LifetimeRemaining = lifetime,
                Damage = damage,
                OwnerIgnoreExpired = false,
                OwnerIgnoreTimer = OwnerIgnoreSeconds,
                BounceRemaining = bounceCount,
                PierceRemaining = pierceCount,
                KnockbackForce = knockbackForce,
                ExplosionRadius = explosionRadius,
                ExplosionDamageRatio = explosionDamageRatio,
                StatusMask = statusMask,
                FreezeMagnitude = freezeMagnitude,
                BurnMagnitude = burnMagnitude,
                PoisonMagnitude = poisonMagnitude,
                ElectricMagnitude = electricMagnitude,
                FreezeDuration = freezeDuration,
                BurnDuration = burnDuration,
                PoisonDuration = poisonDuration,
                ElectricDuration = electricDuration,
                SizeMultiplier = sizeMultiplier,
            });
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            float dt = Runner.DeltaTime;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry e = _entries[i];

                // owner ignore timer
                if (!e.OwnerIgnoreExpired)
                {
                    e.OwnerIgnoreTimer -= dt;
                    if (e.OwnerIgnoreTimer <= 0f) e.OwnerIgnoreExpired = true;
                }

                // 등가속 시뮬 + drag
                Vector2 prevPosition = e.Position;
                e.Velocity += Physics2D.gravity * e.GravityScale * dt;
                e.Velocity *= Mathf.Max(0f, 1f - _linearDrag * dt);
                e.Position += e.Velocity * dt;

                // [A303-171] Fusion 2 의 Physics Scene 분리 대응:
                // 정적 Physics2D.* API 는 Unity 의 default physics scene 만 보지만,
                // Fusion 이 관리하는 NetworkObject 들의 collider 는 Runner 의 별도 PhysicsScene2D 에 있어 안 잡힘.
                // PlayerController 의 wall slide 가 동작하는데 본 BulletSim 이 hit=null 만 나오는 이유.
                // 해결: Runner.GetPhysicsScene2D() 로 Fusion 의 physics scene 에서 CircleCast.
                Vector2 displacement = e.Position - prevPosition;
                float castDistance = displacement.magnitude;
                Vector2 castDirection = castDistance > 0.0001f ? displacement / castDistance : Vector2.right;

                PhysicsScene2D physicsScene = Runner.GetPhysicsScene2D();
                // cast radius 는 BulletVisual sprite scale (0.08 × SizeMultiplier) 의 절반 — visual 의 표면 도달 시점과 server hit 시점을 일치시켜 snap jitter 최소화.
                float castRadius = 0.04f * Mathf.Max(e.SizeMultiplier, 0.001f);
                RaycastHit2D hit = physicsScene.CircleCast(prevPosition, castRadius, castDirection, castDistance, _hitLayerMask);

                if (hit.collider != null)
                {
                    CameraEdgeWall cameraEdge = hit.collider.GetComponent<CameraEdgeWall>();
                    bool isCameraEdge = cameraEdge != null;
                    if (isCameraEdge)
                    {
                        if (TryHandleCameraEdgeHit(e, hit, cameraEdge))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        // 이미 관통한 wall collider 는 무시 — develop 의 IgnoreCollision 패리티.
                        bool isPierced = e.PiercedColliders.Contains(hit.collider);
                        if (!isPierced)
                        {
                            if (HandleHit(e, hit))
                            {
                                // entry 유지 (bounce / pierce) — position 이미 갱신됨
                                continue;
                            }
                            else
                            {
                                // entry 제거 (despawn 확정)
                                _entries.RemoveAt(i);
                                continue;
                            }
                        }
                    }
                }

                // lifetime
                e.LifetimeRemaining -= dt;
                if (e.LifetimeRemaining <= 0f)
                {
                    _entries.RemoveAt(i);
                    continue;
                }
            }
        }

        /// <summary>
        /// 충돌 처리. true 반환 시 entry 유지 (bounce/pierce 진행), false 반환 시 despawn.
        /// </summary>
        private bool HandleHit(Entry e, RaycastHit2D hit)
        {
            // PitTrigger 는 캐릭터 낙사 전용 — bullet cast 가 잡아도 통과시켜 반사탄이 trigger 표면에서 튕기는 회귀 차단.
            if (hit.collider.GetComponentInParent<PitTrigger>() != null)
            {
                e.PiercedColliders.Add(hit.collider);
                return true;
            }

            PlayerHealth hitHealth = hit.collider.GetComponentInParent<PlayerHealth>();
            bool isCharacterHit = hitHealth != null;
            bool isOwnerHit = false;
            if (isCharacterHit)
            {
                NetworkObject hitObj = hitHealth.GetComponent<NetworkObject>();
                isOwnerHit = hitObj != null && hitObj.InputAuthority == e.Owner;
            }

            // owner self-hit 무시 (ignore 기간 동안)
            if (isOwnerHit && !e.OwnerIgnoreExpired)
            {
                return true; // entry 유지
            }

            // 캐릭터 충돌 — develop 패리티: 첫 hit 에 damage + (폭발) + despawn. character pierce 없음.
            if (isCharacterHit)
            {
                PlayerRef targetPlayer = hitHealth.GetComponent<NetworkObject>().InputAuthority;

                ApplyDamageToHealth(hitHealth, e.Damage, e);
                ApplyKnockback(hitHealth, e);

                if (e.ExplosionRadius > 0f)
                {
                    ApplyExplosionSplash(e, hit.point, hitHealth);
                }

                PublishProjectileVfx(e, VfxEventType.ProjectileImpact, hit.point, e.Velocity, e.Damage, ProjectileImpactVfxSequenceSalt, targetPlayer);
                Rpc_BulletHit(e.BulletId, hit.point.x, hit.point.y);
                return false;
            }

            // 비-캐릭터 (벽/지형) 충돌 — bounce 우선, 다음 pierce.
            if (e.BounceRemaining > 0)
            {
                return HandleBounce(e, hit.point, hit.normal);
            }

            // 벽 관통 — develop 의 Physics2D.IgnoreCollision 패리티. PiercedColliders 에 등록해 재충돌 무시.
            if (e.PierceRemaining > 0)
            {
                e.PierceRemaining--;
                e.PiercedColliders.Add(hit.collider);
                PublishProjectileVfx(e, VfxEventType.ProjectilePierced, hit.point, e.Velocity, e.PierceRemaining, WallPierceVfxSequenceSalt);
                Rpc_BulletPierce(e.BulletId, hit.point.x, hit.point.y);
                return true;
            }

            // 벽/지형 + bounce 0 + pierce 0 → 폭발 + despawn
            if (e.ExplosionRadius > 0f)
            {
                ApplyExplosionSplash(e, hit.point, null);
            }

            PublishProjectileVfx(e, VfxEventType.ProjectileImpact, hit.point, e.Velocity, e.Damage, TerrainImpactVfxSequenceSalt);
            Rpc_BulletHit(e.BulletId, hit.point.x, hit.point.y);
            return false;
        }

        private bool TryHandleCameraEdgeHit(Entry e, RaycastHit2D hit, CameraEdgeWall cameraEdge)
        {
            if (cameraEdge == null || cameraEdge.Side == CameraEdgeWall.EdgeSide.Bottom || e.BounceRemaining <= 0)
            {
                return false;
            }

            Vector2 normal = GetCameraEdgeNormal(cameraEdge.Side);
            Vector2 contactPoint = GetCameraEdgeContactPoint(hit, cameraEdge, normal);
            return HandleBounce(e, contactPoint, normal);
        }

        private bool HandleBounce(Entry e, Vector2 contactPoint, Vector2 normal)
        {
            e.BounceRemaining--;

            Vector2 bounceNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : -e.Velocity.normalized;
            Vector2 reflected = Vector2.Reflect(e.Velocity, bounceNormal);
            e.Velocity = reflected;

            // bounce 후 offset 을 SizeMultiplier 비례 cast radius + safety 로 — 크기 카드 누적으로 cast 반경이 0.06 을 넘으면 다음 tick 에 cast circle 이 벽/edge 표면을 침범해 같은 collider 를 즉시 재충돌, BounceRemaining 이 폭소진되어 despawn 되던 회귀 fix.
            float bounceCastRadius = 0.04f * Mathf.Max(e.SizeMultiplier, 0.001f);
            e.Position = contactPoint + bounceNormal * (bounceCastRadius + 0.02f);

            PublishProjectileVfx(e, VfxEventType.ProjectileReflected, contactPoint, reflected, e.BounceRemaining, WallReflectVfxSequenceSalt);
            Rpc_BulletBounce(e.BulletId, e.Position.x, e.Position.y, reflected.x, reflected.y);
            return true;
        }

        private static Vector2 GetCameraEdgeNormal(CameraEdgeWall.EdgeSide side)
        {
            switch (side)
            {
                case CameraEdgeWall.EdgeSide.Left:
                    return Vector2.right;
                case CameraEdgeWall.EdgeSide.Right:
                    return Vector2.left;
                case CameraEdgeWall.EdgeSide.Top:
                    return Vector2.down;
                case CameraEdgeWall.EdgeSide.Bottom:
                    return Vector2.zero;
                default:
                    return Vector2.zero;
            }
        }

        private static Vector2 GetCameraEdgeContactPoint(RaycastHit2D hit, CameraEdgeWall cameraEdge, Vector2 normal)
        {
            Vector2 edgeCenter = cameraEdge.transform.position;
            float halfThickness = GetCameraEdgeHalfThickness(cameraEdge, normal);
            return Mathf.Abs(normal.x) > 0f
                ? new Vector2(edgeCenter.x + normal.x * halfThickness, hit.point.y)
                : new Vector2(hit.point.x, edgeCenter.y + normal.y * halfThickness);
        }

        private static float GetCameraEdgeHalfThickness(CameraEdgeWall cameraEdge, Vector2 normal)
        {
            if (cameraEdge == null || normal == Vector2.zero)
            {
                Debug.LogWarning("[BulletServerSimulation] Camera edge contact fallback used because edge or normal is missing.");
                return 0.5f;
            }

            BoxCollider2D collider = cameraEdge != null ? cameraEdge.GetComponent<BoxCollider2D>() : null;
            if (collider == null)
            {
                Debug.LogWarning($"[BulletServerSimulation] Camera edge contact fallback used because {cameraEdge.name} has no BoxCollider2D.");
                return 0.5f;
            }

            Vector3 lossyScale = collider.transform.lossyScale;
            return Mathf.Abs(normal.x) > 0f
                ? Mathf.Abs(collider.size.x * lossyScale.x) * 0.5f
                : Mathf.Abs(collider.size.y * lossyScale.y) * 0.5f;
        }

        private static void ApplyDamageToHealth(PlayerHealth target, float damage, Entry e)
        {
            if (target == null) return;
            target.TakeDamage(
                damage,
                e.Owner,
                e.StatusMask,
                e.FreezeDuration,
                e.BurnDuration,
                e.PoisonDuration,
                e.ElectricDuration,
                e.FreezeMagnitude,
                e.BurnMagnitude,
                e.PoisonMagnitude,
                e.ElectricMagnitude);
        }

        private void ApplyKnockback(PlayerHealth target, Entry e)
        {
            if (e.KnockbackForce <= 0f) return;
            if (target == null) return;
            if (target.IsCombatKnockbackProtectionActive())
            {
                return;
            }

            Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
            if (rb == null) return;
            Vector2 knockDir = e.Velocity.normalized;
            if (knockDir == Vector2.zero) knockDir = Vector2.right;
            rb.AddForce(knockDir * e.KnockbackForce, ForceMode2D.Impulse);
        }

        private void ApplyExplosionSplash(Entry e, Vector2 center, PlayerHealth directHit)
        {
            if (e.ExplosionRadius <= 0f) return;

            // [A303-171] 폭발 VFX publish (develop BulletController 패리티)
            PublishProjectileVfx(e, VfxEventType.ExplosionTriggered, center, Vector2.zero, e.ExplosionRadius, ExplosionTriggeredVfxSequenceSalt);

            float splashDamage = e.Damage * e.ExplosionDamageRatio;
            HashSet<PlayerHealth> damaged = new HashSet<PlayerHealth>();
            if (directHit != null) damaged.Add(directHit); // 직접 hit 은 splash 중복 제외

            // develop BulletController.Explode 패리티 — Fusion 2 의 Physics Scene 분리로 정적 Physics2D.OverlapCircleAll 가
            // Player NetworkObject collider 를 못 잡으므로 PlayerHealth.ActiveInstances 를 순회해 distance check 한다.
            foreach (var activeHealth in PlayerHealth.ActiveInstances)
            {
                if (activeHealth == null) continue;
                float distance = Vector2.Distance(center, activeHealth.transform.position);
                if (distance > e.ExplosionRadius) continue;
                if (!damaged.Add(activeHealth)) continue;

                ApplyDamageToHealth(activeHealth, splashDamage, e);
                ApplyKnockback(activeHealth, e);
            }
        }

        /// <summary>
        /// 충돌 확정 — visual snap + destroy.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_BulletHit(int bulletId, float hitX, float hitY)
        {
            if (BulletVisualRegistry.TryGet(bulletId, out BulletVisual visual))
            {
                visual.OnServerHit(new Vector2(hitX, hitY));
            }
        }

        /// <summary>
        /// 반사 (Bounce) — visual 위치/velocity 동기화.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_BulletBounce(int bulletId, float posX, float posY, float velX, float velY)
        {
            if (BulletVisualRegistry.TryGet(bulletId, out BulletVisual visual))
            {
                visual.OnServerBounce(new Vector2(posX, posY), new Vector2(velX, velY));
            }
        }

        /// <summary>
        /// 관통 (Pierce) — visual 그대로 진행, VFX 트리거 정도 (BulletVisual 측에서).
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_BulletPierce(int bulletId, float hitX, float hitY)
        {
            if (BulletVisualRegistry.TryGet(bulletId, out BulletVisual visual))
            {
                visual.OnServerPierce(new Vector2(hitX, hitY));
            }
        }

        public int ActiveEntryCount => _entries.Count;

        // ──────────────────────────────────────────────
        //  [A303-171] Projectile VFX Publish
        //  develop BulletController.PublishProjectileVfx 패리티 — 모든 client 가
        //  VfxEventBus.Publish 를 호출하도록 RPC 로 broadcast.
        // ──────────────────────────────────────────────

        private void PublishProjectileVfx(
            Entry e,
            VfxEventType eventType,
            Vector2 position,
            Vector2 direction,
            float magnitude,
            int sequenceSalt,
            PlayerRef targetPlayer = default)
        {
            // BulletServerSimulation 은 카드 modifier 세부 (speedMul / sizeMul / status) 를 모두 entry 에 보관하지 않으므로
            // GetProjectileEffectKey 단순 호출 (default modifier) — develop 의 effectKey 결정 로직을 보수적으로 축약.
            string effectKey = VfxPayloadFactory.GetProjectileEffectKey(
                eventType,
                speedMultiplier: 1f,
                sizeMultiplier: 1f,
                bounceCount: e.BounceRemaining,
                pierceCount: e.PierceRemaining,
                knockbackForce: e.KnockbackForce,
                explosionRadius: e.ExplosionRadius,
                gravityModifier: 0f,
                status: StatusEffect.None);

            float radius = ResolveProjectileVfxRadius(eventType, e);
            int sourceTick = VfxPayloadFactory.GetSourceTick(Runner);
            int sourceEventSequence = VfxPayloadFactory.BuildSourceEventSequence(sourceTick, sequenceSalt + e.BulletId);
            int sourceObjectId = Object != null ? unchecked((int)Object.Id.Raw) : 0;

            Rpc_PublishProjectileVfx(
                (int)eventType,
                effectKey,
                sourceObjectId,
                position.x,
                position.y,
                direction.x,
                direction.y,
                radius,
                magnitude,
                e.Owner,
                targetPlayer,
                sourceTick,
                sourceEventSequence);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishProjectileVfx(
            int eventType,
            string effectKey,
            int sourceObjectId,
            float x,
            float y,
            float dirX,
            float dirY,
            float radius,
            float magnitude,
            PlayerRef ownerPlayer,
            PlayerRef targetPlayer,
            int sourceTick,
            int sourceEventSequence)
        {
            VfxEventBus.Publish(VfxPayloadFactory.CreateWithExplicitIdentity(
                (VfxEventType)eventType,
                effectKey,
                effectKey,
                sourceObjectId,
                ownerPlayer,
                targetPlayer,
                new Vector2(x, y),
                new Vector2(dirX, dirY),
                radius,
                DefaultVfxDuration,
                magnitude,
                VfxAttachPoint.World,
                sourceTick,
                sourceEventSequence));
        }

        private float ResolveProjectileVfxRadius(VfxEventType eventType, Entry e)
        {
            switch (eventType)
            {
                case VfxEventType.ExplosionTriggered:
                    return Mathf.Max(e.ExplosionRadius * 0.5882353f, MinVfxRadius);
                case VfxEventType.ProjectileReflected:
                    return 0.5f; // develop 의 character-scaled 로직은 owner gun 의존 — 보수적 fixed.
                default:
                    return MinVfxRadius;
            }
        }
    }
}
