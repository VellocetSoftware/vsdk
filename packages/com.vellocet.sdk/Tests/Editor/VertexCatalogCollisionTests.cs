using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Tests
{
    public sealed class VertexCatalogCollisionTests
    {
        [Test]
        public void ExactCollisionWeldsSeamsWithoutChangingTrianglePositions()
        {
            CheckMesh(false);
        }

        [Test]
        public void ConvexCollisionKeepsUnreferencedHullPoints()
        {
            CheckMesh(true);
        }

        private static void CheckMesh(bool convex)
        {
            var owner = new GameObject("Collision catalog fixture");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.forward,
                    Vector3.zero, Vector3.right, Vector3.up, new Vector3(4, 3, 5), new Vector3(1e-7f, 0, 0) },
                triangles = new[] { 0, 2, 1, 4, 5, 3, 5, 6, 3, 6, 4, 3 }
            };
            try
            {
                var collider = owner.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = convex;
                var export = typeof(VertexAssetCatalogExporter).GetMethod("NativeColliders", BindingFlags.NonPublic | BindingFlags.Static);
                var result = ((CatalogCollider[])export.Invoke(null, new object[] { owner })).Single().Shape;
                Assert.That(result.Kind, Is.EqualTo(convex ? "Convex" : "Exact"));
                Assert.That(result.Vertices.Length, Is.EqualTo(6), "Weld exact duplicates while retaining nearby distinct points.");
                Assert.That(result.Vertices.Any(v => v.X == 4 && v.Y == 3 && v.Z == -5), Is.True,
                    "Convex cooking may use points that are not referenced by a triangle.");
                var positions = mesh.vertices;
                var indices = mesh.triangles;
                Assert.That(result.Indices.Length, Is.EqualTo(indices.Length));
                for (var i = 0; i < indices.Length; i += 3)
                    for (var corner = 0; corner < 3; corner++)
                    {
                        var original = positions[indices[i + (corner == 0 ? 0 : 3 - corner)]];
                        var point = result.Vertices[result.Indices[i + corner]];
                        Assert.That(new Vector3((float)point.X, (float)point.Y, (float)point.Z),
                            Is.EqualTo(new Vector3(original.x, original.y, -original.z)));
                    }
                Assert.That(mesh.vertexCount, Is.EqualTo(9), "Export must leave the native Unity mesh unchanged.");
                Assert.That(mesh.triangles, Is.EqualTo(new[] { 0, 2, 1, 4, 5, 3, 5, 6, 3, 6, 4, 3 }));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
