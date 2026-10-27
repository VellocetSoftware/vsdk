using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_neuroconsole")]
    [EntityAuthoring("Infrastructure", EntityPlacementKind.Point, "Neuroconsole")]
    public sealed class NeuroconsoleMarker : WaypointPrefabMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.neuroconsole";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("open_ui", EntitySignalValueType.None);
            GameOperationContracts.Utility(builder);
        }
    }
}
