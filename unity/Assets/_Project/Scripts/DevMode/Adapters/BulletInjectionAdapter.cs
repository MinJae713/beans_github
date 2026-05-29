#if UNITY_EDITOR
using UnityEngine;
using Fusion;
using Colosseum.Weapon;

namespace Colosseum.DevMode
{
    /// <summary>
    /// DevMode 전용 — 탄환 프리팹 override 어댑터 (Static cache 모델).
    ///
    /// 동작:
    ///   이 어댑터는 씬에 1개만 배치한다 (process 단일 어댑터).
    ///   OnEnable 시 Inspector 슬롯(_assetPack)에서 즉시 cache를 설정한다 (R13 race mitigation).
    ///   동시에 DevModeController.OnDevModeReady를 구독하고,
    ///   IsReady late-join 분기로 이미 DevMode가 준비된 경우에도 즉시 Apply를 호출한다.
    ///   OnDisable 시 static cache를 null로 정리한다 (R12 cleanup).
    ///
    /// R13 race mitigation:
    ///   Gun.Spawned()가 OnDevModeReady보다 먼저 실행되는 경우를 대비해
    ///   OnEnable에서 즉시 DevModeBulletOverride를 설정한다.
    ///   Gun.DevMode.cs의 OnDevModeSpawned()가 cache를 참조하므로 race 무관.
    ///
    /// R12 cleanup:
    ///   OnDisable에서 DevModeBulletOverride = null — process-wide static leak 방지.
    /// </summary>
    public class BulletInjectionAdapter : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        //  Static cache (process 단일 — Gun.DevMode.cs 에서 참조)
        // ──────────────────────────────────────────────

        /// <summary>
        /// DevMode에서 사용할 탄환 프리팹 override. null이면 기본값 유지.
        /// Gun.DevMode.cs의 OnDevModeSpawned()에서 참조한다.
        /// </summary>
        public static NetworkPrefabRef? DevModeBulletOverride => DevModeBulletOverrideStore.DevModeBulletOverride;

        // ──────────────────────────────────────────────
        //  Inspector 슬롯
        // ──────────────────────────────────────────────

        [SerializeField]
        [Tooltip("DevMode 에셋 팩 직접 참조. OnEnable에서 즉시 cache를 설정하기 위해 필요 (R13 race mitigation).")]
        private DevModeAssetPack _assetPack;

        // ──────────────────────────────────────────────
        //  생명주기
        // ──────────────────────────────────────────────

        // [A303-171] Visual-only projectile 전환 이후 NetworkObject 기반 bullet override 폐기.
        // 본 어댑터는 호환을 위해 컴포넌트 형태만 유지 — 모든 동작은 no-op.
        // DevMode bullet 커스터마이즈는 향후 BulletVisual prefab 슬롯으로 전환 예정.

        private void OnEnable()
        {
            // no-op
        }

        private void OnDisable()
        {
            // no-op
        }
    }
}
#endif
