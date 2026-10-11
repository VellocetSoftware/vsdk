using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_bulletin_board")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Bulletin Board")]
    public sealed class BulletinBoardMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.bulletin-board";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
