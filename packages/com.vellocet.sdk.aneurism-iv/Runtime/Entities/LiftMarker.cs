using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_lift")]
    [EntityAuthoring("Movement", EntityPlacementKind.Point, "Lift")]
    public sealed class LiftMarker : PrefabEntityMarker
    {
        [Serializable] public struct Floor { public string name; public float height; }
        [SerializeField, Tooltip("Floor heights relative to this entity.")] private Floor[] floors = {
            new() { name = "Ground", height = 0 }, new() { name = "Upper", height = 4 }
        };
        [SerializeField, Min(0)] private int startingFloor;
        [SerializeField, Range(1, 8)] private float speed = 2;
        public Floor[] Floors => floors;
        public int StartingFloor => startingFloor;
        public float Speed => speed;
        public override string ArchetypeId => "aniv.lift";
        public override string[] Inputs => new[] { "GoToFloor" };
        public override string[] Outputs => new[] { "OnDepart", "OnArrive" };
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("GoToFloor", EntitySignalValueType.Int);
            builder.AddOutput("OnDepart", EntitySignalValueType.None);
            builder.AddOutput("OnArrive", EntitySignalValueType.None);
            builder.AddProperty("is_moving", EntitySignalValueType.Bool, true, false);
        }
    }
}
