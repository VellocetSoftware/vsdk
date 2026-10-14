using Vellocet.Stereoform;
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
        [SerializeField] private string locationName;
        [SerializeField] private bool showTime = true;
        [SerializeField] private AudioEventReference soundscape;
        public string LocationName => locationName;
        public bool ShowTime => showTime;
        public AudioEventReference Soundscape => soundscape;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
