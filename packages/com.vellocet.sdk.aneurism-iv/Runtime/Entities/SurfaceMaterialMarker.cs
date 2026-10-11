using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_aniv_surface")]
    [EntityAuthoring("Geometry", EntityPlacementKind.Logic, "Surface Material")]
    public sealed class SurfaceMaterialMarker : EntityMarker, IEntityImportGroup
    {
        public enum MaterialKind
        {
            Void, Wood, Dirt, Gravel, Fabric, Metal, Glass, Flesh, Stone, Tile, Plastic, Concrete, Asphalt, Brick, Soil
        }

        [SerializeField, Tooltip("Fallback material for collision sounds when the renderer does not specify one.")]
        private MaterialKind material;
        [SerializeField, Tooltip("Apply this material to owned child colliders as well as this object's colliders.")]
        private bool includeChildren = true;
        public bool IncludeChildren => includeChildren;
        public MaterialKind Material => material;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
