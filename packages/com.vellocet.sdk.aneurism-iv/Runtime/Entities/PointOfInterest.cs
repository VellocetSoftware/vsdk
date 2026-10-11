using UnityEngine;

namespace UI.HUD
{
    [CreateAssetMenu(fileName = "New Point Of Interest", menuName = "AneurismIV/Point Of Interest")]
    public class PointOfInterest : ScriptableObject
    {
        public new string name;
        public Color color;
        public Sprite icon;
    }
}