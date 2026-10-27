using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_ration_station")]
    [EntityAuthoring("Food / Rations", EntityPlacementKind.Point, "Ration Station")]
    public sealed class RationStationMarker : PrefabEntityMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.ration-station";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            GameOperationContracts.Utility(builder);
        }
    }
}
