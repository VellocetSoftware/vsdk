using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_musicbox")]
    [EntityAuthoring("Environment / Effects", EntityPlacementKind.Point, "Musicbox")]
    public sealed class MusicboxMarker : PrefabEntityMarker
    {
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            foreach (var input in new[] { "turn_on", "turn_off", "next_station", "previous_station" }) builder.AddInput(input, EntitySignalValueType.None);
            builder.AddOutput("turned_on", EntitySignalValueType.None);
            builder.AddOutput("turned_off", EntitySignalValueType.None);
            builder.AddOutput("station_changed", EntitySignalValueType.Int);
            builder.AddProperty("is_on", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("station", EntitySignalValueType.Int, true, false);
            builder.AddProperty("tuning", EntitySignalValueType.Bool, true, false);
        }
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.musicbox";
    }
}
