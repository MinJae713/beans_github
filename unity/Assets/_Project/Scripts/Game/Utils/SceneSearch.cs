using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace Colosseum.Utils
{
    /// <summary>
    /// 씬 범위 한정 오브젝트 검색 유틸리티.
    ///
    /// FindObjectOfType / FindObjectsOfType은 로드된 모든 씬을 전역 검색하므로,
    /// 서버 프로세스에서 방(Runner)별로 별도 씬을 로드하는 구조에서는
    /// 다른 방의 오브젝트를 잘못 잡는 문제가 발생한다.
    ///
    /// 이 클래스는 지정된 Scene 내의 루트 오브젝트만 순회하여
    /// 해당 씬에 속한 컴포넌트만 반환한다.
    ///
    /// 사용법:
    ///   var rm = SceneSearch.FindInScene&lt;RoomManager&gt;(gameObject.scene);
    ///   var all = SceneSearch.FindAllInScene&lt;PlayerHealth&gt;(gameObject.scene);
    /// </summary>
    public static class SceneSearch
    {
        /// <summary>
        /// 지정 씬 내에서 T 타입 컴포넌트를 하나 찾아 반환.
        /// 못 찾으면 null.
        /// </summary>
        public static T FindInScene<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid()) return null;
            foreach (var go in scene.GetRootGameObjects())
            {
                var component = go.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        /// <summary>
        /// 지정 씬 내에서 T 타입 컴포넌트를 모두 찾아 배열로 반환.
        /// </summary>
        public static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            var results = new List<T>();
            if (!scene.IsValid()) return results.ToArray();
            foreach (var go in scene.GetRootGameObjects())
            {
                results.AddRange(go.GetComponentsInChildren<T>(true));
            }
            return results.ToArray();
        }
    }
}
