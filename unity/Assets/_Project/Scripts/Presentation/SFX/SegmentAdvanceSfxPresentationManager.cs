using System.Collections;
using System.Collections.Generic;
using Colosseum.Contracts;
using UnityEngine;

namespace Colosseum.Presentation.SFX
{
    public class SegmentAdvanceSfxPresentationManager : MonoBehaviour
    {
        private const int DefaultDedupeLimit = 64;
        private const string SegmentAdvanceEffectKey = "segment_advance";
        private const float SegmentAdvanceVolume = 0.62f;
        private const float SegmentAdvanceClipSeconds = 0.46f;

        [SerializeField] private int _maxDedupeKeys = DefaultDedupeLimit;
        [SerializeField, Range(0f, 1f)] private float _volume = SegmentAdvanceVolume;
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 0.25f;

        private readonly HashSet<string> _dedupeKeys = new HashSet<string>();
        private readonly Queue<string> _dedupeOrder = new Queue<string>();
        private AudioClip _segmentAdvanceClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (FindObjectOfType<SegmentAdvanceSfxPresentationManager>() != null)
            {
                return;
            }

            var obj = new GameObject("SegmentAdvanceSfxPresentationManager");
            DontDestroyOnLoad(obj);
            obj.AddComponent<SegmentAdvanceSfxPresentationManager>();
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
            if (payload.EventType != VfxEventType.SegmentBoundaryAdvanced ||
                payload.EffectKey != SegmentAdvanceEffectKey)
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
            AudioClip clip = GetSegmentAdvanceClip();
            if (clip == null)
            {
                return;
            }

            Vector3 position = new Vector3(payload.WorldPosition.X, payload.WorldPosition.Y, 0f);
            var sourceObj = new GameObject("SfxOneShot_SegmentBoundaryAdvanced");
            sourceObj.transform.position = position;

            var source = sourceObj.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = _volume;
            source.pitch = 1f;
            source.spatialBlend = _spatialBlend;
            source.playOnAwake = false;
            source.Play();

            Debug.Log(
                $"[Colosseum/SFX] ACTUAL_SFX_PRESENTED {payload.EventType}:{payload.EffectKey} " +
                $"channel=segment_sweep position=({position.x:F2},{position.y:F2}) " +
                $"volume={_volume:F2} duration={clip.length:F2} dedupe={dedupeKey}");

            StartCoroutine(DestroyAfter(sourceObj, clip.length + 0.1f));
        }

        private AudioClip GetSegmentAdvanceClip()
        {
            if (_segmentAdvanceClip != null)
            {
                return _segmentAdvanceClip;
            }

            int sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * SegmentAdvanceClipSeconds));
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)(sampleCount - 1);
                float envelope = Mathf.Sin(Mathf.PI * normalized);
                float frequency = Mathf.Lerp(220f, 520f, normalized);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.42f;
            }

            _segmentAdvanceClip = AudioClip.Create("SegmentAdvanceSweep", sampleCount, 1, sampleRate, false);
            _segmentAdvanceClip.SetData(samples, 0);
            return _segmentAdvanceClip;
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
