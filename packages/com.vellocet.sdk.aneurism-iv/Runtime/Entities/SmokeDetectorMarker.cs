using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_smoke_detector")]
    [EntityAuthoring("Security", EntityPlacementKind.Point, "Smoke Detector")]
    public sealed class SmokeDetectorMarker : PrefabEntityMarker
    {
        public override string ArchetypeId => "aniv.smoke-detector";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            GameOperationContracts.Utility(builder);
        }
    }
}
