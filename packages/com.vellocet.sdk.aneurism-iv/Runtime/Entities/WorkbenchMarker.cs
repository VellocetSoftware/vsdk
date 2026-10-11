using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_workbench")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Workbench")]
    public sealed class WorkbenchMarker : PrefabEntityMarker
    {
        public enum Kind { Standard, Scum }
        [SerializeField] private Kind kind;
        public Kind BenchKind => kind;
        public override string ArchetypeId => kind switch {
            Kind.Standard => "aniv.workbench", Kind.Scum => "aniv.scum-workbench", _ => null
        };
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
