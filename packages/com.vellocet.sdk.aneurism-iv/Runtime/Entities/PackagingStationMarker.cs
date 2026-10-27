using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_packaging_station")]
    [EntityAuthoring("Industry", EntityPlacementKind.Point, "Packaging Station")]
    public sealed class PackagingStationMarker : RecipeStationMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.packaging-station";
    }
}
