using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_folding_table")]
    [EntityAuthoring("Laundry", EntityPlacementKind.Point, "Folding Table", AvatarModel = "asset:aniv.base/985e817d6863749b8b2ca7710a06ecae.5716299378049042518")]
    public sealed class FoldingTableMarker : PrefabEntityMarker
    {
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            OperationSignalContracts.Processing(builder);
        }
        public override string ArchetypeId => "aniv.folding-table";
    }
}
