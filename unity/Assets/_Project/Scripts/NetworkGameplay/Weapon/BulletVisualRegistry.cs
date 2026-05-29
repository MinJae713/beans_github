using System.Collections.Generic;
using UnityEngine;

namespace Colosseum.Weapon
{
    /// <summary>
    /// [A303-171] bulletId → BulletVisual instance 매핑 registry.
    ///
    /// Gun.cs 의 Rpc_FireBullet 이 발사한 BulletVisual 인스턴스를 본 registry 에 등록하고,
    /// 후속 Rpc_BulletHit 가 bulletId 로 해당 인스턴스를 찾아 OnServerHit 호출하는 데 사용.
    ///
    /// static class 라 instance 관리 단순. scene 전환 / NetworkRunner shutdown 시 외부에서
    /// Clear() 호출 필요 (단계 8 통합 시점에 NetworkManager 연동).
    /// </summary>
    public static class BulletVisualRegistry
    {
        private static readonly Dictionary<int, BulletVisual> _instances = new Dictionary<int, BulletVisual>();

        /// <summary>
        /// BulletVisual.Init 끝부분에서 호출. bulletId 가 중복이면 이전 instance 를 덮어쓴다
        /// (정상 흐름에선 _nextBulletId 가 monotonic 증가라 충돌 없음).
        /// </summary>
        public static void Register(int bulletId, BulletVisual visual)
        {
            if (visual == null) return;
            _instances[bulletId] = visual;
        }

        /// <summary>
        /// BulletVisual.OnDestroy 에서 호출. 등록 안 된 id 면 silent.
        /// </summary>
        public static void Unregister(int bulletId)
        {
            _instances.Remove(bulletId);
        }

        /// <summary>
        /// Rpc_BulletHit 에서 bulletId 로 instance 조회. 없으면 false (수명 만료 등으로 이미 destroy 된 케이스).
        /// </summary>
        public static bool TryGet(int bulletId, out BulletVisual visual)
        {
            return _instances.TryGetValue(bulletId, out visual);
        }

        /// <summary>
        /// scene 전환 / runner shutdown 시 외부에서 호출. 잔존 instance 직접 destroy 는 안 함
        /// (호출자가 GameObject lifecycle 따로 책임).
        /// </summary>
        public static void Clear()
        {
            _instances.Clear();
        }

        /// <summary>
        /// 디버그용 등록 인스턴스 수.
        /// </summary>
        public static int Count => _instances.Count;
    }
}
