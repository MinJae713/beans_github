using System.Collections;
using System.Collections.Generic;
using Colosseum.Contracts;
using UnityEngine;

namespace Colosseum.Presentation.SFX
{
    public class PitFallSfxPresentationManager : MonoBehaviour
    {
        private const int DefaultDedupeLimit = 64;
        private const string PitFallDeathEffectKey = "pit_fall_death";
        private const float DeathClipSeconds = 0.38f;

        [SerializeField] private int _maxDedupeKeys = DefaultDedupeLimit;
        [SerializeField, Range(0f, 1f)] private float _deathVolume = 0.72f;
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 0.18f;

        private readonly HashSet<string> _dedupeKeys = new HashSet<string>();
        private readonly Queue<string> _dedupeOrder = new Queue<string>();
        private AudioClip _deathClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (FindObjectOfType<PitFallSfxPresentationManager>() != null)
            {
                return;
            }

            var obj = new GameObject("PitFallSfxPresentationManager");
            DontDestroyOnLoad(obj);
            obj.AddComponent<PitFallSfxPresentationManager>();
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
            if (payload.EventType != VfxEventType.PitFallDeath ||
                payload.EffectKey != PitFallDeathEffectKey)
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
            Play(payload, key);
        }

        private void Play(VfxEventPayload payload, string dedupeKey)
        {
            AudioClip clip = GetDeathClip();
            if (clip == null)
            {
                return;
            }

            Vector3 position = new Vector3(payload.WorldPosition.X, payload.WorldPosition.Y, 0f);
            var sourceObj = new GameObject("SfxOneShot_PitFallDeath");
            sourceObj.transform.position = position;

            var source = sourceObj.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = _deathVolume;
            source.pitch = ResolvePitch(payload);
            source.spatialBlend = _spatialBlend;
            source.playOnAwake = false;
            source.Play();

            Debug.Log(
                $"[Colosseum/SFX] ACTUAL_SFX_PRESENTED {payload.EventType}:{payload.EffectKey} " +
                $"channel=pit_fall_death position=({position.x:F2},{position.y:F2}) " +
                $"volume={source.volume:F2} pitch={source.pitch:F2} duration={clip.length:F2} dedupe={dedupeKey}");

            StartCoroutine(DestroyAfter(sourceObj, clip.length + 0.1f));
        }

        private static float ResolvePitch(VfxEventPayload payload)
        {
            int salt = Mathf.Abs(payload.SourceEventSequence % 5);
            return 0.92f + salt * 0.012f;
        }

        private AudioClip GetDeathClip()
        {
            return _deathClip != null ? _deathClip : (_deathClip = CreateDeathClip());
        }

        private static AudioClip CreateDeathClip()
        {
            int sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            int sampleCount = Mathf.Max(2, Mathf.RoundToInt(sampleRate * DeathClipSeconds));
            var samples = new float[sampleCount];
            int maxIndex = Mathf.Max(1, sampleCount - 1);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)maxIndex;
                float attack = Mathf.Clamp01(normalized / 0.08f);
                float decay = Mathf.Exp(-normalized * 5.8f);
                float drop = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 58f, normalized) * t) * decay;
                float grit = Mathf.Sin(2f * Mathf.PI * 420f * t + Mathf.Sin(18f * t)) * decay * 0.18f;
                samples[i] = (drop + grit) * attack * 0.54f;
            }

            var clip = AudioClip.Create("PitFallDropThud", sampleCount, 1, sampleRate, false);
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
