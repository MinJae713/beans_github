#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using Colosseum.DevMode;

namespace Colosseum.UI
{
    /// <summary>
    /// DevMode 전용 설정 패널. Editor 빌드에서만 컴파일되며 production 빌드 zero impact.
    ///
    /// 책임:
    ///   - 봇 수동 소환/제거 토글 (DevModeController.ToggleBot 호출)
    ///   - 향후 dev 도구 확장 자리 (시간 빠르게, 카드 강제 적용, Status Effect 트리거 등)
    ///
    /// 사용 흐름:
    ///   1. DevModeController가 활성화된 씬에서 본 컴포넌트가 부착된 GameObject가 자동 동작
    ///   2. F5 키로 패널 토글 (default false — 시작 시 숨김)
    ///   3. "Spawn Bot" 클릭 → DevModeController.ActivateBot(0). 텍스트가 "Despawn Bot"으로 변경.
    ///   4. "Despawn Bot" 클릭 → DevModeController.DeactivateBot(0). 텍스트 복원.
    ///
    /// SampleScene 변경 0 — 본 컴포넌트가 자체 Canvas + Layout을 런타임 동적 생성한다.
    /// SampleScene에 부착할 필요도 없음 (Editor 진입 시 자동 인스턴스화).
    /// </summary>
    public class DevModeSettingsPanel : MonoBehaviour
    {
        public static DevModeSettingsPanel Instance { get; private set; }

        [Header("Toggle Key")]
        [Tooltip("패널 표시/숨김 단축키. default F5.")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.F5;

        [Header("Layout")]
        [Tooltip("패널 화면 우측 상단 offset (스크린 좌표).")]
        [SerializeField] private Vector2 _anchoredPosition = new Vector2(-20f, -20f);

        [SerializeField] private Vector2 _panelSize = new Vector2(220f, 200f);

        private Canvas _canvas;
        private GameObject _panelRoot;
        private Button _spawnBotButton;
        private Text _spawnBotButtonText;

        // ──────────────────────────────────────────────
        //  생명주기
        // ──────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            BuildUI();
            SetVisible(false); // 시작 시 숨김 — F5로 토글
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                bool nextVisible = !(_panelRoot != null && _panelRoot.activeSelf);
                SetVisible(nextVisible);
                Debug.Log($"[DevModeSettingsPanel] Toggle visibility: {nextVisible}");
            }

            // 봇 활성 상태 변동 시 버튼 텍스트 자동 갱신
            // (외부에서 ActivateBot/DeactivateBot 호출 시 동기화 보장)
            RefreshSpawnBotButtonLabel();
        }

        // ──────────────────────────────────────────────
        //  외부 진입점
        // ──────────────────────────────────────────────

        public void SetVisible(bool visible)
        {
            if (_panelRoot != null) _panelRoot.SetActive(visible);
        }

        public bool IsVisible => _panelRoot != null && _panelRoot.activeSelf;

        // ──────────────────────────────────────────────
        //  UI 동적 생성
        // ──────────────────────────────────────────────

        private void BuildUI()
        {
            // Canvas 생성 (자체 Canvas — 다른 UI와 독립)
            var canvasObj = new GameObject("DevModeSettingsCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000; // 다른 UI 위에
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            // Panel 루트 (우상단 anchor)
            _panelRoot = new GameObject("DevModeSettingsPanel");
            _panelRoot.transform.SetParent(canvasObj.transform, false);

            var panelRect = _panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = _anchoredPosition;
            panelRect.sizeDelta = _panelSize;

            var panelBg = _panelRoot.AddComponent<Image>();
            panelBg.color = new Color(0f, 0f, 0f, 0.75f);

            // Vertical Layout
            var layout = _panelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;

            // 헤더 라벨
            CreateLabel(_panelRoot.transform, "DEV MODE SETTINGS [F5]", 14, FontStyle.Bold);
            CreateLabel(_panelRoot.transform, "─── Bot ───", 12, FontStyle.Italic);

            // Spawn/Despawn Bot 토글 버튼
            (_spawnBotButton, _spawnBotButtonText) = CreateButton(_panelRoot.transform, "Spawn Bot", OnSpawnBotClicked);

            // TODO (확장 자리):
            // - 시간 빠르게 (Time.timeScale 슬라이더)
            // - 카드 강제 적용 (CardData 드롭다운 + Apply 버튼)
            // - Status Effect 트리거 (4종 + N스택)
            // - GameplayProfile 인-게임 핫리로드
            CreateLabel(_panelRoot.transform, "─── (확장 자리: 시간/카드/Status) ───", 10, FontStyle.Italic);
        }

        private void CreateLabel(Transform parent, string content, int fontSize, FontStyle style)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, fontSize + 6);

            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = fontSize + 6;
        }

        private (Button, Text) CreateButton(Transform parent, string label, System.Action onClick)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 28);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.30f, 1f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.25f, 0.25f, 0.30f, 1f);
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.45f, 1f);
            colors.pressedColor = new Color(0.45f, 0.45f, 0.55f, 1f);
            btn.colors = colors;

            // 텍스트 자식
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(go.transform, false);
            var textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<Text>();
            text.text = label;
            text.fontSize = 13;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 28;

            btn.onClick.AddListener(() => onClick?.Invoke());

            return (btn, text);
        }

        // ──────────────────────────────────────────────
        //  Bot 토글 핸들러
        // ──────────────────────────────────────────────

        private void OnSpawnBotClicked()
        {
            var ctrl = DevModeController.Instance;
            if (ctrl == null)
            {
                Debug.LogWarning("[DevModeSettingsPanel] DevModeController.Instance 가 null. DevMode 진입 상태 확인.");
                return;
            }
            if (ctrl.BotCount == 0)
            {
                Debug.LogWarning("[DevModeSettingsPanel] DevModeController.botGameObjects 슬롯이 비어 있습니다. Inspector에서 봇 GameObject를 등록해주세요.");
                return;
            }

            ctrl.ToggleBot(0);
            RefreshSpawnBotButtonLabel();
        }

        private void RefreshSpawnBotButtonLabel()
        {
            if (_spawnBotButtonText == null) return;
            var ctrl = DevModeController.Instance;
            if (ctrl == null || ctrl.BotCount == 0)
            {
                _spawnBotButtonText.text = "Spawn Bot (slot 비어있음)";
                return;
            }
            _spawnBotButtonText.text = ctrl.IsBotActive(0) ? "Despawn Bot" : "Spawn Bot";
        }
    }
}
#endif
