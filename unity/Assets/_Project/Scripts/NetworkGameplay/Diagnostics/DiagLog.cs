using System.Diagnostics;

namespace Colosseum.NetworkGameplay.Diagnostics
{
    /// <summary>
    /// [A303-372] 진단용 DIAG 로그 wrapper.
    ///
    /// [Conditional] attribute 가 caller 측에서 release build (Development Build off + non-Editor)
    /// 의 호출 자체를 stripping 한다. 즉 호출 + argument expression 평가가 모두 제거되어 zero-cost.
    /// string interpolation ($"...") 같은 비용도 같이 사라지므로 release 의 sustained CPU 부담 0.
    ///
    /// 사용 기준:
    ///   - 한 번 발생 (Spawned, Despawned 등) 또는 액션 시점 (점프 발동, 벽 슬라이드 진입/해제, 방 전환 등)
    ///     의 디버깅 가치 있는 로그만 본 wrapper 로 감싼다.
    ///   - 매 tick, 매 발사, 매 hit 같은 고빈도 path 의 디버그 출력은 본 wrapper 로 감싸지 말고 통째로 제거한다.
    ///     compile-time stripping 이라도 코드 가독성과 git diff noise 측면에서 line 자체를 삭제하는 게 낫다.
    /// </summary>
    public static class DiagLog
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string message)
        {
            UnityEngine.Debug.Log(message);
        }
    }
}
