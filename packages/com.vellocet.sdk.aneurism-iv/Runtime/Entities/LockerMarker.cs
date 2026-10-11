using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_locker")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Locker")]
    public sealed class LockerMarker : WaypointPrefabMarker
    {
        public enum Kind { Locker, ScumStash }
        [SerializeField] private Kind kind;
        public Kind Variant => kind;
        public override string ArchetypeId => "aniv.locker." + kind.ToString().ToLowerInvariant();
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
