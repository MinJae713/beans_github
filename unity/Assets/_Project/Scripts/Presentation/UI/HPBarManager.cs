using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
    /// </summary>
    public class HPBarManager : MonoBehaviour
    {
        public static HPBarManager Instance { get; private set; }

        [Header("HP Bar Settings")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private float _barWidth = 80f;
        [SerializeField] private float _barHeight = 10f;

        private readonly Dictionary<PlayerHealth, PlayerHPBar> _bars = new Dictionary<PlayerHealth, PlayerHPBar>();

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
            ReplayActivePlayers();
        }

        private void OnDisable()
        {
            PlayerHealth.SpawnedEvent -= Register;
            PlayerHealth.DespawnedEvent -= Unregister;
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

            // PlayerHPBar 초기화 — fillImage를 직접 인자로 전달 (이전 Reflection 패턴 제거)
            hpBar.Init(player, fillImage);
            return hpBar;
        }
    }
}
