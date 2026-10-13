using UnityEngine;

namespace Entities.MapObjects
{
    [CreateAssetMenu(fileName = "New LightStyle", menuName = "AneurismIV/LightStyle")]
    public class LightStyle : ScriptableObject
    {
        [SerializeField] private AnimationCurve intensityCurve;

        public float EvaluateIntensity(float time)
        {
            return intensityCurve.Evaluate(time);
        }
    }
}