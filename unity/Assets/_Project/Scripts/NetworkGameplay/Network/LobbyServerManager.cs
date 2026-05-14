using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;

namespace Colosseum.Network
{
    /// <summary>
    /// 로비 전용 서버 프로세스.
    /// 로비 Runner 1개만 운영하며, 방 생성 요청 시 Worker Agent 계층을 통해
    /// 별도 GameServer 프로세스를 실행한다.
    ///
    /// 현 단계의 Worker Agent는 같은 머신에서 프로세스를 띄우는 하드코딩 구현이지만,
    /// Lobby는 더 이상 직접 Process.Start()를 호출하지 않는다.
    ///
    /// 실행: Server.exe --mode lobby
    /// </summary>
    public class LobbyServerManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Lobby Settings")]
        [SerializeField] private string _lobbySessionName = "ColosseumLobby";
        [SerializeField] private int _lobbyMaxPlayers = 20;
        [SerializeField] private int _roomMaxPlayers = 2;

        [Header("Worker Agent")]
        [SerializeField] private string _workerAgentBaseUrl = "http://127.0.0.1:8080";
        
        [Header("Room Registry API")]
        [SerializeField] private string _roomApiBaseUrl = "http://127.0.0.1:8081/api/v1";

        private NetworkRunner _lobbyRunner;
        private int _reliableKeyCounter = 0;

        // ── 로비 접속 플레이어 닉네임 ──
        // 클라이언트가 로비 접속 직후 NICKNAME:이름 메시지로 등록.
        private Dictionary<PlayerRef, string> _lobbyNicknames = new();
        private IWorkerSelector _workerSelector;
        private HttpWorkerAgentClient _workerAgentClient;

        public void Initialize()
        {
            UnityEngine.Debug.Log("[Colosseum Lobby] Initializing Lobby Server...");
            string envWorkerBaseUrl = Environment.GetEnvironmentVariable("COLOSSEUM_WORKER_BASE_URL");
            if (!string.IsNullOrWhiteSpace(envWorkerBaseUrl))
            {
                _workerAgentBaseUrl = envWorkerBaseUrl.Trim();
            }

            string envLobbySession = Environment.GetEnvironmentVariable("COLOSSEUM_LOBBY_SESSION");
            if (!string.IsNullOrWhiteSpace(envLobbySession))
            {
                _lobbySessionName = envLobbySession.Trim();
            }

            string envRoomApiBaseUrl = Environment.GetEnvironmentVariable("COLOSSEUM_ROOM_API_BASE_URL");
            if (!string.IsNullOrWhiteSpace(envRoomApiBaseUrl))
            {
                _roomApiBaseUrl = envRoomApiBaseUrl.Trim();
            }

            Debug.Log($"[Colosseum Lobby] Lobby session = {_lobbySessionName}");
            Debug.Log($"[Colosseum Lobby] Worker base URL = {_workerAgentBaseUrl}");
            Debug.Log($"[Colosseum Lobby] Room API base URL = {_roomApiBaseUrl}");
            _workerSelector = new FixedWorkerSelector(baseUrl: _workerAgentBaseUrl);
            _workerAgentClient = new HttpWorkerAgentClient(
                _roomApiBaseUrl,
                message => UnityEngine.Debug.Log(message),
                message => UnityEngine.Debug.LogError(message));
            StartLobby();
        }

        private void OnDestroy()
        {
            _workerAgentClient?.ShutdownAll();
        }

        // ──────────────────────────────────────────────
        //  로비 시작
        // ──────────────────────────────────────────────

        private async void StartLobby()
        {
            var lobbyObj = new GameObject("LobbyRunner");
            DontDestroyOnLoad(lobbyObj);
            _lobbyRunner = lobbyObj.AddComponent<NetworkRunner>();
            _lobbyRunner.ProvideInput = false;
            _lobbyRunner.AddCallbacks(this);

            var result = await _lobbyRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Server,
                SessionName = _lobbySessionName,
                PlayerCount = _lobbyMaxPlayers,
                // STUN 프로브 스킵용 placeholder. Relay 아키텍처라 실제 주소값은 사용되지 않음. EC2 Dedicated Server 도입 시 실제 공인 주소로 교체.
                CustomPublicAddress = NetAddress.CreateFromIpPort("127.0.0.1", 0),
            });

            if (result.Ok)
                UnityEngine.Debug.Log($"[Colosseum Lobby] Lobby '{_lobbySessionName}' started!");
            else
                UnityEngine.Debug.LogError($"[Colosseum Lobby] Lobby failed: {result.ShutdownReason}");
        }

        // ──────────────────────────────────────────────
        //  게임 서버 프로세스 스폰
        // ──────────────────────────────────────────────

        private async void SpawnGameServer(string roomName, PlayerRef creator)
        {
            string sessionName = $"Room_{roomName}_{Guid.NewGuid().ToString().Substring(0, 6)}";
            WorkerInfo worker = _workerSelector?.SelectWorker();
            if (worker == null)
            {
                SendToLobbyPlayer(creator, "ERROR:NO_AVAILABLE_WORKER");
                return;
            }

            string hostNickname = _lobbyNicknames.TryGetValue(creator, out var nick) ? nick : string.Empty;
            var response = await _workerAgentClient.SpawnRoomAsync(worker, new SpawnRoomRequest
            {
                SessionName = sessionName,
                RoomName = roomName,
                HostNickname = hostNickname,
                MaxPlayers = _roomMaxPlayers
            });

            if (response != null && response.Ok)
            {
                SendToLobbyPlayer(creator, $"ROOM_CREATED:{sessionName}");
                Invoke(nameof(RefreshAndBroadcastRoomList), 1.5f);
                return;
            }

            string errorCode = response?.ErrorCode ?? "ROOM_CREATE_FAILED";
            SendToLobbyPlayer(creator, $"ERROR:{errorCode}");
        }

        private void RefreshAndBroadcastRoomList()
        {
            _ = BroadcastRoomListToLobbyAsync();
        }

        // ──────────────────────────────────────────────
        //  메시지 처리
        // ──────────────────────────────────────────────

        private void HandleLobbyMessage(PlayerRef player, string message)
        {
            UnityEngine.Debug.Log($"[Colosseum Lobby] Message from {player}: {message}");

            if (message.StartsWith("NICKNAME:"))
            {
                string nickname = message.Substring(9);
                var result = NicknameValidator.Validate(nickname);
                if (result != NicknameValidator.Result.Ok)
                {
                    SendToLobbyPlayer(player, $"ERROR:{NicknameValidator.ToErrorCode(result)}");
                    UnityEngine.Debug.LogWarning($"[Colosseum Lobby] Nickname rejected from {player}: {result}");
                    return;
                }
                _lobbyNicknames[player] = nickname;
                UnityEngine.Debug.Log($"[Colosseum Lobby] Nickname registered: {player} -> '{nickname}'");
            }
            else if (message.StartsWith("CREATE_ROOM:"))
            {
                string roomName = message.Substring(12).Trim();
                if (string.IsNullOrEmpty(roomName))
                {
                    SendToLobbyPlayer(player, "ERROR:room_name_empty");
                    return;
                }
                if (roomName.Length > 24)
                {
                    SendToLobbyPlayer(player, "ERROR:room_name_too_long");
                    return;
                }
                // 닉네임 미등록 상태에서 방 생성 거절
                if (!_lobbyNicknames.ContainsKey(player))
                {
                    SendToLobbyPlayer(player, "ERROR:nickname_not_set");
                    UnityEngine.Debug.LogWarning($"[Colosseum Lobby] CREATE_ROOM rejected: {player} has no nickname");
                    return;
                }
                SpawnGameServer(roomName, player);
            }
            else if (message == "LIST_ROOMS")
            {
                SendRoomList(player);
            }
        }

        // ──────────────────────────────────────────────
        //  메시지 전송
        // ──────────────────────────────────────────────

        private ReliableKey NextReliableKey()
        {
            _reliableKeyCounter++;
            return ReliableKey.FromInts(_reliableKeyCounter, 0, 0, 0);
        }

        private void SendToLobbyPlayer(PlayerRef player, string message)
        {
            if (_lobbyRunner == null) return;
            byte[] data = Encoding.UTF8.GetBytes(message);
            _lobbyRunner.SendReliableDataToPlayer(player, NextReliableKey(), data);
        }

        private async void SendRoomList(PlayerRef player)
        {
            string json = await _workerAgentClient.FetchRoomListJsonAsync();
            SendToLobbyPlayer(player, $"ROOM_LIST:{json}");
        }

        private async System.Threading.Tasks.Task BroadcastRoomListToLobbyAsync()
        {
            if (_lobbyRunner == null) return;

            string json = await _workerAgentClient.FetchRoomListJsonAsync();
            string message = $"ROOM_LIST:{json}";
            byte[] data = Encoding.UTF8.GetBytes(message);

            foreach (var player in _lobbyRunner.ActivePlayers)
            {
                _lobbyRunner.SendReliableDataToPlayer(player, NextReliableKey(), data);
            }
        }

        // ──────────────────────────────────────────────
        //  INetworkRunnerCallbacks
        // ──────────────────────────────────────────────

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            UnityEngine.Debug.Log($"[Colosseum Lobby] Player {player} joined lobby");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            UnityEngine.Debug.Log($"[Colosseum Lobby] Player {player} left lobby");
            _lobbyNicknames.Remove(player);
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player,
            ReliableKey key, ArraySegment<byte> data)
        {
            string message = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            HandleLobbyMessage(player, message);
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            UnityEngine.Debug.Log($"[Colosseum Lobby] Lobby shutdown: {shutdownReason}");
            _lobbyRunner = null;
        }

        // ── 사용하지 않는 콜백 ──
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
