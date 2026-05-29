using System.IO;
using System.Reflection;
using Colosseum.Contracts;
using Colosseum.Network;
using NUnit.Framework;
using UnityEngine;

namespace Colosseum.Tests.EditMode
{
    public sealed class SelectableSceneContractTests
    {
        [TestCase("MVPArena", "Assets/_Project/Scenes/Greybox/MVPArena.unity")]
        [TestCase("Assets/_Project/Scenes/Greybox/MVPArena.unity", "Assets/_Project/Scenes/Greybox/MVPArena.unity")]
        [TestCase("SampleScene", "Assets/Scenes/SampleScene.unity")]
        [TestCase("Assets\\Scenes\\SampleScene.unity", "Assets/Scenes/SampleScene.unity")]
        [TestCase("", "Assets/_Project/Scenes/Greybox/MVPArena.unity")]
        [TestCase("UnknownScene", "Assets/_Project/Scenes/Greybox/MVPArena.unity")]
        public void ToBuildScenePath_NormalizesNameOnlyAndFullPathValues(string input, string expected)
        {
            Assert.That(SelectableSceneExtensions.ToBuildScenePath(input), Is.EqualTo(expected));
        }

        [Test]
        public void ServerSceneDefaults_UseMVPArena()
        {
            Assert.That(SelectableSceneExtensions.DefaultSceneName, Is.EqualTo("MVPArena"));

            var initialize = typeof(GameServerManager).GetMethod(nameof(GameServerManager.Initialize));
            var sceneNameParameter = initialize.GetParameters()[3];
            Assert.That(sceneNameParameter.DefaultValue, Is.EqualTo(SelectableSceneExtensions.DefaultSceneName));

            var gameObject = new GameObject("GameServerManager default scene contract");
            try
            {
                var manager = gameObject.AddComponent<GameServerManager>();
                var field = typeof(GameServerManager).GetField("_gameSceneName", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.GetValue(manager), Is.EqualTo(SelectableSceneExtensions.DefaultSceneName));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase("Assets/_Project/Scripts/NetworkGameplay/Network/ServerManager.cs")]
        [TestCase("Assets/_Project/Scripts/NetworkGameplay/Network/HttpWorkerAgentClient.cs")]
        [TestCase("Assets/_Project/Scripts/NetworkGameplay/Network/LobbyServerManager.cs")]
        public void NetworkSceneFallbacks_DoNotUseSampleSceneAsNullDefault(string assetPath)
        {
            string source = File.ReadAllText(ToProjectPath(assetPath));

            Assert.That(source, Does.Not.Contain("?? \"SampleScene\""));
            Assert.That(source, Does.Contain("SelectableSceneExtensions.DefaultSceneName"));
        }

        private static string ToProjectPath(string assetPath)
        {
            string relativeToAssets = assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Application.dataPath, relativeToAssets);
        }
    }
}
