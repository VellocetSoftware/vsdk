using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_security_camera")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Security Camera")]
    public sealed class SecurityCameraMarker : WaypointPrefabMarker
    {
        [SerializeField] private bool cortexTrespassZone;
        public bool CortexTrespassZone => cortexTrespassZone;
        public override string ArchetypeId => "aniv.security-camera";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
