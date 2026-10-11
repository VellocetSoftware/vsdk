using System;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_capitron_rack")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Capitron Rack")]
    public sealed class RackMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.capitron-rack";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
