using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using Colosseum.Card;
using Colosseum.Utils;
using System.Collections;
using System.Linq;
using MoreMountains.Feedbacks;

namespace Colosseum.UI
{
    public class CardSelectionUI : MonoBehaviour
    {
        private const int CardSlotCount = 3;
        private static readonly Color TransparentTextBoxColor = new Color(1f, 1f, 1f, 0f);
        private static readonly Color CardTextColor = Color.white;

        [System.Serializable]
        private sealed class HudStyle
        {
            [Header("Frame")]
            public Vector2 panelSize = new Vector2(1080f, 640f);
            // (A303-362) panel 자체를 캔버스 중심에서 +60 위로 띄움 — title부터 카드 하단까지 그룹의 시각 무게중심이
            // 캔버스 중심보다 ~47px 아래 (title top +276, card bottom -370) 라서 살짝 아래로 느껴진다는 피드백 반영.
            public float panelVerticalOffset = 60f;
            // (A303-362) 모달 박스 윤곽 제거 — 컨테이너 자체는 투명, 풀스크림으로 화면 전체 어둡게.
            // 이전엔 backgroundColor 가 모달 박스 배경이었음.
            public Color backgroundColor = new Color(0f, 0f, 0f, 0f);
            // 풀스크림 색 — 카드 선택 중 화면 전체를 살짝 어둡게 덮어 "지금 카드 타임" 표시.
            // 알파 0.89: 맵·캐릭터 윤곽이 비치도록 의도 (이전 모달 0.94 대비 5%p 더 투명).
            public Color scrimColor = new Color(0.04f, 0.045f, 0.09f, 0.89f);

            [Header("Cards")]
            // (A303-362) 카드 1.5배 확대. 간격 435 = 카드 폭 405 + 카드 사이 빈 공간 30 (이전 30 그대로).
            // 세로 오프셋 -100 = 1.5x로 키워진 카드 상단이 timer 텍스트("카드 선택까지 X초...") 와 겹치지 않도록 아래로 이동.
            public Vector2 cardSize = new Vector2(405f, 540f);
            public float cardSpacing = 435f;
            public float cardVerticalOffset = -100f;
            public float[] cardAngles = { 0f, 0f, 0f };
            public float frameOutlineThickness = 4f;
            public Color cardBaseColor = new Color(0.10f, 0.10f, 0.18f, 0.96f);
            public Color cardDimColor = new Color(0.08f, 0.08f, 0.12f, 0.62f);
            public Color selectedOverlayColor = new Color(1f, 0.78f, 0f, 0.16f);
            public Vector2 iconSize = new Vector2(150f, 130f);
            public Color textPanelColor = new Color(0.015f, 0.022f, 0.038f, 0.72f);
            public Color textPanelOutlineColor = new Color(0.70f, 0.78f, 0.92f, 0.55f);

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

        [System.Serializable]
        private sealed class CardFrameSpriteSet
        {
            public Sprite commonFrame;
            public Sprite rareFrame;
            public Sprite epicFrame;
            public Sprite legendaryFrame;

            public Sprite GetFrameSprite(CardRarity rarity)
            {
                switch (rarity)
                {
                    case CardRarity.Common: return commonFrame;
                    case CardRarity.Rare: return rareFrame;
                    case CardRarity.Epic: return epicFrame;
                    case CardRarity.Legendary: return legendaryFrame;
                    default: return commonFrame;
                }
            }
        }

        public readonly struct CardVisualAssembly
        {
            public CardVisualAssembly(bool hasCard, CardRarity rarity, int frameVariantIndex, Sprite illustration)
            {
                HasCard = hasCard;
                Rarity = rarity;
                FrameVariantIndex = frameVariantIndex;
                Illustration = illustration;
            }

            public bool HasCard { get; }
            public CardRarity Rarity { get; }
            public int FrameVariantIndex { get; }
            public Sprite Illustration { get; }
            public bool HasIllustration => Illustration != null;
        }

        [Header("UI References")]
        [SerializeField] private GameObject _cardPanel;
        // (A303-362) 풀스크림 — 카드 선택 중 + 상대 대기 중 화면 전체를 어둡게 덮음. _cardPanel 형제, sibling order는 카드보다 뒤.
        private GameObject _scrim;
        // (A303-362) 상대 카드 선택 대기 패널 — 로컬이 chooser 가 아니고 상대가 HasPendingDraw 일 때 표시.
        private GameObject _waitingPanel;
        private TextMeshProUGUI _waitingPanelText;
        private bool _waitingShowing;
        private Coroutine _waitingDotsCo;
        private const string WaitingMessageBase = "상대가 카드 선택 중";
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
        [SerializeField] private Image[] _cardRarityBoxes = new Image[CardSlotCount];
        [SerializeField] private Image[] _cardNameBoxes = new Image[CardSlotCount];
        [SerializeField] private Image[] _cardDescriptionBoxes = new Image[CardSlotCount];
        [SerializeField] private Image[] _cardSelectedOverlays = new Image[CardSlotCount];

        [SerializeField] private MMFeedbacks _cardSelectFeedbacks;

        [Header("Timer / Waiting")]
        [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private TextMeshProUGUI _waitingText;

        [Header("HUD Style")]
        [SerializeField] private HudStyle _style = new HudStyle();

        [Header("Card Assembly")]
        [SerializeField] private CardFrameSpriteSet _frameSprites = new CardFrameSpriteSet();

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
            // Unity 가 비활성 시 코루틴을 자동 stop 하지만 우리 reference 는 stale 로 남아
            // 재활성 후 Update 의 분기가 다시 ShowWaitingForOpponent 를 안 부르고 점 애니가 안 돌아가는 회귀가 생김.
            // 명시적으로 null + 플래그 리셋해 다음 Update 에서 정상 재진입하도록.
            _waitingDotsCo = null;
            _waitingShowing = false;
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
                if (_cardRarityBoxes[i] == null) _cardRarityBoxes[i] = FindChildImage(cardTransform, "RarityBox");
                if (_cardNameBoxes[i] == null) _cardNameBoxes[i] = FindChildImage(cardTransform, "CardNameBox");
                if (_cardDescriptionBoxes[i] == null) _cardDescriptionBoxes[i] = FindChildImage(cardTransform, "CardDescriptionBox");
                if (_cardIconImages[i] == null)
                {
                    _cardIconImages[i] = FindChildImage(cardTransform, "CardIllustration");
                    if (_cardIconImages[i] == null)
                    {
                        _cardIconImages[i] = FindChildImage(cardTransform, "CardIcon");
                    }
                }
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
            if (_cardRarityBoxes == null || _cardRarityBoxes.Length != CardSlotCount)
                _cardRarityBoxes = new Image[CardSlotCount];
            if (_cardNameBoxes == null || _cardNameBoxes.Length != CardSlotCount)
                _cardNameBoxes = new Image[CardSlotCount];
            if (_cardDescriptionBoxes == null || _cardDescriptionBoxes.Length != CardSlotCount)
                _cardDescriptionBoxes = new Image[CardSlotCount];
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
                panelRect.anchoredPosition = new Vector2(0f, _style.panelVerticalOffset);
                panelRect.sizeDelta = _style.panelSize;
            }

            var panelImage = _cardPanel.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = _cardPanel.AddComponent<Image>();
            }
            // (A303-362) 모달 박스 윤곽 제거: 컨테이너 자체는 투명. 어둠은 풀스크림이 담당.
            // 직렬화된 prefab override 가 옛 backgroundColor(짙은 푸른빛 0.94)일 수 있어 명시적으로 Color.clear 강제.
            panelImage.color = Color.clear;
            panelImage.raycastTarget = false;

            EnsureScrim();
        }

        // (A303-362) 화면 전체를 살짝 어둡게 덮는 풀스크림 보장. _cardPanel 형제로 만들고 sibling order는 맨 앞(=가장 뒤로 렌더)으로.
        // raycastTarget false — 게임 입력 차단은 PlayerController/Gun 의 IsCardSelectionPending 가드가 담당.
        private void EnsureScrim()
        {
            Transform parent = _cardPanel != null ? _cardPanel.transform.parent : transform;
            if (parent == null) return;

            if (_scrim == null)
            {
                Transform existing = parent.Find("CardSelectionScrim");
                if (existing != null)
                {
                    _scrim = existing.gameObject;
                }
            }

            if (_scrim == null)
            {
                _scrim = new GameObject("CardSelectionScrim");
                _scrim.transform.SetParent(parent, false);
                _scrim.layer = _cardPanel != null ? _cardPanel.layer : gameObject.layer;
                _scrim.AddComponent<RectTransform>();
                _scrim.AddComponent<Image>();
            }

            var scrimRect = _scrim.transform as RectTransform;
            if (scrimRect != null)
            {
                scrimRect.anchorMin = Vector2.zero;
                scrimRect.anchorMax = Vector2.one;
                scrimRect.pivot = new Vector2(0.5f, 0.5f);
                scrimRect.offsetMin = Vector2.zero;
                scrimRect.offsetMax = Vector2.zero;
            }

            var scrimImage = _scrim.GetComponent<Image>();
            if (scrimImage != null)
            {
                scrimImage.color = _style.scrimColor;
                scrimImage.raycastTarget = false;
            }

            // 카드/텍스트가 위에 보이도록 스크림을 _cardPanel 보다 앞 sibling 으로 (=먼저 렌더 = 뒤에 깔림).
            _scrim.transform.SetAsFirstSibling();

            // 스크림 활성 상태는 Update 가 (_isShowing || _waitingShowing) 으로 매 프레임 중앙 갱신.
            // EnsureScrim 은 생성 직후 항상 inactive 로 두어, Start 시점의 호출 순서로 인해 다른 화면 위에 스크림이 남는 회귀를 차단.
            _scrim.SetActive(false);
        }

        // (A303-362) 상대 카드 선택 대기 화면. _cardPanel/_scrim 형제로 만들고 중앙에 cyan 9-sliced 박스.
        // 표시/활성은 ShowWaitingForOpponent/HideWaitingForOpponent 가 관리.
        private void EnsureWaitingPanel()
        {
            Transform parent = _cardPanel != null ? _cardPanel.transform.parent : transform;
            if (parent == null) return;

            if (_waitingPanel == null)
            {
                Transform existing = parent.Find("CardSelectionWaitingPanel");
                if (existing != null) _waitingPanel = existing.gameObject;
            }

            if (_waitingPanel == null)
            {
                _waitingPanel = new GameObject("CardSelectionWaitingPanel");
                _waitingPanel.transform.SetParent(parent, false);
                _waitingPanel.layer = _cardPanel != null ? _cardPanel.layer : gameObject.layer;
                _waitingPanel.AddComponent<RectTransform>();
                _waitingPanel.AddComponent<Image>();
            }

            var waitRect = _waitingPanel.transform as RectTransform;
            if (waitRect != null)
            {
                waitRect.anchorMin = new Vector2(0.5f, 0.5f);
                waitRect.anchorMax = new Vector2(0.5f, 0.5f);
                waitRect.pivot = new Vector2(0.5f, 0.5f);
                waitRect.anchoredPosition = Vector2.zero;
                waitRect.sizeDelta = new Vector2(800f, 200f);
            }

            var waitImage = _waitingPanel.GetComponent<Image>();
            if (waitImage != null)
            {
                // 9-slice 외곽선 sprite (Resources/UI/MainMenu/cyber_btn_outline) 재사용 — MainMenu cyber 룩과 톤 일치.
                if (waitImage.sprite == null)
                {
                    waitImage.sprite = Resources.Load<Sprite>("UI/MainMenu/cyber_btn_outline");
                }
                waitImage.type = Image.Type.Sliced;
                waitImage.color = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 1f); // #7CE6D8 cyan
                waitImage.raycastTarget = false;
            }

            // MessageText 자식 (TMP) — 부모 가득 채우고 중앙 정렬.
            if (_waitingPanelText == null)
            {
                Transform existingText = _waitingPanel.transform.Find("MessageText");
                if (existingText != null) _waitingPanelText = existingText.GetComponent<TextMeshProUGUI>();
            }
            if (_waitingPanelText == null)
            {
                var textObj = new GameObject("MessageText");
                textObj.transform.SetParent(_waitingPanel.transform, false);
                textObj.layer = _waitingPanel.layer;
                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                _waitingPanelText = textObj.AddComponent<TextMeshProUGUI>();
                _waitingPanelText.fontSize = 36;
                _waitingPanelText.fontStyle = FontStyles.Bold;
                _waitingPanelText.color = Color.white;
                _waitingPanelText.alignment = TextAlignmentOptions.Center;
                _waitingPanelText.text = WaitingMessageBase;
                _waitingPanelText.raycastTarget = false;
            }

            _waitingPanel.SetActive(false);
        }

        // (A303-362) "상대가 카드 선택 중" 뒤 점 0~3개 순환 애니메이션. 0.4s 간격.
        private IEnumerator AnimateWaitingDots()
        {
            string[] suffixes = { "", ".", "..", "..." };
            int i = 0;
            while (true)
            {
                if (_waitingPanelText != null)
                    _waitingPanelText.text = WaitingMessageBase + suffixes[i % suffixes.Length];
                i++;
                yield return new WaitForSeconds(0.4f);
            }
        }

        private void ShowWaitingForOpponent()
        {
            EnsureScrim();
            EnsureWaitingPanel();
            if (_waitingPanel != null) _waitingPanel.SetActive(true);
            _waitingShowing = true;
            if (_waitingDotsCo == null && isActiveAndEnabled)
            {
                _waitingDotsCo = StartCoroutine(AnimateWaitingDots());
            }
        }

        private void HideWaitingForOpponent()
        {
            if (_waitingPanel != null) _waitingPanel.SetActive(false);
            _waitingShowing = false;
            if (_waitingDotsCo != null)
            {
                StopCoroutine(_waitingDotsCo);
                _waitingDotsCo = null;
            }
        }

        // (A303-362) 로컬 외 ActivePlayer 중 누구든 HasPendingDraw 이면 true.
        private bool IsOpponentDrawing()
        {
            if (_cardDeck == null || _cardDeck.Runner == null) return false;
            PlayerRef localPlayer = _cardDeck.Runner.LocalPlayer;
            foreach (var p in _cardDeck.Runner.ActivePlayers)
            {
                if (p == localPlayer) continue;
                if (_cardDeck.HasPendingDraw(p)) return true;
            }
            return false;
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
            cardImage.preserveAspect = false;
            _cardRarityBorders[index] = cardImage;

            ConfigureFrameOutline(cardTransform, _style.mutedColor);

            var button = cardTransform.GetComponent<Button>();
            if (button != null)
            {
                button.targetGraphic = cardImage;
                button.colors = CreateButtonColors();
                _cardButtons[index] = button;
            }

            if (_cardAccentBars[index] == null)
                _cardAccentBars[index] = CreateChildImage(cardTransform, "AccentBar", new Vector2(0.08f, 0.93f), new Vector2(0.92f, 0.93f), Vector2.zero, new Vector2(0f, 8f), _style.confirmColor);
            if (_cardRarityBoxes[index] == null)
                _cardRarityBoxes[index] = CreateChildImage(cardTransform, "RarityBox", new Vector2(0.105f, 0.852f), new Vector2(0.46f, 0.925f), Vector2.zero, Vector2.zero, _style.textPanelColor);
            ConfigureBoxRect(_cardRarityBoxes[index], new Vector2(0.105f, 0.852f), new Vector2(0.46f, 0.925f), _style.confirmColor, 0.8f);
            if (_cardRarityLabels[index] == null)
                _cardRarityLabels[index] = CreateChildText(cardTransform, "RarityText", new Vector2(0.125f, 0.852f), new Vector2(0.44f, 0.925f), Vector2.zero, Vector2.zero, _style.rarityFontSize, FontStyles.Bold, TextAlignmentOptions.Left, CardTextColor);
            ConfigureTextRect(_cardRarityLabels[index], new Vector2(0.125f, 0.852f), new Vector2(0.44f, 0.925f), _style.rarityFontSize, FontStyles.Bold, TextAlignmentOptions.Left, CardTextColor);
            if (_cardIconImages[index] == null)
                _cardIconImages[index] = CreateChildImage(cardTransform, "CardIllustration", new Vector2(0.5f, 0.605f), new Vector2(0.5f, 0.605f), Vector2.zero, _style.iconSize, _style.confirmColor * 0.65f);
            ConfigureImageRect(_cardIconImages[index], new Vector2(0.5f, 0.605f), new Vector2(0.5f, 0.605f), Vector2.zero, _style.iconSize);
            _cardIconImages[index].preserveAspect = true;
            if (_cardIconPlaceholderTexts[index] == null)
                _cardIconPlaceholderTexts[index] = CreateChildText(cardTransform, "CardIconPlaceholder", new Vector2(0.20f, 0.52f), new Vector2(0.80f, 0.69f), Vector2.zero, Vector2.zero, _style.iconPlaceholderFontSize, FontStyles.Bold, TextAlignmentOptions.Center, _style.mutedColor);
            if (_cardStatusLabels[index] == null)
                _cardStatusLabels[index] = CreateChildText(cardTransform, "StatusText", new Vector2(0.12f, 0.055f), new Vector2(0.88f, 0.105f), Vector2.zero, Vector2.zero, _style.statusFontSize, FontStyles.Bold, TextAlignmentOptions.Center, _style.mutedColor);
            if (_cardSelectedOverlays[index] == null)
                _cardSelectedOverlays[index] = CreateChildImage(cardTransform, "SelectedOverlay", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, _style.selectedOverlayColor);
            _cardSelectedOverlays[index].raycastTarget = false;
            _cardSelectedOverlays[index].transform.SetAsFirstSibling();
            _cardSelectedOverlays[index].gameObject.SetActive(false);

            if (_cardNameBoxes[index] == null)
                _cardNameBoxes[index] = CreateChildImage(cardTransform, "CardNameBox", new Vector2(0.145f, 0.335f), new Vector2(0.855f, 0.415f), Vector2.zero, Vector2.zero, _style.textPanelColor);
            ConfigureBoxRect(_cardNameBoxes[index], new Vector2(0.145f, 0.335f), new Vector2(0.855f, 0.415f), _style.textPanelOutlineColor, 0.6f);

            if (_cardDescriptionBoxes[index] == null)
                _cardDescriptionBoxes[index] = CreateChildImage(cardTransform, "CardDescriptionBox", new Vector2(0.13f, 0.125f), new Vector2(0.87f, 0.315f), Vector2.zero, Vector2.zero, _style.textPanelColor);
            ConfigureBoxRect(_cardDescriptionBoxes[index], new Vector2(0.13f, 0.125f), new Vector2(0.87f, 0.315f), _style.textPanelOutlineColor, 0.45f);

            ConfigureTextRect(_cardNames[index], new Vector2(0.16f, 0.345f), new Vector2(0.84f, 0.405f), _style.cardNameFontSize, FontStyles.Bold, TextAlignmentOptions.Center, CardTextColor);
            ConfigureTextRect(_cardDescriptions[index], new Vector2(0.155f, 0.145f), new Vector2(0.845f, 0.295f), _style.cardDescriptionFontSize, FontStyles.Normal, TextAlignmentOptions.Midline, CardTextColor);
            MoveTextLayersToFront(index);
        }

        private void ConfigureFrameOutline(Transform cardTransform, Color color)
        {
            var outline = cardTransform.GetComponent<Outline>();
            if (outline == null)
            {
                outline = cardTransform.gameObject.AddComponent<Outline>();
            }

            outline.effectDistance = new Vector2(_style.frameOutlineThickness, -_style.frameOutlineThickness);
            outline.effectColor = color;
            outline.useGraphicAlpha = true;
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

        private static void ConfigureImageRect(Image image, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (image == null || !(image.transform is RectTransform rect))
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void ConfigureBoxRect(Image image, Vector2 anchorMin, Vector2 anchorMax, Color outlineColor, float outlineAlpha)
        {
            ConfigureImageRect(image, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            if (image == null)
            {
                return;
            }

            image.raycastTarget = false;
            image.color = TransparentTextBoxColor;
            var outline = image.GetComponent<Outline>();
            if (outline == null)
            {
                outline = image.gameObject.AddComponent<Outline>();
            }

            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.effectColor = TransparentTextBoxColor;
            outline.useGraphicAlpha = true;
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
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Max(10, fontSize - 8);
        }

        private void MoveTextLayersToFront(int index)
        {
            if (_cardRarityLabels[index] != null) _cardRarityLabels[index].transform.SetAsLastSibling();
            if (_cardNames[index] != null) _cardNames[index].transform.SetAsLastSibling();
            if (_cardDescriptions[index] != null) _cardDescriptions[index].transform.SetAsLastSibling();
            if (_cardStatusLabels[index] != null) _cardStatusLabels[index].transform.SetAsLastSibling();
            if (_cardIconPlaceholderTexts[index] != null) _cardIconPlaceholderTexts[index].transform.SetAsLastSibling();
        }

        private float GetCardAngle(int index)
        {
            return 0f;
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

            bool localIsChooser = IsLocalDrawingPlayer();
            bool opponentChoosing = !localIsChooser && IsOpponentDrawing();

            // 카드 모달 토글
            if (localIsChooser && !_isShowing)
            {
                ShowCards();
            }
            else if (!localIsChooser && _isShowing)
            {
                HideCards();
            }

            // 상대 대기 화면 토글 (로컬이 chooser 가 아니고 상대가 뽑는 중일 때만)
            if (opponentChoosing && !_waitingShowing)
            {
                ShowWaitingForOpponent();
            }
            else if (!opponentChoosing && _waitingShowing)
            {
                HideWaitingForOpponent();
            }

            // 스크림은 두 모드 중 하나라도 켜져있을 때만 활성. ShowCards/HideCards 의 명시 토글 대신 여기서 단일 권한 관리.
            if (_scrim != null) _scrim.SetActive(_isShowing || _waitingShowing);

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

            PlayerRef localPlayer = _cardDeck.Runner.LocalPlayer;
            return ShouldShowForLocalDrawingPlayer(
                localPlayer,
                _cardDeck.HasPendingDraw(localPlayer));
        }

        public static bool ShouldShowForLocalDrawingPlayer(PlayerRef localPlayer, bool hasPendingDraw)
        {
            return localPlayer != default && hasPendingDraw;
        }

        public static CardVisualAssembly ResolveCardVisualAssembly(CardData card)
        {
            if (card == null)
            {
                return new CardVisualAssembly(false, CardRarity.Common, -1, null);
            }

            return new CardVisualAssembly(
                true,
                card.rarity,
                GetFrameVariantIndex(card.rarity),
                card.cardIcon);
        }

        public static int GetFrameVariantIndex(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return 0;
                case CardRarity.Rare: return 1;
                case CardRarity.Epic: return 2;
                case CardRarity.Legendary: return 3;
                default: return 0;
            }
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
            // scrim 활성은 Update 의 중앙 분기가 관리 — _isShowing/_waitingShowing 둘 중 하나라도 true 면 켬.
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
            // scrim 비활성은 Update 의 중앙 분기가 관리 — 둘 다 false 면 자동으로 끔.
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

                _cardNames[i] = CreateChildText(cardObject.transform, "CardName", new Vector2(0.16f, 0.345f), new Vector2(0.84f, 0.405f), Vector2.zero, Vector2.zero, _style.cardNameFontSize, FontStyles.Bold, TextAlignmentOptions.Center, CardTextColor);
                _cardDescriptions[i] = CreateChildText(cardObject.transform, "CardDesc", new Vector2(0.155f, 0.145f), new Vector2(0.845f, 0.295f), Vector2.zero, Vector2.zero, _style.cardDescriptionFontSize, FontStyles.Normal, TextAlignmentOptions.Midline, CardTextColor);
            }

            ConfigureCardSelectionHud();
            panel.SetActive(false);
        }

        private void OnCardSelected(int index)
        {
            if (_cardDeck == null) return;
            if (_cardDeck.Runner == null) return;
            if (_selectedThisRound) return;
            PlayerRef localPlayer = _cardDeck.Runner.LocalPlayer;
            if (!_cardDeck.HasPendingDraw(localPlayer)) return;
            if (_cardDeck.GetDrawnCard(localPlayer, index) == null) return;

            bool submitted = _cardDeck.Object != null &&
                             _cardDeck.Object.HasStateAuthority
                ? _cardDeck.TrySelectCardForPlayer(localPlayer, index)
                : SubmitCardSelectionRequest(index);
            if (!submitted)
            {
                HandleSelectionRejected();
                return;
            }

            _cardSelectFeedbacks?.PlayFeedbacks();

            _selectedThisRound = true;
            for (int i = 0; i < CardSlotCount; i++)
            {
                CardData card = _cardDeck.GetDrawnCard(localPlayer, i);
                SetCardVisual(i, card, selected: i == index, dimmed: i != index);
            }

            if (_timerText != null) _timerText.gameObject.SetActive(false);
            if (_waitingText != null)
            {
                _waitingText.text = "SELECTED  //  확정 대기 중...";
                _waitingText.gameObject.SetActive(true);
            }
        }

        private bool SubmitCardSelectionRequest(int index)
        {
            _cardDeck.Rpc_SelectCard(index);
            return true;
        }

        private void SetCardVisual(int index, CardData card, bool selected, bool dimmed)
        {
            CardVisualAssembly visual = ResolveCardVisualAssembly(card);
            bool hasCard = visual.HasCard;
            Color rarityColor = hasCard ? GetRarityColor(visual.Rarity) : _style.mutedColor;
            Color cardColor = hasCard ? Color.Lerp(_style.cardBaseColor, rarityColor, selected ? 0.28f : 0.10f) : _style.cardDimColor;
            cardColor.a = dimmed ? 0.52f : 0.96f;

            if (_cardButtons[index] != null)
            {
                _cardButtons[index].interactable = hasCard && !_selectedThisRound;
            }

            if (_cardRarityBorders[index] != null)
            {
                ApplyFrameVisual(index, visual, cardColor, rarityColor, dimmed);
            }

            if (_cardAccentBars[index] != null)
            {
                _cardAccentBars[index].color = rarityColor;
            }

            ApplyTextBoxVisuals(index, rarityColor, dimmed);

            if (_cardRarityLabels[index] != null)
            {
                _cardRarityLabels[index].text = hasCard ? GetRarityLabel(visual.Rarity) : "NO SIGNAL";
                _cardRarityLabels[index].color = CardTextColor;
            }

            if (_cardNames[index] != null)
            {
                _cardNames[index].text = hasCard ? card.cardName : "-";
                _cardNames[index].color = CardTextColor;
            }

            if (_cardDescriptions[index] != null)
            {
                _cardDescriptions[index].text = hasCard ? card.description : "선택 가능한 카드가 없습니다.";
                _cardDescriptions[index].color = CardTextColor;
            }

            if (_cardIconImages[index] != null)
            {
                _cardIconImages[index].sprite = visual.Illustration;
                _cardIconImages[index].preserveAspect = true;
                _cardIconImages[index].color = hasCard && visual.HasIllustration ? Color.white : new Color(rarityColor.r, rarityColor.g, rarityColor.b, dimmed ? 0.16f : 0.32f);
            }

            if (_cardIconPlaceholderTexts[index] != null)
            {
                bool shouldShowPlaceholder = !hasCard || !visual.HasIllustration;
                _cardIconPlaceholderTexts[index].gameObject.SetActive(shouldShowPlaceholder);
                _cardIconPlaceholderTexts[index].text = GetIconPlaceholderText(card);
                _cardIconPlaceholderTexts[index].color = dimmed ? _style.mutedColor : rarityColor;
            }

            if (_cardStatusLabels[index] != null)
            {
                _cardStatusLabels[index].text = selected ? "SELECTED" : hasCard ? string.Empty : "OFFLINE";
                _cardStatusLabels[index].color = selected ? _style.confirmColor : _style.mutedColor;
            }

            if (_cardSelectedOverlays[index] != null)
            {
                _cardSelectedOverlays[index].gameObject.SetActive(selected);
                _cardSelectedOverlays[index].color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.18f);
            }
        }

        private void ApplyTextBoxVisuals(int index, Color rarityColor, bool dimmed)
        {
            SetBoxVisual(
                _cardRarityBoxes[index],
                TransparentTextBoxColor,
                TransparentTextBoxColor,
                0f);
            SetBoxVisual(
                _cardNameBoxes[index],
                TransparentTextBoxColor,
                TransparentTextBoxColor,
                0f);
            SetBoxVisual(
                _cardDescriptionBoxes[index],
                TransparentTextBoxColor,
                TransparentTextBoxColor,
                0f);
        }

        private static void SetBoxVisual(Image image, Color fillColor, Color outlineColor, float outlineAlpha)
        {
            if (image == null)
            {
                return;
            }

            image.color = fillColor;
            Outline outline = image.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(outlineColor.r, outlineColor.g, outlineColor.b, outlineAlpha);
            }
        }

        private void ApplyFrameVisual(int index, CardVisualAssembly visual, Color cardColor, Color rarityColor, bool dimmed)
        {
            Image frameImage = _cardRarityBorders[index];
            if (frameImage == null)
            {
                return;
            }

            Sprite frameSprite = visual.HasCard && _frameSprites != null ? _frameSprites.GetFrameSprite(visual.Rarity) : null;
            frameImage.sprite = frameSprite;
            frameImage.type = Image.Type.Simple;
            frameImage.preserveAspect = frameSprite != null;
            frameImage.color = frameSprite != null ? new Color(1f, 1f, 1f, dimmed ? 0.62f : 1f) : cardColor;

            var outline = frameImage.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectDistance = new Vector2(_style.frameOutlineThickness, -_style.frameOutlineThickness);
                outline.effectColor = new Color(rarityColor.r, rarityColor.g, rarityColor.b, dimmed ? 0.45f : 0.9f);
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
