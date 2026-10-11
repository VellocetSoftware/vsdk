using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_soul_fetcher")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Soul Fetcher")]
    public sealed class SoulFetcherMarker : PrefabEntityMarker
    {
        public enum AnimationKind { SoulFetcher, Player }
        [SerializeField] private AnimationKind animation;
        public AnimationKind Animation => animation;
        [Tooltip("Stable Fate ID; empty leaves the fetcher unassigned.")]
        [SerializeField] private string fate = "prole";
        public string FateId => fate;
        public override string ArchetypeId => "aniv.soul-fetcher";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
