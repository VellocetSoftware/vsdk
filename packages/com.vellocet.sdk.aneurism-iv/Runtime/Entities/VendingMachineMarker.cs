using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_vending_machine")]
    [EntityAuthoring("Commerce", EntityPlacementKind.Point, "Soda Vending Machine")]
    public sealed class VendingMachineMarker : OperationEntityMarker
    {
        public enum Kind { VendingMachine, Pharmastributor, FoodStand }
        [SerializeField] private Kind kind;
        [SerializeField] private PropertyMarker property;
        [SerializeField] private bool startFullOnFirstBoot = true;
        public Kind Variant => kind;
        public PropertyMarker Property => property;
        public bool StartFullOnFirstBoot => startFullOnFirstBoot;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.vending." + kind.ToString().ToLowerInvariant();
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            GameOperationContracts.Utility(builder);
            builder.AddInput("purchase", EntitySignalValueType.String);
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddOutput("purchased", EntitySignalValueType.String);
            builder.AddOutput("restocked", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("busy", EntitySignalValueType.Bool, true, false);
        }
    }
}
