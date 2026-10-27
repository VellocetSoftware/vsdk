using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_auto_turret")]
    [EntityAuthoring("Security", EntityPlacementKind.Point, "Automatic Turret")]
    public sealed class AutoTurretMarker : ContactOperationMarker
    {
        [SerializeField] private EntityMarker aimPivot;
        [SerializeField] private EntityMarker shotPoint;
        public EntityMarker AimPivot => aimPivot;
        public EntityMarker ShotPoint => shotPoint;

        [SerializeField] private float inactivityResetTime = 10f;
        [SerializeField] private float proximityScanInterval = 0.25f;
        [SerializeField] private float visionWidth = 2f;
        [SerializeField] private float visionHeight = 12f;
        [SerializeField] private float visionDepth = 4.5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float verticalLimit = 90f;
        [SerializeField] private float horizontalLimit = 90f;
        [SerializeField] private float resetTimeScaling = 5f;
        [SerializeField] private float fireSpeed = 10f;
        [SerializeField] private float bulletDamage = 20f;
        [SerializeField] private float accuracy = 80f;
        [Tooltip("Fate attribute mask: Cortex 1, Antagonist 2, Dealer 4, Psychopath 8, Cleaner 16, Infection Resistance 32.")]
        [SerializeField] private int targetedFates = 1;
        [SerializeField] private bool whitelist = true;
        [SerializeField] private bool targetLimits;
        [SerializeField] private SecurityCameraMarker securityCamera;
        public float InactivityResetTime => inactivityResetTime;
        public float ProximityScanInterval => proximityScanInterval;
        public float VisionWidth => visionWidth;
        public float VisionHeight => visionHeight;
        public float VisionDepth => visionDepth;
        public float RotationSpeed => rotationSpeed;
        public float VerticalLimit => verticalLimit;
        public float HorizontalLimit => horizontalLimit;
        public float ResetTimeScaling => resetTimeScaling;
        public float FireSpeed => fireSpeed;
        public float BulletDamage => bulletDamage;
        public float Accuracy => accuracy;
        public int TargetedFates => targetedFates;
        public bool Whitelist => whitelist;
        public bool TargetLimits => targetLimits;
        public SecurityCameraMarker SecurityCamera => securityCamera;
        public override string ArchetypeId => "aniv.auto-turret";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("enable", EntitySignalValueType.None);
            builder.AddInput("disable", EntitySignalValueType.None);
            builder.AddInput("toggle", EntitySignalValueType.None);
            builder.AddOutput("enabled", EntitySignalValueType.None);
            builder.AddOutput("disabled", EntitySignalValueType.None);
            builder.AddOutput("target_changed", EntitySignalValueType.Bool);
            builder.AddOutput("fired", EntitySignalValueType.None);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("has_target", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("target_count", EntitySignalValueType.Int, true, false);
        }
    }
}
