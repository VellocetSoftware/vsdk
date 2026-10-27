using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_planter")]
    [EntityAuthoring("Services", EntityPlacementKind.Point, "Planter")]
    public sealed class PlanterMarker : OperationEntityMarker
    {
        public enum Kind { Tumortato, Bonecorn, SanguineGrapes }
        [SerializeField] private Kind kind;
        [SerializeField, Range(1, 3600)] private float growthSeconds = 60;
        [SerializeField, Range(1, 3600)] private float feedingSeconds = 20;
        [SerializeField, Tooltip("Optional independently authored crop visual, scaled along its local Y axis.")]
        private EntityMarker cropVisual;
        public float GrowthSeconds => growthSeconds;
        public float FeedingSeconds => feedingSeconds;
        public EntityMarker CropVisual => cropVisual;
        public Kind Variant => kind;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("harvest", EntitySignalValueType.None);
            builder.AddOutput("fed", EntitySignalValueType.None);
            builder.AddOutput("ready", EntitySignalValueType.None);
            builder.AddOutput("starved", EntitySignalValueType.None);
            builder.AddOutput("harvested", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("progress", EntitySignalValueType.Float, readable: true, writable: false);
            builder.AddProperty("hydration", EntitySignalValueType.Float, readable: true, writable: false);
            builder.AddProperty("is_ready", EntitySignalValueType.Bool, readable: true, writable: false);
        }
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.planter." + kind.ToString().ToLowerInvariant();
    }
}
