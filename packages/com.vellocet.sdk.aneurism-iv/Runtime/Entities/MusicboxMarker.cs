using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_musicbox")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Musicbox")]
    public sealed class MusicboxMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.musicbox";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
