using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("trigger_aniv_zone")]
    [EntityAuthoring("Audio", EntityPlacementKind.Volume, "Location and Soundscape")]
    public sealed class ZoneMarker : EntityMarker, IBoundedEntityMarker
    {
        public enum Reverb { None, InCity, InRoomSmall, InRoomLarge, InTunnel, InUnderground }
        [SerializeField] private string locationName;
        [SerializeField] private bool showTime = true;
        [SerializeField] private AudioClip soundscape;
        [SerializeField] private bool useSnapshot;
        [SerializeField] private Reverb snapshot;
        [SerializeField, Range(0, 4)] private float transitionTime = 1;
        public string LocationName => locationName;
        public bool ShowTime => showTime;
        public AudioClip Soundscape => soundscape;
        public bool UseSnapshot => useSnapshot;
        public Reverb Snapshot => snapshot;
        public float TransitionTime => transitionTime;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
