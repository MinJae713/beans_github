namespace Colosseum.Weapon
{
    /// <summary>
    /// [A303-171 단계 7a] 발사 무기의 상태이상 종류.
    /// 기존 `BulletController.StatusEffect` 의 nested enum 을 top-level 로 이전한 결과.
    /// CardData.StatusEffectType / Status 시스템 전반에서 같은 정렬 사용.
    /// </summary>
    public enum StatusEffect
    {
        None = 0,
        Freeze = 1,
        Burn = 2,
        Poison = 3,
        Electric = 4,
    }

    /// <summary>
    /// [A303-171 단계 7a] StatusEffect ↔ bit mask 변환 helper.
    /// 기존 `BulletController.ToStatusEffectMask` / `HasStatusEffect` 의 정확한 이전.
    /// extension method 라 `myStatus.ToMask()` / `myMask.HasStatus(status)` 형태 호출 가능.
    /// </summary>
    public static class StatusEffectExtensions
    {
        public static int ToMask(this StatusEffect status)
        {
            return status == StatusEffect.None ? 0 : 1 << ((int)status - 1);
        }

        public static bool HasStatus(this int mask, StatusEffect status)
        {
            int statusMask = status.ToMask();
            return statusMask != 0 && (mask & statusMask) != 0;
        }
    }
}
