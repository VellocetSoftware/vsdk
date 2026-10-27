using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("env_ration_instructions")]
    [EntityAuthoring("Food / Rations", EntityPlacementKind.Point, "Ration Factory Instructions", Description = "Optional instructions sign using ration_sign_01. Link its Factory field.")]
    public sealed class RationFactoryInstructionsMarker : RationFactoryPanelMarker
    {
        public override Content DisplayContent => Content.Instructions;
        public override string ArchetypeId => "aniv.ration-factory-instructions";
    }
}
