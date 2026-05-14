using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using Colosseum.Bot;

namespace Colosseum.Network
{
    public class InputProvider : MonoBehaviour, INetworkRunnerCallbacks
    {
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            BotInputBridge bridge = GetComponent<BotInputBridge>();
            if (bridge != null && bridge.IsBotControlled)
            {
                input.Set(bridge.GetBotInput());
                return;
            }

            var data = new NetworkInputData
            {
                Direction = new Vector2(
                    Input.GetAxisRaw("Horizontal"),
                    Input.GetAxisRaw("Vertical")),
                IsJumpPressed = Input.GetKey(KeyCode.Space),
                IsFirePressed = Input.GetMouseButton(0),
                AimDirection = GetMouseWorldPosition()
            };

            input.Set(data);
        }

        private Vector2 GetMouseWorldPosition()
        {
            if (UnityEngine.Camera.main == null) return Vector2.zero;
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = Mathf.Abs(UnityEngine.Camera.main.transform.position.z);
            Vector3 mouseWorld = UnityEngine.Camera.main.ScreenToWorldPoint(mousePos);
            return new Vector2(mouseWorld.x, mouseWorld.y);
        }

        // INetworkRunnerCallbacks 빈 구현
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
