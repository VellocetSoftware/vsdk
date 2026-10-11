using System;
using Entities.Shared;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_aniv_damage")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Volume, "Damage Volume")]
    public sealed class DamageVolumeMarker : EntityMarker, IBoundedEntityMarker
    {
        [SerializeField] private bool destroyItems;
        [SerializeField] private bool instantKill;
        [SerializeField] private bool affectSpirits = true;
        [SerializeField] private string damageName;
        [SerializeField] private float damage;
        [SerializeField] private float interval;
        [SerializeField] private float length;
        [SerializeField] private TagsWeapon damageType;
        [SerializeField] private bool refreshable;
        [SerializeField] private bool initialDamage = true;
        [Tooltip("Stable Fate IDs that are immune to this volume.")]
        [SerializeField] private string[] invulnerableFates = Array.Empty<string>();
        public bool DestroyItems => destroyItems;
        public bool InstantKill => instantKill;
        public bool AffectSpirits => affectSpirits;
        public string DamageName => damageName;
        public float Damage => damage;
        public float Interval => interval;
        public float Length => length;
        public TagsWeapon DamageType => damageType;
        public bool Refreshable => refreshable;
        public bool InitialDamage => initialDamage;
        public string[] InvulnerableFates => invulnerableFates;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
