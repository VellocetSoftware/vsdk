using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_evil_one")]
    [EntityAuthoring("Environment / Rot", EntityPlacementKind.Point, "Evil One")]
    public sealed class EvilOneMarker : ContactOperationMarker
    {
        [SerializeField, Tooltip("Show the board artwork. Disable when placing it separately with env_decal.")]
        private bool useBuiltInDecal = true;
        public bool UseBuiltInDecal => useBuiltInDecal;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.evil-one";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddInput("cancel_game", EntitySignalValueType.None);
            builder.AddOutput("started", EntitySignalValueType.None);
            builder.AddOutput("completed", EntitySignalValueType.None);
            builder.AddOutput("cancelled", EntitySignalValueType.None);
            builder.AddProperty("phase", EntitySignalValueType.String, true, false);
            builder.AddProperty("player_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("pot", EntitySignalValueType.Int, true, false);
        }
    }
}
