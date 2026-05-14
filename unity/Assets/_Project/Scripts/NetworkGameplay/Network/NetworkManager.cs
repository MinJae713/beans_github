using System;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Fusion;
using Fusion.Sockets;
using Fusion.Addons.Physics;
using Colosseum.Contracts;

namespace Colosseum.Network
{
    /// <summary>
    /// 클라이언트 전용 네트워크 매니저.
    /// ServerManager의 로비에 접속하여 ReliableData로 통신.
    ///
    /// 흐름:
    ///   1) ConnectToLobby() → ColosseumLobby 세션에 Client로 접속
    ///   2) RequestCreateRoom(name) → 서버에 CREATE_ROOM 메시지 전송
    ///   3) RequestRoomList() → 서버에 LIST_ROOMS 메시지 전송
    ///   4) JoinGameRoom(sessionName) → 로비 나가고, 방 세션에 Client로 접속
    ///   5) SendReady() / SendUnready() → 방에서 READY/UNREADY 전송
    ///
    /// Dev Mode: Host 모드로 로컬 단독 테스트 (기존 동작 유지)
    /// </summary>
    public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Session Settings")]
        [SerializeField] private string _lobbySessionName = "ColosseumLobby";
        [SerializeField] private int _maxPlayers = 2;

        [Header("Player")]
        [SerializeField] private NetworkPrefabRef _playerPrefab;

        // ── Static 플래그 ──
        public static bool AutoStartDevMode = false;
        public static SelectableScene AutoStartDevModeScene = SelectableScene.Unspecified;
        // RestartDevMode가 직전 DevMode 진입 씬을 복원하기 위한 별도 보존 — StartDevMode가
        // AutoStartDevModeScene을 capture 직후 Unspecified로 reset하므로 RestartDevMode 호출 시점에
        // AutoStartDevModeScene만으로는 MVPArena 등 사용자 의도 씬 복원 불가 (MR !58 reviewer 2번).
        private static SelectableScene _lastStartedDevScene = SelectableScene.Unspecified;

        // ── 이벤트: UI에서 구독 ──

        /// <summary>로비 접속 완료</summary>
        public static event Action OnLobbyConnected;

        /// <summary>로비 접속 실패</summary>
        public static event Action<string> OnLobbyFailed;

        /// <summary>서버로부터 방 목록 수신 (JSON 문자열)</summary>
        public static event Action<string> OnRoomListReceived;

        /// <summary>방 생성 완료 (세션 이름)</summary>
        public static event Action<string> OnRoomCreated;

        /// <summary>방 생성 실패 (에러 메시지)</summary>
        public static event Action<string> OnError;

        /// <summary>게임 방 접속 완료</summary>
        public static event Action OnGameRoomJoined;

        /// <summary>게임 방 접속 실패</summary>
        public static event Action<string> OnGameRoomFailed;

        /// <summary>방 내 플레이어 입장 (playerCount/maxPlayers)</summary>
        public static event Action<string> OnPlayerJoinedRoom;

        /// <summary>방 내 플레이어 퇴장 (playerCount/maxPlayers)</summary>
        public static event Action<string> OnPlayerLeftRoom;

        /// <summary>Ready 상태 업데이트 (readyCount/playerCount)</summary>
        public static event Action<string> OnReadyUpdate;

        /// <summary>카운트다운 시작</summary>
        public static event Action OnCountdownStart;

        /// <summary>카운트다운 진행 (초)</summary>
        public static event Action<int> OnCountdownTick;

        /// <summary>카운트다운 취소</summary>
        public static event Action OnCountdownCancel;

        /// <summary>게임 시작</summary>
        public static event Action OnGameStart;

        /// <summary>게임 오버 (승자 이름)</summary>
        public static event Action<string> OnGameOver;

        /// <summary>방 리셋 완료 — 대기실로 복귀 (playerCount/maxPlayers)</summary>
        public static event Action<string> OnRoomReset;

        /// <summary>세션 종료/연결 끊김</summary>
        public static event Action<string> OnDisconnected;

        /// <summary>방 안 닉네임 목록 수신 (서버 NICKNAMES 메시지)</summary>
        public static event Action<string[]> OnNicknamesUpdated;

        // ── 상태 ──
        private NetworkRunner _lobbyRunner;
        private NetworkRunner _gameRunner;
        private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();

        // ── 닉네임 ──
        // UI에서 SetNickname으로 등록 후 로비/방 접속 직후 자동 전송.
        // PlayerPrefs 영구 저장은 UI 책임 (NetworkManager는 메모리만 보유).
        // 작명 규칙은 NicknameValidator 참조.
        private string _nickname = "";

        /// <summary>현재 등록된 로컬 닉네임. 빈 문자열이면 미등록.</summary>
        public string Nickname => _nickname;

        /// <summary>방 안 모든 플레이어 닉네임 (서버 NICKNAMES 메시지 수신 시 갱신).</summary>
        public string[] LastRoomNicknames { get; private set; } = System.Array.Empty<string>();

        private enum ClientState { Idle, InLobby, JoiningRoom, InRoom, DevMode }
        private ClientState _state = ClientState.Idle;

        private string _pendingRoomName; // 방 생성 시 UI 표시용
        private int _reliableKeyCounter = 0;

        public NetworkRunner Runner => _gameRunner != null ? _gameRunner : _lobbyRunner;
        public int PlayerCount => _spawnedPlayers.Count;
        public bool IsInLobby => _state == ClientState.InLobby;
        public bool IsInRoom => _state == ClientState.InRoom;
        public bool IsDevMode => _state == ClientState.DevMode;

        /// <summary>마지막으로 수신한 방 내 플레이어 정보 (예: "2/2"). 이벤트를 놓친 UI가 초기화 시 참조.</summary>
        public string LastRoomPlayerInfo { get; private set; }

        // ── 싱글톤 ──
        public static NetworkManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            string envLobbySession = Environment.GetEnvironmentVariable("COLOSSEUM_LOBBY_SESSION");
            if (!string.IsNullOrWhiteSpace(envLobbySession))
            {
                _lobbySessionName = envLobbySession.Trim();
                Debug.Log($"[Colosseum] Lobby session = {_lobbySessionName}");
            }

            // DontDestroyOnLoad라 Start()가 첫 씬에서 한 번만 호출됨. AutoStartDevMode 플래그는
            // "Dev Mode" 버튼 클릭 → LoadScene 직전에 set되므로 Start() 검사를 놓친다.
            // 새 씬이 로드될 때마다 플래그를 재검사하여 DevMode 진입을 보장한다.
            SceneManager.sceneLoaded += OnSceneLoadedForDevMode;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoadedForDevMode;
                Instance = null;
            }
        }

        private void OnSceneLoadedForDevMode(Scene scene, LoadSceneMode mode)
        {
            if (!AutoStartDevMode) return;
            if (_gameRunner != null) return;
            if (ServerManager.IsServerMode()) return;

            AutoStartDevMode = false;
            // AutoStartDevModeScene reset은 StartDevMode 내부 capture 직후로 이전 — MR !58 reviewer 1번 race 차단.
            Debug.Log($"[Colosseum] Dev Mode: scene '{scene.name}' loaded with AutoStartDevMode — starting Host.");
            StartDevMode();
        }

        private void Start()
        {
            // Dedicated Server 모드면 비활성화
            if (ServerManager.IsServerMode())
            {
                enabled = false;
                return;
            }

            if (AutoStartDevMode)
            {
                AutoStartDevMode = false;
                Debug.Log("[Colosseum] Dev Mode: Auto-starting Host...");
                StartDevMode();
            }
        }

        // ──────────────────────────────────────────────
        //  공개 API
        // ──────────────────────────────────────────────

        /// <summary>
        /// 닉네임 등록. UI가 입력 폼에서 받아서 호출.
        /// NicknameValidator 규칙 통과 시에만 등록 + 즉시 서버 전송 (접속 상태이면).
        /// 영구 저장(PlayerPrefs)은 UI 책임.
        /// </summary>
        /// <returns>검증 통과 시 Result.Ok. 실패 시 사유 코드 반환 (UI에서 사용자에게 안내).</returns>
        public NicknameValidator.Result SetNickname(string nickname)
        {
            var result = NicknameValidator.Validate(nickname);
            if (result != NicknameValidator.Result.Ok)
            {
                Debug.LogWarning($"[Colosseum] Nickname rejected by client: {result}");
                return result;
            }

            _nickname = nickname;
            Debug.Log($"[Colosseum] Nickname set: '{_nickname}'");

            // 이미 접속한 서버에 즉시 전송
            if (_lobbyRunner != null && _state == ClientState.InLobby)
                SendToServer(_lobbyRunner, $"NICKNAME:{_nickname}");
            if (_gameRunner != null && _state == ClientState.InRoom)
                SendToServer(_gameRunner, $"NICKNAME:{_nickname}");

            return NicknameValidator.Result.Ok;
        }

        /// <summary>
        /// 서버의 로비 세션에 Client로 접속.
        /// 이미 접속 중이면 무시. 이미 접속 완료면 이벤트 재발행.
        /// </summary>
        public async void ConnectToLobby()
        {
            if (_state == ClientState.InLobby)
            {
                // 이미 접속 완료 → 이벤트만 다시 발행
                OnLobbyConnected?.Invoke();
                return;
            }

            if (_lobbyRunner != null)
            {
                // 이미 접속 진행 중 → 중복 방지
                Debug.Log("[Colosseum] Already connecting to lobby...");
                return;
            }

            if (_state != ClientState.Idle)
            {
                Debug.LogWarning("[Colosseum] Cannot connect: state is " + _state);
                return;
            }

            Debug.Log("[Colosseum] Connecting to lobby...");
            var lobbySw = System.Diagnostics.Stopwatch.StartNew();

            var lobbyObj = new GameObject("LobbyRunner");
            DontDestroyOnLoad(lobbyObj);
            _lobbyRunner = lobbyObj.AddComponent<NetworkRunner>();
            _lobbyRunner.ProvideInput = false;
            _lobbyRunner.AddCallbacks(this);

            var result = await _lobbyRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = _lobbySessionName,
                // STUN 프로브 스킵용 placeholder. Relay 아키텍처라 실제 주소값은 사용되지 않음. EC2 Dedicated Server 도입 시 실제 공인 주소로 교체.
                CustomPublicAddress = NetAddress.CreateFromIpPort("127.0.0.1", 0),
            });
            lobbySw.Stop();
            Debug.Log($"[Colosseum-Timing] ConnectToLobby StartGame took {lobbySw.ElapsedMilliseconds}ms, ok={result.Ok}");

            if (result.Ok)
            {
                _state = ClientState.InLobby;
                Debug.Log("[Colosseum] Connected to lobby!");

                // 닉네임 미리 등록돼 있으면 자동 전송. 미등록이면 SetNickname 호출 시 전송됨.
                if (!string.IsNullOrEmpty(_nickname))
                    SendToServer(_lobbyRunner, $"NICKNAME:{_nickname}");

                OnLobbyConnected?.Invoke();
            }
            else
            {
                Debug.LogError($"[Colosseum] Lobby connection failed: {result.ShutdownReason}");
                CleanupLobbyRunner();
                OnLobbyFailed?.Invoke(result.ShutdownReason.ToString());
            }
        }

        /// <summary>
        /// 로비에서 나가기 (메인 메뉴로).
        /// </summary>
        public void DisconnectFromLobby()
        {
            CleanupLobbyRunner();
            _state = ClientState.Idle;
        }

        /// <summary>
        /// 서버에 방 생성 요청. 결과는 OnRoomCreated/OnError 이벤트로.
        /// </summary>
        public void RequestCreateRoom(string roomName)
        {
            if (_state != ClientState.InLobby || _lobbyRunner == null)
            {
                Debug.LogWarning("[Colosseum] Not in lobby.");
                return;
            }

            _pendingRoomName = roomName;
            _createRoomSw = System.Diagnostics.Stopwatch.StartNew();
            SendToServer(_lobbyRunner, $"CREATE_ROOM:{roomName}");
            Debug.Log($"[Colosseum-Timing] CREATE_ROOM sent for '{roomName}' (T0)");
        }

        private System.Diagnostics.Stopwatch _createRoomSw;

        /// <summary>
        /// 서버에 방 목록 요청. 결과는 OnRoomListReceived 이벤트로.
        /// </summary>
        public void RequestRoomList()
        {
            if (_state != ClientState.InLobby || _lobbyRunner == null)
            {
                Debug.LogWarning("[Colosseum] Not in lobby.");
                return;
            }

            SendToServer(_lobbyRunner, "LIST_ROOMS");
            Debug.Log("[Colosseum] Requested room list");
        }

        /// <summary>
        /// 게임 방에 Client로 접속.
        /// 로비 연결은 유지 (방 목록 업데이트 수신).
        /// </summary>
        /// <summary>
        /// 게임 방에 Client로 접속.
        ///
        /// 게임 서버 프로세스가 Fusion 세션을 생성하는 데 수 초가 걸리므로,
        /// 세션이 아직 없을 때 일정 간격으로 재시도한다.
        /// </summary>
        private const int JoinMaxRetries = 5;
        private const float JoinRetryIntervalSec = 2f;

        public async void JoinGameRoom(string sessionName)
        {
            if (_state != ClientState.InLobby)
            {
                Debug.LogWarning("[Colosseum] Must be in lobby to join a room.");
                return;
            }

            _state = ClientState.JoiningRoom;
            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            Debug.Log($"[Colosseum-Timing] JoinGameRoom START session={sessionName}");

            for (int attempt = 1; attempt <= JoinMaxRetries; attempt++)
            {
                var attemptSw = System.Diagnostics.Stopwatch.StartNew();
                bool ok = await TryJoinGameRoom(sessionName);
                attemptSw.Stop();
                Debug.Log($"[Colosseum-Timing] Join attempt {attempt} took {attemptSw.ElapsedMilliseconds}ms (ok={ok})");
                if (ok)
                {
                    totalSw.Stop();
                    Debug.Log($"[Colosseum-Timing] JoinGameRoom TOTAL {totalSw.ElapsedMilliseconds}ms (succeeded on attempt {attempt})");
                    return;
                }

                if (attempt < JoinMaxRetries)
                {
                    Debug.Log($"[Colosseum] Join attempt {attempt}/{JoinMaxRetries} failed, retrying in {JoinRetryIntervalSec}s...");
                    await System.Threading.Tasks.Task.Delay((int)(JoinRetryIntervalSec * 1000));

                    // 재시도 중 상태가 변경되었으면 중단
                    if (_state != ClientState.JoiningRoom) return;
                }
                else
                {
                    totalSw.Stop();
                    Debug.LogError($"[Colosseum-Timing] JoinGameRoom FAILED after {JoinMaxRetries} attempts, total {totalSw.ElapsedMilliseconds}ms");
                    _state = ClientState.InLobby;
                    OnGameRoomFailed?.Invoke("Game server not ready (timeout)");
                }
            }
        }

        private async System.Threading.Tasks.Task<bool> TryJoinGameRoom(string sessionName)
        {
            var roomObj = new GameObject("GameRunner");
            DontDestroyOnLoad(roomObj);
            _gameRunner = roomObj.AddComponent<NetworkRunner>();
            _gameRunner.ProvideInput = true;
            _gameRunner.AddCallbacks(this);

            // InputProvider 등록 (클라이언트 입력을 서버로 전송)
            var inputProvider = roomObj.AddComponent<InputProvider>();
            _gameRunner.AddCallbacks(inputProvider);

            // Fusion 씬 관리자 추가 (서버와 씬 동기화에 필수)
            // 프로세스-per-룸 구조: 클라이언트도 방 1개만 접속하므로 SceneTakeOver 기본값(true) 사용
            roomObj.AddComponent<Fusion.NetworkSceneManagerDefault>();
            ConfigureRunnerPhysics2D(roomObj, ClientPhysicsSimulation.SimulateAlways);

            // 클라이언트는 Scene을 지정하지 않음 → 서버가 로드한 씬에 자동 동기화
            var startSw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _gameRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = sessionName,
                PlayerCount = _maxPlayers,
                // STUN 프로브 스킵용 placeholder. Relay 아키텍처라 실제 주소값은 사용되지 않음. EC2 Dedicated Server 도입 시 실제 공인 주소로 교체.
                CustomPublicAddress = NetAddress.CreateFromIpPort("127.0.0.1", 0),
            });
            startSw.Stop();
            Debug.Log($"[Colosseum-Timing]   StartGame(Client) took {startSw.ElapsedMilliseconds}ms, ok={result.Ok}, reason={result.ShutdownReason}");

            if (result.Ok)
            {
                _state = ClientState.InRoom;
                Debug.Log($"[Colosseum] Joined room '{sessionName}'!");

                // 게임 서버는 별개 프로세스라 로비 닉네임을 모름. 방 입장 직후 다시 전송.
                if (!string.IsNullOrEmpty(_nickname))
                    SendToServer(_gameRunner, $"NICKNAME:{_nickname}");

                OnGameRoomJoined?.Invoke();
                return true;
            }
            else
            {
                Debug.LogWarning($"[Colosseum] Join attempt failed: {result.ShutdownReason}");
                CleanupGameRunner();
                // 상태는 JoiningRoom 유지 (재시도를 위해)
                return false;
            }
        }

        /// <summary>
        /// 게임 방에서 나가기. 로비는 유지.
        /// </summary>
        public void LeaveGameRoom()
        {
            CleanupGameRunner();
            _spawnedPlayers.Clear();
            LastRoomPlayerInfo = null;

            // 로비 러너가 살아있으면 InLobby, 아니면 Idle
            _state = _lobbyRunner != null ? ClientState.InLobby : ClientState.Idle;
            Debug.Log($"[Colosseum] Left game room. State: {_state}");
        }

        /// <summary>
        /// 방에서 Ready 전송.
        /// </summary>
        public void SendReady()
        {
            if (_state != ClientState.InRoom || _gameRunner == null) return;
            SendToServer(_gameRunner, "READY");
            Debug.Log("[Colosseum] Sent READY");
        }

        /// <summary>
        /// 방에서 Unready 전송.
        /// </summary>
        public void SendUnready()
        {
            if (_state != ClientState.InRoom || _gameRunner == null) return;
            SendToServer(_gameRunner, "UNREADY");
            Debug.Log("[Colosseum] Sent UNREADY");
        }

        /// <summary>
        /// 모든 연결 종료하고 메인 메뉴로.
        /// </summary>
        public void DisconnectAll()
        {
            CleanupGameRunner();
            CleanupLobbyRunner();
            _spawnedPlayers.Clear();
            LastRoomPlayerInfo = null;
            _state = ClientState.Idle;
        }

        /// <summary>
        /// 모든 연결 종료 후 메인 메뉴 씬 로드.
        /// </summary>
        public void LeaveToMainMenu()
        {
            DisconnectAll();
            SceneManager.LoadScene("MainMenu");
        }

#if UNITY_EDITOR
        /// <summary>
        /// DevMode 안전 재시작. Runner.Shutdown await 후 DisconnectAll + 씬 reload + StartDevMode 트리거.
        /// DevMode reset tooling에서 호출. DisconnectAll 단독은 CleanupGameRunner가
        /// fire-and-forget이라 Shutdown 완료 전 LoadScene 호출 시 race condition 발생.
        /// </summary>
        public async System.Threading.Tasks.Task RestartDevMode()
        {
            // StartDevMode가 capture 후 AutoStartDevModeScene을 Unspecified로 reset하므로
            // RestartDevMode 호출 시점에 AutoStartDevModeScene는 stale일 가능성.
            // 1순위 AutoStartDevModeScene → 2순위 _lastStartedDevScene (StartDevMode 정상 진입 시 갱신) → 3순위 SampleScene 안전 fallback.
            // 다음 sceneLoaded 콜백까지 prevScene 유지 (OnSceneLoadedForDevMode reset 안 함, MR !58 reviewer 2번 차단).
            var prevScene = AutoStartDevModeScene != SelectableScene.Unspecified
                ? AutoStartDevModeScene
                : (_lastStartedDevScene != SelectableScene.Unspecified
                    ? _lastStartedDevScene
                    : SelectableScene.SampleScene);
            if (_gameRunner != null)
            {
                try { await _gameRunner.Shutdown(); }
                catch { /* best-effort */ }
            }
            DisconnectAll();
            AutoStartDevMode = true;
            AutoStartDevModeScene = prevScene;
            // RestartDevMode는 현재 active scene 그대로 reload (Editor 디버깅용).
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }
#endif

        // ──────────────────────────────────────────────
        //  Dev Mode (Host 모드 — 로컬 1인 테스트)
        // ──────────────────────────────────────────────

        private async void StartDevMode()
        {
            _state = ClientState.DevMode;

            // Capture-then-reset (MR !58 reviewer 1번 race 차단) — OnSceneLoadedForDevMode가
            // reset 안 하므로 본 메서드 진입 시점에 AutoStartDevModeScene는 OnDevModeClicked/RestartDevMode가
            // set한 값 그대로. 이후 모든 ToScenePath 계산은 targetScene 지역변수 사용.
            var targetScene = AutoStartDevModeScene;
            AutoStartDevModeScene = SelectableScene.Unspecified;
            // RestartDevMode 후속 호출이 본 씬 복원 가능하도록 정상 진입 시점에 _lastStartedDevScene 갱신
            // (Unspecified는 fail/stale 의미라 갱신 안 함 — prior 값 유지).
            if (targetScene != SelectableScene.Unspecified)
                _lastStartedDevScene = targetScene;

            var runnerObj = new GameObject("DevRunner");
            DontDestroyOnLoad(runnerObj);
            _gameRunner = runnerObj.AddComponent<NetworkRunner>();
            _gameRunner.ProvideInput = true;
            _gameRunner.AddCallbacks(this);

            // InputProvider 등록
            var inputProvider = runnerObj.AddComponent<InputProvider>();
            _gameRunner.AddCallbacks(inputProvider);

            // Fusion 씬 관리자
            runnerObj.AddComponent<Fusion.NetworkSceneManagerDefault>();
            ConfigureRunnerPhysics2D(runnerObj, ClientPhysicsSimulation.SimulateAlways);

            var sceneInfo = new NetworkSceneInfo();
            int idx = SceneUtility.GetBuildIndexByScenePath(targetScene.ToScenePath());
            if (idx < 0)
            {
                int mainMenuIdx = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/MainMenu.unity");
                if (SceneManager.GetActiveScene().buildIndex == mainMenuIdx)
                {
                    Debug.LogError($"[Colosseum] DevMode aborted: scene '{targetScene}' not in Build Settings and active scene is MainMenu (no valid arena).");
                    Destroy(runnerObj);
                    _gameRunner = null;
                    _state = ClientState.Idle;
                    return;
                }
                Debug.LogError($"[Colosseum] DevMode scene '{targetScene}' not in Build Settings (idx={idx}). Falling back to active scene.");
                idx = SceneManager.GetActiveScene().buildIndex;
            }
            sceneInfo.AddSceneRef(SceneRef.FromIndex(idx));

            var result = await _gameRunner.StartGame(new StartGameArgs
            {
                // 같은 PC에서 Editor + Standalone 동시 실행 시 두 번째 process가 Client로 자동 join.
                // 첫 진입자 = Host, 후속 진입자 = Client (Fusion 2 AutoHostOrClient 라우팅).
                GameMode = GameMode.AutoHostOrClient,
                // 5인팀 공유 AppId에서 SessionName 충돌 방지 — A303-239 (Photon Code 104 ServerInRoom)
                SessionName = $"DevRoom_{SystemInfo.deviceUniqueIdentifier.Substring(0, 6)}",
                PlayerCount = _maxPlayers,
                Scene = sceneInfo,
                // STUN 프로브 스킵용 placeholder. Relay 아키텍처라 실제 주소값은 사용되지 않음. EC2 Dedicated Server 도입 시 실제 공인 주소로 교체.
                CustomPublicAddress = NetAddress.CreateFromIpPort("127.0.0.1", 0),
            });

            if (result.Ok)
            {
                Debug.Log("[Colosseum] Dev Mode started!");
                // Dev tooling object가 씬에 배치된 경우 Awake/Start에서 준비 이벤트를 발행한다.
                // 이 메서드는 외부 tooling 타입을 직접 참조하지 않는다.
            }
            else
            {
                Debug.LogError($"[Colosseum] Dev Mode failed: {result.ShutdownReason}");
                if (_gameRunner != null)
                {
                    try { await _gameRunner.Shutdown(); }
                    catch { /* best-effort */ }
                    _gameRunner = null;
                }
                if (runnerObj != null) Destroy(runnerObj);
                _state = ClientState.Idle;
            }
        }

        private static void ConfigureRunnerPhysics2D(GameObject runnerObj, ClientPhysicsSimulation clientPhysicsSimulation)
        {
            var physics2D = runnerObj.AddComponent<RunnerSimulatePhysics2D>();
            physics2D.ClientPhysicsSimulation = clientPhysicsSimulation;
        }

#if UNITY_EDITOR
        // Editor friend assembly only. 멀티플레이 환경 사용 금지.
        // The caller must prove DevMode readiness without making this
        // Fusion-weaved runtime assembly reference the DevMode assembly.
        internal void SetDevModePlayerPrefab(NetworkPrefabRef prefabRef, bool devModeReady)
        {
            if (!devModeReady) return;
            if (_state != ClientState.DevMode) return;
            if (Runner == null || !Runner.IsServer) return;
            _playerPrefab = prefabRef;
        }

        // Editor friend assembly only. PlayerSpawnAdapter R12 원복용.
        internal NetworkPrefabRef GetDevModePlayerPrefab()
        {
            return _playerPrefab;
        }
#endif

        // ──────────────────────────────────────────────
        //  메시지 송수신
        // ──────────────────────────────────────────────

        private void SendToServer(NetworkRunner runner, string message)
        {
            if (runner == null) return;
            byte[] data = Encoding.UTF8.GetBytes(message);
            _reliableKeyCounter++;
            runner.SendReliableDataToServer(ReliableKey.FromInts(_reliableKeyCounter, 0, 0, 0), data);
        }

        private void HandleServerMessage(NetworkRunner runner, string message)
        {
            Debug.Log($"[Colosseum] Server message: {message}");

            if (message.StartsWith("ROOM_CREATED:"))
            {
                string sessionName = message.Substring(13);
                if (_createRoomSw != null)
                {
                    _createRoomSw.Stop();
                    Debug.Log($"[Colosseum-Timing] ROOM_CREATED received {_createRoomSw.ElapsedMilliseconds}ms after CREATE_ROOM (T1)");
                    _createRoomSw = null;
                }
                Debug.Log($"[Colosseum] Room created: {sessionName}");
                OnRoomCreated?.Invoke(sessionName);
            }
            else if (message.StartsWith("ROOM_LIST:"))
            {
                string json = message.Substring(10);
                OnRoomListReceived?.Invoke(json);
            }
            else if (message.StartsWith("ERROR:"))
            {
                string error = message.Substring(6);
                Debug.LogWarning($"[Colosseum] Server error: {error}");
                OnError?.Invoke(error);
            }
            else if (message.StartsWith("PLAYER_JOINED:"))
            {
                string info = message.Substring(14);
                LastRoomPlayerInfo = info;
                OnPlayerJoinedRoom?.Invoke(info);
            }
            else if (message.StartsWith("PLAYER_LEFT:"))
            {
                string info = message.Substring(12);
                LastRoomPlayerInfo = info;
                OnPlayerLeftRoom?.Invoke(info);
            }
            else if (message.StartsWith("READY_UPDATE:"))
            {
                string info = message.Substring(13);
                OnReadyUpdate?.Invoke(info);
            }
            else if (message == "COUNTDOWN_START")
            {
                OnCountdownStart?.Invoke();
            }
            else if (message.StartsWith("COUNTDOWN:"))
            {
                if (int.TryParse(message.Substring(10), out int seconds))
                    OnCountdownTick?.Invoke(seconds);
            }
            else if (message == "COUNTDOWN_CANCEL")
            {
                OnCountdownCancel?.Invoke();
            }
            else if (message == "GAME_START")
            {
                // 게임 시작 → UI 이벤트 발행
                // 로비 러너는 즉시 종료하지 않음!
                // Fusion이 같은 프로세스 내 다른 runner를 연쇄 종료시키는 이슈 방지.
                // 로비 러너는 게임 종료 후 LeaveGameRoom() 또는 LeaveToMainMenu()에서 정리.
                OnGameStart?.Invoke();
                Debug.Log("[Colosseum] Game started! Lobby runner kept alive for post-game return.");
            }
            else if (message.StartsWith("GAME_OVER:"))
            {
                string winnerName = message.Substring(10);
                Debug.Log($"[Colosseum] Game over! Winner: {winnerName}");
                OnGameOver?.Invoke(winnerName);
            }
            else if (message.StartsWith("ROOM_RESET:"))
            {
                string info = message.Substring(11);
                Debug.Log($"[Colosseum] Room reset for rematch ({info})");
                OnRoomReset?.Invoke(info);
            }
            else if (message.StartsWith("NICKNAMES:"))
            {
                string json = message.Substring(10);
                LastRoomNicknames = ParseNicknameArray(json);
                OnNicknamesUpdated?.Invoke(LastRoomNicknames);
            }
        }

        /// <summary>
        /// `["nick1","nick2"]` 형태 단순 파싱. 이스케이프(\", \\\\) 복원.
        /// 외부 JSON 라이브러리 없이 가벼운 메시지 전용 파서.
        /// </summary>
        private static string[] ParseNicknameArray(string json)
        {
            if (string.IsNullOrEmpty(json)) return System.Array.Empty<string>();
            json = json.Trim();
            if (!json.StartsWith("[") || !json.EndsWith("]")) return System.Array.Empty<string>();
            string inner = json.Substring(1, json.Length - 2).Trim();
            if (string.IsNullOrEmpty(inner)) return System.Array.Empty<string>();

            var result = new List<string>();
            var sb = new StringBuilder();
            bool inString = false;
            bool escape = false;
            foreach (char c in inner)
            {
                if (escape)
                {
                    sb.Append(c);
                    escape = false;
                    continue;
                }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (!inString && c == ',')
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }
                if (inString) sb.Append(c);
            }
            if (sb.Length > 0) result.Add(sb.ToString());
            return result.ToArray();
        }

        // ──────────────────────────────────────────────
        //  내부 정리
        // ──────────────────────────────────────────────

        private void CleanupLobbyRunner()
        {
            if (_lobbyRunner != null)
            {
                // 로컬 참조 보관: Shutdown()이 OnShutdown 콜백을 동기 호출하여
                // _lobbyRunner를 null로 만들 수 있으므로
                var runner = _lobbyRunner;
                _lobbyRunner = null;
                try
                {
                    runner.Shutdown();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Colosseum] LobbyRunner shutdown error: {e.Message}");
                }
                if (runner != null && runner.gameObject != null)
                    Destroy(runner.gameObject);
            }
        }

        private void CleanupGameRunner()
        {
            if (_gameRunner != null)
            {
                var runner = _gameRunner;
                _gameRunner = null;
                try
                {
                    runner.Shutdown();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Colosseum] GameRunner shutdown error: {e.Message}");
                }
                if (runner != null && runner.gameObject != null)
                    Destroy(runner.gameObject);
            }
        }

        // ──────────────────────────────────────────────
        //  INetworkRunnerCallbacks
        // ──────────────────────────────────────────────

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            // Dev Mode에서는 Host이므로 직접 스폰
            if (_state == ClientState.DevMode && runner.IsServer)
            {
                // X1'-α + A1 (D77 D-15 시한부): 맵 좌표 ×2 후 X ×1.3 (4:1 ratio + cameraSize 8).
                //   (-3,2)/(3,2) → ×2 → (-6,4)/(6,4) → ×1.3 X → (-7.8, 4)/(7.8, 4).
                // post-demo closure 의무 (D74-2와 함께): SpawnPoint adapter 도입 후 hardcode 제거.
                Vector3 spawnPos = _spawnedPlayers.Count == 0
                    ? new Vector3(-7.8f, 4f, 0f)
                    : new Vector3(7.8f, 4f, 0f);

                NetworkObject playerObj = runner.Spawn(
                    _playerPrefab, spawnPos, Quaternion.identity, player);
                _spawnedPlayers.Add(player, playerObj);

                var roomManager = FindObjectOfType<Colosseum.Game.RoomManager>();
                if (roomManager != null)
                {
                    if (_spawnedPlayers.Count == 1)
                        roomManager.Player1 = player;
                    else
                        roomManager.Player2 = player;
                }

                Debug.Log($"[Colosseum] Dev Mode: Player {player} spawned");
            }
            // Dedicated Server 모드에서는 서버가 스폰 → 클라이언트는 수신만
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (_state == ClientState.DevMode && _spawnedPlayers.TryGetValue(player, out NetworkObject playerObj))
            {
                runner.Despawn(playerObj);
                _spawnedPlayers.Remove(player);
            }
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player,
            ReliableKey key, ArraySegment<byte> data)
        {
            string message = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            HandleServerMessage(runner, message);
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"[Colosseum] Runner shutdown: {shutdownReason}");

            if (runner == _lobbyRunner)
            {
                _lobbyRunner = null;
                if (_state == ClientState.InLobby)
                {
                    _state = ClientState.Idle;
                    OnDisconnected?.Invoke($"Lobby disconnected: {shutdownReason}");
                }
            }
            else if (runner == _gameRunner)
            {
                _gameRunner = null;
                _spawnedPlayers.Clear();

                if (_state == ClientState.InRoom || _state == ClientState.DevMode)
                {
                    _state = _lobbyRunner != null ? ClientState.InLobby : ClientState.Idle;
                    OnDisconnected?.Invoke($"Room disconnected: {shutdownReason}");
                }
            }
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("[Colosseum] Connected to server");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"[Colosseum] Disconnected from server: {reason}");

            if (runner == _lobbyRunner)
            {
                CleanupLobbyRunner();
                _state = ClientState.Idle;
                OnDisconnected?.Invoke($"Disconnected: {reason}");
            }
            else if (runner == _gameRunner)
            {
                CleanupGameRunner();
                _spawnedPlayers.Clear();
                _state = _lobbyRunner != null ? ClientState.InLobby : ClientState.Idle;
                OnDisconnected?.Invoke($"Room disconnected: {reason}");
            }
        }

        // ── 사용하지 않는 콜백 ──
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            // Light2D 정렬 레이어 빈 배열 보정 (씬 직렬화 결함 대비)
            // Light2D.Awake()는 m_ApplyToSortingLayers가 null일 때만 기본값(All)으로 초기화.
            // 빈 배열(int[0])은 null이 아니라서 기본값 초기화가 안 됨 → 모든 스프라이트 unlit.
            FixLight2DSortingLayers();
        }
        public void OnSceneLoadStart(NetworkRunner runner) { }

        /// <summary>
        /// 씬 로드 후 Light2D의 m_ApplyToSortingLayers를 강제로 모든 Sorting Layer로 설정.
        /// 씬 직렬화 결함으로 빈 배열 또는 잘못된 ID가 들어갈 수 있으므로 무조건 덮어쓴다.
        /// </summary>
        private void FixLight2DSortingLayers()
        {
            var lights = FindObjectsOfType<Light2D>();
            Debug.Log($"[Colosseum] FixLight2D: Found {lights.Length} Light2D components");
            if (lights.Length == 0) return;

            var fieldInfo = typeof(Light2D).GetField("m_ApplyToSortingLayers",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo == null)
            {
                Debug.LogWarning("[Colosseum] Light2D.m_ApplyToSortingLayers field not found (URP version mismatch?)");
                return;
            }

            var allSortingLayers = SortingLayer.layers;
            int[] allLayerIds = new int[allSortingLayers.Length];
            for (int i = 0; i < allSortingLayers.Length; i++)
            {
                allLayerIds[i] = allSortingLayers[i].id;
                Debug.Log($"[Colosseum] SortingLayer[{i}]: name='{allSortingLayers[i].name}', id={allSortingLayers[i].id}");
            }

            foreach (var light in lights)
            {
                var currentLayers = fieldInfo.GetValue(light) as int[];
                string currentStr = currentLayers != null
                    ? $"[{string.Join(",", currentLayers)}] (len={currentLayers.Length})"
                    : "null";
                Debug.Log($"[Colosseum] Light2D '{light.name}': current sorting layers = {currentStr}");

                // 무조건 모든 레이어로 설정 (씬 직렬화 결함 무시)
                fieldInfo.SetValue(light, allLayerIds);
                Debug.Log($"[Colosseum] Light2D '{light.name}': FORCED to All ({allLayerIds.Length} layers)");
            }
        }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
