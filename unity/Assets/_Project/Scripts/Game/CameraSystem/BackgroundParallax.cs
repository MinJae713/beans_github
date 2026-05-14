using UnityEngine;

namespace Colosseum.CameraSystem
{
    /// <summary>
    /// 배경 sprite에 부착하면 카메라가 이동할 때 부착된 GameObject가
    /// 카메라 이동량의 일부(parallaxFactor 비율)만큼만 따라감 — 원근감/입체감.
    /// </summary>
    [ExecuteAlways]
    public class BackgroundParallax : MonoBehaviour
    {
        [Range(0f, 1f)]
        [Tooltip("0 = BG가 카메라를 따라가지 않음 (static), 1 = 카메라와 동일하게 이동 (parallax 없음). 0.3~0.6 권장.")]
        public float parallaxFactor = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("Y축 parallax (보통 0 — 가로만 따라감)")]
        public float parallaxFactorY = 0f;

        [Tooltip("따라갈 카메라 (비워두면 Camera.main 자동 사용)")]
        public Camera targetCamera;

        Vector3 _startPosition;
        Vector3 _startCameraPosition;
        bool _initialized;

        void OnEnable()
        {
            _initialized = false;
        }

        void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            if (!_initialized)
            {
                _startPosition = transform.position;
                _startCameraPosition = targetCamera.transform.position;
                _initialized = true;
                return;
            }

            var camDelta = targetCamera.transform.position - _startCameraPosition;
            var offset = new Vector3(camDelta.x * parallaxFactor, camDelta.y * parallaxFactorY, 0f);
            transform.position = _startPosition + offset;
        }
    }
}
