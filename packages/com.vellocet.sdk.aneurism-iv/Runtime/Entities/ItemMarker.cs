using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("item_pickup")]
    [EntityAuthoring("Items", EntityPlacementKind.Point, "Game Item")]
    public sealed class ItemMarker : PrefabEntityMarker
    {
        [SerializeField] private string item = GameItem.Screwdriver;
        public string Item => item;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.item." + item;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("effect_applied", EntitySignalValueType.String);
        }
    }
}
