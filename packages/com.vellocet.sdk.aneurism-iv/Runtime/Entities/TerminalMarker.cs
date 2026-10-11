using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_terminal")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Terminal")]
    public sealed class TerminalMarker : PrefabEntityMarker
    {
        [SerializeField] private PropertyMarker property;
        public PropertyMarker Property => property;
        public override string ArchetypeId => "aniv.terminal";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
