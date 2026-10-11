using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_civil_terminal")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Civil Terminal")]
    public sealed class CivilTerminalMarker : WaypointPrefabMarker
    {
        public override string ArchetypeId => "aniv.civil-terminal";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
