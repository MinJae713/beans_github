using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Colosseum.UI.MainMenu
{
    /// <summary>
    /// A303-354 메뉴 톤 통일 — TMP_InputField 위에 시안 underline 한 줄.
    /// 박스 배경 없음, placeholder 알파 0.4 흰색, 입력 텍스트 흰색.
    /// Focus 시 underline 알파 1.0 / Blur 시 0.6 으로 미세 강조.
    /// 외부 wiring: <see cref="input"/> 의 onValueChanged / onSubmit 를 그대로 사용.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CyberInputField : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public TMP_InputField input;
        public Image underline;
        public TextMeshProUGUI placeholderText;
        public TextMeshProUGUI inputText;

        public Color underlineColor = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 1f);
        public Color placeholderColor = new Color(1f, 1f, 1f, 0.4f);
        public Color textColor = Color.white;
        public float blurUnderlineAlpha = 0.6f;
        public float focusUnderlineAlpha = 1f;

        public event Action<string> OnValueChanged;
        public event Action<string> OnSubmit;
        public event Action<bool> OnFocusChanged;

        private void Awake()
        {
            ApplyTokens();

            if (input != null)
            {
                input.onValueChanged.AddListener(value => OnValueChanged?.Invoke(value));
                input.onSubmit.AddListener(value => OnSubmit?.Invoke(value));
                input.onSelect.AddListener(_ => SetFocus(true));
                input.onDeselect.AddListener(_ => SetFocus(false));
            }
        }

        private void OnEnable()
        {
            SetFocus(false);
        }

        public string Text => input != null ? input.text : string.Empty;

        public void SetText(string value)
        {
            if (input != null) input.text = value;
        }

        public void SetPlaceholder(string value)
        {
            if (placeholderText != null) placeholderText.text = value;
        }

        public void ActivateInputField()
        {
            if (input != null) input.ActivateInputField();
        }

        public void OnSelect(BaseEventData _) { SetFocus(true); }
        public void OnDeselect(BaseEventData _) { SetFocus(false); }

        private void ApplyTokens()
        {
            if (placeholderText != null) placeholderText.color = placeholderColor;
            if (inputText != null) inputText.color = textColor;
            if (input != null)
            {
                input.caretColor = underlineColor;
                input.selectionColor = new Color(underlineColor.r, underlineColor.g, underlineColor.b, 0.35f);
            }
        }

        private void SetFocus(bool focused)
        {
            if (underline != null)
            {
                var c = underlineColor;
                c.a = focused ? focusUnderlineAlpha : blurUnderlineAlpha;
                underline.color = c;
            }
            OnFocusChanged?.Invoke(focused);
        }
    }
}
