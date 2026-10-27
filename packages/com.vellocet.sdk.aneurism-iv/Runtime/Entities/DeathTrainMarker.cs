using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_death_train")]
    [EntityAuthoring("Movement", EntityPlacementKind.Point, "Death Train")]
    public sealed class DeathTrainMarker : PrefabEntityMarker
    {
        [SerializeField, Tooltip("Destination relative to this entity, in world axes.")]
        private Vector3 endOffset = new(0, 0, 100);
        [SerializeField] private bool pingPong;
        [SerializeField] private bool useHornPosition;
        [SerializeField, Tooltip("Horn trigger relative to this entity, in world axes.")]
        private Vector3 hornOffset;
        [SerializeField, Min(0)] private float hornRadius = 2;
        [SerializeField] private bool automaticPasses = true;
        [SerializeField, Min(.01f)] private float minimumInterval = 45;
        [SerializeField, Min(.01f)] private float maximumInterval = 120;
        public float MinimumInterval => minimumInterval;
        public float MaximumInterval => maximumInterval;
        public bool AutomaticPasses => automaticPasses;
        public Vector3 EndOffset => endOffset;
        public bool PingPong => pingPong;
        public bool UseHornPosition => useHornPosition;
        public Vector3 HornOffset => hornOffset;
        public float HornRadius => hornRadius;
        public override string ArchetypeId => "aniv.death-train";
        public override string[] Inputs => new[] { "Horn" };
        public override string[] Outputs => new[] { "OnDepart", "OnArrive" };
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("Horn", EntitySignalValueType.None);
            builder.AddInput("depart", EntitySignalValueType.None);
            builder.AddProperty("can_depart", EntitySignalValueType.Bool, true, false);
            builder.AddOutput("cycle_completed", EntitySignalValueType.None);
            builder.AddOutput("OnDepart", EntitySignalValueType.None);
            builder.AddOutput("OnArrive", EntitySignalValueType.None);
        }
    }
}
