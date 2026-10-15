using Vellocet.Stereoform;
using System;
using Entities.MapObjects;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_aniv_door")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Door")]
    public sealed class AnivDoorMarker : PrefabEntityMarker
    {
        public enum Style { Door, StorefrontGate, Hatch, MaintenanceVent }
        public enum Movement { Rotating, Sliding, Scaling }
        public enum Direction { Left = -1, Right = 1, Forward = 2, Backward = -2, Up = 3, Down = -3 }
        public enum Axis { X, Y, Z }

        [Header("Appearance")]
        [SerializeField] private Style style;
        [SerializeField] private DoorModel model;
        [SerializeField] private string doorName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Color color;
        [Header("Movement")]
        [SerializeField] private Movement movement;
        [SerializeField, Min(0)] private float duration = 1;
        [SerializeField] private bool matchMoveSoundDuration;
        [SerializeField] private bool startOpen;
        [SerializeField, Min(0)] private float autoCloseDelay;
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [SerializeField] private float rotationAmount = 90;
        [SerializeField] private bool inverted;
        [SerializeField] private Direction slideDirection = Direction.Left;
        [SerializeField] private float slideAmount = 1;
        [SerializeField] private Axis scaleAxis;
        [SerializeField] private float targetScale;
        [Header("Access")]
        [SerializeField] private bool breakable = true;
        [SerializeField, Tooltip("Offer the standard Interact and Breach actions. Lease and standalone-lock menus are configured separately.")]
        private bool standardInteractions = true;
        [SerializeField] private bool allowStandaloneLocking;
        [SerializeField] private bool startLocked;
        [SerializeField, Tooltip("Fate attribute flags allowed to operate this standalone lock.")]
        private int allowedFateAttributes;
        [Header("Audio")]
        [SerializeField] private AudioEventReference lockSound;
        [SerializeField] private AudioEventReference unlockSound;
        [SerializeField] private AudioEventReference[] breakSounds = Array.Empty<AudioEventReference>();
        [SerializeField, HideInInspector] private LocalizedText localizedName;
        [SerializeField, HideInInspector] private LocalizedText localizedDescription;

        [Serializable]
        public struct LocalizedText
        {
            public string table;
            public long keyId;
            public string key;
            public int fallback;
            public bool waitForCompletion;
        }

        public Style DoorStyle => style;
        public DoorModel Model => model;
        public string DoorName => doorName;
        public string Description => description;
        public Color Color => color;
        public Movement Motion => movement;
        public float Duration => duration;
        public bool MatchMoveSoundDuration => matchMoveSoundDuration;
        public bool StartOpen => startOpen;
        public float AutoCloseDelay => autoCloseDelay;
        public Vector3 RotationAxis => rotationAxis;
        public float RotationAmount => rotationAmount;
        public bool Inverted => inverted;
        public Direction SlideDirection => slideDirection;
        public float SlideAmount => slideAmount;
        public Axis ScaleAxis => scaleAxis;
        public float TargetScale => targetScale;
        public bool Breakable => breakable;
        public bool StandardInteractions => standardInteractions;
        public bool AllowStandaloneLocking => allowStandaloneLocking;
        public bool StartLocked => startLocked;
        public int AllowedFateAttributes => allowedFateAttributes;
        public AudioEventReference LockSound => lockSound;
        public AudioEventReference UnlockSound => unlockSound;
        public AudioEventReference[] BreakSounds => breakSounds;
        public LocalizedText LocalizedName => localizedName;
        public LocalizedText LocalizedDescription => localizedDescription;
        public override string ArchetypeId => style switch {
            Style.Door => "aniv.door", Style.StorefrontGate => "aniv.storefront-gate",
            Style.Hatch => "aniv.hatch", Style.MaintenanceVent => "aniv.maintenance-vent",
            _ => null
        };
        public override string[] Inputs => new[] { "Open", "Close", "Toggle", "Lock", "Unlock" };
        public override string[] Outputs => new[] { "OnOpen", "OnClose" };
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddProperty("is_open", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("is_locked", EntitySignalValueType.Bool, true, false);
        }
    }
}
