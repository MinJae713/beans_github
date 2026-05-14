#if UNITY_EDITOR
using UnityEngine;

namespace Colosseum.DevMode
{
    /// <summary>
    /// DevMode 전용 — 머터리얼 override 어댑터.
    ///
    /// 명명 규칙: GameObject 이름이 "devmode-material-{index}" 형식이어야 한다.
    ///   예) "devmode-material-0", "devmode-material-1"
    ///
    /// 동작:
    ///   OnEnable 시 DevModeController.OnDevModeReady를 구독한다.
    ///   IsReady late-join 분기: 이미 DevMode가 준비된 상태라면 즉시 Apply를 호출한다.
    ///   Apply 시 AssetPack.materials[index]를 Renderer.material(인스턴스)에 적용한다.
    ///   OnDisable 시 생성된 머터리얼 인스턴스를 Destroy하고 sharedMaterial을 원복한다.
    ///
    /// R10 mitigation:
    ///   sharedMaterial 직접 변경 절대 금지. Renderer.material(Unity 자동 인스턴스화) 또는
    ///   MaterialPropertyBlock을 사용한다. OnDisable에서 인스턴스 Destroy + 원복 필수.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class MaterialOverrideAdapter : MonoBehaviour
    {
        private const string Prefix = "devmode-material-";

        private Renderer _renderer;
        private Material _originalSharedMaterial;
        private Material _instancedMaterial;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            // 원본 sharedMaterial 캐시 (OnDisable 복원용)
            _originalSharedMaterial = _renderer.sharedMaterial;
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

            // 인스턴스화된 머터리얼 정리 (R10 mitigation)
            if (_instancedMaterial != null)
            {
                DestroyImmediate(_instancedMaterial);
                _instancedMaterial = null;
            }

            // sharedMaterial 원복
            if (_renderer != null && _originalSharedMaterial != null)
                _renderer.sharedMaterial = _originalSharedMaterial;
        }

        private void Apply(DevModeAssetPack pack)
        {
            if (pack == null)
            {
                Debug.LogWarning($"[Colosseum] Dev Mode: MaterialOverrideAdapter — AssetPack이 null입니다. ({gameObject.name})", this);
                return;
            }

            if (!DevModeAdapterMatcher.TryParseIndex(gameObject.name, Prefix, out int index))
            {
                Debug.LogWarning($"[Colosseum] Dev Mode: MaterialOverrideAdapter — 명명 규칙 불일치. 이름: '{gameObject.name}'. 기대 형식: 'devmode-material-{{index}}'", this);
                return;
            }

            if (pack.materials == null || index >= pack.materials.Count)
            {
                Debug.LogWarning($"[Colosseum] Dev Mode: MaterialOverrideAdapter — materials[{index}] 슬롯이 없습니다. (AssetPack.materials.Count={pack.materials?.Count ?? 0})", this);
                return;
            }

            var sourceMaterial = pack.materials[index];
            if (sourceMaterial == null)
            {
                Debug.LogWarning($"[Colosseum] Dev Mode: MaterialOverrideAdapter — materials[{index}]가 null입니다.", this);
                return;
            }

            // 이전 인스턴스 정리
            if (_instancedMaterial != null)
            {
                DestroyImmediate(_instancedMaterial);
                _instancedMaterial = null;
            }

            // Renderer.material은 자동으로 인스턴스를 생성한다 (sharedMaterial 변경 금지).
            // 명시적으로 new Material을 만들어 할당하면 Destroy 책임이 명확해진다.
            _instancedMaterial = new Material(sourceMaterial);
            _renderer.material = _instancedMaterial;

            Debug.Log($"[Colosseum] Dev Mode: Material applied to {gameObject.name}");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 인스펙터에서 명명 규칙 오타 조기 검출
            if (!string.IsNullOrEmpty(gameObject.name) &&
                !DevModeAdapterMatcher.TryParseIndex(gameObject.name, Prefix, out _))
            {
                Debug.LogWarning(
                    $"[Colosseum] Dev Mode: MaterialOverrideAdapter OnValidate — '{gameObject.name}'이 'devmode-material-{{숫자}}' 형식이 아닙니다. 명명 규칙을 확인하세요.",
                    this);
            }
        }
#endif
    }
}
#endif
