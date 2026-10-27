using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("logic_item_spawner")]
    [EntityAuthoring("Items", EntityPlacementKind.Volume, "Item Spawner")]
    public sealed class ItemSpawnerMarker : EntityMarker, IBoundedEntityMarker
    {
        [SerializeField, Tooltip("Registered game item to spawn. Use VSig for batches, delays and cooldowns.")]
        private string item = GameItem.DirtyLaundry;
        [SerializeField, Range(1, 64), Tooltip("Maximum living items owned by this spawner. Removed items free capacity.")]
        private int capacity = 8;
        public string Item => item;
        public int Capacity => capacity;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("spawn", EntitySignalValueType.None);
            builder.AddInput("clear", EntitySignalValueType.None);
            builder.AddOutput("spawned", EntitySignalValueType.None);
            builder.AddOutput("cleared", EntitySignalValueType.None);
            builder.AddOutput("failed", EntitySignalValueType.String);
            builder.AddOutput("capacity_reached", EntitySignalValueType.None);
            builder.AddProperty("active_count", EntitySignalValueType.Int, readable: true, writable: false);
        }
    }
}
