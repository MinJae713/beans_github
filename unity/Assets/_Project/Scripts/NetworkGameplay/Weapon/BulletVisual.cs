using UnityEngine;
using Fusion;

namespace Colosseum.Weapon
{
    /// <summary>
    /// [A303-171] Client-side local visual + simulation 용 projectile.
    ///
    /// NetworkObject 가 아닌 MonoBehaviour. Gun.cs 의 발사 RPC 핸들러가 모든 client 에서
    /// 본 인스턴스를 spawn 한 뒤 Init 으로 발사 정보를 전달하면, 본 인스턴스가 자체적으로
    /// 시뮬레이션 + 시각 표현을 담당한다.
    ///
    /// server-authoritative hit 정보 (Rpc_BulletHit) 가 도착하면 정확한 충돌 위치로 snap 후
    /// 본 인스턴스를 destroy 한다.
    ///
    /// 본 commit (단계 1) 은 기본 시뮬레이션 + 시각 골격만 정의한다.
    ///   - 등가속 운동 (gravity scale)
    ///   - 수명 만료 시 self destroy
    ///   - OnServerHit 단순 호출 골격
    /// 후속 commit 에서 추가:
    ///   - BulletVisualRegistry (bulletId → BulletVisual 매핑) 도입
    ///   - 카드 효과 (반사/관통/폭발) deterministic 시뮬레이션
    ///   - trail / impact VFX 통합
    ///   - 발사자 즉시 muzzle flash + prediction
    /// </summary>
    public class BulletVisual : MonoBehaviour
    {
        [Header("Simulation")]
        [Tooltip("선형 drag — develop BulletController 의 Rigidbody2D.drag (0.2) 대비 체감 발사력 매칭용 (현재 0.5). BulletServerSimulation 과 동일한 값 유지 필요.")]
        [SerializeField] private float _linearDrag = 0.5f;

        // 시뮬레이션 상태 (Init 으로 설정)
        private Vector2 _velocity;
        private float _gravityScale;
        private float _lifetimeRemaining;

        /// <summary>
        /// 식별자. Gun.cs 의 발사 RPC 가 발급한 unique id.
        /// Rpc_BulletHit 도착 시 이 id 로 BulletVisualRegistry 에서 해당 인스턴스를 찾는다 (후속 commit).
        /// </summary>
        public int BulletId { get; private set; }

        /// <summary>
        /// 발사자 PlayerRef. owner ignore time 등 후속 처리에 사용.
        /// </summary>
        public PlayerRef Owner { get; private set; }

        private SpriteRenderer _spriteRenderer;
        private TrailRenderer _trailRenderer;

        private void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _trailRenderer = GetComponentInChildren<TrailRenderer>();

            if (_trailRenderer != null)
            {
                Shader trailShader = Shader.Find("Sprites/Default");
                if (trailShader != null)
                {
                    _trailRenderer.material = new Material(trailShader);
                }
            }
        }

        /// <summary>
        /// 발사 시점 초기화. Gun.cs 의 발사 RPC 핸들러가 모든 client 에서 호출.
        /// </summary>
        public void Init(
            int bulletId,
            PlayerRef owner,
            Vector2 startPosition,
            Vector2 direction,
            float speed,
            float gravityScale,
            float lifetime,
            float sizeMultiplier)
        {
            BulletId = bulletId;
            Owner = owner;

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

            transform.position = startPosition;
            transform.right = dir;
            // develop BulletController 패리티 — prefab base scale (0.08) × 카드 SizeMultiplier.
            float scale = 0.08f * Mathf.Max(sizeMultiplier, 0.001f);
            transform.localScale = new Vector3(scale, scale, 1f);

            _velocity = dir * speed;
            _gravityScale = gravityScale;
            _lifetimeRemaining = lifetime;

            // [A303-171 단계 3] Registry 등록 — Rpc_BulletHit 가 bulletId 로 본 인스턴스를 찾기 위함.
            BulletVisualRegistry.Register(bulletId, this);
        }

        private void OnDestroy()
        {
            // 수명 만료 / OnServerHit / scene 전환 등 모든 destroy 경로에서 registry 해제.
            BulletVisualRegistry.Unregister(BulletId);
        }

        private void FixedUpdate()
        {
            // 등가속 운동 시뮬레이션 (Rigidbody2D 사용 안 함, 직접 계산).
            // Physics2D.gravity 의 default 는 (0, -9.81). gravityScale 1.0 이면 그대로 적용.
            //
            // 본 시뮬레이션은 server 와 client 가 동일한 알고리즘을 사용하지만, frame rate /
            // FixedUpdate 호출 빈도 차이로 미세한 위치 차이가 발생할 수 있다. 충돌 위치는 server
            // 권위로 별도 Rpc_BulletHit 가 전달하므로 시각 잔여는 OnServerHit 의 snap 으로 해소한다.
            float dt = Time.fixedDeltaTime;

            _velocity += Physics2D.gravity * _gravityScale * dt;
            _velocity *= Mathf.Max(0f, 1f - _linearDrag * dt);
            transform.position += (Vector3)(_velocity * dt);

            if (_velocity.sqrMagnitude > 0.0001f)
            {
                transform.right = _velocity.normalized;
            }

            _lifetimeRemaining -= dt;
            if (_lifetimeRemaining <= 0f)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// server-authoritative hit 도착 시 호출. 정확한 충돌 위치로 snap 후 visual destroy.
        /// 후속 commit 에서 impact VFX 트리거 + 즉시 sprite/trail hide 추가.
        /// </summary>
        public void OnServerHit(Vector2 hitPosition)
        {
            transform.position = hitPosition;
            Destroy(gameObject);
        }

        /// <summary>
        /// [A303-171 단계 6] server-authoritative bounce 도착 시 호출.
        /// 반사 후 정확한 위치 + velocity 로 client-side 시뮬레이션 재설정.
        /// </summary>
        public void OnServerBounce(Vector2 newPosition, Vector2 newVelocity)
        {
            transform.position = newPosition;
            _velocity = newVelocity;
            if (_velocity.sqrMagnitude > 0.0001f)
            {
                transform.right = _velocity.normalized;
            }
        }

        /// <summary>
        /// [A303-171 단계 6] server-authoritative pierce 도착 시 호출.
        /// 위치/velocity 변경 없음. 본 메서드는 향후 pierce VFX 트리거 hook (지금은 단순 marker).
        /// </summary>
        public void OnServerPierce(Vector2 piercePosition)
        {
            // 향후 VFX 트리거 hook. 현재는 no-op.
        }
    }
}
