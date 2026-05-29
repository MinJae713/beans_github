using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;
using Colosseum.Weapon;
using Colosseum.Game;
using Colosseum.Card;
using Colosseum.Contracts;
using Colosseum.NetworkGameplay.VFX;
using Colosseum.Status;
using Colosseum.Utils;
using Colosseum.NetworkGameplay.Diagnostics;

namespace Colosseum.Player
{
    public class PlayerHealth : NetworkBehaviour
    {
        [Header("Gameplay Profile")]
        [Tooltip("Assets/_Project/Data/GameplayProfile.asset 드래그. 미할당 시 fallback 값 사용 + LogError.")]
        [SerializeField] private GameplayProfile _profile;

        // Profile 미할당 fallback. 마이그레이션 안전망.
        private const float FallbackMaxHealth = 100f;
        private const float FallbackRespawnDelay = 2f;
        private const float FallbackRespawnInvulnerable = 1f;
        private const int PlayerSpawnVfxSequenceSalt = 200;
        private const int PlayerDeathVfxSequenceSalt = 201;
        private const int PlayerRespawnVfxSequenceSalt = 202;
        private const int PlayerHealOnKillVfxSequenceSalt = 203;
        private const int PlayerInvulnerabilityStartedVfxSequenceSalt = 204;
        private const int PitFallDeathVfxSequenceSalt = 390;
        private const float RespawnHorizontalCameraDeathGraceSeconds = 3f;

        /// <summary>플레이어 최대 체력. PlayerHPBar 등 외부에서 참조.</summary>
        public float MaxHealth => _profile != null ? _profile.combat.baseline.maxHealth : FallbackMaxHealth;
        private float RespawnDelay => _profile != null ? _profile.combat.baseline.respawnDelaySeconds : FallbackRespawnDelay;
        // D48 (Q45 closed): 리스폰 직후 무적 시간. 명세 = 1.0초.
        // 근거: docs/30_전장_레벨디자인/10_세부설계/02_MVP_첫_전장_명세.md §리스폰 처리,
        //       docs/85_밸런스_프리셋/20_수치프리셋/01_MVP_전투_수치_프리셋.md respawnInvulnerableSeconds.
        private float RespawnInvulnerableSeconds => _profile != null ? _profile.combat.baseline.respawnInvulnerableSeconds : FallbackRespawnInvulnerable;

        [Networked, OnChangedRender(nameof(OnHealthChanged))]
        public float CurrentHealth { get; set; }

        [Networked, OnChangedRender(nameof(OnDeadChanged))]
        public NetworkBool IsDead { get; set; }
        [Networked] public float LifestealPercent { get; set; } = 0f;
        [Networked] private TickTimer _respawnTimer { get; set; }
        // D48: A214 1초 무적 게이트. Spawned + DoRespawn 시점에 StartRespawnInvulnerability()로 갱신.
        [Networked] public TickTimer InvulnerabilityTimer { get; set; }
        [Networked] private TickTimer _horizontalCameraDeathGraceTimer { get; set; }

        /// <summary>
        /// CurrentHealth가 변동될 때 외부 구독자에게 발행되는 이벤트.
        /// PlayerHPBar 등 UI가 매 프레임 폴링 대신 이 이벤트를 구독하여 변동 시점에만 갱신한다.
        /// 구독자는 OnDestroy/Despawned에서 -= 해제 필수 (이벤트 누수 방지).
        /// </summary>
        public event System.Action<PlayerHealth> OnHealthChangedEvent;
        private static readonly HashSet<PlayerHealth> ActivePlayers = new HashSet<PlayerHealth>();
        public static IEnumerable<PlayerHealth> ActiveInstances => ActivePlayers;
        public static event System.Action<PlayerHealth> SpawnedEvent;
        public static event System.Action<PlayerHealth> DespawnedEvent;

        private RoomManager _roomManager;
        private SpriteRenderer _spriteRenderer;
        private Rigidbody2D _rb;
        private CardDeck _cardDeck;
        private PlayerStatusController _statusController;

        public override void Spawned()
        {
            if (_profile == null)
            {
                Debug.LogError($"[Colosseum] PlayerHealth on {gameObject.name}: GameplayProfile 미할당. Fallback 값 사용 중. " +
                    $"Player.prefab → PlayerHealth → Profile 슬롯에 GameplayProfile.asset 드래그 필요.");
            }

            if (Object.HasStateAuthority)
            {
                CurrentHealth = MaxHealth;
                IsDead = false;
                // D48: 게임 시작 spawn 직후에도 1초 무적 적용 (P1/P2 첫 등장 안전 가드).
                StartRespawnInvulnerability();
                PublishInvulnerabilityStartedVfx();
                PublishPlayerHealthVfx(
                    VfxEventType.PlayerSpawn,
                    VfxPayloadFactory.GetPlayerHealthEffectKey(VfxEventType.PlayerSpawn),
                    Object.InputAuthority,
                    Object.InputAuthority,
                    transform.position,
                    1f,
                    MaxHealth,
                    PlayerSpawnVfxSequenceSalt);
            }

            _roomManager = SceneSearch.FindInScene<RoomManager>(gameObject.scene);
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _rb = GetComponent<Rigidbody2D>();
            _cardDeck = SceneSearch.FindInScene<CardDeck>(gameObject.scene);
            _statusController = GetComponent<PlayerStatusController>();

            ActivePlayers.Add(this);
            SpawnedEvent?.Invoke(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ActivePlayers.Remove(this);
            DespawnedEvent?.Invoke(this);
            base.Despawned(runner, hasState);
        }

        private void OnDestroy()
        {
            if (ActivePlayers.Remove(this))
            {
                DespawnedEvent?.Invoke(this);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            // 리스폰 타이머
            if (IsDead && CanRespawn() && _respawnTimer.Expired(Runner))
            {
                DoRespawn();
            }

            if (!IsDead && _roomManager != null && ShouldApplyOutOfBoundsDeath(_roomManager.IsVictoryRunActive))
            {
                bool belowVisibleCamera = _roomManager.IsBelowVisibleCamera(transform.position);
                bool horizontalGraceActive = IsHorizontalCameraDeathGraceActive();
                bool horizontalOutOfRoomBounds = _roomManager.IsOutOfBounds(transform.position);
                bool horizontalOutsideVisibleCamera = _roomManager.IsHorizontallyOutsideVisibleCamera(transform.position);
                bool horizontalDeath = ShouldApplyHorizontalOutOfBoundsDeath(_roomManager.IsVictoryRunActive, horizontalGraceActive) &&
                                       (horizontalOutOfRoomBounds || horizontalOutsideVisibleCamera);

                if (belowVisibleCamera || horizontalDeath)
                {
                    // 맵/카메라 밖으로 밀려나면 상대방이 킬한 것으로 처리
                    PlayerRef opponent = FindOpponentRef();
                    DiagLog.Log(
                        $"[Colosseum] Player out of combat bounds! " +
                        $"roomHorizontal={horizontalOutOfRoomBounds}, cameraHorizontal={horizontalOutsideVisibleCamera}, " +
                        $"cameraBelow={belowVisibleCamera}, horizontalGrace={horizontalGraceActive}, credited={opponent}");
                    Die(opponent);
                }
            }
        }

        private static bool ShouldApplyOutOfBoundsDeath(bool isVictoryRunActive)
        {
            return !isVictoryRunActive;
        }

        private static bool ShouldApplyOutOfBoundsDeath(bool isVictoryRunActive, bool isRespawnInvulnerabilityActive)
        {
            return !isVictoryRunActive;
        }

        private static bool ShouldApplyHorizontalOutOfBoundsDeath(bool isVictoryRunActive, bool isRespawnCameraGraceActive)
        {
            return !isVictoryRunActive && !isRespawnCameraGraceActive;
        }

        private static bool ShouldIgnoreDamageDuringVictoryRun(bool isVictoryRunActive)
        {
            return isVictoryRunActive;
        }

        public static bool ShouldIgnoreEnvironmentDeath(bool isVictoryRunDamageProtected, bool isRespawnInvulnerabilityActive)
        {
            return isVictoryRunDamageProtected || isRespawnInvulnerabilityActive;
        }

        public static bool ShouldIgnoreCombatKnockback(bool isVictoryRunDamageProtected, bool isRespawnInvulnerabilityActive)
        {
            return isVictoryRunDamageProtected || isRespawnInvulnerabilityActive;
        }

        public bool IsVictoryRunDamageProtectionActive()
        {
            if (_roomManager == null)
            {
                _roomManager = SceneSearch.FindInScene<RoomManager>(gameObject.scene);
            }

            return _roomManager != null &&
                   _roomManager.IsNetworkedStateReady &&
                   ShouldIgnoreDamageDuringVictoryRun(_roomManager.IsVictoryRunActive);
        }

        public bool IsCombatKnockbackProtectionActive()
        {
            return ShouldIgnoreCombatKnockback(
                IsVictoryRunDamageProtectionActive(),
                IsRespawnInvulnerabilityActive());
        }

        public void TakeDamage(float damage, PlayerRef attacker, Colosseum.Weapon.StatusEffect status, float statusDuration = 0f, float statusMagnitude = 1f)
        {
            int statusMask = Colosseum.Weapon.StatusEffectExtensions.ToMask(status);
            float freezeDuration = 0f;
            float burnDuration = 0f;
            float poisonDuration = 0f;
            float electricDuration = 0f;
            AssignStatusDuration(status, statusDuration, ref freezeDuration, ref burnDuration, ref poisonDuration, ref electricDuration);

            TakeDamage(
                damage,
                attacker,
                statusMask,
                freezeDuration,
                burnDuration,
                poisonDuration,
                electricDuration,
                statusMagnitude);
        }

        public void TakeDamage(
            float damage,
            PlayerRef attacker,
            int statusMask,
            float freezeStatusDuration = 0f,
            float burnStatusDuration = 0f,
            float poisonStatusDuration = 0f,
            float electricStatusDuration = 0f,
            float statusMagnitude = 1f)
        {
            TakeDamage(
                damage,
                attacker,
                statusMask,
                freezeStatusDuration,
                burnStatusDuration,
                poisonStatusDuration,
                electricStatusDuration,
                statusMagnitude,
                statusMagnitude,
                statusMagnitude,
                statusMagnitude);
        }

        public void TakeDamage(
            float damage,
            PlayerRef attacker,
            int statusMask,
            float freezeStatusDuration,
            float burnStatusDuration,
            float poisonStatusDuration,
            float electricStatusDuration,
            float freezeStatusMagnitude,
            float burnStatusMagnitude,
            float poisonStatusMagnitude,
            float electricStatusMagnitude)
        {
            if (!Object.HasStateAuthority) return;
            if (IsDead) return;
            // VictoryRun blocks both damage and status application so late projectiles cannot change the match result.
            if (IsVictoryRunDamageProtectionActive())
            {
                DiagLog.Log($"[Colosseum] PlayerHealth: damage {damage} ignored — victory run active.");
                return;
            }

            // D48: A214 무적 게이트. 리스폰 직후 1초간 데미지 무시.
            if (!InvulnerabilityTimer.ExpiredOrNotRunning(Runner))
            {
                DiagLog.Log($"[Colosseum] PlayerHealth: damage {damage} ignored — invulnerability active.");
                return;
            }

            CurrentHealth -= damage;
            DiagLog.Log($"[Colosseum] Player hit! HP: {CurrentHealth}/{MaxHealth}");

            // 라이프스틸 처리
            if (LifestealPercent > 0f && attacker != Object.InputAuthority)
            {
                HealAttacker(attacker, damage * LifestealPercent);
            }

            // 상태이상 forward — A303-261 인프라가 받아내는 진입점.
            // statusMask가 0이 아니면 PlayerStatusController가 매핑된 SO를 찾아 모두 Apply.
            // SO 매핑 미할당 시 ApplyByEnum 내부에서 no-op (Debug.Log 한 줄과 동일 효과).
            ApplyStatusesByMask(
                statusMask,
                attacker,
                freezeStatusMagnitude,
                burnStatusMagnitude,
                poisonStatusMagnitude,
                electricStatusMagnitude,
                freezeStatusDuration,
                burnStatusDuration,
                poisonStatusDuration,
                electricStatusDuration);

            if (CurrentHealth <= 0f)
            {
                Die(attacker);
            }
        }

        private void ApplyStatusesByMask(
            int statusMask,
            PlayerRef attacker,
            float freezeStatusMagnitude,
            float burnStatusMagnitude,
            float poisonStatusMagnitude,
            float electricStatusMagnitude,
            float freezeStatusDuration,
            float burnStatusDuration,
            float poisonStatusDuration,
            float electricStatusDuration)
        {
            if (statusMask == 0 || _statusController == null)
            {
                return;
            }

            ApplyStatusIfPresent(statusMask, Colosseum.Weapon.StatusEffect.Freeze, attacker, freezeStatusMagnitude, freezeStatusDuration);
            ApplyStatusIfPresent(statusMask, Colosseum.Weapon.StatusEffect.Burn, attacker, burnStatusMagnitude, burnStatusDuration);
            ApplyStatusIfPresent(statusMask, Colosseum.Weapon.StatusEffect.Poison, attacker, poisonStatusMagnitude, poisonStatusDuration);
            ApplyStatusIfPresent(statusMask, Colosseum.Weapon.StatusEffect.Electric, attacker, electricStatusMagnitude, electricStatusDuration);
        }

        private void ApplyStatusIfPresent(
            int statusMask,
            Colosseum.Weapon.StatusEffect status,
            PlayerRef attacker,
            float statusMagnitude,
            float statusDuration)
        {
            if (!Colosseum.Weapon.StatusEffectExtensions.HasStatus(statusMask, status))
            {
                return;
            }

            _statusController.ApplyByEnum(status, attacker, statusMagnitude, statusDuration);
        }

        private static void AssignStatusDuration(
            Colosseum.Weapon.StatusEffect status,
            float statusDuration,
            ref float freezeStatusDuration,
            ref float burnStatusDuration,
            ref float poisonStatusDuration,
            ref float electricStatusDuration)
        {
            switch (status)
            {
                case Colosseum.Weapon.StatusEffect.Freeze:
                    freezeStatusDuration = statusDuration;
                    break;
                case Colosseum.Weapon.StatusEffect.Burn:
                    burnStatusDuration = statusDuration;
                    break;
                case Colosseum.Weapon.StatusEffect.Poison:
                    poisonStatusDuration = statusDuration;
                    break;
                case Colosseum.Weapon.StatusEffect.Electric:
                    electricStatusDuration = statusDuration;
                    break;
            }
        }

        private void Die(PlayerRef killer, bool triggerCardDraw = false)
        {
            bool isSuicide = killer == default(PlayerRef) || killer == Object.InputAuthority;
            PlayerRef creditedKiller = isSuicide ? default(PlayerRef) : killer;
            PlayerRef movementAuthority = ResolveMovementAuthorityAfterDeath(
                creditedKiller,
                Object.InputAuthority,
                FindOpponentRefOrDefault());

            IsDead = true;
            CurrentHealth = 0f;

            // 물리 비활성화 (서버 측)
            // 시각 숨김은 PlayerAnimatorController가 Death MMFeedbacks 완료 후 처리
            if (_rb != null)
            {
                _rb.velocity = Vector2.zero;
                _rb.simulated = false;
            }

            // 카드 드로우: 방 전환으로 인한 사망 시에만
            if (triggerCardDraw)
            {
                if (_cardDeck == null)
                    _cardDeck = SceneSearch.FindInScene<CardDeck>(gameObject.scene);

                if (_cardDeck != null)
                    _cardDeck.StartDraw(Object.InputAuthority);
            }

            // Camera/advance authority follows the surviving opponent even for pit/self deaths.
            // Reward effects still use creditedKiller so self-kill does not grant heal-on-kill.
            if (_roomManager != null)
            {
                _roomManager.RegisterDeathRespawnAnchor(
                    Object.InputAuthority,
                    movementAuthority,
                    FindPlayerPosition(movementAuthority, transform.position));
                _roomManager.RegisterKill(movementAuthority);
            }

            PublishPlayerHealthVfx(
                VfxEventType.PlayerDeath,
                VfxPayloadFactory.GetPlayerHealthEffectKey(VfxEventType.PlayerDeath),
                creditedKiller,
                Object.InputAuthority,
                transform.position,
                1.1f,
                RespawnDelay,
                PlayerDeathVfxSequenceSalt);

            // 킬러에게 HealOnKill 적용
            ApplyHealOnKill(creditedKiller);

            _respawnTimer = TickTimer.CreateFromSeconds(Runner, RespawnDelay);
            DiagLog.Log($"[Colosseum] Player died! Killer: {creditedKiller} (authority: {movementAuthority}, original: {killer}, suicide:{isSuicide}, cardDraw:{triggerCardDraw})");
        }

        private void DoRespawn()
        {
            IsDead = false;
            CurrentHealth = MaxHealth;
            // D48: A214 1초 무적. 리스폰 직후 입력 전 사망/억울 피격 방지.
            StartRespawnInvulnerability();

            // 시각적 재활성화 (본인 + 자식 포함)
            SetVisuals(true);
            if (_rb != null)
            {
                _rb.simulated = true;
                _rb.velocity = Vector2.zero;
            }

            // 총 재장전 리셋
            var gun = GetComponentInChildren<Gun>();
            if (gun != null)
            {
                gun.ForceReload();
            }

            // 리스폰 위치 계산
            if (_roomManager != null)
            {
                Vector2 opponentPos = FindOpponentPosition();
                Vector2 respawnPos = _roomManager.GetRespawnPosition(Object.InputAuthority, opponentPos, this);
                TeleportNetworkedRigidbody(new Vector3(respawnPos.x, respawnPos.y, 0f));
                _roomManager.RegisterRespawn(Object.InputAuthority);
            }

            PublishPlayerHealthVfx(
                VfxEventType.PlayerRespawn,
                VfxPayloadFactory.GetPlayerHealthEffectKey(VfxEventType.PlayerRespawn),
                Object.InputAuthority,
                Object.InputAuthority,
                transform.position,
                1f,
                RespawnInvulnerableSeconds,
                PlayerRespawnVfxSequenceSalt);
            PublishInvulnerabilityStartedVfx();

            DiagLog.Log("[Colosseum] Player respawned!");
        }

        private bool CanRespawn()
        {
            return _roomManager == null || !_roomManager.IsNetworkedStateReady || !_roomManager.IsVictoryRunActive;
        }

        /// <summary>
        /// D48 (Q45 closed) — 리스폰 직후 1초 무적 시작.
        /// StateAuthority에서만 호출. Spawned() / DoRespawn() / 외부 리스폰 어댑터에서 호출 가능.
        /// 명세: docs/30_전장_레벨디자인/10_세부설계/02_MVP_첫_전장_명세.md §리스폰 처리,
        ///       docs/85_밸런스_프리셋/20_수치프리셋/01_MVP_전투_수치_프리셋.md respawnInvulnerableSeconds.
        /// </summary>
        public void StartRespawnInvulnerability()
        {
            if (Object == null || !Object.HasStateAuthority) return;
            InvulnerabilityTimer = TickTimer.CreateFromSeconds(Runner, RespawnInvulnerableSeconds);
            _horizontalCameraDeathGraceTimer = TickTimer.CreateFromSeconds(Runner, RespawnHorizontalCameraDeathGraceSeconds);
        }

        private bool IsRespawnInvulnerabilityActive()
        {
            return Runner != null && !InvulnerabilityTimer.ExpiredOrNotRunning(Runner);
        }

        private bool IsHorizontalCameraDeathGraceActive()
        {
            return Runner != null && !_horizontalCameraDeathGraceTimer.ExpiredOrNotRunning(Runner);
        }

        private void PublishInvulnerabilityStartedVfx()
        {
            PublishPlayerHealthVfx(
                VfxEventType.PlayerInvulnerabilityStarted,
                VfxPayloadFactory.GetPlayerHealthEffectKey(VfxEventType.PlayerInvulnerabilityStarted),
                Object.InputAuthority,
                Object.InputAuthority,
                transform.position,
                1f,
                1f,
                PlayerInvulnerabilityStartedVfxSequenceSalt,
                RespawnInvulnerableSeconds);
        }

        public void ForceDeath(PlayerRef killer)
        {
            if (!Object.HasStateAuthority) return;
            if (IsDead) return;
            Die(killer);
        }

        public static PlayerRef ResolveMovementAuthorityAfterDeath(
            PlayerRef creditedKiller,
            PlayerRef deadPlayer,
            PlayerRef opponentPlayer)
        {
            if (creditedKiller != default(PlayerRef) && creditedKiller != deadPlayer)
            {
                return creditedKiller;
            }

            if (opponentPlayer != default(PlayerRef) && opponentPlayer != deadPlayer)
            {
                return opponentPlayer;
            }

            return default;
        }

        private void TeleportNetworkedRigidbody(Vector3 position)
        {
            var networkRigidbody = GetComponent<NetworkRigidbody2D>();
            if (networkRigidbody != null)
            {
                networkRigidbody.Teleport(position);
            }

            transform.position = position;

            if (_rb != null)
            {
                _rb.position = position;
                _rb.velocity = Vector2.zero;
            }

            Physics2D.SyncTransforms();
        }

        public void PublishPitFallDeathVfx(Vector2 worldPosition)
        {
            if (!Object.HasStateAuthority || Runner == null)
            {
                return;
            }

            Vector2 velocity = _rb != null ? _rb.velocity : Vector2.down;
            Rpc_PublishPitFallDeathVfx(
                worldPosition.x,
                worldPosition.y,
                velocity.x,
                velocity.y,
                PitFallDeathVfxSequenceSalt);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishPitFallDeathVfx(
            float x,
            float y,
            float directionX,
            float directionY,
            int sequenceSalt)
        {
            string effectKey = VfxPayloadFactory.GetEnvironmentEffectKey(VfxEventType.PitFallDeath);
            if (string.IsNullOrEmpty(effectKey)) return;

            VfxEventBus.Publish(VfxPayloadFactory.Create(
                VfxEventType.PitFallDeath,
                string.Empty,
                effectKey,
                Runner,
                Object,
                Object.InputAuthority,
                Object.InputAuthority,
                new Vector2(x, y),
                new Vector2(directionX, directionY),
                1.4f,
                0.8f,
                1f,
                VfxAttachPoint.World,
                sequenceSalt));
        }

        /// <summary>
        /// D48 visualizer — 무적 중 SpriteRenderer alpha flicker (0.5↔1.0 PingPong).
        /// Time.time 사용 (Time.deltaTime in FixedUpdateNetwork 금지선과 무관 — 50/04 Fusion 가드레일 준수).
        /// 모든 클라이언트에서 시각 동기화되며, [Networked] InvulnerabilityTimer 자체는 StateAuthority에서만 set.
        /// </summary>
        private void Update()
        {
            if (_spriteRenderer == null) return;
            if (Runner == null) return;
            bool invuln = !InvulnerabilityTimer.ExpiredOrNotRunning(Runner);
            Color c = _spriteRenderer.color;
            c.a = invuln ? (Mathf.PingPong(Time.time * 4f, 0.5f) + 0.5f) : 1.0f;
            _spriteRenderer.color = c;
        }

        /// <summary>
        /// 방 전환으로 인한 사망. 카드 드로우 트리거됨.
        /// RoomManager.HandleRoomAdvance()에서 호출.
        /// </summary>
        public void DieByRoomAdvance(PlayerRef killer)
        {
            if (!Object.HasStateAuthority) return;
            if (IsDead) return;
            Die(killer, triggerCardDraw: true);
        }

        private void OnHealthChanged()
        {
            // CurrentHealth가 변동될 때 Fusion이 자동으로 호출. 외부 구독자(PlayerHPBar 등)에게 알림.
            OnHealthChangedEvent?.Invoke(this);
        }

        /// <summary>
        /// IsDead가 네트워크 동기화될 때 모든 클라이언트에서 호출.
        /// 서버에서만 실행되던 SetVisuals를 클라이언트에도 반영.
        /// </summary>
        private void OnDeadChanged()
        {
            // 부활 시에만 시각 복원. 사망 시 숨김은 PlayerAnimatorController가 Death MMFeedbacks 완료 후 처리
            if (!IsDead)
            {
                SetVisuals(true);
                var animatorController = GetComponent<PlayerAnimatorController>();
                if (animatorController != null)
                {
                    animatorController.ResetVisualStateAfterRespawn();
                }
                PublishRespawnVfx();
            }

            if (_rb != null)
            {
                _rb.simulated = !IsDead;
                if (IsDead)
                    _rb.velocity = Vector2.zero;
            }
        }

        private void PublishRespawnVfx()
        {
            if (Object == null || Runner == null)
            {
                return;
            }

            VfxEventBus.Publish(VfxPayloadFactory.Create(
                VfxEventType.PlayerRespawned,
                "player",
                "player_respawned",
                Runner,
                Object,
                Object.InputAuthority,
                default(PlayerRef),
                transform.position,
                Vector2.up,
                1f,
                RespawnInvulnerableSeconds,
                CurrentHealth,
                VfxAttachPoint.World,
                71));
        }

        public void HideVisualsAfterDeath()
        {
            if (IsDead)
                SetVisuals(false);
        }

        private void SetVisuals(bool visible)
        {
            // 본인 + 자식(Gun 등)의 모든 SpriteRenderer 제어
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var r in renderers)
            {
                r.enabled = visible;
            }
        }

        private PlayerRef FindOpponentRef()
        {
            var players = SceneSearch.FindAllInScene<PlayerHealth>(gameObject.scene);
            foreach (var p in players)
            {
                var netObj = p.GetComponent<Fusion.NetworkObject>();
                if (netObj != null && netObj.InputAuthority != Object.InputAuthority)
                {
                    return netObj.InputAuthority;
                }
            }
            // 솔로 테스트: 상대가 없으면 본인을 killer로 (자해 처리)
            return Object.InputAuthority;
        }

        private PlayerRef FindAliveOpponentRef()
        {
            var players = SceneSearch.FindAllInScene<PlayerHealth>(gameObject.scene);
            foreach (var p in players)
            {
                var netObj = p.GetComponent<Fusion.NetworkObject>();
                if (netObj != null && netObj.InputAuthority != Object.InputAuthority && !p.IsDead)
                {
                    return netObj.InputAuthority;
                }
            }

            return default;
        }

        private PlayerRef FindOpponentRefOrDefault()
        {
            var players = SceneSearch.FindAllInScene<PlayerHealth>(gameObject.scene);
            foreach (var p in players)
            {
                var netObj = p.GetComponent<Fusion.NetworkObject>();
                if (netObj != null && netObj.InputAuthority != Object.InputAuthority)
                {
                    return netObj.InputAuthority;
                }
            }

            return default;
        }

        private Vector2 FindOpponentPosition()
        {
            var players = SceneSearch.FindAllInScene<NetworkObject>(gameObject.scene);
            foreach (var p in players)
            {
                if (p.InputAuthority != Object.InputAuthority && p.GetComponent<PlayerHealth>() != null)
                {
                    return p.transform.position;
                }
            }
            return Vector2.zero;
        }

        private Vector2 FindPlayerPosition(PlayerRef player, Vector2 fallbackPosition)
        {
            if (player == default(PlayerRef))
            {
                return fallbackPosition;
            }

            var players = SceneSearch.FindAllInScene<NetworkObject>(gameObject.scene);
            foreach (var p in players)
            {
                if (p.InputAuthority == player && p.GetComponent<PlayerHealth>() != null)
                {
                    return p.transform.position;
                }
            }

            return fallbackPosition;
        }

        private void HealAttacker(PlayerRef attacker, float healAmount)
        {
            var players = SceneSearch.FindAllInScene<PlayerHealth>(gameObject.scene);
            foreach (var p in players)
            {
                var netObj = p.GetComponent<NetworkObject>();
                if (netObj == null || netObj.InputAuthority != attacker) continue;

                p.CurrentHealth = Mathf.Min(p.CurrentHealth + healAmount, p.MaxHealth);
                DiagLog.Log($"[Colosseum] Lifesteal: healed {attacker} for {healAmount:F1} HP");
                break;
            }
        }

        private void ApplyHealOnKill(PlayerRef killer)
        {
            if (killer == default(PlayerRef)) return;

            // 킬러를 찾아서 CardEffect의 HealOnKill만큼 회복
            var players = SceneSearch.FindAllInScene<PlayerHealth>(gameObject.scene);
            foreach (var p in players)
            {
                var netObj = p.GetComponent<NetworkObject>();
                if (netObj == null || netObj.InputAuthority != killer) continue;

                var cardEffect = p.GetComponent<CardEffect>();
                if (cardEffect != null && cardEffect.HealOnKill > 0f)
                {
                    p.CurrentHealth = Mathf.Min(p.CurrentHealth + cardEffect.HealOnKill, p.MaxHealth);
                    PublishPlayerHealthVfx(
                        VfxEventType.PlayerHealOnKill,
                        VfxPayloadFactory.GetPlayerHealthEffectKey(VfxEventType.PlayerHealOnKill),
                        killer,
                        killer,
                        p.transform.position,
                        0.85f,
                        cardEffect.HealOnKill,
                        PlayerHealOnKillVfxSequenceSalt);
                    DiagLog.Log($"[Colosseum] HealOnKill: healed {killer} for {cardEffect.HealOnKill} HP");
                }
                break;
            }
        }

        private void PublishPlayerHealthVfx(
            VfxEventType eventType,
            string effectKey,
            PlayerRef sourcePlayer,
            PlayerRef targetPlayer,
            Vector2 position,
            float radius,
            float magnitude,
            int sequenceSalt,
            float duration = 0.9f)
        {
            Rpc_PublishPlayerHealthVfx(
                (int)eventType,
                effectKey,
                sourcePlayer,
                targetPlayer,
                position.x,
                position.y,
                radius,
                duration,
                magnitude,
                sequenceSalt);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishPlayerHealthVfx(
            int eventType,
            string effectKey,
            PlayerRef sourcePlayer,
            PlayerRef targetPlayer,
            float x,
            float y,
            float radius,
            float duration,
            float magnitude,
            int sequenceSalt)
        {
            VfxEventBus.Publish(VfxPayloadFactory.Create(
                (VfxEventType)eventType,
                effectKey,
                effectKey,
                Runner,
                Object,
                sourcePlayer,
                targetPlayer,
                new Vector2(x, y),
                Vector2.up,
                radius,
                duration,
                magnitude,
                VfxAttachPoint.CharacterBody,
                sequenceSalt));
        }
    }
}
