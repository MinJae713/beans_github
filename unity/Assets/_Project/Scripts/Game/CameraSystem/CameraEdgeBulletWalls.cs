using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace Colosseum.CameraSystem
{
    /// <summary>
    /// 카메라에 부착되어 ortho 영역 4변에 invisible Trigger BoxCollider2D 4개를 자동 생성·동기화한다.
    /// 총알이 화면 밖으로 못 나가게 막는 "카메라 경계 = 총알 벽" 시스템의 코어.
    ///
    /// 동작:
    ///   Awake: Camera 컴포넌트 캐시 + 4개 자식 GameObject 생성 (각 GameObject에 BoxCollider2D + CameraEdgeWall 부착, isTrigger=true)
    ///   LateUpdate: cam.orthographicSize, cam.aspect, padding 기반으로 4 collider 위치·크기 갱신
    ///
    /// 캐릭터 영향 0:
    ///   - Trigger collider라 캐릭터는 물리적으로 통과
    ///   - 캐릭터의 OnTriggerEnter2D는 CameraEdgeWall을 무시 (PlayerController/PlayerHealth가 검사 안 함)
    ///
    /// 총알 처리:
    ///   - BulletController 자식 trigger collider가 본 trigger와 만나면 OnTriggerEnter2D 발화
    ///   - BulletController.OnHitTrigger가 GetComponent&lt;CameraEdgeWall&gt;() 분기로 처리
    ///   - 일반탄 → Runner.Despawn / 반사탄 → velocity 반사 + BounceCount 차감
    ///
    /// 네트워크 결정론:
    ///   - 카메라 위치는 RoomManager.CurrentRoomIndex 기반 (모든 클라 같은 값) + 넓은 방 추적 (NetworkObject 기반)
    ///   - BulletController는 SA 한정 충돌 처리 (HasStateAuthority 가드)
    /// </summary>
    [RequireComponent(typeof(UnityCamera))]
    public class CameraEdgeBulletWalls : MonoBehaviour
    {
        [Header("Edge Wall 설정")]
        [Tooltip("카메라 ortho 영역 끝에서 안쪽으로 패딩 (월드 단위). default 0 = 화면 끝 정확. 비주얼팀 부딪힘 VFX 튜닝 시 사용.")]
        [Min(0f)]
        [SerializeField] private float _padding = 0f;

        [Tooltip("collider 두께 (월드 단위). 너무 얇으면 빠른 총알이 통과할 수 있음. default 1m.")]
        [Min(0.1f)]
        [SerializeField] private float _wallThickness = 1f;

        private UnityCamera _cam;
        private BoxCollider2D _leftCollider;
        private BoxCollider2D _rightCollider;
        private BoxCollider2D _topCollider;
        private BoxCollider2D _bottomCollider;

        private void Awake()
        {
            _cam = GetComponent<UnityCamera>();
            if (_cam == null)
            {
                Debug.LogError("[CameraEdgeBulletWalls] Camera 컴포넌트가 같은 GameObject에 없음. RequireComponent 우회됨.");
                enabled = false;
                return;
            }

            _leftCollider = CreateEdge("BulletWall_Left", CameraEdgeWall.EdgeSide.Left);
            _rightCollider = CreateEdge("BulletWall_Right", CameraEdgeWall.EdgeSide.Right);
            _topCollider = CreateEdge("BulletWall_Top", CameraEdgeWall.EdgeSide.Top);
            _bottomCollider = CreateEdge("BulletWall_Bottom", CameraEdgeWall.EdgeSide.Bottom);
        }

        private BoxCollider2D CreateEdge(string objName, CameraEdgeWall.EdgeSide side)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false); // 카메라 자식

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true; // 캐릭터는 통과, 총알 trigger 자식과 OnTriggerEnter2D로 만남

            var marker = go.AddComponent<CameraEdgeWall>();
            marker.Configure(side);

            return col;
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            if (!_cam.orthographic) return; // 본 게임은 2D ortho 전제

            // ortho 기반 화면 절반 크기 (월드 단위)
            float halfHeight = _cam.orthographicSize - _padding;
            float halfWidth = _cam.orthographicSize * _cam.aspect - _padding;
            if (halfHeight <= 0f || halfWidth <= 0f) return; // 패딩이 너무 커서 음수면 무시

            // collider는 카메라 transform 기준 local. 카메라 자식이라 카메라 이동 자동 추적.
            // 좌측 변: x=-halfWidth, y=0, size=(thickness, halfHeight*2 + thickness)
            UpdateEdge(_leftCollider,  new Vector2(-halfWidth, 0f),    new Vector2(_wallThickness, halfHeight * 2f + _wallThickness));
            UpdateEdge(_rightCollider, new Vector2( halfWidth, 0f),    new Vector2(_wallThickness, halfHeight * 2f + _wallThickness));
            UpdateEdge(_topCollider,   new Vector2(0f,  halfHeight),   new Vector2(halfWidth * 2f + _wallThickness, _wallThickness));
            UpdateEdge(_bottomCollider,new Vector2(0f, -halfHeight),   new Vector2(halfWidth * 2f + _wallThickness, _wallThickness));
        }

        private static void UpdateEdge(BoxCollider2D col, Vector2 localPos, Vector2 size)
        {
            if (col == null) return;
            col.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
            col.size = size;
        }
    }
}
