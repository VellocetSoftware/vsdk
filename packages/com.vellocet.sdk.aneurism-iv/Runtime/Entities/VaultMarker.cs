using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_vault")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Vault")]
    public sealed class VaultMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.vault";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
