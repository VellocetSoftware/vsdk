using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_container")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Container")]
    public sealed class ContainerMarker : PrefabEntityMarker
    {
        public enum Kind { Chest, ScumChest, StorageBox, TrashBin, Dumpster, UndergroundLoot, UndergroundLoot2, UndergroundLoot3, UndergroundLoot4, UndergroundLoot5 }
        public enum LootKind { PrefabDefault = -1, None, Common, Underground, All }
        public enum Access { PrefabDefault, Allowed, Forbidden }
        [SerializeField] private Kind kind;
        [SerializeField] private LootKind loot = LootKind.PrefabDefault;
        [SerializeField, Min(-1), Tooltip("Maximum inventory weight; -1 uses the selected prop's default.")] private int capacity = -1;
        [SerializeField] private Access deposits;
        [SerializeField] private Access opening;
        [SerializeField] private bool audioOcclusion = true;
        public Kind ContainerKind => kind;
        public LootKind Loot => loot;
        public int Capacity => capacity;
        public Access Deposits => deposits;
        public Access Opening => opening;
        public bool AudioOcclusion => audioOcclusion;
        public override string ArchetypeId => Enum.IsDefined(typeof(Kind), kind) ? "aniv.container." + kind.ToString().ToLowerInvariant() : null;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
