using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Fusion;
using Colosseum.Card;
using Colosseum.Game;
using Colosseum.Network;
using Colosseum.Utils;

namespace Colosseum.UI
{
    /// <summary>
    /// 화면 하단 좌/우 — P1/P2 보유 카드 인벤토리 HUD.
    /// CardDeck.OnCardApplied 구독으로 자체 카드 리스트 추적.
    /// 닉네임은 NetworkManager.OnNicknamesUpdated + LastRoomNicknames.
    /// 정렬: 등급 내림차순(Legendary>Epic>Rare>Common) + 같은 등급은 첫 획득 순서 오름차순.
    /// 레이아웃: 1~8 unique 한 줄, 9~16 unique 두 줄(아래 8 고정 + 위 N-8).
    /// 같은 카드 재획득 시 슬롯 추가 X — count 누적 후 우하단 배지(>=2일 때만)로 숫자 표시.
    /// 미러링: P1 좌하 + 좌→우, P2 우하 + 우→좌.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CardInventoryHUD : MonoBehaviour
    {
        // 등급 색 (CardSelectionUI와 통일)
        private static readonly Color CommonColor = new Color(0.64f, 0.57f, 0.48f, 1f);
        private static readonly Color RareColor = new Color(0.71f, 0.77f, 1f, 1f);
        private static readonly Color EpicColor = new Color(0.72f, 0.35f, 1f, 1f);
        private static readonly Color LegendaryColor = new Color(0.91f, 0.77f, 0f, 1f);
        private static readonly Color LabelColor = Color.white;
        private static readonly Color SlotBgColor = new Color(0.165f, 0.165f, 0.165f, 1f);
        private static readonly Color BadgeBgColor = Color.white;
        private static readonly Color BadgeTextColor = Color.black;
        private static readonly Color BadgeOutlineColor = Color.black;

        private const int MAX_DISPLAY = 16;
        private const int BADGE_MIN_COUNT = 2;
        private const int SlotsPerRow = 8;
        private const float SlotSize = 60f;
        private const float SlotSpacing = 5f;
        // 행 간격은 배지가 위쪽 줄 슬롯과 겹치지 않도록 SlotSpacing보다 더 크게.
        private const float RowSpacing = 12f;
        private const float LabelHeight = 24f;
        private const float SlotOutlineThickness = 3f;
        private const float MarginX = 40f;
        private const float MarginY = 60f;
        private const float BadgeSizeRatio = 0.35f;
        private const float BadgeOutlineThickness = 1.5f;
        private const float BadgeFontSize = 18f;
        private const float LabelFontSize = 18f;
        private const float HoverDelay = 0.2f;
        private const float PopupBottomGap = 16f;

        private int _playerSlot; // 0=P1, 1=P2
        private bool IsMirrored => _playerSlot == 1;

        private RoomManager _roomManager;
        private CardDeck _cardDeck;
        private PlayerRef _myPlayerRef;
        private bool _subscribedToCardDeck;
        private int _lastSyncedAppliedCardCount = -1;

        private RectTransform _rect;
        private TextMeshProUGUI _label;
        private Sprite _whiteSprite;
        private readonly List<SlotEntry> _entries = new List<SlotEntry>();
        private int _acquireCounter;

        private CardHoverPopup _popup;
        private Coroutine _hoverDelayCoroutine;

        private class SlotEntry
        {
            public CardData Card;
            public int Count;
            public int FirstAcquiredOrder;
            public GameObject SlotGO;
            public GameObject BadgeRoot;
            public TextMeshProUGUI BadgeText;
        }

        /// <summary>
        /// 슬롯 GameObject에 부착되어 PointerEnter/Exit 이벤트를 부모 인벤토리에 전달.
        /// EventSystem + GraphicRaycaster 조합으로만 동작.
        /// </summary>
        private class CardSlotHoverProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public System.Action OnEnter;
            public System.Action OnExit;
            public void OnPointerEnter(PointerEventData _) { OnEnter?.Invoke(); }
            public void OnPointerExit(PointerEventData _) { OnExit?.Invoke(); }
        }

        public void Init(int playerSlot, RoomManager roomManager)
        {
            _playerSlot = playerSlot;
            _roomManager = roomManager;

            _whiteSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                Vector2.one * 0.5f);

            BuildContainerAndLabel();

            SubscribeNicknames();
            CardDeck.SpawnedInScene += HandleCardDeckSpawned;
            if (_roomManager != null)
            {
                _roomManager.OnPlayerSlotsChanged += HandlePlayerSlotsChanged;
            }

            TryBindCardDeckFromScene();
            TryAssignPlayerRef();
            TryCompleteBinding();
        }

        private void Update()
        {
            if (_cardDeck == null ||
                _cardDeck.Object == null ||
                !_cardDeck.Object.IsValid ||
                _myPlayerRef == default ||
                !_subscribedToCardDeck)
            {
                TryBindCardDeckFromScene();
                TryAssignPlayerRef();
                TryCompleteBinding();
            }

            TrySyncAppliedCardsFromDeck();
        }

        private void SubscribeNicknames()
        {
            NetworkManager.OnNicknamesUpdated += OnNicknamesUpdated;
            if (NetworkManager.Instance != null)
            {
                ApplyNicknames(NetworkManager.Instance.LastRoomNicknames);
            }
        }

        private void OnDestroy()
        {
            CardDeck.SpawnedInScene -= HandleCardDeckSpawned;
            if (_roomManager != null)
            {
                _roomManager.OnPlayerSlotsChanged -= HandlePlayerSlotsChanged;
            }
            if (_cardDeck != null) _cardDeck.OnCardApplied -= OnCardApplied;
            _subscribedToCardDeck = false;
            NetworkManager.OnNicknamesUpdated -= OnNicknamesUpdated;
        }

        private void HandleCardDeckSpawned(CardDeck deck)
        {
            // HUD GameObject가 InGameHUDManager에 의해 active scene(보통 MainMenu)에 동적 생성되는 반면,
            // CardDeck은 GameRunner 산하 별도 씬에 살아 SceneSearch 기반 동일 씬 비교가 영영 실패한다(A303-363).
            // 단일 client process엔 CardDeck이 1개만 존재하므로 FindObjectOfType이 안전.
            if (deck == null)
            {
                return;
            }

            BindCardDeck(deck);
        }

        private void TryBindCardDeckFromScene()
        {
            if (_cardDeck != null && _cardDeck.Object != null && _cardDeck.Object.IsValid)
            {
                return;
            }

            BindCardDeck(FindObjectOfType<CardDeck>());
        }

        private void BindCardDeck(CardDeck deck)
        {
            if (deck == null || deck == _cardDeck)
            {
                return;
            }

            if (_cardDeck != null)
            {
                _cardDeck.OnCardApplied -= OnCardApplied;
            }

            _cardDeck = deck;
            _subscribedToCardDeck = false;
            _lastSyncedAppliedCardCount = -1;
            TryCompleteBinding();
        }

        private void HandlePlayerSlotsChanged(PlayerRef player1, PlayerRef player2)
        {
            PlayerRef nextPlayerRef = _playerSlot == 0 ? player1 : player2;
            if (nextPlayerRef == _myPlayerRef)
            {
                return;
            }

            if (_cardDeck != null && _subscribedToCardDeck)
            {
                _cardDeck.OnCardApplied -= OnCardApplied;
                _subscribedToCardDeck = false;
            }

            _myPlayerRef = nextPlayerRef;
            ClearEntries();
            _lastSyncedAppliedCardCount = -1;
            TryCompleteBinding();
        }

        private void TryAssignPlayerRef()
        {
            if (_roomManager == null || _roomManager.Object == null || !_roomManager.Object.IsValid)
            {
                return;
            }

            PlayerRef nextPlayerRef = _playerSlot == 0 ? _roomManager.Player1 : _roomManager.Player2;
            if (nextPlayerRef == _myPlayerRef)
            {
                return;
            }

            if (_cardDeck != null && _subscribedToCardDeck)
            {
                _cardDeck.OnCardApplied -= OnCardApplied;
                _subscribedToCardDeck = false;
            }

            _myPlayerRef = nextPlayerRef;
            ClearEntries();
            _lastSyncedAppliedCardCount = -1;
        }

        private void TryCompleteBinding()
        {
            if (_cardDeck == null || _cardDeck.Object == null || !_cardDeck.Object.IsValid)
            {
                return;
            }

            if (_myPlayerRef == default)
            {
                return;
            }

            if (_subscribedToCardDeck)
            {
                return;
            }

            _cardDeck.OnCardApplied += OnCardApplied;
            _subscribedToCardDeck = true;
            Debug.Log($"[CardInventoryHUD] Bound slot={_playerSlot} player={_myPlayerRef}");
        }

        private void BuildContainerAndLabel()
        {
            _rect = GetComponent<RectTransform>();

            // 좌하/우하 anchor + pivot
            float ax = IsMirrored ? 1f : 0f;
            _rect.anchorMin = new Vector2(ax, 0f);
            _rect.anchorMax = new Vector2(ax, 0f);
            _rect.pivot = new Vector2(ax, 0f);
            float xPos = IsMirrored ? -MarginX : MarginX;
            _rect.anchoredPosition = new Vector2(xPos, MarginY);

            // 컨테이너 크기는 두 줄 + 라벨 높이
            float width = SlotsPerRow * SlotSize + (SlotsPerRow - 1) * SlotSpacing;
            float height = 2f * SlotSize + RowSpacing + LabelHeight;
            _rect.sizeDelta = new Vector2(width, height);

            // 라벨: 두 줄 슬롯 위에 배치
            // A303-327: 닉네임은 캐릭터 머리 위 체력바 위에 표시(PlayerHPBar)로 이동.
            // 하단 P1/P2 라벨 GameObject는 비활성 — 닉네임 갱신 경로(ApplyNicknames)는 그대로 두되 표시되지 않음.
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(transform, false);
            _label = labelObj.AddComponent<TextMeshProUGUI>();
            _label.text = _playerSlot == 0 ? "P1" : "P2";
            _label.fontSize = LabelFontSize;
            _label.fontStyle = FontStyles.Bold;
            _label.color = LabelColor;
            _label.alignment = IsMirrored ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(IsMirrored ? 1f : 0f, 1f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(0f, LabelHeight);
            labelObj.SetActive(false);

            // Hover 팝업 빌드 (1회). 인벤토리 자식이라 위치 추적 자동.
            // popupBottomY = 인벤토리 height + gap. 인벤토리 anchor가 (ax,0) pivot (ax,0)이라
            // local y = height = 팝업이 인벤토리 바로 위.
            float invHeight = _rect.sizeDelta.y;
            GameObject popupObj = new GameObject("CardHoverPopup");
            popupObj.transform.SetParent(transform, false);
            _popup = popupObj.AddComponent<CardHoverPopup>();
            _popup.Build(_playerSlot, _whiteSprite, invHeight + PopupBottomGap);
        }

        private void OnCardApplied(PlayerRef player, CardData card)
        {
            if (player != _myPlayerRef) return;
            if (card == null) return;

            AddOrIncrementEntry(card, rebuild: true);
            _lastSyncedAppliedCardCount = -1;
            Debug.Log($"[CardInventoryHUD] Event applied slot={_playerSlot} player={_myPlayerRef} card={card.cardName}");
        }

        private bool TrySyncAppliedCardsFromDeck(bool force = false)
        {
            if (_cardDeck == null ||
                _cardDeck.Object == null ||
                !_cardDeck.Object.IsValid ||
                _myPlayerRef == default)
            {
                return false;
            }

            int count = _cardDeck.GetAppliedCardCount(_myPlayerRef);
            if (!force && count == _lastSyncedAppliedCardCount)
            {
                return true;
            }

            ClearEntries();
            for (int i = 0; i < count; i++)
            {
                CardData card = _cardDeck.GetAppliedCard(_myPlayerRef, i);
                if (card == null)
                {
                    Debug.LogWarning($"[CardInventoryHUD] Missing applied card data slot={_playerSlot} player={_myPlayerRef} index={i}");
                    continue;
                }

                AddOrIncrementEntry(card, rebuild: false);
            }

            _lastSyncedAppliedCardCount = count;
            RebuildLayout();
            Debug.Log($"[CardInventoryHUD] Synced slot={_playerSlot} player={_myPlayerRef} count={count}");
            return true;
        }

        private void AddOrIncrementEntry(CardData card, bool rebuild)
        {
            // 같은 cardId 이미 있음 → count++ + 배지 갱신만
            SlotEntry existing = FindEntryByCardId(card.cardId);
            if (existing != null)
            {
                existing.Count++;
                UpdateBadge(existing);
                return;
            }

            // 새 카드 — HUD cap 초과 시 무시 (실제 게임 룰상 발생 X, 안전선)
            if (_entries.Count >= MAX_DISPLAY)
            {
                Debug.LogWarning($"[CardInventoryHUD] Slot {_playerSlot} reached MAX_DISPLAY={MAX_DISPLAY}, ignoring new card {card.cardName}.");
                return;
            }

            SlotEntry entry = new SlotEntry
            {
                Card = card,
                Count = 1,
                FirstAcquiredOrder = _acquireCounter++,
            };
            _entries.Add(entry);
            if (rebuild)
            {
                RebuildLayout();
            }
        }

        private void ClearEntries()
        {
            if (_hoverDelayCoroutine != null)
            {
                StopCoroutine(_hoverDelayCoroutine);
                _hoverDelayCoroutine = null;
            }
            if (_popup != null) _popup.Hide();

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].SlotGO != null)
                {
                    Destroy(_entries[i].SlotGO);
                    _entries[i].SlotGO = null;
                    _entries[i].BadgeRoot = null;
                    _entries[i].BadgeText = null;
                }
            }

            _entries.Clear();
            _acquireCounter = 0;
        }

        private SlotEntry FindEntryByCardId(int cardId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Card.cardId == cardId) return _entries[i];
            }
            return null;
        }

        private void OnSlotPointerEnter(SlotEntry slot)
        {
            if (_popup == null) return;
            if (_hoverDelayCoroutine != null)
            {
                StopCoroutine(_hoverDelayCoroutine);
                _hoverDelayCoroutine = null;
            }

            if (_popup.gameObject.activeSelf)
            {
                // 이미 떠 있는 상태면 즉시 갱신 (재딜레이 X)
                _popup.Show(slot.Card);
            }
            else
            {
                _hoverDelayCoroutine = StartCoroutine(HoverDelayThenShow(slot));
            }
        }

        private IEnumerator HoverDelayThenShow(SlotEntry slot)
        {
            yield return new WaitForSecondsRealtime(HoverDelay);
            if (_popup != null) _popup.Show(slot.Card);
            _hoverDelayCoroutine = null;
        }

        private void OnSlotPointerExit(SlotEntry slot)
        {
            if (_hoverDelayCoroutine != null)
            {
                StopCoroutine(_hoverDelayCoroutine);
                _hoverDelayCoroutine = null;
            }
            if (_popup != null) _popup.Hide();
        }

        private void RebuildLayout()
        {
            // 슬롯 GO 일괄 재생성 시 hover 상태도 초기화 — 옛 슬롯의 proxy 콜백은 GO와 함께 사라짐.
            if (_hoverDelayCoroutine != null)
            {
                StopCoroutine(_hoverDelayCoroutine);
                _hoverDelayCoroutine = null;
            }
            if (_popup != null) _popup.Hide();

            // 기존 슬롯 GO 일괄 제거 (count만 변경되는 경로는 UpdateBadge로 분리되어 여기 안 옴)
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].SlotGO != null)
                {
                    Destroy(_entries[i].SlotGO);
                    _entries[i].SlotGO = null;
                    _entries[i].BadgeRoot = null;
                    _entries[i].BadgeText = null;
                }
            }

            // 정렬: 등급 내림차순 + 같은 등급은 FirstAcquiredOrder 오름차순
            var sorted = _entries
                .OrderByDescending(e => GetRarityRank(e.Card.rarity))
                .ThenBy(e => e.FirstAcquiredOrder)
                .ToList();

            int total = sorted.Count;
            int bottomCount = Mathf.Min(total, SlotsPerRow);
            int topCount = Mathf.Max(0, total - SlotsPerRow);

            // 위 줄: sorted[0..topCount-1] (등급 가장 높은 N-8개, stragglers 위로)
            for (int i = 0; i < topCount; i++)
            {
                int xIdx = IsMirrored ? (topCount - 1 - i) : i;
                CreateSlot(sorted[i], xIdx, row: 1);
            }
            // 아래 줄: sorted[topCount..]
            for (int i = 0; i < bottomCount; i++)
            {
                int xIdx = IsMirrored ? (bottomCount - 1 - i) : i;
                CreateSlot(sorted[topCount + i], xIdx, row: 0);
            }
        }

        private void CreateSlot(SlotEntry entry, int xIdx, int row)
        {
            CardData card = entry.Card;

            GameObject slot = new GameObject($"Slot_{row}_{xIdx}_{card.cardId}");
            slot.transform.SetParent(transform, false);

            Image bg = slot.AddComponent<Image>();
            bg.sprite = _whiteSprite;
            bg.color = SlotBgColor;

            Outline outline = slot.AddComponent<Outline>();
            outline.effectColor = GetRarityColor(card.rarity);
            outline.effectDistance = new Vector2(SlotOutlineThickness, -SlotOutlineThickness);

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            float ax = IsMirrored ? 1f : 0f;
            slotRect.anchorMin = new Vector2(ax, 0f);
            slotRect.anchorMax = new Vector2(ax, 0f);
            slotRect.pivot = new Vector2(ax, 0f);

            float xOffset = xIdx * (SlotSize + SlotSpacing);
            if (IsMirrored) xOffset = -xOffset;
            float yOffset = row * (SlotSize + RowSpacing);
            slotRect.anchoredPosition = new Vector2(xOffset, yOffset);
            slotRect.sizeDelta = new Vector2(SlotSize, SlotSize);

            if (card.cardIcon != null)
            {
                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(slot.transform, false);
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = card.cardIcon;
                iconImg.preserveAspect = true;
                RectTransform iconRect = iconObj.GetComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(4f, 4f);
                iconRect.offsetMax = new Vector2(-4f, -4f);
            }

            // 배지 (count >= BADGE_MIN_COUNT 일 때만 활성).
            // 슬롯 우상단에 약간 밖으로 걸쳐 배치 — 우하단은 옆 슬롯 외곽선과 겹쳐 가려져서 상단으로 이동.
            GameObject badgeRoot = new GameObject("Badge");
            badgeRoot.transform.SetParent(slot.transform, false);
            RectTransform badgeRect = badgeRoot.AddComponent<RectTransform>();
            float badgeSize = SlotSize * BadgeSizeRatio;
            badgeRect.anchorMin = new Vector2(1f, 0f);
            badgeRect.anchorMax = new Vector2(1f, 0f);
            badgeRect.pivot = new Vector2(1f, 0f);
            badgeRect.anchoredPosition = new Vector2(-3f, 3f); // 슬롯 우하단 안쪽 3px 인셋 (등급 외곽선 INSIDE)
            badgeRect.sizeDelta = new Vector2(badgeSize, badgeSize);

            // 배경 (흰 사각형 — 시각상 원형 안 만들어도 OK)
            Image badgeBg = badgeRoot.AddComponent<Image>();
            badgeBg.sprite = _whiteSprite;
            badgeBg.color = BadgeBgColor;

            Outline badgeOutline = badgeRoot.AddComponent<Outline>();
            badgeOutline.effectColor = BadgeOutlineColor;
            badgeOutline.effectDistance = new Vector2(BadgeOutlineThickness, -BadgeOutlineThickness);

            // 숫자 텍스트
            GameObject badgeTextObj = new GameObject("Count");
            badgeTextObj.transform.SetParent(badgeRoot.transform, false);
            TextMeshProUGUI badgeText = badgeTextObj.AddComponent<TextMeshProUGUI>();
            badgeText.text = entry.Count.ToString();
            badgeText.color = BadgeTextColor;
            badgeText.fontSize = BadgeFontSize;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.alignment = TextAlignmentOptions.Center;
            RectTransform badgeTextRect = badgeTextObj.GetComponent<RectTransform>();
            badgeTextRect.anchorMin = Vector2.zero;
            badgeTextRect.anchorMax = Vector2.one;
            badgeTextRect.offsetMin = Vector2.zero;
            badgeTextRect.offsetMax = Vector2.zero;

            badgeRoot.SetActive(entry.Count >= BADGE_MIN_COUNT);

            badgeRoot.transform.SetAsLastSibling();

            entry.SlotGO = slot;
            entry.BadgeRoot = badgeRoot;
            entry.BadgeText = badgeText;

            // Hover proxy — Image의 raycastTarget=true(기본값)라 슬롯 전체에서 잡힘.
            CardSlotHoverProxy proxy = slot.AddComponent<CardSlotHoverProxy>();
            SlotEntry capture = entry;
            proxy.OnEnter = () => OnSlotPointerEnter(capture);
            proxy.OnExit = () => OnSlotPointerExit(capture);
        }

        private void UpdateBadge(SlotEntry entry)
        {
            if (entry.BadgeText != null) entry.BadgeText.text = entry.Count.ToString();
            if (entry.BadgeRoot != null) entry.BadgeRoot.SetActive(entry.Count >= BADGE_MIN_COUNT);
        }

        private Color GetRarityColor(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return CommonColor;
                case CardRarity.Rare: return RareColor;
                case CardRarity.Epic: return EpicColor;
                case CardRarity.Legendary: return LegendaryColor;
                default: return CommonColor;
            }
        }

        // CardRarity enum 값(Common=0, Rare=1, Legendary=2, Epic=3)이 직렬화 호환을 위해 자연순서가
        // 아니므로, 정렬용 rank를 명시적으로 매핑해 Legendary > Epic > Rare > Common 순서를 보장.
        private int GetRarityRank(CardRarity r)
        {
            switch (r)
            {
                case CardRarity.Legendary: return 4;
                case CardRarity.Epic: return 3;
                case CardRarity.Rare: return 2;
                case CardRarity.Common: return 1;
                default: return 0;
            }
        }

        private void OnNicknamesUpdated(string[] nicknames)
        {
            ApplyNicknames(nicknames);
        }

        private void ApplyNicknames(string[] nicknames)
        {
            if (_label == null) return;
            if (nicknames == null || nicknames.Length == 0) return;

            string name = null;
            if (_playerSlot == 0 && nicknames.Length >= 1) name = nicknames[0];
            else if (_playerSlot == 1 && nicknames.Length >= 2) name = nicknames[1];

            if (!string.IsNullOrEmpty(name))
            {
                _label.text = name;
            }
        }
    }
}
