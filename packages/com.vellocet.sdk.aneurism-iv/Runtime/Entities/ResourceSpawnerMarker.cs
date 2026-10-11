using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("info_resource_spawner")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Resource Spawner")]
    public sealed class ResourceSpawnerMarker : PrefabEntityMarker
    {
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
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
