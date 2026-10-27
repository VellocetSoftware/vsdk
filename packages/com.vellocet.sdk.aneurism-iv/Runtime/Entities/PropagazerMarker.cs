using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_propagazer")]
    [EntityAuthoring("Information", EntityPlacementKind.Point, "Propagazer")]
    public sealed class PropagazerMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.propagazer";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            GameOperationContracts.Utility(builder);
        }
    }
}
