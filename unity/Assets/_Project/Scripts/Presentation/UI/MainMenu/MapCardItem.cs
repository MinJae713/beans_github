using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Colosseum.UI.MainMenu
{
    /// <summary>
    /// A303-354 방 생성 화면의 맵 선택 카드.
    /// - 선택: thumbnail color sprite + 시안 outline alpha 1.0 + 흰 라벨
    /// - 비선택: thumbnail gray sprite + 시안 outline alpha 0.5 + 흐린 라벨
    /// - 클릭 시 OnClicked invoke (외부에서 다른 카드 IsSelected = false 처리)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MapCardItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Image thumbnail;
        public TextMeshProUGUI label;
        public Image[] outlineImages;
        public Sprite colorSprite;
        public Sprite graySprite;

        public Color outlineColor = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 1f);
        public Color labelSelectedColor = Color.white;
        public Color labelUnselectedColor = new Color(1f, 1f, 1f, 0.55f);
        public float selectedOutlineAlpha = 1f;
        public float unselectedOutlineAlpha = 0.5f;
        public float hoverScale = 1.02f;
        public float scaleTransitionTime = 0.1f;

        public event Action OnClicked;

        private RectTransform _rt;
        private bool _isSelected;
        private bool _hovered;
        private Coroutine _scaleCo;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                ApplyState();
            }
        }

        private void Awake()
        {
            _rt = (RectTransform)transform;
        }

        private void OnEnable()
        {
            _hovered = false;
            ApplyState();
        }

        public void OnPointerEnter(PointerEventData _) { _hovered = true; ApplyAnimated(); }
        public void OnPointerExit(PointerEventData _)  { _hovered = false; ApplyAnimated(); }
        public void OnPointerClick(PointerEventData _) { OnClicked?.Invoke(); }

        private void ApplyState()
        {
            if (thumbnail != null)
                thumbnail.sprite = _isSelected ? colorSprite : graySprite;

            if (label != null)
                label.color = _isSelected ? labelSelectedColor : labelUnselectedColor;

            if (outlineImages != null)
            {
                float a = _isSelected ? selectedOutlineAlpha : unselectedOutlineAlpha;
                var c = new Color(outlineColor.r, outlineColor.g, outlineColor.b, a);
                for (int i = 0; i < outlineImages.Length; i++)
                {
                    if (outlineImages[i] != null) outlineImages[i].color = c;
                }
            }
        }

        private void ApplyAnimated()
        {
            float target = _hovered ? hoverScale : 1f;
            if (_scaleCo != null) StopCoroutine(_scaleCo);
            _scaleCo = StartCoroutine(LerpScale(target));
        }

        private IEnumerator LerpScale(float target)
        {
            float start = _rt != null ? _rt.localScale.x : 1f;
            float t = 0f;
            while (t < scaleTransitionTime)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / scaleTransitionTime));
                float s = Mathf.Lerp(start, target, u);
                if (_rt != null) _rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (_rt != null) _rt.localScale = new Vector3(target, target, 1f);
            _scaleCo = null;
        }
    }
}
