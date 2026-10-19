using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Doors;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("func_door_scaling")]
    [EntityAuthoring("Movement", EntityPlacementKind.Volume, "Scaling Door", Description = "Scale selected brushes or models along an axis.")]
    public sealed class ScalingDoorMarker : DoorMarker
    {
        public enum ScaleAxis
        {
            X, Y, Z
        }
        public override DoorMotionKind MotionKind => DoorMotionKind.Scaling;
        [field: Header("Scaling")]
        [field: SerializeField] public ScaleAxis Axis { get; private set; } = ScaleAxis.Y;
        [field: SerializeField, Min(0)]
        public float TargetScale
        {
            get; private set;
        }
    }
}
