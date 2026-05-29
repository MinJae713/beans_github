using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Colosseum.Game;
using Colosseum.Network;
using Colosseum.Utils;

namespace Colosseum.UI
{
    /// <summary>
    /// 게임 씬 위의 오버레이 UI. 3가지 상태를 관리:
    ///
    /// Phase 1 (PreGame): 방 입장 후 게임 시작 전 대기실
    ///   - 플레이어 슬롯, Ready 버튼, 카운트다운, Leave Room
    ///
    /// Phase 2 (GameOver): 게임 종료 화면
    ///   - 승자 표시, 5초 카운트다운
    ///
    /// Phase 3 (Lobby): 게임 종료 후 재경기 대기실
    ///   - 플레이어 슬롯, Ready 버튼, 카운트다운, Leave Room
    ///
    /// Dedicated Server 모드: NetworkManager 이벤트로 구동
    /// Dev Mode/동일 프로세스 모드: RoomManager.OnGameOverEvent 구독으로 구동
    /// </summary>
    public class GameOverUI : MonoBehaviour
    {
        [Header("UI References (Optional - auto-creates if null)")]
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private TextMeshProUGUI _winnerText;

        // ── 내부 상태 ──
        private enum Phase { Hidden, PreGame, GameOver, Lobby }
        private Phase _phase = Phase.Hidden;

        public bool IsShowing => _phase != Phase.Hidden;

        // ── Phase 2: Game Over ──
        private float _gameOverTimer;
        private TextMeshProUGUI _countdownText;
        private string _lastGameOverWinnerName;

        // ── Phase 1 & 3: Lobby ──
        private GameObject _lobbyPanel;

        // A303-357 Beans 톤 리디자인 — 좌/우 캐릭터 슬롯, 중앙 VS/Countdown, 하단 CyberButton.
        // 기존 텍스트-only 슬롯/Ready status 표시 폐기, 모든 시각 정보는 캐릭터 자세 + 상태 텍스트로 표현.
        private TextMeshProUGUI _lobbyRoomNameText;         // 상단 "방 이름 : {name}" (StateText 위, 플레이어가 어느 방인지 식별)
        private TextMeshProUGUI _lobbyStateText;            // "플레이어 대기중..." / "준비 대기중..." / "게임 시작중 N..."
        private TextMeshProUGUI _lobbyCountdownText;        // 중앙 숫자 (3/2/1/GO!), CountdownText 와 동일 GO
        private Image _lobbyVsImage;                        // 중앙 VS (lobby_versus.png)

        // 좌/우 슬롯 (좌=Player1, 우=Player2, 본인 위치는 ▼YOU 마커로 식별)
        private GameObject _slotLeftRoot;
        private GameObject _slotRightRoot;
        private GameObject _slotLeftIdleGO;                 // sprite swap: idle 자세 GameObject
        private GameObject _slotLeftAimingGO;               // sprite swap: aiming 자세 GameObject
        private GameObject _slotRightIdleGO;
        private GameObject _slotRightAimingGO;
        private TextMeshProUGUI _slotLeftNicknameLabel;
        private TextMeshProUGUI _slotRightNicknameLabel;
        private GameObject _slotLeftYouMarker;
        private GameObject _slotRightYouMarker;

        // Ready/Leave 는 CyberButton 사용 (A303-354 메뉴 톤 통일과 일관)
        private MainMenu.CyberButton _readyCyberButton;
        private MainMenu.CyberButton _leaveCyberButton;
        private TextMeshProUGUI _readyButtonText;           // Ready CyberButton 내부 라벨 reference (텍스트 갱신용)

        private bool _isReady = false;
        private bool _readyRequestPending;
        private int _readyCount = 0;
        private int _playerCount = 0;
        private bool _isCountdownActive;                    // VS ↔ Countdown 전환 상태 추적
        private Coroutine _vsCountdownTransitionCo;
        private Coroutine _countdownPunchCo;
        // 좌/우 슬롯 idle ↔ aiming cross-fade 코루틴 (자세 전환 부드럽게)
        private Coroutine _leftSlotFadeCo;
        private Coroutine _rightSlotFadeCo;

        // ── 스타일 ──
        private Color _textColor = new Color(0.9f, 0.88f, 0.82f);
        private Color _goldColor = new Color(0.85f, 0.75f, 0.55f);
        private Color _buttonColor = new Color(0.15f, 0.15f, 0.2f, 0.95f);

        [Header("Style — Player Accent")]
        [SerializeField] private Color _player1AccentColor = new Color32(0xD7, 0x73, 0x89, 0xFF);  // 핑크
        [SerializeField] private Color _player2AccentColor = new Color32(0x8F, 0xBF, 0x3F, 0xFF);  // 연두
        [SerializeField] private Color _cyberAccentColor   = new Color32(0x7C, 0xE6, 0xD8, 0xFF);  // VS / Countdown 시안

        [Header("Lobby Style")]
        [SerializeField] private Color _lobbyReadyBoxColor = new Color32(0xFF, 0xB3, 0x47, 0xFF);
        [SerializeField] private Color _lobbyLeaveBoxColor = new Color32(0x33, 0x33, 0x33, 0xFF);

        // ── A303-357 Beans 톤 sprite (Inspector wiring 우선, Resources fallback) ──
        [Header("Lobby Beans Tone (A303-357)")]
        [SerializeField] private Sprite _bgArenaSprite;
        [SerializeField] private Sprite _lobbyPlayer1IdleSprite;
        [SerializeField] private Sprite _lobbyPlayer1AimingSprite;
        [SerializeField] private Sprite _lobbyPlayer2IdleSprite;
        [SerializeField] private Sprite _lobbyPlayer2AimingSprite;
        [SerializeField] private Sprite _lobbyVersusSprite;

        private const string ResBgArena         = "UI/MainMenu/bg_main_arena";
        private const string ResLobbyP1Idle     = "UI/MainMenu/lobby_player1_idle";
        private const string ResLobbyP1Aiming   = "UI/MainMenu/lobby_player1_aiming";
        private const string ResLobbyP2Idle     = "UI/MainMenu/lobby_player2_idle";
        private const string ResLobbyP2Aiming   = "UI/MainMenu/lobby_player2_aiming";
        private const string ResLobbyVersus     = "UI/MainMenu/lobby_versus";

        [Header("Lobby Beans Layout (A303-357)")]
        [SerializeField] private float _lobbyCharacterHeight = 520f;          // 캐릭터 sprite 높이
        [SerializeField] private Vector2 _lobbyPlayer1Anchor = new Vector2(0.22f, 0.42f);
        [SerializeField] private Vector2 _lobbyPlayer2Anchor = new Vector2(0.78f, 0.42f);
        [SerializeField] private int _lobbyNicknameFontSize = 38;
        [SerializeField] private float _lobbyVersusHeight = 220f;     // VS Image 표시 높이 (aspect 자동)
        [SerializeField] private int _lobbyCountdownFontSize = 200;
        [SerializeField] private int _lobbyStateFontSize = 36;
        [SerializeField] private int _lobbyRoomNameFontSize = 30;
        // 상단 라벨 두 줄(방 이름 ↑ / StateText ↓) 의 화면 상단 기준 y offset (음수=아래).
        [SerializeField] private float _lobbyRoomNameAnchorY = -70f;
        [SerializeField] private float _lobbyStateAnchorY    = -150f;
        // Ready / Leave CyberButton 위치 (center anchor 기준, y 음수일수록 화면 하단)
        [SerializeField] private float _lobbyReadyButtonAnchorY = -260f;
        [SerializeField] private float _lobbyLeaveButtonAnchorY = -360f;
        // 자세 cross-fade 시간 — 0.04~0.10 범위 추천. 0 에 가까울수록 instant, 클수록 부드러움.
        [SerializeField] private float _lobbyPoseFadeDuration = 0.06f;

        private Canvas _canvas;

        private void Start()
        {
            // Dedicated Server(headless)에서는 UI가 필요 없다. 이벤트 구독도 건너뛴다.
            // - 렌더링 타깃이 없어 Canvas/Text 갱신이 무의미.
            // - 서버 자신은 자기가 브로드캐스트한 GAME_OVER/ROOM_RESET/GAME_START를 되받지
            //   않으므로 구독해도 콜백이 오지 않는다. 죽은 구독을 달고 있을 이유가 없다.
            // - Update()의 자동 타임아웃이 LeaveToMainMenu()로 서버 씬을 언로드시켜
            //   양쪽 클라가 동시에 튕기는 버그가 있었다(그 방어는 Update()쪽에 이미 있음).
            if (Application.isBatchMode) return;

            if (_gameOverPanel != null)
                _gameOverPanel.SetActive(false);

            // 서버 브로드캐스트 및 동일 프로세스 RoomManager 이벤트 구독
            NetworkManager.OnGameRoomJoined += OnGameRoomJoined;
            NetworkManager.OnGameOver += OnNetworkGameOver;
            NetworkManager.OnRoomReset += OnRoomReset;
            NetworkManager.OnReadyUpdate += OnReadyUpdate;
            NetworkManager.OnLocalReadyStateChanged += OnLocalReadyStateChanged;
            NetworkManager.OnCountdownStart += OnCountdownStart;
            NetworkManager.OnCountdownTick += OnCountdownTick;
            NetworkManager.OnCountdownCancel += OnCountdownCancel;
            NetworkManager.OnGameStart += OnGameRestart;
            NetworkManager.OnPlayerJoinedRoom += OnPlayerUpdate;
            NetworkManager.OnPlayerLeftRoom += OnPlayerUpdate;
            NetworkManager.OnError += OnServerError;
            RoomManager.OnGameOverEvent += OnRoomManagerGameOver;
            NetworkManager.OnNicknamesUpdated += OnNicknamesUpdated;

            // 씬 로드 시 이미 방에 접속한 상태면 대기실 표시
            // (Fusion 씬 동기화로 게임 씬이 로드된 후 이 Start()가 호출됨)
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsInRoom)
            {
                ShowPreGameLobby();
            }
        }

        private void OnDestroy()
        {
            // Start()에서 구독하지 않은 서버 인스턴스는 해제도 생략.
            // (MonoBehaviour는 파괴 시점에 isBatchMode가 바뀌지 않으므로 동일 조건으로 분기)
            if (Application.isBatchMode) return;

            NetworkManager.OnGameRoomJoined -= OnGameRoomJoined;
            NetworkManager.OnGameOver -= OnNetworkGameOver;
            NetworkManager.OnRoomReset -= OnRoomReset;
            NetworkManager.OnReadyUpdate -= OnReadyUpdate;
            NetworkManager.OnLocalReadyStateChanged -= OnLocalReadyStateChanged;
            NetworkManager.OnCountdownStart -= OnCountdownStart;
            NetworkManager.OnCountdownTick -= OnCountdownTick;
            NetworkManager.OnCountdownCancel -= OnCountdownCancel;
            NetworkManager.OnGameStart -= OnGameRestart;
            NetworkManager.OnPlayerJoinedRoom -= OnPlayerUpdate;
            NetworkManager.OnPlayerLeftRoom -= OnPlayerUpdate;
            NetworkManager.OnError -= OnServerError;
            RoomManager.OnGameOverEvent -= OnRoomManagerGameOver;
            NetworkManager.OnNicknamesUpdated -= OnNicknamesUpdated;
        }

        private void Update()
        {
            // Dedicated Server에서는 UI 이벤트 구독과 자동 타임아웃을 모두 비활성화한다.
            // 서버는 자기 자신이 브로드캐스트한 ROOM_RESET/GAME_START 메시지를 다시
            // 받지 않으므로 UI 상태 전환/LeaveToMainMenu 흐름이 서버 씬을 언로드하면 안 된다.
            if (Application.isBatchMode) return;

            if (_phase == Phase.GameOver)
            {
                _gameOverTimer -= Time.deltaTime;

                if (_countdownText != null)
                {
                    int sec = Mathf.Max(0, Mathf.CeilToInt(_gameOverTimer));
                    _countdownText.text = sec > 0
                        ? $"방으로 돌아가는 중... ({sec}초)"
                        : "방으로 돌아가는 중...";
                }

                // 서버로부터 ROOM_RESET이 10초 내에 오지 않으면 자동으로 메인 메뉴로
                if (_gameOverTimer <= -10f)
                {
                    Debug.Log("[Colosseum] Game over timeout — returning to main menu");
                    _phase = Phase.Hidden;
                    var networkManager = NetworkManager.Instance;
                    if (networkManager != null)
                    {
                        networkManager.DisconnectGameSession();
                        SceneManager.LoadScene("MainMenu");
                    }
                    else
                        SceneManager.LoadScene("MainMenu");
                }
            }
        }

        // ──────────────────────────────────────────────
        //  Phase 1: 대기실 (게임 시작 전)
        // ──────────────────────────────────────────────

        private void OnGameRoomJoined()
        {
            // 방 접속 완료 → 대기실 표시
            // (Start()에서 IsInRoom 체크보다 늦게 올 수 있으므로 중복 방지)
            ShowPreGameLobby();
        }

        private void ShowPreGameLobby()
        {
            if (_phase != Phase.Hidden) return;

            ResetGameOverDedupe();
            EnsureCanvas();

            if (_lobbyPanel == null)
                CreateLobbyPanel();

            _phase = Phase.PreGame;
            _lobbyPanel.SetActive(true);
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);

            // Ready 초기화
            _readyRequestPending = false;
            _isReady = false;
            _readyCount = 0;
            _playerCount = 0;
            _isCountdownActive = false;
            ResetCountdownVisualImmediate();
            ApplyReadyButtonState(false, interactable: true);

            // 이벤트를 놓쳤을 수 있으므로, 캐시된 플레이어 정보로 즉시 갱신
            if (NetworkManager.Instance != null &&
                !string.IsNullOrEmpty(NetworkManager.Instance.LastRoomPlayerInfo))
            {
                UpdatePlayerSlots(NetworkManager.Instance.LastRoomPlayerInfo);
            }
            RefreshLobbyVisuals();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("[Colosseum] Pre-game lobby shown");
        }

        // ──────────────────────────────────────────────
        //  Phase 2: 게임 오버 표시
        // ──────────────────────────────────────────────

        /// <summary>
        /// NetworkManager GAME_OVER 브로드캐스트 또는 RoomManager.OnGameOverEvent
        /// 구독 경로에서 호출된다.
        /// </summary>
        public void ShowGameOver(string winnerName)
        {
            if (Application.isBatchMode) return;
            if (IsDuplicateGameOverSignal(winnerName)) return;

            EnsureCanvas();

            if (_gameOverPanel == null)
                CreateGameOverPanel();

            _phase = Phase.GameOver;
            _gameOverTimer = 5f;
            _lastGameOverWinnerName = winnerName;

            _gameOverPanel.SetActive(true);
            if (_lobbyPanel != null) _lobbyPanel.SetActive(false);

            _winnerText.text = $"{winnerName} Wins!";

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log($"[Colosseum] Game Over UI: {winnerName} Wins!");
        }

        private void OnNetworkGameOver(string winnerName)
        {
            ShowGameOver(winnerName);
        }

        private void OnRoomManagerGameOver(Fusion.NetworkRunner runner, string winnerName)
        {
            if (Application.isBatchMode) return;
            if (!IsSameSceneRoomManagerRunner(runner))
            {
                return;
            }

            ShowGameOver(winnerName);
        }

        private bool IsDuplicateGameOverSignal(string winnerName)
        {
            return _phase == Phase.GameOver &&
                string.Equals(_lastGameOverWinnerName, winnerName, System.StringComparison.Ordinal);
        }

        private void ResetGameOverDedupe()
        {
            _lastGameOverWinnerName = null;
        }

        private bool IsSameSceneRoomManagerRunner(Fusion.NetworkRunner runner)
        {
            if (runner == null) return false;

            var roomManager = SceneSearch.FindInScene<RoomManager>(gameObject.scene);
            return roomManager != null && roomManager.Runner == runner;
        }

        // ──────────────────────────────────────────────
        //  Phase 3: 재경기 대기실
        // ──────────────────────────────────────────────

        private void OnRoomReset(string playerInfo)
        {
            Debug.Log($"[Colosseum] Room reset → showing lobby. Players: {playerInfo}");

            _phase = Phase.Lobby;
            ResetGameOverDedupe();
            _readyRequestPending = false;
            _isReady = false;
            _isCountdownActive = false;

            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);

            if (_lobbyPanel == null)
                CreateLobbyPanel();

            _lobbyPanel.SetActive(true);

            // Ready 초기화
            _readyCount = 0;
            ResetCountdownVisualImmediate();
            ApplyReadyButtonState(false, interactable: true);

            // 플레이어 슬롯 업데이트 + 시각 갱신 (RefreshLobbyVisuals 가 _phase 가 Lobby 일 때 StateText 를 "재경기 대기중..." 으로)
            UpdatePlayerSlots(playerInfo);
            RefreshLobbyVisuals();
        }

        private void OnGameRestart()
        {
            // 게임 시작 → UI 숨기기
            _phase = Phase.Hidden;
            ResetGameOverDedupe();
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
            if (_lobbyPanel != null) _lobbyPanel.SetActive(false);

            // 2.5D 브롤러: 마우스로 조준하므로 커서를 화면 안에 가두되 보이게 유지
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;

            Debug.Log("[Colosseum] Game starting! Hiding lobby UI.");
        }

        // ──────────────────────────────────────────────
        //  대기실 이벤트 핸들러 (PreGame + Lobby 공용)
        // ──────────────────────────────────────────────

        private bool IsInLobbyPhase => _phase == Phase.PreGame || _phase == Phase.Lobby;

        private void OnPlayerUpdate(string info)
        {
            if (!IsInLobbyPhase) return;
            UpdatePlayerSlots(info);
        }

        private void OnReadyUpdate(string info)
        {
            if (!IsInLobbyPhase) return;

            // info: "X/Y" — X=ready, Y=joined
            string[] parts = info.Split('/');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int ready) &&
                int.TryParse(parts[1], out int total))
            {
                _readyCount = ready;
                _playerCount = total;
                UpdateReadyButtonLabel();
                RefreshLobbyVisuals();  // 자세·StateText 같이 갱신
            }
        }

        private void OnNicknamesUpdated(string[] nicknames)
        {
            if (!IsInLobbyPhase) return;
            RefreshLobbyVisuals();
        }

        private void OnCountdownStart()
        {
            if (!IsInLobbyPhase) return;
            _isCountdownActive = true;
            if (_lobbyCountdownText != null)
                _lobbyCountdownText.text = "3";
            TransitionVsToCountdown();
            if (_readyCyberButton != null)
                _readyCyberButton.Interactable = false;
            UpdateStateText();
        }

        private void OnCountdownTick(int seconds)
        {
            if (!IsInLobbyPhase) return;
            if (_lobbyCountdownText != null)
                _lobbyCountdownText.text = seconds.ToString();
            StartCountdownPunch();
            UpdateStateText();
        }

        private void OnCountdownCancel()
        {
            if (!IsInLobbyPhase) return;
            _isCountdownActive = false;
            TransitionCountdownToVs();
            ApplyReadyButtonState(false, interactable: true);
            UpdateStateText();
        }

        private void UpdatePlayerSlots(string info)
        {
            string[] parts = info.Split('/');
            if (parts.Length != 2) return;
            if (!int.TryParse(parts[0], out int count)) return;

            _playerCount = count;
            RefreshLobbyVisuals();
            UpdateReadyButtonLabel();
        }

        /// <summary>
        /// 닉네임 / ▼YOU 마커 / 자세 / StateText / 슬롯 가시성 한 번에 갱신.
        /// 기존 RefreshPlayerSlotTexts 의 역할을 흡수 + 신규 시각 요소 추가.
        /// </summary>
        private void RefreshLobbyVisuals()
        {
            if (_lobbyPanel == null) return;

            string[] nicks = NetworkManager.Instance != null
                ? NetworkManager.Instance.LastRoomNicknames
                : null;

            // 좌/우 닉네임 라벨
            if (_slotLeftNicknameLabel != null)
                _slotLeftNicknameLabel.text = ResolveSlotNickname(0, nicks);
            if (_slotRightNicknameLabel != null)
                _slotRightNicknameLabel.text = ResolveSlotNickname(1, nicks);

            UpdateSlotVisibility();
            UpdateYouMarker();
            UpdateCharacterPoses();
            UpdateStateText();
            UpdateRoomNameText();
        }

        private void UpdateRoomNameText()
        {
            if (_lobbyRoomNameText == null) return;
            string roomName = NetworkManager.Instance != null
                ? NetworkManager.Instance.CurrentRoomName
                : null;
            _lobbyRoomNameText.text = string.IsNullOrEmpty(roomName)
                ? "방 이름 : -"
                : $"방 이름 : {roomName}";
        }

        private string ResolveSlotNickname(int idx, string[] nicks)
        {
            if (nicks != null && idx < nicks.Length && !string.IsNullOrEmpty(nicks[idx]))
                return nicks[idx];
            return idx == 0 ? "Player 1" : "Player 2";
        }

        private void UpdateSlotVisibility()
        {
            // 두 슬롯 항상 활성. 단 상대가 아직 안 들어왔으면 sprite / 닉네임만 dim 처리.
            // mockup 1(Solo): 본인 쪽만 캐릭터, 상대 쪽 비움. 가장 단순한 표현은 상대 GO 전체 hide.
            bool leftFilled  = _playerCount >= 1;
            bool rightFilled = _playerCount >= 2;

            // 본인이 P2 일 때 좌측이 상대, P1 일 때 우측이 상대.
            int myIdx = GetMyPlayerIndex();
            bool leftIsMine  = myIdx == 0;
            bool rightIsMine = myIdx == 1;

            // 본인 슬롯은 _playerCount>=1 이면 항상 표시. 상대 슬롯은 _playerCount>=2 일 때만.
            if (_slotLeftRoot  != null) _slotLeftRoot.SetActive(leftIsMine ? leftFilled : rightFilled);
            if (_slotRightRoot != null) _slotRightRoot.SetActive(rightIsMine ? rightFilled : leftFilled);

            // 본인 인덱스 모르면(myIdx=-1) — 일단 채워진 만큼 표시
            if (myIdx < 0)
            {
                if (_slotLeftRoot  != null) _slotLeftRoot.SetActive(leftFilled);
                if (_slotRightRoot != null) _slotRightRoot.SetActive(rightFilled);
            }
        }

        private void UpdateYouMarker()
        {
            int myIdx = GetMyPlayerIndex();
            if (_slotLeftYouMarker  != null) _slotLeftYouMarker.SetActive(myIdx == 0);
            if (_slotRightYouMarker != null) _slotRightYouMarker.SetActive(myIdx == 1);
        }

        private void UpdateCharacterPoses()
        {
            // 본인 자세 = _isReady (즉시 반영).
            // 상대 자세 = _readyCount - (_isReady ? 1 : 0) >= 1 (서버 readyCount 에서 본인 몫 차감).
            // OnLocalReadyStateChanged 가 _readyCount 사전 보정도 하므로 두 콜백 도착 순서와 무관하게 정확.
            int myIdx = GetMyPlayerIndex();
            int otherReadyCount = Mathf.Max(0, _readyCount - (_isReady ? 1 : 0));
            bool otherReady = otherReadyCount >= 1;

            bool leftReady, rightReady;
            if (myIdx == 0)      { leftReady = _isReady;  rightReady = otherReady; }
            else if (myIdx == 1) { leftReady = otherReady; rightReady = _isReady;  }
            else
            {
                // 본인 미식별 — readyCount 만으로 보수적 추론 (한 명만 ready 면 좌/우 모두 idle 유지)
                leftReady  = _readyCount >= 2;
                rightReady = _readyCount >= 2;
            }

            SetSlotPose(_slotLeftIdleGO,  _slotLeftAimingGO,  leftReady,  ref _leftSlotFadeCo);
            SetSlotPose(_slotRightIdleGO, _slotRightAimingGO, rightReady, ref _rightSlotFadeCo);
        }

        private void SetSlotPose(GameObject idleGO, GameObject aimingGO, bool isReady, ref Coroutine fadeCo)
        {
            if (idleGO == null || aimingGO == null) return;
            if (fadeCo != null) StopCoroutine(fadeCo);
            fadeCo = StartCoroutine(CrossFadePose(idleGO, aimingGO, fadeToAiming: isReady));
        }

        private System.Collections.IEnumerator CrossFadePose(GameObject idleGO, GameObject aimingGO, bool fadeToAiming)
        {
            float duration = Mathf.Max(0f, _lobbyPoseFadeDuration);
            var idleImg   = idleGO.GetComponent<Image>();
            var aimingImg = aimingGO.GetComponent<Image>();
            if (idleImg == null || aimingImg == null) yield break;

            // 두 GameObject 모두 활성 (cross-fade 가능하게) — fade 끝난 후 alpha=0 인 쪽 비활성 처리.
            idleGO.SetActive(true);
            aimingGO.SetActive(true);

            float startIdleA   = idleImg.color.a;
            float startAimingA = aimingImg.color.a;
            float endIdleA   = fadeToAiming ? 0f : 1f;
            float endAimingA = fadeToAiming ? 1f : 0f;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                var ic = idleImg.color;   ic.a = Mathf.Lerp(startIdleA,   endIdleA,   u); idleImg.color   = ic;
                var ac = aimingImg.color; ac.a = Mathf.Lerp(startAimingA, endAimingA, u); aimingImg.color = ac;
                yield return null;
            }

            var iEnd = idleImg.color;   iEnd.a = endIdleA;   idleImg.color   = iEnd;
            var aEnd = aimingImg.color; aEnd.a = endAimingA; aimingImg.color = aEnd;

            // alpha=0 인 쪽은 raycast 차단 / GC 위해 비활성. 다음 fade 호출 시 다시 활성.
            if (endIdleA   == 0f) idleGO.SetActive(false);
            if (endAimingA == 0f) aimingGO.SetActive(false);
        }

        private void UpdateStateText()
        {
            if (_lobbyStateText == null) return;
            string text;
            if (_isCountdownActive)
            {
                string num = _lobbyCountdownText != null ? _lobbyCountdownText.text : "3";
                text = $"게임 시작중 {num}...";
            }
            else if (_playerCount < 2)
            {
                text = _phase == Phase.Lobby ? "재경기 대기중..." : "플레이어 대기중...";
            }
            else
            {
                text = "준비 대기중...";
            }
            _lobbyStateText.text = text;
        }

        private int GetMyPlayerIndex()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return -1;
            string myNick = nm.Nickname;
            string[] nicks = nm.LastRoomNicknames;
            if (string.IsNullOrEmpty(myNick) || nicks == null) return -1;
            for (int i = 0; i < nicks.Length; i++)
            {
                if (nicks[i] == myNick) return i;
            }
            return -1;
        }

        private void UpdateReadyButtonLabel()
        {
            // 라벨 + Interactable 한 번에 갱신 — 모든 갱신 경로(UpdatePlayerSlots / OnReadyUpdate /
            // OnReadyClicked / OnLocalReadyStateChanged / OnCountdownStart/Cancel)가 같은 결정 사용.
            if (_readyButtonText != null)
            {
                if (_readyRequestPending)
                    _readyButtonText.text = $"처리 중... ({_readyCount}/{_playerCount})";
                else
                {
                    string baseLabel = _isReady ? "Ready 취소" : "Ready";
                    _readyButtonText.text = $"{baseLabel} ({_readyCount} / {_playerCount})";
                }
            }
            if (_readyCyberButton != null)
            {
                // Solo(_playerCount<2) / 카운트다운 중 / 요청 대기 중 → 비활성
                bool effectiveInteractable = _playerCount >= 2 && !_isCountdownActive && !_readyRequestPending;
                _readyCyberButton.Interactable = effectiveInteractable;
            }
        }

        // ──────────────────────────────────────────────
        //  버튼 콜백
        // ──────────────────────────────────────────────

        private void OnReadyClicked()
        {
            if (_readyRequestPending) return;

            bool targetReady = !_isReady;
            bool sent = targetReady
                ? NetworkManager.Instance?.SendReady() == true
                : NetworkManager.Instance?.SendUnready() == true;
            if (!sent) return;

            _readyRequestPending = true;
            if (_readyCyberButton != null)
                _readyCyberButton.Interactable = false;
            UpdateReadyButtonLabel();
        }

        private void OnLocalReadyStateChanged(bool isReady)
        {
            if (!IsInLobbyPhase) return;

            // OnReadyUpdate 보다 OnLocalReadyStateChanged 가 먼저 도착하면 _readyCount 는 아직 stale.
            // 본인 변화량만큼 _readyCount 를 사전 보정해 자세 추론이 잘못된 분기로 빠지지 않게 한다.
            // 곧 도착할 OnReadyUpdate 가 서버 권위 값으로 덮어쓰므로 일시 보정은 안전.
            if (isReady != _isReady)
                _readyCount = Mathf.Clamp(_readyCount + (isReady ? 1 : -1), 0, _playerCount);

            ApplyReadyButtonState(isReady, interactable: true);
            RefreshLobbyVisuals();
        }

        private void OnServerError(string error)
        {
            if (!IsInLobbyPhase || !_readyRequestPending) return;
            ApplyReadyButtonState(_isReady, interactable: true);
        }

        private void ApplyReadyButtonState(bool isReady, bool interactable)
        {
            // `interactable` 인자는 시그니처 보존용 (외부 호출자와 일관). 실제 활성 결정은
            // UpdateReadyButtonLabel 안에서 _playerCount / _isCountdownActive / _readyRequestPending 으로 일원화.
            _ = interactable;
            _isReady = isReady;
            _readyRequestPending = false;
            UpdateReadyButtonLabel();
        }

        private void OnLeaveRoomClicked()
        {
            _phase = Phase.Hidden;
            var networkManager = NetworkManager.Instance;
            if (networkManager != null)
            {
                networkManager.DisconnectGameSession();
                SceneManager.LoadScene("MainMenu");
            }
            else
                SceneManager.LoadScene("MainMenu");
        }

        // ──────────────────────────────────────────────
        //  UI 생성
        // ──────────────────────────────────────────────

        private void EnsureCanvas()
        {
            if (_canvas != null) return;

            var canvasObj = new GameObject("GameOverCanvas");
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // EventSystem이 없으면 생성
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        private void CreateGameOverPanel()
        {
            _gameOverPanel = new GameObject("GameOverPanel");
            _gameOverPanel.transform.SetParent(_canvas.transform, false);

            var panelRect = _gameOverPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImage = _gameOverPanel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.75f);

            // 승자 텍스트
            var textObj = new GameObject("WinnerText");
            textObj.transform.SetParent(_gameOverPanel.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.6f);
            textRect.anchorMax = new Vector2(0.5f, 0.6f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(800f, 120f);

            _winnerText = textObj.AddComponent<TextMeshProUGUI>();
            _winnerText.fontSize = 72;
            _winnerText.color = _goldColor;
            _winnerText.alignment = TextAlignmentOptions.Center;
            _winnerText.fontStyle = FontStyles.Bold;

            // "GAME OVER" 라벨
            var subObj = new GameObject("GameOverLabel");
            subObj.transform.SetParent(_gameOverPanel.transform, false);
            var subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.45f);
            subRect.anchorMax = new Vector2(0.5f, 0.45f);
            subRect.pivot = new Vector2(0.5f, 0.5f);
            subRect.sizeDelta = new Vector2(600f, 60f);

            var subText = subObj.AddComponent<TextMeshProUGUI>();
            subText.text = "GAME OVER";
            subText.fontSize = 36;
            subText.color = new Color(0.7f, 0.7f, 0.7f);
            subText.alignment = TextAlignmentOptions.Center;

            // 카운트다운 텍스트 ("Returning to lobby in 5...")
            var cdObj = new GameObject("ReturnCountdown");
            cdObj.transform.SetParent(_gameOverPanel.transform, false);
            var cdRect = cdObj.AddComponent<RectTransform>();
            cdRect.anchorMin = new Vector2(0.5f, 0.3f);
            cdRect.anchorMax = new Vector2(0.5f, 0.3f);
            cdRect.pivot = new Vector2(0.5f, 0.5f);
            cdRect.sizeDelta = new Vector2(600f, 50f);

            _countdownText = cdObj.AddComponent<TextMeshProUGUI>();
            _countdownText.fontSize = 24;
            _countdownText.color = new Color(0.6f, 0.6f, 0.6f);
            _countdownText.alignment = TextAlignmentOptions.Center;
        }

        private void CreateLobbyPanel()
        {
            // A303-357 Beans 톤 리디자인.
            // 구조: bg_main_arena → StateText(top) → PlayerSlot_Left/Right (캐릭터 + 닉네임 + ▼YOU)
            //       → CenterVS (VS + Countdown, fade swap) → BottomControls (Ready/Leave CyberButton).
            EnsureCanvas();

            Sprite bg       = ResolveSprite(_bgArenaSprite,            ResBgArena);
            Sprite p1Idle   = ResolveSprite(_lobbyPlayer1IdleSprite,   ResLobbyP1Idle);
            Sprite p1Aiming = ResolveSprite(_lobbyPlayer1AimingSprite, ResLobbyP1Aiming);
            Sprite p2Idle   = ResolveSprite(_lobbyPlayer2IdleSprite,   ResLobbyP2Idle);
            Sprite p2Aiming = ResolveSprite(_lobbyPlayer2AimingSprite, ResLobbyP2Aiming);
            Sprite versus   = ResolveSprite(_lobbyVersusSprite,        ResLobbyVersus);

            _lobbyPanel = new GameObject("PreGameLobbyPanel");
            _lobbyPanel.transform.SetParent(_canvas.transform, false);
            var panelRect = _lobbyPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var bgImg = _lobbyPanel.AddComponent<Image>();
            if (bg != null)
            {
                bgImg.sprite = bg;
                bgImg.color = Color.white;
                bgImg.preserveAspect = false;
            }
            else
            {
                bgImg.color = new Color(0.05f, 0.05f, 0.12f, 0.95f);
                Debug.LogWarning("[GameOverUI] bg_main_arena sprite 로드 실패 — 단색 fallback");
            }
            bgImg.raycastTarget = false;

            // ── RoomNameText (상단 중앙, "방 이름 : {name}") ──
            // StateText 위에 위치. 플레이어가 어느 방에 있는지 즉시 확인 가능.
            var roomNameGO = new GameObject("RoomNameText");
            roomNameGO.transform.SetParent(_lobbyPanel.transform, false);
            var roomNameRt = roomNameGO.AddComponent<RectTransform>();
            roomNameRt.anchorMin = new Vector2(0.5f, 1f);
            roomNameRt.anchorMax = new Vector2(0.5f, 1f);
            roomNameRt.pivot = new Vector2(0.5f, 1f);
            roomNameRt.anchoredPosition = new Vector2(0f, _lobbyRoomNameAnchorY);
            roomNameRt.sizeDelta = new Vector2(900f, 50f);
            _lobbyRoomNameText = roomNameGO.AddComponent<TextMeshProUGUI>();
            _lobbyRoomNameText.text = "방 이름 : -";
            _lobbyRoomNameText.fontSize = _lobbyRoomNameFontSize;
            _lobbyRoomNameText.color = new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 1f);
            _lobbyRoomNameText.alignment = TextAlignmentOptions.Center;
            _lobbyRoomNameText.fontStyle = FontStyles.Bold;
            _lobbyRoomNameText.raycastTarget = false;

            // ── StateText (RoomNameText 아래) ──
            var stateGO = new GameObject("StateText");
            stateGO.transform.SetParent(_lobbyPanel.transform, false);
            var stateRt = stateGO.AddComponent<RectTransform>();
            stateRt.anchorMin = new Vector2(0.5f, 1f);
            stateRt.anchorMax = new Vector2(0.5f, 1f);
            stateRt.pivot = new Vector2(0.5f, 1f);
            stateRt.anchoredPosition = new Vector2(0f, _lobbyStateAnchorY);
            stateRt.sizeDelta = new Vector2(900f, 60f);
            _lobbyStateText = stateGO.AddComponent<TextMeshProUGUI>();
            _lobbyStateText.text = "플레이어 대기중...";
            _lobbyStateText.fontSize = _lobbyStateFontSize;
            _lobbyStateText.color = Color.white;
            _lobbyStateText.alignment = TextAlignmentOptions.Center;
            _lobbyStateText.fontStyle = FontStyles.Bold;
            _lobbyStateText.raycastTarget = false;

            // ── PlayerSlot_Left (P1) ──
            _slotLeftRoot = BuildCharacterSlot(_lobbyPanel.transform, "PlayerSlot_Left",
                _lobbyPlayer1Anchor, p1Idle, p1Aiming, _player1AccentColor,
                out _slotLeftIdleGO, out _slotLeftAimingGO,
                out _slotLeftNicknameLabel, out _slotLeftYouMarker);

            // ── PlayerSlot_Right (P2) ──
            _slotRightRoot = BuildCharacterSlot(_lobbyPanel.transform, "PlayerSlot_Right",
                _lobbyPlayer2Anchor, p2Idle, p2Aiming, _player2AccentColor,
                out _slotRightIdleGO, out _slotRightAimingGO,
                out _slotRightNicknameLabel, out _slotRightYouMarker);

            // ── CenterVS (VS Image + Countdown TMP 같은 자리 fade swap) ──
            BuildCenterVs(_lobbyPanel.transform, versus);

            // ── BottomControls (Ready + Leave CyberButton, 중앙 하단) ──
            BuildBottomControls(_lobbyPanel.transform);

            _lobbyPanel.SetActive(false);
        }

        // ──────────────────────────────────────────────
        //  A303-357 Beans 톤 헬퍼
        // ──────────────────────────────────────────────

        private Sprite ResolveSprite(Sprite serialized, string resourcePath)
        {
            if (serialized != null) return serialized;
            var loaded = Resources.Load<Sprite>(resourcePath);
            if (loaded == null)
                Debug.LogWarning($"[GameOverUI] Sprite 로드 실패: SerializeField 미할당 + Resources.Load('{resourcePath}') 실패");
            return loaded;
        }

        private GameObject BuildCharacterSlot(Transform parent, string objName,
            Vector2 anchor, Sprite idleSprite, Sprite aimingSprite, Color accentColor,
            out GameObject idleGO, out GameObject aimingGO,
            out TextMeshProUGUI nicknameLabel, out GameObject youMarkerGO)
        {
            var slotRoot = new GameObject(objName);
            slotRoot.transform.SetParent(parent, false);
            var rt = slotRoot.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(_lobbyCharacterHeight * 0.85f, _lobbyCharacterHeight + 140f);

            // sprite swap 자세 — Idle / Aiming 두 GameObject 가 alpha cross-fade.
            // 초기: Idle alpha 1 + active, Aiming alpha 0 + inactive (다음 fade 시작점).
            idleGO   = BuildCharacterSprite(slotRoot.transform, "IdleSprite",   idleSprite);
            aimingGO = BuildCharacterSprite(slotRoot.transform, "AimingSprite", aimingSprite);
            var aimingImg = aimingGO.GetComponent<Image>();
            if (aimingImg != null)
            {
                var c = aimingImg.color; c.a = 0f; aimingImg.color = c;
            }
            aimingGO.SetActive(false);

            // NicknameLabel — 캐릭터 머리 위
            var nickGO = new GameObject("NicknameLabel");
            nickGO.transform.SetParent(slotRoot.transform, false);
            var nickRt = nickGO.AddComponent<RectTransform>();
            nickRt.anchorMin = new Vector2(0.5f, 1f);
            nickRt.anchorMax = new Vector2(0.5f, 1f);
            nickRt.pivot = new Vector2(0.5f, 0.5f);
            nickRt.anchoredPosition = new Vector2(0f, -50f);
            nickRt.sizeDelta = new Vector2(300f, 50f);
            nicknameLabel = nickGO.AddComponent<TextMeshProUGUI>();
            nicknameLabel.text = "Player";
            nicknameLabel.fontSize = _lobbyNicknameFontSize;
            nicknameLabel.color = Color.white;
            nicknameLabel.alignment = TextAlignmentOptions.Center;
            nicknameLabel.fontStyle = FontStyles.Bold;
            nicknameLabel.raycastTarget = false;
            nicknameLabel.enableWordWrapping = false;
            nicknameLabel.overflowMode = TextOverflowModes.Ellipsis;
            if (nicknameLabel.fontSharedMaterial != null)
            {
                nicknameLabel.fontMaterial = new Material(nicknameLabel.fontSharedMaterial);
                nicknameLabel.outlineColor = accentColor;
                nicknameLabel.outlineWidth = 0.25f;
            }

            // YouMarker — ▼ + "YOU", 닉네임 위
            youMarkerGO = new GameObject("YouMarker");
            youMarkerGO.transform.SetParent(slotRoot.transform, false);
            var youRt = youMarkerGO.AddComponent<RectTransform>();
            youRt.anchorMin = new Vector2(0.5f, 1f);
            youRt.anchorMax = new Vector2(0.5f, 1f);
            youRt.pivot = new Vector2(0.5f, 0f);
            youRt.anchoredPosition = new Vector2(0f, -10f);
            youRt.sizeDelta = new Vector2(160f, 32f);
            var youTmp = youMarkerGO.AddComponent<TextMeshProUGUI>();
            youTmp.text = "▼  YOU";
            youTmp.fontSize = 22;
            youTmp.color = accentColor;
            youTmp.alignment = TextAlignmentOptions.Center;
            youTmp.fontStyle = FontStyles.Bold;
            youTmp.raycastTarget = false;
            youMarkerGO.SetActive(false);

            return slotRoot;
        }

        private GameObject BuildCharacterSprite(Transform parent, string objName, Sprite sprite)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 100f);
            float aspect = sprite != null ? (sprite.rect.width / sprite.rect.height) : 0.7f;
            rt.sizeDelta = new Vector2(_lobbyCharacterHeight * aspect, _lobbyCharacterHeight);
            var img = go.AddComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
            }
            else
            {
                img.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                Debug.LogWarning($"[GameOverUI] {objName} sprite 로드 실패 — placeholder color");
            }
            img.raycastTarget = false;
            return go;
        }

        private void BuildCenterVs(Transform parent, Sprite versusSprite)
        {
            // VS Image — lobby_versus.png (별도 텍스트 만들지 않음)
            var vsGO = new GameObject("VSImage");
            vsGO.transform.SetParent(parent, false);
            var vsRt = vsGO.AddComponent<RectTransform>();
            vsRt.anchorMin = new Vector2(0.5f, 0.5f);
            vsRt.anchorMax = new Vector2(0.5f, 0.5f);
            vsRt.pivot = new Vector2(0.5f, 0.5f);
            vsRt.anchoredPosition = new Vector2(0f, 40f);
            float versusAspect = versusSprite != null ? (versusSprite.rect.width / versusSprite.rect.height) : 1.5f;
            vsRt.sizeDelta = new Vector2(_lobbyVersusHeight * versusAspect, _lobbyVersusHeight);
            _lobbyVsImage = vsGO.AddComponent<Image>();
            if (versusSprite != null)
            {
                _lobbyVsImage.sprite = versusSprite;
                _lobbyVsImage.preserveAspect = true;
                _lobbyVsImage.color = Color.white;
            }
            else
            {
                _lobbyVsImage.color = new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.4f);
                Debug.LogWarning("[GameOverUI] lobby_versus sprite 로드 실패 — 단색 fallback");
            }
            _lobbyVsImage.raycastTarget = false;

            // Countdown Number — VS 동일 위치, 시작 alpha 0
            var cdGO = new GameObject("CountdownNumber");
            cdGO.transform.SetParent(parent, false);
            var cdRt = cdGO.AddComponent<RectTransform>();
            cdRt.anchorMin = new Vector2(0.5f, 0.5f);
            cdRt.anchorMax = new Vector2(0.5f, 0.5f);
            cdRt.pivot = new Vector2(0.5f, 0.5f);
            cdRt.anchoredPosition = new Vector2(0f, 40f);
            cdRt.sizeDelta = new Vector2(400f, 280f);
            _lobbyCountdownText = cdGO.AddComponent<TextMeshProUGUI>();
            _lobbyCountdownText.text = "3";
            _lobbyCountdownText.fontSize = _lobbyCountdownFontSize;
            _lobbyCountdownText.color = new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0f);
            _lobbyCountdownText.alignment = TextAlignmentOptions.Center;
            _lobbyCountdownText.fontStyle = FontStyles.Bold;
            _lobbyCountdownText.raycastTarget = false;
            if (_lobbyCountdownText.fontSharedMaterial != null)
            {
                _lobbyCountdownText.fontMaterial = new Material(_lobbyCountdownText.fontSharedMaterial);
                _lobbyCountdownText.outlineColor = new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.7f);
                _lobbyCountdownText.outlineWidth = 0.25f;
            }
            cdGO.SetActive(false);
        }

        private void BuildBottomControls(Transform parent)
        {
            // Ready / 방 나가기 동일 크기·폰트 — 시각 hierarchy 보조 톤. Inspector 에서 anchorY 튜닝.
            const float btnWidth = 360f;
            const float btnHeight = 60f;
            const int btnFontSize = 32;

            var readyGO = BuildLobbyCyberButton(parent, "Ready (0 / 2)", OnReadyClicked,
                new Vector2(0.5f, 0.5f), new Vector2(0f, _lobbyReadyButtonAnchorY),
                width: btnWidth, height: btnHeight, fontSize: btnFontSize);
            _readyCyberButton = readyGO.GetComponent<MainMenu.CyberButton>();
            _readyButtonText = _readyCyberButton != null ? _readyCyberButton.label : null;
            if (_readyCyberButton != null) _readyCyberButton.Interactable = false;

            var leaveGO = BuildLobbyCyberButton(parent, "방 나가기", OnLeaveRoomClicked,
                new Vector2(0.5f, 0.5f), new Vector2(0f, _lobbyLeaveButtonAnchorY),
                width: btnWidth, height: btnHeight, fontSize: btnFontSize);
            _leaveCyberButton = leaveGO.GetComponent<MainMenu.CyberButton>();
        }

        private GameObject BuildLobbyCyberButton(Transform parent, string label, System.Action onClicked,
            Vector2 anchor, Vector2 anchoredPos, float width, float height, int fontSize)
        {
            const float strokeThickness = 2f;
            var go = new GameObject($"CyberBtn_{label}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(width, height);

            var hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            Color stripCol = new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.7f);
            var top    = BuildOutlineStrip(go.transform, "Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, strokeThickness), stripCol);
            var bottom = BuildOutlineStrip(go.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, strokeThickness), stripCol);
            var left   = BuildOutlineStrip(go.transform, "Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(strokeThickness, 0f), stripCol);
            var right  = BuildOutlineStrip(go.transform, "Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(strokeThickness, 0f), stripCol);

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var labelRt = labelGO.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(20f, 0f);
            labelRt.offsetMax = new Vector2(-20f, 0f);
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;

            var btn = go.AddComponent<MainMenu.CyberButton>();
            btn.outlineImage = null;
            btn.outlineImages = new[] { top, bottom, left, right };
            btn.label = tmp;
            btn.borderColor = _cyberAccentColor;
            btn.OnClicked += onClicked;
            return go;
        }

        private Image BuildOutlineStrip(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // ──────────────────────────────────────────────
        //  VS ↔ Countdown 전환 + Countdown scale punch (Coroutine + Mathf.SmoothStep)
        // ──────────────────────────────────────────────

        private void ResetCountdownVisualImmediate()
        {
            if (_vsCountdownTransitionCo != null) { StopCoroutine(_vsCountdownTransitionCo); _vsCountdownTransitionCo = null; }
            if (_countdownPunchCo != null) { StopCoroutine(_countdownPunchCo); _countdownPunchCo = null; }

            if (_lobbyVsImage != null)
            {
                var c = _lobbyVsImage.color; c.a = 1f; _lobbyVsImage.color = c;
                _lobbyVsImage.rectTransform.localScale = Vector3.one;
            }
            if (_lobbyCountdownText != null)
            {
                var c = _lobbyCountdownText.color; c.a = 0f; _lobbyCountdownText.color = c;
                _lobbyCountdownText.gameObject.SetActive(false);
                _lobbyCountdownText.rectTransform.localScale = Vector3.one;
            }
        }

        private void TransitionVsToCountdown()
        {
            if (_lobbyCountdownText != null) _lobbyCountdownText.gameObject.SetActive(true);
            if (_vsCountdownTransitionCo != null) StopCoroutine(_vsCountdownTransitionCo);
            _vsCountdownTransitionCo = StartCoroutine(FadeVsAndCountdown(toCountdown: true, duration: 0.3f));
        }

        private void TransitionCountdownToVs()
        {
            if (_vsCountdownTransitionCo != null) StopCoroutine(_vsCountdownTransitionCo);
            _vsCountdownTransitionCo = StartCoroutine(FadeVsAndCountdown(toCountdown: false, duration: 0.3f));
        }

        private System.Collections.IEnumerator FadeVsAndCountdown(bool toCountdown, float duration)
        {
            float vsStart = _lobbyVsImage != null ? _lobbyVsImage.color.a : 1f;
            float cdStart = _lobbyCountdownText != null ? _lobbyCountdownText.color.a : 0f;
            float vsEnd = toCountdown ? 0f : 1f;
            float cdEnd = toCountdown ? 1f : 0f;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                if (_lobbyVsImage != null)
                {
                    var c = _lobbyVsImage.color; c.a = Mathf.Lerp(vsStart, vsEnd, u); _lobbyVsImage.color = c;
                }
                if (_lobbyCountdownText != null)
                {
                    var c = _lobbyCountdownText.color; c.a = Mathf.Lerp(cdStart, cdEnd, u); _lobbyCountdownText.color = c;
                }
                yield return null;
            }

            if (_lobbyVsImage != null)
            {
                var c = _lobbyVsImage.color; c.a = vsEnd; _lobbyVsImage.color = c;
            }
            if (_lobbyCountdownText != null)
            {
                var c = _lobbyCountdownText.color; c.a = cdEnd; _lobbyCountdownText.color = c;
                if (!toCountdown) _lobbyCountdownText.gameObject.SetActive(false);
            }
            _vsCountdownTransitionCo = null;
        }

        private void StartCountdownPunch()
        {
            if (_lobbyCountdownText == null) return;
            if (_countdownPunchCo != null) StopCoroutine(_countdownPunchCo);
            _countdownPunchCo = StartCoroutine(CountdownPunchRoutine());
        }

        private System.Collections.IEnumerator CountdownPunchRoutine()
        {
            var rt = _lobbyCountdownText.rectTransform;
            const float duration = 0.2f;
            const float peak = 1.2f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                // 0→0.5: 1.0 → peak, 0.5→1: peak → 1.0
                float scale = u < 0.5f
                    ? Mathf.Lerp(1f, peak, u * 2f)
                    : Mathf.Lerp(peak, 1f, (u - 0.5f) * 2f);
                rt.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
            _countdownPunchCo = null;
        }

        // ──────────────────────────────────────────────
        //  UI 헬퍼
        // ──────────────────────────────────────────────

        private TextMeshProUGUI CreateText(Transform parent, string text, int fontSize, Color color, float height)
        {
            var obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        private TextMeshProUGUI CreateSlot(Transform parent, string defaultText, Color? accentColor = null)
        {
            var obj = new GameObject("PlayerSlot");
            obj.transform.SetParent(parent, false);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = 35f;

            var bg = obj.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.15f, 0.8f);

            if (accentColor.HasValue)
            {
                var accentObj = new GameObject("AccentBar");
                accentObj.transform.SetParent(obj.transform, false);
                var accentRect = accentObj.AddComponent<RectTransform>();
                accentRect.anchorMin = new Vector2(0f, 0f);
                accentRect.anchorMax = new Vector2(0f, 1f);
                accentRect.pivot = new Vector2(0f, 0.5f);
                accentRect.anchoredPosition = Vector2.zero;
                accentRect.sizeDelta = new Vector2(5f, 0f);
                var accentImg = accentObj.AddComponent<Image>();
                accentImg.color = accentColor.Value;
            }

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(accentColor.HasValue ? 20f : 15f, 0f);
            textRect.offsetMax = new Vector2(-15f, 0f);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = defaultText;
            tmp.fontSize = 26;
            tmp.color = _textColor;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            return tmp;
        }

        private void CreateButton(Transform parent, string label, float height,
            Color bgColor, UnityEngine.Events.UnityAction onClick,
            out Button button, out TextMeshProUGUI text)
        {
            var obj = new GameObject($"Btn_{label}");
            obj.transform.SetParent(parent, false);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;

            var img = obj.AddComponent<Image>();
            img.color = bgColor;

            button = obj.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(onClick);

            var colors = button.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = new Color(
                Mathf.Min(bgColor.r + 0.1f, 1f),
                Mathf.Min(bgColor.g + 0.1f, 1f),
                Mathf.Min(bgColor.b + 0.1f, 1f), 1f);
            colors.pressedColor = new Color(
                Mathf.Min(bgColor.r + 0.2f, 1f),
                Mathf.Min(bgColor.g + 0.2f, 1f),
                Mathf.Min(bgColor.b + 0.2f, 1f), 1f);
            button.colors = colors;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 28;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
        }

        private void CreateSpacer(Transform parent, float height)
        {
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(parent, false);
            var le = spacer.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }
    }
}
