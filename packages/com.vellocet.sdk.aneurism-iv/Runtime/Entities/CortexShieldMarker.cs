using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_cortex_shield")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Cortex Shield")]
    public sealed class CortexShieldMarker : PrefabEntityMarker
    {
        [SerializeField] private bool eventBased;
        [SerializeField] private string[] exceptionFates = { "liquidator", "rogue" };
        [SerializeField] private bool wantedTrespassers;
        [SerializeField] private bool killTrespassers = true;
        public bool EventBased => eventBased;
        public System.Collections.Generic.IReadOnlyList<string> ExceptionFates => exceptionFates;
        public bool WantedTrespassers => wantedTrespassers;
        public bool KillTrespassers => killTrespassers;
        public override string ArchetypeId => "aniv.cortex-shield";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
