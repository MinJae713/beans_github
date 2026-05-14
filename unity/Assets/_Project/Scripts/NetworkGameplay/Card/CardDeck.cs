using UnityEngine;
using Fusion;
using System.Collections.Generic;
using Colosseum.Game;
using Colosseum.Utils;
using Colosseum.Contracts;
using Colosseum.NetworkGameplay.VFX;

namespace Colosseum.Card
{
    public class CardDeck : NetworkBehaviour
    {
        private const int MaxPendingPlayers = 2;
        private const int FixedDrawChoices = 3;
        private const int InvalidCardId = -1;
        private const string UnknownCardKeyPrefix = "unknown-card";
        private const int EpicOwnershipCap = 2;
        private const int LegendaryOwnershipCap = 1;
        private const int CardToastVfxSequenceSaltOffset = 1;
        private static readonly CardRarity[] OfferRarities = { CardRarity.Common, CardRarity.Rare, CardRarity.Epic, CardRarity.Legendary };

        [Header("Card Pool")]
        [SerializeField] private List<CardData> _allCards = new();

        [Header("Draw Settings")]
        [SerializeField] private int _drawChoices = FixedDrawChoices;

        [Header("Draft Offer Policy")]
        [SerializeField, Min(0f)] private float _commonOfferWeight = 1.0f;
        [SerializeField, Min(0f)] private float _rareOfferWeight = 0.5f;
        [SerializeField, Min(0f)] private float _epicOfferWeight = 0.2f;
        [SerializeField, Min(0f)] private float _legendaryOfferWeight = 0.1f;

        [Header("Gameplay Profile")]
        [Tooltip("Assets/_Project/Data/GameplayProfile.asset 드래그. 미할당 시 Timeout fallback 값 사용.")]
        [SerializeField] private GameplayProfile _gameplayProfile;

        [Header("Timeout (GameplayProfile 미연결 시 Fallback)")]
        [SerializeField] private float _drawTimeoutSeconds = 10f;

        [Networked, Capacity(MaxPendingPlayers)] private NetworkArray<PlayerRef> _pendingPlayers => default;
        [Networked, Capacity(6)] private NetworkArray<int> _drawnCardIndices => default;
        [Networked] private int _offerSeedCounter { get; set; }
        [Networked] private TickTimer _drawTimeout0 { get; set; }
        [Networked] private TickTimer _drawTimeout1 { get; set; }

        private readonly Dictionary<PlayerRef, Dictionary<string, int>> _playerCardStacks = new();
        private readonly HashSet<int> _invalidCardMetadataLoggedIndices = new();
        private bool _catalogValidationChecked;
        private bool _catalogValidationPassed = true;

        public static event System.Action<CardDeck> SpawnedInScene;
        public event System.Action OnSelectionRejected;
        // Presentation 측 인벤토리 HUD 등이 카드 적용을 관찰하기 위한 이벤트.
        // ApplyCard는 서버 전용이라 직접 발행하면 클라가 못 받음 → Rpc_NotifyCardApplied로 fan-out.
        public event System.Action<Fusion.PlayerRef, CardData> OnCardApplied;

        public bool IsDrawing => GetFirstOccupiedSlot() >= 0;

        public PlayerRef DrawingPlayer
        {
            get
            {
                int slot = GetFirstOccupiedSlot();
                return slot >= 0 ? _pendingPlayers[slot] : default;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            for (int slot = 0; slot < MaxPendingPlayers; slot++)
            {
                PlayerRef pendingPlayer = _pendingPlayers[slot];
                if (pendingPlayer == default) continue;

                if (GetDrawTimeout(slot).Expired(Runner))
                {
                    TryAutoSelectRandomCard(slot, pendingPlayer);
                }
            }
        }

        public void StartDraw(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;
            EnsureDrawChoicePolicyInvariant();
            if (!EnsureCardCatalogValidated()) return;

            List<int> draftPool = BuildDraftPoolIndices(player);
            if (draftPool.Count == 0)
            {
                Debug.LogWarning($"[Colosseum] No draft-enabled cards available for {player}. (stack cap / metadata)");
                return;
            }

            if (player == default)
            {
                Debug.LogWarning("[Colosseum] Cannot start draw for PlayerRef.None");
                return;
            }

            int existingSlot = GetPendingSlot(player);
            if (existingSlot >= 0)
            {
                Debug.Log($"[Colosseum] Draw already pending for {player}, skipping duplicate request.");
                return;
            }

            int slot = GetFirstEmptySlot();
            if (slot < 0)
            {
                Debug.LogWarning($"[Colosseum] No free pending card slot for {player}");
                return;
            }

            _pendingPlayers.Set(slot, player);
            float timeout = _gameplayProfile != null
                ? _gameplayProfile.cardOffer.visibleChoiceDuration
                : _drawTimeoutSeconds;
            SetDrawTimeout(slot, TickTimer.CreateFromSeconds(Runner, timeout));

            int drawSeed = GetNextOfferSeed(player, slot);
            FillDrawChoices(slot, draftPool, drawSeed);

            Debug.Log($"[Colosseum] Draw started for {player}: " +
                $"{GetDrawnCard(player, 0)?.cardName}, {GetDrawnCard(player, 1)?.cardName}, {GetDrawnCard(player, 2)?.cardName} (seed={drawSeed})");
        }

        public void SelectCard(int choiceIndex)
        {
            if (!Object.HasStateAuthority) return;

            int slot = GetFirstOccupiedSlot();
            if (slot < 0) return;

            SelectCard(slot, choiceIndex);
        }

        public void CancelDraw()
        {
            if (!Object.HasStateAuthority) return;

            for (int slot = 0; slot < MaxPendingPlayers; slot++)
            {
                if (_pendingPlayers[slot] == default) continue;

                Debug.Log($"[Colosseum] Card draw cancelled for {_pendingPlayers[slot]}");
                ClearPendingSlot(slot);
            }
        }

        public void ResetPlayerStacks()
        {
            if (!Object.HasStateAuthority) return;

            _playerCardStacks.Clear();
            Debug.Log("[Colosseum] Card stacks reset for rematch.");
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_SelectCard(int choiceIndex, RpcInfo info = default)
        {
            int slot = GetPendingSlot(info.Source);
            if (slot < 0)
            {
                Debug.LogWarning($"[Colosseum] Card select rejected: {info.Source} has no pending draw");
                return;
            }

            SelectCard(slot, choiceIndex);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_NotifySelectionRejected(PlayerRef player)
        {
            if (Runner == null || Runner.LocalPlayer != player) return;
            OnSelectionRejected?.Invoke();
        }

        public CardData GetDrawnCard(int choiceIndex)
        {
            int slot = GetFirstOccupiedSlot();
            if (slot < 0) return null;
            return GetDrawnCard(_pendingPlayers[slot], choiceIndex);
        }

        public CardData GetDrawnCard(PlayerRef player, int choiceIndex)
        {
            if (choiceIndex < 0 || choiceIndex >= _drawChoices) return null;

            int slot = GetPendingSlot(player);
            if (slot < 0) return null;

            int cardIndex = _drawnCardIndices[GetChoiceOffset(slot, choiceIndex)];
            if (cardIndex < 0 || cardIndex >= _allCards.Count) return null;
            return _allCards[cardIndex];
        }

        public bool HasPendingDraw(PlayerRef player)
        {
            if (player == default) return false;
            return GetPendingSlot(player) >= 0;
        }

        public float GetRemainingSeconds(PlayerRef player)
        {
            if (Runner == null) return 0f;
            int slot = GetPendingSlot(player);
            if (slot < 0) return 0f;
            TickTimer t = GetDrawTimeout(slot);
            return t.IsRunning ? t.RemainingTime(Runner) ?? 0f : 0f;
        }

        private void SelectCard(int slot, int choiceIndex)
        {
            if (!Object.HasStateAuthority) return;

            PlayerRef drawingPlayer = _pendingPlayers[slot];
            if (drawingPlayer == default) return;

            CardData selected = GetDrawnCard(drawingPlayer, choiceIndex);
            if (selected == null) return;
            if (!CanPlayerTakeCard(drawingPlayer, selected))
            {
                Debug.LogWarning($"[Colosseum] Selection rejected for {drawingPlayer}: stack/rarity cap reached for {selected.cardKey}");
                Rpc_NotifySelectionRejected(drawingPlayer);
                return;
            }

            ApplyCard(drawingPlayer, selected);
            IncrementPlayerCardStack(drawingPlayer, selected);
            Vector2 vfxPosition = TryGetPlayerPosition(drawingPlayer, out Vector2 playerPosition)
                ? playerPosition
                : (Vector2)transform.position;
            Rpc_PublishCardEffectVfx(
                selected.cardKey,
                drawingPlayer,
                vfxPosition.x,
                vfxPosition.y,
                _offerSeedCounter);
            Debug.Log($"[Colosseum] {drawingPlayer} selected: {selected.cardName}");
            ClearPendingSlot(slot);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishCardEffectVfx(string cardKey, PlayerRef player, float x, float y, int sequenceSalt)
        {
            string effectKey = string.IsNullOrEmpty(cardKey) ? UnknownCardKeyPrefix : cardKey;
            Vector2 position = new Vector2(x, y);

            VfxEventBus.Publish(VfxPayloadFactory.Create(
                VfxEventType.CardEffectApplied,
                cardKey,
                effectKey,
                Runner,
                Object,
                player,
                player,
                position,
                Vector2.up,
                1f,
                0.75f,
                1f,
                VfxAttachPoint.CardSlot,
                sequenceSalt));

            VfxEventBus.Publish(VfxPayloadFactory.Create(
                VfxEventType.CardEffectToastRequested,
                cardKey,
                effectKey,
                Runner,
                Object,
                player,
                player,
                position,
                Vector2.up,
                1f,
                1.25f,
                1f,
                VfxAttachPoint.HudOverlay,
                sequenceSalt + CardToastVfxSequenceSaltOffset));
        }

        private void ApplyCard(PlayerRef player, CardData card)
        {
            var allHealth = SceneSearch.FindAllInScene<Player.PlayerHealth>(gameObject.scene);
            foreach (var health in allHealth)
            {
                var netObj = health.GetComponent<Fusion.NetworkObject>();
                if (netObj == null || netObj.InputAuthority != player) continue;

                var gun = netObj.GetComponentInChildren<Weapon.Gun>();
                if (gun != null)
                {
                    gun.FireRateMultiplier *= card.fireRateMultiplier;
                    gun.ExtraBullets += card.extraBullets;
                }

                if (card.healAmount > 0)
                {
                    health.CurrentHealth = Mathf.Min(health.CurrentHealth + card.healAmount, 100f);
                }

                health.LifestealPercent += card.lifestealPercent;

                var cardEffect = netObj.GetComponent<CardEffect>();
                if (cardEffect != null)
                {
                    cardEffect.ApplyCard(card);
                    Debug.Log($"[Colosseum] CardEffect AFTER apply - SizeMul:{cardEffect.BulletSizeMultiplier}, SpeedMul:{cardEffect.BulletSpeedMultiplier}, Bounce:{cardEffect.BonusBounce}");
                }
                else
                {
                    Debug.LogWarning("[Colosseum] CardEffect NOT FOUND on player!");
                }

                Debug.Log($"[Colosseum] Card applied: {card.cardName} to {player}");
                break;
            }

            // ApplyCard는 서버 권한에서만 실행되므로 OnCardApplied를 직접 invoke하면
            // 클라이언트 구독자(CardInventoryHUD 등)에 도달 못 함. RPC로 모든 클라에 fan-out.
            Rpc_NotifyCardApplied(player, card.cardId);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_NotifyCardApplied(PlayerRef player, int cardId)
        {
            CardData card = LookupCardById(cardId);
            if (card != null)
            {
                OnCardApplied?.Invoke(player, card);
            }
        }

        private CardData LookupCardById(int cardId)
        {
            for (int i = 0; i < _allCards.Count; i++)
            {
                CardData c = _allCards[i];
                if (c != null && c.cardId == cardId) return c;
            }
            return null;
        }

        private bool TryGetPlayerPosition(PlayerRef player, out Vector2 position)
        {
            var allHealth = SceneSearch.FindAllInScene<Player.PlayerHealth>(gameObject.scene);
            foreach (var health in allHealth)
            {
                var netObj = health.GetComponent<Fusion.NetworkObject>();
                if (netObj == null || netObj.InputAuthority != player) continue;

                position = health.transform.position;
                return true;
            }

            position = default;
            return false;
        }

        private List<int> BuildDraftPoolIndices(PlayerRef player)
        {
            var draftPool = new List<int>(_allCards.Count);

            for (int i = 0; i < _allCards.Count; i++)
            {
                CardData card = _allCards[i];
                if (card == null) continue;
                if (!card.isDraftEnabled) continue;
                if (!HasValidRuntimeMetadata(card, i)) continue;
                if (!CanPlayerTakeCard(player, card)) continue;
                draftPool.Add(i);
            }

            return draftPool;
        }

        private void FillDrawChoices(int slot, List<int> draftPool, int drawSeed)
        {
            for (int i = 0; i < _drawChoices; i++)
            {
                _drawnCardIndices.Set(GetChoiceOffset(slot, i), InvalidCardId);
            }

            int uniqueChoiceCount = Mathf.Min(_drawChoices, draftPool.Count);
            var random = new System.Random(drawSeed);
            var remaining = new List<int>(draftPool);

            for (int i = 0; i < uniqueChoiceCount; i++)
            {
                int selectedCardIndex = SelectWeightedUniqueCardIndex(remaining, random);
                if (selectedCardIndex < 0)
                {
                    break;
                }

                _drawnCardIndices.Set(GetChoiceOffset(slot, i), selectedCardIndex);
                remaining.Remove(selectedCardIndex);
            }
        }

        private int SelectWeightedUniqueCardIndex(List<int> candidates, System.Random random)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return InvalidCardId;
            }

            float totalWeight = 0f;
            var availableRarities = new List<CardRarity>(OfferRarities.Length);

            for (int i = 0; i < OfferRarities.Length; i++)
            {
                CardRarity rarity = OfferRarities[i];
                if (!HasCandidateByRarity(candidates, rarity))
                {
                    continue;
                }

                float weight = GetOfferWeight(rarity);
                if (weight <= 0f)
                {
                    continue;
                }

                availableRarities.Add(rarity);
                totalWeight += weight;
            }

            if (availableRarities.Count == 0 || totalWeight <= 0f)
            {
                return candidates[random.Next(candidates.Count)];
            }

            float roll = (float)(random.NextDouble() * totalWeight);
            float acc = 0f;
            CardRarity selectedRarity = availableRarities[availableRarities.Count - 1];
            for (int i = 0; i < availableRarities.Count; i++)
            {
                CardRarity rarity = availableRarities[i];
                acc += GetOfferWeight(rarity);
                if (roll <= acc)
                {
                    selectedRarity = rarity;
                    break;
                }
            }

            var rarityCandidates = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
            {
                int candidateIndex = candidates[i];
                CardData candidateCard = _allCards[candidateIndex];
                if (candidateCard == null) continue;
                if (candidateCard.rarity != selectedRarity) continue;
                rarityCandidates.Add(candidateIndex);
            }

            if (rarityCandidates.Count == 0)
            {
                return candidates[random.Next(candidates.Count)];
            }

            return rarityCandidates[random.Next(rarityCandidates.Count)];
        }

        private bool HasCandidateByRarity(List<int> candidates, CardRarity rarity)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                int candidateIndex = candidates[i];
                CardData card = _allCards[candidateIndex];
                if (card == null) continue;
                if (card.rarity != rarity) continue;
                return true;
            }

            return false;
        }

        private float GetOfferWeight(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common:
                    return Mathf.Max(0f, _commonOfferWeight);
                case CardRarity.Rare:
                    return Mathf.Max(0f, _rareOfferWeight);
                case CardRarity.Epic:
                    return Mathf.Max(0f, _epicOfferWeight);
                case CardRarity.Legendary:
                    return Mathf.Max(0f, _legendaryOfferWeight);
                default:
                    return 0f;
            }
        }

        private int GetNextOfferSeed(PlayerRef player, int slot)
        {
            _offerSeedCounter = unchecked(_offerSeedCounter + 1);
            int playerSeed = player.PlayerId;
            int tickSeed = Runner != null ? Runner.Tick.Raw : 0;
            int hash = 17;
            hash = hash * 31 + _offerSeedCounter;
            hash = hash * 31 + playerSeed;
            hash = hash * 31 + slot;
            hash = hash * 31 + tickSeed;
            return hash;
        }

        private bool HasValidRuntimeMetadata(CardData card, int cardIndex)
        {
            bool validId = card.cardId > 0;
            bool validKey = !string.IsNullOrWhiteSpace(card.cardKey);
            bool validStack = card.maxStack >= 1;
            bool validCategory = card.category != CardCategory.None;

            if (validId && validKey && validStack && validCategory)
            {
                return true;
            }

            if (_invalidCardMetadataLoggedIndices.Add(cardIndex))
            {
                Debug.LogWarning(
                    $"[Colosseum] Card metadata invalid. index={cardIndex}, name={card.cardName}, " +
                    $"cardId={card.cardId}, cardKey='{card.cardKey}', maxStack={card.maxStack}, category={card.category}");
            }

            return false;
        }

        public override void Spawned()
        {
            EnsureDrawChoicePolicyInvariant();
            SpawnedInScene?.Invoke(this);
            if (!Object.HasStateAuthority) return;
            EnsureCardCatalogValidated();
        }

        private void OnValidate()
        {
            EnsureDrawChoicePolicyInvariant();
        }

        private bool EnsureCardCatalogValidated()
        {
            if (_catalogValidationChecked)
            {
                return _catalogValidationPassed;
            }

            _catalogValidationPassed = ValidateCardCatalog(out List<string> errors);
            _catalogValidationChecked = true;

            if (_catalogValidationPassed)
            {
                return true;
            }

            Debug.LogError(
                $"[Colosseum] Card catalog validation failed. Draw is blocked until metadata is fixed.\n{string.Join("\n", errors)}");
            return false;
        }

        private bool ValidateCardCatalog(out List<string> errors)
        {
            errors = new List<string>();
            var seenIds = new Dictionary<int, int>();
            var seenKeys = new Dictionary<string, int>(System.StringComparer.Ordinal);

            for (int i = 0; i < _allCards.Count; i++)
            {
                CardData card = _allCards[i];
                if (card == null)
                {
                    errors.Add($"[{i}] null card reference");
                    continue;
                }

                if (card.cardId <= 0)
                {
                    errors.Add($"[{i}] {card.cardName}: cardId must be > 0 (current={card.cardId})");
                }
                else if (seenIds.TryGetValue(card.cardId, out int duplicatedIdIndex))
                {
                    errors.Add($"[{i}] {card.cardName}: duplicated cardId={card.cardId} (already used at index {duplicatedIdIndex})");
                }
                else
                {
                    seenIds[card.cardId] = i;
                }

                string normalizedKey = card.cardKey?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedKey))
                {
                    errors.Add($"[{i}] {card.cardName}: cardKey is empty");
                }
                else if (seenKeys.TryGetValue(normalizedKey, out int duplicatedKeyIndex))
                {
                    errors.Add($"[{i}] {card.cardName}: duplicated cardKey='{normalizedKey}' (already used at index {duplicatedKeyIndex})");
                }
                else
                {
                    seenKeys[normalizedKey] = i;
                }

                if (card.maxStack < 1)
                {
                    errors.Add($"[{i}] {card.cardName}: maxStack must be >= 1 (current={card.maxStack})");
                }

                if (card.category == CardCategory.None)
                {
                    errors.Add($"[{i}] {card.cardName}: category must not be None");
                }
            }

            return errors.Count == 0;
        }

        private bool CanPlayerTakeCard(PlayerRef player, CardData card)
        {
            int currentStack = GetPlayerCardStack(player, card);
            if (currentStack >= Mathf.Max(1, card.maxStack))
            {
                return false;
            }

            int rarityCap = GetRarityOwnershipCap(card.rarity);
            if (rarityCap < 0)
            {
                return true;
            }

            int currentRarityStack = GetPlayerRarityStackCount(player, card.rarity);
            return currentRarityStack < rarityCap;
        }

        private void TryAutoSelectRandomCard(int slot, PlayerRef player)
        {
            var selectableChoices = new List<int>(_drawChoices);
            for (int i = 0; i < _drawChoices; i++)
            {
                CardData card = GetDrawnCard(player, i);
                if (card == null) continue;
                if (!CanPlayerTakeCard(player, card)) continue;
                selectableChoices.Add(i);
            }

            if (selectableChoices.Count == 0)
            {
                Debug.LogWarning($"[Colosseum] Card draw timeout for {player}, but no selectable cards remain. Clearing pending draw.");
                ClearPendingSlot(slot);
                return;
            }

            int randomChoiceIndex = selectableChoices[Random.Range(0, selectableChoices.Count)];
            Debug.Log($"[Colosseum] Card draw timeout for {player}! Auto-selecting random choice {randomChoiceIndex}.");
            SelectCard(slot, randomChoiceIndex);
        }

        private int GetPlayerRarityStackCount(PlayerRef player, CardRarity rarity)
        {
            int total = 0;
            for (int i = 0; i < _allCards.Count; i++)
            {
                CardData card = _allCards[i];
                if (card == null) continue;
                if (card.rarity != rarity) continue;
                total += GetPlayerCardStack(player, card);
            }

            return total;
        }

        private static int GetRarityOwnershipCap(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Epic:
                    return EpicOwnershipCap;
                case CardRarity.Legendary:
                    return LegendaryOwnershipCap;
                default:
                    return -1;
            }
        }

        private int GetPlayerCardStack(PlayerRef player, CardData card)
        {
            string stackKey = GetCardStackKey(card);
            if (!_playerCardStacks.TryGetValue(player, out Dictionary<string, int> perPlayerStacks))
            {
                return 0;
            }

            if (!perPlayerStacks.TryGetValue(stackKey, out int stack))
            {
                return 0;
            }

            return stack;
        }

        private void IncrementPlayerCardStack(PlayerRef player, CardData card)
        {
            string stackKey = GetCardStackKey(card);
            if (!_playerCardStacks.TryGetValue(player, out Dictionary<string, int> perPlayerStacks))
            {
                perPlayerStacks = new Dictionary<string, int>();
                _playerCardStacks[player] = perPlayerStacks;
            }

            perPlayerStacks.TryGetValue(stackKey, out int currentStack);
            perPlayerStacks[stackKey] = currentStack + 1;
        }

        private static string GetCardStackKey(CardData card)
        {
            if (!string.IsNullOrWhiteSpace(card.cardKey))
            {
                return card.cardKey;
            }

            if (card.cardId > 0)
            {
                return $"card-id-{card.cardId}";
            }

            return $"{UnknownCardKeyPrefix}-{card.cardName}";
        }

        private int GetPendingSlot(PlayerRef player)
        {
            for (int i = 0; i < MaxPendingPlayers; i++)
            {
                if (_pendingPlayers[i] == player)
                {
                    return i;
                }
            }

            return -1;
        }

        private int GetFirstEmptySlot()
        {
            for (int i = 0; i < MaxPendingPlayers; i++)
            {
                if (_pendingPlayers[i] == default)
                {
                    return i;
                }
            }

            return -1;
        }

        private int GetFirstOccupiedSlot()
        {
            for (int i = 0; i < MaxPendingPlayers; i++)
            {
                if (_pendingPlayers[i] != default)
                {
                    return i;
                }
            }

            return -1;
        }

        private int GetChoiceOffset(int slot, int choiceIndex)
        {
            return slot * _drawChoices + choiceIndex;
        }

        private TickTimer GetDrawTimeout(int slot)
        {
            return slot == 0 ? _drawTimeout0 : _drawTimeout1;
        }

        private void SetDrawTimeout(int slot, TickTimer timeout)
        {
            if (slot == 0)
            {
                _drawTimeout0 = timeout;
            }
            else
            {
                _drawTimeout1 = timeout;
            }
        }

        private void ClearPendingSlot(int slot)
        {
            _pendingPlayers.Set(slot, default);

            for (int i = 0; i < _drawChoices; i++)
            {
                _drawnCardIndices.Set(GetChoiceOffset(slot, i), InvalidCardId);
            }

            SetDrawTimeout(slot, default);
        }

        private void EnsureDrawChoicePolicyInvariant()
        {
            if (_drawChoices == FixedDrawChoices)
            {
                return;
            }

            Debug.LogWarning($"[Colosseum] Draft choice count is fixed to {FixedDrawChoices}. Overriding configured value {_drawChoices}.");
            _drawChoices = FixedDrawChoices;
        }
    }
}
