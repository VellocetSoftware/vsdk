using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_security_camera")]
    [EntityAuthoring("Security", EntityPlacementKind.Point, "Security Camera")]
    public sealed class SecurityCameraMarker : WaypointPrefabMarker
    {
        [SerializeField] private OperationVolumeMarker visionVolume;
        [SerializeField] private EntityMarker viewPoint;
        public OperationVolumeMarker VisionVolume => visionVolume;
        public EntityMarker ViewPoint => viewPoint;

        [SerializeField] private bool cortexTrespassZone;
        public bool CortexTrespassZone => cortexTrespassZone;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.security-camera";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddInput("enable", EntitySignalValueType.None);
            builder.AddInput("disable", EntitySignalValueType.None);
            builder.AddInput("toggle", EntitySignalValueType.None);
            builder.AddOutput("enabled", EntitySignalValueType.None);
            builder.AddOutput("disabled", EntitySignalValueType.None);
            builder.AddInput("upgrade", EntitySignalValueType.None);
            builder.AddInput("clear", EntitySignalValueType.None);
            builder.AddOutput("upgraded", EntitySignalValueType.None);
            builder.AddOutput("cleared", EntitySignalValueType.None);
            builder.AddOutput("suspect_detected", EntitySignalValueType.None);
            builder.AddOutput("alarm_started", EntitySignalValueType.None);
            builder.AddOutput("alarm_stopped", EntitySignalValueType.None);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("is_clear", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("is_upgraded", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("is_alarming", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("has_target", EntitySignalValueType.Bool, true, false);
            GameOperationContracts.Utility(builder);
        }
    }
}
