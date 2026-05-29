using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Colosseum.UI.MainMenu
{
    /// <summary>
    /// A303-354 메뉴 톤 통일 — 시안 outline 9-slice 박스 버튼.
    /// Normal : outline alpha 0.7, scale 1.00, text 흰 (alpha 1.0)
    /// Hover  : outline alpha 1.0, scale 1.02, text 흰 (변경 없음)
    /// Pressed: scale 0.97
    /// 색 전환 0.15s, scale 전환 0.1s. Coroutine + Mathf.SmoothStep (DOTween 미사용).
    /// 텍스트는 모든 상태에서 흰색 고정 — fill 영역(없음) 과 동일 색 충돌 차단.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CyberButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler
    {
        // 단일 outline image (legacy / 9-slice 시나리오). null 일 수 있다.
        public Image outlineImage;
        // 다중 outline image (4-strip thin outline 시나리오). 비어 있으면 outlineImage 사용.
        public Image[] outlineImages;
        public TextMeshProUGUI label;

        public Color borderColor = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 1f);
        public Color textNormalColor = Color.white;
        // 텍스트는 hover 전환 없이 흰색 고정 (디자인 의도 — outline 만 시안 강조).
        // 따라서 별도 textHoverColor 필드는 두지 않는다. RoomCardItem 은 다른 디자인이라 자체 필드 보유.
        public float normalOutlineAlpha = 0.7f;
        public float hoverOutlineAlpha = 1f;
        public float hoverScale = 1.02f;
        public float pressedScale = 0.97f;
        public float colorTransitionTime = 0.15f;
        public float scaleTransitionTime = 0.1f;

        public event Action OnClicked;

        private RectTransform _rt;
        private bool _hovered;
        private bool _pressed;
        private bool _interactable = true;
        private Coroutine _colorCo;
        private Coroutine _scaleCo;

        public bool Interactable
        {
            get => _interactable;
            set
            {
                if (_interactable == value) return;
                _interactable = value;
                if (!value) { _hovered = false; _pressed = false; }
                ApplyImmediate();
            }
        }

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

        public void OnPointerEnter(PointerEventData _) { if (!_interactable) return; _hovered = true; ApplyAnimated(); }
        public void OnPointerExit(PointerEventData _)  { _hovered = false; _pressed = false; ApplyAnimated(); }
        public void OnPointerDown(PointerEventData _)  { if (!_interactable) return; _pressed = true; ApplyAnimated(); }
        public void OnPointerUp(PointerEventData _)    { _pressed = false; ApplyAnimated(); }
        public void OnPointerClick(PointerEventData _) { if (_interactable) OnClicked?.Invoke(); }

        private void ApplyImmediate()
        {
            var (outlineCol, textCol, scale) = ComputeTargets();
            SetOutlineColor(outlineCol);
            if (label != null) label.color = textCol;
            if (_rt != null) _rt.localScale = new Vector3(scale, scale, 1f);
        }

        private void SetOutlineColor(Color c)
        {
            if (outlineImages != null && outlineImages.Length > 0)
            {
                for (int i = 0; i < outlineImages.Length; i++)
                {
                    var img = outlineImages[i];
                    if (img != null) img.color = c;
                }
                return;
            }
            if (outlineImage != null) outlineImage.color = c;
        }

        private Color GetCurrentOutlineColor()
        {
            if (outlineImages != null)
            {
                for (int i = 0; i < outlineImages.Length; i++)
                {
                    var img = outlineImages[i];
                    if (img != null) return img.color;
                }
            }
            if (outlineImage != null) return outlineImage.color;
            return Color.white;
        }

        private void ApplyAnimated()
        {
            if (!isActiveAndEnabled) { ApplyImmediate(); return; }

            var (outlineCol, textCol, scale) = ComputeTargets();
            if (_colorCo != null) StopCoroutine(_colorCo);
            if (_scaleCo != null) StopCoroutine(_scaleCo);
            _colorCo = StartCoroutine(LerpColor(outlineCol, textCol));
            _scaleCo = StartCoroutine(LerpScale(scale));
        }

        private (Color outlineCol, Color textCol, float scale) ComputeTargets()
        {
            float outlineAlpha = _hovered ? hoverOutlineAlpha : normalOutlineAlpha;
            if (!_interactable) outlineAlpha = normalOutlineAlpha * 0.5f;

            var outlineCol = new Color(borderColor.r, borderColor.g, borderColor.b, outlineAlpha);
            // 텍스트는 hover 전환 없이 흰색 고정 (outline 만 시안). 비활성 시 alpha 0.4.
            var textCol = _interactable
                ? textNormalColor
                : new Color(textNormalColor.r, textNormalColor.g, textNormalColor.b, 0.4f);

            float scale = 1f;
            if (_pressed) scale = pressedScale;
            else if (_hovered) scale = hoverScale;
            return (outlineCol, textCol, scale);
        }

        private IEnumerator LerpColor(Color targetOutline, Color targetText)
        {
            Color startOutline = GetCurrentOutlineColor();
            Color startText = label != null ? label.color : targetText;
            float t = 0f;
            while (t < colorTransitionTime)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / colorTransitionTime));
                SetOutlineColor(Color.Lerp(startOutline, targetOutline, u));
                if (label != null) label.color = Color.Lerp(startText, targetText, u);
                yield return null;
            }
            SetOutlineColor(targetOutline);
            if (label != null) label.color = targetText;
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
    }
}
