using System;
using UnityEngine;
using Vellocet.Framework.Runtime.Content.Surfaces;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("func_surface")]
    [EntityAuthoring("Geometry", EntityPlacementKind.Logic, "Surface")]
    public sealed class SurfaceMarker : EntityMarker, IEntityImportGroup
    {
        [SerializeField] private SurfaceType surfaceType;
        [SerializeField] private Color surfaceColor = Color.white;
        [SerializeField] private bool includeChildren = true;
        public SurfaceType SurfaceType => surfaceType;
        public Color SurfaceColor => surfaceColor;
        public bool IncludeChildren => includeChildren;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;

        private void Start() => BindRuntime();

        public void BindRuntime()
        {
            if (!Enum.IsDefined(typeof(SurfaceType), surfaceType))
                throw new ArgumentOutOfRangeException(nameof(surfaceType));
            foreach (var collider in includeChildren ? GetComponentsInChildren<Collider>(true) : GetComponents<Collider>())
            {
                if (collider.GetComponentInParent<EntityMarker>(true) != this) continue;
                var provider = SurfaceQuerySystem.AddProvider(collider);
                if (enabled) provider.SetSurfaceOverride(new SurfaceInfo(surfaceType, surfaceColor));
                else provider.ClearSurfaceOverride();
            }
        }
    }
}
