using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Vellocet.Sdk.Audio;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Doors;
using Vertex.Contracts;
using Vertex.Unity.Editor;

namespace Vellocet.Sdk.Editor.Entities
{
    internal static class DoorSoundAuthoring
    {
        internal static DoorSoundReference Reference(UnityEngine.Object source)
        {
            if (source is AddonAudioCue cue)
                return new DoorSoundReference { Cue = cue };
            var audio = VertexAudioEvents.For(source);
            if (audio == null)
                throw new InvalidDataException("Choose a game audio event or add-on audio cue for this door sound.");
            return new DoorSoundReference { Runtime = audio.Runtime, Bank = audio.Bank, Event = audio.Event, Duration = (float)audio.Duration };
        }
        internal static bool VisibleForType(DoorMarker door, string id)
        {
            if (id is "move_direction" or "lip" or "use_travel_distance" or "travel_distance")
                return door.MotionKind == DoorMotionKind.Sliding;
            if (id == "pivot")
                return door.MotionKind != DoorMotionKind.Sliding;
            return true;
        }
        internal static void Conditions(EntityPropertyDefinition field)
        {
            string property = null;
            object value = true;
            switch (field.Id)
            {
                case "duration":
                    property = "use_duration";
                    break;
                case "speed":
                    property = "use_duration";
                    value = false;
                    break;
                case "travel_distance":
                    property = "use_travel_distance";
                    break;
                case "occlusion_padding":
                    property = "use_occlusion_portal";
                    break;
            }
            if (property != null)
                field.VisibleWhen = new[] { new EntityWarningCondition { Property = property, Values = new[] { value } } };
        }
    }
}
