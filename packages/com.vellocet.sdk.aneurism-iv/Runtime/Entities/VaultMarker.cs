using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_vault")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Point, "Vault")]
    public sealed class VaultMarker : ContactOperationMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.vault";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("request_access", EntitySignalValueType.None);
            builder.AddOutput("access_changed", EntitySignalValueType.Bool);
            builder.AddOutput("trespass_detected", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("is_open", EntitySignalValueType.Bool, true, false);
        }
    }
}
