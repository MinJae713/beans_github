using System;
using System.Collections.Generic;
using Colosseum.Contracts;
using Colosseum.NetworkGameplay.Diagnostics;
using UnityEngine;

namespace Colosseum.Network
{
    /// <summary>
    /// Dedicated Server 엔트리포인트 (디스패처).
    ///
    /// CLI 인자에 따라 적절한 서버 매니저를 활성화:
    ///   --mode lobby   → LobbyServerManager (로비 전용, 게임서버 프로세스 spawn)
    ///   --mode game    → GameServerManager  (단일 게임 방 전용)
    ///   --server       → 레거시 호환: lobby 모드로 동작 (기존 단일 프로세스 방식은 제거됨)
    ///
    /// 게임 서버 프로세스 추가 인자:
    ///   --session "Room_xxx"    세션명
    ///   --room-name "방이름"     방 이름
    ///   --max-players 2         최대 플레이어 수
    ///
    /// 서버 감지: --server, --mode, 또는 UNITY_SERVER 빌드.
    /// </summary>
    public class ServerManager : MonoBehaviour
    {
        [Header("References (Inspector에서 할당)")]
        [SerializeField] private LobbyServerManager _lobbyManager;
        [SerializeField] private GameServerManager _gameManager;

        // ── 싱글톤 ──
        public static ServerManager Instance { get; private set; }

        /// <summary>
        /// 현재 실행 중인 서버 모드.
        /// </summary>
        public enum ServerMode { None, Lobby, Game }
        public static ServerMode CurrentMode { get; private set; } = ServerMode.None;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (!IsServerMode())
            {
                // 서버 모드가 아니면 비활성화
                if (_lobbyManager != null) _lobbyManager.enabled = false;
                if (_gameManager != null) _gameManager.enabled = false;
                enabled = false;
                return;
            }

            // [A303-371] dedicated server headless mode 의 main loop CPU 폭주 차단.
            // Unity headless (-batchmode -nographics) 는 Application.targetFrameRate 기본값이 -1
            // (cap 없음) 이라, batchmode 에서는 vsync 도 무시되어 main loop 가 CPU 가 허락하는 한
            // 수백 FPS 까지 폭주하면서 sustained 1 vCPU 점유.
            //
            // Fusion 의 simulation tick (64Hz, NetworkProjectConfig.TickRateSelection.Client) 은
            // Unity main loop 와 분리된 fixed step 이라, targetFrameRate 를 낮춰도 sim 정확성에는
            // 영향 0. main frame 사이의 idle 시간을 OS 에 양보해 CPU 점유만 절감된다.
            //
            // 30 을 택한 근거: server 는 headless 라 화면을 안 그리니 Render() 호출 빈도가 낮아도 됨.
            // 우리 코드의 Render() 본문은 RoomManager 의 ChangeDetector 폴링 정도라 30Hz 로도
            // [Networked] 값 변화를 한 frame (33ms) 안에 감지. 게임 진행 체감 영향 0.
            // client 빌드에는 본 코드 path 가 IsServerMode() 가드로 안 닿아 모니터 hz 그대로 동작.
            Application.targetFrameRate = 60;

            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (!IsServerMode()) return;

            CurrentMode = DetermineMode();
            DiagLog.Log($"[Colosseum Server] Starting in {CurrentMode} mode...");

            switch (CurrentMode)
            {
                case ServerMode.Lobby:
                    InitializeLobbyMode();
                    break;

                case ServerMode.Game:
                    InitializeGameMode();
                    break;

                default:
                    Debug.LogError("[Colosseum Server] Unknown mode! Use --mode lobby or --mode game");
                    break;
            }
        }

        // ──────────────────────────────────────────────
        //  모드 판별
        // ──────────────────────────────────────────────

        private ServerMode DetermineMode()
        {
            string mode = GetCommandLineArg("--mode");

            if (mode != null)
            {
                switch (mode.ToLower())
                {
                    case "lobby": return ServerMode.Lobby;
                    case "game":  return ServerMode.Game;
                }
            }

            // --server 플래그만 있으면 lobby 모드로 동작 (하위호환)
            return ServerMode.Lobby;
        }

        // ──────────────────────────────────────────────
        //  모드별 초기화
        // ──────────────────────────────────────────────

        private void InitializeLobbyMode()
        {
            // GameManager 비활성화
            if (_gameManager != null) _gameManager.enabled = false;

            // LobbyManager가 Inspector에 없으면 동적 추가
            if (_lobbyManager == null)
            {
                _lobbyManager = gameObject.AddComponent<LobbyServerManager>();
            }
            _lobbyManager.enabled = true;
            _lobbyManager.Initialize();

            DiagLog.Log("[Colosseum Server] Lobby server initialized.");
        }

        private void InitializeGameMode()
        {
            // LobbyManager 비활성화
            if (_lobbyManager != null) _lobbyManager.enabled = false;

            string sessionName = GetCommandLineArg("--session");
            string roomName = GetCommandLineArg("--room-name") ?? "Unknown";
            int maxPlayers = GetCommandLineArgInt("--max-players", 2);
            string sceneName = GetCommandLineArg("--scene") ?? SelectableSceneExtensions.DefaultSceneName;

            if (string.IsNullOrEmpty(sessionName))
            {
                Debug.LogError("[Colosseum Server] --session is required for game mode!");
                Application.Quit();
                return;
            }

            // GameManager가 Inspector에 없으면 동적 추가
            if (_gameManager == null)
            {
                _gameManager = gameObject.AddComponent<GameServerManager>();
            }
            _gameManager.enabled = true;
            _gameManager.Initialize(sessionName, roomName, maxPlayers, sceneName);

            DiagLog.Log($"[Colosseum Server] Game server initialized: {roomName} ({sessionName}), scene: {sceneName}");
        }

        // ──────────────────────────────────────────────
        //  서버 모드 감지
        // ──────────────────────────────────────────────

        public static bool IsServerMode()
        {
#if UNITY_SERVER
            return true;
#else
            string[] args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg == "--server" || arg == "-server" ||
                    arg == "--mode")
                    return true;
            }
            return false;
#endif
        }

        // ──────────────────────────────────────────────
        //  CLI 인자 파싱
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
    }
}
