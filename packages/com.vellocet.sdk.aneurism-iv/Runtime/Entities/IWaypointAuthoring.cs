using UI.HUD;
using UnityEngine;

namespace Vellocet.Sdk.Aneurism.Entities
{
    public interface IWaypointAuthoring
    {
        PointOfInterest PointOfInterest { get; }
        string Label { get; }
        Color Color { get; }
        Sprite Icon { get; }
        bool Toggleable { get; }
        bool Registered { get; }
    }
}
