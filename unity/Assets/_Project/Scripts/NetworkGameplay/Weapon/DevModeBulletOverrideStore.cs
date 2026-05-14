#if UNITY_EDITOR
using Fusion;

namespace Colosseum.Weapon
{
    /// <summary>
    /// Editor DevMode bridge kept in NetworkGameplay so Gun does not depend on the DevMode assembly.
    /// </summary>
    public static class DevModeBulletOverrideStore
    {
        public static NetworkPrefabRef? DevModeBulletOverride { get; private set; }

        public static void Set(NetworkPrefabRef bulletPrefab)
        {
            DevModeBulletOverride = bulletPrefab;
        }

        public static void Clear()
        {
            DevModeBulletOverride = null;
        }
    }
}
#endif
