using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_pharma_machine")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Pharmaceutical Machine")]
    public sealed class PharmaMachineMarker : PrefabEntityMarker
    {
        public enum Kind { AnalepticalCompounder, AntiradiativeEncapsulator, HemobaseSeparator, LucidialTonicAmpoules, ParaceuticalReagentTank }
        [SerializeField] private Kind kind;
        public Kind Variant => kind;
        public override string ArchetypeId => "aniv.pharma-machine." + kind.ToString().ToLowerInvariant();
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
