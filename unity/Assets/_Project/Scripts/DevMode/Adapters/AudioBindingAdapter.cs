#if UNITY_EDITOR
using UnityEngine;

namespace Colosseum.DevMode
{
    /// <summary>
    /// DevMode 전용 — 오디오 클립 바인딩 어댑터.
    ///
    /// 명명 규칙: GameObject 이름이 "devmode-audio-{index}" 형식이어야 한다.
    ///   예) "devmode-audio-0", "devmode-audio-1"
    ///
    /// 동작:
    ///   Start 시 DevModeController.OnDevModeReady를 구독한다. (Adapter 5 Start 패턴)
    ///   IsReady late-join 분기: 이미 DevMode가 준비된 상태라면 즉시 Apply를 호출한다.
    ///   Apply 시 AssetPack.audioClips[index]를 AudioSource.clip에 적용한다.
    ///   spatialBlend 등 기존 AudioSource 설정은 그대로 보존된다.
    ///
    /// 주의: Start 패턴이므로 Scene 배치 시 OnDevModeReady보다 Start가 늦게 호출될 수 있다.
    ///   IsReady late-join 분기가 이를 보완한다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioBindingAdapter : MonoBehaviour
    {
        private const string Prefix = "devmode-audio-";

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            DevModeController.OnDevModeReady += Apply;

            // IsReady late-join 분기: Start가 OnDevModeReady 이후에 호출된 경우 대비
            if (DevModeController.Instance?.IsReady == true)
                Apply(DevModeController.Instance.AssetPack);
        }

        private void OnDisable()
        {
            DevModeController.OnDevModeReady -= Apply;
        }

        private void Apply(DevModeAssetPack pack)
        {
            if (pack == null)
            {
                Debug.LogWarning($"[Colosseum] Dev Mode: AudioBindingAdapter — AssetPack이 null입니다. ({gameObject.name})", this);
                return;
            }

            if (!DevModeAdapterMatcher.TryParseIndex(gameObject.name, Prefix, out int index))
            {
                Debug.LogWarning($"[Colosseum] Dev Mode: AudioBindingAdapter — 명명 규칙 불일치. 이름: '{gameObject.name}'. 기대 형식: 'devmode-audio-{{index}}'", this);
                return;
            }

            if (pack.audioClips == null || index >= pack.audioClips.Count)
            {
                Debug.LogWarning($"[Colosseum] Dev Mode: AudioBindingAdapter — audioClips[{index}] 슬롯이 없습니다. (AssetPack.audioClips.Count={pack.audioClips?.Count ?? 0})", this);
                return;
            }

            var clip = pack.audioClips[index];
            if (clip == null)
            {
                Debug.LogWarning($"[Colosseum] Dev Mode: AudioBindingAdapter — audioClips[{index}]가 null입니다.", this);
                return;
            }

            // spatialBlend, volume, pitch 등 기존 AudioSource 설정은 변경하지 않음
            _audioSource.clip = clip;

            Debug.Log($"[Colosseum] Dev Mode: Clip applied to {gameObject.name}");
        }
    }
}
#endif
