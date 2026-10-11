using System;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("group_aniv_entities")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Entity Group")]
    public sealed class EntityGroupMarker : EntityMarker, IEntityImportGroup
    {
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
