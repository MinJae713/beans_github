using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using Colosseum.Game;
using Colosseum.Network;
using Colosseum.Player;

namespace Colosseum.UI
{
    /// <summary>
    /// 플레이어 HP 바 라이프사이클 관리자.
    /// Presentation assembly가 PlayerHealth를 관찰해 HP 바 인스턴스를 생성·제거한다.
    /// NetworkGameplay 쪽 PlayerHealth는 UI 타입을 직접 참조하지 않는다.
    ///
    /// 등록 흐름:
    ///   HPBarManager enable → PlayerHealth.ActiveInstances replay → PlayerHPBar 생성 + Init
    ///   PlayerHealth Spawned/Despawned 이벤트 → PlayerHPBar 생성/제거
    ///
    /// fill 갱신은 PlayerHPBar 측에서 PlayerHealth.OnHealthChangedEvent를 직접 구독한다.
    /// HPBarManager는 인스턴스 라이프사이클만 책임진다.
    ///
    /// A303-327: 바 위에 닉네임 라벨을 추가. PlayerHealth.Object.InputAuthority ↔ RoomManager.Player1/2
    /// 매핑으로 슬롯 번호를 얻고, NetworkManager.LastRoomNicknames[slot]을 라벨에 주입한다.
    /// 닉네임 갱신 이벤트(NetworkManager.OnNicknamesUpdated) 구독으로 늦게 도착하는 경우도 처리.
    /// </summary>
    public class HPBarManager : MonoBehaviour
    {
        public static HPBarManager Instance { get; private set; }

        [Header("HP Bar Settings")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private float _barWidth = 80f;
        [SerializeField] private float _barHeight = 10f;

        [Header("Nickname Label (A303-327)")]
        [SerializeField] private float _nicknameFontSize = 12f;
        [SerializeField] private float _nicknameGap = 2f;  // 바 상단 ↔ 닉네임 하단 사이 px

        private readonly Dictionary<PlayerHealth, PlayerHPBar> _bars = new Dictionary<PlayerHealth, PlayerHPBar>();
        private RoomManager _roomManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Colosseum] HPBarManager 중복 인스턴스 감지 — 신규 인스턴스 파괴.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            PlayerHealth.SpawnedEvent += Register;
            PlayerHealth.DespawnedEvent += Unregister;
            NetworkManager.OnNicknamesUpdated += OnNicknamesUpdated;
            ReplayActivePlayers();
        }

        private void OnDisable()
        {
            PlayerHealth.SpawnedEvent -= Register;
            PlayerHealth.DespawnedEvent -= Unregister;
            NetworkManager.OnNicknamesUpdated -= OnNicknamesUpdated;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// PlayerHealth 이벤트/replay에서 호출. HP 바 인스턴스 생성 + 캐시 등록.
        /// 다른 scene/runner의 PlayerHealth는 무시한다.
        /// 중복 호출 시 무시 (멱등성 보장).
        /// </summary>
        public void Register(PlayerHealth player)
        {
            if (player == null) return;
            if (player.gameObject.scene != gameObject.scene) return;
            if (_bars.ContainsKey(player)) return;
            if (_canvas == null)
            {
                Debug.LogWarning("[Colosseum] HPBarManager._canvas 미할당 — HP 바 생성 불가.");
                return;
            }

            var hpBar = CreateHPBar(player);
            _bars[player] = hpBar;
            Debug.Log($"[Colosseum] HP Bar registered for {player.name}");
        }

        /// <summary>
        /// PlayerHealth Despawned 이벤트에서 호출. HP 바 destroy + 캐시 제거.
        /// 등록 안 된 player에 대해서도 안전 (no-op).
        /// </summary>
        public void Unregister(PlayerHealth player)
        {
            if (player == null) return;
            if (!_bars.TryGetValue(player, out var hpBar)) return;
            if (hpBar != null) Destroy(hpBar.gameObject);
            _bars.Remove(player);
            Debug.Log($"[Colosseum] HP Bar unregistered for {player.name}");
        }

        private void ReplayActivePlayers()
        {
            var activePlayers = new HashSet<PlayerHealth>();
            foreach (var player in PlayerHealth.ActiveInstances)
            {
                if (player == null || player.gameObject.scene != gameObject.scene) continue;
                activePlayers.Add(player);
                Register(player);
            }

            var staleBars = new List<KeyValuePair<PlayerHealth, PlayerHPBar>>();
            foreach (var pair in _bars)
            {
                if (pair.Key == null || pair.Value == null || !activePlayers.Contains(pair.Key))
                {
                    staleBars.Add(pair);
                }
            }

            foreach (var pair in staleBars)
            {
                if (pair.Key != null)
                {
                    Unregister(pair.Key);
                }
                else
                {
                    if (pair.Value != null)
                    {
                        Destroy(pair.Value.gameObject);
                    }
                    _bars.Remove(pair.Key);
                }
            }
        }

        private PlayerHPBar CreateHPBar(PlayerHealth player)
        {
            // HP Bar 루트
            GameObject barObj = new GameObject($"HPBar_{player.name}");
            barObj.transform.SetParent(_canvas.transform, false);

            var hpBar = barObj.AddComponent<PlayerHPBar>();

            // 배경 (어두운 바)
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(barObj.transform, false);
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(_barWidth, _barHeight);

            // 채우기 (HP 표시)
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(bgObj.transform, false);
            var fillImage = fillObj.AddComponent<Image>();
            fillImage.color = Color.green;
            fillImage.sprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                Vector2.one * 0.5f
            );
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0; // Left
            fillImage.fillAmount = 1f;
            var fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // 닉네임 라벨 (A303-327) — 바 상단 위에 배치, 폭은 바보다 넓게 잡아 긴 닉네임도 보이게
            GameObject nickObj = new GameObject("Nickname");
            nickObj.transform.SetParent(barObj.transform, false);
            var nickText = nickObj.AddComponent<TextMeshProUGUI>();
            nickText.text = string.Empty;
            nickText.fontSize = _nicknameFontSize;
            nickText.color = Color.white;
            nickText.alignment = TextAlignmentOptions.Center;
            nickText.enableWordWrapping = false;
            nickText.overflowMode = TextOverflowModes.Ellipsis;
            nickText.raycastTarget = false;
            var nickRect = nickObj.GetComponent<RectTransform>();
            nickRect.anchorMin = new Vector2(0.5f, 0.5f);
            nickRect.anchorMax = new Vector2(0.5f, 0.5f);
            nickRect.pivot = new Vector2(0.5f, 0f);
            nickRect.sizeDelta = new Vector2(_barWidth + 60f, _nicknameFontSize + 4f);
            nickRect.anchoredPosition = new Vector2(0f, _barHeight * 0.5f + _nicknameGap);

            // PlayerHPBar 초기화 — fillImage / nicknameLabel을 직접 인자로 전달 (이전 Reflection 패턴 제거)
            hpBar.Init(player, fillImage, nickText);

            // 초기 닉네임 반영 — 매핑 가능하면 즉시 주입, 아니면 OnNicknamesUpdated 콜백에서 갱신.
            RefreshNicknameFor(hpBar, player);
            return hpBar;
        }

        /// <summary>
        /// NetworkManager.OnNicknamesUpdated 이벤트 콜백. 모든 등록된 바의 닉네임을 재해석.
        /// 닉네임 ARRIVAL과 PlayerHealth Spawn의 도착 순서가 보장되지 않으므로 이벤트 기반 refresh가 필요.
        /// </summary>
        private void OnNicknamesUpdated(string[] nicknames)
        {
            foreach (var pair in _bars)
            {
                if (pair.Key == null || pair.Value == null) continue;
                RefreshNicknameFor(pair.Value, pair.Key);
            }
        }

        /// <summary>
        /// PlayerHealth.Object.InputAuthority ↔ RoomManager.Player1/2 매핑으로 슬롯 결정 →
        /// NetworkManager.LastRoomNicknames[slot] 을 라벨에 주입.
        /// 매핑 실패 시 빈 문자열로 비워둔다 (다음 OnNicknamesUpdated에서 재시도됨).
        /// </summary>
        private void RefreshNicknameFor(PlayerHPBar bar, PlayerHealth health)
        {
            if (bar == null) return;
            if (health == null || health.Object == null || !health.Object.IsValid)
            {
                bar.SetNickname(string.Empty);
                return;
            }

            var rm = ResolveRoomManager();
            var nm = NetworkManager.Instance;
            if (rm == null || nm == null)
            {
                bar.SetNickname(string.Empty);
                return;
            }

            PlayerRef ownerRef = health.Object.InputAuthority;
            int slot = -1;
            if (ownerRef == rm.Player1) slot = 0;
            else if (ownerRef == rm.Player2) slot = 1;

            string nickname = null;
            string[] nicks = nm.LastRoomNicknames;
            if (slot >= 0 && nicks != null && slot < nicks.Length)
            {
                nickname = nicks[slot];
            }

            bar.SetNickname(nickname ?? string.Empty);
        }

        /// <summary>
        /// RoomManager 참조 캐시 — 초기/이벤트 경로에서만 호출되는 cold path이므로 FindObjectOfType 허용.
        /// 캐시가 stale(파괴됨) 한 경우 재해석.
        /// </summary>
        private RoomManager ResolveRoomManager()
        {
            if (_roomManager != null && _roomManager.Object != null && _roomManager.Object.IsValid)
            {
                return _roomManager;
            }
            _roomManager = FindObjectOfType<RoomManager>();
            return _roomManager;
        }
    }
}
