using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_laundry_bin")]
    [EntityAuthoring("Laundry", EntityPlacementKind.Point, "Laundry Bin", Description = "Accepts folded laundry. Rewards and acceptance follow acknowledged item retirement.")]
    public sealed class LaundryBinMarker : PrefabEntityMarker
    {
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("accepted", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
        }
        public override string ArchetypeId => "aniv.laundry-bin";
    }
}
