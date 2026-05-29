#if UNITY_EDITOR
using UnityEngine;
using Fusion;
using Colosseum.Weapon;
using Colosseum.NetworkGameplay.Diagnostics;

namespace Colosseum.Status
{
    /// <summary>
    /// Editor 전용 디버그 트리거. Player.prefab에 부착하면 키보드로 자기 자신에게 상태이상 강제 발동.
    ///
    /// 키 매핑:
    ///   F1 = Burn      F2 = Poison
    ///   F3 = Frost     F4 = Electric (1회 클릭 = +1 스택, 임계 도달 시 stun)
    ///
    /// 사용 흐름 (DevMode):
    ///   1. Editor에서 Play → MainMenu → Dev Mode 진입
    ///   2. 자기 자신(InputAuthority)에서 키 누름
    ///   3. ApplyByEnum이 PlayerStatusController에 매핑된 SO를 조회해 효과 적용
    ///   4. Console에 "[Colosseum] StatusEffect applied: ..." 로그 확인
    ///   5. HP 감소 / 이동 둔화 / 입력 잠금 등 직접 체감
    ///
    /// production 빌드에는 #if UNITY_EDITOR 가드로 컴파일 자체에서 제외.
    /// </summary>
    [RequireComponent(typeof(PlayerStatusController))]
    public class StatusEffectDevTrigger : MonoBehaviour
    {
        private PlayerStatusController _controller;
        private NetworkObject _netObj;

        private void Awake()
        {
            _controller = GetComponent<PlayerStatusController>();
            _netObj = GetComponent<NetworkObject>();
        }

        private void Update()
        {
            if (_controller == null || _netObj == null) return;

            // InputAuthority 측에서만 키 입력 수용 — 다른 플레이어 캐릭터에 적용되는 사고 방지.
            if (!_netObj.HasInputAuthority) return;

            // DevMode는 Host = SA + IA 단독이라 직접 ApplyByEnum 호출 가능.
            // 일반 매칭에서는 SA만 Apply 가능하므로, IA가 SA에게 RPC로 요청하는 흐름이 정공법이지만
            // 본 컴포넌트는 Editor 전용 검증용이라 RPC 없이 직접 호출 (DevMode Host = SA 가정).
            if (Input.GetKeyDown(KeyCode.F1))
            {
                DiagLog.Log("[Colosseum/DEV] Trigger Burn on self");
                _controller.ApplyByEnum(StatusEffect.Burn, _netObj.InputAuthority);
            }
            else if (Input.GetKeyDown(KeyCode.F2))
            {
                DiagLog.Log("[Colosseum/DEV] Trigger Poison on self");
                _controller.ApplyByEnum(StatusEffect.Poison, _netObj.InputAuthority);
            }
            else if (Input.GetKeyDown(KeyCode.F3))
            {
                DiagLog.Log("[Colosseum/DEV] Trigger Frost on self (enum 이름은 Freeze, SO는 FrostEffect — 같은 효과)");
                _controller.ApplyByEnum(StatusEffect.Freeze, _netObj.InputAuthority);
            }
            else if (Input.GetKeyDown(KeyCode.F4))
            {
                DiagLog.Log("[Colosseum/DEV] Trigger Electric +1 stack on self");
                _controller.ApplyByEnum(StatusEffect.Electric, _netObj.InputAuthority);
            }
        }
    }
}
#endif
