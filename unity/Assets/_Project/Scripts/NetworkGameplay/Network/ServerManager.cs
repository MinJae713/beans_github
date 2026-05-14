using System;
using System.Collections.Generic;
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

            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (!IsServerMode()) return;

            CurrentMode = DetermineMode();
            Debug.Log($"[Colosseum Server] Starting in {CurrentMode} mode...");

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

            Debug.Log("[Colosseum Server] Lobby server initialized.");
        }

        private void InitializeGameMode()
        {
            // LobbyManager 비활성화
            if (_lobbyManager != null) _lobbyManager.enabled = false;

            string sessionName = GetCommandLineArg("--session");
            string roomName = GetCommandLineArg("--room-name") ?? "Unknown";
            int maxPlayers = GetCommandLineArgInt("--max-players", 2);

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
            _gameManager.Initialize(sessionName, roomName, maxPlayers);

            Debug.Log($"[Colosseum Server] Game server initialized: {roomName} ({sessionName})");
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
