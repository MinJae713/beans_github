#if UNITY_EDITOR
namespace Colosseum.Weapon
{
    /// <summary>
    /// Gun partial class — DevMode 전용 hook.
    ///
    /// [A303-171] Visual-only projectile 전환 이후 NetworkObject 기반 _bulletPrefab 이
    /// Gun.cs 에서 폐기됨 → DevMode bullet override 도 의미를 잃음.
    /// DevMode bullet 커스터마이즈는 BulletVisual prefab 슬롯 (_bulletVisualPrefab) 으로 전환 예정.
    /// 본 partial 은 호환을 위해 no-op 으로만 유지 — production zero overhead.
    /// </summary>
    public partial class Gun
    {
        partial void OnDevModeSpawned()
        {
            // [A303-171] no-op. NetworkObject 기반 bullet override 폐기.
        }
    }
}
#endif
