#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Colosseum.Editor.DuskCity
{
    /// <summary>
    /// DuskCity Scene View 보조선 (Editor 전용 Gizmo).
    /// S2~S6 segment boundary + 각 segment 4분할 기준 X 좌표를 vertical line + label 로 표시.
    /// 사용자가 prop 배치 시 segment 좌표 참고용.
    ///
    /// - DuskCity.unity 한정 (다른 scene 에서는 그리지 않음)
    /// - EditorPrefs 로 on/off 상태 저장 (재시작 후에도 유지)
    /// - Menu: Colosseum → DuskCity → Toggle Scene Guides (Ctrl+Shift+Alt+J)
    /// - Handles.zTest=Always 라 GameObject 뒤에 가려지지 않음
    ///
    /// 색상: boundary=yellow(굵게) · quarter=cyan(가늘게)
    ///
    /// MVPArenaSceneGuides 와 격리 (각 scene 별 별도 도구). segment X 좌표는 baseline
    /// 단계에서 동일 SegmentProfile 복제했으므로 Lines[] 배열도 동일.
    /// </summary>
    [InitializeOnLoad]
    public static class DuskCitySceneGuides
    {
        private const string TargetSceneSuffix = "/DuskCity.unity";
        private const string PrefKey = "Colosseum.DuskCity.SceneGuides.Enabled";
        // Alt + J. MVPArena (H) 와 충돌 회피, Unity 기본 shortcut 회피.
        private const string MenuPath = "Colosseum/DuskCity/Toggle Scene Guides %#&j";
        private const string LogPrefix = "[DuskCityGuides]";
        private const float YMin = -10f;
        private const float YMax = 30f;

        // (label, X, isBoundary)
        // S2~S6 width=104 unit, quarter step=26
        private static readonly (string name, float x, bool isBoundary)[] Lines = new[]
        {
            // boundaries (yellow, bold)
            ("S1|S2  -260", -260f, true),
            ("S2|S3  -156", -156f, true),
            ("S3|S4   -52",  -52f, true),
            ("S4|S5    52",   52f, true),
            ("S5|S6   156",  156f, true),
            ("S6|S7   260",  260f, true),
            // S2 quarters
            ("S2 1/4 -234", -234f, false),
            ("S2 2/4 -208", -208f, false),
            ("S2 3/4 -182", -182f, false),
            // S3 quarters
            ("S3 1/4 -130", -130f, false),
            ("S3 2/4 -104", -104f, false),
            ("S3 3/4  -78",  -78f, false),
            // S4 quarters
            ("S4 1/4  -26",  -26f, false),
            ("S4 2/4    0",    0f, false),
            ("S4 3/4   26",   26f, false),
            // S5 quarters
            ("S5 1/4   78",   78f, false),
            ("S5 2/4  104",  104f, false),
            ("S5 3/4  130",  130f, false),
            // S6 quarters
            ("S6 1/4  182",  182f, false),
            ("S6 2/4  208",  208f, false),
            ("S6 3/4  234",  234f, false),
        };

        // Default OFF — 다른 팀원이 DuskCity scene 처음 열 때 보조선 surprise 방지.
        // 사용자가 명시적으로 toggle(Ctrl+Shift+Alt+J)하면 EditorPrefs 에 ON 저장 (per-user).
        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, false);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static DuskCitySceneGuides()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        [MenuItem(MenuPath)]
        public static void Toggle()
        {
            Enabled = !Enabled;
            SceneView.RepaintAll();
            Debug.Log($"{LogPrefix} Scene guides {(Enabled ? "ON" : "OFF")}");
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static GUIStyle _boundaryLabelStyle;
        private static GUIStyle _quarterLabelStyle;

        private static void EnsureStyles()
        {
            if (_boundaryLabelStyle == null)
            {
                _boundaryLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.LowerCenter,
                    fontSize = 11,
                };
                _boundaryLabelStyle.normal.textColor = new Color(1f, 0.85f, 0.2f, 1f);
            }
            if (_quarterLabelStyle == null)
            {
                _quarterLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.LowerCenter,
                    fontSize = 11,
                };
                _quarterLabelStyle.normal.textColor = new Color(0.65f, 0.85f, 1f, 1f);
            }
        }

        private static void OnSceneGUI(SceneView sv)
        {
            if (!Enabled) return;
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith(TargetSceneSuffix)) return;

            EnsureStyles();

            var prevColor = Handles.color;
            var prevZ = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            foreach (var line in Lines)
            {
                Handles.color = line.isBoundary
                    ? new Color(1f, 0.85f, 0.2f, 0.95f) // yellow
                    : new Color(0.4f, 0.75f, 1f, 0.9f); // cyan (bumped to match yellow visibility)
                Vector3 p1 = new Vector3(line.x, YMin, 0f);
                Vector3 p2 = new Vector3(line.x, YMax, 0f);
                float thickness = line.isBoundary ? 2.5f : 2.2f;
                Handles.DrawAAPolyLine(thickness, p1, p2);

                Vector3 labelPos = new Vector3(line.x, YMax + 0.5f, 0f);
                Handles.Label(labelPos, line.name,
                    line.isBoundary ? _boundaryLabelStyle : _quarterLabelStyle);
            }

            Handles.color = prevColor;
            Handles.zTest = prevZ;
        }
    }
}
#endif
