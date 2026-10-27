using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_slop_tower")]
    [EntityAuthoring("Food / Supply", EntityPlacementKind.Point, "Slop Tower")]
    public sealed class SlopTowerMarker : ContactOperationMarker
    {
        [SerializeField] private bool automaticPouring = true;
        [SerializeField, Min(.01f)] private float pouringDuration = 480;
        [SerializeField, Min(.01f)] private float minimumDowntime = 120;
        [SerializeField, Min(.01f)] private float maximumDowntime = 240;
        public float PouringDuration => pouringDuration;
        public float MinimumDowntime => minimumDowntime;
        public float MaximumDowntime => maximumDowntime;
        public bool AutomaticPouring => automaticPouring;
        public override void ConfigureSignalContract(Vellocet.Sdk.Entities.Signals.EntitySignalContractBuilder builder)
        {
            builder.AddInput("start_pouring", Vellocet.Sdk.Entities.Signals.EntitySignalValueType.None);
            builder.AddInput("stop_pouring", Vellocet.Sdk.Entities.Signals.EntitySignalValueType.None);
            builder.AddOutput("pouring_started", Vellocet.Sdk.Entities.Signals.EntitySignalValueType.None);
            builder.AddOutput("pouring_stopped", Vellocet.Sdk.Entities.Signals.EntitySignalValueType.None);
            builder.AddProperty("is_pouring", Vellocet.Sdk.Entities.Signals.EntitySignalValueType.Bool, true, false);
        }
        public enum Kind { SlopTower, SlopMachine }
        [SerializeField] private Kind kind;
        public Kind Variant => kind;
        public override string ArchetypeId => "aniv.slop-tower." + kind.ToString().ToLowerInvariant();
    }
}
