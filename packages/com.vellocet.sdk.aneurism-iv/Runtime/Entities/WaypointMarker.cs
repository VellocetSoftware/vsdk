using System;
using UI.HUD;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("info_aniv_waypoint")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Point, "Waypoint")]
    public sealed class WaypointMarker : EntityMarker, IWaypointAuthoring
    {
        [SerializeField] private PointOfInterest pointOfInterest;
        [SerializeField] private string label;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private Sprite icon;
        [SerializeField] private bool toggleable = true;
        [SerializeField] private bool registered = true;
        public PointOfInterest PointOfInterest => pointOfInterest;
        public string Label => label;
        public Color Color => color;
        public Sprite Icon => icon;
        public bool Toggleable => toggleable;
        public bool Registered => registered;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
