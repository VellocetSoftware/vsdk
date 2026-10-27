using UnityEngine;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_laundry_button")]
    [EntityAuthoring("Laundry", EntityPlacementKind.Point, "Laundry Button", Description = "Native laundry request button with validated player eligibility and request cooldown.")]
    public sealed class LaundryButtonMarker : ButtonMarker
    {
        [SerializeField, Tooltip("Native laundry chute receiving this button's requests.")] private LaundryChuteMarker chute;
        public LaundryChuteMarker Chute => chute;
        protected override string DefaultModel => "aniv.laundry-button";
    }
}
