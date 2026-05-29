using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Colosseum.UI.MainMenu
{
    /// <summary>
    /// A303-354 메뉴 톤 통일 — Find Room 한 row.
    /// 좌측 방 이름 (흰), 가운데 인원 "1 / 2" (시안), 우측 상태 (분기 색).
    /// 입장 가능(canJoin=true) 시에만 hover 시 텍스트 시안 + scale 1.01 (0.15s), 클릭 시 OnRoomSelected.
    /// 외부 sprite 의존성 없음 — 텍스트만 사용. 부모는 VerticalLayoutGroup 으로 spacing 12 권장.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class RoomCardItem : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public enum RoomStatus { Waiting, Playing, Full }

        public TextMeshProUGUI nameLabel;
        public TextMeshProUGUI capacityLabel;
        public TextMeshProUGUI statusLabel;

        public Color textNormalColor = Color.white;
        public Color textHoverColor = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 1f);
        public Color capacityColor = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 1f);
        public Color statusWaitingColor = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 1f);
        public Color statusPlayingColor = new Color(0x8F / 255f, 0xBF / 255f, 0x3F / 255f, 1f);
        public Color statusFullColor = new Color(0xD7 / 255f, 0x73 / 255f, 0x89 / 255f, 1f);
        public Color disabledNameColor = new Color(1f, 1f, 1f, 0.4f);
        public float hoverScale = 1.01f;
        public float colorTransitionTime = 0.15f;
        public float scaleTransitionTime = 0.15f;

        public event Action OnRoomSelected;

        private RectTransform _rt;
        private bool _canJoin;
        private bool _hovered;
        private Coroutine _colorCo;
        private Coroutine _scaleCo;

        private void Awake()
        {
            _rt = (RectTransform)transform;
        }

        public void Setup(string roomName, int players, int max, RoomStatus status, Action onSelected)
        {
            bool full = status == RoomStatus.Full || players >= max;
            bool playing = status == RoomStatus.Playing;
            _canJoin = !full && !playing;

            if (nameLabel != null)
            {
                nameLabel.text = roomName;
                nameLabel.color = _canJoin ? textNormalColor : disabledNameColor;
            }
            if (capacityLabel != null)
            {
                capacityLabel.text = $"{players} / {max}";
                capacityLabel.color = capacityColor;
            }
            if (statusLabel != null)
            {
                if (full) { statusLabel.text = "Full"; statusLabel.color = statusFullColor; }
                else if (playing) { statusLabel.text = "Playing"; statusLabel.color = statusPlayingColor; }
                else { statusLabel.text = "Waiting"; statusLabel.color = statusWaitingColor; }
            }

            OnRoomSelected = onSelected;
            _hovered = false;
            ApplyImmediate();
        }

        public void OnPointerEnter(PointerEventData _) { if (!_canJoin) return; _hovered = true; ApplyAnimated(); }
        public void OnPointerExit(PointerEventData _)  { _hovered = false; ApplyAnimated(); }
        public void OnPointerClick(PointerEventData _) { if (_canJoin) OnRoomSelected?.Invoke(); }

        private void ApplyImmediate()
        {
            var target = _canJoin && _hovered ? textHoverColor : (_canJoin ? textNormalColor : disabledNameColor);
            float scale = _hovered && _canJoin ? hoverScale : 1f;
            if (nameLabel != null) nameLabel.color = target;
            if (_rt != null) _rt.localScale = new Vector3(scale, scale, 1f);
        }

        private void ApplyAnimated()
        {
            if (!isActiveAndEnabled) { ApplyImmediate(); return; }

            var target = _canJoin && _hovered ? textHoverColor : (_canJoin ? textNormalColor : disabledNameColor);
            float scale = _hovered && _canJoin ? hoverScale : 1f;

            if (_colorCo != null) StopCoroutine(_colorCo);
            if (_scaleCo != null) StopCoroutine(_scaleCo);
            _colorCo = StartCoroutine(LerpNameColor(target));
            _scaleCo = StartCoroutine(LerpScale(scale));
        }

        private IEnumerator LerpNameColor(Color target)
        {
            Color start = nameLabel != null ? nameLabel.color : target;
            float t = 0f;
            while (t < colorTransitionTime)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / colorTransitionTime));
                if (nameLabel != null) nameLabel.color = Color.Lerp(start, target, u);
                yield return null;
            }
            if (nameLabel != null) nameLabel.color = target;
            _colorCo = null;
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
