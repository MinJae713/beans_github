using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using Colosseum.Card;
using Colosseum.Utils;
using System.Linq;
using MoreMountains.Feedbacks;

namespace Colosseum.UI
{
    public class CardSelectionUI : MonoBehaviour
    {
        private const int CardSlotCount = 3;

        [System.Serializable]
        private sealed class HudStyle
        {
            [Header("Frame")]
            public Vector2 panelSize = new Vector2(1080f, 640f);
            public Color backgroundColor = new Color(0.04f, 0.045f, 0.09f, 0.94f);

            [Header("Cards")]
            public Vector2 cardSize = new Vector2(270f, 360f);
            public float cardSpacing = 300f;
            public float cardVerticalOffset = -30f;
            public float[] cardAngles = { -7f, 0f, 7f };
            public Color cardBaseColor = new Color(0.10f, 0.10f, 0.18f, 0.96f);
            public Color cardDimColor = new Color(0.08f, 0.08f, 0.12f, 0.62f);
            public Color selectedOverlayColor = new Color(1f, 0.78f, 0f, 0.16f);
            public Vector2 iconSize = new Vector2(78f, 78f);

            [Header("Text")]
            public int titleFontSize = 34;
            public int timerFontSize = 20;
            public int waitingFontSize = 24;
            public int cardNameFontSize = 28;
            public int cardDescriptionFontSize = 17;
            public int rarityFontSize = 18;
            public int statusFontSize = 15;
            public int iconPlaceholderFontSize = 15;

            [Header("Palette")]
            public Color titleColor = new Color(0.89f, 0.88f, 0.99f, 1f);
            public Color bodyColor = new Color(0.86f, 0.76f, 0.68f, 1f);
            public Color mutedColor = new Color(0.58f, 0.61f, 0.72f, 1f);
            public Color confirmColor = new Color(1f, 0.55f, 0f, 1f);
            public Color urgentTimerColor = Color.red;
            public Color commonColor = new Color(0.64f, 0.57f, 0.48f, 1f);
            public Color rareColor = new Color(0.71f, 0.77f, 1f, 1f);
            public Color epicColor = new Color(0.72f, 0.35f, 1f, 1f);
            public Color legendaryColor = new Color(0.91f, 0.77f, 0f, 1f);
        }

        [Header("UI References")]
        [SerializeField] private GameObject _cardPanel;
        [SerializeField] private Button[] _cardButtons = new Button[CardSlotCount];
        [SerializeField] private TextMeshProUGUI[] _cardNames = new TextMeshProUGUI[CardSlotCount];
        [SerializeField] private TextMeshProUGUI[] _cardDescriptions = new TextMeshProUGUI[CardSlotCount];
        [SerializeField] private Image[] _cardRarityBorders = new Image[CardSlotCount];

        [Header("Figma HUD Presentation")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI[] _cardRarityLabels = new TextMeshProUGUI[CardSlotCount];
        [SerializeField] private TextMeshProUGUI[] _cardStatusLabels = new TextMeshProUGUI[CardSlotCount];
        [SerializeField] private TextMeshProUGUI[] _cardIconPlaceholderTexts = new TextMeshProUGUI[CardSlotCount];
        [SerializeField] private Image[] _cardAccentBars = new Image[CardSlotCount];
        [SerializeField] private Image[] _cardIconImages = new Image[CardSlotCount];
        [SerializeField] private Image[] _cardSelectedOverlays = new Image[CardSlotCount];

        [SerializeField] private MMFeedbacks _cardSelectFeedbacks;

        [Header("Timer / Waiting")]
        [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private TextMeshProUGUI _waitingText;

        [Header("HUD Style")]
        [SerializeField] private HudStyle _style = new HudStyle();

        private CardDeck _cardDeck;
        private bool _isShowing = false;
        private bool _selectedThisRound = false;

        private void OnEnable()
        {
            CardDeck.SpawnedInScene += HandleCardDeckSpawned;
            TryBindCardDeckFromScene();
        }

        private void OnDisable()
        {
            CardDeck.SpawnedInScene -= HandleCardDeckSpawned;
            UnbindCardDeck();
        }

        private void Start()
        {
            AutoBindReferences();
            ConfigureCardSelectionHud();
            TryBindCardDeckFromScene();

            var rootRect = transform as RectTransform;
            if (rootRect != null)
            {
                if (rootRect.localScale == Vector3.zero)
                {
                    rootRect.localScale = Vector3.one;
                }

                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = Vector2.zero;
            }

            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 150);
            }

            if (_cardPanel != null)
            {
                _cardPanel.SetActive(false);
            }

            for (int i = 0; i < _cardButtons.Length; i++)
            {
                if (_cardButtons[i] == null) continue;
                int index = i;
                _cardButtons[i].onClick.AddListener(() => OnCardSelected(index));
            }
        }

        private void AutoBindReferences()
        {
            if (_cardPanel == null)
            {
                Transform panelTransform = GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "CardPanel" || t.name == "RuntimeCardPanel");
                if (panelTransform != null)
                {
                    _cardPanel = panelTransform.gameObject;
                }
            }

            EnsureArraySizes();

            if (_cardPanel == null)
            {
                return;
            }

            if (_titleText == null)
            {
                _titleText = FindText("TitleText");
            }

            if (_timerText == null)
            {
                _timerText = FindText("TimerText");
            }

            if (_waitingText == null)
            {
                _waitingText = FindText("WaitingText");
            }

            for (int i = 0; i < CardSlotCount; i++)
            {
                Transform cardTransform = FindCardTransform(i);
                if (cardTransform == null)
                {
                    continue;
                }

                if (_cardButtons[i] == null)
                {
                    _cardButtons[i] = cardTransform.GetComponent<Button>();
                }

                if (_cardRarityBorders[i] == null)
                {
                    _cardRarityBorders[i] = cardTransform.GetComponent<Image>();
                }

                if (_cardNames[i] == null) _cardNames[i] = FindChildText(cardTransform, "CardName");
                if (_cardDescriptions[i] == null) _cardDescriptions[i] = FindChildText(cardTransform, "CardDesc");
                if (_cardRarityLabels[i] == null) _cardRarityLabels[i] = FindChildText(cardTransform, "RarityText");
                if (_cardStatusLabels[i] == null) _cardStatusLabels[i] = FindChildText(cardTransform, "StatusText");
                if (_cardIconPlaceholderTexts[i] == null) _cardIconPlaceholderTexts[i] = FindChildText(cardTransform, "CardIconPlaceholder");
                if (_cardAccentBars[i] == null) _cardAccentBars[i] = FindChildImage(cardTransform, "AccentBar");
                if (_cardIconImages[i] == null) _cardIconImages[i] = FindChildImage(cardTransform, "CardIcon");
                if (_cardSelectedOverlays[i] == null) _cardSelectedOverlays[i] = FindChildImage(cardTransform, "SelectedOverlay");
            }
        }

        private void EnsureArraySizes()
        {
            if (_cardButtons == null || _cardButtons.Length != CardSlotCount)
                _cardButtons = new Button[CardSlotCount];
            if (_cardNames == null || _cardNames.Length != CardSlotCount)
                _cardNames = new TextMeshProUGUI[CardSlotCount];
            if (_cardDescriptions == null || _cardDescriptions.Length != CardSlotCount)
                _cardDescriptions = new TextMeshProUGUI[CardSlotCount];
            if (_cardRarityBorders == null || _cardRarityBorders.Length != CardSlotCount)
                _cardRarityBorders = new Image[CardSlotCount];
            if (_cardRarityLabels == null || _cardRarityLabels.Length != CardSlotCount)
                _cardRarityLabels = new TextMeshProUGUI[CardSlotCount];
            if (_cardStatusLabels == null || _cardStatusLabels.Length != CardSlotCount)
                _cardStatusLabels = new TextMeshProUGUI[CardSlotCount];
            if (_cardIconPlaceholderTexts == null || _cardIconPlaceholderTexts.Length != CardSlotCount)
                _cardIconPlaceholderTexts = new TextMeshProUGUI[CardSlotCount];
            if (_cardAccentBars == null || _cardAccentBars.Length != CardSlotCount)
                _cardAccentBars = new Image[CardSlotCount];
            if (_cardIconImages == null || _cardIconImages.Length != CardSlotCount)
                _cardIconImages = new Image[CardSlotCount];
            if (_cardSelectedOverlays == null || _cardSelectedOverlays.Length != CardSlotCount)
                _cardSelectedOverlays = new Image[CardSlotCount];
        }

        private TextMeshProUGUI FindText(string objectName)
        {
            Transform t = GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(tr => tr.name == objectName);
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }

        private Transform FindCardTransform(int index)
        {
            return _cardPanel.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == $"Card{index}" || t.name == $"RuntimeCard{index}");
        }

        private static TextMeshProUGUI FindChildText(Transform root, string objectName)
        {
            Transform t = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(tr => tr.name == objectName);
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }

        private static Image FindChildImage(Transform root, string objectName)
        {
            Transform t = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(tr => tr.name == objectName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private void ConfigureCardSelectionHud()
        {
            if (_cardPanel == null)
            {
                return;
            }

            EnsureArraySizes();
            ConfigurePanelFrame();

            if (_titleText == null)
                _titleText = CreatePanelText("TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(620f, 48f), _style.titleFontSize, FontStyles.Bold, TextAlignmentOptions.Center, _style.titleColor);
            _titleText.text = "카드를 선택하세요";

            if (_timerText == null)
                _timerText = CreatePanelText("TimerText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(620f, 32f), _style.timerFontSize, FontStyles.Bold, TextAlignmentOptions.Center, _style.confirmColor);
            _timerText.text = "카드 선택까지 8초...";

            if (_waitingText == null)
                _waitingText = CreatePanelText("WaitingText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(520f, 42f), _style.waitingFontSize, FontStyles.Bold, TextAlignmentOptions.Center, _style.confirmColor);
            _waitingText.text = "확정 대기 중...";
            _waitingText.gameObject.SetActive(false);

            for (int i = 0; i < CardSlotCount; i++)
            {
                Transform cardTransform = FindCardTransform(i);
                if (cardTransform == null)
                {
                    continue;
                }

                ConfigureCardFrame(i, cardTransform);
            }
        }

        private void ConfigurePanelFrame()
        {
            var panelRect = _cardPanel.transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;
                panelRect.sizeDelta = _style.panelSize;
            }

            var panelImage = _cardPanel.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = _cardPanel.AddComponent<Image>();
            }
            panelImage.color = _style.backgroundColor;
            panelImage.raycastTarget = false;
        }

        private void ConfigureCardFrame(int index, Transform cardTransform)
        {
            var cardRect = cardTransform as RectTransform;
            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition = new Vector2((index - 1) * _style.cardSpacing, _style.cardVerticalOffset);
                cardRect.sizeDelta = _style.cardSize;
                cardRect.localEulerAngles = new Vector3(0f, 0f, GetCardAngle(index));
            }

            var cardImage = cardTransform.GetComponent<Image>();
            if (cardImage == null)
            {
                cardImage = cardTransform.gameObject.AddComponent<Image>();
            }
            cardImage.color = _style.cardBaseColor;
            _cardRarityBorders[index] = cardImage;

            var button = cardTransform.GetComponent<Button>();
            if (button != null)
            {
                button.targetGraphic = cardImage;
                button.colors = CreateButtonColors();
                _cardButtons[index] = button;
            }

            if (_cardAccentBars[index] == null)
                _cardAccentBars[index] = CreateChildImage(cardTransform, "AccentBar", new Vector2(0.08f, 0.93f), new Vector2(0.92f, 0.93f), Vector2.zero, new Vector2(0f, 8f), _style.confirmColor);
            if (_cardRarityLabels[index] == null)
                _cardRarityLabels[index] = CreateChildText(cardTransform, "RarityText", new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero, _style.rarityFontSize, FontStyles.Bold, TextAlignmentOptions.Left, _style.confirmColor);
            if (_cardIconImages[index] == null)
                _cardIconImages[index] = CreateChildImage(cardTransform, "CardIcon", new Vector2(0.5f, 0.64f), new Vector2(0.5f, 0.64f), Vector2.zero, _style.iconSize, _style.confirmColor * 0.65f);
            if (_cardIconPlaceholderTexts[index] == null)
                _cardIconPlaceholderTexts[index] = CreateChildText(cardTransform, "CardIconPlaceholder", new Vector2(0.34f, 0.55f), new Vector2(0.66f, 0.73f), Vector2.zero, Vector2.zero, _style.iconPlaceholderFontSize, FontStyles.Bold, TextAlignmentOptions.Center, _style.mutedColor);
            if (_cardStatusLabels[index] == null)
                _cardStatusLabels[index] = CreateChildText(cardTransform, "StatusText", new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.14f), Vector2.zero, Vector2.zero, _style.statusFontSize, FontStyles.Bold, TextAlignmentOptions.Center, _style.mutedColor);
            if (_cardSelectedOverlays[index] == null)
                _cardSelectedOverlays[index] = CreateChildImage(cardTransform, "SelectedOverlay", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, _style.selectedOverlayColor);
            _cardSelectedOverlays[index].raycastTarget = false;
            _cardSelectedOverlays[index].transform.SetAsFirstSibling();
            _cardSelectedOverlays[index].gameObject.SetActive(false);

            ConfigureTextRect(_cardNames[index], new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.49f), _style.cardNameFontSize, FontStyles.Bold, TextAlignmentOptions.Center, _style.titleColor);
            ConfigureTextRect(_cardDescriptions[index], new Vector2(0.10f, 0.18f), new Vector2(0.90f, 0.36f), _style.cardDescriptionFontSize, FontStyles.Normal, TextAlignmentOptions.Top, _style.bodyColor);
        }

        private static ColorBlock CreateButtonColors()
        {
            return new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1f, 0.93f, 0.68f, 1f),
                pressedColor = new Color(1f, 0.75f, 0.28f, 1f),
                selectedColor = new Color(1f, 0.88f, 0.5f, 1f),
                disabledColor = new Color(0.55f, 0.55f, 0.62f, 0.55f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };
        }

        private TextMeshProUGUI CreatePanelText(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, int fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(_cardPanel.transform, false);
            textObject.layer = _cardPanel.layer;
            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = true;
            return text;
        }

        private static TextMeshProUGUI CreateChildText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, int fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.layer = parent.gameObject.layer;
            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = true;
            return text;
        }

        private static Image CreateChildImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);
            imageObject.layer = parent.gameObject.layer;
            var rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void ConfigureTextRect(TextMeshProUGUI text, Vector2 anchorMin, Vector2 anchorMax, int fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            if (text == null)
            {
                return;
            }

            if (text.transform is RectTransform rect)
            {
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = true;
        }

        private float GetCardAngle(int index)
        {
            if (_style.cardAngles == null || index < 0 || index >= _style.cardAngles.Length)
            {
                return 0f;
            }

            return _style.cardAngles[index];
        }

        private void HandleCardDeckSpawned(CardDeck deck)
        {
            if (deck == null || deck.gameObject.scene != gameObject.scene)
            {
                return;
            }

            BindCardDeck(deck);
        }

        private void TryBindCardDeckFromScene()
        {
            if (_cardDeck != null)
            {
                return;
            }

            BindCardDeck(SceneSearch.FindInScene<CardDeck>(gameObject.scene));
        }

        private void BindCardDeck(CardDeck deck)
        {
            if (deck == null || deck == _cardDeck)
            {
                return;
            }

            if (_cardDeck != null)
            {
                _cardDeck.OnSelectionRejected -= HandleSelectionRejected;
            }

            _cardDeck = deck;
            _cardDeck.OnSelectionRejected += HandleSelectionRejected;
        }

        private void UnbindCardDeck()
        {
            if (_cardDeck == null)
            {
                return;
            }

            _cardDeck.OnSelectionRejected -= HandleSelectionRejected;
            _cardDeck = null;
        }

        private void Update()
        {
            if (_cardDeck == null) return;

            if (_cardDeck.Object == null || !_cardDeck.Object.IsValid) return;

            if (_cardPanel == null)
            {
                AutoBindReferences();
                if (_cardPanel == null)
                {
                    EnsureRuntimePanel();
                    if (_cardPanel == null) return;
                }
            }

            bool shouldShow = IsLocalDrawingPlayer();

            if (shouldShow && !_isShowing)
            {
                ShowCards();
            }
            else if (!shouldShow && _isShowing)
            {
                HideCards();
            }

            if (_isShowing && !_selectedThisRound && _timerText != null
                && _cardDeck.Runner != null && _cardDeck.Object.IsValid)
            {
                float remaining = _cardDeck.GetRemainingSeconds(_cardDeck.Runner.LocalPlayer);
                int remainingSeconds = Mathf.CeilToInt(remaining);
                _timerText.text = $"카드 선택까지 {remainingSeconds}초...";
                _timerText.color = remaining <= 3f ? _style.urgentTimerColor : _style.confirmColor;
            }
        }

        private bool IsLocalDrawingPlayer()
        {
            if (_cardDeck == null || _cardDeck.Runner == null) return false;

            if (_cardDeck.Runner.IsServer && !_cardDeck.Runner.IsClient) return false;

            return _cardDeck.HasPendingDraw(_cardDeck.Runner.LocalPlayer);
        }

        public void ShowCards()
        {
            TryBindCardDeckFromScene();

            if (_cardPanel == null)
            {
                EnsureRuntimePanel();
            }

            if (_cardPanel == null)
            {
                Debug.LogWarning("[Colosseum] CardSelectionUI: _cardPanel is still null after EnsureRuntimePanel!");
                return;
            }

            AutoBindReferences();
            ConfigureCardSelectionHud();

            _isShowing = true;
            _selectedThisRound = false;
            _cardPanel.SetActive(true);

            if (_titleText != null)
            {
                _titleText.gameObject.SetActive(true);
                _titleText.text = "카드를 선택하세요";
            }

            if (_timerText != null)
            {
                _timerText.gameObject.SetActive(true);
                _timerText.color = _style.confirmColor;
            }

            if (_waitingText != null)
                _waitingText.gameObject.SetActive(false);

            if (_cardDeck != null && _cardDeck.Runner != null)
            {
                for (int i = 0; i < CardSlotCount; i++)
                {
                    CardData card = _cardDeck.GetDrawnCard(_cardDeck.Runner.LocalPlayer, i);
                    SetCardVisual(i, card, false, false);
                }
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void HideCards()
        {
            if (_cardPanel == null)
            {
                return;
            }

            _isShowing = false;
            _selectedThisRound = false;
            _cardPanel.SetActive(false);

            if (_titleText != null) _titleText.gameObject.SetActive(false);
            if (_timerText != null) _timerText.gameObject.SetActive(false);
            if (_waitingText != null) _waitingText.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            UnbindCardDeck();
        }

        private void HandleSelectionRejected()
        {
            _selectedThisRound = false;
            for (int i = 0; i < _cardButtons.Length; i++)
            {
                CardData card = _cardDeck != null && _cardDeck.Runner != null
                    ? _cardDeck.GetDrawnCard(_cardDeck.Runner.LocalPlayer, i)
                    : null;
                SetCardVisual(i, card, false, false);
            }
            if (_timerText != null) _timerText.gameObject.SetActive(true);
            if (_waitingText != null) _waitingText.gameObject.SetActive(false);
        }

        private void EnsureRuntimePanel()
        {
            if (_cardPanel != null)
            {
                return;
            }

            Debug.Log("[Colosseum] CardSelectionUI: Creating runtime panel (no prefab panel found)");

            EnsureArraySizes();

            var panel = new GameObject("RuntimeCardPanel");
            panel.transform.SetParent(transform, false);
            panel.layer = gameObject.layer;
            panel.AddComponent<RectTransform>();
            panel.AddComponent<Image>();
            _cardPanel = panel;

            ConfigurePanelFrame();

            for (int i = 0; i < CardSlotCount; i++)
            {
                var cardObject = new GameObject($"RuntimeCard{i}");
                cardObject.transform.SetParent(panel.transform, false);
                cardObject.layer = gameObject.layer;
                cardObject.AddComponent<RectTransform>();
                cardObject.AddComponent<Image>();
                _cardButtons[i] = cardObject.AddComponent<Button>();
                int index = i;
                _cardButtons[i].onClick.AddListener(() => OnCardSelected(index));

                _cardNames[i] = CreateChildText(cardObject.transform, "CardName", new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.49f), Vector2.zero, Vector2.zero, _style.cardNameFontSize, FontStyles.Bold, TextAlignmentOptions.Center, _style.titleColor);
                _cardDescriptions[i] = CreateChildText(cardObject.transform, "CardDesc", new Vector2(0.10f, 0.18f), new Vector2(0.90f, 0.36f), Vector2.zero, Vector2.zero, _style.cardDescriptionFontSize, FontStyles.Normal, TextAlignmentOptions.Top, _style.bodyColor);
            }

            ConfigureCardSelectionHud();
            panel.SetActive(false);
        }

        private void OnCardSelected(int index)
        {
            if (_cardDeck == null) return;
            if (_cardDeck.Runner == null) return;
            if (_selectedThisRound) return;
            if (!_cardDeck.HasPendingDraw(_cardDeck.Runner.LocalPlayer)) return;
            if (_cardDeck.GetDrawnCard(_cardDeck.Runner.LocalPlayer, index) == null) return;

            _cardDeck.Rpc_SelectCard(index);
            _cardSelectFeedbacks?.PlayFeedbacks();

            _selectedThisRound = true;
            for (int i = 0; i < CardSlotCount; i++)
            {
                CardData card = _cardDeck.GetDrawnCard(_cardDeck.Runner.LocalPlayer, i);
                SetCardVisual(i, card, selected: i == index, dimmed: i != index);
            }

            if (_timerText != null) _timerText.gameObject.SetActive(false);
            if (_waitingText != null)
            {
                _waitingText.text = "SELECTED  //  확정 대기 중...";
                _waitingText.gameObject.SetActive(true);
            }
        }

        private void SetCardVisual(int index, CardData card, bool selected, bool dimmed)
        {
            bool hasCard = card != null;
            Color rarityColor = hasCard ? GetRarityColor(card.rarity) : _style.mutedColor;
            Color cardColor = hasCard ? Color.Lerp(_style.cardBaseColor, rarityColor, selected ? 0.28f : 0.10f) : _style.cardDimColor;
            cardColor.a = dimmed ? 0.52f : 0.96f;

            if (_cardButtons[index] != null)
            {
                _cardButtons[index].interactable = hasCard && !_selectedThisRound;
            }

            if (_cardRarityBorders[index] != null)
            {
                _cardRarityBorders[index].color = cardColor;
            }

            if (_cardAccentBars[index] != null)
            {
                _cardAccentBars[index].color = rarityColor;
            }

            if (_cardRarityLabels[index] != null)
            {
                _cardRarityLabels[index].text = hasCard ? GetRarityLabel(card.rarity) : "NO SIGNAL";
                _cardRarityLabels[index].color = rarityColor;
            }

            if (_cardNames[index] != null)
            {
                _cardNames[index].text = hasCard ? card.cardName : "-";
                _cardNames[index].color = dimmed ? _style.mutedColor : _style.titleColor;
            }

            if (_cardDescriptions[index] != null)
            {
                _cardDescriptions[index].text = hasCard ? card.description : "선택 가능한 카드가 없습니다.";
                _cardDescriptions[index].color = dimmed ? _style.mutedColor : _style.bodyColor;
            }

            if (_cardIconImages[index] != null)
            {
                _cardIconImages[index].sprite = hasCard ? card.cardIcon : null;
                _cardIconImages[index].color = hasCard && card.cardIcon != null ? Color.white : new Color(rarityColor.r, rarityColor.g, rarityColor.b, dimmed ? 0.16f : 0.32f);
            }

            if (_cardIconPlaceholderTexts[index] != null)
            {
                bool shouldShowPlaceholder = !hasCard || card.cardIcon == null;
                _cardIconPlaceholderTexts[index].gameObject.SetActive(shouldShowPlaceholder);
                _cardIconPlaceholderTexts[index].text = GetIconPlaceholderText(card);
                _cardIconPlaceholderTexts[index].color = dimmed ? _style.mutedColor : rarityColor;
            }

            if (_cardStatusLabels[index] != null)
            {
                _cardStatusLabels[index].text = selected ? "SELECTED" : hasCard ? "READY TO ENGAGE" : "OFFLINE";
                _cardStatusLabels[index].color = selected ? _style.confirmColor : _style.mutedColor;
            }

            if (_cardSelectedOverlays[index] != null)
            {
                _cardSelectedOverlays[index].gameObject.SetActive(selected);
                _cardSelectedOverlays[index].color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.18f);
            }
        }

        private static string GetRarityLabel(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return "COMMON";
                case CardRarity.Rare: return "RARE";
                case CardRarity.Epic: return "EPIC";
                case CardRarity.Legendary: return "LEGENDARY";
                default: return "UNKNOWN";
            }
        }

        private static string GetIconPlaceholderText(CardData card)
        {
            if (card == null)
            {
                return "NO DATA";
            }

            return "NO ICON";
        }

        private Color GetRarityColor(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return _style.commonColor;
                case CardRarity.Rare: return _style.rareColor;
                case CardRarity.Epic: return _style.epicColor;
                case CardRarity.Legendary: return _style.legendaryColor;
                default: return Color.white;
            }
        }
    }
}
