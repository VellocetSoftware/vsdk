using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_workbench")]
    [EntityAuthoring("Industry", EntityPlacementKind.Point, "Workbench")]
    public sealed class WorkbenchMarker : RecipeStationMarker
    {
        public enum Kind { Standard, Scum }
        [SerializeField] private Kind kind;
        public Kind BenchKind => kind;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => kind switch {
            Kind.Standard => "aniv.workbench", Kind.Scum => "aniv.scum-workbench", _ => null
        };
    }
}
