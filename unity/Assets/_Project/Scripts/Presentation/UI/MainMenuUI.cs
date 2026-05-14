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
    ///   1) Create Room / Find Room 클릭 → 로비 접속 (ConnectToLobby)
    ///   2) 방 생성 → ReliableData로 서버에 요청 → ROOM_CREATED 수신 → 방 세션에 접속
    ///   3) 방 찾기 → ReliableData로 서버에 요청 → ROOM_LIST 수신 → 목록 표시
    ///   4) 방 입장 → 방 세션에 Client 접속 → Ready → 카운트다운 → 게임 시작
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
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
        [SerializeField] private Sprite _logoSprite;  // MainPanel 로고 이미지. 미할당 시 로고 미표시.
        [SerializeField] private Sprite _fightSprite; // MainPanel 우하단 장식 이미지.

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

        // ── Create Room ──
        private TMP_InputField _roomNameInput;
        private TextMeshProUGUI _createRoomErrorText;
        private Coroutine _createRoomErrorFade;

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

        // ── 상태 ──
        private string _currentRoomName;
        private string _pendingJoinSession; // 방 생성 후 자동 입장용
        private string _pendingCreateRoomName; // 로비 접속 후 자동 방 생성용

        // ── 알림 ──
        private TextMeshProUGUI _noticeText;
        private float _noticeTimer;

        // ── Map Selector ──
        private const string MapPrefsKey = "Colosseum.UI.SelectedMap";
        private SelectableScene _selectedMap = SelectableScene.SampleScene;
        private Button _btnMapSample;
        private Button _btnMapMVPArena;
        private TextMeshProUGUI _mapSelectorLabel;

        private void Start()
        {
            LoadSelectedMap();
            CreateCanvas();
            CreateNicknamePanel();
            CreateMainPanel();
            CreateCreateRoomPanel();
            CreateFindRoomPanel();
            CreateLobbyPanel();
            CreateNoticeText();

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
            NetworkManager.OnCountdownStart += OnCountdownStart;
            NetworkManager.OnCountdownTick += OnCountdownTick;
            NetworkManager.OnCountdownCancel += OnCountdownCancel;
            NetworkManager.OnGameStart += OnGameStart;
            NetworkManager.OnDisconnected += OnDisconnected;
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
            NetworkManager.OnCountdownStart -= OnCountdownStart;
            NetworkManager.OnCountdownTick -= OnCountdownTick;
            NetworkManager.OnCountdownCancel -= OnCountdownCancel;
            NetworkManager.OnGameStart -= OnGameStart;
            NetworkManager.OnDisconnected -= OnDisconnected;
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
            _nicknamePanel = CreatePanelBase("NicknamePanel");

            // 입력 한 개에 집중하는 화면 → 메인 메뉴와 달리 가운데 정렬, 컨테이너는 입력 필드 길이를 살리기 위해 확장.
            var container = CreateVerticalContainer(_nicknamePanel.transform, _buttonWidth + 200f);

            CreateLabel(container.transform, "사용할 닉네임을 입력하세요", _backButtonFontSize, _textColor, _menuFont);
            CreateSpacer(container.transform, 6f);

            // 입력 텍스트는 _inputFieldFontSize(42) 그대로, placeholder 만 28 로 작게 분리.
            // 박스 세로 높이는 75 로 컴팩트하게 (기존 자동 계산 ~62 보다 살짝 여유).
            _nicknameInput = CreateInputField(
                container.transform,
                "한글 2~8자 또는 영문/숫자/_ 1~16자",
                _inputFieldFontSize,
                _menuFont,
                _placeholderColor,
                placeholderFontSize: 28f,
                fieldHeight: 75f,
                textAlign: TextAlignmentOptions.Center);
            _nicknameInput.characterLimit = NicknameValidator.AsciiMaxLength;
            _nicknameInput.onSubmit.AddListener(_ => OnConfirmNickname());
            // 입력 필드 폭 명시 (컨테이너 VLG 의 childForceExpandWidth 와 함께 동작)
            var inputLE = _nicknameInput.GetComponent<LayoutElement>();
            if (inputLE != null) inputLE.preferredWidth = 600f;
            CreateSpacer(container.transform, 12f);

            // 에러 메시지 — 버튼 바로 위에 위치.
            var errObj = new GameObject("NicknameError");
            errObj.transform.SetParent(container.transform, false);
            var errLE = errObj.AddComponent<LayoutElement>();
            errLE.preferredHeight = 28f;
            _nicknameErrorText = errObj.AddComponent<TextMeshProUGUI>();
            _nicknameErrorText.fontSize = 24;
            _nicknameErrorText.color = _errorTextColor;
            _nicknameErrorText.alignment = TextAlignmentOptions.Center;
            _nicknameErrorText.text = "";
            if (_menuFont != null) _nicknameErrorText.font = _menuFont;

            // Start: 입력 비어있으면 Disabled. 비활성 상태로 클릭하면 에러 텍스트로 안내.
            var startBtn = CreateActionButton(
                container.transform,
                "Start",
                OnConfirmNickname,
                _menuFont,
                widthOverride: _buttonWidth * 0.7f,
                disabledTextColor: _actionButtonDisabledTextColor,
                onDisabledClick: () =>
                {
                    if (_nicknameErrorText != null)
                        ShowErrorWithFade(_nicknameErrorText, NicknameValidator.ToKoreanMessage(NicknameValidator.Result.Empty), ref _nicknameErrorFade);
                });
            var startBtnTmp = startBtn.GetComponentInChildren<TextMeshProUGUI>();
            startBtn.interactable = false;
            if (startBtnTmp != null) startBtnTmp.color = _actionButtonDisabledTextColor;
            _nicknameInput.onValueChanged.AddListener(text =>
            {
                bool valid = !string.IsNullOrWhiteSpace(text);
                startBtn.interactable = valid;
                if (startBtnTmp != null)
                    startBtnTmp.color = valid ? _textColor : _actionButtonDisabledTextColor;
                if (valid && _nicknameErrorText != null)
                {
                    if (_nicknameErrorFade != null) { StopCoroutine(_nicknameErrorFade); _nicknameErrorFade = null; }
                    _nicknameErrorText.text = "";
                    _nicknameErrorText.color = _errorTextColor;
                }
            });
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
            ShowPanel(_mainPanel);
        }

        private static string ToNicknameErrorMessage(NicknameValidator.Result r)
            => NicknameValidator.ToKoreanMessage(r);

        // ──────────────────────────────────────────────
        //  1. Main Panel
        // ──────────────────────────────────────────────

        private void CreateMainPanel()
        {
            _mainPanel = CreatePanelBase("MainPanel");

            // 장식 이미지: 우하단 고정. container보다 먼저 생성해서 sibling index를 낮게 유지 → 뒤에 렌더링
            if (_fightSprite != null)
            {
                const float fightH = 874f;
                var fightObj = new GameObject("FightBgImage");
                fightObj.transform.SetParent(_mainPanel.transform, false);
                var fightRect = fightObj.AddComponent<RectTransform>();
                fightRect.anchorMin = new Vector2(1f, 0f); // 우하단 anchor
                fightRect.anchorMax = new Vector2(1f, 0f);
                fightRect.pivot    = new Vector2(1f, 0f); // 이미지 우하단 기준점
                fightRect.anchoredPosition = Vector2.zero;
                float fightAspect = _fightSprite.rect.width / _fightSprite.rect.height;
                fightRect.sizeDelta = new Vector2(fightH * fightAspect, fightH);
                var fightImg = fightObj.AddComponent<Image>();
                fightImg.sprite = _fightSprite;
                fightImg.color  = Color.white;
                fightImg.raycastTarget = false; // 우하단 큰 이미지가 버튼 클릭을 가로채지 않도록
            }

            var container = CreateVerticalContainer(_mainPanel.transform, _buttonWidth, leftAlign: true, leftMargin: 90f);

            // 로고: 미할당 시 wrapper 자체를 만들지 않아 흰 박스가 그려지지 않게 함
            if (_logoSprite != null)
            {
                const float logoH = 257f;          // 본체 높이
                const float logoBottomGap = 50f;   // wrapper 하단 여백 → 버튼과의 시각적 간격
                float aspectRatio = _logoSprite.rect.width / _logoSprite.rect.height;

                // VLG childControlHeight=false이므로 wrapper의 sizeDelta.y를 직접 설정해야 함
                var logoWrapper = new GameObject("LogoWrapper");
                logoWrapper.transform.SetParent(container.transform, false);
                var logoWrapperLE = logoWrapper.AddComponent<LayoutElement>();
                logoWrapperLE.preferredHeight = logoH + logoBottomGap;
                logoWrapper.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, logoH + logoBottomGap);

                var logoObj = new GameObject("LogoImage");
                logoObj.transform.SetParent(logoWrapper.transform, false);
                var logoRect = logoObj.AddComponent<RectTransform>();
                // 상단 고정: 여백이 wrapper 하단에 남아 버튼과의 간격이 됨
                logoRect.anchorMin = new Vector2(0f, 1f);
                logoRect.anchorMax = new Vector2(0f, 1f);
                logoRect.pivot = new Vector2(0f, 1f);
                logoRect.anchoredPosition = new Vector2(20f, 0f);
                logoRect.sizeDelta = new Vector2(logoH * aspectRatio, logoH);

                var logoImg = logoObj.AddComponent<Image>();
                logoImg.sprite = _logoSprite;
                logoImg.color = Color.white;
                logoImg.raycastTarget = false; // 로고 위 클릭은 차단
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] _logoSprite 미할당 — 로고 미표시");
            }

            CreateStyledButton(container.transform, "Create Room", OnCreateRoomClicked, _menuFont, TextAlignmentOptions.MidlineLeft, textOnly: true);
            CreateStyledButton(container.transform, "Find Room", OnFindRoomClicked, _menuFont, TextAlignmentOptions.MidlineLeft, textOnly: true);
            CreateStyledButton(container.transform, "Dev Mode", OnDevModeClicked, _menuFont, TextAlignmentOptions.MidlineLeft, textOnly: true);
            CreateStyledButton(container.transform, "Settings", OnSettingsClicked, _menuFont, TextAlignmentOptions.MidlineLeft, textOnly: true);
            CreateSpacer(container.transform, 18f);
            CreateMapSelectorRow(container.transform);
            CreateSpacer(container.transform, 12f);
            CreateStyledButton(container.transform, "Quit Game", OnQuitClicked, _menuFont, TextAlignmentOptions.MidlineLeft, textOnly: true);
        }

        // ──────────────────────────────────────────────
        //  2. Create Room Panel
        // ──────────────────────────────────────────────

        private void CreateCreateRoomPanel()
        {
            _createRoomPanel = CreatePanelBase("CreateRoomPanel");

            var container = CreateVerticalContainer(_createRoomPanel.transform, _buttonWidth + 200f);

            CreateTitleText(container.transform, "CREATE ROOM", _pageTitleFontSize, _goldColor, _menuFont, TextAlignmentOptions.Center, useOutline: false);
            CreateSpacer(container.transform, 10f);

            CreateLabel(container.transform, "방 이름을 입력하세요", _backButtonFontSize, _textColor, _menuFont, TextAlignmentOptions.Center);
            CreateSpacer(container.transform, 4f);

            _roomNameInput = CreateInputField(
                container.transform,
                "방 이름 (최대 20자)",
                _inputFieldFontSize,
                _menuFont,
                _placeholderColor,
                placeholderFontSize: 28f,
                fieldHeight: 75f,
                textAlign: TextAlignmentOptions.Center);
            var inputLE = _roomNameInput.GetComponent<LayoutElement>();
            if (inputLE != null) inputLE.preferredWidth = 600f;
            CreateSpacer(container.transform, 12f);

            // 에러 메시지 — 버튼 바로 위에 위치.
            var errObj = new GameObject("CreateRoomError");
            errObj.transform.SetParent(container.transform, false);
            var errLE = errObj.AddComponent<LayoutElement>();
            errLE.preferredHeight = 28f;
            _createRoomErrorText = errObj.AddComponent<TextMeshProUGUI>();
            _createRoomErrorText.fontSize = 22;
            _createRoomErrorText.color = _errorTextColor;
            _createRoomErrorText.alignment = TextAlignmentOptions.Center;
            _createRoomErrorText.text = "";
            if (_menuFont != null) _createRoomErrorText.font = _menuFont;

            var createBtn = CreateActionButton(
                container.transform,
                "방 생성",
                OnConfirmCreateRoom,
                _menuFont,
                widthOverride: _buttonWidth * 0.7f,
                fontSizeOverride: 36,
                hoverTextColor: _textColor,
                disabledTextColor: _actionButtonDisabledTextColor,
                onDisabledClick: () =>
                {
                    if (_createRoomErrorText != null)
                        ShowErrorWithFade(_createRoomErrorText, "방 이름을 입력하세요.", ref _createRoomErrorFade);
                });
            var createBtnTmp = createBtn.GetComponentInChildren<TextMeshProUGUI>();
            createBtn.interactable = false;
            if (createBtnTmp != null) createBtnTmp.color = _actionButtonDisabledTextColor;
            _roomNameInput.onValueChanged.AddListener(text =>
            {
                bool valid = !string.IsNullOrWhiteSpace(text);
                createBtn.interactable = valid;
                if (createBtnTmp != null)
                    createBtnTmp.color = valid ? _textColor : _actionButtonDisabledTextColor;
                if (valid && _createRoomErrorText != null)
                {
                    if (_createRoomErrorFade != null) { StopCoroutine(_createRoomErrorFade); _createRoomErrorFade = null; }
                    _createRoomErrorText.text = "";
                    _createRoomErrorText.color = _errorTextColor;
                }
            });

            CreateStyledButton(
                container.transform,
                "< 이전으로",
                OnCreateRoomBack,
                _menuFont,
                TextAlignmentOptions.Center,
                textOnly: true,
                overrideTextColor: _backButtonColor,
                fontSizeOverride: _backButtonFontSize,
                showHoverBullet: false);
        }

        // ──────────────────────────────────────────────
        //  3. Find Room Panel
        // ──────────────────────────────────────────────

        private void CreateFindRoomPanel()
        {
            _findRoomPanel = CreatePanelBase("FindRoomPanel");

            // 타이틀
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_findRoomPanel.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.15f, 0.85f);
            titleRect.anchorMax = new Vector2(0.85f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "FIND ROOM";
            titleTMP.fontSize = _pageTitleFontSize;
            titleTMP.color = _goldColor;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.fontStyle = FontStyles.Bold;
            if (_menuFont != null) titleTMP.font = _menuFont;

            // 상태 텍스트
            var statusObj = new GameObject("SearchStatus");
            statusObj.transform.SetParent(_findRoomPanel.transform, false);
            var statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.15f, 0.78f);
            statusRect.anchorMax = new Vector2(0.85f, 0.84f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            _searchStatusText = statusObj.AddComponent<TextMeshProUGUI>();
            _searchStatusText.text = "로비 접속 중...";
            _searchStatusText.fontSize = 26;
            _searchStatusText.color = new Color(0.63f, 0.63f, 0.63f, 1f);
            _searchStatusText.alignment = TextAlignmentOptions.Center;
            if (_menuFont != null) _searchStatusText.font = _menuFont;

            // 방 목록 스크롤 영역
            var scrollObj = new GameObject("ScrollArea");
            scrollObj.transform.SetParent(_findRoomPanel.transform, false);
            var scrollRectTransform = scrollObj.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0.15f, 0.28f);
            scrollRectTransform.anchorMax = new Vector2(0.85f, 0.77f);
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;

            var scrollImage = scrollObj.AddComponent<Image>();
            scrollImage.color = new Color(0.13f, 0.13f, 0.18f, 0.6f);

            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // 빈 상태 오버레이 (방 없을 때 표시)
            var emptyStateObj = new GameObject("EmptyState");
            emptyStateObj.transform.SetParent(scrollObj.transform, false);
            var emptyRect = emptyStateObj.AddComponent<RectTransform>();
            emptyRect.anchorMin = Vector2.zero;
            emptyRect.anchorMax = Vector2.one;
            emptyRect.offsetMin = Vector2.zero;
            emptyRect.offsetMax = Vector2.zero;
            _emptyStateText = emptyStateObj.AddComponent<TextMeshProUGUI>();
            _emptyStateText.text = "열려 있는 방이 없습니다.\n새로고침을 눌러주세요.";
            _emptyStateText.fontSize = 22;
            _emptyStateText.color = _placeholderColor;
            _emptyStateText.alignment = TextAlignmentOptions.Center;
            if (_menuFont != null) _emptyStateText.font = _menuFont;

            // Viewport (Mask)
            var viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(scrollObj.transform, false);
            var viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportObj.AddComponent<Image>().color = Color.white;
            viewportObj.AddComponent<Mask>().showMaskGraphic = false;

            // Content
            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            var contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var contentVLG = contentObj.AddComponent<VerticalLayoutGroup>();
            contentVLG.spacing = 6f;
            contentVLG.childControlWidth = true;
            contentVLG.childControlHeight = false;
            contentVLG.childForceExpandWidth = true;
            contentVLG.childForceExpandHeight = false;
            contentVLG.padding = new RectOffset(10, 10, 8, 8);

            var contentFitter = contentObj.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect.GetComponent<RectTransform>();
            _roomListContent = contentObj.transform;

            // 새로고침 버튼 위 진행/에러 알림 슬롯 (방 입장 중·실패 등 흐름성 메시지)
            var noticeObj = new GameObject("FindRoomNotice");
            noticeObj.transform.SetParent(_findRoomPanel.transform, false);
            var noticeRect = noticeObj.AddComponent<RectTransform>();
            noticeRect.anchorMin = new Vector2(0.15f, 0.235f);
            noticeRect.anchorMax = new Vector2(0.85f, 0.275f);
            noticeRect.offsetMin = Vector2.zero;
            noticeRect.offsetMax = Vector2.zero;
            _findRoomNoticeText = noticeObj.AddComponent<TextMeshProUGUI>();
            _findRoomNoticeText.text = "";
            _findRoomNoticeText.fontSize = 20;
            _findRoomNoticeText.color = _textColor;
            _findRoomNoticeText.alignment = TextAlignmentOptions.Center;
            if (_menuFont != null) _findRoomNoticeText.font = _menuFont;

            // Refresh 버튼 — CreateRoom 의 '방 생성' 액션 버튼과 톤·크기 통일.
            var refreshBtn = CreateAnchoredButton(_findRoomPanel.transform, "새로고침",
                new Vector2(0.4f, 0.15f), new Vector2(0.6f, 0.23f),
                _actionButtonBoxColor, OnRefreshRooms,
                hoverBgColor: _actionButtonHoverBoxColor,
                textColor: _textColor,
                hoverTextColor: _textColor,
                fontSize: 36);
            // CreateActionButton 과 동일한 outline 강조 효과 적용.
            var refreshTmp = refreshBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (refreshTmp != null && refreshTmp.fontSharedMaterial != null)
            {
                refreshTmp.fontMaterial = new Material(refreshTmp.fontSharedMaterial);
                refreshTmp.outlineColor = _actionButtonTextOutlineColor;
                refreshTmp.outlineWidth = _actionButtonTextOutlineWidth;

                // CreateAnchoredButton 안에서 HoverStateResetter 가 outline 기본값(투명)을 캡처해 둔 상태.
                // 위에서 outline 을 변경했으므로 resetter 의 normalOutlineColor 도 새 값으로 재설정 →
                // 패널 전환으로 OnEnable 시 outline 이 투명으로 되돌아가 사라지는 회귀 차단.
                var refreshResetter = refreshBtn.GetComponent<HoverStateResetter>();
                refreshResetter?.Setup(refreshTmp, "새로고침", _textColor, _actionButtonTextOutlineColor);
            }

            // Back 버튼 (투명 배경, 회색 텍스트) — CreateRoom 의 < 이전으로 와 톤 통일.
            CreateAnchoredButton(_findRoomPanel.transform, "< 이전으로",
                new Vector2(0.35f, 0.05f), new Vector2(0.65f, 0.13f),
                Color.clear, OnFindRoomBack,
                hoverTextColor: _buttonHoverColor,
                textColor: _backButtonColor,
                fontSize: _backButtonFontSize);
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
            SetCreateRoomStatus("", isError: false);
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

        private void OnDevModeClicked()
        {
            // Dev Mode는 기존 Host 모드 (서버 불필요)
            NetworkManager.Instance?.DisconnectAll();

            // Phase 1 미머지 또는 Build Settings 미등록 상태에서 LoadScene이 ArgumentException으로 throw하면
            // 사용자 피드백 0의 silent dead button 발생 — 사전 차단 + SampleScene fallback.
            var path = _selectedMap.ToScenePath();
            if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
            {
                ShowNotice($"{_selectedMap.ToDisplayName()} not in Build Settings yet — falling back to Sample Arena.");
                _selectedMap = SelectableScene.SampleScene;
                SaveSelectedMap();
                UpdateMapSelectorVisual();
                path = _selectedMap.ToScenePath();
            }

            NetworkManager.AutoStartDevMode = true;
            NetworkManager.AutoStartDevModeScene = _selectedMap;
            SceneManager.LoadScene(path);
        }

        private void OnSettingsClicked()
        {
            ShowNotice("설정은 준비 중입니다.");
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

            // Create Room scene routing은 후속 PR (A303-275-2 [NETWORK])에서 지원.
            // 본 PR scope에서는 MVPArena Create Room 진입 시 SampleScene fallback이 발생하므로 차단.
            if (_selectedMap == SelectableScene.MVPArena)
            {
                SetCreateRoomStatus("MVPArena 방 생성은 후속 PR에서 지원됩니다. Dev Mode를 사용하세요.", isError: true);
                return;
            }

            if (!NetworkManager.Instance.IsInLobby)
            {
                // 로비 미접속 → 접속 후 자동으로 방 생성
                _pendingCreateRoomName = roomName;
                SetCreateRoomStatus("서버에 연결 중...", isError: false);
                NetworkManager.Instance.ConnectToLobby();
                return;
            }

            _currentRoomName = roomName;
            NetworkManager.Instance.RequestCreateRoom(roomName);
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

            bool isFull = room.players >= room.max;
            bool isPlaying = room.status == "playing";
            bool isCountdown = room.status == "countdown";
            bool canJoin = !isFull && !isPlaying && !isCountdown;

            var itemObj = new GameObject($"Room_{room.name}");
            itemObj.transform.SetParent(_roomListContent, false);

            var itemLE = itemObj.AddComponent<LayoutElement>();
            itemLE.preferredHeight = 70f;

            var itemImage = itemObj.AddComponent<Image>();

            Color itemNormalColor;
            if (isPlaying || isCountdown)
                itemNormalColor = new Color(0.12f, 0.1f, 0.14f, 0.6f);
            else if (isFull)
                itemNormalColor = new Color(0.15f, 0.13f, 0.18f, 0.7f);
            else
                itemNormalColor = new Color(0.18f, 0.16f, 0.22f, 0.9f);

            if (canJoin)
            {
                itemImage.color = Color.white;
                itemImage.sprite = GetRoundedRectSprite();
                itemImage.type = Image.Type.Sliced;
                itemImage.pixelsPerUnitMultiplier = 1f;

                var btn = itemObj.AddComponent<Button>();
                btn.targetGraphic = itemImage;
                string sName = room.session;
                string rName = room.name;
                btn.onClick.AddListener(() => OnJoinRoom(sName, rName));

                var colors = btn.colors;
                colors.normalColor = itemNormalColor;
                colors.highlightedColor = _actionButtonHoverBoxColor;
                colors.pressedColor = Color.Lerp(_actionButtonHoverBoxColor, Color.black, 0.15f);
                colors.selectedColor = itemNormalColor;
                colors.colorMultiplier = 1f;
                btn.colors = colors;
            }
            else
            {
                itemImage.color = itemNormalColor;
            }

            // HorizontalLayoutGroup으로 자식 배치
            var hlg = itemObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.padding = new RectOffset(20, 20, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // 방 이름 (왼쪽)
            var nameObj = new GameObject("RoomName");
            nameObj.transform.SetParent(itemObj.transform, false);
            var nameLE = nameObj.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1f;

            var nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.text = room.name;
            nameTMP.fontSize = 28;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.color = (isPlaying || isCountdown) ? new Color(0.4f, 0.4f, 0.4f) : _textColor;
            nameTMP.alignment = TextAlignmentOptions.Midline;
            nameTMP.enableWordWrapping = false;
            nameTMP.overflowMode = TextOverflowModes.Ellipsis;
            if (_menuFont != null) nameTMP.font = _menuFont;

            // 상태 (오른쪽)
            var infoObj = new GameObject("RoomInfo");
            infoObj.transform.SetParent(itemObj.transform, false);
            var infoLE = infoObj.AddComponent<LayoutElement>();
            infoLE.minWidth = 160f;
            infoLE.preferredWidth = 200f;

            var infoTMP = infoObj.AddComponent<TextMeshProUGUI>();
            string statusLabel = isPlaying ? "게임 중" : isCountdown ? "시작 중" : isFull ? "만석" : "입장 가능";
            infoTMP.text = $"{room.players}/{room.max}  {statusLabel}";
            infoTMP.fontSize = 24;
            infoTMP.fontStyle = FontStyles.Bold;
            infoTMP.alignment = TextAlignmentOptions.MidlineRight;
            infoTMP.enableWordWrapping = false;
            infoTMP.overflowMode = TextOverflowModes.Overflow;
            infoTMP.color = canJoin ? _actionButtonBoxColor : _placeholderColor;
            if (_menuFont != null) infoTMP.font = _menuFont;

            if (canJoin)
            {
                // hover 시 infoTMP 텍스트를 흰색으로, exit 시 오렌지 복원
                Color infoNormal = _actionButtonBoxColor;
                var infoTrigger = itemObj.AddComponent<EventTrigger>();

                var infoEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                infoEnter.callback.AddListener(_ => infoTMP.color = _textColor);
                infoTrigger.triggers.Add(infoEnter);

                var infoExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                infoExit.callback.AddListener(_ => infoTMP.color = infoNormal);
                infoTrigger.triggers.Add(infoExit);
            }

            // ">" 화살표 (입장 가능 카드에만)
            if (canJoin)
            {
                var arrowObj = new GameObject("Arrow");
                arrowObj.transform.SetParent(itemObj.transform, false);
                var arrowLE = arrowObj.AddComponent<LayoutElement>();
                arrowLE.preferredWidth = 30f;
                arrowLE.flexibleWidth = 0f;

                var arrowTMP = arrowObj.AddComponent<TextMeshProUGUI>();
                arrowTMP.text = ">";
                arrowTMP.fontSize = 28;
                arrowTMP.color = _textColor;
                arrowTMP.alignment = TextAlignmentOptions.MidlineRight;
                if (_menuFont != null) arrowTMP.font = _menuFont;
            }
        }

        private void OnJoinRoom(string sessionName, string roomName)
        {
            _currentRoomName = roomName;
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

            if (_lobbyRoomNameText != null)
                _lobbyRoomNameText.text = roomName;

            if (_lobbyPlayer1Text != null)
                _lobbyPlayer1Text.text = "기다리는 중...";
            if (_lobbyPlayer2Text != null)
                _lobbyPlayer2Text.text = "기다리는 중...";
            if (_lobbyReadyStatusText != null)
                _lobbyReadyStatusText.text = "준비: 0/0";

            // Ready 초기화
            _isReady = false;
            if (_readyButtonText != null)
                _readyButtonText.text = "준비";
            if (_readyButton != null)
            {
                _readyButton.interactable = true;
                var img = _readyButton.GetComponent<Image>();
                if (img != null) img.color = new Color(0.1f, 0.4f, 0.1f, 0.95f);
            }
            if (_lobbyCountdownText != null)
                _lobbyCountdownText.gameObject.SetActive(false);
        }

        private void OnReadyClicked()
        {
            _isReady = !_isReady;

            if (_isReady)
                NetworkManager.Instance?.SendReady();
            else
                NetworkManager.Instance?.SendUnready();

            if (_readyButtonText != null)
                _readyButtonText.text = _isReady ? "준비 취소" : "준비";

            if (_readyButton != null)
            {
                var img = _readyButton.GetComponent<Image>();
                if (img != null)
                    img.color = _isReady
                        ? new Color(0.5f, 0.15f, 0.1f, 0.95f)
                        : new Color(0.1f, 0.4f, 0.1f, 0.95f);
            }
        }

        private void OnLeaveRoomClicked()
        {
            NetworkManager.Instance?.LeaveGameRoom();
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

            // 대기 중인 방 생성 요청이 있으면 자동 실행
            if (!string.IsNullOrEmpty(_pendingCreateRoomName))
            {
                string roomName = _pendingCreateRoomName;
                _pendingCreateRoomName = null;
                _currentRoomName = roomName;
                NetworkManager.Instance?.RequestCreateRoom(roomName);
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
                SetCreateRoomStatus($"연결 실패: {reason}", isError: true);
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

        private void OnCountdownStart()
        {
            Debug.Log("[Colosseum UI] Countdown started!");
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
            _isReady = false;
            if (_readyButtonText != null)
                _readyButtonText.text = "준비";
            if (_readyButton != null)
            {
                var img = _readyButton.GetComponent<Image>();
                if (img != null) img.color = new Color(0.1f, 0.4f, 0.1f, 0.95f);
            }
        }

        private void OnGameStart()
        {
            Debug.Log("[Colosseum UI] Game starting! Disabling menu scene components.");
            // Fusion NetworkSceneManagerDefault가 이미 게임 씬을 로드했으므로
            // 직접 SceneManager.LoadScene()을 호출하면 씬이 재로드되어
            // Fusion이 스폰한 NetworkObject가 파괴됨 → 버그.
            // 대신 메뉴 씬의 렌더/입력/오디오 컴포넌트만 전부 끈다.
            //
            // 문제: MainMenu 씬이 언로드되지 않은 채 SampleScene이 additive로
            // 로드되면, MainMenu 카메라(depth 0, SolidColor clear)가
            // SampleScene 카메라(depth -1) 위에 매 프레임 덮어써서
            // 월드 스프라이트(캐릭터/바닥/총알)가 전혀 보이지 않게 된다.
            // Screen Space Overlay Canvas(HP바/방 인디케이터)는 카메라
            // 파이프라인 밖이라 살아남음 → 증상과 일치.
            //
            // 이 씬의 Camera / EventSystem / AudioListener 만 선택적으로
            // 끈다. GameObject 전체를 비활성화하면 MainMenuUI 자신이 꺼져서
            // 이후 OnDisconnected 같은 콜백이 못 돌아오기 때문.
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);

            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                    cam.enabled = false;
                foreach (var es in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))
                    es.enabled = false;
                foreach (var al in root.GetComponentsInChildren<AudioListener>(true))
                    al.enabled = false;
            }
        }

        private void OnDisconnected(string reason)
        {
            Debug.Log($"[Colosseum UI] Disconnected: {reason}");
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

            _btnMapSample = CreateToggleMapButton(rowObj.transform, "Sample", SelectableScene.SampleScene);
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
            ApplyToggleVisual(_btnMapSample, _selectedMap == SelectableScene.SampleScene);
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
            int v = PlayerPrefs.GetInt(MapPrefsKey, (int)SelectableScene.SampleScene);
            // Unspecified=0은 sentinel — user-selectable 아니므로 SampleScene fallback.
            if (System.Enum.IsDefined(typeof(SelectableScene), v) && (SelectableScene)v != SelectableScene.Unspecified)
                _selectedMap = (SelectableScene)v;
            else
                _selectedMap = SelectableScene.SampleScene;
        }

        private void SaveSelectedMap()
        {
            PlayerPrefs.SetInt(MapPrefsKey, (int)_selectedMap);
            PlayerPrefs.Save();
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
