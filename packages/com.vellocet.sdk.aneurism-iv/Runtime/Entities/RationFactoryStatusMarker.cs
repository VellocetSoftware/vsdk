using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("env_ration_status")]
    [EntityAuthoring("Food / Rations", EntityPlacementKind.Point, "Ration Factory Status", Description = "Optional status sign using ration_sign_02. Link its Factory field.")]
    public sealed class RationFactoryStatusMarker : RationFactoryPanelMarker
    {
        public override Content DisplayContent => Content.Status;
        public override string ArchetypeId => "aniv.ration-factory-status";
    }
}
