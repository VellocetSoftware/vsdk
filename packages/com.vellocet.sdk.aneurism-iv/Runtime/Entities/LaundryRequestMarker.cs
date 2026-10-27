using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("logic_laundry_request")]
    [EntityAuthoring("Advanced / Processing", EntityPlacementKind.Point, "Laundry Request")]
    public sealed class LaundryRequestMarker : EntityMarker
    {
        [SerializeField, Tooltip("Optional delivery point. Character eligibility and batch rules belong to the game.")]
        private EntityMarker outputTarget;
        public EntityMarker OutputTarget => outputTarget;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("request", EntitySignalValueType.None);
            builder.AddOutput("completed", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("busy", EntitySignalValueType.Bool, readable: true, writable: false);
        }
    }
}
