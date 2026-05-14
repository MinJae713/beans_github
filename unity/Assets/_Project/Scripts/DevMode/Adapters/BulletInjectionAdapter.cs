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

        private void OnEnable()
        {
            // R13 race mitigation: OnDevModeReady 대기 없이 즉시 cache 설정.
            // Gun.Spawned()가 OnDevModeReady보다 먼저 실행되는 경우를 커버한다.
            if (_assetPack != null && _assetPack.bulletPrefab.IsValid)
                SetCache(_assetPack.bulletPrefab);

            // OnDevModeReady 구독 (씬 배치 어댑터 재구독 포함)
            DevModeController.OnDevModeReady += Apply;

            // IsReady late-join 분기: 이미 DevMode가 준비된 상태라면 즉시 적용
            if (DevModeController.Instance?.IsReady == true)
                Apply(DevModeController.Instance.AssetPack);
        }

        private void OnDisable()
        {
            DevModeController.OnDevModeReady -= Apply;

            // R12 cleanup: static cache 정리 — process-wide leak 방지
            DevModeBulletOverrideStore.Clear();
            Debug.Log("[Colosseum] Dev Mode: Bullet override cleared");
        }

        private void Apply(DevModeAssetPack pack)
        {
            if (pack == null)
            {
                Debug.LogWarning("[Colosseum] Dev Mode: BulletInjectionAdapter — AssetPack이 null입니다.", this);
                return;
            }

            if (!pack.bulletPrefab.IsValid)
            {
                Debug.LogWarning("[Colosseum] Dev Mode: BulletInjectionAdapter — bulletPrefab이 유효하지 않습니다. PrefabTable 등록 여부를 확인하세요.", this);
                return;
            }

            SetCache(pack.bulletPrefab);
        }

        private static void SetCache(NetworkPrefabRef bulletPrefab)
        {
            DevModeBulletOverrideStore.Set(bulletPrefab);
            Debug.Log($"[Colosseum] Dev Mode: Bullet override cached - {bulletPrefab}");
        }
    }
}
#endif
