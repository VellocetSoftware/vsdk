using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("info_aniv_property_upgrade")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Property Upgrade")]
    public sealed class PropertyUpgradeMarker : EntityMarker
    {
        [SerializeField] private string upgradeName;
        [SerializeField, Tooltip("Persistent slot key within the property. Keep this stable after publishing.")]
        private string slot;
        [SerializeField, Min(0)] private int price;
        [SerializeField] private bool allowUninstall = true;
        [SerializeField] private EntityMarker target;
        public string UpgradeName => upgradeName;
        public string Slot => slot;
        public int Price => price;
        public bool AllowUninstall => allowUninstall;
        public EntityMarker Target => target;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
