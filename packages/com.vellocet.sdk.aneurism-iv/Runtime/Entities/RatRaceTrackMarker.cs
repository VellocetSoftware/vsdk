using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_rat_track")]
    [EntityAuthoring("Recreation / Racing", EntityPlacementKind.Point, "Rat Track")]
    public sealed class RatRaceTrackMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.rat-track";
    }
}
