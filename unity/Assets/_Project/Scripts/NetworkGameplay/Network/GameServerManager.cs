using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Colosseum.Contracts;
using Colosseum.Player;
using Colosseum.NetworkGameplay.Diagnostics;
using Fusion;
using Fusion.Sockets;
using Fusion.Addons.Physics;

namespace Colosseum.Network
{
    /// <summary>
    /// 단일 게임 방 전용 서버 프로세스.
    /// 프로세스 1개 = 게임 방 1개. Fusion Runner도 1개만 운영.
    ///
    /// CLI 인자:
    ///   --mode game --session "Room_xxx" --room-name "방이름" --max-players 2 --scene "MVPArena"
    ///
    /// 방이 비면(모든 플레이어 퇴장) 자체 프로세스 종료.
    /// </summary>
    public class GameServerManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Game Scene")]
        [SerializeField] private string _gameSceneName = SelectableSceneExtensions.DefaultSceneName;

        [Header("Player")]
        [SerializeField] private NetworkPrefabRef _playerPrefab;

        // AddComponent()로 동적 생성 시 SerializeField가 할당되지 않으므로
        // Resources 폴더에서 런타임 로딩한 프리팹을 보관
        private NetworkObject _playerPrefabFallback;

        // ── CLI에서 전달받는 설정 ──
        private string _sessionName;
        private string _roomName;
        private int _maxPlayers = 2;

        // ── 런타임 상태 ──
        private NetworkRunner _gameRunner;
        private int _reliableKeyCounter = 0;
        private int _playerCount;
        private int _readyCount;
        private string _status = "waiting";  // waiting, countdown, playing, gameover
        private float _countdownTimer;
        private int _lastCountdownSecond = -1;
        private float _gameOverTimer;
        private Dictionary<PlayerRef, bool> _playerReady = new();
        private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();
        private System.Diagnostics.Stopwatch _initializeSw;
        private System.Diagnostics.Stopwatch _startGameRunnerSw;
        private System.Diagnostics.Stopwatch _startGameCallSw;
        // PlayerRef ↔ 닉네임. 클라가 방 입장 직후 NICKNAME:이름 으로 등록.
        private Dictionary<PlayerRef, string> _nicknames = new();
        private readonly HashSet<PlayerRef> _pendingDisconnect = new();

        public void Initialize(string sessionName, string roomName, int maxPlayers, string sceneName = SelectableSceneExtensions.DefaultSceneName)
        {
            _initializeSw = System.Diagnostics.Stopwatch.StartNew();
            _sessionName = sessionName;
            _roomName = roomName;
            _maxPlayers = maxPlayers;
            _gameSceneName = sceneName;
            var initializeEnteredAt = DateTimeOffset.UtcNow;
            DiagLog.Log($"[Colosseum-Timing][GameServer - Initialize Enter][GT1] room='{_roomName}' session='{_sessionName}' scene='{_gameSceneName}' at='{initializeEnteredAt:O}'");

            DiagLog.Log($"[Colosseum Game] Initializing Game Server - Room: {_roomName}, Session: {_sessionName}, Scene: {_gameSceneName}");

            // SerializeField가 비어있으면 Resources에서 로드 (AddComponent 동적 생성 대응)
            if (!_playerPrefab.IsValid)
            {
                _playerPrefabFallback = Resources.Load<NetworkObject>("Player");
                if (_playerPrefabFallback != null)
                    DiagLog.Log("[Colosseum Game] Player prefab loaded from Resources (runtime fallback)");
                else
                    Debug.LogError("[Colosseum Game] Player prefab not found in Resources/Player!");
            }

            // RoomManager 게임 오버 이벤트 구독
            Colosseum.Game.RoomManager.OnGameOverEvent += OnRoomGameOver;

            // 로비 서버에 초기 상태 보고
            Console.WriteLine("##STATUS:waiting");

            StartGameRunner();
        }

        private void OnDestroy()
        {
            Colosseum.Game.RoomManager.OnGameOverEvent -= OnRoomGameOver;
        }

        private void Update()
        {
            // 카운트다운 처리
            if (_status == "countdown")
            {
                _countdownTimer -= Time.deltaTime;

                int seconds = Mathf.CeilToInt(_countdownTimer);
                if (seconds != _lastCountdownSecond && seconds >= 0)
                {
                    _lastCountdownSecond = seconds;
                    BroadcastToAll($"COUNTDOWN:{seconds}");
                }

                if (_countdownTimer <= 0f)
                {
                    _lastCountdownSecond = -1;
                    StartGame();
                }
            }

            // 게임 오버 후 자동 리셋 (5초 대기)
            if (_status == "gameover")
            {
                _gameOverTimer -= Time.deltaTime;
                if (_gameOverTimer <= 0f)
                {
                    ResetRoom();
                }
            }

        }

        // ──────────────────────────────────────────────
        //  게임 러너 시작
        // ──────────────────────────────────────────────

        private async void StartGameRunner()
        {
            var startGameRunnerEnteredAt = DateTimeOffset.UtcNow;
            DiagLog.Log($"[Colosseum-Timing][GameServer - StartGameRunner Enter][GT2] room='{_roomName}' session='{_sessionName}' at='{startGameRunnerEnteredAt:O}' elapsed_from_gt1_ms={_initializeSw?.ElapsedMilliseconds ?? 0}");
            _startGameRunnerSw = System.Diagnostics.Stopwatch.StartNew();

            var runnerObj = new GameObject("GameRunner");
            DontDestroyOnLoad(runnerObj);
            _gameRunner = runnerObj.AddComponent<NetworkRunner>();
            _gameRunner.ProvideInput = false;
            _gameRunner.AddCallbacks(this);

            // Fusion 씬 관리자
            runnerObj.AddComponent<NetworkSceneManagerDefault>();
            ConfigureRunnerPhysics2D(runnerObj);

            // 게임 씬 참조
            var sceneInfo = new NetworkSceneInfo();
            string scenePath = SelectableSceneExtensions.ToBuildScenePath(_gameSceneName);
            var sceneIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
            if (sceneIndex >= 0)
            {
                sceneInfo.AddSceneRef(SceneRef.FromIndex(sceneIndex));
            }
            else
            {
                Debug.LogError($"[Colosseum Game] Scene '{_gameSceneName}' resolved to '{scenePath}' but is not registered in Build Settings.");
            }

            var startGameCallAt = DateTimeOffset.UtcNow;
            _startGameCallSw = System.Diagnostics.Stopwatch.StartNew();
            DiagLog.Log($"[Colosseum-Timing][GameServer - StartGame Call][GT3] room='{_roomName}' session='{_sessionName}' at='{startGameCallAt:O}' elapsed_from_gt2_ms={_startGameRunnerSw?.ElapsedMilliseconds ?? 0}");
            var result = await _gameRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Server,
                SessionName = _sessionName,
                PlayerCount = _maxPlayers,
                Scene = sceneInfo,
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    { "roomName", _roomName },
                    { "status", "waiting" }
                },
                // STUN 프로브 스킵용 placeholder. Relay 아키텍처라 실제 주소값은 사용되지 않음. EC2 Dedicated Server 도입 시 실제 공인 주소로 교체.
                CustomPublicAddress = NetAddress.CreateFromIpPort("127.0.0.1", 0),
            });
            _startGameCallSw?.Stop();
            _startGameRunnerSw?.Stop();
            var startGameReturnedAt = DateTimeOffset.UtcNow;
            DiagLog.Log($"[Colosseum-Timing][GameServer - StartGame Returned][GT4] room='{_roomName}' session='{_sessionName}' ok={result.Ok} reason='{result.ShutdownReason}' at='{startGameReturnedAt:O}' elapsed_from_gt3_ms={_startGameCallSw?.ElapsedMilliseconds ?? 0}");

            if (result.Ok)
            {
                // Worker Agent가 이 마커를 받으면 실제 Fusion 세션이 열린 것으로 보고
                // Lobby에 ROOM_CREATED 응답을 반환한다.
                var readySignalAt = DateTimeOffset.UtcNow;
                _initializeSw?.Stop();
                DiagLog.Log($"[Colosseum-Timing][GameServer - Ready Signal][GT5] room='{_roomName}' session='{_sessionName}' at='{readySignalAt:O}' elapsed_from_gt1_ms={_initializeSw?.ElapsedMilliseconds ?? 0}");
                Console.WriteLine("##READY:session_open");
                DiagLog.Log($"[Colosseum Game] Room '{_roomName}' started (session: {_sessionName})");
            }
            else
            {
                Debug.LogError($"[Colosseum Game] Room creation failed: {result.ShutdownReason}");
                ShutdownAndQuit();
            }
        }

        private static void ConfigureRunnerPhysics2D(GameObject runnerObj)
        {
            var physics2D = runnerObj.AddComponent<RunnerSimulatePhysics2D>();
            physics2D.ClientPhysicsSimulation = ClientPhysicsSimulation.Disabled;
        }

        // ──────────────────────────────────────────────
        //  게임 시작
        // ──────────────────────────────────────────────

        private void StartGame()
        {
            _status = "playing";
            BroadcastToAll("GAME_START");
            DiagLog.Log($"[Colosseum Game] Room '{_roomName}' game started!");

            bool hasPrefabRef = _playerPrefab.IsValid;
            bool hasFallback = _playerPrefabFallback != null;

            if (!hasPrefabRef && !hasFallback)
            {
                Debug.LogError("[Colosseum Game] _playerPrefab is not set and fallback not loaded!");
            }
            else
            {
                foreach (var player in _playerReady.Keys.ToList())
                {
                    try
                    {
                        if (_spawnedPlayers.ContainsKey(player) && _spawnedPlayers[player] != null)
                        {
                            DespawnPlayer(player);
                        }
                        SpawnPlayer(player);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Colosseum Game] Failed to spawn player {player}: {e.Message}");
                    }
                }
            }

            UpdateSessionProperty("status", "playing");
        }

        // ──────────────────────────────────────────────
        //  게임 오버 + 리셋
        // ──────────────────────────────────────────────

        private void OnRoomGameOver(NetworkRunner runner, string winnerName)
        {
            // 이 프로세스에는 Runner가 1개뿐이므로 runner 검증은 간단
            // runner가 null일 수 있음: RoomManager에 NetworkObject가 없으면 Runner 속성이 null
            if (runner != null && runner != _gameRunner) return;
            if (_status != "playing") return;

            _status = "gameover";
            _gameOverTimer = 5f;

            BroadcastToAll($"GAME_OVER:{winnerName}");
            DiagLog.Log($"[Colosseum Game] Room '{_roomName}' game over! Winner: {winnerName}");
            UpdateSessionProperty("status", "gameover");
        }

        private void ResetRoom()
        {
            DiagLog.Log($"[Colosseum Game] Resetting room '{_roomName}' for rematch");

            // 플레이어 디스폰. 딕셔너리 누락/late spawn race가 있어도 씬의 PlayerHealth까지 훑어 승자 오브젝트가 맵에 남지 않게 한다.
            DespawnAllSpawnedPlayerObjects();

            // RoomManager 초기화.
            // SceneManager.GetSceneByName(_gameSceneName)은 Fusion NetworkSceneManager가
            // NetworkSceneInfo로 로드한 씬 이름과 일치하지 않거나 Scene.IsValid()가 false를
            // 반환해서 SceneSearch.FindInScene이 null을 돌려주는 경우가 있다. (실측 재현됨.)
            // 로드된 모든 씬을 순회해 방탄으로 찾는다. 못 찾으면 명시 에러 로그.
            var roomManager = FindRoomManagerInLoadedScenes();
            if (roomManager != null)
            {
                roomManager.ResetGame();
            }
            else
            {
                Debug.LogError(
                    $"[Colosseum Game] ResetRoom: RoomManager not found in any loaded scene! " +
                    $"Rematch state will NOT reset (camera/indicator will remain on end-room).");
            }

            // 상태 리셋
            _status = "waiting";
            _readyCount = 0;
            foreach (var key in _playerReady.Keys.ToList())
                _playerReady[key] = false;

            UpdateSessionProperty("status", "waiting");

            BroadcastToAll($"ROOM_RESET:{_playerCount}/{_maxPlayers}");
            BroadcastToAll($"READY_UPDATE:0/{_playerCount}");
        }

        /// <summary>
        /// Fusion이 로드한 씬 이름은 SceneManager.GetSceneByName으로 못 잡히는 경우가 있다.
        /// (NetworkSceneManager 내부 이름 매핑 / 비동기 로드 상태 이슈.)
        /// 로드된 모든 씬을 순회해 RoomManager를 찾는다.
        /// Spawn된 1개만 존재한다고 가정 — 여러 개면 첫 번째 반환.
        /// </summary>
        private Game.RoomManager FindRoomManagerInLoadedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.IsValid() || !s.isLoaded) continue;

                var rm = Utils.SceneSearch.FindInScene<Game.RoomManager>(s);
                if (rm != null) return rm;
            }
            return null;
        }

        // ──────────────────────────────────────────────
        //  플레이어 스폰
        // ──────────────────────────────────────────────

        private void SpawnPlayer(PlayerRef player)
        {
            if (_gameRunner == null) return;

            var roomManager = FindRoomManagerInLoadedScenes();
            bool isFirstPlayer = _spawnedPlayers.Count == 0;
            Vector3 spawnPos = roomManager != null
                ? roomManager.GetInitialSpawnPosition(isFirstPlayer)
                : Game.RoomManager.CalculateFallbackInitialSpawnPosition(isFirstPlayer);

            // SerializeField가 설정되어 있으면 NetworkPrefabRef 사용, 아니면 fallback NetworkObject 사용
            NetworkObject playerObj;
            if (_playerPrefab.IsValid)
                playerObj = _gameRunner.Spawn(_playerPrefab, spawnPos, Quaternion.identity, player);
            else
                playerObj = _gameRunner.Spawn(_playerPrefabFallback, spawnPos, Quaternion.identity, player);

            if (playerObj != null)
            {
                _spawnedPlayers[player] = playerObj;
                DiagLog.Log($"[Colosseum Game] Spawned player {player} at {spawnPos}");

                if (roomManager != null)
                {
                    if (_spawnedPlayers.Count == 1)
                        roomManager.Player1 = player;
                    else if (_spawnedPlayers.Count == 2)
                        roomManager.Player2 = player;
                }
            }
            else
            {
                Debug.LogError($"[Colosseum Game] Failed to spawn player {player}");
            }
        }

        private void DespawnPlayer(PlayerRef player)
        {
            if (_spawnedPlayers.TryGetValue(player, out NetworkObject playerObj))
            {
                if (playerObj != null && _gameRunner != null)
                    _gameRunner.Despawn(playerObj);
                _spawnedPlayers.Remove(player);
            }
        }

        private void DespawnAllSpawnedPlayerObjects()
        {
            if (_gameRunner == null)
            {
                _spawnedPlayers.Clear();
                return;
            }

            var despawned = new HashSet<NetworkObject>();
            foreach (var playerObj in _spawnedPlayers.Values)
            {
                TryDespawnPlayerObject(playerObj, despawned);
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) continue;

                foreach (var health in Utils.SceneSearch.FindAllInScene<PlayerHealth>(scene))
                {
                    if (health == null) continue;
                    TryDespawnPlayerObject(health.GetComponent<NetworkObject>(), despawned);
                }
            }

            _spawnedPlayers.Clear();
        }

        private void TryDespawnPlayerObject(NetworkObject playerObj, HashSet<NetworkObject> despawned)
        {
            if (playerObj == null || _gameRunner == null || despawned == null || despawned.Contains(playerObj))
            {
                return;
            }

            despawned.Add(playerObj);
            if (playerObj.IsValid)
            {
                _gameRunner.Despawn(playerObj);
            }
        }

        // ──────────────────────────────────────────────
        //  메시지 처리
        // ──────────────────────────────────────────────

        private void HandleMessage(PlayerRef player, string message)
        {
            if (_pendingDisconnect.Contains(player)) return;
            DiagLog.Log($"[Colosseum Game] Room '{_roomName}' message from {player}: {message}");

            if (message.StartsWith("VERSION:"))
            {
                string clientVersion = VersionValidator.ParseVersionPayload(message);
                if (string.IsNullOrEmpty(clientVersion))
                {
                    SendToPlayer(player, $"VERSION_MISMATCH:{Application.version}|");
                    Debug.LogWarning($"[Colosseum Game] Empty version from {player}. Disconnecting.");
                    DisconnectDelayed(player);
                    return;
                }
                // Production 서버에서는 Development Build 사용 금지 — IsDevBuild()가 true면 모든 버전을 허용한다.
                if (VersionValidator.IsDevBuild() || VersionValidator.IsCompatible(Application.version, clientVersion))
                {
                    SendToPlayer(player, "VERSION_OK");
                    DiagLog.Log($"[Colosseum Game] Version OK: server={Application.version}, client={clientVersion}");
                }
                else
                {
                    SendToPlayer(player, $"VERSION_MISMATCH:{Application.version}|{clientVersion}");
                    Debug.LogWarning($"[Colosseum Game] Version mismatch: server={Application.version}, client={clientVersion}. Disconnecting {player}.");
                    DisconnectDelayed(player);
                }
                return;
            }
            else if (message.StartsWith("NICKNAME:"))
            {
                string nickname = message.Substring(9);
                var result = NicknameValidator.Validate(nickname);
                if (result != NicknameValidator.Result.Ok)
                {
                    SendToPlayer(player, $"ERROR:{NicknameValidator.ToErrorCode(result)}");
                    Debug.LogWarning($"[Colosseum Game] Nickname rejected from {player}: {result}");
                    return;
                }
                _nicknames[player] = nickname;
                DiagLog.Log($"[Colosseum Game] Nickname registered: {player} -> '{nickname}'");
                BroadcastNicknames();
                return;
            }

            if (message == "READY")
            {
                // 대기 상태에서만 Ready 허용 (게임 중/카운트다운 중 무시)
                if (_status != "waiting")
                {
                    DiagLog.Log($"[Colosseum Game] Ignoring READY from {player} (status: {_status})");
                    SendToPlayer(player, "ERROR:ready_not_available");
                    return;
                }
                // 닉네임 미등록 상태에서 Ready 거절 — 클라가 NICKNAME 보내기 전 Ready 누른 케이스
                if (!_nicknames.ContainsKey(player))
                {
                    SendToPlayer(player, "ERROR:nickname_not_set");
                    Debug.LogWarning($"[Colosseum Game] READY rejected: {player} has no nickname");
                    return;
                }
                _playerReady[player] = true;
                _readyCount = CountReady();
                SendToPlayer(player, "READY_STATE:1");
                BroadcastToAll($"READY_UPDATE:{_readyCount}/{_playerCount}");

                if (_playerCount >= 2 && _readyCount >= _playerCount)
                {
                    _status = "countdown";
                    _countdownTimer = 3f;
                    _lastCountdownSecond = -1;
                    BroadcastToAll("COUNTDOWN_START");
                    DiagLog.Log($"[Colosseum Game] Room '{_roomName}' countdown started!");
                    UpdateSessionProperty("status", "countdown");
                }
            }
            else if (message == "UNREADY")
            {
                // 대기/카운트다운 상태에서만 Unready 허용
                if (_status != "waiting" && _status != "countdown")
                {
                    DiagLog.Log($"[Colosseum Game] Ignoring UNREADY from {player} (status: {_status})");
                    SendToPlayer(player, "ERROR:ready_not_available");
                    return;
                }
                _playerReady[player] = false;
                _readyCount = CountReady();
                SendToPlayer(player, "READY_STATE:0");
                BroadcastToAll($"READY_UPDATE:{_readyCount}/{_playerCount}");

                if (_status == "countdown")
                {
                    _status = "waiting";
                    BroadcastToAll("COUNTDOWN_CANCEL");
                    UpdateSessionProperty("status", "waiting");
                }
            }
        }

        // ──────────────────────────────────────────────
        //  메시지 전송
        // ──────────────────────────────────────────────

        private void DisconnectDelayed(PlayerRef player)
        {
            _pendingDisconnect.Add(player);
            StartCoroutine(DisconnectAfterDelay(player, 1f));
        }

        private System.Collections.IEnumerator DisconnectAfterDelay(PlayerRef player, float delay)
        {
            yield return new UnityEngine.WaitForSeconds(delay);
            if (!_pendingDisconnect.Contains(player)) yield break;
            if (_gameRunner != null)
                _gameRunner.Disconnect(player);
        }

        private ReliableKey NextReliableKey()
        {
            _reliableKeyCounter++;
            return ReliableKey.FromInts(_reliableKeyCounter, 0, 0, 0);
        }

        private void BroadcastToAll(string message)
        {
            if (_gameRunner == null) return;
            byte[] data = Encoding.UTF8.GetBytes(message);
            foreach (var player in GetBroadcastRecipients())
            {
                _gameRunner.SendReliableDataToPlayer(player, NextReliableKey(), data);
            }
        }

        private List<PlayerRef> GetBroadcastRecipients()
        {
            var recipients = new HashSet<PlayerRef>();
            AddBroadcastRecipients(recipients, _playerReady.Keys);
            AddBroadcastRecipients(recipients, _nicknames.Keys);
            AddBroadcastRecipients(recipients, _spawnedPlayers.Keys);
            return recipients.ToList();
        }

        private static void AddBroadcastRecipients(HashSet<PlayerRef> recipients, IEnumerable<PlayerRef> players)
        {
            if (recipients == null || players == null)
            {
                return;
            }

            foreach (var player in players)
            {
                if (player == default)
                {
                    continue;
                }

                recipients.Add(player);
            }
        }

        private void SendToPlayer(PlayerRef player, string message)
        {
            if (_gameRunner == null) return;
            byte[] data = Encoding.UTF8.GetBytes(message);
            _gameRunner.SendReliableDataToPlayer(player, NextReliableKey(), data);
        }

        /// <summary>
        /// 방 안 모든 플레이어 닉네임 목록을 JSON 배열로 브로드캐스트.
        /// 클라이언트(UI)는 Ready 화면 등에서 양쪽 닉네임 표시에 사용.
        /// </summary>
        private void BroadcastNicknames()
        {
            var sb = new StringBuilder("NICKNAMES:[");
            bool first = true;
            foreach (var kvp in _nicknames)
            {
                string nick = kvp.Value.Replace("\\", "\\\\").Replace("\"", "\\\"");
                if (!first) sb.Append(",");
                sb.Append($"\"{nick}\"");
                first = false;
            }
            sb.Append("]");
            BroadcastToAll(sb.ToString());
        }

        // ──────────────────────────────────────────────
        //  헬퍼
        // ──────────────────────────────────────────────

        private int CountReady()
        {
            int count = 0;
            foreach (var kv in _playerReady)
                if (kv.Value) count++;
            return count;
        }

        private void UpdateSessionProperty(string key, string value)
        {
            if (_gameRunner != null && _gameRunner.SessionInfo != null)
            {
                _gameRunner.SessionInfo.UpdateCustomProperties(
                    new Dictionary<string, SessionProperty>
                    {
                        { key, value }
                    });
            }

            // 로비 서버 프로세스가 stdout을 파싱하여 방 상태를 추적할 수 있도록 구조화된 마커 출력
            if (key == "status")
            {
                // 이 포맷은 LobbyServerManager.OutputDataReceived에서 파싱됨
                Console.WriteLine($"##STATUS:{value}");
            }
        }

        private void ShutdownAndQuit()
        {
            DiagLog.Log($"[Colosseum Game] Shutting down game server process for '{_roomName}'");

            if (_gameRunner != null)
            {
                _gameRunner.Shutdown();
            }

            // 프로세스 종료
#if UNITY_EDITOR
            // 에디터에서는 종료하지 않음
            DiagLog.Log("[Colosseum Game] Would quit, but running in editor.");
#else
            Application.Quit();
#endif
        }

        // ──────────────────────────────────────────────
        //  CLI 인자 파싱 헬퍼 (static)
        // ──────────────────────────────────────────────

        public static string GetCommandLineArg(string argName)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == argName)
                    return args[i + 1];
            }
            return null;
        }

        public static int GetCommandLineArgInt(string argName, int defaultValue)
        {
            string val = GetCommandLineArg(argName);
            if (val != null && int.TryParse(val, out int result))
                return result;
            return defaultValue;
        }

        // ──────────────────────────────────────────────
        //  INetworkRunnerCallbacks
        // ──────────────────────────────────────────────

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            // 게임 진행 중 / 카운트다운 중 늦은 입장 거절. ERROR 메시지 후 disconnect.
            if (_status == "playing" || _status == "countdown")
            {
                Debug.LogWarning($"[Colosseum Game] Late join rejected from {player} (status: {_status})");
                SendToPlayer(player, "ERROR:game_in_progress");
                runner.Disconnect(player);
                return;
            }

            // 만석 거절 (Photon이 PlayerCount로 1차 차단하지만 안전망 추가).
            if (_playerCount >= _maxPlayers)
            {
                Debug.LogWarning($"[Colosseum Game] Room_full reject from {player}");
                SendToPlayer(player, "ERROR:room_full");
                runner.Disconnect(player);
                return;
            }

            _playerCount++;
            if (!_playerReady.ContainsKey(player))
            {
                _playerReady[player] = false;
            }

            DiagLog.Log($"[Colosseum Game] Player {player} joined room '{_roomName}' ({_playerCount}/{_maxPlayers})");
            Console.WriteLine($"##PLAYERS:{_playerCount}");
            BroadcastToAll($"PLAYER_JOINED:{_playerCount}/{_maxPlayers}");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            DespawnPlayer(player);
            _pendingDisconnect.Remove(player);

            _playerCount--;
            _playerReady.Remove(player);
            _readyCount = CountReady();

            DiagLog.Log($"[Colosseum Game] Player {player} left room '{_roomName}' ({_playerCount}/{_maxPlayers})");
            Console.WriteLine($"##PLAYERS:{_playerCount}");

            // 카운트다운 중이면 취소
            if (_status == "countdown")
            {
                _status = "waiting";
                BroadcastToAll("COUNTDOWN_CANCEL");
                UpdateSessionProperty("status", "waiting");
            }

            // 게임 중에 한 명이 나가면 남은 사람 승리
            if (_status == "playing" && _playerCount >= 1)
            {
                BroadcastToAll("GAME_OVER:You Win! (Opponent Left)");
                _status = "gameover";
                _gameOverTimer = 5f;
                UpdateSessionProperty("status", "gameover");
            }

            BroadcastToAll($"PLAYER_LEFT:{_playerCount}/{_maxPlayers}");

            // 닉네임 정리 + 남은 플레이어들에게 새 닉네임 목록 브로드캐스트
            _nicknames.Remove(player);
            BroadcastNicknames();

            // 빈 방이면 즉시 서버 종료. worker-agent가 프로세스 종료를 감지해 Room API 삭제를 수행한다.
            if (_playerCount <= 0)
            {
                DiagLog.Log($"[Colosseum Game] Room '{_roomName}' is empty. Shutting down immediately.");
                ShutdownAndQuit();
                return;
            }
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player,
            ReliableKey key, ArraySegment<byte> data)
        {
            string message = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            HandleMessage(player, message);
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            DiagLog.Log($"[Colosseum Game] Room '{_roomName}' runner shutdown: {shutdownReason}");
            _gameRunner = null;

            // Runner가 셧다운되면 프로세스도 종료
            ShutdownAndQuit();
        }

        // ── 사용하지 않는 콜백 ──
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
