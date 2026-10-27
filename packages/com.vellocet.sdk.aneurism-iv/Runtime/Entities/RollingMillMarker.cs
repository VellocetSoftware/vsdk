using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_rolling_mill")]
    [EntityAuthoring("Industry", EntityPlacementKind.Point, "Rolling Mill")]
    public sealed class RollingMillMarker : PoweredMachineMarker
    {
        [SerializeField, Min(0)] private float drainScale = 0.05f;
        public override float DrainScale => drainScale;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.rolling-mill";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            base.ConfigureSignalContract(builder);
            builder.AddOutput("completed", EntitySignalValueType.None);
        }
    }
}
