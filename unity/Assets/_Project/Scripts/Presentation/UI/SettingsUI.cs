using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Colosseum.UI.MainMenu;

namespace Colosseum.UI
{
    /// <summary>
    /// A303-354 Beans 톤 Settings 모달.
    /// 메인메뉴 / 인게임 ESC 메뉴 양쪽에서 SettingsUI.Instance.Show() 로 호출.
    /// 핵심 기능 (PlayerPrefs 저장 / Screen.SetResolution / AudioListener.volume /
    /// RuntimeInitializeOnLoadMethod 시작 시 적용 / ESC 닫기 / 싱글톤) 보존.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        private const string PrefsMasterVolume = "Colosseum.Audio.MasterVolume";
        private const string PrefsResolutionIndex = "Colosseum.Display.ResolutionIndex";
        private const string PrefsFullscreen = "Colosseum.Display.Fullscreen";
        // 폴리싱 단계에서 디폴트를 전체화면 ON 으로 재설정 (이전 OFF 저장된 PlayerPrefs 1회 리셋).
        // 키 이름 변경하면 다시 1회 리셋 발생.
        private const string PrefsFullscreenDefaultSentinel = "Colosseum.Display.Fullscreen.DefaultV2";
        // 디폴트 해상도를 1920x1080 (Resolutions[0]) 로 1회 강제 리셋. 이전 빌드에서
        // 다른 해상도가 PlayerPrefs 에 저장돼 있어도 다음 실행 시 한 번 0 으로 덮어쓴다.
        private const string PrefsResolutionDefaultSentinel = "Colosseum.Display.Resolution.DefaultV2";

        private static readonly (int w, int h)[] Resolutions =
        {
            (1920, 1080), (1600, 900), (1280, 720), (960, 540)
        };

        // Beans 톤 색상
        private static readonly Color _dimColor          = new Color(0f, 0f, 0f, 0.85f);  // 모달 강조 위해 더 어둡게
        private static readonly Color _modalBgColor      = new Color(0.10f, 0.10f, 0.18f, 0.95f);
        private static readonly Color _cyberAccentColor  = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 1f);
        private static readonly Color _textColor         = Color.white;
        private static readonly Color _labelDimColor     = new Color(1f, 1f, 1f, 0.75f);
        private static readonly Color _dropdownBgColor   = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 0.85f);
        private static readonly Color _toggleTrackOnColor  = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 1f);
        private static readonly Color _toggleTrackOffColor = new Color(0.30f, 0.30f, 0.38f, 1f);

        private static SettingsUI _instance;
        public static SettingsUI Instance => _instance;

        private Canvas _canvas;
        private GameObject _panelRoot;
        private Slider _masterSlider;
        private TMP_Dropdown _resolutionDropdown;
        private Toggle _fullscreenToggle;
        private Image _toggleThumbImage;
        private TextMeshProUGUI _toggleStateLabel;

        private float _pendingMaster;
        private int _pendingResIndex;
        private bool _pendingFullscreen;

        public bool IsVisible => _panelRoot != null && _panelRoot.activeSelf;

        public void Show()
        {
            if (!IsVisible)
            {
                LoadSettingsToUI();
                _panelRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            BuildUI();
            LoadSettingsToUI();
            Hide();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (IsVisible && Input.GetKeyDown(KeyCode.Escape))
                Hide();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyStartupResolution()
        {
            if (Application.isBatchMode) return;

            // 디폴트 ON 강제 리셋 (1회) — 폴리싱 단계 전 OFF 로 저장된 PlayerPrefs 무효화.
            if (!PlayerPrefs.HasKey(PrefsFullscreenDefaultSentinel))
            {
                PlayerPrefs.SetInt(PrefsFullscreen, 1);
                PlayerPrefs.SetInt(PrefsFullscreenDefaultSentinel, 1);
                PlayerPrefs.Save();
            }

            // 디폴트 해상도 1920x1080 (index 0) 강제 리셋 (1회) — 이전 빌드/테스트로 저장된 다른 값 무효화.
            if (!PlayerPrefs.HasKey(PrefsResolutionDefaultSentinel))
            {
                PlayerPrefs.SetInt(PrefsResolutionIndex, 0);
                PlayerPrefs.SetInt(PrefsResolutionDefaultSentinel, 1);
                PlayerPrefs.Save();
            }

            int idx = PlayerPrefs.GetInt(PrefsResolutionIndex, 0);
            if (idx < 0 || idx >= Resolutions.Length) idx = 0;
            var (w, h) = Resolutions[idx];
            bool fs = PlayerPrefs.GetInt(PrefsFullscreen, 1) == 1;
            Screen.SetResolution(w, h, fs ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            AudioListener.volume = PlayerPrefs.GetFloat(PrefsMasterVolume, 1f);
        }

        private void LoadSettingsToUI()
        {
            _pendingMaster = PlayerPrefs.GetFloat(PrefsMasterVolume, 1f);
            _pendingResIndex = PlayerPrefs.GetInt(PrefsResolutionIndex, 0);
            _pendingFullscreen = PlayerPrefs.GetInt(PrefsFullscreen, 1) == 1;

            if (_masterSlider != null) _masterSlider.value = _pendingMaster;
            if (_resolutionDropdown != null) _resolutionDropdown.value = _pendingResIndex;
            if (_fullscreenToggle != null) _fullscreenToggle.isOn = _pendingFullscreen;
            RefreshToggleVisual();
        }

        private void ApplySettings()
        {
            PlayerPrefs.SetFloat(PrefsMasterVolume, _pendingMaster);
            PlayerPrefs.SetInt(PrefsResolutionIndex, _pendingResIndex);
            PlayerPrefs.SetInt(PrefsFullscreen, _pendingFullscreen ? 1 : 0);
            PlayerPrefs.Save();

            var (w, h) = Resolutions[_pendingResIndex];
            Screen.SetResolution(w, h, _pendingFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            AudioListener.volume = _pendingMaster;
        }

        // ──────────────────────────────────────────────
        //  UI build (Beans 톤)
        // ──────────────────────────────────────────────

        private void BuildUI()
        {
            var canvasObj = new GameObject("SettingsCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 300;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // 루트 (dim 배경)
            _panelRoot = new GameObject("SettingsPanel");
            _panelRoot.transform.SetParent(canvasObj.transform, false);
            var panelRect = _panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var dimImg = _panelRoot.AddComponent<Image>();
            dimImg.color = _dimColor;
            dimImg.raycastTarget = true; // 배경 click-through 차단

            // 중앙 모달 — 다크 반투명 + 시안 outline
            var modal = new GameObject("Modal");
            modal.transform.SetParent(_panelRoot.transform, false);
            var modalRect = modal.AddComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.pivot = new Vector2(0.5f, 0.5f);
            modalRect.sizeDelta = new Vector2(820f, 620f);
            var modalImg = modal.AddComponent<Image>();
            modalImg.color = _modalBgColor;
            modalImg.raycastTarget = true;

            // 시안 outline (4 strip)
            BuildOutlineStrip(modal.transform, "Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f));
            BuildOutlineStrip(modal.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f));
            BuildOutlineStrip(modal.transform, "Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f));
            BuildOutlineStrip(modal.transform, "Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, 0f));

            var container = new GameObject("Container");
            container.transform.SetParent(modal.transform, false);
            var contRect = container.AddComponent<RectTransform>();
            contRect.anchorMin = Vector2.zero;
            contRect.anchorMax = Vector2.one;
            contRect.offsetMin = new Vector2(60f, 60f);
            contRect.offsetMax = new Vector2(-60f, -60f);

            var vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 28f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 제목
            CreateLabel(container.transform, "Settings", 56, _textColor, FontStyles.Bold, TextAlignmentOptions.Center, 80f);

            CreateSpacer(container.transform, 12f);

            // 음량 row
            _masterSlider = CreateVolumeRow(container.transform, "음량");
            _masterSlider.onValueChanged.AddListener(v => _pendingMaster = v);

            // 해상도 row
            _resolutionDropdown = CreateResolutionRow(container.transform, "해상도");
            _resolutionDropdown.onValueChanged.AddListener(v => _pendingResIndex = v);

            // 전체화면 row
            _fullscreenToggle = CreateFullscreenRow(container.transform, "전체화면");
            _fullscreenToggle.onValueChanged.AddListener(v => { _pendingFullscreen = v; RefreshToggleVisual(); });

            CreateSpacer(container.transform, 12f);

            // 저장/닫기 버튼
            CreateButtonRow(container.transform);
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text, int fontSize, Color color,
            FontStyles style, TextAlignmentOptions align, float heightOverride = 0f)
        {
            var obj = new GameObject("Label_" + text);
            obj.transform.SetParent(parent, false);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = heightOverride > 0f ? heightOverride : fontSize + 14f;
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }

        private void CreateSpacer(Transform parent, float height)
        {
            var obj = new GameObject("Spacer");
            obj.transform.SetParent(parent, false);
            var le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleHeight = 0f;
        }

        private Slider CreateVolumeRow(Transform parent, string labelText)
        {
            var row = new GameObject("VolumeRow");
            row.transform.SetParent(parent, false);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 50f;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 30f;
            hlg.padding = new RectOffset(40, 40, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // 라벨
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            var labelLE = labelObj.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 140f;
            var labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
            labelTmp.text = labelText;
            labelTmp.fontSize = 28;
            labelTmp.color = _labelDimColor;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.raycastTarget = false;

            // 슬라이더
            var sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(row.transform, false);
            var sliderLE = sliderObj.AddComponent<LayoutElement>();
            sliderLE.flexibleWidth = 1f;
            sliderLE.preferredHeight = 24f;
            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            // 슬라이더 시각 — 시안 fill + 어두운 trail
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.35f);
            bgRect.anchorMax = new Vector2(1f, 0.65f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.12f, 1f);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.35f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.65f);
            fillAreaRect.offsetMin = new Vector2(0f, 0f);
            fillAreaRect.offsetMax = new Vector2(-10f, 0f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.85f);

            // handle
            var handleSlideArea = new GameObject("Handle Slide Area");
            handleSlideArea.transform.SetParent(sliderObj.transform, false);
            var hsaRect = handleSlideArea.AddComponent<RectTransform>();
            hsaRect.anchorMin = new Vector2(0f, 0f);
            hsaRect.anchorMax = new Vector2(1f, 1f);
            hsaRect.offsetMin = new Vector2(8f, 0f);
            hsaRect.offsetMax = new Vector2(-8f, 0f);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleSlideArea.transform, false);
            var handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(16f, 36f);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = _cyberAccentColor;

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

        private TMP_Dropdown CreateResolutionRow(Transform parent, string labelText)
        {
            var row = new GameObject("ResolutionRow");
            row.transform.SetParent(parent, false);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 56f;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 30f;
            hlg.padding = new RectOffset(40, 40, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            var labelLE = labelObj.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 140f;
            var labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
            labelTmp.text = labelText;
            labelTmp.fontSize = 28;
            labelTmp.color = _labelDimColor;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.raycastTarget = false;

            // Dropdown — Unity 표준 TMP_Dropdown hierarchy 준수
            var ddObj = new GameObject("Dropdown");
            ddObj.transform.SetParent(row.transform, false);
            var ddLE = ddObj.AddComponent<LayoutElement>();
            ddLE.preferredWidth = 360f;
            ddLE.preferredHeight = 50f;
            ddLE.flexibleWidth = 0f;
            var ddBg = ddObj.AddComponent<Image>();
            ddBg.color = _dropdownBgColor;
            ddBg.raycastTarget = true;

            // Caption (선택된 값 표시) — dd.captionText
            var captionObj = new GameObject("Label");
            captionObj.transform.SetParent(ddObj.transform, false);
            var captionRect = captionObj.AddComponent<RectTransform>();
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(20f, 0f);
            captionRect.offsetMax = new Vector2(-40f, 0f);
            var captionTmp = captionObj.AddComponent<TextMeshProUGUI>();
            captionTmp.fontSize = 22;
            captionTmp.color = new Color(0.04f, 0.04f, 0.12f, 1f);
            captionTmp.fontStyle = FontStyles.Bold;
            captionTmp.alignment = TextAlignmentOptions.MidlineLeft;
            captionTmp.raycastTarget = false;

            // Arrow ▼
            var arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(ddObj.transform, false);
            var arrowRect = arrowObj.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1f, 0.5f);
            arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-12f, 0f);
            arrowRect.sizeDelta = new Vector2(24f, 24f);
            var arrowTmp = arrowObj.AddComponent<TextMeshProUGUI>();
            arrowTmp.text = "▼";
            arrowTmp.fontSize = 18;
            arrowTmp.color = new Color(0.04f, 0.04f, 0.12f, 1f);
            arrowTmp.alignment = TextAlignmentOptions.MidlineRight;
            arrowTmp.raycastTarget = false;

            // Template (펼침 panel) — dropdown 아래쪽으로 펼침. SetActive(false) 시작.
            var templateObj = new GameObject("Template");
            templateObj.transform.SetParent(ddObj.transform, false);
            var tmplRect = templateObj.AddComponent<RectTransform>();
            tmplRect.anchorMin = new Vector2(0f, 0f);
            tmplRect.anchorMax = new Vector2(1f, 0f);
            tmplRect.pivot = new Vector2(0.5f, 1f);
            tmplRect.anchoredPosition = new Vector2(0f, 2f);
            tmplRect.sizeDelta = new Vector2(0f, 180f);  // 4 옵션 × ~40 + padding
            var tmplImg = templateObj.AddComponent<Image>();
            tmplImg.color = new Color(0.04f, 0.04f, 0.12f, 0.98f);  // 다크 배경, 거의 불투명
            tmplImg.raycastTarget = true;
            var tmplScroll = templateObj.AddComponent<ScrollRect>();

            // Viewport — Mask 가 정상 동작하려면 Image alpha > 0 필요
            var viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(templateObj.transform, false);
            var vpRect = viewportObj.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            var vpImg = viewportObj.AddComponent<Image>();
            vpImg.color = Color.white;
            vpImg.raycastTarget = false;
            var vpMask = viewportObj.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;

            // Content — TMP_Dropdown 이 자체 placement (Item 복제 후 anchoredPos 설정)
            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            var contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 40f);  // 한 item 의 height — dropdown 이 추가 시 늘림

            // Item — Toggle template. 한 옵션의 visual prefab.
            var itemObj = new GameObject("Item");
            itemObj.transform.SetParent(contentObj.transform, false);
            var itemRect = itemObj.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 40f);
            var itemToggle = itemObj.AddComponent<Toggle>();

            // Item Background — hover/select 시 visible
            var itemBgObj = new GameObject("Item Background");
            itemBgObj.transform.SetParent(itemObj.transform, false);
            var itemBgRect = itemBgObj.AddComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            itemBgRect.offsetMin = Vector2.zero;
            itemBgRect.offsetMax = Vector2.zero;
            var itemBgImg = itemBgObj.AddComponent<Image>();
            itemBgImg.color = new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.25f);
            itemBgImg.raycastTarget = true;

            // Item Checkmark — toggle.graphic (선택된 항목에 표시)
            var itemCheckObj = new GameObject("Item Checkmark");
            itemCheckObj.transform.SetParent(itemObj.transform, false);
            var itemCheckRect = itemCheckObj.AddComponent<RectTransform>();
            itemCheckRect.anchorMin = new Vector2(0f, 0.5f);
            itemCheckRect.anchorMax = new Vector2(0f, 0.5f);
            itemCheckRect.pivot = new Vector2(0.5f, 0.5f);
            itemCheckRect.sizeDelta = new Vector2(20f, 20f);
            itemCheckRect.anchoredPosition = new Vector2(15f, 0f);
            var itemCheckImg = itemCheckObj.AddComponent<Image>();
            itemCheckImg.color = _cyberAccentColor;
            itemCheckImg.raycastTarget = false;

            // Item Label — dd.itemText
            var itemLabelObj = new GameObject("Item Label");
            itemLabelObj.transform.SetParent(itemObj.transform, false);
            var itemLabelRect = itemLabelObj.AddComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(35f, 1f);
            itemLabelRect.offsetMax = new Vector2(-10f, -2f);
            var itemLabelTmp = itemLabelObj.AddComponent<TextMeshProUGUI>();
            itemLabelTmp.fontSize = 22;
            itemLabelTmp.color = Color.white;
            itemLabelTmp.fontStyle = FontStyles.Bold;
            itemLabelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            itemLabelTmp.raycastTarget = false;

            itemToggle.targetGraphic = itemBgImg;
            itemToggle.graphic = itemCheckImg;
            itemToggle.isOn = true;

            tmplScroll.content = contentRect;
            tmplScroll.viewport = vpRect;
            tmplScroll.horizontal = false;
            tmplScroll.vertical = true;
            tmplScroll.movementType = ScrollRect.MovementType.Clamped;
            tmplScroll.scrollSensitivity = 30f;

            templateObj.SetActive(false);

            // TMP_Dropdown 컴포넌트 — wiring 은 자식 GameObject 들이 모두 만들어진 후 가장 마지막에 추가.
            // 그래야 Awake 시점에 caption/template 등이 이미 존재.
            var dd = ddObj.AddComponent<TMP_Dropdown>();
            dd.targetGraphic = ddBg;
            dd.captionText = captionTmp;
            dd.template = tmplRect;
            dd.itemText = itemLabelTmp;
            dd.options.Clear();
            foreach (var (w, h) in Resolutions)
                dd.options.Add(new TMP_Dropdown.OptionData($"{w} x {h}"));
            dd.RefreshShownValue();

            return dd;
        }

        private Toggle CreateFullscreenRow(Transform parent, string labelText)
        {
            var row = new GameObject("FullscreenRow");
            row.transform.SetParent(parent, false);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 50f;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 30f;
            hlg.padding = new RectOffset(40, 40, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            var labelLE = labelObj.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 140f;
            var labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
            labelTmp.text = labelText;
            labelTmp.fontSize = 28;
            labelTmp.color = _labelDimColor;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.raycastTarget = false;

            // Toggle 본체 (raycast 만 받는 Image 없음, 자체 영역)
            var toggleObj = new GameObject("Toggle");
            toggleObj.transform.SetParent(row.transform, false);
            var toggleLE = toggleObj.AddComponent<LayoutElement>();
            toggleLE.preferredWidth = 160f;
            toggleLE.preferredHeight = 40f;
            var toggle = toggleObj.AddComponent<Toggle>();

            // Track (rounded background)
            var trackObj = new GameObject("Track");
            trackObj.transform.SetParent(toggleObj.transform, false);
            var trackRect = trackObj.AddComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(0f, 0.5f);
            trackRect.pivot = new Vector2(0f, 0.5f);
            trackRect.anchoredPosition = Vector2.zero;
            trackRect.sizeDelta = new Vector2(60f, 28f);
            var trackImg = trackObj.AddComponent<Image>();
            trackImg.color = _toggleTrackOnColor;
            toggle.targetGraphic = trackImg;

            // Thumb (원형 동그라미)
            var thumbObj = new GameObject("Thumb");
            thumbObj.transform.SetParent(trackObj.transform, false);
            var thumbRect = thumbObj.AddComponent<RectTransform>();
            thumbRect.anchorMin = new Vector2(1f, 0.5f);
            thumbRect.anchorMax = new Vector2(1f, 0.5f);
            thumbRect.pivot = new Vector2(1f, 0.5f);
            thumbRect.anchoredPosition = new Vector2(-2f, 0f);
            thumbRect.sizeDelta = new Vector2(24f, 24f);
            var thumbImg = thumbObj.AddComponent<Image>();
            thumbImg.color = Color.white;
            _toggleThumbImage = thumbImg;
            toggle.graphic = thumbImg;

            // ON/OFF 텍스트 (track 우측)
            var stateLabelObj = new GameObject("StateLabel");
            stateLabelObj.transform.SetParent(toggleObj.transform, false);
            var stateLabelRect = stateLabelObj.AddComponent<RectTransform>();
            stateLabelRect.anchorMin = new Vector2(0f, 0.5f);
            stateLabelRect.anchorMax = new Vector2(0f, 0.5f);
            stateLabelRect.pivot = new Vector2(0f, 0.5f);
            stateLabelRect.anchoredPosition = new Vector2(72f, 0f);
            stateLabelRect.sizeDelta = new Vector2(80f, 32f);
            _toggleStateLabel = stateLabelObj.AddComponent<TextMeshProUGUI>();
            _toggleStateLabel.text = "ON";
            _toggleStateLabel.fontSize = 22;
            _toggleStateLabel.color = _textColor;
            _toggleStateLabel.fontStyle = FontStyles.Bold;
            _toggleStateLabel.alignment = TextAlignmentOptions.MidlineLeft;
            _toggleStateLabel.raycastTarget = false;

            return toggle;
        }

        private void RefreshToggleVisual()
        {
            if (_fullscreenToggle == null) return;
            bool on = _fullscreenToggle.isOn;
            if (_toggleStateLabel != null) _toggleStateLabel.text = on ? "ON" : "OFF";
            // Track color
            var track = _fullscreenToggle.targetGraphic as Image;
            if (track != null) track.color = on ? _toggleTrackOnColor : _toggleTrackOffColor;
            // Thumb position — Track 우측(ON) ↔ 좌측(OFF)
            if (_toggleThumbImage != null)
            {
                var thumbRect = _toggleThumbImage.rectTransform;
                thumbRect.anchorMin = on ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
                thumbRect.anchorMax = on ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
                thumbRect.pivot = on ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
                thumbRect.anchoredPosition = on ? new Vector2(-2f, 0f) : new Vector2(2f, 0f);
            }
        }

        private void CreateButtonRow(Transform parent)
        {
            var row = new GameObject("ButtonsRow");
            row.transform.SetParent(parent, false);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 80f;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f;
            hlg.padding = new RectOffset(60, 60, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            BuildCyberButton(row.transform, "저장하기", 220f, 60f, 28, () =>
            {
                ApplySettings();
                Hide();
            });
            BuildCyberButton(row.transform, "닫기", 220f, 60f, 28, () => Hide());
        }

        private GameObject BuildCyberButton(Transform parent, string label, float width, float height, int fontSize, System.Action onClicked)
        {
            const float strokeThickness = 2f;
            var go = new GameObject($"CyberBtn_{label}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.flexibleWidth = 0f;

            var hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            Color strip = new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.85f);
            var top = BuildOutlineStripWithColor(go.transform, "Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, strokeThickness), strip);
            var bottom = BuildOutlineStripWithColor(go.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, strokeThickness), strip);
            var left = BuildOutlineStripWithColor(go.transform, "Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(strokeThickness, 0f), strip);
            var right = BuildOutlineStripWithColor(go.transform, "Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(strokeThickness, 0f), strip);

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(go.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 0f);
            labelRect.offsetMax = new Vector2(-10f, 0f);
            var tmp = labelObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;

            var btn = go.AddComponent<CyberButton>();
            btn.outlineImage = null;
            btn.outlineImages = new[] { top, bottom, left, right };
            btn.label = tmp;
            btn.borderColor = _cyberAccentColor;
            btn.OnClicked += onClicked;
            return go;
        }

        private void BuildOutlineStrip(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
        {
            BuildOutlineStripWithColor(parent, name, anchorMin, anchorMax, pivot, sizeDelta,
                new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.6f));
        }

        private Image BuildOutlineStripWithColor(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
    }
}
