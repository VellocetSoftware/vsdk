using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_telescreen")]
    [EntityAuthoring("Information", EntityPlacementKind.Point, "Telescreen")]
    public sealed class TelescreenMarker : PrefabEntityMarker
    {
        public enum Kind { Telescreen1, Telescreen3, Telescreen4 }
        [SerializeField] private Kind kind;
        public Kind Variant => kind;
        public override string ArchetypeId => "aniv.telescreen." + kind.ToString().ToLowerInvariant();
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            GameOperationContracts.Utility(builder);
        }
    }
}
