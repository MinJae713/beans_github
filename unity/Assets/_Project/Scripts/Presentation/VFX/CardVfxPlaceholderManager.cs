using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Colosseum.Contracts;
using Colosseum.Card;
using Colosseum.Network;
using Colosseum.Player;
using Colosseum.Weapon;

namespace Colosseum.Presentation.VFX
{
    public class CardVfxPlaceholderManager : MonoBehaviour
    {
        private const int DefaultDedupeLimit = 256;
        private const int DefaultMaxWorldMarkers = 32;
        private const string DefaultProfileResourcePath = "Presentation/VfxFeedbackProfile";
        private const float HitBlinkDurationSeconds = 0.18f;
        private const float HitBlinkIntervalSeconds = 0.045f;
        private const int StatusVfxForegroundSortingOrder = 42;
        private const int OverheadVfxSortingOrder = 64;
        private const float MinPrefabScale = 0.01f;
        private const float CharacterHeadAttachYOffset = 0.45f;
        private const float EmojiOverheadFollowYOffset = 2.35f;
        private const float CardIllustrationDurationSeconds = 2f;
        private const float CardIllustrationWorldHeight = 1.5f;
        private const float CardIllustrationHeadYOffset = CharacterHeadAttachYOffset + EmojiOverheadFollowYOffset;
        private static readonly Dictionary<string, Sprite> CardIconSpriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static bool _cardIconSpriteCacheInitialized;

        [SerializeField] private VfxFeedbackProfile _profile;
        [SerializeField] private int _maxDedupeKeys = DefaultDedupeLimit;
        [SerializeField] private int _maxWorldMarkers = DefaultMaxWorldMarkers;
        [SerializeField] private float _victoryConfettiTileSpacing = 7f;
        [SerializeField] private int _victoryConfettiMaxTiles = 96;
        [SerializeField] private float _toastDurationSeconds = 1.25f;
        [SerializeField] private Vector2 _toastSize = new Vector2(360f, 56f);

        private readonly HashSet<string> _dedupeKeys = new HashSet<string>();
        private readonly Queue<string> _dedupeOrder = new Queue<string>();
        private readonly Queue<GameObject> _worldMarkers = new Queue<GameObject>();
        private readonly Dictionary<string, GameObject> _overchargeReadyEffects = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _activeStatusEffects = new Dictionary<string, GameObject>();
        private GameObject _victoryConfettiMarker;
        private Canvas _toastCanvas;
        private RectTransform _toastRoot;
        private Sprite _circleSprite;
        private Sprite _squareSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return;
            }

            if (FindObjectOfType<CardVfxPlaceholderManager>() != null)
            {
                return;
            }

            var obj = new GameObject("CardVfxPlaceholderManager");
            DontDestroyOnLoad(obj);
            obj.AddComponent<CardVfxPlaceholderManager>();
        }

        private void OnEnable()
        {
            VfxEventBus.Published += OnVfxEvent;
            VfxEventBus.HandlerFailed += OnVfxHandlerFailed;
            NetworkManager.OnRoomReset += HandleRoomResetPresentationState;
            NetworkManager.OnGameStart += ClearVictoryZoneConfetti;
            NetworkManager.OnGameStart += ClearStatusVfxEffects;
            NetworkManager.OnDisconnected += HandleDisconnectedPresentationState;
        }

        private void OnDisable()
        {
            VfxEventBus.Published -= OnVfxEvent;
            VfxEventBus.HandlerFailed -= OnVfxHandlerFailed;
            NetworkManager.OnRoomReset -= HandleRoomResetPresentationState;
            NetworkManager.OnGameStart -= ClearVictoryZoneConfetti;
            NetworkManager.OnGameStart -= ClearStatusVfxEffects;
            NetworkManager.OnDisconnected -= HandleDisconnectedPresentationState;
            ClearOverchargeReadyEffects();
            ClearStatusVfxEffects();
            ClearVictoryZoneConfetti();
        }

        private void OnVfxHandlerFailed(VfxEventPayload payload, Exception exception)
        {
            Debug.LogException(new InvalidOperationException(
                $"[Colosseum/VFX] Placeholder handler failed for {payload.EventType}/{payload.EffectKey}.",
                exception));
        }

        private void OnVfxEvent(VfxEventPayload payload)
        {
            EnsureProfileLoaded();

            if (payload.PredictionState == VfxPredictionState.Rejected)
            {
                _dedupeKeys.Remove(payload.GetDedupeKey());
                return;
            }

            if (IsSfxOnlyEvent(payload.EventType))
            {
                return;
            }

            if (payload.EventType == VfxEventType.OverchargeShotFired &&
                payload.EffectKey == "reload_overcharge")
            {
                ClearOverchargeReadyEffect(payload);
            }

            string key = payload.GetDedupeKey();
            if (!_dedupeKeys.Add(key))
            {
                return;
            }

            _dedupeOrder.Enqueue(key);
            TrimDedupe();

            CardVfxPreset preset = ResolvePreset(payload);
            if (payload.EventType == VfxEventType.CardEffectApplied)
            {
                SpawnCardIllustration(payload);
            }

            bool shouldBlinkTargetPlayer = ShouldBlinkTargetPlayer(payload);
            if (ShouldSuppressLegacyStatusImpact(payload))
            {
                if (shouldBlinkTargetPlayer)
                {
                    BlinkTargetPlayer(payload);
                }

                return;
            }

            if (ShouldShowToast(payload, preset))
            {
                ShowToast(payload, preset);
            }

            if (ShouldSpawnWorldMarker(payload, preset))
            {
                SpawnWorldEffect(payload, preset);
            }

            if (shouldBlinkTargetPlayer)
            {
                BlinkTargetPlayer(payload);
            }
        }

        private void SpawnCardIllustration(VfxEventPayload payload)
        {
            Sprite icon = ResolveCardIconSprite(payload);
            if (icon == null)
            {
                return;
            }

            int playerSlot = payload.TargetPlayerSlot > 0 ? payload.TargetPlayerSlot : payload.SourcePlayerSlot;
            Vector3 position = ResolveCardIllustrationPosition(payload, playerSlot);

            var marker = new GameObject("VfxMarker_CardIllustration");
            marker.transform.position = position;

            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = icon;
            renderer.sortingOrder = OverheadVfxSortingOrder;
            renderer.color = Color.white;

            marker.transform.localScale = Vector3.one * ResolveCardIllustrationScale(icon);
            _worldMarkers.Enqueue(marker);
            TrimWorldMarkers();

            StartCoroutine(AnimateCardIllustration(marker, renderer, payload, playerSlot, CardIllustrationDurationSeconds));
            LogPresentation(payload, null, position, 1f, CardIllustrationDurationSeconds, 0.2f, "card_illustration", icon.name);
        }

        private static Sprite ResolveCardIconSprite(VfxEventPayload payload)
        {
            string key = !string.IsNullOrEmpty(payload.CardKey) ? payload.CardKey : payload.EffectKey;
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            EnsureCardIconSpriteCache(false);
            if (CardIconSpriteCache.TryGetValue(key, out Sprite icon))
            {
                return icon;
            }

            EnsureCardIconSpriteCache(true);
            if (CardIconSpriteCache.TryGetValue(key, out icon))
            {
                return icon;
            }

            return null;
        }

        private static void EnsureCardIconSpriteCache(bool forceRefresh)
        {
            if (_cardIconSpriteCacheInitialized && !forceRefresh)
            {
                return;
            }

            _cardIconSpriteCacheInitialized = true;
            CardIconSpriteCache.Clear();
            CardData[] loadedCards = Resources.FindObjectsOfTypeAll<CardData>();
            for (int i = 0; i < loadedCards.Length; i++)
            {
                CardData card = loadedCards[i];
                if (card == null || card.cardIcon == null)
                {
                    continue;
                }

                AddCardIconSpriteCacheEntry(card.cardKey, card.cardIcon);
                AddCardIconSpriteCacheEntry(card.cardName, card.cardIcon);
            }
        }

        private static void AddCardIconSpriteCacheEntry(string key, Sprite icon)
        {
            if (string.IsNullOrEmpty(key) || icon == null || CardIconSpriteCache.ContainsKey(key))
            {
                return;
            }

            CardIconSpriteCache.Add(key, icon);
        }

        private static float ResolveCardIllustrationScale(Sprite icon)
        {
            if (icon == null)
            {
                return 1f;
            }

            Vector2 size = icon.bounds.size;
            float maxSize = Mathf.Max(size.x, size.y);
            return maxSize > 0.0001f ? CardIllustrationWorldHeight / maxSize : 1f;
        }

        private static Vector3 ResolveCardIllustrationPosition(VfxEventPayload payload, int playerSlot)
        {
            PlayerHealth player = FindPlayerHealthBySlot(playerSlot);
            if (player != null)
            {
                return player.transform.position + new Vector3(0f, CardIllustrationHeadYOffset, 0f);
            }

            Vector3 fallback = ResolveMarkerPosition(payload);
            fallback.y += CardIllustrationHeadYOffset;
            return fallback;
        }

        private IEnumerator AnimateCardIllustration(
            GameObject marker,
            SpriteRenderer renderer,
            VfxEventPayload payload,
            int playerSlot,
            float duration)
        {
            float elapsed = 0f;
            Vector3 baseScale = marker != null ? marker.transform.localScale : Vector3.one;
            while (marker != null && elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                marker.transform.position = ResolveCardIllustrationPosition(payload, playerSlot) +
                    new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.12f, 0f);
                float pop = Mathf.Lerp(0.82f, 1f, Mathf.SmoothStep(0f, 0.18f, t));
                marker.transform.localScale = baseScale * pop;

                if (renderer != null)
                {
                    renderer.color = Color.white;
                }

                yield return null;
                elapsed += Time.deltaTime;
            }

            if (marker != null)
            {
                Destroy(marker);
            }
        }

        private static bool IsSfxOnlyEvent(VfxEventType eventType)
        {
            return eventType == VfxEventType.PlayerRespawned;
        }

        private CardVfxPreset ResolvePreset(VfxEventPayload payload)
        {
            if (_profile != null && _profile.TryFindPreset(payload, out CardVfxPreset preset))
            {
                return preset;
            }

            return CreateFallbackPreset(payload);
        }

        private void EnsureProfileLoaded()
        {
            if (_profile != null)
            {
                return;
            }

            _profile = Resources.Load<VfxFeedbackProfile>(DefaultProfileResourcePath);
        }

        private static CardVfxPreset CreateFallbackPreset(VfxEventPayload payload)
        {
            var preset = new CardVfxPreset
            {
                eventType = payload.EventType,
                cardKey = payload.CardKey,
                effectKey = payload.EffectKey,
                color = ResolveColor(payload),
                durationSeconds = payload.DurationHint > 0f ? payload.DurationHint : 0.75f,
                radiusScale = payload.RadiusHint > 0f ? payload.RadiusHint : 1f,
                showToast = payload.EventType == VfxEventType.CardEffectApplied ||
                    payload.EventType == VfxEventType.CardEffectToastRequested ||
                    payload.EventType == VfxEventType.StatusApplied ||
                    payload.EventType == VfxEventType.ReloadCompleted ||
                    payload.EventType == VfxEventType.PlayerRespawn ||
                    payload.EventType == VfxEventType.PlayerHealOnKill ||
                    payload.EventType == VfxEventType.OverchargeReady,
                toastLabel = ResolveLabel(payload)
            };

            preset.style = payload.EventType switch
            {
                VfxEventType.ExplosionTriggered => VfxPlaceholderStyle.Ring,
                VfxEventType.ProjectileImpact => VfxPlaceholderStyle.Burst,
                VfxEventType.ProjectileSpawnedWithModifier => VfxPlaceholderStyle.Pulse,
                VfxEventType.StatusApplied => VfxPlaceholderStyle.Pulse,
                VfxEventType.ReloadStarted => VfxPlaceholderStyle.Pulse,
                VfxEventType.ReloadCompleted => VfxPlaceholderStyle.Pulse,
                VfxEventType.CardEffectToastRequested => VfxPlaceholderStyle.ToastOnly,
                VfxEventType.OverchargeReady => VfxPlaceholderStyle.Pulse,
                VfxEventType.OverchargeShotFired => VfxPlaceholderStyle.Burst,
                VfxEventType.PlayerDeath => VfxPlaceholderStyle.Ring,
                VfxEventType.PlayerRespawn => VfxPlaceholderStyle.Pulse,
                VfxEventType.PlayerHealOnKill => VfxPlaceholderStyle.Pulse,
                VfxEventType.SegmentBoundaryAdvanced => VfxPlaceholderStyle.Ring,
                VfxEventType.PitFallDeath => VfxPlaceholderStyle.Ring,
                _ => VfxPlaceholderStyle.Pulse
            };

            return preset;
        }

        private static Color ResolveColor(VfxEventPayload payload)
        {
            string key = string.IsNullOrEmpty(payload.EffectKey) ? payload.CardKey : payload.EffectKey;
            switch (key)
            {
                case "poison":
                case "hit_apply_poison": return new Color(0.35f, 1f, 0.25f, 0.82f);
                case "burn":
                case "hit_apply_burn": return new Color(1f, 0.32f, 0.08f, 0.88f);
                case "freeze":
                case "frost":
                case "hit_apply_frost": return new Color(0.35f, 0.8f, 1f, 0.82f);
                case "electric":
                case "hit_apply_electric_stack": return new Color(1f, 0.95f, 0.1f, 0.9f);
                case "projectile_explosion": return new Color(1f, 0.55f, 0.08f, 0.72f);
                case "projectile_reflect": return new Color(0.28f, 0.7f, 1f, 0.82f);
                case "projectile_pierce": return new Color(0.75f, 0.55f, 1f, 0.82f);
                case "projectile_speed_up": return new Color(0.1f, 0.85f, 1f, 0.82f);
                case "projectile_size_up": return new Color(1f, 0.75f, 0.2f, 0.82f);
                case "projectile_heavy": return new Color(0.86f, 0.34f, 0.12f, 0.86f);
                case "projectile_multishot": return new Color(0.35f, 1f, 0.72f, 0.84f);
                case "projectile_gravity_zero": return new Color(0.62f, 0.46f, 1f, 0.84f);
                case "hit_knockback_up": return new Color(1f, 0.35f, 0.18f, 0.82f);
                case "reload_started": return new Color(0.45f, 0.78f, 1f, 0.78f);
                case "reload_completed": return new Color(0.35f, 1f, 0.6f, 0.84f);
                case "reload_overcharge": return new Color(1f, 0.9f, 0.05f, 0.9f);
                case "player_death": return new Color(1f, 0.12f, 0.18f, 0.86f);
                case "player_respawn": return new Color(0.25f, 0.85f, 1f, 0.82f);
                case "heal_on_kill": return new Color(0.28f, 1f, 0.45f, 0.86f);
                case "segment_advance": return new Color(0.55f, 0.92f, 1f, 0.82f);
                case "pit_fall_death": return new Color(0.78f, 0.25f, 1f, 0.84f);
                default: return new Color(0.95f, 0.95f, 1f, 0.72f);
            }
        }

        private static string ResolveLabel(VfxEventPayload payload)
        {
            if (!string.IsNullOrEmpty(payload.EffectKey))
            {
                return payload.EventType == VfxEventType.CardEffectApplied
                    ? $"CARD // {payload.EffectKey}"
                    : payload.EffectKey;
            }

            return payload.EventType.ToString();
        }

        private static bool ShouldShowToast(VfxEventPayload payload, CardVfxPreset preset)
        {
            if (payload.EventType == VfxEventType.CardEffectToastRequested)
            {
                return true;
            }

            if (payload.EventType == VfxEventType.CardEffectApplied)
            {
                return false;
            }

            return preset.showToast || payload.AttachPoint == VfxAttachPoint.HudOverlay;
        }

        private static bool ShouldSpawnWorldMarker(VfxEventPayload payload, CardVfxPreset preset)
        {
            if (ShouldSuppressProjectileSpawnPlaceholder(payload, preset))
            {
                return false;
            }

            return preset.style != VfxPlaceholderStyle.ToastOnly && !IsUiOnlyAttachPoint(payload.AttachPoint);
        }

        private static bool ShouldSuppressProjectileSpawnPlaceholder(VfxEventPayload payload, CardVfxPreset preset)
        {
            return payload.EventType == VfxEventType.ProjectileSpawnedWithModifier &&
                preset != null &&
                preset.effectPrefab == null &&
                IsLegacyProjectileSpawnPlaceholderSuppressedKey(payload.EffectKey);
        }

        private static bool ShouldSuppressLegacyStatusImpact(VfxEventPayload payload)
        {
            return payload.EventType == VfxEventType.ProjectileImpact &&
                IsStatusProjectileEffectKey(payload.EffectKey);
        }

        private static bool ShouldBlinkTargetPlayer(VfxEventPayload payload)
        {
            return payload.EventType == VfxEventType.ProjectileImpact &&
                payload.TargetPlayerSlot > 0;
        }

        private static bool IsLegacyProjectileSpawnPlaceholderSuppressedKey(string effectKey)
        {
            switch (effectKey)
            {
                case "hit_apply_burn":
                case "hit_apply_poison":
                case "hit_apply_frost":
                case "hit_apply_electric_stack":
                case "projectile_gravity_zero":
                case "projectile_reflect":
                case "projectile_pierce":
                case "projectile_explosion":
                case "hit_knockback_up":
                case "projectile_size_up":
                case "projectile_speed_up":
                case "projectile_heavy":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsUiOnlyAttachPoint(VfxAttachPoint attachPoint)
        {
            return attachPoint == VfxAttachPoint.HudOverlay || attachPoint == VfxAttachPoint.CardSlot;
        }

        private void ShowToast(VfxEventPayload payload, CardVfxPreset preset)
        {
            EnsureToastCanvas();

            var toastObj = new GameObject("VfxToast");
            toastObj.transform.SetParent(_toastRoot, false);

            var rect = toastObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.82f);
            rect.anchorMax = new Vector2(0.5f, 0.82f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = _toastSize;

            var group = toastObj.AddComponent<CanvasGroup>();
            var image = toastObj.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(0.04f, 0.05f, 0.09f, 0.78f);

            var accentObj = new GameObject("Accent");
            accentObj.transform.SetParent(toastObj.transform, false);
            var accentRect = accentObj.AddComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.sizeDelta = new Vector2(6f, 0f);
            var accent = accentObj.AddComponent<Image>();
            accent.raycastTarget = false;
            accent.color = preset.color;

            var textObj = new GameObject("Label");
            textObj.transform.SetParent(toastObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.text = string.IsNullOrEmpty(preset.toastLabel) ? ResolveLabel(payload) : preset.toastLabel;
            text.fontSize = 18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.characterSpacing = 4f;

            StartCoroutine(FadeAndDestroy(toastObj, group, _toastDurationSeconds));
            LogPresentation(payload, preset, Vector3.zero, 0f, _toastDurationSeconds, 0f, "toast");
        }

        private void SpawnWorldEffect(VfxEventPayload payload, CardVfxPreset preset)
        {
            if (preset.effectPrefab == null &&
                payload.EventType == VfxEventType.ProjectileReflected &&
                payload.EffectKey == "projectile_reflect")
            {
                SpawnProjectileReflectFlash(payload, preset);
                return;
            }

            if (preset.effectPrefab == null &&
                payload.EventType == VfxEventType.ProjectilePierced &&
                payload.EffectKey == "projectile_pierce")
            {
                SpawnProjectilePierceStreak(payload, preset);
                return;
            }

            if (preset.effectPrefab == null &&
                payload.EventType == VfxEventType.ProjectileImpact &&
                payload.EffectKey == "projectile_impact")
            {
                SpawnProjectileImpactBurst(payload, preset);
                return;
            }

            if (preset.effectPrefab == null &&
                payload.EventType == VfxEventType.ProjectileSpawnedWithModifier)
            {
                SpawnProjectileModifierTrail(payload, preset);
                return;
            }

            if (payload.EventType == VfxEventType.OverchargeReady &&
                payload.EffectKey == "reload_overcharge" &&
                preset.keepAliveUntilOverchargeShot &&
                preset.effectPrefab != null)
            {
                SpawnStatefulPrefabEffect(payload, preset);
                return;
            }

            if (payload.EventType == VfxEventType.VictoryZoneReached &&
                payload.EffectKey == "victory_zone_confetti" &&
                preset.effectPrefab != null)
            {
                SpawnVictoryZoneConfetti(payload, preset);
                return;
            }

            if (preset.effectPrefab != null)
            {
                SpawnPrefabEffect(payload, preset);
                return;
            }

            SpawnWorldMarker(payload, preset);
        }

        private void SpawnPrefabEffect(VfxEventPayload payload, CardVfxPreset preset)
        {
            bool isStatefulStatusVfx = IsStatefulStatusVfx(payload);
            string statusEffectKey = null;
            if (isStatefulStatusVfx)
            {
                statusEffectKey = GetStatusVfxKey(payload);
                ClearStatusVfxEffect(statusEffectKey);
            }

            Vector3 position = ResolveEffectPosition(payload, preset);
            Quaternion rotation = ResolveMarkerRotation(payload, preset);
            var instance = Instantiate(preset.effectPrefab, position, rotation);

            float radius = ResolveMarkerRadius(payload, preset);
            float scale = Mathf.Max(MinPrefabScale, preset.prefabScale) * radius * ResolvePayloadScaleMultiplier(payload);
            instance.transform.localScale = instance.transform.localScale * scale;
            if (isStatefulStatusVfx)
            {
                _activeStatusEffects[statusEffectKey] = instance;
            }
            else
            {
                _worldMarkers.Enqueue(instance);
                TrimWorldMarkers();
            }

            PrepareParticleSystems(instance, preset, isStatefulStatusVfx);

            float duration = ResolveDuration(payload, preset);
            float peakSeconds = ResolvePeakSeconds(duration, preset.style);
            LogPresentation(payload, preset, position, radius, duration, peakSeconds, "prefab", preset.effectPrefab.name);
            if (ShouldFollowEffect(preset))
            {
                StartCoroutine(FollowEffect(instance, payload, preset, duration + 0.25f));
            }

            SpawnAdditionalPrefabEffects(payload, preset, position, rotation, radius, duration);
            if (isStatefulStatusVfx)
            {
                StartCoroutine(StopStatusVfxLoopingAfter(instance, payload));
                StartCoroutine(DestroyStatusEffectAfter(statusEffectKey, instance, duration + 0.25f));
            }
            else
            {
                StartCoroutine(DestroyAfter(instance, duration + 0.25f));
            }
        }

        private void SpawnVictoryZoneConfetti(VfxEventPayload payload, CardVfxPreset preset)
        {
            ClearVictoryZoneConfetti();

            Vector3 center = ResolveMarkerPosition(payload);
            float width = Mathf.Max(_victoryConfettiTileSpacing, ResolveMarkerRadius(payload, preset));
            float height = Mathf.Max(_victoryConfettiTileSpacing, payload.MagnitudeHint > 0f ? payload.MagnitudeHint : width * 0.45f);
            float duration = ResolveDuration(payload, preset);
            float peakSeconds = ResolvePeakSeconds(duration, preset.style);
            float tileCoverageHeight = Mathf.Max(1f, _victoryConfettiTileSpacing);
            float scale = Mathf.Max(MinPrefabScale, preset.prefabScale) * Mathf.Max(1f, height / tileCoverageHeight);
            float tileSpanX = Mathf.Max(1f, height * 0.85f);
            int columnCount = Mathf.Clamp(
                Mathf.CeilToInt(width / tileSpanX) + 2,
                3,
                Mathf.Max(3, _victoryConfettiMaxTiles));
            float leftX = center.x - width * 0.5f - tileSpanX * 0.5f;
            float stepX = columnCount > 1 ? (width + tileSpanX) / (columnCount - 1) : 0f;
            float topAlignedY = center.y + height * 0.5f;

            var marker = new GameObject("VfxMarker_VictoryZoneConfetti");
            marker.transform.position = center;
            _victoryConfettiMarker = marker;

            Quaternion prefabRotation = preset.effectPrefab.transform.rotation;
            for (int column = 0; column < columnCount; column++)
            {
                Vector3 position = new Vector3(leftX + stepX * column, topAlignedY, center.z);
                var instance = Instantiate(preset.effectPrefab, position, prefabRotation, marker.transform);
                instance.transform.localScale = instance.transform.localScale * scale;
                PrepareParticleSystems(instance, preset, true);
            }

            _worldMarkers.Enqueue(marker);
            TrimWorldMarkers();

            LogPresentation(payload, preset, center, width, duration, peakSeconds, "victory_zone_confetti", preset.effectPrefab.name);
        }

        private void HandleRoomResetPresentationState(string _)
        {
            ClearStatusVfxEffects();
            ClearVictoryZoneConfetti();
        }

        private void HandleDisconnectedPresentationState(string _)
        {
            ClearStatusVfxEffects();
            ClearVictoryZoneConfetti();
        }

        private void ClearVictoryZoneConfetti()
        {
            if (_victoryConfettiMarker == null)
            {
                return;
            }

            Destroy(_victoryConfettiMarker);
            _victoryConfettiMarker = null;
        }

        private void SpawnStatefulPrefabEffect(VfxEventPayload payload, CardVfxPreset preset)
        {
            string key = GetOverchargeReadyKey(payload);
            ClearOverchargeReadyEffect(key);

            Vector3 position = ResolveEffectPosition(payload, preset);
            Quaternion rotation = ResolveMarkerRotation(payload, preset);
            var instance = Instantiate(preset.effectPrefab, position, rotation);

            float radius = ResolveMarkerRadius(payload, preset);
            float scale = Mathf.Max(MinPrefabScale, preset.prefabScale) * radius;
            instance.transform.localScale = instance.transform.localScale * scale;
            _overchargeReadyEffects[key] = instance;
            _worldMarkers.Enqueue(instance);
            TrimWorldMarkers();

            PrepareParticleSystems(instance, preset, true);

            float duration = ResolveDuration(payload, preset);
            float peakSeconds = ResolvePeakSeconds(duration, preset.style);
            LogPresentation(payload, preset, position, radius, duration, peakSeconds, "prefab_state", preset.effectPrefab.name);
            if (ShouldFollowEffect(preset))
            {
                StartCoroutine(FollowEffect(instance, payload, preset, 0f));
            }
        }

        private void SpawnAdditionalPrefabEffects(
            VfxEventPayload payload,
            CardVfxPreset preset,
            Vector3 position,
            Quaternion rotation,
            float radius,
            float duration)
        {
            if (preset.additionalEffectPrefabs == null || preset.additionalEffectPrefabs.Length == 0)
            {
                return;
            }

            if (preset.additionalEffectMinMagnitude > 0f &&
                payload.MagnitudeHint < preset.additionalEffectMinMagnitude)
            {
                return;
            }

            for (int i = 0; i < preset.additionalEffectPrefabs.Length; i++)
            {
                GameObject additionalPrefab = preset.additionalEffectPrefabs[i];
                if (additionalPrefab == null)
                {
                    continue;
                }

                var instance = Instantiate(additionalPrefab, position, rotation);
                float scale = Mathf.Max(MinPrefabScale, preset.prefabScale) * radius * ResolvePayloadScaleMultiplier(payload);
                instance.transform.localScale = instance.transform.localScale * scale;
                _worldMarkers.Enqueue(instance);
                TrimWorldMarkers();

                PrepareParticleSystems(instance, preset, false);
                LogPresentation(payload, preset, position, radius, duration, ResolvePeakSeconds(duration, preset.style), "prefab_additional", additionalPrefab.name);
                if (ShouldFollowEffect(preset))
                {
                    StartCoroutine(FollowEffect(instance, payload, preset, duration + 0.25f));
                }

                StartCoroutine(DestroyAfter(instance, duration + 0.25f));
            }
        }

        private static void PrepareParticleSystems(GameObject instance, CardVfxPreset preset, bool keepLooping)
        {
            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particles = particleSystems[i];
                var main = particles.main;
                if (!keepLooping)
                {
                    main.loop = false;
                }
                else
                {
                    main.loop = true;
                }

                if (preset.tintParticlesWithPresetColor)
                {
                    main.startColor = new ParticleSystem.MinMaxGradient(preset.color);
                }

                if (preset.useHierarchyParticleScale)
                {
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }

                if (ShouldApplyEffectSortingOrder(preset))
                {
                    var renderer = particles.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        renderer.sortingOrder = ResolveEffectSortingOrder(renderer.sortingOrder, preset);
                    }
                }

                particles.Clear(true);
                particles.Play(true);
            }

            if (ShouldApplyEffectSortingOrder(preset))
            {
                SpriteRenderer[] spriteRenderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    spriteRenderers[i].sortingOrder = ResolveEffectSortingOrder(spriteRenderers[i].sortingOrder, preset);
                }
            }
        }

        private static bool ShouldApplyEffectSortingOrder(CardVfxPreset preset)
        {
            return preset != null &&
                (preset.overrideSortingOrder || preset.eventType == VfxEventType.StatusApplied);
        }

        private static int ResolveEffectSortingOrder(int currentSortingOrder, CardVfxPreset preset)
        {
            if (preset != null && preset.overrideSortingOrder)
            {
                return preset.sortingOrder;
            }

            return Mathf.Max(currentSortingOrder, StatusVfxForegroundSortingOrder);
        }

        private static float ResolvePayloadScaleMultiplier(VfxEventPayload payload)
        {
            if (payload.EventType != VfxEventType.StatusApplied || !IsStackScaledStatusEffectKey(payload.EffectKey))
            {
                return 1f;
            }

            int stack = Mathf.Max(1, Mathf.RoundToInt(payload.MagnitudeHint));
            return 1f + ((stack - 1) * 0.6f);
        }

        private static bool IsStackScaledStatusEffectKey(string effectKey)
        {
            switch (effectKey)
            {
                case "hit_apply_poison":
                case "hit_apply_burn":
                case "hit_apply_frost":
                case "hit_apply_electric_stack":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsStatefulStatusVfx(VfxEventPayload payload)
        {
            return payload.EventType == VfxEventType.StatusApplied &&
                IsStackScaledStatusEffectKey(payload.EffectKey);
        }

        private static string GetStatusVfxKey(VfxEventPayload payload)
        {
            int targetSlot = payload.TargetPlayerSlot;
            if (targetSlot > 0)
            {
                return $"{targetSlot}:{payload.EffectKey}";
            }

            if (payload.SourceObjectId != 0)
            {
                return $"object:{payload.SourceObjectId}:{payload.EffectKey}";
            }

            return $"world:{payload.WorldPosition.X:F2}:{payload.WorldPosition.Y:F2}:{payload.EffectKey}";
        }

        private static float ResolveStatusVfxLoopCutoffSeconds(VfxEventPayload payload)
        {
            float cutoffSeconds;
            switch (payload.EffectKey)
            {
                case "hit_apply_electric_stack":
                    cutoffSeconds = 2.0f;
                    break;
                case "hit_apply_burn":
                case "hit_apply_poison":
                    cutoffSeconds = 2.0f;
                    break;
                case "hit_apply_frost":
                    cutoffSeconds = 1.0f;
                    break;
                default:
                    return 0f;
            }

            return ClampStatusVfxLoopCutoffSeconds(cutoffSeconds, payload.DurationHint);
        }

        private static float ClampStatusVfxLoopCutoffSeconds(float cutoffSeconds, float durationSeconds)
        {
            if (cutoffSeconds <= 0f)
            {
                return 0f;
            }

            if (durationSeconds <= 0f)
            {
                return cutoffSeconds;
            }

            return Mathf.Min(cutoffSeconds, durationSeconds);
        }

        private void ClearStatusVfxEffect(string key)
        {
            if (string.IsNullOrEmpty(key) || !_activeStatusEffects.TryGetValue(key, out GameObject instance))
            {
                return;
            }

            _activeStatusEffects.Remove(key);
            if (instance != null)
            {
                Destroy(instance);
            }
        }

        private void ClearStatusVfxEffects()
        {
            foreach (GameObject instance in _activeStatusEffects.Values)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }

            _activeStatusEffects.Clear();
        }

        private void ClearOverchargeReadyEffect(VfxEventPayload payload)
        {
            ClearOverchargeReadyEffect(GetOverchargeReadyKey(payload));
        }

        private void ClearOverchargeReadyEffect(string key)
        {
            if (!_overchargeReadyEffects.TryGetValue(key, out GameObject instance))
            {
                return;
            }

            _overchargeReadyEffects.Remove(key);
            if (instance != null)
            {
                Destroy(instance);
            }
        }

        private void ClearOverchargeReadyEffects()
        {
            foreach (GameObject instance in _overchargeReadyEffects.Values)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }

            _overchargeReadyEffects.Clear();
        }

        private static string GetOverchargeReadyKey(VfxEventPayload payload)
        {
            if (payload.SourceObjectId != 0)
            {
                return payload.SourceObjectId.ToString();
            }

            if (payload.SourcePlayerSlot > 0)
            {
                return $"player:{payload.SourcePlayerSlot}";
            }

            return "global";
        }

        private static bool ShouldFollowEffect(CardVfxPreset preset)
        {
            return preset != null && (preset.followSourcePlayer || preset.followTargetPlayer);
        }

        private IEnumerator FollowEffect(GameObject instance, VfxEventPayload payload, CardVfxPreset preset, float maxDuration)
        {
            float elapsed = 0f;
            while (instance != null && (maxDuration <= 0f || elapsed < maxDuration))
            {
                instance.transform.position = ResolveEffectPosition(payload, preset);
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        private void BlinkTargetPlayer(VfxEventPayload payload)
        {
            PlayerHealth target = FindPlayerHealthBySlot(payload.TargetPlayerSlot);
            if (target == null)
            {
                return;
            }

            SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            StartCoroutine(BlinkRenderers(renderers, HitBlinkDurationSeconds, HitBlinkIntervalSeconds));
        }

        private static PlayerHealth FindPlayerHealthBySlot(int playerSlot)
        {
            foreach (PlayerHealth health in PlayerHealth.ActiveInstances)
            {
                if (health == null || health.Object == null)
                {
                    continue;
                }

                if (health.Object.InputAuthority.PlayerId == playerSlot)
                {
                    return health;
                }
            }

            return null;
        }

        private static IEnumerator BlinkRenderers(SpriteRenderer[] renderers, float duration, float interval)
        {
            duration = Mathf.Max(0.05f, duration);
            interval = Mathf.Max(0.02f, interval);

            Color[] originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalColors[i] = renderers[i] != null ? renderers[i].color : Color.white;
            }

            float elapsed = 0f;
            bool flash = false;
            while (elapsed < duration)
            {
                flash = !flash;
                for (int i = 0; i < renderers.Length; i++)
                {
                    SpriteRenderer renderer = renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    Color current = originalColors[i];
                    Color target = flash ? Color.white : current;
                    target.a = current.a;
                    renderer.color = target;
                }

                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].color = originalColors[i];
                }
            }
        }

        private void SpawnWorldMarker(VfxEventPayload payload, CardVfxPreset preset)
        {
            if (payload.EventType == VfxEventType.SegmentBoundaryAdvanced)
            {
                SpawnSegmentAdvanceSweep(payload, preset);
                return;
            }

            var marker = new GameObject($"VfxMarker_{payload.EventType}");
            marker.transform.position = ResolveMarkerPosition(payload);

            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = GetCircleSprite();
            renderer.color = preset.color;
            renderer.sortingOrder = 20;

            float radius = ResolveMarkerRadius(payload, preset);
            marker.transform.localScale = Vector3.one * radius;
            _worldMarkers.Enqueue(marker);
            TrimWorldMarkers();

            float duration = ResolveDuration(payload, preset);
            float peakSeconds = ResolvePeakSeconds(duration, preset.style);
            LogPresentation(payload, preset, marker.transform.position, radius, duration, peakSeconds, "marker");
            StartCoroutine(AnimateMarker(marker, renderer, duration, peakSeconds, preset.style));
        }

        private static Quaternion ResolveMarkerRotation(VfxEventPayload payload, CardVfxPreset preset)
        {
            Vector2 direction = new Vector2(payload.Direction.X, payload.Direction.Y);
            float rotationOffset = preset != null ? preset.rotationOffsetDegrees : 0f;
            if (preset != null && preset.usePrefabRotation && preset.effectPrefab != null)
            {
                return preset.effectPrefab.transform.rotation *
                    Quaternion.AngleAxis(rotationOffset, Vector3.forward);
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Quaternion.AngleAxis(rotationOffset, Vector3.forward);
            }

            if (preset != null && preset.alignLocalForwardToDirection)
            {
                Vector3 direction3 = new Vector3(direction.x, direction.y, 0f).normalized;
                return Quaternion.FromToRotation(Vector3.forward, direction3) *
                    Quaternion.AngleAxis(rotationOffset, Vector3.forward);
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(angle + rotationOffset, Vector3.forward);
        }

        private void SpawnSegmentAdvanceSweep(VfxEventPayload payload, CardVfxPreset preset)
        {
            Vector3 position = ResolveMarkerPosition(payload);
            float radius = ResolveMarkerRadius(payload, preset);
            float duration = ResolveDuration(payload, preset);
            float peakSeconds = ResolvePeakSeconds(duration, VfxPlaceholderStyle.Ring);

            var marker = new GameObject("VfxMarker_SegmentBoundaryAdvanced");
            marker.transform.position = position;

            var glow = CreateSweepRenderer(marker.transform, "Glow", preset.color, 18, 0.24f, radius * 2.25f);
            glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, Mathf.Min(glow.color.a, 0.26f));

            var edge = CreateSweepRenderer(marker.transform, "Edge", preset.color, 22, 0.055f, radius * 2.4f);
            edge.color = new Color(edge.color.r, edge.color.g, edge.color.b, Mathf.Min(edge.color.a + 0.12f, 0.92f));

            _worldMarkers.Enqueue(marker);
            TrimWorldMarkers();

            LogPresentation(payload, preset, marker.transform.position, radius, duration, peakSeconds, "segment_sweep");
            StartCoroutine(AnimateSegmentAdvanceSweep(marker, glow, edge, duration, peakSeconds, radius));
        }

        private SpriteRenderer CreateSweepRenderer(
            Transform parent,
            string name,
            Color color,
            int sortingOrder,
            float widthScale,
            float height)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            obj.transform.localScale = new Vector3(widthScale, Mathf.Max(0.5f, height), 1f);
            return renderer;
        }

        private void SpawnProjectileReflectFlash(VfxEventPayload payload, CardVfxPreset preset)
        {
            Vector3 position = ResolveMarkerPosition(payload);
            float radius = ResolveMarkerRadius(payload, preset);
            float duration = ResolveDuration(payload, preset);
            float peakSeconds = ResolvePeakSeconds(duration, VfxPlaceholderStyle.Burst);
            Vector2 direction = ResolveDirection(payload, Vector2.up);
            Vector2 tangent = new Vector2(-direction.y, direction.x);

            var marker = new GameObject("VfxMarker_ProjectileReflect");
            marker.transform.position = position;

            SpriteRenderer shield = CreateImpactRenderer(marker.transform, "ReflectPlane", GetSquareSprite(), preset.color, 27);
            shield.transform.localScale = new Vector3(Mathf.Max(0.1f, radius * 0.1f), Mathf.Max(0.68f, radius * 1.05f), 1f);
            shield.transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(tangent.x, tangent.y, 0f));

            SpriteRenderer glint = CreateImpactRenderer(marker.transform, "ReflectGlint", GetCircleSprite(), preset.color, 28);
            glint.transform.localScale = Vector3.one * Mathf.Max(0.24f, radius * 0.3f);
            glint.color = WithAlpha(glint.color, Mathf.Min(glint.color.a, 0.72f));

            SpriteRenderer[] shards = new SpriteRenderer[3];
            Vector3[] startPositions = new Vector3[shards.Length];
            Vector3[] travelOffsets = new Vector3[shards.Length];
            Vector2[] shardDirs =
            {
                direction,
                (direction + tangent * 0.65f).normalized,
                (direction - tangent * 0.65f).normalized
            };

            for (int i = 0; i < shards.Length; i++)
            {
                shards[i] = CreateImpactRenderer(marker.transform, $"ReflectShard_{i + 1}", GetSquareSprite(), preset.color, 29);
                shards[i].transform.localScale = new Vector3(Mathf.Max(0.06f, radius * 0.07f), Mathf.Max(0.22f, radius * 0.32f), 1f);
                shards[i].transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(shardDirs[i].x, shardDirs[i].y, 0f));
                startPositions[i] = (Vector3)(tangent * radius * (i - 1) * 0.12f);
                travelOffsets[i] = (Vector3)(shardDirs[i] * radius * 0.48f);
                shards[i].transform.localPosition = startPositions[i];
            }

            _worldMarkers.Enqueue(marker);
            TrimWorldMarkers();

            LogPresentation(payload, preset, marker.transform.position, radius, duration, peakSeconds, "projectile_reflect_flash");
            StartCoroutine(AnimateProjectileReflectFlash(marker, shield, glint, shards, startPositions, travelOffsets, duration, peakSeconds));
        }

        private void SpawnProjectilePierceStreak(VfxEventPayload payload, CardVfxPreset preset)
        {
            Vector3 position = ResolveMarkerPosition(payload);
            float radius = ResolveMarkerRadius(payload, preset);
            float duration = ResolveDuration(payload, preset);
            float peakSeconds = ResolvePeakSeconds(duration, VfxPlaceholderStyle.Burst);
            Vector2 direction = ResolveDirection(payload, Vector2.up);
            Vector2 tangent = new Vector2(-direction.y, direction.x);

            var marker = new GameObject("VfxMarker_ProjectilePierce");
            marker.transform.position = position;

            SpriteRenderer core = CreateImpactRenderer(marker.transform, "PierceCore", GetSquareSprite(), preset.color, 29);
            core.transform.localScale = new Vector3(Mathf.Max(0.08f, radius * 0.09f), Mathf.Max(0.7f, radius * 1.08f), 1f);
            core.transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(direction.x, direction.y, 0f));

            SpriteRenderer[] trails = new SpriteRenderer[2];
            Vector3[] startPositions = new Vector3[trails.Length];
            Vector3[] travelOffsets = new Vector3[trails.Length];
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i] = CreateImpactRenderer(marker.transform, $"PierceTrail_{i + 1}", GetSquareSprite(), preset.color, 26 + i);
                trails[i].transform.localScale = new Vector3(Mathf.Max(0.04f, radius * 0.045f), Mathf.Max(0.46f, radius * 0.76f), 1f);
                trails[i].transform.rotation = core.transform.rotation;
                startPositions[i] = (Vector3)(tangent * radius * (i == 0 ? -0.12f : 0.12f) - direction * radius * 0.18f);
                travelOffsets[i] = (Vector3)(direction * radius * 0.45f);
                trails[i].transform.localPosition = startPositions[i];
                trails[i].color = WithAlpha(trails[i].color, Mathf.Min(trails[i].color.a, 0.5f));
            }

            _worldMarkers.Enqueue(marker);
            TrimWorldMarkers();

            LogPresentation(payload, preset, marker.transform.position, radius, duration, peakSeconds, "projectile_pierce_streak");
            StartCoroutine(AnimateProjectilePierceStreak(marker, core, trails, startPositions, travelOffsets, direction, radius, duration, peakSeconds));
        }

        private void SpawnProjectileImpactBurst(VfxEventPayload payload, CardVfxPreset preset)
        {
            Vector3 position = ResolveMarkerPosition(payload);
            float radius = ResolveMarkerRadius(payload, preset);
            float duration = ResolveDuration(payload, preset);
            float peakSeconds = ResolvePeakSeconds(duration, VfxPlaceholderStyle.Burst);
            Vector2 direction = ResolveDirection(payload, Vector2.up);
            Vector2 tangent = new Vector2(-direction.y, direction.x);

            var marker = new GameObject("VfxMarker_ProjectileImpact");
            marker.transform.position = position;

            SpriteRenderer ring = CreateImpactRenderer(marker.transform, "ShockRing", GetCircleSprite(), preset.color, 22);
            ring.transform.localScale = Vector3.one * Mathf.Max(0.45f, radius * 0.82f);
            ring.color = new Color(ring.color.r, ring.color.g, ring.color.b, Mathf.Min(ring.color.a, 0.34f));

            SpriteRenderer core = CreateImpactRenderer(marker.transform, "CoreFlash", GetCircleSprite(), preset.color, 26);
            core.transform.localScale = Vector3.one * Mathf.Max(0.28f, radius * 0.36f);

            SpriteRenderer[] sparks = new SpriteRenderer[3];
            Vector3[] startPositions = new Vector3[sparks.Length];
            Vector3[] travelOffsets = new Vector3[sparks.Length];
            Vector2[] sparkDirs =
            {
                direction,
                (direction + tangent * 0.55f).normalized,
                (direction - tangent * 0.55f).normalized
            };

            for (int i = 0; i < sparks.Length; i++)
            {
                sparks[i] = CreateImpactRenderer(marker.transform, $"Spark_{i + 1}", GetSquareSprite(), preset.color, 28);
                sparks[i].transform.localScale = new Vector3(Mathf.Max(0.08f, radius * 0.08f), Mathf.Max(0.28f, radius * 0.42f), 1f);
                sparks[i].transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(sparkDirs[i].x, sparkDirs[i].y, 0f));
                startPositions[i] = (Vector3)(sparkDirs[i] * radius * 0.12f);
                travelOffsets[i] = (Vector3)(sparkDirs[i] * radius * 0.48f);
                sparks[i].transform.localPosition = startPositions[i];
            }

            _worldMarkers.Enqueue(marker);
            TrimWorldMarkers();

            LogPresentation(payload, preset, marker.transform.position, radius, duration, peakSeconds, "projectile_impact_burst");
            StartCoroutine(AnimateProjectileImpactBurst(marker, core, ring, sparks, startPositions, travelOffsets, duration, peakSeconds));
        }

        private void SpawnProjectileModifierTrail(VfxEventPayload payload, CardVfxPreset preset)
        {
            Vector3 position = ResolveMarkerPosition(payload);
            float radius = Mathf.Max(0.48f, ResolveMarkerRadius(payload, preset));
            float duration = Mathf.Clamp(ResolveDuration(payload, preset), 0.32f, 0.9f);
            float peakSeconds = ResolvePeakSeconds(duration, VfxPlaceholderStyle.Burst);
            Vector2 direction = ResolveDirection(payload, Vector2.right);
            Vector2 tangent = new Vector2(-direction.y, direction.x);
            Color trailColor = ResolveProjectileModifierTrailColor(payload, preset);
            bool isStatusTrail = IsStatusProjectileEffectKey(payload.EffectKey);
            bool isHeavyTrail = payload.EffectKey == "projectile_heavy";
            bool isMultiShotTrail = payload.EffectKey == "projectile_multishot";
            bool isGravityTrail = payload.EffectKey == "projectile_gravity_zero";

            var marker = new GameObject("VfxMarker_ProjectileModifierTrail");
            marker.transform.position = position;

            SpriteRenderer glow = CreateImpactRenderer(marker.transform, "ModifierGlow", GetCircleSprite(), WithAlpha(trailColor, 0.24f), 21);
            float glowScale = isHeavyTrail ? 0.74f : (isStatusTrail ? 0.62f : (isGravityTrail ? 0.56f : 0.5f));
            glow.transform.localScale = Vector3.one * Mathf.Max(0.32f, radius * glowScale);

            SpriteRenderer core = CreateImpactRenderer(marker.transform, "ModifierCore", GetSquareSprite(), WithAlpha(trailColor, 0.9f), 29);
            float coreWidth = isHeavyTrail ? 0.13f : (isGravityTrail ? 0.045f : 0.08f);
            float coreLength = isHeavyTrail ? 0.64f : (isStatusTrail ? 1.05f : (isGravityTrail ? 1.24f : 0.86f));
            core.transform.localScale = new Vector3(Mathf.Max(0.08f, radius * coreWidth), Mathf.Max(0.54f, radius * coreLength), 1f);
            core.transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(direction.x, direction.y, 0f));

            int trailCount = isMultiShotTrail ? 5 : (isGravityTrail ? 2 : 3);
            SpriteRenderer[] trails = new SpriteRenderer[trailCount];
            Vector3[] trailStartPositions = new Vector3[trails.Length];
            Vector3[] trailTravelOffsets = new Vector3[trails.Length];
            for (int i = 0; i < trails.Length; i++)
            {
                float side = i - (trails.Length - 1f) * 0.5f;
                float trailWidth = isHeavyTrail ? 0.07f : (isGravityTrail ? 0.028f : 0.045f);
                float trailLengthBase = isHeavyTrail ? 0.46f : (isGravityTrail ? 1.08f : (isMultiShotTrail ? 0.52f : 0.62f));
                float sideSpacing = isMultiShotTrail ? 0.15f : (isGravityTrail ? 0.22f : 0.1f);
                trails[i] = CreateImpactRenderer(marker.transform, $"ModifierTrail_{i + 1}", GetSquareSprite(), trailColor, 24 + i);
                trails[i].transform.localScale = new Vector3(Mathf.Max(0.035f, radius * trailWidth), Mathf.Max(0.34f, radius * (trailLengthBase + i * 0.045f)), 1f);
                trails[i].transform.rotation = core.transform.rotation;
                trailStartPositions[i] = (Vector3)(-direction * radius * (0.18f + i * 0.045f) + tangent * radius * side * sideSpacing);
                trailTravelOffsets[i] = isGravityTrail
                    ? (Vector3)(direction * radius * 0.12f + tangent * radius * side * 0.08f)
                    : (Vector3)(-direction * radius * (isHeavyTrail ? 0.2f : 0.34f) + tangent * radius * side * 0.05f);
                trails[i].transform.localPosition = trailStartPositions[i];
                trails[i].color = WithAlpha(trails[i].color, Mathf.Min(trails[i].color.a, isHeavyTrail ? 0.58f : 0.48f));
            }

            int sparkCount = isStatusTrail ? 4 : (isMultiShotTrail ? 6 : (isHeavyTrail || isGravityTrail ? 3 : 2));
            SpriteRenderer[] sparks = new SpriteRenderer[sparkCount];
            Vector3[] sparkStartPositions = new Vector3[sparks.Length];
            Vector3[] sparkTravelOffsets = new Vector3[sparks.Length];
            for (int i = 0; i < sparks.Length; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float lane = i < 2 ? 0.16f : (isMultiShotTrail ? 0.34f : 0.28f);
                sparks[i] = CreateImpactRenderer(marker.transform, $"ModifierSpark_{i + 1}", GetCircleSprite(), WithAlpha(trailColor, 0.76f), 30 + i);
                float sparkScale = isHeavyTrail ? 0.16f : (isGravityTrail ? 0.1f : 0.13f);
                sparks[i].transform.localScale = Vector3.one * Mathf.Max(0.08f, radius * sparkScale);
                sparkStartPositions[i] = (Vector3)(-direction * radius * 0.02f + tangent * radius * side * lane);
                sparkTravelOffsets[i] = (Vector3)(direction * radius * (isGravityTrail ? 0.34f : 0.2f) + tangent * radius * side * (isMultiShotTrail ? 0.26f : 0.18f));
                sparks[i].transform.localPosition = sparkStartPositions[i];
            }

            _worldMarkers.Enqueue(marker);
            TrimWorldMarkers();

            LogPresentation(payload, preset, marker.transform.position, radius, duration, peakSeconds, "projectile_modifier_trail");
            StartCoroutine(AnimateProjectileModifierTrail(
                marker,
                glow,
                core,
                trails,
                trailStartPositions,
                trailTravelOffsets,
                sparks,
                sparkStartPositions,
                sparkTravelOffsets,
                direction,
                radius,
                duration,
                peakSeconds));
        }

        private void SpawnOverchargeReadyCharge(VfxEventPayload payload, CardVfxPreset preset)
        {
            Vector3 position = ResolveMarkerPosition(payload);
            float radius = ResolveMarkerRadius(payload, preset);
            float duration = ResolveDuration(payload, preset);
            float peakSeconds = ResolvePeakSeconds(duration, VfxPlaceholderStyle.Pulse);

            var marker = new GameObject("VfxMarker_OverchargeReady");
            marker.transform.position = position;

            SpriteRenderer aura = CreateImpactRenderer(marker.transform, "ChargeAura", GetCircleSprite(), preset.color, 24);
            aura.transform.localScale = Vector3.one * Mathf.Max(0.65f, radius * 0.72f);
            aura.color = WithAlpha(aura.color, Mathf.Min(aura.color.a, 0.46f));

            SpriteRenderer core = CreateImpactRenderer(marker.transform, "ChargeCore", GetCircleSprite(), preset.color, 28);
            core.transform.localScale = Vector3.one * Mathf.Max(0.28f, radius * 0.34f);

            SpriteRenderer[] motes = new SpriteRenderer[4];
            Vector3[] startPositions = new Vector3[motes.Length];
            float[] spinOffsets = { 0f, 90f, 180f, 270f };
            for (int i = 0; i < motes.Length; i++)
            {
                motes[i] = CreateImpactRenderer(marker.transform, $"ChargeMote_{i + 1}", GetCircleSprite(), preset.color, 29);
                motes[i].transform.localScale = Vector3.one * Mathf.Max(0.12f, radius * 0.13f);
                Vector2 offset = Quaternion.Euler(0f, 0f, spinOffsets[i]) * Vector2.up;
                startPositions[i] = (Vector3)(offset * radius * 0.48f);
                motes[i].transform.localPosition = startPositions[i];
            }

            _worldMarkers.Enqueue(marker);
            TrimWorldMarkers();

            LogPresentation(payload, preset, marker.transform.position, radius, duration, peakSeconds, "overcharge_ready_charge");
            StartCoroutine(AnimateOverchargeReadyCharge(marker, aura, core, motes, startPositions, radius, duration, peakSeconds));
        }

        private static SpriteRenderer CreateImpactRenderer(
            Transform parent,
            string name,
            Sprite sprite,
            Color color,
            int sortingOrder)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static Vector2 ResolveDirection(VfxEventPayload payload, Vector2 fallback)
        {
            Vector2 direction = new Vector2(payload.Direction.X, payload.Direction.Y);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = fallback;
            }

            return direction.normalized;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static bool IsStatusProjectileEffectKey(string effectKey)
        {
            return effectKey == "hit_apply_poison" ||
                effectKey == "hit_apply_burn" ||
                effectKey == "hit_apply_frost" ||
                effectKey == "hit_apply_electric_stack";
        }

        private static Color ResolveProjectileModifierTrailColor(VfxEventPayload payload, CardVfxPreset preset)
        {
            Color color = preset != null ? preset.color : ResolveColor(payload);
            if (payload.EffectKey == "projectile_speed_up")
            {
                return new Color(Mathf.Min(1f, color.r + 0.08f), Mathf.Min(1f, color.g + 0.12f), 1f, 0.86f);
            }

            if (payload.EffectKey == "projectile_size_up")
            {
                return new Color(1f, Mathf.Min(1f, color.g + 0.05f), Mathf.Max(0.12f, color.b * 0.75f), 0.86f);
            }

            if (payload.EffectKey == "projectile_heavy")
            {
                return new Color(0.95f, Mathf.Min(0.5f, color.g + 0.08f), Mathf.Max(0.08f, color.b * 0.45f), 0.9f);
            }

            if (payload.EffectKey == "projectile_multishot")
            {
                return new Color(0.36f, 1f, 0.72f, 0.86f);
            }

            if (payload.EffectKey == "projectile_gravity_zero")
            {
                return new Color(0.64f, 0.52f, 1f, 0.86f);
            }

            if (IsStatusProjectileEffectKey(payload.EffectKey))
            {
                return new Color(color.r, color.g, color.b, Mathf.Max(color.a, 0.88f));
            }

            return new Color(color.r, color.g, color.b, Mathf.Max(color.a, 0.78f));
        }

        private IEnumerator AnimateMarker(GameObject marker, SpriteRenderer renderer, float duration, float peakSeconds, VfxPlaceholderStyle style)
        {
            duration = Mathf.Max(0.1f, duration);
            peakSeconds = Mathf.Clamp(peakSeconds, 0.01f, duration);
            Vector3 startScale = marker.transform.localScale;
            float peakMultiplier = style == VfxPlaceholderStyle.Ring ? 1.12f : (style == VfxPlaceholderStyle.Burst ? 1.2f : 1.08f);
            Vector3 peakScale = startScale * peakMultiplier;
            Color startColor = renderer.color;

            float elapsed = 0f;
            while (elapsed < duration && marker != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scaleT = elapsed <= peakSeconds ? elapsed / peakSeconds : 1f;
                marker.transform.localScale = Vector3.Lerp(startScale, peakScale, scaleT);
                renderer.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
                yield return null;
            }

            if (marker != null)
            {
                Destroy(marker);
            }
        }

        private IEnumerator AnimateSegmentAdvanceSweep(
            GameObject marker,
            SpriteRenderer glow,
            SpriteRenderer edge,
            float duration,
            float peakSeconds,
            float radius)
        {
            duration = Mathf.Max(0.1f, duration);
            peakSeconds = Mathf.Clamp(peakSeconds, 0.01f, duration);

            Vector3 startScale = marker.transform.localScale;
            // Child renderers own sweep height; parent scale widens the line without thickening it.
            Vector3 peakScale = new Vector3(Mathf.Max(1.15f, radius * 0.28f), 1f, 1f);
            Color glowStart = glow.color;
            Color edgeStart = edge.color;

            float elapsed = 0f;
            while (elapsed < duration && marker != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scaleT = elapsed <= peakSeconds ? elapsed / peakSeconds : 1f;
                marker.transform.localScale = Vector3.Lerp(startScale, peakScale, Mathf.Clamp01(scaleT));

                float alpha = Mathf.Lerp(1f, 0f, t);
                if (glow != null)
                {
                    glow.color = new Color(glowStart.r, glowStart.g, glowStart.b, glowStart.a * alpha);
                }

                if (edge != null)
                {
                    edge.color = new Color(edgeStart.r, edgeStart.g, edgeStart.b, edgeStart.a * alpha);
                }

                yield return null;
            }

            if (marker != null)
            {
                Destroy(marker);
            }
        }

        private IEnumerator AnimateProjectileReflectFlash(
            GameObject marker,
            SpriteRenderer shield,
            SpriteRenderer glint,
            SpriteRenderer[] shards,
            Vector3[] shardStartPositions,
            Vector3[] shardTravelOffsets,
            float duration,
            float peakSeconds)
        {
            duration = Mathf.Max(0.12f, duration);
            peakSeconds = Mathf.Clamp(peakSeconds, 0.02f, duration);
            Vector3 shieldStartScale = shield.transform.localScale;
            Vector3 glintStartScale = glint.transform.localScale;
            Color shieldColor = shield.color;
            Color glintColor = glint.color;
            Color[] shardColors = new Color[shards.Length];
            Vector3[] shardStartScales = new Vector3[shards.Length];
            for (int i = 0; i < shards.Length; i++)
            {
                shardColors[i] = shards[i].color;
                shardStartScales[i] = shards[i].transform.localScale;
            }

            float elapsed = 0f;
            while (elapsed < duration && marker != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float peakT = Mathf.Clamp01(elapsed / peakSeconds);
                float fade = 1f - Mathf.SmoothStep(0.04f, 1f, t);

                shield.transform.localScale = Vector3.Lerp(shieldStartScale * 0.62f, shieldStartScale * 1.24f, peakT);
                shield.color = WithAlpha(shieldColor, shieldColor.a * fade);
                glint.transform.localScale = Vector3.Lerp(glintStartScale * 0.45f, glintStartScale * 1.55f, t);
                glint.color = WithAlpha(glintColor, glintColor.a * fade);

                for (int i = 0; i < shards.Length; i++)
                {
                    if (shards[i] == null)
                    {
                        continue;
                    }

                    shards[i].transform.localPosition = Vector3.Lerp(shardStartPositions[i], shardStartPositions[i] + shardTravelOffsets[i], t);
                    shards[i].transform.localScale = Vector3.Lerp(shardStartScales[i], shardStartScales[i] * 0.42f, t);
                    shards[i].color = WithAlpha(shardColors[i], shardColors[i].a * fade);
                }

                yield return null;
            }

            if (marker != null)
            {
                Destroy(marker);
            }
        }

        private IEnumerator AnimateProjectilePierceStreak(
            GameObject marker,
            SpriteRenderer core,
            SpriteRenderer[] trails,
            Vector3[] trailStartPositions,
            Vector3[] trailTravelOffsets,
            Vector2 direction,
            float radius,
            float duration,
            float peakSeconds)
        {
            duration = Mathf.Max(0.1f, duration);
            peakSeconds = Mathf.Clamp(peakSeconds, 0.02f, duration);
            Vector3 coreStartScale = core.transform.localScale;
            Color coreColor = core.color;
            Color[] trailColors = new Color[trails.Length];
            Vector3[] trailStartScales = new Vector3[trails.Length];
            for (int i = 0; i < trails.Length; i++)
            {
                trailColors[i] = trails[i].color;
                trailStartScales[i] = trails[i].transform.localScale;
            }

            Vector3 coreStart = (Vector3)(-direction * radius * 0.22f);
            Vector3 coreEnd = (Vector3)(direction * radius * 0.34f);
            core.transform.localPosition = coreStart;

            float elapsed = 0f;
            while (elapsed < duration && marker != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float peakT = Mathf.Clamp01(elapsed / peakSeconds);
                float fade = 1f - Mathf.SmoothStep(0.02f, 1f, t);

                core.transform.localPosition = Vector3.Lerp(coreStart, coreEnd, t);
                core.transform.localScale = Vector3.Lerp(coreStartScale * 0.75f, coreStartScale * 1.22f, peakT);
                core.color = WithAlpha(coreColor, coreColor.a * fade);

                for (int i = 0; i < trails.Length; i++)
                {
                    if (trails[i] == null)
                    {
                        continue;
                    }

                    trails[i].transform.localPosition = Vector3.Lerp(trailStartPositions[i], trailStartPositions[i] + trailTravelOffsets[i], t);
                    trails[i].transform.localScale = Vector3.Lerp(trailStartScales[i] * 1.08f, trailStartScales[i] * 0.36f, t);
                    trails[i].color = WithAlpha(trailColors[i], trailColors[i].a * fade);
                }

                yield return null;
            }

            if (marker != null)
            {
                Destroy(marker);
            }
        }

        private IEnumerator AnimateProjectileModifierTrail(
            GameObject marker,
            SpriteRenderer glow,
            SpriteRenderer core,
            SpriteRenderer[] trails,
            Vector3[] trailStartPositions,
            Vector3[] trailTravelOffsets,
            SpriteRenderer[] sparks,
            Vector3[] sparkStartPositions,
            Vector3[] sparkTravelOffsets,
            Vector2 direction,
            float radius,
            float duration,
            float peakSeconds)
        {
            duration = Mathf.Max(0.18f, duration);
            peakSeconds = Mathf.Clamp(peakSeconds, 0.02f, duration);
            Vector3 glowStartScale = glow.transform.localScale;
            Vector3 coreStartScale = core.transform.localScale;
            Color glowColor = glow.color;
            Color coreColor = core.color;
            Color[] trailColors = new Color[trails.Length];
            Vector3[] trailStartScales = new Vector3[trails.Length];
            for (int i = 0; i < trails.Length; i++)
            {
                trailColors[i] = trails[i].color;
                trailStartScales[i] = trails[i].transform.localScale;
            }

            Color[] sparkColors = new Color[sparks.Length];
            Vector3[] sparkStartScales = new Vector3[sparks.Length];
            for (int i = 0; i < sparks.Length; i++)
            {
                sparkColors[i] = sparks[i].color;
                sparkStartScales[i] = sparks[i].transform.localScale;
            }

            Vector3 coreStart = (Vector3)(-direction * radius * 0.16f);
            Vector3 coreEnd = (Vector3)(direction * radius * 0.24f);
            core.transform.localPosition = coreStart;

            float elapsed = 0f;
            while (elapsed < duration && marker != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float peakT = Mathf.Clamp01(elapsed / peakSeconds);
                float fade = 1f - Mathf.SmoothStep(0.04f, 1f, t);
                float shimmer = 1f + Mathf.Sin(t * Mathf.PI * 5f) * 0.05f;

                glow.transform.localScale = Vector3.Lerp(glowStartScale * 0.72f, glowStartScale * 1.36f, t) * shimmer;
                glow.color = WithAlpha(glowColor, glowColor.a * fade);
                core.transform.localPosition = Vector3.Lerp(coreStart, coreEnd, t);
                core.transform.localScale = Vector3.Lerp(coreStartScale * 0.78f, coreStartScale * 1.18f, peakT);
                core.color = WithAlpha(coreColor, coreColor.a * fade);

                for (int i = 0; i < trails.Length; i++)
                {
                    if (trails[i] == null)
                    {
                        continue;
                    }

                    trails[i].transform.localPosition = Vector3.Lerp(trailStartPositions[i], trailStartPositions[i] + trailTravelOffsets[i], t);
                    trails[i].transform.localScale = Vector3.Lerp(trailStartScales[i] * 1.08f, trailStartScales[i] * 0.24f, t);
                    trails[i].color = WithAlpha(trailColors[i], trailColors[i].a * fade);
                }

                for (int i = 0; i < sparks.Length; i++)
                {
                    if (sparks[i] == null)
                    {
                        continue;
                    }

                    sparks[i].transform.localPosition = Vector3.Lerp(sparkStartPositions[i], sparkStartPositions[i] + sparkTravelOffsets[i], t);
                    sparks[i].transform.localScale = Vector3.Lerp(sparkStartScales[i] * 0.82f, sparkStartScales[i] * 1.24f, Mathf.Max(peakT, t));
                    sparks[i].color = WithAlpha(sparkColors[i], sparkColors[i].a * fade);
                }

                yield return null;
            }

            if (marker != null)
            {
                Destroy(marker);
            }
        }

        private IEnumerator AnimateProjectileImpactBurst(
            GameObject marker,
            SpriteRenderer core,
            SpriteRenderer ring,
            SpriteRenderer[] sparks,
            Vector3[] sparkStartPositions,
            Vector3[] sparkTravelOffsets,
            float duration,
            float peakSeconds)
        {
            duration = Mathf.Max(0.12f, duration);
            peakSeconds = Mathf.Clamp(peakSeconds, 0.02f, duration);
            Vector3 coreStartScale = core.transform.localScale;
            Vector3 ringStartScale = ring.transform.localScale;
            Color coreColor = core.color;
            Color ringColor = ring.color;
            Color[] sparkColors = new Color[sparks.Length];
            Vector3[] sparkStartScales = new Vector3[sparks.Length];
            for (int i = 0; i < sparks.Length; i++)
            {
                sparkColors[i] = sparks[i].color;
                sparkStartScales[i] = sparks[i].transform.localScale;
            }

            float elapsed = 0f;
            while (elapsed < duration && marker != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float peakT = Mathf.Clamp01(elapsed / peakSeconds);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, t);

                core.transform.localScale = Vector3.Lerp(coreStartScale * 0.55f, coreStartScale * 1.18f, peakT);
                core.color = new Color(coreColor.r, coreColor.g, coreColor.b, coreColor.a * fade);

                ring.transform.localScale = Vector3.Lerp(ringStartScale * 0.62f, ringStartScale * 1.42f, t);
                ring.color = new Color(ringColor.r, ringColor.g, ringColor.b, ringColor.a * fade);

                for (int i = 0; i < sparks.Length; i++)
                {
                    if (sparks[i] == null)
                    {
                        continue;
                    }

                    sparks[i].transform.localPosition = Vector3.Lerp(sparkStartPositions[i], sparkStartPositions[i] + sparkTravelOffsets[i], t);
                    sparks[i].transform.localScale = Vector3.Lerp(sparkStartScales[i] * 1.15f, sparkStartScales[i] * 0.35f, t);
                    sparks[i].color = new Color(sparkColors[i].r, sparkColors[i].g, sparkColors[i].b, sparkColors[i].a * fade);
                }

                yield return null;
            }

            if (marker != null)
            {
                Destroy(marker);
            }
        }

        private IEnumerator AnimateOverchargeReadyCharge(
            GameObject marker,
            SpriteRenderer aura,
            SpriteRenderer core,
            SpriteRenderer[] motes,
            Vector3[] moteStartPositions,
            float radius,
            float duration,
            float peakSeconds)
        {
            duration = Mathf.Max(0.24f, duration);
            peakSeconds = Mathf.Clamp(peakSeconds, 0.04f, duration);
            Vector3 auraStartScale = aura.transform.localScale;
            Vector3 coreStartScale = core.transform.localScale;
            Color auraColor = aura.color;
            Color coreColor = core.color;
            Color[] moteColors = new Color[motes.Length];
            Vector3[] moteStartScales = new Vector3[motes.Length];
            for (int i = 0; i < motes.Length; i++)
            {
                moteColors[i] = motes[i].color;
                moteStartScales[i] = motes[i].transform.localScale;
            }

            float elapsed = 0f;
            while (elapsed < duration && marker != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float peakT = Mathf.Clamp01(elapsed / peakSeconds);
                float fade = 1f - Mathf.SmoothStep(0.18f, 1f, t);
                float pulse = 1f + Mathf.Sin(t * Mathf.PI * 2.5f) * 0.08f;

                aura.transform.localScale = Vector3.Lerp(auraStartScale * 0.72f, auraStartScale * 1.34f, t) * pulse;
                aura.color = WithAlpha(auraColor, auraColor.a * fade);
                core.transform.localScale = Vector3.Lerp(coreStartScale * 0.65f, coreStartScale * 1.24f, peakT);
                core.color = WithAlpha(coreColor, coreColor.a * fade);

                float angle = t * 190f;
                for (int i = 0; i < motes.Length; i++)
                {
                    if (motes[i] == null)
                    {
                        continue;
                    }

                    Vector3 start = moteStartPositions[i];
                    Quaternion spin = Quaternion.Euler(0f, 0f, angle + i * 8f);
                    Vector3 orbit = spin * start.normalized * Mathf.Lerp(radius * 0.32f, radius * 0.68f, t);
                    motes[i].transform.localPosition = orbit;
                    motes[i].transform.localScale = Vector3.Lerp(moteStartScales[i] * 0.86f, moteStartScales[i] * 1.18f, Mathf.Max(peakT, t));
                    motes[i].color = WithAlpha(moteColors[i], moteColors[i].a * fade);
                }

                yield return null;
            }

            if (marker != null)
            {
                Destroy(marker);
            }
        }

        private Vector3 ResolveEffectPosition(VfxEventPayload payload, CardVfxPreset preset)
        {
            Vector3 position;
            if (TryResolveFollowPosition(payload, preset, out Vector3 followPosition))
            {
                position = followPosition;
            }
            else
            {
                position = ResolveMarkerPosition(payload);
            }

            if (preset != null)
            {
                position += (Vector3)preset.followOffset;
                position.z += preset.followZOffset;
            }

            return position;
        }

        private bool TryResolveFollowPosition(VfxEventPayload payload, CardVfxPreset preset, out Vector3 followPosition)
        {
            followPosition = Vector3.zero;
            if (preset == null)
            {
                return false;
            }

            int playerSlot = 0;
            if (preset.followTargetPlayer && payload.TargetPlayerSlot > 0)
            {
                playerSlot = payload.TargetPlayerSlot;
            }
            else if (preset.followSourcePlayer && payload.SourcePlayerSlot > 0)
            {
                playerSlot = payload.SourcePlayerSlot;
            }

            if (playerSlot <= 0)
            {
                return false;
            }

            PlayerHealth player = FindPlayerHealthBySlot(playerSlot);
            if (player == null)
            {
                return false;
            }

            if (payload.AttachPoint == VfxAttachPoint.WeaponMuzzle &&
                TryResolvePlayerMuzzlePosition(player, out Vector3 muzzlePosition))
            {
                followPosition = muzzlePosition;
                return true;
            }

            followPosition = ApplyAttachPointOffset(player.transform.position, payload);
            return true;
        }

        private static bool TryResolvePlayerMuzzlePosition(PlayerHealth player, out Vector3 muzzlePosition)
        {
            muzzlePosition = Vector3.zero;
            if (player == null)
            {
                return false;
            }

            Gun gun = player.GetComponentInChildren<Gun>(true);
            if (gun == null)
            {
                return false;
            }

            muzzlePosition = gun.MuzzleWorldPosition;
            return true;
        }

        private static Vector3 ResolveMarkerPosition(VfxEventPayload payload)
        {
            return ApplyAttachPointOffset(new Vector3(payload.WorldPosition.X, payload.WorldPosition.Y, 0f), payload);
        }

        private static Vector3 ApplyAttachPointOffset(Vector3 position, VfxEventPayload payload)
        {
            switch (payload.AttachPoint)
            {
                case VfxAttachPoint.CharacterHead:
                    position.y += CharacterHeadAttachYOffset;
                    break;
                case VfxAttachPoint.CharacterBody:
                    position.y += 0.35f;
                    break;
                case VfxAttachPoint.CharacterFeet:
                    position.y += 0.05f;
                    break;
                case VfxAttachPoint.WeaponMuzzle:
                    Vector2 direction = new Vector2(payload.Direction.X, payload.Direction.Y);
                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        position += (Vector3)(direction.normalized * 0.1f);
                    }
                    break;
            }

            return position;
        }

        private static float ResolveMarkerRadius(VfxEventPayload payload, CardVfxPreset preset)
        {
            float profileRadius = preset != null ? preset.radiusScale : 1f;
            float payloadRadius = payload.RadiusHint;
            if (payloadRadius > 0f && ShouldHonorRadiusHint(payload))
            {
                return Mathf.Max(0.2f, payloadRadius);
            }

            if (payloadRadius > 0f)
            {
                return Mathf.Max(0.2f, Mathf.Max(profileRadius, payloadRadius));
            }

            return Mathf.Max(0.2f, profileRadius);
        }

        private static bool ShouldHonorRadiusHint(VfxEventPayload payload)
        {
            return payload.EventType == VfxEventType.ExplosionTriggered ||
                payload.EventType == VfxEventType.ProjectileReflected ||
                payload.EventType == VfxEventType.VictoryZoneReached ||
                payload.EventType == VfxEventType.SegmentBoundaryAdvanced ||
                payload.EventType == VfxEventType.PitFallDeath;
        }

        private static float ResolveDuration(VfxEventPayload payload, CardVfxPreset preset)
        {
            float presetDuration = preset != null ? preset.durationSeconds : 0.75f;
            if (payload.DurationHint > 0f)
            {
                return Mathf.Max(payload.DurationHint, presetDuration);
            }

            return presetDuration;
        }

        private static float ResolvePeakSeconds(float duration, VfxPlaceholderStyle style)
        {
            float normalizedPeak = style == VfxPlaceholderStyle.Burst ? 0.12f : 0.2f;
            return Mathf.Min(0.25f, Mathf.Max(0.05f, duration * normalizedPeak));
        }

        private void LogPresentation(
            VfxEventPayload payload,
            CardVfxPreset preset,
            Vector3 markerPosition,
            float radius,
            float duration,
            float peakSeconds,
            string channel,
            string prefabName = null)
        {
            string effectKey = string.IsNullOrEmpty(payload.EffectKey) ? "none" : payload.EffectKey;
            string label = preset != null && !string.IsNullOrEmpty(preset.toastLabel) ? preset.toastLabel : ResolveLabel(payload);
            string prefabInfo = string.IsNullOrEmpty(prefabName) ? string.Empty : $" prefab={prefabName}";
            Debug.Log(
                $"[Colosseum/VFX] ACTUAL_VFX_PRESENTED {payload.EventType}:{effectKey} " +
                $"channel={channel} label={label} " +
                $"position=({markerPosition.x:F2},{markerPosition.y:F2}) " +
                $"radius={radius:F2} duration={duration:F2} peak={peakSeconds:F2} " +
                $"attach={payload.AttachPoint} active={_worldMarkers.Count}{prefabInfo}");
        }

        private IEnumerator DestroyAfter(GameObject target, float duration)
        {
            duration = Mathf.Max(0.1f, duration);
            yield return new WaitForSeconds(duration);

            if (target != null)
            {
                Destroy(target);
            }
        }

        private IEnumerator StopStatusVfxLoopingAfter(GameObject target, VfxEventPayload payload)
        {
            float cutoffSeconds = ResolveStatusVfxLoopCutoffSeconds(payload);
            if (cutoffSeconds <= 0f)
            {
                yield break;
            }

            yield return new WaitForSeconds(cutoffSeconds);
            SetStatusVfxLooping(target, false);
        }

        private static void SetStatusVfxLooping(GameObject target, bool loop)
        {
            if (target == null)
            {
                return;
            }

            ParticleSystem[] particleSystems = target.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                var main = particleSystems[i].main;
                main.loop = loop;
            }
        }

        private IEnumerator DestroyStatusEffectAfter(string key, GameObject target, float duration)
        {
            duration = Mathf.Max(0.1f, duration);
            yield return new WaitForSeconds(duration);

            if (!string.IsNullOrEmpty(key) &&
                _activeStatusEffects.TryGetValue(key, out GameObject active) &&
                active == target)
            {
                _activeStatusEffects.Remove(key);
            }

            if (target != null)
            {
                Destroy(target);
            }
        }

        private IEnumerator FadeAndDestroy(GameObject toast, CanvasGroup group, float duration)
        {
            duration = Mathf.Max(0.1f, duration);
            float elapsed = 0f;
            while (elapsed < duration && toast != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                group.alpha = t < 0.75f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.75f) / 0.25f);
                yield return null;
            }

            if (toast != null)
            {
                Destroy(toast);
            }
        }

        private void EnsureToastCanvas()
        {
            if (_toastCanvas != null)
            {
                return;
            }

            var canvasObj = new GameObject("VfxToastCanvas");
            canvasObj.transform.SetParent(transform, false);
            _toastCanvas = canvasObj.AddComponent<Canvas>();
            _toastCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _toastCanvas.sortingOrder = 80;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObj.AddComponent<GraphicRaycaster>();
            _toastRoot = canvasObj.GetComponent<RectTransform>();
        }

        private Sprite GetCircleSprite()
        {
            if (_circleSprite != null)
            {
                return _circleSprite;
            }

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.45f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            _circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return _circleSprite;
        }

        private Sprite GetSquareSprite()
        {
            if (_squareSprite != null)
            {
                return _squareSprite;
            }

            const int size = 4;
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }

            texture.Apply();
            _squareSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return _squareSprite;
        }

        private void TrimDedupe()
        {
            int max = Mathf.Max(32, _maxDedupeKeys);
            while (_dedupeOrder.Count > max)
            {
                _dedupeKeys.Remove(_dedupeOrder.Dequeue());
            }
        }

        private void TrimWorldMarkers()
        {
            int max = Mathf.Max(4, _maxWorldMarkers);
            while (_worldMarkers.Count > 0 && _worldMarkers.Peek() == null)
            {
                _worldMarkers.Dequeue();
            }

            while (_worldMarkers.Count > max)
            {
                GameObject stale = _worldMarkers.Dequeue();
                if (stale != null)
                {
                    Destroy(stale);
                }
            }
        }
    }
}
