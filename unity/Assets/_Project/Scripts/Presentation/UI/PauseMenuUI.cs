using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Colosseum.Network;
using Colosseum.UI.MainMenu;

namespace Colosseum.UI
{
    /// <summary>
    /// A303-354 Beans 톤 인게임 ESC 메뉴.
    /// MainMenu 외 모든 씬에서 RuntimeInitializeOnLoadMethod 로 자동 instantiate.
    /// ESC 키 토글, SettingsUI / GameOverUI 우선순위 보존.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        private static PauseMenuUI _instance;

        private Canvas _canvas;
        private GameObject _panelRoot;
        private GameObject _confirmModalRoot;       // 게임 나가기 confirm modal (panelRoot 위에 별도 overlay)
        private bool _wasSettingsVisibleLastFrame;
        private GameOverUI _cachedGameOverUI;

        // Beans 톤 색상
        private static readonly Color _dimColor         = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color _modalBgColor     = new Color(0.10f, 0.10f, 0.18f, 0.95f);
        private static readonly Color _cyberAccentColor = new Color(0x7C / 255f, 0xE6 / 255f, 0xD8 / 255f, 1f);
        private static readonly Color _textColor        = Color.white;
        private static readonly Color _hintDimColor     = new Color(1f, 1f, 1f, 0.6f);

        public bool IsVisible => _panelRoot != null && _panelRoot.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu") return;
            if (_instance != null) return;
            var go = new GameObject("PauseMenuUI");
            go.AddComponent<PauseMenuUI>();
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
            Hide();

            if (SettingsUI.Instance == null)
                gameObject.AddComponent<SettingsUI>();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                _wasSettingsVisibleLastFrame = SettingsUI.Instance != null && SettingsUI.Instance.IsVisible;
                return;
            }

            // Priority 0: ConfirmModal 떠 있으면 그것만 닫음 (pause menu 유지)
            if (_confirmModalRoot != null && _confirmModalRoot.activeSelf)
            {
                _confirmModalRoot.SetActive(false);
                return;
            }

            // Priority 1: SettingsUI 가 떠 있었으면 그쪽이 ESC 처리 (자체 닫힘)
            if (_wasSettingsVisibleLastFrame)
            {
                _wasSettingsVisibleLastFrame = false;
                return;
            }

            // Priority 2: GameOver 중에는 ESC 무시
            if (_cachedGameOverUI == null)
                _cachedGameOverUI = FindObjectOfType<GameOverUI>();
            if (_cachedGameOverUI != null && _cachedGameOverUI.IsShowing)
                return;

            // Priority 3: 토글
            if (IsVisible) Hide(); else Show();
        }

        private void LateUpdate()
        {
            _wasSettingsVisibleLastFrame = SettingsUI.Instance != null && SettingsUI.Instance.IsVisible;
        }

        public void Show()
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(true);
        }

        public void Hide()
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
        }

        private void OnSettingsClicked()
        {
            if (SettingsUI.Instance != null)
                SettingsUI.Instance.Show();
        }

        private void OnQuitClicked()
        {
            // 한 번 더 확인 모달 띄움 — 게임 나가기 실수 클릭 방어.
            if (_confirmModalRoot != null)
                _confirmModalRoot.SetActive(true);
        }

        private void OnConfirmQuitYesClicked()
        {
            if (_confirmModalRoot != null) _confirmModalRoot.SetActive(false);
            Hide();
            var nm = NetworkManager.Instance;
            if (nm != null)
            {
                nm.DisconnectGameSession();
                SceneManager.LoadScene("MainMenu");
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }
        }

        private void OnConfirmQuitNoClicked()
        {
            if (_confirmModalRoot != null) _confirmModalRoot.SetActive(false);
        }

        // ──────────────────────────────────────────────
        //  UI build (Beans 톤)
        // ──────────────────────────────────────────────

        private void BuildUI()
        {
            var canvasObj = new GameObject("PauseMenuCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 250;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }

            // 루트 — dim 배경
            _panelRoot = new GameObject("PausePanel");
            _panelRoot.transform.SetParent(canvasObj.transform, false);
            var panelRect = _panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var dimImg = _panelRoot.AddComponent<Image>();
            dimImg.color = _dimColor;
            dimImg.raycastTarget = true;

            // 중앙 모달 (다크 반투명 + 시안 outline)
            var modal = new GameObject("Modal");
            modal.transform.SetParent(_panelRoot.transform, false);
            var modalRect = modal.AddComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.pivot = new Vector2(0.5f, 0.5f);
            modalRect.sizeDelta = new Vector2(540f, 560f);
            var modalImg = modal.AddComponent<Image>();
            modalImg.color = _modalBgColor;
            modalImg.raycastTarget = true;

            BuildOutlineStripWithColor(modal.transform, "Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f),
                new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.5f));
            BuildOutlineStripWithColor(modal.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f),
                new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.5f));
            BuildOutlineStripWithColor(modal.transform, "Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f),
                new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.5f));
            BuildOutlineStripWithColor(modal.transform, "Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, 0f),
                new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.5f));

            // Title "MENU"
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(modal.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -90f);
            titleRect.sizeDelta = new Vector2(400f, 80f);
            var titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "MENU";
            titleTmp.fontSize = 64;
            titleTmp.color = _textColor;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.raycastTarget = false;

            // Settings CyberButton
            BuildCyberButton(modal.transform, "Settings", 320f, 70f, 30,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), OnSettingsClicked);

            // Quit (게임 나가기) CyberButton
            BuildCyberButton(modal.transform, "게임 나가기", 320f, 70f, 30,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), OnQuitClicked);

            // Hint text (하단)
            var hintObj = new GameObject("Hint");
            hintObj.transform.SetParent(modal.transform, false);
            var hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 60f);
            hintRect.sizeDelta = new Vector2(460f, 40f);
            var hintTmp = hintObj.AddComponent<TextMeshProUGUI>();
            hintTmp.text = "게임으로 돌아가려면 ESC 키를 누르세요";
            hintTmp.fontSize = 20;
            hintTmp.color = _hintDimColor;
            hintTmp.fontStyle = FontStyles.Bold;
            hintTmp.alignment = TextAlignmentOptions.Center;
            hintTmp.raycastTarget = false;

            // 게임 나가기 확인 모달 (panelRoot 의 sibling, 같은 canvas)
            BuildConfirmQuitModal(canvasObj.transform);
        }

        private void BuildConfirmQuitModal(Transform canvasParent)
        {
            _confirmModalRoot = new GameObject("QuitConfirmModal");
            _confirmModalRoot.transform.SetParent(canvasParent, false);
            var rootRt = _confirmModalRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            // 한 단계 더 어두운 dim — pause menu 위에 또 한 겹.
            var dimImg = _confirmModalRoot.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.7f);
            dimImg.raycastTarget = true;

            // 모달 박스
            var modal = new GameObject("ConfirmModalBox");
            modal.transform.SetParent(_confirmModalRoot.transform, false);
            var modalRt = modal.AddComponent<RectTransform>();
            modalRt.anchorMin = new Vector2(0.5f, 0.5f);
            modalRt.anchorMax = new Vector2(0.5f, 0.5f);
            modalRt.pivot = new Vector2(0.5f, 0.5f);
            modalRt.sizeDelta = new Vector2(520f, 280f);
            var modalImg = modal.AddComponent<Image>();
            modalImg.color = _modalBgColor;
            modalImg.raycastTarget = true;

            // 시안 outline (얇은 strip)
            BuildOutlineStripWithColor(modal.transform, "Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f),
                new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.7f));
            BuildOutlineStripWithColor(modal.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f),
                new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.7f));
            BuildOutlineStripWithColor(modal.transform, "Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f),
                new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.7f));
            BuildOutlineStripWithColor(modal.transform, "Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, 0f),
                new Color(_cyberAccentColor.r, _cyberAccentColor.g, _cyberAccentColor.b, 0.7f));

            // 메시지
            var msgObj = new GameObject("Message");
            msgObj.transform.SetParent(modal.transform, false);
            var msgRt = msgObj.AddComponent<RectTransform>();
            msgRt.anchorMin = new Vector2(0.5f, 1f);
            msgRt.anchorMax = new Vector2(0.5f, 1f);
            msgRt.pivot = new Vector2(0.5f, 1f);
            msgRt.anchoredPosition = new Vector2(0f, -60f);
            msgRt.sizeDelta = new Vector2(460f, 80f);
            var msgTmp = msgObj.AddComponent<TextMeshProUGUI>();
            msgTmp.text = "정말 게임을 나가시겠습니까?";
            msgTmp.fontSize = 30;
            msgTmp.color = _textColor;
            msgTmp.fontStyle = FontStyles.Bold;
            msgTmp.alignment = TextAlignmentOptions.Center;
            msgTmp.raycastTarget = false;

            // 예 / 아니오 CyberButton — modal 중앙 좌우
            BuildCyberButton(modal.transform, "예", 180f, 56f, 26,
                new Vector2(0.5f, 0.5f), new Vector2(-110f, -50f), OnConfirmQuitYesClicked);
            BuildCyberButton(modal.transform, "아니오", 180f, 56f, 26,
                new Vector2(0.5f, 0.5f), new Vector2(110f, -50f), OnConfirmQuitNoClicked);

            _confirmModalRoot.SetActive(false);
        }

        private GameObject BuildCyberButton(Transform parent, string label, float width, float height, int fontSize,
            Vector2 anchor, Vector2 anchoredPos, System.Action onClicked)
        {
            const float strokeThickness = 2f;
            var go = new GameObject($"CyberBtn_{label}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(width, height);

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
            labelRect.offsetMin = new Vector2(20f, 0f);
            labelRect.offsetMax = new Vector2(-20f, 0f);
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
