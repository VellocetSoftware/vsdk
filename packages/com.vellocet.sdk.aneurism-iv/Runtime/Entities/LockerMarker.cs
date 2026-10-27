using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_locker")]
    [EntityAuthoring("Services", EntityPlacementKind.Point, "Locker")]
    public sealed class LockerMarker : WaypointPrefabMarker
    {
        public enum Kind { Locker, ScumStash }
        [SerializeField] private Kind kind;
        public Kind Variant => kind;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.locker." + kind.ToString().ToLowerInvariant();
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddInput("withdraw", EntitySignalValueType.String);
            builder.AddOutput("deposited", EntitySignalValueType.String);
            builder.AddOutput("withdrawn", EntitySignalValueType.String);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("capacity", EntitySignalValueType.Int, true, false);
            builder.AddProperty("is_openable", EntitySignalValueType.Bool, true, false);
        }
    }
}
