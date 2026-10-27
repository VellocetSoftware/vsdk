using System;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_toilet")]
    [EntityAuthoring("Services", EntityPlacementKind.Point, "Toilet")]
    public sealed class ToiletMarker : PrefabEntityMarker
    {
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            OperationSignalContracts.Processing(builder);
        }
        public override string ArchetypeId => "aniv.toilet";
    }
}
