using Vellocet.Stereoform;
using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("info_aniv_property")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Property")]
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
        [SerializeField, HideInInspector] private string legacyPersistenceId;
        public string PropertyName => propertyName;
        public Category PropertyCategory => category;
        public bool Rentable => rentable;
        public bool StartLocked => startLocked;
        public int Price => price;
        public EntityMarker DoorRoot => doorRoot;
        public PropertyUpgradeMarker[] Upgrades => upgrades;
        public AudioEventReference BuildSound => buildSound;
        public EntityMarker BuildSoundEmitter => buildSoundEmitter;
        public string LegacyPersistenceId => legacyPersistenceId;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
