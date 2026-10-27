using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("logic_rock_spawner")]
    [EntityAuthoring("Industry / Resources", EntityPlacementKind.Point, "Rock Spawner", Description = "Independent rock spawner. Optional destinations select output points; no camp doors are required.")]
    public sealed class RockSpawnerMarker : PrefabEntityMarker
    {
        [SerializeField, Tooltip("Optional spawn destinations. Empty spawns at this entity.")] private EntityMarker[] spawnPoints = Array.Empty<EntityMarker>();
        [SerializeField] private bool automaticSpawning = true;
        [SerializeField] private bool startDisabled;
        [SerializeField, Min(.05f)] private float minimumInterval = 15;
        [SerializeField, Min(.05f)] private float maximumInterval = 30;
        [SerializeField, Range(1, 64)] private int capacity = 8;
        [SerializeField, Min(0)] private float spawnRadius = 2.5f;
        [SerializeField, Min(.05f)] private float rockLifetime = 30;
        [SerializeField, HideInInspector] private string publicationSource = "";
        public EntityMarker[] SpawnPoints => spawnPoints;
        public bool AutomaticSpawning => automaticSpawning;
        public bool StartDisabled => startDisabled;
        public float MinimumInterval => minimumInterval;
        public float MaximumInterval => maximumInterval;
        public int Capacity => capacity;
        public float SpawnRadius => spawnRadius;
        public float RockLifetime => rockLifetime;
        public string PublicationSource => publicationSource;
        public override string ArchetypeId => "aniv.rock-spawner";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            foreach (var input in new[] { "spawn", "enable", "disable", "toggle" }) builder.AddInput(input, EntitySignalValueType.None);
            builder.AddInput("select_source", EntitySignalValueType.Int);
            foreach (var output in new[] { "spawned", "source_changed", "capacity_reached", "enabled", "disabled" }) builder.AddOutput(output, EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            foreach (var property in new[] { "active_count", "capacity", "source_index", "source_count" }) builder.AddProperty(property, EntitySignalValueType.Int, true, false);
            foreach (var property in new[] { "busy", "is_enabled" }) builder.AddProperty(property, EntitySignalValueType.Bool, true, false);
        }
    }
}
