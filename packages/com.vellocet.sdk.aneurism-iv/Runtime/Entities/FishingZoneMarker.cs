using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_fishing")]
    [EntityAuthoring("Triggers", EntityPlacementKind.Point, "Fishing Zone")]
    public sealed class FishingZoneMarker : PrefabEntityMarker
    {
        [SerializeField, Tooltip("Ordered fishing area vertices relative to this entity.")]
        private Vector3[] polygonVertices = { new(-4, 0, -4), new(4, 0, -4), new(4, 0, 4), new(-4, 0, 4) };
        public IReadOnlyList<Vector3> Vertices => polygonVertices;
        public override string ArchetypeId => "aniv.fishing-zone";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("caught", EntitySignalValueType.String);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddOutput("target_surfaced", EntitySignalValueType.None);
            builder.AddProperty("target_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("active_count", EntitySignalValueType.Int, true, false);
        }
    }
}
