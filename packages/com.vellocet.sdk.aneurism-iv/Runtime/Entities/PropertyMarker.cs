using Vellocet.Stereoform;
using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("info_property")]
    [EntityAuthoring("Map setup", EntityPlacementKind.Logic, "Property")]
    public sealed class PropertyMarker : EntityMarker, IEntityImportGroup
    {
        public enum Category { Residential, Commercial, Industrial, Other }
        [SerializeField] private string propertyName = "Unknown";
        [SerializeField] private Category category;
        [SerializeField] private bool rentable = true;
        [SerializeField] private bool startLocked = true;
        [SerializeField, Min(0)] private int price;
        [SerializeField, Tooltip("The entity/group containing this property's doors. Defaults to this property.")]
        private EntityMarker doorRoot;
        [SerializeField] private PropertyUpgradeMarker[] upgrades = Array.Empty<PropertyUpgradeMarker>();
        [SerializeField] private AudioEventReference buildSound;
        [SerializeField, Tooltip("Optional entity with an authored AudioSource for upgrade sounds.")]
        private EntityMarker buildSoundEmitter;
        public string PropertyName => propertyName;
        public Category PropertyCategory => category;
        public bool Rentable => rentable;
        public bool StartLocked => startLocked;
        public int Price => price;
        public EntityMarker DoorRoot => doorRoot;
        public PropertyUpgradeMarker[] Upgrades => upgrades;
        public AudioEventReference BuildSound => buildSound;
        public EntityMarker BuildSoundEmitter => buildSoundEmitter;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("upgrade_purchased", EntitySignalValueType.String);
            builder.AddOutput("upgrade_installed", EntitySignalValueType.String);
            builder.AddOutput("upgrade_uninstalled", EntitySignalValueType.String);
            builder.AddInput("rent", EntitySignalValueType.None);
            builder.AddInput("renew", EntitySignalValueType.None);
            builder.AddInput("sell", EntitySignalValueType.None);
            builder.AddOutput("rented", EntitySignalValueType.None);
            builder.AddOutput("renewed", EntitySignalValueType.None);
            builder.AddOutput("sold", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("is_rentable", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("price", EntitySignalValueType.Int, true, false);
        }
    }
}
