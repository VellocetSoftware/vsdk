using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("env_fire")]
    [EntityAuthoring("Environment", EntityPlacementKind.Point, "Fire", Description = "Native fire with bounded size, replicated ignition state and hazard/effect control.")]
    public sealed class FireMarker : PrefabEntityMarker
    {
        [SerializeField, Range(.1f, 10f)] private float scale = 1f;
        public float Scale => scale;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            OperationSignalContracts.Ignition(builder);
        }
        public override string ArchetypeId => "aniv.fire";
    }
}
