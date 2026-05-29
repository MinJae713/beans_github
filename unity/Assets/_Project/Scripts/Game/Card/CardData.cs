using UnityEngine;

namespace Colosseum.Card
{
    // enum 직렬화 값 호환성 유지: 기존 Common=0, Rare=1, Legendary=2를 보존
    public enum CardRarity { Common = 0, Rare = 1, Legendary = 2, Epic = 3 }
    public enum CardCategory
    {
        None,
        Projectile,
        Hit,
        Reload,
        Status,
        Trajectory,
        Explosion,
        Targeting,
        Fire,
        Recovery,
        Conditional,
        Stat,
        Aura,
        Area,
        Defense,
        Mobility
    }
    // enum 직렬화 값 호환성 유지: 기존 None=0, Freeze=1, Burn=2, Poison=3 보존
    public enum StatusEffectType { None = 0, Freeze = 1, Burn = 2, Poison = 3, Electric = 4 }
    public enum SpecialEffect { None, Explosive, Homing, Piercing }

    [CreateAssetMenu(fileName = "NewCard", menuName = "Colosseum/Card Data")]
    public class CardData : ScriptableObject
    {
        [Header("카드 정보")]
        public string cardName = "New Card";
        public int cardId = 0;
        public string cardKey = "";
        [Min(1)] public int maxStack = 1;
        public CardCategory category = CardCategory.None;
        [TextArea] public string description = "";
        public Sprite cardIcon;
        public CardRarity rarity = CardRarity.Common;
        public bool isDraftEnabled = true;

        [Header("총알 강화")]
        public float damageMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float sizeMultiplier = 1f;
        public int extraBounce = 0;
        public float gravityModifier = 0f;

        [Header("총 강화")]
        public float fireRateMultiplier = 1f;
        public float reloadSpeedMultiplier = 1f;
        public int extraBullets = 0;
        [Min(0)] public int extraAmmoCapacity = 0;
        public int extraPierce = 0;
        public float knockbackForce = 0f;

        [Header("플레이어 강화")]
        public float healAmount = 0f;
        public float lifestealPercent = 0f;
        public float healOnKill = 0f;
        public float moveSpeedMultiplier = 1f;
        public float jumpForceMultiplier = 1f;

        [Header("상태이상")]
        public StatusEffectType statusEffect = StatusEffectType.None;
        public float statusDuration = 0f;

        [Header("폭발")]
        public float explosionRadius = 0f;
        public float explosionRadiusMultiplier = 1f;
        public float explosionDamageRatio = 0.5f;

        [Header("과충전")]
        public float overchargeDamageMultiplier = 1f;

        [Header("특수 효과")]
        public SpecialEffect specialEffect = SpecialEffect.None;
        public float specialValue = 0f;
    }
}
