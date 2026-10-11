using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("info_aniv_fishing_zone")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Fishing Zone")]
    public sealed class FishingZoneMarker : PrefabEntityMarker
    {
        [SerializeField, Tooltip("Ordered fishing area vertices relative to this entity.")]
        private Vector3[] polygonVertices = { new(-4, 0, -4), new(4, 0, -4), new(4, 0, 4), new(-4, 0, 4) };
        public IReadOnlyList<Vector3> Vertices => polygonVertices;
        public override string ArchetypeId => "aniv.fishing-zone";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
