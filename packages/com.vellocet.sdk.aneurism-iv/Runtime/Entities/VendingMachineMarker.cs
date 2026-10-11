using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_vending_machine")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Vending Machine")]
    public sealed class VendingMachineMarker : PrefabEntityMarker
    {
        public enum Kind { VendingMachine, Pharmastributor, FoodStand }
        [SerializeField] private Kind kind;
        [SerializeField] private PropertyMarker property;
        [SerializeField] private bool startFullOnFirstBoot = true;
        [SerializeField, HideInInspector] private string legacyPersistenceKey;
        public Kind Variant => kind;
        public PropertyMarker Property => property;
        public bool StartFullOnFirstBoot => startFullOnFirstBoot;
        public string LegacyPersistenceKey => legacyPersistenceKey;
        public override string ArchetypeId => "aniv.vending." + kind.ToString().ToLowerInvariant();
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
