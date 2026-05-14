using Colosseum.Network;
using UnityEngine;

namespace Colosseum.Bot
{
    /// <summary>
    /// PlayerController(또는 InputProvider) 와 DummyBotFSM 사이의 브리지.
    ///
    /// 통합 방법:
    ///   1. 봇으로 사용할 PlayerController GameObject에 이 컴포넌트를 추가한다.
    ///   2. [SerializeField] bot 슬롯에 DummyBotFSM 컴포넌트를 연결한다.
    ///   3. InputProvider.OnInput 콜백에서 IsBotControlled 를 확인한 뒤
    ///      GetBotInput() 결과를 input.Set()으로 전달한다.
    ///
    /// 예시 (통합 단계에서 메인 컨텍스트가 InputProvider에 추가):
    /// <code>
    ///   void OnInput(NetworkRunner runner, NetworkInput input)
    ///   {
    ///       var bridge = runner.GetPlayerObject(runner.LocalPlayer)
    ///                         ?.GetComponent&lt;BotInputBridge&gt;();
    ///       if (bridge != null &amp;&amp; bridge.IsBotControlled)
    ///       {
    ///           input.Set(bridge.GetBotInput());
    ///           return;
    ///       }
    ///       // 기존 사람 입력 처리 …
    ///   }
    /// </code>
    /// </summary>
    public class BotInputBridge : MonoBehaviour
    {
        [SerializeField] private DummyBotFSM _bot;

        /// <summary>이 플레이어 오브젝트가 봇 제어 중인지 여부.</summary>
        public bool IsBotControlled => _bot != null && _bot.enabled;

        /// <summary>
        /// 봇 FSM에서 다음 NetworkInputData를 가져온다.
        /// IsBotControlled == false 이면 빈 입력(zero)을 반환한다.
        /// </summary>
        public NetworkInputData GetBotInput()
        {
            if (!IsBotControlled)
                return new NetworkInputData();

            return _bot.GetNextInput();
        }

        /// <summary>런타임에서 FSM 타깃을 동적으로 교체한다.</summary>
        public void SetBot(DummyBotFSM bot) => _bot = bot;
    }
}
