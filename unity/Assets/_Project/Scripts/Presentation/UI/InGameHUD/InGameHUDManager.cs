using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Colosseum.Game;

namespace Colosseum.UI
{
    /// <summary>
    /// 인게임 HUD 통합 매니저. Canvas + 하위 HUD 컴포넌트를 코드로 동적 생성.
    /// SegmentProgressHUD / AttackDirectionHUD / CardInventoryHUD 등록 — CardHoverPopup은
    /// CardInventoryHUD가 자체 자식으로 빌드하므로 여기서 직접 만들지 않는다.
    /// </summary>
    public class InGameHUDManager : MonoBehaviour
    {
        private Canvas _canvas;
        private RoomManager _roomManager;
        private SegmentProgressHUD _segmentProgress;
        private AttackDirectionHUD _attackDirection;
        private CardInventoryHUD _cardInventoryP1;
        private CardInventoryHUD _cardInventoryP2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneListener()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            // AfterSceneLoad 시점엔 첫 씬이 이미 로드 완료 — sceneLoaded 이벤트를
            // 놓쳤을 수 있으니 현재 active scene 1회 즉시 처리. 중복 가드는 OnSceneLoaded 내부.
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.IsValid() && active.isLoaded)
            {
                OnSceneLoaded(active, UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (FindObjectOfType<InGameHUDManager>() != null) return;

            if (scene.name == "MainMenu") return;

            GameObject go = new GameObject("InGameHUDManager (auto)");
            go.AddComponent<InGameHUDManager>();
        }

        private void Start()
        {
            StartCoroutine(WaitForRoomManagerThenSetup());
        }

        private IEnumerator WaitForRoomManagerThenSetup()
        {
            int maxFrames = 1800;
            int frame = 0;
            while (_roomManager == null && frame < maxFrames)
            {
                _roomManager = FindObjectOfType<RoomManager>();
                if (_roomManager != null) break;
                frame++;
                yield return null;
            }

            if (_roomManager == null)
            {
                Debug.LogWarning("[InGameHUDManager] RoomManager not found within 30s. HUD disabled.");
                gameObject.SetActive(false);
                yield break;
            }

            CreateCanvas();
            CreateSegmentProgressHUD();
            CreateAttackDirectionHUD();
            CreateCardInventoryHUD();

            // 옛 RoomIndicatorUI 비활성 — 새 SegmentProgressHUD 와 시각 중복.
            // 메인 Canvas 같은 공유 GameObject 에 부착된 케이스가 있어 GameObject.SetActive(false) 는 금지
            // (Canvas/CardSelectionUI/GameOverUI 등을 동반 비활성화시킴). component.enabled = false 만 끔.
            var oldIndicators = FindObjectsOfType<RoomIndicatorUI>(true);
            foreach (var ind in oldIndicators)
            {
                if (ind != null && ind.enabled)
                {
                    Debug.Log($"[InGameHUDManager] 옛 RoomIndicatorUI '{ind.gameObject.name}' 비활성화 (새 SegmentProgressHUD 와 중복).");
                    ind.enabled = false;
                }
            }

            // 옛 'RoomIndicator' GameObject(자식 RoomText 가 default "New Text" 그대로 화면 상단에 떠있는 잔재) 비활성화.
            // RoomIndicatorUI 컴포넌트가 부착된 GameObject 와는 별도 — 어떤 코드도 RoomText 를 채우지 않으며,
            // 새 SegmentProgressHUD 위에 거슬려 표시됨. 씬 편집 회피 위해 런타임 처리.
            var oldRoomIndicatorGO = GameObject.Find("RoomIndicator");
            if (oldRoomIndicatorGO != null && oldRoomIndicatorGO.activeSelf)
            {
                Debug.Log($"[InGameHUDManager] 옛 'RoomIndicator' GameObject 비활성화 (RoomText 'New Text' 잔재).");
                oldRoomIndicatorGO.SetActive(false);
            }
        }

        private void CreateCanvas()
        {
            GameObject canvasObj = new GameObject("InGameHUDCanvas");
            canvasObj.transform.SetParent(transform, false);

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        private void CreateSegmentProgressHUD()
        {
            GameObject obj = new GameObject("SegmentProgressHUD");
            obj.transform.SetParent(_canvas.transform, false);
            _segmentProgress = obj.AddComponent<SegmentProgressHUD>();
            _segmentProgress.Init(_roomManager);
        }

        private void CreateAttackDirectionHUD()
        {
            GameObject obj = new GameObject("AttackDirectionHUD");
            obj.transform.SetParent(_canvas.transform, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _attackDirection = obj.AddComponent<AttackDirectionHUD>();
            _attackDirection.Init(_roomManager);
        }

        private void CreateCardInventoryHUD()
        {
            GameObject p1Obj = new GameObject("CardInventoryP1");
            p1Obj.transform.SetParent(_canvas.transform, false);
            _cardInventoryP1 = p1Obj.AddComponent<CardInventoryHUD>();
            _cardInventoryP1.Init(0, _roomManager);

            GameObject p2Obj = new GameObject("CardInventoryP2");
            p2Obj.transform.SetParent(_canvas.transform, false);
            _cardInventoryP2 = p2Obj.AddComponent<CardInventoryHUD>();
            _cardInventoryP2.Init(1, _roomManager);
        }
    }
}
