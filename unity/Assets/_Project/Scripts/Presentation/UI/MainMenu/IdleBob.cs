using System.Collections;
using UnityEngine;

namespace Colosseum.UI.MainMenu
{
    /// <summary>
    /// RectTransform anchoredPosition.y 를 Mathf.Sin 으로 위아래로 흔든다.
    /// DOTween 없이 Coroutine + Time.deltaTime 만 사용. A303-334 메인메뉴 캐릭터 idle bob.
    /// </summary>
    public class IdleBob : MonoBehaviour
    {
        public float amplitude = 4f;
        public float period = 2.4f;
        public float startDelay = 0f;

        private RectTransform _rt;
        private float _origY;

        private void OnEnable()
        {
            _rt = (RectTransform)transform;
            _origY = _rt.anchoredPosition.y;
            StartCoroutine(Bob());
        }

        private IEnumerator Bob()
        {
            if (startDelay > 0f)
                yield return new WaitForSeconds(startDelay);

            float t = 0f;
            while (enabled)
            {
                t += Time.deltaTime;
                float y = _origY + Mathf.Sin((t / period) * Mathf.PI * 2f) * amplitude;
                var p = _rt.anchoredPosition;
                p.y = y;
                _rt.anchoredPosition = p;
                yield return null;
            }
        }
    }
}
