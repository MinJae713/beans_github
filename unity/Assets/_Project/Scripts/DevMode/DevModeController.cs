#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Fusion;
using Colosseum.UI;
using Colosseum.Card;
using Colosseum.Network;

namespace Colosseum.DevMode
{
    /// <summary>
    /// DevMode 전용 컨트롤러 (MonoBehaviour — NetworkBehaviour 아님).
    /// DevMode 씬에 배치하면 Awake에서 싱글턴 등록, Start에서 OnDevModeReady를 invoke한다.
    ///
    /// 다른 Agent/시스템이 의존하는 인터페이스:
    ///   - DevModeController.Instance          : 싱글턴 접근
    ///   - DevModeController.OnDevModeReady    : DevMode 준비 완료 이벤트 (DevModeAssetPack 전달)
    ///   - DevModeController.Instance.AssetPack: 현재 활성 에셋 팩 읽기
    ///
    /// DontDestroyOnLoad 적용 (NetworkScene reload 대비, L86 참조) — 씬 reload 시 두 번째 인스턴스는 싱글턴 가드에서 자기 파괴.
    ///
    /// DevMode 진입 흐름:
    ///   Awake → 싱글턴 등록
    ///   Start → OnDevModeReady 발행 → ActivateBots() → ShowDebugPanel() (autoShowDebugPanel == true 시)
    /// </summary>
    public class DevModeController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        //  싱글턴
        // ──────────────────────────────────────────────

        public static DevModeController Instance { get; private set; }

        // ──────────────────────────────────────────────
        //  공개 이벤트
        // ──────────────────────────────────────────────

        /// <summary>
        /// DevMode 준비 완료 시 발행. 구독자는 AssetPack을 통해 에셋을 주입한다.
        /// </summary>
        public static event System.Action<DevModeAssetPack> OnDevModeReady;

        // ──────────────────────────────────────────────
        //  Inspector 슬롯
        // ──────────────────────────────────────────────

        [SerializeField] private DevModeAssetPack assetPack;

        [Header("Bot 설정")]
        [Tooltip("씬에 비활성 상태로 미리 배치한 봇 GameObject 목록. ActivateBot(index) / DeactivateBot(index) / ToggleBot(index)로 수동 제어.")]
        [SerializeField] private GameObject[] botGameObjects;

        [Tooltip("DevMode 진입 시 botGameObjects 전체를 자동으로 SetActive(true)할지. A303-262 이후 default false — DevModeSettingsPanel에서 수동 소환.")]
        [SerializeField] private bool autoActivateBots = false;

        [Header("Debug Card Panel")]
        [Tooltip("씬에 배치된 DebugCardSelectPanel 인스턴스 또는 프리팹 레퍼런스.")]
        [SerializeField] private DebugCardSelectPanel debugCardPanelPrefab;

        [Tooltip("DevMode 진입 시 Debug Card Panel을 자동으로 표시할지 여부.")]
        [SerializeField] private bool autoShowDebugPanel = true;

        // ──────────────────────────────────────────────
        //  공개 프로퍼티
        // ──────────────────────────────────────────────

        /// <summary>
        /// Start() 이후 true. 어댑터 OnEnable에서 late-join 분기 체크에 사용.
        /// 어댑터 책임: OnDisable에서 OnDevModeReady -= Apply 명시 (Domain Reload 비활성 환경 대비).
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Start()에서 캐시된 AssetPack. IsReady == true 이후 유효.
        /// </summary>
        public DevModeAssetPack AssetPack => _lastAssetPack;

        // ──────────────────────────────────────────────
        //  내부 캐시
        // ──────────────────────────────────────────────

        private DevModeAssetPack _lastAssetPack;

        // ──────────────────────────────────────────────
        //  생명주기
        // ──────────────────────────────────────────────

        private bool _panelShown;
        private float _waitElapsed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DevModeController] 중복 인스턴스 감지 — 신규 인스턴스를 파괴합니다.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Photon Fusion이 NetworkScene 등록을 위해 SampleScene을 reload하면 첫 인스턴스의
            // GameObject가 파괴되어 코루틴이 stop된다. DontDestroyOnLoad로 살아남게 하고,
            // 두 번째 인스턴스는 위 Singleton 가드에서 자기 파괴된다.
            // 씬 배치 어댑터 GameObject는 reload 시 파괴되므로 어댑터는 IsReady late-join 분기 필수
            // (5종 어댑터 OnEnable 패턴 참조: if (Instance?.IsReady == true) Apply(Instance.AssetPack)).
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // production 가드: DevMode 진입이 아니면 자기 비활성화.
            // SampleScene에 직접 배치된 DevModeController가 일반 매칭 진입 시
            // ShowDebugPanel을 트리거하는 결함을 차단한다.
            var nmGuard = NetworkManager.Instance;
            if (nmGuard == null || !nmGuard.IsDevMode)
            {
                Debug.Log("[DevModeController] DevMode 진입 상태 아님 — 자기 비활성화 (production guard).");
                gameObject.SetActive(false);
                return;
            }

            // NetworkManager.StartDevMode() 완료 후 씬이 로드되면
            // DevModeController의 Start가 실행되어 에셋 팩을 알린다.
            //
            // 호출 순서 (plan §8 준수):
            // (1) invoke 먼저 — 이미 활성된 어댑터에 알림
            OnDevModeReady?.Invoke(assetPack);
            // (2) cache — 이후 AssetPack getter가 유효한 값 반환
            _lastAssetPack = assetPack;
            // (3) flag set — 이후 OnEnable되는 어댑터의 late-join 분기 활성
            IsReady = true;
            Debug.Log($"[DevModeController] OnDevModeReady 발행 — AssetPack: {assetPack?.name ?? "null (Inspector 슬롯 미할당)"}");

            // A303-262: 자동 활성화는 SerializeField 토글 default false. DevModeSettingsPanel에서 수동 소환.
            if (autoActivateBots)
            {
                ActivateBots();
            }

            // A303-PENDING: DevModeSettingsPanel 자동 부착.
            // Editor 진입 시 F5 토글 + Spawn/Despawn Bot UI를 사용하려면 컴포넌트가 살아있어야 한다.
            // 본 컨트롤러가 production 가드(IsDevMode 체크)를 통과한 시점이라 안전하게 부착할 수 있고,
            // 씬에 별도 GameObject를 배치할 필요가 없다 (자체 Canvas + Layout 동적 생성).
            EnsureDevModeSettingsPanel();
        }

        /// <summary>
        /// DevModeSettingsPanel을 런타임 동적 부착한다. 이미 존재하면 no-op (idempotent).
        /// 본 컨트롤러가 production 가드(IsDevMode 체크) 통과 후 호출되므로 일반 매칭 진입 시에는 만들어지지 않는다.
        /// 본 클래스 자체가 #if UNITY_EDITOR 가드 안에 있어서 production 빌드에서는 컴파일도 안 됨.
        /// </summary>
        private void EnsureDevModeSettingsPanel()
        {
            if (Colosseum.UI.DevModeSettingsPanel.Instance != null) return;

            var panelGO = new GameObject("DevModeSettingsPanel (auto)");
            panelGO.AddComponent<Colosseum.UI.DevModeSettingsPanel>();
            DontDestroyOnLoad(panelGO);
            Debug.Log("[DevModeController] DevModeSettingsPanel 자동 부착됨 (F5 토글 활성화)");
        }

        /// <summary>
        /// 매 프레임 NetworkManager.PlayerCount를 폴링해서 spawn 완료 시점에 패널 표시.
        /// Update는 GameObject reload되어도 새 인스턴스에서 다시 동작 (코루틴보다 robust).
        /// 한 번 표시되면 _panelShown으로 잠금.
        /// </summary>
        private void Update()
        {
            if (_panelShown) return;
            if (!autoShowDebugPanel) return;

            var nm = NetworkManager.Instance;
            if (nm == null) return;
            if (!nm.IsDevMode) return; // production 가드 — 일반 매칭 시 패널 트리거 차단

            var runner = nm.Runner;
            if (runner == null || !runner.IsRunning)
            {
                _waitElapsed += Time.deltaTime;
                if (_waitElapsed > 15f)
                {
                    Debug.LogWarning("[DevModeController] Runner 폴링 타임아웃 (15s). 빈 PlayerRef 배열로 패널 표시.");
                    _panelShown = true;
                    ShowDebugPanel();
                }
                return;
            }

            if (nm.PlayerCount == 0)
            {
                _waitElapsed += Time.deltaTime;
                if (_waitElapsed > 15f)
                {
                    Debug.LogWarning("[DevModeController] PlayerCount 폴링 타임아웃 (15s). NetworkManager._playerPrefab이 비어있거나 Spawn 실패 가능. 빈 배열로 패널 표시.");
                    _panelShown = true;
                    ShowDebugPanel();
                }
                return;
            }

            Debug.Log($"[DevModeController] Player 감지 (PlayerCount={nm.PlayerCount}) — Debug Panel 표시.");
            _panelShown = true;
            ShowDebugPanel();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ──────────────────────────────────────────────
        //  봇 활성화
        // ──────────────────────────────────────────────

        /// <summary>
        /// Inspector 슬롯에 연결된 봇 GameObject들을 SetActive(true)로 일괄 활성화한다.
        /// A303-262: 자동 호출은 autoActivateBots 토글로 분리. 수동 일괄 활성화는 본 메서드 그대로 유지.
        /// PoC: 일반 GameObject 활성화. Photon NetworkObject spawn / InputAuthority 처리는 후속 작업.
        /// </summary>
        public void ActivateBots()
        {
            if (botGameObjects == null || botGameObjects.Length == 0)
            {
                Debug.Log("[DevModeController] botGameObjects 슬롯이 비어 있습니다. 봇 활성화를 건너뜁니다.");
                return;
            }

            foreach (var botGO in botGameObjects)
            {
                if (botGO == null) continue;
                botGO.SetActive(true);
                Debug.Log($"[DevModeController] 봇 활성화: {botGO.name}");
            }
        }

        /// <summary>
        /// botGameObjects 슬롯 개수. DevModeSettingsPanel이 토글 가능 봇 개수를 알기 위해 사용.
        /// </summary>
        public int BotCount => botGameObjects?.Length ?? 0;

        /// <summary>
        /// 단일 봇이 현재 활성화돼 있는지. index 범위 밖이면 false.
        /// </summary>
        public bool IsBotActive(int index)
        {
            if (botGameObjects == null) return false;
            if (index < 0 || index >= botGameObjects.Length) return false;
            var botGO = botGameObjects[index];
            return botGO != null && botGO.activeSelf;
        }

        /// <summary>
        /// 단일 봇을 SetActive(true). DevModeSettingsPanel "Spawn Bot" 버튼이 호출.
        /// </summary>
        public void ActivateBot(int index = 0)
        {
            if (botGameObjects == null || index < 0 || index >= botGameObjects.Length)
            {
                Debug.LogWarning($"[DevModeController] ActivateBot: 잘못된 index {index}. BotCount={BotCount}");
                return;
            }
            var botGO = botGameObjects[index];
            if (botGO == null)
            {
                Debug.LogWarning($"[DevModeController] ActivateBot: botGameObjects[{index}]가 null. Inspector 슬롯 확인.");
                return;
            }
            botGO.SetActive(true);
            Debug.Log($"[DevModeController] 봇 활성화: {botGO.name} (index={index})");
        }

        /// <summary>
        /// 단일 봇을 SetActive(false). DevModeSettingsPanel "Despawn Bot" 버튼이 호출.
        /// </summary>
        public void DeactivateBot(int index = 0)
        {
            if (botGameObjects == null || index < 0 || index >= botGameObjects.Length)
            {
                Debug.LogWarning($"[DevModeController] DeactivateBot: 잘못된 index {index}. BotCount={BotCount}");
                return;
            }
            var botGO = botGameObjects[index];
            if (botGO == null) return;
            botGO.SetActive(false);
            Debug.Log($"[DevModeController] 봇 비활성화: {botGO.name} (index={index})");
        }

        /// <summary>
        /// 단일 봇 활성/비활성을 토글한다. 편의 메서드.
        /// </summary>
        public void ToggleBot(int index = 0)
        {
            if (IsBotActive(index)) DeactivateBot(index);
            else ActivateBot(index);
        }

        // ──────────────────────────────────────────────
        //  Debug Card Panel
        // ──────────────────────────────────────────────

        /// <summary>
        /// DebugCardSelectPanel을 표시한다.
        /// 카드 목록: assetPack.cardDataList 우선, 비어 있으면 빈 목록으로 표시 후 경고.
        /// PlayerRef 목록: PoC에서는 빈 배열 (후속 작업에서 Runner.ActivePlayers 연결).
        /// </summary>
        public void ShowDebugPanel()
        {
            if (debugCardPanelPrefab == null)
            {
                // dangling reference — Reset(LoadScene)으로 이전 DebugCardPanel destroy되고
                // DontDestroyOnLoad DevModeController가 무효 참조 보유한 경우 재탐색.
                debugCardPanelPrefab = FindObjectOfType<DebugCardSelectPanel>(true);
                if (debugCardPanelPrefab == null)
                {
                    Debug.LogWarning("[DevModeController] debugCardPanelPrefab 슬롯이 비어 있고 씬에서도 DebugCardSelectPanel을 찾지 못했습니다.");
                    return;
                }
                Debug.Log($"[DevModeController] debugCardPanelPrefab 재탐색 성공: {debugCardPanelPrefab.name}");
            }

            // 카드 목록: assetPack.cardDataList 우선
            List<CardData> cards;
            if (assetPack != null && assetPack.cardDataList != null && assetPack.cardDataList.Count > 0)
            {
                cards = assetPack.cardDataList;
            }
            else
            {
                Debug.LogWarning("[DevModeController] AssetPack.cardDataList가 비어 있습니다. 빈 카드 목록으로 패널을 표시합니다. (Inspector에서 DevModeAssetPack에 CardData를 할당하세요.)");
                cards = new List<CardData>();
            }

            // DevMode는 Host 단독 — LocalPlayer가 유일한 플레이어.
            // 기존 ActivePlayers는 PlayerRef와 실제 InputAuthority가 매칭 안 되어
            // DebugCardSelectPanel.Apply가 효과 미적용으로 끝나는 결함을 유발했다.
            PlayerRef[] players;
            var runner = NetworkManager.Instance != null ? NetworkManager.Instance.Runner : null;
            if (runner != null && runner.LocalPlayer != PlayerRef.None)
            {
                players = new[] { runner.LocalPlayer };
            }
            else
            {
                Debug.LogWarning("[DevModeController] Runner.LocalPlayer 가 None — 빈 PlayerRef 배열로 패널을 표시합니다.");
                players = new PlayerRef[0];
            }

            debugCardPanelPrefab.Show(deckRef: null, allCards: cards, availablePlayers: players);
        }

        /// <summary>
        /// DebugCardSelectPanel을 숨긴다.
        /// </summary>
        public void HideDebugPanel()
        {
            if (debugCardPanelPrefab == null) return;
            debugCardPanelPrefab.Hide();
        }

        // ──────────────────────────────────────────────
        //  하단 버튼 핸들러 (Reset / Back)
        // ──────────────────────────────────────────────

        /// <summary>
        /// DevMode 씬을 다시 로드하여 Player + 적(봇)을 초기 상태로 spawn한다.
        /// NetworkManager.RestartDevMode가 Runner.Shutdown await 후 씬 reload를 처리해
        /// race condition (No Cameras Rendering / Back 버튼 미응답)을 차단한다.
        /// </summary>
        public async void ResetDevModeState()
        {
#if UNITY_EDITOR
            var nm = NetworkManager.Instance;
            if (nm == null)
            {
                Debug.LogWarning("[DevModeController] ResetDevModeState — NetworkManager.Instance 가 null. 무시.");
                return;
            }

            Debug.Log("[DevModeController] DevMode 상태 초기화 — Runner shutdown + 씬 reload.");
            await nm.RestartDevMode();
#else
            Debug.LogWarning("[DevModeController] ResetDevModeState — 빌드에서는 에디터 전용 기능. 무시.");
            await System.Threading.Tasks.Task.CompletedTask;
#endif
        }

        /// <summary>
        /// 메인 메뉴로 복귀. NetworkRunner 정리 후 MainMenu 씬 로드.
        /// </summary>
        public void BackToMainMenu()
        {
            var nm = NetworkManager.Instance;
            nm?.DisconnectAll();
            Debug.Log("[DevModeController] DevMode 종료 — MainMenu 로드.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }
}
#endif
