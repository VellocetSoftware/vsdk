using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_paystation")]
    [EntityAuthoring("Commerce", EntityPlacementKind.Point, "Paystation")]
    public sealed class PaystationMarker : ContactOperationMarker
    {
        public enum MenuKind { Standard, ItemStand }
        [SerializeField] private PropertyMarker property;
        [SerializeField] private MenuKind menu;
        [SerializeField] private bool injectIntoEconomy = true;
        [SerializeField] private bool nonOwnersCanOpen = true;
        [SerializeField] private bool triggerEnter = true;
        [SerializeField] private bool allowDeposit = true;
        [SerializeField] private bool allowTheft = true;
        public PropertyMarker Property => property;
        public MenuKind Menu => menu;
        public bool InjectIntoEconomy => injectIntoEconomy;
        public bool NonOwnersCanOpen => nonOwnersCanOpen;
        public bool TriggerEnter => triggerEnter;
        public bool AllowDeposit => allowDeposit;
        public bool AllowTheft => allowTheft;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.paystation";
        public override string[] Inputs => new[] { "Ring" };
        public override string[] Outputs => new[] { "OnRing" };

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddInput("purchase", EntitySignalValueType.String);
            builder.AddInput("collect", EntitySignalValueType.None);
            builder.AddOutput("purchased", EntitySignalValueType.String);
            builder.AddOutput("stock_added", EntitySignalValueType.String);
            builder.AddOutput("collected", EntitySignalValueType.Int);
            builder.AddOutput("deposited", EntitySignalValueType.Int);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("item_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("allows_deposit", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("allows_theft", EntitySignalValueType.Bool, true, false);
            builder.AddInput("Ring", EntitySignalValueType.None);
            builder.AddOutput("OnRing", EntitySignalValueType.None);
        }
    }
}
