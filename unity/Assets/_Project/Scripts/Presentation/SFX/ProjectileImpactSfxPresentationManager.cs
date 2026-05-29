using System.Collections;
using System.Collections.Generic;
using Colosseum.Contracts;
using UnityEngine;

namespace Colosseum.Presentation.SFX
{
    public class ProjectileImpactSfxPresentationManager : MonoBehaviour
    {
        private const int DefaultDedupeLimit = 128;
        private const float DefaultClipSeconds = 0.18f;

        [SerializeField] private int _maxDedupeKeys = DefaultDedupeLimit;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.58f;
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 0.55f;

        private readonly HashSet<string> _dedupeKeys = new HashSet<string>();
        private readonly Queue<string> _dedupeOrder = new Queue<string>();
        private AudioClip _impactClip;
        private AudioClip _reflectClip;
        private AudioClip _pierceClip;
        private AudioClip _overchargeReadyClip;

        private readonly struct SfxCue
        {
            public readonly string Channel;
            public readonly float VolumeOffset;
            public readonly float PitchBase;
            public readonly float ClipSeconds;

            public SfxCue(string channel, float volumeOffset, float pitchBase, float clipSeconds)
            {
                Channel = channel;
                VolumeOffset = volumeOffset;
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

            if (FindObjectOfType<ProjectileImpactSfxPresentationManager>() != null)
            {
                return;
            }

            var obj = new GameObject("ProjectileImpactSfxPresentationManager");
            DontDestroyOnLoad(obj);
            obj.AddComponent<ProjectileImpactSfxPresentationManager>();
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

            if (payload.PredictionState == VfxPredictionState.Rejected)
            {
                _dedupeKeys.Remove(payload.GetDedupeKey());
                return;
            }

            string key = payload.GetDedupeKey();
            if (!_dedupeKeys.Add(key))
            {
                return;
            }

            _dedupeOrder.Enqueue(key);
            TrimDedupe();
            Play(payload, key, cue);
        }

        private static bool TryResolveCue(VfxEventPayload payload, out SfxCue cue)
        {
            if (payload.EventType == VfxEventType.ProjectileImpact &&
                payload.EffectKey == "projectile_impact")
            {
                cue = new SfxCue("projectile_impact", 0f, 0.96f, DefaultClipSeconds);
                return true;
            }

            if (payload.EventType == VfxEventType.ProjectileReflected &&
                payload.EffectKey == "projectile_reflect")
            {
                cue = new SfxCue("projectile_reflect", -0.03f, 1.12f, 0.16f);
                return true;
            }

            if (payload.EventType == VfxEventType.ProjectilePierced &&
                payload.EffectKey == "projectile_pierce")
            {
                cue = new SfxCue("projectile_pierce", -0.01f, 1.04f, 0.15f);
                return true;
            }

            if (payload.EventType == VfxEventType.OverchargeReady &&
                payload.EffectKey == "reload_overcharge")
            {
                cue = new SfxCue("overcharge_ready", 0.04f, 0.9f, 0.42f);
                return true;
            }

            cue = default;
            return false;
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
                $"[Colosseum/SFX] Play event={payload.EventType} card={payload.CardKey} " +
                $"effect={payload.EffectKey} clip={clip.name} channel={cue.Channel}");
            Debug.Log(
                $"[Colosseum/SFX] ACTUAL_SFX_PRESENTED {payload.EventType}:{payload.EffectKey} " +
                $"channel={cue.Channel} position=({position.x:F2},{position.y:F2}) " +
                $"volume={source.volume:F2} pitch={source.pitch:F2} duration={clip.length:F2} dedupe={dedupeKey}");

            StartCoroutine(DestroyAfter(sourceObj, clip.length + 0.1f));
        }

        private float ResolveVolume(VfxEventPayload payload, SfxCue cue)
        {
            float magnitudeBoost = Mathf.Clamp01(payload.MagnitudeHint / 30f) * 0.12f;
            return Mathf.Clamp01(_volume + cue.VolumeOffset + magnitudeBoost);
        }

        private static float ResolvePitch(VfxEventPayload payload, SfxCue cue)
        {
            int salt = Mathf.Abs(payload.SourceEventSequence % 7);
            float magnitudeBoost = Mathf.Clamp(payload.MagnitudeHint / 120f, 0f, 0.08f);
            return cue.PitchBase + salt * 0.015f + magnitudeBoost;
        }

        private AudioClip GetClip(SfxCue cue)
        {
            switch (cue.Channel)
            {
                case "projectile_reflect":
                    return _reflectClip != null ? _reflectClip : (_reflectClip = CreateReflectClip(cue.ClipSeconds));
                case "projectile_pierce":
                    return _pierceClip != null ? _pierceClip : (_pierceClip = CreatePierceClip(cue.ClipSeconds));
                case "overcharge_ready":
                    return _overchargeReadyClip != null ? _overchargeReadyClip : (_overchargeReadyClip = CreateOverchargeReadyClip(cue.ClipSeconds));
                default:
                    return _impactClip != null ? _impactClip : (_impactClip = CreateImpactClip(cue.ClipSeconds));
            }
        }

        private static AudioClip CreateImpactClip(float clipSeconds)
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
                float decay = Mathf.Exp(-normalized * 9.5f);
                float click = Mathf.Sin(2f * Mathf.PI * 880f * t) * decay;
                float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(210f, 90f, normalized) * t) * decay * 0.55f;
                float grit = Mathf.Sin(2f * Mathf.PI * 1390f * t + Mathf.Sin(37f * t)) * decay * 0.12f;
                samples[i] = (click + body + grit) * attack * 0.52f;
            }

            var clip = AudioClip.Create("ProjectileImpactHit", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateReflectClip(float clipSeconds)
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
                float decay = Mathf.Exp(-normalized * 8.2f);
                float chime = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1180f, 760f, normalized) * t) * decay;
                float shimmer = Mathf.Sin(2f * Mathf.PI * 1780f * t) * Mathf.Exp(-normalized * 12f) * 0.22f;
                samples[i] = (chime + shimmer) * attack * 0.42f;
            }

            var clip = AudioClip.Create("ProjectileReflectPing", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreatePierceClip(float clipSeconds)
        {
            int sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            int sampleCount = Mathf.Max(2, Mathf.RoundToInt(sampleRate * clipSeconds));
            var samples = new float[sampleCount];
            int maxIndex = Mathf.Max(1, sampleCount - 1);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)maxIndex;
                float attack = Mathf.Clamp01(normalized / 0.035f);
                float decay = Mathf.Exp(-normalized * 10.5f);
                float slice = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1450f, 1030f, normalized) * t) * decay;
                float body = Mathf.Sin(2f * Mathf.PI * 310f * t) * Mathf.Exp(-normalized * 7.5f) * 0.28f;
                samples[i] = (slice + body) * attack * 0.46f;
            }

            var clip = AudioClip.Create("ProjectilePierceSlice", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateOverchargeReadyClip(float clipSeconds)
        {
            int sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            int sampleCount = Mathf.Max(2, Mathf.RoundToInt(sampleRate * clipSeconds));
            var samples = new float[sampleCount];
            int maxIndex = Mathf.Max(1, sampleCount - 1);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)maxIndex;
                float attack = Mathf.Clamp01(normalized / 0.12f);
                float sustain = 1f - Mathf.SmoothStep(0.55f, 1f, normalized);
                float sweepFrequency = Mathf.Lerp(240f, 720f, normalized);
                float sweep = Mathf.Sin(2f * Mathf.PI * sweepFrequency * t) * sustain;
                float sparkle = Mathf.Sin(2f * Mathf.PI * 1440f * t) * Mathf.Exp(-normalized * 4.5f) * 0.12f;
                samples[i] = (sweep * 0.5f + sparkle) * attack * 0.48f;
            }

            var clip = AudioClip.Create("OverchargeReadyCharge", sampleCount, 1, sampleRate, false);
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
