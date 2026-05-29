using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using Colosseum.Game;

namespace Colosseum.UI
{
    /// <summary>
    /// 화면 상단 좌/우 — 다음 라운드 공격권 화살표.
    /// RoomManager.LastKiller가 [Networked]로 전환되어 클라이언트에서 직접 read 가능.
    /// 매 frame Update에서 polling으로 변경 감지(전용 ChangeDetector 안 씀 — 단일 값이라 단순 비교가 더 가벼움).
    /// 표시: lastKiller 미정 시 SegmentProgressHUD 아래 중앙에 'Fight!' 라벨만 (양쪽 화살표 숨김),
    ///       정해지면 라벨 숨기고 그 쪽 화살표만 활성. (양쪽 화살표 동시 노출은 의미가 약하다는 피드백 반영 — A303-362)
    /// 화살표 sprite는 Resources/UI/InGameHUD/Arrow/{P1,P2}_ARROW 에서 자동 로드 — 색·라벨·외곽선이
    /// 이미 이미지에 포함돼 있어 Outline 컴포넌트나 TMP Label 자식은 만들지 않는다.
    /// </summary>
    public class AttackDirectionHUD : MonoBehaviour
    {
        private const float ArrowWidth = 220f;
        // PNG 자체에 회전·여백이 베이크돼 있어 (P2_ARROW.png / P1_ARROW.png) 코드에서는 회전 0,
        // 화면 좌상·우상 꼭짓점에 정렬. (A303-362 — 이전엔 PNG가 정자세였어서 코드에서 -7/-8°와 60px 여백을 줬음)
        private const float MarginX = 0f;
        private const float MarginY = 0f;
        private const float P1RotationZ = 0f;
        private const float P2RotationZ = 0f;

        private const string P1SpritePath = "UI/InGameHUD/Arrow/P1_ARROW";
        private const string P2SpritePath = "UI/InGameHUD/Arrow/P2_ARROW";

        // 'Fight!' 라벨 — SegmentProgressHUD 바로 아래 중앙. SegmentProgressHUD가 anchor (0.5,1) pivot (0.5,1)
        // anchoredPosition (0,-40) + height 40 이라 bottom edge가 y=-80. 5px 여유를 둬 top y=-85에 배치.
        private const string FightLabelText = "Fight!";
        private const float FightLabelTopOffset = 85f;
        private const float FightLabelHeight = 56f;
        private const float FightLabelFontSize = 44f;
        private const float FightLabelOutlineWidth = 0.25f;
        private static readonly Color FightLabelColor = Color.white;
        private static readonly Color FightLabelOutlineColor = new Color(0f, 0f, 0f, 1f);

        private RoomManager _roomManager;
        private Sprite _p1Sprite;
        private Sprite _p2Sprite;
        private GameObject _p1Arrow;
        private GameObject _p2Arrow;
        private GameObject _fightLabel;

        private PlayerRef _lastKnownLastKiller = default;
        private PlayerRef _serverPlayer1 = default;
        private PlayerRef _serverPlayer2 = default;
        private bool _ready;

        public void Init(RoomManager rm)
        {
            _roomManager = rm;

            _p1Sprite = Resources.Load<Sprite>(P1SpritePath);
            _p2Sprite = Resources.Load<Sprite>(P2SpritePath);
            if (_p1Sprite == null || _p2Sprite == null)
            {
                Debug.LogError("[AttackDirectionHUD] Arrow sprite not found at Resources/UI/InGameHUD/Arrow/");
                gameObject.SetActive(false);
                return;
            }

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

            BuildArrows();
            BuildFightLabel();
            _lastKnownLastKiller = _roomManager.LastKiller;
            _serverPlayer1 = _roomManager.Player1;
            _serverPlayer2 = _roomManager.Player2;
            RefreshArrows();
            _ready = true;
        }

        private void Update()
        {
            if (!_ready) return;
            if (_roomManager == null) return;
            if (_roomManager.Object == null || !_roomManager.Object.IsValid) return;

            // Player1/Player2도 [Networked]라 클라에 자동 도착. 매 frame 변동 시만 갱신해서
            // RefreshArrows의 (lastKiller == _serverPlayer1) 비교가 양쪽 클라에서 일관되게 동작.
            // P1/P2가 LastKiller보다 늦게 바인딩되는 케이스에서도 화살표가 갱신되도록 셋 중 하나라도 변하면 RefreshArrows 호출.
            bool changed = false;
            if (_serverPlayer1 != _roomManager.Player1) { _serverPlayer1 = _roomManager.Player1; changed = true; }
            if (_serverPlayer2 != _roomManager.Player2) { _serverPlayer2 = _roomManager.Player2; changed = true; }
            PlayerRef serverLastKiller = _roomManager.LastKiller;
            if (serverLastKiller != _lastKnownLastKiller) { _lastKnownLastKiller = serverLastKiller; changed = true; }
            if (changed) RefreshArrows();
        }

        private void BuildArrows()
        {
            // P1: 우상, P2: 좌상
            _p1Arrow = CreateArrow("P1Arrow", new Vector2(1f, 1f), new Vector2(-MarginX, -MarginY), _p1Sprite, P1RotationZ);
            _p2Arrow = CreateArrow("P2Arrow", new Vector2(0f, 1f), new Vector2(MarginX, -MarginY), _p2Sprite, P2RotationZ);
        }

        private GameObject CreateArrow(string name, Vector2 anchor, Vector2 anchoredPos, Sprite sprite, float rotationZ)
        {
            GameObject arrow = new GameObject(name);
            arrow.transform.SetParent(transform, false);

            float aspect = sprite.texture != null && sprite.texture.width > 0
                ? (float)sprite.texture.height / sprite.texture.width
                : 1f;

            RectTransform rect = arrow.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, anchor.y);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(ArrowWidth, ArrowWidth * aspect);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

            GameObject bg = new GameObject("Bg");
            bg.transform.SetParent(arrow.transform, false);
            Image bgImage = bg.AddComponent<Image>();
            bgImage.sprite = sprite;
            bgImage.color = Color.white;
            bgImage.preserveAspect = true;
            // (A303-362) 화살표 위 클릭이 게임으로 통과되도록 — 총 발사 막혀 답답하다는 피드백 반영.
            bgImage.raycastTarget = false;
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            return arrow;
        }

        private void BuildFightLabel()
        {
            _fightLabel = new GameObject("FightLabel");
            _fightLabel.transform.SetParent(transform, false);

            RectTransform rect = _fightLabel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -FightLabelTopOffset);
            rect.sizeDelta = new Vector2(0f, FightLabelHeight);

            TextMeshProUGUI label = _fightLabel.AddComponent<TextMeshProUGUI>();
            label.text = FightLabelText;
            label.fontSize = FightLabelFontSize;
            label.fontStyle = FontStyles.Bold;
            label.color = FightLabelColor;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;

            // shared material 직접 수정 시 같은 폰트 사용하는 모든 TMP에 outline이 번지므로 instance로 복제 후 적용.
            // 패턴 출처: GameOverUI.cs (닉네임 라벨/카운트다운 등).
            label.fontMaterial = new Material(label.fontSharedMaterial);
            label.outlineColor = FightLabelOutlineColor;
            label.outlineWidth = FightLabelOutlineWidth;

            _fightLabel.SetActive(false);
        }

        private void RefreshArrows()
        {
            bool hasLastKiller = (_lastKnownLastKiller != default(PlayerRef));

            if (!hasLastKiller)
            {
                if (_p1Arrow != null) _p1Arrow.SetActive(false);
                if (_p2Arrow != null) _p2Arrow.SetActive(false);
                if (_fightLabel != null) _fightLabel.SetActive(true);
            }
            else
            {
                bool isP1 = (_lastKnownLastKiller == _serverPlayer1);
                if (_p1Arrow != null) _p1Arrow.SetActive(isP1);
                if (_p2Arrow != null) _p2Arrow.SetActive(!isP1);
                if (_fightLabel != null) _fightLabel.SetActive(false);
            }
        }
    }
}
