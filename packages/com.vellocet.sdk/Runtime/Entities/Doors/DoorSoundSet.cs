using System;
using System.IO;
using UnityEngine;
using Vellocet.Sdk.Audio;

namespace Vellocet.Sdk.Entities.Doors
{
    public enum DoorSoundAction
    {
        Interact, Opening, Moving, Opened, Closing, Closed, Lock, Unlock, LockedAttempt, Break
    }

    [Serializable]
    public struct DoorSoundReference
    {
        public string Runtime;
        public string Bank;
        public string Event;
        public float Duration;
        public AddonAudioCue Cue;
        public void ValidateSound()
        {
            if (!float.IsFinite(Duration) || Duration < 0 || Runtime?.Length > 128 || Bank?.Length > 1024 || Event?.Length > 1024 ||
                !IsEmpty && !Cue && string.IsNullOrWhiteSpace(Runtime))
                throw new InvalidDataException("Invalid door sound reference.");
        }
        public bool IsEmpty => !Cue && string.IsNullOrWhiteSpace(Event);
    }

    [Serializable]
    public struct DoorSoundOverride
    {
        public DoorSoundAction Action;
        public DoorSoundReference Sound;
    }

    [Vertex.Contracts.CatalogAssetRole("door-sounds")]
    [CreateAssetMenu(menuName = "Vellocet/SDK/Doors/Sound set")]
    public sealed class DoorSoundSet : ScriptableObject
    {
        [Serializable]
        public struct ActionSounds
        {
            public DoorSoundAction Action;
            public DoorSoundReference[] Sounds;
        }
        [SerializeField] private ActionSounds[] actions = Array.Empty<ActionSounds>();
        public void ValidateSounds()
        {
            var mask = 0;
            foreach (var entry in actions)
            {
                if (!Enum.IsDefined(typeof(DoorSoundAction), entry.Action) || (mask & (1 << (int)entry.Action)) != 0 || entry.Sounds?.Length > 128)
                    throw new InvalidDataException("Duplicate or invalid door sound action: " + name);
                mask |= 1 << (int)entry.Action;
                if (entry.Sounds != null)
                    foreach (var sound in entry.Sounds)
                        sound.ValidateSound();
            }
        }
        public DoorSoundReference Select(DoorSoundAction action)
        {
            foreach (var entry in actions)
                if (entry.Action == action && entry.Sounds is { Length: > 0 })
                    return entry.Sounds[UnityEngine.Random.Range(0, entry.Sounds.Length)];
            return default;
        }
        public bool Has(DoorSoundAction action)
        {
            foreach (var entry in actions)
                if (entry.Action == action && entry.Sounds is { Length: > 0 })
                    return true;
            return false;
        }
    }
}
