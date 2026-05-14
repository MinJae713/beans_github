using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Colosseum.Contracts;

namespace Colosseum.Presentation.VFX
{
    public class CardVfxPlaceholderManager : MonoBehaviour
    {
        private const int DefaultDedupeLimit = 256;
        private const int DefaultMaxWorldMarkers = 32;
        private const string DefaultProfileResourcePath = "Presentation/VfxFeedbackProfile";

        [SerializeField] private VfxFeedbackProfile _profile;
        [SerializeField] private int _maxDedupeKeys = DefaultDedupeLimit;
        [SerializeField] private int _maxWorldMarkers = DefaultMaxWorldMarkers;
        [SerializeField] private float _toastDurationSeconds = 1.25f;
        [SerializeField] private Vector2 _toastSize = new Vector2(360f, 56f);

        private readonly HashSet<string> _dedupeKeys = new HashSet<string>();
        private readonly Queue<string> _dedupeOrder = new Queue<string>();
        private readonly Queue<GameObject> _worldMarkers = new Queue<GameObject>();
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
        }

        private void OnDisable()
        {
            VfxEventBus.Published -= OnVfxEvent;
            VfxEventBus.HandlerFailed -= OnVfxHandlerFailed;
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

            string key = payload.GetDedupeKey();
            if (!_dedupeKeys.Add(key))
            {
                return;
            }

            _dedupeOrder.Enqueue(key);
            TrimDedupe();

            CardVfxPreset preset = ResolvePreset(payload);
            if (ShouldShowToast(payload, preset))
            {
                ShowToast(payload, preset);
            }

            if (ShouldSpawnWorldMarker(payload, preset))
            {
                SpawnWorldEffect(payload, preset);
            }
        }

        private static bool IsSfxOnlyEvent(VfxEventType eventType)
        {
            return eventType == VfxEventType.ProjectileShot ||
                eventType == VfxEventType.PlayerRespawned;
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
            return preset.style != VfxPlaceholderStyle.ToastOnly && !IsUiOnlyAttachPoint(payload.AttachPoint);
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
            if (preset.effectPrefab != null)
            {
                SpawnPrefabEffect(payload, preset);
                return;
            }

            SpawnWorldMarker(payload, preset);
        }

        private void SpawnPrefabEffect(VfxEventPayload payload, CardVfxPreset preset)
        {
            Vector3 position = ResolveMarkerPosition(payload);
            Quaternion rotation = ResolveMarkerRotation(payload);
            var instance = Instantiate(preset.effectPrefab, position, rotation);

            float radius = ResolveMarkerRadius(payload, preset);
            float scale = Mathf.Max(0.05f, preset.prefabScale) * radius;
            instance.transform.localScale = instance.transform.localScale * scale;
            _worldMarkers.Enqueue(instance);
            TrimWorldMarkers();

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particles = particleSystems[i];
                var main = particles.main;
                main.loop = false;
                particles.Clear(true);
                particles.Play(true);
            }

            float duration = ResolveDuration(payload, preset);
            float peakSeconds = ResolvePeakSeconds(duration, preset.style);
            LogPresentation(payload, preset, position, radius, duration, peakSeconds, "prefab", preset.effectPrefab.name);
            StartCoroutine(DestroyAfter(instance, duration + 0.25f));
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

        private static Quaternion ResolveMarkerRotation(VfxEventPayload payload)
        {
            Vector2 direction = new Vector2(payload.Direction.X, payload.Direction.Y);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Quaternion.identity;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(angle, Vector3.forward);
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

        private static Vector3 ResolveMarkerPosition(VfxEventPayload payload)
        {
            Vector3 position = new Vector3(payload.WorldPosition.X, payload.WorldPosition.Y, 0f);
            switch (payload.AttachPoint)
            {
                case VfxAttachPoint.CharacterHead:
                    position.y += 0.45f;
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
