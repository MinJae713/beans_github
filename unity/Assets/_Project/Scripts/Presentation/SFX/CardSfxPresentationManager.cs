using System;
using System.Collections;
using System.Collections.Generic;
using Colosseum.Contracts;
using UnityEngine;

namespace Colosseum.Presentation.SFX
{
    public class CardSfxPresentationManager : MonoBehaviour
    {
        private const int DefaultDedupeLimit = 256;
        private const string DefaultProfileResourcePath = "Presentation/SfxFeedbackProfile";

        [SerializeField] private SfxFeedbackProfile _profile;
        [SerializeField] private int _maxDedupeKeys = DefaultDedupeLimit;
        [SerializeField] private bool _logPlayback = true;

        private readonly HashSet<string> _dedupeKeys = new HashSet<string>();
        private readonly Queue<string> _dedupeOrder = new Queue<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (FindObjectOfType<CardSfxPresentationManager>() != null)
            {
                return;
            }

            var obj = new GameObject("CardSfxPresentationManager");
            DontDestroyOnLoad(obj);
            obj.AddComponent<CardSfxPresentationManager>();
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
                $"[Colosseum/SFX] Presentation handler failed for {payload.EventType}/{payload.EffectKey}.",
                exception));
        }

        private void OnVfxEvent(VfxEventPayload payload)
        {
            EnsureProfileLoaded();

            if (payload.PredictionState == VfxPredictionState.Rejected)
            {
                _dedupeKeys.Remove(GetSfxDedupeKey(payload));
                return;
            }

            string key = GetSfxDedupeKey(payload);
            if (!_dedupeKeys.Add(key))
            {
                if (_logPlayback)
                {
                    Debug.Log($"[Colosseum/SFX] Skipped duplicate {Describe(payload, key)}");
                }

                return;
            }

            _dedupeOrder.Enqueue(key);
            TrimDedupe();

            if (_profile == null || !_profile.TryFindPreset(payload, out SfxEventPreset preset))
            {
                return;
            }

            Play(payload, preset);
        }

        private static string GetSfxDedupeKey(VfxEventPayload payload)
        {
            // VfxEventPayload.GetDedupeKey may collapse object/sequence cases; SFX keeps card/effect distinct.
            return $"{payload.GetDedupeKey()}:{payload.CardKey}:{payload.EffectKey}";
        }

        private void EnsureProfileLoaded()
        {
            if (_profile != null)
            {
                return;
            }

            _profile = Resources.Load<SfxFeedbackProfile>(DefaultProfileResourcePath);
        }

        private void Play(VfxEventPayload payload, SfxEventPreset preset)
        {
            if (preset.clip == null)
            {
                return;
            }

            if (_logPlayback)
            {
                Debug.Log($"[Colosseum/SFX] Play {Describe(payload, GetSfxDedupeKey(payload))} clip={preset.clip.name}");
            }

            Vector3 position = preset.playAtWorldPosition
                ? new Vector3(payload.WorldPosition.X, payload.WorldPosition.Y, 0f)
                : transform.position;

            var sourceObj = new GameObject($"SfxOneShot_{payload.EventType}");
            sourceObj.transform.position = position;

            var source = sourceObj.AddComponent<AudioSource>();
            source.clip = preset.clip;
            source.volume = preset.volume;
            source.pitch = UnityEngine.Random.Range(
                Mathf.Min(preset.pitchMin, preset.pitchMax),
                Mathf.Max(preset.pitchMin, preset.pitchMax));
            source.spatialBlend = preset.playAtWorldPosition ? preset.spatialBlend : 0f;
            source.playOnAwake = false;
            source.Play();

            float lifetime = preset.clip.length / Mathf.Max(0.1f, Mathf.Abs(source.pitch)) + 0.1f;
            StartCoroutine(DestroyAfter(sourceObj, lifetime));
        }

        private IEnumerator DestroyAfter(GameObject obj, float seconds)
        {
            yield return new WaitForSeconds(seconds);

            if (obj != null)
            {
                Destroy(obj);
            }
        }

        private static string Describe(VfxEventPayload payload, string key)
        {
            return $"event={payload.EventType} card={payload.CardKey} effect={payload.EffectKey} key={key}";
        }

        private void TrimDedupe()
        {
            int max = Mathf.Max(32, _maxDedupeKeys);
            while (_dedupeOrder.Count > max)
            {
                _dedupeKeys.Remove(_dedupeOrder.Dequeue());
            }
        }
    }
}
