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

        // ── Phase 2: Game Over ──
        private float _gameOverTimer;
        private TextMeshProUGUI _countdownText;

        // ── Phase 1 & 3: Lobby ──
        private GameObject _lobbyPanel;
        private TextMeshProUGUI _lobbyTitleText;
        private TextMeshProUGUI _lobbyPlayer1Text;
        private TextMeshProUGUI _lobbyPlayer2Text;
        private TextMeshProUGUI _lobbyReadyStatusText;
        private TextMeshProUGUI _lobbyCountdownText;
        private Button _readyButton;
        private TextMeshProUGUI _readyButtonText;
        private bool _isReady = false;
        private int _readyCount = 0;
        private int _playerCount = 0;

        // ── 스타일 ──
        private Color _textColor = new Color(0.9f, 0.88f, 0.82f);
        private Color _goldColor = new Color(0.85f, 0.75f, 0.55f);
        private Color _buttonColor = new Color(0.15f, 0.15f, 0.2f, 0.95f);

        [Header("Style — Player Accent")]
        [SerializeField] private Color _player1AccentColor = new Color32(0xFF, 0x5A, 0x1F, 0xFF);
        [SerializeField] private Color _player2AccentColor = new Color32(0x55, 0xAA, 0xFF, 0xFF);

        [Header("Lobby Style")]
        [SerializeField] private Color _lobbyReadyBoxColor = new Color32(0xFF, 0xB3, 0x47, 0xFF);
        [SerializeField] private Color _lobbyLeaveBoxColor = new Color32(0x33, 0x33, 0x33, 0xFF);

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
            NetworkManager.OnCountdownStart += OnCountdownStart;
            NetworkManager.OnCountdownTick += OnCountdownTick;
            NetworkManager.OnCountdownCancel += OnCountdownCancel;
            NetworkManager.OnGameStart += OnGameRestart;
            NetworkManager.OnPlayerJoinedRoom += OnPlayerUpdate;
            NetworkManager.OnPlayerLeftRoom += OnPlayerUpdate;
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
            NetworkManager.OnCountdownStart -= OnCountdownStart;
            NetworkManager.OnCountdownTick -= OnCountdownTick;
            NetworkManager.OnCountdownCancel -= OnCountdownCancel;
            NetworkManager.OnGameStart -= OnGameRestart;
            NetworkManager.OnPlayerJoinedRoom -= OnPlayerUpdate;
            NetworkManager.OnPlayerLeftRoom -= OnPlayerUpdate;
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
                        networkManager.LeaveToMainMenu();
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

            EnsureCanvas();

            if (_lobbyPanel == null)
                CreateLobbyPanel();

            _phase = Phase.PreGame;
            _lobbyPanel.SetActive(true);
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);

            // 타이틀 설정
            if (_lobbyTitleText != null)
                _lobbyTitleText.text = "플레이어 대기 중";

            // Ready 초기화
            _isReady = false;
            _readyCount = 0;
            _playerCount = 0;
            if (_readyButton != null)
            {
                _readyButton.interactable = true;
                var img = _readyButton.GetComponent<Image>();
                if (img != null) img.color = _lobbyReadyBoxColor;
            }
            UpdateReadyButtonLabel();
            if (_lobbyCountdownText != null) _lobbyCountdownText.gameObject.SetActive(false);
            if (_lobbyReadyStatusText != null) _lobbyReadyStatusText.text = "준비: 0/0";
            if (_lobbyPlayer1Text != null) _lobbyPlayer1Text.text = "기다리는 중...";
            if (_lobbyPlayer2Text != null) _lobbyPlayer2Text.text = "기다리는 중...";

            // 이벤트를 놓쳤을 수 있으므로, 캐시된 플레이어 정보로 즉시 갱신
            if (NetworkManager.Instance != null &&
                !string.IsNullOrEmpty(NetworkManager.Instance.LastRoomPlayerInfo))
            {
                UpdatePlayerSlots(NetworkManager.Instance.LastRoomPlayerInfo);
            }

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

            EnsureCanvas();

            if (_gameOverPanel == null)
                CreateGameOverPanel();

            _phase = Phase.GameOver;
            _gameOverTimer = 5f;

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
            _isReady = false;

            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);

            if (_lobbyPanel == null)
                CreateLobbyPanel();

            _lobbyPanel.SetActive(true);

            // 타이틀 설정
            if (_lobbyTitleText != null)
                _lobbyTitleText.text = "재경기 대기 중";

            // Ready 초기화
            _readyCount = 0;
            if (_readyButton != null)
            {
                _readyButton.interactable = true;
                var img = _readyButton.GetComponent<Image>();
                if (img != null) img.color = _lobbyReadyBoxColor;
            }
            UpdateReadyButtonLabel();
            if (_lobbyCountdownText != null) _lobbyCountdownText.gameObject.SetActive(false);
            if (_lobbyReadyStatusText != null) _lobbyReadyStatusText.text = "준비: 0/0";

            // 플레이어 슬롯 업데이트
            UpdatePlayerSlots(playerInfo);
        }

        private void OnGameRestart()
        {
            // 게임 시작 → UI 숨기기
            _phase = Phase.Hidden;
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
            if (_lobbyReadyStatusText != null)
                _lobbyReadyStatusText.text = $"준비: {info}";

            // info: "X/Y" — X=ready, Y=joined
            string[] parts = info.Split('/');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int ready) &&
                int.TryParse(parts[1], out int total))
            {
                _readyCount = ready;
                _playerCount = total;
                UpdateReadyButtonLabel();
            }
        }

        private void OnNicknamesUpdated(string[] nicknames)
        {
            if (!IsInLobbyPhase) return;
            RefreshPlayerSlotTexts();
        }

        private void OnCountdownStart()
        {
            if (!IsInLobbyPhase) return;
            if (_lobbyCountdownText != null)
            {
                _lobbyCountdownText.gameObject.SetActive(true);
                _lobbyCountdownText.text = "3";
            }
            if (_readyButton != null)
                _readyButton.interactable = false;
        }

        private void OnCountdownTick(int seconds)
        {
            if (!IsInLobbyPhase) return;
            if (_lobbyCountdownText != null)
                _lobbyCountdownText.text = seconds.ToString();
        }

        private void OnCountdownCancel()
        {
            if (!IsInLobbyPhase) return;
            if (_lobbyCountdownText != null)
                _lobbyCountdownText.gameObject.SetActive(false);
            if (_readyButton != null)
                _readyButton.interactable = true;

            _isReady = false;
            if (_readyButton != null)
            {
                var img = _readyButton.GetComponent<Image>();
                if (img != null) img.color = _lobbyReadyBoxColor;
            }
            UpdateReadyButtonLabel();
        }

        private void UpdatePlayerSlots(string info)
        {
            string[] parts = info.Split('/');
            if (parts.Length != 2) return;
            if (!int.TryParse(parts[0], out int count)) return;

            _playerCount = count;
            RefreshPlayerSlotTexts();
            UpdateReadyButtonLabel();
        }

        private void RefreshPlayerSlotTexts()
        {
            string[] nicks = NetworkManager.Instance != null
                ? NetworkManager.Instance.LastRoomNicknames
                : null;

            if (_lobbyPlayer1Text != null)
                _lobbyPlayer1Text.text = GetSlotText(0, nicks);
            if (_lobbyPlayer2Text != null)
                _lobbyPlayer2Text.text = GetSlotText(1, nicks);
        }

        private string GetSlotText(int idx, string[] nicks)
        {
            if (idx >= _playerCount) return "기다리는 중...";
            if (nicks != null && idx < nicks.Length && !string.IsNullOrEmpty(nicks[idx]))
                return nicks[idx];
            return "기다리는 중...";
        }

        private void UpdateReadyButtonLabel()
        {
            if (_readyButtonText == null) return;
            string baseLabel = _isReady ? "준비 취소" : "준비";
            _readyButtonText.text = $"{baseLabel} ({_readyCount}/{_playerCount})";
        }

        // ──────────────────────────────────────────────
        //  버튼 콜백
        // ──────────────────────────────────────────────

        private void OnReadyClicked()
        {
            _isReady = !_isReady;

            if (_isReady)
                NetworkManager.Instance?.SendReady();
            else
                NetworkManager.Instance?.SendUnready();

            UpdateReadyButtonLabel();

            if (_readyButton != null)
            {
                var img = _readyButton.GetComponent<Image>();
                if (img != null)
                    img.color = _isReady
                        ? Color.Lerp(_lobbyReadyBoxColor, Color.black, 0.4f)
                        : _lobbyReadyBoxColor;
            }
        }

        private void OnLeaveRoomClicked()
        {
            _phase = Phase.Hidden;
            var networkManager = NetworkManager.Instance;
            if (networkManager != null)
                networkManager.LeaveToMainMenu();
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
            EnsureCanvas();

            _lobbyPanel = new GameObject("InGameLobbyPanel");
            _lobbyPanel.transform.SetParent(_canvas.transform, false);

            var panelRect = _lobbyPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImage = _lobbyPanel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.06f, 0.1f, 0.9f);

            // 컨테이너
            var container = new GameObject("Container");
            container.transform.SetParent(_lobbyPanel.transform, false);
            var cRect = container.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0.5f, 0.5f);
            cRect.anchorMax = new Vector2(0.5f, 0.5f);
            cRect.pivot = new Vector2(0.5f, 0.5f);
            cRect.sizeDelta = new Vector2(400f, 500f);

            var vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = container.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 타이틀 (동적으로 변경됨)
            _lobbyTitleText = CreateText(container.transform, "플레이어 대기 중", 32, _goldColor, 42f);
            CreateSpacer(container.transform, 8f);

            // 플레이어 슬롯
            CreateText(container.transform, "플레이어", 18, new Color(0.6f, 0.6f, 0.6f), 24f);
            _lobbyPlayer1Text = CreateSlot(container.transform, "기다리는 중...", _player1AccentColor);
            _lobbyPlayer2Text = CreateSlot(container.transform, "기다리는 중...", _player2AccentColor);

            CreateSpacer(container.transform, 6f);

            // Ready 상태
            var rsObj = new GameObject("ReadyStatus");
            rsObj.transform.SetParent(container.transform, false);
            var rsLE = rsObj.AddComponent<LayoutElement>();
            rsLE.preferredHeight = 26f;
            _lobbyReadyStatusText = rsObj.AddComponent<TextMeshProUGUI>();
            _lobbyReadyStatusText.text = "준비: 0/0";
            _lobbyReadyStatusText.fontSize = 20;
            _lobbyReadyStatusText.color = new Color(0.6f, 0.6f, 0.6f);
            _lobbyReadyStatusText.alignment = TextAlignmentOptions.Center;
            rsObj.SetActive(false);

            CreateSpacer(container.transform, 4f);

            // 카운트다운
            var cdObj = new GameObject("LobbyCountdown");
            cdObj.transform.SetParent(container.transform, false);
            var cdLE = cdObj.AddComponent<LayoutElement>();
            cdLE.preferredHeight = 50f;
            _lobbyCountdownText = cdObj.AddComponent<TextMeshProUGUI>();
            _lobbyCountdownText.fontSize = 48;
            _lobbyCountdownText.color = Color.white;
            _lobbyCountdownText.alignment = TextAlignmentOptions.Center;
            _lobbyCountdownText.fontStyle = FontStyles.Bold;
            cdObj.SetActive(false);

            // Ready 버튼
            CreateButton(container.transform, "준비", 50f,
                _lobbyReadyBoxColor, OnReadyClicked,
                out _readyButton, out _readyButtonText);

            CreateSpacer(container.transform, 4f);

            // Leave Room 버튼 — MainMenuUI/CreateRoom 의 < 이전으로 와 톤·크기 통일: font 34 / color #888.
            Button leaveBtn;
            TextMeshProUGUI leaveTxt;
            CreateButton(container.transform, "< 방 나가기", 50f,
                Color.clear, OnLeaveRoomClicked, out leaveBtn, out leaveTxt);
            if (leaveTxt != null)
            {
                leaveTxt.color = new Color32(0x88, 0x88, 0x88, 0xFF);
                leaveTxt.fontSize = 34;
                leaveTxt.fontStyle = FontStyles.Normal;
            }
            // CreateButton 의 highlight/pressed 색은 alpha=1 로 강제되어 투명 배경에서도 박스가 깜빡임 →
            // 텍스트 스타일 유지 위해 모든 state color 를 투명으로 덮어씀.
            if (leaveBtn != null)
            {
                var lc = leaveBtn.colors;
                lc.normalColor = Color.clear;
                lc.highlightedColor = Color.clear;
                lc.pressedColor = Color.clear;
                lc.selectedColor = Color.clear;
                lc.disabledColor = Color.clear;
                lc.colorMultiplier = 1f;
                leaveBtn.colors = lc;
            }

            _lobbyPanel.SetActive(false);
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
