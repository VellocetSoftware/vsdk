using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_loading_bay")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Loading Bay")]
    public sealed class LoadingBayMarker : PrefabEntityMarker
    {
        [SerializeField, Min(1)] private int quotaPerCrate = 4;
        [SerializeField, Tooltip("Items requested by each shipping cycle. An empty list leaves the bay idle.")] private GameItem[] merchandise = Array.Empty<GameItem>();
        public int QuotaPerCrate => quotaPerCrate;
        public System.Collections.Generic.IReadOnlyList<GameItem> Merchandise => merchandise;
        public override string ArchetypeId => "aniv.loading-bay";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
