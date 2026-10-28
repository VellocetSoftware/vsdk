using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Vellocet.Sdk.Entities
{
    [MovedFrom("Entities.MapObjects")]
    [CreateAssetMenu(fileName = "New LightStyle", menuName = "Vellocet/Light Style")]
    public class LightStyle : ScriptableObject
    {
        [SerializeField] private AnimationCurve intensityCurve;

        public float EvaluateIntensity(float time)
        {
            return intensityCurve.Evaluate(time);
        }
    }
}