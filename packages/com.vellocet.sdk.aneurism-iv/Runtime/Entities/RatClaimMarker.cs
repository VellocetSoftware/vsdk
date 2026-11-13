using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_rat_claim")]
    [EntityAuthoring("Recreation / Racing", EntityPlacementKind.Point, "Rat Claim")]
    public sealed class RatClaimMarker : ContactOperationMarker
    {
        public override string ArchetypeId => "aniv.rat-claim";
        public override bool SupportsInteraction => true;

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("retrieve_rat", EntitySignalValueType.None);
            builder.AddInput("claim_lost_rat", EntitySignalValueType.None);
            builder.AddInput("claim_money", EntitySignalValueType.None);
        }
    }
}
