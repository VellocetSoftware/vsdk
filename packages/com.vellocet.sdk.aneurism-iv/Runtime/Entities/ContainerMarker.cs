using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_container")]
    [EntityAuthoring("Services", EntityPlacementKind.Point, "Container")]
    public sealed class ContainerMarker : ContactOperationMarker
    {
        public enum Kind { Chest, ScumChest, StorageBox, TrashBin, Dumpster, UndergroundLoot, UndergroundLoot2, UndergroundLoot3, UndergroundLoot4, UndergroundLoot5 }
        public enum LootKind { PrefabDefault = -1, None, Common, Underground, All }
        public enum Access { PrefabDefault, Allowed, Forbidden }
        [SerializeField] private Kind kind;
        [SerializeField] private LootKind loot = LootKind.PrefabDefault;
        [SerializeField, Min(-1), Tooltip("Maximum inventory weight; -1 uses the selected prop's default.")] private int capacity = -1;
        [SerializeField] private Access deposits;
        [SerializeField] private Access opening;
        public Kind ContainerKind => kind;
        public LootKind Loot => loot;
        public int Capacity => capacity;
        public Access Deposits => deposits;
        public Access Opening => opening;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => Enum.IsDefined(typeof(Kind), kind) ? "aniv.container." + kind.ToString().ToLowerInvariant() : null;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddInput("withdraw", EntitySignalValueType.String);
            builder.AddOutput("deposited", EntitySignalValueType.String);
            builder.AddOutput("withdrawn", EntitySignalValueType.String);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("capacity", EntitySignalValueType.Int, true, false);
            builder.AddProperty("is_openable", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("weight", EntitySignalValueType.Int, true, false);
            builder.AddProperty("item_count", EntitySignalValueType.Int, true, false);
        }
    }
}
