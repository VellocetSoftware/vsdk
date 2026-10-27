using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_armory")]
    [EntityAuthoring("Commerce", EntityPlacementKind.Point, "Armory")]
    public sealed class ArmoryMarker : ContactOperationMarker
    {
        public enum Kind { Armory, UndergroundArmory }
        [SerializeField] private Kind kind;
        public Kind Variant => kind;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.armory." + kind.ToString().ToLowerInvariant();
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddInput("take", EntitySignalValueType.String);
            builder.AddInput("return", EntitySignalValueType.String);
            builder.AddOutput("taken", EntitySignalValueType.String);
            builder.AddOutput("returned", EntitySignalValueType.String);
            builder.AddOutput("rejected", EntitySignalValueType.String);
        }
    }
}
