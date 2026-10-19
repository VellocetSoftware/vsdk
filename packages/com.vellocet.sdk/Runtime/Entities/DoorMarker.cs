// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.IO;
using UnityEngine;
using Vellocet.Sdk.Entities.Doors;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Entities.States;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Entities
{
    [EntityClass("func_door")]
    [EntityAuthoring("Movement", EntityPlacementKind.Volume, "Sliding Door", Description = "Slide selected brushes or models. Configure sounds and connect behavior with VSig.")]
    public class DoorMarker : MovingBrushEntityMarker<DoorMarker.Input, DoorMarker.Output>, IEntityInteractionMarker
    {
        [SerializeField, HideInInspector] private Transform body;
        public override Transform BrushTransform => body ? body : transform;
        public void SetBody(Transform value) => body = value;
        public virtual DoorMotionKind MotionKind => DoorMotionKind.Sliding;
        [field: Header("Travel")]
        [field: SerializeField]
        public bool UseTravelDistance
        {
            get; private set;
        }
        [field: SerializeField] public float TravelDistance { get; private set; } = 1;
        [field: SerializeField]
        public Vector3 Pivot
        {
            get; private set;
        }
        [field: Header("Timing")]
        [field: SerializeField]
        public bool UseDuration
        {
            get; private set;
        }
        [field: SerializeField, Min(0)] public float Duration { get; private set; } = 1;
        [field: SerializeField]
        public DoorEase OpeningEase
        {
            get; private set;
        }
        [field: SerializeField]
        public DoorEase ClosingEase
        {
            get; private set;
        }
        [field: Header("Sounds")]
        [field: SerializeField]
        public DoorSoundSet Sounds
        {
            get; private set;
        }
        [field: SerializeField]
        public bool OverrideSounds
        {
            get; private set;
        }
        [SerializeField, HideInInspector] private DoorSoundOverride[] soundOverrides = Array.Empty<DoorSoundOverride>();
        public bool TryGetSoundOverride(DoorSoundAction action, out DoorSoundReference sound)
        {
            foreach (var entry in soundOverrides)
                if (entry.Action == action)
                {
                    sound = entry.Sound;
                    return true;
                }
            sound = default;
            return false;
        }
        public void RemoveSoundOverride(DoorSoundAction action)
        {
            for (var i = 0; i < soundOverrides.Length; i++)
                if (soundOverrides[i].Action == action)
                {
                    Array.Copy(soundOverrides, i + 1, soundOverrides, i, soundOverrides.Length - i - 1);
                    Array.Resize(ref soundOverrides, soundOverrides.Length - 1);
                    return;
                }
        }
        public DoorSoundReference SelectSound(DoorSoundAction action)
        {
            if (OverrideSounds)
                foreach (var entry in soundOverrides)
                    if (entry.Action == action)
                        return entry.Sound;
            return Sounds ? Sounds.Select(action) : default;
        }
        public void SetSoundOverride(DoorSoundAction action, DoorSoundReference sound)
        {
            for (var i = 0; i < soundOverrides.Length; i++)
                if (soundOverrides[i].Action == action)
                {
                    soundOverrides[i].Sound = sound;
                    return;
                }
            Array.Resize(ref soundOverrides, soundOverrides.Length + 1);
            soundOverrides[soundOverrides.Length - 1] = new DoorSoundOverride { Action = action, Sound = sound };
        }
        [field: Header("Occlusion")]
        [field: SerializeField] public bool UseOcclusionPortal { get; private set; } = true;
        [field: SerializeField, Min(0)] public float OcclusionPadding { get; private set; } = .05f;
        public bool SupportsInteraction => true;

        public Vector3 GetLocalOpenDirection() => MoveDirection switch
        {
            LocalDirection.Up => Vector3.up,
            LocalDirection.Down => Vector3.down,
            LocalDirection.Left => Vector3.left,
            LocalDirection.Right => Vector3.right,
            LocalDirection.Front => Vector3.forward,
            _ => Vector3.back
        };

        public void ValidateDoor()
        {
            if (body && body != transform && !body.IsChildOf(transform))
                throw new InvalidDataException("Door geometry must belong to the door: " + name);
            if (Sounds)
                Sounds.ValidateSounds();
            var mask = 0;
            foreach (var entry in soundOverrides)
            {
                if (!Enum.IsDefined(typeof(DoorSoundAction), entry.Action) || (mask & (1 << (int)entry.Action)) != 0)
                    throw new InvalidDataException("Duplicate or invalid door sound override: " + name);
                mask |= 1 << (int)entry.Action;
                entry.Sound.ValidateSound();
            }
            if (!float.IsFinite(Speed) || Speed <= 0 || !float.IsFinite(Lip) ||
                !float.IsFinite(TravelDistance) || !float.IsFinite(Duration) || Duration < 0 ||
                !float.IsFinite(ResetDelay) || ResetDelay < -1 || !float.IsFinite(Damage) || Damage < 0 ||
                !float.IsFinite(DamageCooldownSeconds) || DamageCooldownSeconds <= 0 ||
                !float.IsFinite(OcclusionPadding) || OcclusionPadding < 0 || OcclusionPadding > 10 ||
                !Finite(Pivot) || !Enum.IsDefined(typeof(LocalDirection), MoveDirection) ||
                !Enum.IsDefined(typeof(DoorEase), OpeningEase) || !Enum.IsDefined(typeof(DoorEase), ClosingEase))
                throw new InvalidDataException("Invalid door motion, collision, or occlusion settings: " + name);
            if (this is RotatingDoorMarker rotating && (!Finite(rotating.RotationAxis) || rotating.RotationAxis.sqrMagnitude < .000001f || !float.IsFinite(rotating.Angle)))
                throw new InvalidDataException("A rotating door needs a finite axis and angle: " + name);
            if (this is ScalingDoorMarker scaling && (!Enum.IsDefined(typeof(ScalingDoorMarker.ScaleAxis), scaling.Axis) || !float.IsFinite(scaling.TargetScale) || scaling.TargetScale < 0))
                throw new InvalidDataException("Invalid door scale: " + name);
        }
        private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

#if UNITY_EDITOR
        public override bool ShowAuthoringProperty(string propertyName) => propertyName switch
        {
            "<MoveDirection>k__BackingField" or "<Lip>k__BackingField" or "<UseTravelDistance>k__BackingField" => MotionKind == DoorMotionKind.Sliding,
            "<TravelDistance>k__BackingField" => MotionKind == DoorMotionKind.Sliding && UseTravelDistance,
            "<Pivot>k__BackingField" => MotionKind != DoorMotionKind.Sliding,
            "<Duration>k__BackingField" => UseDuration,
            "<Speed>k__BackingField" => !UseDuration,
            "<OcclusionPadding>k__BackingField" => UseOcclusionPortal,
            _ => true
        };
#endif

        public enum Input
        {
            Open,
            Close,
            Toggle,
            Lock,
            Unlock
        }

        /// <summary>Local-space directions relative to THIS marker's transform.</summary>
        public enum LocalDirection
        {
            Up,
            Down,
            Left,
            Right,
            Front,
            Back
        }

        public enum Output
        {
            OnOpen,
            OnClose,
            Opening,
            Closing,
            Locked,
            Unlocked,
            LockedAttempt,
            Broken
        }

        [field: Header("Movement")]
        [field: Tooltip("Local direction (relative to this marker) that the door moves when opening.")]
        [field: SerializeField]
        public LocalDirection MoveDirection { get; private set; } = LocalDirection.Front;

        [field: Tooltip("Units per second the door moves between closed and open.")]
        [field: SerializeField]
        [field: Min(0.01f)]
        public float Speed { get; private set; } = 6f;

        [field: Tooltip(
            "Signed travel inset. Positive values leave this much of the door in-frame; negative values move beyond the measured door length.")]
        [field: SerializeField]
        public float Lip
        {
            get; private set;
        }

        [field: Header("Behavior")]
        [field: Tooltip("Automatically close after this many seconds once fully open. -1 = never auto close.")]
        [field: SerializeField]
        [field: Min(-1f)]
        public float ResetDelay { get; private set; } = -1f;

        [field: Header("Collision Damage")]
        [field: Tooltip(
            "Damage dealt when this moving door drives into a damageable target. Separating and tangential contacts do not deal damage. 0 disables collision damage.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float Damage
        {
            get; private set;
        }

        [field: Tooltip("Minimum seconds between collision-damage events against the same target.")]
        [field: SerializeField]
        [field: Min(0.01f)]
        public float DamageCooldownSeconds { get; private set; } = 0.25f;

        [field: Header("Start State")]
        [field: Tooltip("If true, the door starts locked and ignores open/close/toggle until unlocked.")]
        [field: SerializeField]
        public bool StartLocked
        {
            get; private set;
        }

        [field: Tooltip("If true, the door starts open.")]
        [field: SerializeField]
        public bool StartOpen
        {
            get; private set;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if(MotionKind!=DoorMotionKind.Sliding)return;
            var pos = transform.position;
            var direction=GetWorldOpenDirection();
            var rot = Quaternion.LookRotation(direction, Mathf.Abs(Vector3.Dot(direction,Vector3.up))>.99f ? Vector3.forward:Vector3.up);
            var size = HandleUtility.GetHandleSize(pos) * 0.75f;

            using (new Handles.DrawingScope(Color.orange))
            {
                Handles.ArrowHandleCap(0, pos, rot, size, EventType.Repaint);
            }
        }
#endif

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            foreach (var input in Inputs)
                builder.AddInput(input, EntitySignalValueType.None);
            foreach (var output in Outputs)
                builder.AddOutput(output, EntitySignalValueType.None);
            builder.AddState("state", nameof(DoorEntityState));
            builder.AddProperty("is_open", EntitySignalValueType.Bool, true, true);
            builder.AddProperty("is_locked", EntitySignalValueType.Bool, true, true);
        }

        /// <summary>
        ///     World-space opening direction from MoveDirection using THIS marker's local axes.
        /// </summary>
        public Vector3 GetWorldOpenDirection()
        {
            var t = transform;
            return MoveDirection switch
            {
                LocalDirection.Up => t.up,
                LocalDirection.Down => -t.up,
                LocalDirection.Left => -t.right,
                LocalDirection.Right => t.right,
                LocalDirection.Front => t.forward,
                LocalDirection.Back => -t.forward,
                _ => Vector3.zero
            };
        }
    }
}