using UnityEngine;

namespace Vellocet.Sdk.Entities.Base
{
    // Bounds use Transform scale, with the origin at the bottom centre of the volume.
    public interface IBoundedEntityMarker { }

    // Centered native prefabs own their colliders; size includes any serialized dimension multiplier.
    public interface ICenteredBoundsEntityMarker
    {
        Vector3 BoundsSize { get; }
    }
}
