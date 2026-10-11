using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_submachine")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Submachine")]
    public sealed class SubmachineMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.submachine";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
