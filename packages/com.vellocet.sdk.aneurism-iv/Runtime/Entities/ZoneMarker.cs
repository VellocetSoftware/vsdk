using Vellocet.Stereoform;
using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_location")]
    [EntityAuthoring("Triggers", EntityPlacementKind.Volume, "Location and Soundscape")]
    public sealed class ZoneMarker : EntityMarker, IBoundedEntityMarker, IEntityImportHierarchy
    {
        [SerializeField] private string locationName;
        [SerializeField] private bool showTime = true;
        [SerializeField] private AudioEventReference soundscape;
        [Header("Room acoustics")]
        [SerializeField] private bool overrideRoomAcoustics;
        [SerializeField, Range(0, 1)] private float roomSize = .5f;
        [SerializeField] private int roomPriority;
        [SerializeField, Min(0)] private float roomTransitionSeconds = 1;
        public string LocationName => locationName;
        public bool ShowTime => showTime;
        public AudioEventReference Soundscape => soundscape;
        public bool OverrideRoomAcoustics => overrideRoomAcoustics;
        public float RoomSize => roomSize;
        public int RoomPriority => roomPriority;
        public float RoomTransitionSeconds => roomTransitionSeconds;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
