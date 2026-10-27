using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_laundry_chute")]
    [EntityAuthoring("Laundry", EntityPlacementKind.Point, "Laundry Chute", Description = "Native timed laundry supply point. Link a request button to request a new delivery.")]
    public sealed class LaundryChuteMarker : PrefabEntityMarker
    {
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("request", EntitySignalValueType.None);
            builder.AddOutput("completed", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("busy", EntitySignalValueType.Bool, true, false);
        }
        public override string ArchetypeId => "aniv.laundry-chute";
    }
}
