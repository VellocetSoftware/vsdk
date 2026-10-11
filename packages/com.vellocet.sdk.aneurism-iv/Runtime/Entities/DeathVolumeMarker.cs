using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_aniv_death")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Volume, "Death Volume")]
    public sealed class DeathVolumeMarker : EntityMarker, IBoundedEntityMarker
    {
        [SerializeField] private bool destroyItems;
        public bool DestroyItems => destroyItems;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
