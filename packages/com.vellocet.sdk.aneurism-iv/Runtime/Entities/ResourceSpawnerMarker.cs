using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("logic_resource_spawner")]
    [EntityAuthoring("Industry / Resources", EntityPlacementKind.Point, "Resource Spawner")]
    public sealed class ResourceSpawnerMarker : PrefabEntityMarker
    {
        [SerializeField] private EntityMarker outputTarget;
        [SerializeField] private bool automaticSpawning = true;
        public EntityMarker OutputTarget => outputTarget;
        public bool AutomaticSpawning => automaticSpawning;

        public enum Kind { Coal, Ore }
        [SerializeField] private Kind[] resources = { Kind.Coal, Kind.Ore };
        [SerializeField, Min(0.05f)] private float minSpawnTime = 5;
        [SerializeField, Min(0.05f)] private float maxSpawnTime = 20;
        [SerializeField, Min(0)] private float spawnRadius = 3.75f;
        public IReadOnlyList<Kind> Resources => resources;
        public float MinSpawnTime => minSpawnTime;
        public float MaxSpawnTime => maxSpawnTime;
        public float SpawnRadius => spawnRadius;
        public override string ArchetypeId => "aniv.resource-spawner";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("spawn", EntitySignalValueType.None);
            builder.AddInput("enable", EntitySignalValueType.None);
            builder.AddInput("disable", EntitySignalValueType.None);
            builder.AddInput("toggle", EntitySignalValueType.None);
            builder.AddOutput("spawned", EntitySignalValueType.Int);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddOutput("enabled", EntitySignalValueType.None);
            builder.AddOutput("disabled", EntitySignalValueType.None);
            builder.AddProperty("active_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("capacity", EntitySignalValueType.Int, true, false);
            builder.AddProperty("busy", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, false);
        }
    }
}
