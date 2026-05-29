namespace Colosseum.Contracts
{
    /// <summary>
    /// 맵 선택 UI(MainMenuUI)와 NetworkManager DevMode 라우팅 양쪽에서 import. PlayerPrefs int와 영구 연결.
    /// 빌드 인덱스 변경 시 EditorBuildSettings.asset 정합 갱신 필수.
    /// Unspecified=0 sentinel: PlayerPrefs default(0) = 첫 진입. user-selectable 아님 — extension fallback에서 MVPArena로 코어션.
    /// 재정렬 금지, append-only (PlayerPrefs 영구 호환 보존).
    /// </summary>
    public enum SelectableScene
    {
        Unspecified = 0,
        SampleScene = 1,
        MVPArena = 2,
        DuskCity = 3,
    }

    public static class SelectableSceneExtensions
    {
        public const string SampleSceneName = "SampleScene";
        public const string MVPArenaSceneName = "MVPArena";
        public const string DuskCitySceneName = "DuskCity";
        public const string DefaultSceneName = MVPArenaSceneName;

        /// <summary>SceneManager.LoadScene / SceneUtility.GetBuildIndexByScenePath 인자용 full path. 정의 외 enum 값은 MVPArena fallback.</summary>
        public static string ToScenePath(this SelectableScene s) => s switch
        {
            SelectableScene.SampleScene => "Assets/Scenes/SampleScene.unity",
            SelectableScene.MVPArena => "Assets/_Project/Scenes/Greybox/MVPArena.unity",
            SelectableScene.DuskCity => "Assets/_Project/Scenes/Greybox/DuskCity.unity",
            _ => "Assets/_Project/Scenes/Greybox/MVPArena.unity",
        };

        /// <summary>프로토콜 / CLI 인자용 scene name only. 정의 외 MVPArena fallback.</summary>
        public static string ToSceneName(this SelectableScene s) => s switch
        {
            SelectableScene.SampleScene => SampleSceneName,
            SelectableScene.MVPArena => MVPArenaSceneName,
            SelectableScene.DuskCity => DuskCitySceneName,
            _ => DefaultSceneName,
        };

        /// <summary>
        /// 로비 프로토콜/CLI의 name-only 값과 Unity full path 값을 Build Settings path로 정규화한다.
        /// 정의 외 값은 MVPArena fallback.
        /// </summary>
        public static string ToBuildScenePath(string sceneNameOrPath)
        {
            string normalized = string.IsNullOrWhiteSpace(sceneNameOrPath)
                ? string.Empty
                : sceneNameOrPath.Trim().Replace('\\', '/');

            if (string.Equals(normalized, SelectableScene.SampleScene.ToSceneName(), System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, SelectableScene.SampleScene.ToScenePath(), System.StringComparison.OrdinalIgnoreCase))
                return SelectableScene.SampleScene.ToScenePath();

            if (string.Equals(normalized, SelectableScene.MVPArena.ToSceneName(), System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, SelectableScene.MVPArena.ToScenePath(), System.StringComparison.OrdinalIgnoreCase))
                return SelectableScene.MVPArena.ToScenePath();

            if (string.Equals(normalized, SelectableScene.DuskCity.ToSceneName(), System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, SelectableScene.DuskCity.ToScenePath(), System.StringComparison.OrdinalIgnoreCase))
                return SelectableScene.DuskCity.ToScenePath();

            return SelectableScene.MVPArena.ToScenePath();
        }

        /// <summary>UI 표시용 라벨. Unspecified는 "Unknown"으로 표시되어 디버깅 컨텍스트 보존.</summary>
        public static string ToDisplayName(this SelectableScene s) => s switch
        {
            SelectableScene.SampleScene => "Sample Arena (legacy)",
            SelectableScene.MVPArena => "MVP Arena (greybox)",
            SelectableScene.DuskCity => "Dusk City (greybox)",
            _ => "Unknown",
        };
    }
}
