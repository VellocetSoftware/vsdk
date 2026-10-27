using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_theft")]
    [EntityAuthoring("Industry / Resources", EntityPlacementKind.Point, "Theft Zone")]
    public sealed class TheftZoneMarker : ContactOperationMarker
    {
        public enum Material { Metal, WarheadParts }
        [SerializeField] private Material material;
        public Material Variant => material;
        public override string ArchetypeId => "aniv.theft-zone." + material.ToString().ToLowerInvariant();
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("theft_detected", EntitySignalValueType.String);
        }
    }
}
