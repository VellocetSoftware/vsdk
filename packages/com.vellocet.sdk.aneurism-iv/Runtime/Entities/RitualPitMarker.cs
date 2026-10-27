using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_ritual_pit")]
    [EntityAuthoring("Environment / Rot", EntityPlacementKind.Point, "Ritual Pit")]
    public sealed class RitualPitMarker : ContactOperationMarker
    {
        [SerializeField] private EntityMarker eligibilityPoint;
        [SerializeField, Range(.1f, 32f)] private float eligibilityRadius = 10f;
        public EntityMarker EligibilityPoint => eligibilityPoint;
        public float EligibilityRadius => eligibilityRadius;

        public enum Kind { Standard, Volume }
        [SerializeField] private Kind kind;
        [SerializeField] private string allowedFate = "zealot";
        public Kind Variant => kind;
        public string AllowedFate => allowedFate;
        public override string ArchetypeId => kind == Kind.Volume ? "aniv.ritual-pit.volume" : "aniv.ritual-pit";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("accepted", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddOutput("progress_changed", EntitySignalValueType.Int);
            builder.AddOutput("threshold_reached", EntitySignalValueType.None);
            builder.AddOutput("darkness_started", EntitySignalValueType.None);
            builder.AddProperty("local_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("global_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("progress", EntitySignalValueType.Float, true, false);
            builder.AddProperty("is_darkness", EntitySignalValueType.Bool, true, false);
        }
    }
}
