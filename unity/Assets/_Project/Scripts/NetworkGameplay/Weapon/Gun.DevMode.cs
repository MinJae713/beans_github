#if UNITY_EDITOR
namespace Colosseum.Weapon
{
    /// <summary>
    /// Gun partial class — DevMode 전용 hook.
    ///
    /// Gun.cs의 Spawned()가 OnDevModeSpawned()를 호출하면
    /// DevModeBulletOverrideStore cache를 확인해
    /// _bulletPrefab을 DevMode override로 교체한다.
    ///
    /// partial void 메커니즘:
    ///   #if UNITY_EDITOR 가드 외부 (production build)에서는
    ///   구현이 없으므로 컴파일러가 호출문을 자동 제거 — production zero overhead.
    /// </summary>
    public partial class Gun
    {
        partial void OnDevModeSpawned()
        {
            if (DevModeBulletOverrideStore.DevModeBulletOverride.HasValue)
                _bulletPrefab = DevModeBulletOverrideStore.DevModeBulletOverride.Value;
        }
    }
}
#endif
