using System;
using System.Globalization;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_aniv_item")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Game Item")]
    public sealed class ItemMarker : PrefabEntityMarker
    {
        [SerializeField] private GameItem item = GameItem.Screwdriver;
        // The two legacy debug cubes intentionally use server-owned physics.
        [SerializeField, HideInInspector] private bool legacyServerAuthority;
        public bool LegacyServerAuthority => legacyServerAuthority;
        public GameItem Item => item;
        public override string ArchetypeId => "aniv.item." + ((int)item).ToString(CultureInfo.InvariantCulture);
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
