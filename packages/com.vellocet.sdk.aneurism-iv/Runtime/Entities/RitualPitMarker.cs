using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_ritual_pit")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Ritual Pit")]
    public sealed class RitualPitMarker : PrefabEntityMarker
    {
        public enum Kind { Standard, Volume }
        [SerializeField] private Kind kind;
        [SerializeField] private string allowedFate = "zealot";
        public Kind Variant => kind;
        public string AllowedFate => allowedFate;
        public override string ArchetypeId => kind == Kind.Volume ? "aniv.ritual-pit.volume" : "aniv.ritual-pit";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
