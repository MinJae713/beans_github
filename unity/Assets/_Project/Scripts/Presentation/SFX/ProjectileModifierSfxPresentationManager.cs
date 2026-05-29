using System.Collections;
using System.Collections.Generic;
using Colosseum.Contracts;
using UnityEngine;

namespace Colosseum.Presentation.SFX
{
    public class ProjectileModifierSfxPresentationManager : MonoBehaviour
    {
        private const int DefaultDedupeLimit = 160;
        private const float ModifierClipSeconds = 0.18f;
        private const float HeavyModifierClipSeconds = 0.24f;

        [SerializeField] private int _maxDedupeKeys = DefaultDedupeLimit;
        [SerializeField, Range(0f, 1f)] private float _modifierVolume = 0.62f;
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 0.18f;
        [SerializeField] private bool _logDuplicateSkips = true;

        private readonly HashSet<string> _dedupeKeys = new HashSet<string>();
        private readonly Queue<string> _dedupeOrder = new Queue<string>();
        private AudioClip _brightClip;
        private AudioClip _heavyClip;
        private AudioClip _sizeClip;
        private AudioClip _multiShotClip;
        private AudioClip _gravityClip;
        private AudioClip _statusClip;

        private readonly struct SfxCue
        {
            public readonly string Channel;
            public readonly float Volume;
            public readonly float PitchBase;
            public readonly float ClipSeconds;

            public SfxCue(string channel, float volume, float pitchBase, float clipSeconds)
            {
                Channel = channel;
                Volume = volume;
                PitchBase = pitchBase;
                ClipSeconds = clipSeconds;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (FindObjectOfType<ProjectileModifierSfxPresentationManager>() != null)
            {
                return;
            }

            var obj = new GameObject("ProjectileModifierSfxPresentationManager");
            DontDestroyOnLoad(obj);
            obj.AddComponent<ProjectileModifierSfxPresentationManager>();
        }

        private void OnEnable()
        {
            VfxEventBus.Published += OnVfxEvent;
        }

        private void OnDisable()
        {
            VfxEventBus.Published -= OnVfxEvent;
        }

        private void OnVfxEvent(VfxEventPayload payload)
        {
            if (!TryResolveCue(payload, out SfxCue cue))
            {
                return;
            }

            string key = payload.GetDedupeKey();
            if (payload.PredictionState == VfxPredictionState.Rejected)
            {
                _dedupeKeys.Remove(key);
                return;
            }

            if (!_dedupeKeys.Add(key))
            {
                if (_logDuplicateSkips)
                {
                    Debug.Log($"[Colosseum/SFX] Skipped duplicate ProjectileSpawnedWithModifier key={key}");
                }

                return;
            }

            _dedupeOrder.Enqueue(key);
            TrimDedupe();
            Play(payload, key, cue);
        }

        private bool TryResolveCue(VfxEventPayload payload, out SfxCue cue)
        {
            if (payload.EventType != VfxEventType.ProjectileSpawnedWithModifier)
            {
                cue = default;
                return false;
            }

            switch (payload.EffectKey)
            {
                case "projectile_size_up":
                    cue = new SfxCue("projectile_modifier_size", _modifierVolume, 0.94f, ModifierClipSeconds);
                    return true;
                case "projectile_heavy":
                case "projectile_explosion":
                    cue = new SfxCue("projectile_modifier_heavy", _modifierVolume, 0.86f, HeavyModifierClipSeconds);
                    return true;
                case "projectile_multishot":
                    cue = new SfxCue("projectile_modifier_multishot", _modifierVolume, 1.1f, ModifierClipSeconds);
                    return true;
                case "projectile_gravity_zero":
                    cue = new SfxCue("projectile_modifier_float", _modifierVolume, 1.02f, ModifierClipSeconds);
                    return true;
                case "hit_knockback_up":
                case "projectile_pierce":
                case "projectile_reflect":
                    cue = new SfxCue("projectile_modifier_heavy", _modifierVolume, 0.98f, ModifierClipSeconds);
                    return true;
                case "hit_apply_poison":
                case "hit_apply_burn":
                case "hit_apply_frost":
                case "hit_apply_electric_stack":
                    cue = new SfxCue("projectile_modifier_status", _modifierVolume, 1.04f, ModifierClipSeconds);
                    return true;
                default:
                    cue = new SfxCue("projectile_modifier_bright", _modifierVolume, 1.14f, ModifierClipSeconds);
                    return true;
            }
        }

        private void Play(VfxEventPayload payload, string dedupeKey, SfxCue cue)
        {
            AudioClip clip = GetClip(cue);
            if (clip == null)
            {
                return;
            }

            Vector3 position = new Vector3(payload.WorldPosition.X, payload.WorldPosition.Y, 0f);
            var sourceObj = new GameObject($"SfxOneShot_{payload.EventType}");
            sourceObj.transform.position = position;

            var source = sourceObj.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = ResolveVolume(payload, cue);
            source.pitch = ResolvePitch(payload, cue);
            source.spatialBlend = _spatialBlend;
            source.playOnAwake = false;
            source.Play();

            Debug.Log(
                $"[Colosseum/SFX] ACTUAL_SFX_PRESENTED {payload.EventType}:{payload.EffectKey} " +
                $"channel={cue.Channel} position=({position.x:F2},{position.y:F2}) " +
                $"volume={source.volume:F2} pitch={source.pitch:F2} duration={clip.length:F2} dedupe={dedupeKey}");

            StartCoroutine(DestroyAfter(sourceObj, clip.length + 0.1f));
        }

        private static float ResolveVolume(VfxEventPayload payload, SfxCue cue)
        {
            float magnitudeBoost = Mathf.Clamp01(payload.MagnitudeHint / 4f) * 0.08f;
            return Mathf.Clamp01(cue.Volume + magnitudeBoost);
        }

        private static float ResolvePitch(VfxEventPayload payload, SfxCue cue)
        {
            int salt = Mathf.Abs(payload.SourceEventSequence % 7);
            return cue.PitchBase + salt * 0.014f;
        }

        private AudioClip GetClip(SfxCue cue)
        {
            switch (cue.Channel)
            {
                case "projectile_modifier_heavy":
                    return _heavyClip != null ? _heavyClip : (_heavyClip = CreateHeavyModifierClip(cue.ClipSeconds));
                case "projectile_modifier_size":
                    return _sizeClip != null ? _sizeClip : (_sizeClip = CreateSizeModifierClip(cue.ClipSeconds));
                case "projectile_modifier_multishot":
                    return _multiShotClip != null ? _multiShotClip : (_multiShotClip = CreateMultiShotModifierClip(cue.ClipSeconds));
                case "projectile_modifier_float":
                    return _gravityClip != null ? _gravityClip : (_gravityClip = CreateFloatModifierClip(cue.ClipSeconds));
                case "projectile_modifier_status":
                    return _statusClip != null ? _statusClip : (_statusClip = CreateStatusModifierClip(cue.ClipSeconds));
                default:
                    return _brightClip != null ? _brightClip : (_brightClip = CreateBrightModifierClip(cue.ClipSeconds));
            }
        }

        private static AudioClip CreateBrightModifierClip(float clipSeconds)
        {
            int sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            int sampleCount = Mathf.Max(2, Mathf.RoundToInt(sampleRate * clipSeconds));
            var samples = new float[sampleCount];
            int maxIndex = Mathf.Max(1, sampleCount - 1);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)maxIndex;
                float attack = Mathf.Clamp01(normalized / 0.08f);
                float decay = Mathf.Exp(-normalized * 7.5f);
                float rise = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(620f, 1280f, normalized) * t) * decay;
                float sparkle = Mathf.Sin(2f * Mathf.PI * 1860f * t) * Mathf.Exp(-normalized * 11f) * 0.18f;
                samples[i] = (rise + sparkle) * attack * 0.4f;
            }

            var clip = AudioClip.Create("ProjectileModifierBright", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateSizeModifierClip(float clipSeconds)
        {
            int sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            int sampleCount = Mathf.Max(2, Mathf.RoundToInt(sampleRate * clipSeconds));
            var samples = new float[sampleCount];
            int maxIndex = Mathf.Max(1, sampleCount - 1);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)maxIndex;
                float attack = Mathf.Clamp01(normalized / 0.07f);
                float decay = Mathf.Exp(-normalized * 6.2f);
                float swell = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(260f, 410f, normalized) * t) * decay;
                float edge = Mathf.Sin(2f * Mathf.PI * 980f * t) * Mathf.Exp(-normalized * 8f) * 0.16f;
                samples[i] = (swell + edge) * attack * 0.45f;
            }

            var clip = AudioClip.Create("ProjectileModifierSize", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateHeavyModifierClip(float clipSeconds)
        {
            int sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            int sampleCount = Mathf.Max(2, Mathf.RoundToInt(sampleRate * clipSeconds));
            var samples = new float[sampleCount];
            int maxIndex = Mathf.Max(1, sampleCount - 1);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)maxIndex;
                float attack = Mathf.Clamp01(normalized / 0.06f);
                float decay = Mathf.Exp(-normalized * 6.8f);
                float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(170f, 74f, normalized) * t) * decay;
                float edge = Mathf.Sin(2f * Mathf.PI * 740f * t) * Mathf.Exp(-normalized * 9f) * 0.2f;
                samples[i] = (thump + edge) * attack * 0.5f;
            }

            var clip = AudioClip.Create("ProjectileModifierHeavy", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateMultiShotModifierClip(float clipSeconds)
        {
            int sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            int sampleCount = Mathf.Max(2, Mathf.RoundToInt(sampleRate * clipSeconds));
            var samples = new float[sampleCount];
            int maxIndex = Mathf.Max(1, sampleCount - 1);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)maxIndex;
                float attack = Mathf.Clamp01(normalized / 0.05f);
                float decay = Mathf.Exp(-normalized * 7.2f);
                float chirpA = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(720f, 1120f, normalized) * t);
                float chirpB = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(980f, 1480f, normalized) * t + 0.7f);
                float stagger = Mathf.Sin(2f * Mathf.PI * 18f * t) * 0.16f + 0.84f;
                samples[i] = (chirpA * 0.28f + chirpB * 0.2f) * attack * decay * stagger;
            }

            var clip = AudioClip.Create("ProjectileModifierMultiShot", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateFloatModifierClip(float clipSeconds)
        {
            int sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            int sampleCount = Mathf.Max(2, Mathf.RoundToInt(sampleRate * clipSeconds));
            var samples = new float[sampleCount];
            int maxIndex = Mathf.Max(1, sampleCount - 1);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)maxIndex;
                float attack = Mathf.Clamp01(normalized / 0.1f);
                float decay = Mathf.Exp(-normalized * 5.6f);
                float lift = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(480f, 860f, normalized) * t) * decay;
                float shimmer = Mathf.Sin(2f * Mathf.PI * 1580f * t) * Mathf.Exp(-normalized * 10f) * 0.18f;
                samples[i] = (lift + shimmer) * attack * 0.38f;
            }

            var clip = AudioClip.Create("ProjectileModifierFloat", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateStatusModifierClip(float clipSeconds)
        {
            int sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            int sampleCount = Mathf.Max(2, Mathf.RoundToInt(sampleRate * clipSeconds));
            var samples = new float[sampleCount];
            int maxIndex = Mathf.Max(1, sampleCount - 1);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)maxIndex;
                float attack = Mathf.Clamp01(normalized / 0.07f);
                float decay = Mathf.Exp(-normalized * 8.5f);
                float pulse = Mathf.Sin(2f * Mathf.PI * 520f * t) * decay;
                float shimmer = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(950f, 1480f, normalized) * t) * decay * 0.24f;
                samples[i] = (pulse + shimmer) * attack * 0.42f;
            }

            var clip = AudioClip.Create("ProjectileModifierStatus", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private IEnumerator DestroyAfter(GameObject obj, float seconds)
        {
            yield return new WaitForSeconds(seconds);

            if (obj != null)
            {
                Destroy(obj);
            }
        }

        private void TrimDedupe()
        {
            int max = Mathf.Max(16, _maxDedupeKeys);
            while (_dedupeOrder.Count > max)
            {
                _dedupeKeys.Remove(_dedupeOrder.Dequeue());
            }
        }
    }
}
