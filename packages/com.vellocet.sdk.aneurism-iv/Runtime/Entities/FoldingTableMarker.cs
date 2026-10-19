using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_folding_table")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Folding Table", AvatarModel = "asset:aniv.base/985e817d6863749b8b2ca7710a06ecae.5716299378049042518")]
    public sealed class FoldingTableMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.folding-table";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
