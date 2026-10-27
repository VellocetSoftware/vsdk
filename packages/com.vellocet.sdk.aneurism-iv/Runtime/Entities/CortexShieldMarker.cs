using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_cortex_shield")]
    [EntityAuthoring("Security", EntityPlacementKind.Point, "Cortex Shield")]
    public sealed class CortexShieldMarker : ContactOperationMarker
    {
        [SerializeField, Tooltip("Shield dimensions before the entity transform scale.")] private Vector3 size = Vector3.one;
        [SerializeField] private bool eventBased;
        [SerializeField] private string[] exceptionFates = { "liquidator", "rogue" };
        [SerializeField] private bool wantedTrespassers;
        [SerializeField] private bool killTrespassers = true;
        public Vector3 Size => size;
        public bool EventBased => eventBased;
        public System.Collections.Generic.IReadOnlyList<string> ExceptionFates => exceptionFates;
        public bool WantedTrespassers => wantedTrespassers;
        public bool KillTrespassers => killTrespassers;
        public override string ArchetypeId => "aniv.cortex-shield";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("enable", EntitySignalValueType.None);
            builder.AddInput("disable", EntitySignalValueType.None);
            builder.AddInput("toggle", EntitySignalValueType.None);
            builder.AddOutput("enabled", EntitySignalValueType.None);
            builder.AddOutput("disabled", EntitySignalValueType.None);
            builder.AddOutput("passed", EntitySignalValueType.None);
            builder.AddOutput("trespass_detected", EntitySignalValueType.None);
            builder.AddOutput("enforcement_applied", EntitySignalValueType.None);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, false);
        }
    }
}
