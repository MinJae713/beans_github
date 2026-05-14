using UnityEngine;

namespace Colosseum.Game
{
    /// <summary>
    /// 게임플레이 핵심 수치/정책 프리셋.
    /// 기본 전투(combat)와 카드 제시(cardOffer), 구간/경계(segment) 시그니처를 함께 관리한다.
    /// 런타임 로직 전환은 단계적으로 진행한다.
    ///
    /// 근거: docs/85_밸런스_프리셋/10_데이터스키마/01_GameplayProfile_스키마.md
    ///       docs/85_밸런스_프리셋/20_수치프리셋/01_MVP_전투_수치_프리셋.md
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayProfile", menuName = "Colosseum/Gameplay Profile", order = 0)]
    public class GameplayProfile : ScriptableObject
    {
        [System.Serializable]
        public class Metadata
        {
            public string profileKey = "gameplay/mvp/default";
            public string version = "v0";
            public string status = "value_candidate";
            [TextArea] public string note = "";
        }

        [System.Serializable]
        public class CombatBaseline
        {
            [Header("Player")]
            [Tooltip("baseline.maxHealth")]
            public float maxHealth = 100f;

            [Tooltip("baseline.respawnDelaySeconds")]
            public float respawnDelaySeconds = 2f;

            [Tooltip("baseline.respawnInvulnerableSeconds")]
            public float respawnInvulnerableSeconds = 1f;

            [Header("Player Locomotion")]
            [Tooltip("baseline.moveSpeed — 좌우 이동 속도")]
            public float moveSpeed = 8f;

            [Tooltip("baseline.jumpForce — 점프 초기 속도. 카드 JumpForceMultiplier 적용 전 값")]
            public float jumpForce = 12f;

            [Tooltip("baseline.jumpBufferSeconds — 공중에서 점프 키 누른 후 착지 시 자동 점프 허용 시간. 0이면 즉시 비활성")]
            public float jumpBufferSeconds = 0.1f;

            [Tooltip("baseline.variableJumpCutFactor — 점프 키 떼는 순간 상승 속도 cut 비율. 1=풀 점프, 0.5=절반, 0=즉시 정지")]
            [Range(0f, 1f)]
            public float variableJumpCutFactor = 0.5f;

            [Tooltip("baseline.fallGravityMultiplier — 떨어질 때(y속도 음수) 중력 배율. 1.0=평소, 1.5~2.5=가벼운 점프 느낌")]
            public float fallGravityMultiplier = 2.0f;

            [Tooltip("baseline.apexHangThreshold — 점프 정점 부근으로 간주할 |y속도| 임계값(m/s). 이 속도 이하 상승 중일 때 apexGravityMultiplier 적용")]
            public float apexHangThreshold = 1.5f;

            [Tooltip("baseline.apexGravityMultiplier — 정점 부근 중력 배율. 1.0 미만이면 정점에서 잠깐 떠 있음")]
            [Range(0f, 1f)]
            public float apexGravityMultiplier = 0.5f;

            [Tooltip("baseline.ascendGravityMultiplier — 상승 중(y속도 양수 + apex 임계값 이상) 중력 배율. 1.0=평소, >1.0=상승 시간 단축(빠르게 정점 도달)")]
            public float ascendGravityMultiplier = 1.0f;

            [Header("Player Wall Climb (A303-292)")]
            [Tooltip("baseline.wallSlideSpeed — 벽 슬라이드 시 하강 속도(m/s, 양수). 자유낙하 대비 절댓값 작을수록 천천히 미끄러짐")]
            public float wallSlideSpeed = 2.5f;

            [Tooltip("baseline.wallJumpVerticalForce — 벽 점프 수직 속도(m/s). 일반 jumpForce와 별도로 잠금")]
            public float wallJumpVerticalForce = 11f;

            [Tooltip("baseline.wallJumpHorizontalForce — 벽 점프 수평 추진 속도(m/s). 벽 반대 방향으로 적용")]
            public float wallJumpHorizontalForce = 7f;

            [Tooltip("baseline.wallJumpVariance — 도움 닫기 변동 비율(0~0.5). 0.1=±10%. ROUNDS 동일 사상")]
            [Range(0f, 0.5f)]
            public float wallJumpVariance = 0.1f;

            [Tooltip("baseline.wallReattachLockoutSeconds — 벽 점프 직후 같은 측 벽 재부착 차단 시간(초)")]
            public float wallReattachLockoutSeconds = 0.2f;

            [Tooltip("baseline.wallSlideStickInputThreshold — 벽에 붙기 위한 수평 입력 dead zone(0~1). 이 값 이상 입력 + 벽 접촉 시 부착")]
            [Range(0f, 1f)]
            public float wallSlideStickInputThreshold = 0.5f;

            [Header("Weapon")]
            [Tooltip("baseline.fireInterval")]
            public float fireInterval = 1f;

            [Tooltip("baseline.ammoCapacity")]
            public int ammoCapacity = 3;

            [Tooltip("baseline.reloadTime")]
            public float reloadTime = 1.5f;

            [Header("Projectile")]
            [Tooltip("baseline.directHitDamage")]
            public float directHitDamage = 10f;

            [Tooltip("baseline.projectileSpeed")]
            public float projectileSpeed = 15f;

            [Tooltip("baseline.projectileGravityScale")]
            public float projectileGravityScale = 1f;

            [Tooltip("baseline.projectileLifetimeSeconds")]
            public float projectileLifetimeSeconds = 5f;

            [Tooltip("baseline.projectileBaseScale")]
            public float projectileBaseScale = 1f;

            [Tooltip("baseline.directHitKnockbackImpulse")]
            public float directHitKnockbackImpulse = 0f;
        }

        [System.Serializable]
        public class CombatResolvedCaps
        {
            public float projectileSizeMin = 0.7f;
            public float projectileSizeMax = 5.0f;
            public float projectileSpeedMin = 0.4f;
            public float projectileSpeedMax = 3.0f;
            public float reloadTimeMin = 0.5f;
            public float fireIntervalMin = 0.05f;
            public float hitKnockbackImpulseMax = 4.0f;
            public float explosionRadiusMax = 30.0f;
            public float statusStunMax = 0.8f;
            public float statusSlowMax = 0.3f;
        }

        public enum SameProjectileHitPolicy
        {
            RequiresWorldCollisionReset = 0
        }

        public enum MultiProjectileStatusPolicy
        {
            ApplyPerProjectileHit = 0
        }

        public enum ExplosionStatusPolicy
        {
            ApplyOncePerProjectilePerTarget = 0
        }

        public enum KnockbackAggregationMode
        {
            VectorSumWithMagnitudeCap = 0
        }

        public enum DamageAggregationMode
        {
            SeparateChannelsAdditive = 0
        }

        public enum CollisionPriorityPolicy
        {
            TargetExplodeWallReflectFirst = 0
        }

        [System.Serializable]
        public class CombatCombinationRules
        {
            public SameProjectileHitPolicy sameProjectileSameTargetHitPolicy = SameProjectileHitPolicy.RequiresWorldCollisionReset;
            public float physicsSafetyCooldown = 0.02f;
            public MultiProjectileStatusPolicy statusApplicationFromMultiProjectilePolicy = MultiProjectileStatusPolicy.ApplyPerProjectileHit;
            public ExplosionStatusPolicy statusApplicationFromExplosionPolicy = ExplosionStatusPolicy.ApplyOncePerProjectilePerTarget;
            public KnockbackAggregationMode knockbackAggregationMode = KnockbackAggregationMode.VectorSumWithMagnitudeCap;
            public DamageAggregationMode damageAggregationMode = DamageAggregationMode.SeparateChannelsAdditive;
            public CollisionPriorityPolicy collisionPriorityPolicy = CollisionPriorityPolicy.TargetExplodeWallReflectFirst;
        }

        [System.Serializable]
        public class CombatSection
        {
            public CombatBaseline baseline = new CombatBaseline();
            public CombatResolvedCaps resolvedCaps = new CombatResolvedCaps();
            public CombatCombinationRules combinationRules = new CombatCombinationRules();
        }

        [System.Serializable]
        public class CardOfferSection
        {
            [Tooltip("한 번에 제시할 카드 수")]
            public int drawChoices = 3;
            [Tooltip("D46: 플레이어가 UI에서 보는 카드 선택 카운트다운 (초)")]
            public float visibleChoiceDuration = 10f;

            [Tooltip("D46: StateAuthority가 수락하는 추가 유예 시간 (초)")]
            public float serverAcceptPadding = 0.3f;

            [Tooltip("레거시 호환: 기존 런타임 timeout 값 (초)")]
            public float drawTimeoutSeconds = 8f;

            [Tooltip("같은 제시 내 중복 카드를 제외한다")]
            public bool excludeDuplicateChoicesInOffer = true;

            [Tooltip("maxStack 도달 카드를 후보에서 제외한다")]
            public bool excludeMaxStackReachedCards = true;

            [Tooltip("티어 cap 도달 카드를 후보에서 제외한다")]
            public bool excludeTierCapReachedCards = true;

            public ChoiceSeedSource choiceSeedSource = ChoiceSeedSource.NetworkedRandomBroadcast;
            public SameTickRacePolicy sameTickRacePolicy = SameTickRacePolicy.PlayerChoiceWins;
            public OfferFallbackPolicy offerFallbackPolicy = OfferFallbackPolicy.RelaxTierCapsThenRelaxRarity;
            public float commonWeight = 1.0f;
            public float rareWeight = 0.5f;
            public float epicWeight = 0.2f;
            public float legendaryWeight = 0.1f;
            public int epicOwnershipCap = 2;
            public int legendaryOwnershipCap = 1;
        }

        public enum ChoiceSeedSource
        {
            MatchSeedPlusRoundCounter = 0,
            NetworkedRandomBroadcast = 1
        }

        public enum SameTickRacePolicy
        {
            PlayerChoiceWins = 0
        }

        public enum OfferFallbackPolicy
        {
            RelaxTierCapsThenRelaxRarity = 0,
            PickFromAnyEligible = 1
        }

        public enum KnockbackBoundaryPolicy
        {
            SharedMovementPath = 0
        }

        [System.Serializable]
        public class SegmentSection
        {
            [Tooltip("Q40: 구간 돌파 판정 시 SegmentBoundary 기준 보정값")]
            public float segmentBoundaryPadding = 0f;

            [Tooltip("경계 근접 판정 여유값(월드 단위)")]
            public float boundaryJudgeEpsilon = 0.1f;

            [Tooltip("Q41: 넉백도 일반 이동과 동일한 경계 판정 경로를 사용")]
            public KnockbackBoundaryPolicy knockbackBoundaryPolicy = KnockbackBoundaryPolicy.SharedMovementPath;

            [Tooltip("Q45: 리스폰 직후 경계 재진입 판정 안정화용 유예(초)")]
            public float respawnBoundaryGraceSeconds = 0.2f;

            public float finalRunDurationSeconds = 0f;
        }

        [Header("GameplayProfile")]
        public Metadata metadata = new Metadata();
        public CombatSection combat = new CombatSection();
        public CardOfferSection cardOffer = new CardOfferSection();
        public SegmentSection segment = new SegmentSection();

        private void OnEnable()
        {
            EnsureSectionsNotNull();
        }

        private void OnValidate()
        {
            EnsureSectionsNotNull();
        }

        private void EnsureSectionsNotNull()
        {
            metadata ??= new Metadata();
            combat ??= new CombatSection();
            combat.baseline ??= new CombatBaseline();
            combat.resolvedCaps ??= new CombatResolvedCaps();
            combat.combinationRules ??= new CombatCombinationRules();
            cardOffer ??= new CardOfferSection();
            segment ??= new SegmentSection();
        }
    }
}
