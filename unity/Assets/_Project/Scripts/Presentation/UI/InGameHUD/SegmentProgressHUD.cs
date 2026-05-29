using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Colosseum.Game;

namespace Colosseum.UI
{
    /// <summary>
    /// 화면 상단 중앙 — 전체 구간 진행도 막대.
    /// RoomManager.OnAdvanceTriggered는 서버에서만 invoke되므로 클라이언트에서는
    /// CurrentRoomIndex([Networked]) polling으로 변경을 감지한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SegmentProgressHUD : MonoBehaviour
    {
        // 색상 상수 (시안 기반)
        private static readonly Color P1Color = new Color(0.843f, 0.451f, 0.537f, 1f);   // #D77389 핑크 (Beans P1)
        private static readonly Color P2Color = new Color(0.561f, 0.749f, 0.247f, 1f);   // #8FBF3F 라임 (Beans P2)
        private static readonly Color WhiteHighlight = Color.white;                      // #FFFFFF
        private static readonly Color CurrentBorderColor = new Color(1f, 1f, 1f, 1f);    // #FFFFFF 현재 라운드 강조 흰색

        // 사이즈 상수
        private const float SegmentWidth = 100f;
        private const float SegmentHeight = 40f;
        private const float SegmentSpacing = 5f;
        private const float TopOffset = 40f;
        private const float DividerWidth = 4f;
        private const float CurrentBorderThickness = 3f;

        private RoomManager _roomManager;
        private RectTransform _rect;
        private Sprite _whiteSprite;
        private Image[] _segmentImages;
        private GameObject[] _segmentRightHalves; // 현재 구간 우반 (P2 색)
        private GameObject[] _segmentDividers;    // 현재 구간 가운데 흰 구분선
        private GameObject[] _segmentBorders;     // 현재 구간 빨간 외곽 테두리
        private int _lastIndex = -1;
        private bool _ready;

        public void Init(RoomManager rm)
        {
            _roomManager = rm;

            _rect = GetComponent<RectTransform>();
            _rect.anchorMin = new Vector2(0.5f, 1f);
            _rect.anchorMax = new Vector2(0.5f, 1f);
            _rect.pivot = new Vector2(0.5f, 1f);
            _rect.anchoredPosition = new Vector2(0f, -TopOffset);

            // HPBarManager Phase A303-281 fix와 동일 패턴: Resources.GetBuiltinResource는
            // 빌드/런타임에 null 반환 사례가 있어 Sprite.Create 직접 사용.
            _whiteSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                Vector2.one * 0.5f);

            BuildSegments();
            StartCoroutine(WaitForSpawnedThenActivate());
        }

        private IEnumerator WaitForSpawnedThenActivate()
        {
            while (_roomManager == null
                   || _roomManager.Object == null
                   || !_roomManager.Object.IsValid)
            {
                yield return null;
            }

            _lastIndex = _roomManager.CurrentRoomIndex;
            RefreshSegments();
            _ready = true;
        }

        private void Update()
        {
            if (!_ready) return;
            if (_roomManager == null) return;
            if (_roomManager.Object == null || !_roomManager.Object.IsValid) return;

            int currentIndex = _roomManager.CurrentRoomIndex;
            if (currentIndex != _lastIndex)
            {
                _lastIndex = currentIndex;
                RefreshSegments();
            }
        }

        private void BuildSegments()
        {
            int roomCount = _roomManager.RoomCount;
            _segmentImages = new Image[roomCount];
            _segmentRightHalves = new GameObject[roomCount];
            _segmentDividers = new GameObject[roomCount];
            _segmentBorders = new GameObject[roomCount];

            float totalWidth = roomCount * SegmentWidth + Mathf.Max(0, roomCount - 1) * SegmentSpacing;
            _rect.sizeDelta = new Vector2(totalWidth, SegmentHeight);

            for (int i = 0; i < roomCount; i++)
            {
                // 메인 세그먼트 (좌반 또는 전체)
                GameObject seg = new GameObject($"Segment_{i}");
                seg.transform.SetParent(transform, false);
                Image segImage = seg.AddComponent<Image>();
                segImage.sprite = _whiteSprite;
                segImage.raycastTarget = false;

                RectTransform segRect = seg.GetComponent<RectTransform>();
                segRect.anchorMin = new Vector2(0f, 0.5f);
                segRect.anchorMax = new Vector2(0f, 0.5f);
                segRect.pivot = new Vector2(0f, 0.5f);
                float xStart = i * (SegmentWidth + SegmentSpacing);
                segRect.anchoredPosition = new Vector2(xStart, 0f);
                segRect.sizeDelta = new Vector2(SegmentWidth, SegmentHeight);
                _segmentImages[i] = segImage;

                // 현재 구간용 우반 (처음엔 비활성, RefreshSegments에서 활성)
                GameObject rightHalf = new GameObject($"RightHalf_{i}");
                rightHalf.transform.SetParent(seg.transform, false);
                Image rightImage = rightHalf.AddComponent<Image>();
                rightImage.sprite = _whiteSprite;
                rightImage.color = P2Color;
                rightImage.raycastTarget = false;
                RectTransform rightRect = rightHalf.GetComponent<RectTransform>();
                rightRect.anchorMin = new Vector2(0.5f, 0f);
                rightRect.anchorMax = new Vector2(1f, 1f);
                rightRect.offsetMin = Vector2.zero;
                rightRect.offsetMax = Vector2.zero;
                rightHalf.SetActive(false);
                _segmentRightHalves[i] = rightHalf;

                // 가운데 흰 구분선 (처음엔 비활성)
                GameObject divider = new GameObject($"Divider_{i}");
                divider.transform.SetParent(seg.transform, false);
                Image divImage = divider.AddComponent<Image>();
                divImage.sprite = _whiteSprite;
                divImage.color = WhiteHighlight;
                divImage.raycastTarget = false;
                RectTransform divRect = divider.GetComponent<RectTransform>();
                divRect.anchorMin = new Vector2(0.5f, 0f);
                divRect.anchorMax = new Vector2(0.5f, 1f);
                divRect.pivot = new Vector2(0.5f, 0.5f);
                divRect.anchoredPosition = Vector2.zero;
                divRect.sizeDelta = new Vector2(DividerWidth, 0f);
                divider.SetActive(false);
                _segmentDividers[i] = divider;

                // 현재 구간 외곽 빨간 테두리 (4면 막대 컨테이너, 처음엔 비활성)
                GameObject border = new GameObject($"Border_{i}");
                border.transform.SetParent(seg.transform, false);
                RectTransform borderRect = border.AddComponent<RectTransform>();
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.offsetMin = Vector2.zero;
                borderRect.offsetMax = Vector2.zero;
                CreateBorderEdge(border.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, CurrentBorderThickness)); // top
                CreateBorderEdge(border.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0.5f), new Vector2(0f, CurrentBorderThickness)); // bottom
                CreateBorderEdge(border.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0f), new Vector2(CurrentBorderThickness, 0f)); // left
                CreateBorderEdge(border.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(CurrentBorderThickness, 0f)); // right
                border.SetActive(false);
                _segmentBorders[i] = border;
            }
        }

        private void CreateBorderEdge(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
        {
            GameObject edge = new GameObject("Edge");
            edge.transform.SetParent(parent, false);
            Image img = edge.AddComponent<Image>();
            img.sprite = _whiteSprite;
            img.color = CurrentBorderColor;
            img.raycastTarget = false;
            RectTransform rt = edge.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;
        }

        private void RefreshSegments()
        {
            int currentIndex = _roomManager.CurrentRoomIndex;
            int lastIndex = _segmentImages.Length - 1;

            // 양쪽 끝 = victory 구간이라 분기(반반) 표시 대신 단색.
            bool atRightVictory = (currentIndex >= lastIndex);
            bool atLeftVictory = (currentIndex <= 0);

            // 현재 칸 외곽 빨간 테두리는 victory 단색 시엔 끄고, 일반 전투 구간에선 currentIndex 칸만 켠다.
            bool showBorder = !atRightVictory && !atLeftVictory;

            for (int i = 0; i < _segmentImages.Length; i++)
            {
                if (atRightVictory)
                {
                    _segmentImages[i].color = P1Color;
                    _segmentRightHalves[i].SetActive(false);
                    _segmentDividers[i].SetActive(false);
                }
                else if (atLeftVictory)
                {
                    _segmentImages[i].color = P2Color;
                    _segmentRightHalves[i].SetActive(false);
                    _segmentDividers[i].SetActive(false);
                }
                else if (i < currentIndex)
                {
                    _segmentImages[i].color = P1Color;
                    _segmentRightHalves[i].SetActive(false);
                    _segmentDividers[i].SetActive(false);
                }
                else if (i > currentIndex)
                {
                    _segmentImages[i].color = P2Color;
                    _segmentRightHalves[i].SetActive(false);
                    _segmentDividers[i].SetActive(false);
                }
                else
                {
                    _segmentImages[i].color = P1Color;
                    _segmentRightHalves[i].SetActive(true);
                    _segmentDividers[i].SetActive(true);
                }

                _segmentBorders[i].SetActive(showBorder && i == currentIndex);
            }
        }
    }
}
