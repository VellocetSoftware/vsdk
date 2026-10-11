using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_paystation")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Paystation")]
    public sealed class PaystationMarker : PrefabEntityMarker
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
        public override string ArchetypeId => "aniv.paystation";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
