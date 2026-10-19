using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities.Doors
{
    public static class DoorGeometry
    {
        public static bool TryGetLocalBounds(Transform root, Transform space, EntityMarker owner, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh && Owned(filter, owner))
                    Add(filter.sharedMesh.bounds, space.worldToLocalMatrix * filter.transform.localToWorldMatrix, ref bounds, ref found);
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh && Owned(renderer, owner))
                    Add(renderer.localBounds, space.worldToLocalMatrix * renderer.transform.localToWorldMatrix, ref bounds, ref found);
            // Collision-only brush doors are valid, too.
            if (!found)
                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                {
                    if (collider.isTrigger || !Owned(collider, owner))
                        continue;
                    if (collider is BoxCollider box)
                        Add(new Bounds(box.center, box.size), space.worldToLocalMatrix * box.transform.localToWorldMatrix, ref bounds, ref found);
                    else if (collider is MeshCollider mesh && mesh.sharedMesh)
                        Add(mesh.sharedMesh.bounds, space.worldToLocalMatrix * mesh.transform.localToWorldMatrix, ref bounds, ref found);
                    else
                        Add(collider.bounds, space.worldToLocalMatrix, ref bounds, ref found);
                }
            return found;
        }

        private static bool Owned(Component component, EntityMarker owner) => !owner || component.GetComponentInParent<EntityMarker>(true) == owner;

        public static void Add(Bounds source, Matrix4x4 matrix, ref Bounds target, ref bool found)
        {
            for (var i = 0; i < 8; i++)
            {
                var point = matrix.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!found)
                {
                    target = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                    target.Encapsulate(point);
            }
        }

        public static Vector3 PaddedSize(Bounds bounds, Transform space, float worldPadding)
        {
            var inverse = space.worldToLocalMatrix;
            var padding = new Vector3(new Vector3(inverse.m00, inverse.m01, inverse.m02).magnitude,
                new Vector3(inverse.m10, inverse.m11, inverse.m12).magnitude,
                new Vector3(inverse.m20, inverse.m21, inverse.m22).magnitude) * (2 * worldPadding);
            return bounds.size + padding;
        }

        public static OcclusionPortal CreatePortal(DoorMarker marker, Transform anchor, Bounds bounds)
        {
            if (!marker.UseOcclusionPortal)
                return null;
            var portalObject = new GameObject("Door occlusion");
            portalObject.transform.SetParent(anchor, false);
            portalObject.transform.localPosition = bounds.center;
            portalObject.transform.localScale = PaddedSize(bounds, anchor, marker.OcclusionPadding);
            var portal = portalObject.AddComponent<OcclusionPortal>();
            portal.open = marker.StartOpen;
            return portal;
        }
    }
}
