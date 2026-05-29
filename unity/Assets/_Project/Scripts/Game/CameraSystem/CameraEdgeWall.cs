using UnityEngine;

namespace Colosseum.CameraSystem
{
    /// <summary>
    /// 카메라 ortho 영역 4변에 부착되는 식별 컴포넌트.
    /// CameraEdgeBulletWalls가 카메라 자식으로 4개 GameObject를 동적 생성하면서 각각에 본 컴포넌트를 부착한다.
    ///
    /// BulletServerSimulation 이 CircleCast hit collider 에서 GetComponent&lt;CameraEdgeWall&gt;()로 식별하여
    /// "카메라 경계에 닿았다"는 신호를 받는다. Layer/Tag 변경 없이 컴포넌트 검사만으로 동작 (ProjectSettings 무관).
    ///
    /// Side 필드는 BulletServerSimulation 이 velocity 반사 방향 결정에 사용:
    ///   Left/Right → velocity.x 반전
    ///   Top → velocity.y 반전
    ///   Bottom → 낙하/화면 하단 이탈은 반사하지 않고 통과
    /// </summary>
    public class CameraEdgeWall : MonoBehaviour
    {
        public enum EdgeSide
        {
            Left,
            Right,
            Top,
            Bottom
        }

        [Tooltip("이 경계가 카메라의 어느 변에 위치하는지. CameraEdgeBulletWalls가 4개 자식 생성 시 자동 설정.")]
        [SerializeField] private EdgeSide _side;

        public EdgeSide Side => _side;

        /// <summary>
        /// CameraEdgeBulletWalls가 자식 생성 직후 호출.
        /// </summary>
        public void Configure(EdgeSide side)
        {
            _side = side;
        }
    }
}
