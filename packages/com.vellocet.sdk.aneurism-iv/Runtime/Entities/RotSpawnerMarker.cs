using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("info_rot_spawner")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Rot Spawner")]
    public sealed class RotSpawnerMarker : EntityMarker, IEntityImportGroup
    {
        [SerializeField] private bool enableServerScan = true;
        [SerializeField, HideInInspector] private int legacyLayerMask;
        public bool EnableServerScan => enableServerScan;
        public LayerMask Layers => legacyLayerMask;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
