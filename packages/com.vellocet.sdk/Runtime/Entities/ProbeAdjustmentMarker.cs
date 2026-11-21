using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("env_probe_adjustment")]
    [EntityAuthoring("Environment", EntityPlacementKind.Volume, "Probe Adjustment Volume")]
    public sealed class ProbeAdjustmentMarker : EntityMarker, ICenteredBoundsEntityMarker
    {
        public Vector3 BoundsSize => new(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z));
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
