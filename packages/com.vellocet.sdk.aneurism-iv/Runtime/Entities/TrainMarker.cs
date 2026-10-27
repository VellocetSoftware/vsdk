using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_train")]
    [EntityAuthoring("Movement", EntityPlacementKind.Point, "Train")]
    public sealed class TrainMarker : PrefabEntityMarker
    {
        [SerializeField, Tooltip("Use native automatic departures. Disable to author station dispatch timing in VSig.")]
        private bool automaticDepartures = true;
        public bool AutomaticDepartures => automaticDepartures;

        public enum Model { Train, Metro, Genova }
        [SerializeField] private Model model;
        [SerializeField, Tooltip("Stop positions relative to this entity.")] private Vector3[] stops = { Vector3.zero, new Vector3(0, 0, 40) };
        [SerializeField, Min(.01f)] private float speed = 16;
        [SerializeField, Min(0)] private float departureDelay = 4;
        [SerializeField, Min(0)] private float stopTime = 8;
        public Model Variant => model;
        public Vector3[] Stops => stops;
        public float Speed => speed;
        public float DepartureDelay => departureDelay;
        public float StopTime => stopTime;
        public override string ArchetypeId => "aniv.train." + model.ToString().ToLowerInvariant();
        public override string[] Inputs => new[] { "Depart" };
        public override string[] Outputs => new[] { "OnDepart", "OnArrive" };
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("about_to_depart", EntitySignalValueType.None);
            builder.AddProperty("is_moving", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("stop_index", EntitySignalValueType.Int, true, false);
            builder.AddProperty("phase", EntitySignalValueType.String, true, false);
            builder.AddInput("Depart", EntitySignalValueType.None);
            builder.AddOutput("OnDepart", EntitySignalValueType.None);
            builder.AddOutput("OnArrive", EntitySignalValueType.None);
        }
    }
}
