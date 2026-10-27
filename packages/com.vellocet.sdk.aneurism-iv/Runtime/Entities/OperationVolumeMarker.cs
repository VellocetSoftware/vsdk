using System;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_contact")]
    [EntityAuthoring("Advanced / Geometry", EntityPlacementKind.Volume, "Contact Volume")]
    public sealed class OperationVolumeMarker : EntityMarker, IBoundedEntityMarker
    {
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
