#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace Colosseum.DevMode
{
    /// <summary>
    /// DevMode 전용 — UI 스프라이트 바인딩 어댑터.
    ///
    /// 명명 규칙: GameObject 이름이 "devmode-ui-{index}" 형식이어야 한다.
    ///   예) "devmode-ui-0", "devmode-ui-1"
    ///
    /// 동작:
    ///   OnEnable 시 DevModeController.OnDevModeReady를 구독한다.
    ///   IsReady late-join 분기: 이미 DevMode가 준비된 상태라면 즉시 Apply를 호출한다.
    ///   Apply 시 AssetPack.uiSprites[index]를 Image.sprite에 적용한다.
    ///   preserveAspect 등 기존 Image 설정은 그대로 보존된다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UIBindingAdapter : MonoBehaviour
    {
        private const string Prefix = "devmode-ui-";

        private Image _image;

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        private void OnEnable()
        {
            DevModeController.OnDevModeReady += Apply;

            // IsReady late-join 분기: 이미 DevMode가 준비된 상태라면 즉시 적용
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
                Debug.LogWarning($"[Colosseum] Dev Mode: UIBindingAdapter — AssetPack이 null입니다. ({gameObject.name})", this);
                return;
            }

            if (!DevModeAdapterMatcher.TryParseIndex(gameObject.name, Prefix, out int index))
            {
                Debug.LogWarning($"[Colosseum] Dev Mode: UIBindingAdapter — 명명 규칙 불일치. 이름: '{gameObject.name}'. 기대 형식: 'devmode-ui-{{index}}'", this);
                return;
            }

            if (pack.uiSprites == null || index >= pack.uiSprites.Count)
            {
                Debug.LogWarning($"[Colosseum] Dev Mode: UIBindingAdapter — uiSprites[{index}] 슬롯이 없습니다. (AssetPack.uiSprites.Count={pack.uiSprites?.Count ?? 0})", this);
                return;
            }

            var sprite = pack.uiSprites[index];
            if (sprite == null)
            {
                Debug.LogWarning($"[Colosseum] Dev Mode: UIBindingAdapter — uiSprites[{index}]가 null입니다.", this);
                return;
            }

            // preserveAspect 등 기존 Image 설정은 변경하지 않음
            _image.sprite = sprite;

            Debug.Log($"[Colosseum] Dev Mode: Sprite applied to {gameObject.name}");
        }
    }
}
#endif
