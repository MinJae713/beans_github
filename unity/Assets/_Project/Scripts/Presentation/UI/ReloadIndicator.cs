using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Colosseum.Weapon;

namespace Colosseum.UI
{
    /// <summary>
    /// 총(Gun) 오브젝트에 부착하는 재장전 인디케이터.
    /// 총 옆에 작은 흰색 원형 게이지가 재장전 중에만 나타나며,
    /// 진행률에 따라 12시 방향부터 시계 방향으로 차오른다.
    ///
    /// - Gun의 자식이므로 마우스 에임에 따라 총과 함께 이동
    /// - 총이 회전해도 링은 항상 정면(upright)을 유지 (counter-rotate)
    /// - 외부 스프라이트 에셋 불필요 (코드로 링 텍스처 생성)
    ///
    /// 사용법: Gun과 같은 GameObject에 AddComponent, 또는
    ///         Gun 오브젝트의 Inspector에서 추가.
    /// </summary>
    public class ReloadIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Gun _gun;
        [SerializeField] private Transform _muzzle;

        [Header("Position")]
        [Tooltip("총구(Muzzle) 기준 오프셋. 총의 로컬 좌표계 기준이 아니라, 월드 기준 고정 오프셋.")]
        [SerializeField] private Vector2 _offset = new Vector2(0f, 0.3f);

        [Header("Appearance")]
        [SerializeField] private float _size = 0.25f;
        [SerializeField] private float _ringThickness = 0.15f;
        [SerializeField] private Color _ringColor = Color.white;
        [SerializeField] private int _textureResolution = 128;

        [Header("Ammo Dots")]
        [SerializeField] private float _dotSize = 0.05f;
        [SerializeField] private float _dotSpacing = 0.08f;
        [SerializeField] private float _rowSpacing = 0.08f;
        [SerializeField] private int _dotsPerRow = 3;
        [SerializeField] private Color _dotColor = Color.white;
        [SerializeField] private int _dotTextureResolution = 32;

        private Canvas _canvas;
        private Image _ringImage;
        private RectTransform _canvasRect;
        private GameObject _dotsCanvasObj;
        private RectTransform _dotsCanvasRect;
        private Sprite _dotSprite;
        private readonly List<Image> _dotPool = new List<Image>();
        private int _lastDisplayedAmmo = -1;
        private bool _initialized;

        private void Start()
        {
            if (_gun == null)
                _gun = GetComponent<Gun>();
            if (_gun == null)
                _gun = GetComponentInParent<Gun>();

            // Muzzle을 못 찾으면 Gun의 Transform 자체를 기준으로 사용
            if (_muzzle == null)
            {
                // Gun에서 _muzzle은 private이므로, 자식 중 "Muzzle"이란 이름의 Transform을 탐색
                _muzzle = transform.Find("Muzzle");
                if (_muzzle == null)
                    _muzzle = transform; // 폴백: Gun 위치 자체
            }

            CreateIndicator();
        }

        private void CreateIndicator()
        {
            // ── 월드 스페이스 Canvas (Gun의 자식이 아닌 씬 루트에 생성) ──
            // 이유: Gun이 회전하면 자식 Canvas도 같이 회전하기 때문에,
            //       독립 오브젝트로 만들고 LateUpdate에서 위치만 추적한다.
            var canvasObj = new GameObject("ReloadIndicatorCanvas");

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 5;

            _canvasRect = _canvas.GetComponent<RectTransform>();
            _canvasRect.sizeDelta = new Vector2(_size, _size);
            _canvasRect.localScale = Vector3.one;

            // ── 링 이미지 ──
            var ringObj = new GameObject("ReloadRing");
            ringObj.transform.SetParent(canvasObj.transform, false);

            _ringImage = ringObj.AddComponent<Image>();
            _ringImage.sprite = GenerateRingSprite(_textureResolution, _ringThickness);
            _ringImage.type = Image.Type.Filled;
            _ringImage.fillMethod = Image.FillMethod.Radial360;
            _ringImage.fillOrigin = (int)Image.Origin360.Top;
            _ringImage.fillClockwise = true;
            _ringImage.fillAmount = 0f;
            _ringImage.color = _ringColor;
            _ringImage.raycastTarget = false;

            var ringRect = ringObj.GetComponent<RectTransform>();
            ringRect.anchorMin = Vector2.zero;
            ringRect.anchorMax = Vector2.one;
            ringRect.offsetMin = Vector2.zero;
            ringRect.offsetMax = Vector2.zero;

            // 처음에는 숨김
            canvasObj.SetActive(false);

            // ── 탄약 도트용 별도 월드 스페이스 Canvas ──
            _dotsCanvasObj = new GameObject("AmmoDotsCanvas");
            var dotsCanvas = _dotsCanvasObj.AddComponent<Canvas>();
            dotsCanvas.renderMode = RenderMode.WorldSpace;
            dotsCanvas.sortingOrder = 5;

            _dotsCanvasRect = _dotsCanvasObj.GetComponent<RectTransform>();
            _dotsCanvasRect.sizeDelta = new Vector2(1f, 1f);
            _dotsCanvasRect.localScale = Vector3.one;

            _dotSprite = GenerateDotSprite(_dotTextureResolution);

            _dotsCanvasObj.SetActive(false);
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized || _gun == null) return;

            Vector3 anchorPos = _muzzle.position;
            Vector3 targetPos = new Vector3(
                anchorPos.x + _offset.x,
                anchorPos.y + _offset.y,
                anchorPos.z
            );

            if (_gun.IsReloading)
            {
                if (!_canvas.gameObject.activeSelf)
                    _canvas.gameObject.SetActive(true);
                if (_dotsCanvasObj.activeSelf)
                    _dotsCanvasObj.SetActive(false);

                _canvasRect.position = targetPos;
                _canvasRect.rotation = Quaternion.identity;
                _ringImage.fillAmount = _gun.ReloadProgress;
            }
            else
            {
                if (_canvas.gameObject.activeSelf)
                    _canvas.gameObject.SetActive(false);
                if (!_dotsCanvasObj.activeSelf)
                    _dotsCanvasObj.SetActive(true);

                _dotsCanvasRect.position = targetPos;
                _dotsCanvasRect.rotation = Quaternion.identity;

                UpdateAmmoDots(_gun.CurrentAmmo);
            }
        }

        private void UpdateAmmoDots(int ammo)
        {
            if (ammo < 0) ammo = 0;
            if (ammo == _lastDisplayedAmmo) return;
            _lastDisplayedAmmo = ammo;

            while (_dotPool.Count < ammo)
            {
                var dotObj = new GameObject($"AmmoDot_{_dotPool.Count}");
                dotObj.transform.SetParent(_dotsCanvasObj.transform, false);

                var img = dotObj.AddComponent<Image>();
                img.sprite = _dotSprite;
                img.color = _dotColor;
                img.raycastTarget = false;

                var rect = img.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(_dotSize, _dotSize);

                _dotPool.Add(img);
            }

            for (int i = 0; i < _dotPool.Count; i++)
            {
                _dotPool[i].gameObject.SetActive(false);
            }

            if (ammo <= 0) return;

            int perRow = Mathf.Max(1, _dotsPerRow);
            int numRows = (ammo + perRow - 1) / perRow;
            int topRowCount = ammo % perRow;
            if (topRowCount == 0) topRowCount = perRow;

            int idx = 0;
            for (int row = 0; row < numRows; row++)
            {
                int countInRow = (row == 0) ? topRowCount : perRow;
                float y = (numRows - 1 - row) * _rowSpacing;
                float totalWidth = (countInRow - 1) * _dotSpacing;
                float startX = -totalWidth / 2f;

                for (int i = 0; i < countInRow; i++)
                {
                    var img = _dotPool[idx];
                    img.rectTransform.anchoredPosition = new Vector2(startX + i * _dotSpacing, y);
                    img.gameObject.SetActive(true);
                    idx++;
                }
            }
        }

        private void OnDestroy()
        {
            // 독립 오브젝트이므로 Gun이 파괴될 때 같이 정리
            if (_canvas != null)
                Destroy(_canvas.gameObject);
            if (_dotsCanvasObj != null)
                Destroy(_dotsCanvasObj);
        }

        /// <summary>
        /// 코드로 작은 원(도트) 텍스처를 생성하여 Sprite로 반환.
        /// </summary>
        private Sprite GenerateDotSprite(int resolution)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            float center = resolution * 0.5f;
            float radius = center;
            float edgeSoftness = 1.5f;

            var pixels = new Color32[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((radius - dist) / edgeSoftness);
                    pixels[y * resolution + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution
            );
        }

        /// <summary>
        /// 코드로 흰색 링(도넛) 텍스처를 생성하여 Sprite로 반환.
        /// 외부 에셋 없이 동작한다.
        /// </summary>
        private Sprite GenerateRingSprite(int resolution, float thickness)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            float center = resolution * 0.5f;
            float outerRadius = center;
            float innerRadius = outerRadius * (1f - thickness);
            float edgeSoftness = 1.5f;

            var pixels = new Color32[resolution * resolution];

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float outerAlpha = Mathf.Clamp01((outerRadius - dist) / edgeSoftness);
                    float innerAlpha = Mathf.Clamp01((dist - innerRadius) / edgeSoftness);

                    byte a = (byte)(outerAlpha * innerAlpha * 255);
                    pixels[y * resolution + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution
            );
        }
    }
}
