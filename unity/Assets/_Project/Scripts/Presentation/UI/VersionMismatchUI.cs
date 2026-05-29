using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Colosseum.Network;

namespace Colosseum.UI
{
    public class VersionMismatchUI : MonoBehaviour
    {
        [Header("Bean Images (Inspector 할당)")]
        [SerializeField] private Sprite _bean1;
        [SerializeField] private Sprite _bean2;
        [SerializeField] private Sprite _bean3;

        [Header("Style")]
        [SerializeField] private Color _bgColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color _panelColor = new Color32(0x1E, 0x1E, 0x2E, 0xFF);
        [SerializeField] private Color _textColor = new Color32(0xF0, 0xF0, 0xF0, 0xFF);
        [SerializeField] private Color _versionColor = new Color32(0xFF, 0x6B, 0x6B, 0xFF);
        [SerializeField] private Color _buttonColor = new Color32(0xFF, 0x5A, 0x1F, 0xFF);
        [SerializeField] private Color _buttonHoverColor = new Color32(0xB8, 0x3A, 0x12, 0xFF);
        [SerializeField] private Color _buttonTextColor = Color.white;

        [Header("Update URL")]
        [SerializeField] private string _updateUrl = "https://lab.ssafy.com/s14-final/S14P31A303/-/releases";

        private Canvas _canvas;
        private GameObject _root;

        private void OnEnable()
        {
            NetworkManager.OnVersionMismatch += Show;
        }

        private void OnDisable()
        {
            NetworkManager.OnVersionMismatch -= Show;
        }

        public void Show(string serverVersion, string clientVersion)
        {
            if (_root != null) return;
            CreateUI(serverVersion, clientVersion);
        }

        private void CreateUI(string serverVersion, string clientVersion)
        {
            // Canvas
            var canvasObj = new GameObject("VersionMismatchCanvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Dim overlay
            _root = new GameObject("DimOverlay");
            _root.transform.SetParent(canvasObj.transform, false);
            var dimRT = _root.AddComponent<RectTransform>();
            dimRT.anchorMin = Vector2.zero;
            dimRT.anchorMax = Vector2.one;
            dimRT.sizeDelta = Vector2.zero;
            var dimImg = _root.AddComponent<Image>();
            dimImg.color = _bgColor;

            // Center panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_root.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(700, 520);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = _panelColor;

            // Rounded corners via outline
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color32(0x55, 0xAA, 0xFF, 0x66);
            outline.effectDistance = new Vector2(2, -2);

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(40, 40, 30, 30);
            vlg.spacing = 16;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Bean images row
            var imageRow = new GameObject("BeanRow");
            imageRow.transform.SetParent(panel.transform, false);
            var rowLE = imageRow.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 160;
            var hlg = imageRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            CreateBeanImage(imageRow.transform, _bean1, "Bean1");
            CreateBeanImage(imageRow.transform, _bean2, "Bean2");
            CreateBeanImage(imageRow.transform, _bean3, "Bean3");

            // Title
            CreateText(panel.transform, "패치 버전이 다릅니다", 36, _textColor, FontStyles.Bold, 50f);

            // Version info
            CreateText(panel.transform, $"현재 v{clientVersion}  →  최신 v{serverVersion}", 28, _versionColor, FontStyles.Normal, 40f);

            // Spacer
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(panel.transform, false);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.preferredHeight = 20;

            // Update button
            CreateUpdateButton(panel.transform);
        }

        private void CreateBeanImage(Transform parent, Sprite sprite, string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredWidth = 140;
            le.preferredHeight = 140;
            var img = obj.AddComponent<Image>();
            img.preserveAspect = true;
            if (sprite != null)
                img.sprite = sprite;
            else
                img.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        }

        private void CreateText(Transform parent, string text, int fontSize, Color color, FontStyles style, float height)
        {
            var obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
        }

        private void CreateUpdateButton(Transform parent)
        {
            var btnObj = new GameObject("UpdateButton");
            btnObj.transform.SetParent(parent, false);
            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 64;
            le.preferredWidth = 300;

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = _buttonColor;

            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = _buttonColor;
            colors.highlightedColor = _buttonHoverColor;
            colors.pressedColor = _buttonHoverColor;
            btn.colors = colors;
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(OnUpdateClicked);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "업데이트";
            tmp.fontSize = 32;
            tmp.color = _buttonTextColor;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private void OnUpdateClicked()
        {
            if (!string.IsNullOrEmpty(_updateUrl))
                Application.OpenURL(_updateUrl);
        }
    }
}
