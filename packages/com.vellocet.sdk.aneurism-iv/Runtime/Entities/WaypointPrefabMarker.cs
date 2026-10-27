using UI.HUD;
using UnityEngine;

namespace Vellocet.Sdk.Aneurism.Entities
{
    public abstract class WaypointPrefabMarker : ContactOperationMarker, IWaypointAuthoring
    {
        [SerializeField] private bool overrideWaypoint;
        [SerializeField] private bool waypointEnabled = true;
        [SerializeField] private PointOfInterest pointOfInterest;
        [SerializeField] private string label;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private Sprite icon;
        [SerializeField] private bool toggleable = true;
        [SerializeField] private bool registered = true;
        public bool OverrideWaypoint => overrideWaypoint;
        public bool WaypointEnabled => waypointEnabled;
        public PointOfInterest PointOfInterest => pointOfInterest;
        public string Label => label;
        public Color Color => color;
        public Sprite Icon => icon;
        public bool Toggleable => toggleable;
        public bool Registered => registered;
    }
}
