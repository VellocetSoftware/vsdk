using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_hit_surface")]
    [EntityAuthoring("Advanced / Geometry", EntityPlacementKind.Volume, "Hit Surface")]
    public sealed class OperationSurfaceMarker : EntityMarker, IBoundedEntityMarker
    {
        [SerializeField, Tooltip("Operation receiving hits on this independent geometry. Its native damage, repair and attribution rules still apply.")]
        private EntityMarker target;
        public EntityMarker Target => target;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
