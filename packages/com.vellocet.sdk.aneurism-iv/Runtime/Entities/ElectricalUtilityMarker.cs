using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_electrical_utility")]
    [EntityAuthoring("Infrastructure", EntityPlacementKind.Point, "Electrical Utility")]
    public sealed class ElectricalUtilityMarker : PrefabEntityMarker
    {
        public enum Model { Base, Utility1, Utility2, Utility3, Utility4 }
        [SerializeField] private Model model;
        public Model Variant => model;
        public override string ArchetypeId => model == Model.Base ? "aniv.electrical-utility" :
            "aniv.electrical-utility." + ((int)model).ToString(System.Globalization.CultureInfo.InvariantCulture);
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            GameOperationContracts.Utility(builder);
        }
    }
}
