using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Doors;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("func_door_rotating")]
    [EntityAuthoring("Movement", EntityPlacementKind.Volume, "Rotating Door", Description = "Rotate selected brushes or models around an authored hinge.")]
    public sealed class RotatingDoorMarker : DoorMarker
    {
        public override DoorMotionKind MotionKind => DoorMotionKind.Rotating;
        [field: Header("Rotation")]
        [field: SerializeField] public Vector3 RotationAxis { get; private set; } = Vector3.up;
        [field: SerializeField] public float Angle { get; private set; } = 90;
    }
}
