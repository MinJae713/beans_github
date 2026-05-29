#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using Colosseum.Card;
using Colosseum.Contracts;
using Colosseum.Utils;

namespace Colosseum.UI
{
    /// <summary>
    /// DevMode 디버그 카드 선택 패널.
    /// Show() 호출 전까지 절대 활성화되지 않음.
    /// CardDeck / CardEffect 소스를 직접 수정하지 않음.
    /// </summary>
    public class DebugCardSelectPanel : MonoBehaviour
    {
        // ── 내부 상태 ──
        private Canvas _canvas;
        private GameObject _rootPanel;
        private Transform _cardListContent;
        private TextMeshProUGUI _noCardsLabel;
        private TextMeshProUGUI _playerDropdownLabel;

        private CardDeck _deckRef;
        private List<CardData> _allCards = new List<CardData>();
        private PlayerRef[] _availablePlayers = new PlayerRef[0];
        private int _selectedPlayerIndex = 0;

        private readonly List<GameObject> _itemRows = new List<GameObject>();

        // A303-270 (review fix): 카드별 누적 적용 횟수를 PlayerRef 기준으로 분리 추적.
        // 같은 카드를 P1/P2 양쪽에 1번씩 적용했을 때 ×2로 합쳐 보이는 혼동 차단 — 각자 ×1로 표시되고,
        // 플레이어 토글 시 해당 플레이어 카운트로 배지 갱신.
        // Show() 호출 시 리셋 (패널 재오픈 = fresh debug 세션).
        private readonly Dictionary<PlayerRef, Dictionary<CardData, int>> _applyCountByPlayer = new Dictionary<PlayerRef, Dictionary<CardData, int>>();
        private readonly Dictionary<CardData, DebugCardListItem> _itemByCard = new Dictionary<CardData, DebugCardListItem>();

        // ── 색상 상수 ──
        private static readonly Color BgColor         = new Color(0.06f, 0.07f, 0.09f, 0.95f);
        private static readonly Color PanelColor      = new Color(0.10f, 0.11f, 0.14f, 0.98f);
        private static readonly Color ButtonColor     = new Color(0.18f, 0.22f, 0.30f, 1f);
        private static readonly Color ApplyBtnColor   = new Color(0.15f, 0.45f, 0.25f, 1f);
        private static readonly Color ResetBtnColor   = new Color(0.50f, 0.18f, 0.18f, 1f);
        private static readonly Color CloseBtnColor   = new Color(0.28f, 0.28f, 0.32f, 1f);
        private static readonly Color TextColor       = new Color(0.90f, 0.88f, 0.82f, 1f);
        private static readonly Color RowBgEven       = new Color(0.12f, 0.13f, 0.16f, 1f);
        private static readonly Color RowBgOdd        = new Color(0.10f, 0.11f, 0.13f, 1f);
        private const int DevModeCardAppliedVfxSequenceSaltBase = 91;
        private const int DevModeCardToastVfxSequenceSaltBase = 92;

        private void Awake()
        {
            // Show() 전까지 비활성
            gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────
        // 공개 API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 패널을 활성화합니다.
        /// </summary>
        /// <param name="deckRef">CardDeck 참조 (null이면 경고 후 카드 즉시 적용 기능만 동작)</param>
        /// <param name="allCards">표시할 전체 카드 목록</param>
        /// <param name="availablePlayers">선택 가능한 플레이어 목록</param>
        public void Show(CardDeck deckRef, List<CardData> allCards, PlayerRef[] availablePlayers)
        {
            _deckRef = deckRef;
            _allCards = allCards ?? new List<CardData>();
            _availablePlayers = availablePlayers ?? new PlayerRef[0];
            _selectedPlayerIndex = 0;

            // A303-270: 패널 재오픈 시 누적 카운트 리셋 (fresh debug 세션 의미)
            _applyCountByPlayer.Clear();
            _itemByCard.Clear();

            // A303-270: DevMode 디버그 도구는 CardDeck 없이 동작이 정상.
            // LogWarning(빨강 가까운 노랑) → Log 로 격하해 노이즈 줄임.
            if (_deckRef == null)
            {
                Debug.Log("[DebugCard] CardDeck reference is null — DevMode 직접 적용 모드로 동작합니다 (정상).");
            }
            else
            {
                Debug.Log($"[DebugCard] CardDeck reference set ({_deckRef.name}).");
            }

            EnsureUI();

            gameObject.SetActive(true);
            _rootPanel.SetActive(true);

            RefreshPlayerLabel();
            RefreshCardList();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// 패널을 비활성화합니다.
        /// </summary>
        public void Hide()
        {
            if (_rootPanel != null)
                _rootPanel.SetActive(false);

            gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────
        // UI 빌드
        // ─────────────────────────────────────────────

        private void EnsureUI()
        {
            if (_canvas != null) return;

            // Canvas
            var canvasGO = new GameObject("DebugCardSelectPanel_Canvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // 배경 블록
            var bg = CreateRect("Background", canvasGO.transform);
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = BgColor;

            // 중앙 패널
            _rootPanel = new GameObject("DebugCardSelectPanel_Root");
            _rootPanel.transform.SetParent(bg.transform, false);
            var panelRect = _rootPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot     = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(700f, 520f);
            var panelImg = _rootPanel.AddComponent<Image>();
            panelImg.color = PanelColor;

            // 타이틀
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(_rootPanel.transform, false);
            var titleRect = titleGO.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot     = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(0f, 44f);
            var titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.text = "DevMode Debug Card Panel";
            titleText.fontSize = 22;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = TextColor;

            // Player Selector 영역
            var selectorRow = new GameObject("PlayerSelectorRow");
            selectorRow.transform.SetParent(_rootPanel.transform, false);
            var selRect = selectorRow.AddComponent<RectTransform>();
            selRect.anchorMin = new Vector2(0f, 1f);
            selRect.anchorMax = new Vector2(1f, 1f);
            selRect.pivot     = new Vector2(0.5f, 1f);
            selRect.anchoredPosition = new Vector2(0f, -44f);
            selRect.sizeDelta = new Vector2(0f, 40f);

            // Player label
            var playerLabelGO = new GameObject("PlayerLabel");
            playerLabelGO.transform.SetParent(selectorRow.transform, false);
            var plRect = playerLabelGO.AddComponent<RectTransform>();
            plRect.anchorMin = new Vector2(0f, 0f);
            plRect.anchorMax = new Vector2(0.35f, 1f);
            plRect.offsetMin = new Vector2(12f, 0f);
            plRect.offsetMax = Vector2.zero;
            var plText = playerLabelGO.AddComponent<TextMeshProUGUI>();
            plText.text = "Target Player:";
            plText.fontSize = 16;
            plText.alignment = TextAlignmentOptions.MidlineLeft;
            plText.color = TextColor;

            // Player 1 버튼
            var btn1GO = CreateButtonGO("Btn_Player1", selectorRow.transform, "Player 1");
            var btn1Rect = btn1GO.GetComponent<RectTransform>();
            btn1Rect.anchorMin = new Vector2(0.36f, 0.1f);
            btn1Rect.anchorMax = new Vector2(0.62f, 0.9f);
            btn1Rect.offsetMin = new Vector2(4f, 0f);
            btn1Rect.offsetMax = new Vector2(-4f, 0f);
            btn1GO.GetComponent<Button>().onClick.AddListener(() => SelectPlayer(0));

            // Player 2 버튼
            var btn2GO = CreateButtonGO("Btn_Player2", selectorRow.transform, "Player 2");
            var btn2Rect = btn2GO.GetComponent<RectTransform>();
            btn2Rect.anchorMin = new Vector2(0.63f, 0.1f);
            btn2Rect.anchorMax = new Vector2(0.89f, 0.9f);
            btn2Rect.offsetMin = new Vector2(4f, 0f);
            btn2Rect.offsetMax = new Vector2(-4f, 0f);
            btn2GO.GetComponent<Button>().onClick.AddListener(() => SelectPlayer(1));

            // 현재 선택 플레이어 표시 라벨
            var dropLabelGO = new GameObject("PlayerDropdownLabel");
            dropLabelGO.transform.SetParent(selectorRow.transform, false);
            var dlRect = dropLabelGO.AddComponent<RectTransform>();
            dlRect.anchorMin = new Vector2(0.89f, 0f);
            dlRect.anchorMax = new Vector2(1f, 1f);
            dlRect.offsetMin = Vector2.zero;
            dlRect.offsetMax = new Vector2(-8f, 0f);
            _playerDropdownLabel = dropLabelGO.AddComponent<TextMeshProUGUI>();
            _playerDropdownLabel.fontSize = 14;
            _playerDropdownLabel.alignment = TextAlignmentOptions.Center;
            _playerDropdownLabel.color = new Color(0.6f, 0.9f, 0.6f);

            // ScrollView 영역
            var scrollAreaGO = new GameObject("ScrollArea");
            scrollAreaGO.transform.SetParent(_rootPanel.transform, false);
            var scrollAreaRect = scrollAreaGO.AddComponent<RectTransform>();
            scrollAreaRect.anchorMin = new Vector2(0f, 0f);
            scrollAreaRect.anchorMax = new Vector2(1f, 1f);
            scrollAreaRect.offsetMin = new Vector2(8f, 88f);
            scrollAreaRect.offsetMax = new Vector2(-8f, -88f);

            var scrollRect = scrollAreaGO.AddComponent<ScrollRect>();

            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollAreaGO.transform, false);
            var vpRect = viewportGO.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot     = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);
            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth  = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _cardListContent = contentGO.transform;

            scrollRect.content  = contentRect;
            scrollRect.viewport = vpRect;
            scrollRect.horizontal = false;
            scrollRect.vertical   = true;
            scrollRect.scrollSensitivity = 30f;

            // "No cards available" 라벨
            var noCardsGO = new GameObject("NoCardsLabel");
            noCardsGO.transform.SetParent(_cardListContent, false);
            var ncRect = noCardsGO.AddComponent<RectTransform>();
            ncRect.sizeDelta = new Vector2(0f, 48f);
            _noCardsLabel = noCardsGO.AddComponent<TextMeshProUGUI>();
            _noCardsLabel.text = "No cards available";
            _noCardsLabel.fontSize = 18;
            _noCardsLabel.alignment = TextAlignmentOptions.Center;
            _noCardsLabel.color = new Color(0.6f, 0.6f, 0.6f);
            noCardsGO.SetActive(false);

            // 하단 버튼 영역
            var bottomRow = new GameObject("BottomRow");
            bottomRow.transform.SetParent(_rootPanel.transform, false);
            var bottomRect = bottomRow.AddComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot     = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = Vector2.zero;
            bottomRect.sizeDelta = new Vector2(0f, 48f);

            // Reset All 버튼
            var resetBtnGO = CreateButtonGO("Btn_ResetAll", bottomRow.transform, "Reset All Enhancements", ResetBtnColor);
            var resetRect = resetBtnGO.GetComponent<RectTransform>();
            resetRect.anchorMin = new Vector2(0.02f, 0.1f);
            resetRect.anchorMax = new Vector2(0.48f, 0.9f);
            resetRect.offsetMin = Vector2.zero;
            resetRect.offsetMax = Vector2.zero;
            resetBtnGO.GetComponent<Button>().onClick.AddListener(OnResetAll);

            // Close 버튼
            var closeBtnGO = CreateButtonGO("Btn_Close", bottomRow.transform, "Close Panel", CloseBtnColor);
            var closeRect = closeBtnGO.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.52f, 0.1f);
            closeRect.anchorMax = new Vector2(0.98f, 0.9f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            closeBtnGO.GetComponent<Button>().onClick.AddListener(Hide);
        }

        // ─────────────────────────────────────────────
        // 카드 목록 갱신
        // ─────────────────────────────────────────────

        private void RefreshCardList()
        {
            // 기존 row 제거
            foreach (var row in _itemRows)
            {
                if (row != null) Destroy(row);
            }
            _itemRows.Clear();

            bool hasCards = _allCards != null && _allCards.Count > 0;
            _noCardsLabel.gameObject.SetActive(!hasCards);

            if (!hasCards) return;

            for (int i = 0; i < _allCards.Count; i++)
            {
                CardData card = _allCards[i];
                if (card == null) continue;

                var rowGO = BuildCardRow(card, i);
                rowGO.transform.SetParent(_cardListContent, false);
                _itemRows.Add(rowGO);
            }
        }

        private GameObject BuildCardRow(CardData card, int rowIndex)
        {
            var rowGO = new GameObject($"CardRow_{rowIndex}_{card.cardName}");
            var rowRect = rowGO.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 40f);
            var rowImg = rowGO.AddComponent<Image>();
            rowImg.color = rowIndex % 2 == 0 ? RowBgEven : RowBgOdd;

            // 아이콘
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(rowGO.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.1f);
            iconRect.anchorMax = new Vector2(0f, 0.9f);
            iconRect.pivot     = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(6f, 0f);
            iconRect.sizeDelta = new Vector2(30f, 0f);
            var iconImg = iconGO.AddComponent<Image>();
            if (card.cardIcon != null)
            {
                iconImg.sprite = card.cardIcon;
                iconImg.color  = Color.white;
            }
            else
            {
                iconImg.color = GetRarityColor(card.rarity) * 0.6f;
            }

            // 카드 이름
            var nameGO = new GameObject("CardName");
            nameGO.transform.SetParent(rowGO.transform, false);
            var nameRect = nameGO.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(0.45f, 1f);
            nameRect.offsetMin = new Vector2(44f, 0f);
            nameRect.offsetMax = Vector2.zero;
            var nameText = nameGO.AddComponent<TextMeshProUGUI>();
            nameText.text = card.cardName;
            nameText.fontSize = 15;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.color = TextColor;
            nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            // A303-270: 누적 적용 횟수 ×N 배지 (Apply 좌측, 카드 이름 우측)
            var countGO = new GameObject("CountBadge");
            countGO.transform.SetParent(rowGO.transform, false);
            var countRect = countGO.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.45f, 0f);
            countRect.anchorMax = new Vector2(0.55f, 1f);
            countRect.offsetMin = new Vector2(2f, 0f);
            countRect.offsetMax = new Vector2(-2f, 0f);
            var countText = countGO.AddComponent<TextMeshProUGUI>();
            countText.text = string.Empty;
            countText.fontSize = 16;
            countText.fontStyle = FontStyles.Bold;
            countText.alignment = TextAlignmentOptions.Center;
            countText.color = new Color(1f, 0.85f, 0.35f);   // 황금빛 — 누적 강조
            countText.enableWordWrapping = false;
            countGO.SetActive(false);

            // Apply 버튼 (즉시 효과 발동)
            var applyBtnGO = CreateButtonGO("Btn_Apply", rowGO.transform, "Apply", ApplyBtnColor, 14);
            var applyRect = applyBtnGO.GetComponent<RectTransform>();
            applyRect.anchorMin = new Vector2(0.56f, 0.1f);
            applyRect.anchorMax = new Vector2(0.78f, 0.9f);
            applyRect.offsetMin = new Vector2(2f, 0f);
            applyRect.offsetMax = new Vector2(-2f, 0f);
            var capturedCard = card;
            applyBtnGO.GetComponent<Button>().onClick.AddListener(() => OnApplyCard(capturedCard));

            // Add to Deck 버튼
            var addBtnGO = CreateButtonGO("Btn_AddToDeck", rowGO.transform, "+ Deck", ButtonColor, 13);
            var addRect = addBtnGO.GetComponent<RectTransform>();
            addRect.anchorMin = new Vector2(0.79f, 0.1f);
            addRect.anchorMax = new Vector2(1f, 0.9f);
            addRect.offsetMin = new Vector2(2f, 0f);
            addRect.offsetMax = new Vector2(-6f, 0f);
            addBtnGO.GetComponent<Button>().onClick.AddListener(() => OnAddToDeck(capturedCard));

            // DebugCardListItem 컴포넌트 부착
            var item = rowGO.AddComponent<DebugCardListItem>();
            item.cardIcon      = iconImg;
            item.cardName      = nameText;
            item.countBadge    = countText;   // A303-270
            item.applyButton   = applyBtnGO.GetComponent<Button>();
            item.addToDeckButton = addBtnGO.GetComponent<Button>();
            item.Setup(card, OnApplyCard, OnAddToDeck);

            // A303-270: 카드 → row 매핑 — OnApplyCard 에서 카운트 갱신할 때 해당 row 찾기 위함
            _itemByCard[card] = item;

            return rowGO;
        }

        // ─────────────────────────────────────────────
        // 플레이어 선택
        // ─────────────────────────────────────────────

        private void SelectPlayer(int index)
        {
            _selectedPlayerIndex = index;
            RefreshPlayerLabel();
            // A303-270 (review fix): 플레이어 토글 시 현재 선택된 플레이어 카운트로 배지 동기화
            RefreshAllBadgesForCurrentPlayer();
        }

        private void RefreshPlayerLabel()
        {
            if (_playerDropdownLabel == null) return;

            if (_availablePlayers == null || _availablePlayers.Length == 0)
            {
                _playerDropdownLabel.text = "—";
                return;
            }

            int clampedIndex = Mathf.Clamp(_selectedPlayerIndex, 0, _availablePlayers.Length - 1);
            _playerDropdownLabel.text = $"P{clampedIndex + 1}";
        }

        /// <summary>
        /// A303-270 (review fix): clamped index를 out 으로 반환해 로그/배지 측에서
        /// 실제 적용된 플레이어 번호와 일치하는 표기를 사용할 수 있도록 한다.
        /// (가용 플레이어가 1명일 때 P2 버튼 클릭 → 클램프돼 P1에 적용되지만 로그가 "P2"로 찍히는 회귀 차단)
        /// </summary>
        private PlayerRef GetSelectedPlayer(out int clampedIndex)
        {
            if (_availablePlayers == null || _availablePlayers.Length == 0)
            {
                clampedIndex = -1;
                return PlayerRef.None;
            }

            clampedIndex = Mathf.Clamp(_selectedPlayerIndex, 0, _availablePlayers.Length - 1);
            return _availablePlayers[clampedIndex];
        }

        /// <summary>
        /// A303-270 (review fix): 현재 선택된 플레이어 기준으로 모든 카드 row 의 ×N 배지를 갱신한다.
        /// 플레이어 토글 / Apply / ResetAll 시 호출.
        /// </summary>
        private void RefreshAllBadgesForCurrentPlayer()
        {
            PlayerRef current = GetSelectedPlayer(out _);
            Dictionary<CardData, int> counts = null;
            bool hasCounts = current != PlayerRef.None && _applyCountByPlayer.TryGetValue(current, out counts);

            foreach (var kv in _itemByCard)
            {
                if (kv.Value == null) continue;
                int count = 0;
                if (hasCounts && counts.TryGetValue(kv.Key, out int c)) count = c;
                kv.Value.SetCount(count);
            }
        }

        // ─────────────────────────────────────────────
        // 카드 즉시 스폰 (Apply)
        // ─────────────────────────────────────────────

        /// <summary>
        /// 카드 즉시 효과 발동 흐름:
        ///   1) 선택된 PlayerRef로 scene에서 CardEffect 탐색 (InputAuthority 기준)
        ///   2) CardEffect.ApplyCard(cardData) 호출
        ///   3) 실패 시 Debug.LogWarning
        ///
        /// A303-270:
        ///   - 카드별 누적 적용 횟수 추적 + ×N 배지 갱신
        ///   - 콘솔 로그 한 줄 포맷: "[DebugCard] Applied: {name} (×N) → DMG=...x, SPD=...x, Bounce=N, Pierce=N"
        /// </summary>
        private void OnApplyCard(CardData card)
        {
            if (card == null)
            {
                Debug.LogWarning("[DebugCard] CardData is null.");
                return;
            }

            // A303-270 (review fix): clampedIndex 를 받아 로그 표기를 실제 적용 대상과 일치시킨다.
            PlayerRef target = GetSelectedPlayer(out int appliedClampedIndex);

            if (target == PlayerRef.None)
            {
                Debug.LogWarning("[DebugCard] No player selected. Cannot apply card.");
                return;
            }

            // scene에서 CardEffect를 InputAuthority 기준으로 탐색
            var allCardEffects = SceneSearch.FindAllInScene<CardEffect>(gameObject.scene);
            bool applied = false;

            foreach (var effect in allCardEffects)
            {
                var netObj = effect.GetComponent<Fusion.NetworkObject>();
                if (netObj == null || netObj.InputAuthority != target) continue;

                // CardDeck.ApplyCard 동등 처리 — DevMode에서도 Gun/PlayerHealth 측 효과 발동.
                // (DevMode 경로가 CardEffect만 호출하면 extraBullets, fireRateMultiplier,
                //  ammo capacity, healAmount, lifestealPercent 등이 미적용된다.)
                var gun = netObj.GetComponentInChildren<Colosseum.Weapon.Gun>();
                int magazineCapacityBeforeCard = gun != null ? gun.MagazineCapacity : 0;
                if (gun != null)
                {
                    gun.FireRateMultiplier *= card.fireRateMultiplier;
                    gun.ExtraBullets += card.extraBullets;
                }
                var health = netObj.GetComponent<Colosseum.Player.PlayerHealth>();
                if (health != null)
                {
                    if (card.healAmount > 0)
                        health.CurrentHealth = Mathf.Min(health.CurrentHealth + card.healAmount, 100f);
                    health.LifestealPercent += card.lifestealPercent;
                }

                // A303-270 (review fix): 카드 카운트를 PlayerRef 기준으로 증가.
                // 동일 카드를 P1/P2에 1번씩 적용했을 때 두 번 합쳐 ×2로 보이는 합산 회귀 차단.
                if (!_applyCountByPlayer.TryGetValue(target, out var perPlayerCounts))
                {
                    perPlayerCounts = new Dictionary<CardData, int>();
                    _applyCountByPlayer[target] = perPlayerCounts;
                }
                perPlayerCounts.TryGetValue(card, out int prevCount);
                int newCount = prevCount + 1;
                perPlayerCounts[card] = newCount;

                effect.ApplyCard(card, newCount);
                if (gun != null)
                {
                    gun.ApplyMagazineCapacityChange(magazineCapacityBeforeCard);
                }

                if (_itemByCard.TryGetValue(card, out var rowItem) && rowItem != null)
                {
                    rowItem.SetCount(newCount);
                }

                Debug.Log(
                    $"[DebugCard] Applied: {card.cardName} (×{newCount}) → P{appliedClampedIndex + 1} " +
                    $"DMG={effect.DamageMultiplier:0.##}x, SPD={effect.BulletSpeedMultiplier:0.##}x, " +
                    $"Bounce={effect.BonusBounce}, Pierce={effect.BonusPierce}");

                PublishDevModeCardVfx(card, netObj, target, newCount);
                applied = true;
                break;
            }

            if (!applied)
            {
                Debug.LogWarning($"[DebugCard] CardEffect not found for player {target}. Card '{card.cardName}' was NOT applied.");
            }
        }

        private static void PublishDevModeCardVfx(CardData card, NetworkObject sourceObject, PlayerRef target, int sequenceSalt)
        {
            if (card == null || sourceObject == null || target == PlayerRef.None) return;

            string effectKey = string.IsNullOrWhiteSpace(card.cardKey) ? card.cardName : card.cardKey;
            int objectId = unchecked((int)sourceObject.Id.Raw);
            Vector2 position = sourceObject.transform.position;

            // DevMode 패널은 CardDeck/Runner 없이도 동작하므로 deterministic local payload를 직접 만든다.
            VfxEventBus.Publish(new VfxEventPayload(
                VfxEventType.CardEffectApplied,
                effectKey,
                effectKey,
                0,
                objectId,
                DevModeCardAppliedVfxSequenceSaltBase + sequenceSalt,
                VfxPredictionState.Confirmed,
                target.PlayerId,
                target.PlayerId,
                new VfxVector2(position.x, position.y),
                new VfxVector2(0f, 1f),
                1f,
                0.75f,
                1f,
                VfxAttachPoint.CardSlot));

            VfxEventBus.Publish(new VfxEventPayload(
                VfxEventType.CardEffectToastRequested,
                effectKey,
                effectKey,
                0,
                objectId,
                DevModeCardToastVfxSequenceSaltBase + sequenceSalt,
                VfxPredictionState.Confirmed,
                target.PlayerId,
                target.PlayerId,
                new VfxVector2(position.x, position.y),
                new VfxVector2(0f, 1f),
                1f,
                1.25f,
                1f,
                VfxAttachPoint.HudOverlay));
        }

        private void OnAddToDeck(CardData card)
        {
            if (card == null) return;
            // 덱 추가는 외부 통합 시 확장 가능 — 현재는 로그만 출력
            Debug.Log($"[DebugCard] AddToDeck requested: {card.cardName} (integration pending)");
        }

        // ─────────────────────────────────────────────
        // Reset All
        // ─────────────────────────────────────────────

        private void OnResetAll()
        {
            PlayerRef target = GetSelectedPlayer(out int targetClampedIndex);

            if (target == PlayerRef.None)
            {
                Debug.LogWarning("[DebugCard] No player selected. Cannot reset.");
                return;
            }

            var allCardEffects = SceneSearch.FindAllInScene<CardEffect>(gameObject.scene);
            bool found = false;

            foreach (var effect in allCardEffects)
            {
                var netObj = effect.GetComponent<Fusion.NetworkObject>();
                if (netObj == null || netObj.InputAuthority != target) continue;

                effect.ResetAll();

                // A303-270 (review fix): effect.ResetAll() 은 해당 플레이어 effect만 초기화하므로
                // 카운트도 그 플레이어 것만 비운다 (다른 플레이어 누적은 유지).
                if (_applyCountByPlayer.TryGetValue(target, out var perPlayerCounts))
                {
                    perPlayerCounts.Clear();
                }
                RefreshAllBadgesForCurrentPlayer();

                Debug.Log($"[DebugCard] ResetAll: P{targetClampedIndex + 1} 전체 효과/카운트 초기화");
                found = true;
                break;
            }

            if (!found)
            {
                Debug.LogWarning($"[DebugCard] CardEffect not found for player {target}. ResetAll skipped.");
            }
        }

        // ─────────────────────────────────────────────
        // UI 헬퍼
        // ─────────────────────────────────────────────

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        private static GameObject CreateButtonGO(string name, Transform parent, string label,
            Color? bgColor = null, int fontSize = 15)
        {
            var btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);
            btnGO.AddComponent<RectTransform>();
            var img = btnGO.AddComponent<Image>();
            img.color = bgColor ?? ButtonColor;
            var btn = btnGO.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor      = bgColor ?? ButtonColor;
            cb.highlightedColor = Color.Lerp(bgColor ?? ButtonColor, Color.white, 0.2f);
            cb.pressedColor     = Color.Lerp(bgColor ?? ButtonColor, Color.black, 0.2f);
            btn.colors = cb;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var labelText = labelGO.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = fontSize;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = TextColor;
            labelText.enableWordWrapping = false;

            return btnGO;
        }

        private static Color GetRarityColor(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common:    return new Color(0.7f, 0.7f, 0.7f);
                case CardRarity.Rare:      return new Color(0.2f, 0.5f, 1f);
                case CardRarity.Legendary: return new Color(1f, 0.84f, 0f);
                default:                   return Color.white;
            }
        }
    }
}
#endif
