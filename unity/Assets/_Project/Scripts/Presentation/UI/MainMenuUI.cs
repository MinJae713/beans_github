using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using Colosseum.Contracts;
using Colosseum.Network;

namespace Colosseum.UI
{
    /// <summary>
    /// 메인 메뉴 전체 UI 관리.
    /// 4개 패널: MainPanel, CreateRoomPanel, FindRoomPanel, LobbyPanel
    ///
    /// Dedicated Server 구조:
    ///   1) 닉네임 확인 시 로비 선접속 (ConnectToLobby)
    ///   2) Create Room / Find Room 은 기존 로비 연결 재사용, 미접속 시 fallback 재접속
    ///   3) 방 생성 → ReliableData로 서버에 요청 → ROOM_CREATED 수신 → 방 세션에 접속
    ///   4) 방 찾기 → ReliableData로 서버에 요청 → ROOM_LIST 수신 → 목록 표시
    ///   5) 방 입장 → 방 세션에 Client 접속 → Ready → 카운트다운 → 게임 시작
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        private const string CreateRoomLobbyConnectingMessage = "로비 접속 중입니다.";

        [Header("Style")]
        [SerializeField] private float _buttonWidth = 400f;
        [SerializeField] private float _buttonHeight = 70f;
        [SerializeField] private float _buttonSpacing = 16f;
        [SerializeField] private int _menuFontSize = 72; // 텍스트-only 모드 메뉴 폰트 사이즈
        [SerializeField] private int _titleFontSize = 100;
        [SerializeField] private Color _titleOutlineColor = new Color32(0x55, 0xAA, 0xFF, 0xFF); // 플레이어 B 하이라이트 파랑
        [SerializeField] private float _titleOutlineWidth = 0.2f;
        [SerializeField] private Color _bgColor = new Color32(0x12, 0x12, 0x1E, 0xFF);              // #12121E
        [SerializeField] private Color _buttonColor = new Color32(0x1E, 0x1E, 0x2E, 0xFF);          // #1E1E2E
        [SerializeField] private Color _buttonHoverColor = new Color32(0xFF, 0x5A, 0x1F, 0xFF);     // #FF5A1F
        [SerializeField] private Color _buttonDisabledColor = new Color32(0x0E, 0x0E, 0x18, 0xFF);  // #0E0E18
        [SerializeField] private Color _textColor = new Color32(0xF0, 0xF0, 0xF0, 0xFF);            // #F0F0F0
        [SerializeField] private Color _goldColor = new Color32(0xFF, 0x5A, 0x1F, 0xFF);            // #FF5A1F

        [Header("Logo")]
        [SerializeField] private Sprite _logoSprite;  // (legacy) MainPanel 로고 이미지. A303-334 후 _beansLogoSprite 로 대체.
        [SerializeField] private Sprite _fightSprite; // (legacy) MainPanel 우하단 장식 이미지. A303-334에서 우하단 비움으로 미사용.

        // ── A303-334 Beans 비주얼 톤 (메인메뉴 풀 리디자인) ──
        [Header("Beans Visual Tone (A303-334)")]
        [SerializeField] private Sprite _beansLogoSprite;    // beans_logo.png — 시안 글로우 BEANS 로고
        [SerializeField] private Sprite _bgArenaSprite;      // bg_main_arena.png — 다크 우주 + 별 + 시안 라인 배경
        [SerializeField] private Sprite _peanyAimingSprite;  // peany_aiming.png — 좌측 완두콩
        [SerializeField] private Sprite _beanyAimingSprite;  // beany_aiming.png — 우측 분홍콩
        [SerializeField] private Color _beansMenuNormalColor = new Color(1f, 1f, 1f, 0.7f);
        [SerializeField] private Color _beansMenuHoverColor = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 1f);
        [SerializeField] private Color _beansBottomInfoColor = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] private int _beansMenuFontSize = 56;
        [SerializeField] private float _beansMenuItemHeight = 72f;
        [SerializeField] private float _beansMenuBlockSpacing = 18f;
        [SerializeField] private float _beansLogoHeight = 216f; // 240 × 0.9
        [SerializeField] private float _beansLogoTopOffset = 120f;
        [SerializeField] private float _beansMenuBlockOffsetY = -135f; // MenuBlock center y (logo와 baseline-to-baseline 간격 ≈ 메뉴줄 간격 ×1.5)
        [SerializeField] private float _beansCharacterHeight = 494f;  // peany/beany 본체 높이 (1.3× 키움)
        [SerializeField] private Vector2 _beansPeanyAnchor = new Vector2(0f, 0.34f);  // x=0 → 화면 좌측 끝에 캐릭터 left edge 붙임
        [SerializeField] private Vector2 _beansBeanyAnchor = new Vector2(1f, 0.34f);  // x=1 → 화면 우측 끝에 캐릭터 right edge 붙임
        [SerializeField] private float _peanyIdleAmplitude = 4f;
        [SerializeField] private float _peanyIdlePeriod = 3.6f;  // 더 느린 호흡 (1.5× 기존)
        [SerializeField] private float _beanyIdleAmplitude = 5f;
        [SerializeField] private float _beanyIdlePeriod = 4.0f;  // 더 느린 호흡 (1.5× 기존), 위상 어긋남 유지
        [SerializeField] private float _beanyIdleStartDelay = 0.5f;
        [SerializeField] private bool _beansLogoPulse = true;
        [SerializeField] private float _beansLogoPulsePeriod = 3.5f;
        [SerializeField] private string _beansVersionText = "BEANS · Team A303";

        // ── Dev Controls: 시연 빌드 직전에 _beansShowDevControls=false 또는 GameObject 삭제 ──
        [Header("Dev Controls (개발 전용, 시연 빌드 직전 끄기)")]
        [SerializeField] private bool _beansShowDevControls = true;
        [SerializeField] private Vector2 _beansDevControlsAnchor = new Vector2(1f, 0f); // 우하단
        [SerializeField] private Vector2 _beansDevControlsOffset = new Vector2(-60f, 50f);
        [SerializeField] private int _beansDevTextFontSize = 16;

        // ── 패널별 토큰 (Nickname / CreateRoom / FindRoom / Lobby) ──
        [SerializeField] private int _pageTitleFontSize = 70;
        [SerializeField] private int _inputFieldFontSize = 42;
        [SerializeField] private int _actionButtonFontSize = 48;
        [SerializeField] private int _backButtonFontSize = 34;
        [SerializeField] private Color _actionButtonBoxColor = new Color32(0xFF, 0xB3, 0x47, 0xFF);
        [SerializeField] private Color _actionButtonHoverBoxColor = new Color32(0xB8, 0x5A, 0x1F, 0xFF);
        [SerializeField] private Color _backButtonColor = new Color32(0x88, 0x88, 0x88, 0xFF);
        [SerializeField] private Color _errorTextColor = new Color32(0xFF, 0x6B, 0x6B, 0xFF);
        [SerializeField] private Color _placeholderColor = new Color32(0x66, 0x66, 0x66, 0xFF);
        [SerializeField] private Color _actionButtonDisabledColor = new Color32(0x33, 0x33, 0x33, 0xFF);
        [SerializeField] private Color _actionButtonDisabledTextColor = new Color32(0x77, 0x77, 0x77, 0xFF);
        [SerializeField] private Color _actionButtonTextOutlineColor = new Color32(0x8B, 0x3A, 0x12, 0xFF);
        [SerializeField] private float _actionButtonTextOutlineWidth = 0.15f;
        [SerializeField] private Color _inputFieldBgColor = new Color32(0x1E, 0x1E, 0x26, 0xF2);

        // ── A303-354 메뉴 톤 통일 (Nickname / CreateRoom / FindRoom) ──
        [Header("Beans Cyber Tone (A303-354)")]
        [SerializeField] private Sprite _cyberBtnOutlineSprite;     // cyber_btn_outline.png — 9-slice 시안 outline 박스
        [SerializeField] private Sprite _createRoomTitleSprite;     // create_room_title.png — "- CREATE ROOM -" 타이틀
        [SerializeField] private Sprite _findRoomTitleSprite;       // find_room_title.png — "- FIND ROOM -" 타이틀
        [SerializeField] private float _cyberTitleHeight = 140f;    // 타이틀 sprite 기준 표시 높이
        [SerializeField] private float _cyberTitleScale = 0.8f;     // 위 기준값에 곱해질 스케일 (BEANS 로고 대비 비율 조절)
        [SerializeField] private float _cyberTitleTopOffset = 80f;  // 화면 상단에서의 여백 (anchor top, pivot top) — FindRoom 기본값
        // CreateRoom 전용 anchoredY 오버라이드 — BEANS 로고 (height 216, topOffset 120) center y = -228.
        // 0.8 scale 적용된 CREATE ROOM 로고 height 112 가 같은 center 를 가지려면 anchoredY = -228 + 56 = -172.
        [SerializeField] private float _createRoomTitleAnchoredY = -172f;
        // 입력 필드/액션 버튼/Status/Back MenuItem 의 anchored Y (Inspector 에서 튜닝)
        [SerializeField] private float _cyberInputAnchorY = 0f;            // anchor center → 화면 세로 가운데
        [SerializeField] private float _cyberStatusTextAnchorY = -100f;    // anchor center → input 아래, 버튼 위
        [SerializeField] private float _cyberActionButtonAnchorY = 200f;   // anchor bottom → 화면 하단에서 위로 px
        [SerializeField] private float _cyberBackMenuItemAnchorY = 80f;    // anchor bottom → CreateRoom 이전으로
        [SerializeField] private float _findRoomStatsTextAnchorY = -260f;  // anchor top → FIND ROOM 로고와 패널 사이
        [SerializeField] private float _findRoomBottomButtonsAnchorY = 140f; // anchor bottom → 새로고침/이전으로 위치
        // ScrollRect 휠 sensitivity. Unity default 1.0 은 사실상 불가, 100 정도가 일반 웹 휠 체감과 유사.
        // 휠 raw delta * 이 값 = 이동 px. 60~150 범위에서 취향 조정.
        [SerializeField] private float _findRoomScrollSensitivity = 100f;

        // ── A303-354 추가: Create Room 맵 선택 카드 ──
        [Header("Create Room — Map Selection (A303-354)")]
        [SerializeField] private float _createRoomInputAnchorY = 180f;          // anchor center → 화면 가운데 위쪽
        [SerializeField] private float _createRoomMapsLabelAnchorY = 80f;       // anchor center → "맵을 선택하세요" (위로)
        [SerializeField] private float _createRoomMapsAreaAnchorY = -100f;      // anchor center → 카드 영역 중앙 (위로). label↔카드 여백 = input↔label 여백 ≈ 52
        [SerializeField] private float _createRoomMapCardWidth = 320f;
        [SerializeField] private float _createRoomMapCardHeight = 220f;
        [SerializeField] private float _createRoomMapCardGap = 60f;
        [SerializeField] private float _createRoomStatusTextAnchorY = -250f;    // anchor center → 카드 하단 / 버튼 상단 사이
        [SerializeField] private float _createRoomActionButtonBottomY = 200f;   // anchor bottom → 방 생성하기 (위로)
        [SerializeField] private float _createRoomBackMenuItemBottomY = 100f;   // anchor bottom → 이전으로 (위로)
        [SerializeField] private float _cyberButtonWidth = 300f;
        [SerializeField] private float _cyberButtonHeight = 70f;
        [SerializeField] private int _cyberButtonFontSize = 34;
        [SerializeField] private float _cyberInputWidth = 700f;
        [SerializeField] private float _cyberInputHeight = 60f;
        [SerializeField] private int _cyberInputFontSize = 36;
        [SerializeField] private int _cyberInputPlaceholderFontSize = 28;
        [SerializeField] private Color _cyberAccentColor = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 1f);
        [SerializeField] private Color _cyberPlaceholderColor = new Color(1f, 1f, 1f, 0.4f);
        [SerializeField] private Color _cyberColumnHeaderColor = new Color(1f, 1f, 1f, 0.5f);

        [Header("Fonts")]
        [SerializeField] private TMP_FontAsset _menuFont; // MainPanel 타이틀/버튼 전용. 미할당 시 TMP 기본 폰트 사용.

        // ── 패널 참조 ──
        private Canvas _canvas;
        private GameObject _nicknamePanel;
        private GameObject _mainPanel;
        private GameObject _createRoomPanel;
        private GameObject _findRoomPanel;
        private GameObject _lobbyPanel;

        // ── Nickname ──
        private TMP_InputField _nicknameInput;
        private TextMeshProUGUI _nicknameErrorText;
        private Coroutine _nicknameErrorFade;
        private MainMenu.CyberButton _nicknameStartButton;

        // ── Create Room ──
        private TMP_InputField _roomNameInput;
        private TextMeshProUGUI _createRoomErrorText;
        private Coroutine _createRoomErrorFade;
        private MainMenu.CyberButton _createRoomConfirmButton;
        private MainMenu.MapCardItem _universeMapCard;
        private MainMenu.MapCardItem _cityMapCard;

        // ── Find Room ──
        private Transform _roomListContent;
        private TextMeshProUGUI _searchStatusText;
        private TextMeshProUGUI _emptyStateText;
        private TextMeshProUGUI _findRoomNoticeText;

        // ── Lobby ──
        private TextMeshProUGUI _lobbyRoomNameText;
        private TextMeshProUGUI _lobbyPlayer1Text;
        private TextMeshProUGUI _lobbyPlayer2Text;
        private TextMeshProUGUI _lobbyCountdownText;
        private TextMeshProUGUI _lobbyReadyStatusText;
        private Button _readyButton;
        private TextMeshProUGUI _readyButtonText;
        private bool _isReady = false;
        private bool _readyRequestPending;

        private SettingsUI _settingsUI;

        // ── 상태 ──
        private string _currentRoomName;
        private string _pendingJoinSession; // 방 생성 후 자동 입장용
        private string _pendingCreateRoomName; // 로비 접속 후 자동 방 생성용

        // ── 알림 ──
        private TextMeshProUGUI _noticeText;
        private float _noticeTimer;
        private bool _menuDuplicatesDisabled;

        // ── Map Selector ──
        private const string MapPrefsKey = "Colosseum.UI.SelectedMap";
        private SelectableScene _selectedMap = SelectableScene.MVPArena;
        private Button _btnMapDuskCity;
        private Button _btnMapMVPArena;
        private TextMeshProUGUI _mapSelectorLabel;

        private void Start()
        {
            if (Application.isBatchMode) return;

            LoadSelectedMap();
            CreateCanvas();
            CreateNicknamePanel();
            CreateMainPanel();
            CreateCreateRoomPanel();
            CreateFindRoomPanel();
            CreateLobbyPanel();
            CreateNoticeText();
            _settingsUI = gameObject.AddComponent<SettingsUI>();

            if (NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.Nickname))
                ShowPanel(_mainPanel);
            else
                ShowPanel(_nicknamePanel);

            // 이벤트 구독
            NetworkManager.OnLobbyConnected += OnLobbyConnected;
            NetworkManager.OnLobbyFailed += OnLobbyFailed;
            NetworkManager.OnRoomListReceived += OnRoomListReceived;
            NetworkManager.OnRoomCreated += OnRoomCreated;
            NetworkManager.OnError += OnServerError;
            NetworkManager.OnGameRoomJoined += OnGameRoomJoined;
            NetworkManager.OnGameRoomFailed += OnGameRoomFailed;
            NetworkManager.OnPlayerJoinedRoom += OnPlayerJoined;
            NetworkManager.OnPlayerLeftRoom += OnPlayerLeft;
            NetworkManager.OnReadyUpdate += OnReadyUpdate;
            NetworkManager.OnLocalReadyStateChanged += OnLocalReadyStateChanged;
            NetworkManager.OnCountdownStart += OnCountdownStart;
            NetworkManager.OnCountdownTick += OnCountdownTick;
            NetworkManager.OnCountdownCancel += OnCountdownCancel;
            NetworkManager.OnGameStart += OnGameStart;
            NetworkManager.OnDisconnected += OnDisconnected;

            SceneManager.sceneLoaded += OnGameSceneLoaded;
        }

        private void OnDestroy()
        {
            NetworkManager.OnLobbyConnected -= OnLobbyConnected;
            NetworkManager.OnLobbyFailed -= OnLobbyFailed;
            NetworkManager.OnRoomListReceived -= OnRoomListReceived;
            NetworkManager.OnRoomCreated -= OnRoomCreated;
            NetworkManager.OnError -= OnServerError;
            NetworkManager.OnGameRoomJoined -= OnGameRoomJoined;
            NetworkManager.OnGameRoomFailed -= OnGameRoomFailed;
            NetworkManager.OnPlayerJoinedRoom -= OnPlayerJoined;
            NetworkManager.OnPlayerLeftRoom -= OnPlayerLeft;
            NetworkManager.OnReadyUpdate -= OnReadyUpdate;
            NetworkManager.OnLocalReadyStateChanged -= OnLocalReadyStateChanged;
            NetworkManager.OnCountdownStart -= OnCountdownStart;
            NetworkManager.OnCountdownTick -= OnCountdownTick;
            NetworkManager.OnCountdownCancel -= OnCountdownCancel;
            NetworkManager.OnGameStart -= OnGameStart;
            NetworkManager.OnDisconnected -= OnDisconnected;

            SceneManager.sceneLoaded -= OnGameSceneLoaded;
        }

        private void Update()
        {
            // 알림 타이머
            if (_noticeText != null && _noticeText.gameObject.activeSelf)
            {
                _noticeTimer -= Time.deltaTime;
                if (_noticeTimer <= 0f)
                    _noticeText.gameObject.SetActive(false);
            }
        }

        // ──────────────────────────────────────────────
        //  패널 전환
        // ──────────────────────────────────────────────

        private void ShowPanel(GameObject panel)
        {
            _nicknamePanel.SetActive(panel == _nicknamePanel);
            _mainPanel.SetActive(panel == _mainPanel);
            _createRoomPanel.SetActive(panel == _createRoomPanel);
            _findRoomPanel.SetActive(panel == _findRoomPanel);
            _lobbyPanel.SetActive(panel == _lobbyPanel);
            // 패널 전환 시 EventSystem 선택 해제 — 이전 선택이 유지되면 버튼이 selectedColor 상태로 남는다.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        // ──────────────────────────────────────────────
        //  Canvas + EventSystem
        // ──────────────────────────────────────────────

        private void CreateCanvas()
        {
            var canvasObj = new GameObject("MainMenuCanvas");
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f; // 가로/세로 균형 스케일링

            canvasObj.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }
        }

        // ──────────────────────────────────────────────
        //  0. Nickname Panel
        // ──────────────────────────────────────────────

        private void CreateNicknamePanel()
        {
            // A303-355: Beans 톤 풀 리디자인.
            // 구조: bg_main_arena → BeansLogo(top) → Input(center y=0) → StartButton(bottom y=200) → Error(input 아래).
            _nicknamePanel = CreatePanelBase("NicknamePanel");
            ApplyBeansBackground(_nicknamePanel);

            CreateBeansLogoTitle(_nicknamePanel.transform, pulse: _beansLogoPulse);

            // Input — anchor center, y=0 (화면 세로 가운데)
            var cyberInput = CreateCyberInputField(_nicknamePanel.transform, "사용하실 닉네임을 입력하세요", _cyberInputWidth, _cyberInputHeight);
            AnchorAtCenter(cyberInput.GetComponent<RectTransform>(), _cyberInputWidth, _cyberInputHeight, _cyberInputAnchorY);
            _nicknameInput = cyberInput.input;
            _nicknameInput.characterLimit = NicknameValidator.AsciiMaxLength;
            _nicknameInput.onSubmit.AddListener(_ => OnConfirmNickname());

            // Start button — anchor bottom, y=_cyberActionButtonAnchorY (화면 하단에서 위)
            _nicknameStartButton = CreateCyberButton(_nicknamePanel.transform, "시작하기",
                OnConfirmNickname,
                _cyberButtonWidth, _cyberButtonHeight, _cyberButtonFontSize);
            AnchorAtBottom(_nicknameStartButton.GetComponent<RectTransform>(), _cyberButtonWidth, _cyberButtonHeight, _cyberActionButtonAnchorY);
            _nicknameStartButton.Interactable = false;

            // Error slot — input 바로 아래, anchor center
            var errGO = new GameObject("NicknameError");
            errGO.transform.SetParent(_nicknamePanel.transform, false);
            var errRt = errGO.AddComponent<RectTransform>();
            errRt.anchorMin = new Vector2(0.5f, 0.5f);
            errRt.anchorMax = new Vector2(0.5f, 0.5f);
            errRt.pivot = new Vector2(0.5f, 0.5f);
            errRt.anchoredPosition = new Vector2(0f, _cyberInputAnchorY - 50f);
            errRt.sizeDelta = new Vector2(_cyberInputWidth, 30f);
            _nicknameErrorText = errGO.AddComponent<TextMeshProUGUI>();
            _nicknameErrorText.fontSize = 22;
            _nicknameErrorText.color = _errorTextColor;
            _nicknameErrorText.alignment = TextAlignmentOptions.Center;
            _nicknameErrorText.text = "";
            _nicknameErrorText.raycastTarget = false;
            if (_menuFont != null) _nicknameErrorText.font = _menuFont;

            _nicknameInput.onValueChanged.AddListener(text =>
            {
                bool valid = !string.IsNullOrWhiteSpace(text);
                _nicknameStartButton.Interactable = valid;
                if (valid && _nicknameErrorText != null)
                {
                    if (_nicknameErrorFade != null) { StopCoroutine(_nicknameErrorFade); _nicknameErrorFade = null; }
                    _nicknameErrorText.text = "";
                    _nicknameErrorText.color = _errorTextColor;
                }
            });
        }

        private static void AnchorAtCenter(RectTransform rt, float width, float height, float anchoredY)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, anchoredY);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static void AnchorAtBottom(RectTransform rt, float width, float height, float anchoredY)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, anchoredY);
            rt.sizeDelta = new Vector2(width, height);
        }

        private void OnConfirmNickname()
        {
            string nick = _nicknameInput != null ? _nicknameInput.text.Trim() : "";
            var result = NicknameValidator.Validate(nick);

            if (result != NicknameValidator.Result.Ok)
            {
                if (_nicknameErrorText != null)
                    ShowErrorWithFade(_nicknameErrorText, ToNicknameErrorMessage(result), ref _nicknameErrorFade);
                return;
            }

            NetworkManager.Instance?.SetNickname(nick);
            NetworkManager.Instance?.ConnectToLobby();
            ShowPanel(_mainPanel);
        }

        private static string ToNicknameErrorMessage(NicknameValidator.Result r)
            => NicknameValidator.ToKoreanMessage(r);

        // ──────────────────────────────────────────────
        //  1. Main Panel
        // ──────────────────────────────────────────────

        private void CreateMainPanel()
        {
            // A303-334: Beans 비주얼 톤 풀 리디자인.
            // 구조: Background → CharactersLayer(PeanyLeft, BeanyRight) → TitleBlock(BeansLogo)
            //       → MenuBlock(Create/Find/Settings/Quit) → BottomCenter(Version) → (옵션) DevControls.
            _mainPanel = CreatePanelBase("MainPanel");

            // CreatePanelBase 가 만든 background Image 를 bg_main_arena 로 덮어쓴다 (단색 → 일러스트).
            var panelImg = _mainPanel.GetComponent<Image>();
            if (_bgArenaSprite != null)
            {
                panelImg.sprite = _bgArenaSprite;
                panelImg.color = Color.white;
                panelImg.preserveAspect = false; // 화면에 stretch
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] _bgArenaSprite 미할당 — 단색 fallback");
            }
            panelImg.raycastTarget = false;

            // ── CharactersLayer (Logo 보다 뒤, 즉 더 깊은 z) ──
            var charsLayer = new GameObject("CharactersLayer");
            charsLayer.transform.SetParent(_mainPanel.transform, false);
            var charsRect = charsLayer.AddComponent<RectTransform>();
            charsRect.anchorMin = Vector2.zero;
            charsRect.anchorMax = Vector2.one;
            charsRect.offsetMin = Vector2.zero;
            charsRect.offsetMax = Vector2.zero;

            if (_peanyAimingSprite != null)
            {
                CreateMainMenuCharacter(charsLayer.transform, "PeanyLeft", _peanyAimingSprite,
                    _beansPeanyAnchor, _beansCharacterHeight,
                    _peanyIdleAmplitude, _peanyIdlePeriod, 0f);
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] _peanyAimingSprite 미할당 — Peany 미표시");
            }
            if (_beanyAimingSprite != null)
            {
                CreateMainMenuCharacter(charsLayer.transform, "BeanyRight", _beanyAimingSprite,
                    _beansBeanyAnchor, _beansCharacterHeight,
                    _beanyIdleAmplitude, _beanyIdlePeriod, _beanyIdleStartDelay);
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] _beanyAimingSprite 미할당 — Beany 미표시");
            }

            // ── TitleBlock (BeansLogo, 상단 가운데) ──
            if (_beansLogoSprite != null)
            {
                var titleBlock = new GameObject("TitleBlock");
                titleBlock.transform.SetParent(_mainPanel.transform, false);
                var titleRect = titleBlock.AddComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0.5f, 1f);
                titleRect.anchorMax = new Vector2(0.5f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.anchoredPosition = new Vector2(0f, -_beansLogoTopOffset);
                float aspect = _beansLogoSprite.rect.width / _beansLogoSprite.rect.height;
                titleRect.sizeDelta = new Vector2(_beansLogoHeight * aspect, _beansLogoHeight);

                var logoImg = titleBlock.AddComponent<Image>();
                logoImg.sprite = _beansLogoSprite;
                logoImg.preserveAspect = true;
                logoImg.raycastTarget = false;

                if (_beansLogoPulse)
                    StartCoroutine(BeansLogoPulseLoop(logoImg, _beansLogoPulsePeriod));
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] _beansLogoSprite 미할당 — 로고 미표시");
            }

            // ── MenuBlock (가운데, 4 메뉴) ──
            var menuBlock = new GameObject("MenuBlock");
            menuBlock.transform.SetParent(_mainPanel.transform, false);
            var menuRect = menuBlock.AddComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(0.5f, 0.5f);
            menuRect.anchorMax = new Vector2(0.5f, 0.5f);
            menuRect.pivot = new Vector2(0.5f, 0.5f);
            menuRect.anchoredPosition = new Vector2(0f, _beansMenuBlockOffsetY);
            menuRect.sizeDelta = new Vector2(480f, 400f);

            var menuVLG = menuBlock.AddComponent<VerticalLayoutGroup>();
            menuVLG.spacing = _beansMenuBlockSpacing;
            menuVLG.childAlignment = TextAnchor.MiddleCenter;
            menuVLG.childControlWidth = true;
            menuVLG.childControlHeight = false;
            menuVLG.childForceExpandWidth = true;
            menuVLG.childForceExpandHeight = false;

            var menuFitter = menuBlock.AddComponent<ContentSizeFitter>();
            menuFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateBeansMenuItem(menuBlock.transform, "Create Room", OnCreateRoomClicked);
            CreateBeansMenuItem(menuBlock.transform, "Find Room", OnFindRoomClicked);
            CreateBeansMenuItem(menuBlock.transform, "Settings", OnSettingsClicked);
            CreateBeansMenuItem(menuBlock.transform, "Quit", OnQuitClicked);

            // ── BottomCenter (Version) ──
            var bottomCenter = new GameObject("BottomCenter");
            bottomCenter.transform.SetParent(_mainPanel.transform, false);
            var bcRect = bottomCenter.AddComponent<RectTransform>();
            bcRect.anchorMin = new Vector2(0.5f, 0f);
            bcRect.anchorMax = new Vector2(0.5f, 0f);
            bcRect.pivot = new Vector2(0.5f, 0f);
            bcRect.anchoredPosition = new Vector2(0f, 30f);
            bcRect.sizeDelta = new Vector2(600f, 32f);

            string versionLine = string.IsNullOrEmpty(_beansVersionText)
                ? $"v{Application.version} · alpha"
                : $"{_beansVersionText} · v{Application.version}";
            CreateBeansBottomInfo(bottomCenter.transform, versionLine, 20, TextAlignmentOptions.Center);

            // ── Dev Controls (개발 전용 토글바) ──
#if UNITY_EDITOR
            if (_beansShowDevControls)
                CreateBeansDevControlsBar(_mainPanel.transform);
#endif
        }

        // ──────────────────────────────────────────────
        //  2. Create Room Panel
        // ──────────────────────────────────────────────

        private void CreateCreateRoomPanel()
        {
            // A303-356 / A303-354: Beans 톤 + 맵 선택 카드.
            // 구조: bg → title(top) → Input(center) → "맵을 선택하세요" → MapCards(Universe/City)
            //       → Status(input/cards/button 사이) → CreateButton(bottom) → BackMenuItem(bottom).
            // 위치는 모두 SerializeField (`_createRoom*AnchorY` / `_createRoomMap*`) 로 Inspector 튜닝 가능.
            _createRoomPanel = CreatePanelBase("CreateRoomPanel");
            ApplyBeansBackground(_createRoomPanel);

            CreateCyberTitleSprite(_createRoomPanel.transform, _createRoomTitleSprite, "UI/MainMenu/create_room_title", "CreateRoomTitle", _createRoomTitleAnchoredY);

            // Input — anchor center, y=_createRoomInputAnchorY
            var cyberInput = CreateCyberInputField(_createRoomPanel.transform, "방 이름을 입력하세요", _cyberInputWidth, _cyberInputHeight);
            AnchorAtCenter(cyberInput.GetComponent<RectTransform>(), _cyberInputWidth, _cyberInputHeight, _createRoomInputAnchorY);
            _roomNameInput = cyberInput.input;
            _roomNameInput.characterLimit = 20;
            _roomNameInput.onSubmit.AddListener(_ => { if (_createRoomConfirmButton != null && _createRoomConfirmButton.Interactable) OnConfirmCreateRoom(); });

            // "맵을 선택하세요" 라벨
            var mapsLabelGO = new GameObject("MapsLabel");
            mapsLabelGO.transform.SetParent(_createRoomPanel.transform, false);
            var mapsLabelRt = mapsLabelGO.AddComponent<RectTransform>();
            mapsLabelRt.anchorMin = new Vector2(0.5f, 0.5f);
            mapsLabelRt.anchorMax = new Vector2(0.5f, 0.5f);
            mapsLabelRt.pivot = new Vector2(0.5f, 0.5f);
            mapsLabelRt.anchoredPosition = new Vector2(0f, _createRoomMapsLabelAnchorY);
            mapsLabelRt.sizeDelta = new Vector2(400f, 36f);
            var mapsLabelTmp = mapsLabelGO.AddComponent<TextMeshProUGUI>();
            mapsLabelTmp.text = "맵을 선택하세요";
            mapsLabelTmp.fontSize = 26;
            mapsLabelTmp.color = Color.white;
            mapsLabelTmp.fontStyle = FontStyles.Bold;
            mapsLabelTmp.alignment = TextAlignmentOptions.Center;
            mapsLabelTmp.raycastTarget = false;
            if (_menuFont != null) mapsLabelTmp.font = _menuFont;

            // Map cards (HLG, 가운데, anchor center)
            var mapsGO = new GameObject("MapCards");
            mapsGO.transform.SetParent(_createRoomPanel.transform, false);
            var mapsRt = mapsGO.AddComponent<RectTransform>();
            float mapsAreaWidth = _createRoomMapCardWidth * 2f + _createRoomMapCardGap;
            mapsRt.anchorMin = new Vector2(0.5f, 0.5f);
            mapsRt.anchorMax = new Vector2(0.5f, 0.5f);
            mapsRt.pivot = new Vector2(0.5f, 0.5f);
            mapsRt.anchoredPosition = new Vector2(0f, _createRoomMapsAreaAnchorY);
            mapsRt.sizeDelta = new Vector2(mapsAreaWidth, _createRoomMapCardHeight);

            var mapsHLG = mapsGO.AddComponent<HorizontalLayoutGroup>();
            mapsHLG.spacing = _createRoomMapCardGap;
            mapsHLG.childAlignment = TextAnchor.MiddleCenter;
            mapsHLG.childControlWidth = false;
            mapsHLG.childControlHeight = false;
            mapsHLG.childForceExpandWidth = false;
            mapsHLG.childForceExpandHeight = false;

            _universeMapCard = BuildMapCard(mapsGO.transform, "Universe Map",
                "UI/MainMenu/map_universe_color", "UI/MainMenu/map_universe_gray", isSelectedDefault: true);
            _cityMapCard = BuildMapCard(mapsGO.transform, "City Map",
                "UI/MainMenu/map_city_color", "UI/MainMenu/map_city_gray", isSelectedDefault: false);

            // 카드 클릭 — 두 카드 토글 + _selectedMap 동기 (A303-361: Universe = MVPArena, City = DuskCity)
            _universeMapCard.OnClicked += () => SelectMap(_universeMapCard, _cityMapCard, SelectableScene.MVPArena);
            _cityMapCard.OnClicked     += () => SelectMap(_cityMapCard, _universeMapCard, SelectableScene.DuskCity);

            // Status/Error slot — 카드 하단 / 버튼 상단 사이.
            // 평소 빈 텍스트, "방을 생성하는 중..." / "방 이름을 입력하세요." 등이 SetCreateRoomStatus 로 표시됨.
            var errGO = new GameObject("CreateRoomError");
            errGO.transform.SetParent(_createRoomPanel.transform, false);
            var errRt = errGO.AddComponent<RectTransform>();
            errRt.anchorMin = new Vector2(0.5f, 0.5f);
            errRt.anchorMax = new Vector2(0.5f, 0.5f);
            errRt.pivot = new Vector2(0.5f, 0.5f);
            errRt.anchoredPosition = new Vector2(0f, _createRoomStatusTextAnchorY);
            errRt.sizeDelta = new Vector2(_cyberInputWidth, 30f);
            _createRoomErrorText = errGO.AddComponent<TextMeshProUGUI>();
            _createRoomErrorText.fontSize = 22;
            _createRoomErrorText.color = _textColor;
            _createRoomErrorText.alignment = TextAlignmentOptions.Center;
            _createRoomErrorText.text = "";
            _createRoomErrorText.raycastTarget = false;
            if (_menuFont != null) _createRoomErrorText.font = _menuFont;

            // Create button — anchor bottom, y=_createRoomActionButtonBottomY
            float createBtnWidth = _cyberButtonWidth + 40f;
            _createRoomConfirmButton = CreateCyberButton(_createRoomPanel.transform, "방 생성하기",
                OnConfirmCreateRoom,
                createBtnWidth, _cyberButtonHeight, _cyberButtonFontSize);
            AnchorAtBottom(_createRoomConfirmButton.GetComponent<RectTransform>(), createBtnWidth, _cyberButtonHeight, _createRoomActionButtonBottomY);

            // 이전으로 — 화면 하단 가까이 (anchor bottom, y=_createRoomBackMenuItemBottomY)
            var back = CreateBackMenuItem(_createRoomPanel.transform, "이전으로", OnCreateRoomBack, fontSize: 28);
            var backRt = back.GetComponent<RectTransform>();
            AnchorAtBottom(backRt, 240f, 48f, _createRoomBackMenuItemBottomY);

            _roomNameInput.onValueChanged.AddListener(_ => UpdateCreateRoomButtonState());
            UpdateCreateRoomButtonState();
        }

        // ──────────────────────────────────────────────
        //  A303-354 맵 선택 카드 헬퍼
        // ──────────────────────────────────────────────

        private MainMenu.MapCardItem BuildMapCard(Transform parent, string label,
            string colorResourcePath, string grayResourcePath, bool isSelectedDefault)
        {
            Sprite colorSprite = Resources.Load<Sprite>(colorResourcePath);
            Sprite graySprite  = Resources.Load<Sprite>(grayResourcePath);

            var go = new GameObject($"MapCard_{label}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(_createRoomMapCardWidth, _createRoomMapCardHeight);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = _createRoomMapCardWidth;
            le.preferredHeight = _createRoomMapCardHeight;

            // raycast hit zone
            var hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            // Thumbnail Image (color/gray sprite swap)
            var thumbGO = new GameObject("Thumbnail");
            thumbGO.transform.SetParent(go.transform, false);
            var thumbRt = thumbGO.AddComponent<RectTransform>();
            thumbRt.anchorMin = Vector2.zero;
            thumbRt.anchorMax = Vector2.one;
            thumbRt.offsetMin = Vector2.zero;
            thumbRt.offsetMax = Vector2.zero;
            var thumbImg = thumbGO.AddComponent<Image>();
            thumbImg.sprite = isSelectedDefault ? colorSprite : graySprite;
            thumbImg.preserveAspect = false;
            thumbImg.raycastTarget = false;
            if (colorSprite == null || graySprite == null)
                Debug.LogWarning($"[MainMenuUI] MapCard '{label}' sprite 로드 실패 — Resources/{colorResourcePath} 또는 {grayResourcePath}");

            // PNG 자체가 시안 테두리 + 카드 이름 라벨을 포함 — 추가 outline / TMP 없음.
            // 선택/비선택 표시는 MapCardItem.ApplyState() 가 color↔gray sprite 만 교체.

            var card = go.AddComponent<MainMenu.MapCardItem>();
            card.thumbnail = thumbImg;
            card.label = null;          // PNG 가 라벨 포함 — label TMP 미사용
            card.outlineImages = null;  // PNG 가 테두리 포함 — 4-strip outline 미사용
            card.colorSprite = colorSprite;
            card.graySprite = graySprite;
            card.outlineColor = _cyberAccentColor;
            card.IsSelected = isSelectedDefault;
            return card;
        }

        /// <summary>
        /// 카드 클릭 → 선택한 카드만 IsSelected=true, 나머지 false.
        /// _selectedMap 동기 갱신 — Universe = MVPArena, City = DuskCity (A303-361).
        /// </summary>
        private void SelectMap(MainMenu.MapCardItem selected, MainMenu.MapCardItem other, SelectableScene scene)
        {
            if (selected != null) selected.IsSelected = true;
            if (other    != null) other.IsSelected    = false;
            if (_selectedMap != scene)
            {
                _selectedMap = scene;
                SaveSelectedMap();
                UpdateMapSelectorVisual();
            }
        }

        // ──────────────────────────────────────────────
        //  3. Find Room Panel
        // ──────────────────────────────────────────────

        private void CreateFindRoomPanel()
        {
            // A303-356: Beans 톤 통일.
            // 구조: bg → title(top) → SearchStatus → RoomListPanel{ColumnHeader, Divider, ScrollList}
            //       → NoticeText → Refresh/Back (CyberButton 작게).
            _findRoomPanel = CreatePanelBase("FindRoomPanel");
            ApplyBeansBackground(_findRoomPanel);

            CreateCyberTitleSprite(_findRoomPanel.transform, _findRoomTitleSprite, "UI/MainMenu/find_room_title", "FindRoomTitle");

            const float panelWidth = 1152f;   // 1920 * 0.6 → 화면 가로 60%
            const float panelHeight = 500f;
            const float headerRowHeight = 40f;

            // ── SearchStatus (title 과 RoomListPanel 사이) ──
            var statusGO = new GameObject("SearchStatus");
            statusGO.transform.SetParent(_findRoomPanel.transform, false);
            var statusRt = statusGO.AddComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0.5f, 1f);
            statusRt.anchorMax = new Vector2(0.5f, 1f);
            statusRt.pivot = new Vector2(0.5f, 1f);
            statusRt.anchoredPosition = new Vector2(0f, _findRoomStatsTextAnchorY);
            statusRt.sizeDelta = new Vector2(900f, 26f);
            _searchStatusText = statusGO.AddComponent<TextMeshProUGUI>();
            _searchStatusText.text = "로비 접속 중...";
            _searchStatusText.fontSize = 18;
            _searchStatusText.color = new Color(1f, 1f, 1f, 0.45f);
            _searchStatusText.alignment = TextAlignmentOptions.Center;
            _searchStatusText.raycastTarget = false;
            if (_menuFont != null) _searchStatusText.font = _menuFont;

            // ── RoomListPanel (다크 컨테이너; 안에 ColumnHeader + Divider + ScrollList 통합) ──
            var panelGO = new GameObject("RoomListPanel");
            panelGO.transform.SetParent(_findRoomPanel.transform, false);
            var panelRt = panelGO.AddComponent<RectTransform>();
            AnchorAtCenter(panelRt, panelWidth, panelHeight, -40f);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.04f, 0.04f, 0.12f, 0.55f);
            panelImg.raycastTarget = false;

            var panelVLG = panelGO.AddComponent<VerticalLayoutGroup>();
            panelVLG.spacing = 6f;
            panelVLG.padding = new RectOffset(0, 0, 14, 14);
            panelVLG.childAlignment = TextAnchor.UpperCenter;
            panelVLG.childControlWidth = true;
            panelVLG.childControlHeight = true;
            panelVLG.childForceExpandWidth = true;
            panelVLG.childForceExpandHeight = false;

            // ── ColumnHeader (RoomListPanel 안 첫 row) ──
            var headerGO = new GameObject("ColumnHeader");
            headerGO.transform.SetParent(panelGO.transform, false);
            headerGO.AddComponent<RectTransform>();
            var headerLE = headerGO.AddComponent<LayoutElement>();
            headerLE.preferredHeight = headerRowHeight;
            headerLE.flexibleHeight = 0f;

            var headerHLG = headerGO.AddComponent<HorizontalLayoutGroup>();
            headerHLG.spacing = 0f;
            headerHLG.padding = new RectOffset(40, 40, 0, 0);
            headerHLG.childAlignment = TextAnchor.MiddleLeft;
            headerHLG.childControlWidth = true;
            headerHLG.childControlHeight = true;
            headerHLG.childForceExpandWidth = false;
            headerHLG.childForceExpandHeight = true;
            CreateRoomColumnText(headerGO.transform, "방 이름", 1f, TextAlignmentOptions.MidlineLeft, 0f,    18, false, _cyberColumnHeaderColor);
            CreateRoomColumnText(headerGO.transform, "인원",   0f, TextAlignmentOptions.Center,      180f,  18, false, _cyberColumnHeaderColor);
            CreateRoomColumnText(headerGO.transform, "상태",   0f, TextAlignmentOptions.Center,      180f,  18, false, _cyberColumnHeaderColor);

            // ── Divider (얇은 시안 가로 라인) ──
            var dividerGO = new GameObject("Divider");
            dividerGO.transform.SetParent(panelGO.transform, false);
            dividerGO.AddComponent<RectTransform>();
            var divLE = dividerGO.AddComponent<LayoutElement>();
            divLE.preferredHeight = 1f;
            divLE.flexibleHeight = 0f;
            var divImg = dividerGO.AddComponent<Image>();
            divImg.color = new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.35f);
            divImg.raycastTarget = false;

            // ── ScrollArea (남은 세로 공간 채움) ──
            var scrollGO = new GameObject("ScrollArea");
            scrollGO.transform.SetParent(panelGO.transform, false);
            scrollGO.AddComponent<RectTransform>();
            var scrollLE = scrollGO.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;
            scrollLE.minHeight = 200f;

            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            // 마우스 휠 delta(보통 0.1~0.5) × scrollSensitivity = 이동 픽셀.
            // Unity default 1.0 / 직전 30 모두 너무 작음. _findRoomScrollSensitivity (default 100) 로 적용 — 일반 웹 휠 체감.
            scrollRect.scrollSensitivity = _findRoomScrollSensitivity;

            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRt = viewportGO.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            var viewportImg = viewportGO.AddComponent<Image>();
            viewportImg.color = Color.white;
            // 휠 이벤트를 ScrollRect 가 받으려면 viewport 영역 어딘가가 raycast target 이어야 함.
            // 카드 사이 빈 공간/카드 위 휠 모두 viewport 가 잡아 ScrollRect 에 IScrollHandler 로 bubble.
            viewportImg.raycastTarget = true;
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRt = contentGO.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            var contentVLG = contentGO.AddComponent<VerticalLayoutGroup>();
            contentVLG.spacing = 8f;
            contentVLG.childControlWidth = true;
            contentVLG.childControlHeight = false;
            contentVLG.childForceExpandWidth = true;
            contentVLG.childForceExpandHeight = false;
            contentVLG.padding = new RectOffset(0, 0, 8, 8);
            var contentFitter = contentGO.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRt;
            scrollRect.viewport = viewportRt;
            _roomListContent = contentGO.transform;

            // ── EmptyState (overlay, 방 0개 일 때만 active) ──
            var emptyGO = new GameObject("EmptyState");
            emptyGO.transform.SetParent(scrollGO.transform, false);
            var emptyRt = emptyGO.AddComponent<RectTransform>();
            emptyRt.anchorMin = Vector2.zero;
            emptyRt.anchorMax = Vector2.one;
            emptyRt.offsetMin = Vector2.zero;
            emptyRt.offsetMax = Vector2.zero;
            _emptyStateText = emptyGO.AddComponent<TextMeshProUGUI>();
            _emptyStateText.text = "No rooms — Create one!";
            _emptyStateText.fontSize = 26;
            _emptyStateText.color = new Color(1f, 1f, 1f, 0.5f);
            _emptyStateText.alignment = TextAlignmentOptions.Center;
            _emptyStateText.raycastTarget = false;
            if (_menuFont != null) _emptyStateText.font = _menuFont;

            // ── NoticeText (panel 과 하단 버튼 사이) ──
            var noticeGO = new GameObject("FindRoomNotice");
            noticeGO.transform.SetParent(_findRoomPanel.transform, false);
            var noticeRt = noticeGO.AddComponent<RectTransform>();
            noticeRt.anchorMin = new Vector2(0.5f, 0f);
            noticeRt.anchorMax = new Vector2(0.5f, 0f);
            noticeRt.pivot = new Vector2(0.5f, 0f);
            noticeRt.anchoredPosition = new Vector2(0f, _findRoomBottomButtonsAnchorY + 70f);
            noticeRt.sizeDelta = new Vector2(900f, 26f);
            _findRoomNoticeText = noticeGO.AddComponent<TextMeshProUGUI>();
            _findRoomNoticeText.text = "";
            _findRoomNoticeText.fontSize = 20;
            _findRoomNoticeText.color = _textColor;
            _findRoomNoticeText.alignment = TextAlignmentOptions.Center;
            _findRoomNoticeText.raycastTarget = false;
            if (_menuFont != null) _findRoomNoticeText.font = _menuFont;

            // ── Refresh / Back (CyberButton, 메인 액션보다 작음, 가운데 정렬 + gap 30) ──
            const float subBtnWidth = 180f;
            const float subBtnHeight = 44f;
            const int subBtnFontSize = 22;
            const float subBtnHalfGap = 15f;  // 두 버튼 사이 gap 30 = ±15 offset

            var refresh = CreateCyberButton(_findRoomPanel.transform, "새로고침",
                OnRefreshRooms, subBtnWidth, subBtnHeight, subBtnFontSize);
            var refreshRt = refresh.GetComponent<RectTransform>();
            refreshRt.anchorMin = new Vector2(0.5f, 0f);
            refreshRt.anchorMax = new Vector2(0.5f, 0f);
            refreshRt.pivot = new Vector2(0.5f, 0f);
            refreshRt.anchoredPosition = new Vector2(-(subBtnWidth * 0.5f + subBtnHalfGap), _findRoomBottomButtonsAnchorY);
            refreshRt.sizeDelta = new Vector2(subBtnWidth, subBtnHeight);

            var back = CreateCyberButton(_findRoomPanel.transform, "이전으로",
                OnFindRoomBack, subBtnWidth, subBtnHeight, subBtnFontSize);
            var backRt = back.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0.5f, 0f);
            backRt.anchorMax = new Vector2(0.5f, 0f);
            backRt.pivot = new Vector2(0.5f, 0f);
            backRt.anchoredPosition = new Vector2((subBtnWidth * 0.5f + subBtnHalfGap), _findRoomBottomButtonsAnchorY);
            backRt.sizeDelta = new Vector2(subBtnWidth, subBtnHeight);
        }

        private GameObject CreateAnchoredButton(Transform parent, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color bgColor,
            UnityEngine.Events.UnityAction onClick,
            Color? hoverBgColor = null, Color? textColor = null,
            Color? hoverTextColor = null, TMP_FontAsset font = null, int fontSize = 26)
        {
            var btnObj = new GameObject($"Btn_{label}");
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = Color.white;
            btnImage.sprite = GetRoundedRectSprite();
            btnImage.type = Image.Type.Sliced;
            btnImage.pixelsPerUnitMultiplier = 1f;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(onClick);

            bool isTransparent = bgColor.a < 0.01f;
            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = hoverBgColor ?? (isTransparent ? Color.clear : Color.Lerp(bgColor, Color.white, 0.15f));
            colors.pressedColor = isTransparent ? Color.clear : Color.Lerp(bgColor, Color.black, 0.15f);
            colors.selectedColor = bgColor;
            colors.colorMultiplier = 1f;
            btn.colors = colors;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Color resolvedTextColor = textColor ?? _bgColor;
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = resolvedTextColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            var resolvedFont = font ?? _menuFont;
            if (resolvedFont != null) tmp.font = resolvedFont;

            if (hoverTextColor.HasValue)
            {
                Color normalTextCol = resolvedTextColor;
                Color hoverTextCol = hoverTextColor.Value;
                var trigger = btnObj.AddComponent<EventTrigger>();

                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => tmp.color = hoverTextCol);
                trigger.triggers.Add(enter);

                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => tmp.color = normalTextCol);
                trigger.triggers.Add(exit);

                // 패널 전환으로 SetActive(false) 시 PointerExit 미발화 → hover 색 stuck 방지.
                // textOnly 버튼과 동일한 OnEnable 복원 컴포넌트 적용.
                var resetter = btnObj.AddComponent<HoverStateResetter>();
                resetter.Setup(tmp, label, normalTextCol, tmp.outlineColor);
            }

            return btnObj;
        }

        // ──────────────────────────────────────────────
        //  4. Lobby Panel (방 내부 대기실)
        // ──────────────────────────────────────────────

        private void CreateLobbyPanel()
        {
            _lobbyPanel = CreatePanelBase("LobbyPanel");

            var container = CreateVerticalContainer(_lobbyPanel.transform, _buttonWidth);

            // 방 이름
            var roomNameObj = new GameObject("LobbyRoomName");
            roomNameObj.transform.SetParent(container.transform, false);
            var rnLE = roomNameObj.AddComponent<LayoutElement>();
            rnLE.preferredHeight = _pageTitleFontSize + 20f;
            rnLE.preferredWidth = _buttonWidth;
            _lobbyRoomNameText = roomNameObj.AddComponent<TextMeshProUGUI>();
            _lobbyRoomNameText.fontSize = _pageTitleFontSize;
            _lobbyRoomNameText.color = _goldColor;
            _lobbyRoomNameText.alignment = TextAlignmentOptions.Center;
            _lobbyRoomNameText.fontStyle = FontStyles.Bold;
            if (_menuFont != null) _lobbyRoomNameText.font = _menuFont;

            CreateSpacer(container.transform, 20f);

            // 플레이어 표시
            CreateLabel(container.transform, "플레이어", _backButtonFontSize, _placeholderColor, _menuFont);
            CreateSpacer(container.transform, 8f);

            _lobbyPlayer1Text = CreatePlayerSlot(container.transform, "기다리는 중...");
            CreateSpacer(container.transform, 4f);
            _lobbyPlayer2Text = CreatePlayerSlot(container.transform, "기다리는 중...");

            CreateSpacer(container.transform, 15f);

            // Ready 상태 텍스트
            var readyStatusObj = new GameObject("ReadyStatus");
            readyStatusObj.transform.SetParent(container.transform, false);
            var rsLE = readyStatusObj.AddComponent<LayoutElement>();
            rsLE.preferredHeight = _backButtonFontSize + 10f;
            rsLE.preferredWidth = _buttonWidth;
            _lobbyReadyStatusText = readyStatusObj.AddComponent<TextMeshProUGUI>();
            _lobbyReadyStatusText.text = "준비: 0/0";
            _lobbyReadyStatusText.fontSize = _backButtonFontSize;
            _lobbyReadyStatusText.color = _placeholderColor;
            _lobbyReadyStatusText.alignment = TextAlignmentOptions.Center;
            if (_menuFont != null) _lobbyReadyStatusText.font = _menuFont;

            CreateSpacer(container.transform, 10f);

            // 카운트다운 텍스트
            var cdObj = new GameObject("Countdown");
            cdObj.transform.SetParent(container.transform, false);
            var cdLE = cdObj.AddComponent<LayoutElement>();
            cdLE.preferredHeight = 140f;
            cdLE.preferredWidth = _buttonWidth;
            _lobbyCountdownText = cdObj.AddComponent<TextMeshProUGUI>();
            _lobbyCountdownText.fontSize = 120;
            _lobbyCountdownText.color = _goldColor;
            _lobbyCountdownText.alignment = TextAlignmentOptions.Center;
            _lobbyCountdownText.fontStyle = FontStyles.Bold;
            if (_menuFont != null) _lobbyCountdownText.font = _menuFont;
            cdObj.SetActive(false);

            // Ready 버튼
            var readyObj = new GameObject("ReadyButton");
            readyObj.transform.SetParent(container.transform, false);
            var readyLE = readyObj.AddComponent<LayoutElement>();
            readyLE.preferredWidth = _buttonWidth;
            readyLE.preferredHeight = 60f;

            var readyImage = readyObj.AddComponent<Image>();
            readyImage.color = new Color(0.1f, 0.4f, 0.1f, 0.95f);

            _readyButton = readyObj.AddComponent<Button>();
            _readyButton.targetGraphic = readyImage;
            _readyButton.onClick.AddListener(OnReadyClicked);

            var readyColors = _readyButton.colors;
            readyColors.normalColor = new Color(0.1f, 0.4f, 0.1f, 0.95f);
            readyColors.highlightedColor = new Color(0.15f, 0.55f, 0.15f, 1f);
            readyColors.pressedColor = new Color(0.2f, 0.65f, 0.2f, 1f);
            _readyButton.colors = readyColors;

            var readyTextObj = new GameObject("Text");
            readyTextObj.transform.SetParent(readyObj.transform, false);
            var rtRect = readyTextObj.AddComponent<RectTransform>();
            rtRect.anchorMin = Vector2.zero;
            rtRect.anchorMax = Vector2.one;
            rtRect.offsetMin = Vector2.zero;
            rtRect.offsetMax = Vector2.zero;
            _readyButtonText = readyTextObj.AddComponent<TextMeshProUGUI>();
            _readyButtonText.text = "준비";
            _readyButtonText.fontSize = 30;
            _readyButtonText.color = Color.white;
            _readyButtonText.alignment = TextAlignmentOptions.Center;
            _readyButtonText.fontStyle = FontStyles.Bold;

            CreateSpacer(container.transform, 10f);
            CreateStyledButton(
                container.transform,
                "< 방 나가기",
                OnLeaveRoomClicked,
                _menuFont,
                TextAlignmentOptions.Center,
                textOnly: true,
                overrideTextColor: _backButtonColor,
                fontSizeOverride: _backButtonFontSize,
                showHoverBullet: false);
        }

        // ──────────────────────────────────────────────
        //  Main Panel 콜백
        // ──────────────────────────────────────────────

        private void OnCreateRoomClicked()
        {
            ShowPanel(_createRoomPanel);
            if (_roomNameInput != null)
                _roomNameInput.text = "";
            UpdateCreateRoomButtonState();
            // 로비 접속은 Create 버튼 클릭 시 처리 (OnConfirmCreateRoom)
        }

        private void OnFindRoomClicked()
        {
            ShowPanel(_findRoomPanel);
            ClearRoomList();
            SetFindRoomStatus("", isError: false);

            if (_searchStatusText != null)
                _searchStatusText.text = "로비 접속 중...";

            if (NetworkManager.Instance == null) return;

            if (NetworkManager.Instance.IsInLobby)
            {
                if (_searchStatusText != null)
                    _searchStatusText.text = "방 목록 불러오는 중...";
                NetworkManager.Instance.RequestRoomList();
            }
            else
            {
                NetworkManager.Instance.ConnectToLobby();
            }
        }

#if UNITY_EDITOR
        private void OnDevModeClicked()
        {
            // Dev Mode는 기존 Host 모드 (서버 불필요)
            NetworkManager.Instance?.DisconnectAll();

            // Phase 1 미머지 또는 Build Settings 미등록 상태에서 LoadScene이 ArgumentException으로 throw하면
            // 사용자 피드백 0의 silent dead button 발생 — 사전 차단 + SampleScene fallback.
            var path = _selectedMap.ToScenePath();
            if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
            {
                ShowNotice($"{_selectedMap.ToDisplayName()} not in Build Settings yet — falling back to MVP Arena.");
                _selectedMap = SelectableScene.MVPArena;
                SaveSelectedMap();
                UpdateMapSelectorVisual();
                path = _selectedMap.ToScenePath();
            }

            NetworkManager.AutoStartDevMode = true;
            NetworkManager.AutoStartDevModeScene = _selectedMap;
            SceneManager.LoadScene(path);
        }
#endif

        private void OnSettingsClicked()
        {
            if (_settingsUI != null)
                _settingsUI.Show();
        }

        private void OnQuitClicked()
        {
            Debug.Log("[Colosseum] Quit Game");
            NetworkManager.Instance?.DisconnectAll();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ──────────────────────────────────────────────
        //  Create Room 콜백
        // ──────────────────────────────────────────────

        private void OnConfirmCreateRoom()
        {
            string roomName = _roomNameInput != null ? _roomNameInput.text.Trim() : "";
            if (string.IsNullOrEmpty(roomName))
            {
                SetCreateRoomStatus("방 이름을 입력하세요.", isError: true);
                return;
            }

            if (NetworkManager.Instance == null) return;

            if (!NetworkManager.Instance.IsInLobby)
            {
                // 로비 미접속 → 접속 후 자동으로 방 생성
                _pendingCreateRoomName = roomName;
                SetCreateRoomStatus("서버에 연결 중...", isError: false);
                NetworkManager.Instance.ConnectToLobby();
                return;
            }

            _currentRoomName = roomName;
            NetworkManager.Instance.SetCurrentRoomName(roomName);
            NetworkManager.Instance.RequestCreateRoom(roomName, _selectedMap);
            SetCreateRoomStatus("방을 생성하는 중...", isError: false);
        }

        // CreateRoom 패널의 상태 메시지(검증 에러 / 진행 안내)를
        // 입력 필드와 액션 버튼 사이의 _createRoomErrorText 슬롯에 표시.
        private void SetCreateRoomStatus(string message, bool isError)
        {
            if (_createRoomErrorText == null) return;
            if (_createRoomErrorFade != null) { StopCoroutine(_createRoomErrorFade); _createRoomErrorFade = null; }
            _createRoomErrorText.text = message;
            _createRoomErrorText.color = isError ? _errorTextColor : _textColor;
        }

        private void UpdateCreateRoomButtonState()
        {
            bool hasLobbyConnection = NetworkManager.Instance != null && NetworkManager.Instance.IsInLobby;
            bool hasRoomName = _roomNameInput != null && !string.IsNullOrWhiteSpace(_roomNameInput.text);

            if (_createRoomConfirmButton != null)
                _createRoomConfirmButton.Interactable = hasLobbyConnection && hasRoomName;

            if (_createRoomErrorText == null || _createRoomPanel == null || !_createRoomPanel.activeSelf)
                return;

            if (!hasLobbyConnection)
            {
                SetCreateRoomStatus(CreateRoomLobbyConnectingMessage, isError: false);
                return;
            }

            if (_createRoomErrorText.text == CreateRoomLobbyConnectingMessage || string.IsNullOrWhiteSpace(_createRoomErrorText.text))
                SetCreateRoomStatus("", isError: false);
        }

        private void OnCreateRoomBack()
        {
            _pendingCreateRoomName = null; // 예약된 방 생성 취소
            ShowPanel(_mainPanel);
        }

        // ──────────────────────────────────────────────
        //  Find Room 콜백
        // ──────────────────────────────────────────────

        private void OnRefreshRooms()
        {
            ClearRoomList();
            if (_searchStatusText != null)
                _searchStatusText.text = "방 목록 불러오는 중...";

            if (NetworkManager.Instance == null) return;

            if (NetworkManager.Instance.IsInLobby)
            {
                NetworkManager.Instance.RequestRoomList();
            }
            else
            {
                // 로비 미접속 → 재접속 시도
                if (_searchStatusText != null)
                    _searchStatusText.text = "로비 재접속 중...";
                NetworkManager.Instance.ConnectToLobby();
            }
        }

        private void OnFindRoomBack()
        {
            ShowPanel(_mainPanel);
        }

        private void RefreshRoomListUI(string json)
        {
            ClearRoomList();

            // 간단한 JSON 파싱 (서버에서 보낸 배열)
            // [{"name":"..","session":"..","players":0,"max":2,"status":"waiting"},...]
            var rooms = ParseRoomList(json);

            if (rooms.Count == 0)
            {
                if (_searchStatusText != null)
                    _searchStatusText.text = "열려 있는 방이 없습니다.";
                return;
            }

            int availableCount = 0;
            foreach (var r in rooms)
            {
                bool full = r.players >= r.max;
                bool playing = r.status == "playing";
                bool countdown = r.status == "countdown";
                if (!full && !playing && !countdown) availableCount++;
            }
            if (_searchStatusText != null)
                _searchStatusText.text = $"전체 방 {rooms.Count}개  ·  <color=#FFB347>입장 가능 {availableCount}개</color>";

            if (_emptyStateText != null) _emptyStateText.gameObject.SetActive(false);

            foreach (var room in rooms)
            {
                CreateRoomListItem(room);
            }
        }

        private void CreateRoomListItem(RoomInfo room)
        {
            if (_roomListContent == null) return;

            // A303-356: 카드 프레임 없는 3-column row. RoomCardItem 이 hover/scale/색 분기를 관리.
            var itemGO = new GameObject($"RoomCard_{room.name}");
            itemGO.transform.SetParent(_roomListContent, false);

            var le = itemGO.AddComponent<LayoutElement>();
            le.preferredHeight = 58f;

            // 투명 raycast Image — row 전체 hover/클릭 hot zone.
            var bg = itemGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = true;

            var hlg = itemGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 0f;
            hlg.padding = new RectOffset(40, 40, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var nameTmp = CreateRoomColumnText(itemGO.transform, room.name, 1f,
                TextAlignmentOptions.MidlineLeft, 0f, 28, true, Color.white);
            var capTmp = CreateRoomColumnText(itemGO.transform, "", 0f,
                TextAlignmentOptions.Center, 180f, 24, false, _cyberAccentColor);
            var statusTmp = CreateRoomColumnText(itemGO.transform, "", 0f,
                TextAlignmentOptions.Center, 180f, 24, false, _cyberAccentColor);

            var card = itemGO.AddComponent<MainMenu.RoomCardItem>();
            card.nameLabel = nameTmp;
            card.capacityLabel = capTmp;
            card.statusLabel = statusTmp;

            MainMenu.RoomCardItem.RoomStatus rs;
            if (room.players >= room.max) rs = MainMenu.RoomCardItem.RoomStatus.Full;
            else if (room.status == "playing" || room.status == "countdown") rs = MainMenu.RoomCardItem.RoomStatus.Playing;
            else rs = MainMenu.RoomCardItem.RoomStatus.Waiting;

            string sName = room.session;
            string rName = room.name;
            card.Setup(room.name, room.players, room.max, rs, () => OnJoinRoom(sName, rName));
        }

        private void OnJoinRoom(string sessionName, string roomName)
        {
            _currentRoomName = roomName;
            NetworkManager.Instance?.SetCurrentRoomName(roomName);
            NetworkManager.Instance?.JoinGameRoom(sessionName);
            SetFindRoomStatus("방에 입장 중...", isError: false);
        }

        // FindRoom 패널의 진행/에러 메시지를 새로고침 버튼 바로 위 슬롯에 표시.
        private void SetFindRoomStatus(string message, bool isError)
        {
            if (_findRoomNoticeText == null) return;
            _findRoomNoticeText.text = message;
            _findRoomNoticeText.color = isError ? _errorTextColor : _textColor;
        }

        private void ClearRoomList()
        {
            if (_roomListContent == null) return;
            foreach (Transform child in _roomListContent)
                Destroy(child.gameObject);
            if (_emptyStateText != null) _emptyStateText.gameObject.SetActive(true);
        }

        // ──────────────────────────────────────────────
        //  Lobby 콜백
        // ──────────────────────────────────────────────

        private void ShowLobby(string roomName)
        {
            ShowPanel(_lobbyPanel);

            _currentRoomName = roomName;
            NetworkManager.Instance?.SetCurrentRoomName(roomName);

            if (_lobbyRoomNameText != null)
                _lobbyRoomNameText.text = roomName;

            if (_lobbyPlayer1Text != null)
                _lobbyPlayer1Text.text = "기다리는 중...";
            if (_lobbyPlayer2Text != null)
                _lobbyPlayer2Text.text = "기다리는 중...";
            if (_lobbyReadyStatusText != null)
                _lobbyReadyStatusText.text = "준비: 0/0";

            // Ready 초기화
            ApplyReadyButtonState(false, interactable: true);
            if (_lobbyCountdownText != null)
                _lobbyCountdownText.gameObject.SetActive(false);
        }

        private void OnReadyClicked()
        {
            if (_readyRequestPending) return;

            bool targetReady = !_isReady;
            bool sent = targetReady
                ? NetworkManager.Instance?.SendReady() == true
                : NetworkManager.Instance?.SendUnready() == true;
            if (!sent)
            {
                ShowNotice("준비 요청을 보낼 수 없습니다.");
                return;
            }

            _readyRequestPending = true;
            if (_readyButtonText != null)
                _readyButtonText.text = "처리 중...";
            if (_readyButton != null)
                _readyButton.interactable = false;
        }

        private void OnLeaveRoomClicked()
        {
            NetworkManager.Instance?.LeaveGameRoom();
            _readyRequestPending = false;
            _isReady = false;

            if (NetworkManager.Instance == null || string.IsNullOrEmpty(NetworkManager.Instance.Nickname))
            {
                ShowPanel(_nicknamePanel);
                return;
            }

            if (NetworkManager.Instance.IsInLobby)
                ShowPanel(_findRoomPanel);
            else
                ShowPanel(_mainPanel);
        }

        // ──────────────────────────────────────────────
        //  서버 이벤트 핸들러
        // ──────────────────────────────────────────────

        private void OnLobbyConnected()
        {
            Debug.Log("[Colosseum UI] Lobby connected!");

            if (_createRoomPanel != null && _createRoomPanel.activeSelf)
                UpdateCreateRoomButtonState();

            // 대기 중인 방 생성 요청이 있으면 자동 실행
            if (!string.IsNullOrEmpty(_pendingCreateRoomName))
            {
                string roomName = _pendingCreateRoomName;
                _pendingCreateRoomName = null;
                _currentRoomName = roomName;
                NetworkManager.Instance?.SetCurrentRoomName(roomName);
                NetworkManager.Instance?.RequestCreateRoom(roomName, _selectedMap);
                SetCreateRoomStatus("방을 생성하는 중...", isError: false);
                return;
            }

            // Find Room 패널이 활성화되어 있으면 방 목록 요청
            if (_findRoomPanel.activeSelf)
            {
                if (_searchStatusText != null)
                    _searchStatusText.text = "방 목록 불러오는 중...";
                NetworkManager.Instance?.RequestRoomList();
            }
        }

        private void OnLobbyFailed(string reason)
        {
            // 방 생성 / 방 찾기 흐름에서 시작된 로비 접속 시도라면 해당 패널의 상태 슬롯에 표시.
            // 그 외에는 기존대로 전역 토스트로.
            if (_createRoomPanel != null && _createRoomPanel.activeSelf)
            {
                UpdateCreateRoomButtonState();
                SetCreateRoomStatus($"연결 실패: {reason}", isError: true);
            }
            else if (_findRoomPanel != null && _findRoomPanel.activeSelf)
                SetFindRoomStatus($"연결 실패: {reason}", isError: true);
            else
                ShowNotice($"연결 실패: {reason}");

            if (_searchStatusText != null)
                _searchStatusText.text = $"연결 실패: {reason}";
        }

        private void OnRoomListReceived(string json)
        {
            // 로비에서 자동으로 방 목록 푸시가 오기도 함
            if (_findRoomPanel.activeSelf)
            {
                RefreshRoomListUI(json);
            }
        }

        private void OnRoomCreated(string sessionName)
        {
            Debug.Log($"[Colosseum UI] Room created, joining: {sessionName}");
            // 방이 만들어졌으니 자동으로 입장
            _pendingJoinSession = sessionName;
            NetworkManager.Instance?.JoinGameRoom(sessionName);
        }

        private void OnServerError(string error)
        {
            if (_readyRequestPending)
                ApplyReadyButtonState(_isReady, interactable: true);
            ShowNotice($"오류: {error}");
        }

        private void OnGameRoomJoined()
        {
            Debug.Log("[Colosseum UI] Joined game room! Hiding all panels...");
            // 모든 패널 즉시 숨기기 (씬 전환 전 UI 겹침 방지)
            _nicknamePanel.SetActive(false);
            _mainPanel.SetActive(false);
            _createRoomPanel.SetActive(false);
            _findRoomPanel.SetActive(false);
            _lobbyPanel.SetActive(false);
            // Fusion이 자동으로 게임 씬(SampleScene)을 로드하면
            // MainMenuUI는 파괴되고, 게임 씬의 GameOverUI가 대기실을 표시함.
        }

        private void OnGameRoomFailed(string reason)
        {
            // FindRoom 패널이 활성이면 새로고침 버튼 위 슬롯에, 아니면 전역 토스트로.
            if (_findRoomPanel != null && _findRoomPanel.activeSelf)
                SetFindRoomStatus($"방 입장 실패: {reason}", isError: true);
            else
                ShowNotice($"방 입장 실패: {reason}");
        }

        private void OnPlayerJoined(string info)
        {
            // info = "playerCount/maxPlayers"
            UpdatePlayerSlots(info);
        }

        private void OnPlayerLeft(string info)
        {
            UpdatePlayerSlots(info);
        }

        private void UpdatePlayerSlots(string info)
        {
            // "1/2" 또는 "2/2"
            string[] parts = info.Split('/');
            if (parts.Length != 2) return;

            if (!int.TryParse(parts[0], out int count)) return;

            if (_lobbyPlayer1Text != null)
                _lobbyPlayer1Text.text = count >= 1 ? "플레이어 1: 입장" : "기다리는 중...";
            if (_lobbyPlayer2Text != null)
                _lobbyPlayer2Text.text = count >= 2 ? "플레이어 2: 입장" : "기다리는 중...";
        }

        private void OnReadyUpdate(string info)
        {
            // "readyCount/playerCount"
            if (_lobbyReadyStatusText != null)
                _lobbyReadyStatusText.text = $"준비: {info}";
        }

        private void OnLocalReadyStateChanged(bool isReady)
        {
            if (_lobbyPanel == null || !_lobbyPanel.activeSelf) return;
            ApplyReadyButtonState(isReady, interactable: true);
        }

        private void ApplyReadyButtonState(bool isReady, bool interactable)
        {
            _isReady = isReady;
            _readyRequestPending = false;

            if (_readyButtonText != null)
                _readyButtonText.text = _isReady ? "준비 취소" : "준비";

            if (_readyButton != null)
            {
                _readyButton.interactable = interactable;
                var img = _readyButton.GetComponent<Image>();
                if (img != null)
                    img.color = _isReady
                        ? new Color(0.5f, 0.15f, 0.1f, 0.95f)
                        : new Color(0.1f, 0.4f, 0.1f, 0.95f);
            }
        }

        private void OnCountdownStart()
        {
            Debug.Log("[Colosseum UI] Countdown started!");
            _readyRequestPending = false;
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
            if (_lobbyCountdownText != null)
                _lobbyCountdownText.text = seconds.ToString();
        }

        private void OnCountdownCancel()
        {
            Debug.Log("[Colosseum UI] Countdown cancelled.");
            if (_lobbyCountdownText != null)
                _lobbyCountdownText.gameObject.SetActive(false);
            if (_readyButton != null)
                _readyButton.interactable = true;

            // Ready 상태 리셋
            ApplyReadyButtonState(false, interactable: true);
        }

        // Fusion NetworkSceneManagerDefault가 게임 씬을 additive로 로드하면
        // MainMenu 씬이 언로드되지 않아 Camera / EventSystem / AudioListener가
        // 게임 씬의 것과 중복된다. sceneLoaded 콜백에서 즉시 비활성화하여
        // pre-game lobby 동안의 매 프레임 에러 스팸을 방지한다.
        private void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path == gameObject.scene.path) return;
            DisableMenuSceneDuplicates();
        }

        private void DisableMenuSceneDuplicates()
        {
            if (_menuDuplicatesDisabled) return;
            _menuDuplicatesDisabled = true;

            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                    cam.enabled = false;
                foreach (var al in root.GetComponentsInChildren<AudioListener>(true))
                    al.enabled = false;
                foreach (var es in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))
                    es.enabled = false;
            }
        }

        private void OnGameStart()
        {
            Debug.Log("[Colosseum UI] Game starting! Disabling menu scene components.");
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
            DisableMenuSceneDuplicates();
        }

        private void OnDisconnected(string reason)
        {
            Debug.Log($"[Colosseum UI] Disconnected: {reason}");
            _readyRequestPending = false;
            _isReady = false;
            ShowNotice(reason);
            ShowPanel(_mainPanel);
        }

        // ──────────────────────────────────────────────
        //  알림
        // ──────────────────────────────────────────────

        private void ShowNotice(string message)
        {
            if (_noticeText == null) return;
            _noticeText.text = message;
            _noticeText.gameObject.SetActive(true);
            _noticeTimer = 3f;
        }

        private void CreateNoticeText()
        {
            var noticeObj = new GameObject("NoticeText");
            noticeObj.transform.SetParent(_canvas.transform, false);
            var noticeRect = noticeObj.AddComponent<RectTransform>();
            noticeRect.anchorMin = new Vector2(0.5f, 0.08f);
            noticeRect.anchorMax = new Vector2(0.5f, 0.08f);
            noticeRect.pivot = new Vector2(0.5f, 0.5f);
            noticeRect.sizeDelta = new Vector2(800f, 50f);

            _noticeText = noticeObj.AddComponent<TextMeshProUGUI>();
            _noticeText.fontSize = 24;
            _noticeText.color = new Color(1f, 0.3f, 0.3f);
            _noticeText.alignment = TextAlignmentOptions.Center;
            _noticeText.fontStyle = FontStyles.Bold;
            noticeObj.SetActive(false);
        }

        // ──────────────────────────────────────────────
        //  JSON 파싱 (간이)
        // ──────────────────────────────────────────────

        private struct RoomInfo
        {
            public string name;
            public string session;
            public int players;
            public int max;
            public string status;
        }

        private List<RoomInfo> ParseRoomList(string json)
        {
            var result = new List<RoomInfo>();

            // 빈 배열
            if (string.IsNullOrEmpty(json) || json == "[]")
                return result;

            // 간단한 파싱: 각 {} 블록 추출
            int i = 0;
            while (i < json.Length)
            {
                int start = json.IndexOf('{', i);
                if (start < 0) break;
                int end = json.IndexOf('}', start);
                if (end < 0) break;

                string obj = json.Substring(start + 1, end - start - 1);
                var room = new RoomInfo();

                room.name = ExtractJsonString(obj, "name");
                room.session = ExtractJsonString(obj, "session");
                room.status = ExtractJsonString(obj, "status");

                string playersStr = ExtractJsonValue(obj, "players");
                if (int.TryParse(playersStr, out int p)) room.players = p;

                string maxStr = ExtractJsonValue(obj, "max");
                if (int.TryParse(maxStr, out int m)) room.max = m;

                result.Add(room);
                i = end + 1;
            }

            return result;
        }

        private string ExtractJsonString(string json, string key)
        {
            string search = $"\"{key}\":\"";
            int start = json.IndexOf(search);
            if (start < 0) return "";
            start += search.Length;
            int end = json.IndexOf('"', start);
            if (end < 0) return "";
            return json.Substring(start, end - start);
        }

        private string ExtractJsonValue(string json, string key)
        {
            string search = $"\"{key}\":";
            int start = json.IndexOf(search);
            if (start < 0) return "";
            start += search.Length;

            int end = start;
            while (end < json.Length && json[end] != ',' && json[end] != '}')
                end++;

            return json.Substring(start, end - start).Trim();
        }

        // ──────────────────────────────────────────────
        //  UI 헬퍼
        // ──────────────────────────────────────────────

        private GameObject CreatePanelBase(string name)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(_canvas.transform, false);

            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = panel.AddComponent<Image>();
            bg.color = _bgColor;

            panel.SetActive(false);
            return panel;
        }

        // leftMargin: leftAlign=true일 때만 적용되는 좌측 여백(픽셀). leftAlign=false면 무시됨.
        private GameObject CreateVerticalContainer(Transform parent, float width, bool leftAlign = false, float leftMargin = 60f)
        {
            var container = new GameObject("Container");
            container.transform.SetParent(parent, false);

            var rect = container.AddComponent<RectTransform>();
            if (leftAlign)
            {
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(leftMargin, 0f);
            }
            else
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }
            rect.sizeDelta = new Vector2(width, 600f);

            var vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = _buttonSpacing;
            vlg.childAlignment = leftAlign ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = container.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return container;
        }

        private void CreateTitleText(Transform parent, string text, int fontSize, Color color, TMP_FontAsset font = null, TextAlignmentOptions textAlign = TextAlignmentOptions.Center, bool useOutline = false)
        {
            var obj = new GameObject("Title");
            obj.transform.SetParent(parent, false);

            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = fontSize + 20f;

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = textAlign;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            if (font != null) tmp.font = font;

            if (useOutline && tmp.fontSharedMaterial != null)
            {
                // fontMaterial 인스턴스 분리 → 다른 텍스트(메뉴 5개)의 shared material에 영향 없음
                tmp.fontMaterial = new Material(tmp.fontSharedMaterial);
                tmp.outlineColor = _titleOutlineColor;
                tmp.outlineWidth = _titleOutlineWidth;
            }
            else if (useOutline)
            {
                Debug.LogWarning("[MainMenuUI] _menuFont 미할당 — outline 미적용");
            }
        }

        private void CreateLabel(Transform parent, string text, int fontSize, Color? color = null, TMP_FontAsset font = null, TextAlignmentOptions textAlign = TextAlignmentOptions.Center)
        {
            var obj = new GameObject("Label");
            obj.transform.SetParent(parent, false);

            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = fontSize + 10f;

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color ?? new Color(0.6f, 0.6f, 0.6f);
            tmp.alignment = textAlign;
            if (font != null) tmp.font = font;
        }

        private TextMeshProUGUI CreatePlayerSlot(Transform parent, string defaultText)
        {
            var obj = new GameObject("PlayerSlot");
            obj.transform.SetParent(parent, false);

            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = 35f;

            var bg = obj.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.15f, 0.8f);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(15f, 0f);
            textRect.offsetMax = new Vector2(-15f, 0f);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = defaultText;
            tmp.fontSize = 22;
            tmp.color = _textColor;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            return tmp;
        }

        private void CreateSpacer(Transform parent, float height)
        {
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(parent, false);
            var le = spacer.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }

        // ── 둥근 9-slice sprite (런타임 생성, 정적 캐시) ──
        private static Sprite _roundedRectSprite;
        private static Sprite GetRoundedRectSprite()
        {
            if (_roundedRectSprite != null) return _roundedRectSprite;

            const int size = 64;
            const int radius = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = x < radius ? radius : (x >= size - radius ? size - 1 - radius : x);
                    int cy = y < radius ? radius : (y >= size - radius ? size - 1 - radius : y);
                    float dx = x - cx;
                    float dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a;
                    if (d <= radius - 1f) a = 255;
                    else if (d >= radius) a = 0;
                    else a = (byte)Mathf.RoundToInt(255f * (1f - (d - (radius - 1f))));
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            _roundedRectSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            _roundedRectSprite.name = "RoundedRect_RuntimeSprite";
            _roundedRectSprite.hideFlags = HideFlags.HideAndDontSave;
            return _roundedRectSprite;
        }

        // 에러 텍스트를 표시하고 displaySec 후 fadeSec 동안 알파 페이드 → 제거.
        // 이전 코루틴이 돌고 있으면 중단 후 새로 시작 (연속 클릭 대응).
        private void ShowErrorWithFade(TextMeshProUGUI tmp, string message, ref Coroutine handle)
        {
            if (handle != null) StopCoroutine(handle);
            handle = StartCoroutine(ErrorFadeRoutine(tmp, message));
        }

        private IEnumerator ErrorFadeRoutine(TextMeshProUGUI tmp, string msg, float displaySec = 2f, float fadeSec = 0.3f)
        {
            Color baseColor = _errorTextColor;
            tmp.text = msg;
            tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);

            yield return new WaitForSeconds(displaySec);

            float elapsed = 0f;
            while (elapsed < fadeSec)
            {
                elapsed += Time.deltaTime;
                tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(1f, 0f, elapsed / fadeSec));
                yield return null;
            }

            tmp.text = "";
            tmp.color = baseColor;
        }

        /// <summary>
        /// 박스형 액션 버튼 (Nickname Start / CreateRoom Create / FindRoom Refresh / Lobby Ready 등 주요 CTA 용).
        /// 불투명한 _actionButtonBoxColor 박스 위에 _bgColor 텍스트로 대비 강조.
        /// </summary>
        private Button CreateActionButton(
            Transform parent,
            string label,
            UnityEngine.Events.UnityAction onClick,
            TMP_FontAsset font = null,
            float widthOverride = 0f,
            int fontSizeOverride = 0,
            Color? hoverTextColor = null,
            Color? disabledTextColor = null,
            UnityEngine.Events.UnityAction onDisabledClick = null)
        {
            // widthOverride > 0 시: 가운데 정렬 가로 래퍼로 감싸 컨테이너 VLG 의 childForceExpandWidth 를 우회.
            Transform actualParent = parent;
            if (widthOverride > 0f)
            {
                var wrapper = new GameObject($"ActionBtnRow_{label}");
                wrapper.transform.SetParent(parent, false);
                var wrapLE = wrapper.AddComponent<LayoutElement>();
                wrapLE.preferredHeight = _buttonHeight + 10f;
                var wrapHLG = wrapper.AddComponent<HorizontalLayoutGroup>();
                wrapHLG.childAlignment = TextAnchor.MiddleCenter;
                wrapHLG.childControlWidth = true;
                wrapHLG.childControlHeight = true;
                wrapHLG.childForceExpandWidth = false;
                wrapHLG.childForceExpandHeight = true;
                actualParent = wrapper.transform;
            }

            var btnObj = new GameObject($"ActionBtn_{label}");
            btnObj.transform.SetParent(actualParent, false);

            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = _buttonHeight + 10f;
            if (widthOverride > 0f)
            {
                le.preferredWidth = widthOverride;
                le.flexibleWidth = 0f;
            }

            var btnImage = btnObj.AddComponent<Image>();
            // base를 white로 두어야 ColorBlock state color가 그대로 출력된다.
            // 비흰색이면 image.color × stateColor 곱셈으로 disabled 시 탁한 색이 된다.
            btnImage.color = Color.white;
            btnImage.sprite = GetRoundedRectSprite();
            btnImage.type = Image.Type.Sliced;
            btnImage.pixelsPerUnitMultiplier = 1f;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(onClick);

            var colors = btn.colors;
            colors.normalColor = _actionButtonBoxColor;
            colors.highlightedColor = _actionButtonHoverBoxColor;
            colors.pressedColor = Color.Lerp(_actionButtonHoverBoxColor, Color.black, 0.1f);
            // selectedColor 는 normalColor 와 동일 — EventSystem 자동 select 시 박스가 hover 색으로 stuck 방지.
            colors.selectedColor = _actionButtonBoxColor;
            colors.disabledColor = _actionButtonDisabledColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            btn.colors = colors;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 0f);
            textRect.offsetMax = new Vector2(-20f, 0f);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            // fontSizeOverride > 0 이면 그 값, 아니면 기본 _actionButtonFontSize.
            tmp.fontSize = fontSizeOverride > 0 ? fontSizeOverride : _actionButtonFontSize;
            tmp.fontStyle = FontStyles.Bold;
            // 기본 텍스트 색은 _textColor (흰색 Bold).
            tmp.color = _textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            var resolvedFont = font != null ? font : _menuFont;
            if (resolvedFont != null) tmp.font = resolvedFont;
            if (tmp.fontSharedMaterial != null)
            {
                tmp.fontMaterial = new Material(tmp.fontSharedMaterial);
                tmp.outlineColor = _actionButtonTextOutlineColor;
                tmp.outlineWidth = _actionButtonTextOutlineWidth;
            }

            // 호버/비활성/비활성-클릭 핸들러: 필요한 항목만 부착.
            bool needsTrigger = hoverTextColor.HasValue || disabledTextColor.HasValue || onDisabledClick != null;
            if (needsTrigger)
            {
                Color normalTextColor = tmp.color;
                Color hoverCol = hoverTextColor ?? normalTextColor;
                Color disabledCol = disabledTextColor ?? normalTextColor;
                var trigger = btnObj.AddComponent<EventTrigger>();

                if (hoverTextColor.HasValue || disabledTextColor.HasValue)
                {
                    var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    enter.callback.AddListener(_ =>
                    {
                        if (btn.interactable) tmp.color = hoverCol;
                    });
                    trigger.triggers.Add(enter);

                    var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                    exit.callback.AddListener(_ =>
                    {
                        tmp.color = btn.interactable ? normalTextColor : disabledCol;
                    });
                    trigger.triggers.Add(exit);
                }

                if (onDisabledClick != null)
                {
                    // EventTrigger 의 PointerClick 은 Selectable.interactable 와 무관하게 발화 → 비활성-클릭 안내 가능.
                    var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                    click.callback.AddListener(_ =>
                    {
                        if (!btn.interactable) onDisabledClick();
                    });
                    trigger.triggers.Add(click);
                }
            }

            return btn;
        }

        private void CreateStyledButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, TMP_FontAsset font = null, TextAlignmentOptions textAlign = TextAlignmentOptions.Center, bool textOnly = false, Color? overrideTextColor = null, int? fontSizeOverride = null, bool showHoverBullet = true)
        {
            var btnObj = new GameObject($"Btn_{label}");
            btnObj.transform.SetParent(parent, false);

            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = _buttonHeight;

            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = textOnly ? new Color(0f, 0f, 0f, 0f) : _buttonColor;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(onClick);

            var colors = btn.colors;
            if (textOnly)
            {
                // 박스 비가시: 모든 상태 알파 0. 호버 효과는 EventTrigger로 텍스트만 변경.
                // selectedColor 는 명시적으로 normalColor 와 동일하게 — 패널 진입 시 EventSystem 자동 select 가
                // 시각적 하이라이트로 보이지 않도록 보장.
                var transparent = new Color(0f, 0f, 0f, 0f);
                colors.normalColor = transparent;
                colors.highlightedColor = transparent;
                colors.pressedColor = transparent;
                colors.selectedColor = colors.normalColor;
            }
            else
            {
                // 평소: _buttonColor / 호버·선택: _buttonHoverColor / 클릭: 호버의 어두운 변형
                colors.normalColor = _buttonColor;
                colors.highlightedColor = _buttonHoverColor;
                colors.pressedColor = Color.Lerp(_buttonHoverColor, Color.black, 0.2f);
                colors.selectedColor = _buttonHoverColor;
            }
            btn.colors = colors;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 0f);
            textRect.offsetMax = new Vector2(-20f, 0f);

            // textOnly 모드에서 overrideTextColor 지정 시 평소 텍스트 색을 오버라이드 (보조 버튼용).
            // null 이면 기존 _textColor 그대로 사용 → 메인 메뉴 호환.
            Color baseTextColor = (textOnly && overrideTextColor.HasValue) ? overrideTextColor.Value : _textColor;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            // fontSizeOverride 우선 적용. 없으면 textOnly 모드의 _menuFontSize / 박스 모드의 26.
            tmp.fontSize = fontSizeOverride ?? (textOnly ? _menuFontSize : 26);
            tmp.color = baseTextColor;
            tmp.alignment = textAlign;
            tmp.fontStyle = FontStyles.Bold;
            if (font != null) tmp.font = font;

            if (textOnly)
            {
                // outline을 글자색과 동일하게 설정 → 글리프 획이 두꺼워 보이는 효과 (ExtraBold 시뮬레이션)
                if (tmp.fontSharedMaterial != null)
                {
                    tmp.fontMaterial = new Material(tmp.fontSharedMaterial);
                    tmp.outlineColor = _textColor;
                    tmp.outlineWidth = 0.15f;
                }

                string original = label;
                Color hoverColor = _buttonHoverColor;
                Color normalColor = baseTextColor;

                var trigger = btnObj.AddComponent<EventTrigger>();

                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ =>
                {
                    if (showHoverBullet) tmp.text = "•  " + original;
                    tmp.color = hoverColor;
                    tmp.outlineColor = hoverColor;
                });
                trigger.triggers.Add(enter);

                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ =>
                {
                    if (showHoverBullet) tmp.text = original;
                    tmp.color = normalColor;
                    tmp.outlineColor = normalColor;
                });
                trigger.triggers.Add(exit);

                // 패널 전환으로 SetActive(false) 될 때 PointerExit 가 발화하지 않아 호버 색이 남는다.
                // OnEnable 시 정상 색·텍스트로 복원하는 컴포넌트를 붙여 해결.
                var resetter = btnObj.AddComponent<HoverStateResetter>();
                resetter.Setup(tmp, original, normalColor, normalColor);
            }
        }

        private TMP_InputField CreateInputField(Transform parent, string placeholder, int fontSize = 22, TMP_FontAsset font = null, Color? placeholderColor = null, float placeholderFontSize = 0f, float fieldHeight = 0f, TextAlignmentOptions? textAlign = null)
        {
            var fieldObj = new GameObject("InputField");
            fieldObj.transform.SetParent(parent, false);

            var le = fieldObj.AddComponent<LayoutElement>();
            // fieldHeight > 0 이면 명시 높이 사용, 아니면 폰트 기반 자동 산정 (기존 default 보존).
            le.preferredHeight = fieldHeight > 0f ? fieldHeight : Mathf.Max(50f, fontSize + 20f);

            var fieldImage = fieldObj.AddComponent<Image>();
            fieldImage.color = _inputFieldBgColor;
            fieldImage.sprite = GetRoundedRectSprite();
            fieldImage.type = Image.Type.Sliced;
            fieldImage.pixelsPerUnitMultiplier = 1f;

            var textAreaObj = new GameObject("TextArea");
            textAreaObj.transform.SetParent(fieldObj.transform, false);
            var taRect = textAreaObj.AddComponent<RectTransform>();
            taRect.anchorMin = Vector2.zero;
            taRect.anchorMax = Vector2.one;
            taRect.offsetMin = new Vector2(15f, 5f);
            taRect.offsetMax = new Vector2(-15f, -5f);

            var phObj = new GameObject("Placeholder");
            phObj.transform.SetParent(textAreaObj.transform, false);
            var phRect = phObj.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = Vector2.zero;
            phRect.offsetMax = Vector2.zero;

            var phTMP = phObj.AddComponent<TextMeshProUGUI>();
            phTMP.text = placeholder;
            // placeholderFontSize > 0 이면 입력 텍스트와 분리된 별도 크기 사용.
            phTMP.fontSize = placeholderFontSize > 0f ? placeholderFontSize : fontSize;
            phTMP.color = placeholderColor ?? new Color(0.4f, 0.4f, 0.4f);
            phTMP.fontStyle = FontStyles.Normal;
            if (font != null) phTMP.font = font;
            if (textAlign.HasValue) phTMP.alignment = textAlign.Value;

            var inputTextObj = new GameObject("Text");
            inputTextObj.transform.SetParent(textAreaObj.transform, false);
            var itRect = inputTextObj.AddComponent<RectTransform>();
            itRect.anchorMin = Vector2.zero;
            itRect.anchorMax = Vector2.one;
            itRect.offsetMin = Vector2.zero;
            itRect.offsetMax = Vector2.zero;

            var inputTMP = inputTextObj.AddComponent<TextMeshProUGUI>();
            inputTMP.fontSize = fontSize;
            inputTMP.color = _textColor;
            if (font != null) inputTMP.font = font;
            if (textAlign.HasValue) inputTMP.alignment = textAlign.Value;

            var inputField = fieldObj.AddComponent<TMP_InputField>();
            inputField.textViewport = taRect;
            inputField.textComponent = inputTMP;
            inputField.placeholder = phTMP;
            inputField.characterLimit = 30;
            inputField.contentType = TMP_InputField.ContentType.Standard;
            inputField.lineType = TMP_InputField.LineType.SingleLine;

            inputField.caretColor = _textColor;
            inputField.selectionColor = new Color(0.3f, 0.3f, 0.5f, 0.5f);

            return inputField;
        }

        // ──────────────────────────────────────────────
        //  Map Selector Helpers (A303-275)
        // ──────────────────────────────────────────────

        private void CreateMapSelectorRow(Transform parent)
        {
            var labelObj = new GameObject("MapLabel");
            labelObj.transform.SetParent(parent, false);
            var labelLE = labelObj.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24f;
            _mapSelectorLabel = labelObj.AddComponent<TextMeshProUGUI>();
            _mapSelectorLabel.fontSize = 18;
            _mapSelectorLabel.color = _textColor;
            _mapSelectorLabel.alignment = TextAlignmentOptions.Center;
            _mapSelectorLabel.text = $"MAP (Dev Mode): {_selectedMap.ToDisplayName()}";

            var rowObj = new GameObject("MapSelectorRow");
            rowObj.transform.SetParent(parent, false);
            var rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = _buttonHeight * 0.8f;
            var hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            _btnMapDuskCity = CreateToggleMapButton(rowObj.transform, "DuskCity", SelectableScene.DuskCity);
            _btnMapMVPArena = CreateToggleMapButton(rowObj.transform, "MVPArena", SelectableScene.MVPArena);
            UpdateMapSelectorVisual();
        }

        private Button CreateToggleMapButton(Transform parent, string label, SelectableScene target)
        {
            var btnObj = new GameObject($"BtnMap_{label}");
            btnObj.transform.SetParent(parent, false);

            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = _buttonColor;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(() => OnMapToggleClicked(target));

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20;
            tmp.color = _textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;

            return btn;
        }

        private void OnMapToggleClicked(SelectableScene target)
        {
            if (_selectedMap == target) return; // 같은 토글 재클릭 — debounce
            _selectedMap = target;
            SaveSelectedMap();
            UpdateMapSelectorVisual();
        }

        private void UpdateMapSelectorVisual()
        {
            if (_mapSelectorLabel != null)
                _mapSelectorLabel.text = $"MAP (Dev Mode): {_selectedMap.ToDisplayName()}";
            ApplyToggleVisual(_btnMapDuskCity, _selectedMap == SelectableScene.DuskCity);
            ApplyToggleVisual(_btnMapMVPArena, _selectedMap == SelectableScene.MVPArena);
        }

        private void ApplyToggleVisual(Button btn, bool selected)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = selected ? _goldColor : _buttonColor;
            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
                tmp.color = selected ? _bgColor : _textColor;
        }

        private void LoadSelectedMap()
        {
            int v = PlayerPrefs.GetInt(MapPrefsKey, (int)SelectableScene.MVPArena);
            // Unspecified=0은 sentinel — user-selectable 아니므로 MVPArena fallback.
            if (System.Enum.IsDefined(typeof(SelectableScene), v) && (SelectableScene)v != SelectableScene.Unspecified)
                _selectedMap = (SelectableScene)v;
            else
                _selectedMap = SelectableScene.MVPArena;
        }

        private void SaveSelectedMap()
        {
            PlayerPrefs.SetInt(MapPrefsKey, (int)_selectedMap);
            PlayerPrefs.Save();
        }

        // ──────────────────────────────────────────────
        //  A303-334 Beans 비주얼 톤 헬퍼
        // ──────────────────────────────────────────────

        private void CreateMainMenuCharacter(Transform parent, string objName, Sprite sprite,
            Vector2 anchor, float height,
            float idleAmplitude, float idlePeriod, float idleStartDelay)
        {
            var obj = new GameObject(objName);
            obj.transform.SetParent(parent, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            // pivot.x = anchor.x → anchor 0이면 좌측 끝, 1이면 우측 끝에 캐릭터 edge가 정렬.
            // pivot.y = 0.5 → 캐릭터 vertical center가 anchor.y 높이에 옴.
            rect.pivot = new Vector2(anchor.x, 0.5f);
            float aspect = sprite.rect.width / sprite.rect.height;
            rect.sizeDelta = new Vector2(height * aspect, height);
            rect.anchoredPosition = Vector2.zero;

            var img = obj.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            var bob = obj.AddComponent<MainMenu.IdleBob>();
            bob.amplitude = idleAmplitude;
            bob.period = idlePeriod;
            bob.startDelay = idleStartDelay;
        }

        private void CreateBeansMenuItem(Transform parent, string label, System.Action onClick)
        {
            var itemGO = new GameObject(label.Replace(" ", "") + "MenuItem");
            itemGO.transform.SetParent(parent, false);
            itemGO.AddComponent<RectTransform>();

            // VerticalLayoutGroup 가 row 폭을 결정. 높이는 LayoutElement.preferredHeight 로 고정.
            var itemLE = itemGO.AddComponent<LayoutElement>();
            itemLE.preferredHeight = _beansMenuItemHeight;

            // 점·텍스트 사이/주변 빈 공간도 hover 영역에 들어가도록 보이지 않는 raycast target.
            var bg = itemGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = true;

            var hlg = itemGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 22f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var leftDot = CreateBeansMenuText(itemGO.transform, "·", _beansMenuFontSize, "LeftDot");
            var labelTMP = CreateBeansMenuText(itemGO.transform, label, _beansMenuFontSize, "Label");
            var rightDot = CreateBeansMenuText(itemGO.transform, "·", _beansMenuFontSize, "RightDot");

            // 점은 초기 alpha 0 (Normal 상태)
            var hidden = new Color(_beansMenuNormalColor.r, _beansMenuNormalColor.g, _beansMenuNormalColor.b, 0f);
            leftDot.color = hidden;
            rightDot.color = hidden;

            var menuItem = itemGO.AddComponent<MainMenu.MenuItem>();
            menuItem.label = labelTMP;
            menuItem.leftDot = leftDot;
            menuItem.rightDot = rightDot;
            menuItem.normalColor = _beansMenuNormalColor;
            menuItem.hoverColor = _beansMenuHoverColor;
            menuItem.OnClicked += onClick;
        }

        private TextMeshProUGUI CreateBeansMenuText(Transform parent, string text, int fontSize, string objName)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = _beansMenuNormalColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false; // pointer 이벤트는 부모 row 의 raycast Image 가 받음
            if (_menuFont != null) tmp.font = _menuFont;
            return tmp;
        }

        private void CreateBeansBottomInfo(Transform parent, string text, int fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject("Info");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = fontSize + 6f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = _beansBottomInfoColor;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            if (_menuFont != null) tmp.font = _menuFont;
        }

        private IEnumerator BeansLogoPulseLoop(Image img, float period)
        {
            const float minAlpha = 0.7f;
            const float maxAlpha = 1f;
            float half = Mathf.Max(0.2f, period * 0.5f);
            while (img != null)
            {
                float t = 0f;
                while (t < half)
                {
                    if (img == null) yield break;
                    t += Time.deltaTime;
                    float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / half));
                    var c = img.color; c.a = Mathf.Lerp(minAlpha, maxAlpha, u); img.color = c;
                    yield return null;
                }
                t = 0f;
                while (t < half)
                {
                    if (img == null) yield break;
                    t += Time.deltaTime;
                    float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / half));
                    var c = img.color; c.a = Mathf.Lerp(maxAlpha, minAlpha, u); img.color = c;
                    yield return null;
                }
            }
        }

        // ──────────────────────────────────────────────
        //  A303-354 Cyber 톤 헬퍼 (Nickname / CreateRoom / FindRoom)
        // ──────────────────────────────────────────────

        private void ApplyBeansBackground(GameObject panel)
        {
            var img = panel.GetComponent<Image>();
            if (img == null) return;
            if (_bgArenaSprite != null)
            {
                img.sprite = _bgArenaSprite;
                img.color = Color.white;
                img.preserveAspect = false;
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] _bgArenaSprite 미할당 — 단색 fallback");
            }
            img.raycastTarget = false;
        }

        private void CreateBeansLogoTitle(Transform parent, bool pulse)
        {
            if (_beansLogoSprite == null)
            {
                Debug.LogWarning("[MainMenuUI] _beansLogoSprite 미할당 — 로고 미표시");
                return;
            }
            var go = new GameObject("BeansLogoTitle");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -_beansLogoTopOffset);
            float aspect = _beansLogoSprite.rect.width / _beansLogoSprite.rect.height;
            rt.sizeDelta = new Vector2(_beansLogoHeight * aspect, _beansLogoHeight);
            var img = go.AddComponent<Image>();
            img.sprite = _beansLogoSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (pulse) StartCoroutine(BeansLogoPulseLoop(img, _beansLogoPulsePeriod));
        }

        private void CreateCyberTitleSprite(Transform parent, Sprite serializedSprite, string resourcePath, string objName, float? anchoredYOverride = null)
        {
            // 1순위: Inspector 에서 wiring 된 SerializeField
            // 2순위: Resources.Load — Assets/_Project/Resources/UI/MainMenu/{path}.png 자동 로드
            // TMP 텍스트 fallback 은 사용하지 않는다 (PNG 가 안 보이는 회귀 차단).
            Sprite sprite = serializedSprite;
            if (sprite == null && !string.IsNullOrEmpty(resourcePath))
                sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                Debug.LogError($"[MainMenuUI] {objName} sprite 로드 실패 — SerializeField 미할당 + Resources.Load('{resourcePath}') 실패. PNG 가 Resources 폴더에 있는지 확인.");
                return;
            }
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            float anchoredY = anchoredYOverride ?? -_cyberTitleTopOffset;
            rt.anchoredPosition = new Vector2(0f, anchoredY);
            float effectiveHeight = _cyberTitleHeight * _cyberTitleScale;
            float aspect = sprite.rect.width / sprite.rect.height;
            rt.sizeDelta = new Vector2(effectiveHeight * aspect, effectiveHeight);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        private MainMenu.CyberInputField CreateCyberInputField(Transform parent, string placeholder, float width, float height)
        {
            var rootGO = new GameObject("CyberInputField");
            rootGO.transform.SetParent(parent, false);
            var rootRt = rootGO.AddComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(width, height);
            var rootLE = rootGO.AddComponent<LayoutElement>();
            rootLE.preferredWidth = width;
            rootLE.preferredHeight = height;
            rootLE.flexibleWidth = 0f;

            // Input row (TMP_InputField + transparent raycast Image)
            var inputGO = new GameObject("Input");
            inputGO.transform.SetParent(rootGO.transform, false);
            var inputRt = inputGO.AddComponent<RectTransform>();
            inputRt.anchorMin = Vector2.zero;
            inputRt.anchorMax = Vector2.one;
            inputRt.offsetMin = new Vector2(0f, 6f);    // leave room for underline at bottom
            inputRt.offsetMax = Vector2.zero;
            var inputBg = inputGO.AddComponent<Image>();
            inputBg.color = new Color(0f, 0f, 0f, 0f);

            var textAreaGO = new GameObject("TextArea");
            textAreaGO.transform.SetParent(inputGO.transform, false);
            var taRt = textAreaGO.AddComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(12f, 4f);
            taRt.offsetMax = new Vector2(-12f, -4f);

            var phGO = new GameObject("Placeholder");
            phGO.transform.SetParent(textAreaGO.transform, false);
            var phRt = phGO.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;
            var phTmp = phGO.AddComponent<TextMeshProUGUI>();
            phTmp.text = placeholder;
            phTmp.fontSize = _cyberInputPlaceholderFontSize;
            phTmp.color = _cyberPlaceholderColor;
            phTmp.alignment = TextAlignmentOptions.Center;
            phTmp.fontStyle = FontStyles.Normal;
            phTmp.raycastTarget = false;
            if (_menuFont != null) phTmp.font = _menuFont;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(textAreaGO.transform, false);
            var textRt = textGO.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var textTmp = textGO.AddComponent<TextMeshProUGUI>();
            textTmp.fontSize = _cyberInputFontSize;
            textTmp.color = Color.white;
            textTmp.alignment = TextAlignmentOptions.Center;
            textTmp.raycastTarget = false;
            if (_menuFont != null) textTmp.font = _menuFont;

            var input = inputGO.AddComponent<TMP_InputField>();
            input.textViewport = taRt;
            input.textComponent = textTmp;
            input.placeholder = phTmp;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.caretColor = _cyberAccentColor;
            input.selectionColor = new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.35f);

            // Underline at bottom
            var ulGO = new GameObject("Underline");
            ulGO.transform.SetParent(rootGO.transform, false);
            var ulRt = ulGO.AddComponent<RectTransform>();
            ulRt.anchorMin = new Vector2(0f, 0f);
            ulRt.anchorMax = new Vector2(1f, 0f);
            ulRt.pivot = new Vector2(0.5f, 0f);
            ulRt.anchoredPosition = Vector2.zero;
            ulRt.sizeDelta = new Vector2(0f, 3f);
            var ulImg = ulGO.AddComponent<Image>();
            ulImg.color = _cyberAccentColor;
            ulImg.raycastTarget = false;

            var cyber = rootGO.AddComponent<MainMenu.CyberInputField>();
            cyber.input = input;
            cyber.underline = ulImg;
            cyber.placeholderText = phTmp;
            cyber.inputText = textTmp;
            cyber.underlineColor = _cyberAccentColor;
            cyber.placeholderColor = _cyberPlaceholderColor;
            cyber.textColor = Color.white;
            return cyber;
        }

        private MainMenu.CyberButton CreateCyberButton(Transform parent, string label, System.Action onClicked, float width, float height, int fontSize)
        {
            // 4 Image strip 방식 — PNG sprite 사용 안 함. 모서리 radius 없는 얇은 직사각형 outline.
            // CyberInputField underline (3px) 보다 살짝 얇은 2px 로 visual hierarchy 확보.
            const float strokeThickness = 2f;

            var go = new GameObject($"CyberBtn_{label}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.flexibleWidth = 0f;

            // raycast hit zone — 투명 Image 로 클릭/hover 영역만 확보.
            var hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            // 4 strip — 항상 같은 색으로 동기화됨.
            Color stripCol = new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.7f);
            var top    = CreateOutlineStrip(go.transform, "Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, strokeThickness), stripCol);
            var bottom = CreateOutlineStrip(go.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, strokeThickness), stripCol);
            var left   = CreateOutlineStrip(go.transform, "Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(strokeThickness, 0f), stripCol);
            var right  = CreateOutlineStrip(go.transform, "Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(strokeThickness, 0f), stripCol);

            // Label
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
            if (_menuFont != null) tmp.font = _menuFont;

            var btn = go.AddComponent<MainMenu.CyberButton>();
            btn.outlineImage = null;
            btn.outlineImages = new[] { top, bottom, left, right };
            btn.label = tmp;
            btn.borderColor = _cyberAccentColor;
            btn.OnClicked += onClicked;
            return btn;
        }

        private Image CreateOutlineStrip(Transform parent, string name,
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

        private TextMeshProUGUI CreateRoomColumnText(Transform parent, string text, float flexibleWidth, TextAlignmentOptions align, float preferredWidth, int fontSize, bool bold, Color color)
        {
            string goName = string.IsNullOrEmpty(text) ? "Col" : $"Col_{text}";
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = flexibleWidth;
            if (preferredWidth > 0f) le.preferredWidth = preferredWidth;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            if (_menuFont != null) tmp.font = _menuFont;
            return tmp;
        }

        private MainMenu.MenuItem CreateBackMenuItem(Transform parent, string label, System.Action onClick, int fontSize = 28)
        {
            // Normal/Hover 색을 SerializeField (_beansMenuNormalColor) 에 의존하지 않고 명시 고정.
            // 시나리오: 이 헬퍼는 secondary 액션(Back/Refresh)에만 쓰이며, 메인메뉴 톤 토큰의 동시 변경이
            // 의도치 않게 cyan 으로 보이게 만드는 회귀를 차단한다.
            var normalCol = new Color(1f, 1f, 1f, 0.7f);
            var hoverCol = _beansMenuHoverColor;

            var itemGO = new GameObject(label.Replace(" ", "") + "MenuItem");
            itemGO.transform.SetParent(parent, false);
            var rt = itemGO.AddComponent<RectTransform>();

            var le = itemGO.AddComponent<LayoutElement>();
            le.preferredHeight = fontSize + 16f;

            var bg = itemGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = true;

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(itemGO.transform, false);
            var textRt = textGO.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = normalCol;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
            if (_menuFont != null) tmp.font = _menuFont;

            var item = itemGO.AddComponent<MainMenu.MenuItem>();
            item.label = tmp;
            item.leftDot = null;
            item.rightDot = null;
            item.normalColor = normalCol;
            item.hoverColor = hoverCol;
            item.OnClicked += onClick;
            return item;
        }

        // ── A303-334 Dev Controls (개발 단계 임시) ──
        // 4 메뉴와 분리된 우하단 코너 한 줄 바: "· Dev Mode  [Sample] [MVPArena]".
        // 시연 빌드 직전 _beansShowDevControls=false 또는 DevControls GameObject 삭제로 무영향 제거.

        private void CreateBeansDevControlsBar(Transform parent)
        {
            var root = new GameObject("DevControls");
            root.transform.SetParent(parent, false);

            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = _beansDevControlsAnchor;
            rect.anchorMax = _beansDevControlsAnchor;
            rect.pivot = _beansDevControlsAnchor;
            rect.anchoredPosition = _beansDevControlsOffset;
            rect.sizeDelta = new Vector2(280f, 32f);

            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(2, 2, 2, 2);

#if UNITY_EDITOR
            CreateBeansDevModeButton(root.transform);
#endif
            _btnMapDuskCity = CreateBeansCompactMapToggle(root.transform, "DuskCity", SelectableScene.DuskCity);
            _btnMapMVPArena = CreateBeansCompactMapToggle(root.transform, "MVPArena", SelectableScene.MVPArena);
            UpdateMapSelectorVisual();
        }

#if UNITY_EDITOR
        private void CreateBeansDevModeButton(Transform parent)
        {
            var btnObj = new GameObject("DevModeButton");
            btnObj.transform.SetParent(parent, false);

            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 28f;
            le.preferredWidth = 110f;
            le.minWidth = 90f;

            // 보이지 않는 raycast 영역 = 클릭 핫스팟
            var bg = btnObj.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = true;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(OnDevModeClicked);
            // Button.colors 가 bg(Image, alpha 0)에만 영향 → 텍스트 색은 그대로. 미세하게 hover 시 bg alpha 만 살짝 올림.
            var colors = btn.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.06f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.12f);
            colors.fadeDuration = 0.1f;
            btn.colors = colors;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "· Dev Mode";
            tmp.fontSize = _beansDevTextFontSize;
            tmp.color = _beansBottomInfoColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            if (_menuFont != null) tmp.font = _menuFont;
        }
#endif

        private Button CreateBeansCompactMapToggle(Transform parent, string label, SelectableScene target)
        {
            var btnObj = new GameObject($"BtnMap_{label}");
            btnObj.transform.SetParent(parent, false);

            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 28f;
            le.preferredWidth = label.Length > 6 ? 92f : 76f;
            le.minHeight = 28f;
            le.minWidth = 60f;

            var img = btnObj.AddComponent<Image>();
            img.color = _buttonColor;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OnMapToggleClicked(target));

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 0f);
            textRect.offsetMax = new Vector2(-6f, 0f);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = _beansDevTextFontSize - 2; // 살짝 작게
            tmp.color = _textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            if (_menuFont != null) tmp.font = _menuFont;

            return btn;
        }

        // SetActive(false) 시 PointerExit 미발화로 hover 색·텍스트가 남는 버그 대응.
        // textOnly 버튼의 btnObj 에 붙여 OnEnable 마다 정상 상태로 복원.
        private class HoverStateResetter : MonoBehaviour
        {
            private TextMeshProUGUI _tmp;
            private string _originalText;
            private Color _normalColor;
            private Color _normalOutlineColor;

            internal void Setup(TextMeshProUGUI tmp, string originalText, Color normalColor, Color normalOutlineColor)
            {
                _tmp = tmp;
                _originalText = originalText;
                _normalColor = normalColor;
                _normalOutlineColor = normalOutlineColor;
            }

            private void OnEnable()
            {
                if (_tmp == null) return;
                _tmp.text = _originalText;
                _tmp.color = _normalColor;
                _tmp.outlineColor = _normalOutlineColor;
            }
        }
    }
}
