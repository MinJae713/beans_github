#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace Colosseum.Editor
{
    /// <summary>
    /// 서버/클라이언트 빌드를 Unity Editor 메뉴에서 실행.
    /// 메뉴: Colosseum > Build Server / Build Client / Build Both
    ///
    /// 빌드 결과:
    ///   Builds/Server/Colosseum-Server.exe   (Windows Dedicated Server, headless)
    ///   Builds/Client/Colosseum.exe           (Windows Standalone)
    /// </summary>
    public static class BuildHelper
    {
        private static readonly string ServerBuildPath = "Builds/Server";
        private static readonly string ClientBuildPath = "Builds/Client";

        private static string[] GetBuildScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }

        // ──────────────────────────────────────────────
        //  서버 빌드 (Windows + Server 서브타겟)
        // ──────────────────────────────────────────────

        [MenuItem("Colosseum/Build Server (Windows)", false, 100)]
        public static void BuildServer()
        {
            NetworkGameplayUnsafeBuildGuard.ValidateOrThrow();

            string[] scenes = GetBuildScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildHelper] No scenes in Build Settings!");
                return;
            }

            string outputPath = Path.Combine(ServerBuildPath, "Colosseum-Server.exe");
            Directory.CreateDirectory(ServerBuildPath);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
#if UNITY_2021_2_OR_NEWER
                subtarget = (int)StandaloneBuildSubtarget.Server,
#endif
                options = BuildOptions.None
            };

            var result = BuildPipeline.BuildPlayer(options);

            if (result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[BuildHelper] Server build succeeded! → {outputPath}");
                Debug.Log($"[BuildHelper] 실행: {outputPath} --server");
            }
            else
            {
                Debug.LogError($"[BuildHelper] Server build failed: {result.summary.totalErrors} errors");
            }
        }

        // ──────────────────────────────────────────────
        //  클라이언트 빌드 (Windows Standalone)
        // ──────────────────────────────────────────────

        [MenuItem("Colosseum/Build Client (Windows)", false, 101)]
        public static void BuildClient()
        {
            NetworkGameplayUnsafeBuildGuard.ValidateOrThrow();

            string[] scenes = GetBuildScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildHelper] No scenes in Build Settings!");
                return;
            }

            string outputPath = Path.Combine(ClientBuildPath, "Colosseum.exe");
            Directory.CreateDirectory(ClientBuildPath);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
#if UNITY_2021_2_OR_NEWER
                subtarget = (int)StandaloneBuildSubtarget.Player,
#endif
                options = BuildOptions.None
            };

            var result = BuildPipeline.BuildPlayer(options);

            if (result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[BuildHelper] Client build succeeded! → {outputPath}");
            }
            else
            {
                Debug.LogError($"[BuildHelper] Client build failed: {result.summary.totalErrors} errors");
            }
        }

        // ──────────────────────────────────────────────
        //  둘 다 빌드
        // ──────────────────────────────────────────────

        [MenuItem("Colosseum/Build Both (Server + Client)", false, 102)]
        public static void BuildBoth()
        {
            Debug.Log("[BuildHelper] Building Server...");
            BuildServer();

            Debug.Log("[BuildHelper] Building Client...");
            BuildClient();

            Debug.Log("[BuildHelper] All builds complete!");
        }

        // ──────────────────────────────────────────────
        //  빌드 폴더 열기
        // ──────────────────────────────────────────────

        [MenuItem("Colosseum/Open Build Folder", false, 200)]
        public static void OpenBuildFolder()
        {
            string fullPath = Path.GetFullPath("Builds");
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
            EditorUtility.RevealInFinder(fullPath);
        }

        // ──────────────────────────────────────────────
        //  Linux 서버 빌드 (EC2 배포용)
        // ──────────────────────────────────────────────

        [MenuItem("Colosseum/Build Server (Linux - EC2)", false, 103)]
        public static void BuildServerLinux()
        {
            NetworkGameplayUnsafeBuildGuard.ValidateOrThrow();

            string[] scenes = GetBuildScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildHelper] No scenes in Build Settings!");
                return;
            }

            string linuxPath = "Builds/Server-Linux";
            string outputPath = Path.Combine(linuxPath, "Colosseum-Server");
            Directory.CreateDirectory(linuxPath);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneLinux64,
#if UNITY_2021_2_OR_NEWER
                subtarget = (int)StandaloneBuildSubtarget.Server,
#endif
                options = BuildOptions.None
            };

            var result = BuildPipeline.BuildPlayer(options);

            if (result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[BuildHelper] Linux Server build succeeded! → {outputPath}");
                Debug.Log("[BuildHelper] EC2에 업로드 후: chmod +x Colosseum-Server && ./Colosseum-Server --server");
            }
            else
            {
                Debug.LogError($"[BuildHelper] Linux Server build failed: {result.summary.totalErrors} errors");
            }
        }
    }
}
#endif
