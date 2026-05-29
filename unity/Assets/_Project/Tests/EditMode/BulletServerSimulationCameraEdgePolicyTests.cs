using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Colosseum.Tests.EditMode
{
    public sealed class BulletServerSimulationCameraEdgePolicyTests
    {
        [Test]
        public void CameraEdgePolicy_ReflectsOnlyWhenBounceRemains()
        {
            string source = ReadBulletServerSimulationSource();

            Assert.That(source, Does.Contain("CameraEdgeWall cameraEdge = hit.collider.GetComponent<CameraEdgeWall>();"));
            Assert.That(source, Does.Contain("TryHandleCameraEdgeHit(e, hit, cameraEdge)"));
            Assert.That(source, Does.Contain("if (cameraEdge == null || cameraEdge.Side == CameraEdgeWall.EdgeSide.Bottom || e.BounceRemaining <= 0)"));
            Assert.That(source, Does.Contain("return false;"));
            Assert.That(source, Does.Contain("return HandleBounce(e, contactPoint, normal);"));
        }

        [Test]
        public void CameraEdgePolicy_PassThroughStillFallsThroughToLifetime()
        {
            string source = ReadBulletServerSimulationSource().Replace("\r\n", "\n");

            Assert.That(source, Does.Contain(
                "if (isCameraEdge)\n" +
                "                    {\n" +
                "                        if (TryHandleCameraEdgeHit(e, hit, cameraEdge))\n" +
                "                        {\n" +
                "                            continue;\n" +
                "                        }\n" +
                "                    }\n" +
                "                    else"));
        }

        [Test]
        public void CameraEdgePolicy_UsesDeterministicEdgeNormals()
        {
            string source = ReadBulletServerSimulationSource();

            Assert.That(source, Does.Contain("case CameraEdgeWall.EdgeSide.Left:"));
            Assert.That(source, Does.Contain("return Vector2.right;"));
            Assert.That(source, Does.Contain("case CameraEdgeWall.EdgeSide.Right:"));
            Assert.That(source, Does.Contain("return Vector2.left;"));
            Assert.That(source, Does.Contain("case CameraEdgeWall.EdgeSide.Top:"));
            Assert.That(source, Does.Contain("return Vector2.down;"));
            Assert.That(source, Does.Contain("case CameraEdgeWall.EdgeSide.Bottom:"));
            Assert.That(source, Does.Contain("return Vector2.zero;"));
        }

        [Test]
        public void WallAndCameraEdgeBounce_ShareSizeScaledOffset()
        {
            string source = ReadBulletServerSimulationSource();

            Assert.That(source, Does.Contain("return HandleBounce(e, hit.point, hit.normal);"));
            Assert.That(source, Does.Contain("float bounceCastRadius = 0.04f * Mathf.Max(e.SizeMultiplier, 0.001f);"));
            Assert.That(source, Does.Contain("contactPoint + bounceNormal * (bounceCastRadius + 0.02f)"));
        }

        [Test]
        public void CameraEdgePolicy_OffsetsFromInnerColliderSurface()
        {
            string source = ReadBulletServerSimulationSource();

            Assert.That(source, Does.Contain("GetCameraEdgeHalfThickness(cameraEdge, normal)"));
            Assert.That(source, Does.Contain("edgeCenter.x + normal.x * halfThickness"));
            Assert.That(source, Does.Contain("edgeCenter.y + normal.y * halfThickness"));
            Assert.That(source, Does.Contain("Camera edge contact fallback used"));
            Assert.That(source, Does.Contain("Mathf.Abs(collider.size.x * lossyScale.x) * 0.5f"));
            Assert.That(source, Does.Contain("Mathf.Abs(collider.size.y * lossyScale.y) * 0.5f"));
        }

        [Test]
        public void CameraEdgeWalls_ArePlacedOutsideVisibleCameraEdge()
        {
            string source = ReadCameraEdgeBulletWallsSource().Replace("\r\n", "\n");

            Assert.That(source, Does.Contain("float halfThickness = _wallThickness * 0.5f;"));
            Assert.That(source, Does.Contain("new Vector2(-halfWidth - halfThickness, 0f)"));
            Assert.That(source, Does.Contain("new Vector2( halfWidth + halfThickness, 0f)"));
            Assert.That(source, Does.Contain("new Vector2(0f,  halfHeight + halfThickness)"));
            Assert.That(source, Does.Contain("new Vector2(0f, -halfHeight - halfThickness)"));
        }

        private static string ReadBulletServerSimulationSource()
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "NetworkGameplay",
                "Weapon",
                "BulletServerSimulation.cs"));
        }

        private static string ReadCameraEdgeBulletWallsSource()
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "Game",
                "CameraSystem",
                "CameraEdgeBulletWalls.cs"));
        }
    }
}
