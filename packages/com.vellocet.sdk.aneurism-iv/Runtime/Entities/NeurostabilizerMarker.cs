using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_neurostabilizer")]
    [EntityAuthoring("Infrastructure", EntityPlacementKind.Point, "Neurostabilizer")]
    public sealed class NeurostabilizerMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.neurostabilizer";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            GameOperationContracts.Utility(builder);
        }
    }
}
