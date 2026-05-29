using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Colosseum.UI.MainMenu
{
    /// <summary>
    /// A303-334 메인메뉴 — 외곽선 박스 없는 미니멀 텍스트 메뉴 아이템.
    ///   Normal: label color (white@0.7), 좌우 "·" 점 숨김
    ///   Hover : label color (cyan #7CE6D8), 좌우 "·" 점 페이드 인
    ///   Pressed: cyan + scale 0.97
    /// 색 전환 0.15s, scale 전환 0.1s. Coroutine 기반 (DOTween 미사용).
    /// </summary>
    public class MenuItem : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler
    {
        public TextMeshProUGUI label;
        public TextMeshProUGUI leftDot;
        public TextMeshProUGUI rightDot;

        public Color normalColor = new Color(1f, 1f, 1f, 0.7f);
        public Color hoverColor = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 1f);
        public float colorTransitionTime = 0.15f;
        public float scaleTransitionTime = 0.1f;
        public float pressedScale = 0.97f;

        public event Action OnClicked;

        private RectTransform _rt;
        private bool _hovered;
        private bool _pressed;
        private Coroutine _colorCo;
        private Coroutine _scaleCo;

        private void Awake()
        {
            _rt = (RectTransform)transform;
        }

        private void OnEnable()
        {
            _hovered = false;
            _pressed = false;
            ApplyImmediate();
        }

        public void OnPointerEnter(PointerEventData _) { _hovered = true; ApplyAnimated(); }
        public void OnPointerExit(PointerEventData _)  { _hovered = false; _pressed = false; ApplyAnimated(); }
        public void OnPointerDown(PointerEventData _)  { _pressed = true; ApplyAnimated(); }
        public void OnPointerUp(PointerEventData _)    { _pressed = false; ApplyAnimated(); }
        public void OnPointerClick(PointerEventData _) { OnClicked?.Invoke(); }

        private void ApplyImmediate()
        {
            var targetColor = _hovered ? hoverColor : normalColor;
            float targetDotAlpha = _hovered ? 1f : 0f;
            float targetScale = _pressed ? pressedScale : 1f;

            if (label != null) label.color = targetColor;
            SetDots(targetColor, targetDotAlpha);
            if (_rt != null) _rt.localScale = new Vector3(targetScale, targetScale, 1f);
        }

        private void ApplyAnimated()
        {
            if (!isActiveAndEnabled)
            {
                ApplyImmediate();
                return;
            }

            var targetColor = _hovered ? hoverColor : normalColor;
            float targetDotAlpha = _hovered ? 1f : 0f;
            float targetScale = _pressed ? pressedScale : 1f;

            if (_colorCo != null) StopCoroutine(_colorCo);
            if (_scaleCo != null) StopCoroutine(_scaleCo);
            _colorCo = StartCoroutine(LerpColor(targetColor, targetDotAlpha));
            _scaleCo = StartCoroutine(LerpScale(targetScale));
        }

        private IEnumerator LerpColor(Color targetColor, float targetDotAlpha)
        {
            Color startColor = label != null ? label.color : targetColor;
            Color startDot = leftDot != null ? leftDot.color
                            : rightDot != null ? rightDot.color
                            : new Color(startColor.r, startColor.g, startColor.b, 0f);
            var targetDot = new Color(targetColor.r, targetColor.g, targetColor.b, targetDotAlpha);

            float t = 0f;
            while (t < colorTransitionTime)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / colorTransitionTime));
                if (label != null) label.color = Color.Lerp(startColor, targetColor, u);
                var dotNow = Color.Lerp(startDot, targetDot, u);
                if (leftDot != null) leftDot.color = dotNow;
                if (rightDot != null) rightDot.color = dotNow;
                yield return null;
            }
            if (label != null) label.color = targetColor;
            SetDots(targetColor, targetDotAlpha);
            _colorCo = null;
        }

        private IEnumerator LerpScale(float targetScale)
        {
            float startScale = _rt != null ? _rt.localScale.x : 1f;
            float t = 0f;
            while (t < scaleTransitionTime)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / scaleTransitionTime));
                float s = Mathf.Lerp(startScale, targetScale, u);
                if (_rt != null) _rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (_rt != null) _rt.localScale = new Vector3(targetScale, targetScale, 1f);
            _scaleCo = null;
        }

        private void SetDots(Color baseColor, float alpha)
        {
            var c = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            if (leftDot != null) leftDot.color = c;
            if (rightDot != null) rightDot.color = c;
        }
    }
}
