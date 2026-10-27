using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_laundry_machine")]
    [EntityAuthoring("Laundry", EntityPlacementKind.Point, "Laundry Machine", Description = "Place a washer or dryer. Native admission, door animation, conversion timing and capacity remain game-owned.")]
    public sealed class LaundryMachineMarker : PrefabEntityMarker
    {
        public enum Kind { Washer, Dryer }
        [SerializeField] private Kind kind;
        public Kind Variant => kind;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            OperationSignalContracts.Processing(builder);
            builder.AddProperty("busy", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("progress", EntitySignalValueType.Float, true, false);
            builder.AddProperty("is_open", EntitySignalValueType.Bool, true, false);
        }
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.laundry." + kind.ToString().ToLowerInvariant();
    }
}
