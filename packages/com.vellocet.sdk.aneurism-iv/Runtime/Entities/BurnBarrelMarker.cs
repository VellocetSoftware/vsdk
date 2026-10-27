using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("env_burn_barrel")]
    [EntityAuthoring("Environment", EntityPlacementKind.Point, "Burn Barrel", Description = "Native burn barrel with replicated ignition state and hazard/effect control.")]
    public sealed class BurnBarrelMarker : PrefabEntityMarker
    {
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            OperationSignalContracts.Ignition(builder);
        }
        public override string ArchetypeId => "aniv.burn-barrel";
    }
}
