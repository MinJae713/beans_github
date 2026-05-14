#if UNITY_EDITOR
using Fusion;
using UnityEngine;

namespace Colosseum.DevMode
{
    /// <summary>
    /// DevMode 전용 — 플레이어 스폰 프리팹 override 어댑터.
    ///
    /// 동작:
    ///   OnEnable 시 DevModeController.OnDevModeReady를 구독한다.
    ///   IsReady late-join 분기: 이미 DevMode가 준비된 상태라면 즉시 Apply를 호출한다.
    ///   Apply 시 NetworkManager.SetDevModePlayerPrefab(pack.playerPrefab, devModeReady)을 호출해
    ///   플레이어 스폰 프리팹을 override한다.
    ///   OnDisable 시 원본 프리팹으로 원복한다 (R12 cleanup).
    ///
    /// 씬에 1개만 배치할 것 (단일 어댑터 원칙).
    /// NetworkManager.SetDevModePlayerPrefab은 Colosseum.DevMode friend assembly에서만 접근 가능하며,
    /// 내부 가드(R11)는 DevModeController readiness + ClientState.DevMode + Runner.IsServer이다.
    /// </summary>
    public class PlayerSpawnAdapter : MonoBehaviour
    {
        // R12: Apply 전 NetworkManager의 원본 playerPrefab 캐시
        private NetworkPrefabRef _originalPlayerPrefab;
        private bool _originalCached;
        private bool _receivedReadySignal;

        private void OnEnable()
        {
            DevModeController.OnDevModeReady += Apply;

            // IsReady late-join 분기: 이미 DevMode가 준비된 상태라면 즉시 적용
            if (DevModeController.Instance?.IsReady == true)
                Apply(DevModeController.Instance.AssetPack);
        }

        private void OnDisable()
        {
            DevModeController.OnDevModeReady -= Apply;

            // R12: 원본 프리팹으로 원복
            Revert();
        }

        private void Apply(DevModeAssetPack pack)
        {
            _receivedReadySignal = true;

            if (pack == null)
            {
                Debug.LogWarning("[Colosseum] Dev Mode: PlayerSpawnAdapter — AssetPack이 null입니다.", this);
                return;
            }

            if (!pack.playerPrefab.IsValid)
            {
                Debug.LogWarning("[Colosseum] Dev Mode: PlayerSpawnAdapter — pack.playerPrefab이 유효하지 않습니다. PrefabTable 등록을 확인하세요.", this);
                return;
            }

            var nm = Colosseum.Network.NetworkManager.Instance;
            if (nm == null)
            {
                Debug.LogWarning("[Colosseum] Dev Mode: PlayerSpawnAdapter — NetworkManager.Instance가 null입니다.", this);
                return;
            }

            // R12: 최초 Apply 전에 원본 캐시
            if (!_originalCached)
            {
                _originalPlayerPrefab = nm.GetDevModePlayerPrefab();
                _originalCached = true;
            }

            nm.SetDevModePlayerPrefab(pack.playerPrefab, HasDevModeReadySignal());
            Debug.Log("[Colosseum] Dev Mode: PlayerPrefab override applied");
        }

        private void Revert()
        {
            if (!_originalCached) return;

            var nm = Colosseum.Network.NetworkManager.Instance;
            if (nm == null) return;

            // Revert preserves the pre-split null guard: if the controller is gone,
            // do not mutate NetworkManager during teardown.
            nm.SetDevModePlayerPrefab(_originalPlayerPrefab, DevModeController.Instance != null);
            _originalCached = false;
            _receivedReadySignal = false;
            Debug.Log("[Colosseum] Dev Mode: PlayerPrefab override reverted");
        }

        private bool HasDevModeReadySignal()
        {
            // OnDevModeReady fires before DevModeController.IsReady is set, so
            // this adapter records the event signal as the readiness proof.
            return _receivedReadySignal || DevModeController.Instance?.IsReady == true;
        }
    }
}
#endif
