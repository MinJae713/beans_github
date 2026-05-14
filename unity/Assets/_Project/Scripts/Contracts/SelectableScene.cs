namespace Colosseum.Contracts
{
    /// <summary>
    /// 맵 선택 UI(MainMenuUI)와 NetworkManager DevMode 라우팅 양쪽에서 import. PlayerPrefs int와 영구 연결.
    /// 빌드 인덱스 변경 시 EditorBuildSettings.asset 정합 갱신 필수.
    /// Unspecified=0 sentinel: PlayerPrefs default(0) = 첫 진입. user-selectable 아님 — extension fallback에서 SampleScene으로 코어션.
    /// 재정렬 금지, append-only (PlayerPrefs 영구 호환 보존).
    /// </summary>
    public enum SelectableScene
    {
        Unspecified = 0,
        SampleScene = 1,
        MVPArena = 2,
    }

    public static class SelectableSceneExtensions
    {
        /// <summary>SceneManager.LoadScene 인자용 full path. 정의 외 enum 값은 SampleScene fallback.</summary>
        public static string ToScenePath(this SelectableScene s) => s switch
        {
            SelectableScene.SampleScene => "Assets/Scenes/SampleScene.unity",
            SelectableScene.MVPArena => "Assets/_Project/Scenes/Greybox/MVPArena.unity",
            _ => "Assets/Scenes/SampleScene.unity",
        };

        /// <summary>SceneUtility.GetBuildIndexByScenePath / CLI 인자용 scene name only. 정의 외 SampleScene fallback.</summary>
        public static string ToSceneName(this SelectableScene s) => s switch
        {
            SelectableScene.SampleScene => "SampleScene",
            SelectableScene.MVPArena => "MVPArena",
            _ => "SampleScene",
        };

        /// <summary>UI 표시용 라벨. Unspecified는 "Unknown"으로 표시되어 디버깅 컨텍스트 보존.</summary>
        public static string ToDisplayName(this SelectableScene s) => s switch
        {
            SelectableScene.SampleScene => "Sample Arena (legacy)",
            SelectableScene.MVPArena => "MVP Arena (greybox)",
            _ => "Unknown",
        };
    }
}
