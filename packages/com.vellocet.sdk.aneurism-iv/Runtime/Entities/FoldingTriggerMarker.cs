using System;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_laundry_folding")]
    [EntityAuthoring("Laundry", EntityPlacementKind.Volume, "Laundry Folding Trigger",
        Description = "Allows players to fold dry laundry placed inside this volume. Add table geometry separately or use the Folding Table prefab.")]
    public sealed class FoldingTriggerMarker : EntityMarker, IBoundedEntityMarker
    {
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder) =>
            OperationSignalContracts.Processing(builder);
    }
}
